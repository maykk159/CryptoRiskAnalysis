using System.Text.Json.Serialization;

namespace CryptoRiskAnalysis.API.Models;

[JsonConverter(typeof(JsonStringEnumConverter<MarketDataSource>))]
public enum MarketDataSource { Binance, CoinGecko }

public sealed record DailyMarketObservation(DateOnly Date, long Timestamp, decimal Price, decimal Volume);

public sealed record HistoricalMarketData(
    string AssetId, MarketDataSource Source, string QuoteCurrency,
    DateOnly From, DateOnly To, IReadOnlyList<DailyMarketObservation> Observations);
