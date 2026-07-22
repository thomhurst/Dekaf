using Dekaf.Producer;
using Dekaf.Tests.Unit.Producer;

namespace Dekaf.Tests.Unit.Concurrency;

/// <summary>
/// Leak gates for the produce path under concurrency and load — the conditions where the
/// historical leaks actually happened (BufferMemory refund leaks #1516/#2199, pool
/// double-returns #2187, stranded pipeline tracking). Complements
/// <c>ProducerMemoryLeakGateTests</c> (sequential drift) and
/// <c>ProducerBatchLifecycleHammerTests</c> (single-batch cleanup-owner races) by racing
/// many appenders against concurrent drainers, a flush storm, linger expiry, and injected
/// batch failures — then asserting every accounting invariant lands exactly on zero.
/// The <c>#if DEBUG</c> counter-balance blocks compile only against Debug builds
/// (<c>ProducerDebugCounters</c> is stripped from Release); the accounting and heap
/// assertions run in every configuration.
/// </summary>
[NotInParallel]
public sealed class ProducerLeakUnderLoadTests
{
    private const string Topic = "leak-load";
    private const int PartitionCount = 8;
    private const int ValueSize = 1024;
    private const int Appenders = 4;
    private const int MessagesPerAppender = 2_000;
    private const int Drainers = 2;
    private const int FailEveryNthBatch = 5;

    /// <summary>
    /// Sustained load with every producer-side race surface active at once: 4 concurrent
    /// appenders (awaited + fire-and-forget), 2 concurrent drainers completing batches with
    /// every 5th batch failed, a flush-storm task racing seals against appends, and a linger
    /// expiry loop forcing partial-batch seals mid-append. Two waves: the first warms pools
    /// and the thread pool, the second is measured for retained-heap growth after full GC.
    /// After each wave the accounting must return exactly to zero — under load, any missed
    /// or doubled refund on the success, failure, or flush path shows up here.
    /// </summary>
    [Test]
    [Timeout(120_000)]
    public async Task ConcurrentAppendDrainFlushRaces_AccountingReturnsToZero_AndHeapDoesNotGrow(
        CancellationToken cancellationToken)
    {
        const long MaxRetainedGrowthBytes = 8 * 1024 * 1024;

        var accumulator = new RecordAccumulator(new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "leak-load",
            BufferMemory = 16UL * 1024 * 1024,
            BatchSize = 16384,
            LingerMs = 1_000,
            MaxBlockMs = 60_000,
        });
        var completionPool = new ValueTaskSourcePool<RecordMetadata>();
        ProducerDebugCounters.Reset();

        var totalDrainedBatches = 0;
        var totalFailedBatches = 0;
        var totalAwaited = 0;
        try
        {
            async Task RunAssertedWaveAsync()
            {
                var (counters, appenders) = await RunLoadWaveAsync(accumulator, completionPool, cancellationToken);
                await AssertWaveFullyRetiredAsync(accumulator, counters, appenders);
                totalDrainedBatches += counters.DrainedBatches;
                totalFailedBatches += counters.FailedBatches;
                totalAwaited += appenders.Sum(static r => r.CompletionTasks.Count);
            }

            await RunAssertedWaveAsync();
            var baseline = LeakGateHarness.GetStableHeapBytes();
            await RunAssertedWaveAsync();
            var retainedGrowth = LeakGateHarness.GetStableHeapBytes() - baseline;

            await Assert.That(retainedGrowth).IsLessThan(MaxRetainedGrowthBytes);

#if DEBUG
            var counters = ProducerDebugCounters.GetSnapshot();
            await Assert.That(counters.MessagesAppended).IsEqualTo(2 * Appenders * MessagesPerAppender);
            await Assert.That(counters.CompletionSourcesStoredInBatch).IsEqualTo(totalAwaited);
            await Assert.That(counters.CompletionSourcesCompleted + counters.CompletionSourcesFailed)
                .IsEqualTo(totalAwaited);
            await Assert.That(counters.ReadyBatchesRented).IsEqualTo(totalDrainedBatches);
            await Assert.That(counters.ReadyBatchesReturned).IsEqualTo(totalDrainedBatches);
            await Assert.That(counters.ReadyBatchDuplicateReturns).IsEqualTo(0);
            await Assert.That(counters.BatchesSentSuccessfully)
                .IsEqualTo(totalDrainedBatches - totalFailedBatches);
            await Assert.That(counters.BatchesFailed).IsEqualTo(totalFailedBatches);
#endif
        }
        finally
        {
            await accumulator.DisposeAsync();
            await completionPool.DisposeAsync();
            ProducerDebugCounters.Reset();
        }
    }

    /// <summary>
    /// Backpressure saturation: 4 MB of traffic pushed through a 128 KB buffer while the
    /// drainer is gated shut until an appender is provably blocked in the slow-path memory
    /// reservation wait. The drainer then races the blocked appenders' admission path, with
    /// every 7th batch failed so the failure-path refund races the pending-append drain.
    /// This is the exact territory of the #2199 refund-leak family: a refund that is lost
    /// (or doubled) under saturation either strands the blocked appenders or corrupts
    /// <c>BufferedBytes</c> — both caught by the end-state asserts.
    /// </summary>
    [Test]
    [Timeout(120_000)]
    public async Task BackpressureSaturation_RefundsUnderPressure_AccountingReturnsToZero(
        CancellationToken cancellationToken)
    {
        const int SaturationMessagesPerAppender = 1_024;
        const int SaturationFailEveryNthBatch = 7;

        var accumulator = new RecordAccumulator(new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "leak-saturation",
            BufferMemory = 128 * 1024,
            BatchSize = 16384,
            LingerMs = 1_000,
            MaxBlockMs = 60_000,
        });
        var completionPool = new ValueTaskSourcePool<RecordMetadata>();
        ProducerDebugCounters.Reset();

        try
        {
            var counters = new LeakGateCounters();
            var drainerGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var appenderTasks = StartAppenders(
                accumulator, completionPool, counters, SaturationMessagesPerAppender, cancellationToken);

            // Hold the drainer shut until an appender is provably blocked so the slow-path
            // reservation wait (and its refund-driven wakeup) is guaranteed to be
            // exercised, not skipped on a fast machine.
            var drainerTask = Task.Run(async () =>
            {
                await drainerGate.Task.WaitAsync(cancellationToken);
                await LeakGateHarness.DrainUntilStoppedAsync(
                    accumulator, counters, SaturationFailEveryNthBatch, DrainWaitMode.WakeupSignal, cancellationToken);
            }, cancellationToken);

            await LeakGateHarness.WaitForSaturationAsync(accumulator, appenderTasks, cancellationToken);
            drainerGate.SetResult();

            var appenderResults = await Task.WhenAll(appenderTasks).WaitAsync(cancellationToken);
            await accumulator.FlushAsync(cancellationToken);
            counters.RequestStop();
            await drainerTask.WaitAsync(cancellationToken);

            await AwaitCompletionsAsync(appenderResults, cancellationToken);

            // Saturation must actually have happened for this test to mean anything.
            await Assert.That(accumulator.BufferPressureEvents).IsGreaterThan(0L);

            var totalMessages = Appenders * SaturationMessagesPerAppender;
            await Assert.That(counters.DrainedRecords).IsEqualTo(totalMessages);
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(0L);
            await Assert.That(accumulator.InFlightBatchCount).IsEqualTo(0L);
            await Assert.That(accumulator.UnsealedBatchCount).IsEqualTo(0);
            await Assert.That(accumulator.PendingAppendCountForTest).IsEqualTo(0);

#if DEBUG
            var snapshot = ProducerDebugCounters.GetSnapshot();
            var awaited = appenderResults.Sum(static r => r.CompletionTasks.Count);
            await Assert.That(snapshot.CompletionSourcesStoredInBatch).IsEqualTo(awaited);
            await Assert.That(snapshot.CompletionSourcesCompleted + snapshot.CompletionSourcesFailed)
                .IsEqualTo(awaited);
            await Assert.That(snapshot.ReadyBatchesRented).IsEqualTo(counters.DrainedBatches);
            await Assert.That(snapshot.ReadyBatchesReturned).IsEqualTo(counters.DrainedBatches);
            await Assert.That(snapshot.ReadyBatchDuplicateReturns).IsEqualTo(0);
#endif
        }
        finally
        {
            await accumulator.DisposeAsync();
            await completionPool.DisposeAsync();
            ProducerDebugCounters.Reset();
        }
    }

    /// <summary>
    /// One full loaded wave: concurrent appenders, concurrent drainers with failure
    /// injection, a flush storm, and a linger-expiry loop — then quiesce and retire
    /// every outstanding completion.
    /// </summary>
    private static async Task<(LeakGateCounters Counters, AppenderResult[] Appenders)> RunLoadWaveAsync(
        RecordAccumulator accumulator,
        ValueTaskSourcePool<RecordMetadata> completionPool,
        CancellationToken cancellationToken)
    {
        var counters = new LeakGateCounters();
        using var stormCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var appenderTasks = StartAppenders(
            accumulator, completionPool, counters, MessagesPerAppender, cancellationToken);

        // Only the first drainer may await the wakeup signal (single-waiter contract —
        // see DrainWaitMode); the rest poll.
        var drainerTasks = new Task[Drainers];
        for (var i = 0; i < Drainers; i++)
        {
            var waitMode = i == 0 ? DrainWaitMode.WakeupSignal : DrainWaitMode.Poll;
            drainerTasks[i] = Task.Run(
                () => LeakGateHarness.DrainUntilStoppedAsync(
                    accumulator, counters, FailEveryNthBatch, waitMode, cancellationToken),
                cancellationToken);
        }

        // Flush storm: races seal-and-claim against in-progress appends.
        var flushStormTask = Task.Run(async () =>
        {
            while (!stormCts.Token.IsCancellationRequested)
            {
                try
                {
                    await accumulator.FlushAsync(stormCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                await Task.Yield();
            }
        }, CancellationToken.None);

        // Linger expiry loop: forces partial-batch seals mid-append, racing the appenders.
        var lingerTask = Task.Run(async () =>
        {
            try
            {
                while (!stormCts.Token.IsCancellationRequested)
                {
                    await accumulator.ExpireLingerAsync(stormCts.Token);
                    await Task.Delay(1, stormCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, CancellationToken.None);

        var appenderResults = await Task.WhenAll(appenderTasks).WaitAsync(cancellationToken);

        await stormCts.CancelAsync();
        await flushStormTask.WaitAsync(cancellationToken);
        await lingerTask.WaitAsync(cancellationToken);

        // Final flush completes only when every batch has exited the pipeline, so the
        // stop flag cannot strand an undrained batch.
        await accumulator.FlushAsync(cancellationToken);
        counters.RequestStop();
        await Task.WhenAll(drainerTasks).WaitAsync(cancellationToken);

        await AwaitCompletionsAsync(appenderResults, cancellationToken);

        return (counters, appenderResults);
    }

    private static Task<AppenderResult>[] StartAppenders(
        RecordAccumulator accumulator,
        ValueTaskSourcePool<RecordMetadata> completionPool,
        LeakGateCounters counters,
        int messagesPerAppender,
        CancellationToken cancellationToken)
    {
        var tasks = new Task<AppenderResult>[Appenders];
        for (var i = 0; i < Appenders; i++)
        {
            var appenderId = i;
            tasks[i] = Task.Run(
                () => LeakGateHarness.RunAppenderAsync(
                    accumulator, completionPool, counters, appenderId, messagesPerAppender,
                    PartitionCount, ValueSize, Topic, cancellationToken),
                cancellationToken);
        }

        return tasks;
    }

    private static async Task AwaitCompletionsAsync(
        AppenderResult[] appenderResults,
        CancellationToken cancellationToken)
    {
        foreach (var result in appenderResults)
        {
            foreach (var task in result.CompletionTasks)
            {
                try
                {
                    _ = await task.WaitAsync(cancellationToken);
                }
                catch (ExpectedInjectedFailureException)
                {
                    // Injected failure winner; awaiting proves the source terminated.
                }
            }
        }
    }

    private static async Task AssertWaveFullyRetiredAsync(
        RecordAccumulator accumulator,
        LeakGateCounters counters,
        AppenderResult[] appenderResults)
    {
        foreach (var result in appenderResults)
        {
            await Assert.That(result.WasCancelled).IsFalse();
            await Assert.That(result.AcceptedTotal).IsEqualTo(MessagesPerAppender);
        }

        var fireAndForget = appenderResults.Sum(static r => r.AcceptedFireAndForget);
        await Assert.That(counters.DrainedRecords).IsEqualTo(Appenders * MessagesPerAppender);
        await Assert.That(Volatile.Read(ref counters.CallbackInvocations)).IsEqualTo(fireAndForget);
        await Assert.That(accumulator.BufferedBytes).IsEqualTo(0L);
        await Assert.That(accumulator.InFlightBatchCount).IsEqualTo(0L);
        await Assert.That(accumulator.UnsealedBatchCount).IsEqualTo(0);
        await Assert.That(accumulator.PendingAppendCountForTest).IsEqualTo(0);
    }
}
