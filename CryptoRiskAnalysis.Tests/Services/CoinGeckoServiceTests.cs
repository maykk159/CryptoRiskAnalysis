using System.Net;
using System.Text.Json;
using CryptoRiskAnalysis.API.Services;
using CryptoRiskAnalysis.API.Exceptions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace CryptoRiskAnalysis.Tests.Services
{
    public class CoinGeckoServiceTests
    {
        [Fact]
        public async Task GetAllMarketDataAsync_RequestsDailyDataAndExcludesIntradayPoint()
        {
            var today = DateTimeOffset.UtcNow.Date;
            var firstTimestamp = new DateTimeOffset(today.AddDays(-2), TimeSpan.Zero).ToUnixTimeMilliseconds();
            var firstIntradayTimestamp = new DateTimeOffset(today.AddDays(-2).AddHours(12), TimeSpan.Zero).ToUnixTimeMilliseconds();
            var completedTimestamp = new DateTimeOffset(today.AddDays(-1), TimeSpan.Zero).ToUnixTimeMilliseconds();
            var intradayTimestamp = new DateTimeOffset(today.AddHours(1), TimeSpan.Zero).ToUnixTimeMilliseconds();

            var payload = JsonSerializer.Serialize(new
            {
                prices = new object[][]
                {
                    new object[] { firstTimestamp, 100m },
                    new object[] { firstIntradayTimestamp, 105m },
                    new object[] { completedTimestamp, 110m },
                    new object[] { intradayTimestamp, 999m }
                },
                total_volumes = new object[][]
                {
                    new object[] { firstTimestamp, 1000m },
                    new object[] { firstIntradayTimestamp, 1050m },
                    new object[] { completedTimestamp, 1100m },
                    new object[] { intradayTimestamp, 9999m }
                }
            });

            var handler = new Mock<HttpMessageHandler>();
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(request =>
                        request.RequestUri != null &&
                        request.RequestUri.Query.Contains("interval=daily")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(payload)
                });

            using var httpClient = new HttpClient(handler.Object);
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var logger = Mock.Of<ILogger<CoinGeckoService>>();
            var service = new CoinGeckoService(httpClient, cache, logger);

            var (priceHistory, currentVolume, avgVolume) =
                await service.GetAllMarketDataAsync(
                    "bitcoin", 2, TestContext.Current.CancellationToken);

            Assert.Equal(2, priceHistory.Count);
            Assert.Equal(105m, priceHistory[0].Price);
            Assert.Equal(110m, priceHistory[^1].Price);
            Assert.Equal(1100m, currentVolume);
            Assert.Equal(1075m, avgVolume);
        }

        [Fact]
        public async Task GetAllMarketDataAsync_ExcludesFutureMidnightPoint()
        {
            var today = DateTimeOffset.UtcNow.Date;
            var firstCompletedTimestamp = new DateTimeOffset(today.AddDays(-2), TimeSpan.Zero)
                .ToUnixTimeMilliseconds();
            var lastCompletedTimestamp = new DateTimeOffset(today.AddDays(-1), TimeSpan.Zero)
                .ToUnixTimeMilliseconds();
            var futureMidnightTimestamp = new DateTimeOffset(today.AddDays(1), TimeSpan.Zero)
                .ToUnixTimeMilliseconds();
            var payload = JsonSerializer.Serialize(new
            {
                prices = new object[][]
                {
                    new object[] { firstCompletedTimestamp, 100m },
                    new object[] { lastCompletedTimestamp, 110m },
                    new object[] { futureMidnightTimestamp, 999m }
                },
                total_volumes = new object[][]
                {
                    new object[] { firstCompletedTimestamp, 1000m },
                    new object[] { lastCompletedTimestamp, 1100m },
                    new object[] { futureMidnightTimestamp, 9999m }
                }
            });

            using var httpClient = CreateHttpClient(payload);
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new CoinGeckoService(
                httpClient,
                cache,
                Mock.Of<ILogger<CoinGeckoService>>());

            var (priceHistory, currentVolume, avgVolume) =
                await service.GetAllMarketDataAsync(
                    "bitcoin", 2, TestContext.Current.CancellationToken);

            Assert.Equal(2, priceHistory.Count);
            Assert.Equal(lastCompletedTimestamp, priceHistory[^1].Timestamp);
            Assert.Equal(110m, priceHistory[^1].Price);
            Assert.Equal(1100m, currentVolume);
            Assert.Equal(1050m, avgVolume);
        }

        [Fact]
        public async Task GetAllMarketDataAsync_RejectsMismatchedPriceAndVolumeDates()
        {
            var today = DateTimeOffset.UtcNow.Date;
            var dayMinusThree = new DateTimeOffset(today.AddDays(-3), TimeSpan.Zero).ToUnixTimeMilliseconds();
            var dayMinusTwo = new DateTimeOffset(today.AddDays(-2), TimeSpan.Zero).ToUnixTimeMilliseconds();
            var dayMinusOne = new DateTimeOffset(today.AddDays(-1), TimeSpan.Zero).ToUnixTimeMilliseconds();
            var payload = JsonSerializer.Serialize(new
            {
                prices = new object[][]
                {
                    new object[] { dayMinusTwo, 100m },
                    new object[] { dayMinusOne, 101m }
                },
                total_volumes = new object[][]
                {
                    new object[] { dayMinusThree, 1000m },
                    new object[] { dayMinusTwo, 1100m }
                }
            });

            using var httpClient = CreateHttpClient(payload);
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new CoinGeckoService(httpClient, cache, Mock.Of<ILogger<CoinGeckoService>>());

            await Assert.ThrowsAsync<MarketDataProviderException>(() =>
                service.GetAllMarketDataAsync("bitcoin", 2, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task GetAllMarketDataAsync_RejectsNegativeVolume()
        {
            var timestamp = new DateTimeOffset(DateTimeOffset.UtcNow.Date.AddDays(-1), TimeSpan.Zero)
                .ToUnixTimeMilliseconds();
            var payload = JsonSerializer.Serialize(new
            {
                prices = new object[][] { new object[] { timestamp, 100m } },
                total_volumes = new object[][] { new object[] { timestamp, -1m } }
            });

            using var httpClient = CreateHttpClient(payload);
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new CoinGeckoService(httpClient, cache, Mock.Of<ILogger<CoinGeckoService>>());

            await Assert.ThrowsAsync<MarketDataProviderException>(() =>
                service.GetAllMarketDataAsync("bitcoin", 1, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task GetAllMarketDataAsync_RejectsFewerCompletedDaysThanRequested()
        {
            var timestamp = new DateTimeOffset(DateTimeOffset.UtcNow.Date.AddDays(-1), TimeSpan.Zero)
                .ToUnixTimeMilliseconds();
            var payload = JsonSerializer.Serialize(new
            {
                prices = new object[][] { new object[] { timestamp, 100m } },
                total_volumes = new object[][] { new object[] { timestamp, 1000m } }
            });

            using var httpClient = CreateHttpClient(payload);
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new CoinGeckoService(httpClient, cache, Mock.Of<ILogger<CoinGeckoService>>());

            await Assert.ThrowsAsync<MarketDataProviderException>(() =>
                service.GetAllMarketDataAsync("bitcoin", 2, TestContext.Current.CancellationToken));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GetAllMarketDataAsync_RejectsAndDoesNotCacheMissingSeries(bool omitPrices)
        {
            var timestamp = new DateTimeOffset(DateTimeOffset.UtcNow.Date.AddDays(-1), TimeSpan.Zero)
                .ToUnixTimeMilliseconds();
            var payload = omitPrices
                ? JsonSerializer.Serialize(new
                {
                    total_volumes = new object[][] { new object[] { timestamp, 1000m } }
                })
                : JsonSerializer.Serialize(new
                {
                    prices = new object[][] { new object[] { timestamp, 100m } }
                });

            using var httpClient = CreateHttpClient(payload);
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new CoinGeckoService(httpClient, cache, Mock.Of<ILogger<CoinGeckoService>>());

            await Assert.ThrowsAsync<MarketDataProviderException>(() =>
                service.GetAllMarketDataAsync("bitcoin", 1, TestContext.Current.CancellationToken));
            Assert.False(cache.TryGetValue("market_data_bitcoin_1", out _));
        }

        [Fact]
        public async Task GetAllMarketDataAsync_ConcurrentCacheMissesShareOneProviderCall()
        {
            var timestamp = new DateTimeOffset(DateTimeOffset.UtcNow.Date.AddDays(-1), TimeSpan.Zero)
                .ToUnixTimeMilliseconds();
            var payload = JsonSerializer.Serialize(new
            {
                prices = new object[][] { new object[] { timestamp, 100m } },
                total_volumes = new object[][] { new object[] { timestamp, 1000m } }
            });
            var providerCalls = 0;
            var providerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseProvider = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var handler = new Mock<HttpMessageHandler>();
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(async () =>
                {
                    Interlocked.Increment(ref providerCalls);
                    providerStarted.SetResult();
                    await releaseProvider.Task;
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(payload)
                    };
                });

            using var httpClient = new HttpClient(handler.Object);
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var requestLock = new MarketDataRequestLock();
            var logger = Mock.Of<ILogger<CoinGeckoService>>();
            var firstService = new CoinGeckoService(httpClient, cache, logger, requestLock);
            var secondService = new CoinGeckoService(httpClient, cache, logger, requestLock);

            var firstRequest = firstService.GetAllMarketDataAsync(
                "bitcoin", 1, TestContext.Current.CancellationToken);
            await providerStarted.Task;
            var secondRequest = secondService.GetAllMarketDataAsync(
                "bitcoin", 1, TestContext.Current.CancellationToken);

            Assert.Equal(1, Volatile.Read(ref providerCalls));
            releaseProvider.SetResult();
            await Task.WhenAll(firstRequest, secondRequest);

            Assert.Equal(1, providerCalls);
            Assert.Equal(firstRequest.Result.currentVolume, secondRequest.Result.currentVolume);
        }

        private static HttpClient CreateHttpClient(string payload)
        {
            var handler = new Mock<HttpMessageHandler>();
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(payload)
                });

            return new HttpClient(handler.Object);
        }
    }
}
