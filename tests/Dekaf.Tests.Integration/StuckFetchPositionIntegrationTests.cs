using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

[Category("Consumer")]
public sealed class StuckFetchPositionIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task EmptyParsedFetchesAtSameOffset_SurfaceFatalConsumeError()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using (var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync())
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "key",
                Value = "value"
            }, CancellationToken.None);
        }

        var options = new ConsumerOptions
        {
            BootstrapServers = [KafkaContainer.BootstrapServers],
            AutoOffsetReset = AutoOffsetReset.Earliest,
            OffsetCommitMode = OffsetCommitMode.Manual,
            QueuedMinMessages = 1,
            EnableFetchSessions = false,
            RequestTimeoutMs = 10_000
        };
        var connectionOptions = new ConnectionOptions
        {
            RequestTimeout = TimeSpan.FromMilliseconds(options.RequestTimeoutMs)
        };
        var injector = new EmptyFetchInjector();
        var connectionPool = new ConnectionPool(
            options.ClientId,
            connectionOptions,
            options.ConnectionsPerBroker,
            async (brokerId, host, port, _, cancellationToken) =>
            {
                var connection = new KafkaConnection(
                    brokerId,
                    host,
                    port,
                    options.ClientId,
                    connectionOptions);
                await connection.ConnectAsync(cancellationToken);
                return new EmptyFetchConnection(connection, injector);
            });
        var metadataManager = new MetadataManager(connectionPool, options.BootstrapServers);

        await using var consumer = new KafkaConsumer<string, string>(
            options,
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager,
            GlobalTestSetup.GetLoggerFactory());
        await consumer.InitializeAsync();
        consumer.Assign(new TopicPartition(topic, 0));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var exception = await Assert.ThrowsAsync<ConsumeException>(async () =>
            await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(20), timeout.Token));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.CorruptMessage);
        await Assert.That(exception.IsRetriable).IsFalse();
        await Assert.That(injector.MutatedFetchCount).IsGreaterThanOrEqualTo(3);
    }

    private sealed class EmptyFetchInjector
    {
        private int _mutatedFetchCount;

        public int MutatedFetchCount => Volatile.Read(ref _mutatedFetchCount);

        public void Mutate(FetchResponse response)
        {
            var mutated = false;
            foreach (var topic in response.Responses)
            {
                foreach (var partition in topic.Partitions)
                {
                    if (partition.Records is not List<RecordBatch> records || records.Count == 0)
                    {
                        continue;
                    }

                    records.Clear();
                    mutated = true;
                }
            }

            if (mutated)
            {
                Interlocked.Increment(ref _mutatedFetchCount);
            }
        }
    }

    private sealed class EmptyFetchConnection(
        IKafkaConnection inner,
        EmptyFetchInjector injector) : IKafkaConnection
    {
        public int BrokerId => inner.BrokerId;
        public string Host => inner.Host;
        public int Port => inner.Port;
        public bool IsConnected => inner.IsConnected;

        public async ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            var response = await inner.SendAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);
            MutateFetchResponse(response);
            return response;
        }

        public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => inner.SendFireAndForgetAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);

        public async Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            var response = await inner.SendPipelinedAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);
            MutateFetchResponse(response);
            return response;
        }

        public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => inner.SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);

        public async Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            var response = await inner.SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(
                request,
                apiVersion,
                cancellationToken);
            MutateFetchResponse(response);
            return response;
        }

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
            => inner.ConnectAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();

        private void MutateFetchResponse<TResponse>(TResponse response)
            where TResponse : IKafkaResponse
        {
            if (response is FetchResponse fetchResponse)
            {
                injector.Mutate(fetchResponse);
            }
        }
    }
}
