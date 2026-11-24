using System.Diagnostics;
using System.Net;
using CryptoRiskAnalysis.API.Diagnostics;
using CryptoRiskAnalysis.API.Exceptions;
using CryptoRiskAnalysis.API.Models;

namespace CryptoRiskAnalysis.API.Services;

internal sealed class MarketDataResponseBufferingHandler(MarketDataSource provider, MarketDataMetrics metrics) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        HttpResponseMessage? response = null;
        var locallyRejected = false;
        try
        {
            response = await base.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                metrics.RecordProviderRateLimit(provider, "provider");

            // HttpClient normally buffers after the handler chain returns. Read the body
            // here so the resilience pipeline can time out and retry a stalled download.
            await response.Content.LoadIntoBufferAsync(cancellationToken);
            return response;
        }
        catch (UpstreamRateLimitException) when (response is null)
        {
            locallyRejected = true;
            metrics.RecordProviderRateLimit(provider, "provider_budget");
            throw;
        }
        catch
        {
            response?.Dispose();
            throw;
        }
        finally
        {
            if (!locallyRejected)
                metrics.RecordProviderAttempt(provider, Stopwatch.GetElapsedTime(startedAt));
        }
    }
}
