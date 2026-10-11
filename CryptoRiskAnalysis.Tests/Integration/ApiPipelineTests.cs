using System.Net;
using System.Text;
using System.Text.Json;
using CryptoRiskAnalysis.API.Extensions;
using CryptoRiskAnalysis.API.Middleware;
using CryptoRiskAnalysis.API.Services;
using CryptoRiskAnalysis.API.Wrappers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CryptoRiskAnalysis.Tests.Integration;

public class ApiPipelineTests
{
    private static CryptoRiskAnalysis.API.Interfaces.ICurrentQuoteService CreateQuotes()
    {
        var quotes = new Mock<CryptoRiskAnalysis.API.Interfaces.ICurrentQuoteService>();
        quotes.Setup(q => q.GetAsync(It.IsAny<string>(), It.IsAny<CryptoRiskAnalysis.API.Models.MarketDataSource>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CryptoRiskAnalysis.API.Models.MarketQuote(100m, "Binance", "USDT", DateTimeOffset.UtcNow, null));
        return quotes.Object;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RiskController_InvalidEngineInputBecomesBadGateway(bool missingVolume)
    {
        await using var provider = CreateMiddlewareServices(Environments.Production);
        var history = Enumerable.Range(0, 7).Select(i => new CryptoRiskAnalysis.API.Models.PriceData
        {
            Timestamp = i * 86_400_000L,
            Price = 100m
        }).ToList();
        if (!missingVolume) history[^1].Timestamp += 86_400_000L;
        var service = new Mock<CryptoRiskAnalysis.API.Interfaces.ICryptoDataService>();
        service.Setup(s => s.GetAllMarketDataAsync("bitcoin", 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync((history, 100m, 1000m, missingVolume ? 0m : 1000m));
        var controller = new CryptoRiskAnalysis.API.Controllers.RiskAnalysisController(
            service.Object, new RiskAnalysisEngine(NullLogger<RiskAnalysisEngine>.Instance),
            NullLogger<CryptoRiskAnalysis.API.Controllers.RiskAnalysisController>.Instance, CreateQuotes());
        var application = new ApplicationBuilder(provider);
        application.UseMiddleware<ExceptionHandlingMiddleware>();
        application.Run(async context => { await controller.GetRiskAnalysis("bitcoin", 7, context.RequestAborted); });
        var context = CreateHttpContext(provider);
        await application.Build()(context);
        Assert.Equal(StatusCodes.Status502BadGateway, context.Response.StatusCode);
        Assert.False((await ReadResponseAsync(context)).Succeeded);
    }

    [Fact]
    public async Task RiskController_SerializesMethodologyAndUndefinedSharpeFromRealEngine()
    {
        var history = Enumerable.Range(0, 7).Select(i => new CryptoRiskAnalysis.API.Models.PriceData
        {
            Timestamp = i * 86_400_000L,
            Price = 100m
        }).ToList();
        var service = new Mock<CryptoRiskAnalysis.API.Interfaces.ICryptoDataService>();
        service.Setup(s => s.GetAllMarketDataAsync("bitcoin", 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync((history, 100m, 1000m, 1000m));
        var controller = new CryptoRiskAnalysis.API.Controllers.RiskAnalysisController(
            service.Object, new RiskAnalysisEngine(NullLogger<RiskAnalysisEngine>.Instance),
            NullLogger<CryptoRiskAnalysis.API.Controllers.RiskAnalysisController>.Instance, CreateQuotes());
        var response = await controller.GetRiskAnalysis("bitcoin", 7, TestContext.Current.CancellationToken);
        var ok = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(response.Result);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var data = json.RootElement.GetProperty("data");
        Assert.Equal(JsonValueKind.Null, data.GetProperty("sharpeRatio").ValueKind);
        Assert.Equal(9m, data.GetProperty("compositeRiskScore").GetDecimal());
        Assert.Equal("2.0.0", data.GetProperty("methodology").GetProperty("version").GetString());
        Assert.Equal(6, data.GetProperty("methodology").GetProperty("returnCount").GetInt32());
    }

    [Fact]
    public async Task ExceptionMiddleware_UsesJsonEnvelopeForUnhandledErrors()
    {
        await using var provider = CreateMiddlewareServices(Environments.Development);
        var application = new ApplicationBuilder(provider);
        application.UseMiddleware<ExceptionHandlingMiddleware>();
        application.Run(_ => throw new InvalidOperationException("integration failure"));
        var pipeline = application.Build();
        var context = CreateHttpContext(provider);

        await pipeline(context);
        var payload = await ReadResponseAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.False(payload.Succeeded);
        Assert.Equal("integration failure", payload.Message);
    }

    [Fact]
    public async Task MissingProviderSeries_IsRejectedAndMappedToBadGateway()
    {
        await using var provider = CreateMiddlewareServices(Environments.Production);
        var timestamp = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeMilliseconds();
        var incompletePayload = JsonSerializer.Serialize(new
        {
            prices = new object[][] { new object[] { timestamp, 100m } }
        });
        using var providerClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(incompletePayload, Encoding.UTF8, "application/json")
            })));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var marketDataService = new CoinGeckoService(
            providerClient,
            cache,
            NullLogger<CoinGeckoService>.Instance);
        var application = new ApplicationBuilder(provider);
        application.UseMiddleware<ExceptionHandlingMiddleware>();
        application.Run(async context =>
        {
            _ = await marketDataService.GetAllMarketDataAsync(
                "bitcoin", 1, context.RequestAborted);
        });
        var pipeline = application.Build();
        var context = CreateHttpContext(provider);

        await pipeline(context);
        var payload = await ReadResponseAsync(context);

        Assert.Equal(StatusCodes.Status502BadGateway, context.Response.StatusCode);
        Assert.False(payload.Succeeded);
        Assert.Equal("Market data provider is temporarily unavailable.", payload.Message);
        Assert.DoesNotContain("total_volumes", payload.Message);
    }

    [Fact]
    public async Task RateLimitMiddleware_ThirtyFirstRequestUsesJsonEnvelope()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationServices();
        await using var provider = services.BuildServiceProvider();
        var endpoint = new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new EnableRateLimitingAttribute("RiskAnalysis")),
            "risk-analysis-test-endpoint");
        var application = new ApplicationBuilder(provider);
        application.UseRateLimiter();
        application.Run(context => context.Response.WriteAsync("accepted"));
        var pipeline = application.Build();

        for (var requestNumber = 1; requestNumber <= 30; requestNumber++)
        {
            var acceptedContext = CreateHttpContext(provider, endpoint);
            await pipeline(acceptedContext);
            Assert.Equal(StatusCodes.Status200OK, acceptedContext.Response.StatusCode);
        }

        var rejectedContext = CreateHttpContext(provider, endpoint);
        await pipeline(rejectedContext);
        var payload = await ReadResponseAsync(rejectedContext);

        Assert.Equal(StatusCodes.Status429TooManyRequests, rejectedContext.Response.StatusCode);
        Assert.False(payload.Succeeded);
        Assert.Contains("Too many requests", payload.Message);
    }

    [Fact]
    public async Task RateLimitMiddleware_UsesForwardedClientIpFromTrustedProxy()
    {
        var proxyAddress = IPAddress.Parse("10.0.0.10");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReverseProxy:KnownProxies:0"] = proxyAddress.ToString()
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationServices();
        services.AddForwardedHeadersConfiguration(configuration);
        await using var provider = services.BuildServiceProvider();
        var endpoint = new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new EnableRateLimitingAttribute("RiskAnalysis")),
            "proxied-risk-analysis-test-endpoint");
        var application = new ApplicationBuilder(provider);
        application.UseForwardedHeaders();
        application.UseRateLimiter();
        application.Run(context => context.Response.WriteAsync("accepted"));
        var pipeline = application.Build();

        for (var requestNumber = 1; requestNumber <= 30; requestNumber++)
        {
            var firstClientContext = CreateProxiedHttpContext(
                provider,
                endpoint,
                proxyAddress,
                "198.51.100.10");
            await pipeline(firstClientContext);
            Assert.Equal(StatusCodes.Status200OK, firstClientContext.Response.StatusCode);
        }

        var secondClientContext = CreateProxiedHttpContext(
            provider,
            endpoint,
            proxyAddress,
            "198.51.100.11");
        await pipeline(secondClientContext);

        Assert.Equal(StatusCodes.Status200OK, secondClientContext.Response.StatusCode);
        Assert.Equal(IPAddress.Parse("198.51.100.11"), secondClientContext.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task ForwardedHeadersMiddleware_IgnoresUntrustedProxy()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReverseProxy:KnownProxies:0"] = "10.0.0.10"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddForwardedHeadersConfiguration(configuration);
        await using var provider = services.BuildServiceProvider();
        var application = new ApplicationBuilder(provider);
        application.UseForwardedHeaders();
        application.Run(_ => Task.CompletedTask);
        var pipeline = application.Build();
        var untrustedProxyAddress = IPAddress.Parse("10.0.0.20");
        var context = CreateProxiedHttpContext(
            provider,
            endpoint: null,
            untrustedProxyAddress,
            "198.51.100.99");

        await pipeline(context);

        Assert.Equal(untrustedProxyAddress, context.Connection.RemoteIpAddress);
    }

    private static ServiceProvider CreateMiddlewareServices(string environmentName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns(environmentName);
        services.AddSingleton(environment.Object);
        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext CreateHttpContext(
        IServiceProvider provider,
        Endpoint? endpoint = null)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = provider
        };
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Response.Body = new MemoryStream();
        if (endpoint is not null)
        {
            context.SetEndpoint(endpoint);
        }

        return context;
    }

    private static DefaultHttpContext CreateProxiedHttpContext(
        IServiceProvider provider,
        Endpoint? endpoint,
        IPAddress proxyAddress,
        string forwardedClientAddress)
    {
        var context = CreateHttpContext(provider, endpoint);
        context.Connection.RemoteIpAddress = proxyAddress;
        context.Request.Headers["X-Forwarded-For"] = forwardedClientAddress;
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        return context;
    }

    private static async Task<ApiResponse<string>> ReadResponseAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        var payload = await JsonSerializer.DeserializeAsync<ApiResponse<string>>(
            context.Response.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            TestContext.Current.CancellationToken);
        return Assert.IsType<ApiResponse<string>>(payload);
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
