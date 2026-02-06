using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit;

public sealed class KafkaFactoryTests
{
    #region CreateProducer

    [Test]
    public async Task CreateProducer_Generic_ReturnsBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task CreateProducer_WithBootstrapServers_ReturnsProducer()
    {
        var producer = Kafka.CreateProducer<string, string>("localhost:9092");
        await Assert.That(producer).IsNotNull();
        await producer.DisposeAsync();
    }

    [Test]
    public async Task CreateProducer_ImplementsIKafkaProducer()
    {
        var producer = Kafka.CreateProducer<string, string>("localhost:9092");
        await Assert.That(producer).IsAssignableTo<IKafkaProducer<string, string>>();
        await producer.DisposeAsync();
    }

    #endregion

    #region CreateTopicProducer

    [Test]
    public async Task CreateTopicProducer_ReturnsTopicProducer()
    {
        await using var producer = Kafka.CreateTopicProducer<string, string>("localhost:9092", "my-topic");
        await Assert.That(producer).IsNotNull();
        await Assert.That(producer.Topic).IsEqualTo("my-topic");
    }

    [Test]
    public async Task CreateTopicProducer_ImplementsITopicProducer()
    {
        await using var producer = Kafka.CreateTopicProducer<string, string>("localhost:9092", "my-topic");
        await Assert.That(producer).IsAssignableTo<ITopicProducer<string, string>>();
    }

    #endregion

    #region CreateConsumer

    [Test]
    public async Task CreateConsumer_Generic_ReturnsBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task CreateConsumer_WithBootstrapAndGroupId_ReturnsConsumer()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>("localhost:9092", "my-group");
        await Assert.That(consumer).IsNotNull();
    }

    [Test]
    public async Task CreateConsumer_WithBootstrapAndGroupId_ImplementsIKafkaConsumer()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>("localhost:9092", "my-group");
        await Assert.That(consumer).IsAssignableTo<IKafkaConsumer<string, string>>();
    }

    [Test]
    public async Task CreateConsumer_WithTopics_ReturnsConsumerWithSubscription()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>("localhost:9092", "my-group", "topic-a", "topic-b");
        await Assert.That(consumer).IsNotNull();
        await Assert.That(consumer.Subscription).Contains("topic-a");
        await Assert.That(consumer.Subscription).Contains("topic-b");
    }

    #endregion

    #region CreateAdminClient

    [Test]
    public async Task CreateAdminClient_Generic_ReturnsBuilder()
    {
        var builder = Kafka.CreateAdminClient();
        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task CreateAdminClient_WithBootstrapServers_ReturnsAdminClient()
    {
        await using var admin = Kafka.CreateAdminClient("localhost:9092");
        await Assert.That(admin).IsNotNull();
    }

    #endregion
}
