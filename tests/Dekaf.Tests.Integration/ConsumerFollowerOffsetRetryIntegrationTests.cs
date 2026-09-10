using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

[Category("Consumer")]
[NotInParallel("RackAwareKafkaContainer")]
[ClassDataSource<RackAwareKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class ConsumerFollowerOffsetRetryIntegrationTests(RackAwareKafkaContainer kafka)
{
    [Test]
    [Arguments(AutoOffsetReset.Earliest)]
    [Arguments(AutoOffsetReset.Latest)]
    [Arguments(AutoOffsetReset.None)]
    public async Task FollowerCannotServeOffset_LeaderRetryDoesNotReplayOrSkip(AutoOffsetReset reset)
    {
        var topic = await kafka.CreateTopicWithRemoteLeaderAndLocalFollowerAsync();
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithAcks(Acks.All).BuildAsync();
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic, Partition = 0, Key = "key", Value = "0"
        });

        var clientId = $"follower-offset-{Guid.NewGuid():N}";
        var pool = new FollowerErrorPool(new ConnectionPool(clientId, new ConnectionOptions()), topic);
        var servers = kafka.BootstrapServers.Split(',');
        var metadata = new MetadataManager(pool, servers);
        await using var consumer = new KafkaConsumer<string, string>(new ConsumerOptions
        {
            BootstrapServers = servers,
            ClientId = clientId,
            ClientRack = "rack-a",
            AutoOffsetReset = reset,
            EnableFetchSessions = false
        }, Serializers.String, Serializers.String, pool, metadata);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await consumer.InitializeAsync(timeout.Token);
        consumer.IncrementalAssign([new TopicPartitionOffset(topic, 0, 0)]);

        try
        {
            var first = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), timeout.Token);
            await Assert.That(first).IsNotNull();
            await Assert.That(first!.Value.Offset).IsEqualTo(0);
            await pool.FollowerFetchStarted.Task.WaitAsync(timeout.Token);

            // The real leader selected rack-local broker 2. Hold that broker's offset-1
            // response until offsets 1 and 2 are acknowledged by the replicated cluster.
            // Inject only its error; all metadata, leader fetches and records are real.
            for (var offset = 1; offset <= 2; offset++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic, Partition = 0, Key = "key",
                    Value = offset.ToString(System.Globalization.CultureInfo.InvariantCulture)
                }, timeout.Token);
            }
            pool.ReleaseFollowerError.TrySetResult();

            for (var expected = 1; expected <= 2; expected++)
            {
                var record = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), timeout.Token);
                await Assert.That(record).IsNotNull();
                await Assert.That(record!.Value.Offset).IsEqualTo(expected);
                await Assert.That(record.Value.Value).IsEqualTo(expected.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            await Assert.That(await pool.LeaderRetried.Task.WaitAsync(timeout.Token)).IsEqualTo(1);
            await Assert.That(consumer.GetPosition(new TopicPartition(topic, 0))).IsEqualTo(3);
        }
        finally
        {
            pool.ReleaseFollowerError.TrySetResult();
            await timeout.CancelAsync();
        }
    }

    private sealed class FollowerErrorPool(ConnectionPool inner, string topic) : IConnectionPool
    {
        private int _faultStarted;
        private int _faultReturned;
        public TaskCompletionSource FollowerFetchStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFollowerError { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<long> LeaderRetried { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<IKafkaConnection> GetConnectionAsync(int brokerId, CancellationToken cancellationToken = default) =>
            new FaultingConnection(this, await inner.GetConnectionAsync(brokerId, cancellationToken), brokerId);

        public async ValueTask<IKafkaConnection> GetConnectionByIndexAsync(int brokerId, int index, CancellationToken cancellationToken = default) =>
            new FaultingConnection(this, await inner.GetConnectionByIndexAsync(brokerId, index, cancellationToken), brokerId);

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

        private async ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            IKafkaConnection connection, int brokerId, TRequest request, short apiVersion, CancellationToken cancellationToken)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            if (request is FetchRequest fetch)
            {
                foreach (var requestedTopic in fetch.Topics)
                {
                    if (requestedTopic.Topic != topic)
                        continue;
                    foreach (var partition in requestedTopic.Partitions)
                    {
                        if (partition.Partition != 0)
                            continue;
                        if (brokerId == 2 && partition.FetchOffset == 1
                            && Interlocked.CompareExchange(ref _faultStarted, 1, 0) == 0)
                        {
                            FollowerFetchStarted.TrySetResult();
                            await ReleaseFollowerError.Task.WaitAsync(cancellationToken);
                            Volatile.Write(ref _faultReturned, 1);
                            return (TResponse)(object)new FetchResponse
                            {
                                Responses =
                                [
                                    new FetchResponseTopic
                                    {
                                        Topic = topic,
                                        TopicId = requestedTopic.TopicId,
                                        Partitions =
                                        [
                                            new FetchResponsePartition
                                            {
                                                PartitionIndex = 0,
                                                ErrorCode = ErrorCode.OffsetOutOfRange,
                                                HighWatermark = 3,
                                                LastStableOffset = 3,
                                                LogStartOffset = 0,
                                                PreferredReadReplica = -1
                                            }
                                        ]
                                    }
                                ]
                            };
                        }
                        if (brokerId == 1 && Volatile.Read(ref _faultReturned) != 0)
                            LeaderRetried.TrySetResult(partition.FetchOffset);
                    }
                }
            }
            return await connection.SendAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);
        }

        private sealed class FaultingConnection(FollowerErrorPool owner, IKafkaConnection innerConnection, int brokerId) : IKafkaConnection
        {
            public int BrokerId => brokerId;
            public string Host => innerConnection.Host;
            public int Port => innerConnection.Port;
            public bool IsConnected => innerConnection.IsConnected;
            public ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
                where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse =>
                owner.SendAsync<TRequest, TResponse>(innerConnection, brokerId, request, apiVersion, cancellationToken);
            public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
                where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse =>
                innerConnection.SendFireAndForgetAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);
            public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
                where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse =>
                SendAsync<TRequest, TResponse>(request, apiVersion, cancellationToken).AsTask();
            public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
                where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse =>
                innerConnection.SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);
            public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
                where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse =>
                SendAsync<TRequest, TResponse>(request, apiVersion, cancellationToken).AsTask();
            public ValueTask ConnectAsync(CancellationToken cancellationToken = default) => innerConnection.ConnectAsync(cancellationToken);
            public ValueTask DisposeAsync() => innerConnection.DisposeAsync();
        }
    }
}
