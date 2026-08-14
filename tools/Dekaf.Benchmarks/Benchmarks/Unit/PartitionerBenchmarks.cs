using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class PartitionerBenchmarks
{
    private const string Topic = "partitioner-hot-path";
    private const uint PositiveMask = 0x7fff_ffff;
    private readonly IPartitioner _baselinePartitioner = new HashingDefaultPartitioner();
    private readonly IPartitioner _partitioner = new DefaultPartitioner();
    private readonly byte[] _key = "benchmark-partition-key"u8.ToArray();

    [Params(1, 12)]
    public int PartitionCount { get; set; }

    [Benchmark(Baseline = true, OperationsPerInvoke = 1_000)]
    public int HashThenPartitionKeyedRecords()
        => PartitionKeyedRecords(_baselinePartitioner);

    [Benchmark(OperationsPerInvoke = 1_000)]
    public int PartitionKeyedRecords()
        => PartitionKeyedRecords(_partitioner);

    private int PartitionKeyedRecords(IPartitioner partitioner)
    {
        var result = 0;
        for (var i = 0; i < 1_000; i++)
            result ^= partitioner.Partition(Topic, _key, keyIsNull: false, PartitionCount);

        return result;
    }

    private sealed class HashingDefaultPartitioner : IPartitioner
    {
        public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount)
            => (int)((Murmur2.Hash(key) & PositiveMask) % (uint)partitionCount);
    }
}
