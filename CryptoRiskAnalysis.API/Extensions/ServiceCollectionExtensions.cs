using CryptoRiskAnalysis.API.Interfaces;
using CryptoRiskAnalysis.API.Services;
using CryptoRiskAnalysis.API.Wrappers;
using Microsoft.Extensions.Http.Resilience;
using System.Threading.RateLimiting;

namespace CryptoRiskAnalysis.API.Extensions
{
    public static class ServiceCollectionExtensions
    {
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

            // Retry transient failures and 429 responses up to three times with
            // exponential backoff, with per-attempt and total request timeouts.
            services.AddHttpClient<BinanceSpotService>()
                .AddStandardResilienceHandler(ConfigureResilience);

            services.AddHttpClient<CoinGeckoService>()
                .AddStandardResilienceHandler(ConfigureResilience);

            // Register HybridCryptoDataService as the single implementation of ICryptoDataService
            services.AddScoped<ICryptoDataService, HybridCryptoDataService>();

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
                        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
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

        public static IServiceCollection AddCorsConfiguration(this IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddPolicy("AllowReactApp",
                    builder => builder.WithOrigins("http://localhost:5173", "http://localhost:5174")
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
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
            options.CircuitBreaker.FailureRatio = 1.0;
            options.CircuitBreaker.MinimumThroughput = 5;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
        }
    }
}
