using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

[Category("ConsumerGroup")]
public class RebalanceListenerTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task OnPartitionsAssigned_CalledWhenConsumerSubscribes()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";
        var listener = new TestRebalanceListener();

        // Produce a message first so the consumer has something to join for
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value"
        });

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRebalanceListener(listener)
            .BuildAsync();

        consumer.Subscribe(topic);

        // Consume one message to trigger the rebalance
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(listener.AssignedCallCount).IsGreaterThanOrEqualTo(1);
    }

    private sealed class TestRebalanceListener : IRebalanceListener
    {
        private int _assignedCount;

        public int AssignedCallCount => _assignedCount;

        public ValueTask OnPartitionsAssignedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _assignedCount);
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsRevokedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsLostAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }
    }
}
