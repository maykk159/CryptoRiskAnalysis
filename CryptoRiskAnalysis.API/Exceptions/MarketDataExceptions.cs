using System.Net;

namespace CryptoRiskAnalysis.API.Exceptions
{
    public sealed class AssetNotFoundException : Exception
    {
        public AssetNotFoundException(string assetId)
            : base($"No data found for asset: {assetId}")
        {
        }
    }

    public sealed class UpstreamRateLimitException : Exception
    {
        public UpstreamRateLimitException(string provider)
            : base($"{provider} rate limit exceeded. Please try again later.")
        {
        }
    }

    public sealed class MarketDataProviderException : Exception
    {
        public MarketDataProviderException(string provider, HttpStatusCode statusCode)
            : base($"{provider} returned HTTP {(int)statusCode}.")
        {
        }

        public MarketDataProviderException(string provider, Exception innerException)
            : base($"{provider} could not be reached.", innerException)
        {
        }
    }
}
