using System.Net;
using System.Text.Json;
using CryptoRiskAnalysis.API.Services;
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
            var firstTimestamp = new DateTimeOffset(today.AddDays(-1), TimeSpan.Zero).ToUnixTimeMilliseconds();
            var firstIntradayTimestamp = new DateTimeOffset(today.AddDays(-1).AddHours(12), TimeSpan.Zero).ToUnixTimeMilliseconds();
            var completedTimestamp = new DateTimeOffset(today, TimeSpan.Zero).ToUnixTimeMilliseconds();
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
                await service.GetAllMarketDataAsync("bitcoin", 30);

            Assert.Equal(2, priceHistory.Count);
            Assert.Equal(105m, priceHistory[0].Price);
            Assert.Equal(110m, priceHistory[^1].Price);
            Assert.Equal(1100m, currentVolume);
            Assert.Equal(1075m, avgVolume);
        }
    }
}
