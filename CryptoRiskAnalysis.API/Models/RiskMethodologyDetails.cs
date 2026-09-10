namespace CryptoRiskAnalysis.API.Models;

public class RiskMethodologyDetails
{
    public string Version { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public int ObservationCount { get; set; }
    public int ReturnCount { get; set; }
    public long PeriodStart { get; set; }
    public long PeriodEnd { get; set; }
    public decimal VolatilityWeight => 0.4m;
    public decimal TrendWeight => 0.3m;
    public decimal VolumeWeight => 0.3m;
    public decimal VolatilityContribution { get; set; }
    public decimal TrendContribution { get; set; }
    public decimal VolumeContribution { get; set; }
    public List<string> Warnings { get; set; } = new();
}
