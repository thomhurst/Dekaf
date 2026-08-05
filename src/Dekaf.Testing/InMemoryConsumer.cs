using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Serialization;
using Dekaf.Telemetry;

namespace Dekaf.Testing;

/// <summary>
/// In-memory <see cref="IKafkaConsumer{TKey,TValue}"/> backed by an <see cref="InMemoryKafkaCluster"/>.
/// </summary>
public sealed class InMemoryConsumer<TKey, TValue> :
    IKafkaConsumer<TKey, TValue>,
    IConsumerPositions,
    IConsumerPartitions,
    IConsumerOffsets,
    IConsumerCommitConfiguration
{
    private readonly object _gate = new();
    private readonly InMemoryKafkaCluster _cluster;
    private readonly IDeserializer<TKey> _keyDeserializer;
    private readonly IDeserializer<TValue> _valueDeserializer;
    // Non-null when the caller configured an IAsyncDeserializer for that component. The matching
    // synchronous slot then holds a throwing placeholder, mirroring KafkaConsumer: reaching it
    // means a consume path missed the asynchronous divert and must fail loudly.
    private readonly IAsyncDeserializer<TKey>? _asyncKeyDeserializer;
    private readonly IAsyncDeserializer<TValue>? _asyncValueDeserializer;
    private readonly bool _hasAsyncDeserializers;
    private readonly InMemoryConsumerOptions _options;
    private readonly HashSet<string> _subscription = new(StringComparer.Ordinal);
    private readonly HashSet<TopicPartition> _assignment = [];
    private readonly HashSet<TopicPartition> _paused = [];
    private readonly Dictionary<TopicPartition, long> _positions = [];
    private readonly Dictionary<TopicPartition, long> _storedOffsets = [];
    // In-doubt record under OffsetStoreTiming.AfterProcessing: delivered but not yet proven
    // processed. Staged for commit only when the next consume call or an explicit commit
    // proves it; an unwind (or close) leaves it unstaged so it is redelivered.
    private TopicPartition _inDoubtPartition;
    private long _inDoubtNextOffset = -1;
    private readonly string? _groupId;
    private readonly string? _memberId;
    private string? _subscriptionPattern;
    private bool _disposed;

    public InMemoryConsumer(InMemoryKafkaCluster cluster)
        : this(
            cluster,
            InMemorySerdeResolver.Deserializer<TKey>(),
            InMemorySerdeResolver.Deserializer<TValue>(),
            new InMemoryConsumerOptions())
    {
    }

    public InMemoryConsumer(
        InMemoryKafkaCluster cluster,
        InMemoryConsumerOptions options)
        : this(
            cluster,
            InMemorySerdeResolver.Deserializer<TKey>(),
            InMemorySerdeResolver.Deserializer<TValue>(),
            options)
    {
    }

    public InMemoryConsumer(
        InMemoryKafkaCluster cluster,
        IDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer)
        : this(cluster, keyDeserializer, valueDeserializer, new InMemoryConsumerOptions())
    {
    }

    public InMemoryConsumer(
        InMemoryKafkaCluster cluster,
        IDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer,
        InMemoryConsumerOptions options)
        : this(
            cluster,
            InMemorySerdeResolver.Required(keyDeserializer, nameof(keyDeserializer)),
            InMemorySerdeResolver.Required(valueDeserializer, nameof(valueDeserializer)),
            asyncKeyDeserializer: null,
            asyncValueDeserializer: null,
            options)
    {
    }

    /// <summary>
    /// Creates a consumer that awaits <see cref="IAsyncDeserializer{T}"/> for both components.
    /// </summary>
    public InMemoryConsumer(
        InMemoryKafkaCluster cluster,
        IAsyncDeserializer<TKey> keyDeserializer,
        IAsyncDeserializer<TValue> valueDeserializer)
        : this(cluster, keyDeserializer, valueDeserializer, new InMemoryConsumerOptions())
    {
    }

    /// <summary>
    /// Creates a consumer that awaits <see cref="IAsyncDeserializer{T}"/> for both components.
    /// </summary>
    public InMemoryConsumer(
        InMemoryKafkaCluster cluster,
        IAsyncDeserializer<TKey> keyDeserializer,
        IAsyncDeserializer<TValue> valueDeserializer,
        InMemoryConsumerOptions options)
        : this(
            cluster,
            keyDeserializer: null,
            valueDeserializer: null,
            InMemorySerdeResolver.Required(keyDeserializer, nameof(keyDeserializer)),
            InMemorySerdeResolver.Required(valueDeserializer, nameof(valueDeserializer)),
            options)
    {
    }

    /// <summary>
    /// Creates a consumer with a synchronous key deserializer and an asynchronous value deserializer.
    /// </summary>
    public InMemoryConsumer(
        InMemoryKafkaCluster cluster,
        IDeserializer<TKey> keyDeserializer,
        IAsyncDeserializer<TValue> valueDeserializer)
        : this(cluster, keyDeserializer, valueDeserializer, new InMemoryConsumerOptions())
    {
    }

    /// <summary>
    /// Creates a consumer with a synchronous key deserializer and an asynchronous value deserializer.
    /// </summary>
    public InMemoryConsumer(
        InMemoryKafkaCluster cluster,
        IDeserializer<TKey> keyDeserializer,
        IAsyncDeserializer<TValue> valueDeserializer,
        InMemoryConsumerOptions options)
        : this(
            cluster,
            InMemorySerdeResolver.Required(keyDeserializer, nameof(keyDeserializer)),
            valueDeserializer: null,
            asyncKeyDeserializer: null,
            InMemorySerdeResolver.Required(valueDeserializer, nameof(valueDeserializer)),
            options)
    {
    }

    /// <summary>
    /// Creates a consumer with an asynchronous key deserializer and a synchronous value deserializer.
    /// </summary>
    public InMemoryConsumer(
        InMemoryKafkaCluster cluster,
        IAsyncDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer)
        : this(cluster, keyDeserializer, valueDeserializer, new InMemoryConsumerOptions())
    {
    }

    /// <summary>
    /// Creates a consumer with an asynchronous key deserializer and a synchronous value deserializer.
    /// </summary>
    public InMemoryConsumer(
        InMemoryKafkaCluster cluster,
        IAsyncDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer,
        InMemoryConsumerOptions options)
        : this(
            cluster,
            keyDeserializer: null,
            InMemorySerdeResolver.Required(valueDeserializer, nameof(valueDeserializer)),
            InMemorySerdeResolver.Required(keyDeserializer, nameof(keyDeserializer)),
            asyncValueDeserializer: null,
            options)
    {
    }

    private InMemoryConsumer(
        InMemoryKafkaCluster cluster,
        IDeserializer<TKey>? keyDeserializer,
        IDeserializer<TValue>? valueDeserializer,
        IAsyncDeserializer<TKey>? asyncKeyDeserializer,
        IAsyncDeserializer<TValue>? asyncValueDeserializer,
        InMemoryConsumerOptions options)
    {
        _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
        _asyncKeyDeserializer = asyncKeyDeserializer;
        _asyncValueDeserializer = asyncValueDeserializer;
        _keyDeserializer = asyncKeyDeserializer is null
            ? keyDeserializer!
            : AsyncOnlyDeserializerPlaceholder<TKey>.Instance;
        _valueDeserializer = asyncValueDeserializer is null
            ? valueDeserializer!
            : AsyncOnlyDeserializerPlaceholder<TValue>.Instance;
        _hasAsyncDeserializers = asyncKeyDeserializer is not null || asyncValueDeserializer is not null;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        // Match KafkaConsumer, which treats an empty GroupId as "no consumer group"
        // (no coordinator, no commits). Normalizing here keeps every group-dependent
        // code path in this class consistent with production behavior.
        _groupId = string.IsNullOrEmpty(options.GroupId) ? null : options.GroupId;
        _memberId = _groupId is null ? null : options.MemberId ?? Guid.NewGuid().ToString("N");
    }

    public IReadOnlySet<string> Subscription
    {
        get
        {
            lock (_gate)
                return _subscription.ToHashSet(StringComparer.Ordinal);
        }
    }

    public string? SubscriptionPattern
    {
        get
        {
            lock (_gate)
                return _subscriptionPattern;
        }
    }

    public IReadOnlySet<TopicPartition> Assignment
    {
        get
        {
            lock (_gate)
                return GetCurrentAssignmentUnderLock().ToHashSet();
        }
    }

    public IReadOnlySet<TopicPartition> Paused
    {
        get
        {
            lock (_gate)
                return _paused.ToHashSet();
        }
    }

    public string? MemberId => _memberId;

    public ConsumerGroupMetadata? ConsumerGroupMetadata => _groupId is null || _memberId is null
        ? null
        : new ConsumerGroupMetadata
        {
            GroupId = _groupId,
            GenerationId = 1,
            MemberId = _memberId
        };

    public IConsumerPositions Positions => this;

    public IConsumerPartitions Partitions => this;

    public IConsumerOffsets Offsets => this;

    OffsetCommitMode IConsumerCommitConfiguration.OffsetCommitMode => _options.OffsetCommitMode;

    bool IConsumerCommitConfiguration.EnableAutoOffsetStore => _options.EnableAutoOffsetStore;

    bool IConsumerCommitConfiguration.HasConsumerGroup => _groupId is not null;

#if !NET10_0_OR_GREATER
    IReadOnlyCollection<string> IKafkaConsumer<TKey, TValue>.Subscription => Subscription;

    IReadOnlyCollection<TopicPartition> IKafkaConsumer<TKey, TValue>.Assignment => Assignment;

    IReadOnlyCollection<TopicPartition> IKafkaConsumer<TKey, TValue>.Paused => Paused;
#endif

#if NET10_0_OR_GREATER
    IReadOnlySet<TopicPartition> IConsumerPartitions.Assignment => Assignment;

    IReadOnlySet<TopicPartition> IConsumerPartitions.Paused => Paused;
#else
    IReadOnlyCollection<TopicPartition> IConsumerPartitions.Assignment => Assignment;

    IReadOnlyCollection<TopicPartition> IConsumerPartitions.Paused => Paused;
#endif

    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        return ValueTask.CompletedTask;
    }

    public void Subscribe(params string[] topics)
    {
        ArgumentNullException.ThrowIfNull(topics);
        ThrowIfDisposed();

        var topicPartitions = topics
            .Where(topic => !string.IsNullOrWhiteSpace(topic))
            .Distinct(StringComparer.Ordinal)
            .SelectMany(topic => _cluster.GetTopicPartitions(topic))
            .ToArray();

        lock (_gate)
        {
            _subscriptionPattern = null;
            _subscription.Clear();
            foreach (var topic in topics.Where(topic => !string.IsNullOrWhiteSpace(topic)).Distinct(StringComparer.Ordinal))
                _subscription.Add(topic);

            ReplaceAssignment(topicPartitions);
            RegisterConsumerGroupMemberUnderLock();
        }
    }

    public void Subscribe(Func<string, bool> topicFilter)
    {
        ArgumentNullException.ThrowIfNull(topicFilter);
        ThrowIfDisposed();

        var topics = _cluster.ListTopics()
            .Where(topicFilter)
            .ToArray();

        Subscribe(topics);
    }

    public void SubscribePattern(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (string.IsNullOrWhiteSpace(pattern))
            throw new ArgumentException("Subscription pattern must be specified.", nameof(pattern));

        if (string.IsNullOrWhiteSpace(_groupId))
            throw new InvalidOperationException("Server-side regex subscriptions require a consumer group ID.");

        ThrowIfDisposed();

        var regex = new Regex(pattern, RegexOptions.CultureInvariant);
        var topicPartitions = _cluster.ListTopics()
            .Where(topic => IsFullMatch(regex, topic))
            .SelectMany(topic => _cluster.GetTopicPartitions(topic))
            .ToArray();

        lock (_gate)
        {
            _subscriptionPattern = pattern;
            _subscription.Clear();
            ReplaceAssignment(topicPartitions);
            RegisterConsumerGroupMemberUnderLock();
        }
    }

    public void Unsubscribe()
    {
        ThrowIfDisposed();

        lock (_gate)
        {
            _subscriptionPattern = null;
            _subscription.Clear();
            _assignment.Clear();
            _paused.Clear();
            _positions.Clear();
            _storedOffsets.Clear();
            _inDoubtNextOffset = -1;
            UnregisterConsumerGroupMemberUnderLock();
        }
    }

    public async IAsyncEnumerable<ConsumeResult<TKey, TValue>> ConsumeAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await ConsumeOneAsync(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            if (result.HasValue)
            {
                yield return result.Value;

                // Resuming after the yield proves that the caller processed this record.
                // Do this before the loop observes cancellation, matching KafkaConsumer.
                lock (_gate)
                {
                    ProveInDoubtRecordUnderLock();
                }
            }
        }
    }

    public async ValueTask<ConsumeResult<TKey, TValue>?> ConsumeOneAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        var deadline = timeout == Timeout.InfiniteTimeSpan ? (DateTimeOffset?)null : DateTimeOffset.UtcNow + timeout;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_hasAsyncDeserializers)
            {
                if (await TryConsumeOneAsync(cancellationToken).ConfigureAwait(false) is { } asyncResult)
                    return asyncResult;
            }
            else if (TryConsumeOne(out var result))
            {
                return result;
            }

            if (deadline is null)
            {
                await _cluster.WaitForRecordsAsync(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var remaining = deadline.Value - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
                return null;

            try
            {
                await _cluster.WaitForRecordsAsync(remaining, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                return null;
            }
        }
    }

    public async IAsyncEnumerable<ConsumeBatch<TKey, TValue>> ConsumeBatchAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);
        throw new NotSupportedException("In-memory consumer does not support batch fetch wrappers. Use ConsumeAsync or ConsumeOneAsync.");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    public async IAsyncEnumerable<ConsumeRawBatch> ConsumeRawBatchAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);
        throw new NotSupportedException("In-memory consumer does not support raw batch fetch wrappers. Use InMemoryKafkaCluster.ReadRecords for raw records.");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    public void RegisterMetricForSubscription(ApplicationTelemetryMetric metric)
    {
        ArgumentNullException.ThrowIfNull(metric);
        ThrowIfDisposed();
    }

    public void UnregisterMetricFromSubscription(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ThrowIfDisposed();
    }

    public ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        lock (_gate)
        {
            // Explicit commit: the caller vouches for everything delivered so far,
            // including the in-doubt record still being processed.
            ProveInDoubtRecordUnderLock();
            CommitStoredOffsets();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask CommitAsync(
        IEnumerable<TopicPartitionOffset> offsets,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(offsets);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        if (_groupId is not null)
            _cluster.CommitOffsets(_groupId, offsets);

        return ValueTask.CompletedTask;
    }

    public void StoreOffset(ConsumeResult<TKey, TValue> result)
    {
        ThrowIfDisposed();
        if (result.IsPartitionEof)
            return;

        StoreOffset(new TopicPartitionOffset(
            result.Topic,
            result.Partition,
            checked(result.Offset + 1),
            result.LeaderEpoch ?? -1));
    }

    public void StoreOffset(TopicPartitionOffset offset)
    {
        ThrowIfDisposed();

        lock (_gate)
            _storedOffsets[new TopicPartition(offset.Topic, offset.Partition)] = offset.Offset;
    }

    public ValueTask CloseAsync(CancellationToken cancellationToken = default) =>
        CloseAsync(new ConsumerCloseOptions(), cancellationToken);

    public ValueTask CloseAsync(
        ConsumerCloseOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Enum.IsDefined(options.GroupMembershipOperation))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.GroupMembershipOperation,
                "The group membership operation is invalid.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (_disposed)
            return ValueTask.CompletedTask;

        lock (_gate)
        {
            if (_disposed)
                return ValueTask.CompletedTask;

            if (_options.OffsetCommitMode == OffsetCommitMode.Auto)
                CommitStoredOffsets();

            // The in-memory cluster has no broker session timer. Always unregister on close so
            // RemainInGroup cannot create an immortal member that permanently owns partitions.
            UnregisterConsumerGroupMemberUnderLock();
            _disposed = true;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return CloseAsync();
    }

    public ValueTask<long?> GetCommittedOffsetAsync(
        TopicPartition partition,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var offset = _groupId is null
            ? null
            : _cluster.GetCommittedOffset(_groupId, partition);

        return ValueTask.FromResult(offset);
    }

    public long? GetPosition(TopicPartition partition)
    {
        ThrowIfDisposed();

        lock (_gate)
            return _positions.TryGetValue(partition, out var position) ? position : null;
    }

    public void Seek(TopicPartitionOffset offset)
    {
        ThrowIfDisposed();

        lock (_gate)
        {
            var partition = new TopicPartition(offset.Topic, offset.Partition);
            DiscardInDoubtRecordUnderLock(partition);
            _positions[partition] = offset.Offset;
            if (_options.EnableAutoOffsetStore)
                _storedOffsets[partition] = offset.Offset;
        }
    }

    public void SeekToBeginning(params TopicPartition[] partitions)
    {
        ArgumentNullException.ThrowIfNull(partitions);
        ThrowIfDisposed();

        lock (_gate)
        {
            foreach (var partition in SelectTargetPartitions(partitions))
            {
                var position = _cluster.GetWatermarks(partition).Low;
                DiscardInDoubtRecordUnderLock(partition);
                _positions[partition] = position;
                if (_options.EnableAutoOffsetStore)
                    _storedOffsets[partition] = position;
            }
        }
    }

    public void SeekToEnd(params TopicPartition[] partitions)
    {
        ArgumentNullException.ThrowIfNull(partitions);
        ThrowIfDisposed();

        lock (_gate)
        {
            foreach (var partition in SelectTargetPartitions(partitions))
            {
                var position = _cluster.GetWatermarks(partition).High;
                DiscardInDoubtRecordUnderLock(partition);
                _positions[partition] = position;
                if (_options.EnableAutoOffsetStore)
                    _storedOffsets[partition] = position;
            }
        }
    }

    public void Assign(params TopicPartition[] partitions)
    {
        ArgumentNullException.ThrowIfNull(partitions);
        ThrowIfDisposed();

        lock (_gate)
        {
            _subscriptionPattern = null;
            _subscription.Clear();
            ReplaceAssignment(partitions);
            RegisterConsumerGroupMemberUnderLock();
        }
    }

    public void Unassign()
    {
        ThrowIfDisposed();

        lock (_gate)
        {
            _assignment.Clear();
            _paused.Clear();
            _positions.Clear();
            _storedOffsets.Clear();
            _inDoubtNextOffset = -1;
            UnregisterConsumerGroupMemberUnderLock();
        }
    }

    public void IncrementalAssign(IEnumerable<TopicPartitionOffset> partitions)
    {
        ArgumentNullException.ThrowIfNull(partitions);
        ThrowIfDisposed();

        lock (_gate)
        {
            foreach (var offset in partitions)
            {
                var partition = new TopicPartition(offset.Topic, offset.Partition);
                var position = offset.Offset >= 0 ? offset.Offset : GetStartOffset(partition);
                DiscardInDoubtRecordUnderLock(partition);
                _assignment.Add(partition);
                _positions[partition] = position;
            }

            RegisterConsumerGroupMemberUnderLock();
        }
    }

    public void IncrementalUnassign(IEnumerable<TopicPartition> partitions)
    {
        ArgumentNullException.ThrowIfNull(partitions);
        ThrowIfDisposed();

        lock (_gate)
        {
            foreach (var partition in partitions)
            {
                _assignment.Remove(partition);
                _paused.Remove(partition);
                _positions.Remove(partition);
                _storedOffsets.Remove(partition);
                DiscardInDoubtRecordUnderLock(partition);
            }

            RegisterConsumerGroupMemberUnderLock();
        }
    }

    public void Pause(params TopicPartition[] partitions)
    {
        ArgumentNullException.ThrowIfNull(partitions);
        ThrowIfDisposed();

        lock (_gate)
        {
            foreach (var partition in partitions)
                _paused.Add(partition);
        }
    }

    public void Resume(params TopicPartition[] partitions)
    {
        ArgumentNullException.ThrowIfNull(partitions);
        ThrowIfDisposed();

        lock (_gate)
        {
            foreach (var partition in partitions)
                _paused.Remove(partition);
        }
    }

    public ValueTask<IReadOnlyDictionary<TopicPartition, long>> GetOffsetsForTimesAsync(
        IEnumerable<TopicPartitionTimestamp> timestampsToSearch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(timestampsToSearch);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var result = timestampsToSearch.ToDictionary(
            item => item.TopicPartition,
            item => _cluster.GetOffsetForTimestamp(item.TopicPartition, item.Timestamp));

        return ValueTask.FromResult<IReadOnlyDictionary<TopicPartition, long>>(result);
    }

    public WatermarkOffsets? GetWatermarkOffsets(TopicPartition topicPartition)
    {
        ThrowIfDisposed();
        return _cluster.GetWatermarks(topicPartition);
    }

    public ValueTask<WatermarkOffsets> QueryWatermarkOffsetsAsync(
        TopicPartition topicPartition,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        return ValueTask.FromResult(_cluster.GetWatermarks(topicPartition));
    }

    private bool TryConsumeOne(out ConsumeResult<TKey, TValue> result)
    {
        var previousRecordProven = false;
        while (true)
        {
            if (!TrySelectRecord(ref previousRecordProven, out var partition, out var record, out var position))
            {
                result = default;
                return false;
            }

            // Run user deserializers before advancing any delivery or commit state. A failure
            // leaves the selected offset untouched so the next consume retries the record.
            var selectedResult = ToConsumeResult(partition, record);

            if (!TryAdvancePosition(partition, record, position))
                continue;

            result = selectedResult;
            return true;
        }
    }

    /// <summary>
    /// Consume path used when at least one component has an <see cref="IAsyncDeserializer{T}"/>.
    /// Deliberate mirror of <see cref="TryConsumeOne"/>: both drive the same selection and
    /// delivery bookkeeping helpers so their offset semantics cannot drift apart.
    /// </summary>
    private async ValueTask<ConsumeResult<TKey, TValue>?> TryConsumeOneAsync(CancellationToken cancellationToken)
    {
        var previousRecordProven = false;
        while (true)
        {
            if (!TrySelectRecord(ref previousRecordProven, out var partition, out var record, out var position))
                return null;

            var selectedResult = await ToConsumeResultAsync(partition, record, cancellationToken).ConfigureAwait(false);

            if (!TryAdvancePosition(partition, record, position))
                continue;

            return selectedResult;
        }
    }

    private bool TrySelectRecord(
        ref bool previousRecordProven,
        out TopicPartition selectedPartition,
        out InMemoryRecord selectedRecord,
        out long selectedPosition)
    {
        lock (_gate)
        {
            if (!previousRecordProven)
            {
                // A new consume call proves the previously delivered record was processed
                // (poll contract) — stage it before selecting the next one.
                ProveInDoubtRecordUnderLock();
                previousRecordProven = true;
            }

            foreach (var partition in GetCurrentAssignmentUnderLock().OrderBy(item => item.Topic, StringComparer.Ordinal).ThenBy(item => item.Partition))
            {
                if (_paused.Contains(partition))
                    continue;

                if (!_positions.TryGetValue(partition, out var position))
                    continue;

                if (!_cluster.TryRead(partition, position, out var record))
                    continue;

                selectedPartition = partition;
                selectedRecord = record;
                selectedPosition = position;
                return true;
            }
        }

        selectedPartition = default;
        selectedRecord = null!;
        selectedPosition = -1;
        return false;
    }

    /// <summary>
    /// Publishes a delivered record's offset state. Returns false when the position moved while
    /// user deserializers ran, in which case the caller must reselect instead of publishing a
    /// stale result.
    /// </summary>
    private bool TryAdvancePosition(TopicPartition partition, InMemoryRecord record, long expectedPosition)
    {
        lock (_gate)
        {
            if (!_positions.TryGetValue(partition, out var currentPosition) || currentPosition != expectedPosition)
                return false;

            _positions[partition] = record.Offset + 1;
            if (_options.OffsetStoreTiming == OffsetStoreTiming.OnDelivery)
            {
                if (_options.EnableAutoOffsetStore)
                    _storedOffsets[partition] = record.Offset + 1;
                if (_options.OffsetCommitMode == OffsetCommitMode.Auto)
                    CommitStoredOffsets();
            }
            else
            {
                _inDoubtPartition = partition;
                _inDoubtNextOffset = record.Offset + 1;
            }

            return true;
        }
    }

    /// <summary>
    /// Stages (and under Auto mode commits) the in-doubt record's offset. Called when the
    /// application demonstrably moved past it: a subsequent consume call or explicit commit.
    /// </summary>
    private void ProveInDoubtRecordUnderLock()
    {
        if (_inDoubtNextOffset < 0)
            return;

        if (_options.EnableAutoOffsetStore)
            _storedOffsets[_inDoubtPartition] = _inDoubtNextOffset;

        _inDoubtNextOffset = -1;

        if (_options.OffsetCommitMode == OffsetCommitMode.Auto)
            CommitStoredOffsets();
    }

    private void DiscardInDoubtRecordUnderLock(TopicPartition partition)
    {
        if (_inDoubtNextOffset >= 0 && _inDoubtPartition.Equals(partition))
            _inDoubtNextOffset = -1;
    }

    private ConsumeResult<TKey, TValue> ToConsumeResult(TopicPartition topicPartition, InMemoryRecord record)
    {
        try
        {
            return new ConsumeResult<TKey, TValue>(
                topic: topicPartition.Topic,
                partition: topicPartition.Partition,
                offset: record.Offset,
                keyData: record.Key,
                isKeyNull: record.IsKeyNull,
                valueData: record.Value,
                isValueNull: record.IsValueNull,
                headers: record.Headers,
                timestampMs: record.TimestampMs,
                timestampType: TimestampType.CreateTime,
                leaderEpoch: null,
                keyDeserializer: _keyDeserializer,
                valueDeserializer: _valueDeserializer);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw CreateDeserializationException(
                ConsumeResult<TKey, TValue>.LastDeserializationOrigin,
                topicPartition,
                record,
                ex);
        }
    }

    /// <summary>
    /// Deserializes a record via the configured <see cref="IAsyncDeserializer{T}"/> implementations
    /// (falling back to the synchronous deserializer for a component without one) and builds the
    /// result from the awaited values. Null-ness semantics mirror the eager
    /// <see cref="ConsumeResult{TKey,TValue}"/> constructor used by the synchronous path: null keys
    /// skip the deserializer, null values invoke it with empty data and <c>IsNull = true</c>.
    /// </summary>
    private async ValueTask<ConsumeResult<TKey, TValue>> ToConsumeResultAsync(
        TopicPartition topicPartition,
        InMemoryRecord record,
        CancellationToken cancellationToken)
    {
        TKey? key = default;
        if (!record.IsKeyNull)
        {
            var keyContext = new SerializationContext
            {
                Topic = topicPartition.Topic,
                Component = SerializationComponent.Key,
                IsNull = false
            };

            try
            {
                key = _asyncKeyDeserializer is not null
                    ? await _asyncKeyDeserializer.DeserializeAsync(record.Key, keyContext, cancellationToken).ConfigureAwait(false)
                    : _keyDeserializer.Deserialize(record.Key, keyContext);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw CreateDeserializationException(DeserializationExceptionOrigin.Key, topicPartition, record, ex);
            }
        }

        var valueContext = new SerializationContext
        {
            Topic = topicPartition.Topic,
            Component = SerializationComponent.Value,
            IsNull = record.IsValueNull
        };
        var valueData = record.IsValueNull ? ReadOnlyMemory<byte>.Empty : record.Value;

        TValue value;
        try
        {
            value = _asyncValueDeserializer is not null
                ? await _asyncValueDeserializer.DeserializeAsync(valueData, valueContext, cancellationToken).ConfigureAwait(false)
                : _valueDeserializer.Deserialize(valueData, valueContext);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw CreateDeserializationException(DeserializationExceptionOrigin.Value, topicPartition, record, ex);
        }

        return new ConsumeResult<TKey, TValue>(
            topicPartition.Topic,
            topicPartition.Partition,
            record.Offset,
            key,
            value,
            record.Headers,
            record.TimestampMs,
            TimestampType.CreateTime,
            leaderEpoch: null);
    }

    private static RecordDeserializationException CreateDeserializationException(
        DeserializationExceptionOrigin origin,
        TopicPartition topicPartition,
        InMemoryRecord record,
        Exception innerException) =>
        ConsumeResult<TKey, TValue>.CreateDeserializationException(
            origin,
            topicPartition.Topic,
            topicPartition.Partition,
            record.Offset,
            record.TimestampMs,
            TimestampType.CreateTime,
            record.Key,
            record.IsKeyNull,
            record.Value,
            record.IsValueNull,
            record.Headers,
            pooledHeaders: null,
            pooledHeaderCount: 0,
            innerException);

    private void ReplaceAssignment(IEnumerable<TopicPartition> partitions)
    {
        var nextPositions = new Dictionary<TopicPartition, long>();
        foreach (var partition in partitions.Distinct())
            nextPositions[partition] = GetStartOffset(partition);

        _assignment.Clear();
        _paused.Clear();
        _positions.Clear();
        _storedOffsets.Clear();
        _inDoubtNextOffset = -1;

        foreach (var (partition, position) in nextPositions)
        {
            _assignment.Add(partition);
            _positions[partition] = position;
        }
    }

    private long GetStartOffset(TopicPartition partition)
    {
        if (_groupId is not null &&
            _cluster.GetCommittedOffset(_groupId, partition) is { } committed)
        {
            return committed;
        }

        return _options.AutoOffsetReset switch
        {
            AutoOffsetReset.Earliest => _cluster.GetWatermarks(partition).Low,
            AutoOffsetReset.Latest => _cluster.GetWatermarks(partition).High,
            AutoOffsetReset.ByDuration => _cluster.GetOffsetForTimestamp(
                partition,
                DateTimeOffset.UtcNow.Subtract(_options.AutoOffsetResetDuration ?? TimeSpan.Zero).ToUnixTimeMilliseconds()),
            AutoOffsetReset.None => throw new InvalidOperationException($"No committed offset exists for {partition}."),
            _ => _cluster.GetWatermarks(partition).High
        };
    }

    private IEnumerable<TopicPartition> SelectTargetPartitions(TopicPartition[] partitions)
    {
        return partitions.Length == 0 ? _assignment.ToArray() : partitions;
    }

    private static bool IsFullMatch(Regex regex, string topic)
    {
        var match = regex.Match(topic);
        return match.Success && match.Index == 0 && match.Length == topic.Length;
    }

    private void CommitStoredOffsets()
        => CommitOffsetsFrom(_storedOffsets);

    private void CommitOffsetsFrom(Dictionary<TopicPartition, long> positions)
    {
        if (_groupId is null)
            return;

        var assignment = GetCurrentAssignmentUnderLock();
        var offsets = positions
            .Where(item => assignment.Contains(item.Key))
            .Select(item => new TopicPartitionOffset(
                item.Key.Topic,
                item.Key.Partition,
                item.Value))
            .ToArray();

        if (offsets.Length > 0)
            _cluster.CommitOffsets(_groupId, offsets);
    }

    private IReadOnlySet<TopicPartition> GetCurrentAssignmentUnderLock()
    {
        if (_groupId is null || _memberId is null)
            return _assignment;

        var owned = _cluster.GetConsumerGroupAssignment(_groupId, _memberId);
        return owned.Where(_assignment.Contains).ToHashSet();
    }

    private void RegisterConsumerGroupMemberUnderLock()
    {
        if (_groupId is null || _memberId is null)
            return;

        _cluster.RegisterConsumerGroupMember(_groupId, _memberId, _assignment);
    }

    private void UnregisterConsumerGroupMemberUnderLock()
    {
        if (_groupId is null || _memberId is null)
            return;

        _cluster.UnregisterConsumerGroupMember(_groupId, _memberId);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
