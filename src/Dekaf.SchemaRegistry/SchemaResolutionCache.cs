using System.Collections.Concurrent;

namespace Dekaf.SchemaRegistry;

internal sealed class SchemaResolutionCache<TValue>
{
    private readonly ConcurrentDictionary<SchemaResolutionKey, TValue> _cache =
        new(SchemaResolutionKeyComparer.Instance);
    private readonly ConcurrentDictionary<SchemaResolutionKey, Entry> _inFlight =
        new(SchemaResolutionKeyComparer.Instance);
    private readonly Queue<SchemaResolutionKey> _evictionQueue = new();
    private readonly object _cacheMutationLock = new();
    private readonly int _maxCachedEntries;
    private int _cacheCount;

    internal SchemaResolutionCache(int maxCachedEntries = SubjectSchemaIdCache.MaxCachedEntries)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCachedEntries);
        _maxCachedEntries = maxCachedEntries;
    }

    internal int CachedEntryCount => Volatile.Read(ref _cacheCount);

    internal ValueTask<TValue> ResolveAsync<TState>(
        string subject,
        Schema schema,
        TState state,
        Func<TState, string, Schema, Task<TValue>> resolve,
        CancellationToken cancellationToken) =>
        ResolveAsync(subject, schema, default, state, resolve, cancellationToken);

    internal ValueTask<TValue> ResolveAsync<TState>(
        string subject,
        Schema schema,
        SchemaResolutionScope scope,
        TState state,
        Func<TState, string, Schema, Task<TValue>> resolve,
        CancellationToken cancellationToken)
    {
        var key = new SchemaResolutionKey(subject, schema, scope);
        if (_cache.TryGetValue(key, out var cached))
            return new ValueTask<TValue>(cached);

        cancellationToken.ThrowIfCancellationRequested();

        var entry = GetOrAddInFlight(key, state, resolve);
        if (_cache.TryGetValue(key, out cached))
        {
            RemoveInFlight(key, entry);
            return new ValueTask<TValue>(cached);
        }

        var task = entry.Resolution.Value;
        if (task.IsCompletedSuccessfully)
            return new ValueTask<TValue>(task.Result);

        return new ValueTask<TValue>(task.WaitAsync(cancellationToken));
    }

    internal TValue Resolve<TState>(
        string subject,
        Schema schema,
        TState state,
        Func<TState, string, Schema, Task<TValue>> resolve,
        TimeSpan timeout) =>
        Resolve(subject, schema, default, state, resolve, timeout);

    internal TValue Resolve<TState>(
        string subject,
        Schema schema,
        SchemaResolutionScope scope,
        TState state,
        Func<TState, string, Schema, Task<TValue>> resolve,
        TimeSpan timeout)
    {
        var key = new SchemaResolutionKey(subject, schema, scope);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var entry = GetOrAddInFlight(key, state, resolve);
        if (_cache.TryGetValue(key, out cached))
        {
            RemoveInFlight(key, entry);
            return cached;
        }

        var task = entry.Resolution.Value;
        return task.IsCompletedSuccessfully
            ? task.Result
            : task.WaitAsync(timeout).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private Entry GetOrAddInFlight<TState>(
        SchemaResolutionKey key,
        TState state,
        Func<TState, string, Schema, Task<TValue>> resolve) =>
        _inFlight.GetOrAdd(
            key,
            static (cacheKey, arguments) => Entry.Create(
                arguments.Owner,
                cacheKey,
                arguments.State,
                arguments.Resolve),
            (Owner: this, State: state, Resolve: resolve));

    private async Task<TValue> ResolveAndCacheAsync<TState>(
        SchemaResolutionKey key,
        Entry entry,
        TState state,
        Func<TState, string, Schema, Task<TValue>> resolve)
    {
        try
        {
            var value = await resolve(state, key.Subject, key.Schema).ConfigureAwait(false);
            CacheSuccessfulResolution(key, value);
            return value;
        }
        finally
        {
            RemoveInFlight(key, entry);
        }
    }

    private void RemoveInFlight(SchemaResolutionKey key, Entry entry) =>
        ((ICollection<KeyValuePair<SchemaResolutionKey, Entry>>)_inFlight)
        .Remove(new KeyValuePair<SchemaResolutionKey, Entry>(key, entry));

    private void CacheSuccessfulResolution(SchemaResolutionKey key, TValue value)
    {
        lock (_cacheMutationLock)
        {
            if (_cache.ContainsKey(key))
                return;

            if (_cacheCount == _maxCachedEntries)
            {
                var oldest = _evictionQueue.Dequeue();
                _cache.TryRemove(oldest, out _);
                _cacheCount--;
            }

            if (_cache.TryAdd(key, value))
            {
                _evictionQueue.Enqueue(key);
                _cacheCount++;
            }
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
                () => ObserveFault(owner.ResolveAndCacheAsync(key, entry, state, resolve)));
            return entry;
        }

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

    private readonly record struct SchemaResolutionKey(
        string Subject,
        Schema Schema,
        SchemaResolutionScope Scope);

    private sealed class SchemaResolutionKeyComparer : IEqualityComparer<SchemaResolutionKey>
    {
        internal static readonly SchemaResolutionKeyComparer Instance = new();

        public bool Equals(SchemaResolutionKey left, SchemaResolutionKey right) =>
            string.Equals(left.Subject, right.Subject, StringComparison.Ordinal) &&
            left.Scope.Equals(right.Scope) &&
            (ReferenceEquals(left.Schema, right.Schema) ||
             left.Schema.SchemaType == right.Schema.SchemaType &&
             string.Equals(left.Schema.SchemaString, right.Schema.SchemaString, StringComparison.Ordinal) &&
             ReferencesEqual(left.Schema.References, right.Schema.References) &&
             MetadataEquals(left.Schema.Metadata, right.Schema.Metadata) &&
             RuleSetEquals(left.Schema.RuleSet, right.Schema.RuleSet));

        public int GetHashCode(SchemaResolutionKey key)
        {
            var hash = new HashCode();
            hash.Add(key.Subject, StringComparer.Ordinal);
            hash.Add(key.Scope);
            if (key.Schema.References is { Count: > 0 })
            {
                hash.Add(SchemaFingerprintCache.GetHashCode(key.Schema));
            }
            else
            {
                hash.Add(key.Schema.SchemaType);
                hash.Add(key.Schema.SchemaString, StringComparer.Ordinal);
            }

            return hash.ToHashCode();
        }

        private static bool ReferencesEqual(
            IReadOnlyList<SchemaReference>? left,
            IReadOnlyList<SchemaReference>? right)
        {
            if (ReferenceEquals(left, right))
                return true;

            var count = left?.Count ?? 0;
            if (count != (right?.Count ?? 0))
                return false;

            for (var index = 0; index < count; index++)
            {
                var leftReference = left![index];
                var rightReference = right![index];
                if (!string.Equals(leftReference.Name, rightReference.Name, StringComparison.Ordinal) ||
                    !string.Equals(leftReference.Subject, rightReference.Subject, StringComparison.Ordinal) ||
                    leftReference.Version != rightReference.Version)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MetadataEquals(SchemaMetadata? left, SchemaMetadata? right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left is null || right is null)
                return false;

            return TagsEqual(left.Tags, right.Tags) &&
                   StringDictionaryEquals(left.Properties, right.Properties) &&
                   StringSetEquals(left.Sensitive, right.Sensitive);
        }

        private static bool RuleSetEquals(SchemaRuleSet? left, SchemaRuleSet? right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left is null || right is null)
                return false;

            return string.Equals(left.EnableAt, right.EnableAt, StringComparison.Ordinal) &&
                   RulesEqual(left.MigrationRules, right.MigrationRules) &&
                   RulesEqual(left.DomainRules, right.DomainRules) &&
                   RulesEqual(left.EncodingRules, right.EncodingRules);
        }

        private static bool RulesEqual(
            IReadOnlyList<SchemaRule>? left,
            IReadOnlyList<SchemaRule>? right)
        {
            if (ReferenceEquals(left, right))
                return true;

            var count = left?.Count ?? 0;
            if (count != (right?.Count ?? 0))
                return false;

            for (var index = 0; index < count; index++)
            {
                var leftRule = left![index];
                var rightRule = right![index];
                if (!string.Equals(leftRule.Name, rightRule.Name, StringComparison.Ordinal) ||
                    !string.Equals(leftRule.Doc, rightRule.Doc, StringComparison.Ordinal) ||
                    leftRule.Kind != rightRule.Kind ||
                    leftRule.Mode != rightRule.Mode ||
                    !string.Equals(leftRule.Type, rightRule.Type, StringComparison.Ordinal) ||
                    !StringSetEquals(leftRule.Tags, rightRule.Tags) ||
                    !StringDictionaryEquals(leftRule.Parameters, rightRule.Parameters) ||
                    !string.Equals(leftRule.Expr, rightRule.Expr, StringComparison.Ordinal) ||
                    !string.Equals(leftRule.OnSuccess, rightRule.OnSuccess, StringComparison.Ordinal) ||
                    !string.Equals(leftRule.OnFailure, rightRule.OnFailure, StringComparison.Ordinal) ||
                    leftRule.Disabled != rightRule.Disabled)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TagsEqual(
            IReadOnlyDictionary<string, IReadOnlySet<string>>? left,
            IReadOnlyDictionary<string, IReadOnlySet<string>>? right)
        {
            if (ReferenceEquals(left, right))
                return true;

            var count = left?.Count ?? 0;
            if (count != (right?.Count ?? 0))
                return false;
            if (count == 0)
                return true;

            if (left is Dictionary<string, IReadOnlySet<string>> leftDictionary)
            {
                foreach (var pair in leftDictionary)
                {
                    if (!right!.TryGetValue(pair.Key, out var rightTags) ||
                        !StringSetEquals(pair.Value, rightTags))
                    {
                        return false;
                    }
                }

                return true;
            }

            if (right is Dictionary<string, IReadOnlySet<string>> rightDictionary)
                return TagsEqual(rightDictionary, left);

            // Interface enumeration may box a struct enumerator. Unknown collection
            // implementations therefore remain distinct instead of allocating here.
            return false;
        }

        private static bool StringDictionaryEquals(
            IReadOnlyDictionary<string, string>? left,
            IReadOnlyDictionary<string, string>? right)
        {
            if (ReferenceEquals(left, right))
                return true;

            var count = left?.Count ?? 0;
            if (count != (right?.Count ?? 0))
                return false;
            if (count == 0)
                return true;

            if (left is Dictionary<string, string> leftDictionary)
            {
                foreach (var pair in leftDictionary)
                {
                    if (!right!.TryGetValue(pair.Key, out var rightValue) ||
                        !string.Equals(pair.Value, rightValue, StringComparison.Ordinal))
                    {
                        return false;
                    }
                }

                return true;
            }

            if (right is Dictionary<string, string> rightDictionary)
                return StringDictionaryEquals(rightDictionary, left);

            return false;
        }

        private static bool StringSetEquals(
            IReadOnlySet<string>? left,
            IReadOnlySet<string>? right)
        {
            if (ReferenceEquals(left, right))
                return true;

            var count = left?.Count ?? 0;
            if (count != (right?.Count ?? 0))
                return false;
            if (count == 0)
                return true;

            if (left is HashSet<string> leftSet)
            {
                foreach (var value in leftSet)
                {
                    if (!right!.Contains(value))
                        return false;
                }

                return true;
            }

            if (right is HashSet<string> rightSet)
                return StringSetEquals(rightSet, left);

            return false;
        }
    }
}

internal static class SchemaFingerprintCache
{
    internal static int GetHashCode(Schema schema)
    {
        if (schema.TryGetCachedFingerprint(out var cached))
            return cached;

        var fingerprint = ComputeHashCode(schema);
        schema.CacheFingerprint(fingerprint);
        return fingerprint;
    }

    private static int ComputeHashCode(Schema schema)
    {
        var hash = new HashCode();
        hash.Add(schema.SchemaType);
        hash.Add(schema.SchemaString, StringComparer.Ordinal);
        if (schema.References is { } references)
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
}

internal readonly record struct SchemaResolutionScope(string? Topic, bool IsKey);
