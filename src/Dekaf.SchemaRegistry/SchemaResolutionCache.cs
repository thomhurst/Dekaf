using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Dekaf.SchemaRegistry;

internal sealed class SchemaResolutionCache<TValue>
{
    private readonly ConcurrentDictionary<SchemaResolutionKey, TValue> _cache;
    private readonly ConcurrentDictionary<SchemaResolutionKey, Entry> _inFlight =
        new(SchemaResolutionKeyComparer.Instance);
    // Only completed-resolution mutations take this lock. Cached reads stay lock-free.
    private readonly object _mutationLock = new();
    private readonly Dictionary<SchemaResolutionKey, int> _evictionEntries;
    private EvictionNode[] _evictionNodes;
    private int _allocatedEntryCount;
    private int _oldestEntry = -1;
    private int _newestEntry = -1;
    private int _freeEntry = -1;
    private readonly int _maxCachedEntries;
    private readonly bool _cacheCompletedResolutions;
    private int _cacheCount;

    internal SchemaResolutionCache(int maxCachedEntries = SubjectSchemaIdCache.MaxCachedEntries)
        : this(maxCachedEntries, cacheCompletedResolutions: true)
    {
    }

    internal SchemaResolutionCache(int maxCachedEntries, bool cacheCompletedResolutions)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCachedEntries);
        _maxCachedEntries = maxCachedEntries;
        _cacheCompletedResolutions = cacheCompletedResolutions;
        // Mutations already serialize below; extra dictionary write stripes add no concurrency.
        _cache = new ConcurrentDictionary<SchemaResolutionKey, TValue>(
            concurrencyLevel: 1, capacity: Math.Min(maxCachedEntries, 31), SchemaResolutionKeyComparer.Instance);
        // Avoid repeated small-table growth while keeping sparse, large-capacity caches cheap.
        var initialCapacity = cacheCompletedResolutions ? Math.Min(maxCachedEntries, 16) : 0;
        _evictionEntries = new Dictionary<SchemaResolutionKey, int>(
            initialCapacity, SchemaResolutionKeyComparer.Instance);
        _evictionNodes = initialCapacity == 0 ? [] : new EvictionNode[initialCapacity];
    }

    internal int CachedEntryCount => Volatile.Read(ref _cacheCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGet(string subject, Schema schema, out TValue value) =>
        _cache.TryGetValue(new SchemaResolutionKey(subject, schema, default), out value!);

    internal bool TryRemove(string subject, Schema schema, TValue value)
    {
        if (!_cacheCompletedResolutions)
            return false;

        var entry = new KeyValuePair<SchemaResolutionKey, TValue>(
            new SchemaResolutionKey(subject, schema, default),
            value);
        lock (_mutationLock)
        {
            if (!((ICollection<KeyValuePair<SchemaResolutionKey, TValue>>)_cache).Remove(entry))
                return false;

            RemoveEvictionEntry(entry.Key);
            Volatile.Write(ref _cacheCount, _cacheCount - 1);
            return true;
        }
    }

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
        if (!_cacheCompletedResolutions)
            return;

        lock (_mutationLock)
        {
            if (!_cache.TryAdd(key, value))
                return;

            if (_cacheCount == _maxCachedEntries)
            {
                var oldest = _evictionNodes[_oldestEntry].Key;
                _cache.TryRemove(oldest, out _);
                RemoveEvictionEntry(oldest);
            }
            else
            {
                Volatile.Write(ref _cacheCount, _cacheCount + 1);
            }

            int index;
            if (_freeEntry >= 0)
            {
                index = _freeEntry;
                _freeEntry = _evictionNodes[index].Next;
            }
            else
            {
                if (_allocatedEntryCount == _evictionNodes.Length)
                {
                    var newCapacity = _evictionNodes.Length <= _maxCachedEntries / 2
                        ? _evictionNodes.Length * 2
                        : _maxCachedEntries;
                    Array.Resize(ref _evictionNodes, newCapacity);
                }
                index = _allocatedEntryCount++;
            }

            _evictionNodes[index] = new EvictionNode { Key = key, Previous = _newestEntry, Next = -1 };
            if (_newestEntry >= 0)
                _evictionNodes[_newestEntry].Next = index;
            else
                _oldestEntry = index;
            _newestEntry = index;
            _evictionEntries.Add(key, index);
        }
    }

    private void RemoveEvictionEntry(SchemaResolutionKey key)
    {
        var removed = _evictionEntries.Remove(key, out var index);
        System.Diagnostics.Debug.Assert(removed);
        ref var node = ref _evictionNodes[index];
        if (node.Previous >= 0)
            _evictionNodes[node.Previous].Next = node.Next;
        else
            _oldestEntry = node.Next;
        if (node.Next >= 0)
            _evictionNodes[node.Next].Previous = node.Previous;
        else
            _newestEntry = node.Previous;
        // Reuse bounded bookkeeping storage without retaining invalidated schemas or subjects.
        node = default;
        node.Next = _freeEntry;
        _freeEntry = index;
    }

    private struct EvictionNode
    {
        internal SchemaResolutionKey Key;
        internal int Previous;
        internal int Next;
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
            if (task.IsCompletedSuccessfully)
                return task;

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
            if (key.Schema.Metadata is not null || key.Schema.RuleSet is not null)
            {
                hash.Add(SchemaDataContractFingerprintCache.GetHashCode(key.Schema));
            }
            else if (key.Schema.References is { Count: > 0 })
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

            if (left is not Dictionary<string, IReadOnlySet<string>> leftDictionary ||
                right is not Dictionary<string, IReadOnlySet<string>> rightDictionary ||
                !Equals(leftDictionary.Comparer, rightDictionary.Comparer))
            {
                return false;
            }

            foreach (var pair in leftDictionary)
            {
                if (!rightDictionary.TryGetValue(pair.Key, out var rightTags) ||
                    !StringSetEquals(pair.Value, rightTags))
                {
                    return false;
                }
            }

            return true;
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

            if (left is not Dictionary<string, string> leftDictionary ||
                right is not Dictionary<string, string> rightDictionary ||
                !Equals(leftDictionary.Comparer, rightDictionary.Comparer))
            {
                return false;
            }

            foreach (var pair in leftDictionary)
            {
                if (!rightDictionary.TryGetValue(pair.Key, out var rightValue) ||
                    !string.Equals(pair.Value, rightValue, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
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

            if (left is not HashSet<string> leftSet ||
                right is not HashSet<string> rightSet ||
                !Equals(leftSet.Comparer, rightSet.Comparer))
            {
                return false;
            }

            foreach (var value in leftSet)
            {
                if (!rightSet.Contains(value))
                    return false;
            }

            return true;
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

internal static class SchemaDataContractFingerprintCache
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetHashCode(Schema schema)
    {
        if (schema.TryGetCachedFingerprint(out var cached))
            return cached;

        return ComputeAndCacheHashCode(schema);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ComputeAndCacheHashCode(Schema schema)
    {
        var fingerprint = ComputeHashCode(schema);
        schema.CacheFingerprint(fingerprint);
        return fingerprint;
    }

    private static int ComputeHashCode(Schema schema)
    {
        var hash = new HashCode();
        hash.Add(schema.SchemaType);
        hash.Add(schema.SchemaString, StringComparer.Ordinal);
        AddReferences(ref hash, schema.References);
        AddMetadata(ref hash, schema.Metadata);
        AddRuleSet(ref hash, schema.RuleSet);
        return hash.ToHashCode();
    }

    private static void AddReferences(ref HashCode hash, IReadOnlyList<SchemaReference>? references)
    {
        var count = references?.Count ?? 0;
        hash.Add(count);
        for (var index = 0; index < count; index++)
        {
            var reference = references![index];
            hash.Add(reference.Name, StringComparer.Ordinal);
            hash.Add(reference.Subject, StringComparer.Ordinal);
            hash.Add(reference.Version);
        }
    }

    private static void AddMetadata(ref HashCode hash, SchemaMetadata? metadata)
    {
        hash.Add(metadata is not null);
        if (metadata is null)
            return;

        hash.Add(GetTagsHashCode(metadata.Tags));
        hash.Add(GetStringDictionaryHashCode(metadata.Properties));
        hash.Add(GetStringSetHashCode(metadata.Sensitive));
    }

    private static void AddRuleSet(ref HashCode hash, SchemaRuleSet? ruleSet)
    {
        hash.Add(ruleSet is not null);
        if (ruleSet is null)
            return;

        hash.Add(ruleSet.EnableAt, StringComparer.Ordinal);
        AddRules(ref hash, ruleSet.MigrationRules);
        AddRules(ref hash, ruleSet.DomainRules);
        AddRules(ref hash, ruleSet.EncodingRules);
    }

    private static void AddRules(ref HashCode hash, IReadOnlyList<SchemaRule>? rules)
    {
        var count = rules?.Count ?? 0;
        hash.Add(count);
        for (var index = 0; index < count; index++)
        {
            var rule = rules![index];
            hash.Add(rule.Name, StringComparer.Ordinal);
            hash.Add(rule.Doc, StringComparer.Ordinal);
            hash.Add(rule.Kind);
            hash.Add(rule.Mode);
            hash.Add(rule.Type, StringComparer.Ordinal);
            hash.Add(GetStringSetHashCode(rule.Tags));
            hash.Add(GetStringDictionaryHashCode(rule.Parameters));
            hash.Add(rule.Expr, StringComparer.Ordinal);
            hash.Add(rule.OnSuccess, StringComparer.Ordinal);
            hash.Add(rule.OnFailure, StringComparer.Ordinal);
            hash.Add(rule.Disabled);
        }
    }

    private static int GetTagsHashCode(IReadOnlyDictionary<string, IReadOnlySet<string>>? tags)
    {
        var count = tags?.Count ?? 0;
        if (count == 0)
            return 0;
        if (tags is not Dictionary<string, IReadOnlySet<string>> dictionary)
            return RuntimeHelpers.GetHashCode(tags!);

        var sum = 0;
        var xor = 0;
        foreach (var pair in dictionary)
        {
            var entryHash = new HashCode();
            entryHash.Add(pair.Key, dictionary.Comparer);
            entryHash.Add(GetStringSetHashCode(pair.Value));
            AccumulateUnordered(entryHash.ToHashCode(), ref sum, ref xor);
        }

        return HashCode.Combine(dictionary.Comparer, count, sum, xor);
    }

    private static int GetStringDictionaryHashCode(IReadOnlyDictionary<string, string>? values)
    {
        var count = values?.Count ?? 0;
        if (count == 0)
            return 0;
        if (values is not Dictionary<string, string> dictionary)
            return RuntimeHelpers.GetHashCode(values!);

        var sum = 0;
        var xor = 0;
        foreach (var pair in dictionary)
        {
            var entryHash = new HashCode();
            entryHash.Add(pair.Key, dictionary.Comparer);
            entryHash.Add(pair.Value, StringComparer.Ordinal);
            AccumulateUnordered(entryHash.ToHashCode(), ref sum, ref xor);
        }

        return HashCode.Combine(dictionary.Comparer, count, sum, xor);
    }

    private static int GetStringSetHashCode(IReadOnlySet<string>? values)
    {
        var count = values?.Count ?? 0;
        if (count == 0)
            return 0;
        if (values is not HashSet<string> set)
            return RuntimeHelpers.GetHashCode(values!);

        var sum = 0;
        var xor = 0;
        foreach (var value in set)
        {
            var valueHash = set.Comparer.GetHashCode(value);
            AccumulateUnordered(valueHash, ref sum, ref xor);
        }

        return HashCode.Combine(set.Comparer, count, sum, xor);
    }

    private static void AccumulateUnordered(int value, ref int sum, ref int xor)
    {
        sum = unchecked(sum + value);
        xor ^= value;
    }
}

internal readonly record struct SchemaResolutionScope(string? Topic, bool IsKey);
