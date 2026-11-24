using System.Diagnostics.Metrics;
using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using CryptoRiskAnalysis.API.Diagnostics;
using CryptoRiskAnalysis.API.Exceptions;
using CryptoRiskAnalysis.API.Extensions;
using CryptoRiskAnalysis.API.Interfaces;
using CryptoRiskAnalysis.API.Models;
using CryptoRiskAnalysis.API.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CryptoRiskAnalysis.Tests.Integration;

public class OperationalDiagnosticsTests
{
    [Theory]
    [InlineData("healthy", 200)]
    [InlineData("invalid-database", 503)]
    [InlineData("invalid-directory", 503)]
    public async Task ReadinessReflectsSqliteAccessWhileLivenessStaysHealthy(string scenario, int status)
    {
        var directory = Directory.CreateTempSubdirectory("crypto-health-");
        try
        {
            var databasePath = Path.Combine(directory.FullName, "market-history.db");
            if (scenario == "invalid-database")
                await File.WriteAllTextAsync(databasePath, "not a SQLite database", TestContext.Current.CancellationToken);
            if (scenario == "invalid-directory")
            {
                var invalidParent = Path.Combine(directory.FullName, "file");
                await File.WriteAllTextAsync(invalidParent, "not a directory", TestContext.Current.CancellationToken);
                databasePath = Path.Combine(invalidParent, "market-history.db");
            }

            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.Services.AddSingleton<IHistoricalMarketDataStore>(_ => new SqliteHistoricalMarketDataStore(databasePath));
            builder.Services.AddOperationalHealthChecks();
            await using var host = builder.Build();
            var application = new ApplicationBuilder(host.Services);
            application.UseRouting();
            application.UseEndpoints(endpoints => endpoints.MapOperationalHealthChecks());
            var pipeline = application.Build();

            Assert.Equal((200, "Healthy"), await GetProbeResponse(pipeline, host.Services, "/health/live"));
            if (scenario == "healthy") Assert.False(File.Exists(databasePath));
            Assert.Equal((status, status == 200 ? "Healthy" : "Unhealthy"),
                await GetProbeResponse(pipeline, host.Services, "/health/ready"));
            if (scenario == "healthy") Assert.True(File.Exists(databasePath));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task RecordsProviderDurationsRateLimitsAndFallbackFromApplicationPipeline()
    {
        var now = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
        var timestamp = now.AddDays(-1).ToUnixTimeMilliseconds();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FixedUtcClock(now));
        services.AddApplicationServices(options => options.Retry.ShouldHandle = _ => ValueTask.FromResult(false));
        services.Configure<CoinGeckoOptions>(options => options.RequestsPerMinute = 1);
        services.AddHttpClient<BinanceSpotService>().ConfigurePrimaryHttpMessageHandler(() => new StubHandler(() =>
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)));
        services.AddHttpClient<CoinGeckoService>().ConfigurePrimaryHttpMessageHandler(() => new StubHandler(() =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    prices = new[] { new[] { (decimal)timestamp, 100m } },
                    total_volumes = new[] { new[] { (decimal)timestamp, 1000m } }
                }))
            }));
        await using var provider = services.BuildServiceProvider();
        var meter = provider.GetRequiredService<IMeterFactory>().Create(MarketDataMetrics.MeterName);
        var durations = new List<(string Name, double Seconds, KeyValuePair<string, object?>[] Tags)>();
        var counters = new List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, activeListener) =>
        {
            if (ReferenceEquals(instrument.Meter, meter)) activeListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            durations.Add((instrument.Name, value, tags.ToArray())));
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            counters.Add((instrument.Name, value, tags.ToArray())));
        listener.Start();
        var token = TestContext.Current.CancellationToken;
        await using var scope = provider.CreateAsyncScope();

        var service = scope.ServiceProvider.GetRequiredService<ICryptoDataService>();
        Assert.Equal(MarketDataSource.CoinGecko, (await service.GetAllMarketDataAsync("bitcoin", 1, token)).Source);
        await Assert.ThrowsAsync<UpstreamRateLimitException>(() =>
            scope.ServiceProvider.GetRequiredService<CoinGeckoService>().GetAllMarketDataAsync("ethereum", 1, token));

        using var limiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions { PermitLimit = 1, QueueLimit = 0 });
        using var acquired = limiter.AttemptAcquire();
        using var rejected = limiter.AttemptAcquire();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        await provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value.OnRejected!(
            new OnRejectedContext { HttpContext = context, Lease = rejected }, token);

        Assert.Equal(new object?[] { "Binance", "CoinGecko" }, durations.Select(item => Assert.Single(item.Tags).Value));
        Assert.All(durations, item =>
        {
            Assert.Equal(MarketDataMetrics.ProviderDurationName, item.Name);
            Assert.True(item.Seconds >= 0);
        });
        var rejections = counters.Where(item => item.Name == MarketDataMetrics.RateLimitRejectionsName).ToList();
        Assert.Equal(new object?[] { "provider", "provider_budget", "api" },
            rejections.Select(item => item.Tags.Single(tag => tag.Key == "scope").Value));
        Assert.All(rejections, item => Assert.Equal(1, item.Value));
        var fallback = Assert.Single(counters, item => item.Name == MarketDataMetrics.FallbacksName);
        Assert.Equal(1, fallback.Value);
        Assert.Contains(fallback.Tags, tag => tag.Key == "from_provider" && Equals(tag.Value, "Binance"));
        Assert.Contains(fallback.Tags, tag => tag.Key == "to_provider" && Equals(tag.Value, "CoinGecko"));
    }

    private static async Task<(int Status, string Body)> GetProbeResponse(
        RequestDelegate pipeline, IServiceProvider services, string path)
    {
        await using var scope = services.CreateAsyncScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.RequestAborted = TestContext.Current.CancellationToken;
        await using var body = new MemoryStream();
        context.Response.Body = body;
        await pipeline(context);
        body.Position = 0;
        using var reader = new StreamReader(body);
        return (context.Response.StatusCode, await reader.ReadToEndAsync(context.RequestAborted));
    }

    private sealed class StubHandler(Func<HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response());
    }

    private sealed class FixedUtcClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
