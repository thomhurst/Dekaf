using System.Diagnostics.CodeAnalysis;
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
    IBoundedKafkaConsumer<TKey, TValue>,
    IConsumerPositions,
    IConsumerCommittedOffsets,
    IConsumerPartitions,
    IConsumerOffsets,
    IConsumerBatchOffsetStore,
    IConsumerCommitConfiguration
{
    private readonly record struct SnapshotPartitionBound(
        TopicPartition Partition,
        long EndOffset);

    private readonly record struct PendingAutoCommitAdvancement(
        long Position,
        bool ResumePartition,
        TaskCompletionSource<CapturedAutoCommitProof> Completion,
        CapturedCommitOffsets CapturedCommit);

    private readonly record struct CapturedCommitOffsets(
        TopicPartitionOffset[] Offsets,
        int ConsumerGroupGeneration);

    private readonly record struct CapturedAutoCommitProof(
        TopicPartition Partition,
        long Position,
        CapturedCommitOffsets Commit,
        bool SharedWaiter,
        TaskCompletionSource<CapturedAutoCommitProof>? SharedWaiterCompletion);

    private readonly record struct ConsumeAttemptResult(
        ConsumeResult<TKey, TValue>? Result,
        bool StopPolling);

    private readonly record struct ConsumerSelectionVersion(
        int ConsumerState,
        int ConsumerGroupGeneration);

    private readonly object _gate = new();
    private readonly InMemoryKafkaCluster _cluster;
    private readonly KafkaFaultPlan? _indexedFaultPlan;
    private readonly IDeserializer<TKey> _keyDeserializer;
    private readonly IDeserializer<TValue> _valueDeserializer;
    // Non-null when the caller configured an IAsyncDeserializer for that component. The matching
    // synchronous slot then holds a throwing placeholder, mirroring KafkaConsumer: reaching it
    // means a consume path missed the asynchronous divert and must fail loudly.
    private readonly IAsyncDeserializer<TKey>? _asyncKeyDeserializer;
    private readonly IAsyncDeserializer<TValue>? _asyncValueDeserializer;
    private readonly bool _hasAsyncDeserializers;
    private readonly bool _borrowStoredRecords;
    private readonly InMemoryConsumerOptions _options;
    private readonly HashSet<string> _subscription = new(StringComparer.Ordinal);
    private readonly HashSet<TopicPartition> _assignment = [];
    private readonly HashSet<TopicPartition> _paused = [];
    private readonly Dictionary<TopicPartition, long> _positions = [];
    private readonly Dictionary<TopicPartition, TopicPartitionOffset> _storedOffsets = [];
    private KafkaFaultScope[] _commitFaultScopes = new KafkaFaultScope[4];
    private TopicPartitionOffset[] _commitOffsets = new TopicPartitionOffset[4];
    private Dictionary<TopicPartition, PendingAutoCommitAdvancement>? _pendingAutoCommitAdvancements;
    private int _storedOffsetsVersion;
    private long _potentialFaultPlanVersion = -1;
    private int _potentialFaultConsumerStateVersion = -1;
    private int _potentialFaultConsumerGroupGeneration = -1;
    private int _potentialFaultStoredOffsetsVersion = -1;
    private bool _hasPotentialConsumerFault;
    private IReadOnlySet<TopicPartition>? _potentialFaultAssignment;
    private IReadOnlySet<TopicPartition>? _potentialFaultActiveResources;
    private int _potentialFaultAssignmentConsumerStateVersion = -1;
    private int _potentialFaultAssignmentConsumerGroupGeneration = -1;
    private TopicPartition[] _orderedAssignment = [];
    private int _orderedAssignmentConsumerStateVersion = -1;
    private int _orderedAssignmentConsumerGroupGeneration = -1;
    private int _committableOffsetConsumerStateVersion = -1;
    private int _committableOffsetConsumerGroupGeneration = -1;
    private int _committableOffsetStoredOffsetsVersion = -1;
    private bool _hasCommittableStoredOffset;
    // In-doubt record under OffsetStoreTiming.AfterProcessing: delivered but not yet proven
    // processed. Staged for commit only when the next consume call or an explicit commit
    // proves it; an unwind (or close) leaves it unstaged so it is redelivered.
    private TopicPartition _inDoubtPartition;
    private long _inDoubtNextOffset = -1;
    // Wakes a shared ConsumeAsync stream after the commit-proof owner aborts. The next
    // poll consumes this marker and retries the unchanged in-doubt proof.
    private TopicPartition _retryInDoubtPartition;
    private long _retryInDoubtNextOffset = -1;
    private int _consumerStateVersion;
    private bool _snapshotActive;
    private readonly string? _groupId;
    private readonly string? _memberId;
    private int _consumerGroupGeneration = -1;
    private long _consumerGroupRegistrationId;
    private string? _subscriptionPattern;
    private TaskCompletionSource? _autoCommitAdvancementWaiterEntered;
    private Task? _closeTask;
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
        _indexedFaultPlan = _cluster.FaultPlan as KafkaFaultPlan;
        _asyncKeyDeserializer = asyncKeyDeserializer;
        _asyncValueDeserializer = asyncValueDeserializer;
        _keyDeserializer = asyncKeyDeserializer is null
            ? keyDeserializer!
            : AsyncOnlyDeserializerPlaceholder<TKey>.Instance;
        _valueDeserializer = asyncValueDeserializer is null
            ? valueDeserializer!
            : AsyncOnlyDeserializerPlaceholder<TValue>.Instance;
        _hasAsyncDeserializers = asyncKeyDeserializer is not null || asyncValueDeserializer is not null;
        _borrowStoredRecords = CanBorrowStoredInput(_keyDeserializer, asyncKeyDeserializer) &&
                               CanBorrowStoredInput(_valueDeserializer, asyncValueDeserializer);
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
            {
                var paused = _paused.ToHashSet();
                if (_pendingAutoCommitAdvancements is not null)
                {
                    foreach (var (partition, pending) in _pendingAutoCommitAdvancements)
                    {
                        if (pending.ResumePartition)
                            paused.Remove(partition);
                    }
                }

                return paused;
            }
        }
    }

    public string? MemberId => _memberId;

    public ConsumerGroupMetadata? ConsumerGroupMetadata
    {
        get
        {
            lock (_gate)
            {
                return _groupId is null || _memberId is null || _consumerGroupGeneration < 0
                    ? null
                    : new ConsumerGroupMetadata
                    {
                        GroupId = _groupId,
                        GenerationId = _consumerGroupGeneration,
                        MemberId = _memberId
                    };
            }
        }
    }

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
        var subscription = topics
            .Where(topic => !string.IsNullOrWhiteSpace(topic))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        lock (_gate)
        {
            ThrowIfAutoCommitAdvancementPendingUnderLock();
            ApplyGroupTransitionFaults();
            ThrowIfDisposed();
            var topicPartitions = subscription
                .SelectMany(topic => _cluster.GetTopicPartitions(topic))
                .ToArray();
            _subscriptionPattern = null;
            _subscription.Clear();
            foreach (var topic in subscription)
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
            ThrowIfAutoCommitAdvancementPendingUnderLock();
            ApplyGroupTransitionFaults();
            ThrowIfDisposed();
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
            ThrowIfAutoCommitAdvancementPendingUnderLock();
            if (_groupId is not null)
                ApplyGroupTransitionFaultUnderLock(KafkaFaultOperation.Rebalance);

            _subscriptionPattern = null;
            _subscription.Clear();
            _assignment.Clear();
            _paused.Clear();
            _positions.Clear();
            _storedOffsets.Clear();
            _storedOffsetsVersion++;
            ClearInDoubtRecordUnderLock();
            _consumerStateVersion++;
            UnregisterConsumerGroupMemberUnderLock();
        }
    }

    public async IAsyncEnumerable<ConsumeResult<TKey, TValue>> ConsumeAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await ConsumeOneAsync(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            if (result is not { } consumed)
            {
                await WaitForCurrentAutoCommitDeliveryAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            yield return consumed;

            // Resuming after the yield proves that the caller processed this record.
            // Do this before the loop observes cancellation, matching KafkaConsumer.
            ThrowIfDisposed();
            var capturedProof = await ApplyInDoubtAutoCommitFaultAsync(
                CancellationToken.None).ConfigureAwait(false);
            ThrowIfDisposed();
            if (capturedProof is { SharedWaiter: true })
            {
                await WaitForSharedAutoCommitDeliveryAsync(
                    capturedProof.Value,
                    cancellationToken).ConfigureAwait(false);
                continue;
            }
            try
            {
                lock (_gate)
                {
                    ProveInDoubtRecordUnderLock(capturedProof);
                }
            }
            finally
            {
                CompleteSharedAutoCommitWaiters(capturedProof);
            }
        }
    }

    public async IAsyncEnumerable<ConsumeResult<TKey, TValue>> ConsumeSnapshotAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        SnapshotPartitionBound[] partitions;
        int consumerStateVersion;
        int consumerGroupGeneration;
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_snapshotActive)
            {
                throw new InvalidOperationException(
                    "Only one snapshot enumeration can be active on a consumer.");
            }

            _snapshotActive = true;
            try
            {
                var assignment = GetCurrentAssignmentUnderLock(out consumerGroupGeneration);
                partitions = new SnapshotPartitionBound[assignment.Count];
                var partitionIndex = 0;
                foreach (var partition in assignment)
                {
                    if (_paused.Contains(partition))
                    {
                        throw new InvalidOperationException(
                            $"Cannot start a snapshot while assigned partition {partition} is paused.");
                    }

                    partitions[partitionIndex++] = new SnapshotPartitionBound(
                        partition,
                        _cluster.GetWatermarks(partition).High);
                }

                Array.Sort(partitions, static (left, right) =>
                {
                    var topicComparison = StringComparer.Ordinal.Compare(
                        left.Partition.Topic,
                        right.Partition.Topic);
                    return topicComparison != 0
                        ? topicComparison
                        : left.Partition.Partition.CompareTo(right.Partition.Partition);
                });
                consumerStateVersion = _consumerStateVersion;
            }
            catch
            {
                _snapshotActive = false;
                throw;
            }
        }

        try
        {
            var applyRecordFaults = HasPotentialConsumerFault();
            var activePartitionIndex = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var capturedProof = await ApplyInDoubtAutoCommitFaultAsync(
                    cancellationToken).ConfigureAwait(false);
                ThrowIfDisposed();
                if (capturedProof is { SharedWaiter: true })
                    ThrowSnapshotStateChanged();

                TopicPartition partition;
                InMemoryRecord record;
                long position;
                bool complete;
                bool pendingAutoCommitAdvancement;
                try
                {
                    lock (_gate)
                    {
                        ThrowIfSnapshotStateChangedUnderLock(
                            consumerStateVersion,
                            consumerGroupGeneration);

                        ProveInDoubtRecordUnderLock(capturedProof);
                        complete = !TrySelectSnapshotRecordUnderLock(
                                partitions,
                                ref activePartitionIndex,
                                out partition,
                                out record,
                                out position,
                                out pendingAutoCommitAdvancement) &&
                            !pendingAutoCommitAdvancement;
                    }
                }
                finally
                {
                    CompleteSharedAutoCommitWaiters(capturedProof);
                }

                if (pendingAutoCommitAdvancement)
                {
                    await WaitForAutoCommitAdvancementAsync(
                        partition,
                        cancellationToken).ConfigureAwait(false);
                    applyRecordFaults = HasPotentialConsumerFault(
                        consumerStateVersion,
                        consumerGroupGeneration);
                    continue;
                }

                if (complete)
                {
                    CapturedCommitOffsets completionOffsets;
                    ValueTask commitFaultApplication;
                    lock (_gate)
                    {
                        ThrowIfSnapshotStateChangedUnderLock(
                            consumerStateVersion,
                            consumerGroupGeneration);
                        completionOffsets = CaptureStoredAutoCommitOffsetsUnderLock(
                            cancellationToken,
                            out commitFaultApplication);
                    }

                    await commitFaultApplication.ConfigureAwait(false);
                    lock (_gate)
                    {
                        ThrowIfSnapshotStateChangedUnderLock(
                            consumerStateVersion,
                            consumerGroupGeneration);
                        CommitCapturedOffsetsUnderLock(in completionOffsets);
                    }

                    yield break;
                }

                var fetchScope = new KafkaFaultScope(
                    KafkaFaultOperation.Fetch,
                    partition.Topic,
                    partition.Partition,
                    _groupId);
                if (applyRecordFaults && _cluster.FaultPlan.HasMatchingFault(fetchScope))
                {
                    await _cluster.FaultPlan.ApplyAsync(
                        fetchScope,
                        cancellationToken).ConfigureAwait(false);
                    ThrowIfSnapshotStateChanged(
                        consumerStateVersion,
                        consumerGroupGeneration);
                }

                var result = _hasAsyncDeserializers
                    ? await ToConsumeResultAsync(
                        partition,
                        record,
                        cancellationToken).ConfigureAwait(false)
                    : ToConsumeResult(partition, record);

                applyRecordFaults = HasPotentialConsumerFault(
                    consumerStateVersion,
                    consumerGroupGeneration);

                var consumeScope = new KafkaFaultScope(
                    KafkaFaultOperation.Consume,
                    partition.Topic,
                    partition.Partition,
                    _groupId);
                if (applyRecordFaults && _cluster.FaultPlan.HasMatchingFault(consumeScope))
                {
                    await _cluster.FaultPlan.ApplyAsync(
                        consumeScope,
                        cancellationToken).ConfigureAwait(false);
                    ThrowIfSnapshotStateChanged(
                        consumerStateVersion,
                        consumerGroupGeneration);
                }

                if (!TryPrepareSnapshotOnDeliveryAutoCommitFault(
                        partition,
                        position,
                        record.Offset + 1,
                        consumerStateVersion,
                        consumerGroupGeneration,
                        cancellationToken,
                        out var hasAutoCommitReservation,
                        out var autoCommitFaultApplication))
                {
                    continue;
                }

                if (hasAutoCommitReservation)
                {
                    await ApplyOnDeliveryAutoCommitFaultAsync(
                        autoCommitFaultApplication,
                        partition,
                        position,
                        cancellationToken).ConfigureAwait(false);
                }

                var positionAdvanced = hasAutoCommitReservation
                    ? TryAdvanceReservedSnapshotPosition(
                        partition,
                        record,
                        position,
                        consumerStateVersion,
                        consumerGroupGeneration)
                    : TryAdvanceSnapshotPosition(
                        partition,
                        record,
                        position,
                        consumerStateVersion,
                        consumerGroupGeneration);
                if (!positionAdvanced)
                    continue;

                yield return result;
                // The caller can add fault rules while enumeration is suspended at the yield.
                // Reuse the fault-cache state reads to validate the snapshot before a commit
                // fault can be consumed on the next iteration.
                applyRecordFaults = HasPotentialConsumerFault(
                    consumerStateVersion,
                    consumerGroupGeneration);
            }
        }
        finally
        {
            lock (_gate)
                _snapshotActive = false;
        }
    }

    public ValueTask<TopicPartitionOffset> SeekToTailAsync(
        TopicPartition partition,
        int offsetCount,
        CancellationToken cancellationToken = default)
    {
        if (offsetCount < 0)
            throw new ArgumentOutOfRangeException(nameof(offsetCount), "Offset count cannot be negative.");

        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        var watermarks = _cluster.GetWatermarks(partition);
        var resolved = new TopicPartitionOffset(
            partition.Topic,
            partition.Partition,
            Math.Max(watermarks.Low, watermarks.High - offsetCount));
        Seek(resolved);
        return ValueTask.FromResult(resolved);
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

            if (!_hasAsyncDeserializers && !HasPotentialConsumerFault())
            {
                if (TryConsumeOne(out var result, out var retryWithFaults))
                    return result;
                if (retryWithFaults)
                    continue;
            }
            else
            {
                var attempt = await TryConsumeOneAsync(cancellationToken).ConfigureAwait(false);
                if (attempt.Result is { } asyncResult)
                    return asyncResult;
                if (attempt.StopPolling)
                    return null;
            }

            if (deadline is null)
            {
                await _cluster.WaitForRecordsAsync(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                ThrowIfDisposed();
                continue;
            }

            var remaining = deadline.Value - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
                return null;

            try
            {
                await _cluster.WaitForRecordsAsync(remaining, cancellationToken).ConfigureAwait(false);
                ThrowIfDisposed();
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

    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        var groupId = GetCommitGroupId();

        TopicPartitionOffset? inDoubtRecord;
        CapturedCommitOffsets capturedCommit;
        bool hasFaultApplication;
        ValueTask faultApplication;
        lock (_gate)
        {
            inDoubtRecord = _inDoubtNextOffset >= 0
                ? new TopicPartitionOffset(
                    _inDoubtPartition.Topic,
                    _inDoubtPartition.Partition,
                    _inDoubtNextOffset)
                : null;
            var inDoubtCommitOffset = _options.EnableAutoOffsetStore
                ? inDoubtRecord
                : null;
            capturedCommit = CaptureCommitOffsetsUnderLock(inDoubtCommitOffset);
            hasFaultApplication = TryApplyMatchingCapturedCommitFault(
                groupId,
                capturedCommit.Offsets,
                cancellationToken,
                out faultApplication);
        }

        if (hasFaultApplication)
        {
            await faultApplication.ConfigureAwait(false);
            ThrowIfDisposed();
        }

        lock (_gate)
        {
            ThrowIfDisposed();
            if (IsCapturedCommitCurrentUnderLock(in capturedCommit) &&
                inDoubtRecord is { } capturedInDoubt &&
                _inDoubtNextOffset == capturedInDoubt.Offset &&
                _inDoubtPartition.Topic == capturedInDoubt.Topic &&
                _inDoubtPartition.Partition == capturedInDoubt.Partition)
            {
                // Explicit commit proves the record that was in doubt when this call began.
                if (_options.EnableAutoOffsetStore)
                    StoreOffsetUnderLock(capturedInDoubt);
                ClearInDoubtRecordUnderLock();
            }

            CommitCapturedOffsetsUnderLock(in capturedCommit);
        }
    }

    public async ValueTask CommitAsync(
        IEnumerable<TopicPartitionOffset> offsets,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(offsets);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        var groupId = GetCommitGroupId();
        var faultPlan = _cluster.FaultPlan;
        IReadOnlyList<TopicPartitionOffset>? offsetList = offsets as IReadOnlyList<TopicPartitionOffset>;
        if (faultPlan.Count == 0)
        {
            offsetList ??= offsets.ToArray();
            _cluster.CommitOffsets(groupId, offsetList);
            return;
        }

        var faultApplied = false;
        if (faultPlan is KafkaFaultPlan indexedPlan &&
            offsetList is null &&
            indexedPlan.HasPotentialMatch(KafkaFaultOperation.Commit, groupId))
        {
            offsetList = offsets.ToArray();
            if (indexedPlan.TryApplyLeadingUnconditionalCommitFault(
                    groupId,
                    out var unconditionalApplication,
                    cancellationToken))
            {
                await unconditionalApplication.ConfigureAwait(false);
                ThrowIfDisposed();
                faultApplied = true;
            }
        }

        offsetList ??= offsets.ToArray();
        if (!faultApplied &&
            offsetList.Count == 0 &&
            faultPlan is KafkaFaultPlan emptyInputPlan &&
            emptyInputPlan.HasPotentialMatch(KafkaFaultOperation.Commit, groupId) &&
            emptyInputPlan.TryApplyUnconditionalCommitFault(
                groupId,
                out var emptyInputApplication,
                cancellationToken))
        {
            await emptyInputApplication.ConfigureAwait(false);
            ThrowIfDisposed();
            faultApplied = true;
        }

        if (!faultApplied && faultPlan is KafkaFaultPlan orderedPlan)
        {
            if (orderedPlan.HasPotentialMatch(KafkaFaultOperation.Commit, groupId))
            {
                if (ReferenceEquals(offsetList, offsets))
                    offsetList = offsetList.ToArray();

                if (orderedPlan.TryApplyFirstMatchingCommitFault(
                        groupId,
                        offsetList,
                        out var faultApplication,
                        cancellationToken))
                {
                    await faultApplication.ConfigureAwait(false);
                    ThrowIfDisposed();
                }
            }
        }
        else if (!faultApplied && faultPlan.Count != 0)
        {
            if (ReferenceEquals(offsetList, offsets))
                offsetList = offsetList.ToArray();

            var candidateScopes = new KafkaFaultScope[offsetList.Count + 1];
            candidateScopes[0] = new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: groupId);
            for (var index = 0; index < offsetList.Count; index++)
            {
                var offset = offsetList[index];
                candidateScopes[index + 1] = new KafkaFaultScope(
                    KafkaFaultOperation.Commit,
                    offset.Topic,
                    offset.Partition,
                    groupId);
            }

            if (faultPlan.TryApplyFirstMatchingFault(
                    candidateScopes,
                    out var faultApplication,
                    cancellationToken))
            {
                await faultApplication.ConfigureAwait(false);
                ThrowIfDisposed();
            }
        }

        _cluster.CommitOffsets(groupId, offsetList);
    }

    private string GetCommitGroupId() => _groupId
        ?? throw new InvalidOperationException(
            "Offset commits require a consumer group. Configure one with ConsumerBuilder.WithGroupId(...).");

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
        TopicPartitionOffsetValidator.Validate(offset, nameof(offset));
        ApplySynchronousFaultIfMatching(new KafkaFaultScope(
            KafkaFaultOperation.StoreOffset,
            offset.Topic,
            offset.Partition,
            _groupId));
        ThrowIfDisposed();

        lock (_gate)
        {
            ThrowIfDisposed();
            StoreOffsetUnderLock(offset);
        }
    }

    public void StoreOffsets<TOffsets>(TOffsets offsets)
        where TOffsets : IReadOnlyList<TopicPartitionOffset>
    {
        if (offsets is null)
            throw new ArgumentNullException(nameof(offsets));
        ThrowIfDisposed();

        var count = offsets.Count;
        var snapshot = new TopicPartitionOffset[count];
        for (var index = 0; index < count; index++)
        {
            var offset = offsets[index];
            TopicPartitionOffsetValidator.Validate(offset, nameof(offsets));
            snapshot[index] = offset;
        }

        for (var index = 0; index < count; index++)
        {
            var offset = snapshot[index];
            ApplySynchronousFaultIfMatching(new KafkaFaultScope(
                KafkaFaultOperation.StoreOffset,
                offset.Topic,
                offset.Partition,
                _groupId));
            ThrowIfDisposed();
        }

        lock (_gate)
        {
            ThrowIfDisposed();
            for (var index = 0; index < count; index++)
                StoreOffsetUnderLock(snapshot[index]);
        }
    }

    public void StoreOffsets(ReadOnlySpan<TopicPartitionOffset> offsets)
    {
        ThrowIfDisposed();

        var snapshot = offsets.ToArray();
        for (var index = 0; index < offsets.Length; index++)
            TopicPartitionOffsetValidator.Validate(snapshot[index], nameof(offsets));

        for (var index = 0; index < snapshot.Length; index++)
        {
            var offset = snapshot[index];
            ApplySynchronousFaultIfMatching(new KafkaFaultScope(
                KafkaFaultOperation.StoreOffset,
                offset.Topic,
                offset.Partition,
                _groupId));
            ThrowIfDisposed();
        }

        lock (_gate)
        {
            ThrowIfDisposed();
            for (var index = 0; index < snapshot.Length; index++)
                StoreOffsetUnderLock(snapshot[index]);
        }
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

        Task closeTask;
        TaskCompletionSource? closeCompletion = null;
        lock (_gate)
        {
            if (_closeTask is { IsCompleted: false } activeClose)
            {
                closeTask = activeClose;
            }
            else
            {
                if (_disposed)
                    return ValueTask.CompletedTask;

                closeCompletion = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                closeTask = closeCompletion.Task;
                _closeTask = closeTask;
            }
        }

        if (closeCompletion is not null)
        {
            _ = RunCloseAsync(closeCompletion, cancellationToken);
            return new ValueTask(closeTask);
        }

        return cancellationToken.CanBeCanceled
            ? new ValueTask(closeTask.WaitAsync(cancellationToken))
            : new ValueTask(closeTask);
    }

    private async ValueTask RunCloseAsync(
        TaskCompletionSource completion,
        CancellationToken cancellationToken)
    {
        CapturedCommitOffsets? capturedCommit = null;
        Exception? failure = null;
        try
        {
            ValueTask commitFaultApplication;
            lock (_gate)
            {
                capturedCommit = CaptureStoredAutoCommitOffsetsUnderLock(
                    cancellationToken,
                    out commitFaultApplication);
            }

            await commitFaultApplication.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            failure = ex;
            capturedCommit = null;
        }

        try
        {
            await CompleteClose(capturedCommit).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            failure = failure is null
                ? ex
                : new AggregateException(failure, ex);
        }

        if (failure is null)
            completion.TrySetResult();
        else if (failure is OperationCanceledException canceled)
            completion.TrySetCanceled(canceled.CancellationToken);
        else
            completion.TrySetException(failure);
    }

    private ValueTask CompleteClose(CapturedCommitOffsets? capturedCommit)
    {
        lock (_gate)
        {
            if (_disposed)
                return ValueTask.CompletedTask;

            if (capturedCommit is { } captured)
                CommitCapturedOffsetsUnderLock(in captured);

            // The in-memory cluster has no broker session timer. Always unregister on close so
            // RemainInGroup cannot create an immortal member that permanently owns partitions.
            UnregisterConsumerGroupMemberUnderLock();
            _consumerStateVersion++;
            _disposed = true;
            _autoCommitAdvancementWaiterEntered?.TrySetResult();
            _autoCommitAdvancementWaiterEntered = null;
            if (_pendingAutoCommitAdvancements is not null)
            {
                foreach (var (partition, pending) in _pendingAutoCommitAdvancements)
                {
                    _paused.Remove(partition);
                    pending.Completion.TrySetResult(new CapturedAutoCommitProof(
                        partition,
                        pending.Position,
                        pending.CapturedCommit,
                        SharedWaiter: true,
                        SharedWaiterCompletion: null));
                }

                _pendingAutoCommitAdvancements = null;
            }
        }

        _cluster.SignalRecordsChanged();
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

    public ValueTask<IReadOnlyDictionary<TopicPartition, TopicPartitionOffset>> GetCommittedOffsetsAsync(
        IReadOnlyCollection<TopicPartition> partitions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(partitions);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var offsets = _groupId is null
            ? new Dictionary<TopicPartition, TopicPartitionOffset>()
            : _cluster.GetCommittedOffsets(_groupId, partitions);

        return ValueTask.FromResult<IReadOnlyDictionary<TopicPartition, TopicPartitionOffset>>(offsets);
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
            ThrowIfSnapshotActiveUnderLock();
            var partition = new TopicPartition(offset.Topic, offset.Partition);
            DiscardInDoubtRecordUnderLock(partition);
            _positions[partition] = offset.Offset;
            if (_options.EnableAutoOffsetStore)
                StoreOffsetUnderLock(partition, offset.Offset);
        }
    }

    public void SeekToBeginning(params TopicPartition[] partitions)
    {
        ArgumentNullException.ThrowIfNull(partitions);
        ThrowIfDisposed();

        lock (_gate)
        {
            ThrowIfSnapshotActiveUnderLock();
            foreach (var partition in SelectTargetPartitions(partitions))
            {
                var position = _cluster.GetWatermarks(partition).Low;
                DiscardInDoubtRecordUnderLock(partition);
                _positions[partition] = position;
                if (_options.EnableAutoOffsetStore)
                    StoreOffsetUnderLock(partition, position);
            }
        }
    }

    public void SeekToEnd(params TopicPartition[] partitions)
    {
        ArgumentNullException.ThrowIfNull(partitions);
        ThrowIfDisposed();

        lock (_gate)
        {
            ThrowIfSnapshotActiveUnderLock();
            foreach (var partition in SelectTargetPartitions(partitions))
            {
                var position = _cluster.GetWatermarks(partition).High;
                DiscardInDoubtRecordUnderLock(partition);
                _positions[partition] = position;
                if (_options.EnableAutoOffsetStore)
                    StoreOffsetUnderLock(partition, position);
            }
        }
    }

    public void Assign(params TopicPartition[] partitions)
    {
        ArgumentNullException.ThrowIfNull(partitions);
        ThrowIfDisposed();

        lock (_gate)
        {
            ThrowIfAutoCommitAdvancementPendingUnderLock();
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
            ThrowIfAutoCommitAdvancementPendingUnderLock();
            _assignment.Clear();
            _paused.Clear();
            _positions.Clear();
            _storedOffsets.Clear();
            _storedOffsetsVersion++;
            ClearInDoubtRecordUnderLock();
            _consumerStateVersion++;
            UnregisterConsumerGroupMemberUnderLock();
        }
    }

    public void IncrementalAssign(IEnumerable<TopicPartitionOffset> partitions)
    {
        ArgumentNullException.ThrowIfNull(partitions);
        ThrowIfDisposed();

        lock (_gate)
        {
            ThrowIfAutoCommitAdvancementPendingUnderLock();
            var changed = false;
            foreach (var offset in partitions)
            {
                changed = true;
                var partition = new TopicPartition(offset.Topic, offset.Partition);
                var position = offset.Offset >= 0 ? offset.Offset : GetStartOffset(partition);
                DiscardInDoubtRecordUnderLock(partition);
                _assignment.Add(partition);
                _positions[partition] = position;
            }

            if (changed)
            {
                EnsureCommitOffsetCapacity(_assignment.Count);
                _consumerStateVersion++;
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
            ThrowIfAutoCommitAdvancementPendingUnderLock();
            var changed = false;
            foreach (var partition in partitions)
            {
                changed = true;
                _assignment.Remove(partition);
                _paused.Remove(partition);
                _positions.Remove(partition);
                if (_storedOffsets.Remove(partition))
                    _storedOffsetsVersion++;
                DiscardInDoubtRecordUnderLock(partition);
            }

            if (changed)
                _consumerStateVersion++;

            RegisterConsumerGroupMemberUnderLock();
        }
    }

    public void Pause(params TopicPartition[] partitions)
    {
        ArgumentNullException.ThrowIfNull(partitions);
        ThrowIfDisposed();

        lock (_gate)
        {
            if (SetPauseStateUnderLock(partitions, pause: true))
                _consumerStateVersion++;
        }
    }

    public void Resume(params TopicPartition[] partitions)
    {
        ArgumentNullException.ThrowIfNull(partitions);
        ThrowIfDisposed();

        lock (_gate)
        {
            if (SetPauseStateUnderLock(partitions, pause: false))
                _consumerStateVersion++;
        }
    }

    private bool SetPauseStateUnderLock(IReadOnlyList<TopicPartition> partitions, bool pause)
    {
        var changed = false;
        for (var index = 0; index < partitions.Count; index++)
        {
            var partition = partitions[index];
            if (_pendingAutoCommitAdvancements is not null &&
                _pendingAutoCommitAdvancements.TryGetValue(partition, out var pending))
            {
                var resumePartition = !pause;
                if (pending.ResumePartition != resumePartition)
                {
                    _pendingAutoCommitAdvancements[partition] = pending with
                    {
                        ResumePartition = resumePartition
                    };
                    changed = true;
                }
            }
            else
            {
                changed |= pause ? _paused.Add(partition) : _paused.Remove(partition);
            }
        }

        return changed;
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

    private bool TryConsumeOne(
        out ConsumeResult<TKey, TValue> result,
        out bool retryWithFaults)
    {
        retryWithFaults = false;
        var previousRecordProven = false;
        while (true)
        {
            if (!TrySelectRecordWithVersion(
                    ref previousRecordProven,
                    capturedProof: null,
                    out var partition,
                    out var record,
                    out var position,
                    out var selectionVersion))
            {
                result = default;
                return false;
            }

            // Run user deserializers before advancing any delivery or commit state. A failure
            // leaves the selected offset untouched so the next consume retries the record.
            var selectedResult = ToConsumeResult(partition, record);

            if (Volatile.Read(ref _potentialFaultPlanVersion) != GetFaultPlanVersion() &&
                HasPotentialConsumerFault())
            {
                retryWithFaults = true;
                result = default;
                return false;
            }

            if (!TryAdvancePosition(partition, record, position, in selectionVersion))
                continue;

            result = selectedResult;
            return true;
        }
    }

    private bool TrySelectSnapshotRecordUnderLock(
        SnapshotPartitionBound[] partitions,
        ref int activePartitionIndex,
        out TopicPartition selectedPartition,
        out InMemoryRecord selectedRecord,
        out long selectedPosition,
        out bool pendingAutoCommitAdvancement)
    {
        if (IsInDoubtAutoCommitAdvancementPendingUnderLock())
        {
            selectedPartition = _inDoubtPartition;
            selectedRecord = null!;
            selectedPosition = -1;
            pendingAutoCommitAdvancement = true;
            return false;
        }

        while (activePartitionIndex < partitions.Length)
        {
            var bound = partitions[activePartitionIndex];
            if (_pendingAutoCommitAdvancements is not null &&
                _pendingAutoCommitAdvancements.ContainsKey(bound.Partition))
            {
                selectedPartition = bound.Partition;
                selectedRecord = null!;
                selectedPosition = -1;
                pendingAutoCommitAdvancement = true;
                return false;
            }

            if (_positions.TryGetValue(bound.Partition, out var position) &&
                position < bound.EndOffset)
            {
                if (_cluster.TryRead(
                        bound.Partition,
                        position,
                        _options.IsolationLevel,
                        _borrowStoredRecords,
                        out var record,
                        out var blockedByOngoingTransaction) &&
                    record.Offset < bound.EndOffset)
                {
                    selectedPartition = bound.Partition;
                    selectedRecord = record;
                    selectedPosition = position;
                    pendingAutoCommitAdvancement = false;
                    return true;
                }

                if (blockedByOngoingTransaction)
                {
                    activePartitionIndex++;
                    continue;
                }
            }

            if (!_positions.TryGetValue(bound.Partition, out position)
                || position < bound.EndOffset)
            {
                _positions[bound.Partition] = bound.EndOffset;
                if (_options.EnableAutoOffsetStore)
                    StoreOffsetUnderLock(bound.Partition, bound.EndOffset);
            }
            activePartitionIndex++;
        }

        selectedPartition = default;
        selectedRecord = null!;
        selectedPosition = -1;
        pendingAutoCommitAdvancement = false;
        return false;
    }

    private ValueTask WaitForAutoCommitAdvancementAsync(
        TopicPartition partition,
        CancellationToken cancellationToken)
    {
        Task? advancementChanged;
        lock (_gate)
        {
            advancementChanged = _pendingAutoCommitAdvancements is not null &&
                                 _pendingAutoCommitAdvancements.TryGetValue(partition, out var pending)
                ? pending.Completion.Task
                : null;
            if (advancementChanged is not null)
            {
                _autoCommitAdvancementWaiterEntered?.TrySetResult();
                _autoCommitAdvancementWaiterEntered = null;
            }
        }

        return advancementChanged is null
            ? ValueTask.CompletedTask
            : new ValueTask(advancementChanged.WaitAsync(cancellationToken));
    }

    internal Task WaitUntilAutoCommitAdvancementWaiterEnteredAsync()
    {
        lock (_gate)
        {
            if (_disposed)
                return Task.CompletedTask;

            if (_autoCommitAdvancementWaiterEntered is { Task.IsCompleted: false } existing)
                return existing.Task;

            _autoCommitAdvancementWaiterEntered = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            return _autoCommitAdvancementWaiterEntered.Task;
        }
    }

    private void ThrowIfSnapshotActiveUnderLock()
    {
        if (_snapshotActive)
        {
            throw new InvalidOperationException(
                "Cannot change consumer position while a snapshot enumeration is active.");
        }

        ThrowIfAutoCommitAdvancementPendingUnderLock();
    }

    private void ThrowIfSnapshotStateChangedUnderLock(
        int consumerStateVersion,
        int consumerGroupGeneration)
    {
        ThrowIfDisposed();

        if (_consumerStateVersion != consumerStateVersion)
            ThrowSnapshotStateChanged();

        if (consumerGroupGeneration < 0
            || _groupId is null
            || _memberId is null
            || _cluster.GetConsumerGroupGeneration(_groupId) == consumerGroupGeneration)
        {
            return;
        }

        ThrowSnapshotStateChanged();
    }

    private void ThrowIfSnapshotStateChanged(
        int consumerStateVersion,
        int consumerGroupGeneration)
    {
        lock (_gate)
        {
            ThrowIfSnapshotStateChangedUnderLock(
                consumerStateVersion,
                consumerGroupGeneration);
        }
    }

    [DoesNotReturn]
    private static void ThrowSnapshotStateChanged() =>
        throw new InvalidOperationException(
            "The consumer assignment or pause state changed while a snapshot enumeration was active.");

    /// <summary>
    /// Consume path used when fault injection is active or a component has an
    /// <see cref="IAsyncDeserializer{T}"/>. Faults run before position advancement so a retry
    /// sees the same record.
    /// </summary>
    private async ValueTask<ConsumeAttemptResult> TryConsumeOneAsync(
        CancellationToken cancellationToken)
    {
        var capturedProof = await ApplyInDoubtAutoCommitFaultAsync(
            cancellationToken).ConfigureAwait(false);
        ThrowIfDisposed();
        if (capturedProof is { SharedWaiter: true })
            return new ConsumeAttemptResult(Result: null, StopPolling: true);

        try
        {
            var previousRecordProven = false;
            while (true)
            {
                if (!TrySelectRecordWithVersion(
                        ref previousRecordProven,
                        capturedProof,
                        out var partition,
                        out var record,
                        out var position,
                        out var selectionVersion))
                    return new ConsumeAttemptResult(Result: null, StopPolling: false);

                var fetchScope = new KafkaFaultScope(
                    KafkaFaultOperation.Fetch,
                    partition.Topic,
                    partition.Partition,
                    _groupId);
                if (HasMatchingFault(fetchScope))
                {
                    await _cluster.FaultPlan.ApplyAsync(
                        fetchScope,
                        cancellationToken).ConfigureAwait(false);
                    ThrowIfDisposed();
                    if (!IsRecordSelectionCurrent(partition, position, in selectionVersion))
                        continue;
                }

                var selectedResult = _hasAsyncDeserializers
                    ? await ToConsumeResultAsync(partition, record, cancellationToken).ConfigureAwait(false)
                    : ToConsumeResult(partition, record);

                ThrowIfDisposed();
                if (!IsRecordSelectionCurrent(partition, position, in selectionVersion))
                    continue;

                var consumeScope = new KafkaFaultScope(
                    KafkaFaultOperation.Consume,
                    partition.Topic,
                    partition.Partition,
                    _groupId);
                if (HasMatchingFault(consumeScope))
                {
                    await _cluster.FaultPlan.ApplyAsync(
                        consumeScope,
                        cancellationToken).ConfigureAwait(false);
                    ThrowIfDisposed();
                    if (!IsRecordSelectionCurrent(partition, position, in selectionVersion))
                        continue;
                }

                if (!TryPrepareOnDeliveryAutoCommitFault(
                        partition,
                        position,
                        record.Offset + 1,
                        in selectionVersion,
                        cancellationToken,
                        out var hasAutoCommitReservation,
                        out var autoCommitFaultApplication))
                {
                    continue;
                }

                if (hasAutoCommitReservation)
                {
                    await ApplyOnDeliveryAutoCommitFaultAsync(
                        autoCommitFaultApplication,
                        partition,
                        position,
                        cancellationToken).ConfigureAwait(false);
                }

                var positionAdvanced = hasAutoCommitReservation
                    ? TryAdvanceReservedPosition(partition, record, position, in selectionVersion)
                    : TryAdvancePosition(partition, record, position, in selectionVersion);
                if (!positionAdvanced)
                    continue;

                return new ConsumeAttemptResult(selectedResult, StopPolling: false);
            }
        }
        finally
        {
            CompleteSharedAutoCommitWaiters(capturedProof);
        }
    }

    private bool TrySelectRecordWithVersion(
        ref bool previousRecordProven,
        CapturedAutoCommitProof? capturedProof,
        out TopicPartition selectedPartition,
        out InMemoryRecord selectedRecord,
        out long selectedPosition,
        out ConsumerSelectionVersion selectionVersion)
    {
        lock (_gate)
        {
            if (!previousRecordProven)
            {
                // A new consume call proves the previously delivered record was processed
                // (poll contract) — stage it before selecting the next one.
                ProveInDoubtRecordUnderLock(capturedProof);
                if (IsInDoubtAutoCommitAdvancementPendingUnderLock())
                {
                    selectedPartition = default;
                    selectedRecord = null!;
                    selectedPosition = -1;
                    selectionVersion = default;
                    return false;
                }

                previousRecordProven = true;
            }

            var assignment = GetOrderedCurrentAssignmentUnderLock(out var consumerGroupGeneration);
            selectionVersion = new ConsumerSelectionVersion(
                _consumerStateVersion,
                consumerGroupGeneration);
            for (var partitionIndex = 0; partitionIndex < assignment.Length; partitionIndex++)
            {
                var partition = assignment[partitionIndex];
                if (_paused.Contains(partition))
                    continue;

                if (!_positions.TryGetValue(partition, out var position))
                    continue;

                if (!_cluster.TryRead(
                        partition,
                        position,
                        _options.IsolationLevel,
                        _borrowStoredRecords,
                        out var record))
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
        selectionVersion = default;
        return false;
    }

    /// <summary>
    /// Publishes a delivered record's offset state. Returns false when the position moved while
    /// user deserializers ran, in which case the caller must reselect instead of publishing a
    /// stale result.
    /// </summary>
    private bool TryAdvancePosition(
        TopicPartition partition,
        InMemoryRecord record,
        long expectedPosition,
        in ConsumerSelectionVersion selectionVersion)
    {
        lock (_gate)
        {
            if (_pendingAutoCommitAdvancements is not null &&
                _pendingAutoCommitAdvancements.ContainsKey(partition))
            {
                return false;
            }

            if (!IsRecordSelectionCurrentUnderLock(
                    partition,
                    expectedPosition,
                    in selectionVersion))
            {
                return false;
            }

            AdvancePositionUnderLock(partition, record);
            return true;
        }
    }

    private bool TryAdvanceReservedPosition(
        TopicPartition partition,
        InMemoryRecord record,
        long expectedPosition,
        in ConsumerSelectionVersion selectionVersion)
    {
        lock (_gate)
        {
            if (_pendingAutoCommitAdvancements is null ||
                !_pendingAutoCommitAdvancements.TryGetValue(partition, out var pending) ||
                pending.Position != expectedPosition)
            {
                return false;
            }

            try
            {
                if (!IsReservedRecordSelectionCurrentUnderLock(
                        partition,
                        expectedPosition,
                        pending.ResumePartition,
                        in selectionVersion))
                    return false;

                AdvancePositionUnderLock(partition, record, pending.CapturedCommit);
                return true;
            }
            finally
            {
                ClearPendingAutoCommitAdvancementUnderLock(partition, expectedPosition);
            }
        }
    }

    private bool IsRecordSelectionCurrent(
        TopicPartition partition,
        long expectedPosition,
        in ConsumerSelectionVersion selectionVersion)
    {
        lock (_gate)
        {
            return IsRecordSelectionCurrentUnderLock(
                partition,
                expectedPosition,
                in selectionVersion);
        }
    }

    private bool IsRecordSelectionCurrentUnderLock(
        TopicPartition partition,
        long expectedPosition,
        in ConsumerSelectionVersion selectionVersion)
    {
        ThrowIfDisposed();
        if (_consumerStateVersion != selectionVersion.ConsumerState ||
            selectionVersion.ConsumerGroupGeneration >= 0 &&
            _groupId is not null &&
            _cluster.GetConsumerGroupGeneration(_groupId) != selectionVersion.ConsumerGroupGeneration)
        {
            return false;
        }

        return _positions.TryGetValue(partition, out var currentPosition) &&
               currentPosition == expectedPosition;
    }

    private bool IsReservedRecordSelectionCurrentUnderLock(
        TopicPartition partition,
        long expectedPosition,
        bool resumePartition,
        in ConsumerSelectionVersion selectionVersion)
    {
        ThrowIfDisposed();
        if (!resumePartition ||
            !_assignment.Contains(partition) ||
            selectionVersion.ConsumerGroupGeneration >= 0 &&
            _groupId is not null &&
            _cluster.GetConsumerGroupGeneration(_groupId) != selectionVersion.ConsumerGroupGeneration)
        {
            return false;
        }

        return _positions.TryGetValue(partition, out var currentPosition) &&
               currentPosition == expectedPosition;
    }

    private void AdvancePositionUnderLock(
        TopicPartition partition,
        InMemoryRecord record,
        CapturedCommitOffsets? capturedCommit = null)
    {
        _positions[partition] = record.Offset + 1;
        if (_options.OffsetStoreTiming == OffsetStoreTiming.OnDelivery)
        {
            if (_options.EnableAutoOffsetStore)
                StoreOffsetUnderLock(partition, record.Offset + 1);
            if (_options.OffsetCommitMode == OffsetCommitMode.Auto)
            {
                if (capturedCommit is null)
                    CommitStoredOffsets();
                else
                {
                    var captured = capturedCommit.Value;
                    CommitCapturedOffsetsUnderLock(in captured);
                }
            }
        }
        else
        {
            SetInDoubtRecordUnderLock(partition, record.Offset + 1);
        }
    }

    private bool TryAdvanceSnapshotPosition(
        TopicPartition partition,
        InMemoryRecord record,
        long expectedPosition,
        int consumerStateVersion,
        int consumerGroupGeneration)
    {
        lock (_gate)
        {
            if (_pendingAutoCommitAdvancements is not null &&
                _pendingAutoCommitAdvancements.ContainsKey(partition))
            {
                return false;
            }

            return TryAdvanceSnapshotPositionUnderLock(
                partition,
                record,
                expectedPosition,
                consumerStateVersion,
                consumerGroupGeneration);
        }
    }

    private bool TryAdvanceReservedSnapshotPosition(
        TopicPartition partition,
        InMemoryRecord record,
        long expectedPosition,
        int consumerStateVersion,
        int consumerGroupGeneration)
    {
        lock (_gate)
        {
            if (_pendingAutoCommitAdvancements is null ||
                !_pendingAutoCommitAdvancements.TryGetValue(partition, out var pending) ||
                pending.Position != expectedPosition)
            {
                return false;
            }

            try
            {
                ThrowIfSnapshotStateChangedUnderLock(
                    consumerStateVersion,
                    consumerGroupGeneration);
                if (!_positions.TryGetValue(partition, out var currentPosition) ||
                    currentPosition != expectedPosition)
                {
                    return false;
                }

                AdvancePositionUnderLock(partition, record, pending.CapturedCommit);
                return true;
            }
            finally
            {
                ClearPendingAutoCommitAdvancementUnderLock(partition, expectedPosition);
            }
        }
    }

    private bool TryAdvanceSnapshotPositionUnderLock(
        TopicPartition partition,
        InMemoryRecord record,
        long expectedPosition,
        int consumerStateVersion,
        int consumerGroupGeneration)
    {
        ThrowIfSnapshotStateChangedUnderLock(
            consumerStateVersion,
            consumerGroupGeneration);
        if (!_positions.TryGetValue(partition, out var currentPosition) || currentPosition != expectedPosition)
            return false;

        _positions[partition] = record.Offset + 1;
        if (_options.OffsetStoreTiming == OffsetStoreTiming.OnDelivery)
        {
            if (_options.EnableAutoOffsetStore)
                StoreOffsetUnderLock(partition, record.Offset + 1);
            if (_options.OffsetCommitMode == OffsetCommitMode.Auto)
                CommitStoredOffsets();
        }
        else
        {
            SetInDoubtRecordUnderLock(partition, record.Offset + 1);
        }

        return true;
    }

    /// <summary>
    /// Stages (and under Auto mode commits) the in-doubt record's offset. Called when the
    /// application demonstrably moved past it: a subsequent consume call or explicit commit.
    /// </summary>
    private void ProveInDoubtRecordUnderLock(
        CapturedAutoCommitProof? capturedProof = null,
        bool commitAutomatically = true)
    {
        if (_inDoubtNextOffset < 0)
            return;

        if (capturedProof is { } proof &&
            (_inDoubtNextOffset != proof.Position || !_inDoubtPartition.Equals(proof.Partition)))
        {
            return;
        }

        if (capturedProof is { } captured)
        {
            var capturedCommit = captured.Commit;
            if (!IsCapturedCommitCurrentUnderLock(in capturedCommit))
                return;
        }

        if (IsInDoubtAutoCommitAdvancementPendingUnderLock())
            return;

        if (_options.EnableAutoOffsetStore)
            StoreOffsetUnderLock(_inDoubtPartition, _inDoubtNextOffset);

        ClearInDoubtRecordUnderLock();

        if (commitAutomatically && _options.OffsetCommitMode == OffsetCommitMode.Auto)
        {
            if (capturedProof is null)
                CommitStoredOffsets();
            else
            {
                var capturedCommit = capturedProof.Value.Commit;
                CommitCapturedOffsetsUnderLock(in capturedCommit);
            }
        }
    }

    private bool IsInDoubtAutoCommitAdvancementPendingUnderLock() =>
        _inDoubtNextOffset >= 0 &&
        _pendingAutoCommitAdvancements is not null &&
        _pendingAutoCommitAdvancements.TryGetValue(_inDoubtPartition, out var pending) &&
        pending.Position == _inDoubtNextOffset;

    private void DiscardInDoubtRecordUnderLock(TopicPartition partition)
    {
        if (_inDoubtNextOffset >= 0 && _inDoubtPartition.Equals(partition))
            ClearInDoubtRecordUnderLock();
    }

    private void SetInDoubtRecordUnderLock(TopicPartition partition, long nextOffset)
    {
        _inDoubtPartition = partition;
        _inDoubtNextOffset = nextOffset;
    }

    private void ClearInDoubtRecordUnderLock()
    {
        if (_inDoubtNextOffset < 0)
            return;

        _inDoubtNextOffset = -1;
        _cluster.SignalRecordsChanged();
    }

    private ConsumeResult<TKey, TValue> ToConsumeResult(TopicPartition topicPartition, InMemoryRecord record)
    {
        try
        {
            return ConsumeResult<TKey, TValue>.CreateWithCallerOwnedHeaders(
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
                valueDeserializer: _valueDeserializer,
                deferHeaderSnapshot: _borrowStoredRecords);
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
                Headers = _asyncKeyDeserializer is null
                          && _keyDeserializer is ICallerOwnedHeaderDeserializer<TKey>
                    ? ConsumeResult<TKey, TValue>.GetCallerOwnedSerializationHeaders(record.Headers)
                    : null,
                KeyData = ReadOnlyMemory<byte>.Empty,
                IsNull = false
            };

            try
            {
                key = _asyncKeyDeserializer is not null
                    ? await _asyncKeyDeserializer.DeserializeAsync(record.Key, keyContext, cancellationToken).ConfigureAwait(false)
                    : keyContext.Headers is not null
                        ? RecordHeaderDeserializer.DeserializeCallerOwned(_keyDeserializer, record.Key, keyContext)
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
            Headers = _asyncValueDeserializer is null
                      && _valueDeserializer is ICallerOwnedHeaderDeserializer<TValue>
                ? ConsumeResult<TKey, TValue>.GetCallerOwnedSerializationHeaders(record.Headers)
                : null,
            KeyData = SerializationContext.NormalizeKeyData(record.Key, record.IsKeyNull),
            IsNull = record.IsValueNull
        };
        var valueData = record.IsValueNull ? ReadOnlyMemory<byte>.Empty : record.Value;

        TValue value;
        try
        {
            value = _asyncValueDeserializer is not null
                ? await _asyncValueDeserializer.DeserializeAsync(valueData, valueContext, cancellationToken).ConfigureAwait(false)
                : valueContext.Headers is not null
                    ? RecordHeaderDeserializer.DeserializeCallerOwned(_valueDeserializer, valueData, valueContext)
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
            leaderEpoch: null,
            deferHeaderSnapshot: _borrowStoredRecords);
    }

    private RecordDeserializationException CreateDeserializationException(
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
            _borrowStoredRecords
                ? LazyConsumeHeaders.CreateSnapshot(record.Headers)
                : record.Headers,
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
        _storedOffsetsVersion++;
        ClearInDoubtRecordUnderLock();

        foreach (var (partition, position) in nextPositions)
        {
            _assignment.Add(partition);
            _positions[partition] = position;
        }

        EnsureCommitOffsetCapacity(_assignment.Count);
        _consumerStateVersion++;
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

    private CapturedCommitOffsets CaptureCommitOffsetsUnderLock(
        TopicPartitionOffset? inDoubtOffset)
    {
        var assignment = GetCurrentAssignmentUnderLock(out var consumerGroupGeneration);
        var inDoubtPartition = inDoubtOffset is { } pending
            ? new TopicPartition(pending.Topic, pending.Partition)
            : (TopicPartition?)null;
        var includeInDoubt = inDoubtPartition is { } pendingPartition &&
                             assignment.Contains(pendingPartition);
        var count = includeInDoubt ? 1 : 0;
        foreach (var partition in _storedOffsets.Keys)
        {
            if (partition != inDoubtPartition && assignment.Contains(partition))
                count++;
        }

        if (count == 0)
            return new CapturedCommitOffsets([], consumerGroupGeneration);

        var offsets = new TopicPartitionOffset[count];
        var index = 0;
        if (includeInDoubt && inDoubtOffset is { } capturedInDoubt)
            offsets[index++] = capturedInDoubt;
        foreach (var (partition, offset) in _storedOffsets)
        {
            if (partition != inDoubtPartition && assignment.Contains(partition))
                offsets[index++] = offset;
        }

        return new CapturedCommitOffsets(offsets, consumerGroupGeneration);
    }

    private bool TryApplyMatchingCapturedCommitFault(
        string groupId,
        IReadOnlyList<TopicPartitionOffset> offsets,
        CancellationToken cancellationToken,
        out ValueTask faultApplication)
    {
        faultApplication = ValueTask.CompletedTask;
        if (offsets.Count == 0)
            return false;

        var faultPlan = _cluster.FaultPlan;
        if (faultPlan is KafkaFaultPlan indexedPlan)
        {
            return indexedPlan.HasPotentialMatch(KafkaFaultOperation.Commit, groupId) &&
                   indexedPlan.TryApplyFirstMatchingCommitFault(
                       groupId,
                       offsets,
                       out faultApplication,
                       cancellationToken);
        }

        if (faultPlan.Count == 0)
            return false;

        var candidateScopes = new KafkaFaultScope[offsets.Count + 1];
        candidateScopes[0] = new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: groupId);
        for (var index = 0; index < offsets.Count; index++)
        {
            var offset = offsets[index];
            candidateScopes[index + 1] = new KafkaFaultScope(
                KafkaFaultOperation.Commit,
                offset.Topic,
                offset.Partition,
                groupId);
        }

        return faultPlan.TryApplyFirstMatchingFault(
            candidateScopes,
            out faultApplication,
            cancellationToken);
    }

    internal void CommitOffsetsFrom(Dictionary<TopicPartition, TopicPartitionOffset> positions)
    {
        if (_groupId is null || positions.Count == 0)
            return;

        var assignment = GetPotentialFaultAssignmentUnderLock(out _, out _);
        var count = 0;
        foreach (var (partition, offset) in positions)
        {
            if (!assignment.Contains(partition))
                continue;

            _commitOffsets[count++] = offset;
        }

        if (count == 0)
            return;

        _cluster.CommitOffsets(_groupId, _commitOffsets, count);
    }

    private void EnsureCommitOffsetCapacity(int count)
    {
        if (count <= _commitOffsets.Length)
            return;

        var capacity = _commitOffsets.Length;
        while (capacity < count)
            capacity = checked(capacity * 2);
        Array.Resize(ref _commitOffsets, capacity);
    }

    private static bool CanBorrowStoredInput<T>(
        IDeserializer<T> deserializer,
        IAsyncDeserializer<T>? asyncDeserializer) =>
        asyncDeserializer is null
            ? deserializer is IInputIsolatedDeserializer
              && deserializer is not ICallerOwnedHeaderDeserializer<T>
            : asyncDeserializer is IInputIsolatedDeserializer;

    private bool TryPrepareOnDeliveryAutoCommitFault(
        TopicPartition partition,
        long expectedPosition,
        long nextOffset,
        in ConsumerSelectionVersion selectionVersion,
        CancellationToken cancellationToken,
        out bool hasReservation,
        out ValueTask faultApplication)
    {
        lock (_gate)
        {
            if (!IsRecordSelectionCurrentUnderLock(
                    partition,
                    expectedPosition,
                    in selectionVersion))
            {
                hasReservation = false;
                faultApplication = ValueTask.CompletedTask;
                return false;
            }

            return TryPrepareOnDeliveryAutoCommitFaultUnderLock(
                partition,
                expectedPosition,
                nextOffset,
                in selectionVersion,
                cancellationToken,
                out hasReservation,
                out faultApplication);
        }
    }

    private bool TryPrepareSnapshotOnDeliveryAutoCommitFault(
        TopicPartition partition,
        long expectedPosition,
        long nextOffset,
        int consumerStateVersion,
        int consumerGroupGeneration,
        CancellationToken cancellationToken,
        out bool hasReservation,
        out ValueTask faultApplication)
    {
        lock (_gate)
        {
            ThrowIfSnapshotStateChangedUnderLock(
                consumerStateVersion,
                consumerGroupGeneration);
            var selectionVersion = new ConsumerSelectionVersion(
                consumerStateVersion,
                consumerGroupGeneration);
            return TryPrepareOnDeliveryAutoCommitFaultUnderLock(
                partition,
                expectedPosition,
                nextOffset,
                in selectionVersion,
                cancellationToken,
                out hasReservation,
                out faultApplication);
        }
    }

    private bool TryPrepareOnDeliveryAutoCommitFaultUnderLock(
        TopicPartition partition,
        long expectedPosition,
        long nextOffset,
        in ConsumerSelectionVersion selectionVersion,
        CancellationToken cancellationToken,
        out bool hasReservation,
        out ValueTask faultApplication)
    {
        ThrowIfDisposed();
        hasReservation = false;
        faultApplication = ValueTask.CompletedTask;
        if (!_positions.TryGetValue(partition, out var currentPosition) ||
            currentPosition != expectedPosition)
        {
            return false;
        }

        if (_options.OffsetCommitMode != OffsetCommitMode.Auto ||
            _options.OffsetStoreTiming != OffsetStoreTiming.OnDelivery)
        {
            return true;
        }

        if (_pendingAutoCommitAdvancements is not null &&
            _pendingAutoCommitAdvancements.ContainsKey(partition))
        {
            return false;
        }

        // The partition can be paused while an async deserializer runs. Do not consume
        // its commit fault until this delivery can reserve the partition.
        if (_paused.Contains(partition))
            return false;

        var pendingOffset = _options.EnableAutoOffsetStore
            ? new TopicPartitionOffset(partition.Topic, partition.Partition, nextOffset)
            : (TopicPartitionOffset?)null;
        if (TryCaptureAndApplyMatchingCommitFaultUnderLock(
                partition,
                expectedPosition,
                pendingOffset,
                in selectionVersion,
                cancellationToken,
                out _,
                out faultApplication))
        {
            hasReservation = true;
        }

        return true;
    }

    private bool TryCaptureAndApplyMatchingCommitFaultUnderLock(
        TopicPartition reservationPartition,
        long reservationPosition,
        TopicPartitionOffset? pendingOffset,
        in ConsumerSelectionVersion selectionVersion,
        CancellationToken cancellationToken,
        out CapturedCommitOffsets capturedCommit,
        out ValueTask faultApplication)
    {
        capturedCommit = default;
        faultApplication = ValueTask.CompletedTask;
        if (_groupId is null)
            return false;

        var faultPlan = _cluster.FaultPlan;
        bool hasMatchingCommitFault;
        long faultPlanVersion;
        do
        {
            faultPlanVersion = faultPlan.Version;
            hasMatchingCommitFault = HasMatchingCommitFaultUnderLock(faultPlan, pendingOffset);
        }
        while (faultPlanVersion != faultPlan.Version);
        if (!hasMatchingCommitFault ||
            !IsRecordSelectionCurrentUnderLock(
                reservationPartition,
                reservationPosition,
                in selectionVersion))
        {
            return false;
        }

        capturedCommit = CaptureCommitOffsetsUnderLock(pendingOffset);
        var resumePartition = _paused.Add(reservationPartition);
        if (resumePartition)
            InvalidatePotentialFaultActiveResourcesUnderLock();
        (_pendingAutoCommitAdvancements ??= [])[reservationPartition] = new PendingAutoCommitAdvancement(
            reservationPosition,
            resumePartition,
            new TaskCompletionSource<CapturedAutoCommitProof>(
                TaskCreationOptions.RunContinuationsAsynchronously),
            capturedCommit);

        try
        {
            if (TryApplyMatchingCapturedCommitFault(
                    _groupId,
                    capturedCommit.Offsets,
                    cancellationToken,
                    out faultApplication))
            {
                // Fault observers run synchronously and may re-enter this consumer.
                ThrowIfDisposed();
                return true;
            }
        }
        catch
        {
            ClearPendingAutoCommitAdvancementUnderLock(reservationPartition, reservationPosition);
            throw;
        }

        ClearPendingAutoCommitAdvancementUnderLock(reservationPartition, reservationPosition);
        capturedCommit = default;
        return false;
    }

    private bool HasMatchingCommitFaultUnderLock(
        IKafkaFaultPlan faultPlan,
        TopicPartitionOffset? pendingOffset)
    {
        var groupId = _groupId!;
        var assignment = GetPotentialFaultAssignmentUnderLock(out _, out _);
        if (!faultPlan.HasPotentialFault(
                KafkaFaultOperation.Commit,
                groupId,
                assignment))
        {
            return false;
        }

        var scopeCount = 0;
        AddCommitFaultScopeUnderLock(
            ref scopeCount,
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: groupId));
        TopicPartition? pendingPartition = null;
        if (pendingOffset is { } pending)
        {
            pendingPartition = new TopicPartition(pending.Topic, pending.Partition);
            if (assignment.Contains(pendingPartition.Value))
            {
                AddCommitFaultScopeUnderLock(
                    ref scopeCount,
                    new KafkaFaultScope(
                        KafkaFaultOperation.Commit,
                        pending.Topic,
                        pending.Partition,
                        groupId));
            }
        }

        foreach (var partition in _storedOffsets.Keys)
        {
            if (partition == pendingPartition || !assignment.Contains(partition))
                continue;

            AddCommitFaultScopeUnderLock(
                ref scopeCount,
                new KafkaFaultScope(
                    KafkaFaultOperation.Commit,
                    partition.Topic,
                    partition.Partition,
                    groupId));
        }

        return scopeCount > 1 && faultPlan.TryGetFirstMatchingFaultScope(
            _commitFaultScopes.AsSpan(0, scopeCount),
            out _);
    }

    private void AddCommitFaultScopeUnderLock(ref int scopeCount, KafkaFaultScope scope)
    {
        if (scopeCount == _commitFaultScopes.Length)
            Array.Resize(ref _commitFaultScopes, _commitFaultScopes.Length * 2);

        _commitFaultScopes[scopeCount++] = scope;
    }

    private async ValueTask ApplyOnDeliveryAutoCommitFaultAsync(
        ValueTask faultApplication,
        TopicPartition partition,
        long expectedPosition,
        CancellationToken cancellationToken)
    {
        try
        {
            await faultApplication.ConfigureAwait(false);
            ThrowIfDisposed();
        }
        catch
        {
            ClearPendingAutoCommitAdvancement(partition, expectedPosition);
            throw;
        }
    }

    private TaskCompletionSource<CapturedAutoCommitProof>? ClearPendingAutoCommitAdvancement(
        TopicPartition partition,
        long expectedPosition,
        bool completeWaiters = true)
    {
        lock (_gate)
            return ClearPendingAutoCommitAdvancementUnderLock(
                partition,
                expectedPosition,
                completeWaiters);
    }

    private TaskCompletionSource<CapturedAutoCommitProof>? ClearPendingAutoCommitAdvancementUnderLock(
        TopicPartition partition,
        long expectedPosition,
        bool completeWaiters = true)
    {
        if (_pendingAutoCommitAdvancements is null ||
            !_pendingAutoCommitAdvancements.TryGetValue(partition, out var pending) ||
            pending.Position != expectedPosition)
        {
            return null;
        }

        _pendingAutoCommitAdvancements.Remove(partition);
        if (pending.ResumePartition && _paused.Remove(partition))
            InvalidatePotentialFaultActiveResourcesUnderLock();
        if (_pendingAutoCommitAdvancements.Count == 0)
            _pendingAutoCommitAdvancements = null;

        if (completeWaiters)
        {
            pending.Completion.TrySetResult(new CapturedAutoCommitProof(
                partition,
                pending.Position,
                pending.CapturedCommit,
                SharedWaiter: true,
                SharedWaiterCompletion: null));
        }
        _cluster.SignalRecordsChanged();
        return pending.Completion;
    }

    private void ThrowIfAutoCommitAdvancementPendingUnderLock()
    {
        if (_pendingAutoCommitAdvancements is not null)
            ThrowAutoCommitAdvancementPending();
    }

    [DoesNotReturn]
    private static void ThrowAutoCommitAdvancementPending() =>
        throw new InvalidOperationException(
            "Cannot change consumer assignment or position while an automatic commit fault is pending.");

    private ValueTask<CapturedAutoCommitProof?> ApplyInDoubtAutoCommitFaultAsync(
        CancellationToken cancellationToken)
    {
        if (_options.OffsetCommitMode != OffsetCommitMode.Auto)
            return default;

        TopicPartition inDoubtPartition;
        long inDoubtNextOffset;
        CapturedCommitOffsets capturedCommit;
        ValueTask faultApplication;
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_inDoubtNextOffset < 0)
                return default;

            inDoubtPartition = _inDoubtPartition;
            inDoubtNextOffset = _inDoubtNextOffset;
            if (_retryInDoubtNextOffset == inDoubtNextOffset &&
                _retryInDoubtPartition.Equals(inDoubtPartition))
            {
                _retryInDoubtNextOffset = -1;
            }
            if (_pendingAutoCommitAdvancements is not null &&
                _pendingAutoCommitAdvancements.TryGetValue(inDoubtPartition, out var pending) &&
                pending.Position == inDoubtNextOffset)
            {
                return AwaitPendingAutoCommitProofAsync(
                    pending.Completion.Task,
                    cancellationToken);
            }
            if (_indexedFaultPlan is { Count: 0 })
                return default;

            var inDoubtOffset = _options.EnableAutoOffsetStore
                ? new TopicPartitionOffset(
                    inDoubtPartition.Topic,
                    inDoubtPartition.Partition,
                    inDoubtNextOffset)
                : (TopicPartitionOffset?)null;
            var assignment = GetCurrentAssignmentUnderLock(out var consumerGroupGeneration);
            if (!assignment.Contains(inDoubtPartition))
                return default;
            var selectionVersion = new ConsumerSelectionVersion(
                _consumerStateVersion,
                consumerGroupGeneration);
            if (!TryCaptureAndApplyMatchingCommitFaultUnderLock(
                    inDoubtPartition,
                    inDoubtNextOffset,
                    inDoubtOffset,
                    in selectionVersion,
                    cancellationToken,
                    out capturedCommit,
                    out faultApplication))
            {
                return default;
            }
        }

        return ApplyInDoubtAutoCommitFaultAndClearAsync(
            faultApplication,
            inDoubtPartition,
            inDoubtNextOffset,
            capturedCommit);
    }

    private static async ValueTask<CapturedAutoCommitProof?> AwaitPendingAutoCommitProofAsync(
        Task<CapturedAutoCommitProof> completion,
        CancellationToken cancellationToken) =>
        await completion.WaitAsync(cancellationToken).ConfigureAwait(false);

    private async ValueTask<CapturedAutoCommitProof?> ApplyInDoubtAutoCommitFaultAndClearAsync(
        ValueTask faultApplication,
        TopicPartition partition,
        long expectedPosition,
        CapturedCommitOffsets capturedCommit)
    {
        try
        {
            await faultApplication.ConfigureAwait(false);
        }
        catch
        {
            lock (_gate)
            {
                ClearPendingAutoCommitAdvancementUnderLock(partition, expectedPosition);
                if (_inDoubtNextOffset == expectedPosition &&
                    _inDoubtPartition.Equals(partition))
                {
                    _retryInDoubtPartition = partition;
                    _retryInDoubtNextOffset = expectedPosition;
                }
            }
            throw;
        }

        var completion = ClearPendingAutoCommitAdvancement(
            partition,
            expectedPosition,
            completeWaiters: false);
        return new CapturedAutoCommitProof(
            partition,
            expectedPosition,
            capturedCommit,
            SharedWaiter: false,
            completion);
    }

    private void CompleteSharedAutoCommitWaiters(CapturedAutoCommitProof? capturedProof)
    {
        if (capturedProof is not
            {
                SharedWaiter: false,
                SharedWaiterCompletion: { } completion
            } proof)
        {
            return;
        }

        completion.TrySetResult(new CapturedAutoCommitProof(
            proof.Partition,
            proof.Position,
            proof.Commit,
            SharedWaiter: true,
            SharedWaiterCompletion: null));
        _cluster.SignalRecordsChanged();
    }

    private async ValueTask WaitForSharedAutoCommitDeliveryAsync(
        CapturedAutoCommitProof sharedProof,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var recordsChanged = _cluster.ObserveRecordsChanged();
            lock (_gate)
            {
                ThrowIfDisposed();
                if (_inDoubtNextOffset < 0 ||
                    _inDoubtPartition.Equals(sharedProof.Partition) &&
                    _inDoubtNextOffset == sharedProof.Position)
                {
                    return;
                }
            }

            await recordsChanged.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask WaitForCurrentAutoCommitDeliveryAsync(
        CancellationToken cancellationToken)
    {
        TopicPartition partition;
        long position;
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_inDoubtNextOffset < 0)
                return;

            partition = _inDoubtPartition;
            position = _inDoubtNextOffset;
            if (_retryInDoubtNextOffset == position &&
                _retryInDoubtPartition.Equals(partition))
            {
                return;
            }
        }

        while (true)
        {
            var recordsChanged = _cluster.ObserveRecordsChanged();
            lock (_gate)
            {
                ThrowIfDisposed();
                if (_inDoubtNextOffset < 0 ||
                    !_inDoubtPartition.Equals(partition) ||
                    _inDoubtNextOffset != position ||
                    _retryInDoubtNextOffset == position &&
                    _retryInDoubtPartition.Equals(partition))
                {
                    return;
                }
            }

            await recordsChanged.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private CapturedCommitOffsets CaptureStoredAutoCommitOffsetsUnderLock(
        CancellationToken cancellationToken,
        out ValueTask faultApplication)
    {
        faultApplication = ValueTask.CompletedTask;
        if (_options.OffsetCommitMode != OffsetCommitMode.Auto || _groupId is null)
            return new CapturedCommitOffsets([], -1);

        var capturedCommit = CaptureCommitOffsetsUnderLock(inDoubtOffset: null);
        _ = TryApplyMatchingCapturedCommitFault(
            _groupId,
            capturedCommit.Offsets,
            cancellationToken,
            out faultApplication);
        return capturedCommit;
    }

    private void CommitCapturedOffsetsUnderLock(in CapturedCommitOffsets capturedCommit)
    {
        if (_groupId is null ||
            capturedCommit.Offsets.Length == 0 ||
            !IsCapturedCommitCurrentUnderLock(in capturedCommit))
        {
            return;
        }

        _cluster.CommitOffsets(_groupId, capturedCommit.Offsets);
    }

    private bool IsCapturedCommitCurrentUnderLock(in CapturedCommitOffsets capturedCommit) =>
        capturedCommit.ConsumerGroupGeneration < 0 ||
        _groupId is not null &&
        _cluster.GetConsumerGroupGeneration(_groupId) == capturedCommit.ConsumerGroupGeneration;

    private bool HasPotentialConsumerFault() =>
        HasPotentialConsumerFault(expectedConsumerStateVersion: -1, expectedConsumerGroupGeneration: -1);

    private bool HasPotentialConsumerFault(
        int expectedConsumerStateVersion,
        int expectedConsumerGroupGeneration)
    {
        var faultPlan = _cluster.FaultPlan;
        var indexedPlan = _indexedFaultPlan;
        var planVersion = indexedPlan is null ? faultPlan.Version : indexedPlan.Version;
        var consumerStateVersion = Volatile.Read(ref _consumerStateVersion);
        var autoCommitEnabled = _groupId is not null &&
                                _options.OffsetCommitMode == OffsetCommitMode.Auto;
        var commitFaultEligible = autoCommitEnabled &&
                                  (indexedPlan is null ||
                                   indexedPlan.HasPotentialMatch(
                                       KafkaFaultOperation.Commit,
                                       _groupId!));
        var requiresStoredOffset = commitFaultEligible &&
                                   !_options.EnableAutoOffsetStore;
        var storedOffsetsVersion = requiresStoredOffset
            ? Volatile.Read(ref _storedOffsetsVersion)
            : -1;
        var consumerGroupGeneration = _groupId is null
            ? -1
            : _cluster.GetConsumerGroupGeneration(_groupId);
        if (expectedConsumerStateVersion >= 0)
        {
            ThrowIfDisposed();
            if (consumerStateVersion != expectedConsumerStateVersion ||
                consumerGroupGeneration != expectedConsumerGroupGeneration)
            {
                ThrowSnapshotStateChanged();
            }
        }

        if (Volatile.Read(ref _potentialFaultPlanVersion) == planVersion &&
            Volatile.Read(ref _potentialFaultConsumerStateVersion) == consumerStateVersion &&
            Volatile.Read(ref _potentialFaultConsumerGroupGeneration) == consumerGroupGeneration &&
            Volatile.Read(ref _potentialFaultStoredOffsetsVersion) == storedOffsetsVersion)
        {
            return Volatile.Read(ref _hasPotentialConsumerFault);
        }

        lock (_gate)
        {
            consumerStateVersion = _consumerStateVersion;
            storedOffsetsVersion = requiresStoredOffset ? _storedOffsetsVersion : -1;
            var assignment = GetPotentialFaultAssignmentUnderLock(
                out var activeResources,
                out consumerGroupGeneration);

            var includeCommit = commitFaultEligible &&
                                (_options.EnableAutoOffsetStore ||
                                 HasCommittableStoredOffsetUnderLock(
                                     assignment,
                                     consumerGroupGeneration));
            bool hasPotentialFault;
            if (indexedPlan is not null)
            {
                hasPotentialFault = indexedPlan.HasPotentialConsumerMatch(
                    _groupId,
                    activeResources,
                    assignment,
                    includeCommit,
                    out planVersion);
            }
            else
            {
                do
                {
                    planVersion = faultPlan.Version;
                    hasPotentialFault =
                        faultPlan.HasPotentialFault(KafkaFaultOperation.Fetch, _groupId, activeResources) ||
                        faultPlan.HasPotentialFault(KafkaFaultOperation.Consume, _groupId, activeResources) ||
                        includeCommit &&
                        faultPlan.HasPotentialFault(KafkaFaultOperation.Commit, _groupId, assignment);
                }
                while (planVersion != faultPlan.Version);
            }

            Volatile.Write(ref _hasPotentialConsumerFault, hasPotentialFault);
            Volatile.Write(ref _potentialFaultConsumerStateVersion, consumerStateVersion);
            Volatile.Write(ref _potentialFaultConsumerGroupGeneration, consumerGroupGeneration);
            Volatile.Write(ref _potentialFaultStoredOffsetsVersion, storedOffsetsVersion);
            Volatile.Write(ref _potentialFaultPlanVersion, planVersion);
            return hasPotentialFault;
        }
    }

    private IReadOnlySet<TopicPartition> GetPotentialFaultAssignmentUnderLock(
        out IReadOnlySet<TopicPartition> activeResources,
        out int consumerGroupGeneration)
    {
        while (true)
        {
            consumerGroupGeneration = _groupId is null
                ? -1
                : _cluster.GetConsumerGroupGeneration(_groupId);
            if (_potentialFaultAssignment is not null &&
                _potentialFaultAssignmentConsumerStateVersion == _consumerStateVersion &&
                _potentialFaultAssignmentConsumerGroupGeneration == consumerGroupGeneration)
            {
                activeResources = _potentialFaultActiveResources!;
                return _potentialFaultAssignment;
            }

            var assignment = GetCurrentAssignmentUnderLock(out var assignmentGeneration);
            if (_groupId is not null &&
                assignmentGeneration >= 0 &&
                assignmentGeneration != _cluster.GetConsumerGroupGeneration(_groupId))
            {
                continue;
            }

            consumerGroupGeneration = assignmentGeneration;
            _potentialFaultAssignment = assignment;
            if (_paused.Count == 0)
            {
                activeResources = assignment;
            }
            else
            {
                var unpaused = new HashSet<TopicPartition>(assignment.Count);
                foreach (var partition in assignment)
                {
                    if (!_paused.Contains(partition))
                        unpaused.Add(partition);
                }

                activeResources = unpaused;
            }

            _potentialFaultActiveResources = activeResources;
            _potentialFaultAssignmentConsumerStateVersion = _consumerStateVersion;
            _potentialFaultAssignmentConsumerGroupGeneration = consumerGroupGeneration;
            return assignment;
        }
    }

    private void InvalidatePotentialFaultActiveResourcesUnderLock()
    {
        Volatile.Write(ref _potentialFaultConsumerStateVersion, -1);
        _potentialFaultAssignmentConsumerStateVersion = -1;
    }

    private bool HasCommittableStoredOffsetUnderLock(
        IReadOnlySet<TopicPartition> assignment,
        int consumerGroupGeneration)
    {
        if (_committableOffsetConsumerStateVersion == _consumerStateVersion &&
            _committableOffsetConsumerGroupGeneration == consumerGroupGeneration &&
            _committableOffsetStoredOffsetsVersion == _storedOffsetsVersion)
        {
            return _hasCommittableStoredOffset;
        }

        var hasCommittableStoredOffset = false;
        foreach (var partition in _storedOffsets.Keys)
        {
            if (!assignment.Contains(partition))
                continue;

            hasCommittableStoredOffset = true;
            break;
        }

        _hasCommittableStoredOffset = hasCommittableStoredOffset;
        _committableOffsetConsumerStateVersion = _consumerStateVersion;
        _committableOffsetConsumerGroupGeneration = consumerGroupGeneration;
        _committableOffsetStoredOffsetsVersion = _storedOffsetsVersion;
        return hasCommittableStoredOffset;
    }

    private bool HasMatchingFault(in KafkaFaultScope operationScope)
        => _cluster.FaultPlan.HasMatchingFault(operationScope);

    private void StoreOffsetUnderLock(TopicPartitionOffset offset)
    {
        var partition = new TopicPartition(offset.Topic, offset.Partition);
        _storedOffsets[partition] = offset;
        _storedOffsetsVersion++;
    }

    private void StoreOffsetUnderLock(TopicPartition partition, long offset)
    {
        _storedOffsets[partition] = new TopicPartitionOffset(
            partition.Topic,
            partition.Partition,
            offset);
        _storedOffsetsVersion++;
    }

    private IReadOnlySet<TopicPartition> GetCurrentAssignmentUnderLock() =>
        GetCurrentAssignmentUnderLock(out _);

    private TopicPartition[] GetOrderedCurrentAssignmentUnderLock(
        out int consumerGroupGeneration)
    {
        var assignment = GetPotentialFaultAssignmentUnderLock(
            out _,
            out consumerGroupGeneration);
        if (_orderedAssignmentConsumerStateVersion == _consumerStateVersion &&
            _orderedAssignmentConsumerGroupGeneration == consumerGroupGeneration)
        {
            return _orderedAssignment;
        }

        var orderedAssignment = new TopicPartition[assignment.Count];
        var partitionIndex = 0;
        foreach (var partition in assignment)
            orderedAssignment[partitionIndex++] = partition;

        Array.Sort(orderedAssignment, static (left, right) =>
        {
            var topicComparison = string.CompareOrdinal(left.Topic, right.Topic);
            return topicComparison != 0
                ? topicComparison
                : left.Partition.CompareTo(right.Partition);
        });

        _orderedAssignment = orderedAssignment;
        _orderedAssignmentConsumerStateVersion = _consumerStateVersion;
        _orderedAssignmentConsumerGroupGeneration = consumerGroupGeneration;
        return orderedAssignment;
    }

    private IReadOnlySet<TopicPartition> GetCurrentAssignmentUnderLock(out int consumerGroupGeneration)
    {
        if (_groupId is null || _memberId is null)
        {
            consumerGroupGeneration = -1;
            return _assignment;
        }

        if (_consumerGroupGeneration < 0 || _consumerGroupRegistrationId == 0)
        {
            consumerGroupGeneration = -1;
            return new HashSet<TopicPartition>();
        }

        var owned = _cluster.GetConsumerGroupAssignment(
            _groupId,
            _memberId,
            _consumerGroupRegistrationId,
            out consumerGroupGeneration);
        _consumerGroupGeneration = consumerGroupGeneration;
        return owned.Where(_assignment.Contains).ToHashSet();
    }

    private void RegisterConsumerGroupMemberUnderLock()
    {
        if (_groupId is null || _memberId is null)
            return;

        _consumerGroupGeneration = _cluster.RegisterConsumerGroupMember(
            _groupId,
            _memberId,
            _assignment,
            out _consumerGroupRegistrationId);
    }

    private void UnregisterConsumerGroupMemberUnderLock()
    {
        if (_groupId is null || _memberId is null)
            return;

        _cluster.UnregisterConsumerGroupMember(
            _groupId,
            _memberId,
            _consumerGroupRegistrationId);
        _consumerGroupGeneration = -1;
        _consumerGroupRegistrationId = 0;
    }

    private void ApplyGroupTransitionFaults()
    {
        if (_groupId is null)
            return;

        ApplyGroupTransitionFaultUnderLock(KafkaFaultOperation.JoinGroup);
        ApplyGroupTransitionFaultUnderLock(KafkaFaultOperation.SyncGroup);
        ApplyGroupTransitionFaultUnderLock(KafkaFaultOperation.Rebalance);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long GetFaultPlanVersion()
    {
        var indexedPlan = _indexedFaultPlan;
        return indexedPlan is null
            ? _cluster.FaultPlan.Version
            : indexedPlan.Version;
    }

    private void ApplyGroupTransitionFaultUnderLock(KafkaFaultOperation operation)
    {
        ApplySynchronousFault(new KafkaFaultScope(operation, groupId: _groupId));
        ThrowIfDisposed();
        ThrowIfAutoCommitAdvancementPendingUnderLock();
    }

    private void ApplySynchronousFault(KafkaFaultScope scope)
    {
        var operation = _cluster.FaultPlan.ApplyAsync(scope);
        if (!operation.IsCompleted)
        {
            throw new InvalidOperationException(
                "Pause barriers require an asynchronous in-memory consumer operation.");
        }

        operation.GetAwaiter().GetResult();
    }

    private void ApplySynchronousFaultIfMatching(KafkaFaultScope scope)
    {
        if (HasMatchingFault(scope))
            ApplySynchronousFault(scope);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
