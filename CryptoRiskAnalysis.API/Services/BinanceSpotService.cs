using CryptoRiskAnalysis.API.Interfaces;
using CryptoRiskAnalysis.API.Models;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using CryptoRiskAnalysis.API.Exceptions;

namespace CryptoRiskAnalysis.API.Services
{
    public class BinanceSpotService : ICryptoDataService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<BinanceSpotService> _logger;
        private readonly MarketDataRequestLock _requestLock;
        private readonly IHistoricalMarketDataStore? _historyStore;
        internal Task<MarketQuote> GetCurrentQuoteAsync(string assetId, TimeProvider clock, CancellationToken cancellationToken) =>
            MarketQuoteReader.ReadAsync(_httpClient, assetId, MarketDataSource.Binance, clock, cancellationToken);

        private const string BaseUrl = "https://api.binance.com/api/v3";
        private const int CacheDurationSeconds = 60; // 1 minute cache for fresh data

        public BinanceSpotService(
            HttpClient httpClient,
            IMemoryCache cache,
            ILogger<BinanceSpotService> logger,
            MarketDataRequestLock? requestLock = null,
            IHistoricalMarketDataStore? historyStore = null)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;
            _requestLock = requestLock ?? new MarketDataRequestLock();
            _historyStore = historyStore;
        }

        /// <summary>
        /// Fetches price history and volume data from Binance klines endpoint.
        /// HTTP retries (on 5xx and 429) are handled automatically by the Polly policy
        /// configured in ServiceCollectionExtensions — no manual retry loop needed here.
        /// </summary>
        public async Task<MarketDataSnapshot> GetAllMarketDataAsync(
            string assetId,
            int days,
            CancellationToken cancellationToken = default)
        {
            if (days <= 0)
                throw new ArgumentOutOfRangeException(nameof(days), "The requested day count must be positive.");

            // 1. Map CoinGecko ID to Binance symbol
            var symbol = BinanceSymbolMapper.GetBinanceSymbol(assetId);
            if (symbol == null)
                throw new MarketDataProviderException("Binance", $"asset '{assetId}' is not mapped.");

            // 2. Check cache first (1-minute cache for fresh data)
            string cacheKey = $"binance_{symbol}_{days}";
            if (_cache.TryGetValue(cacheKey, out CachedMarketData? cachedData) && cachedData is not null)
            {
                _logger.LogDebug("Binance Cache HIT for {AssetId} ({Symbol})", assetId, symbol);
                await cachedData.PersistAsync(_historyStore, assetId, MarketDataSource.Binance, cancellationToken);
                return cachedData.Data;
            }

            using var requestLease = await _requestLock.AcquireAsync(cacheKey, cancellationToken);
            if (_cache.TryGetValue(cacheKey, out cachedData) && cachedData is not null)
            {
                _logger.LogDebug("Binance Cache HIT after waiting for {AssetId} ({Symbol})", assetId, symbol);
                await cachedData.PersistAsync(_historyStore, assetId, MarketDataSource.Binance, cancellationToken);
                return cachedData.Data;
            }

            _logger.LogInformation("Binance Cache MISS for {AssetId} ({Symbol}) — fetching from API", assetId, symbol);

            // 3. Request one extra candle because Binance includes the currently open daily candle.
            // The open candle is filtered below so risk calculations only use completed days.
            var (interval, limit) = GetKlineParams(days);
            var url = $"{BaseUrl}/klines?symbol={symbol}&interval={interval}&limit={limit}";

            using var response = await FetchResponseAsync(url, cancellationToken);
            ValidateResponse(response);
            var cacheEntry = await ReadMarketDataAsync(response, symbol, days, cancellationToken);
            await PersistAndCacheAsync(cacheKey, assetId, symbol, cacheEntry, cancellationToken);
            return cacheEntry.Data;
        }

        private async Task<HttpResponseMessage> FetchResponseAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                return await _httpClient.GetAsync(url, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new MarketDataProviderException("Binance", ex);
            }
        }

        private static void ValidateResponse(HttpResponseMessage response)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                throw new UpstreamRateLimitException("Binance");
            if (!response.IsSuccessStatusCode)
                throw new MarketDataProviderException("Binance", response.StatusCode);
        }

        private async Task<CachedMarketData> ReadMarketDataAsync(
            HttpResponseMessage response, string symbol, int days, CancellationToken cancellationToken)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var klines = DeserializeKlines(content, symbol);
            var (completedKlines, latestCloseValue) = SelectCompletedKlines(klines, symbol, days);
            var (priceHistory, volumes, currentPrice) = ParseCompletedKlines(completedKlines, latestCloseValue);
            var volumeHistory = completedKlines
                .Select((k, index) => (priceHistory[index].Timestamp, Volume: volumes[index]))
                .ToList();

            MarketDataValidator.ValidateCompletedDailySeries("Binance", priceHistory, volumeHistory, days);

            var (currentVolume, avgVolume) = CalculateVolumeStatistics(volumes, symbol);
            var result = new MarketDataSnapshot(priceHistory, currentPrice, currentVolume, avgVolume, MarketDataSource.Binance);
            return CachedMarketData.Create(result, volumeHistory);
        }

        private static List<List<JsonElement>> DeserializeKlines(string content, string symbol)
        {
            List<List<JsonElement>>? klines;
            try
            {
                klines = JsonSerializer.Deserialize<List<List<JsonElement>>>(content);
            }
            catch (JsonException ex)
            {
                throw new MarketDataProviderException("Binance", "response was not valid JSON.", ex);
            }

            if (klines == null || klines.Count == 0)
                throw new MarketDataProviderException("Binance", $"no kline data was returned for {symbol}.");

            if (klines.Any(k => k.Count < 8))
                throw new MarketDataProviderException("Binance", "a kline did not contain all required fields.");
            return klines;
        }

        private static (List<List<JsonElement>> Completed, JsonElement LatestClose) SelectCompletedKlines(
            List<List<JsonElement>> klines, string symbol, int days)
        {
            List<List<JsonElement>> completedKlines;
            JsonElement latestCloseValue;
            try
            {
                var nowUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var parsedKlines = klines
                    .Select(k => (
                        Kline: k,
                        OpenTime: ReadUnixTimestamp(k[0]),
                        CloseTime: ReadUnixTimestamp(k[6])))
                    .ToList();
                latestCloseValue = parsedKlines.MaxBy(k => k.OpenTime).Kline[4];
                completedKlines = parsedKlines
                    .Where(k => k.CloseTime < nowUnixMilliseconds)
                    .OrderBy(k => k.OpenTime)
                    .TakeLast(days)
                    .Select(k => k.Kline)
                    .ToList();
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException or OverflowException)
            {
                throw new MarketDataProviderException("Binance", "a kline contained an invalid timestamp.", ex);
            }

            if (completedKlines.Count == 0)
                throw new MarketDataProviderException("Binance", $"no completed kline data was returned for {symbol}.");
            return (completedKlines, latestCloseValue);
        }

        private static (List<PriceData> Prices, List<decimal> Volumes, decimal CurrentPrice) ParseCompletedKlines(
            List<List<JsonElement>> completedKlines, JsonElement latestCloseValue)
        {
            List<PriceData> priceHistory;
            List<decimal> volumes;
            decimal currentPrice;
            try
            {
                currentPrice = ReadDecimal(latestCloseValue);
                // completedKlines is already ordered by this same timestamp.
                priceHistory = completedKlines.Select(k => new PriceData
                {
                    // Timestamp is a number
                    Timestamp = ReadInt64(k[0]),
                    // Close price is index 4 — use InvariantCulture to handle decimal points correctly
                    Price = ReadDecimal(k[4])
                }).ToList();

                // Quote-asset turnover (USDT) is index 7. Using base-asset quantity
                // (index 5) would not be comparable with CoinGecko's USD volume series.
                volumes = completedKlines.Select(k => ReadDecimal(k[7])).ToList();
            }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException or OverflowException)
            {
                throw new MarketDataProviderException("Binance", "a kline contained an invalid price, volume, or timestamp.", ex);
            }

            if (currentPrice <= 0)
                throw new MarketDataProviderException("Binance", "the current price was zero or negative.");
            return (priceHistory, volumes, currentPrice);
        }

        private (decimal Current, decimal Average) CalculateVolumeStatistics(List<decimal> volumes, string symbol)
        {
            decimal currentVolume;
            decimal avgVolume;

            if (volumes.Count > 0)
            {
                currentVolume = volumes[^1];
                avgVolume = volumes.Average();
                _logger.LogInformation("Volume for {Symbol}: LastCompleted={Vol:F0}, Avg={Avg:F0}", symbol, currentVolume, avgVolume);
            }
            else
            {
                currentVolume = 0;
                avgVolume = 0;
            }
            return (currentVolume, avgVolume);
        }

        private async Task PersistAndCacheAsync(string cacheKey, string assetId, string symbol,
            CachedMarketData cacheEntry, CancellationToken cancellationToken)
        {
            await cacheEntry.PersistAsync(_historyStore, assetId, MarketDataSource.Binance, cancellationToken);

            // 7. Cache for 1 minute
            _cache.Set(cacheKey, cacheEntry, new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromSeconds(CacheDurationSeconds)));

            _logger.LogInformation("Binance: Fetched {Count} candles for {AssetId} ({Symbol}) — cached for {Duration}s",
                cacheEntry.Data.priceHistory.Count, assetId, symbol, CacheDurationSeconds);
        }

        /// <summary>
        /// Determines the optimal kline interval and limit based on time range.
        /// Balances data granularity with API efficiency.
        /// </summary>
        private static (string interval, int limit) GetKlineParams(int days)
        {
            return ("1d", Math.Min(days + 1, 1000));
        }

        private static long ReadInt64(JsonElement value)
        {
            return value.ValueKind == JsonValueKind.Number
                ? value.GetInt64()
                : long.Parse(value.GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static decimal ReadDecimal(JsonElement value)
        {
            return value.ValueKind == JsonValueKind.String
                ? decimal.Parse(value.GetString()!, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture)
                : value.GetDecimal();
        }

        private static long ReadUnixTimestamp(JsonElement value)
        {
            var timestamp = ReadInt64(value);
            _ = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
            return timestamp;
        }

    }
}
