using CryptoRiskAnalysis.API.Models;

namespace CryptoRiskAnalysis.API.Interfaces;

public interface ICurrentQuoteService
{
    Task<MarketQuote> GetAsync(string assetId, MarketDataSource source, bool refresh,
        CancellationToken cancellationToken);
}
