using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using CryptoRiskAnalysis.API.Extensions;
using CryptoRiskAnalysis.API.Exceptions;
using CryptoRiskAnalysis.API.Services;
using CryptoRiskAnalysis.API.Wrappers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace CryptoRiskAnalysis.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public async Task RateLimitRejection_UsesApiResponseEnvelope()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationServices();
        await using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        using var limiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = 1,
            QueueLimit = 0
        });
        using var acquiredLease = limiter.AttemptAcquire();
        using var rejectedLease = limiter.AttemptAcquire();

        Assert.NotNull(options.OnRejected);
        await options.OnRejected(
            new OnRejectedContext { HttpContext = httpContext, Lease = rejectedLease },
            TestContext.Current.CancellationToken);

        httpContext.Response.Body.Position = 0;
        var response = await JsonSerializer.DeserializeAsync<ApiResponse<string>>(
            httpContext.Response.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            TestContext.Current.CancellationToken);

        Assert.Equal((int)HttpStatusCode.TooManyRequests, httpContext.Response.StatusCode);
        Assert.StartsWith("application/json", httpContext.Response.ContentType);
        Assert.NotNull(response);
        Assert.False(response.Succeeded);
        Assert.Contains("Too many requests", response.Message);
    }

    [Fact]
    public async Task BinanceClient_RetriesTransientResponses()
    {
        var providerCalls = 0;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            providerCalls++;
            return Task.FromResult(providerCalls < 3
                ? new HttpResponseMessage(HttpStatusCode.BadGateway)
                : JsonResponse(CreateBinancePayload()));
        });
        var services = CreateServices(handler, options =>
        {
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.Zero;
            options.Retry.UseJitter = false;
        });

        await using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<BinanceSpotService>();

        var result = await service.GetAllMarketDataAsync(
            "bitcoin", 1, TestContext.Current.CancellationToken);

        Assert.Single(result.priceHistory);
        Assert.Equal(3, providerCalls);
    }

    [Fact]
    public async Task BinanceClient_AttemptTimeout_ThrowsTimeoutRejectedException()
    {
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var services = CreateServices(handler, options =>
        {
            options.Retry.MaxRetryAttempts = 1;
            options.Retry.ShouldHandle = static _ => ValueTask.FromResult(false);
            options.AttemptTimeout.Timeout = TimeSpan.FromMilliseconds(50);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(1);
        });

        await using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<BinanceSpotService>();

        await Assert.ThrowsAsync<TimeoutRejectedException>(() =>
            service.GetAllMarketDataAsync(
                "bitcoin", 1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BinanceClient_RepeatedFailures_OpenCircuit()
    {
        var providerCalls = 0;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            providerCalls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        });
        var services = CreateServices(handler, options =>
        {
            options.Retry.MaxRetryAttempts = 1;
            options.Retry.ShouldHandle = static _ => ValueTask.FromResult(false);
            options.AttemptTimeout.Timeout = TimeSpan.FromMilliseconds(100);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(1);
            options.CircuitBreaker.FailureRatio = 1;
            options.CircuitBreaker.MinimumThroughput = 2;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(1);
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(1);
        });

        await using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<BinanceSpotService>();

        await Assert.ThrowsAsync<MarketDataProviderException>(() =>
            service.GetAllMarketDataAsync(
                "bitcoin", 1, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<MarketDataProviderException>(() =>
            service.GetAllMarketDataAsync(
                "bitcoin", 1, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<BrokenCircuitException>(() =>
            service.GetAllMarketDataAsync(
                "bitcoin", 1, TestContext.Current.CancellationToken));

        Assert.Equal(2, providerCalls);
    }

    private static ServiceCollection CreateServices(
        HttpMessageHandler handler,
        Action<HttpStandardResilienceOptions> configureResilience)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationServices(configureResilience);
        services.AddHttpClient<BinanceSpotService>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        return services;
    }

    private static string CreateBinancePayload()
    {
        var closeTime = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds();
        var openTime = closeTime - (long)TimeSpan.FromDays(1).TotalMilliseconds;
        return JsonSerializer.Serialize(new object[][]
        {
            new object[] { openTime, "100", "110", "90", "105", "1000", closeTime, "105000" }
        });
    }

    private static HttpResponseMessage JsonResponse(string content)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
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
