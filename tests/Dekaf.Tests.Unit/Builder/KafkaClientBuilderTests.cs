using System.Reflection;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Builder;

public sealed class KafkaClientBuilderTests
{
    [Test]
    public async Task RootClient_CreatedClients_ShareConnectionPoolAndMetadata()
    {
        await using var client = Kafka.Connect("broker1:9092,broker2:9092");
        await using var producer = client.CreateProducer<string, string>().Build();
        await using var consumer = client.CreateConsumer<string, string>("group-a").Build();
        await using var admin = client.CreateAdminClient().Build();

        var producerPool = GetField<ConnectionPool>(producer, "_connectionPool");
        var consumerPool = GetField<IConnectionPool>(consumer, "_connectionPool");
        var adminPool = GetField<IConnectionPool>(admin, "_connectionPool");

        await Assert.That(ReferenceEquals(producerPool, consumerPool)).IsTrue();
        await Assert.That(ReferenceEquals(producerPool, adminPool)).IsTrue();

        var producerMetadata = GetField<MetadataManager>(producer, "_metadataManager");
        var consumerMetadata = GetField<MetadataManager>(consumer, "_metadataManager");
        var adminMetadata = GetField<MetadataManager>(admin, "_metadataManager");

        await Assert.That(ReferenceEquals(producerMetadata, consumerMetadata)).IsTrue();
        await Assert.That(ReferenceEquals(producerMetadata, adminMetadata)).IsTrue();
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

        await Assert.That(() => client.CreateConsumer<string, string>().UseTls())
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateConsumer<string, string>().WithSaslPlain("user", "pass"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateConsumer<string, string>().WithConnectionsPerBroker(2))
            .Throws<InvalidOperationException>();

        await Assert.That(() => client.CreateShareConsumer<string, string>().WithTls())
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateShareConsumer<string, string>().WithSaslPlain("user", "pass"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateShareConsumer<string, string>().WithSocketReceiveBufferBytes(4096))
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateShareConsumer<string, string>().WithConnectionsPerBroker(2))
            .Throws<InvalidOperationException>();

        await Assert.That(() => client.CreateAdminClient().UseTls())
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateAdminClient().WithSaslPlain("user", "pass"))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task RootClient_SharedConnectionPool_UsesAdaptiveFetchResponseBufferCeiling()
    {
        await using var client = Kafka.Connect("localhost:9092");
        await using var producer = client.CreateProducer<string, string>().Build();

        var pool = GetField<ConnectionPool>(producer, "_connectionPool");
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
}
