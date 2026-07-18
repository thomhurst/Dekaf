using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Compares deterministic append throughput and steady-state allocations for full and
/// incremental batch storage without the accumulator benchmark's asynchronous drainer noise.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class BatchMemoryAllocationBenchmarks
{
    private const int OperationsPerInvoke = 100;
    private readonly TopicPartition _topicPartition = new("benchmark-topic", 0);
    private ProducerOptions _options = null!;
    private PartitionBatchPool _batchPool = null!;
    private ReadyBatchPool _readyBatchPool = null!;
    private PartitionBatch _batch = null!;
    private byte[] _value = null!;
    private int _estimatedSize;
    private long _timestamp;

    [Params(100, 1000)]
    public int MessageSize { get; set; }

    [Params(BufferMemoryAllocationStrategy.Full, BufferMemoryAllocationStrategy.Incremental)]
    public BufferMemoryAllocationStrategy AllocationStrategy { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = 1_048_576,
            BufferMemory = 256L * 1024 * 1024,
            LingerMs = 100,
            BufferMemoryAllocationStrategy = AllocationStrategy
        };
        _value = new byte[MessageSize];
        _estimatedSize = PartitionBatch.EstimateRecordSize(0, MessageSize, null, 0);
        _timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        const int poolSize = BatchArena.DefaultPoolSize;
        _readyBatchPool = new ReadyBatchPool(poolSize * 2);
        _batchPool = new PartitionBatchPool(_options, poolSize);
        _batchPool.SetReadyBatchPool(_readyBatchPool);

        if (AllocationStrategy == BufferMemoryAllocationStrategy.Incremental)
        {
            IncrementalBatchBuffer.RatchetPoolSize(poolSize * 2);
            IncrementalBatchBuffer.PreWarm(poolSize * 2, _options.BatchSize);
        }
        else
        {
            BatchArena.PreWarm(16, ProducerOptions.GetEffectiveArenaCapacity(
                _options.BatchSize,
                _options.ArenaCapacity));
        }

        _readyBatchPool.PreWarm(32);
        _batchPool.PreWarm(16);
        _batch = _batchPool.Rent(_topicPartition, partitionCount: 1);

        for (var i = 0; i < 100_000; i++)
            AppendOne();
    }

    [GlobalCleanup]
    public void Cleanup() => CompleteAndReturnBatch();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public int Append()
    {
        for (var i = 0; i < OperationsPerInvoke; i++)
            AppendOne();

        return _batch.RecordCount;
    }

    private void AppendOne()
    {
        var result = _batch.TryAppendFromSpans(
            _timestamp++,
            ReadOnlySpan<byte>.Empty,
            keyIsNull: true,
            _value,
            valueIsNull: false,
            headers: null,
            headerCount: 0,
            completionSource: null,
            callback: null,
            _estimatedSize);

        if (result.Success)
            return;

        RotateBatch();
        if (!_batch.TryAppendFromSpans(
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
    }

    private void RotateBatch()
    {
        CompleteAndReturnBatch();
        _batch = _batchPool.Rent(_topicPartition, partitionCount: 1);
    }

    private void CompleteAndReturnBatch()
    {
        var completed = _batch.Complete();
        if (completed is not null)
        {
            completed.CompleteSend(0, DateTimeOffset.UnixEpoch);
            _readyBatchPool.Return(completed);
        }

        _batchPool.Return(_batch);
    }
}
