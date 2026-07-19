using System.Net.Sockets;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Internal;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;
using NSubstitute;

namespace Dekaf.Tests.Unit.Metadata;

public sealed class BootstrapResolutionClientTests
{
    [Test]
    public async Task ProducerInitializeAsync_TransientDnsFailure_Recovers()
    {
        var infrastructure = CreateRecoveryInfrastructure();
        await using var pool = infrastructure.Pool;
        await using var metadataManager = infrastructure.MetadataManager;
        await using var producer = new KafkaProducer<string, string>(
            new ProducerOptions
            {
                BootstrapServers = ["recovering-host:9092"],
                EnableIdempotence = false
            },
            Serializers.String,
            Serializers.String,
            pool,
            metadataManager,
            DekafMemoryBudget.Global);

        await producer.InitializeAsync();

        await Assert.That(infrastructure.GetAttempts()).IsEqualTo(2);
    }

    [Test]
    public async Task ConsumerInitializeAsync_TransientDnsFailure_Recovers()
    {
        var infrastructure = CreateRecoveryInfrastructure();
        await using var pool = infrastructure.Pool;
        await using var metadataManager = infrastructure.MetadataManager;
        await using var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["recovering-host:9092"],
                GroupId = "group"
            },
            Serializers.String,
            Serializers.String,
            pool,
            metadataManager,
            DekafMemoryBudget.Global);

        await consumer.InitializeAsync();

        await Assert.That(infrastructure.GetAttempts()).IsEqualTo(2);
    }

    [Test]
    public async Task ShareConsumerInitializeAsync_TransientDnsFailure_Recovers()
    {
        var infrastructure = CreateRecoveryInfrastructure();
        await using var pool = infrastructure.Pool;
        await using var metadataManager = infrastructure.MetadataManager;
        await using var consumer = new KafkaShareConsumer<string, string>(
            new ShareConsumerOptions
            {
                BootstrapServers = ["recovering-host:9092"],
                GroupId = "group"
            },
            Serializers.String,
            Serializers.String,
            pool,
            metadataManager);

        await consumer.InitializeAsync();

        await Assert.That(infrastructure.GetAttempts()).IsEqualTo(2);
    }

    [Test]
    public async Task AdminProgress_TransientDnsFailure_Recovers()
    {
        var infrastructure = CreateRecoveryInfrastructure();
        await using var pool = infrastructure.Pool;
        await using var metadataManager = infrastructure.MetadataManager;
        await using var admin = new AdminClient(
            new AdminClientOptions { BootstrapServers = ["recovering-host:9092"] },
            pool,
            metadataManager);

        _ = await admin.ListTopicsAsync();

        await Assert.That(infrastructure.GetAttempts()).IsEqualTo(2);
    }

    private static RecoveryInfrastructure CreateRecoveryInfrastructure()
    {
        var connection = Substitute.For<IKafkaConnection>();
        connection.IsConnected.Returns(true);
        connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                Arg.Any<ApiVersionsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys =
                [
                    new ApiVersion(
                        ApiKey.Metadata,
                        MetadataRequest.LowestSupportedVersion,
                        MetadataRequest.HighestSupportedVersion)
                ]
            }));
        connection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new MetadataResponse
            {
                Brokers = [new BrokerMetadata { NodeId = 1, Host = "recovering-host", Port = 9092 }],
                Topics = []
            }));

        var attempts = 0;
        var pool = new ConnectionPool(
            clientId: null,
            new ConnectionOptions
            {
                ReconnectBackoff = TimeSpan.Zero,
                ReconnectBackoffMax = TimeSpan.Zero
            },
            connectionsPerBroker: 1,
            connectionFactory: (_, host, port, _, _) => Interlocked.Increment(ref attempts) == 1
                ? ValueTask.FromException<IKafkaConnection>(new DnsResolutionException(
                    host,
                    port,
                    new SocketException((int)SocketError.HostNotFound)))
                : ValueTask.FromResult(connection));
        var metadataManager = new MetadataManager(
            pool,
            ["recovering-host:9092"],
            new MetadataOptions
            {
                EnableBackgroundRefresh = false,
                InitTimeoutMs = 1000,
                MaxInitRetries = 0,
                BootstrapResolveTimeoutMs = 1000,
                RetryBackoffMs = 0,
                RetryBackoffMaxMs = 0
            });

        return new RecoveryInfrastructure(pool, metadataManager, () => Volatile.Read(ref attempts));
    }

    private sealed record RecoveryInfrastructure(
        ConnectionPool Pool,
        MetadataManager MetadataManager,
        Func<int> GetAttempts);
}
