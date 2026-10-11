using CryptoRiskAnalysis.API.Exceptions;
using Microsoft.Extensions.Options;

namespace CryptoRiskAnalysis.API.Services;

public sealed class CoinGeckoOptions
{
    // Application budget, not a claim about the provider's current plan quota.
    public int RequestsPerMinute { get; set; } = 20;
}

/// <summary>One rolling-minute budget and Retry-After cooldown per application instance.</summary>
public sealed class CoinGeckoRequestBudget(IOptions<CoinGeckoOptions> options, TimeProvider clock)
{
    private readonly object _sync = new();
    private readonly Queue<long> _attempts = new();
    private TimeSpan _cooldown;
    private long _cooldownStarted;

    public void Acquire(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            var now = clock.GetTimestamp();
            while (_attempts.TryPeek(out var first) && clock.GetElapsedTime(first, now) >= TimeSpan.FromMinutes(1))
                _attempts.Dequeue();
            var wait = _cooldown - clock.GetElapsedTime(_cooldownStarted, now);
            if (_attempts.Count >= options.Value.RequestsPerMinute)
            {
                var quotaWait = TimeSpan.FromMinutes(1) - clock.GetElapsedTime(_attempts.Peek(), now);
                if (quotaWait > wait) wait = quotaWait;
            }
            if (wait > TimeSpan.Zero)
                throw new UpstreamRateLimitException("CoinGecko", wait);
            _attempts.Enqueue(now);
        }
    }

    public void Defer(TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero) return;
        lock (_sync)
        {
            var now = clock.GetTimestamp();
            if (delay > _cooldown - clock.GetElapsedTime(_cooldownStarted, now))
            {
                _cooldown = delay;
                _cooldownStarted = now;
            }
        }
    }
}
