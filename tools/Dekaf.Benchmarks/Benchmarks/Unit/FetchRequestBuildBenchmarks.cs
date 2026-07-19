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
    private const int TopicCount = 4;
    private const int PartitionCount = 100;

    private readonly ConcurrentDictionary<TopicPartition, long> _fetchPositions = new();
    private readonly Dictionary<string, List<(FetchRequestPartition Partition, TopicPartition TopicPartition)>>
        _templates = [];
    private readonly Dictionary<string, List<(FetchRequestPartition Partition, TopicPartition TopicPartition)>>
        _multiTopicTemplates = [];
    private readonly List<string> _topicOrder = ["fetch-ordering"];
    private readonly List<string> _multiTopicOrder = [];

    [GlobalSetup]
    public void Setup()
    {
        _templates.Add("fetch-ordering", CreatePartitions("fetch-ordering", PartitionCount));

        var partitionsPerTopic = PartitionCount / TopicCount;
        for (var topicIndex = 0; topicIndex < TopicCount; topicIndex++)
        {
            var topic = $"fetch-ordering-{topicIndex}";
            _multiTopicOrder.Add(topic);
            _multiTopicTemplates.Add(topic, CreatePartitions(topic, partitionsPerTopic));
        }
    }

    private List<(FetchRequestPartition Partition, TopicPartition TopicPartition)> CreatePartitions(
        string topic,
        int count)
    {
        var partitions = new List<(FetchRequestPartition, TopicPartition)>(count);
        for (var partition = 0; partition < count; partition++)
        {
            var topicPartition = new TopicPartition(topic, partition);
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

        return partitions;
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
            partitionRotation: 37);
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
    public int BuildMultiTopicRotatedCachedFetchRequest()
    {
        var topics = KafkaConsumer<string, string>.BuildRotatedFetchResult(
            _multiTopicTemplates,
            _fetchPositions,
            _multiTopicOrder,
            topicRotation: 1,
            partitionRotation: 7);
        try
        {
            return topics.Count;
        }
        finally
        {
            ConsumerFetchPools.ReturnFetchRequestTopics(topics);
        }
    }
}
