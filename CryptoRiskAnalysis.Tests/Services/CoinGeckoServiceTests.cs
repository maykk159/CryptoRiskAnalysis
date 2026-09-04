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
                await service.GetAllMarketDataAsync("bitcoin", 2);

            Assert.Equal(2, priceHistory.Count);
            Assert.Equal(105m, priceHistory[0].Price);
            Assert.Equal(110m, priceHistory[^1].Price);
            Assert.Equal(1100m, currentVolume);
            Assert.Equal(1075m, avgVolume);
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
