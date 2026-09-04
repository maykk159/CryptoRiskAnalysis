using CryptoRiskAnalysis.API.Models;

namespace CryptoRiskAnalysis.API.DTOs
{
    public class RiskAnalysisResponseDto
    {
        public string AssetId { get; set; } = string.Empty;

        // Existing risk scores (0-100 scale)
        public decimal CompositeRiskScore { get; set; }
        public decimal VolatilityScore { get; set; }
        public decimal TrendScore { get; set; }
        public decimal VolumeScore { get; set; }
        public RiskAnalysisResponseDto() { }

        public RiskAnalysisResponseDto(string assetId, RiskScoreResult riskResult)
        {
            AssetId = assetId;
            CompositeRiskScore = riskResult.CompositeRiskScore;
            VolatilityScore = riskResult.VolatilityScore;
            TrendScore = riskResult.TrendScore;
            VolumeScore = riskResult.VolumeScore;
            DownsideRisk = riskResult.DownsideRisk;
            MaxDrawdown = riskResult.MaxDrawdown;
            SharpeRatio = riskResult.SharpeRatio;
            ValueAtRisk95 = riskResult.ValueAtRisk95;
            AnnualizedVolatility = riskResult.AnnualizedVolatility;
            PriceHistory = riskResult.PriceHistory;
        }

        // ✨ NEW: Advanced financial metrics
        public decimal DownsideRisk { get; set; }
        public decimal MaxDrawdown { get; set; }
        public decimal SharpeRatio { get; set; }
        public decimal ValueAtRisk95 { get; set; }
        public decimal AnnualizedVolatility { get; set; }

        public List<PriceData> PriceHistory { get; set; } = new();
    }
}
