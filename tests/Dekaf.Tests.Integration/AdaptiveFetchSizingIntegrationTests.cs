using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

[Category("Consumer")]
public sealed class AdaptiveFetchSizingIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task FetchLoop_AdjustsRequestsWithinLimitsAndDeliversEveryOffset(bool memoryPressure, CancellationToken cancellationToken)
    {
        const int count = 96;
        var topic = await KafkaContainer.CreateTestTopicAsync();
        await using (var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithBatchSize(16 * 1024).BuildAsync(cancellationToken))
        {
            for (var index = 0; index < count; index++)
                await producer.FireAsync(topic, index.ToString(System.Globalization.CultureInfo.InvariantCulture), new string('x', 8192));
            await producer.FlushAsync(cancellationToken);
        }

        var options = new AdaptiveFetchSizingOptions
        {
            MinPartitionFetchBytes = 32 * 1024, InitialPartitionFetchBytes = 64 * 1024, MaxPartitionFetchBytes = 256 * 1024,
            MinFetchMaxBytes = 64 * 1024, InitialFetchMaxBytes = 128 * 1024, MaxFetchMaxBytes = 512 * 1024,
            StableWindowCount = 1,
            // Make the grow signal insensitive to runner scheduling; pressure still takes precedence.
            GrowThreshold = 10000, ShrinkThreshold = 20000
        };
        var clientId = $"adaptive-fetch-{Guid.NewGuid():N}";
        await using var pool = new FetchObservingPool(new ConnectionPool(clientId, new ConnectionOptions()));
        await using var metadata = new MetadataManager(pool, [KafkaContainer.BootstrapServers]);
        await using var consumer = new KafkaConsumer<string, string>(new ConsumerOptions
        {
            BootstrapServers = [KafkaContainer.BootstrapServers], ClientId = clientId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAdaptiveFetchSizing = true, AdaptiveFetchSizingOptions = options,
            QueuedMinMessages = memoryPressure ? 1000 : 1, QueuedMaxMessagesKbytes = 32,
            EnableFetchSessions = false, PrefetchPipelineDepth = 1
        }, Serializers.String, Serializers.String, pool, metadata);
        await consumer.InitializeAsync(cancellationToken);
        consumer.Assign(new TopicPartition(topic, 0));

        var first = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), cancellationToken);
        await Assert.That(first).IsNotNull();
        await Assert.That(first!.Value.Offset).IsEqualTo(0);
        if (memoryPressure)
        {
            await TestWait.WaitForConditionAsync(() => consumer.CaptureDiagnosticSnapshot().PrefetchedBytes >= 32 * 1024,
                TimeSpan.FromSeconds(10), description: "actual prefetch fills the configured byte budget");
        }
        for (var expected = 1; expected < count; expected++)
        {
            var record = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), cancellationToken);
            await Assert.That(record).IsNotNull();
            await Assert.That(record!.Value.Offset).IsEqualTo(expected);
            await Assert.That(record.Value.Key).IsEqualTo(expected.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        var observed = pool.Requests.ToArray();
        await Assert.That(observed.Length).IsGreaterThan(1);
        foreach (var request in observed)
        {
            await Assert.That(request.PartitionBytes).IsGreaterThanOrEqualTo(options.MinPartitionFetchBytes);
            await Assert.That(request.PartitionBytes).IsLessThanOrEqualTo(options.MaxPartitionFetchBytes);
            await Assert.That(request.TotalBytes).IsGreaterThanOrEqualTo(options.MinFetchMaxBytes);
            await Assert.That(request.TotalBytes).IsLessThanOrEqualTo(options.MaxFetchMaxBytes);
        }
        await Assert.That(memoryPressure
            ? observed.Any(request => request.PartitionBytes < options.InitialPartitionFetchBytes)
            : observed.Any(request => request.PartitionBytes > options.InitialPartitionFetchBytes)).IsTrue()
            .Because($"Fetch requests must {(memoryPressure ? "shrink after real memory pressure" : "grow after processing")}: {string.Join(", ", observed)}");
        await Assert.That(consumer.GetPosition(new TopicPartition(topic, 0))).IsEqualTo(count);
    }

    private sealed class FetchObservingPool(ConnectionPool inner) : IConnectionPool
    {
        public ConcurrentQueue<(int TotalBytes, int PartitionBytes)> Requests { get; } = new();
        public async ValueTask<IKafkaConnection> GetConnectionAsync(int brokerId, CancellationToken cancellationToken = default) =>
            new ObservedConnection(this, await inner.GetConnectionAsync(brokerId, cancellationToken));
        public async ValueTask<IKafkaConnection> GetConnectionByIndexAsync(int brokerId, int index, CancellationToken cancellationToken = default) =>
            new ObservedConnection(this, await inner.GetConnectionByIndexAsync(brokerId, index, cancellationToken));
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

        private sealed class ObservedConnection(FetchObservingPool owner, IKafkaConnection connection) : IKafkaConnection
        {
            public int BrokerId => connection.BrokerId;
            public string Host => connection.Host;
            public int Port => connection.Port;
            public bool IsConnected => connection.IsConnected;
            public ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
                where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse
            {
                Observe(request);
                return connection.SendAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);
            }
            public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
                where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse
            {
                Observe(request);
                return connection.SendPipelinedAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);
            }
            public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
                where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse
            {
                Observe(request);
                return connection.SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);
            }

            private void Observe<TRequest>(TRequest request)
            {
                if (request is FetchRequest fetch)
                    foreach (var topic in fetch.Topics)
                        foreach (var partition in topic.Partitions)
                            owner.Requests.Enqueue((fetch.MaxBytes, partition.PartitionMaxBytes));
            }

            public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
                where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => connection.SendFireAndForgetAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);
            public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
                where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => connection.SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);
            public ValueTask ConnectAsync(CancellationToken cancellationToken = default) => connection.ConnectAsync(cancellationToken);
            public ValueTask DisposeAsync() => connection.DisposeAsync();
        }
    }
}
