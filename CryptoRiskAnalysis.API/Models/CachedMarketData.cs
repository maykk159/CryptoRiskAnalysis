using CryptoRiskAnalysis.API.Interfaces;

namespace CryptoRiskAnalysis.API.Models;

internal sealed record CachedMarketData(
    MarketDataSnapshot Data,
    IReadOnlyList<DailyMarketObservation> Observations)
{
    public static CachedMarketData Create(MarketDataSnapshot data,
        IReadOnlyList<(long Timestamp, decimal Volume)> volumes) =>
        new(data, data.priceHistory.Select((price, index) => new DailyMarketObservation(
            DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(price.Timestamp).UtcDateTime),
            price.Timestamp, price.Price, volumes[index].Volume)).ToList());

    public Task PersistAsync(IHistoricalMarketDataStore? store, string assetId, MarketDataSource source,
        CancellationToken cancellationToken) =>
        store?.SaveAsync(assetId, source, Observations, cancellationToken) ?? Task.CompletedTask;
}
