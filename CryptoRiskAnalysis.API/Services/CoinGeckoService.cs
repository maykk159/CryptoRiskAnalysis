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
            if (days <= 0)
                throw new ArgumentOutOfRangeException(nameof(days), "The requested day count must be positive.");

            string cacheKey = $"market_data_{assetId}_{days}";

            // Check cache first
            if (_cache.TryGetValue(cacheKey, out (List<PriceData>, decimal, decimal) cachedData))
            {
                _logger.LogDebug("CoinGecko Cache HIT for {AssetId}", assetId);
                return cachedData;
            }

            _logger.LogInformation("CoinGecko Cache MISS for {AssetId} — fetching from API", assetId);

            // Polly handles retries on 429 and transient errors — no manual loop needed
            // Request one extra day because CoinGecko may include today's still-open UTC candle.
            var providerDays = (long)days + 1;
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.GetAsync(
                    $"{BaseUrl}/coins/{assetId}/market_chart?vs_currency=usd&days={providerDays}&interval=daily",
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
                    throw new MarketDataProviderException("CoinGecko", "the price or volume series was missing.");
                }

                // CoinGecko can append a live intraday point even when daily granularity is
                // requested. Keep UTC-midnight daily points and completed previous days only.
                var normalizedPrices = NormalizeCompletedDailyValues(data.Prices, "price")
                    .TakeLast(days)
                    .ToList();
                var normalizedVolumes = NormalizeCompletedDailyValues(data.Total_Volumes, "volume")
                    .TakeLast(days)
                    .ToList();

                var priceHistory = normalizedPrices
                    .Select(point => new PriceData
                    {
                        Timestamp = point.Timestamp,
                        Price = point.Value
                    })
                    .ToList();
                var volumeHistory = normalizedVolumes
                    .Select(point => (point.Timestamp, Volume: point.Value))
                    .ToList();

                MarketDataValidator.ValidateCompletedDailySeries("CoinGecko", priceHistory, volumeHistory, days);

                var currentVolume = volumeHistory[^1].Volume;
                var avgVolume = volumeHistory.Average(point => point.Volume);

                var result = (priceHistory, currentVolume, avgVolume);

                // Cache for 3 minutes (CoinGecko rate limits are stricter than Binance)
                _cache.Set(cacheKey, result, new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromSeconds(180)));

                _logger.LogInformation("CoinGecko: Fetched {PriceCount} prices for {AssetId} — cached for 3 minutes",
                    priceHistory.Count, assetId);

                return result;
            }
        }

        private static IEnumerable<DailyValue> NormalizeCompletedDailyValues(
            IEnumerable<List<double>> values,
            string fieldName)
        {
            var todayUtc = DateTime.UtcNow.Date;
            var parsedValues = new List<DailyValue>();

            foreach (var value in values)
            {
                if (value.Count < 2 || !double.IsFinite(value[0]) || !double.IsFinite(value[1]))
                {
                    throw new MarketDataProviderException(
                        "CoinGecko",
                        $"a {fieldName} observation was malformed or non-finite.");
                }

                try
                {
                    if (Math.Truncate(value[0]) != value[0])
                        throw new OverflowException("Timestamp was not an integer.");

                    var timestamp = checked((long)value[0]);
                    var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime.Date;
                    var numericValue = checked((decimal)value[1]);

                    if (fieldName == "price" && numericValue <= 0)
                        throw new MarketDataProviderException("CoinGecko", "a price observation was zero or negative.");
                    if (fieldName == "volume" && numericValue < 0)
                        throw new MarketDataProviderException("CoinGecko", "a volume observation was negative.");

                    // Today's UTC candle is still open, including a point timestamped at midnight.
                    if (date < todayUtc)
                        parsedValues.Add(new DailyValue(timestamp, date, numericValue));
                }
                catch (Exception ex) when (ex is ArgumentOutOfRangeException or OverflowException)
                {
                    throw new MarketDataProviderException(
                        "CoinGecko",
                        $"a {fieldName} observation contained an invalid timestamp or numeric value.",
                        ex);
                }
            }

            return parsedValues
                .GroupBy(value => value.Date)
                .Select(group => group.OrderBy(value => value.Timestamp).Last())
                .OrderBy(value => value.Timestamp);
        }

        private readonly record struct DailyValue(long Timestamp, DateTime Date, decimal Value);

        // Helper class for deserialization
        private class CoinGeckoMarketChart
        {
            public List<List<double>> Prices { get; set; } = new();
            public List<List<double>> Total_Volumes { get; set; } = new();
        }
    }
}
