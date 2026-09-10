using CryptoRiskAnalysis.API.DTOs;
using CryptoRiskAnalysis.API.Exceptions;
using CryptoRiskAnalysis.API.Interfaces;
using CryptoRiskAnalysis.API.Wrappers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CryptoRiskAnalysis.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableRateLimiting("RiskAnalysis")]
    public class RiskAnalysisController : ControllerBase
    {
        private readonly ICryptoDataService _cryptoDataService;
        private readonly IRiskEngine _riskEngine;
        private readonly ILogger<RiskAnalysisController> _logger;

        public RiskAnalysisController(
            ICryptoDataService cryptoDataService,
            IRiskEngine riskEngine,
            ILogger<RiskAnalysisController> logger)
        {
            _cryptoDataService = cryptoDataService;
            _riskEngine = riskEngine;
            _logger = logger;
        }

        [HttpGet("{assetId}")]
        public async Task<ActionResult<ApiResponse<RiskAnalysisResponseDto>>> GetRiskAnalysis(
            string assetId,
            [FromQuery] int days = 30,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Received risk analysis request for {AssetId} over {Days} days", assetId, days);

            if (string.IsNullOrWhiteSpace(assetId))
            {
                _logger.LogWarning("Risk analysis request received with empty or null assetId.");
                return BadRequest(new ApiResponse<RiskAnalysisResponseDto>("Asset ID boş olamaz."));
            }

            if (assetId.Length > 200)
            {
                _logger.LogWarning("Risk analysis request received with oversized assetId (length={Length}).", assetId.Length);
                return BadRequest(new ApiResponse<RiskAnalysisResponseDto>("Asset ID çok uzun."));
            }

            // Validate days parameter - only allow 7, 30, or 90
            if (days != 7 && days != 30 && days != 90)
            {
                _logger.LogWarning("Invalid days parameter: {Days}", days);
                return BadRequest(new ApiResponse<RiskAnalysisResponseDto>("Geçersiz gün parametresi. Yalnızca 7, 30 veya 90 gün kabul edilir."));
            }

            // 1. Fetch ALL data in one call (optimized!)
            var (priceHistory, currentPrice, currentVolume, avgVolume) = await _cryptoDataService.GetAllMarketDataAsync(
                assetId,
                days,
                cancellationToken);

            if (priceHistory == null || !priceHistory.Any())
            {
                _logger.LogWarning("No data found for asset: {AssetId}", assetId);
                return NotFound(new ApiResponse<RiskAnalysisResponseDto>($"No data found for asset: {assetId}"));
            }

            if (priceHistory.Count != days)
            {
                _logger.LogWarning(
                    "Unexpected market data count for {AssetId}: expected {ExpectedCount}, received {ActualCount}",
                    assetId,
                    days,
                    priceHistory.Count);
                throw new MarketDataProviderException(
                    "Market data service",
                    $"expected exactly {days} daily observations but received {priceHistory.Count}.");
            }

            // 2. Calculate Risk (100% local - no API calls!)
            Models.RiskScoreResult riskResult;
            try
            {
                riskResult = _riskEngine.CalculateRisk(priceHistory, currentVolume, avgVolume);
            }
            catch (ArgumentException ex)
            {
                throw new MarketDataProviderException("Market data service", "data is unsuitable for daily risk analysis.", ex);
            }

            // 3. Map to DTO
            var responseDto = new RiskAnalysisResponseDto(assetId, currentPrice, riskResult);

            _logger.LogInformation("Successfully calculated risk for {AssetId}: Score {Score}. returning {Count} history points.",
                assetId, riskResult.CompositeRiskScore, responseDto.PriceHistory.Count);

            return Ok(new ApiResponse<RiskAnalysisResponseDto>(responseDto));
        }
    }
}
