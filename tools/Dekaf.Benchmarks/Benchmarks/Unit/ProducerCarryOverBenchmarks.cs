using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Compression;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol.Records;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <remarks>
/// <see cref="DispatchConnectionBuckets"/> is the per-wave bucket selection of the
/// multi-connection send path (#3798): one connection has capacity and the other is
/// saturated, and only the dispatched bucket's retries are finalized. The selection itself
/// allocates nothing; each finalized retry writes the existing Unmute event to the send loop's
/// channel, whose amortized queue segments show as about 2 B per batch.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 10, iterationCount: 10)]
public class ProducerCarryOverBenchmarks
{
    private const int BatchCount = 8;
    private const int ConnectionCount = 2;
    private const int BatchesPerBucket = BatchCount / ConnectionCount;

    private readonly BrokerSender.PartitionCarryOver _carryOver = new();
    private readonly List<BrokerSender.BatchReference> _drained = new(BatchCount);
    private readonly BrokerSender.ConnectionBucket[] _buckets = new BrokerSender.ConnectionBucket[ConnectionCount];
    private ReadyBatch[] _batches = null!;
    private ReadyBatch[] _bucketBatches = null!;
    private BrokerSender _sender = null!;
    private RecordAccumulator _accumulator = null!;
    private MetadataManager _metadata = null!;

    [GlobalSetup]
    public void Setup()
    {
        _batches = CreateRetryBatches(partitionCount: 1);
        _bucketBatches = CreateRetryBatches(partitionCount: BatchCount);

        var pool = new Pool();
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ConnectionsPerBroker = ConnectionCount,
            EnableAdaptiveConnections = false,
            EnableIdempotence = false,
            LingerMs = 1_000,
        };
        _metadata = new MetadataManager(pool, options.BootstrapServers);
        _accumulator = new RecordAccumulator(options);
        _sender = new BrokerSender(
            brokerId: 1,
            pool,
            _metadata,
            _accumulator,
            options,
            new CompressionCodecRegistry(),
            new PartitionInflightTracker(),
            getProduceApiVersion: static () => 9,
            setProduceApiVersion: static _ => { },
            isTransactional: static () => false,
            tryEnsurePartitionsInTransaction: null,
            bumpEpoch: null,
            getProducerState: null,
            rerouteBatch: null,
            onAcknowledgement: null,
            logger: null);

        // Saturate the second connection's in-flight byte budget.
        var pendingBytes = (long[])typeof(BrokerSender)
            .GetField("_pendingResponseBytesByConnection", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(_sender)!;
        pendingBytes[1] = long.MaxValue;

        for (var c = 0; c < ConnectionCount; c++)
        {
            _buckets[c].Batches = new ReadyBatch[BatchesPerBucket];
            _buckets[c].Generations = new int[BatchesPerBucket];
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _sender.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _accumulator.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _metadata.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Benchmark(OperationsPerInvoke = BatchCount)]
    public int EnqueueAndDrainReadyRetries()
    {
        for (var i = 0; i < _batches.Length; i++)
        {
            var batch = _batches[i];
            _carryOver.AddAfterRetries(new BrokerSender.BatchReference(batch, batch.Generation));
        }

        _drained.Clear();
        _carryOver.DrainTo(_drained);
        return _drained.Count;
    }

    [Benchmark(OperationsPerInvoke = BatchCount)]
    public int DispatchConnectionBuckets()
    {
        for (var c = 0; c < ConnectionCount; c++)
        {
            ref var bucket = ref _buckets[c];
            for (var i = 0; i < BatchesPerBucket; i++)
            {
                var batch = _bucketBatches[c * BatchesPerBucket + i];
                batch.IsRetry = true;
                bucket.Batches[i] = batch;
                bucket.Generations[i] = batch.Generation;
            }

            bucket.Count = BatchesPerBucket;
        }

        _sender.MarkDispatchableBuckets(_buckets, _carryOver);

        var dispatched = 0;
        for (var c = 0; c < ConnectionCount; c++)
        {
            ref var bucket = ref _buckets[c];
            if (bucket.Dispatch)
                dispatched++;
            bucket.Dispatch = false;
            bucket.Count = 0;
        }

        return dispatched;
    }

    private static ReadyBatch[] CreateRetryBatches(int partitionCount)
    {
        var batches = new ReadyBatch[BatchCount];
        for (var i = 0; i < batches.Length; i++)
        {
            var batch = new ReadyBatch();
            batch.Initialize(
                new TopicPartition("carry-over", i % partitionCount),
                new RecordBatch { Records = [] },
                completionSourcesArray: null,
                completionSourcesCount: 0,
                recordCount: 0,
                dataSize: 0);
            batch.IsRetry = true;
            batch.MarkPreSerialized();
            batches[i] = batch;
        }

        return batches;
    }

    private sealed class Pool : IConnectionPool
    {
        public ValueTask<IKafkaConnection> GetConnectionAsync(int brokerId, CancellationToken token = default) => throw new NotSupportedException();
        public ValueTask<IKafkaConnection> GetConnectionAsync(string host, int port, CancellationToken token = default) => throw new NotSupportedException();
        public ValueTask<IKafkaConnection> GetConnectionByIndexAsync(int brokerId, int index, CancellationToken token = default) => throw new NotSupportedException();
        public void RegisterBroker(int id, string host, int port) { }
        public ValueTask<int> ScaleConnectionGroupAsync(int id, int count, CancellationToken token = default) => ValueTask.FromResult(count);
        public ValueTask<IKafkaConnection?> ShrinkConnectionGroupAsync(int id, int count, CancellationToken token = default) => ValueTask.FromResult<IKafkaConnection?>(null);
        public ValueTask RemoveConnectionAsync(int id) => ValueTask.CompletedTask;
        public ValueTask CloseAllAsync() => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
