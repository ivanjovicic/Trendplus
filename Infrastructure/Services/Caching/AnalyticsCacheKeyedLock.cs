using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services.Caching;

/// <summary>
/// Per-key async lock with reference counting, so the dictionary never keeps entries for keys nobody waits on.
/// Prevents one instance from computing the same cache entry concurrently (stampede).
/// </summary>
public sealed class AnalyticsCacheKeyedLock
{
    private sealed class Entry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int RefCount;
    }

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public int Count => _entries.Count;

    public async Task<IDisposable> AcquireAsync(string key, CancellationToken ct)
    {
        while (true)
        {
            var entry = _entries.GetOrAdd(key, static _ => new Entry());
            var current = Volatile.Read(ref entry.RefCount);
            if (current < 0)
            {
                continue; // entry is being removed; retry with a fresh one
            }

            if (Interlocked.CompareExchange(ref entry.RefCount, current + 1, current) != current)
            {
                continue;
            }

            try
            {
                await entry.Semaphore.WaitAsync(ct);
            }
            catch
            {
                Release(key, entry, acquired: false);
                throw;
            }

            return new Releaser(this, key, entry);
        }
    }

    private void Release(string key, Entry entry, bool acquired)
    {
        if (acquired)
        {
            entry.Semaphore.Release();
        }

        if (Interlocked.Decrement(ref entry.RefCount) == 0
            && Interlocked.CompareExchange(ref entry.RefCount, -1, 0) == 0)
        {
            _entries.TryRemove(new KeyValuePair<string, Entry>(key, entry));
        }
    }

    private sealed class Releaser : IDisposable
    {
        private readonly AnalyticsCacheKeyedLock _owner;
        private readonly string _key;
        private Entry? _entry;

        public Releaser(AnalyticsCacheKeyedLock owner, string key, Entry entry)
        {
            _owner = owner;
            _key = key;
            _entry = entry;
        }

        public void Dispose()
        {
            var entry = Interlocked.Exchange(ref _entry, null);
            if (entry is not null)
            {
                _owner.Release(_key, entry, acquired: true);
            }
        }
    }
}
