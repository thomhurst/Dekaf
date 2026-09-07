using BenchmarkDotNet.Attributes;
using Dekaf.Admin;
using Dekaf.Testing;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
public class InMemoryConsumerGroupOffsetQueryBenchmarks
{
    [Params(1, 32)]
    public int Partitions { get; set; }

    private InMemoryAdminClient _admin = null!;
    private InMemoryProducer<string, string> _producer = null!;
    private TopicPartitionOffset[] _offsets = null!;
    private Dictionary<string, ListStreamsGroupOffsetsSpec> _streams = null!;
    private Dictionary<string, ListConsumerGroupOffsetsSpec> _rich = null!;
    private readonly ListConsumerGroupOffsetsOptions _stable = new() { RequireStable = true };

    [GlobalSetup]
    public void Setup()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders", partitionCount: Partitions);
        _admin = new(cluster);
        _producer = new(cluster);
        _offsets = new TopicPartitionOffset[Partitions];
        for (var i = 0; i < _offsets.Length; i++)
            _offsets[i] = new("orders", i, 42, 3) { Metadata = "checkpoint" };
        _admin.AlterConsumerGroupOffsetsAsync("group", _offsets).GetAwaiter().GetResult();
        _streams = new() { ["group"] = new() };
        _rich = new() { ["group"] = new() };
    }

    [Benchmark]
    public ValueTask<IReadOnlyDictionary<TopicPartition, long>> LegacyQuery() =>
        _admin.ListConsumerGroupOffsetsAsync("group");

    [Benchmark]
    public ValueTask<IReadOnlyDictionary<string, StreamsGroupOffsetsResult>> StreamsQuery() =>
        _admin.ListStreamsGroupOffsetsAsync(_streams);

    [Benchmark]
    public ValueTask<IReadOnlyDictionary<string, ConsumerGroupOffsetsResult>> RichStableQuery() =>
        _admin.ListConsumerGroupOffsetsAsync(_rich, _stable);

    [Benchmark]
    public async ValueTask CommitOffsets()
    {
        await using var transaction = _producer.BeginTransaction();
        await transaction.SendOffsetsToTransactionAsync(_offsets, "group");
        await transaction.CommitAsync();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _producer.DisposeAsync().GetAwaiter().GetResult();
        _admin.DisposeAsync().GetAwaiter().GetResult();
    }
}
