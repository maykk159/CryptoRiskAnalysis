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
        private readonly MarketDataRequestLock _requestLock;
        private readonly IHistoricalMarketDataStore? _historyStore;
        internal Task<MarketQuote> GetCurrentQuoteAsync(string assetId, TimeProvider clock, CancellationToken cancellationToken) =>
            MarketQuoteReader.ReadAsync(_httpClient, assetId, MarketDataSource.CoinGecko, clock, cancellationToken);

        private const string BaseUrl = "https://api.coingecko.com/api/v3";
        private const int CacheDurationSeconds = 60;
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private enum MarketField { Price, Volume }

        public CoinGeckoService(
            HttpClient httpClient,
            IMemoryCache cache,
            ILogger<CoinGeckoService> logger,
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
        /// Fetches price history and volume from CoinGecko with in-memory caching.
        /// HTTP retries (on 5xx and 429) are handled by the Polly policy in ServiceCollectionExtensions.
        /// Previously: 429 was silently returning an empty list, hiding the error from the caller.
        /// Now: typed exceptions preserve provider failure details for the middleware.
        /// </summary>
        public async Task<MarketDataSnapshot> GetAllMarketDataAsync(
            string assetId,
            int days,
            CancellationToken cancellationToken = default)
        {
            if (days <= 0)
                throw new ArgumentOutOfRangeException(nameof(days), "The requested day count must be positive.");

            string cacheKey = $"market_data_{assetId}_{days}";

            // Check cache first
            if (_cache.TryGetValue(cacheKey, out CachedMarketData? cachedData) && cachedData is not null)
            {
                _logger.LogDebug("CoinGecko Cache HIT for {AssetId}", assetId);
                await cachedData.PersistAsync(_historyStore, assetId, MarketDataSource.CoinGecko, cancellationToken);
                return cachedData.Data;
            }

            using var requestLease = await _requestLock.AcquireAsync(cacheKey, cancellationToken);
            if (_cache.TryGetValue(cacheKey, out cachedData) && cachedData is not null)
            {
                _logger.LogDebug("CoinGecko Cache HIT after waiting for {AssetId}", assetId);
                await cachedData.PersistAsync(_historyStore, assetId, MarketDataSource.CoinGecko, cancellationToken);
                return cachedData.Data;
            }

            _logger.LogInformation("CoinGecko Cache MISS for {AssetId} — fetching from API", assetId);

            // Request one extra day because CoinGecko may include today's still-open UTC candle.
            var providerDays = days + 1;
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.GetAsync(
                    $"{BaseUrl}/coins/{Uri.EscapeDataString(assetId)}/market_chart?vs_currency=usd&days={providerDays}&interval=daily",
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
                    throw new UpstreamRateLimitException("CoinGecko", response.Headers.RetryAfter?.Delta
                        ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow));
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


                var currentPrice = ReadCurrentPrice(data.Prices);
                // CoinGecko can append a live intraday point even when daily granularity is
                // requested. Keep UTC-midnight daily points and completed previous days only.
                var normalizedPrices = NormalizeCompletedDailyValues(data.Prices, MarketField.Price)
                    .TakeLast(days)
                    .ToList();
                var normalizedVolumes = NormalizeCompletedDailyValues(data.Total_Volumes, MarketField.Volume)
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

                var result = new MarketDataSnapshot(priceHistory, currentPrice, currentVolume, avgVolume, MarketDataSource.CoinGecko);

                var cacheEntry = new CachedMarketData(result,
                        priceHistory.Select((p, i) => new DailyMarketObservation(
                            DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(p.Timestamp).UtcDateTime),
                            p.Timestamp, p.Price, volumeHistory[i].Volume)).ToList());
                await cacheEntry.PersistAsync(_historyStore, assetId, MarketDataSource.CoinGecko, cancellationToken);

                // Keep the displayed current price no more than one application-cache minute old.
                _cache.Set(cacheKey, cacheEntry, new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromSeconds(CacheDurationSeconds)));

                _logger.LogInformation("CoinGecko: Fetched {PriceCount} prices for {AssetId} — cached for {Duration}s",
                    priceHistory.Count, assetId, CacheDurationSeconds);

                return result;
            }
        }


        private static decimal ReadCurrentPrice(IEnumerable<List<decimal>> values)
        {
            var nowUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var latestValue = values
                .Where(value => value is { Count: >= 2 } &&
                                value[0] <= nowUnixMilliseconds)
                .OrderBy(value => value[0])
                .LastOrDefault();

            if (latestValue == null)
                throw new MarketDataProviderException("CoinGecko", "the price series was empty or malformed.");

            try
            {
                if (decimal.Truncate(latestValue[0]) != latestValue[0])
                    throw new OverflowException("Timestamp was not an integer.");

                var timestamp = checked((long)latestValue[0]);
                _ = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
                var currentPrice = latestValue[1];

                if (currentPrice <= 0)
                    throw new MarketDataProviderException("CoinGecko", "the current price was zero or negative.");

                return currentPrice;
            }
            catch (Exception ex) when (ex is ArgumentOutOfRangeException or OverflowException)
            {
                throw new MarketDataProviderException(
                    "CoinGecko", "the latest price contained an invalid timestamp or numeric value.", ex);
            }
        }

        private static IEnumerable<DailyValue> NormalizeCompletedDailyValues(
            IEnumerable<List<decimal>> values,
            MarketField field)
        {
            var fieldName = field.ToString().ToLowerInvariant(); // used only in error messages
            var todayUtc = DateTime.UtcNow.Date;
            var parsedValues = new List<DailyValue>();

            foreach (var value in values)
            {
                if (value is null || value.Count < 2)
                {
                    throw new MarketDataProviderException(
                        "CoinGecko",
                        $"a {fieldName} observation was malformed or non-finite.");
                }

                try
                {
                    if (decimal.Truncate(value[0]) != value[0])
                        throw new OverflowException("Timestamp was not an integer.");

                    var timestamp = checked((long)value[0]);
                    var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime.Date;
                    var numericValue = value[1];

                    if (field == MarketField.Price && numericValue <= 0)
                        throw new MarketDataProviderException("CoinGecko", "a price observation was zero or negative.");
                    if (field == MarketField.Volume && numericValue < 0)
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
            public List<List<decimal>>? Prices { get; set; }
            public List<List<decimal>>? Total_Volumes { get; set; }
        }
    }
}
