using Dekaf.Producer;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Producer;

public sealed class PartitionCarryOverExpiryTests
{
    [Test]
    public async Task MayContainExpiredBatch_TracksEarliestBatch()
    {
        const long Now = 10_000;
        const long DeliveryTimeoutTicks = 1_000;
        var carryOver = new BrokerSender.PartitionCarryOver();

        carryOver.Add(CreateBatchReference(partition: 0, createdTicks: 9_500));

        await Assert.That(carryOver.MayContainExpiredBatch(Now, DeliveryTimeoutTicks)).IsFalse();

        carryOver.Add(CreateBatchReference(partition: 1, createdTicks: 8_500));

        await Assert.That(carryOver.MayContainExpiredBatch(Now, DeliveryTimeoutTicks)).IsTrue();
    }

    [Test]
    public async Task MayContainExpiredBatch_EmptyAfterDrain_ReturnsFalse()
    {
        const long Now = 10_000;
        var carryOver = new BrokerSender.PartitionCarryOver();
        carryOver.Add(CreateBatchReference(partition: 0, createdTicks: 1));

        carryOver.DrainTo([]);

        await Assert.That(carryOver.Count).IsEqualTo(0);
        await Assert.That(carryOver.MayContainExpiredBatch(Now, deliveryTimeoutTicks: 1)).IsFalse();
    }

    private static BrokerSender.BatchReference CreateBatchReference(int partition, long createdTicks)
    {
        var batch = new ReadyBatch();
        batch.Initialize(
            new TopicPartition("carry-over-expiry", partition),
            new RecordBatch { Records = [] },
            completionSourcesArray: null,
            completionSourcesCount: 0,
            recordCount: 0,
            dataSize: 0,
            createdStopwatchTimestamp: createdTicks);
        batch.MarkPreSerialized();
        return new BrokerSender.BatchReference(batch, batch.Generation);
    }
}
