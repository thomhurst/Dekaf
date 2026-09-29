using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;
using Dekaf.Protocol;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures one full batch rotation per operation: rent a pooled PartitionBatch, append a record,
/// seal it into a ReadyBatch, complete the send and return both to their pools. This is the
/// per-batch path of low-volume producers with LingerMs 0, where every record gets its own batch.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class PartitionBatchRotationBenchmarks
{
    private const int OperationsPerInvoke = 100;
    private readonly TopicPartition _topicPartition = new("benchmark-topic", 0);
    private readonly byte[] _value = new byte[256];
    private PartitionBatchPool _batchPool = null!;
    private ReadyBatchPool _readyBatchPool = null!;
    private int _estimatedSize;
    private long _timestamp;

    [Params(16_384, 1_048_576)]
    public int BatchSize { get; set; }

    [Params(BufferMemoryAllocationStrategy.Full, BufferMemoryAllocationStrategy.Incremental)]
    public BufferMemoryAllocationStrategy AllocationStrategy { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = BatchSize,
            BufferMemory = 256L * 1024 * 1024,
            LingerMs = 0,
            BufferMemoryAllocationStrategy = AllocationStrategy,
        };
        _estimatedSize = PartitionBatch.EstimateRecordSize(0, _value.Length, null, 0);
        _timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        const int poolSize = BatchArena.DefaultPoolSize;
        _readyBatchPool = new ReadyBatchPool(poolSize * 2);
        _batchPool = new PartitionBatchPool(options, maxPoolSize: poolSize);
        _batchPool.SetReadyBatchPool(_readyBatchPool);
        if (AllocationStrategy == BufferMemoryAllocationStrategy.Incremental)
        {
            IncrementalBatchBuffer.RatchetPoolSize(poolSize * 2, BatchSize);
            IncrementalBatchBuffer.PreWarm(16, BatchSize);
        }
        else
        {
            BatchArena.PreWarm(16, ProducerOptions.GetEffectiveArenaCapacity(BatchSize, 0));
        }

        _readyBatchPool.PreWarm(16);
        _batchPool.PreWarm(16);

        for (var i = 0; i < 10_000; i++)
            RotateOne();
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public int Rotate()
    {
        var records = 0;
        for (var i = 0; i < OperationsPerInvoke; i++)
            records += RotateOne();

        return records;
    }

    private int RotateOne()
    {
        var batch = _batchPool.Rent(_topicPartition, partitionCount: 1);
        if (!batch.TryAppendFromSpans(
                _timestamp++,
                ReadOnlySpan<byte>.Empty,
                keyIsNull: true,
                _value,
                valueIsNull: false,
                headers: null,
                headerCount: 0,
                completionSource: null,
                callback: null,
                _estimatedSize).Success)
        {
            throw new InvalidOperationException("Record did not fit in an empty benchmark batch.");
        }

        var records = batch.RecordCount;
        var ready = batch.Complete() ?? throw new InvalidOperationException("Batch did not complete.");
        ready.CompleteSend(0, DateTimeOffset.UnixEpoch);
        _readyBatchPool.Return(ready);
        _batchPool.Return(batch);
        return records;
    }
}
