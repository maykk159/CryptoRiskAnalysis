using CryptoRiskAnalysis.API.Interfaces;
using CryptoRiskAnalysis.API.Models;

namespace CryptoRiskAnalysis.API.Services;

/// <summary>Deterministic daily indicators; see docs/risk-methodology.md.</summary>
public class RiskAnalysisEngine(ILogger<RiskAnalysisEngine> logger) : IRiskEngine
{
    public const string MethodologyVersion = "2.0.0";
    private static readonly double Annualization = Math.Sqrt(365);

    public RiskScoreResult CalculateRisk(List<PriceData> priceHistory, decimal currentVolume, decimal averageVolume)
    {
        ArgumentNullException.ThrowIfNull(priceHistory);
        ValidateInputs(priceHistory, currentVolume, averageVolume);
        var history = priceHistory.Select(p => new PriceData { Timestamp = p.Timestamp, Price = p.Price }).ToList();
        var prices = history.Select(p => p.Price).ToList();
        var returns = new List<double>(prices.Count - 1);
        for (var i = 1; i < prices.Count; i++)
            returns.Add(Math.Log((double)prices[i] / (double)prices[i - 1]));

        var mean = returns.Average();
        var stdDev = Math.Sqrt(returns.Sum(r => (r - mean) * (r - mean)) / (returns.Count - 1));
        var annualizedVolatility = (decimal)(stdDev * Annualization);
        var volatilityScore = annualizedVolatility switch
        {
            < 0.5m => annualizedVolatility * 100m,
            < 1m => 50m + (annualizedVolatility - 0.5m) * 50m,
            _ => Math.Min(100m, 75m + (annualizedVolatility - 1m) * 25m)
        };
        // Normalize before averaging to avoid decimal sum overflow; ratios are unchanged.
        var maximum = prices.Max();
        var normalized = prices.Select(p => (double)p / (double)maximum).ToList();
        var recentCount = prices.Count >= 30 ? 7 : Math.Max(3, prices.Count / 3);
        var momentum = (decimal)Math.Abs(normalized.TakeLast(recentCount).Average() / normalized.Average() - 1d);
        var trendScore = momentum switch
        {
            <= 0.05m => momentum * 400m,
            <= 0.15m => 20m + (momentum - 0.05m) * 300m,
            <= 0.30m => 50m + (momentum - 0.15m) * 200m,
            _ => Math.Min(100m, 80m + (momentum - 0.30m) * 200m)
        };
        var volumeScore = CalculateVolumeScore(prices, currentVolume, averageVolume);
        // Fixed weights avoid threshold-driven jumps and expose exact contributions.
        var composite = Round(volatilityScore * 0.4m + trendScore * 0.3m + volumeScore * 0.3m);
        var peak = prices[0];
        var maxDrawdown = 0m;
        foreach (var price in prices)
        {
            peak = Math.Max(peak, price);
            maxDrawdown = Math.Max(maxDrawdown, (peak - price) / peak * 100m);
        }
        var quantile = returns.OrderBy(r => r).ElementAt((int)Math.Ceiling(returns.Count * 0.05d) - 1);
        // Convert log return to an actual percentage loss, bounded by 100%.
        var valueAtRisk = (decimal)(Math.Max(0d, 1d - Math.Exp(quantile)) * 100d);
        decimal? sharpe = stdDev <= 1e-12 ? null : Round((decimal)(mean / stdDev * Annualization));
        var warnings = new List<string>();
        if (history.Count < 30)
            warnings.Add("Short history: fewer than 30 daily prices; annualized estimates are unstable.");
        if (returns.Count <= 20)
            warnings.Add("Historical VaR uses the single worst observed return with 20 or fewer returns.");
        if (sharpe is null)
            warnings.Add("Sharpe ratio is unavailable because daily return variability is effectively zero.");
        logger.LogDebug("Risk methodology {Version}: composite {Score} from {Count} daily prices", MethodologyVersion, composite, history.Count);
        return new RiskScoreResult
        {
            VolatilityScore = Round(volatilityScore),
            TrendScore = Round(trendScore),
            VolumeScore = Round(volumeScore),
            CompositeRiskScore = composite,
            AnnualizedVolatility = Round(annualizedVolatility * 100m),
            DownsideRisk = Round((decimal)(Math.Sqrt(returns.Sum(r => Math.Pow(Math.Min(0d, r), 2)) / returns.Count) * Annualization * 100d)),
            MaxDrawdown = Round(maxDrawdown),
            SharpeRatio = sharpe,
            ValueAtRisk95 = Round(valueAtRisk),
            PriceHistory = history,
            Methodology = new RiskMethodologyDetails
            {
                Version = MethodologyVersion,
                ObservationCount = history.Count,
                ReturnCount = returns.Count,
                PeriodStart = history[0].Timestamp,
                PeriodEnd = history[^1].Timestamp,
                RiskLevel = composite < 30m ? "Low" : composite < 70m ? "Medium" : "High",
                VolatilityContribution = volatilityScore * 0.4m,
                TrendContribution = trendScore * 0.3m,
                VolumeContribution = volumeScore * 0.3m,
                Warnings = warnings
            }
        };
    }

    private static void ValidateInputs(IReadOnlyList<PriceData> history, decimal currentVolume, decimal averageVolume)
    {
        if (currentVolume < 0)
            throw new ArgumentOutOfRangeException(nameof(currentVolume), "Current volume cannot be negative.");
        if (averageVolume <= 0)
            throw new ArgumentOutOfRangeException(nameof(averageVolume), "A positive average volume is required; unavailable volume cannot be scored.");
        if (history.Count < 7)
            throw new ArgumentException("At least 7 daily price observations are required.", nameof(history));
        DateTime? previousDate = null;
        foreach (var observation in history)
        {
            if (observation is null || observation.Price <= 0)
                throw new ArgumentException("Price history must contain non-null observations with positive prices.", nameof(history));
            DateTime date;
            try { date = DateTimeOffset.FromUnixTimeMilliseconds(observation.Timestamp).UtcDateTime.Date; }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new ArgumentException("Price history contains an invalid Unix millisecond timestamp.", nameof(history), ex);
            }
            if (previousDate.HasValue && (date - previousDate.Value).Days != 1)
                throw new ArgumentException("Price history must contain consecutive UTC dates in chronological order; missing days cannot be filled with fabricated prices.", nameof(history));
            previousDate = date;
        }
    }

    private static decimal CalculateVolumeScore(List<decimal> prices, decimal currentVolume, decimal averageVolume)
    {
        // Saturation at 6x; convert before dividing to avoid decimal overflow.
        var ratio = (decimal)Math.Min(6d, (double)currentVolume / (double)averageVolume);
        var baseline = ratio switch
        {
            < 0.3m => 100m - ratio * (35m / 0.3m),
            < 0.5m => 65m - (ratio - 0.3m) * 125m,
            <= 1m => 40m - (ratio - 0.5m) * 20m,
            <= 3m => 30m + (ratio - 1m) * 20m,
            _ => 70m + (ratio - 3m) * 10m
        };
        if (prices.Count <= 7) return baseline;
        var weeklyChange = (double)prices[^1] / (double)prices[^8] - 1d;
        // Context bonuses fade to zero at their boundaries.
        var sellingPressure = (decimal)Math.Clamp((-weeklyChange - 0.05d) * 300d, 0d, 60d)
            * Math.Clamp((ratio - 1.5m) / 1.5m, 0m, 1m);
        var weakRally = (decimal)Math.Clamp((weeklyChange - 0.05d) * 600d, 0d, 30d)
            * Math.Clamp((0.5m - ratio) / 0.1m, 0m, 1m);
        return Math.Min(100m, baseline + sellingPressure + weakRally);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.ToEven);
}
