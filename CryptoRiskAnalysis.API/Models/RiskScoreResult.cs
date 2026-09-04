namespace CryptoRiskAnalysis.API.Models
{
    public class RiskScoreResult
    {
        // Existing risk scores (0-100 scale)
        public decimal VolatilityScore { get; set; }
        public decimal TrendScore { get; set; }
        public decimal VolumeScore { get; set; }
        public decimal CompositeRiskScore { get; set; }

        // ✨ NEW: Advanced financial metrics
        public decimal DownsideRisk { get; set; }           // Volatility of negative returns only (%)
        public decimal MaxDrawdown { get; set; }            // Maximum peak-to-trough decline (%)
        public decimal SharpeRatio { get; set; }            // Risk-adjusted return metric
        public decimal ValueAtRisk95 { get; set; }          // 95% confidence worst-case loss (%)
        public decimal AnnualizedVolatility { get; set; }   // Raw annualized volatility (%)

        public List<PriceData> PriceHistory { get; set; } = new();
    }
}
