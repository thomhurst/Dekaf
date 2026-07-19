using System.Reflection;
using System.Net.Security;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Unit.Builder;

public sealed class KafkaClientBuilderTests
{
    [Test]
    public async Task RootClient_MetadataClusterCheck_IsAppliedToSharedMetadataManager()
    {
        await using var client = new KafkaClientBuilder()
            .WithBootstrapServers("localhost:9092")
            .WithMetadataClusterCheck(false)
            .Build();
        await using var producer = client.CreateProducer<string, string>().Build();

        var manager = GetField<MetadataManager>(producer, "_metadataManager");
        var options = GetField<MetadataOptions>(manager, "_options");

        await Assert.That(options.MetadataClusterCheckEnabled).IsFalse();
    }

    [Test]
    public async Task RootClient_CreatedClients_ShareMetadataAndUseRoleAppropriateConnectionPools()
    {
        await using var client = Kafka.Connect("broker1:9092,broker2:9092");
        await using var producer = client.CreateProducer<string, string>().Build();
        await using var consumer = client.CreateConsumer<string, string>("group-a").Build();
        await using var admin = client.CreateAdminClient().Build();

        var producerPool = GetField<ConnectionPool>(producer, "_connectionPool");
        var consumerPool = GetField<IConnectionPool>(consumer, "_connectionPool");
        var adminPool = GetField<IConnectionPool>(admin, "_connectionPool");

        await Assert.That(ReferenceEquals(producerPool, consumerPool)).IsFalse();
        await Assert.That(ReferenceEquals(producerPool, adminPool)).IsTrue();
        await Assert.That(GetField<bool>(producerPool, "_responseMemoryAdmissionsEnabled")).IsFalse();
        await Assert.That(GetField<bool>(consumerPool, "_responseMemoryAdmissionsEnabled")).IsTrue();

        var producerMetadata = GetField<MetadataManager>(producer, "_metadataManager");
        var consumerMetadata = GetField<MetadataManager>(consumer, "_metadataManager");
        var adminMetadata = GetField<MetadataManager>(admin, "_metadataManager");

        await Assert.That(ReferenceEquals(producerMetadata, consumerMetadata)).IsTrue();
        await Assert.That(ReferenceEquals(producerMetadata, adminMetadata)).IsTrue();
    }

    [Test]
    public async Task RootClient_CreatedProducer_UsesDefaultAdaptiveMaximumOf3()
    {
        await using var client = Kafka.Connect("localhost:9092");
        await using var producer = client.CreateProducer<string, string>().Build();

        var options = GetField<ProducerOptions>(producer, "_options");

        await Assert.That(options.MaxConnectionsPerBroker).IsEqualTo(3);
    }

    [Test]
    public async Task RootClient_StaticSasl_CopiesProducerAuthModeWithoutCredentials()
    {
        await using var client = Kafka.Connect("localhost:9092", builder => builder
            .WithSaslScramSha256("user", "password"));
        await using var producer = client.CreateProducer<string, string>().Build();

        var options = GetField<ProducerOptions>(producer, "_options");

        await Assert.That(options.SaslMechanism).IsEqualTo(SaslMechanism.ScramSha256);
        await Assert.That(options.UsesDynamicSaslCredentials).IsFalse();
        await Assert.That(options.SaslUsername).IsNull();
        await Assert.That(options.SaslPassword).IsNull();
    }

    [Test]
    public async Task RootClient_DynamicSasl_CopiesProducerAuthModeWithoutProvider()
    {
        await using var client = Kafka.Connect("localhost:9092", builder => builder
            .WithSaslPlain(static _ => ValueTask.FromResult(new SaslCredentials("user", "password"))));
        await using var producer = client.CreateProducer<string, string>().Build();

        var options = GetField<ProducerOptions>(producer, "_options");

        await Assert.That(options.SaslMechanism).IsEqualTo(SaslMechanism.Plain);
        await Assert.That(options.UsesDynamicSaslCredentials).IsTrue();
        await Assert.That(options.SaslCredentialProvider is null).IsTrue();
    }

    [Test]
    public async Task RootClient_Gssapi_CreatesProducerUsingSharedConfiguration()
    {
        await using var client = Kafka.Connect("localhost:9092", builder => builder
            .WithGssapi(new GssapiConfig()));
        await using var producer = client.CreateProducer<string, string>().Build();

        var options = GetField<ProducerOptions>(producer, "_options");

        await Assert.That(options.SaslMechanism).IsEqualTo(SaslMechanism.Gssapi);
        await Assert.That(options.GssapiConfig).IsNull();
    }

    [Test]
    public async Task RootClient_CreatedConsumer_UsesDefaultAdaptiveMaximumOf4()
    {
        await using var client = Kafka.Connect("localhost:9092");
        await using var consumer = client.CreateConsumer<string, string>().Build();

        var options = GetField<ConsumerOptions>(consumer, "_options");

        await Assert.That(options.MaxConnectionsPerBroker).IsEqualTo(4);
    }

    [Test]
    public async Task RootClient_InitialWidthAboveImplicitMaximum_ExpandsMaximum()
    {
        await using var client = Kafka.Connect()
            .WithBootstrapServers("localhost:9092")
            .WithConnectionsPerBroker(5)
            .Build();
        await using var producer = client.CreateProducer<string, string>().Build();

        var options = GetField<ProducerOptions>(producer, "_options");

        await Assert.That(options.ConnectionsPerBroker).IsEqualTo(5);
        await Assert.That(options.MaxConnectionsPerBroker).IsEqualTo(5);
    }

    [Test]
    public async Task RootClient_InitialWidthAboveExplicitMaximum_ThrowsOnBuild()
    {
        await Assert.That(() => Kafka.Connect()
                .WithBootstrapServers("localhost:9092")
                .WithMaxConnectionsPerBroker(3)
                .WithConnectionsPerBroker(5)
                .Build())
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task RootClient_CreatedProducer_DisposeDoesNotDisposeSharedPool()
    {
        var client = Kafka.Connect("localhost:9092");
        var producer = client.CreateProducer<string, string>().Build();
        var pool = GetField<ConnectionPool>(producer, "_connectionPool");

        await producer.DisposeAsync();

        await Assert.That(GetField<int>(pool, "_disposed")).IsEqualTo(0);

        await client.DisposeAsync();

        await Assert.That(GetField<int>(pool, "_disposed")).IsEqualTo(1);
    }

    [Test]
    public async Task RootClient_CreatedConsumer_DisposeDoesNotDisposeSharedConsumerPool()
    {
        var client = Kafka.Connect("localhost:9092");
        var consumer = client.CreateConsumer<string, string>().Build();
        var pool = GetField<ConnectionPool>(consumer, "_connectionPool");

        await consumer.DisposeAsync();

        await Assert.That(GetField<int>(pool, "_disposed")).IsEqualTo(0);

        await client.DisposeAsync();

        await Assert.That(GetField<int>(pool, "_disposed")).IsEqualTo(1);
    }

    [Test]
    public async Task RootClient_CreatedBuilders_RejectBootstrapOverrides()
    {
        await using var client = Kafka.Connect("localhost:9092");

        await Assert.That(() => client.CreateProducer<string, string>().WithBootstrapServers("other:9092"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateConsumer<string, string>().WithBootstrapServers("other:9092"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateShareConsumer<string, string>().WithBootstrapServers("other:9092"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateAdminClient().WithBootstrapServers("other:9092"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateAdminClient().WithBootstrapControllers("controller:9093"))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task RootClient_CreatedBuilders_RejectConnectionOverrides()
    {
        await using var client = Kafka.Connect("localhost:9092");

        await Assert.That(() => client.CreateProducer<string, string>().UseTls())
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateProducer<string, string>().WithSaslPlain("user", "pass"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateProducer<string, string>().WithSocketSendBufferBytes(4096))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateProducer<string, string>().WithConnectionsPerBroker(2))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateProducer<string, string>().WithMetadataRecoveryStrategy(MetadataRecoveryStrategy.None))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateProducer<string, string>().WithMetadataClusterCheck(false))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateProducer<string, string>().WithMetadataRecoveryRebootstrapTrigger(TimeSpan.FromMinutes(1)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateProducer<string, string>().WithMetadataMaxAge(TimeSpan.FromMinutes(1)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateProducer<string, string>().WithReconnectBackoff(TimeSpan.FromMilliseconds(100)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateProducer<string, string>().WithRetryBackoff(TimeSpan.FromMilliseconds(100)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateProducer<string, string>().WithConnectionsMaxIdle(TimeSpan.FromMinutes(1)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateProducer<string, string>().WithConnectionTimeout(TimeSpan.FromSeconds(1)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateProducer<string, string>().WithConnectionTimeoutMax(TimeSpan.FromSeconds(2)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateProducer<string, string>().WithTcpKeepAlive(false))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateProducer<string, string>().WithRemoteCertificateValidationCallback(AcceptCertificate))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateProducer<string, string>().WithClientDnsLookup(ClientDnsLookup.ResolveCanonicalBootstrapServersOnly))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateProducer<string, string>()
                .WithDnsResolver(new ClientDnsEndpointResolver(new SystemDnsLookup())))
            .Throws<InvalidOperationException>();

        await Assert.That(() => client.CreateConsumer<string, string>().UseTls())
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateConsumer<string, string>().WithSaslPlain("user", "pass"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateConsumer<string, string>().WithConnectionsPerBroker(2))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateConsumer<string, string>().WithMetadataRecoveryStrategy(MetadataRecoveryStrategy.None))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateConsumer<string, string>().WithMetadataClusterCheck(false))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateConsumer<string, string>().WithMetadataRecoveryRebootstrapTrigger(TimeSpan.FromMinutes(1)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateConsumer<string, string>().WithMetadataMaxAge(TimeSpan.FromMinutes(1)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateConsumer<string, string>().WithReconnectBackoff(TimeSpan.FromMilliseconds(100)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateConsumer<string, string>().WithRetryBackoff(TimeSpan.FromMilliseconds(100)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateConsumer<string, string>().WithConnectionsMaxIdle(TimeSpan.FromMinutes(1)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateConsumer<string, string>().WithConnectionTimeout(TimeSpan.FromSeconds(1)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateConsumer<string, string>().WithConnectionTimeoutMax(TimeSpan.FromSeconds(2)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateConsumer<string, string>().WithTcpKeepAlive(false))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateConsumer<string, string>().WithRemoteCertificateValidationCallback(AcceptCertificate))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateConsumer<string, string>().WithClientDnsLookup(ClientDnsLookup.ResolveCanonicalBootstrapServersOnly))
            .Throws<InvalidOperationException>();

        await Assert.That(() => client.CreateShareConsumer<string, string>().WithTls())
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateShareConsumer<string, string>().WithSaslPlain("user", "pass"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateShareConsumer<string, string>().WithSocketReceiveBufferBytes(4096))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateShareConsumer<string, string>().WithConnectionsPerBroker(2))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateShareConsumer<string, string>().WithMetadataClusterCheck(false))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateShareConsumer<string, string>().WithReconnectBackoff(TimeSpan.FromMilliseconds(100)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateShareConsumer<string, string>().WithRetryBackoff(TimeSpan.FromMilliseconds(100)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateShareConsumer<string, string>().WithConnectionsMaxIdle(TimeSpan.FromMinutes(1)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateShareConsumer<string, string>().WithConnectionTimeout(TimeSpan.FromSeconds(1)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateShareConsumer<string, string>().WithConnectionTimeoutMax(TimeSpan.FromSeconds(2)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateShareConsumer<string, string>().WithTcpKeepAlive(false))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateShareConsumer<string, string>().WithRemoteCertificateValidationCallback(AcceptCertificate))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateShareConsumer<string, string>().WithClientDnsLookup(ClientDnsLookup.ResolveCanonicalBootstrapServersOnly))
            .Throws<InvalidOperationException>();

        await Assert.That(() => client.CreateAdminClient().UseTls())
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateAdminClient().WithSaslPlain("user", "pass"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateAdminClient().WithMetadataRecoveryStrategy(MetadataRecoveryStrategy.None))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateAdminClient().WithMetadataClusterCheck(false))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateAdminClient().WithMetadataRecoveryRebootstrapTrigger(TimeSpan.FromMinutes(1)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateAdminClient().WithMetadataMaxAge(TimeSpan.FromMinutes(1)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateAdminClient().WithReconnectBackoff(TimeSpan.FromMilliseconds(100)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateAdminClient().WithRetryBackoff(TimeSpan.FromMilliseconds(100)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateAdminClient().WithConnectionsMaxIdle(TimeSpan.FromMinutes(1)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateAdminClient().WithConnectionTimeout(TimeSpan.FromSeconds(1)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateAdminClient().WithConnectionTimeoutMax(TimeSpan.FromSeconds(2)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateAdminClient().WithTcpKeepAlive(false))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateAdminClient().WithRemoteCertificateValidationCallback(AcceptCertificate))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateAdminClient().WithClientDnsLookup(ClientDnsLookup.ResolveCanonicalBootstrapServersOnly))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task RootClient_WithReconnectBackoff_ConfiguresSharedConnectionPool()
    {
        await using var client = Kafka.Connect("localhost:9092", builder => builder
            .WithReconnectBackoff(TimeSpan.FromMilliseconds(123))
            .WithReconnectBackoffMax(TimeSpan.FromMilliseconds(456)));

        await using var producer = client.CreateProducer<string, string>().Build();

        var pool = GetField<ConnectionPool>(producer, "_connectionPool");

        await Assert.That(pool.EffectiveConnectionOptions.ReconnectBackoff).IsEqualTo(TimeSpan.FromMilliseconds(123));
        await Assert.That(pool.EffectiveConnectionOptions.ReconnectBackoffMax).IsEqualTo(TimeSpan.FromMilliseconds(456));
    }

    [Test]
    public async Task RootClient_WithRetryBackoff_ConfiguresCreatedClients()
    {
        await using var client = Kafka.Connect("localhost:9092", builder => builder
            .WithRetryBackoff(TimeSpan.FromMilliseconds(123))
            .WithRetryBackoffMax(TimeSpan.FromMilliseconds(456)));
        await using var producer = client.CreateProducer<string, string>().Build();
        await using var consumer = client.CreateConsumer<string, string>("group").Build();

        var producerOptions = GetField<ProducerOptions>(producer, "_options");
        var consumerOptions = GetField<ConsumerOptions>(consumer, "_options");

        await Assert.That(producerOptions.RetryBackoffMs).IsEqualTo(123);
        await Assert.That(producerOptions.RetryBackoffMaxMs).IsEqualTo(456);
        await Assert.That(consumerOptions.RetryBackoffMs).IsEqualTo(123);
        await Assert.That(consumerOptions.RetryBackoffMaxMs).IsEqualTo(456);
    }

    [Test]
    public async Task RootClient_WithConnectionsMaxIdle_ConfiguresSharedConnectionPool()
    {
        await using var client = Kafka.Connect("localhost:9092", builder => builder
            .WithConnectionsMaxIdle(TimeSpan.FromMilliseconds(1234)));

        await using var producer = client.CreateProducer<string, string>().Build();

        var pool = GetField<ConnectionPool>(producer, "_connectionPool");

        await Assert.That(pool.EffectiveConnectionOptions.ConnectionsMaxIdleMs).IsEqualTo(1234);
    }

    [Test]
    public async Task RootClient_WithClientDnsLookup_ConfiguresSharedConnectionPool()
    {
        await using var client = Kafka.Connect("localhost:9092", builder => builder
            .WithClientDnsLookup(ClientDnsLookup.ResolveCanonicalBootstrapServersOnly));
        await using var producer = client.CreateProducer<string, string>().Build();

        var pool = GetField<ConnectionPool>(producer, "_connectionPool");

        await Assert.That(pool.EffectiveConnectionOptions.ClientDnsLookup)
            .IsEqualTo(ClientDnsLookup.ResolveCanonicalBootstrapServersOnly);
    }

    [Test]
    public async Task RootClient_ConsumerConnectionPool_UsesAdaptiveFetchResponseBufferCeiling()
    {
        await using var client = Kafka.Connect("localhost:9092");
        await using var consumer = client.CreateConsumer<string, string>().Build();

        var pool = GetField<ConnectionPool>(consumer, "_connectionPool");
        var responseBufferPool = GetField<ResponseBufferPool>(pool, "_responseBufferPool");

        await Assert.That(responseBufferPool.MaxArrayLength)
            .IsEqualTo(200 * 1024 * 1024 + ResponseBufferPool.ProtocolOverheadBytes);
    }

    [Test]
    public async Task MetadataManager_BrokerCountCallbacks_AreMulticast()
    {
        await using var metadataManager = new MetadataManager(
            connectionPool: null!,
            bootstrapServers: ["localhost:9092"]);
        var first = 0;
        var second = 0;

        metadataManager.AddBrokerCountDiscoveredCallback(count => first = count);
        metadataManager.AddBrokerCountDiscoveredCallback(count => second = count);
        metadataManager.NotifyBrokerCountDiscovered(3);

        await Assert.That(first).IsEqualTo(3);
        await Assert.That(second).IsEqualTo(3);
    }

    [Test]
    public async Task RootClient_CreatedProducer_DisposeRemovesBrokerCountCallback()
    {
        await using var client = Kafka.Connect("localhost:9092");
        var producer = client.CreateProducer<string, string>().Build();
        var metadataManager = GetField<MetadataManager>(producer, "_metadataManager");

        await Assert.That(metadataManager.BrokerCountDiscoveredCallbackCount).IsEqualTo(1);

        await producer.DisposeAsync();

        await Assert.That(metadataManager.BrokerCountDiscoveredCallbackCount).IsEqualTo(0);
    }

    [Test]
    public async Task RootClient_WithMemoryBudget_UsesIndependentProducerBudget()
    {
        await using var client = Kafka.Connect("localhost:9092", builder =>
            builder.WithMemoryBudget(768UL * 1024 * 1024));
        await using var first = client.CreateProducer<string, string>().Build();
        await using var second = client.CreateProducer<string, string>().Build();

        var firstAccumulator = GetField<RecordAccumulator>(first, "_accumulator");
        var secondAccumulator = GetField<RecordAccumulator>(second, "_accumulator");

        await Assert.That(firstAccumulator.MaxBufferMemory).IsEqualTo(64UL * 1024 * 1024);
        await Assert.That(secondAccumulator.MaxBufferMemory).IsEqualTo(64UL * 1024 * 1024);
    }

    private static T GetField<T>(object instance, string name)
    {
        var field = instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Could not find {name} on {instance.GetType()}");
        return (T)field.GetValue(instance)!;
    }

    private static bool AcceptCertificate(
        object sender,
        System.Security.Cryptography.X509Certificates.X509Certificate? certificate,
        System.Security.Cryptography.X509Certificates.X509Chain? chain,
        SslPolicyErrors sslPolicyErrors) => sslPolicyErrors == SslPolicyErrors.None;
}
