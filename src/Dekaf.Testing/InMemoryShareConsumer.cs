using System.Runtime.CompilerServices;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Testing;

/// <summary>
/// In-memory <see cref="IKafkaShareConsumer{TKey,TValue}"/> backed by an <see cref="InMemoryKafkaCluster"/>.
/// </summary>
public sealed class InMemoryShareConsumer<TKey, TValue> : IKafkaShareConsumer<TKey, TValue>
{
    private readonly object _gate = new();
    private readonly InMemoryKafkaCluster _cluster;
    private readonly IDeserializer<TKey> _keyDeserializer;
    private readonly IDeserializer<TValue> _valueDeserializer;
    // Non-null when the caller configured an IAsyncDeserializer for that component. The matching
    // synchronous slot then holds a throwing placeholder so a missed asynchronous divert fails
    // loudly instead of silently decoding nothing.
    private readonly IAsyncDeserializer<TKey>? _asyncKeyDeserializer;
    private readonly IAsyncDeserializer<TValue>? _asyncValueDeserializer;
    private readonly bool _keyUsesRecordHeaders;
    private readonly bool _valueUsesRecordHeaders;
    private readonly bool _hasAsyncDeserializers;
    private readonly InMemoryShareConsumerOptions _options;
    private readonly HashSet<string> _subscription = new(StringComparer.Ordinal);
    private readonly HashSet<TopicPartition> _assignment = [];
    private readonly Dictionary<ShareConsumeResult<TKey, TValue>, PendingShareRecord> _pending = [];
    private readonly SemaphoreSlim _commitSemaphore = new(1, 1);
    private KeyValuePair<ShareConsumeResult<TKey, TValue>, PendingShareRecord>[] _commitSnapshot = [];
    private PendingShareRecord[] _commitRecords = [];
    private TopicPartitionOffset[] _completedRecords = [];
    private TopicPartitionOffset[] _commitOffsets = [];
    private readonly string _memberId;
    private ShareGroupMemberRegistration? _registration;
    private int _shareFaultIndexVersion = -1;
    private int _shareFaultOperationMask;
    private bool _disposed;

    public InMemoryShareConsumer(InMemoryKafkaCluster cluster)
        : this(
            cluster,
            InMemorySerdeResolver.Deserializer<TKey>(),
            InMemorySerdeResolver.Deserializer<TValue>(),
            new InMemoryShareConsumerOptions())
    {
    }

    public InMemoryShareConsumer(
        InMemoryKafkaCluster cluster,
        InMemoryShareConsumerOptions options)
        : this(
            cluster,
            InMemorySerdeResolver.Deserializer<TKey>(),
            InMemorySerdeResolver.Deserializer<TValue>(),
            options)
    {
    }

    public InMemoryShareConsumer(
        InMemoryKafkaCluster cluster,
        IDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer)
        : this(cluster, keyDeserializer, valueDeserializer, new InMemoryShareConsumerOptions())
    {
    }

    public InMemoryShareConsumer(
        InMemoryKafkaCluster cluster,
        IDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer,
        InMemoryShareConsumerOptions options)
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
    /// Creates a share consumer that awaits <see cref="IAsyncDeserializer{T}"/> for both components.
    /// </summary>
    public InMemoryShareConsumer(
        InMemoryKafkaCluster cluster,
        IAsyncDeserializer<TKey> keyDeserializer,
        IAsyncDeserializer<TValue> valueDeserializer)
        : this(cluster, keyDeserializer, valueDeserializer, new InMemoryShareConsumerOptions())
    {
    }

    /// <summary>
    /// Creates a share consumer that awaits <see cref="IAsyncDeserializer{T}"/> for both components.
    /// </summary>
    public InMemoryShareConsumer(
        InMemoryKafkaCluster cluster,
        IAsyncDeserializer<TKey> keyDeserializer,
        IAsyncDeserializer<TValue> valueDeserializer,
        InMemoryShareConsumerOptions options)
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
    /// Creates a share consumer with a synchronous key deserializer and an asynchronous value deserializer.
    /// </summary>
    public InMemoryShareConsumer(
        InMemoryKafkaCluster cluster,
        IDeserializer<TKey> keyDeserializer,
        IAsyncDeserializer<TValue> valueDeserializer)
        : this(cluster, keyDeserializer, valueDeserializer, new InMemoryShareConsumerOptions())
    {
    }

    /// <summary>
    /// Creates a share consumer with a synchronous key deserializer and an asynchronous value deserializer.
    /// </summary>
    public InMemoryShareConsumer(
        InMemoryKafkaCluster cluster,
        IDeserializer<TKey> keyDeserializer,
        IAsyncDeserializer<TValue> valueDeserializer,
        InMemoryShareConsumerOptions options)
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
    /// Creates a share consumer with an asynchronous key deserializer and a synchronous value deserializer.
    /// </summary>
    public InMemoryShareConsumer(
        InMemoryKafkaCluster cluster,
        IAsyncDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer)
        : this(cluster, keyDeserializer, valueDeserializer, new InMemoryShareConsumerOptions())
    {
    }

    /// <summary>
    /// Creates a share consumer with an asynchronous key deserializer and a synchronous value deserializer.
    /// </summary>
    public InMemoryShareConsumer(
        InMemoryKafkaCluster cluster,
        IAsyncDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer,
        InMemoryShareConsumerOptions options)
        : this(
            cluster,
            keyDeserializer: null,
            InMemorySerdeResolver.Required(valueDeserializer, nameof(valueDeserializer)),
            InMemorySerdeResolver.Required(keyDeserializer, nameof(keyDeserializer)),
            asyncValueDeserializer: null,
            options)
    {
    }

    private InMemoryShareConsumer(
        InMemoryKafkaCluster cluster,
        IDeserializer<TKey>? keyDeserializer,
        IDeserializer<TValue>? valueDeserializer,
        IAsyncDeserializer<TKey>? asyncKeyDeserializer,
        IAsyncDeserializer<TValue>? asyncValueDeserializer,
        InMemoryShareConsumerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.GroupId);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxPollRecords, 1);
        _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
        _asyncKeyDeserializer = asyncKeyDeserializer;
        _asyncValueDeserializer = asyncValueDeserializer;
        _keyDeserializer = asyncKeyDeserializer is null
            ? keyDeserializer!
            : AsyncOnlyDeserializerPlaceholder<TKey>.Instance;
        _valueDeserializer = asyncValueDeserializer is null
            ? valueDeserializer!
            : AsyncOnlyDeserializerPlaceholder<TValue>.Instance;
        _keyUsesRecordHeaders = asyncKeyDeserializer is not null
            ? asyncKeyDeserializer is IRecordHeaderDeserializer { ConsumesRecordHeaders: true }
            : RecordHeaderDeserializer.UsesCallerOwnedHeaders(_keyDeserializer);
        _valueUsesRecordHeaders = asyncValueDeserializer is not null
            ? asyncValueDeserializer is IRecordHeaderDeserializer { ConsumesRecordHeaders: true }
            : RecordHeaderDeserializer.UsesCallerOwnedHeaders(_valueDeserializer);
        _hasAsyncDeserializers = asyncKeyDeserializer is not null || asyncValueDeserializer is not null;
        _options = options;
        _memberId = _options.MemberId ?? Guid.NewGuid().ToString("N");
    }

    public IReadOnlySet<string> Subscription
    {
        get
        {
            lock (_gate)
                return _subscription.ToHashSet(StringComparer.Ordinal);
        }
    }

    public IReadOnlySet<TopicPartition> Assignment
    {
        get
        {
            lock (_gate)
                return _assignment.ToHashSet();
        }
    }

    public string? MemberId => _memberId;
    public int? AcquisitionLockTimeoutMs => null;

#if !NET10_0_OR_GREATER
    IReadOnlyCollection<string> IKafkaShareConsumer<TKey, TValue>.Subscription => Subscription;

    IReadOnlyCollection<TopicPartition> IKafkaShareConsumer<TKey, TValue>.Assignment => Assignment;
#endif

    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        return ValueTask.CompletedTask;
    }

    public IKafkaShareConsumer<TKey, TValue> Subscribe(params string[] topics)
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
            ThrowIfDisposed();
            ReleasePendingUnderLock();
            UnregisterShareGroupMemberUnderLock();
            _subscription.Clear();
            foreach (var topic in topics.Where(topic => !string.IsNullOrWhiteSpace(topic)).Distinct(StringComparer.Ordinal))
                _subscription.Add(topic);

            _assignment.Clear();
            foreach (var topicPartition in topicPartitions)
                _assignment.Add(topicPartition);

            if (_subscription.Count != 0)
                _registration = _cluster.RegisterShareGroupMember(_options.GroupId, _memberId);

            Volatile.Write(ref _shareFaultIndexVersion, -1);
        }

        return this;
    }

    public IKafkaShareConsumer<TKey, TValue> Unsubscribe()
    {
        ThrowIfDisposed();

        lock (_gate)
        {
            ReleasePendingUnderLock();
            _subscription.Clear();
            _assignment.Clear();
            UnregisterShareGroupMemberUnderLock();
            Volatile.Write(ref _shareFaultIndexVersion, -1);
        }

        return this;
    }

    public async IAsyncEnumerable<ShareConsumeResult<TKey, TValue>> PollAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await CommitAsync(cancellationToken).ConfigureAwait(false);

        for (var i = 0; i < _options.MaxPollRecords; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = HasPotentialFault(KafkaFaultOperation.ShareConsume)
                ? await TryTakeAvailableRecordWithFaultAsync(cancellationToken).ConfigureAwait(false)
                : _hasAsyncDeserializers
                ? await TryTakeAvailableRecordAsync(cancellationToken).ConfigureAwait(false)
                : TryTakeAvailableRecord();
            if (record is null)
                yield break;

            yield return record;
        }
    }

    public void Acknowledge(
        ShareConsumeResult<TKey, TValue> record,
        AcknowledgeType type = AcknowledgeType.Accept)
    {
        ArgumentNullException.ThrowIfNull(record);
        ThrowIfDisposed();

        lock (_gate)
        {
            if (!_pending.TryGetValue(record, out var pending))
                throw new InvalidOperationException("Record was not returned by the current poll.");

            pending.AcknowledgeType = type;
        }
    }

    public ValueTask CommitAsync(CancellationToken cancellationToken = default)
        => CommitAsync(allowDisposed: false, cancellationToken);

    private ValueTask CommitAsync(bool allowDisposed, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_commitSemaphore.Wait(0, cancellationToken))
            return CommitAfterWaitAsync(allowDisposed, cancellationToken);

        return AwaitCommitSemaphoreAsync(
            _commitSemaphore.WaitAsync(cancellationToken),
            allowDisposed,
            cancellationToken);
    }

    private async ValueTask AwaitCommitSemaphoreAsync(
        Task wait,
        bool allowDisposed,
        CancellationToken cancellationToken)
    {
        await wait.ConfigureAwait(false);
        await CommitAfterWaitAsync(allowDisposed, cancellationToken).ConfigureAwait(false);
    }

    private ValueTask CommitAfterWaitAsync(bool allowDisposed, CancellationToken cancellationToken)
    {
        if (HasPotentialFault(KafkaFaultOperation.ShareAcknowledge))
            return CommitWithFaultAsync(allowDisposed, cancellationToken);

        try
        {
            lock (_gate)
            {
                if (allowDisposed && _disposed)
                    return ValueTask.CompletedTask;

                ThrowIfDisposed();
                if (_pending.Count == 0)
                    return ValueTask.CompletedTask;

                EnsureCommitRecordCapacity(_pending.Count);
                var recordCount = 0;
                foreach (var record in _pending.Values)
                    _commitRecords[recordCount++] = record;

                try
                {
                    CompleteRecords(recordCount);
                    _pending.Clear();
                }
                finally
                {
                    Array.Clear(_commitRecords, 0, recordCount);
                }
            }

            return ValueTask.CompletedTask;
        }
        finally
        {
            _commitSemaphore.Release();
        }
    }

    private ValueTask CommitWithFaultAsync(bool allowDisposed, CancellationToken cancellationToken)
    {
        var snapshotCount = 0;
        try
        {
            lock (_gate)
            {
                if (allowDisposed && _disposed)
                {
                    _commitSemaphore.Release();
                    return ValueTask.CompletedTask;
                }

                ThrowIfDisposed();
                if (_pending.Count == 0)
                {
                    _commitSemaphore.Release();
                    return ValueTask.CompletedTask;
                }

                EnsureSnapshotCapacity(_pending.Count);
                foreach (var pair in _pending)
                    _commitSnapshot[snapshotCount++] = pair;
            }

            for (var index = 0; index < snapshotCount; index++)
            {
                var record = _commitSnapshot[index].Value;
                if (!HasPotentialFault(KafkaFaultOperation.ShareAcknowledge, record.TopicPartition))
                    continue;

                var apply = _cluster.FaultPlan.ApplyAsync(
                    new KafkaFaultScope(
                        KafkaFaultOperation.ShareAcknowledge,
                        record.TopicPartition.Topic,
                        record.TopicPartition.Partition,
                        _options.GroupId),
                    cancellationToken);
                if (!apply.IsCompletedSuccessfully)
                    return AwaitCommitFaultAsync(apply, index + 1, snapshotCount, cancellationToken);

                apply.GetAwaiter().GetResult();
            }

            CompleteFaultedRecords(snapshotCount);
            Array.Clear(_commitSnapshot, 0, snapshotCount);
            _commitSemaphore.Release();
            return ValueTask.CompletedTask;
        }
        catch
        {
            Array.Clear(_commitSnapshot, 0, snapshotCount);
            _commitSemaphore.Release();
            throw;
        }
    }

    private async ValueTask AwaitCommitFaultAsync(
        ValueTask apply,
        int nextIndex,
        int snapshotCount,
        CancellationToken cancellationToken)
    {
        try
        {
            await apply.ConfigureAwait(false);
            for (var index = nextIndex; index < snapshotCount; index++)
            {
                var record = _commitSnapshot[index].Value;
                if (!HasPotentialFault(KafkaFaultOperation.ShareAcknowledge, record.TopicPartition))
                    continue;

                await _cluster.FaultPlan.ApplyAsync(
                    new KafkaFaultScope(
                        KafkaFaultOperation.ShareAcknowledge,
                        record.TopicPartition.Topic,
                        record.TopicPartition.Partition,
                        _options.GroupId),
                    cancellationToken).ConfigureAwait(false);
            }

            CompleteFaultedRecords(snapshotCount);
        }
        finally
        {
            Array.Clear(_commitSnapshot, 0, snapshotCount);
            _commitSemaphore.Release();
        }
    }

    private void CompleteFaultedRecords(int snapshotCount)
    {
        lock (_gate)
        {
            EnsureCommitRecordCapacity(snapshotCount);
            var recordCount = 0;
            for (var index = 0; index < snapshotCount; index++)
            {
                var pair = _commitSnapshot[index];
                if (_pending.TryGetValue(pair.Key, out var record) && ReferenceEquals(record, pair.Value))
                    _commitRecords[recordCount++] = record;
            }

            if (recordCount == 0)
                return;

            try
            {
                CompleteRecords(recordCount);
                for (var index = 0; index < snapshotCount; index++)
                {
                    var pair = _commitSnapshot[index];
                    if (_pending.TryGetValue(pair.Key, out var record) && ReferenceEquals(record, pair.Value))
                        _pending.Remove(pair.Key);
                }
            }
            finally
            {
                Array.Clear(_commitRecords, 0, recordCount);
            }
        }
    }

    public async ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return;

        await CommitAsync(allowDisposed: true, cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            if (_disposed)
                return;

            cancellationToken.ThrowIfCancellationRequested();
            UnregisterShareGroupMemberUnderLock();
            _disposed = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        await CloseAsync().ConfigureAwait(false);
    }

    private ShareConsumeResult<TKey, TValue>? TryTakeAvailableRecord()
    {
        var assignment = OrderedAssignment(out var registration);
        foreach (var partition in assignment)
        {
            if (!TryAcquireRecord(
                    partition,
                    registration,
                    out var record,
                    out var deliveryCount,
                    out var acquiredRegistration))
                continue;

            ShareConsumeResult<TKey, TValue> result;
            try
            {
                result = ToShareResult(record, deliveryCount);
            }
            catch
            {
                ReleaseAcquiredRecord(partition, record, acquiredRegistration);
                throw;
            }

            return RegisterPending(partition, record, result, acquiredRegistration);
        }

        return null;
    }

    /// <summary>
    /// Poll path used when at least one component has an <see cref="IAsyncDeserializer{T}"/>.
    /// Deliberate mirror of <see cref="TryTakeAvailableRecord"/>: both drive the same acquisition
    /// and pending-record bookkeeping helpers.
    /// </summary>
    private async ValueTask<ShareConsumeResult<TKey, TValue>?> TryTakeAvailableRecordAsync(
        CancellationToken cancellationToken)
    {
        var assignment = OrderedAssignment(out var registration);
        foreach (var partition in assignment)
        {
            if (!TryAcquireRecord(
                    partition,
                    registration,
                    out var record,
                    out var deliveryCount,
                    out var acquiredRegistration))
                continue;

            ShareConsumeResult<TKey, TValue> result;
            try
            {
                result = await ToShareResultAsync(record, deliveryCount, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                ReleaseAcquiredRecord(partition, record, acquiredRegistration);
                throw;
            }

            return RegisterPending(partition, record, result, acquiredRegistration);
        }

        return null;
    }

    private async ValueTask<ShareConsumeResult<TKey, TValue>?> TryTakeAvailableRecordWithFaultAsync(
        CancellationToken cancellationToken)
    {
        var assignment = OrderedAssignment(out var assignmentRegistration);
        foreach (var partition in assignment)
        {
            var hasPotentialFault = HasPotentialFault(KafkaFaultOperation.ShareConsume, partition);
            InMemoryRecord record;
            int deliveryCount;
            ShareGroupMemberRegistration registration;
            if (hasPotentialFault)
            {
                if (!TryAcquireRecordForFault(
                        partition,
                        assignmentRegistration,
                        out record,
                        out registration))
                    continue;

                try
                {
                    await _cluster.FaultPlan.ApplyAsync(
                        new KafkaFaultScope(
                            KafkaFaultOperation.ShareConsume,
                            partition.Topic,
                            partition.Partition,
                            _options.GroupId),
                        cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    _cluster.RollbackShareRecordAcquisition(
                        _options.GroupId,
                        _memberId,
                        registration,
                        partition,
                        record.Offset);
                    throw;
                }

                if (!_cluster.TryCompleteShareRecordAcquisition(
                        _options.GroupId,
                        _memberId,
                        registration,
                        partition,
                        record.Offset,
                        out deliveryCount))
                {
                    _cluster.RollbackShareRecordAcquisition(
                        _options.GroupId,
                        _memberId,
                        registration,
                        partition,
                        record.Offset);
                    continue;
                }
            }
            else if (!TryAcquireRecord(
                         partition,
                         assignmentRegistration,
                         out record,
                         out deliveryCount,
                         out registration))
            {
                continue;
            }

            ShareConsumeResult<TKey, TValue> result;
            try
            {
                result = _hasAsyncDeserializers
                    ? await ToShareResultAsync(record, deliveryCount, cancellationToken).ConfigureAwait(false)
                    : ToShareResult(record, deliveryCount);
            }
            catch
            {
                ReleaseAcquiredRecord(partition, record, registration);
                throw;
            }

            return RegisterPending(partition, record, result, registration);
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HasPotentialFault(KafkaFaultOperation operation)
    {
        if (_cluster.FaultPlan is not KafkaFaultPlan indexedPlan)
            return true;

        var version = indexedPlan.ShareFaultIndexVersion;
        if (Volatile.Read(ref _shareFaultIndexVersion) != version)
            return RefreshShareFaultIndex(indexedPlan, operation);

        return (Volatile.Read(ref _shareFaultOperationMask) & (1 << (int)operation)) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HasPotentialFault(KafkaFaultOperation operation, TopicPartition partition) =>
        _cluster.FaultPlan is not KafkaFaultPlan indexedPlan ||
        indexedPlan.HasPotentialShareMatch(
            operation,
            partition.Topic,
            partition.Partition,
            _options.GroupId);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool RefreshShareFaultIndex(KafkaFaultPlan faultPlan, KafkaFaultOperation operation)
    {
        lock (_gate)
        {
            int version;
            int operationMask;
            do
            {
                version = faultPlan.ShareFaultIndexVersion;
                operationMask = 0;
                if (faultPlan.HasPotentialShareMatch(
                        KafkaFaultOperation.ShareConsume,
                        _options.GroupId,
                        _assignment))
                {
                    operationMask |= 1 << (int)KafkaFaultOperation.ShareConsume;
                }

                if (faultPlan.HasPotentialShareMatch(
                        KafkaFaultOperation.ShareAcknowledge,
                        _options.GroupId,
                        _assignment))
                {
                    operationMask |= 1 << (int)KafkaFaultOperation.ShareAcknowledge;
                }
            }
            while (version != faultPlan.ShareFaultIndexVersion);

            Volatile.Write(ref _shareFaultOperationMask, operationMask);
            Volatile.Write(ref _shareFaultIndexVersion, version);
            return (operationMask & (1 << (int)operation)) != 0;
        }
    }

    private TopicPartition[] OrderedAssignment(out ShareGroupMemberRegistration? registration)
    {
        lock (_gate)
        {
            registration = _registration;
            return _assignment
                .OrderBy(item => item.Topic, StringComparer.Ordinal)
                .ThenBy(item => item.Partition)
                .ToArray();
        }
    }

    private bool TryAcquireRecord(
        TopicPartition partition,
        ShareGroupMemberRegistration? registration,
        out InMemoryRecord record,
        out int deliveryCount,
        out ShareGroupMemberRegistration acquiredRegistration)
    {
        long offset;
        lock (_gate)
            offset = GetNextOffsetUnderLock(partition);

        if (registration is null)
        {
            record = null!;
            deliveryCount = 0;
            acquiredRegistration = null!;
            return false;
        }

        acquiredRegistration = registration;
        return _cluster.TryAcquireShareRecord(
            _options.GroupId,
            _memberId,
            registration,
            partition,
            offset,
            out record,
            out deliveryCount);
    }

    private bool TryAcquireRecordForFault(
        TopicPartition partition,
        ShareGroupMemberRegistration? registration,
        out InMemoryRecord record,
        out ShareGroupMemberRegistration acquiredRegistration)
    {
        long offset;
        lock (_gate)
            offset = GetNextOffsetUnderLock(partition);

        if (registration is null)
        {
            record = null!;
            acquiredRegistration = null!;
            return false;
        }

        acquiredRegistration = registration;
        return _cluster.TryAcquireShareRecordForFault(
            _options.GroupId,
            _memberId,
            registration,
            partition,
            offset,
            out record);
    }

    /// <summary>
    /// Releases a record acquired for delivery that never became a pending result — a deserializer
    /// threw or was cancelled. Without this the lease has no owner in <c>_pending</c>, so no
    /// acknowledge, commit, unsubscribe or close path can ever release it and the record stays
    /// unavailable to the group's other members.
    /// </summary>
    private void ReleaseAcquiredRecord(
        TopicPartition partition,
        InMemoryRecord record,
        ShareGroupMemberRegistration registration) =>
        _cluster.ReleaseShareRecords(
            _options.GroupId,
            _memberId,
            registration,
            [new TopicPartitionOffset(partition.Topic, partition.Partition, record.Offset)]);

    private ShareConsumeResult<TKey, TValue>? RegisterPending(
        TopicPartition partition,
        InMemoryRecord record,
        ShareConsumeResult<TKey, TValue> result,
        ShareGroupMemberRegistration registration)
    {
        var pending = new PendingShareRecord(partition, record.Offset, record.Offset + 1);
        bool disposed;

        lock (_gate)
        {
            disposed = _disposed;
            if (!disposed && ReferenceEquals(_registration, registration))
            {
                _pending[result] = pending;
                return result;
            }
        }

        ReleaseAcquiredRecord(partition, record, registration);
        if (disposed)
            throw new ObjectDisposedException(GetType().FullName);

        return null;
    }

    private async ValueTask<ShareConsumeResult<TKey, TValue>> ToShareResultAsync(
        InMemoryRecord record,
        int deliveryCount,
        CancellationToken cancellationToken)
    {
        var serializationHeaders = _keyUsesRecordHeaders || _valueUsesRecordHeaders
            ? new Headers(record.Headers)
            : null;
        var key = record.IsKeyNull
            ? default
            : await DeserializeAsync(
                _asyncKeyDeserializer,
                _keyDeserializer,
                record.Key,
                Context(
                    record.Topic,
                    SerializationComponent.Key,
                    _keyUsesRecordHeaders ? serializationHeaders : null,
                    isNull: false),
                cancellationToken).ConfigureAwait(false);

        var value = await DeserializeAsync(
            _asyncValueDeserializer,
            _valueDeserializer,
            record.IsValueNull ? ReadOnlyMemory<byte>.Empty : record.Value,
            Context(
                record.Topic,
                SerializationComponent.Value,
                _valueUsesRecordHeaders ? serializationHeaders : null,
                isNull: record.IsValueNull,
                keyData: record.Key,
                isKeyNull: record.IsKeyNull),
            cancellationToken).ConfigureAwait(false);

        return new ShareConsumeResult<TKey, TValue>
        {
            Topic = record.Topic,
            Partition = record.Partition,
            Offset = record.Offset,
            Key = key,
            Value = value,
            Headers = record.Headers,
            TimestampMs = record.TimestampMs,
            DeliveryCount = deliveryCount
        };
    }

    private static ValueTask<T> DeserializeAsync<T>(
        IAsyncDeserializer<T>? asyncDeserializer,
        IDeserializer<T> deserializer,
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        CancellationToken cancellationToken) =>
        asyncDeserializer is not null
            ? asyncDeserializer.DeserializeAsync(data, context, cancellationToken)
            : ValueTask.FromResult(deserializer.Deserialize(data, context));

    private ShareConsumeResult<TKey, TValue> ToShareResult(InMemoryRecord record, int deliveryCount)
    {
        var serializationHeaders = _keyUsesRecordHeaders || _valueUsesRecordHeaders
            ? new Headers(record.Headers)
            : null;
        var key = record.IsKeyNull
            ? default
            : _keyDeserializer.Deserialize(
                record.Key,
                Context(
                    record.Topic,
                    SerializationComponent.Key,
                    _keyUsesRecordHeaders ? serializationHeaders : null,
                    isNull: false));

        var value = record.IsValueNull
            ? _valueDeserializer.Deserialize(
                ReadOnlyMemory<byte>.Empty,
                Context(
                    record.Topic,
                    SerializationComponent.Value,
                    _valueUsesRecordHeaders ? serializationHeaders : null,
                    isNull: true,
                    keyData: record.Key,
                    isKeyNull: record.IsKeyNull))
            : _valueDeserializer.Deserialize(
                record.Value,
                Context(
                    record.Topic,
                    SerializationComponent.Value,
                    _valueUsesRecordHeaders ? serializationHeaders : null,
                    isNull: false,
                    keyData: record.Key,
                    isKeyNull: record.IsKeyNull));

        return new ShareConsumeResult<TKey, TValue>
        {
            Topic = record.Topic,
            Partition = record.Partition,
            Offset = record.Offset,
            Key = key,
            Value = value,
            Headers = record.Headers,
            TimestampMs = record.TimestampMs,
            DeliveryCount = deliveryCount
        };
    }

    private long GetNextOffsetUnderLock(TopicPartition partition)
    {
        var offset = _cluster.GetCommittedShareOffset(_options.GroupId, partition) ??
                     _cluster.GetWatermarks(partition).Low;

        foreach (var pending in _pending.Values)
        {
            if (pending.TopicPartition == partition && pending.NextOffset > offset)
                offset = pending.NextOffset;
        }

        return offset;
    }

    private void ReleasePendingUnderLock()
    {
        if (_pending.Count == 0)
            return;

        _cluster.ReleaseShareRecords(
            _options.GroupId,
            _memberId,
            _registration!,
            BuildCompletedRecords(_pending.Values));
        _pending.Clear();
    }

    private void UnregisterShareGroupMemberUnderLock()
    {
        if (_registration is not { } registration)
            return;

        _cluster.UnregisterShareGroupMember(_options.GroupId, _memberId, registration);
        _registration = null;
    }

    private void EnsureSnapshotCapacity(int count)
    {
        if (_commitSnapshot.Length < count)
            Array.Resize(ref _commitSnapshot, count);
    }

    private void EnsureCommitRecordCapacity(int count)
    {
        if (_commitRecords.Length < count)
            Array.Resize(ref _commitRecords, count);
        if (_completedRecords.Length < count)
            Array.Resize(ref _completedRecords, count);
        if (_commitOffsets.Length < count)
            Array.Resize(ref _commitOffsets, count);
    }

    private void CompleteRecords(int recordCount)
    {
        for (var index = 0; index < recordCount; index++)
        {
            var record = _commitRecords[index];
            _completedRecords[index] = new TopicPartitionOffset(
                record.TopicPartition.Topic,
                record.TopicPartition.Partition,
                record.Offset);
        }

        if (recordCount > 1)
            Array.Sort(_commitRecords, 0, recordCount, PendingShareRecordComparer.Instance);
        var commitCount = BuildCommitOffsets(recordCount);
        _cluster.CompleteShareRecords(
            _options.GroupId,
            _memberId,
            _registration!,
            _completedRecords,
            recordCount,
            _commitOffsets,
            commitCount);
    }

    private int BuildCommitOffsets(int recordCount)
    {
        var commitCount = 0;
        var groupStart = 0;
        while (groupStart < recordCount)
        {
            var first = _commitRecords[groupStart];
            var groupEnd = groupStart + 1;
            while (groupEnd < recordCount &&
                   _commitRecords[groupEnd].TopicPartition == first.TopicPartition)
            {
                groupEnd++;
            }

            var commitOffset = first.NextOffset - 1;
            for (var index = groupStart; index < groupEnd; index++)
            {
                var record = _commitRecords[index];
                if (record.NextOffset - 1 != commitOffset ||
                    record.AcknowledgeType is not (AcknowledgeType.Accept or AcknowledgeType.Reject))
                {
                    break;
                }

                commitOffset = record.NextOffset;
            }

            if (commitOffset > first.NextOffset - 1)
                _commitOffsets[commitCount++] = new TopicPartitionOffset(
                    first.TopicPartition.Topic,
                    first.TopicPartition.Partition,
                    commitOffset);

            groupStart = groupEnd;
        }

        return commitCount;
    }

    private static TopicPartitionOffset[] BuildCompletedRecords(IEnumerable<PendingShareRecord> records)
    {
        return records
            .Select(record => new TopicPartitionOffset(
                record.TopicPartition.Topic,
                record.TopicPartition.Partition,
                record.Offset))
            .ToArray();
    }

    private static SerializationContext Context(
        string topic,
        SerializationComponent component,
        Headers? headers,
        bool isNull,
        ReadOnlyMemory<byte> keyData = default,
        bool isKeyNull = false) =>
        new()
        {
            Topic = topic,
            Component = component,
            Headers = headers,
            KeyData = SerializationContext.NormalizeKeyData(keyData, isKeyNull),
            IsNull = isNull
        };

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed class PendingShareRecord
    {
        public PendingShareRecord(TopicPartition topicPartition, long offset, long nextOffset)
        {
            TopicPartition = topicPartition;
            Offset = offset;
            NextOffset = nextOffset;
        }

        public TopicPartition TopicPartition { get; }
        public long Offset { get; }
        public long NextOffset { get; }
        public AcknowledgeType AcknowledgeType { get; set; } = AcknowledgeType.Accept;
    }

    private sealed class PendingShareRecordComparer : IComparer<PendingShareRecord>
    {
        public static PendingShareRecordComparer Instance { get; } = new();

        public int Compare(PendingShareRecord? left, PendingShareRecord? right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left is null)
                return -1;
            if (right is null)
                return 1;

            var topic = StringComparer.Ordinal.Compare(
                left.TopicPartition.Topic,
                right.TopicPartition.Topic);
            if (topic != 0)
                return topic;

            var partition = left.TopicPartition.Partition.CompareTo(right.TopicPartition.Partition);
            return partition != 0 ? partition : left.NextOffset.CompareTo(right.NextOffset);
        }
    }
}
