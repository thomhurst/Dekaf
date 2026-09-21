using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Consumer;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures the two sides of FetchRequest.LastFetchedEpoch tracking while the prefetch position
/// runs ahead of the consumed one: recording the epoch when a partition response is published,
/// and resolving it when the next request is built. Both run once per partition per fetch,
/// never per consumed message, and must not allocate.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class FetchedEpochTrackingBenchmarks
{
    private const int PartitionCount = 100;
    private const string Topic = "fetched-epoch-tracking";

    private readonly ConcurrentDictionary<TopicPartition, long> _fetchPositions = new();
    private readonly ConcurrentDictionary<TopicPartition, int> _lastConsumedLeaderEpochs = new();
    private readonly ConcurrentDictionary<TopicPartition, long> _lastFetchedLeaderEpochs = new();
    private readonly Dictionary<string, List<(FetchRequestPartition Partition, TopicPartition TopicPartition)>>
        _templates = [];
    private readonly TopicPartition[] _partitions = new TopicPartition[PartitionCount];

    private KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> _consumer = null!;
    private int _fetchBufferEpoch;
    private long _nextOffset;

    [GlobalSetup]
    public void Setup()
    {
        var templates = new List<(FetchRequestPartition, TopicPartition)>(PartitionCount);
        var assignment = new TopicPartitionOffset[PartitionCount];
        for (var partition = 0; partition < PartitionCount; partition++)
        {
            var topicPartition = new TopicPartition(Topic, partition);
            _partitions[partition] = topicPartition;
            assignment[partition] = new TopicPartitionOffset(Topic, partition, 0, leaderEpoch: 5);

            // The consumed position is still in epoch 5; the prefetch reached epoch 6.
            _fetchPositions[topicPartition] = 1_000 + partition;
            _lastConsumedLeaderEpochs[topicPartition] = 5;
            _lastFetchedLeaderEpochs[topicPartition] =
                KafkaConsumer<string, string>.PackFetchedLeaderEpoch(1_000 + partition, 6);
            templates.Add((
                new FetchRequestPartition
                {
                    Partition = partition,
                    FetchOffset = 0,
                    PartitionMaxBytes = 1024 * 1024
                },
                topicPartition));
        }

        _templates.Add(Topic, templates);

        _consumer = new KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                OffsetCommitMode = OffsetCommitMode.Manual,
            },
            Serializers.RawBytes,
            Serializers.RawBytes);
        _consumer.IncrementalAssign(assignment);
        _fetchBufferEpoch = (int)BufferedConsumerHarness.GetPrivateField(_consumer, "_fetchBufferEpoch")!;
    }

    [GlobalCleanup]
    public async Task Cleanup() => await _consumer.DisposeAsync().ConfigureAwait(false);

    [Benchmark(OperationsPerInvoke = PartitionCount)]
    public int BuildFetchRequestAheadOfConsumedPosition()
    {
        var topics = KafkaConsumer<string, string>.BuildFetchResult(
            _templates,
            _fetchPositions,
            lastConsumedLeaderEpochs: _lastConsumedLeaderEpochs,
            lastFetchedLeaderEpochs: _lastFetchedLeaderEpochs);
        try
        {
            return topics[0].Partitions.Count;
        }
        finally
        {
            ConsumerFetchPools.ReturnFetchRequestTopics(topics);
        }
    }

    [Benchmark(OperationsPerInvoke = PartitionCount)]
    public long AdvancePrefetchPositions()
    {
        // One published response per partition: the position and its epoch move together.
        var nextOffset = ++_nextOffset;
        for (var partition = 0; partition < PartitionCount; partition++)
            _consumer.UpdateFetchPositionsFromPrefetch(_partitions[partition], nextOffset, 6, _fetchBufferEpoch);

        return nextOffset;
    }
}
