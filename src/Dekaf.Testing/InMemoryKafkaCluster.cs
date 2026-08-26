using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Testing;

internal readonly record struct InMemoryShareGroupListing(
    string GroupId,
    bool HasActiveMembers);

/// <summary>
/// Shared in-memory topic, partition, offset, and group-offset store.
/// </summary>
public sealed class InMemoryKafkaCluster
{
    private readonly object _gate = new();
    private readonly Dictionary<string, TopicState> _topics = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<TopicPartition, TopicPartitionOffset>> _consumerGroupOffsets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<TopicPartition, TopicPartitionOffset>> _shareGroupOffsets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, ConsumerGroupMemberState>> _consumerGroupMembers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _consumerGroupGenerations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, int>> _shareGroupMembers = new(StringComparer.Ordinal);
    private readonly HashSet<string> _shareGroupsWithMemberHistory = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<TopicPartition, Dictionary<long, ShareGroupMemberRegistration>>> _shareLeases = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<TopicPartition, Dictionary<long, int>>> _shareDeliveryCounts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Exception> _produceFailures = new(StringComparer.Ordinal);
    private readonly Dictionary<PreparedTransactionState, IInMemoryPreparedTransaction> _preparedTransactions = [];
    private readonly Dictionary<InMemoryTransactionMarker, List<PartitionState>> _transactionPartitions = [];
    private readonly InMemoryKafkaClusterOptions _options;
    private TaskCompletionSource _recordsChanged = NewRecordsChangedSource();
    private long[] _shareDeliveryRemovalOffsets = [];
    private long _nextProducerId;
    private long _nextConsumerGroupRegistrationId;
    private TimeSpan _produceLatency;
    private int _nextConsumerGroupGeneration;

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
            return _topics.Remove(name);
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

            return topicName is not null && _topics.Remove(topicName);
        }
    }

    public IReadOnlyList<string> ListTopics()
    {
        lock (_gate)
            return _topics.Keys.Order(StringComparer.Ordinal).ToArray();
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
        out long registrationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        ArgumentNullException.ThrowIfNull(subscribedPartitions);

        var partitions = subscribedPartitions.Distinct().ToHashSet();

        lock (_gate)
        {
            if (!_consumerGroupMembers.TryGetValue(groupId, out var members))
            {
                members = new Dictionary<string, ConsumerGroupMemberState>(StringComparer.Ordinal);
                _consumerGroupMembers[groupId] = members;
            }

            registrationId = ++_nextConsumerGroupRegistrationId;
            members[memberId] = new ConsumerGroupMemberState(registrationId, partitions);
            var generation = ++_nextConsumerGroupGeneration;
            _consumerGroupGenerations[groupId] = generation;
            return generation;
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

            _consumerGroupGenerations[groupId] = ++_nextConsumerGroupGeneration;
            if (members.Count == 0)
            {
                _consumerGroupMembers.Remove(groupId);
                _consumerGroupGenerations.TryRemove(groupId, out _);
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

    internal void CompleteTransaction(
        InMemoryTransactionMarker transactionMarker,
        bool committed,
        IEnumerable<(
            string GroupId,
            IReadOnlyList<ConsumerGroupMetadata> MetadataSnapshots,
            IReadOnlyList<TopicPartitionOffset> Offsets)> pendingOffsets,
        PreparedTransactionState preparedState,
        IInMemoryPreparedTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(pendingOffsets);
        ArgumentNullException.ThrowIfNull(transactionMarker);
        ArgumentNullException.ThrowIfNull(transaction);

        TaskCompletionSource signal;
        lock (_gate)
        {
            if (transactionMarker.State != InMemoryTransactionState.Ongoing)
                throw new InvalidOperationException("The in-memory transaction is no longer active.");

            if (committed)
            {
                foreach (var (groupId, metadataSnapshots, _) in pendingOffsets)
                {
                    for (var i = 0; i < metadataSnapshots.Count; i++)
                        ValidateConsumerGroupMetadataUnderLock(groupId, metadataSnapshots[i]);
                }

                foreach (var (groupId, _, offsets) in pendingOffsets)
                    CommitOffsetsUnderLock(groupId, offsets);
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

            signal = _recordsChanged;
        }

        signal.TrySetResult();
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

        partitionState.Records.Add(record);
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
        TryRead(topicPartition, offset, IsolationLevel.ReadCommitted, out record, out _);

    internal bool TryRead(
        TopicPartition topicPartition,
        long offset,
        IsolationLevel isolationLevel,
        out InMemoryRecord record) =>
        TryRead(topicPartition, offset, isolationLevel, out record, out _);

    internal bool TryRead(
        TopicPartition topicPartition,
        long offset,
        out InMemoryRecord record,
        out bool blockedByOngoingTransaction) =>
        TryRead(
            topicPartition,
            offset,
            IsolationLevel.ReadCommitted,
            out record,
            out blockedByOngoingTransaction);

    internal bool TryRead(
        TopicPartition topicPartition,
        long offset,
        IsolationLevel isolationLevel,
        out InMemoryRecord record,
        out bool blockedByOngoingTransaction)
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
                return false;
            }

            record = CloneRecord(candidate);
            return true;
        }
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
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);

        lock (_gate)
        {
            if (!registration.IsActive)
            {
                record = null!;
                deliveryCount = 0;
                return false;
            }

            if (!TryReadRecordUnderLock(
                    topicPartition,
                    offset,
                    IsolationLevel.ReadCommitted,
                    out var candidate,
                    out _))
            {
                record = null!;
                deliveryCount = 0;
                return false;
            }

            var partitionLeases = GetShareLeasePartition(groupId, topicPartition, create: true)!;
            var hasLease = partitionLeases.TryGetValue(candidate.Offset, out var leaseRegistration);
            if (hasLease)
            {
                var existingRegistration = leaseRegistration!;
                if (!StringComparer.Ordinal.Equals(existingRegistration.MemberId, memberId) ||
                    (!ReferenceEquals(existingRegistration, registration) &&
                     existingRegistration.IsActive))
                {
                    record = null!;
                    deliveryCount = 0;
                    return false;
                }

                if (!ReferenceEquals(existingRegistration, registration))
                {
                    partitionLeases[candidate.Offset] = registration;
                    deliveryCount = RecordShareRedeliveryUnderLock(
                        groupId,
                        topicPartition,
                        candidate.Offset);
                    record = CloneRecord(candidate);
                    return true;
                }
            }

            var partitionDeliveryCounts = GetShareDeliveryCountPartition(groupId, topicPartition, create: true)!;
            if (!hasLease)
            {
                partitionDeliveryCounts.TryGetValue(candidate.Offset, out deliveryCount);
                deliveryCount++;
                partitionDeliveryCounts[candidate.Offset] = deliveryCount;
                partitionLeases[candidate.Offset] = registration;
            }
            else
            {
                deliveryCount = partitionDeliveryCounts.GetValueOrDefault(candidate.Offset, 1);
            }

            record = CloneRecord(candidate);
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
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);

        lock (_gate)
        {
            if (!registration.IsActive)
            {
                record = null!;
                return false;
            }

            if (!TryReadRecordUnderLock(
                    topicPartition,
                    offset,
                    IsolationLevel.ReadCommitted,
                    out var candidate,
                    out _))
            {
                record = null!;
                return false;
            }

            var partitionLeases = GetShareLeasePartition(groupId, topicPartition, create: true)!;
            var hasLease = partitionLeases.TryGetValue(candidate.Offset, out var leaseRegistration);
            if (hasLease)
            {
                var existingRegistration = leaseRegistration!;
                if (!StringComparer.Ordinal.Equals(existingRegistration.MemberId, memberId) ||
                    (!ReferenceEquals(existingRegistration, registration) &&
                     existingRegistration.IsActive))
                {
                    record = null!;
                    return false;
                }
            }

            if (!hasLease || !ReferenceEquals(leaseRegistration, registration))
                partitionLeases[candidate.Offset] = registration;

            record = CloneRecord(candidate);
            return true;
        }
    }

    internal bool TryCompleteShareRecordAcquisition(
        string groupId,
        string memberId,
        ShareGroupMemberRegistration registration,
        TopicPartition topicPartition,
        long offset,
        out int deliveryCount)
    {
        lock (_gate)
        {
            if (!registration.IsActive ||
                !_shareLeases.TryGetValue(groupId, out var groupLeases) ||
                !groupLeases.TryGetValue(topicPartition, out var partitionLeases) ||
                !partitionLeases.TryGetValue(offset, out var lease) ||
                !StringComparer.Ordinal.Equals(lease.MemberId, memberId) ||
                !ReferenceEquals(lease, registration))
            {
                deliveryCount = 0;
                return false;
            }

            deliveryCount = RecordShareRedeliveryUnderLock(groupId, topicPartition, offset);
            return true;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private int RecordShareRedeliveryUnderLock(
        string groupId,
        TopicPartition topicPartition,
        long offset)
    {
        var partitionDeliveryCounts = GetShareDeliveryCountPartition(
            groupId,
            topicPartition,
            create: true)!;
        partitionDeliveryCounts.TryGetValue(offset, out var deliveryCount);
        deliveryCount++;
        partitionDeliveryCounts[offset] = deliveryCount;
        return deliveryCount;
    }

    internal void CompleteShareRecords(
        string groupId,
        string memberId,
        ShareGroupMemberRegistration registration,
        IEnumerable<TopicPartitionOffset> completedRecords,
        IEnumerable<TopicPartitionOffset> commitOffsets)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        ArgumentNullException.ThrowIfNull(completedRecords);
        ArgumentNullException.ThrowIfNull(commitOffsets);

        var completed = completedRecords.ToArray();
        var commits = commitOffsets.ToArray();

        lock (_gate)
        {
            ReleaseShareLeasesUnderLock(groupId, memberId, registration, completed);
            if (commits.Length > 0)
            {
                CommitShareOffsetsUnderLock(groupId, commits);
                foreach (var commitOffset in commits)
                    RemoveCommittedShareDeliveryCountsUnderLock(groupId, commitOffset);
            }
        }
    }

    internal void ReleaseShareRecords(
        string groupId,
        string memberId,
        ShareGroupMemberRegistration registration,
        IEnumerable<TopicPartitionOffset> records)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        ArgumentNullException.ThrowIfNull(records);

        lock (_gate)
            ReleaseShareLeasesUnderLock(groupId, memberId, registration, records);
    }

    internal void CompleteShareRecords(
        string groupId,
        string memberId,
        ShareGroupMemberRegistration registration,
        TopicPartitionOffset[] completedRecords,
        int completedRecordCount,
        TopicPartitionOffset[] commitOffsets,
        int commitOffsetCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        ArgumentNullException.ThrowIfNull(completedRecords);
        ArgumentNullException.ThrowIfNull(commitOffsets);
        ArgumentOutOfRangeException.ThrowIfNegative(completedRecordCount);
        ArgumentOutOfRangeException.ThrowIfNegative(commitOffsetCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(completedRecordCount, completedRecords.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(commitOffsetCount, commitOffsets.Length);

        lock (_gate)
        {
            ReleaseShareLeasesUnderLock(
                groupId,
                memberId,
                registration,
                completedRecords,
                completedRecordCount);
            if (commitOffsetCount == 0)
                return;

            CommitShareOffsetsUnderLock(groupId, commitOffsets, commitOffsetCount);
            for (var index = 0; index < commitOffsetCount; index++)
                RemoveCommittedShareDeliveryCountsUnderLock(groupId, commitOffsets[index]);
        }
    }

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
        long offset)
    {
        lock (_gate)
        {
            if (!_shareLeases.TryGetValue(groupId, out var groupLeases) ||
                !groupLeases.TryGetValue(topicPartition, out var partitionLeases) ||
                !partitionLeases.TryGetValue(offset, out var lease) ||
                !StringComparer.Ordinal.Equals(lease.MemberId, memberId) ||
                !ReferenceEquals(lease, registration))
            {
                return;
            }

            partitionLeases.Remove(offset);
            if (partitionLeases.Count == 0)
            {
                groupLeases.Remove(topicPartition);
                if (groupLeases.Count == 0)
                    _shareLeases.Remove(groupId);
            }
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
    {
        lock (_gate)
        {
            if (!_topics.TryGetValue(topicPartition.Topic, out var topic) ||
                (uint)topicPartition.Partition >= (uint)topic.Partitions.Count)
            {
                return new WatermarkOffsets(0, 0);
            }

            var partition = topic.Partitions[topicPartition.Partition];
            return new WatermarkOffsets(partition.LogStartOffset, partition.HighWatermark);
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
            CommitShareOffsetsUnderLock(groupId, offsets);
    }

    internal void CommitOffsets(string groupId, IReadOnlyList<TopicPartitionOffset> offsets)
    {
        lock (_gate)
        {
            var groupOffsets = GetOrCreateGroupOffsetsUnderLock(groupId);
            for (var index = 0; index < offsets.Count; index++)
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

    internal IReadOnlyDictionary<TopicPartition, TopicPartitionOffset> GetGroupOffsetDetails(string groupId)
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
            groupIds.UnionWith(_shareLeases.Keys);
            groupIds.UnionWith(_shareDeliveryCounts.Keys);
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
            var hasLeases = _shareLeases.TryGetValue(groupId, out var leases);
            var hasDeliveryCounts = _shareDeliveryCounts.ContainsKey(groupId);
            var hasMemberHistory = _shareGroupsWithMemberHistory.Contains(groupId);
            if (!hasOffsets && !hasMembers && !hasLeases && !hasDeliveryCounts && !hasMemberHistory)
                return ErrorCode.GroupIdNotFound;

            if (members is { Count: > 0 } || leases is { Count: > 0 })
                return ErrorCode.NonEmptyGroup;

            _shareGroupOffsets.Remove(groupId);
            _shareGroupMembers.Remove(groupId);
            _shareLeases.Remove(groupId);
            _shareDeliveryCounts.Remove(groupId);
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

            return RemoveConsumerGroupUnderLock(groupId)
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

    internal void DeleteShareGroupOffsets(string groupId, IEnumerable<TopicPartition> partitions)
    {
        lock (_gate)
        {
            if (!_shareGroupOffsets.TryGetValue(groupId, out var offsets))
                return;

            foreach (var partition in partitions)
                offsets.Remove(partition);

            if (offsets.Count == 0)
                _shareGroupOffsets.Remove(groupId);
        }
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

    private void ValidateConsumerGroupMetadataUnderLock(
        string groupId,
        ConsumerGroupMetadata? metadata)
    {
        if (metadata is null)
            return;

        var generation = _consumerGroupGenerations.GetValueOrDefault(groupId);
        if (generation != metadata.GenerationId ||
            !_consumerGroupMembers.TryGetValue(groupId, out var members) ||
            !members.ContainsKey(metadata.MemberId))
        {
            throw new FatalTransactionException(
                ErrorCode.IllegalGeneration,
                $"Consumer group metadata for '{groupId}' is no longer current.");
        }
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
            groupOffsets[new TopicPartition(offset.Topic, offset.Partition)] = offset;
        }
        while (enumerator.MoveNext());
    }

    private void CommitShareOffsetsUnderLock(
        string groupId,
        TopicPartitionOffset[] offsets,
        int offsetCount)
    {
        if (!_shareGroupOffsets.TryGetValue(groupId, out var groupOffsets))
        {
            groupOffsets = [];
            _shareGroupOffsets[groupId] = groupOffsets;
        }

        for (var index = 0; index < offsetCount; index++)
        {
            var offset = offsets[index];
            groupOffsets[new TopicPartition(offset.Topic, offset.Partition)] = offset;
        }
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
        return _consumerGroupGenerations.Remove(groupId) || existed;
    }

    private Dictionary<long, ShareGroupMemberRegistration>? GetShareLeasePartition(
        string groupId,
        TopicPartition topicPartition,
        bool create)
    {
        if (!_shareLeases.TryGetValue(groupId, out var groupLeases))
        {
            if (!create)
                return null;

            groupLeases = [];
            _shareLeases[groupId] = groupLeases;
        }

        if (!groupLeases.TryGetValue(topicPartition, out var partitionLeases))
        {
            if (!create)
                return null;

            partitionLeases = [];
            groupLeases[topicPartition] = partitionLeases;
        }

        return partitionLeases;
    }

    private Dictionary<long, int>? GetShareDeliveryCountPartition(
        string groupId,
        TopicPartition topicPartition,
        bool create)
    {
        if (!_shareDeliveryCounts.TryGetValue(groupId, out var groupCounts))
        {
            if (!create)
                return null;

            groupCounts = [];
            _shareDeliveryCounts[groupId] = groupCounts;
        }

        if (!groupCounts.TryGetValue(topicPartition, out var partitionCounts))
        {
            if (!create)
                return null;

            partitionCounts = [];
            groupCounts[topicPartition] = partitionCounts;
        }

        return partitionCounts;
    }

    private void ReleaseShareLeasesUnderLock(
        string groupId,
        string memberId,
        ShareGroupMemberRegistration registration,
        IEnumerable<TopicPartitionOffset> records)
    {
        if (!_shareLeases.TryGetValue(groupId, out var groupLeases))
            return;

        foreach (var record in records)
        {
            var topicPartition = new TopicPartition(record.Topic, record.Partition);
            if (!groupLeases.TryGetValue(topicPartition, out var partitionLeases) ||
                !partitionLeases.TryGetValue(record.Offset, out var lease) ||
                !StringComparer.Ordinal.Equals(lease.MemberId, memberId) ||
                !ReferenceEquals(lease, registration))
            {
                continue;
            }

            partitionLeases.Remove(record.Offset);
            if (partitionLeases.Count == 0)
                groupLeases.Remove(topicPartition);
        }

        if (groupLeases.Count == 0)
            _shareLeases.Remove(groupId);
    }

    private void ReleaseShareLeasesUnderLock(
        string groupId,
        string memberId,
        ShareGroupMemberRegistration registration,
        TopicPartitionOffset[] records,
        int recordCount)
    {
        if (!_shareLeases.TryGetValue(groupId, out var groupLeases))
            return;

        for (var index = 0; index < recordCount; index++)
        {
            var record = records[index];
            var topicPartition = new TopicPartition(record.Topic, record.Partition);
            if (!groupLeases.TryGetValue(topicPartition, out var partitionLeases) ||
                !partitionLeases.TryGetValue(record.Offset, out var lease) ||
                !StringComparer.Ordinal.Equals(lease.MemberId, memberId) ||
                !ReferenceEquals(lease, registration))
            {
                continue;
            }

            partitionLeases.Remove(record.Offset);
            if (partitionLeases.Count == 0)
                groupLeases.Remove(topicPartition);
        }

        if (groupLeases.Count == 0)
            _shareLeases.Remove(groupId);
    }

    private void RemoveCommittedShareDeliveryCountsUnderLock(string groupId, TopicPartitionOffset commitOffset)
    {
        var topicPartition = new TopicPartition(commitOffset.Topic, commitOffset.Partition);
        var partitionCounts = GetShareDeliveryCountPartition(groupId, topicPartition, create: false);
        if (partitionCounts is null)
            return;

        if (_shareDeliveryRemovalOffsets.Length < partitionCounts.Count)
            Array.Resize(ref _shareDeliveryRemovalOffsets, partitionCounts.Count);

        var removalCount = 0;
        foreach (var pair in partitionCounts)
        {
            if (pair.Key < commitOffset.Offset)
                _shareDeliveryRemovalOffsets[removalCount++] = pair.Key;
        }

        for (var index = 0; index < removalCount; index++)
            partitionCounts.Remove(_shareDeliveryRemovalOffsets[index]);

        if (partitionCounts.Count == 0 &&
            _shareDeliveryCounts.TryGetValue(groupId, out var groupCounts))
        {
            groupCounts.Remove(topicPartition);
            if (groupCounts.Count == 0)
                _shareDeliveryCounts.Remove(groupId);
        }
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
        return state;
    }

    private static PartitionState GetPartitionForRead(TopicState topic, int partition)
    {
        if ((uint)partition >= (uint)topic.Partitions.Count)
            throw new ArgumentOutOfRangeException(nameof(partition), $"Topic '{topic.Name}' has {topic.Partitions.Count} partitions.");

        return topic.Partitions[partition];
    }

    private static int SelectPartition(TopicState topic, int? requestedPartition, byte[] key, bool isKeyNull)
    {
        if (requestedPartition is { } partition)
        {
            if ((uint)partition >= (uint)topic.Partitions.Count)
                throw new ArgumentOutOfRangeException(nameof(requestedPartition), $"Topic '{topic.Name}' has {topic.Partitions.Count} partitions.");

            return partition;
        }

        if (!isKeyNull && key.Length > 0)
            return (int)(Fnv1A(key) % (uint)topic.Partitions.Count);

        var selected = topic.NextPartition;
        topic.NextPartition = (topic.NextPartition + 1) % topic.Partitions.Count;
        return selected;
    }

    private static uint Fnv1A(ReadOnlySpan<byte> bytes)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;

        var hash = offset;
        foreach (var value in bytes)
        {
            hash ^= value;
            hash *= prime;
        }

        return hash;
    }

    private static IReadOnlyList<Header> CopyHeaders(IReadOnlyList<Header>? headers)
    {
        if (headers is null || headers.Count == 0)
            return Array.Empty<Header>();

        var copy = new Header[headers.Count];
        for (var i = 0; i < headers.Count; i++)
        {
            var header = headers[i];
            copy[i] = new Header(header.Key, header.IsValueNull ? null : header.Value.ToArray());
        }

        return copy;
    }

    private static InMemoryRecord CloneRecord(InMemoryRecord record)
    {
        return record with
        {
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
        public long HighWatermark => Records.Count == 0 ? LogStartOffset : Records[^1].Offset + 1;
        public long FirstUnstableOffset { get; private set; } = long.MaxValue;

        public bool RegisterTransaction(InMemoryTransactionMarker transaction, long offset)
        {
            if (!_transactionOffsets.TryAdd(transaction, offset))
                return false;

            FirstUnstableOffset = Math.Min(FirstUnstableOffset, offset);
            return true;
        }

        public void CompleteTransaction(InMemoryTransactionMarker transaction)
        {
            if (!_transactionOffsets.Remove(transaction, out var offset) ||
                offset != FirstUnstableOffset)
            {
                return;
            }

            FirstUnstableOffset = long.MaxValue;
            foreach (var remainingOffset in _transactionOffsets.Values)
                FirstUnstableOffset = Math.Min(FirstUnstableOffset, remainingOffset);
        }
    }

    private readonly record struct ConsumerGroupMemberState(
        long RegistrationId,
        HashSet<TopicPartition> SubscribedPartitions);
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
