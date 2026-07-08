using Dekaf.Metadata;
using Dekaf.Producer;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Regression tests for issue #1570: pooled ReadyBatch objects are recycled while stale
/// references still sit in retry queues. The old staleness check (IsReturnedToPool alone)
/// is unsound because the flag resets when the object is re-rented — a stale holder then
/// treats a batch owned by another partition as its own, which surfaced as PRODUCE framing
/// guard failures (declared/emitted length mismatch) and message loss in stress runs.
/// IsCurrentIncarnation combines the flag with a monotonic generation stamp to close this.
/// </summary>
public sealed class ReadyBatchIncarnationTests
{
    private static ReadyBatch CreateInitializedBatch(string topic = "t", int partition = 0)
    {
        var batch = new ReadyBatch();
        InitializeBatch(batch, topic, partition);
        return batch;
    }

    private static void InitializeBatch(ReadyBatch batch, string topic = "t", int partition = 0)
    {
        batch.Initialize(
            new TopicPartition(topic, partition),
            new RecordBatch { Records = Array.Empty<Record>() },
            completionSourcesArray: null,
            completionSourcesCount: 0,
            dataSize: 100);
    }

    [Test]
    public async Task Initialize_IncrementsGeneration()
    {
        var batch = CreateInitializedBatch();
        var firstGeneration = batch.Generation;

        InitializeBatch(batch, "other-topic", 1);

        await Assert.That(batch.Generation).IsNotEqualTo(firstGeneration);
    }

    [Test]
    public async Task IsCurrentIncarnation_LiveBatch_ReturnsTrue()
    {
        var batch = CreateInitializedBatch();

        await Assert.That(batch.IsCurrentIncarnation(batch.Generation)).IsTrue();
    }

    [Test]
    public async Task IsCurrentIncarnation_ReturnedToPool_ReturnsFalse()
    {
        var batch = CreateInitializedBatch();
        var capturedGeneration = batch.Generation;

        // Simulate ReturnReadyBatch marking the batch as returned (still in the pool,
        // not yet re-rented). The generation is unchanged, so only the flag catches this.
        Interlocked.Exchange(ref batch._returnedToPool, 1);

        await Assert.That(batch.IsCurrentIncarnation(capturedGeneration)).IsFalse();
    }

    [Test]
    public async Task IsCurrentIncarnation_RecycledAndReRented_ReturnsFalse()
    {
        // The exact sequence from #1570: a stale reference (e.g. in _sendFailedRetries)
        // captured the generation, then the batch completed, went back to the pool, and
        // was re-rented for a different partition. Re-initialization resets
        // _returnedToPool to 0, so the flag check alone passes — only the generation
        // comparison detects that this is a different incarnation.
        var batch = CreateInitializedBatch("original-topic", 0);
        var staleGeneration = batch.Generation;

        Interlocked.Exchange(ref batch._returnedToPool, 1); // returned to pool
        InitializeBatch(batch, "new-owner-topic", 3);       // re-rented by a new owner

        await Assert.That(batch.IsReturnedToPool).IsFalse();       // flag check alone would pass
        await Assert.That(batch.IsCurrentIncarnation(staleGeneration)).IsFalse(); // generation catches it
        await Assert.That(batch.IsCurrentIncarnation(batch.Generation)).IsTrue(); // new owner unaffected
    }

    [Test]
    public async Task Initialize_GenerationIncrementVisibleBeforeFlagClear()
    {
        // Initialize() must bump the generation BEFORE clearing _returnedToPool.
        // IsCurrentIncarnation reads the flag first and the generation second, so this
        // write order guarantees a reader that sees the cleared flag also sees the new
        // generation. This test pins the observable end state; the ordering itself is
        // enforced by the write sequence in Initialize() (see comment there).
        var batch = CreateInitializedBatch();
        var staleGeneration = batch.Generation;

        Interlocked.Exchange(ref batch._returnedToPool, 1);
        InitializeBatch(batch);

        await Assert.That(batch.IsReturnedToPool).IsFalse();
        await Assert.That(batch.Generation).IsNotEqualTo(staleGeneration);
    }
}
