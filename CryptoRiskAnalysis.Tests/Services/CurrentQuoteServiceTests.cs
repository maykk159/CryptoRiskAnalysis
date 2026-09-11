using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CryptoRiskAnalysis.API.Controllers;
using CryptoRiskAnalysis.API.DTOs;
using CryptoRiskAnalysis.API.Exceptions;
using CryptoRiskAnalysis.API.Interfaces;
using CryptoRiskAnalysis.API.Models;
using CryptoRiskAnalysis.API.Services;
using CryptoRiskAnalysis.API.Wrappers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CryptoRiskAnalysis.Tests.Services;

public class CurrentQuoteServiceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RiskPeriodsUseTicker_RefreshBypassesCache_ExpiredQuotesAreRefetched()
    {
        var calls = 0;
        using var fixture = new Fixture((request, _) =>
        {
            Assert.Equal("/api/v3/ticker/price", request.RequestUri!.AbsolutePath);
            Assert.Equal("?symbol=BTCUSDT", request.RequestUri.Query);
            return Task.FromResult(Json(new { symbol = "BTCUSDT", price = (++calls * 100).ToString() }));
        });
        var history = new Mock<ICryptoDataService>();
        history.Setup(s => s.GetAllMarketDataAsync("bitcoin", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, int days, CancellationToken _) => new MarketDataSnapshot(
                Enumerable.Range(0, days).Select(i => new PriceData { Timestamp = i * 86_400_000L, Price = 10m }).ToList(),
                999m, 1000m, 1000m));
        var controller = new RiskAnalysisController(history.Object,
            new RiskAnalysisEngine(NullLogger<RiskAnalysisEngine>.Instance),
            NullLogger<RiskAnalysisController>.Instance, fixture.Quotes);
        async Task<RiskAnalysisResponseDto> Read(int days, bool refresh = false)
        {
            var result = await controller.GetRiskAnalysis("bitcoin", days, Token, refresh);
            return Assert.IsType<ApiResponse<RiskAnalysisResponseDto>>(
                Assert.IsType<OkObjectResult>(result.Result).Value).Data!;
        }

        var first = await Read(30);
        Assert.Equal(100m, first.CurrentPrice); // Never the 999 historical snapshot price.
        Assert.Equal("Binance", first.CurrentQuote!.Source);
        Assert.Equal("USDT", first.CurrentQuote.Currency);
        Assert.Equal(fixture.Clock.GetUtcNow(), first.CurrentQuote.FetchedAt);
        Assert.Null(first.CurrentQuote.SourceUpdatedAt); // Ticker does not supply an observation timestamp.
        Assert.Equal(first.CurrentQuote, (await Read(90)).CurrentQuote);
        Assert.Equal(1, calls);
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        var refreshed = await Read(90, true);
        Assert.Equal(200m, refreshed.CurrentPrice);
        Assert.Equal(2, calls);
        Assert.Equal(refreshed.CurrentQuote, (await Read(30)).CurrentQuote);
        Assert.Equal(refreshed.CurrentQuote, (await Read(7)).CurrentQuote);
        fixture.Clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(300m, (await Read(30)).CurrentPrice);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task ConcurrentRequestsAndAliasesShareOneTickerFetch()
    {
        var calls = 0;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var fixture = new Fixture(async (_, token) =>
        {
            calls++;
            started.TrySetResult();
            await release.Task.WaitAsync(token);
            return Json(new { symbol = "POLUSDT", price = "0.1234567890123456789012345678" });
        });
        var first = fixture.Quotes.GetAsync("matic-network", MarketDataSource.Binance, true, Token);
        await started.Task.WaitAsync(Token);
        var second = fixture.Quotes.GetAsync("polygon-ecosystem-token", MarketDataSource.Binance, true, Token);
        release.SetResult();
        var results = await Task.WhenAll(first, second);
        Assert.Equal(1, calls);
        Assert.Equal(results[0], results[1]);
        Assert.Equal(0.1234567890123456789012345678m, results[0].Price);
    }

    [Fact]
    public async Task ControllerUsesHistoricalProviderForQuoteEvenWhenBitcoinIsMappedToBinance()
    {
        var history = new Mock<ICryptoDataService>();
        var points = Enumerable.Range(0, 7).Select(i => new PriceData { Timestamp = i * 86_400_000L, Price = 100m }).ToList();
        history.Setup(s => s.GetAllMarketDataAsync("bitcoin", 7, Token)).ReturnsAsync(
            new MarketDataSnapshot(points, 999m, 1000m, 1000m, MarketDataSource.CoinGecko));
        var quotes = new Mock<ICurrentQuoteService>(MockBehavior.Strict);
        var expected = new MarketQuote(120m, "CoinGecko", "USD", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        quotes.Setup(q => q.GetAsync("bitcoin", MarketDataSource.CoinGecko, true, Token)).ReturnsAsync(expected);
        var controller = new RiskAnalysisController(history.Object,
            new RiskAnalysisEngine(NullLogger<RiskAnalysisEngine>.Instance),
            NullLogger<RiskAnalysisController>.Instance, quotes.Object);
        var result = await controller.GetRiskAnalysis("bitcoin", 7, Token, true);
        var data = Assert.IsType<ApiResponse<RiskAnalysisResponseDto>>(Assert.IsType<OkObjectResult>(result.Result).Value).Data!;
        Assert.Equal(expected, data.CurrentQuote);
        Assert.Equal(120m, data.CurrentPrice);
        quotes.VerifyAll();
    }

    [Fact]
    public async Task CoinGeckoQuoteKeepsSourceCurrencyAndTimestamp_UsesLongerQuotaFriendlyCache()
    {
        var clock = new Clock();
        var calls = 0;
        using var fixture = new Fixture((request, _) =>
        {
            calls++;
            Assert.Equal("/api/v3/simple/price", request.RequestUri!.AbsolutePath);
            Assert.Contains("include_last_updated_at=true", request.RequestUri.Query);
            Assert.Contains("ids=bitcoin", request.RequestUri.Query);
            return Task.FromResult(Json(new { bitcoin = new { usd = 0.000007651234567890123456789m,
                last_updated_at = clock.GetUtcNow().AddSeconds(-20).ToUnixTimeSeconds() } }));
        }, clock);
        var quote = await fixture.Quotes.GetAsync("bitcoin", MarketDataSource.CoinGecko, false, Token);
        Assert.Equal("CoinGecko", quote.Source);
        Assert.Equal("USD", quote.Currency);
        Assert.Equal(clock.GetUtcNow().AddSeconds(-20), quote.SourceUpdatedAt);
        Assert.Equal(0.000007651234567890123456789m, quote.Price);
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(quote, await fixture.Quotes.GetAsync("bitcoin", MarketDataSource.CoinGecko, false, Token));
        Assert.Equal(1, calls);
        clock.Advance(TimeSpan.FromSeconds(20));
        await fixture.Quotes.GetAsync("bitcoin", MarketDataSource.CoinGecko, false, Token);
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData("{\"symbol\":\"ETHUSDT\",\"price\":\"100\"}")]
    [InlineData("{\"symbol\":\"BTCUSDT\",\"price\":\"0\"}")]
    [InlineData("{\"symbol\":\"BTCUSDT\",\"price\":null}")]
    [InlineData("{\"price\":\"100\"}")]
    [InlineData("not json")]
    public async Task InvalidQuoteIsRejectedAndNotCached(string payload)
    {
        var calls = 0;
        using var fixture = new Fixture((_, _) => Task.FromResult(++calls == 1
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload) }
            : Json(new { symbol = "BTCUSDT", price = "101" })));
        await Assert.ThrowsAsync<MarketDataProviderException>(() =>
            fixture.Quotes.GetAsync("bitcoin", MarketDataSource.Binance, false, Token));
        Assert.Equal(101m, (await fixture.Quotes.GetAsync("bitcoin", MarketDataSource.Binance, false, Token)).Price);
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(-301)]
    [InlineData(61)]
    public async Task StaleOrFutureCoinGeckoTimestampIsRejected(int age)
    {
        var clock = new Clock();
        using var fixture = new Fixture((_, _) => Task.FromResult(Json(new { bitcoin = new { usd = 100m,
            last_updated_at = clock.GetUtcNow().AddSeconds(age).ToUnixTimeSeconds() } })), clock);
        await Assert.ThrowsAsync<MarketDataProviderException>(() =>
            fixture.Quotes.GetAsync("bitcoin", MarketDataSource.CoinGecko, false, Token));
    }

    [Fact]
    public async Task ForcedRefreshHonorsCoinGeckoQuotaAndDoesNotReturnOldQuoteAsSuccess()
    {
        var clock = new Clock();
        var budget = new CoinGeckoRequestBudget(Options.Create(new CoinGeckoOptions { RequestsPerMinute = 1 }), clock);
        using var fixture = new Fixture((_, _) => Task.FromResult(Json(new { bitcoin = new { usd = 100m,
            last_updated_at = clock.GetUtcNow().ToUnixTimeSeconds() } })), clock, budget);
        await fixture.Quotes.GetAsync("bitcoin", MarketDataSource.CoinGecko, false, Token);
        var error = await Assert.ThrowsAsync<UpstreamRateLimitException>(() =>
            fixture.Quotes.GetAsync("bitcoin", MarketDataSource.CoinGecko, true, Token));
        Assert.Equal(TimeSpan.FromMinutes(1), error.RetryAfter);
    }

    [Fact]
    public async Task CancellationReleasesQuoteLock()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var fixture = new Fixture(async (_, token) =>
        {
            if (++calls == 1) { started.SetResult(); await Task.Delay(Timeout.Infinite, token); }
            return Json(new { symbol = "BTCUSDT", price = "100" });
        });
        using var cancel = new CancellationTokenSource();
        var first = fixture.Quotes.GetAsync("bitcoin", MarketDataSource.Binance, true, cancel.Token);
        await started.Task.WaitAsync(Token);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.Equal(100m, (await fixture.Quotes.GetAsync("bitcoin", MarketDataSource.Binance, true, Token)).Price);
    }

    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
        { Content = new StringContent(JsonSerializer.Serialize(value)) };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token);
    }
    private sealed class Clock : TimeProvider
    {
        private long _seconds;
        public override long TimestampFrequency => 1;
        public override long GetTimestamp() => _seconds;
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 9, 11, 0, 0, 0, TimeSpan.Zero).AddSeconds(_seconds);
        public void Advance(TimeSpan value) => _seconds += (long)value.TotalSeconds;
    }
    private sealed class Fixture : IDisposable
    {
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        private readonly HttpClient _client;
        public Clock Clock { get; }
        public CurrentQuoteService Quotes { get; }
        public Fixture(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send, Clock? clock = null,
            CoinGeckoRequestBudget? budget = null)
        {
            Clock = clock ?? new Clock();
            HttpMessageHandler handler = new Handler(send);
            if (budget is not null) handler = new CoinGeckoRateLimitHandler(budget, Clock) { InnerHandler = handler };
            _client = new HttpClient(handler);
            Quotes = new CurrentQuoteService(
                new BinanceSpotService(_client, _cache, NullLogger<BinanceSpotService>.Instance),
                new CoinGeckoService(_client, _cache, NullLogger<CoinGeckoService>.Instance),
                _cache, new MarketDataRequestLock(), Clock);
        }
        public void Dispose() { _client.Dispose(); _cache.Dispose(); }
    }
}
