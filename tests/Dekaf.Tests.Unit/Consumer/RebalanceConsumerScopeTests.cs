using Dekaf.Consumer;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class RebalanceConsumerScopeTests
{
    [Test]
    public async Task PublicSurface_ExcludesUnsafeConsumerOperations()
    {
        var methodNames = typeof(IRebalanceConsumer).GetMethods()
            .Select(static method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        await Assert.That(methodNames.Contains("ConsumeAsync")).IsFalse();
        await Assert.That(methodNames.Contains("ConsumeOneAsync")).IsFalse();
        await Assert.That(methodNames.Contains("CloseAsync")).IsFalse();
        await Assert.That(methodNames.Contains("DisposeAsync")).IsFalse();
        await Assert.That(methodNames.Contains("Subscribe")).IsFalse();
        await Assert.That(methodNames.Contains("Unsubscribe")).IsFalse();
        await Assert.That(methodNames.Contains("Assign")).IsFalse();
        await Assert.That(methodNames.Contains("Unassign")).IsFalse();
        await Assert.That(methodNames.Contains("IncrementalAssign")).IsFalse();
        await Assert.That(methodNames.Contains("IncrementalUnassign")).IsFalse();
    }

    [Test]
    public async Task AssignedSeek_IsStagedAndVisibleDuringCallback()
    {
        var partition = new TopicPartition("topic", 1);
        var positions = Substitute.For<IConsumerPositions>();
        var consumer = Substitute.For<IKafkaConsumer<byte[], byte[]>>();
        consumer.Positions.Returns(positions);
        TopicPartitionOffset? staged = null;
        var scope = new RebalanceConsumerScope<byte[], byte[]>(
            consumer,
            [partition],
            [partition],
            offset => staged = offset,
            requested => requested == partition ? staged?.Offset : null);

        scope.Seek(new TopicPartitionOffset("topic", 1, 42));

        await Assert.That(staged?.Offset).IsEqualTo(42L);
        await Assert.That(scope.GetPosition(partition)).IsEqualTo(42L);
        positions.DidNotReceiveWithAnyArgs().Seek(default);
    }

    [Test]
    public async Task Invalidate_RejectsRetainedViewOperations()
    {
        var consumer = Substitute.For<IKafkaConsumer<byte[], byte[]>>();
        consumer.Positions.Returns(Substitute.For<IConsumerPositions>());
        var scope = new RebalanceConsumerScope<byte[], byte[]>(consumer, [], []);
        scope.Invalidate();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await scope.CommitAsync());

        await Assert.That(exception!.Message).Contains("callback has completed");
        Assert.Throws<InvalidOperationException>(() => _ = scope.Assignment);
    }
}
