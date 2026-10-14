using System.Reflection;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// A batch that swaps its arena must rent the replacement before returning the current one.
/// Renting can throw (OutOfMemoryException from the POH allocation). Returning first hands the
/// current arena to the shared pool, or drops its buffer when the pool cannot retain it, so a
/// failed rent left the batch writing into a buffer it no longer owned.
/// </summary>
/// <remarks>
/// The tests make the rent fail for real: an <see cref="ProducerOptions.ArenaCapacity"/> beyond
/// the maximum array length makes the replacement allocation throw OutOfMemoryException. The
/// current arena's pooled-capacity cap is below its length, so returning it drops its buffer
/// deterministically, whatever the shared pool's limits are.
/// </remarks>
[NotInParallel(nameof(BatchArena))]
public class BatchArenaSwapFailureTests
{
    private const int BatchSize = 16 * 1024;

    private static readonly FieldInfo s_arenaField = typeof(PartitionBatch).GetField(
        "_arena",
        BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Test]
    public async Task FirstRecordArenaSwap_RentFails_BatchKeepsItsArena()
    {
        var batch = CreateBatchWithUnpoolableArena(BatchSize, out var original);
        // Larger than the current arena, so the first append swaps in an expanded one.
        var oversizedValue = new byte[BatchSize * 2];

        await Assert.That(() => Append(batch, oversizedValue)).Throws<OutOfMemoryException>();

        await Assert.That(ReferenceEquals(s_arenaField.GetValue(batch), original)).IsTrue();
        await Assert.That(original.Buffer).IsNotNull();

        // The batch still owns a usable arena and releases it cleanly.
        var result = Append(batch, new byte[16]);
        await Assert.That(result.Success).IsTrue();
        await Assert.That(batch.RecordCount).IsEqualTo(1);
        batch.ReleaseForDiscard();
        await Assert.That(s_arenaField.GetValue(batch)).IsNull();
    }

    [Test]
    public async Task NormalArenaSwap_RentFails_BatchKeepsItsArena()
    {
        // An arena below the normal capacity makes EnsureNormalArenaCapacity swap it.
        var batch = CreateBatchWithUnpoolableArena(1024, out var undersized);

        await Assert.That(() => batch.SetSplitBatchSizeLimit(BatchSize)).Throws<OutOfMemoryException>();

        await Assert.That(ReferenceEquals(s_arenaField.GetValue(batch), undersized)).IsTrue();
        await Assert.That(undersized.Buffer).IsNotNull();

        var result = Append(batch, new byte[16]);
        await Assert.That(result.Success).IsTrue();
        await Assert.That(batch.RecordCount).IsEqualTo(1);
        batch.ReleaseForDiscard();
        await Assert.That(s_arenaField.GetValue(batch)).IsNull();
    }

    private static PartitionBatch CreateBatchWithUnpoolableArena(int arenaCapacity, out BatchArena arena)
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = BatchSize,
            // Beyond Array.MaxLength: renting a normal arena throws OutOfMemoryException.
            ArenaCapacity = int.MaxValue,
            LingerMs = 0,
        };
        var batch = new PartitionBatch(
            new TopicPartition("arena-swap", 0),
            options,
            compressionRatioEstimator: null,
            allocateAppendBuffer: false);

        // A pooled-capacity cap below the length makes ReturnToPool drop the buffer.
        arena = new BatchArena(arenaCapacity, maxPooledCapacity: 1);
        s_arenaField.SetValue(batch, arena);
        return batch;
    }

    private static RecordAppendResult Append(PartitionBatch batch, byte[] value) =>
        batch.TryAppendFromSpans(
            timestamp: 0,
            keyData: default,
            keyIsNull: true,
            valueData: value,
            valueIsNull: false,
            headers: null,
            headerCount: 0,
            completionSource: null,
            callback: null,
            estimatedSize: 0);
}
