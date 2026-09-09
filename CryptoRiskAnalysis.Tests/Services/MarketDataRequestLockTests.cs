using CryptoRiskAnalysis.API.Services;

namespace CryptoRiskAnalysis.Tests.Services;

public class MarketDataRequestLockTests
{
    [Fact]
    public async Task SameKeyWaitsUntilHolderReleasesAndCanBeReacquired()
    {
        var locks = new MarketDataRequestLock();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var first = await locks.AcquireAsync("btc", deadline.Token);
        var secondTask = locks.AcquireAsync("btc", deadline.Token).AsTask();
        var thirdTask = locks.AcquireAsync("btc", deadline.Token).AsTask();
        Assert.False(secondTask.IsCompleted);
        Assert.False(thirdTask.IsCompleted);
        first.Dispose();
        var winner = await Task.WhenAny(secondTask, thirdTask).WaitAsync(deadline.Token);
        using var second = await winner;
        var remaining = winner == secondTask ? thirdTask : secondTask;
        Assert.False(remaining.IsCompleted);
        second.Dispose();
        using var third = await remaining;
        third.Dispose();
        using var next = await locks.AcquireAsync("btc", deadline.Token);
    }

    [Fact]
    public async Task DifferentKeysDoNotBlockEachOther()
    {
        var locks = new MarketDataRequestLock();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var first = await locks.AcquireAsync("btc", deadline.Token);
        using var other = await locks.AcquireAsync("eth", deadline.Token);
        Assert.NotNull(other);
    }

    [Fact]
    public async Task CancelledWaiterDoesNotReleaseHolderOrBlockRemainingWaiters()
    {
        var locks = new MarketDataRequestLock();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();
        using var holder = await locks.AcquireAsync("btc", deadline.Token);
        var cancelled = locks.AcquireAsync("btc", cancellation.Token).AsTask();
        var survivor = locks.AcquireAsync("btc", deadline.Token).AsTask();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        Assert.False(survivor.IsCompleted);
        holder.Dispose();
        using var lease = await survivor;
        lease.Dispose();
        using var next = await locks.AcquireAsync("btc", deadline.Token);
    }

    [Fact]
    public async Task AlreadyCancelledAcquisitionLeavesKeyUsable()
    {
        var locks = new MarketDataRequestLock();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            locks.AcquireAsync("btc", cancellation.Token).AsTask());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var lease = await locks.AcquireAsync("btc", deadline.Token);
    }

    [Fact]
    public async Task DoubleDisposeDoesNotReleaseTheNextHolder()
    {
        var locks = new MarketDataRequestLock();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var first = await locks.AcquireAsync("btc", deadline.Token);
        first.Dispose();
        using var second = await locks.AcquireAsync("btc", deadline.Token);
        first.Dispose();
        var third = locks.AcquireAsync("btc", deadline.Token).AsTask();
        Assert.False(third.IsCompleted);
        second.Dispose();
        using var last = await third;
    }
}
