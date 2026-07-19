#if NETSTANDARD2_0
using StringSet = System.Collections.Generic.IReadOnlyCollection<string>;
using TopicPartitionSet = System.Collections.Generic.IReadOnlyCollection<Dekaf.TopicPartition>;
#else
using StringSet = System.Collections.Generic.IReadOnlySet<string>;
using TopicPartitionSet = System.Collections.Generic.IReadOnlySet<Dekaf.TopicPartition>;
#endif

namespace Dekaf.Consumer;

internal sealed class RebalanceConsumerScope<TKey, TValue> : IRebalanceConsumerScope
{
    private IKafkaConsumer<TKey, TValue>? _consumer;
    private readonly TopicPartitionSet _assignment;
    private readonly HashSet<TopicPartition> _newlyAssigned;
    private readonly Action<TopicPartitionOffset> _stageSeek;
    private readonly Func<TopicPartition, long?> _getPosition;

    internal RebalanceConsumerScope(
        IKafkaConsumer<TKey, TValue> consumer,
        IEnumerable<TopicPartition> assignment,
        IEnumerable<TopicPartition> newlyAssigned,
        Action<TopicPartitionOffset>? stageSeek = null,
        Func<TopicPartition, long?>? getPosition = null)
    {
        _consumer = consumer;
        _assignment = assignment.ToHashSet();
        _newlyAssigned = newlyAssigned.ToHashSet();
        _stageSeek = stageSeek ?? consumer.Positions.Seek;
        _getPosition = getPosition ?? consumer.Positions.GetPosition;
    }

    public StringSet Subscription => GetConsumer().Subscription;
    public string? SubscriptionPattern => GetConsumer().SubscriptionPattern;
    public TopicPartitionSet Assignment
    {
        get
        {
            _ = GetConsumer();
            return _assignment;
        }
    }

    public TopicPartitionSet Paused => GetConsumer().Paused;
    public string? MemberId => GetConsumer().MemberId;
    public ConsumerGroupMetadata? ConsumerGroupMetadata => GetConsumer().ConsumerGroupMetadata;

    public ValueTask CommitAsync(CancellationToken cancellationToken = default) =>
        GetConsumer().CommitAsync(cancellationToken);

    public ValueTask CommitAsync(
        IEnumerable<TopicPartitionOffset> offsets,
        CancellationToken cancellationToken = default) =>
        GetConsumer().CommitAsync(offsets, cancellationToken);

    public void StoreOffset(TopicPartitionOffset offset) => GetConsumer().StoreOffset(offset);

    public ValueTask<long?> GetCommittedOffsetAsync(
        TopicPartition partition,
        CancellationToken cancellationToken = default) =>
        GetConsumer().Positions.GetCommittedOffsetAsync(partition, cancellationToken);

    public long? GetPosition(TopicPartition partition) =>
        GetPositionCore(partition);

    public void Seek(TopicPartitionOffset offset)
    {
        var consumer = GetConsumer();
        if (_newlyAssigned.Contains(new TopicPartition(offset.Topic, offset.Partition)))
            _stageSeek(offset);
        else
            consumer.Positions.Seek(offset);
    }

    public void SeekToBeginning(params TopicPartition[] partitions) =>
        SeekPartitions(partitions, offset: 0);

    public void SeekToEnd(params TopicPartition[] partitions) =>
        SeekPartitions(partitions, offset: -1);

    public void Pause(params TopicPartition[] partitions) => GetConsumer().Pause(partitions);
    public void Resume(params TopicPartition[] partitions) => GetConsumer().Resume(partitions);

    public ValueTask<IReadOnlyDictionary<TopicPartition, long>> GetOffsetsForTimesAsync(
        IEnumerable<TopicPartitionTimestamp> timestampsToSearch,
        CancellationToken cancellationToken = default) =>
        GetConsumer().Offsets.GetOffsetsForTimesAsync(timestampsToSearch, cancellationToken);

    public WatermarkOffsets? GetWatermarkOffsets(TopicPartition topicPartition) =>
        GetConsumer().Offsets.GetWatermarkOffsets(topicPartition);

    public ValueTask<WatermarkOffsets> QueryWatermarkOffsetsAsync(
        TopicPartition topicPartition,
        CancellationToken cancellationToken = default) =>
        GetConsumer().Offsets.QueryWatermarkOffsetsAsync(topicPartition, cancellationToken);

    public void Invalidate() => Interlocked.Exchange(ref _consumer, null);

    private IKafkaConsumer<TKey, TValue> GetConsumer() =>
        Volatile.Read(ref _consumer) ?? throw new InvalidOperationException(
            "The rebalance consumer view is no longer valid because its callback has completed.");

    private long? GetPositionCore(TopicPartition partition)
    {
        _ = GetConsumer();
        return _getPosition(partition);
    }

    private void SeekPartitions(IEnumerable<TopicPartition> partitions, long offset)
    {
        var consumer = GetConsumer();
        foreach (var partition in partitions)
        {
            var topicPartitionOffset = new TopicPartitionOffset(
                partition.Topic,
                partition.Partition,
                offset);
            if (_newlyAssigned.Contains(partition))
                _stageSeek(topicPartitionOffset);
            else
                consumer.Positions.Seek(topicPartitionOffset);
        }
    }
}
