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
    private object? _cooldownOwner;

    public void Acquire(CancellationToken cancellationToken) => AcquireOrGetDelay(null, cancellationToken);

    internal async ValueTask AcquireAsync(object? retryOwner, CancellationToken cancellationToken)
    {
        // A retry timer can resume just before the monotonic cooldown ends. Recheck
        // after waiting so no attempt reaches the provider sooner than allowed.
        while (AcquireOrGetDelay(retryOwner, cancellationToken) is { Ticks: > 0 } delay)
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Ceiling(delay.TotalMilliseconds)), clock, cancellationToken);
    }

    private TimeSpan AcquireOrGetDelay(object? retryOwner, CancellationToken cancellationToken)
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
                throw new UpstreamRateLimitException("CoinGecko", quotaWait > wait ? quotaWait : wait);
            }
            if (wait > TimeSpan.Zero)
            {
                if (retryOwner is null || !ReferenceEquals(retryOwner, _cooldownOwner))
                    throw new UpstreamRateLimitException("CoinGecko", wait);
                return wait;
            }
            _attempts.Enqueue(now);
            return TimeSpan.Zero;
        }
    }

    public void Defer(TimeSpan delay) => Defer(delay, null);

    internal void Defer(TimeSpan delay, object? owner)
    {
        if (delay <= TimeSpan.Zero) return;
        lock (_sync)
        {
            var now = clock.GetTimestamp();
            if (delay > _cooldown - clock.GetElapsedTime(_cooldownStarted, now))
            {
                _cooldown = delay;
                _cooldownStarted = now;
                _cooldownOwner = owner;
            }
        }
    }
}
