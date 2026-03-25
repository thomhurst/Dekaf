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

        using var pending = new PendingFetchData(
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

        using var pending = new PendingFetchData(
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

        using var pending1 = new PendingFetchData(
            topic,
            partitionIndex: 0,
            batches: Array.Empty<RecordBatch>(),
            activityName: activityName);

        using var pending2 = new PendingFetchData(
            topic,
            partitionIndex: 1,
            batches: Array.Empty<RecordBatch>(),
            activityName: activityName);

        // Assert - both instances share the exact same string reference
        await Assert.That(ReferenceEquals(pending1.ActivityName, pending2.ActivityName)).IsTrue();
    }
}
