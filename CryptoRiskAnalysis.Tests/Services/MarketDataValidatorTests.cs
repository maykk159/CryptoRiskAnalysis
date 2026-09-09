using CryptoRiskAnalysis.API.Exceptions;
using CryptoRiskAnalysis.API.Models;
using CryptoRiskAnalysis.API.Services;

namespace CryptoRiskAnalysis.Tests.Services;

public class MarketDataValidatorTests
{
    private const long Day = 86_400_000;
    private static readonly long Start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

    private static List<PriceData> Prices(params long[] timestamps) =>
        timestamps.Select(timestamp => new PriceData { Timestamp = timestamp, Price = 100m }).ToList();

    [Fact]
    public void AcceptsConsecutiveUtcDaysWithZeroVolumeAndDifferentIntradayTimes()
    {
        MarketDataValidator.ValidateCompletedDailySeries("test",
            Prices(Start, Start + Day), [(Start + 1000, 0m), (Start + Day + 1000, 10m)], 2);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsMissingDuplicateOrReversedDays(int dayOffset)
    {
        var second = Start + dayOffset * Day;
        var error = Assert.Throws<MarketDataProviderException>(() =>
            MarketDataValidator.ValidateCompletedDailySeries("test",
                Prices(Start, second), [(Start, 1m), (second, 1m)], 2));
        Assert.Contains("not consecutive", error.Message);
    }

    [Theory]
    [InlineData(long.MinValue, true)]
    [InlineData(long.MaxValue, true)]
    [InlineData(long.MinValue, false)]
    [InlineData(long.MaxValue, false)]
    public void WrapsOutOfRangePriceAndVolumeTimestamps(long invalid, bool invalidPrice)
    {
        var error = Assert.Throws<MarketDataProviderException>(() =>
            MarketDataValidator.ValidateCompletedDailySeries("test",
                Prices(invalidPrice ? invalid : Start), [(invalidPrice ? Start : invalid, 1m)], 1));
        Assert.Contains("invalid timestamp", error.Message);
        Assert.IsType<ArgumentOutOfRangeException>(error.InnerException);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    [InlineData(0, 0)]
    [InlineData(3, 3)]
    public void RejectsUnexpectedSeriesLengths(int priceCount, int volumeCount)
    {
        var prices = Prices(Enumerable.Range(0, priceCount).Select(i => Start + i * Day).ToArray());
        var volumes = Enumerable.Range(0, volumeCount).Select(i => (Start + i * Day, 1m)).ToArray();
        var error = Assert.Throws<MarketDataProviderException>(() =>
            MarketDataValidator.ValidateCompletedDailySeries("test", prices, volumes, 2));
        Assert.Contains($"{priceCount} prices and {volumeCount} volumes", error.Message);
    }

    [Fact]
    public void RejectsMismatchedPriceAndVolumeDates()
    {
        var error = Assert.Throws<MarketDataProviderException>(() =>
            MarketDataValidator.ValidateCompletedDailySeries("test", Prices(Start), [(Start + Day, 1m)], 1));
        Assert.Contains("dates do not match", error.Message);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(100, -1)]
    public void RejectsInvalidPriceOrVolume(int price, int volume)
    {
        Assert.Throws<MarketDataProviderException>(() =>
            MarketDataValidator.ValidateCompletedDailySeries("test",
                [new PriceData { Timestamp = Start, Price = price }], [(Start, volume)], 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsNonPositiveExpectedDayCount(int days)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            MarketDataValidator.ValidateCompletedDailySeries("test", [], [], days));
        Assert.Equal("expectedDays", error.ParamName);
    }
}
