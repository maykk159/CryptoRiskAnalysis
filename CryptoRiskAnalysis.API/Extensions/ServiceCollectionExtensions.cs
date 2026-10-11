using CryptoRiskAnalysis.API.Interfaces;
using CryptoRiskAnalysis.API.Services;
using Microsoft.Extensions.Http.Resilience;

namespace CryptoRiskAnalysis.API.Extensions
{
    public static partial class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(
            this IServiceCollection services,
            Action<HttpStandardResilienceOptions>? configureResilience = null)
        {
            AddMarketDataServices(services, configureResilience);
            services.AddScoped<IRiskEngine, RiskAnalysisEngine>();
            AddRiskAnalysisRateLimiting(services);
            return services;
        }
    }
}
