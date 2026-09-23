using System.Reflection;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Regression test for #3389: <see cref="RecordAccumulator.DrainPendingAppends"/> reserves for a
/// queued append under the queue lock and claims it afterwards. If that append is cancelled in
/// between, its pooled <see cref="PendingAppend"/> can be returned and re-rented for another
/// partition and record size. A claim through the drain's stale reference then took over the new
/// rental and appended it to the old partition, and the lost-claim refund used the new rental's
/// size instead of the reserved one.
/// </summary>
public class PendingAppendReuseRaceTests
{
    private const string Topic = "pending-append-reuse-topic";
    private const int BufferMemory = 4096;
    private const int FirstValueLength = 200;

    [Test]
    [Timeout(30_000)]
    public async Task DrainPendingAppends_OperationReusedBetweenReservationAndClaim_ServesNewRentalOnItsOwnPartition(
        CancellationToken cancellationToken)
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-producer",
            BufferMemory = BufferMemory,
            BatchSize = 16_384,
            LingerMs = 60_000 // Keep batches open so the appended records stay inspectable.
        };
        await using var accumulator = new RecordAccumulator(
            options,
            resolveLeaderId: static (_, _) => 1);
        AccumulatorTestHelpers.KeepBatchesOpenDespiteAppLimitedBypass(accumulator);

        var firstSize = PartitionBatch.EstimateRecordSize(0, FirstValueLength, null, 0);
        var secondSize = PartitionBatch.EstimateRecordSize(0, 0, null, 0);
        await Assert.That(secondSize).IsLessThan(firstSize);

        // Fill BufferMemory so the next append queues as a pooled PendingAppend.
        await Assert.That(accumulator.TryReserveMemoryForTest(BufferMemory)).IsTrue();

        using var firstCancellation = new CancellationTokenSource();
        var firstValue = ProducerDataPool.BytePool.Rent(FirstValueLength);
        var firstAppend = accumulator.AppendAsync(
            Topic,
            partition: 0,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            PooledMemory.Null,
            new PooledMemory(firstValue, FirstValueLength),
            headers: null,
            headerCount: 0,
            completionSource: null,
            callback: null,
            firstCancellation.Token,
            partitionCount: 2);
        await Assert.That(firstAppend.IsCompleted).IsFalse();
        var firstOperation = accumulator.PeekPendingAppendForTest();
        await Assert.That(firstOperation).IsNotNull();

        ValueTask<bool> secondAppend = default;
        PendingAppend? secondOperation = null;
        Exception? firstFailure = null;
        var hookRuns = 0;
        accumulator.AfterPendingAppendDrainReservationForTest = () =>
        {
            if (++hookRuns != 1)
                return;

            // The drain reserved firstSize for the first rental and has not claimed it yet.
            // Cancel it and observe the result on this thread: GetResult returns the instance to
            // this thread's pool slot, so the next slow-path append re-rents the same object.
            firstCancellation.Cancel();
            try
            {
                firstAppend.GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                firstFailure = ex;
            }

            // Refill the buffer so the second append also takes the slow path.
            if (!accumulator.TryReserveMemoryForTest(BufferMemory - firstSize))
                throw new InvalidOperationException("Could not refill BufferMemory.");

            secondAppend = AccumulatorTestHelpers.AppendNullRecordAsync(
                accumulator, Topic, partition: 1, partitionCount: 2);
            secondOperation = accumulator.PeekPendingAppendForTest();
        };

        // Frees the initial fill and runs the drain on this thread (owner scan -> hook -> claims).
        accumulator.ReleaseMemory(BufferMemory);
        accumulator.AfterPendingAppendDrainReservationForTest = null;

        await Assert.That(hookRuns).IsGreaterThanOrEqualTo(1);
        await Assert.That(firstFailure).IsTypeOf<OperationCanceledException>();
        // The race needs the second append to reuse the first append's pooled instance.
        await Assert.That(ReferenceEquals(secondOperation, firstOperation)).IsTrue();

        // The drain refunded the cancelled rental's reservation, then served the new rental on
        // its own partition in the next pass.
        await Assert.That(secondAppend.IsCompleted).IsTrue();
        await Assert.That(await secondAppend).IsTrue();

        var firstPartitionRecords = accumulator.TryGetBatch(Topic, 0, out var firstBatch)
            ? firstBatch!.RecordCount
            : 0;
        await Assert.That(firstPartitionRecords).IsEqualTo(0);
        await Assert.That(accumulator.TryGetBatch(Topic, 1, out var secondBatch)).IsTrue();
        await Assert.That(secondBatch!.RecordCount).IsEqualTo(1);

        // Held: the refill plus the second record. The first rental's reservation is fully
        // refunded; a refund sized from the new rental would leave firstSize - secondSize behind.
        await Assert.That(accumulator.BufferedBytes).IsEqualTo((long)(BufferMemory - firstSize + secondSize));

        // The second rental's own queue entry was consumed by the drain that served it.
        await Assert.That(accumulator.PendingAppendCountForTest).IsEqualTo(0);
    }

    [Test]
    public async Task PendingAppend_StaleGeneration_CannotClaimOrFailLaterRental()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-producer",
            BufferMemory = BufferMemory,
            LingerMs = 60_000
        };
        await using var accumulator = new RecordAccumulator(options);
        var pool = new PendingAppendPool(1);

        var operation = Rent(accumulator, pool, partition: 0);
        var firstGeneration = operation.Generation;
        await Assert.That(operation.IsPending(firstGeneration)).IsTrue();
        var failed = operation.TryFail(new OperationCanceledException(), firstGeneration);
        var pendingAfterFail = operation.IsPending(firstGeneration);

        // Observe the failure (returning the instance to the pool) and re-rent with no await in
        // between, so the re-rent hits the same thread's pool slot.
        Exception? observed = null;
        try
        {
            new ValueTask<bool>(operation, operation.Version).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            observed = ex;
        }

        var reused = Rent(accumulator, pool, partition: 1);

        await Assert.That(failed).IsTrue();
        await Assert.That(pendingAfterFail).IsFalse();
        await Assert.That(observed).IsTypeOf<OperationCanceledException>();
        await Assert.That(ReferenceEquals(reused, operation)).IsTrue();
        var secondGeneration = reused.Generation;
        await Assert.That(secondGeneration).IsNotEqualTo(firstGeneration);

        // References captured for the first rental are inert against the second.
        await Assert.That(reused.IsPending(firstGeneration)).IsFalse();
        await Assert.That(reused.TryClaim(firstGeneration)).IsFalse();
        await Assert.That(reused.TryFail(new OperationCanceledException(), firstGeneration)).IsFalse();
        await Assert.That(reused.IsPending(secondGeneration)).IsTrue();

        await Assert.That(reused.TryClaim(secondGeneration)).IsTrue();
        await Assert.That(reused.TryClaim(secondGeneration)).IsFalse();
        reused.ReleasePendingCountAfterClaim();
        reused.CompleteResult(true);
        await Assert.That(new ValueTask<bool>(reused, reused.Version).GetAwaiter().GetResult()).IsTrue();
    }

    [Test]
    public async Task PendingAppend_StaleCancellationCallback_DoesNotCancelRentalWithLiveToken()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-producer",
            BufferMemory = BufferMemory,
            LingerMs = 60_000
        };
        await using var accumulator = new RecordAccumulator(options);
        var pool = new PendingAppendPool(1);
        using var cancellation = new CancellationTokenSource();
        var operation = Rent(accumulator, pool, partition: 0, cancellation.Token);
        var generation = operation.Generation;

        // A callback registered for an earlier rental of this instance runs now. The current
        // rental's token is not cancelled, so the callback must leave it pending.
        var onCancellation = typeof(PendingAppend).GetMethod(
            "OnCancellation", BindingFlags.NonPublic | BindingFlags.Instance)!;
        onCancellation.Invoke(operation, null);
        await Assert.That(operation.IsPending(generation)).IsTrue();

        // The rental's own token still cancels it.
        cancellation.Cancel();
        await Assert.That(operation.IsPending(generation)).IsFalse();
        await Assert.That(async () => await new ValueTask<bool>(operation, operation.Version))
            .Throws<OperationCanceledException>();
    }

    private static PendingAppend Rent(
        RecordAccumulator accumulator,
        PendingAppendPool pool,
        int partition,
        CancellationToken cancellationToken = default)
    {
        var now = Dekaf.MonotonicClock.GetMilliseconds();
        var operation = pool.Rent();
        operation.Initialize(
            Topic,
            partition,
            partitionCount: 2,
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            key: PooledMemory.Null,
            value: PooledMemory.Null,
            headers: null,
            headerCount: 0,
            completionSource: null,
            callback: null,
            recordSize: PartitionBatch.EstimateRecordSize(0, 0, null, 0),
            startTicks: now,
            deadlineTickCount: now + 30_000,
            accumulator: accumulator,
            pool: pool,
            cancellationToken: cancellationToken);
        return operation;
    }
}
