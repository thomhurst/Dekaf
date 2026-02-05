using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

[ClassDataSource<KafkaTestContainer>(Shared = SharedType.PerTestSession)]
public class RebalanceListenerTests(KafkaTestContainer kafka)
{
    [Test]
    public async Task OnPartitionsAssigned_CalledWhenConsumerSubscribes()
    {
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";
        var listener = new TestRebalanceListener();

        // Produce a message first so the consumer has something to join for
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value"
        });

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRebalanceListener(listener)
            .Build();

        consumer.Subscribe(topic);

        // Consume one message to trigger the rebalance
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(listener.AssignedCallCount).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task OnPartitionsRevoked_CalledWhenConsumerCloses()
    {
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";
        var listener = new TestRebalanceListener();

        // Produce a message
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value"
        });

        var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRebalanceListener(listener)
            .Build();

        consumer.Subscribe(topic);

        // Consume to join the group
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Close the consumer which should trigger revocation
        await consumer.DisposeAsync();

        await Assert.That(listener.RevokedCallCount).IsGreaterThanOrEqualTo(1);
    }

    private sealed class TestRebalanceListener : IRebalanceListener
    {
        private int _assignedCount;
        private int _revokedCount;
        private int _lostCount;

        public int AssignedCallCount => _assignedCount;
        public int RevokedCallCount => _revokedCount;
        public int LostCallCount => _lostCount;

        public ValueTask OnPartitionsAssignedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _assignedCount);
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsRevokedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _revokedCount);
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsLostAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _lostCount);
            return ValueTask.CompletedTask;
        }
    }
}
