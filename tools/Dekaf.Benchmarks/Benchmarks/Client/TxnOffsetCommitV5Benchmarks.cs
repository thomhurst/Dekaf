using System.Reflection;
using BenchmarkDotNet.Attributes;
using Dekaf.Internal;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Client;

[MemoryDiagnoser]
public class TxnOffsetCommitV5Benchmarks
{
    private Harness _tv1 = null!;
    private Harness _tv2 = null!;

    [Params(0, 1)]
    public int SimulatedRpcLatencyMs { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _tv1 = CreateHarness(transactionVersion: 1);
        _tv2 = CreateHarness(transactionVersion: 2);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _tv1.DisposeAsync().ConfigureAwait(false);
        await _tv2.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark(Baseline = true, Description = "TV1: explicit enrollment (3 RPCs)")]
    public Task Tv1ThreeRpcs() => RunAsync(_tv1, expectedRpcCount: 3);

    [Benchmark(Description = "TV2: implicit enrollment (2 RPCs)")]
    public Task Tv2TwoRpcs() => RunAsync(_tv2, expectedRpcCount: 2);

    private static async Task RunAsync(Harness harness, int expectedRpcCount)
    {
        harness.Connection.ResetRpcCount();
        await harness.Producer.SendOffsetsToTransactionInternalAsync(
            [new TopicPartitionOffset("orders", 0, 42)],
            "benchmark-group",
            CancellationToken.None).ConfigureAwait(false);

        if (harness.Connection.RpcCount != expectedRpcCount)
        {
            throw new InvalidOperationException(
                $"Expected {expectedRpcCount} RPCs, observed {harness.Connection.RpcCount}");
        }
    }

    private Harness CreateHarness(short transactionVersion)
    {
        var connection = new BenchmarkConnection(SimulatedRpcLatencyMs);
        var connectionPool = new ConnectionPool(
            $"txn-offset-v{transactionVersion}-benchmark",
            connectionOptions: null,
            connectionsPerBroker: 1,
            connectionFactory: (_, _, _, _, _) => ValueTask.FromResult<IKafkaConnection>(connection));
        connectionPool.RegisterBroker(1, "localhost", 9092);

        var metadataManager = new MetadataManager(connectionPool, ["localhost:9092"]);
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }],
            Topics = []
        });
        metadataManager.ObserveClusterCapabilities(
            "benchmark-cluster",
            KafkaConnectionCapabilities.Create(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys = [],
                FinalizedFeaturesEpoch = 1,
                FinalizedFeatures =
                [
                    new FinalizedFeature(
                        "transaction.version",
                        transactionVersion,
                        transactionVersion)
                ]
            }));

        var producer = new KafkaProducer<string, string>(
            new ProducerOptions
            {
                BootstrapServers = ["localhost:9092"],
                TransactionalId = $"txn-offset-v{transactionVersion}-benchmark",
                RetryBackoffMs = 0,
                RetryBackoffMaxMs = 0,
                CloseTimeoutMs = 100
            },
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager,
            DekafMemoryBudget.Global);
        SetField(producer, "_initialized", true);
        SetField(producer, "_producerId", 42L);
        SetField(producer, "_producerEpoch", (short)3);
        SetField(producer, "_transactionCoordinatorId", 1);
        SetField(producer, "_currentTransactionFeatureVersion", transactionVersion);
        producer._currentTransactionUsesTV2 = transactionVersion >= 2;
        producer._transactionState = TransactionState.InTransaction;

        return new Harness(producer, connectionPool, metadataManager, connection);
    }

    private static void SetField<T>(object target, string name, T value) =>
        target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(target, value);

    private sealed class Harness(
        KafkaProducer<string, string> producer,
        ConnectionPool connectionPool,
        MetadataManager metadataManager,
        BenchmarkConnection connection) : IAsyncDisposable
    {
        internal KafkaProducer<string, string> Producer { get; } = producer;
        internal BenchmarkConnection Connection { get; } = connection;

        public async ValueTask DisposeAsync()
        {
            Producer._transactionState = TransactionState.Ready;
            await Producer.DisposeAsync().ConfigureAwait(false);
            await metadataManager.DisposeAsync().ConfigureAwait(false);
            await connectionPool.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class BenchmarkConnection(int simulatedRpcLatencyMs)
        : IKafkaConnection, IKafkaCapabilityProvider
    {
        private int _rpcCount;

        public int BrokerId => 1;
        public string Host => "localhost";
        public int Port => 9092;
        public bool IsConnected => true;
        public int RpcCount => Volatile.Read(ref _rpcCount);
        public KafkaConnectionCapabilities Capabilities { get; } =
            KafkaConnectionCapabilities.Create(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys =
                [
                    new ApiVersion(ApiKey.AddOffsetsToTxn, 3, 4),
                    new ApiVersion(ApiKey.FindCoordinator, 4, 5),
                    new ApiVersion(ApiKey.TxnOffsetCommit, 3, 5)
                ]
            });

        internal void ResetRpcCount() => Interlocked.Exchange(ref _rpcCount, 0);

        public async ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            Interlocked.Increment(ref _rpcCount);
            if (simulatedRpcLatencyMs > 0)
            {
                await Task.Delay(simulatedRpcLatencyMs, cancellationToken).ConfigureAwait(false);
            }

            IKafkaResponse response = request switch
            {
                AddOffsetsToTxnRequest => new AddOffsetsToTxnResponse { ErrorCode = ErrorCode.None },
                FindCoordinatorRequest findRequest => new FindCoordinatorResponse
                {
                    Coordinators =
                    [
                        new Coordinator
                        {
                            Key = findRequest.Key,
                            NodeId = 1,
                            Host = "localhost",
                            Port = 9092,
                            ErrorCode = ErrorCode.None
                        }
                    ]
                },
                TxnOffsetCommitRequest commitRequest => new TxnOffsetCommitResponse
                {
                    Topics = commitRequest.Topics.Select(topic => new TxnOffsetCommitResponseTopic
                    {
                        Name = topic.Name,
                        Partitions = topic.Partitions.Select(partition =>
                            new TxnOffsetCommitResponsePartition
                            {
                                PartitionIndex = partition.PartitionIndex,
                                ErrorCode = ErrorCode.None
                            }).ToArray()
                    }).ToArray()
                },
                _ => throw new NotSupportedException(typeof(TRequest).Name)
            };

            return (TResponse)response;
        }

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse => throw new NotSupportedException();

        public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse => throw new NotSupportedException();

        public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse => throw new NotSupportedException();

        public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse => throw new NotSupportedException();
    }
}
