using System.Net;
using CryptoRiskAnalysis.API.Exceptions;
using CryptoRiskAnalysis.API.Interfaces;
using CryptoRiskAnalysis.API.Models;
using CryptoRiskAnalysis.API.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CryptoRiskAnalysis.Tests.Services;

public class MarketDataProviderLifecycleTests
{
    [Theory]
    [InlineData(MarketDataSource.Binance, "success")]
    [InlineData(MarketDataSource.CoinGecko, "success")]
    [InlineData(MarketDataSource.Binance, "malformed")]
    [InlineData(MarketDataSource.CoinGecko, "malformed")]
    [InlineData(MarketDataSource.Binance, "rate-limit")]
    [InlineData(MarketDataSource.CoinGecko, "rate-limit")]
    public async Task ProviderResponseIsDisposedOnSuccessAndFailure(MarketDataSource source, string scenario)
    {
        using var content = new TrackingContent(scenario == "malformed" ? "{invalid" : Payload(source));
        using var handler = new Handler(() => new HttpResponseMessage(
            scenario == "rate-limit" ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK)
        { Content = content });
        using var client = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(source, client, cache);

        if (scenario == "success")
        {
            var result = await service.GetAllMarketDataAsync("bitcoin", 1, TestContext.Current.CancellationToken);
            Assert.Equal(100m, Assert.Single(result.priceHistory).Price);
        }
        else if (scenario == "malformed")
        {
            await Assert.ThrowsAsync<MarketDataProviderException>(() =>
                service.GetAllMarketDataAsync("bitcoin", 1, TestContext.Current.CancellationToken));
        }
        else
        {
            await Assert.ThrowsAsync<UpstreamRateLimitException>(() =>
                service.GetAllMarketDataAsync("bitcoin", 1, TestContext.Current.CancellationToken));
        }

        Assert.True(content.Disposed);
    }

    [Theory]
    [InlineData(MarketDataSource.Binance)]
    [InlineData(MarketDataSource.CoinGecko)]
    public async Task PersistenceFailureDoesNotPopulateCacheOrRetainProviderResponse(MarketDataSource source)
    {
        var store = new Mock<IHistoricalMarketDataStore>();
        var failure = new InvalidOperationException("Storage unavailable.");
        store.Setup(s => s.SaveAsync("bitcoin", source, It.IsAny<IReadOnlyList<DailyMarketObservation>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        var contents = new List<TrackingContent>();
        using var handler = new Handler(() =>
        {
            var content = new TrackingContent(Payload(source));
            contents.Add(content);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        using var client = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(source, client, cache, store.Object);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.GetAllMarketDataAsync("bitcoin", 1, TestContext.Current.CancellationToken));
            Assert.Same(failure, actual);
        }

        // Both calls must fetch again: unsuccessful persistence cannot publish a cached result.
        Assert.Equal(2, contents.Count);
        Assert.All(contents, content => Assert.True(content.Disposed));
        store.Verify(s => s.SaveAsync("bitcoin", source, It.IsAny<IReadOnlyList<DailyMarketObservation>>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private static ICryptoDataService CreateService(MarketDataSource source, HttpClient client,
        IMemoryCache cache, IHistoricalMarketDataStore? store = null) => source == MarketDataSource.Binance
        ? new BinanceSpotService(client, cache, NullLogger<BinanceSpotService>.Instance, historyStore: store)
        : new CoinGeckoService(client, cache, NullLogger<CoinGeckoService>.Instance, historyStore: store);

    private static string Payload(MarketDataSource source) => source == MarketDataSource.Binance
        ? "[[1705276800000,\"100\",\"110\",\"90\",\"100\",\"10\",1705363199999,\"1000\"]]"
        : "{\"prices\":[[1705276800000,100]],\"total_volumes\":[[1705276800000,1000]]}";

    private sealed class Handler(Func<HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromResult(response());
    }

    private sealed class TrackingContent(string value) : StringContent(value)
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
