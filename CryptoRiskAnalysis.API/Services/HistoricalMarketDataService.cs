using CryptoRiskAnalysis.API.Exceptions;
using CryptoRiskAnalysis.API.Interfaces;
using CryptoRiskAnalysis.API.Models;

namespace CryptoRiskAnalysis.API.Services;

public sealed class HistoricalMarketDataService(
    IHistoricalMarketDataStore store, BinanceSpotService binance, CoinGeckoService coinGecko,
    MarketDataRequestLock requestLock, TimeProvider clock)
{
    public async Task<HistoricalMarketData> GetAsync(string assetId, MarketDataSource source, int days,
        DateOnly? endDate, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId) || assetId.Length > 200)
            throw new ArgumentException("Asset ID must contain between 1 and 200 characters.", nameof(assetId));
        if (days is < 1 or > 90) throw new ArgumentOutOfRangeException(nameof(days), "Days must be between 1 and 90.");
        var currency = SqliteHistoricalMarketDataStore.QuoteCurrency(source);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var to = endDate ?? today.AddDays(-1);
        if (to >= today || to.DayNumber < days - 1)
            throw new InvalidHistoricalDateRangeException();
        var from = to.AddDays(1 - days);

        // Serialize fills for an asset/source even when callers ask for different windows.
        using var lease = await requestLock.AcquireAsync($"history:{source}:{assetId}", cancellationToken);
        var rows = await store.ReadAsync(assetId, source, from, to, cancellationToken);
        if (rows.Count != days)
        {
            var present = rows.Select(r => r.Date).ToHashSet();
            var firstMissing = Enumerable.Range(0, days).Select(from.AddDays).First(d => !present.Contains(d));
            // Existing provider adapters fetch recent windows, not arbitrary ancient ranges.
            var fetchDays = today.DayNumber - firstMissing.DayNumber;
            if (fetchDays > 90)
                throw new HistoricalDataUnavailableException("Missing dates are outside the supported 90-day backfill window.");
            ICryptoDataService provider = source == MarketDataSource.Binance ? binance : coinGecko;
            await provider.GetAllMarketDataAsync(assetId, fetchDays, cancellationToken);
            rows = await store.ReadAsync(assetId, source, from, to, cancellationToken);
            if (rows.Count != days)
                throw new MarketDataProviderException(source.ToString(), "the requested historical window is still incomplete after backfill.");
        }
        return new HistoricalMarketData(assetId, source, currency, from, to, rows);
    }
}
