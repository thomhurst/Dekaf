using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for the linger loop's deadline-based wait (issue #2114): instead of polling a
/// fixed 1 ms timer, the loop sleeps until the earliest pending batch can reach its
/// linger deadline and is re-armed by wakeup signals when that deadline shortens.
/// </summary>
public class LingerDeadlineWaitTests
{
    private static ProducerOptions CreateOptions(int lingerMs) => new()
    {
        BootstrapServers = ["localhost:9092"],
        LingerMs = lingerMs
    };

    private static ValueTask<bool> AppendAsync(
        RecordAccumulator accumulator,
        PooledValueTaskSource<RecordMetadata>? completionSource = null,
        int partition = 0) =>
        accumulator.AppendAsync(
            "test-topic",
            partition,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            PooledMemory.Null,
            PooledMemory.Null,
            headers: null,
            headerCount: 0,
            completionSource,
            callback: null,
            CancellationToken.None);

    private static long GetOldestBatchHint(RecordAccumulator accumulator) =>
        AccumulatorTestHelpers.GetPrivateField<long>(accumulator, "_oldestBatchCreatedTicks");

    private static int GetUnsealedBatchCount(RecordAccumulator accumulator) =>
        AccumulatorTestHelpers.GetPrivateField<int>(accumulator, "_unsealedBatchCount");

    [Test]
    public async Task NoBatches_WaitClampedToMaxAndLinger()
    {
        var accumulator = new RecordAccumulator(CreateOptions(lingerMs: 10_000));
        try
        {
            // No oldest-batch hint: the horizon is the full linger window, capped by maxWaitMs.
            await Assert.That(accumulator.GetMillisUntilEarliestLingerDeadline(maxWaitMs: 5_000)).IsEqualTo(5_000);
            await Assert.That(accumulator.GetMillisUntilEarliestLingerDeadline(maxWaitMs: 30_000)).IsEqualTo(10_000);
            // maxWaitMs of 0 (orphan sweep due) still floors at 1 to avoid a busy spin.
            await Assert.That(accumulator.GetMillisUntilEarliestLingerDeadline(maxWaitMs: 0)).IsEqualTo(1);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task FireAndForgetBatch_WaitTracksFullLingerDeadline()
    {
        var accumulator = new RecordAccumulator(CreateOptions(lingerMs: 60_000));
        try
        {
            await AppendAsync(accumulator);

            var waitMs = accumulator.GetMillisUntilEarliestLingerDeadline(maxWaitMs: 120_000);

            // Fresh batch: remaining linger is the full window minus scheduling slop.
            await Assert.That(waitMs).IsGreaterThan(30_000);
            await Assert.That(waitMs).IsLessThanOrEqualTo(60_000);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task AwaitedBatch_WaitShrinksToAwaitedWindow()
    {
        var accumulator = new RecordAccumulator(CreateOptions(lingerMs: 60_000));
        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        try
        {
            await AppendAsync(accumulator, pool.Rent());

            var waitMs = accumulator.GetMillisUntilEarliestLingerDeadline(maxWaitMs: 120_000);

            // An awaited produce seals on min(2ms, LingerMs/2), so the wait must not
            // exceed the 2 ms awaited window.
            await Assert.That(waitMs).IsGreaterThanOrEqualTo(1);
            await Assert.That(waitMs).IsLessThanOrEqualTo(2);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task ZeroLinger_WaitFloorsAtOneMillisecond()
    {
        var accumulator = new RecordAccumulator(CreateOptions(lingerMs: 0));
        try
        {
            await AppendAsync(accumulator);

            // Deadline already passed: floor at 1 ms (the old fixed-tick cadence) instead of 0,
            // which would busy-spin the linger loop.
            await Assert.That(accumulator.GetMillisUntilEarliestLingerDeadline(maxWaitMs: 5_000)).IsEqualTo(1);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task FirstAwaitedProduceInExistingBatch_SignalsLingerWakeup()
    {
        var accumulator = new RecordAccumulator(CreateOptions(lingerMs: 60_000));
        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        try
        {
            // Fire-and-forget append queues the partition and signals; consume that signal.
            await AppendAsync(accumulator);
            await Assert.That(await accumulator.WaitForLingerWakeupAsync(5_000)).IsTrue();

            var wakeup = accumulator.WaitForLingerWakeupAsync(10_000).AsTask();
            await Assert.That(wakeup.IsCompleted).IsFalse();

            // First awaited produce lands in the existing batch: the partition is already
            // linger-queued, so only the awaited 0→1 transition can signal. Without it the
            // linger loop would sleep out the full 60 s deadline it armed for the
            // fire-and-forget batch instead of re-arming on the 2 ms awaited window.
            await AppendAsync(accumulator, pool.Rent());

            await Assert.That(await wakeup.WaitAsync(TimeSpan.FromSeconds(5))).IsTrue();
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task NewerFireAndForgetBatch_DoesNotSignalLingerWakeup()
    {
        var accumulator = new RecordAccumulator(CreateOptions(lingerMs: 60_000));
        try
        {
            await AppendAsync(accumulator, partition: 0);
            await Assert.That(await accumulator.WaitForLingerWakeupAsync(5_000)).IsTrue();

            var wakeup = accumulator.WaitForLingerWakeupAsync(100).AsTask();

            // A newer batch cannot shorten the existing batch's linger deadline, so it
            // must not interrupt an active wait that is already armed for that deadline.
            await AppendAsync(accumulator, partition: 1);

            await Assert.That(await wakeup).IsFalse();
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    [Timeout(30_000)]
    public async Task LingerSweep_RaisesOldestBatchHintToSurvivingBatch(CancellationToken cancellationToken)
    {
        var accumulator = new RecordAccumulator(CreateOptions(lingerMs: 60_000));
        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        try
        {
            // Batch A (partition 0): awaited, seals on the 2 ms awaited window.
            await AppendAsync(accumulator, pool.Rent(), partition: 0);
            // Batch B (partition 1): fire-and-forget, survives the sweep for 60 s.
            await AppendAsync(accumulator, partition: 1);

            var hintBeforeSweep = GetOldestBatchHint(accumulator);
            await Assert.That(hintBeforeSweep).IsNotEqualTo(long.MaxValue);

            // Run linger sweeps until batch A expires and seals, leaving only batch B.
            while (GetUnsealedBatchCount(accumulator) != 1)
            {
                await accumulator.ExpireLingerAsync(CancellationToken.None);
                await Task.Delay(5, cancellationToken);
            }

            // The sweep must raise the hint from sealed batch A's creation time to
            // surviving batch B's, so the next deadline wait tracks a live batch instead
            // of flooring at 1 ms against a long-sealed one.
            await Assert.That(GetOldestBatchHint(accumulator)).IsGreaterThan(hintBeforeSweep);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    [Timeout(30_000)]
    public async Task BatchQueuedAfterSweepSnapshot_SignalsLingerWakeup(CancellationToken cancellationToken)
    {
        var accumulator = new RecordAccumulator(CreateOptions(lingerMs: 60_000));
        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        using var snapshotTaken = new ManualResetEventSlim();
        using var continueSweep = new ManualResetEventSlim();
        try
        {
            await AppendAsync(accumulator, pool.Rent(), partition: 0);
            await Assert.That(await accumulator.WaitForLingerWakeupAsync(5_000)).IsTrue();

            accumulator.AfterLingerQueueSnapshotForTest = () =>
            {
                snapshotTaken.Set();
                continueSweep.Wait(cancellationToken);
            };

            await Task.Delay(5, cancellationToken);
            var sweep = Task.Run(
                async () => await accumulator.ExpireLingerAsync(CancellationToken.None),
                cancellationToken);

            snapshotTaken.Wait(cancellationToken);
            await AppendAsync(accumulator, partition: 1);
            continueSweep.Set();
            await sweep;

            await Assert.That(await accumulator.WaitForLingerWakeupAsync(100)).IsTrue();
        }
        finally
        {
            continueSweep.Set();
            accumulator.AfterLingerQueueSnapshotForTest = null;
            await accumulator.DisposeAsync();
        }
    }
}
