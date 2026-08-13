using Dekaf.Consumer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumerGroupLivenessTests
{
    [Test]
    public async Task ManualAssignmentWithConfiguredGroup_DoesNotReportGroupParticipation()
    {
        await using var consumer = CreateConsumer();
        consumer.Assign(new TopicPartition("test-topic", 0));

        var liveness = ((IConsumerGroupLiveness)consumer).GroupLiveness;

        await Assert.That(liveness.HasConsumerGroup).IsFalse();
        await Assert.That(liveness.IsJoined).IsFalse();
    }

    [Test]
    public async Task SubscriptionWithConfiguredGroup_ReportsGroupParticipation()
    {
        await using var consumer = CreateConsumer();
        consumer.Subscribe("test-topic");

        var liveness = ((IConsumerGroupLiveness)consumer).GroupLiveness;

        await Assert.That(liveness.HasConsumerGroup).IsTrue();
        await Assert.That(liveness.IsJoined).IsFalse();
    }

    private static KafkaConsumer<string, string> CreateConsumer() => new(
        new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "liveness-test-consumer",
            GroupId = "liveness-test-group"
        },
        Serializers.String,
        Serializers.String);
}
