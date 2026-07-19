using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class RackAwarePartitionerBenchmarks
{
    private const string Topic = "rack-aware-partitioner";

    private DefaultPartitioner _partitioner = null!;
    private IUniformStickyPartitioner _uniformPartitioner = null!;

    [Params(10, 10_000)]
    public int PartitionCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _partitioner = new DefaultPartitioner(
            stickyBatchSize: 1,
            adaptivePartitioning: false,
            availabilityTimeoutMs: 0,
            ignoreKeys: false,
            rackAwarePartitioning: true,
            clientRack: "rack-a");
        _uniformPartitioner = _partitioner;
        var localPartitions = new int[PartitionCount / 10];
        for (var i = 0; i < localPartitions.Length; i++)
            localPartitions[i] = i * 10;
        _uniformPartitioner.SetRackLocalPartitionsProvider((_, _) => localPartitions);
    }

    [Benchmark(OperationsPerInvoke = 1_000)]
    public int RotateBatch()
    {
        var partition = 0;
        for (var i = 0; i < 1_000; i++)
        {
            partition = _partitioner.Partition(
                Topic,
                ReadOnlySpan<byte>.Empty,
                keyIsNull: true,
                PartitionCount);
            _uniformPartitioner.OnRecordAppended(Topic, partition, bytes: 1, PartitionCount);
        }

        return partition;
    }
}
