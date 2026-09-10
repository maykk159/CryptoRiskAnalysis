using CryptoRiskAnalysis.API.Services;
using CryptoRiskAnalysis.API.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CryptoRiskAnalysis.Tests.Services;

public class RiskMethodologyTests
{
    private readonly RiskAnalysisEngine _engine = new(NullLogger<RiskAnalysisEngine>.Instance);
    private static List<PriceData> History(params decimal[] prices) => prices.Select((p, i) =>
        new PriceData { Timestamp = 1_700_006_400_000L + i * 86_400_000L, Price = p }).ToList();

    [Fact]
    public void FlatSeries_HasZeroPriceRiskAndUndefinedSharpe()
    {
        var result = _engine.CalculateRisk(History(100, 100, 100, 100, 100, 100, 100), 1000, 1000);
        Assert.Equal(0m, result.AnnualizedVolatility);
        Assert.Equal(0m, result.DownsideRisk);
        Assert.Equal(0m, result.MaxDrawdown);
        Assert.Equal(0m, result.ValueAtRisk95);
        Assert.Null(result.SharpeRatio);
        Assert.Equal(9m, result.CompositeRiskScore);
        Assert.Equal("Low", result.Methodology.RiskLevel);
        Assert.Equal(3, result.Methodology.Warnings.Count);
    }

    [Fact]
    public void KnownLoss_UsesPercentageLossRatherThanLogLoss()
    {
        var result = _engine.CalculateRisk(History(100, 100, 100, 100, 100, 100, 50), 1000, 1000);
        Assert.Equal(50m, result.ValueAtRisk95);
        Assert.Equal(50m, result.MaxDrawdown);
        // Six returns, one nonzero: sample variance = log(0.5)^2 / 6.
        Assert.Equal(Math.Round((decimal)(Math.Abs(Math.Log(0.5)) / Math.Sqrt(6) * Math.Sqrt(365) * 100), 2), result.AnnualizedVolatility);
    }

    [Fact]
    public void ConstantGrowth_HasUndefinedSharpeDespiteFloatingPointNoise()
    {
        var result = _engine.CalculateRisk(History(1, 2, 4, 8, 16, 32, 64), 1000, 1000);
        Assert.Null(result.SharpeRatio);
        Assert.Equal(0m, result.DownsideRisk);
        Assert.Equal(0m, result.ValueAtRisk95);
    }

    [Theory]
    [InlineData("gap")]
    [InlineData("duplicate")]
    [InlineData("reverse")]
    [InlineData("intraday")]
    [InlineData("timestamp")]
    [InlineData("null")]
    [InlineData("zero")]
    public void InvalidDailySeries_IsRejected(string kind)
    {
        var history = History(100, 101, 102, 103, 104, 105, 106);
        switch (kind)
        {
            case "gap": history[^1].Timestamp += 86_400_000L; break;
            case "duplicate": history[1].Timestamp = history[0].Timestamp; break;
            case "reverse": history.Reverse(); break;
            case "intraday": history[1].Timestamp = history[0].Timestamp + 1000; break;
            case "timestamp": history[1].Timestamp = long.MaxValue; break;
            case "null": history[1] = null!; break;
            case "zero": history[1].Price = 0; break;
        }
        Assert.Throws<ArgumentException>(() => _engine.CalculateRisk(history, 1000, 1000));
    }

    [Fact]
    public void MissingVolumeBaseline_IsRejectedButObservedZeroVolumeIsHighRisk()
    {
        var history = History(100, 100, 100, 100, 100, 100, 100);
        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.CalculateRisk(history, 0, 0));
        Assert.Equal(100m, _engine.CalculateRisk(history, 0, 1000).VolumeScore);
    }

    [Fact]
    public void Result_IsReproducibleExplainableAndIndependentOfInputMutation()
    {
        var history = History(100, 110, 90, 95, 102, 100, 105);
        var result = _engine.CalculateRisk(history, 1200, 1000);
        var again = _engine.CalculateRisk(history, 1200, 1000);
        Assert.Equal(result.CompositeRiskScore, again.CompositeRiskScore);
        var details = result.Methodology;
        Assert.Equal("2.0.0", details.Version);
        Assert.Equal(7, details.ObservationCount);
        Assert.Equal(6, details.ReturnCount);
        Assert.Equal(1m, details.VolatilityWeight + details.TrendWeight + details.VolumeWeight);
        Assert.Equal(result.CompositeRiskScore, Math.Round(details.VolatilityContribution + details.TrendContribution + details.VolumeContribution, 2));
        history[0].Price = 999;
        history.Clear();
        Assert.Equal(100m, result.PriceHistory[0].Price);
        Assert.Equal(7, result.PriceHistory.Count);
    }

    [Theory]
    [InlineData(300)]
    [InlineData(500)]
    [InlineData(1000)]
    [InlineData(1500)]
    [InlineData(3000)]
    public void VolumeThresholds_AreContinuous(int currentVolume)
    {
        foreach (var finalPrice in new[] { 70m, 100m, 120m })
        {
            var history = History(100, 100, 100, 100, 100, 100, 100, finalPrice);
            var below = _engine.CalculateRisk(history, currentVolume - 0.01m, 1000);
            var above = _engine.CalculateRisk(history, currentVolume + 0.01m, 1000);
            Assert.InRange(Math.Abs(above.VolumeScore - below.VolumeScore), 0m, 0.02m);
            Assert.InRange(Math.Abs(above.CompositeRiskScore - below.CompositeRiskScore), 0m, 0.02m);
        }
    }

    [Fact]
    public void ScalingPricesAndVolumes_DoesNotChangeScores()
    {
        var history = History(100, 120, 90, 110, 105, 98, 102, 106);
        var scaled = history.Select(p => new PriceData { Timestamp = p.Timestamp, Price = p.Price * 100 }).ToList();
        var original = _engine.CalculateRisk(history, 400, 1000);
        var result = _engine.CalculateRisk(scaled, 40000, 100000);
        Assert.Equal(original.CompositeRiskScore, result.CompositeRiskScore);
        Assert.Equal(original.ValueAtRisk95, result.ValueAtRisk95);
        Assert.Equal(original.AnnualizedVolatility, result.AnnualizedVolatility);
    }

    [Fact]
    public void ExtremeValidDecimals_DoNotOverflowAndLossStaysBounded()
    {
        var history = History(decimal.MaxValue, 0.0000000000000000000000000001m,
            decimal.MaxValue, 1, decimal.MaxValue, 1, decimal.MaxValue);
        var result = _engine.CalculateRisk(history, decimal.MaxValue, 0.0000000000000000000000000001m);
        Assert.InRange(result.CompositeRiskScore, 0m, 100m);
        Assert.InRange(result.ValueAtRisk95, 0m, 100m);
        Assert.Equal(100m, result.VolumeScore);
    }
}
