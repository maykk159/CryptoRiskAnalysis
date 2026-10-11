using CryptoRiskAnalysis.API.Interfaces;
using CryptoRiskAnalysis.API.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace CryptoRiskAnalysis.API.Extensions
{
    public static partial class ServiceCollectionExtensions
    {
        private static void AddMarketDataServices(
            IServiceCollection services, Action<HttpStandardResilienceOptions>? configureResilience)
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
        }

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
