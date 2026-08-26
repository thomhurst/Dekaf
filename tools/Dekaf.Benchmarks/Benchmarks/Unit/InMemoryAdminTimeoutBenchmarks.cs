using BenchmarkDotNet.Attributes;
using Dekaf.Admin;
using Dekaf.Testing;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser(displayGenColumns: false)]
[ShortRunJob]
public class InMemoryAdminTimeoutBenchmarks
{
    private const string GroupId = "streams-benchmark";
    private readonly InMemoryAdminClient _admin;
    private readonly IReadOnlyDictionary<string, ListStreamsGroupOffsetsSpec> _groupSpecs;

    public InMemoryAdminTimeoutBenchmarks()
    {
        var partition = new TopicPartition("input", 0);
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(partition.Topic);
        _admin = new InMemoryAdminClient(cluster);
        _admin.AlterStreamsGroupOffsetsAsync(
                GroupId,
                [new TopicPartitionOffset(partition.Topic, partition.Partition, 42)])
            .GetAwaiter()
            .GetResult();
        _groupSpecs = new Dictionary<string, ListStreamsGroupOffsetsSpec>
        {
            [GroupId] = new() { TopicPartitions = [partition] }
        };
    }

    [Benchmark]
    public ValueTask<IReadOnlyDictionary<string, StreamsGroupOffsetsResult>> ListOffsets() =>
        _admin.ListStreamsGroupOffsetsAsync(_groupSpecs);
}
