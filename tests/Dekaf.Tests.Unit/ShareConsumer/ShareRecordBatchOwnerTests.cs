using Dekaf.Protocol.Records;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed class ShareRecordBatchOwnerTests
{
    [Test]
    [NotInParallel]
    public async Task WarmOwnerReuse_DoesNotAllocateAcrossManyPolls()
    {
        var pool = new ShareRecordBatchOwner.Pool(new TopicPartition("topic", 0));
        for (var index = 0; index < 64; index++)
        {
            var owner = pool.Rent(RecordBatch.RentFromPool());
            owner.Release();
            owner.Release();
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 10000; index++)
        {
            var owner = pool.Rent(RecordBatch.RentFromPool());
            owner.Release();
            owner.Release();
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        await Assert.That(allocated).IsEqualTo(0L);
        await Assert.That(pool.Misses).IsEqualTo(1L);
    }

    [Test]
    [Arguments(500, 0, 512)]
    [Arguments(1024, 128, 2048)]
    public async Task MultiplePolls_RetainAllOwnersIncludingRenewedBatches(int batchCount, int renewedCount, int expectedCapacity)
    {
        var pool = new ShareRecordBatchOwner.Pool(new TopicPartition("topic", 0));
        var renewed = new ShareRecordBatchOwner[renewedCount];
        for (var index = 0; index < renewed.Length; index++)
        {
            var owner = pool.Rent(RecordBatch.RentFromPool());
            owner.Retain();
            owner.Release();
            owner.Release();
            renewed[index] = owner;
        }

        var owners = new ShareRecordBatchOwner[batchCount];
        try
        {
            for (var poll = 0; poll < 70; poll++)
            {
                for (var index = 0; index < owners.Length; index++)
                    owners[index] = pool.Rent(RecordBatch.RentFromPool());
                foreach (var owner in owners)
                {
                    owner.Release();
                    owner.Release();
                }
                Array.Clear(owners);
                await Assert.That(pool.Misses).IsEqualTo((long)batchCount + renewedCount);
                await Assert.That(pool.MaxPoolSize).IsEqualTo(expectedCapacity);
            }
        }
        finally
        {
            foreach (var owner in owners)
            {
                if (owner is null)
                    continue;
                owner.Release();
                owner.Release();
            }
            foreach (var owner in renewed)
                owner.Release();
        }
    }

    [Test]
    public async Task Reuse_PreservesMetadataAndRejectsStaleOwner()
    {
        var pool = new ShareRecordBatchOwner.Pool(new TopicPartition("topic", 0));
        var originalOwner = pool.Rent(RecordBatch.RentFromPool());
        var original = CreateRecord(short.MinValue);
        original.AttachBatchOwner(originalOwner);
        originalOwner.Release();
        originalOwner.Release();

        var owner = pool.Rent(RecordBatch.RentFromPool());
        try
        {
            var current = CreateRecord(short.MaxValue);
            current.AttachBatchOwner(owner);
            await Assert.That(owner).IsSameReferenceAs(originalOwner);
            await Assert.That(original.Topic).IsEqualTo("topic");
            await Assert.That(original.Partition).IsEqualTo(0);
            await Assert.That(current.Partition).IsEqualTo(0);
            await Assert.That(original.DeliveryCount).IsEqualTo((int)short.MinValue);
            await Assert.That(current.DeliveryCount).IsEqualTo((int)short.MaxValue);
            await Assert.That(current.BatchOwner).IsSameReferenceAs(owner);
            foreach (var type in Enum.GetValues<AcknowledgeType>())
            {
                current.AcknowledgeType = type;
                await Assert.That(current.AcknowledgeType).IsEqualTo(type);
                await Assert.That(current.BatchOwner).IsSameReferenceAs(owner);
            }
            await Assert.That(() => { _ = original.BatchOwner; }).Throws<InvalidOperationException>();
        }
        finally
        {
            owner.Release();
            owner.Release();
        }
    }

    [Test]
    public async Task Renewal_KeepsOwnerOutOfPool()
    {
        var pool = new ShareRecordBatchOwner.Pool(new TopicPartition("topic", 0));
        var owner = pool.Rent(RecordBatch.RentFromPool());
        owner.Retain();
        owner.Release();
        owner.Release();
        var next = pool.Rent(RecordBatch.RentFromPool());
        try
        {
            await Assert.That(next).IsNotSameReferenceAs(owner);
        }
        finally
        {
            owner.Release();
            next.Release();
            next.Release();
        }
    }

    [Test]
    public async Task ExhaustedGeneration_RetiresOwnerInsteadOfWrapping()
    {
        var pool = new ShareRecordBatchOwner.Pool(new TopicPartition("topic", 0));
        var first = pool.Rent(RecordBatch.RentFromPool());
        typeof(ShareRecordBatchOwner).GetProperty("Generation",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(first, ShareRecordBatchOwner.MaximumGeneration - 1);
        first.Release();
        first.Release();
        var current = pool.Rent(RecordBatch.RentFromPool());
        await Assert.That(current).IsSameReferenceAs(first);
        await Assert.That(current.Generation).IsEqualTo(ShareRecordBatchOwner.MaximumGeneration);
        current.Release();
        current.Release();

        var next = pool.Rent(RecordBatch.RentFromPool());
        try
        {
            await Assert.That(next).IsNotSameReferenceAs(first);
            await Assert.That(next.Generation).IsEqualTo(1u);
            await Assert.That(first.Retain).Throws<InvalidOperationException>();
        }
        finally
        {
            next.Release();
            next.Release();
        }
    }

    [Test]
    [Arguments(int.MinValue, 1u)]
    [Arguments(-1, (uint)int.MaxValue - 1)]
    [Arguments(int.MaxValue, (uint)int.MaxValue)]
    public async Task GenerationStorage_PreservesFullPartitionAndDeliveryMetadata(int partition, uint generation)
    {
        var pool = new ShareRecordBatchOwner.Pool(new TopicPartition("topic", partition));
        var owner = pool.Rent(RecordBatch.RentFromPool());
        typeof(ShareRecordBatchOwner).GetProperty("Generation",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(owner, generation);
        var record = CreateRecord(int.MaxValue, partition);
        await Assert.That(record.Partition).IsEqualTo(partition);
        record.AttachBatchOwner(owner);
        try
        {
            await Assert.That(record.Partition).IsEqualTo(partition);
            await Assert.That(record.DeliveryCount).IsEqualTo(int.MaxValue);
            await Assert.That(record.BatchOwner).IsSameReferenceAs(owner);
        }
        finally
        {
            owner.Release();
            owner.Release();
        }
        await Assert.That(record.Topic).IsEqualTo("topic");
        await Assert.That(record.Partition).IsEqualTo(partition);
    }

    [Test]
    [Arguments(int.MinValue)]
    [Arguments(-1)]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(int.MaxValue)]
    public async Task CallerCreatedRecord_PreservesFullDeliveryCount(int count)
    {
        var record = CreateRecord(count);
        await Assert.That(record.DeliveryCount).IsEqualTo(count);
    }

    private static ShareConsumeResult<int, int> CreateRecord(int deliveryCount, int partition = 0) => new()
    {
        Topic = "topic", Partition = partition, Offset = 7, Value = 42, DeliveryCount = deliveryCount
    };
}
