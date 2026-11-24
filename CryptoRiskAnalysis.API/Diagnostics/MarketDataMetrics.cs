using System.Diagnostics.Metrics;
using CryptoRiskAnalysis.API.Models;

namespace CryptoRiskAnalysis.API.Diagnostics;

public sealed class MarketDataMetrics
{
    internal const string MeterName = "CryptoRiskAnalysis.MarketData";
    internal const string ProviderDurationName = "market_data.provider.request.duration";
    internal const string RateLimitRejectionsName = "market_data.rate_limit.rejections";
    internal const string FallbacksName = "market_data.fallbacks";

    private readonly Histogram<double> _providerDuration;
    private readonly Counter<long> _rateLimitRejections;
    private readonly Counter<long> _fallbacks;

    public MarketDataMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _providerDuration = meter.CreateHistogram<double>(ProviderDurationName, "s",
            "Duration of a market data provider attempt, including response buffering.");
        _rateLimitRejections = meter.CreateCounter<long>(RateLimitRejectionsName,
            description: "Requests rejected by a market data rate limit.");
        _fallbacks = meter.CreateCounter<long>(FallbacksName,
            description: "Fallbacks from Binance to CoinGecko after a provider failure.");
    }

    internal void RecordProviderAttempt(MarketDataSource provider, TimeSpan duration) =>
        _providerDuration.Record(duration.TotalSeconds, new KeyValuePair<string, object?>("provider", provider.ToString()));

    internal void RecordProviderRateLimit(MarketDataSource provider, string scope) =>
        _rateLimitRejections.Add(1, new("scope", scope), new("provider", provider.ToString()));

    internal void RecordIncomingRateLimit() =>
        _rateLimitRejections.Add(1, new KeyValuePair<string, object?>("scope", "api"));

    internal void RecordBinanceFallback() =>
        _fallbacks.Add(1, new("from_provider", "Binance"), new("to_provider", "CoinGecko"));
}
