using Dekaf.Consumer;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Consumer;

public class PendingFetchDataTests
{
    [Test]
    public async Task Constructor_WithActivityName_UsesCachedValue()
    {
        // Arrange
        const string topic = "test-topic";
        const string activityName = "test-topic receive";

        using var pending = PendingFetchData.Create(
            topic,
            partitionIndex: 0,
            batches: Array.Empty<RecordBatch>(),
            activityName: activityName);

        // Assert - uses the provided cached activity name
        await Assert.That(pending.ActivityName).IsEqualTo(activityName);
    }

    [Test]
    public async Task Constructor_WithoutActivityName_GeneratesActivityName()
    {
        // Arrange
        const string topic = "my-topic";

        using var pending = PendingFetchData.Create(
            topic,
            partitionIndex: 0,
            batches: Array.Empty<RecordBatch>());

        // Assert - falls back to string interpolation
        await Assert.That(pending.ActivityName).IsEqualTo("my-topic receive");
    }

    [Test]
    public async Task Constructor_WithActivityName_AvoidsDuplicateAllocation()
    {
        // Arrange - pre-compute the activity name (simulating the cache)
        const string topic = "shared-topic";
        var activityName = $"{topic} receive";

        using var pending1 = PendingFetchData.Create(
            topic,
            partitionIndex: 0,
            batches: Array.Empty<RecordBatch>(),
            activityName: activityName);

        using var pending2 = PendingFetchData.Create(
            topic,
            partitionIndex: 1,
            batches: Array.Empty<RecordBatch>(),
            activityName: activityName);

        // Assert - both instances share the exact same string reference
        await Assert.That(ReferenceEquals(pending1.ActivityName, pending2.ActivityName)).IsTrue();
    }

    [Test]
    public async Task Create_AfterDispose_ReusesPooledInstance()
    {
        // Arrange: create and dispose a PendingFetchData to return it to pool
        var first = PendingFetchData.Create(
            "topic-1",
            partitionIndex: 0,
            batches: Array.Empty<RecordBatch>());
        first.Dispose();

        // Act: create another, which should reuse the pooled instance
        using var second = PendingFetchData.Create(
            "topic-2",
            partitionIndex: 1,
            batches: Array.Empty<RecordBatch>());

        // Assert: the second instance has the correct new state
        await Assert.That(second.Topic).IsEqualTo("topic-2");
        await Assert.That(second.PartitionIndex).IsEqualTo(1);
        await Assert.That(second.LastYieldedOffset).IsEqualTo(-1L);
        await Assert.That(second.TotalBytesConsumed).IsEqualTo(0L);
        await Assert.That(second.MessageCount).IsEqualTo(0L);
    }

    [Test]
    public async Task RatchetPoolSize_IncreasesMaxPoolSize()
    {
        var before = PendingFetchData.MaxPoolSizeValue;

        PendingFetchData.RatchetPoolSize(before + 100);
        await Assert.That(PendingFetchData.MaxPoolSizeValue).IsGreaterThanOrEqualTo(before + 100);
    }

    [Test]
    public async Task RatchetPoolSize_DoesNotDecrease()
    {
        // Ratchet to a known high value first to avoid ordering dependency with other tests
        PendingFetchData.RatchetPoolSize(50_000);
        var current = PendingFetchData.MaxPoolSizeValue;

        // Try to ratchet down — should be no-op
        PendingFetchData.RatchetPoolSize(1);
        await Assert.That(PendingFetchData.MaxPoolSizeValue).IsEqualTo(current);
    }

    [Test]
    public async Task Create_WithAbortedTransactions_ReusesClearedDictionary()
    {
        // Arrange: first instance has aborted transactions, dispose returns to pool
        var abortedTxns = new[] { new Dekaf.Protocol.Messages.AbortedTransaction { ProducerId = 1, FirstOffset = 10 } };
        var first = PendingFetchData.Create(
            "topic",
            partitionIndex: 0,
            batches: Array.Empty<RecordBatch>(),
            abortedTransactions: abortedTxns);
        first.Dispose();

        // Act: second instance without aborted transactions (reuses pooled dictionary, now empty)
        using var second = PendingFetchData.Create(
            "topic",
            partitionIndex: 0,
            batches: Array.Empty<RecordBatch>());

        // Assert: instance is usable and has correct state
        await Assert.That(second.Topic).IsEqualTo("topic");
        await Assert.That(second.LastYieldedOffset).IsEqualTo(-1L);
    }
}
