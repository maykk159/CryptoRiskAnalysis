using System.Globalization;
using System.Net;
using System.Text.Json;
using CryptoRiskAnalysis.API.Exceptions;
using CryptoRiskAnalysis.API.Models;

namespace CryptoRiskAnalysis.API.Services;

internal static class MarketQuoteReader
{
    public static async Task<MarketQuote> ReadAsync(HttpClient client, string assetId,
        MarketDataSource source, TimeProvider clock, CancellationToken cancellationToken)
    {
        var provider = source.ToString();
        var symbol = source == MarketDataSource.Binance ? BinanceSymbolMapper.GetBinanceSymbol(assetId) : null;
        if (source == MarketDataSource.Binance && symbol is null) throw new AssetNotFoundException(assetId);
        var url = source == MarketDataSource.Binance
            ? $"https://api.binance.com/api/v3/ticker/price?symbol={symbol}"
            : $"https://api.coingecko.com/api/v3/simple/price?ids={Uri.EscapeDataString(assetId)}&vs_currencies=usd&include_last_updated_at=true&precision=full";
        try
        {
            using var response = await client.GetAsync(url, cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new UpstreamRateLimitException(provider, response.Headers.RetryAfter?.Delta ??
                    (response.Headers.RetryAfter?.Date - clock.GetUtcNow()));
            if (response.StatusCode == HttpStatusCode.NotFound) throw new AssetNotFoundException(assetId);
            if (!response.IsSuccessStatusCode) throw new MarketDataProviderException(provider, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = json.RootElement;
            var fetched = clock.GetUtcNow();
            decimal price;
            DateTimeOffset? updated = null;
            if (source == MarketDataSource.Binance)
            {
                if (root.GetProperty("symbol").GetString() != symbol)
                    throw new MarketDataProviderException(provider, "quote symbol did not match the requested asset.");
                var value = root.GetProperty("price");
                price = value.ValueKind == JsonValueKind.String
                    ? decimal.Parse(value.GetString()!, NumberStyles.Float, CultureInfo.InvariantCulture)
                    : value.GetDecimal();
            }
            else
            {
                if (!root.TryGetProperty(assetId, out var coin)) throw new AssetNotFoundException(assetId);
                price = coin.GetProperty("usd").GetDecimal();
                updated = DateTimeOffset.FromUnixTimeSeconds(coin.GetProperty("last_updated_at").GetInt64());
                if (updated > fetched.AddMinutes(1) || updated < fetched.AddMinutes(-5))
                    throw new MarketDataProviderException(provider, "quote timestamp is stale or invalid.");
            }
            if (price <= 0) throw new MarketDataProviderException(provider, "quote price must be positive.");
            return new MarketQuote(price, provider, SqliteHistoricalMarketDataStore.QuoteCurrency(source), fetched, updated);
        }
        catch (HttpRequestException ex) { throw new MarketDataProviderException(provider, ex); }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or FormatException or
            InvalidOperationException or OverflowException or ArgumentOutOfRangeException)
        {
            throw new MarketDataProviderException(provider, "quote response was malformed.", ex);
        }
    }
}
