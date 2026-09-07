using BenchmarkDotNet.Attributes;
using Dekaf.Admin;
using Dekaf.Producer;
using Dekaf.Testing;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Exercises the full stability scan without matching a pending partition.
/// Each live transaction stages 32 offsets; each query selects 32 other partitions.
/// </summary>
[MemoryDiagnoser]
public class InMemoryPendingOffsetQueryBenchmarks
{
    [Params(1, 16, 64)]
    public int Transactions { get; set; }

    private InMemoryAdminClient _admin = null!;
    private InMemoryProducer<string, string>[] _producers = null!;
    private ITransaction<string, string>[] _transactions = null!;
    private Dictionary<string, ListConsumerGroupOffsetsSpec> _query = null!;
    private readonly ListConsumerGroupOffsetsOptions _stable = new() { RequireStable = true };

    [GlobalSetup]
    public void Setup()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders", partitionCount: 64);
        _admin = new(cluster);
        _producers = new InMemoryProducer<string, string>[Transactions];
        _transactions = new ITransaction<string, string>[Transactions];
        var offsets = new TopicPartitionOffset[32];
        var selected = new TopicPartition[32];
        for (var i = 0; i < offsets.Length; i++)
        {
            offsets[i] = new("orders", i, 42);
            selected[i] = new("orders", i + offsets.Length);
        }
        for (var i = 0; i < Transactions; i++)
        {
            _producers[i] = new(cluster);
            _transactions[i] = _producers[i].BeginTransaction();
            _transactions[i].SendOffsetsToTransactionAsync(offsets, "group").GetAwaiter().GetResult();
        }
        _query = new() { ["group"] = new() { TopicPartitions = selected } };
        // A matching partition must remain blocked while the measured selection is ready.
        if (cluster.TryGetStableGroupOffsetDetails("group", [new("orders", 0)], out _, out _))
            throw new InvalidOperationException("Benchmark requires pending transactional offsets.");
        if (!StableQueryWithPendingTransactions().IsCompletedSuccessfully)
            throw new InvalidOperationException("Measured selection must complete synchronously.");
    }

    [Benchmark]
    public ValueTask<IReadOnlyDictionary<string, ConsumerGroupOffsetsResult>> StableQueryWithPendingTransactions() =>
        _admin.ListConsumerGroupOffsetsAsync(_query, _stable);

    [GlobalCleanup]
    public void Cleanup()
    {
        for (var i = 0; i < _transactions.Length; i++)
        {
            _transactions[i].DisposeAsync().GetAwaiter().GetResult();
            _producers[i].DisposeAsync().GetAwaiter().GetResult();
        }
        _admin.DisposeAsync().GetAwaiter().GetResult();
    }
}
