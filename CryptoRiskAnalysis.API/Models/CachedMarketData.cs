using CryptoRiskAnalysis.API.Interfaces;

namespace CryptoRiskAnalysis.API.Models;

internal sealed record CachedMarketData(
    MarketDataSnapshot Data,
    IReadOnlyList<DailyMarketObservation> Observations)
{
    public Task PersistAsync(IHistoricalMarketDataStore? store, string assetId, MarketDataSource source,
        CancellationToken cancellationToken) =>
        store?.SaveAsync(assetId, source, Observations, cancellationToken) ?? Task.CompletedTask;
}
