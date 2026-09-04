using CryptoRiskAnalysis.API.Interfaces;
using CryptoRiskAnalysis.API.Models;
using CryptoRiskAnalysis.API.Exceptions;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace CryptoRiskAnalysis.API.Services
{
    /// <summary>
    /// Hybrid service that uses Binance for better rate limits and fresh data,
    /// with automatic fallback to CoinGecko for unsupported assets
    /// </summary>
    public class HybridCryptoDataService : ICryptoDataService
    {
        private readonly BinanceSpotService _binanceService;
        private readonly CoinGeckoService _coinGeckoService;
        private readonly ILogger<HybridCryptoDataService> _logger;

        public HybridCryptoDataService(
            BinanceSpotService binanceService,
            CoinGeckoService coinGeckoService,
            ILogger<HybridCryptoDataService> logger)
        {
            _binanceService = binanceService;
            _coinGeckoService = coinGeckoService;
            _logger = logger;
        }

        /// <summary>
        /// Fetches market data with smart routing:
        /// 1. Try Binance first (1-min cache, 1200 req/min limit)
        /// 2. Fallback to CoinGecko if needed (3-min cache, ~10-50 req/min limit)
        /// </summary>
        public async Task<(List<PriceData> priceHistory, decimal currentVolume, decimal avgVolume)> GetAllMarketDataAsync(
            string assetId,
            int days,
            CancellationToken cancellationToken = default)
        {
            // Try Binance first for supported assets
            if (BinanceSymbolMapper.IsAvailableOnBinance(assetId))
            {
                try
                {
                    _logger.LogInformation("Attempting Binance for {AssetId}", assetId);
                    return await _binanceService.GetAllMarketDataAsync(assetId, days, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (ex is MarketDataProviderException
                                               or UpstreamRateLimitException
                                               or TimeoutRejectedException
                                               or BrokenCircuitException)
                {
                    _logger.LogWarning(ex, "Binance failed for {AssetId}, falling back to CoinGecko", assetId);
                }
            }
            else
            {
                _logger.LogInformation("{AssetId} not available on Binance, using CoinGecko", assetId);
            }

            // Fallback to CoinGecko
            return await _coinGeckoService.GetAllMarketDataAsync(assetId, days, cancellationToken);
        }

        // Legacy interface methods - delegate to GetAllMarketDataAsync
        public async Task<List<PriceData>> GetHistoricalPriceDataAsync(
            string assetId,
            int days,
            CancellationToken cancellationToken = default)
        {
            var (priceHistory, _, _) = await GetAllMarketDataAsync(assetId, days, cancellationToken);
            return priceHistory;
        }

        public async Task<decimal> GetCurrentVolumeAsync(string assetId, CancellationToken cancellationToken = default)
        {
            var (_, currentVolume, _) = await GetAllMarketDataAsync(assetId, 1, cancellationToken);
            return currentVolume;
        }

        public async Task<decimal> GetAverageVolumeAsync(
            string assetId,
            int days,
            CancellationToken cancellationToken = default)
        {
            var (_, _, avgVolume) = await GetAllMarketDataAsync(assetId, days, cancellationToken);
            return avgVolume;
        }
    }
}
