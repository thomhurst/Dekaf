using System.Buffers;
using Dekaf.Compression;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed class ShareRecordBufferPoolTests
{
    [Test]
    [Arguments(CompressionType.None)]
    [Arguments(CompressionType.Gzip)]
    public async Task BatchPayload_SurvivesMaterializedRecordCleanupAndPoolDisposal(CompressionType compression)
    {
        var bytes = new ArrayBufferWriter<byte>();
        byte[] payload = [10, 20, 30, 40];
        using (var source = new RecordBatch
        {
            Records = [new Record { IsKeyNull = true, Value = payload,
                Headers = [new Header("key", payload)], HeaderCount = 1 }]
        })
            source.Write(bytes, compression);
        using var pool = new ShareRecordBufferPool(1024 * 1024);
        var reader = new KafkaProtocolReader(bytes.WrittenMemory);
        var batch = RecordBatch.ReadForShareConsumer(ref reader, null, pool);
        var owners = new ShareRecordBatchOwner.Pool(new TopicPartition("topic", 0));
        var owner = owners.Rent(batch);
        try
        {
            var record = batch.Records[0];
            var headerValue = record.Headers![0].Value;
            owner.CompleteParsing();
            pool.Dispose();
            await Assert.That(record.Value.ToArray()).IsEquivalentTo(payload);
            await Assert.That(headerValue.ToArray()).IsEquivalentTo(payload);
        }
        finally
        {
            owner.Release();
        }
        await Assert.That(pool.RetainedBytes).IsEqualTo(0);
    }

    [Test]
    public async Task CacheLimit_DoesNotReclaimOutstandingPayloads()
    {
        using var pool = new ShareRecordBufferPool(32);
        var first = pool.Rent(16);
        var second = pool.Rent(16);
        var outstanding = pool.Rent(16);
        outstanding[0] = 42;
        pool.Return(first);
        pool.Return(second);
        await Assert.That(pool.RetainedBytes).IsEqualTo(32);
        await Assert.That(outstanding[0]).IsEqualTo((byte)42);
        pool.Return(outstanding);
        await Assert.That(pool.RetainedBytes).IsEqualTo(32);
        var reused = pool.Rent(16);
        await Assert.That(reused).IsSameReferenceAs(second);
        pool.Return(reused);
    }

    [Test]
    public async Task Dispose_ClearsCacheAndDoesNotRetainLateReturns()
    {
        var pool = new ShareRecordBufferPool(64);
        var retained = pool.Rent(16);
        var outstanding = pool.Rent(16);
        pool.Return(retained);
        pool.Dispose();
        pool.Return(outstanding);
        pool.Dispose();
        await Assert.That(pool.RetainedBytes).IsEqualTo(0);
        await Assert.That(() => pool.Rent(16)).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task Rent_RoundsUpAndReturnCanClearPayload()
    {
        using var pool = new ShareRecordBufferPool(128);
        var array = pool.Rent(17);
        array[0] = 42;
        pool.Return(array, clearArray: true);
        var reused = pool.Rent(31);
        await Assert.That(reused).IsSameReferenceAs(array);
        await Assert.That(reused[0]).IsEqualTo((byte)0);
        pool.Return(reused);
        pool.Return(pool.Rent(0));
        await Assert.That(pool.RetainedBytes).IsEqualTo(32);
    }

    [Test]
    [NotInParallel]
    public async Task WarmCache_ReusesMoreThanSharedPoolDepthWithoutAllocating()
    {
        using var pool = new ShareRecordBufferPool(1024 * 16);
        var arrays = new byte[1024][];
        for (var round = 0; round < 2; round++)
        {
            for (var index = 0; index < arrays.Length; index++)
                arrays[index] = pool.Rent(16);
            foreach (var array in arrays)
                pool.Return(array);
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < arrays.Length; index++)
            arrays[index] = pool.Rent(16);
        foreach (var array in arrays)
            pool.Return(array);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        await Assert.That(allocated).IsEqualTo(0L);
        await Assert.That(pool.RetainedBytes).IsEqualTo(1024 * 16);
    }
}
