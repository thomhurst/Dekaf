using System.Collections.Concurrent;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Testing;

/// <summary>
/// Shared in-memory topic, partition, offset, and group-offset store.
/// </summary>
public sealed class InMemoryKafkaCluster
{
    private readonly object _gate = new();
    private readonly Dictionary<string, TopicState> _topics = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<TopicPartition, TopicPartitionOffset>> _consumerGroupOffsets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, ConsumerGroupMemberState>> _consumerGroupMembers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _consumerGroupGenerations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<TopicPartition, Dictionary<long, ShareLeaseState>>> _shareLeases = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<TopicPartition, Dictionary<long, int>>> _shareDeliveryCounts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Exception> _produceFailures = new(StringComparer.Ordinal);
    private readonly Dictionary<PreparedTransactionState, IInMemoryPreparedTransaction> _preparedTransactions = [];
    private readonly Dictionary<InMemoryTransactionMarker, List<PartitionState>> _transactionPartitions = [];
    private readonly InMemoryKafkaClusterOptions _options;
    private TaskCompletionSource _recordsChanged = NewRecordsChangedSource();
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

    internal bool ContainsTopicPartition(TopicPartition topicPartition)
    {
        lock (_gate)
        {
            return _topics.TryGetValue(topicPartition.Topic, out var topic)
                && (uint)topicPartition.Partition < (uint)topic.Partitions.Count;
        }
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
        TopicPartition topicPartition,
        long offset,
        out InMemoryRecord record,
        out int deliveryCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);

        lock (_gate)
        {
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
            if (partitionLeases.TryGetValue(candidate.Offset, out var lease) &&
                !StringComparer.Ordinal.Equals(lease.MemberId, memberId))
            {
                record = null!;
                deliveryCount = 0;
                return false;
            }

            var partitionDeliveryCounts = GetShareDeliveryCountPartition(groupId, topicPartition, create: true)!;
            if (!partitionLeases.ContainsKey(candidate.Offset))
            {
                partitionDeliveryCounts.TryGetValue(candidate.Offset, out deliveryCount);
                deliveryCount++;
                partitionDeliveryCounts[candidate.Offset] = deliveryCount;
                partitionLeases[candidate.Offset] = new ShareLeaseState(memberId);
            }
            else
            {
                deliveryCount = partitionDeliveryCounts.GetValueOrDefault(candidate.Offset, 1);
            }

            record = CloneRecord(candidate);
            return true;
        }
    }

    internal void CompleteShareRecords(
        string groupId,
        string memberId,
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
            ReleaseShareLeasesUnderLock(groupId, memberId, completed);
            if (commits.Length > 0)
            {
                CommitOffsetsUnderLock(groupId, commits);
                foreach (var commitOffset in commits)
                    RemoveCommittedShareDeliveryCountsUnderLock(groupId, commitOffset);
            }
        }
    }

    internal void ReleaseShareRecords(
        string groupId,
        string memberId,
        IEnumerable<TopicPartitionOffset> records)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        ArgumentNullException.ThrowIfNull(records);

        lock (_gate)
            ReleaseShareLeasesUnderLock(groupId, memberId, records);
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

    internal IReadOnlyList<string> ListGroups()
    {
        lock (_gate)
            return _consumerGroupOffsets.Keys.Order(StringComparer.Ordinal).ToArray();
    }

    internal void DeleteGroup(string groupId)
    {
        lock (_gate)
            _consumerGroupOffsets.Remove(groupId);
    }

    internal void DeleteGroupOffsets(string groupId, IEnumerable<TopicPartition> partitions)
    {
        lock (_gate)
        {
            if (!_consumerGroupOffsets.TryGetValue(groupId, out var offsets))
                return;

            foreach (var partition in partitions)
                offsets.Remove(partition);
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
        var groupOffsets = GetOrCreateGroupOffsetsUnderLock(groupId);

        foreach (var offset in offsets)
            groupOffsets[new TopicPartition(offset.Topic, offset.Partition)] = offset;
    }

    private Dictionary<TopicPartition, TopicPartitionOffset> GetOrCreateGroupOffsetsUnderLock(string groupId)
    {
        if (_consumerGroupOffsets.TryGetValue(groupId, out var groupOffsets))
            return groupOffsets;

        groupOffsets = [];
        _consumerGroupOffsets[groupId] = groupOffsets;
        return groupOffsets;
    }

    private Dictionary<long, ShareLeaseState>? GetShareLeasePartition(
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
        IEnumerable<TopicPartitionOffset> records)
    {
        if (!_shareLeases.TryGetValue(groupId, out var groupLeases))
            return;

        foreach (var record in records)
        {
            var topicPartition = new TopicPartition(record.Topic, record.Partition);
            if (!groupLeases.TryGetValue(topicPartition, out var partitionLeases) ||
                !partitionLeases.TryGetValue(record.Offset, out var lease) ||
                !StringComparer.Ordinal.Equals(lease.MemberId, memberId))
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

        foreach (var offset in partitionCounts.Keys.Where(offset => offset < commitOffset.Offset).ToArray())
            partitionCounts.Remove(offset);

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

    private sealed class ShareLeaseState
    {
        public ShareLeaseState(string memberId)
        {
            MemberId = memberId;
        }

        public string MemberId { get; }
    }

    private readonly record struct ConsumerGroupMemberState(
        long RegistrationId,
        HashSet<TopicPartition> SubscribedPartitions);
}
