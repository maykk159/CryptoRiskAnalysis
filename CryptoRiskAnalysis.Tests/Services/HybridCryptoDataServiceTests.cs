using System.Net;
using System.Text;
using System.Text.Json;
using CryptoRiskAnalysis.API.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace CryptoRiskAnalysis.Tests.Services
{
    public class HybridCryptoDataServiceTests
    {
        [Fact]
        public async Task GetAllMarketDataAsync_FallsBack_WhenBinanceReturnsInvalidJson()
        {
            var coinGeckoCalls = 0;
            using var binanceClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
                Task.FromResult(JsonResponse("not-json"))));
            using var coinGeckoClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            {
                coinGeckoCalls++;
                return Task.FromResult(JsonResponse(CreateCoinGeckoPayload()));
            }));
            using var binanceCache = new MemoryCache(new MemoryCacheOptions());
            using var coinGeckoCache = new MemoryCache(new MemoryCacheOptions());
            var service = CreateService(binanceClient, coinGeckoClient, binanceCache, coinGeckoCache);

            var result = await service.GetAllMarketDataAsync("bitcoin", 1);

            Assert.Equal(1, coinGeckoCalls);
            Assert.Single(result.priceHistory);
            Assert.Equal(100m, result.priceHistory[0].Price);
        }

        [Fact]
        public async Task GetAllMarketDataAsync_FallsBack_WhenBinanceReturnsHttpError()
        {
            var coinGeckoCalls = 0;
            using var binanceClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway))));
            using var coinGeckoClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            {
                coinGeckoCalls++;
                return Task.FromResult(JsonResponse(CreateCoinGeckoPayload()));
            }));
            using var binanceCache = new MemoryCache(new MemoryCacheOptions());
            using var coinGeckoCache = new MemoryCache(new MemoryCacheOptions());
            var service = CreateService(binanceClient, coinGeckoClient, binanceCache, coinGeckoCache);

            var result = await service.GetAllMarketDataAsync("bitcoin", 1);

            Assert.Equal(1, coinGeckoCalls);
            Assert.Single(result.priceHistory);
        }

        [Fact]
        public async Task GetAllMarketDataAsync_DoesNotFallback_WhenBinanceThrowsUnexpectedException()
        {
            var coinGeckoCalls = 0;
            using var binanceClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
                throw new InvalidOperationException("Simulated programming error")));
            using var coinGeckoClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            {
                coinGeckoCalls++;
                return Task.FromResult(JsonResponse(CreateCoinGeckoPayload()));
            }));
            using var binanceCache = new MemoryCache(new MemoryCacheOptions());
            using var coinGeckoCache = new MemoryCache(new MemoryCacheOptions());
            var service = CreateService(binanceClient, coinGeckoClient, binanceCache, coinGeckoCache);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.GetAllMarketDataAsync("bitcoin", 30));
            Assert.Equal(0, coinGeckoCalls);
        }

        private static HybridCryptoDataService CreateService(
            HttpClient binanceClient,
            HttpClient coinGeckoClient,
            IMemoryCache binanceCache,
            IMemoryCache coinGeckoCache)
        {
            var binance = new BinanceSpotService(
                binanceClient,
                binanceCache,
                Mock.Of<ILogger<BinanceSpotService>>());
            var coinGecko = new CoinGeckoService(
                coinGeckoClient,
                coinGeckoCache,
                Mock.Of<ILogger<CoinGeckoService>>());

            return new HybridCryptoDataService(
                binance,
                coinGecko,
                Mock.Of<ILogger<HybridCryptoDataService>>());
        }

        private static string CreateCoinGeckoPayload()
        {
            var timestamp = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-1), TimeSpan.Zero)
                .ToUnixTimeMilliseconds();
            return JsonSerializer.Serialize(new
            {
                prices = new[] { new[] { (double)timestamp, 100d } },
                total_volumes = new[] { new[] { (double)timestamp, 1000d } }
            });
        }

        private static HttpResponseMessage JsonResponse(string content)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };
        }

        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

            public StubHttpMessageHandler(
                Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return _handler(request, cancellationToken);
            }
        }
    }
}
