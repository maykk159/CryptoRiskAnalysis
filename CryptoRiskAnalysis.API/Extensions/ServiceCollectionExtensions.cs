using CryptoRiskAnalysis.API.Diagnostics;
using CryptoRiskAnalysis.API.Interfaces;
using CryptoRiskAnalysis.API.OpenApi;
using CryptoRiskAnalysis.API.Services;
using Microsoft.Extensions.Http.Resilience;

namespace CryptoRiskAnalysis.API.Extensions
{
    public static partial class ServiceCollectionExtensions
    {
        internal static IServiceCollection AddApiDocumentation(this IServiceCollection services)
        {
            services.AddEndpointsApiExplorer();
            return services.AddSwaggerGen(options => options.OperationFilter<ValidationProblemResponseFilter>());
        }

        public static IServiceCollection AddApplicationServices(
            this IServiceCollection services,
            Action<HttpStandardResilienceOptions>? configureResilience = null)
        {
            services.AddMetrics();
            services.AddSingleton<MarketDataMetrics>();
            AddMarketDataServices(services, configureResilience);
            services.AddScoped<IRiskEngine, RiskAnalysisEngine>();
            AddRiskAnalysisRateLimiting(services);
            return services;
        }
    }
}
