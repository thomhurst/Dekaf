using Dekaf.Admin;
using Dekaf.Metadata;
using Dekaf.Protocol;
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
    private readonly Dictionary<string, Dictionary<TopicPartition, long>> _consumerGroupOffsets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, HashSet<TopicPartition>>> _consumerGroupMembers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<TopicPartition, Dictionary<long, ShareLeaseState>>> _shareLeases = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<TopicPartition, Dictionary<long, int>>> _shareDeliveryCounts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Exception> _produceFailures = new(StringComparer.Ordinal);
    private readonly InMemoryKafkaClusterOptions _options;
    private TaskCompletionSource _recordsChanged = NewRecordsChangedSource();
    private TimeSpan _produceLatency;

    public InMemoryKafkaCluster()
        : this(new InMemoryKafkaClusterOptions())
    {
    }

    public InMemoryKafkaCluster(InMemoryKafkaClusterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.DefaultPartitionCount, 1);
        _options = options;
    }

    public InMemoryKafkaClusterOptions Options => _options;

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

    internal void RegisterConsumerGroupMember(
        string groupId,
        string memberId,
        IEnumerable<TopicPartition> subscribedPartitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        ArgumentNullException.ThrowIfNull(subscribedPartitions);

        var partitions = subscribedPartitions.Distinct().ToHashSet();

        lock (_gate)
        {
            if (!_consumerGroupMembers.TryGetValue(groupId, out var members))
            {
                members = new Dictionary<string, HashSet<TopicPartition>>(StringComparer.Ordinal);
                _consumerGroupMembers[groupId] = members;
            }

            members[memberId] = partitions;
        }
    }

    internal void UnregisterConsumerGroupMember(string groupId, string memberId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);

        lock (_gate)
        {
            if (!_consumerGroupMembers.TryGetValue(groupId, out var members))
                return;

            members.Remove(memberId);
            if (members.Count == 0)
                _consumerGroupMembers.Remove(groupId);
        }
    }

    internal IReadOnlySet<TopicPartition> GetConsumerGroupAssignment(string groupId, string memberId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);

        lock (_gate)
        {
            var assignments = BuildConsumerGroupAssignments(groupId);
            return assignments.TryGetValue(memberId, out var partitions)
                ? partitions.ToHashSet()
                : new HashSet<TopicPartition>();
        }
    }

    public IReadOnlyList<InMemoryRecord> ReadRecords(string topic, int partition = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentOutOfRangeException.ThrowIfLessThan(partition, 0);

        lock (_gate)
        {
            var state = GetTopicForRead(topic);
            var partitionState = GetPartitionForRead(state, partition);
            return partitionState.Records.Select(CloneRecord).ToArray();
        }
    }

    internal async ValueTask<RecordMetadata> AppendAsync(
        string topic,
        int? partition,
        byte[] key,
        bool isKeyNull,
        byte[] value,
        bool isValueNull,
        IReadOnlyList<Header>? headers,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        var latency = GetProduceLatency(topic, out var failure);
        if (latency > TimeSpan.Zero)
            await Task.Delay(latency, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (failure is not null)
            throw failure;

        TaskCompletionSource signal;
        RecordMetadata metadata;
        lock (_gate)
        {
            var state = GetOrAutoCreateTopic(topic);
            var selectedPartition = SelectPartition(state, partition, key, isKeyNull);
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
                TimestampMs = timestampMs
            };

            partitionState.Records.Add(record);

            metadata = new RecordMetadata
            {
                Topic = topic,
                Partition = selectedPartition,
                Offset = offset,
                Timestamp = timestamp,
                KeySize = isKeyNull ? 0 : key.Length,
                ValueSize = isValueNull ? 0 : value.Length
            };

            signal = _recordsChanged;
        }

        signal.TrySetResult();
        return metadata;
    }

    internal bool TryRead(TopicPartition topicPartition, long offset, out InMemoryRecord record)
    {
        lock (_gate)
        {
            if (!TryReadRecordUnderLock(topicPartition, offset, out var candidate))
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
            if (!TryReadRecordUnderLock(topicPartition, offset, out var candidate))
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
                ? offset
                : null;
        }
    }

    internal void CommitOffsets(string groupId, IEnumerable<TopicPartitionOffset> offsets)
    {
        lock (_gate)
            CommitOffsetsUnderLock(groupId, offsets);
    }

    internal IReadOnlyDictionary<TopicPartition, long> GetGroupOffsets(string groupId)
    {
        lock (_gate)
        {
            return _consumerGroupOffsets.TryGetValue(groupId, out var offsets)
                ? new Dictionary<TopicPartition, long>(offsets)
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

    private TimeSpan GetProduceLatency(string topic, out Exception? failure)
    {
        lock (_gate)
        {
            failure = _produceFailures.GetValueOrDefault(topic);
            return _produceLatency;
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

    private bool TryReadRecordUnderLock(TopicPartition topicPartition, long offset, out InMemoryRecord record)
    {
        if (!_topics.TryGetValue(topicPartition.Topic, out var topic) ||
            (uint)topicPartition.Partition >= (uint)topic.Partitions.Count)
        {
            record = null!;
            return false;
        }

        var partition = topic.Partitions[topicPartition.Partition];
        foreach (var candidate in partition.Records)
        {
            if (candidate.Offset >= offset)
            {
                record = candidate;
                return true;
            }
        }

        record = null!;
        return false;
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
            .SelectMany(static item => item)
            .Distinct()
            .OrderBy(static item => item.Topic, StringComparer.Ordinal)
            .ThenBy(static item => item.Partition)
            .ToArray();

        for (var i = 0; i < partitions.Length; i++)
        {
            var partition = partitions[i];
            var eligibleMembers = members
                .Where(member => member.Value.Contains(partition))
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
        if (!_consumerGroupOffsets.TryGetValue(groupId, out var groupOffsets))
        {
            groupOffsets = [];
            _consumerGroupOffsets[groupId] = groupOffsets;
        }

        foreach (var offset in offsets)
            groupOffsets[new TopicPartition(offset.Topic, offset.Partition)] = offset.Offset;
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

    private static IReadOnlyList<Header>? CopyHeaders(IReadOnlyList<Header>? headers)
    {
        if (headers is null || headers.Count == 0)
            return null;

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
        public List<InMemoryRecord> Records { get; } = [];
        public long LogStartOffset { get; set; }
        public long HighWatermark => Records.Count == 0 ? LogStartOffset : Records[^1].Offset + 1;
    }

    private sealed class ShareLeaseState
    {
        public ShareLeaseState(string memberId)
        {
            MemberId = memberId;
        }

        public string MemberId { get; }
    }
}
