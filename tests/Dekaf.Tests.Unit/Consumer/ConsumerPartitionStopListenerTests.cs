using Dekaf.Consumer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumerPartitionStopListenerTests
{
    [Test]
    public async Task CloseAsync_InvokesPartitionStopListenerWithCurrentAssignment()
    {
        var listener = new TrackingPartitionStopListener();
        await using var consumer = CreateConsumer(listener);
        var partition = new TopicPartition("topic-a", 0);
        consumer.Assign(partition);

        await consumer.CloseAsync(CancellationToken.None);

        await Assert.That(listener.StoppedPartitions).Count().IsEqualTo(1);
        await Assert.That(listener.StoppedPartitions[0]).IsEquivalentTo([partition]);
        await Assert.That(consumer.Assignment).IsEmpty();
    }

    [Test]
    public async Task DisposeAsync_InvokesPartitionStopListenerWithCurrentAssignment()
    {
        var listener = new TrackingPartitionStopListener();
        var consumer = CreateConsumer(listener);
        var partition = new TopicPartition("topic-a", 1);
        consumer.Assign(partition);

        await consumer.DisposeAsync();

        await Assert.That(listener.StoppedPartitions).Count().IsEqualTo(1);
        await Assert.That(listener.StoppedPartitions[0]).IsEquivalentTo([partition]);
        await Assert.That(consumer.Assignment).IsEmpty();
    }

    [Test]
    public async Task CloseAsync_PassesCancellationTokenToPartitionStopListener()
    {
        var listener = new TrackingPartitionStopListener
        {
            OnStopped = (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.CompletedTask;
            }
        };
        await using var consumer = CreateConsumer(listener);
        consumer.Assign(new TopicPartition("topic-a", 0));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.That(async () => await consumer.CloseAsync(cts.Token))
            .Throws<OperationCanceledException>();
        await Assert.That(listener.CancellationTokens).Count().IsEqualTo(1);
        await Assert.That(listener.CancellationTokens[0].IsCancellationRequested).IsTrue();
        await Assert.That(consumer.Assignment).IsEmpty();
    }

    [Test]
    public async Task CloseAsync_SuppressesPartitionStopListenerNonCancellationException()
    {
        var listener = new TrackingPartitionStopListener
        {
            OnStopped = (_, _) => throw new InvalidOperationException("stop failed")
        };
        await using var consumer = CreateConsumer(listener);
        consumer.Assign(new TopicPartition("topic-a", 0));

        await Assert.That(async () => await consumer.CloseAsync(CancellationToken.None))
            .ThrowsNothing();
        await Assert.That(listener.StoppedPartitions).Count().IsEqualTo(1);
        await Assert.That(consumer.Assignment).IsEmpty();
    }

    private static KafkaConsumer<string, string> CreateConsumer(IRebalanceListener listener)
    {
        return new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                OffsetCommitMode = OffsetCommitMode.Manual,
                QueuedMinMessages = 1,
                RebalanceListener = listener
            },
            Serializers.String,
            Serializers.String);
    }

    private sealed class TrackingPartitionStopListener : IRebalanceListener, IPartitionStopListener
    {
        public List<List<TopicPartition>> StoppedPartitions { get; } = [];
        public List<CancellationToken> CancellationTokens { get; } = [];
        public Func<IEnumerable<TopicPartition>, CancellationToken, ValueTask>? OnStopped { get; init; }

        public ValueTask OnPartitionsAssignedAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask OnPartitionsRevokedAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask OnPartitionsLostAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask OnPartitionsStoppedAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            StoppedPartitions.Add(partitions.ToList());
            CancellationTokens.Add(cancellationToken);
            return OnStopped is null
                ? ValueTask.CompletedTask
                : OnStopped(partitions, cancellationToken);
        }
    }
}
