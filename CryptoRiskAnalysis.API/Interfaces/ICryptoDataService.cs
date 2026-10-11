using CryptoRiskAnalysis.API.Models;

namespace CryptoRiskAnalysis.API.Interfaces
{
    public interface ICryptoDataService
    {
        // Volume values are daily quote-currency turnover (USD/USDT). Risk calculations
        // compare current turnover with the same provider's historical average, avoiding
        // comparisons of absolute venue volume with market-wide volume.
        Task<MarketDataSnapshot> GetAllMarketDataAsync(
            string assetId,
            int days,
            CancellationToken cancellationToken = default);
    }
}
