using System.Globalization;
using CryptoRiskAnalysis.API.Interfaces;
using CryptoRiskAnalysis.API.Models;
using Microsoft.Data.Sqlite;

namespace CryptoRiskAnalysis.API.Services;

/// <summary>Small transactional batches; decimal values stored as text to preserve precision.</summary>
public sealed class SqliteHistoricalMarketDataStore : IHistoricalMarketDataStore, IDisposable
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public SqliteHistoricalMarketDataStore(string databasePath)
    {
        var path = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = false,
            DefaultTimeout = 5
        }.ToString();
    }

    public async Task SaveAsync(string assetId, MarketDataSource source,
        IReadOnlyList<DailyMarketObservation> observations, CancellationToken cancellationToken)
    {
        ValidateKey(assetId, source);
        ArgumentNullException.ThrowIfNull(observations);
        // Validate the whole batch before writing any row. Partial batches may fill gaps.
        var dates = new HashSet<DateOnly>();
        foreach (var row in observations)
        {
            if (row is null || row.Price <= 0 || row.Volume < 0 ||
                DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(row.Timestamp).UtcDateTime) != row.Date ||
                !dates.Add(row.Date))
                throw new ArgumentException("Invalid or duplicate daily observation.", nameof(observations));
        }
        await _gate.WaitAsync(cancellationToken);
        try
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO daily_market_data(asset_id, source, quote_currency, utc_day, timestamp_ms, price, volume)
                VALUES ($asset, $source, $currency, $day, $timestamp, $price, $volume)
                ON CONFLICT(asset_id, source, quote_currency, utc_day) DO UPDATE SET
                    timestamp_ms = excluded.timestamp_ms, price = excluded.price, volume = excluded.volume;
                """;
            command.Parameters.AddWithValue("$asset", assetId);
            command.Parameters.AddWithValue("$source", source.ToString());
            command.Parameters.AddWithValue("$currency", QuoteCurrency(source));
            var day = command.Parameters.Add("$day", SqliteType.Integer);
            var timestamp = command.Parameters.Add("$timestamp", SqliteType.Integer);
            var price = command.Parameters.Add("$price", SqliteType.Text);
            var volume = command.Parameters.Add("$volume", SqliteType.Text);
            foreach (var row in observations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                day.Value = row.Date.DayNumber;
                timestamp.Value = row.Timestamp;
                price.Value = row.Price.ToString(CultureInfo.InvariantCulture);
                volume.Value = row.Volume.ToString(CultureInfo.InvariantCulture);
                command.ExecuteNonQuery();
            }
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<DailyMarketObservation>> ReadAsync(string assetId, MarketDataSource source,
        DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        ValidateKey(assetId, source);
        if (from > to) throw new ArgumentException("Start date must precede end date.");
        await _gate.WaitAsync(cancellationToken);
        try
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT utc_day, timestamp_ms, price, volume FROM daily_market_data
                WHERE asset_id = $asset AND source = $source AND quote_currency = $currency
                  AND utc_day BETWEEN $from AND $to ORDER BY utc_day;
                """;
            command.Parameters.AddWithValue("$asset", assetId);
            command.Parameters.AddWithValue("$source", source.ToString());
            command.Parameters.AddWithValue("$currency", QuoteCurrency(source));
            command.Parameters.AddWithValue("$from", from.DayNumber);
            command.Parameters.AddWithValue("$to", to.DayNumber);
            using var reader = command.ExecuteReader();
            var result = new List<DailyMarketObservation>();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.Add(new DailyMarketObservation(DateOnly.FromDayNumber(reader.GetInt32(0)), reader.GetInt64(1),
                    decimal.Parse(reader.GetString(2), CultureInfo.InvariantCulture),
                    decimal.Parse(reader.GetString(3), CultureInfo.InvariantCulture)));
            }
            return result;
        }
        finally { _gate.Release(); }
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            connection.Open();
            if (!_initialized)
            {
                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA user_version;";
                if (Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 1)
                    throw new InvalidOperationException("The historical database schema is newer than this application supports.");
                command.CommandText = """
                    PRAGMA journal_mode=WAL;
                    CREATE TABLE IF NOT EXISTS daily_market_data (
                        asset_id TEXT NOT NULL,
                        source TEXT NOT NULL,
                        quote_currency TEXT NOT NULL,
                        utc_day INTEGER NOT NULL,
                        timestamp_ms INTEGER NOT NULL,
                        price TEXT NOT NULL,
                        volume TEXT NOT NULL,
                        PRIMARY KEY (asset_id, source, quote_currency, utc_day)
                    );
                    PRAGMA user_version=1;
                    """;
                command.ExecuteNonQuery();
                _initialized = true;
            }
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }

    public static string QuoteCurrency(MarketDataSource source) => source switch
    {
        MarketDataSource.Binance => "USDT",
        MarketDataSource.CoinGecko => "USD",
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };

    private static void ValidateKey(string assetId, MarketDataSource source)
    {
        if (string.IsNullOrWhiteSpace(assetId) || assetId.Length > 200)
            throw new ArgumentException("Asset ID must contain between 1 and 200 characters.", nameof(assetId));
        _ = QuoteCurrency(source);
    }

    public void Dispose() => _gate.Dispose();
}
