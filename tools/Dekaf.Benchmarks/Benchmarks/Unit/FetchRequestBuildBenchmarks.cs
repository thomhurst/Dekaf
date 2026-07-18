using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures the per-fetch cost of materializing request partitions from a cached template.
/// This path runs once per broker fetch, not once per consumed message.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class FetchRequestBuildBenchmarks
{
    private const int PartitionCount = 100;

    private readonly ConcurrentDictionary<TopicPartition, long> _fetchPositions = new();
    private readonly Dictionary<string, List<(FetchRequestPartition Partition, TopicPartition TopicPartition)>>
        _templates = [];
    private readonly List<string> _topicOrder = ["fetch-ordering"];

    [GlobalSetup]
    public void Setup()
    {
        var partitions = new List<(FetchRequestPartition, TopicPartition)>(PartitionCount);
        for (var partition = 0; partition < PartitionCount; partition++)
        {
            var topicPartition = new TopicPartition("fetch-ordering", partition);
            _fetchPositions[topicPartition] = partition;
            partitions.Add((
                new FetchRequestPartition
                {
                    Partition = partition,
                    FetchOffset = 0,
                    PartitionMaxBytes = 1024 * 1024
                },
                topicPartition));
        }

        _templates.Add("fetch-ordering", partitions);
    }

    [Benchmark(OperationsPerInvoke = PartitionCount)]
    public int BuildCachedFetchRequest()
    {
        var topics = KafkaConsumer<string, string>.BuildFetchResult(_templates, _fetchPositions);
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
    public int BuildRotatedCachedFetchRequest()
    {
        var topics = KafkaConsumer<string, string>.BuildRotatedFetchResult(
            _templates,
            _fetchPositions,
            _topicOrder,
            topicRotation: 0,
            firstTopicPartitionRotation: 37);
        try
        {
            return topics[0].Partitions.Count;
        }
        finally
        {
            ConsumerFetchPools.ReturnFetchRequestTopics(topics);
        }
    }
}
