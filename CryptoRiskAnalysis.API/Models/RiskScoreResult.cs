namespace CryptoRiskAnalysis.API.Models
{
    public class RiskScoreResult
    {
        // Existing risk scores (0-100 scale, dimensionless)
        public decimal VolatilityScore { get; set; }
        public decimal TrendScore { get; set; }
        public decimal VolumeScore { get; set; }
        public decimal CompositeRiskScore { get; set; }

        // Advanced financial metrics
        /// <summary>Annualized downside deviation as a percentage. E.g. 25.0 = 25 %.</summary>
        public decimal DownsideRisk { get; set; }
        /// <summary>Maximum peak-to-trough price decline as a percentage. E.g. 30.0 = 30 %.</summary>
        public decimal MaxDrawdown { get; set; }
        /// <summary>Annualized Sharpe ratio (dimensionless). Higher is better. Risk-free rate = 0.</summary>
        public decimal SharpeRatio { get; set; }
        /// <summary>Daily Value-at-Risk at 95 % confidence as a percentage. E.g. 5.0 = 5 % worst-case daily loss.</summary>
        public decimal ValueAtRisk95 { get; set; }
        /// <summary>Annualized volatility (log-return std dev × √365) as a percentage. E.g. 80.0 = 80 %.</summary>
        public decimal AnnualizedVolatility { get; set; }

        public List<PriceData> PriceHistory { get; set; } = new();
    }
}
