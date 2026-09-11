namespace CryptoRiskAnalysis.API.Models;

public sealed record MarketDataSnapshot(List<PriceData> priceHistory, decimal currentPrice,
    decimal currentVolume, decimal avgVolume, MarketDataSource Source = MarketDataSource.Binance)
{
    public void Deconstruct(out List<PriceData> history, out decimal price, out decimal volume, out decimal average)
        => (history, price, volume, average) = (priceHistory, currentPrice, currentVolume, avgVolume);

    public static implicit operator MarketDataSnapshot(
        (List<PriceData> priceHistory, decimal currentPrice, decimal currentVolume, decimal avgVolume) value)
        => new(value.priceHistory, value.currentPrice, value.currentVolume, value.avgVolume);
}
