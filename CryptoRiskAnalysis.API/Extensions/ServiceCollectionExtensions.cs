using CryptoRiskAnalysis.API.Interfaces;
using CryptoRiskAnalysis.API.Services;
using Polly;
using Polly.Extensions.Http;
using System.Threading.RateLimiting;

namespace CryptoRiskAnalysis.API.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // Add Memory Cache
            services.AddMemoryCache();

            // Register API Services with HttpClient + Polly retry policy
            // Retries up to 3 times on transient errors (5xx, network) and 429 with exponential backoff
            services.AddHttpClient<BinanceSpotService>(client =>
                client.Timeout = TimeSpan.FromSeconds(30))
                .AddPolicyHandler(GetRetryPolicy())
                .AddPolicyHandler(GetCircuitBreakerPolicy())
                .AddPolicyHandler(GetTimeoutPolicy());

            services.AddHttpClient<CoinGeckoService>(client =>
                client.Timeout = TimeSpan.FromSeconds(30))
                .AddPolicyHandler(GetRetryPolicy())
                .AddPolicyHandler(GetCircuitBreakerPolicy())
                .AddPolicyHandler(GetTimeoutPolicy());

            // Register HybridCryptoDataService as the single implementation of ICryptoDataService
            services.AddScoped<ICryptoDataService, HybridCryptoDataService>();

            // Register Risk Engine
            services.AddScoped<IRiskEngine, RiskAnalysisEngine>();

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
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
        /// Polly retry policy: retries up to 3 times with exponential backoff (2s → 4s → 8s)
        /// on transient HTTP errors (5xx, network failures) and 429 TooManyRequests.
        /// This replaces the manual for-loop retry logic that was in each service.
        /// </summary>
        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError() // HttpRequestException, 5xx
                .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests) // 429
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                );
        }

        private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30));
        }

        private static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy()
        {
            return Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(10));
        }
    }
}
