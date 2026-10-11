namespace CryptoRiskAnalysis.API.Models;

/// <summary>Shared methodology identity, weights, and composite classification thresholds.</summary>
public static class RiskMethodology
{
    public const string Version = "2.0.0";
    public const decimal VolatilityWeight = 0.4m;
    public const decimal TrendWeight = 0.3m;
    public const decimal VolumeWeight = 0.3m;
    public const decimal MediumRiskThreshold = 30m;
    public const decimal HighRiskThreshold = 70m;
}
