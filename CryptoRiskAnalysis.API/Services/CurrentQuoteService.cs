using CryptoRiskAnalysis.API.Interfaces;
using CryptoRiskAnalysis.API.Models;
using Microsoft.Extensions.Caching.Memory;

namespace CryptoRiskAnalysis.API.Services;

/// <summary>On-demand quotes; no timer or background polling when nobody requests data.</summary>
public sealed class CurrentQuoteService(BinanceSpotService binance, CoinGeckoService coinGecko,
    IMemoryCache cache, MarketDataRequestLock requestLock, TimeProvider clock) : ICurrentQuoteService
{
    private sealed record Entry(MarketQuote Quote, long Timestamp);

    public async Task<MarketQuote> GetAsync(string assetId, MarketDataSource source, bool refresh,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var currency = SqliteHistoricalMarketDataStore.QuoteCurrency(source);
        var identity = source == MarketDataSource.Binance ? BinanceSymbolMapper.GetBinanceSymbol(assetId) : assetId;
        var key = $"quote:{source}:{currency}:{identity}";
        var lifetime = TimeSpan.FromSeconds(source == MarketDataSource.Binance ? 10 : 30);
        bool Fresh(Entry entry) => clock.GetElapsedTime(entry.Timestamp) < lifetime;
        cache.TryGetValue(key, out Entry? previous);
        if (!refresh && previous is not null && Fresh(previous)) return previous.Quote;

        using var lease = await requestLock.AcquireAsync(key, cancellationToken);
        if (cache.TryGetValue(key, out Entry? current) && current is not null && Fresh(current) &&
            (!refresh || !ReferenceEquals(previous, current)))
            return current.Quote;

        var quote = source == MarketDataSource.Binance
            ? await binance.GetCurrentQuoteAsync(assetId, clock, cancellationToken)
            : await coinGecko.GetCurrentQuoteAsync(assetId, clock, cancellationToken);
        cache.Set(key, new Entry(quote, clock.GetTimestamp()), lifetime);
        return quote;
    }
}
