using Xunit;
using Moq;
using Moq.Protected;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using CryptoRiskAnalysis.API.Services;
using CryptoRiskAnalysis.API.Exceptions;
using System.Net;
using System.Text.Json;

namespace CryptoRiskAnalysis.Tests.Services
{
    public class BinanceSpotServiceTests
    {
        private readonly Mock<ILogger<BinanceSpotService>> _mockLogger;
        private readonly Mock<IMemoryCache> _mockCache;
        private readonly Mock<HttpMessageHandler> _mockHttpHandler;
        private readonly HttpClient _httpClient;

        public BinanceSpotServiceTests()
        {
            _mockLogger = new Mock<ILogger<BinanceSpotService>>();
            _mockCache = new Mock<IMemoryCache>();
            _mockHttpHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpHandler.Object);
        }

        [Fact]
        public async Task GetAllMarketDataAsync_ValidSymbol_ReturnsData()
        {
            // Arrange
            var mockResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(GetMockBinanceResponse())
            };

            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(mockResponse);

            object cacheValue = null;
            _mockCache.Setup(c => c.TryGetValue(It.IsAny<object>(), out cacheValue))
                .Returns(false);
            _mockCache.Setup(c => c.CreateEntry(It.IsAny<object>()))
                .Returns(Mock.Of<ICacheEntry>());

            var service = new BinanceSpotService(_httpClient, _mockCache.Object, _mockLogger.Object);

            // Act
            var (priceHistory, currentVolume, avgVolume) = await service.GetAllMarketDataAsync("bitcoin", 3);

            // Assert
            Assert.NotNull(priceHistory);
            Assert.NotEmpty(priceHistory);
            Assert.True(currentVolume > 0);
            Assert.True(avgVolume > 0);
        }

        [Fact]
        public async Task GetAllMarketDataAsync_EnsuresChronologicalOrder()
        {
            // Arrange
            var mockResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(GetMockBinanceResponse())
            };

            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(mockResponse);

            object cacheValue = null;
            _mockCache.Setup(c => c.TryGetValue(It.IsAny<object>(), out cacheValue))
                .Returns(false);
            _mockCache.Setup(c => c.CreateEntry(It.IsAny<object>()))
                .Returns(Mock.Of<ICacheEntry>());

            var service = new BinanceSpotService(_httpClient, _mockCache.Object, _mockLogger.Object);

            // Act
            var (priceHistory, _, _) = await service.GetAllMarketDataAsync("bitcoin", 3);

            // Assert - Verify chronological order (oldest first)
            for (int i = 1; i < priceHistory.Count; i++)
            {
                Assert.True(priceHistory[i].Timestamp > priceHistory[i - 1].Timestamp,
                    "Price history should be in chronological order (oldest to newest)");
            }
        }

        [Fact]
        public async Task GetAllMarketDataAsync_InvalidSymbol_ThrowsException()
        {
            // Arrange
            var mockResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("Invalid symbol")
            };

            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(mockResponse);

            object cacheValue = null;
            _mockCache.Setup(c => c.TryGetValue(It.IsAny<object>(), out cacheValue))
                .Returns(false);

            var service = new BinanceSpotService(_httpClient, _mockCache.Object, _mockLogger.Object);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(async () =>
                await service.GetAllMarketDataAsync("invalid_coin", 30));
        }

        [Fact]
        public async Task GetAllMarketDataAsync_UsesCache_WhenDataAvailable()
        {
            // Arrange
            var cachedData = (
                new List<API.Models.PriceData> { new API.Models.PriceData { Timestamp = 1000, Price = 100m } },
                1000m,
                900m
            );

            object cacheValue = cachedData;
            _mockCache.Setup(c => c.TryGetValue(It.IsAny<object>(), out cacheValue))
                .Returns(true);

            var service = new BinanceSpotService(_httpClient, _mockCache.Object, _mockLogger.Object);

            // Act
            var result = await service.GetAllMarketDataAsync("bitcoin", 30);

            // Assert
            Assert.Equal(cachedData.Item1, result.priceHistory);
            Assert.Equal(cachedData.Item2, result.currentVolume);
            Assert.Equal(cachedData.Item3, result.avgVolume);

            // Verify HTTP was never called
            _mockHttpHandler.Protected().Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task GetAllMarketDataAsync_ExcludesOpenCandle()
        {
            var mockResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(GetMockBinanceResponseWithOpenCandle())
            };

            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(request =>
                        request.RequestUri != null &&
                        request.RequestUri.Query.Contains("interval=1d") &&
                        request.RequestUri.Query.Contains("limit=3")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(mockResponse);

            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new BinanceSpotService(_httpClient, cache, _mockLogger.Object);

            var (priceHistory, currentVolume, avgVolume) = await service.GetAllMarketDataAsync("bitcoin", 2);

            Assert.Equal(2, priceHistory.Count);
            Assert.Equal(200m, priceHistory[^1].Price);
            Assert.Equal(2000m, currentVolume);
            Assert.Equal(1500m, avgVolume);
        }

        [Fact]
        public async Task GetAllMarketDataAsync_RejectsNonPositivePrice()
        {
            var payload = GetMockBinanceResponse().Replace("\"42500.00\",\"1000.5\"", "\"0\",\"1000.5\"");
            var mockResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(payload)
            };

            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(mockResponse);

            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new BinanceSpotService(_httpClient, cache, _mockLogger.Object);

            await Assert.ThrowsAsync<MarketDataProviderException>(() =>
                service.GetAllMarketDataAsync("bitcoin", 3, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task GetAllMarketDataAsync_RejectsFewerCompletedDaysThanRequested()
        {
            var mockResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(GetMockBinanceResponse())
            };

            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(mockResponse);

            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new BinanceSpotService(_httpClient, cache, _mockLogger.Object);

            await Assert.ThrowsAsync<MarketDataProviderException>(() =>
                service.GetAllMarketDataAsync("bitcoin", 4, TestContext.Current.CancellationToken));
        }

        [Fact]
        public void Polygon_UsesCurrentBinanceSymbol()
        {
            Assert.Equal("POLUSDT", BinanceSymbolMapper.GetBinanceSymbol("polygon-ecosystem-token"));
            Assert.Equal("POLUSDT", BinanceSymbolMapper.GetBinanceSymbol("matic-network"));
        }

        private string GetMockBinanceResponse()
        {
            // Mock Binance klines response (3 candles)
            return @"[
                [1705276800000,""42000.00"",""43000.00"",""41000.00"",""42500.00"",""1000.5"",1705363199999,""42500000"",1000,""500.25"",""21250000"",""0""],
                [1705363200000,""42500.00"",""44000.00"",""42000.00"",""43000.00"",""1100.0"",1705449599999,""47300000"",1100,""550.00"",""23650000"",""0""],
                [1705449600000,""43000.00"",""43500.00"",""42500.00"",""43200.00"",""1050.0"",1705535999999,""45360000"",1050,""525.00"",""22680000"",""0""]
            ]";
        }

        private static string GetMockBinanceResponseWithOpenCandle()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var klines = new object[][]
            {
                new object[] { now - 172_800_000, "100", "110", "90", "100", "1000", now - 86_400_001 },
                new object[] { now - 86_400_000, "200", "210", "190", "200", "2000", now - 1 },
                new object[] { now, "300", "310", "290", "300", "3000", now + 86_399_999 }
            };

            return JsonSerializer.Serialize(klines);
        }
    }
}
