using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

[Category("ConsumerGroup")]
public sealed class ConsumerDeferredPositionTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [Timeout(120_000)]
    [Arguments(ErrorCode.CoordinatorLoadInProgress)]
    [Arguments(ErrorCode.UnknownTopicId)]
    public async Task RetriableOffsetLookup_DeliversPositionedPartitionAndInitializesDeferredPartitionAfterRecovery(
        ErrorCode errorCode, CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        await using (var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync(cancellationToken))
        {
            foreach (var (partition, value) in new[] { (0, "initial"), (0, "during-failure"), (1, "after-recovery") })
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic, Partition = partition, Key = "key", Value = value
                }, cancellationToken);
        }

        var clientId = $"deferred-position-{Guid.NewGuid():N}";
        await using var pool = new OffsetFetchFaultPool(new ConnectionPool(clientId, new ConnectionOptions()), errorCode);
        await using var metadata = new MetadataManager(pool, [KafkaContainer.BootstrapServers]);
        await using var consumer = new KafkaConsumer<string, string>(new ConsumerOptions
        {
            BootstrapServers = [KafkaContainer.BootstrapServers],
            ClientId = clientId,
            GroupId = clientId,
            OffsetCommitMode = OffsetCommitMode.Manual,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            RequestTimeoutMs = 30_000
        }, Serializers.String, Serializers.String, pool, metadata);
        await consumer.InitializeAsync(cancellationToken);
        pool.EnableFault();
        consumer.Subscribe(topic);
        var initial = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cancellationToken);
        await Assert.That(initial).IsNotNull();
        await Assert.That(initial!.Value.Value).IsEqualTo("initial");

        // The broker supplies the real membership and assignment; only OffsetFetch fails.
        // A 30 s retry loop under the assignment lock cannot satisfy this 10 s bound.
        using var lookupTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lookupTimeout.CancelAfter(TimeSpan.FromSeconds(10));
        do
        {
            await consumer.EnsureAssignmentForPollAsync(lookupTimeout.Token);
            if (pool.FailedRequests > 0)
                break;
            await Task.Delay(25, lookupTimeout.Token);
        }
        while (true);
        await Assert.That(pool.FailedRequests).IsGreaterThan(0);

        var duringFailure = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(2), lookupTimeout.Token);
        await Assert.That(duringFailure).IsNotNull();
        await Assert.That(duringFailure!.Value.Partition).IsEqualTo(0);
        await Assert.That(duringFailure.Value.Value).IsEqualTo("during-failure");
        await Assert.That(consumer.GetPosition(new TopicPartition(topic, 1))).IsNull();

        pool.DisableFault();
        var recovered = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), cancellationToken);
        await Assert.That(recovered).IsNotNull();
        await Assert.That(recovered!.Value.Partition).IsEqualTo(1);
        await Assert.That(recovered.Value.Value).IsEqualTo("after-recovery");
    }

    private sealed class OffsetFetchFaultPool(ConnectionPool inner, ErrorCode failureCode) : IConnectionPool
    {
        private readonly ErrorCode _failureCode = failureCode;
        private int _enabled;
        private int _failedRequests;
        public int FailedRequests => Volatile.Read(ref _failedRequests);
        public void EnableFault() => Volatile.Write(ref _enabled, 1);
        public void DisableFault() => Volatile.Write(ref _enabled, 0);

        public async ValueTask<IKafkaConnection> GetConnectionAsync(int brokerId, CancellationToken cancellationToken = default) =>
            new FaultConnection(this, await inner.GetConnectionAsync(brokerId, cancellationToken));
        public async ValueTask<IKafkaConnection> GetConnectionByIndexAsync(int brokerId, int index, CancellationToken cancellationToken = default) =>
            new FaultConnection(this, await inner.GetConnectionByIndexAsync(brokerId, index, cancellationToken));
        public ValueTask<IKafkaConnection> GetConnectionAsync(string host, int port, CancellationToken cancellationToken = default) =>
            inner.GetConnectionAsync(host, port, cancellationToken);
        public void RegisterBroker(int brokerId, string host, int port) => inner.RegisterBroker(brokerId, host, port);
        public ValueTask<int> ScaleConnectionGroupAsync(int brokerId, int newCount, CancellationToken cancellationToken = default) =>
            inner.ScaleConnectionGroupAsync(brokerId, newCount, cancellationToken);
        public ValueTask<IKafkaConnection?> ShrinkConnectionGroupAsync(int brokerId, int newCount, CancellationToken cancellationToken = default) =>
            inner.ShrinkConnectionGroupAsync(brokerId, newCount, cancellationToken);
        public ValueTask RemoveConnectionAsync(int brokerId) => inner.RemoveConnectionAsync(brokerId);
        public ValueTask CloseAllAsync() => inner.CloseAllAsync();
        public ValueTask DisposeAsync() => inner.DisposeAsync();

        private sealed class FaultConnection(OffsetFetchFaultPool owner, IKafkaConnection connection) : IKafkaConnection
        {
            public int BrokerId => connection.BrokerId;
            public string Host => connection.Host;
            public int Port => connection.Port;
            public bool IsConnected => connection.IsConnected;
            public ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
                where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse
            {
                if (request is OffsetFetchRequest fetch && Volatile.Read(ref owner._enabled) != 0)
                {
                    // The first response positions partition 0 and defers partition 1. Later
                    // requests fail for a different retriable reason until the test heals them.
                    if (Interlocked.CompareExchange(ref owner._enabled, 2, 1) == 1)
                    {
                        var topics = fetch.Topics!.Select(topic => new OffsetFetchResponseTopic
                        {
                            Name = topic.Name,
                            TopicId = topic.TopicId,
                            Partitions = topic.PartitionIndexes.Select(index => new OffsetFetchResponsePartition
                            {
                                PartitionIndex = index,
                                CommittedOffset = index == 0 ? 0 : -1,
                                ErrorCode = index == 0 ? ErrorCode.None : ErrorCode.UnstableOffsetCommit
                            }).ToArray()
                        }).ToArray();
                        return ValueTask.FromResult((TResponse)(object)new OffsetFetchResponse
                        {
                            Topics = apiVersion < 8 ? topics : null,
                            Groups = apiVersion < 8 ? null : [new OffsetFetchResponseGroup
                            {
                                GroupId = fetch.GroupId!, Topics = topics, ErrorCode = ErrorCode.None
                            }]
                        });
                    }
                    Interlocked.Increment(ref owner._failedRequests);
                    return ValueTask.FromResult((TResponse)(object)new OffsetFetchResponse
                    {
                        ErrorCode = apiVersion < 8 ? owner._failureCode : ErrorCode.None,
                        Groups = apiVersion < 8 ? null : [new OffsetFetchResponseGroup
                        {
                            GroupId = fetch.GroupId!, Topics = [], ErrorCode = owner._failureCode
                        }]
                    });
                }
                return connection.SendAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);
            }

            public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
                where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse =>
                connection.SendPipelinedAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);
            public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
                where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse =>
                connection.SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);
            public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
                where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse =>
                connection.SendFireAndForgetAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);
            public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
                where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse =>
                connection.SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);
            public ValueTask ConnectAsync(CancellationToken cancellationToken = default) => connection.ConnectAsync(cancellationToken);
            public ValueTask DisposeAsync() => connection.DisposeAsync();
        }
    }
}
