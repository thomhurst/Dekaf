using System.Reflection;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.Builder;

public sealed class BuilderClientIdDefaultTests
{
    [Test]
    public async Task ProducerBuilder_WithoutClientId_UsesOptionsDefault()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092").Build();

        await Assert.That(GetPrivateOptions<ProducerOptions>(producer).ClientId).IsEqualTo("dekaf-producer");
    }

    [Test]
    public async Task ProducerBuilder_WithClientId_UsesConfiguredValue()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092").WithClientId("orders-producer").Build();

        await Assert.That(GetPrivateOptions<ProducerOptions>(producer).ClientId).IsEqualTo("orders-producer");
    }

    [Test]
    public async Task ConsumerBuilder_WithoutClientId_UsesOptionsDefault()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092").WithGroupId("group").Build();

        await Assert.That(GetPrivateOptions<ConsumerOptions>(consumer).ClientId).IsEqualTo("dekaf-consumer");
    }

    [Test]
    public async Task ConsumerBuilder_WithClientId_UsesConfiguredValue()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092").WithGroupId("group").WithClientId("orders-consumer").Build();

        await Assert.That(GetPrivateOptions<ConsumerOptions>(consumer).ClientId).IsEqualTo("orders-consumer");
    }

    [Test]
    public async Task ShareConsumerBuilder_WithoutClientId_UsesOptionsDefault()
    {
        await using var consumer = Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers("localhost:9092").WithGroupId("group").Build();

        await Assert.That(GetPrivateOptions<ShareConsumerOptions>(consumer).ClientId).IsEqualTo("dekaf-share-consumer");
    }

    [Test]
    public async Task ShareConsumerBuilder_WithClientId_UsesConfiguredValue()
    {
        await using var consumer = Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers("localhost:9092").WithGroupId("group").WithClientId("orders-share").Build();

        await Assert.That(GetPrivateOptions<ShareConsumerOptions>(consumer).ClientId).IsEqualTo("orders-share");
    }

    [Test]
    public async Task AdminClientBuilder_WithoutClientId_UsesOptionsDefault()
    {
        await using var admin = Kafka.CreateAdminClient().WithBootstrapServers("localhost:9092").Build();

        await Assert.That(GetPrivateOptions<AdminClientOptions>(admin).ClientId).IsEqualTo("dekaf-admin");
    }

    [Test]
    public async Task AdminClientBuilder_WithClientId_UsesConfiguredValue()
    {
        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers("localhost:9092").WithClientId("orders-admin").Build();

        await Assert.That(GetPrivateOptions<AdminClientOptions>(admin).ClientId).IsEqualTo("orders-admin");
    }

    private static TOptions GetPrivateOptions<TOptions>(object client)
    {
        var optionsField = client.GetType().GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Could not find _options on {client.GetType()}");
        return (TOptions)optionsField.GetValue(client)!;
    }
}
