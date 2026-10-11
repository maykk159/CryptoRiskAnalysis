namespace CryptoRiskAnalysis.API.Models;

public class RiskMethodologyDetails
{
    public string Version { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public int ObservationCount { get; set; }
    public int ReturnCount { get; set; }
    public long PeriodStart { get; set; }
    public long PeriodEnd { get; set; }
    public decimal VolatilityWeight => RiskMethodology.VolatilityWeight;
    public decimal TrendWeight => RiskMethodology.TrendWeight;
    public decimal VolumeWeight => RiskMethodology.VolumeWeight;
    public decimal VolatilityContribution { get; set; }
    public decimal TrendContribution { get; set; }
    public decimal VolumeContribution { get; set; }
    public List<string> Warnings { get; set; } = new();
}
