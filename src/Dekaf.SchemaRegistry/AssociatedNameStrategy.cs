using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Dekaf.SchemaRegistry;

/// <summary>
/// Asynchronously resolves a Schema Registry subject name during serializer or
/// deserializer preparation.
/// </summary>
/// <remarks>
/// Implementations must provide a synchronous, allocation-free completion path after
/// a resolution is warmed. A configured custom strategy takes precedence over the
/// built-in <see cref="SubjectNameStrategy" /> value.
/// </remarks>
public interface IAsyncSubjectNameStrategy
{
    /// <summary>Resolves the subject for a Kafka topic and message component.</summary>
    ValueTask<string> GetSubjectNameAsync(
        string topic,
        string? recordType,
        bool isKey,
        CancellationToken cancellationToken = default);
}

/// <summary>Fallback used when a topic has no matching subject association.</summary>
public enum AssociatedNameFallbackStrategy
{
    /// <summary>Use <c>{topic}-key</c> or <c>{topic}-value</c>.</summary>
    TopicName,

    /// <summary>Use the fully qualified record name.</summary>
    RecordName,

    /// <summary>Use <c>{topic}-{recordName}</c>.</summary>
    TopicRecordName,

    /// <summary>Reject a missing association.</summary>
    None
}

/// <summary>Configures <see cref="AssociatedNameStrategy" />.</summary>
public sealed class AssociatedNameStrategyOptions
{
    /// <summary>
    /// Kafka cluster ID passed as the association resource namespace. Null uses the
    /// Schema Registry wildcard namespace <c>-</c>.
    /// </summary>
    public string? KafkaClusterId { get; init; }

    /// <summary>Fallback used when Schema Registry returns no association.</summary>
    public AssociatedNameFallbackStrategy FallbackStrategy { get; init; } =
        AssociatedNameFallbackStrategy.TopicName;

    /// <summary>Maximum successful topic/component/record resolutions retained.</summary>
    public int MaxCachedSubjects { get; init; } = 1000;

    /// <summary>Maximum duration of one shared Schema Registry association lookup.</summary>
    public TimeSpan LookupTimeout { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Resolves subject names from Schema Registry topic associations during preparation.
/// </summary>
/// <remarks>
/// Successful associated and fallback resolutions are cached. Failures and ambiguous
/// results are not cached. Association changes become visible through
/// <see cref="RefreshAsync" />, <see cref="Invalidate" />, or <see cref="ClearCache" />.
/// The resolved name selects the primary subject before latest-version lookup. It does
/// not replace the independent subject-name strategy used for schema references.
/// Cancellation stops only the caller's wait; a shared lookup continues so other
/// callers can use and cache its result.
/// </remarks>
public sealed class AssociatedNameStrategy : IAsyncSubjectNameStrategy
{
    /// <summary>Wildcard Schema Registry association resource namespace.</summary>
    public const string NamespaceWildcard = "-";

    private static readonly string[] KeyAssociationType = ["key"];
    private static readonly string[] ValueAssociationType = ["value"];

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly string _resourceNamespace;
    private readonly AssociatedNameFallbackStrategy _fallbackStrategy;
    private readonly int _maxCachedSubjects;
    private readonly TimeSpan _lookupTimeout;
    private readonly ConcurrentDictionary<CacheKey, string> _cache = new();
    private readonly Dictionary<CacheKey, PendingResolution> _pending = [];
    private readonly HashSet<PendingResolution> _invalidatedPending = [];
    private readonly Dictionary<CacheKey, LinkedListNode<CacheKey>> _orderNodes = [];
    private readonly LinkedList<CacheKey> _order = [];
    private readonly object _gate = new();

    /// <summary>Creates an association-backed subject-name strategy.</summary>
    public AssociatedNameStrategy(
        ISchemaRegistryClient schemaRegistry,
        AssociatedNameStrategyOptions? options = null)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        options ??= new AssociatedNameStrategyOptions();
        if (options.KafkaClusterId is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(options.KafkaClusterId);
        if (!Enum.IsDefined(options.FallbackStrategy))
            throw new ArgumentOutOfRangeException(nameof(options), options.FallbackStrategy, "Unknown fallback strategy.");
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxCachedSubjects, 1);
        if (options.LookupTimeout <= TimeSpan.Zero
            || options.LookupTimeout > TimeSpan.FromMilliseconds(int.MaxValue))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "LookupTimeout must be greater than zero and no greater than Int32.MaxValue milliseconds.");
        }

        _resourceNamespace = options.KafkaClusterId ?? NamespaceWildcard;
        _fallbackStrategy = options.FallbackStrategy;
        _maxCachedSubjects = options.MaxCachedSubjects;
        _lookupTimeout = options.LookupTimeout;
    }

    /// <summary>Gets the number of successful resolutions currently cached.</summary>
    public int CachedSubjectCount => _cache.Count;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<string> GetSubjectNameAsync(
        string topic,
        string? recordType,
        bool isKey,
        CancellationToken cancellationToken = default)
    {
        ValidateTopic(topic);
        cancellationToken.ThrowIfCancellationRequested();
        var key = new CacheKey(topic, recordType, isKey);
        if (_cache.TryGetValue(key, out var subject))
            return new ValueTask<string>(subject);

        return ResolveSlow(key, forceRefresh: false, cancellationToken);
    }

    /// <summary>
    /// Bypasses the successful-resolution cache and atomically replaces it with the
    /// current association or configured fallback.
    /// </summary>
    public ValueTask<string> RefreshAsync(
        string topic,
        string? recordType,
        bool isKey,
        CancellationToken cancellationToken = default)
    {
        ValidateTopic(topic);
        cancellationToken.ThrowIfCancellationRequested();
        return ResolveSlow(new CacheKey(topic, recordType, isKey), forceRefresh: true, cancellationToken);
    }

    /// <summary>Invalidates one exact topic/component/record resolution.</summary>
    public bool Invalidate(string topic, string? recordType, bool isKey)
    {
        ValidateTopic(topic);
        var key = new CacheKey(topic, recordType, isKey);
        lock (_gate)
        {
            if (_pending.TryGetValue(key, out var pending))
                _invalidatedPending.Add(pending);
            var removed = _cache.TryRemove(key, out _);
            RemoveOrderNode(key);
            return removed;
        }
    }

    /// <summary>Invalidates every successful resolution.</summary>
    public void ClearCache()
    {
        lock (_gate)
        {
            foreach (var pending in _pending.Values)
                _invalidatedPending.Add(pending);
            _cache.Clear();
            _order.Clear();
            _orderNodes.Clear();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private ValueTask<string> ResolveSlow(
        CacheKey key,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        Task<string> resolution;
        TaskCompletionSource<bool>? start = null;
        lock (_gate)
        {
            if (!forceRefresh && _cache.TryGetValue(key, out var cached))
                return new ValueTask<string>(cached);

            if (!_pending.TryGetValue(key, out var pending)
                || (forceRefresh && !pending.IsRefresh))
            {
                if (pending is not null)
                    _invalidatedPending.Add(pending);

                start = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                pending = new PendingResolution(forceRefresh);
                resolution = ResolveAndPublishAsync(key, pending, start.Task);
                pending.Task = resolution;
                _pending[key] = pending;
                ObserveFault(resolution);
            }
            else
            {
                resolution = pending.Task;
            }
        }

        start?.TrySetResult(true);

        if (!cancellationToken.CanBeCanceled)
            return new ValueTask<string>(resolution);

        return new ValueTask<string>(WaitWithCancellationAsync(resolution, cancellationToken));
    }

    private async Task<string> ResolveAndPublishAsync(
        CacheKey key,
        PendingResolution pending,
        Task start)
    {
        await start.ConfigureAwait(false);
        try
        {
            var subject = await LookupAsync(key).ConfigureAwait(false);
            lock (_gate)
            {
                if (!_invalidatedPending.Contains(pending))
                    Publish(key, subject);
            }

            return subject;
        }
        finally
        {
            lock (_gate)
            {
                if (_pending.TryGetValue(key, out var current)
                    && ReferenceEquals(current, pending))
                {
                    _pending.Remove(key);
                }

                _invalidatedPending.Remove(pending);
            }
        }
    }

    private async Task<string> LookupAsync(CacheKey key)
    {
        IReadOnlyList<Association> associations;
        try
        {
            associations = await SchemaRegistryOperationTimeout.ExecuteAsync(
                    cancellationToken => _schemaRegistry.GetAssociationsByResourceNameAsync(
                        key.Topic,
                        _resourceNamespace,
                        "topic",
                        key.IsKey ? KeyAssociationType : ValueAssociationType,
                        cancellationToken: cancellationToken),
                    _lookupTimeout,
                    $"Schema Registry association lookup timed out for topic '{key.Topic}'.")
                .ConfigureAwait(false);
        }
        catch (SchemaRegistryException exception) when (IsNotFound(exception))
        {
            associations = [];
        }

        if (associations.Count > 1)
        {
            throw new InvalidOperationException(
                $"Multiple {ComponentName(key.IsKey)} subject associations were found for topic '{key.Topic}'.");
        }

        if (associations.Count == 1)
        {
            var subject = associations[0].Subject;
            if (string.IsNullOrWhiteSpace(subject))
                throw new InvalidOperationException($"The subject association for topic '{key.Topic}' is empty.");
            return subject;
        }

        return ResolveFallback(key);
    }

    private string ResolveFallback(CacheKey key) => _fallbackStrategy switch
    {
        AssociatedNameFallbackStrategy.TopicName =>
            SubjectNameResolver.GetTopicSubjectName(key.Topic, key.IsKey),
        AssociatedNameFallbackStrategy.RecordName =>
            SubjectNameResolver.GetSubjectName(
                SubjectNameStrategy.RecordName,
                key.Topic,
                key.RecordType,
                key.IsKey,
                useLegacySubjectNames: false),
        AssociatedNameFallbackStrategy.TopicRecordName =>
            SubjectNameResolver.GetSubjectName(
                SubjectNameStrategy.TopicRecordName,
                key.Topic,
                key.RecordType,
                key.IsKey,
                useLegacySubjectNames: false),
        AssociatedNameFallbackStrategy.None => throw new InvalidOperationException(
            $"No {ComponentName(key.IsKey)} subject association was found for topic '{key.Topic}'."),
        _ => throw new InvalidOperationException("Unknown associated-name fallback strategy.")
    };

    private void Publish(CacheKey key, string subject)
    {
        _cache[key] = subject;
        RemoveOrderNode(key);
        var node = _order.AddLast(key);
        _orderNodes[key] = node;

        while (_cache.Count > _maxCachedSubjects)
        {
            var oldest = _order.First!;
            _order.RemoveFirst();
            _orderNodes.Remove(oldest.Value);
            _cache.TryRemove(oldest.Value, out _);
        }
    }

    private void RemoveOrderNode(CacheKey key)
    {
        if (!_orderNodes.Remove(key, out var node))
            return;

        _order.Remove(node);
    }

    private static void ValidateTopic(string topic) =>
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

    private static async Task<string> WaitWithCancellationAsync(
        Task<string> resolution,
        CancellationToken cancellationToken) =>
        await resolution.WaitAsync(cancellationToken).ConfigureAwait(false);

    private static void ObserveFault(Task task)
    {
        _ = task.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private static bool IsNotFound(SchemaRegistryException exception) =>
        exception.ErrorCode == 404 || exception.ErrorCode is >= 40400 and < 40500;

    private static string ComponentName(bool isKey) => isKey ? "key" : "value";

    private readonly record struct CacheKey(string Topic, string? RecordType, bool IsKey);

    private sealed class PendingResolution(bool isRefresh)
    {
        internal bool IsRefresh { get; } = isRefresh;
        internal Task<string> Task { get; set; } = null!;
    }
}
