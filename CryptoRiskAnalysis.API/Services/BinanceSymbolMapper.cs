namespace CryptoRiskAnalysis.API.Services
{
    /// <summary>
    /// Maps CoinGecko asset IDs to Binance trading symbols
    /// Identifies which assets can use Binance API vs need CoinGecko fallback
    /// </summary>
    public static class BinanceSymbolMapper
    {
        public static readonly Dictionary<string, string?> SymbolMap = new()
        {
            // Top 20 cryptocurrencies (excluding stablecoins)
            { "bitcoin", "BTCUSDT" },
            { "ethereum", "ETHUSDT" },
            { "binancecoin", "BNBUSDT" },
            { "solana", "SOLUSDT" },
            { "ripple", "XRPUSDT" },
            { "dogecoin", "DOGEUSDT" },
            { "the-open-network", "TONUSDT" },
            { "cardano", "ADAUSDT" },
            { "shiba-inu", "SHIBUSDT" },
            { "avalanche-2", "AVAXUSDT" },
            { "tron", "TRXUSDT" },
            { "polkadot", "DOTUSDT" },
            { "bitcoin-cash", "BCHUSDT" },
            { "chainlink", "LINKUSDT" },
            { "polygon-ecosystem-token", "POLUSDT" },
            { "matic-network", "POLUSDT" }, // Legacy CoinGecko ID
            { "near", "NEARUSDT" },
            { "internet-computer", "ICPUSDT" },
            { "litecoin", "LTCUSDT" },
            { "uniswap", "UNIUSDT" },
            { "aptos", "APTUSDT" }
        };

        /// <summary>
        /// Checks if an asset is available on Binance with good liquidity
        /// </summary>
        public static bool IsAvailableOnBinance(string coinGeckoId)
        {
            return SymbolMap.TryGetValue(coinGeckoId, out var symbol) && symbol != null;
        }

        /// <summary>
        /// Gets the Binance symbol for a CoinGecko asset ID
        /// </summary>
        public static string? GetBinanceSymbol(string coinGeckoId)
        {
            return SymbolMap.TryGetValue(coinGeckoId, out var symbol) ? symbol : null;
        }
    }
}
