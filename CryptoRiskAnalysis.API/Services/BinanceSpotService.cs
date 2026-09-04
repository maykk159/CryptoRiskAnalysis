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
        private const string BaseUrl = "https://api.binance.com/api/v3";
        private const int CacheDurationSeconds = 60; // 1 minute cache for fresh data

        public BinanceSpotService(HttpClient httpClient, IMemoryCache cache, ILogger<BinanceSpotService> logger)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;
        }

        /// <summary>
        /// Fetches price history and volume data from Binance klines endpoint.
        /// HTTP retries (on 5xx and 429) are handled automatically by the Polly policy
        /// configured in ServiceCollectionExtensions — no manual retry loop needed here.
        /// </summary>
        public async Task<(List<PriceData> priceHistory, decimal currentVolume, decimal avgVolume)> GetAllMarketDataAsync(
            string assetId,
            int days,
            CancellationToken cancellationToken = default)
        {
            // 1. Map CoinGecko ID to Binance symbol
            var symbol = BinanceSymbolMapper.GetBinanceSymbol(assetId);
            if (symbol == null)
                throw new Exception($"Asset '{assetId}' not available on Binance");

            // 2. Check cache first (1-minute cache for fresh data)
            string cacheKey = $"binance_{symbol}_{days}";
            if (_cache.TryGetValue(cacheKey, out (List<PriceData>, decimal, decimal) cachedData))
            {
                _logger.LogDebug("Binance Cache HIT for {AssetId} ({Symbol})", assetId, symbol);
                return cachedData;
            }

            _logger.LogInformation("Binance Cache MISS for {AssetId} ({Symbol}) — fetching from API", assetId, symbol);

            // 3. Request one extra candle because Binance includes the currently open daily candle.
            // The open candle is filtered below so risk calculations only use completed days.
            var (interval, limit) = GetKlineParams(days);
            var url = $"{BaseUrl}/klines?symbol={symbol}&interval={interval}&limit={limit}";

            // 4. Fetch — Polly retries on transient errors and 429 automatically
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.GetAsync(url, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new MarketDataProviderException("Binance", ex);
            }

            using (response)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    throw new UpstreamRateLimitException("Binance");
                if (!response.IsSuccessStatusCode)
                    throw new MarketDataProviderException("Binance", response.StatusCode);

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var klines = JsonSerializer.Deserialize<List<List<JsonElement>>>(content);

                if (klines == null || klines.Count == 0)
                    throw new Exception($"No kline data returned for {symbol}");

                // Binance kline index 6 is the candle close time. Exclude the currently open
                // daily candle so a partial day cannot distort volatility and trend metrics.
                var nowUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var completedKlines = klines
                    .Where(k => k.Count >= 7)
                    .Where(k => ReadInt64(k[6]) < nowUnixMilliseconds)
                    .OrderBy(k => ReadInt64(k[0]))
                    .TakeLast(days)
                    .ToList();

                if (completedKlines.Count == 0)
                    throw new Exception($"No completed kline data returned for {symbol}");

                // 5. Parse klines into PriceData
                // Binance returns: [timestamp(number), open(string), high(string), low(string), close(string), volume(string), ...]
                var priceHistory = completedKlines.Select(k => new PriceData
                {
                    // Timestamp is a number
                    Timestamp = ReadInt64(k[0]),
                    // Close price is index 4 — use InvariantCulture to handle decimal points correctly
                    Price = k[4].ValueKind == JsonValueKind.String
                        ? decimal.Parse(k[4].GetString()!, System.Globalization.CultureInfo.InvariantCulture)
                        : k[4].GetDecimal()
                }).OrderBy(p => p.Timestamp).ToList();

                // 6. Calculate volume metrics from the same completed daily candles.
                var volumes = completedKlines.Select(k =>
                    k[5].ValueKind == JsonValueKind.String
                        ? decimal.Parse(k[5].GetString()!, System.Globalization.CultureInfo.InvariantCulture)
                        : k[5].GetDecimal()
                ).ToList();

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

                var result = (priceHistory, currentVolume, avgVolume);

                // 7. Cache for 1 minute
                _cache.Set(cacheKey, result, new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromSeconds(CacheDurationSeconds)));

                _logger.LogInformation("Binance: Fetched {Count} candles for {AssetId} ({Symbol}) — cached for {Duration}s",
                    priceHistory.Count, assetId, symbol, CacheDurationSeconds);

                return result;
            }
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

        // Legacy methods (not used in optimized flow, but required by interface)
        public async Task<List<PriceData>> GetHistoricalPriceDataAsync(
            string assetId,
            int days,
            CancellationToken cancellationToken = default)
        {
            var (priceHistory, _, _) = await GetAllMarketDataAsync(assetId, days, cancellationToken);
            return priceHistory;
        }

        public async Task<decimal> GetCurrentVolumeAsync(string assetId, CancellationToken cancellationToken = default)
        {
            var (_, currentVolume, _) = await GetAllMarketDataAsync(assetId, 1, cancellationToken);
            return currentVolume;
        }

        public async Task<decimal> GetAverageVolumeAsync(
            string assetId,
            int days,
            CancellationToken cancellationToken = default)
        {
            var (_, _, avgVolume) = await GetAllMarketDataAsync(assetId, days, cancellationToken);
            return avgVolume;
        }
    }
}
