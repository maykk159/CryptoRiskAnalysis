using CryptoRiskAnalysis.API.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CryptoRiskAnalysis.API.Diagnostics;

internal sealed class HistoricalStoreHealthCheck(IServiceProvider services) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        // Resolving the store here lets the health service report constructor and database failures as unhealthy.
        var store = services.GetRequiredService<IHistoricalMarketDataStore>();
        await store.CheckAccessAsync(cancellationToken);
        return HealthCheckResult.Healthy();
    }
}
