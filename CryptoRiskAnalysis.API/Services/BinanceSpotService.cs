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
            if (days <= 0)
                throw new ArgumentOutOfRangeException(nameof(days), "The requested day count must be positive.");

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

                // Binance kline index 6 is the candle close time. Exclude the currently open
                // daily candle so a partial day cannot distort volatility and trend metrics.
                List<List<JsonElement>> completedKlines;
                try
                {
                    var nowUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var parsedKlines = klines
                        .Select(k => (
                            Kline: k,
                            OpenTime: ReadUnixTimestamp(k[0]),
                            CloseTime: ReadUnixTimestamp(k[6])))
                        .ToList();
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

                // 5. Parse klines into PriceData
                // Binance returns: [timestamp(number), open(string), high(string), low(string), close(string), volume(string), ...]
                List<PriceData> priceHistory;
                List<decimal> volumes;
                try
                {
                    priceHistory = completedKlines.Select(k => new PriceData
                    {
                        // Timestamp is a number
                        Timestamp = ReadInt64(k[0]),
                        // Close price is index 4 — use InvariantCulture to handle decimal points correctly
                        Price = k[4].ValueKind == JsonValueKind.String
                            ? decimal.Parse(k[4].GetString()!, System.Globalization.CultureInfo.InvariantCulture)
                            : k[4].GetDecimal()
                    }).OrderBy(p => p.Timestamp).ToList();

                    // Quote-asset turnover (USDT) is index 7. Using base-asset quantity
                    // (index 5) would not be comparable with CoinGecko's USD volume series.
                    volumes = completedKlines.Select(k =>
                        k[7].ValueKind == JsonValueKind.String
                            ? decimal.Parse(k[7].GetString()!, System.Globalization.CultureInfo.InvariantCulture)
                            : k[7].GetDecimal()
                    ).ToList();
                }
                catch (Exception ex) when (ex is FormatException or InvalidOperationException or OverflowException)
                {
                    throw new MarketDataProviderException("Binance", "a kline contained an invalid price, volume, or timestamp.", ex);
                }

                var volumeHistory = completedKlines
                    .Select((k, index) => (priceHistory[index].Timestamp, Volume: volumes[index]))
                    .ToList();

                MarketDataValidator.ValidateCompletedDailySeries("Binance", priceHistory, volumeHistory, days);

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

        private static long ReadUnixTimestamp(JsonElement value)
        {
            var timestamp = ReadInt64(value);
            _ = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
            return timestamp;
        }

    }
}
