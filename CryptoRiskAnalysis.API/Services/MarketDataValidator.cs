using CryptoRiskAnalysis.API.Exceptions;
using CryptoRiskAnalysis.API.Models;

namespace CryptoRiskAnalysis.API.Services;

internal static class MarketDataValidator
{
    // CoinGecko publishes the latest completed UTC day at 00:35 UTC.
    public static bool IsCoinGeckoPublicationWindow(DateTimeOffset now) =>
        now.UtcDateTime.TimeOfDay < TimeSpan.FromMinutes(35);

    public static bool HasFreshEndDate(CachedMarketData data, DateTimeOffset now)
    {
        var yesterday = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-1);
        var lastDay = data.Observations[^1].Date;
        return lastDay == yesterday ||
            (data.Data.Source == MarketDataSource.CoinGecko && IsCoinGeckoPublicationWindow(now) &&
             lastDay == yesterday.AddDays(-1));
    }

    public static CachedMarketData RequireFreshEndDate(CachedMarketData data, DateTimeOffset now)
    {
        if (!HasFreshEndDate(data, now))
            throw new MarketDataProviderException(data.Data.Source.ToString(),
                $"daily observations end on {data.Observations[^1].Date:yyyy-MM-dd}, outside the expected recent UTC dates.");
        return data;
    }

    public static void ValidateCompletedDailySeries(
        string provider,
        IReadOnlyList<PriceData> prices,
        IReadOnlyList<(long Timestamp, decimal Volume)> volumes,
        int expectedDays)
    {
        if (expectedDays <= 0)
            throw new ArgumentOutOfRangeException(nameof(expectedDays), "The requested day count must be positive.");

        if (prices.Count != expectedDays || volumes.Count != expectedDays)
        {
            throw new MarketDataProviderException(
                provider,
                $"expected {expectedDays} completed daily price/volume observations but received " +
                $"{prices.Count} prices and {volumes.Count} volumes.");
        }

        DateTime? previousDate = null;

        for (var i = 0; i < expectedDays; i++)
        {
            if (prices[i].Price <= 0)
            {
                throw new MarketDataProviderException(
                    provider,
                    $"price observation {i + 1} must be greater than zero.");
            }

            if (volumes[i].Volume < 0)
            {
                throw new MarketDataProviderException(
                    provider,
                    $"volume observation {i + 1} cannot be negative.");
            }

            DateTime priceDate;
            DateTime volumeDate;
            try
            {
                priceDate = DateTimeOffset.FromUnixTimeMilliseconds(prices[i].Timestamp).UtcDateTime.Date;
                volumeDate = DateTimeOffset.FromUnixTimeMilliseconds(volumes[i].Timestamp).UtcDateTime.Date;
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new MarketDataProviderException(provider, $"observation {i + 1} has an invalid timestamp.", ex);
            }

            if (priceDate != volumeDate)
            {
                throw new MarketDataProviderException(
                    provider,
                    $"price and volume dates do not match at observation {i + 1}.");
            }

            if (previousDate.HasValue && priceDate != previousDate.Value.AddDays(1))
            {
                throw new MarketDataProviderException(
                    provider,
                    $"daily observations are not consecutive between {previousDate:yyyy-MM-dd} and {priceDate:yyyy-MM-dd}.");
            }

            previousDate = priceDate;
        }
    }
}
