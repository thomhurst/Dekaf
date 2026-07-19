using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class SparseReadyBatchCompletionBenchmarks
{
    private const int BatchCount = 64;
    private const int RecordsPerBatch = 16_384;

    private readonly TopicPartition _topicPartition = new("sparse-completion", 0);
    private readonly ValueTaskSourcePool<RecordMetadata> _sourcePool = new();
    private readonly ReadyBatch[] _batches = new ReadyBatch[BatchCount];
    private readonly ValueTask<RecordMetadata>[] _tasks = new ValueTask<RecordMetadata>[BatchCount];
    private ProducerOptions _options = null!;

    [GlobalSetup]
    public void Setup()
    {
        _options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = 1_048_576,
            InitialBatchRecordCapacity = RecordsPerBatch
        };
    }

    [IterationSetup]
    public void PrepareBatches()
    {
        for (var batchIndex = 0; batchIndex < _batches.Length; batchIndex++)
        {
            var batch = new PartitionBatch(_topicPartition, _options);
            for (var recordIndex = 0; recordIndex < RecordsPerBatch - 1; recordIndex++)
                Append(batch, recordIndex, completionSource: null);

            var source = _sourcePool.Rent();
            _tasks[batchIndex] = source.Task;
            Append(batch, RecordsPerBatch - 1, source);
            _batches[batchIndex] = batch.Complete()!;
        }
    }

    [Benchmark(OperationsPerInvoke = BatchCount)]
    public long CompleteSparseBatches()
    {
        var offset = 0L;
        for (var i = 0; i < _batches.Length; i++)
        {
            _batches[i].CompleteSend(offset, DateTimeOffset.UnixEpoch);
            offset += _tasks[i].GetAwaiter().GetResult().Offset;
        }

        return offset;
    }

    [GlobalCleanup]
    public void Cleanup() => _sourcePool.DisposeAsync().GetAwaiter().GetResult();

    private static void Append(
        PartitionBatch batch,
        int recordIndex,
        PooledValueTaskSource<RecordMetadata>? completionSource)
    {
        if (!batch.TryAppendFromSpans(
                recordIndex,
                ReadOnlySpan<byte>.Empty,
                keyIsNull: true,
                ReadOnlySpan<byte>.Empty,
                valueIsNull: true,
                headers: null,
                headerCount: 0,
                completionSource,
                callback: null,
                estimatedSize: 16).Success)
        {
            throw new InvalidOperationException("Benchmark record did not fit.");
        }
    }
}

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class DenseDeliveryIndexTrackingBenchmarks
{
    private const int BatchCount = 256;
    private const int RecordsPerBatch = 16_384;

    private readonly TopicPartition _topicPartition = new("dense-completion", 0);
    private readonly PooledValueTaskSource<RecordMetadata> _source = new();
    private readonly PartitionBatch[] _batches = new PartitionBatch[BatchCount];
    private ProducerOptions _options = null!;

    [GlobalSetup]
    public void Setup()
    {
        _options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = 1_048_576,
            InitialBatchRecordCapacity = RecordsPerBatch
        };
    }

    [IterationSetup]
    public void PrepareBatches()
    {
        for (var i = 0; i < _batches.Length; i++)
            _batches[i] = new PartitionBatch(_topicPartition, _options);
    }

    [Benchmark(OperationsPerInvoke = BatchCount * RecordsPerBatch)]
    public int AppendDenseCompletions()
    {
        for (var batchIndex = 0; batchIndex < _batches.Length; batchIndex++)
        {
            var batch = _batches[batchIndex];
            for (var recordIndex = 0; recordIndex < RecordsPerBatch; recordIndex++)
            {
                if (!batch.TryAppendFromSpans(
                        recordIndex,
                        ReadOnlySpan<byte>.Empty,
                        keyIsNull: true,
                        ReadOnlySpan<byte>.Empty,
                        valueIsNull: true,
                        headers: null,
                        headerCount: 0,
                        _source,
                        callback: null,
                        estimatedSize: 16).Success)
                {
                    throw new InvalidOperationException("Benchmark record did not fit.");
                }
            }
        }

        return _batches[^1].RecordCount;
    }

    [IterationCleanup]
    public void CleanupBatches()
    {
        for (var i = 0; i < _batches.Length; i++)
        {
            var ready = _batches[i].Complete()!;
            ready.TryAbandonForSplit(ready.Generation);
        }
    }
}
