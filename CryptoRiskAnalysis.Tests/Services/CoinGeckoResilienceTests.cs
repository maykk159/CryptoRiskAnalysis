using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CryptoRiskAnalysis.API.Exceptions;
using CryptoRiskAnalysis.API.Extensions;
using CryptoRiskAnalysis.API.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly.Timeout;

namespace CryptoRiskAnalysis.Tests.Services;

public class CoinGeckoResilienceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExpiredOrZeroRetryAfter_UsesTwoSecondCooldown(bool expiredDate)
    {
        var clock = new ManualClock();
        var budget = new CoinGeckoRequestBudget(Options.Create(new CoinGeckoOptions()), clock);
        var calls = 0;
        using var handler = new CoinGeckoRateLimitHandler(budget, clock)
        {
            InnerHandler = new StubHandler((_, _) =>
            {
                calls++;
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = expiredDate
                    ? new RetryConditionHeaderValue(clock.GetUtcNow().AddSeconds(-1))
                    : new RetryConditionHeaderValue(TimeSpan.Zero);
                return Task.FromResult(response);
            })
        };
        using var client = new HttpClient(handler);
        using var first = await client.GetAsync("https://example.test", TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<UpstreamRateLimitException>(() =>
            client.GetAsync("https://example.test/other", TestContext.Current.CancellationToken));
        Assert.Equal(TimeSpan.FromSeconds(2), error.RetryAfter);
        Assert.Equal(1, calls);
        clock.Advance(TimeSpan.FromSeconds(2));
        using var next = await client.GetAsync("https://example.test", TestContext.Current.CancellationToken);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ClientIdentifiesApplicationToProvider()
    {
        await using var provider = CreateProvider(new StubHandler((request, _) =>
        {
            Assert.Equal("CryptoRiskAnalysis/1.0", request.Headers.UserAgent.ToString());
            Assert.Contains(request.Headers.Accept, header => header.MediaType == "application/json");
            return Task.FromResult(Success());
        }));
        var result = await provider.GetRequiredService<CoinGeckoService>()
            .GetAllMarketDataAsync("bitcoin", 1, TestContext.Current.CancellationToken);
        Assert.Single(result.priceHistory);
    }

    [Fact]
    public void Budget_IsRollingAndCanceledCallsDoNotConsumePermits()
    {
        var clock = new ManualClock();
        var budget = new CoinGeckoRequestBudget(Options.Create(new CoinGeckoOptions { RequestsPerMinute = 1 }), clock);
        Assert.Throws<OperationCanceledException>(() => budget.Acquire(new CancellationToken(true)));
        budget.Acquire(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(59));
        var ex = Assert.Throws<UpstreamRateLimitException>(() => budget.Acquire(TestContext.Current.CancellationToken));
        Assert.Equal(TimeSpan.FromSeconds(1), ex.RetryAfter);
        clock.Advance(TimeSpan.FromSeconds(1));
        budget.Acquire(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetryAfterDeltaAndDate_BlockOtherRequests(bool useDate)
    {
        var clock = new ManualClock();
        var budget = new CoinGeckoRequestBudget(Options.Create(new CoinGeckoOptions()), clock);
        var calls = 0;
        using var handler = new CoinGeckoRateLimitHandler(budget, clock)
        {
            InnerHandler = new StubHandler((_, _) =>
            {
                calls++;
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = useDate
                    ? new RetryConditionHeaderValue(clock.GetUtcNow().AddSeconds(15))
                    : new RetryConditionHeaderValue(TimeSpan.FromSeconds(15));
                return Task.FromResult(response);
            })
        };
        using var client = new HttpClient(handler);
        using var response = await client.GetAsync("https://example.test", TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<UpstreamRateLimitException>(() => client.GetAsync("https://example.test/other", TestContext.Current.CancellationToken));
        Assert.Equal(1, calls);
        clock.Advance(TimeSpan.FromSeconds(15));
        using var next = await client.GetAsync("https://example.test", TestContext.Current.CancellationToken);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task EveryRetryConsumesSharedQuotaAcrossClientInstances()
    {
        var calls = 0;
        await using var provider = CreateProvider(new StubHandler((_, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway));
        }), budget: 2);
        await Assert.ThrowsAsync<UpstreamRateLimitException>(() => provider.GetRequiredService<CoinGeckoService>()
            .GetAllMarketDataAsync("bitcoin", 1, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<UpstreamRateLimitException>(() => provider.GetRequiredService<CoinGeckoService>()
            .GetAllMarketDataAsync("ethereum", 1, TestContext.Current.CancellationToken));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task RetryAfter_IsRespectedBeforeSuccessfulRetry()
    {
        var calls = 0;
        var elapsed = Stopwatch.StartNew();
        await using var provider = CreateProvider(new StubHandler((_, _) =>
        {
            calls++;
            if (calls > 1) return Task.FromResult(Success());
            elapsed.Restart();
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
            return Task.FromResult(response);
        }));
        var result = await provider.GetRequiredService<CoinGeckoService>().GetAllMarketDataAsync("bitcoin", 1, TestContext.Current.CancellationToken);
        Assert.Single(result.priceHistory);
        Assert.Equal(2, calls);
        Assert.True(elapsed.Elapsed >= TimeSpan.FromMilliseconds(950));
    }

    [Theory]
    [InlineData(404, 1)]
    [InlineData(400, 1)]
    [InlineData(500, 4)]
    public async Task OnlyTransientResponsesAreRetried(int status, int expectedCalls)
    {
        var calls = 0;
        await using var provider = CreateProvider(new StubHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status));
        }));
        var error = await Record.ExceptionAsync(() => provider.GetRequiredService<CoinGeckoService>().GetAllMarketDataAsync("bitcoin", 1, TestContext.Current.CancellationToken));
        Assert.NotNull(error);
        Assert.Equal(expectedCalls, calls);
    }

    [Fact]
    public async Task TotalTimeoutBoundsLongRetryAfter()
    {
        await using var provider = CreateProvider(new StubHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(5));
            return Task.FromResult(response);
        }), configure: o =>
        {
            o.AttemptTimeout.Timeout = TimeSpan.FromMilliseconds(100);
            o.TotalRequestTimeout.Timeout = TimeSpan.FromMilliseconds(250);
        });
        await Assert.ThrowsAsync<TimeoutRejectedException>(() => provider.GetRequiredService<CoinGeckoService>()
            .GetAllMarketDataAsync("bitcoin", 1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AttemptTimeoutAndCallerCancellationPropagate()
    {
        await using var provider = CreateProvider(new StubHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Success();
        }), configure: o =>
        {
            o.Retry.ShouldHandle = static _ => ValueTask.FromResult(false);
            o.AttemptTimeout.Timeout = TimeSpan.FromMilliseconds(100);
            o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(1);
        });
        var service = provider.GetRequiredService<CoinGeckoService>();
        await Assert.ThrowsAsync<TimeoutRejectedException>(() => service.GetAllMarketDataAsync("bitcoin", 1, TestContext.Current.CancellationToken));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAllMarketDataAsync("bitcoin", 1, canceled.Token));
    }

    private static ServiceProvider CreateProvider(HttpMessageHandler handler, int budget = 20,
        Action<HttpStandardResilienceOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationServices(o =>
        {
            o.Retry.Delay = TimeSpan.Zero;
            o.Retry.UseJitter = false;
            configure?.Invoke(o);
        });
        services.Configure<CoinGeckoOptions>(o => o.RequestsPerMinute = budget);
        services.AddHttpClient<CoinGeckoService>().ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SlowResponseBody_IsInsideAttemptTimeout()
    {
        await using var provider = CreateProvider(new StubHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new SlowContent() })), configure: o =>
        {
            o.Retry.ShouldHandle = static _ => ValueTask.FromResult(false);
            o.AttemptTimeout.Timeout = TimeSpan.FromMilliseconds(100);
            o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(1);
        });
        await Assert.ThrowsAsync<TimeoutRejectedException>(() => provider.GetRequiredService<CoinGeckoService>()
            .GetAllMarketDataAsync("bitcoin", 1, TestContext.Current.CancellationToken));
    }

    private sealed class SlowContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            throw new InvalidOperationException("Cancellation-aware overload must be used.");
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) =>
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }

    private static HttpResponseMessage Success()
    {
        var timestamp = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-1), TimeSpan.Zero).ToUnixTimeMilliseconds();
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                prices = new[] { new[] { (decimal)timestamp, 100m } },
                total_volumes = new[] { new[] { (decimal)timestamp, 1000m } }
            }))
        };
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }

    private sealed class ManualClock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_ticks);
        public void Advance(TimeSpan duration) => _ticks += duration.Ticks;
    }
}
