using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Compression;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// The per-send connection lease in <c>BrokerSender.GetConnectionLeaseAtIndexAsync</c>: every
/// coalesced produce request takes this path before it is written. Steady state pins the
/// slot's connection, so the lease must complete synchronously and allocate nothing; the
/// retirable variant is the pooled <c>KafkaConnection</c> shape with its lease counter.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class BrokerSenderConnectionLeaseBenchmarks
{
    private BrokerSender _sender = null!;
    private RecordAccumulator _accumulator = null!;
    private MetadataManager _metadata = null!;

    [Params(false, true)]
    public bool Retirable { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        IKafkaConnection connection = Retirable ? new RetirableConnection() : new Connection();
        var pool = new Pool(connection);
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
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

        // Pin the slot once so every measured call takes the steady-state path.
        using var lease = await _sender.GetConnectionLeaseAtIndexAsync(0, CancellationToken.None);
        if (!ReferenceEquals(lease.Connection, connection))
            throw new InvalidOperationException("The pool connection was not pinned.");
    }

    [Benchmark]
    public IKafkaConnection PinnedLease()
    {
        var pending = _sender.GetConnectionLeaseAtIndexAsync(0, CancellationToken.None);
        if (!pending.IsCompletedSuccessfully)
            throw new InvalidOperationException("The pinned lease must complete synchronously.");

        using var lease = pending.Result;
        return lease.Connection;
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _sender.DisposeAsync();
        await _accumulator.DisposeAsync();
        await _metadata.DisposeAsync();
    }

    private class Connection : IKafkaConnection
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

    private sealed class RetirableConnection : Connection, IRetirableKafkaConnection
    {
        private int _leaseCount;

        public int LeaseCount => Volatile.Read(ref _leaseCount);
        public int ActiveOperationCount => 0;

        public bool TryAcquireLease()
        {
            Interlocked.Increment(ref _leaseCount);
            return true;
        }

        public void ReleaseLease() => Interlocked.Decrement(ref _leaseCount);
        public void BeginRetirement() { }
        public void CompleteRetirement() { }
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
