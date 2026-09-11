using CryptoRiskAnalysis.API.Models;

namespace CryptoRiskAnalysis.API.Interfaces;

public interface IHistoricalMarketDataStore
{
    Task SaveAsync(string assetId, MarketDataSource source, IReadOnlyList<DailyMarketObservation> observations, CancellationToken cancellationToken);
    Task<IReadOnlyList<DailyMarketObservation>> ReadAsync(string assetId, MarketDataSource source, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
