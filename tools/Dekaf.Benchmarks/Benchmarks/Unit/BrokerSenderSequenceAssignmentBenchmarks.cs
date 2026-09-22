using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Compression;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// <c>BrokerSender.AssignSequences</c>: every idempotent or transactional produce request stamps
/// its batches here, at send time, with the next sequence of each partition and an inflight
/// entry. Steady state: fresh batches, one per partition as the send loop coalesces them. The
/// sender is idle (no batch is ever routed to its loop), so this thread is the only one using
/// the partitions' counters and the tracker.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class BrokerSenderSequenceAssignmentBenchmarks
{
    private const int BatchCount = 8;

    private readonly PartitionInflightTracker _inflightTracker = new();
    private BrokerSender _sender = null!;
    private RecordAccumulator _accumulator = null!;
    private MetadataManager _metadata = null!;
    private ReadyBatch[] _batches = null!;

    [GlobalSetup]
    public void Setup()
    {
        var pool = new Pool(new Connection());
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            EnableAdaptiveConnections = false,
            EnableIdempotence = true,
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
            _inflightTracker,
            getProduceApiVersion: static () => 9,
            setProduceApiVersion: static _ => { },
            isTransactional: static () => true,
            tryEnsurePartitionsInTransaction: null,
            bumpEpoch: null,
            getProducerState: null,
            rerouteBatch: null,
            onAcknowledgement: null,
            logger: null);

        _batches = new ReadyBatch[BatchCount];
        for (var i = 0; i < _batches.Length; i++)
        {
            var batch = new ReadyBatch();
            batch.Initialize(
                new TopicPartition("sequence-assignment", i),
                new RecordBatch { Records = [], ProducerId = 1, ProducerEpoch = 0, BaseSequence = -1 },
                completionSourcesArray: null,
                completionSourcesCount: 0,
                recordCount: 0,
                dataSize: 0);
            _batches[i] = batch;
        }
    }

    [Benchmark(OperationsPerInvoke = BatchCount)]
    public int AssignFreshBatches()
    {
        _sender.AssignSequences(_batches, BatchCount);

        var sum = 0;
        for (var i = 0; i < _batches.Length; i++)
        {
            var batch = _batches[i];
            sum += batch.RecordBatch.BaseSequence;
            // Back to a fresh, untracked batch for the next invocation.
            _inflightTracker.Complete(batch.InflightEntry!);
            batch.InflightEntry = null;
            batch.RecordBatch.BaseSequence = -1;
        }

        return sum;
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _sender.DisposeAsync();
        await _accumulator.DisposeAsync();
        await _metadata.DisposeAsync();
        _inflightTracker.Dispose();
    }

    private sealed class Connection : IKafkaConnection
    {
        public int BrokerId => 1;
        public string Host => "localhost";
        public int Port => 9092;
        public bool IsConnected => true;
        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request, short version, CancellationToken token = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();
        public ValueTask ConnectAsync(CancellationToken token = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(TRequest request, short version, CancellationToken token = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();
        public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(TRequest request, short version, CancellationToken token = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();
        public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(TRequest request, short version, CancellationToken token = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();
        public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(TRequest request, short version, CancellationToken token = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();
    }

    private sealed class Pool(IKafkaConnection connection) : IConnectionPool
    {
        public ValueTask<IKafkaConnection> GetConnectionAsync(int brokerId, CancellationToken token = default) => ValueTask.FromResult(connection);
        public ValueTask<IKafkaConnection> GetConnectionAsync(string host, int port, CancellationToken token = default) => ValueTask.FromResult(connection);
        public ValueTask<IKafkaConnection> GetConnectionByIndexAsync(int brokerId, int index, CancellationToken token = default) => ValueTask.FromResult(connection);
        public void RegisterBroker(int id, string host, int port) { }
        public ValueTask<int> ScaleConnectionGroupAsync(int id, int count, CancellationToken token = default) => ValueTask.FromResult(1);
        public ValueTask<IKafkaConnection?> ShrinkConnectionGroupAsync(int id, int count, CancellationToken token = default) => ValueTask.FromResult<IKafkaConnection?>(null);
        public ValueTask RemoveConnectionAsync(int id) => ValueTask.CompletedTask;
        public ValueTask CloseAllAsync() => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
