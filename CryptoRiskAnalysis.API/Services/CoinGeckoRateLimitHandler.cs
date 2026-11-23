using System.Net;

namespace CryptoRiskAnalysis.API.Services;

/// <summary>Placed inside resilience so every wire attempt, including retries, consumes budget.</summary>
public sealed class CoinGeckoRateLimitHandler(CoinGeckoRequestBudget budget, TimeProvider clock) : DelegatingHandler
{
    private static readonly HttpRequestOptionsKey<object> CooldownOwner = new("CoinGeckoCooldownOwner");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Options.TryGetValue(CooldownOwner, out var retryOwner);
        await budget.AcquireAsync(retryOwner, cancellationToken);
        var response = await base.SendAsync(request, cancellationToken);
        try
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var retryAfter = response.Headers.RetryAfter;
                var delay = retryAfter?.Delta ?? (retryAfter?.Date - clock.GetUtcNow()) ?? TimeSpan.FromSeconds(2);
                // Expired dates or clock skew must not bypass the shared 429 cooldown.
                if (delay <= TimeSpan.Zero) delay = TimeSpan.FromSeconds(2);
                retryOwner ??= new object();
                request.Options.Set(CooldownOwner, retryOwner);
                budget.Defer(delay, retryOwner);
            }
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }
}
