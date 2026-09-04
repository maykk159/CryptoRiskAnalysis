using CryptoRiskAnalysis.API.DTOs;
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

            // Validate days parameter - only allow 7, 30, or 90
            if (days != 7 && days != 30 && days != 90)
            {
                _logger.LogWarning("Invalid days parameter: {Days}", days);
                return BadRequest(new ApiResponse<RiskAnalysisResponseDto>("Geçersiz gün parametresi. Yalnızca 7, 30 veya 90 gün kabul edilir."));
            }

            // FIX: Always fetch at least 30 days for trend/volatility calculation context
            // Trend score requires 30-day MA comparison, so 7 days is insufficient for calculation.
            int calculationDays = Math.Max(days, 30);

            // 1. Fetch ALL data in one call (optimized!)
            var (priceHistory, currentVolume, avgVolume) = await _cryptoDataService.GetAllMarketDataAsync(
                assetId,
                calculationDays,
                cancellationToken);
            
            if (priceHistory == null || !priceHistory.Any())
            {
                _logger.LogWarning("No data found for asset: {AssetId}", assetId);
                return NotFound(new ApiResponse<RiskAnalysisResponseDto>($"No data found for asset: {assetId}"));
            }

            // FIX: Use only the requested period for risk calculation
            // Keep the full dataset for trend calculation context (30-day MA)
            var calculationData = priceHistory.Count > days 
                ? priceHistory.TakeLast(days).ToList() 
                : priceHistory;

            // 2. Calculate Risk (100% local - no API calls!)
            var riskResult = _riskEngine.CalculateRisk(calculationData, currentVolume, avgVolume);

            // 3. Map to DTO
            var responseDto = new RiskAnalysisResponseDto(assetId, riskResult);

            // FIX: Filter the returned price history to match the requested days
            // If user asked for 7 days but we fetched 30 for calculation, only return the last 7 to chart.
            if (responseDto.PriceHistory.Count > days)
            {
                responseDto.PriceHistory = responseDto.PriceHistory
                    .TakeLast(days)
                    .ToList();
            }

            _logger.LogInformation("Successfully calculated risk for {AssetId}: Score {Score}. returning {Count} history points.", 
                assetId, riskResult.CompositeRiskScore, responseDto.PriceHistory.Count);

            return Ok(new ApiResponse<RiskAnalysisResponseDto>(responseDto));
        }
    }
}
