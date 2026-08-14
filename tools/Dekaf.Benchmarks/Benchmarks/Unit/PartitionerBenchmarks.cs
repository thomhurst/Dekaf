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
    private static readonly string[] Topics =
    [
        "partitioner-hot-path-0",
        "partitioner-hot-path-1",
        "partitioner-hot-path-2",
        "partitioner-hot-path-3",
        "partitioner-hot-path-4",
        "partitioner-hot-path-5",
        "partitioner-hot-path-6",
        "partitioner-hot-path-7",
        "partitioner-hot-path-8",
        "partitioner-hot-path-9",
        "partitioner-hot-path-10",
        "partitioner-hot-path-11"
    ];
    private readonly IPartitioner _baselinePartitioner = new HashingDefaultPartitioner();
    private readonly IPartitioner _partitioner = new DefaultPartitioner();
    private readonly IUniformStickyPartitioner _uniformPartitioner;
    private readonly byte[] _key = "benchmark-partition-key"u8.ToArray();

    public PartitionerBenchmarks()
    {
        _uniformPartitioner = (IUniformStickyPartitioner)_partitioner;
    }

    [Params(1, 12)]
    public int PartitionCount { get; set; }

    [Benchmark(Baseline = true, OperationsPerInvoke = 1_000)]
    public int HashThenPartitionKeyedRecords()
        => PartitionKeyedRecords(_baselinePartitioner);

    [Benchmark(OperationsPerInvoke = 1_000)]
    public int PartitionKeyedRecords()
        => PartitionKeyedRecords(_partitioner);

    [Benchmark(OperationsPerInvoke = 1_000)]
    public int PartitionAndRecordNullKeys()
    {
        var result = 0;
        for (var i = 0; i < 1_000; i++)
        {
            var partition = _partitioner.Partition(
                Topic,
                ReadOnlySpan<byte>.Empty,
                keyIsNull: true,
                PartitionCount);
            _uniformPartitioner.OnRecordAppended(
                Topic,
                partition,
                bytes: 1_000,
                PartitionCount);
            result ^= partition;
        }

        return result;
    }

    [Benchmark(OperationsPerInvoke = 1_000)]
    public int PartitionAndRecordNullKeysAcrossTopics()
    {
        var result = 0;
        for (var i = 0; i < 1_000; i++)
        {
            var topic = Topics[i % Topics.Length];
            var partition = _partitioner.Partition(
                topic,
                ReadOnlySpan<byte>.Empty,
                keyIsNull: true,
                PartitionCount);
            _uniformPartitioner.OnRecordAppended(
                topic,
                partition,
                bytes: 1_000,
                PartitionCount);
            result ^= partition;
        }

        return result;
    }

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
