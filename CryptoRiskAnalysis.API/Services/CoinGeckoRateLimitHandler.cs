using System.Net;

namespace CryptoRiskAnalysis.API.Services;

/// <summary>Placed inside resilience so every wire attempt, including retries, consumes budget.</summary>
public sealed class CoinGeckoRateLimitHandler(CoinGeckoRequestBudget budget, TimeProvider clock) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        budget.Acquire(cancellationToken);
        var response = await base.SendAsync(request, cancellationToken);
        try
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var retryAfter = response.Headers.RetryAfter;
                var delay = retryAfter?.Delta ?? (retryAfter?.Date - clock.GetUtcNow()) ?? TimeSpan.FromSeconds(2);
                // Expired dates or clock skew must not bypass the shared 429 cooldown.
                if (delay <= TimeSpan.Zero) delay = TimeSpan.FromSeconds(2);
                budget.Defer(delay);
            }
            // HttpClient normally buffers after delegating handlers return. Buffer here so
            // slow response bodies are covered by the attempt and total resilience timeouts.
            await response.Content.LoadIntoBufferAsync(cancellationToken);
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }
}
