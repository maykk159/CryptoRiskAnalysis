using CryptoRiskAnalysis.API.Interfaces;
using CryptoRiskAnalysis.API.Models;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using CryptoRiskAnalysis.API.Exceptions;

namespace CryptoRiskAnalysis.API.Services
{
    public class CoinGeckoService : ICryptoDataService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CoinGeckoService> _logger;
        private const string BaseUrl = "https://api.coingecko.com/api/v3";
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        public CoinGeckoService(HttpClient httpClient, IMemoryCache cache, ILogger<CoinGeckoService> logger)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;
        }

        /// <summary>
        /// Fetches price history and volume from CoinGecko with in-memory caching.
        /// HTTP retries (on 5xx and 429) are handled by the Polly policy in ServiceCollectionExtensions.
        /// Previously: 429 was silently returning an empty list, hiding the error from the caller.
        /// Now: typed exceptions preserve provider failure details for the middleware.
        /// </summary>
        public async Task<(List<PriceData> priceHistory, decimal currentVolume, decimal avgVolume)> GetAllMarketDataAsync(
            string assetId,
            int days,
            CancellationToken cancellationToken = default)
        {
            string cacheKey = $"market_data_{assetId}_{days}";

            // Check cache first
            if (_cache.TryGetValue(cacheKey, out (List<PriceData>, decimal, decimal) cachedData))
            {
                _logger.LogDebug("CoinGecko Cache HIT for {AssetId}", assetId);
                return cachedData;
            }

            _logger.LogInformation("CoinGecko Cache MISS for {AssetId} — fetching from API", assetId);

            // Polly handles retries on 429 and transient errors — no manual loop needed
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.GetAsync(
                    $"{BaseUrl}/coins/{assetId}/market_chart?vs_currency=usd&days={days}&interval=daily",
                    cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new MarketDataProviderException("CoinGecko", ex);
            }

            using (response)
            {

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    throw new AssetNotFoundException(assetId);
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    throw new UpstreamRateLimitException("CoinGecko");
                if (!response.IsSuccessStatusCode)
                    throw new MarketDataProviderException("CoinGecko", response.StatusCode);

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                CoinGeckoMarketChart? data;
                try
                {
                    data = JsonSerializer.Deserialize<CoinGeckoMarketChart>(content, JsonOptions);
                }
                catch (JsonException ex)
                {
                    throw new MarketDataProviderException("CoinGecko", "response was not valid JSON.", ex);
                }

                if (data?.Prices == null || data.Total_Volumes == null)
                {
                    _logger.LogWarning("CoinGecko returned null or empty data for {AssetId}", assetId);
                    return (new List<PriceData>(), 0, 0);
                }

                // CoinGecko can append a live intraday point even when daily granularity is
                // requested. Keep UTC-midnight daily points and completed previous days only.
                var priceHistory = NormalizeCompletedDailyValues(data.Prices)
                    .TakeLast(days)
                    .Select(p => new PriceData
                    {
                        Timestamp = (long)p[0],
                        Price = (decimal)p[1]
                    })
                    .ToList();

                var volumes = NormalizeCompletedDailyValues(data.Total_Volumes)
                    .TakeLast(days)
                    .Select(v => (decimal)v[1])
                    .ToList();
                var currentVolume = volumes.Count > 0 ? volumes.Last() : 0;
                var avgVolume = volumes.Count > 0 ? volumes.Average() : 0;

                var result = (priceHistory, currentVolume, avgVolume);

                // Cache for 3 minutes (CoinGecko rate limits are stricter than Binance)
                _cache.Set(cacheKey, result, new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromSeconds(180)));

                _logger.LogInformation("CoinGecko: Fetched {PriceCount} prices for {AssetId} — cached for 3 minutes",
                    priceHistory.Count, assetId);

                return result;
            }
        }

        private static IEnumerable<List<double>> NormalizeCompletedDailyValues(IEnumerable<List<double>> values)
        {
            var todayUtc = DateTime.UtcNow.Date;

            return values
                .Where(value => value.Count >= 2)
                .Where(value =>
                {
                    var timestamp = DateTimeOffset.FromUnixTimeMilliseconds((long)value[0]).UtcDateTime;
                    return timestamp.Date < todayUtc || timestamp.TimeOfDay == TimeSpan.Zero;
                })
                .GroupBy(value => DateTimeOffset.FromUnixTimeMilliseconds((long)value[0]).UtcDateTime.Date)
                .Select(group => group.OrderBy(value => value[0]).Last())
                .OrderBy(value => value[0]);
        }

        // Legacy methods — not used in optimized flow, but required by interface
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

        // Helper class for deserialization
        private class CoinGeckoMarketChart
        {
            public List<List<double>> Prices { get; set; } = new();
            public List<List<double>> Total_Volumes { get; set; } = new();
        }
    }
}
