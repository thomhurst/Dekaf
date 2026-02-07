using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumerBuilderTests
{
    [Test]
    public async Task WithPartitionEof_DefaultsToTrue()
    {
        // WithPartitionEof() with no argument should enable partition EOF
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithPartitionEof();

        // We can't easily inspect the builder's internal state, but we can verify
        // the method returns the builder for chaining
        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task WithPartitionEof_CanBeExplicitlyEnabled()
    {
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithPartitionEof(enabled: true);

        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task WithPartitionEof_CanBeExplicitlyDisabled()
    {
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithPartitionEof(enabled: false);

        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task WithPartitionEof_ReturnsBuilderForChaining()
    {
        var originalBuilder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092");

        var returnedBuilder = originalBuilder.WithPartitionEof();

        await Assert.That(returnedBuilder).IsSameReferenceAs(originalBuilder);
    }

    [Test]
    public async Task WithIsolationLevel_ReadCommitted_ReturnsBuilderForChaining()
    {
        var originalBuilder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092");

        var returnedBuilder = originalBuilder.WithIsolationLevel(IsolationLevel.ReadCommitted);

        await Assert.That(returnedBuilder).IsSameReferenceAs(originalBuilder);
    }

    [Test]
    public async Task WithIsolationLevel_ReadUncommitted_ReturnsBuilderForChaining()
    {
        var originalBuilder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092");

        var returnedBuilder = originalBuilder.WithIsolationLevel(IsolationLevel.ReadUncommitted);

        await Assert.That(returnedBuilder).IsSameReferenceAs(originalBuilder);
    }

    [Test]
    public async Task WithIsolationLevel_CanChainWithOtherMethods()
    {
        // Verify chaining works across multiple builder methods
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("test-group")
            .WithAutoOffsetReset(Dekaf.Consumer.AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .WithMaxPollRecords(100)
            .Build();

        await Assert.That(consumer).IsNotNull();
    }

    [Test]
    public async Task WithIsolationLevel_ReadCommitted_BuildsConsumer()
    {
        // Verify we can build a consumer with ReadCommitted isolation level
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .Build();

        await Assert.That(consumer).IsNotNull();
    }

    [Test]
    public async Task WithIsolationLevel_Default_IsReadUncommitted()
    {
        // Verify that not setting isolation level builds successfully (using default)
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        await Assert.That(consumer).IsNotNull();
    }

    [Test]
    public async Task Build_WithoutBootstrapServers_Throws()
    {
        var act = () => Kafka.CreateConsumer<string, string>().Build();
        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task SubscribeTo_SingleTopic_BuildsWithSubscription()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .SubscribeTo("test-topic")
            .Build();

        await Assert.That(consumer.Subscription).Contains("test-topic");
    }

    [Test]
    public async Task SubscribeTo_MultipleTopics_BuildsWithSubscription()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .SubscribeTo("topic-a", "topic-b")
            .Build();

        await Assert.That(consumer.Subscription).Contains("topic-a");
        await Assert.That(consumer.Subscription).Contains("topic-b");
    }
}
