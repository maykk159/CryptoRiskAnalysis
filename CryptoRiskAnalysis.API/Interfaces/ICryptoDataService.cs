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
    }
}
