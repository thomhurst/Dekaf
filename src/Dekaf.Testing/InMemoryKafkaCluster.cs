using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Producer;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Testing;

internal readonly record struct InMemoryShareGroupListing(
    string GroupId,
    bool HasActiveMembers);

/// <summary>
/// Shared in-memory topic, partition, offset, and group-offset store.
/// </summary>
public sealed partial class InMemoryKafkaCluster
{
    private readonly object _gate = new();
    private readonly Dictionary<string, TopicState> _topics = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<TopicPartition, TopicPartitionOffset>> _consumerGroupOffsets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<TopicPartition, TopicPartitionOffset>> _shareGroupOffsets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, ConsumerGroupMemberState>> _consumerGroupMembers = new(StringComparer.Ordinal);
    private Dictionary<(string GroupId, string InstanceId), string>? _staticConsumerGroupMembers;
    private readonly ConcurrentDictionary<string, int> _consumerGroupGenerations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, int>> _shareGroupMembers = new(StringComparer.Ordinal);
    private readonly HashSet<string> _shareGroupsWithMemberHistory = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<TopicPartition, Dictionary<long, ShareRecordState>>> _shareRecords = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Exception> _produceFailures = new(StringComparer.Ordinal);
    private readonly Dictionary<PreparedTransactionState, IInMemoryPreparedTransaction> _preparedTransactions = [];
    private readonly Dictionary<InMemoryTransactionMarker, List<PartitionState>> _transactionPartitions = [];
    private readonly InMemoryKafkaClusterOptions _options;
    private TaskCompletionSource _recordsChanged = NewRecordsChangedSource();
    private long _topologyVersion;
    private long _nextProducerId;
    private long _nextShareAcquisitionId;
    private long _nextConsumerGroupRegistrationId;
    private TimeSpan _produceLatency;
    private int _nextConsumerGroupGeneration;
    private Dictionary<InMemoryTransactionMarker, Dictionary<string, InMemoryPendingGroupOffsets>>? _pendingConsumerGroupOffsets;
    private TaskCompletionSource? _groupOffsetsChanged;

    public InMemoryKafkaCluster()
        : this(new InMemoryKafkaClusterOptions(), new KafkaFaultPlan())
    {
    }

    public InMemoryKafkaCluster(InMemoryKafkaClusterOptions options)
        : this(options, new KafkaFaultPlan())
    {
    }

    public InMemoryKafkaCluster(IKafkaFaultPlan faultPlan)
        : this(new InMemoryKafkaClusterOptions(), faultPlan)
    {
    }

    public InMemoryKafkaCluster(InMemoryKafkaClusterOptions options, IKafkaFaultPlan faultPlan)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(faultPlan);
        ArgumentNullException.ThrowIfNull(options.SupportedFeatures);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.DefaultPartitionCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.DeliveryCountLimit, 1);
        _options = options;
        FaultPlan = faultPlan;
    }

    public InMemoryKafkaClusterOptions Options => _options;

    /// <summary>
    /// Gets the deterministic fault plan consumed by in-memory client operations.
    /// </summary>
    public IKafkaFaultPlan FaultPlan { get; }

    public TimeSpan ProduceLatency
    {
        get
        {
            lock (_gate)
                return _produceLatency;
        }
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
            lock (_gate)
                _produceLatency = value;
        }
    }

    public void FailProduces(string topic, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(exception);

        lock (_gate)
            _produceFailures[topic] = exception;
    }

    public bool ClearProduceFailure(string topic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        lock (_gate)
            return _produceFailures.Remove(topic);
    }

    public void CreateTopic(string name, int partitionCount = 1, IReadOnlyDictionary<string, string>? configs = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(partitionCount, 1);

        lock (_gate)
            EnsureTopic(name, partitionCount, configs);
    }

    public bool DeleteTopic(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_gate)
            return DeleteTopicUnderLock(name);
    }

    internal bool DeleteTopic(Guid topicId)
    {
        if (topicId == Guid.Empty)
            return false;

        lock (_gate)
        {
            string? topicName = null;
            foreach (var topic in _topics.Values)
            {
                if (topic.TopicId == topicId)
                {
                    topicName = topic.Name;
                    break;
                }
            }

            return topicName is not null && DeleteTopicUnderLock(topicName);
        }
    }

    private bool DeleteTopicUnderLock(string name)
    {
        if (!_topics.Remove(name))
            return false;

        foreach (var records in _shareRecords.Values)
            RemoveTopicPartitions(records, name);
        foreach (var offsets in _shareGroupOffsets.Values)
            RemoveTopicPartitions(offsets, name);
        OnTopologyChangedUnderLock();
        return true;
    }

    private static void RemoveTopicPartitions<T>(Dictionary<TopicPartition, T> partitions, string topicName)
    {
        var removed = partitions.Keys.Where(partition => StringComparer.Ordinal.Equals(partition.Topic, topicName)).ToArray();
        foreach (var partition in removed)
            partitions.Remove(partition);
    }

    public IReadOnlyList<string> ListTopics()
    {
        lock (_gate)
            return _topics.Keys.Order(StringComparer.Ordinal).ToArray();
    }

    internal long TopologyVersion => Volatile.Read(ref _topologyVersion);

    internal (long Version, (string Name, Guid Id, int PartitionCount)[] Topics) GetTopologySnapshot()
    {
        lock (_gate)
        {
            var topics = _topics.Values
                .Select(static topic => (topic.Name, topic.TopicId, topic.Partitions.Count))
                .ToArray();
            return (_topologyVersion, topics);
        }
    }

    private void OnTopologyChangedUnderLock()
    {
        Interlocked.Increment(ref _topologyVersion);
        _recordsChanged.TrySetResult();
    }

    public IReadOnlyList<TopicPartition> GetTopicPartitions(string topic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        lock (_gate)
        {
            var state = GetTopicForRead(topic);
            return Enumerable.Range(0, state.Partitions.Count)
                .Select(partition => new TopicPartition(topic, partition))
                .ToArray();
        }
    }

    internal bool TryGetTopicPartitionCount(string topic, out int partitionCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        lock (_gate)
        {
            if (_topics.TryGetValue(topic, out var state))
            {
                partitionCount = state.Partitions.Count;
                return true;
            }

            partitionCount = 0;
            return false;
        }
    }

    internal bool ContainsTopicPartition(TopicPartition topicPartition)
    {
        lock (_gate)
            return ContainsTopicPartitionUnderLock(topicPartition);
    }

    internal ErrorCode GetTopicPartitionError(TopicPartition topicPartition)
    {
        lock (_gate)
            return GetTopicPartitionErrorUnderLock(topicPartition);
    }

    internal int RegisterConsumerGroupMember(
        string groupId,
        string memberId,
        IEnumerable<TopicPartition> subscribedPartitions,
        out long registrationId, string? groupInstanceId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        ArgumentNullException.ThrowIfNull(subscribedPartitions);

        if (groupInstanceId is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(groupInstanceId);
        HashSet<TopicPartition> partitions = groupInstanceId is null
            ? new HashSet<TopicPartition>(subscribedPartitions)
            : new StaticMemberPartitions(subscribedPartitions, groupInstanceId);

        lock (_gate)
        {
            if (!_consumerGroupMembers.TryGetValue(groupId, out var members))
            {
                members = new Dictionary<string, ConsumerGroupMemberState>(StringComparer.Ordinal);
                _consumerGroupMembers[groupId] = members;
            }

            registrationId = ++_nextConsumerGroupRegistrationId;
            if (_staticConsumerGroupMembers is not null && members.TryGetValue(memberId, out var replaced) &&
                replaced.GroupInstanceId is { } replacedInstance)
                _staticConsumerGroupMembers.Remove((groupId, replacedInstance));
            if (groupInstanceId is not null)
            {
                var staticMembers = _staticConsumerGroupMembers ??= new();
                var identity = (groupId, groupInstanceId);
                ref var previousMember = ref CollectionsMarshal.GetValueRefOrAddDefault(staticMembers, identity, out var exists);
                if (exists)
                    members.Remove(previousMember!);
                previousMember = memberId;
            }
            members[memberId] = new ConsumerGroupMemberState(registrationId, partitions);
            var generation = ++_nextConsumerGroupGeneration;
            _consumerGroupGenerations[groupId] = generation;
            return generation;
        }
    }

    internal void UpdateConsumerGroupSubscription(
        string groupId,
        string memberId,
        long registrationId,
        IReadOnlySet<TopicPartition> subscribedPartitions)
    {
        lock (_gate)
        {
            // A metadata refresh must not rejoin a member removed or fenced by the admin client.
            if (!_consumerGroupMembers.TryGetValue(groupId, out var members) ||
                !members.TryGetValue(memberId, out var member) ||
                member.RegistrationId != registrationId ||
                member.SubscribedPartitions.SetEquals(subscribedPartitions))
            {
                return;
            }

            member.SubscribedPartitions.Clear();
            member.SubscribedPartitions.UnionWith(subscribedPartitions);
            _consumerGroupGenerations[groupId] = ++_nextConsumerGroupGeneration;
        }
    }

    internal void UnregisterConsumerGroupMember(
        string groupId,
        string memberId,
        long registrationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);

        lock (_gate)
        {
            if (!_consumerGroupMembers.TryGetValue(groupId, out var members))
                return;

            if (!members.TryGetValue(memberId, out var member) ||
                member.RegistrationId != registrationId)
            {
                return;
            }

            members.Remove(memberId);
            if (member.GroupInstanceId is { } instanceId)
                _staticConsumerGroupMembers!.Remove((groupId, instanceId));

            if (members.Count == 0)
            {
                _consumerGroupMembers.Remove(groupId);
                _consumerGroupGenerations[groupId] = 0;
            }
            else
            {
                _consumerGroupGenerations[groupId] = ++_nextConsumerGroupGeneration;
            }
        }
    }

    internal IReadOnlySet<TopicPartition> GetConsumerGroupAssignment(
        string groupId,
        string memberId,
        long registrationId,
        out int generation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);

        lock (_gate)
        {
            if (!_consumerGroupMembers.TryGetValue(groupId, out var members) ||
                !members.TryGetValue(memberId, out var member) ||
                member.RegistrationId != registrationId)
            {
                generation = -1;
                return new HashSet<TopicPartition>();
            }

            generation = _consumerGroupGenerations.GetValueOrDefault(groupId);
            var assignments = BuildConsumerGroupAssignments(groupId);
            return assignments.TryGetValue(memberId, out var partitions)
                ? partitions.ToHashSet()
                : new HashSet<TopicPartition>();
        }
    }

    internal int GetConsumerGroupGeneration(string groupId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        return _consumerGroupGenerations.TryGetValue(groupId, out var generation)
            ? generation
            : 0;
    }

    public IReadOnlyList<InMemoryRecord> ReadRecords(string topic, int partition = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentOutOfRangeException.ThrowIfLessThan(partition, 0);

        lock (_gate)
        {
            var state = GetTopicForRead(topic);
            var partitionState = GetPartitionForRead(state, partition);
            var visible = new List<InMemoryRecord>(partitionState.Records.Count);
            foreach (var record in partitionState.Records)
            {
                if (record.Offset >= partitionState.FirstUnstableOffset)
                    break;
                if (IsRecordVisibleUnderLock(record))
                    visible.Add(CloneRecord(record));
            }

            return visible;
        }
    }

    internal static InMemoryTransactionMarker CreateTransactionMarker() => new();

    internal long AllocateProducerId() => Interlocked.Increment(ref _nextProducerId);

    internal void RegisterPreparedTransaction(
        PreparedTransactionState state,
        IInMemoryPreparedTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        lock (_gate)
            _preparedTransactions[state] = transaction;
    }

    internal IInMemoryPreparedTransaction? GetPreparedTransaction(PreparedTransactionState state)
    {
        lock (_gate)
            return _preparedTransactions.GetValueOrDefault(state);
    }

    internal FatalTransactionException? CompleteTransaction(
        InMemoryTransactionMarker transactionMarker,
        bool committed,
        Dictionary<string, InMemoryPendingGroupOffsets> pendingOffsets,
        PreparedTransactionState preparedState,
        IInMemoryPreparedTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(pendingOffsets);
        ArgumentNullException.ThrowIfNull(transactionMarker);
        ArgumentNullException.ThrowIfNull(transaction);

        TaskCompletionSource signal;
        TaskCompletionSource? offsetsSignal;
        FatalTransactionException? failure = null;
        lock (_gate)
        {
            if (transactionMarker.State != InMemoryTransactionState.Ongoing)
                throw new InvalidOperationException("The in-memory transaction is no longer active.");

            if (committed)
            {
                foreach (var (groupId, pending) in pendingOffsets)
                {
                    for (var i = 0; i < pending.MetadataSnapshots.Count; i++)
                    {
                        failure = ValidateConsumerGroupMetadataUnderLock(groupId, pending.MetadataSnapshots[i]);
                        if (failure is not null)
                            break;
                    }
                    if (failure is not null)
                        break;
                }

                committed = failure is null;
            }

            if (committed)
            {
                foreach (var (groupId, pending) in pendingOffsets)
                {
                    var groupOffsets = GetOrCreateConsumerGroupOffsetsUnderLock(groupId);
                    for (var i = 0; i < pending.Offsets.Count; i++)
                    {
                        var offset = pending.Offsets[i];
                        groupOffsets[new TopicPartition(offset.Topic, offset.Partition)] = offset;
                    }
                }
            }

            transactionMarker.State = committed
                ? InMemoryTransactionState.Committed
                : InMemoryTransactionState.Aborted;

            if (_transactionPartitions.Remove(transactionMarker, out var transactionPartitions))
            {
                foreach (var partition in transactionPartitions)
                    partition.CompleteTransaction(transactionMarker);
            }

            if (preparedState.HasTransaction &&
                _preparedTransactions.TryGetValue(preparedState, out var registered) &&
                ReferenceEquals(registered, transaction))
            {
                _preparedTransactions.Remove(preparedState);
            }

            var offsetsCompleted = _pendingConsumerGroupOffsets?.Remove(transactionMarker) == true;
            offsetsSignal = offsetsCompleted ? _groupOffsetsChanged : null;
            if (offsetsCompleted)
                _groupOffsetsChanged = null;
            signal = _recordsChanged;
        }

        signal.TrySetResult();
        offsetsSignal?.TrySetResult();
        return failure;
    }

    internal ValueTask<RecordMetadata> AppendAsync(
        string topic,
        int? partition,
        byte[] key,
        bool isKeyNull,
        byte[] value,
        bool isValueNull,
        IReadOnlyList<Header>? headers,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken,
        KafkaFaultOperation faultOperation = KafkaFaultOperation.Produce,
        InMemoryTransactionMarker? transactionMarker = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();

        TimeSpan latency;
        Exception? failure;
        TaskCompletionSource? signal = null;
        RecordMetadata metadata = default;
        var hasMatchingFault = FaultPlan is not KafkaFaultPlan indexedPlan ||
                               indexedPlan.HasPotentialProduceMatch(faultOperation, topic);
        lock (_gate)
        {
            failure = _produceFailures.GetValueOrDefault(topic);
            latency = _produceLatency;
            if (latency == TimeSpan.Zero && failure is null && !hasMatchingFault)
            {
                var state = GetOrAutoCreateTopic(topic);
                var selectedPartition = SelectPartition(state, partition, key, isKeyNull);
                metadata = AppendRecordUnderLock(
                    topic,
                    state,
                    selectedPartition,
                    key,
                    isKeyNull,
                    value,
                    isValueNull,
                    headers,
                    timestamp,
                    transactionMarker,
                    out signal);
            }
        }

        if (signal is not null)
        {
            signal.TrySetResult();
            return new ValueTask<RecordMetadata>(metadata);
        }

        return AppendSlowAsync(
            topic,
            partition,
            key,
            isKeyNull,
            value,
            isValueNull,
            headers,
            timestamp,
            faultOperation,
            transactionMarker,
            latency,
            failure,
            cancellationToken);
    }

    private async ValueTask<RecordMetadata> AppendSlowAsync(
        string topic,
        int? partition,
        byte[] key,
        bool isKeyNull,
        byte[] value,
        bool isValueNull,
        IReadOnlyList<Header>? headers,
        DateTimeOffset timestamp,
        KafkaFaultOperation faultOperation,
        InMemoryTransactionMarker? transactionMarker,
        TimeSpan latency,
        Exception? failure,
        CancellationToken cancellationToken)
    {
        if (latency > TimeSpan.Zero)
            await Task.Delay(latency, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (failure is not null)
            throw failure;

        TopicState selectedTopic;
        int selectedPartition;
        lock (_gate)
        {
            selectedTopic = GetOrAutoCreateTopic(topic);
            selectedPartition = SelectPartition(selectedTopic, partition, key, isKeyNull);
        }

        await FaultPlan.ApplyAsync(
            new KafkaFaultScope(faultOperation, topic, selectedPartition),
            cancellationToken).ConfigureAwait(false);

        TaskCompletionSource signal;
        RecordMetadata metadata;
        lock (_gate)
        {
            if (!_topics.TryGetValue(topic, out var state) ||
                !ReferenceEquals(state, selectedTopic) ||
                (uint)selectedPartition >= (uint)state.Partitions.Count)
            {
                throw new ProduceException(
                    ErrorCode.UnknownTopicOrPartition,
                    $"Topic '{topic}' changed while the produce operation was paused.")
                {
                    Topic = topic,
                    Partition = selectedPartition
                };
            }

            metadata = AppendRecordUnderLock(
                topic,
                state,
                selectedPartition,
                key,
                isKeyNull,
                value,
                isValueNull,
                headers,
                timestamp,
                transactionMarker,
                out signal);
        }

        signal.TrySetResult();
        return metadata;
    }

    private RecordMetadata AppendRecordUnderLock(
        string topic,
        TopicState state,
        int selectedPartition,
        byte[] key,
        bool isKeyNull,
        byte[] value,
        bool isValueNull,
        IReadOnlyList<Header>? headers,
        DateTimeOffset timestamp,
        InMemoryTransactionMarker? transactionMarker,
        out TaskCompletionSource signal)
    {
        if (transactionMarker is { State: not InMemoryTransactionState.Ongoing })
        {
            throw new InvalidOperationException(
                "The in-memory transaction completed while the produce operation was paused.");
        }

        var partitionState = state.Partitions[selectedPartition];
        var offset = partitionState.HighWatermark;
        var timestampMs = timestamp.ToUnixTimeMilliseconds();

        var record = new InMemoryRecord
        {
            Topic = topic,
            Partition = selectedPartition,
            Offset = offset,
            Key = key,
            IsKeyNull = isKeyNull,
            Value = value,
            IsValueNull = isValueNull,
            Headers = CopyHeaders(headers),
            TimestampMs = timestampMs,
            Transaction = transactionMarker
        };

        partitionState.Append(record);
        if (transactionMarker is not null &&
            partitionState.RegisterTransaction(transactionMarker, offset))
        {
            if (!_transactionPartitions.TryGetValue(transactionMarker, out var transactionPartitions))
            {
                transactionPartitions = [];
                _transactionPartitions.Add(transactionMarker, transactionPartitions);
            }

            transactionPartitions.Add(partitionState);
        }

        signal = _recordsChanged;
        return new RecordMetadata
        {
            Topic = topic,
            Partition = selectedPartition,
            Offset = offset,
            Timestamp = timestamp,
            KeySize = isKeyNull ? 0 : key.Length,
            ValueSize = isValueNull ? 0 : value.Length
        };
    }

    internal bool TryRead(TopicPartition topicPartition, long offset, out InMemoryRecord record) =>
        TryRead(topicPartition, offset, IsolationLevel.ReadCommitted, borrowStoredRecord: false, out record, out _);

    internal bool TryRead(
        TopicPartition topicPartition,
        long offset,
        IsolationLevel isolationLevel,
        out InMemoryRecord record) =>
        TryRead(topicPartition, offset, isolationLevel, borrowStoredRecord: false, out record, out _);

    internal bool TryRead(
        TopicPartition topicPartition,
        long offset,
        IsolationLevel isolationLevel,
        bool borrowStoredRecord,
        out InMemoryRecord record) =>
        TryRead(topicPartition, offset, isolationLevel, borrowStoredRecord, out record, out _);

    internal bool TryRead(
        TopicPartition topicPartition,
        long offset,
        out InMemoryRecord record,
        out bool blockedByOngoingTransaction) =>
        TryRead(
            topicPartition,
            offset,
            IsolationLevel.ReadCommitted,
            borrowStoredRecord: false,
            out record,
            out blockedByOngoingTransaction);

    internal bool TryRead(
        TopicPartition topicPartition,
        long offset,
        IsolationLevel isolationLevel,
        bool borrowStoredRecord,
        out InMemoryRecord record,
        out bool blockedByOngoingTransaction) =>
        TryRead(
            topicPartition,
            offset,
            isolationLevel,
            borrowStoredRecord,
            out record,
            out blockedByOngoingTransaction,
            out _);

    internal bool TryRead(
        TopicPartition topicPartition,
        long offset,
        IsolationLevel isolationLevel,
        bool borrowStoredRecord,
        out InMemoryRecord record,
        out bool blockedByOngoingTransaction,
        out long nextReadableOffset)
    {
        lock (_gate)
        {
            if (!TryReadRecordUnderLock(
                    topicPartition,
                    offset,
                    isolationLevel,
                    out var candidate,
                    out blockedByOngoingTransaction))
            {
                record = null!;
                nextReadableOffset = GetReadEndOffsetUnderLock(topicPartition, isolationLevel, offset);
                return false;
            }

            record = borrowStoredRecord ? candidate : CloneRecord(candidate);
            nextReadableOffset = candidate.Offset;
            return true;
        }
    }

    private long GetReadEndOffsetUnderLock(
        TopicPartition topicPartition,
        IsolationLevel isolationLevel,
        long fallbackOffset)
    {
        if (!_topics.TryGetValue(topicPartition.Topic, out var topic)
            || (uint)topicPartition.Partition >= (uint)topic.Partitions.Count)
        {
            return fallbackOffset;
        }

        var partition = topic.Partitions[topicPartition.Partition];
        return isolationLevel == IsolationLevel.ReadCommitted
            && partition.FirstUnstableOffset != long.MaxValue
                ? Math.Min(partition.HighWatermark, partition.FirstUnstableOffset)
                : partition.HighWatermark;
    }

    internal bool TryAcquireShareRecord(
        string groupId,
        string memberId,
        ShareGroupMemberRegistration registration,
        TopicPartition topicPartition,
        long offset,
        out InMemoryRecord record,
        out int deliveryCount)
    {
        lock (_gate)
        {
            if (!TryAcquireShareRecordUnderLock(groupId, memberId, registration, topicPartition, offset, out record, out var state))
            {
                deliveryCount = 0;
                return false;
            }

            deliveryCount = ++state.DeliveryCount;
            return true;
        }
    }

    internal bool TryAcquireShareRecordForFault(
        string groupId,
        string memberId,
        ShareGroupMemberRegistration registration,
        TopicPartition topicPartition,
        long offset,
        out InMemoryRecord record)
    {
        lock (_gate)
            return TryAcquireShareRecordUnderLock(groupId, memberId, registration, topicPartition, offset, out record, out _);
    }

    private bool TryAcquireShareRecordUnderLock(
        string groupId,
        string memberId,
        ShareGroupMemberRegistration registration,
        TopicPartition topicPartition,
        long offset,
        out InMemoryRecord record,
        out ShareRecordState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        record = null!;
        state = null!;
        if (!registration.IsActive ||
            !_topics.TryGetValue(topicPartition.Topic, out var topic) ||
            (uint)topicPartition.Partition >= (uint)topic.Partitions.Count)
            return false;

        offset = Math.Max(offset, GetCommittedShareOffset(groupId, topicPartition) ?? GetWatermarks(topicPartition).Low);
        var partition = topic.Partitions[topicPartition.Partition];
        var records = GetShareRecordPartition(groupId, topicPartition, create: true)!;
        var isolationLevel = GetShareIsolationUnderLock(groupId);
        foreach (var candidate in partition.Records)
        {
            if (isolationLevel == IsolationLevel.ReadCommitted &&
                candidate.Offset >= partition.FirstUnstableOffset)
                break;
            if (candidate.Offset < offset ||
                (isolationLevel == IsolationLevel.ReadCommitted && !IsRecordVisibleUnderLock(candidate)))
                continue;
            if (!records.TryGetValue(candidate.Offset, out var candidateState))
            {
                candidateState = new ShareRecordState();
                records.Add(candidate.Offset, candidateState);
            }

            if (candidateState.Archived || candidateState.Registration is { IsActive: true })
                continue;

            // An inactive member's lease is available to every member, including a different ID.
            if (candidateState.DeliveryCount >= _options.DeliveryCountLimit)
            {
                candidateState.Registration = null;
                candidateState.Archived = true;
                AdvanceShareStartOffsetUnderLock(groupId, topicPartition, records);
                offset = Math.Max(offset, GetCommittedShareOffset(groupId, topicPartition) ?? offset);
                continue;
            }

            candidateState.Registration = registration;
            // Fence delayed work even when a topic or lease is recreated under the same member.
            candidateState.AcquisitionId = ++_nextShareAcquisitionId;
            state = candidateState;
            record = CloneRecord(candidate, candidateState.AcquisitionId);
            return true;
        }

        return false;
    }

    internal bool TryCompleteShareRecordAcquisition(
        string groupId,
        string memberId,
        ShareGroupMemberRegistration registration,
        TopicPartition topicPartition,
        InMemoryRecord record,
        out int deliveryCount)
    {
        lock (_gate)
        {
            var records = GetShareRecordPartition(groupId, topicPartition, create: false);
            if (!registration.IsActive || records is null ||
                !records.TryGetValue(record.Offset, out var state) ||
                !OwnsShareRecord(state, memberId, registration, record.ShareAcquisitionId))
            {
                deliveryCount = 0;
                return false;
            }

            deliveryCount = ++state.DeliveryCount;
            return true;
        }
    }

    internal void ReleaseShareRecords(
        string groupId,
        string memberId,
        ShareGroupMemberRegistration registration,
        IEnumerable<InMemoryRecord> records)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        ArgumentNullException.ThrowIfNull(records);
        lock (_gate)
        {
            HashSet<TopicPartition>? archivedPartitions = null;
            foreach (var record in records)
            {
                if (CompleteShareRecordUnderLock(groupId, memberId, registration, record, AcknowledgeType.Release))
                    (archivedPartitions ??= []).Add(new TopicPartition(record.Topic, record.Partition));
            }
            AdvanceShareStartOffsetsUnderLock(groupId, archivedPartitions);
        }
    }

    internal void CompleteShareRecords(
        string groupId,
        string memberId,
        ShareGroupMemberRegistration registration,
        InMemoryShareRecordCompletion[] records,
        int recordCount,
        Span<bool> deadLetterEligible = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentOutOfRangeException.ThrowIfNegative(recordCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(recordCount, records.Length);
        if (!deadLetterEligible.IsEmpty)
            ArgumentOutOfRangeException.ThrowIfGreaterThan(recordCount, deadLetterEligible.Length);
        lock (_gate)
        {
            HashSet<TopicPartition>? archivedPartitions = null;
            for (var index = 0; index < recordCount; index++)
            {
                var record = records[index];
                var archived = CompleteShareRecordUnderLock(groupId, memberId, registration, record.Record, record.Type);
                // Only the current lease's accepted Reject may write a dead-letter record.
                if (!deadLetterEligible.IsEmpty)
                    deadLetterEligible[index] = archived && record.Type == AcknowledgeType.Reject;
                if (archived)
                    (archivedPartitions ??= []).Add(new TopicPartition(record.Record.Topic, record.Record.Partition));
            }
            AdvanceShareStartOffsetsUnderLock(groupId, archivedPartitions);
        }
    }

    private void AdvanceShareStartOffsetsUnderLock(string groupId, HashSet<TopicPartition>? partitions)
    {
        if (partitions is null)
            return;
        foreach (var partition in partitions)
            AdvanceShareStartOffsetUnderLock(groupId, partition, GetShareRecordPartition(groupId, partition, create: false)!);
    }

    private bool CompleteShareRecordUnderLock(
        string groupId,
        string memberId,
        ShareGroupMemberRegistration registration,
        InMemoryRecord record,
        AcknowledgeType type)
    {
        var partition = new TopicPartition(record.Topic, record.Partition);
        var records = GetShareRecordPartition(groupId, partition, create: false);
        if (records is null || !records.TryGetValue(record.Offset, out var state) ||
            !OwnsShareRecord(state, memberId, registration, record.ShareAcquisitionId))
            return false;

        // Renewal keeps the current acquisition, including at the delivery count limit.
        if (type == AcknowledgeType.Renew)
            return false;

        state.Registration = null;
        state.Archived = type is AcknowledgeType.Accept or AcknowledgeType.Reject ||
                         state.DeliveryCount >= _options.DeliveryCountLimit;
        return state.Archived;
    }

    internal bool IsCurrentShareRecord(
        string groupId,
        ShareGroupMemberRegistration registration,
        InMemoryRecord record)
    {
        lock (_gate)
        {
            var partition = new TopicPartition(record.Topic, record.Partition);
            var records = GetShareRecordPartition(groupId, partition, create: false);
            return registration.IsActive && records is not null &&
                   records.TryGetValue(record.Offset, out var state) &&
                   OwnsShareRecord(state, registration.MemberId, registration, record.ShareAcquisitionId);
        }
    }

    private static bool OwnsShareRecord(
        ShareRecordState state,
        string memberId,
        ShareGroupMemberRegistration registration,
        long acquisitionId) =>
        state.AcquisitionId == acquisitionId &&
        ReferenceEquals(state.Registration, registration) &&
        StringComparer.Ordinal.Equals(registration.MemberId, memberId);

    internal string? GetTopicName(Guid topicId)
    {
        if (topicId == Guid.Empty)
            return null;

        lock (_gate)
        {
            foreach (var topic in _topics.Values)
                if (topic.TopicId == topicId)
                    return topic.Name;
        }

        return null;
    }

    internal void RollbackShareRecordAcquisition(
        string groupId,
        string memberId,
        ShareGroupMemberRegistration registration,
        TopicPartition topicPartition,
        InMemoryRecord record)
    {
        lock (_gate)
        {
            var records = GetShareRecordPartition(groupId, topicPartition, create: false);
            if (records is not null && records.TryGetValue(record.Offset, out var state) &&
                OwnsShareRecord(state, memberId, registration, record.ShareAcquisitionId))
                state.Registration = null;
        }
    }

    internal async Task WaitForRecordsAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        Task task;
        lock (_gate)
        {
            task = _recordsChanged.Task;
            if (task.IsCompleted)
                _recordsChanged = NewRecordsChangedSource();
        }

        if (timeout == Timeout.InfiniteTimeSpan)
        {
            await task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await task.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
    }

    internal Task ObserveRecordsChanged()
    {
        lock (_gate)
        {
            var task = _recordsChanged.Task;
            if (task.IsCompleted)
                _recordsChanged = NewRecordsChangedSource();
            return task;
        }
    }

    internal void SignalRecordsChanged()
    {
        TaskCompletionSource signal;
        lock (_gate)
            signal = _recordsChanged;

        signal.TrySetResult();
    }

    internal WatermarkOffsets GetWatermarks(TopicPartition topicPartition)
        => GetWatermarks(topicPartition, IsolationLevel.ReadUncommitted);

    internal WatermarkOffsets GetWatermarks(
        TopicPartition topicPartition,
        IsolationLevel isolationLevel)
    {
        lock (_gate)
        {
            if (!_topics.TryGetValue(topicPartition.Topic, out var topic) ||
                (uint)topicPartition.Partition >= (uint)topic.Partitions.Count)
            {
                return new WatermarkOffsets(0, 0);
            }

            var partition = topic.Partitions[topicPartition.Partition];
            var highWatermark = isolationLevel == IsolationLevel.ReadCommitted
                && partition.FirstUnstableOffset != long.MaxValue
                    ? Math.Min(partition.HighWatermark, partition.FirstUnstableOffset)
                    : partition.HighWatermark;
            return new WatermarkOffsets(partition.LogStartOffset, highWatermark);
        }
    }

    internal long GetOffsetForTimestamp(TopicPartition topicPartition, long timestamp)
    {
        lock (_gate)
        {
            if (!_topics.TryGetValue(topicPartition.Topic, out var topic) ||
                (uint)topicPartition.Partition >= (uint)topic.Partitions.Count)
            {
                return -1;
            }

            var partition = topic.Partitions[topicPartition.Partition];
            if (timestamp == TopicPartitionTimestamp.Earliest)
                return partition.LogStartOffset;
            if (timestamp == TopicPartitionTimestamp.Latest)
                return partition.HighWatermark;

            foreach (var record in partition.Records)
            {
                if (record.TimestampMs >= timestamp)
                    return record.Offset;
            }

            return -1;
        }
    }

    internal long? GetCommittedOffset(string groupId, TopicPartition topicPartition)
    {
        lock (_gate)
        {
            return _consumerGroupOffsets.TryGetValue(groupId, out var offsets) &&
                   offsets.TryGetValue(topicPartition, out var offset)
                ? offset.Offset
                : null;
        }
    }

    internal TopicPartitionOffset? GetCommittedOffsetInfo(
        string groupId,
        TopicPartition topicPartition)
    {
        lock (_gate)
        {
            return _consumerGroupOffsets.TryGetValue(groupId, out var offsets) &&
                   offsets.TryGetValue(topicPartition, out var offset)
                ? offset
                : null;
        }
    }

    internal IReadOnlyDictionary<TopicPartition, TopicPartitionOffset> GetCommittedOffsets(
        string groupId,
        IReadOnlyCollection<TopicPartition> partitions)
    {
        lock (_gate)
        {
            var result = new Dictionary<TopicPartition, TopicPartitionOffset>(partitions.Count);
            if (!_consumerGroupOffsets.TryGetValue(groupId, out var offsets))
                return result;

            foreach (var partition in partitions)
            {
                if (offsets.TryGetValue(partition, out var offset))
                    result[partition] = offset;
            }

            return result;
        }
    }

    internal void CommitOffsets(string groupId, IEnumerable<TopicPartitionOffset> offsets)
    {
        lock (_gate)
            CommitOffsetsUnderLock(groupId, offsets);
    }

    internal long? GetCommittedShareOffset(string groupId, TopicPartition topicPartition)
    {
        lock (_gate)
        {
            return _shareGroupOffsets.TryGetValue(groupId, out var offsets) &&
                   offsets.TryGetValue(topicPartition, out var offset)
                ? offset.Offset
                : null;
        }
    }

    internal void CommitShareOffsets(string groupId, IEnumerable<TopicPartitionOffset> offsets)
    {
        lock (_gate)
        {
            var errorCode = GetShareGroupMutationErrorUnderLock(groupId, allowMissing: true);
            if (errorCode != ErrorCode.None)
                throw new KafkaException(errorCode, $"Share group '{groupId}' must be empty before altering offsets.");
            CommitShareOffsetsUnderLock(groupId, offsets);
        }
    }

    internal void CommitOffsets(string groupId, IReadOnlyList<TopicPartitionOffset> offsets)
    {
        lock (_gate)
        {
            var groupOffsets = GetOrCreateConsumerGroupOffsetsUnderLock(groupId);
            for (var index = 0; index < offsets.Count; index++)
            {
                var offset = offsets[index];
                groupOffsets[new TopicPartition(offset.Topic, offset.Partition)] = offset;
            }
        }
    }

    internal void CommitOffsets(
        string groupId,
        TopicPartitionOffset[] offsets,
        int offsetCount)
    {
        lock (_gate)
        {
            var groupOffsets = GetOrCreateConsumerGroupOffsetsUnderLock(groupId);
            for (var index = 0; index < offsetCount; index++)
            {
                var offset = offsets[index];
                groupOffsets[new TopicPartition(offset.Topic, offset.Partition)] = offset;
            }
        }
    }

    internal IReadOnlyDictionary<TopicPartition, long> GetGroupOffsets(string groupId)
    {
        lock (_gate)
        {
            return _consumerGroupOffsets.TryGetValue(groupId, out var offsets)
                ? offsets.ToDictionary(static item => item.Key, static item => item.Value.Offset)
                : new Dictionary<TopicPartition, long>();
        }
    }

    internal IReadOnlyDictionary<TopicPartition, long> GetShareGroupOffsets(string groupId)
    {
        lock (_gate)
        {
            return _shareGroupOffsets.TryGetValue(groupId, out var offsets)
                ? offsets.ToDictionary(static item => item.Key, static item => item.Value.Offset)
                : new Dictionary<TopicPartition, long>();
        }
    }

    internal Dictionary<TopicPartition, TopicPartitionOffset> GetGroupOffsetDetails(string groupId)
    {
        lock (_gate)
        {
            return _consumerGroupOffsets.TryGetValue(groupId, out var offsets)
                ? new Dictionary<TopicPartition, TopicPartitionOffset>(offsets)
                : new Dictionary<TopicPartition, TopicPartitionOffset>();
        }
    }

    internal IReadOnlyList<string> ListGroups()
    {
        lock (_gate)
        {
            var groups = new HashSet<string>(_consumerGroupOffsets.Keys, StringComparer.Ordinal);
            groups.UnionWith(_consumerGroupGenerations.Keys);
            return groups.Order(StringComparer.Ordinal).ToArray();
        }
    }

    internal IReadOnlyList<InMemoryShareGroupListing> ListShareGroups()
    {
        lock (_gate)
        {
            var groupIds = new HashSet<string>(_shareGroupsWithMemberHistory, StringComparer.Ordinal);
            groupIds.UnionWith(_shareGroupOffsets.Keys);
            groupIds.UnionWith(_shareGroupMembers.Keys);
            groupIds.UnionWith(_shareRecords.Keys);
            var result = new InMemoryShareGroupListing[groupIds.Count];
            var index = 0;
            foreach (var groupId in groupIds.Order(StringComparer.Ordinal))
            {
                var hasActiveMembers = _shareGroupMembers.TryGetValue(groupId, out var members) &&
                    members.Count > 0;
                result[index++] = new InMemoryShareGroupListing(groupId, hasActiveMembers);
            }

            return result;
        }
    }

    internal ShareGroupMemberRegistration RegisterShareGroupMember(string groupId, string memberId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);

        lock (_gate)
        {
            _shareGroupsWithMemberHistory.Add(groupId);
            if (!_shareGroupMembers.TryGetValue(groupId, out var members))
            {
                members = new Dictionary<string, int>(StringComparer.Ordinal);
                _shareGroupMembers[groupId] = members;
            }

            members[memberId] = members.GetValueOrDefault(memberId) + 1;
            return new ShareGroupMemberRegistration(memberId);
        }
    }

    internal void UnregisterShareGroupMember(
        string groupId,
        string memberId,
        ShareGroupMemberRegistration registration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);

        lock (_gate)
        {
            if (!registration.IsActive)
                return;

            registration.IsActive = false;
            if (!_shareGroupMembers.TryGetValue(groupId, out var members))
                return;

            if (!members.TryGetValue(memberId, out var registrationCount))
                return;

            if (registrationCount > 1)
                members[memberId] = registrationCount - 1;
            else
                members.Remove(memberId);

            if (members.Count == 0)
                _shareGroupMembers.Remove(groupId);
        }
    }

    internal ErrorCode DeleteShareGroup(string groupId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        lock (_gate)
        {
            var hasOffsets = _shareGroupOffsets.ContainsKey(groupId);
            var hasMembers = _shareGroupMembers.TryGetValue(groupId, out var members);
            var hasRecords = _shareRecords.TryGetValue(groupId, out var records);
            var hasMemberHistory = _shareGroupsWithMemberHistory.Contains(groupId);
            if (!hasOffsets && !hasMembers && !hasRecords && !hasMemberHistory)
                return ErrorCode.GroupIdNotFound;

            if (members is { Count: > 0 } || (records is not null && HasShareRecordLeases(records)))
                return ErrorCode.NonEmptyGroup;

            _shareGroupOffsets.Remove(groupId);
            _shareGroupMembers.Remove(groupId);
            _shareRecords.Remove(groupId);
            _shareGroupsWithMemberHistory.Remove(groupId);
            return ErrorCode.None;
        }
    }

    internal ErrorCode DeleteGroup(string groupId)
    {
        lock (_gate)
        {
            if (_consumerGroupMembers.TryGetValue(groupId, out var members) && members.Count != 0)
                return ErrorCode.NonEmptyGroup;

            var hadClassicDescription = false;
            if (_classicGroupDescriptions is { } classicDescriptions)
            {
                if (classicDescriptions.TryGetValue(groupId, out var classic) && classic.Members.Count != 0)
                    return ErrorCode.NonEmptyGroup;
                hadClassicDescription = classicDescriptions.Remove(groupId);
            }

            return RemoveConsumerGroupUnderLock(groupId) || hadClassicDescription
                ? ErrorCode.None
                : ErrorCode.GroupIdNotFound;
        }
    }

    internal bool DeleteGroupOffsets(string groupId, IEnumerable<TopicPartition> partitions)
    {
        lock (_gate)
        {
            if (!_consumerGroupOffsets.TryGetValue(groupId, out var offsets))
                return _consumerGroupGenerations.ContainsKey(groupId);

            foreach (var partition in partitions)
                offsets.Remove(partition);

            return true;
        }
    }

    internal void DeleteShareGroupOffsets(string groupId, string topicName)
    {
        lock (_gate)
        {
            var errorCode = GetShareGroupMutationErrorUnderLock(groupId, allowMissing: true);
            if (errorCode != ErrorCode.None)
                throw new KafkaException(errorCode, $"Share group '{groupId}' must be empty before deleting offsets.");
            if (_shareGroupOffsets.TryGetValue(groupId, out var offsets))
            {
                RemoveTopicPartitions(offsets, topicName);
                if (offsets.Count == 0)
                    _shareGroupOffsets.Remove(groupId);
            }
            ClearShareTopicRecordsUnderLock(groupId, topicName);
        }
    }

    private void ClearShareTopicRecordsUnderLock(string groupId, string topicName)
    {
        if (_shareRecords.TryGetValue(groupId, out var records))
            RemoveTopicPartitions(records, topicName);
    }

    internal IReadOnlyDictionary<TopicPartition, ErrorCode> AlterStreamsGroupOffsets(
        string groupId,
        IReadOnlyList<TopicPartitionOffset> offsets)
    {
        lock (_gate)
        {
            var results = new Dictionary<TopicPartition, ErrorCode>(offsets.Count);
            if (_consumerGroupMembers.TryGetValue(groupId, out var members) && members.Count != 0)
            {
                foreach (var offset in offsets)
                    results[new TopicPartition(offset.Topic, offset.Partition)] = ErrorCode.UnknownMemberId;
                return results;
            }

            Dictionary<TopicPartition, TopicPartitionOffset>? groupOffsets = null;
            foreach (var offset in offsets)
            {
                var partition = new TopicPartition(offset.Topic, offset.Partition);
                var errorCode = GetTopicPartitionErrorUnderLock(partition);
                if (errorCode != ErrorCode.None)
                {
                    results[partition] = errorCode;
                    continue;
                }

                groupOffsets ??= GetOrCreateConsumerGroupOffsetsUnderLock(groupId);
                groupOffsets[partition] = offset;
                results[partition] = ErrorCode.None;
            }
            if (groupOffsets is not null)
                (_streamsGroupIds ??= new HashSet<string>(StringComparer.Ordinal)).Add(groupId);
            return results;
        }
    }

    internal IReadOnlyDictionary<TopicPartition, ErrorCode> DeleteStreamsGroupOffsets(
        string groupId,
        IReadOnlyList<TopicPartition> partitions)
    {
        lock (_gate)
        {
            var results = new Dictionary<TopicPartition, ErrorCode>(partitions.Count);
            _consumerGroupOffsets.TryGetValue(groupId, out var offsets);
            if (offsets is null && !_consumerGroupGenerations.ContainsKey(groupId))
            {
                foreach (var partition in partitions)
                    results[partition] = ErrorCode.GroupIdNotFound;
                return results;
            }

            _consumerGroupMembers.TryGetValue(groupId, out var members);
            foreach (var partition in partitions)
            {
                var errorCode = GetTopicPartitionErrorUnderLock(partition);
                if (errorCode != ErrorCode.None)
                {
                    results[partition] = errorCode;
                    continue;
                }

                var isSubscribed = false;
                if (members is not null)
                {
                    foreach (var member in members.Values)
                    {
                        foreach (var subscribedPartition in member.SubscribedPartitions)
                        {
                            if (subscribedPartition.Topic != partition.Topic)
                                continue;

                            isSubscribed = true;
                            break;
                        }

                        if (isSubscribed)
                            break;
                    }
                }

                if (isSubscribed)
                {
                    results[partition] = ErrorCode.GroupSubscribedToTopic;
                    continue;
                }

                offsets?.Remove(partition);
                results[partition] = ErrorCode.None;
            }

            return results;
        }
    }

    internal IReadOnlyDictionary<TopicPartition, long> DeleteRecords(IReadOnlyDictionary<TopicPartition, long> offsets)
    {
        lock (_gate)
        {
            var result = new Dictionary<TopicPartition, long>();
            foreach (var (topicPartition, offset) in offsets)
            {
                if (!_topics.TryGetValue(topicPartition.Topic, out var topic) ||
                    (uint)topicPartition.Partition >= (uint)topic.Partitions.Count)
                {
                    result[topicPartition] = -1;
                    continue;
                }

                var partition = topic.Partitions[topicPartition.Partition];
                var target = Math.Clamp(offset, partition.LogStartOffset, partition.HighWatermark);
                partition.Records.RemoveAll(record => record.Offset < target);
                partition.LogStartOffset = target;
                result[topicPartition] = target;
            }

            return result;
        }
    }

    internal void CreatePartitions(string topicName, int newPartitionCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(newPartitionCount, 1);

        lock (_gate)
        {
            var topic = GetTopicForRead(topicName);
            if (newPartitionCount <= topic.Partitions.Count)
                return;

            while (topic.Partitions.Count < newPartitionCount)
                topic.Partitions.Add(new PartitionState());
            OnTopologyChangedUnderLock();
        }
    }

    internal void CreatePartitions(CreatePartitionsTopic expansion, bool validateOnly)
    {
        lock (_gate)
        {
            // Validation must never auto-create a missing topic, including with AutoCreateTopics enabled.
            if (!_topics.TryGetValue(expansion.Name, out var topic))
                throw new KafkaException(ErrorCode.UnknownTopicOrPartition, $"Topic '{expansion.Name}' does not exist.");
            if (expansion.Count <= topic.Partitions.Count)
                throw new KafkaException(ErrorCode.InvalidPartitions, "The total partition count must increase.");

            if (expansion.Assignments is { } assignments)
            {
                if (assignments.Count != expansion.Count - topic.Partitions.Count)
                    throw new KafkaException(ErrorCode.InvalidReplicaAssignment, "Assignments must describe every additional partition.");
                foreach (var assignment in assignments)
                {
                    // In-memory metadata models one broker, with replication factor one.
                    if (assignment.BrokerIds.Count != 1 || assignment.BrokerIds[0] != 0)
                        throw new KafkaException(ErrorCode.InvalidReplicaAssignment, "The in-memory cluster supports only replica broker 0.");
                }
            }

            if (!validateOnly)
            {
                while (topic.Partitions.Count < expansion.Count)
                    topic.Partitions.Add(new PartitionState());
                OnTopologyChangedUnderLock();
            }
        }
    }

    internal IReadOnlyDictionary<string, TopicDescription> DescribeTopics(IEnumerable<string> topicNames)
    {
        lock (_gate)
        {
            var result = new Dictionary<string, TopicDescription>(StringComparer.Ordinal);
            foreach (var name in topicNames)
            {
                var topic = GetTopicForRead(name);
                result[name] = DescribeTopic(topic);
            }

            return result;
        }
    }

    internal IReadOnlyDictionary<Guid, TopicDescription> DescribeTopics(IEnumerable<Guid> topicIds)
    {
        lock (_gate)
        {
            var result = new Dictionary<Guid, TopicDescription>();
            foreach (var topicId in topicIds)
            {
                if (topicId == Guid.Empty)
                    throw new ArgumentException("Topic IDs cannot contain the empty UUID.", nameof(topicIds));

                if (result.ContainsKey(topicId))
                    continue;

                TopicState? found = null;
                foreach (var topic in _topics.Values)
                {
                    if (topic.TopicId == topicId)
                    {
                        found = topic;
                        break;
                    }
                }

                result[topicId] = found is not null
                    ? DescribeTopic(found)
                    : new TopicDescription
                    {
                        Name = string.Empty,
                        TopicId = topicId,
                        Partitions = [],
                        ErrorCode = ErrorCode.UnknownTopicId
                    };
            }

            return result;
        }
    }

    internal IReadOnlyList<TopicListing> TopicListings(bool includeInternal)
    {
        lock (_gate)
        {
            return _topics.Values
                .Where(topic => includeInternal || !topic.IsInternal)
                .OrderBy(topic => topic.Name, StringComparer.Ordinal)
                .Select(topic => new TopicListing
                {
                    Name = topic.Name,
                    TopicId = topic.TopicId,
                    IsInternal = topic.IsInternal
                })
                .ToArray();
        }
    }

    private TopicState GetOrAutoCreateTopic(string name)
    {
        if (_topics.TryGetValue(name, out var state))
            return state;

        if (!_options.AutoCreateTopics)
            throw new InvalidOperationException($"Topic '{name}' does not exist.");

        return EnsureTopic(name, _options.DefaultPartitionCount, configs: null);
    }

    private bool TryReadRecordUnderLock(
        TopicPartition topicPartition,
        long offset,
        IsolationLevel isolationLevel,
        out InMemoryRecord record,
        out bool blockedByOngoingTransaction)
    {
        if (!_topics.TryGetValue(topicPartition.Topic, out var topic) ||
            (uint)topicPartition.Partition >= (uint)topic.Partitions.Count)
        {
            record = null!;
            blockedByOngoingTransaction = false;
            return false;
        }

        var partition = topic.Partitions[topicPartition.Partition];
        foreach (var candidate in partition.Records)
        {
            if (isolationLevel == IsolationLevel.ReadCommitted &&
                candidate.Offset >= partition.FirstUnstableOffset)
            {
                record = null!;
                blockedByOngoingTransaction = true;
                return false;
            }
            if (candidate.Offset < offset)
                continue;
            if (isolationLevel == IsolationLevel.ReadUncommitted ||
                IsRecordVisibleUnderLock(candidate))
            {
                record = candidate;
                blockedByOngoingTransaction = false;
                return true;
            }
        }

        record = null!;
        blockedByOngoingTransaction = isolationLevel == IsolationLevel.ReadCommitted &&
            partition.FirstUnstableOffset != long.MaxValue;
        return false;
    }

    private static bool IsRecordVisibleUnderLock(InMemoryRecord record) =>
        record.Transaction is not { } transaction ||
        transaction.State == InMemoryTransactionState.Committed;

    private FatalTransactionException? ValidateConsumerGroupMetadataUnderLock(
        string groupId,
        ConsumerGroupMetadata? metadata)
    {
        if (metadata is null)
            return null;

        var generation = _consumerGroupGenerations.GetValueOrDefault(groupId);
        if (generation != metadata.GenerationId ||
            !_consumerGroupMembers.TryGetValue(groupId, out var members) ||
            !members.ContainsKey(metadata.MemberId))
        {
            return new FatalTransactionException(
                ErrorCode.IllegalGeneration,
                $"Consumer group metadata for '{groupId}' is no longer current.");
        }
        return null;
    }

    private Dictionary<string, HashSet<TopicPartition>> BuildConsumerGroupAssignments(string groupId)
    {
        var result = new Dictionary<string, HashSet<TopicPartition>>(StringComparer.Ordinal);
        if (!_consumerGroupMembers.TryGetValue(groupId, out var members))
            return result;

        foreach (var memberId in members.Keys)
            result[memberId] = [];

        var partitions = members
            .Values
            .SelectMany(static item => item.SubscribedPartitions)
            .Distinct()
            .OrderBy(static item => item.Topic, StringComparer.Ordinal)
            .ThenBy(static item => item.Partition)
            .ToArray();

        for (var i = 0; i < partitions.Length; i++)
        {
            var partition = partitions[i];
            var eligibleMembers = members
                .Where(member => member.Value.SubscribedPartitions.Contains(partition))
                .Select(static member => member.Key)
                .Order(StringComparer.Ordinal)
                .ToArray();

            if (eligibleMembers.Length == 0)
                continue;

            var owner = eligibleMembers[i % eligibleMembers.Length];
            result[owner].Add(partition);
        }

        return result;
    }

    private void CommitOffsetsUnderLock(string groupId, IEnumerable<TopicPartitionOffset> offsets)
    {
        var groupOffsets = GetOrCreateConsumerGroupOffsetsUnderLock(groupId);
        foreach (var offset in offsets)
            groupOffsets[new TopicPartition(offset.Topic, offset.Partition)] = offset;
    }

    private Dictionary<TopicPartition, TopicPartitionOffset> GetOrCreateConsumerGroupOffsetsUnderLock(
        string groupId)
    {
        if (!_consumerGroupOffsets.TryGetValue(groupId, out var groupOffsets))
        {
            groupOffsets = [];
            _consumerGroupOffsets[groupId] = groupOffsets;
        }

        return groupOffsets;
    }

    private void CommitShareOffsetsUnderLock(string groupId, IEnumerable<TopicPartitionOffset> offsets)
    {
        using var enumerator = offsets.GetEnumerator();
        if (!enumerator.MoveNext())
            return;

        if (!_shareGroupOffsets.TryGetValue(groupId, out var groupOffsets))
        {
            groupOffsets = [];
            _shareGroupOffsets[groupId] = groupOffsets;
        }

        do
        {
            var offset = enumerator.Current;
            var partition = new TopicPartition(offset.Topic, offset.Partition);
            groupOffsets[partition] = offset;
            if (_shareRecords.TryGetValue(groupId, out var records))
                records.Remove(partition);
        }
        while (enumerator.MoveNext());
    }

    private bool ContainsTopicPartitionUnderLock(TopicPartition topicPartition) =>
        GetTopicPartitionErrorUnderLock(topicPartition) == ErrorCode.None;

    private ErrorCode GetTopicPartitionErrorUnderLock(TopicPartition topicPartition)
    {
        if (!_topics.TryGetValue(topicPartition.Topic, out var topic))
            return ErrorCode.UnknownTopicId;

        return (uint)topicPartition.Partition < (uint)topic.Partitions.Count
            ? ErrorCode.None
            : ErrorCode.UnknownTopicOrPartition;
    }

    private bool RemoveConsumerGroupUnderLock(string groupId)
    {
        var existed = _consumerGroupOffsets.Remove(groupId);
        var hadGeneration = _consumerGroupGenerations.TryRemove(groupId, out _);
        var hadStreamsType = _streamsGroupIds?.Remove(groupId) == true;
        return hadGeneration || existed || hadStreamsType;
    }

    private Dictionary<long, ShareRecordState>? GetShareRecordPartition(
        string groupId,
        TopicPartition topicPartition,
        bool create)
    {
        if (!_shareRecords.TryGetValue(groupId, out var groupRecords))
        {
            if (!create)
                return null;
            groupRecords = [];
            _shareRecords.Add(groupId, groupRecords);
        }

        if (!groupRecords.TryGetValue(topicPartition, out var records))
        {
            if (!create)
                return null;
            records = [];
            groupRecords.Add(topicPartition, records);
        }
        return records;
    }

    private void AdvanceShareStartOffsetUnderLock(
        string groupId,
        TopicPartition partition,
        Dictionary<long, ShareRecordState> records)
    {
        if (!_topics.TryGetValue(partition.Topic, out var topic) ||
            (uint)partition.Partition >= (uint)topic.Partitions.Count)
            return;

        var start = GetCommittedShareOffset(groupId, partition) ?? GetWatermarks(partition).Low;
        var next = start;
        var partitionState = topic.Partitions[partition.Partition];
        var isolationLevel = GetShareIsolationUnderLock(groupId);
        var lower = 0;
        var upper = partitionState.Records.Count;
        while (lower < upper)
        {
            var middle = lower + (upper - lower) / 2;
            if (partitionState.Records[middle].Offset < start)
                lower = middle + 1;
            else
                upper = middle;
        }

        for (var index = lower; index < partitionState.Records.Count; index++)
        {
            var record = partitionState.Records[index];
            if (isolationLevel == IsolationLevel.ReadCommitted &&
                record.Offset >= partitionState.FirstUnstableOffset)
                break;
            next = record.Offset;
            if (isolationLevel == IsolationLevel.ReadUncommitted || IsRecordVisibleUnderLock(record))
            {
                if (!records.TryGetValue(next, out var state) || !state.Archived)
                    break;
                records.Remove(next);
            }
            next++;
        }

        if (next <= start)
            return;
        if (!_shareGroupOffsets.TryGetValue(groupId, out var offsets))
        {
            offsets = [];
            _shareGroupOffsets.Add(groupId, offsets);
        }
        offsets[partition] = new TopicPartitionOffset(partition.Topic, partition.Partition, next);
    }

    private static bool HasShareRecordLeases(Dictionary<TopicPartition, Dictionary<long, ShareRecordState>> partitions)
    {
        foreach (var records in partitions.Values)
        {
            foreach (var state in records.Values)
            {
                // A closed member can still have an in-flight deserializer releasing its lease.
                if (state.Registration is not null)
                    return true;
            }
        }
        return false;
    }

    private sealed class ShareRecordState
    {
        public long AcquisitionId { get; set; }
        public ShareGroupMemberRegistration? Registration { get; set; }
        public int DeliveryCount { get; set; }
        public bool Archived { get; set; }
    }

    private TopicState GetTopicForRead(string name)
    {
        if (_topics.TryGetValue(name, out var state))
            return state;

        if (!_options.AutoCreateTopics)
            throw new InvalidOperationException($"Topic '{name}' does not exist.");

        return EnsureTopic(name, _options.DefaultPartitionCount, configs: null);
    }

    private TopicState EnsureTopic(string name, int partitionCount, IReadOnlyDictionary<string, string>? configs)
    {
        if (_topics.TryGetValue(name, out var existing))
            return existing;

        var state = new TopicState(name, partitionCount, configs);
        _topics[name] = state;
        OnTopologyChangedUnderLock();
        return state;
    }

    private static PartitionState GetPartitionForRead(TopicState topic, int partition)
    {
        if ((uint)partition >= (uint)topic.Partitions.Count)
            throw new ArgumentOutOfRangeException(nameof(partition), $"Topic '{topic.Name}' has {topic.Partitions.Count} partitions.");

        return topic.Partitions[partition];
    }

    private int SelectPartition(TopicState topic, int? requestedPartition, byte[] key, bool isKeyNull)
        => SelectPartition(topic, requestedPartition, key, isKeyNull, _options.Partitioner);

    private static int SelectPartition(
        TopicState topic,
        int? requestedPartition,
        byte[] key,
        bool isKeyNull,
        IPartitioner? partitioner)
    {
        if (requestedPartition is { } partition)
        {
            if ((uint)partition >= (uint)topic.Partitions.Count)
                throw new ArgumentOutOfRangeException(nameof(requestedPartition), $"Topic '{topic.Name}' has {topic.Partitions.Count} partitions.");

            return partition;
        }

        if (partitioner is not null)
        {
            // No producer batches exist here, so sticky state cannot receive batch callbacks.
            if (partitioner is IUniformStickyPartitioner sticky && sticky.UsesStickyPartition(key, isKeyNull))
            {
                throw new NotSupportedException(
                    "In-memory producers do not support sticky partition selection. Use an explicit partition or a partitioner that does not require batch callbacks.");
            }

            var selectedByPartitioner = partitioner.Partition(topic.Name, key, isKeyNull, topic.Partitions.Count);
            if ((uint)selectedByPartitioner >= (uint)topic.Partitions.Count)
            {
                throw new InvalidOperationException(
                    $"Partitioner returned partition {selectedByPartitioner} for topic '{topic.Name}' with {topic.Partitions.Count} partitions.");
            }

            return selectedByPartitioner;
        }

        // Same mapping as the producer's DefaultPartitioner: Kafka-compatible Murmur2 for keyed
        // records. Null and empty keys use sticky partitioning there; rotate here instead.
        if (!isKeyNull && key.Length > 0)
            return Murmur2.Partition(key, topic.Partitions.Count);

        var selected = topic.NextPartition;
        topic.NextPartition = (topic.NextPartition + 1) % topic.Partitions.Count;
        return selected;
    }

    private static IReadOnlyList<Header> CopyHeaders(IReadOnlyList<Header>? headers)
        => headers is null ? Array.Empty<Header>() : LazyConsumeHeaders.CreateSnapshot(headers);

    private static InMemoryRecord CloneRecord(InMemoryRecord record, long shareAcquisitionId = 0)
    {
        return record with
        {
            ShareAcquisitionId = shareAcquisitionId,
            Key = record.Key.ToArray(),
            Value = record.Value.ToArray(),
            Headers = CopyHeaders(record.Headers)
        };
    }

    private static TopicDescription DescribeTopic(TopicState topic)
    {
        var partitions = topic.Partitions
            .Select((_, index) => new PartitionInfo
            {
                PartitionIndex = index,
                LeaderId = 0,
                LeaderEpoch = 0,
                ReplicaNodes = [0],
                IsrNodes = [0],
                OfflineReplicas = [],
                ErrorCode = ErrorCode.None
            })
            .ToArray();

        return new TopicDescription
        {
            Name = topic.Name,
            TopicId = topic.TopicId,
            IsInternal = topic.IsInternal,
            Partitions = partitions,
            ErrorCode = ErrorCode.None
        };
    }

    private static TaskCompletionSource NewRecordsChangedSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class TopicState
    {
        public TopicState(string name, int partitionCount, IReadOnlyDictionary<string, string>? configs)
        {
            Name = name;
            Configs = configs is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(configs, StringComparer.Ordinal);

            for (var i = 0; i < partitionCount; i++)
                Partitions.Add(new PartitionState());
        }

        public string Name { get; }
        public Guid TopicId { get; } = Guid.NewGuid();
        public bool IsInternal => Name.StartsWith("__", StringComparison.Ordinal);
        public Dictionary<string, string> Configs { get; }
        public List<PartitionState> Partitions { get; } = [];
        public int NextPartition { get; set; }
    }

    private sealed class PartitionState
    {
        private readonly Dictionary<InMemoryTransactionMarker, long> _transactionOffsets = [];

        public List<InMemoryRecord> Records { get; } = [];
        public long LogStartOffset { get; set; }
        public long HighWatermark { get; private set; }
        public long FirstUnstableOffset { get; private set; } = long.MaxValue;

        public void Append(InMemoryRecord record)
        {
            Records.Add(record);
            HighWatermark = record.Offset + 1;
        }

        public bool RegisterTransaction(InMemoryTransactionMarker transaction, long offset)
        {
            if (!_transactionOffsets.TryAdd(transaction, offset))
                return false;

            FirstUnstableOffset = Math.Min(FirstUnstableOffset, offset);
            return true;
        }

        public void CompleteTransaction(InMemoryTransactionMarker transaction)
        {
            if (!_transactionOffsets.Remove(transaction, out var offset))
                return;

            // A broker appends one invisible COMMIT/ABORT control record per touched partition.
            // Reserve its offset without adding a user record to the in-memory log.
            HighWatermark++;
            if (offset != FirstUnstableOffset)
                return;

            FirstUnstableOffset = long.MaxValue;
            foreach (var remainingOffset in _transactionOffsets.Values)
                FirstUnstableOffset = Math.Min(FirstUnstableOffset, remainingOffset);
        }
    }

    private readonly record struct ConsumerGroupMemberState(
        long RegistrationId,
        HashSet<TopicPartition> SubscribedPartitions)
    {
        public string? GroupInstanceId => (SubscribedPartitions as StaticMemberPartitions)?.GroupInstanceId;
    }

    // Keep dynamic registrations and dictionary entries at their original size.
    // Only static members need the additional identity reference.
    private sealed class StaticMemberPartitions(IEnumerable<TopicPartition> partitions, string groupInstanceId)
        : HashSet<TopicPartition>(partitions)
    {
        public string GroupInstanceId { get; } = groupInstanceId;
    }
}

internal sealed class ShareGroupMemberRegistration
{
    internal ShareGroupMemberRegistration(string memberId)
    {
        MemberId = memberId;
    }

    internal string MemberId { get; }
    internal bool IsActive { get; set; } = true;
}

internal readonly record struct InMemoryShareRecordCompletion(InMemoryRecord Record, AcknowledgeType Type);
