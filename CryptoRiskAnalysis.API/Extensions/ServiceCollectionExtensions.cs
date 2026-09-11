using CryptoRiskAnalysis.API.Interfaces;
using CryptoRiskAnalysis.API.Services;
using CryptoRiskAnalysis.API.Wrappers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Http.Resilience;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Polly;

namespace CryptoRiskAnalysis.API.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddHttpsRedirectionConfiguration(
            this IServiceCollection services, IConfiguration configuration)
        {
            return services.AddHttpsRedirection(options =>
            {
                options.HttpsPort = configuration.GetValue<int?>("HttpsRedirection:HttpsPort") ?? 443;
                if (options.HttpsPort is < 1 or > 65535)
                    throw new InvalidOperationException("HttpsRedirection:HttpsPort must be between 1 and 65535.");
                options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
            });
        }

        public static IServiceCollection AddForwardedHeadersConfiguration(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var reverseProxySection = configuration.GetSection("ReverseProxy");

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

                var forwardLimit = reverseProxySection.GetValue<int?>("ForwardLimit") ?? 1;
                if (forwardLimit < 1)
                {
                    throw new InvalidOperationException(
                        "ReverseProxy:ForwardLimit must be greater than zero.");
                }

                options.ForwardLimit = forwardLimit;

                foreach (var proxy in reverseProxySection
                    .GetSection("KnownProxies")
                    .Get<string[]>() ?? [])
                {
                    if (!IPAddress.TryParse(proxy, out var address))
                    {
                        throw new InvalidOperationException(
                            $"ReverseProxy:KnownProxies contains an invalid IP address: '{proxy}'.");
                    }

                    options.KnownProxies.Add(address);
                }

                foreach (var network in reverseProxySection
                    .GetSection("KnownNetworks")
                    .Get<string[]>() ?? [])
                {
                    if (!System.Net.IPNetwork.TryParse(network, out var addressRange))
                    {
                        throw new InvalidOperationException(
                            $"ReverseProxy:KnownNetworks contains an invalid CIDR range: '{network}'.");
                    }

                    options.KnownIPNetworks.Add(addressRange);
                }
            });

            return services;
        }

        public static IServiceCollection AddApplicationServices(
            this IServiceCollection services,
            Action<HttpStandardResilienceOptions>? configureResilience = null)
        {
            void ConfigureResilience(HttpStandardResilienceOptions options)
            {
                ConfigureMarketDataResilience(options);
                configureResilience?.Invoke(options);
            }

            // Add Memory Cache
            services.AddMemoryCache();
            services.AddSingleton<MarketDataRequestLock>();
            services.TryAddSingleton(TimeProvider.System);
            services.AddOptions<CoinGeckoOptions>()
                .Validate(o => o.RequestsPerMinute > 0 && o.RequestsPerMinute <= 10000,
                    "CoinGecko:RequestsPerMinute must be between 1 and 10000.")
                .ValidateOnStart();
            services.AddSingleton<CoinGeckoRequestBudget>();
            services.AddTransient<CoinGeckoRateLimitHandler>();

            // Retry transient failures and 429 responses up to three times with
            // exponential backoff, with per-attempt and total request timeouts.
            services.AddHttpClient<BinanceSpotService>()
                .AddStandardResilienceHandler(ConfigureResilience);

            var coinGeckoClient = services.AddHttpClient<CoinGeckoService>(client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("CryptoRiskAnalysis/1.0");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            });
            coinGeckoClient.AddStandardResilienceHandler(ConfigureResilience);
            coinGeckoClient.AddHttpMessageHandler<CoinGeckoRateLimitHandler>();

            // Register HybridCryptoDataService as the single implementation of ICryptoDataService
            services.AddScoped<ICryptoDataService, HybridCryptoDataService>();
            services.AddScoped<ICurrentQuoteService, CurrentQuoteService>();

            // Register Risk Engine
            services.AddScoped<IRiskEngine, RiskAnalysisEngine>();

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = async (context, cancellationToken) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.HttpContext.Response.ContentType = "application/json";
                    var response = new ApiResponse<string>("Too many requests. Please try again later.");
                    await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken);
                };
                options.AddPolicy("RiskAnalysis", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: GetClientIpAddress(context),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 30,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));
            });

            return services;
        }

        private static string GetClientIpAddress(HttpContext context)
        {
            var address = context.Connection.RemoteIpAddress;
            if (address is null)
            {
                return "unknown";
            }

            return address.IsIPv4MappedToIPv6
                ? address.MapToIPv4().ToString()
                : address.ToString();
        }

        public static IServiceCollection AddCorsConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            services.AddCors(options =>
            {
                options.AddPolicy("AllowReactApp",
                    builder => builder.WithOrigins(origins)
                                      .AllowAnyMethod()
                                      .AllowAnyHeader());
            });

            return services;
        }

        /// <summary>
        /// Configures the standard HTTP resilience pipeline for market-data providers.
        /// The standard retry strategy handles network failures, timeouts, 5xx, 408, and 429 responses.
        /// </summary>
        private static void ConfigureMarketDataResilience(HttpStandardResilienceOptions options)
        {
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.Delay = TimeSpan.FromSeconds(2);
            options.Retry.BackoffType = DelayBackoffType.Exponential;
            options.Retry.UseJitter = true;
            options.Retry.ShouldRetryAfterHeader = true;
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.MinimumThroughput = 5;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
        }
    }
}
