using System.Net;
using System.Text;
using System.Text.Json;
using CryptoRiskAnalysis.API.Extensions;
using CryptoRiskAnalysis.API.Interfaces;
using CryptoRiskAnalysis.API.Models;
using CryptoRiskAnalysis.API.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Moq;

namespace CryptoRiskAnalysis.Tests.Services
{
    public class HybridCryptoDataServiceTests
    {
        [Fact]
        public async Task GetAllMarketDataAsync_FallsBack_WhenBinanceThrowsStandardTimeout()
        {
            var calls = 0;
            using var binanceClient = new HttpClient(new StubHttpMessageHandler((_, _) => throw new TimeoutException()));
            using var coinGeckoClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            {
                calls++;
                return Task.FromResult(JsonResponse(CreateCoinGeckoPayload()));
            }));
            using var binanceCache = new MemoryCache(new MemoryCacheOptions());
            using var coinGeckoCache = new MemoryCache(new MemoryCacheOptions());
            var result = await CreateService(binanceClient, coinGeckoClient, binanceCache, coinGeckoCache)
                .GetAllMarketDataAsync("bitcoin", 1, TestContext.Current.CancellationToken);
            Assert.Single(result.priceHistory);
            Assert.Equal(1, calls);
        }

        [Theory]
        [InlineData(false, 2)]
        [InlineData(true, 1)]
        public async Task GetAllMarketDataAsync_FallsBack_WhenBinanceResponseBodyStalls(
            bool retryExceedsTotalTimeout, int expectedBinanceCalls)
        {
            var binanceCalls = 0;
            var coinGeckoCalls = 0;
            await using var provider = CreateProvider(
                new StubHttpMessageHandler((_, _) =>
                {
                    binanceCalls++;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new SlowContent() });
                }),
                new StubHttpMessageHandler((_, _) =>
                {
                    coinGeckoCalls++;
                    return Task.FromResult(JsonResponse(CreateCoinGeckoPayload()));
                }),
                options =>
                {
                    options.AttemptTimeout.Timeout = TimeSpan.FromMilliseconds(100);
                    options.TotalRequestTimeout.Timeout = retryExceedsTotalTimeout
                        ? TimeSpan.FromMilliseconds(500) : TimeSpan.FromSeconds(3);
                    options.Retry.MaxRetryAttempts = 1;
                    options.Retry.Delay = retryExceedsTotalTimeout ? TimeSpan.FromSeconds(10) : TimeSpan.Zero;
                    options.Retry.UseJitter = false;
                });
            using var scope = provider.CreateScope();

            var result = await scope.ServiceProvider.GetRequiredService<ICryptoDataService>()
                .GetAllMarketDataAsync("bitcoin", 1, TestContext.Current.CancellationToken);

            Assert.Equal(MarketDataSource.CoinGecko, result.Source);
            Assert.Equal(100m, Assert.Single(result.priceHistory).Price);
            Assert.Equal(expectedBinanceCalls, binanceCalls);
            Assert.Equal(1, coinGeckoCalls);
        }

        [Fact]
        public async Task GetAllMarketDataAsync_CallerCancellationDuringBinanceBodyRead_DoesNotFallback()
        {
            var bodyReadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var coinGeckoCalls = 0;
            await using var provider = CreateProvider(
                new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new SlowContent(() => bodyReadStarted.TrySetResult())
                })),
                new StubHttpMessageHandler((_, _) =>
                {
                    coinGeckoCalls++;
                    return Task.FromResult(JsonResponse(CreateCoinGeckoPayload()));
                }));
            using var scope = provider.CreateScope();
            using var callerCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

            var request = scope.ServiceProvider.GetRequiredService<ICryptoDataService>()
                .GetAllMarketDataAsync("bitcoin", 1, callerCancellation.Token);
            await bodyReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await callerCancellation.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
            Assert.Equal(0, coinGeckoCalls);
        }

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

            var result = await service.GetAllMarketDataAsync(
                "bitcoin", 1, TestContext.Current.CancellationToken);

            Assert.Equal(1, coinGeckoCalls);
            Assert.Single(result.priceHistory);
            Assert.Equal(100m, result.priceHistory[0].Price);
            Assert.Equal(MarketDataSource.CoinGecko, result.Source);
        }

        [Fact]
        public async Task GetAllMarketDataAsync_FallsBack_WhenBinanceTimestampIsInvalid()
        {
            var coinGeckoCalls = 0;
            var invalidBinancePayload = JsonSerializer.Serialize(new object[][]
            {
                new object[]
                {
                    "not-a-timestamp", "100", "110", "90", "100", "1000",
                    DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds(), "100000"
                }
            });
            using var binanceClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
                Task.FromResult(JsonResponse(invalidBinancePayload))));
            using var coinGeckoClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            {
                coinGeckoCalls++;
                return Task.FromResult(JsonResponse(CreateCoinGeckoPayload()));
            }));
            using var binanceCache = new MemoryCache(new MemoryCacheOptions());
            using var coinGeckoCache = new MemoryCache(new MemoryCacheOptions());
            var service = CreateService(binanceClient, coinGeckoClient, binanceCache, coinGeckoCache);

            var result = await service.GetAllMarketDataAsync(
                "bitcoin",
                1,
                TestContext.Current.CancellationToken);

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

            var result = await service.GetAllMarketDataAsync(
                "bitcoin", 1, TestContext.Current.CancellationToken);

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
                service.GetAllMarketDataAsync("bitcoin", 30, TestContext.Current.CancellationToken));
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

        private static ServiceProvider CreateProvider(HttpMessageHandler binance, HttpMessageHandler coinGecko,
            Action<HttpStandardResilienceOptions>? configureResilience = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddApplicationServices(configureResilience);
            services.AddHttpClient<BinanceSpotService>(client => client.Timeout = TimeSpan.FromSeconds(5))
                .ConfigurePrimaryHttpMessageHandler(() => binance);
            services.AddHttpClient<CoinGeckoService>().ConfigurePrimaryHttpMessageHandler(() => coinGecko);
            return services.BuildServiceProvider();
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

        private sealed class SlowContent(Action? onReadStarted = null) : HttpContent
        {
            protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
                throw new InvalidOperationException("Cancellation-aware overload must be used.");

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context,
                CancellationToken cancellationToken)
            {
                onReadStarted?.Invoke();
                return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            protected override bool TryComputeLength(out long length) { length = 0; return false; }
        }
    }
}
