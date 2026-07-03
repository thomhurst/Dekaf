using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit;

public sealed class KafkaFactoryTests
{
    [Test]
    public async Task CreateProducer_ReturnsBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();

        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task CreateProducer_BuilderCanBuildProducer()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        await Assert.That(producer).IsAssignableTo<IKafkaProducer<string, string>>();
    }

    [Test]
    public async Task CreateProducer_BuilderCanBuildTopicProducer()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .BuildForTopic("my-topic");

        await Assert.That(producer).IsAssignableTo<ITopicProducer<string, string>>();
        await Assert.That(producer.Topic).IsEqualTo("my-topic");
    }

    [Test]
    public async Task CreateConsumer_ReturnsBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();

        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task CreateConsumer_BuilderCanBuildConsumerWithSubscription()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("my-group")
            .SubscribeTo("topic-a", "topic-b")
            .Build();

        await Assert.That(consumer).IsAssignableTo<IKafkaConsumer<string, string>>();
        await Assert.That(consumer.Subscription).Contains("topic-a");
        await Assert.That(consumer.Subscription).Contains("topic-b");
    }

    [Test]
    public async Task CreateShareConsumer_ReturnsBuilder()
    {
        var builder = Kafka.CreateShareConsumer<string, string>();

        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task CreateShareConsumer_BuilderCanBuildShareConsumer()
    {
        await using var consumer = Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("my-group")
            .SubscribeTo("topic-a")
            .Build();

        await Assert.That(consumer).IsAssignableTo<IKafkaShareConsumer<string, string>>();
    }

    [Test]
    public async Task CreateAdminClient_ReturnsBuilder()
    {
        var builder = Kafka.CreateAdminClient();

        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task CreateAdminClient_BuilderCanBuildAdminClient()
    {
        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers("localhost:9092")
            .Build();

        await Assert.That(admin).IsAssignableTo<IAdminClient>();
    }
}
