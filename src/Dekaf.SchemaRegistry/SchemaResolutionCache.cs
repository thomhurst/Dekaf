using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Dekaf.SchemaRegistry;

internal sealed class SchemaResolutionCache<TValue>
{
    private readonly ConcurrentDictionary<SchemaResolutionKey, Entry> _cache =
        new(SchemaResolutionKeyComparer.Instance);
    private int _cacheCount;

    internal int CachedEntryCount => Volatile.Read(ref _cacheCount);

    internal ValueTask<TValue> ResolveAsync<TState>(
        string subject,
        Schema schema,
        TState state,
        Func<TState, string, Schema, Task<TValue>> resolve,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var task = GetOrAdd(subject, schema, state, resolve).Resolution.Value;
        if (task.IsCompletedSuccessfully)
            return new ValueTask<TValue>(task.Result);

        return new ValueTask<TValue>(task.WaitAsync(cancellationToken));
    }

    internal TValue Resolve<TState>(
        string subject,
        Schema schema,
        TState state,
        Func<TState, string, Schema, Task<TValue>> resolve,
        TimeSpan timeout)
    {
        var task = GetOrAdd(subject, schema, state, resolve).Resolution.Value;
        return task.IsCompletedSuccessfully
            ? task.Result
            : task.WaitAsync(timeout).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private Entry GetOrAdd<TState>(
        string subject,
        Schema schema,
        TState state,
        Func<TState, string, Schema, Task<TValue>> resolve)
    {
        var key = new SchemaResolutionKey(subject, schema);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var entry = Entry.Create(this, key, state, resolve);
        if (!TryReserveSlot())
            return entry;

        entry.IsCached = true;
        if (_cache.TryAdd(key, entry))
            return entry;

        Interlocked.Decrement(ref _cacheCount);
        return _cache.TryGetValue(key, out cached) ? cached : entry;
    }

    private async Task<TValue> ResolveAndEvictOnFailureAsync<TState>(
        SchemaResolutionKey key,
        Entry entry,
        TState state,
        Func<TState, string, Schema, Task<TValue>> resolve)
    {
        try
        {
            return await resolve(state, key.Subject, key.Schema).ConfigureAwait(false);
        }
        catch
        {
            if (entry.IsCached &&
                _cache.TryGetValue(key, out var cached) &&
                ReferenceEquals(cached, entry) &&
                _cache.TryRemove(key, out _))
            {
                Interlocked.Decrement(ref _cacheCount);
            }

            throw;
        }
    }

    private bool TryReserveSlot()
    {
        while (true)
        {
            var count = Volatile.Read(ref _cacheCount);
            if (count >= SubjectSchemaIdCache.MaxCachedEntries)
                return false;

            if (Interlocked.CompareExchange(ref _cacheCount, count + 1, count) == count)
                return true;
        }
    }

    private sealed class Entry
    {
        private Entry()
        {
        }

        internal static Entry Create<TState>(
            SchemaResolutionCache<TValue> owner,
            SchemaResolutionKey key,
            TState state,
            Func<TState, string, Schema, Task<TValue>> resolve)
        {
            var entry = new Entry();
            entry.Resolution = new Lazy<Task<TValue>>(
                () => ObserveFault(owner.ResolveAndEvictOnFailureAsync(key, entry, state, resolve)));
            return entry;
        }

        internal bool IsCached;
        internal Lazy<Task<TValue>> Resolution { get; private set; } = null!;

        private static Task<TValue> ObserveFault(Task<TValue> task)
        {
            _ = task.ContinueWith(
                static completed => _ = completed.Exception,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
            return task;
        }
    }

    private readonly record struct SchemaResolutionKey(string Subject, Schema Schema);

    private sealed class SchemaResolutionKeyComparer : IEqualityComparer<SchemaResolutionKey>
    {
        internal static readonly SchemaResolutionKeyComparer Instance = new();

        public bool Equals(SchemaResolutionKey left, SchemaResolutionKey right) =>
            string.Equals(left.Subject, right.Subject, StringComparison.Ordinal) &&
            left.Schema.SchemaType == right.Schema.SchemaType &&
            string.Equals(left.Schema.SchemaString, right.Schema.SchemaString, StringComparison.Ordinal) &&
            ReferencesEqual(left.Schema.References, right.Schema.References) &&
            ReferenceEquals(left.Schema.Metadata, right.Schema.Metadata) &&
            ReferenceEquals(left.Schema.RuleSet, right.Schema.RuleSet);

        public int GetHashCode(SchemaResolutionKey key)
        {
            var hash = new HashCode();
            hash.Add(key.Subject, StringComparer.Ordinal);
            hash.Add(key.Schema.SchemaType);
            hash.Add(key.Schema.SchemaString, StringComparer.Ordinal);
            hash.Add(key.Schema.Metadata is null ? 0 : RuntimeHelpers.GetHashCode(key.Schema.Metadata));
            hash.Add(key.Schema.RuleSet is null ? 0 : RuntimeHelpers.GetHashCode(key.Schema.RuleSet));

            if (key.Schema.References is { } references)
            {
                for (var index = 0; index < references.Count; index++)
                {
                    var reference = references[index];
                    hash.Add(reference.Name, StringComparer.Ordinal);
                    hash.Add(reference.Subject, StringComparer.Ordinal);
                    hash.Add(reference.Version);
                }
            }

            return hash.ToHashCode();
        }

        private static bool ReferencesEqual(
            IReadOnlyList<SchemaReference>? left,
            IReadOnlyList<SchemaReference>? right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left is null || right is null || left.Count != right.Count)
                return false;

            for (var index = 0; index < left.Count; index++)
            {
                var leftReference = left[index];
                var rightReference = right[index];
                if (!string.Equals(leftReference.Name, rightReference.Name, StringComparison.Ordinal) ||
                    !string.Equals(leftReference.Subject, rightReference.Subject, StringComparison.Ordinal) ||
                    leftReference.Version != rightReference.Version)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
