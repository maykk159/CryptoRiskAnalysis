using CryptoRiskAnalysis.API.Interfaces;
using CryptoRiskAnalysis.API.Models;

namespace CryptoRiskAnalysis.API.Services;

/// <summary>Deterministic daily indicators; see docs/risk-methodology.md.</summary>
public class RiskAnalysisEngine(ILogger<RiskAnalysisEngine> logger) : IRiskEngine
{
    public const string MethodologyVersion = RiskMethodology.Version;
    private static readonly double Annualization = Math.Sqrt(365);

    public RiskScoreResult CalculateRisk(List<PriceData> priceHistory, decimal currentVolume, decimal averageVolume)
    {
        ArgumentNullException.ThrowIfNull(priceHistory);
        ValidateInputs(priceHistory, currentVolume, averageVolume);
        var (history, maximum) = CreatePriceSnapshot(priceHistory);
        var returns = CalculateLogReturns(history);
        var mean = returns.Average();
        var stdDev = CalculateSampleStandardDeviation(returns, mean);
        var annualizedVolatility = (decimal)(stdDev * Annualization);
        var volatilityScore = CalculateVolatilityScore(annualizedVolatility);
        var trendScore = CalculateTrendScore(history, maximum);
        var volumeScore = CalculateVolumeScore(history, currentVolume, averageVolume);
        // Fixed weights avoid threshold-driven jumps and expose exact contributions.
        var volatilityContribution = volatilityScore * RiskMethodology.VolatilityWeight;
        var trendContribution = trendScore * RiskMethodology.TrendWeight;
        var volumeContribution = volumeScore * RiskMethodology.VolumeWeight;
        var composite = Round(volatilityContribution + trendContribution + volumeContribution);
        var maxDrawdown = CalculateMaximumDrawdown(history);
        var valueAtRisk = CalculateHistoricalValueAtRisk95(returns);
        var sharpe = CalculateSharpeRatio(mean, stdDev);
        var warnings = CreateWarnings(history.Count, returns.Count, sharpe);
        logger.LogDebug("Risk methodology {Version}: composite {Score} from {Count} daily prices", MethodologyVersion, composite, history.Count);
        return new RiskScoreResult
        {
            VolatilityScore = Round(volatilityScore),
            TrendScore = Round(trendScore),
            VolumeScore = Round(volumeScore),
            CompositeRiskScore = composite,
            AnnualizedVolatility = Round(annualizedVolatility * 100m),
            DownsideRisk = CalculateDownsideRisk(returns),
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
                RiskLevel = composite < RiskMethodology.MediumRiskThreshold ? "Low"
                    : composite < RiskMethodology.HighRiskThreshold ? "Medium" : "High",
                VolatilityContribution = volatilityContribution,
                TrendContribution = trendContribution,
                VolumeContribution = volumeContribution,
                Warnings = warnings
            }
        };
    }

    private static (List<PriceData> History, decimal Maximum) CreatePriceSnapshot(List<PriceData> priceHistory)
    {
        // Keep an isolated result snapshot without allocating a second price-only list.
        var history = new List<PriceData>(priceHistory.Count);
        var maximum = 0m;
        foreach (var observation in priceHistory)
        {
            var copy = new PriceData { Timestamp = observation.Timestamp, Price = observation.Price };
            history.Add(copy);
            maximum = Math.Max(maximum, copy.Price);
        }
        return (history, maximum);
    }

    private static List<double> CalculateLogReturns(List<PriceData> history)
    {
        var returns = new List<double>(history.Count - 1);
        for (var i = 1; i < history.Count; i++)
            returns.Add(Math.Log((double)history[i].Price / (double)history[i - 1].Price));
        return returns;
    }

    private static double CalculateSampleStandardDeviation(List<double> returns, double mean) =>
        Math.Sqrt(returns.Sum(r => (r - mean) * (r - mean)) / (returns.Count - 1));

    private static decimal CalculateVolatilityScore(decimal annualizedVolatility) => annualizedVolatility switch
    {
        < 0.5m => annualizedVolatility * 100m,
        < 1m => 50m + (annualizedVolatility - 0.5m) * 50m,
        _ => Math.Min(100m, 75m + (annualizedVolatility - 1m) * 25m)
    };

    private static decimal CalculateTrendScore(List<PriceData> history, decimal maximum)
    {
        // Normalize before averaging to avoid decimal sum overflow; ratios are unchanged.
        var normalized = history.Select(p => (double)p.Price / (double)maximum).ToList();
        var recentCount = history.Count >= 30 ? 7 : Math.Max(3, history.Count / 3);
        var momentum = (decimal)Math.Abs(normalized.TakeLast(recentCount).Average() / normalized.Average() - 1d);
        return momentum switch
        {
            <= 0.05m => momentum * 400m,
            <= 0.15m => 20m + (momentum - 0.05m) * 300m,
            <= 0.30m => 50m + (momentum - 0.15m) * 200m,
            _ => Math.Min(100m, 80m + (momentum - 0.30m) * 200m)
        };
    }

    private static decimal CalculateMaximumDrawdown(List<PriceData> history)
    {
        var peak = history[0].Price;
        var maxDrawdown = 0m;
        foreach (var observation in history)
        {
            var price = observation.Price;
            peak = Math.Max(peak, price);
            maxDrawdown = Math.Max(maxDrawdown, (peak - price) / peak * 100m);
        }
        return maxDrawdown;
    }

    private static decimal CalculateHistoricalValueAtRisk95(List<double> returns)
    {
        var quantile = returns.OrderBy(r => r).ElementAt((int)Math.Ceiling(returns.Count * 0.05d) - 1);
        // Convert log return to an actual percentage loss, bounded by 100%.
        return (decimal)(Math.Max(0d, 1d - Math.Exp(quantile)) * 100d);
    }

    private static decimal? CalculateSharpeRatio(double mean, double stdDev) =>
        stdDev <= 1e-12 ? null : Round((decimal)(mean / stdDev * Annualization));

    private static decimal CalculateDownsideRisk(List<double> returns) =>
        Round((decimal)(Math.Sqrt(returns.Sum(r => Math.Pow(Math.Min(0d, r), 2)) / returns.Count) * Annualization * 100d));

    private static List<string> CreateWarnings(int observationCount, int returnCount, decimal? sharpe)
    {
        var warnings = new List<string>();
        if (observationCount < 30)
            warnings.Add("Short history: fewer than 30 daily prices; annualized estimates are unstable.");
        if (returnCount <= 20)
            warnings.Add("Historical VaR uses the single worst observed return with 20 or fewer returns.");
        if (sharpe is null)
            warnings.Add("Sharpe ratio is unavailable because daily return variability is effectively zero.");
        return warnings;
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

    private static decimal CalculateVolumeScore(List<PriceData> history, decimal currentVolume, decimal averageVolume)
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
        if (history.Count <= 7) return baseline;
        var weeklyChange = (double)history[^1].Price / (double)history[^8].Price - 1d;
        // Context bonuses fade to zero at their boundaries.
        var sellingPressure = (decimal)Math.Clamp((-weeklyChange - 0.05d) * 300d, 0d, 60d)
            * Math.Clamp((ratio - 1.5m) / 1.5m, 0m, 1m);
        var weakRally = (decimal)Math.Clamp((weeklyChange - 0.05d) * 600d, 0d, 30d)
            * Math.Clamp((0.5m - ratio) / 0.1m, 0m, 1m);
        return Math.Min(100m, baseline + sellingPressure + weakRally);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.ToEven);
}
