using CryptoRiskAnalysis.API.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace CryptoRiskAnalysis.API.Extensions;

internal static class OperationalHealthExtensions
{
    private const string ReadinessTag = "ready";

    internal static void AddOperationalHealthChecks(this IServiceCollection services) =>
        services.AddHealthChecks().AddCheck<HistoricalStoreHealthCheck>(
            "sqlite", tags: [ReadinessTag], timeout: TimeSpan.FromSeconds(5));

    internal static void MapOperationalHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadinessTag)
        });
    }
}
