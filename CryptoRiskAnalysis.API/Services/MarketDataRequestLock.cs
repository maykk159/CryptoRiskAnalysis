namespace CryptoRiskAnalysis.API.Services;

/// <summary>
/// Serializes cache fills for the same provider/cache key across typed HttpClient instances.
/// Entries are removed when the active holder and all waiters have finished.
/// </summary>
public sealed class MarketDataRequestLock
{
    private readonly object _sync = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public async ValueTask<IDisposable> AcquireAsync(string key, CancellationToken cancellationToken)
    {
        Entry entry;
        lock (_sync)
        {
            if (!_entries.TryGetValue(key, out entry!))
            {
                entry = new Entry();
                _entries.Add(key, entry);
            }

            entry.ReferenceCount++;
        }

        try
        {
            await entry.Semaphore.WaitAsync(cancellationToken);
            return new Releaser(this, key, entry);
        }
        catch
        {
            ReleaseReference(key, entry);
            throw;
        }
    }

    private void Release(string key, Entry entry)
    {
        entry.Semaphore.Release();
        ReleaseReference(key, entry);
    }

    private void ReleaseReference(string key, Entry entry)
    {
        lock (_sync)
        {
            entry.ReferenceCount--;
            if (entry.ReferenceCount == 0 &&
                _entries.TryGetValue(key, out var current) &&
                ReferenceEquals(current, entry))
            {
                _entries.Remove(key);
                entry.Semaphore.Dispose();
            }
        }
    }

    private sealed class Entry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int ReferenceCount { get; set; }
    }

    private sealed class Releaser : IDisposable
    {
        private MarketDataRequestLock? _owner;
        private readonly string _key;
        private readonly Entry _entry;

        public Releaser(MarketDataRequestLock owner, string key, Entry entry)
        {
            _owner = owner;
            _key = key;
            _entry = entry;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.Release(_key, _entry);
        }
    }
}
