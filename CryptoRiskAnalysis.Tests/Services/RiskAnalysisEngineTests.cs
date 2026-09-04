using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using CryptoRiskAnalysis.API.Services;
using CryptoRiskAnalysis.API.Models;

namespace CryptoRiskAnalysis.Tests.Services
{
    public class RiskAnalysisEngineTests
    {
        private readonly RiskAnalysisEngine _engine;

        public RiskAnalysisEngineTests()
        {
            var mockLogger = new Mock<ILogger<RiskAnalysisEngine>>();
            _engine = new RiskAnalysisEngine(mockLogger.Object);
        }

        [Fact]
        public void CalculateRisk_WithStablePrices_ReturnsLowVolatilityScore()
        {
            // Arrange - Stable prices (low volatility)
            var priceHistory = new List<PriceData>();
            for (int i = 0; i < 30; i++)
            {
                priceHistory.Add(new PriceData
                {
                    Timestamp = DateTimeOffset.UtcNow.AddDays(-30 + i).ToUnixTimeMilliseconds(),
                    Price = 100m + (decimal)(i * 0.1) // Very small change
                });
            }

            // Act
            var result = _engine.CalculateRisk(priceHistory, 1000m, 1000m);

            // Assert - Low volatility should produce low score
            Assert.InRange(result.VolatilityScore, 0, 30);
            Assert.InRange(result.CompositeRiskScore, 0, 50);
        }

        [Fact]
        public void CalculateRisk_WithHighVolatility_ReturnsHighScore()
        {
            // Arrange - Volatile prices
            var priceHistory = new List<PriceData>();
            var basePrice = 100m;
            for (int i = 0; i < 30; i++)
            {
                var volatileChange = (i % 2 == 0) ? 1.1m : 0.9m; // +10%, -10% alternating
                basePrice *= volatileChange;
                priceHistory.Add(new PriceData
                {
                    Timestamp = DateTimeOffset.UtcNow.AddDays(-30 + i).ToUnixTimeMilliseconds(),
                    Price = basePrice
                });
            }

            // Act
            var result = _engine.CalculateRisk(priceHistory, 1000m, 1000m);

            // Assert - High volatility should produce high score
            Assert.InRange(result.VolatilityScore, 30, 100);
            Assert.True(result.AnnualizedVolatility > 50);
        }

        [Fact]
        public void CalculateRisk_WithInsufficientData_ReturnsDefaultScore()
        {
            // Arrange - Only 2 data points (insufficient)
            var priceHistory = new List<PriceData>
            {
                new PriceData { Timestamp = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeMilliseconds(), Price = 100m },
                new PriceData { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Price = 105m }
            };

            // Act
            var result = _engine.CalculateRisk(priceHistory, 1000m, 1000m);

            // Assert - Should return neutral/default scores
            Assert.Equal(50m, result.TrendScore); // Default trend score
            Assert.Equal(0m, result.DownsideRisk); // Insufficient data
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void CalculateRisk_WithNonPositivePrice_RejectsEntireSeries(int invalidPrice)
        {
            var priceHistory = new List<PriceData>
            {
                new() { Timestamp = 1000, Price = 100m },
                new() { Timestamp = 2000, Price = invalidPrice },
                new() { Timestamp = 3000, Price = 110m }
            };

            Assert.Throws<ArgumentException>(() =>
                _engine.CalculateRisk(priceHistory, 1000m, 1000m));
        }

        [Theory]
        [InlineData(-1, 1000)]
        [InlineData(1000, -1)]
        public void CalculateRisk_WithNegativeVolume_RejectsInput(int currentVolume, int averageVolume)
        {
            var priceHistory = new List<PriceData>
            {
                new() { Timestamp = 1000, Price = 100m },
                new() { Timestamp = 2000, Price = 101m }
            };

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _engine.CalculateRisk(priceHistory, currentVolume, averageVolume));
        }

        [Fact]
        public void CalculateDownsideRisk_UsesZeroTargetAndAllReturnPeriods()
        {
            // Arrange - Two downside periods and two periods above the 0% target.
            // Standard downside deviation keeps all four periods in the denominator.
            var logReturns = new[] { -0.10, 0.05, -0.20, 0.10 };
            var priceHistory = new List<PriceData>();
            var price = 100d;
            priceHistory.Add(new PriceData { Timestamp = 1000, Price = (decimal)price });

            for (int i = 0; i < logReturns.Length; i++)
            {
                price *= Math.Exp(logReturns[i]);
                priceHistory.Add(new PriceData
                {
                    Timestamp = 2000 + i * 1000,
                    Price = (decimal)price
                });
            }

            var expectedDailyDeviation = Math.Sqrt((0.10 * 0.10 + 0.20 * 0.20) / logReturns.Length);
            var expectedAnnualizedPercent = Math.Round(
                (decimal)(expectedDailyDeviation * Math.Sqrt(365) * 100),
                2);

            // Act
            var result = _engine.CalculateRisk(priceHistory, 1000m, 1000m);

            // Assert
            Assert.Equal(expectedAnnualizedPercent, result.DownsideRisk);
        }

        [Fact]
        public void CalculateVaR_With7Days_UsesWorstReturn()
        {
            // Arrange - 7 days of data
            var priceHistory = new List<PriceData>
            {
                new PriceData { Timestamp = 1000, Price = 100m },
                new PriceData { Timestamp = 2000, Price = 95m },  // -5%
                new PriceData { Timestamp = 3000, Price = 98m },
                new PriceData { Timestamp = 4000, Price = 97m },
                new PriceData { Timestamp = 5000, Price = 99m },
                new PriceData { Timestamp = 6000, Price = 91m },  // Worst: -8.08%
                new PriceData { Timestamp = 7000, Price = 93m }
            };

            // Act
            var result = _engine.CalculateRisk(priceHistory, 1000m, 1000m);

            // Assert - VaR should be approximately 8% (worst single-day drop)
            Assert.InRange(result.ValueAtRisk95, 5, 10);
        }

        [Fact]
        public void CalculateSharpeRatio_WithPositiveReturns_ReturnsPositiveValue()
        {
            // Arrange - Increasing prices
            var priceHistory = new List<PriceData>();
            for (int i = 0; i < 30; i++)
            {
                priceHistory.Add(new PriceData
                {
                    Timestamp = DateTimeOffset.UtcNow.AddDays(-30 + i).ToUnixTimeMilliseconds(),
                    Price = 100m + (decimal)i // Consistent uptrend
                });
            }

            // Act
            var result = _engine.CalculateRisk(priceHistory, 1000m, 1000m);

            // Assert - Positive trend should give positive Sharpe
            Assert.True(result.SharpeRatio > 0);
        }

        [Fact]
        public void CalculateMaxDrawdown_WithSignificantDrop_ReturnsCorrectPercentage()
        {
            // Arrange - Peak then drop scenario
            var priceHistory = new List<PriceData>
            {
                new PriceData { Timestamp = 1000, Price = 100m },
                new PriceData { Timestamp = 2000, Price = 120m }, // Peak
                new PriceData { Timestamp = 3000, Price = 110m },
                new PriceData { Timestamp = 4000, Price = 90m },  // Drop to 90 from 120 = -25%
                new PriceData { Timestamp = 5000, Price = 95m }
            };

            // Act
            var result = _engine.CalculateRisk(priceHistory, 1000m, 1000m);

            // Assert - Drawdown should be ~25%
            Assert.InRange(result.MaxDrawdown, 20, 30);
        }

        [Fact]
        public void CalculateRisk_WithHighVolume_ReturnsLowVolumeScore()
        {
            // Arrange
            var priceHistory = new List<PriceData>();
            for (int i = 0; i < 30; i++)
            {
                priceHistory.Add(new PriceData
                {
                    Timestamp = DateTimeOffset.UtcNow.AddDays(-30 + i).ToUnixTimeMilliseconds(),
                    Price = 100m
                });
            }

            // Act - Current volume higher than average
            var result = _engine.CalculateRisk(priceHistory, 1200m, 1000m);

            // Assert - High volume relative to average = low liquidity risk
            Assert.InRange(result.VolumeScore, 0, 40);
        }

        [Fact]
        public void CalculateRisk_CrossingVolumeSpikeThreshold_DoesNotDecreaseVolumeRisk()
        {
            // Arrange - Keep price flat so only the volume ratio affects the score.
            var priceHistory = Enumerable.Range(0, 30)
                .Select(i => new PriceData
                {
                    Timestamp = DateTimeOffset.UtcNow.AddDays(-30 + i).ToUnixTimeMilliseconds(),
                    Price = 100m
                })
                .ToList();

            // Act - Compare the exact 3.0x threshold with a value just above it.
            var atThreshold = _engine.CalculateRisk(priceHistory, 3000m, 1000m);
            var aboveThreshold = _engine.CalculateRisk(priceHistory, 3010m, 1000m);

            // Assert - The score remains continuous and increases with the anomaly.
            Assert.Equal(70m, atThreshold.VolumeScore);
            Assert.Equal(70.1m, aboveThreshold.VolumeScore);
            Assert.True(aboveThreshold.VolumeScore > atThreshold.VolumeScore);
        }

        [Fact]
        public void CalculateRisk_WithLowVolume_ReturnsHighVolumeScore()
        {
            // Arrange
            var priceHistory = new List<PriceData>();
            for (int i = 0; i < 30; i++)
            {
                priceHistory.Add(new PriceData
                {
                    Timestamp = DateTimeOffset.UtcNow.AddDays(-30 + i).ToUnixTimeMilliseconds(),
                    Price = 100m
                });
            }

            // Act - Current volume much lower than average
            var result = _engine.CalculateRisk(priceHistory, 500m, 1000m);

            // Assert - Low volume relative to average = high liquidity risk
            Assert.InRange(result.VolumeScore, 40, 100);
        }

        [Fact]
        public void CalculateRisk_EnsuresScoresBounded()
        {
            // Arrange - Extreme volatile data
            var priceHistory = new List<PriceData>();
            var price = 100m;
            for (int i = 0; i < 30; i++)
            {
                price *= (i % 2 == 0) ? 1.5m : 0.5m; // Extreme swings
                priceHistory.Add(new PriceData
                {
                    Timestamp = DateTimeOffset.UtcNow.AddDays(-30 + i).ToUnixTimeMilliseconds(),
                    Price = price
                });
            }

            // Act
            var result = _engine.CalculateRisk(priceHistory, 1000m, 1000m);

            // Assert - All scores should be bounded 0-100
            Assert.InRange(result.VolatilityScore, 0, 100);
            Assert.InRange(result.TrendScore, 0, 100);
            Assert.InRange(result.VolumeScore, 0, 100);
            Assert.InRange(result.CompositeRiskScore, 0, 100);
        }
    }
}
