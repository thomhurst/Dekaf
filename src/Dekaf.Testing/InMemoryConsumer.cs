using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Dekaf.Consumer;
using Dekaf.Producer;
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
    IConsumerOffsets
{
    private readonly object _gate = new();
    private readonly InMemoryKafkaCluster _cluster;
    private readonly IDeserializer<TKey> _keyDeserializer;
    private readonly IDeserializer<TValue> _valueDeserializer;
    private readonly InMemoryConsumerOptions _options;
    private readonly HashSet<string> _subscription = new(StringComparer.Ordinal);
    private readonly HashSet<TopicPartition> _assignment = [];
    private readonly HashSet<TopicPartition> _paused = [];
    private readonly Dictionary<TopicPartition, long> _positions = [];
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
    {
        _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
        _keyDeserializer = keyDeserializer ?? throw new ArgumentNullException(nameof(keyDeserializer));
        _valueDeserializer = valueDeserializer ?? throw new ArgumentNullException(nameof(valueDeserializer));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _memberId = options.GroupId is null ? null : options.MemberId ?? Guid.NewGuid().ToString("N");
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

    public ConsumerGroupMetadata? ConsumerGroupMetadata => _options.GroupId is null || _memberId is null
        ? null
        : new ConsumerGroupMetadata
        {
            GroupId = _options.GroupId,
            GenerationId = 1,
            MemberId = _memberId
        };

    public IConsumerPositions Positions => this;

    public IConsumerPartitions Partitions => this;

    public IConsumerOffsets Offsets => this;

    IReadOnlySet<TopicPartition> IConsumerPartitions.Assignment => Assignment;

    IReadOnlySet<TopicPartition> IConsumerPartitions.Paused => Paused;

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

        if (string.IsNullOrWhiteSpace(_options.GroupId))
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
                yield return result.Value;
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

            if (TryConsumeOne(out var result))
                return result;

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
            CommitCurrentOffsets();

        return ValueTask.CompletedTask;
    }

    public ValueTask CommitAsync(
        IEnumerable<TopicPartitionOffset> offsets,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(offsets);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        if (_options.GroupId is not null)
            _cluster.CommitOffsets(_options.GroupId, offsets);

        return ValueTask.CompletedTask;
    }

    public ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_disposed)
            return ValueTask.CompletedTask;

        lock (_gate)
        {
            if (_disposed)
                return ValueTask.CompletedTask;

            if (_options.OffsetCommitMode == OffsetCommitMode.Auto)
                CommitCurrentOffsets();

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

        var offset = _options.GroupId is null
            ? null
            : _cluster.GetCommittedOffset(_options.GroupId, partition);

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
            _positions[new TopicPartition(offset.Topic, offset.Partition)] = offset.Offset;
    }

    public void SeekToBeginning(params TopicPartition[] partitions)
    {
        ArgumentNullException.ThrowIfNull(partitions);
        ThrowIfDisposed();

        lock (_gate)
        {
            foreach (var partition in SelectTargetPartitions(partitions))
                _positions[partition] = _cluster.GetWatermarks(partition).Low;
        }
    }

    public void SeekToEnd(params TopicPartition[] partitions)
    {
        ArgumentNullException.ThrowIfNull(partitions);
        ThrowIfDisposed();

        lock (_gate)
        {
            foreach (var partition in SelectTargetPartitions(partitions))
                _positions[partition] = _cluster.GetWatermarks(partition).High;
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
        InMemoryRecord? selected = null;
        TopicPartition selectedPartition = default;

        lock (_gate)
        {
            foreach (var partition in GetCurrentAssignmentUnderLock().OrderBy(item => item.Topic, StringComparer.Ordinal).ThenBy(item => item.Partition))
            {
                if (_paused.Contains(partition))
                    continue;

                if (!_positions.TryGetValue(partition, out var position))
                    continue;

                if (!_cluster.TryRead(partition, position, out var record))
                    continue;

                selected = record;
                selectedPartition = partition;
                _positions[partition] = record.Offset + 1;
                if (_options.OffsetCommitMode == OffsetCommitMode.Auto)
                    CommitCurrentOffsets();
                break;
            }
        }

        if (selected is null)
        {
            result = default;
            return false;
        }

        result = ToConsumeResult(selectedPartition, selected);
        return true;
    }

    private ConsumeResult<TKey, TValue> ToConsumeResult(TopicPartition topicPartition, InMemoryRecord record)
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

    private void ReplaceAssignment(IEnumerable<TopicPartition> partitions)
    {
        var nextPositions = new Dictionary<TopicPartition, long>();
        foreach (var partition in partitions.Distinct())
            nextPositions[partition] = GetStartOffset(partition);

        _assignment.Clear();
        _paused.Clear();
        _positions.Clear();

        foreach (var (partition, position) in nextPositions)
        {
            _assignment.Add(partition);
            _positions[partition] = position;
        }
    }

    private long GetStartOffset(TopicPartition partition)
    {
        if (_options.GroupId is not null &&
            _cluster.GetCommittedOffset(_options.GroupId, partition) is { } committed)
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

    private void CommitCurrentOffsets()
    {
        if (_options.GroupId is null)
            return;

        var assignment = GetCurrentAssignmentUnderLock();
        var offsets = _positions
            .Where(item => assignment.Contains(item.Key))
            .Select(item => new TopicPartitionOffset(
                item.Key.Topic,
                item.Key.Partition,
                item.Value))
            .ToArray();

        if (offsets.Length > 0)
            _cluster.CommitOffsets(_options.GroupId, offsets);
    }

    private IReadOnlySet<TopicPartition> GetCurrentAssignmentUnderLock()
    {
        if (_options.GroupId is null || _memberId is null)
            return _assignment;

        var owned = _cluster.GetConsumerGroupAssignment(_options.GroupId, _memberId);
        return owned.Where(_assignment.Contains).ToHashSet();
    }

    private void RegisterConsumerGroupMemberUnderLock()
    {
        if (_options.GroupId is null || _memberId is null)
            return;

        _cluster.RegisterConsumerGroupMember(_options.GroupId, _memberId, _assignment);
    }

    private void UnregisterConsumerGroupMemberUnderLock()
    {
        if (_options.GroupId is null || _memberId is null)
            return;

        _cluster.UnregisterConsumerGroupMember(_options.GroupId, _memberId);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
