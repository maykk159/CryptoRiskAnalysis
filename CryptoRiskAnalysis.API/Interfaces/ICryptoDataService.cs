using CryptoRiskAnalysis.API.Models;

namespace CryptoRiskAnalysis.API.Interfaces
{
    public interface ICryptoDataService
    {
        // Optimized: Single API call for all data
        Task<(List<PriceData> priceHistory, decimal currentVolume, decimal avgVolume)> GetAllMarketDataAsync(
            string assetId,
            int days,
            CancellationToken cancellationToken = default);
        
        // Legacy methods (kept for backward compatibility)
        Task<List<PriceData>> GetHistoricalPriceDataAsync(string assetId, int days, CancellationToken cancellationToken = default);
        Task<decimal> GetCurrentVolumeAsync(string assetId, CancellationToken cancellationToken = default);
        Task<decimal> GetAverageVolumeAsync(string assetId, int days, CancellationToken cancellationToken = default);
    }
}
