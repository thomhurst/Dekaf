using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Edge-case leak gates for the produce path, covering the corners the sequential drift gate
/// (<c>ProducerMemoryLeakGateTests</c>) and the load/race gates (<c>ProducerLeakUnderLoadTests</c>)
/// do not reach:
/// <list type="bullet">
/// <item><description>Appender cancellation mid-reservation-wait under saturation — a cancelled
/// waiter that leaves a reservation, a pending-append entry, or a phantom appended record behind
/// is caught by the drained-vs-accepted reconciliation.</description></item>
/// <item><description>Disposal racing active appenders and an active drainer — every accepted
/// message must terminate exactly once and all reserved memory must be refunded by the orphan
/// sweep, with no stranded completion.</description></item>
/// <item><description>Extreme payload shapes (null/empty/1-byte/batch-filling/oversized values,
/// pooled keys, pooled header arrays) — exercises arena rotation boundaries and the oversized
/// single-record batch path, with occupancy asserts proving the oversized path was taken.
/// Key/header pooled-array hygiene is exercised here but is observable only through the
/// accounting and heap gates, not asserted per-array.</description></item>
/// <item><description>User callbacks that throw, on both the success and the failure delivery
/// path — a throwing callback must not skip sibling callbacks or the batch refund.</description></item>
/// <item><description>Cancelled flushes — repeated abandoned flush waits must not accumulate
/// claim state or strand sealed batches.</description></item>
/// </list>
/// </summary>
[NotInParallel]
public sealed class ProducerLeakEdgeCaseTests
{
    private const string Topic = "leak-edge";
    private const int PartitionCount = 4;
    private const int ValueSize = 1024;
    private const int KeySize = 16;

    private static readonly byte[] TraceIdHeaderValue = "0123456789abcdef"u8.ToArray();

    /// <summary>
    /// Saturates a 128 KB buffer with the drainer gated shut so appenders block in the
    /// slow-path reservation wait, then cancels half of them mid-wait. The cancellation
    /// contract under test: a cancelled append must either complete fully (accepted, delivered)
    /// or leave no trace at all — no reservation, no pending-append entry, no phantom record.
    /// Drained records reconciled against per-appender accepted counts detect both a leaked
    /// reservation (BufferedBytes residue) and a phantom append (drained &gt; accepted).
    /// </summary>
    [Test]
    [Timeout(120_000)]
    public async Task CancelledAppendersUnderSaturation_NoPhantomRecordsOrResidue(
        CancellationToken cancellationToken)
    {
        const int MessagesPerAppender = 768;

        var accumulator = new RecordAccumulator(CreateOptions("leak-edge-cancel", bufferMemory: 128 * 1024));
        var completionPool = new ValueTaskSourcePool<RecordMetadata>();

        try
        {
            var counters = new LeakGateCounters();
            var drainerGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var victimCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            Task<AppenderResult> StartAppender(int appenderId, CancellationToken token) =>
                Task.Run(
                    () => LeakGateHarness.RunAppenderAsync(
                        accumulator, completionPool, counters, appenderId, MessagesPerAppender,
                        PartitionCount, ValueSize, Topic, token),
                    cancellationToken);

            var survivorTasks = new[] { StartAppender(0, cancellationToken), StartAppender(1, cancellationToken) };
            var victimTasks = new[] { StartAppender(2, victimCts.Token), StartAppender(3, victimCts.Token) };

            var drainerTask = Task.Run(async () =>
            {
                await drainerGate.Task.WaitAsync(cancellationToken);
                await LeakGateHarness.DrainUntilStoppedAsync(
                    accumulator, counters, failEveryNthBatch: 0, DrainWaitMode.WakeupSignal, cancellationToken);
            }, cancellationToken);

            // Hold the drainer shut until an appender is provably blocked in the slow-path
            // reservation wait, then cancel the victims while they are blocked.
            await LeakGateHarness.WaitForSaturationAsync(
                accumulator, [.. survivorTasks, .. victimTasks], cancellationToken);
            await victimCts.CancelAsync();
            drainerGate.SetResult();

            var survivorResults = await Task.WhenAll(survivorTasks).WaitAsync(cancellationToken);
            var victimResults = await Task.WhenAll(victimTasks).WaitAsync(cancellationToken);

            await accumulator.FlushAsync(cancellationToken);
            counters.RequestStop();
            await drainerTask.WaitAsync(cancellationToken);

            // The victims must actually have been interrupted mid-run for the test to
            // exercise cancellation inside the reservation wait.
            await Assert.That(accumulator.BufferPressureEvents).IsGreaterThan(0L);
            foreach (var victim in victimResults)
            {
                await Assert.That(victim.WasCancelled).IsTrue();
                await Assert.That(victim.AcceptedTotal).IsLessThan(MessagesPerAppender);
            }

            var allResults = survivorResults.Concat(victimResults).ToArray();
            var acceptedTotal = allResults.Sum(static r => r.AcceptedTotal);
            var acceptedFireAndForget = allResults.Sum(static r => r.AcceptedFireAndForget);

            // Exactly the accepted messages must flow through the pipeline — no more, no less.
            await Assert.That(counters.DrainedRecords).IsEqualTo(acceptedTotal);
            await Assert.That(Volatile.Read(ref counters.CallbackInvocations)).IsEqualTo(acceptedFireAndForget);

            // Cancellation after acceptance stops the caller's wait, never the delivery:
            // every accepted awaited message must complete successfully.
            foreach (var result in allResults)
            {
                foreach (var task in result.CompletionTasks)
                    _ = await task.WaitAsync(cancellationToken);
            }

            await Assert.That(accumulator.BufferedBytes).IsEqualTo(0L);
            await Assert.That(accumulator.InFlightBatchCount).IsEqualTo(0L);
            await Assert.That(accumulator.UnsealedBatchCount).IsEqualTo(0);
            await Assert.That(accumulator.PendingAppendCountForTest).IsEqualTo(0);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await completionPool.DisposeAsync();
        }
    }

    /// <summary>
    /// Disposes the accumulator while appenders and a drainer are running at full speed.
    /// Appenders run open-ended until disposal rejects them, so the dispose orphan sweep is
    /// guaranteed to race live appends, live drains, and in-flight batches. Invariants: every
    /// accepted message terminates exactly once (fire-and-forget callbacks invoked exactly
    /// once, every awaited completion reaches a terminal state), and the sweep refunds all
    /// reserved memory — <c>BufferedBytes</c> lands on exactly zero after quiesce.
    /// </summary>
    [Test]
    [Timeout(120_000)]
    public async Task DisposeDuringActiveLoad_AcceptedMessagesTerminateExactlyOnce_NoResidue(
        CancellationToken cancellationToken)
    {
        const int Appenders = 4;
        const int DisposeAfterAcceptedMessages = 20_000;

        var accumulator = new RecordAccumulator(CreateOptions("leak-edge-dispose", bufferMemory: 16UL * 1024 * 1024));
        var completionPool = new ValueTaskSourcePool<RecordMetadata>();

        try
        {
            var counters = new LeakGateCounters();

            var appenderTasks = new Task<AppenderResult>[Appenders];
            for (var i = 0; i < Appenders; i++)
            {
                var appenderId = i;
                appenderTasks[i] = Task.Run(
                    () => LeakGateHarness.RunAppenderAsync(
                        accumulator, completionPool, counters, appenderId, messageCount: int.MaxValue,
                        PartitionCount, ValueSize, Topic, cancellationToken),
                    cancellationToken);
            }

            // Poll mode: this drainer outlives the accumulator, and a drainer caught inside
            // the wakeup signal when the accumulator is disposed strands forever.
            var drainerTask = Task.Run(
                () => LeakGateHarness.DrainUntilStoppedAsync(
                    accumulator, counters, failEveryNthBatch: 0, DrainWaitMode.Poll, cancellationToken),
                cancellationToken);

            while (Volatile.Read(ref counters.AcceptedTotal) < DisposeAfterAcceptedMessages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            // Dispose races live appenders and the live drainer. The drainer keeps running
            // through disposal so the pipeline can quiesce, then is stopped afterwards.
            await accumulator.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

            var appenderResults = await Task.WhenAll(appenderTasks).WaitAsync(cancellationToken);
            counters.RequestStop();
            await drainerTask.WaitAsync(cancellationToken);

            var acceptedTotal = appenderResults.Sum(static r => r.AcceptedTotal);
            var acceptedFireAndForget = appenderResults.Sum(static r => r.AcceptedFireAndForget);
            await Assert.That(acceptedTotal).IsGreaterThanOrEqualTo(DisposeAfterAcceptedMessages);

            // Every accepted awaited message must reach a terminal state — delivered by the
            // drainer or failed by the dispose sweep — never stranded.
            foreach (var result in appenderResults)
            {
                foreach (var task in result.CompletionTasks)
                {
                    try
                    {
                        _ = await task.WaitAsync(cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        // Terminal failure from the dispose sweep is a valid outcome;
                        // awaiting proves the source terminated.
                    }
                }
            }

            await Assert.That(Volatile.Read(ref counters.CallbackInvocations)).IsEqualTo(acceptedFireAndForget);
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(0L);
            await Assert.That(accumulator.InFlightBatchCount).IsEqualTo(0L);
            await Assert.That(accumulator.PendingAppendCountForTest).IsEqualTo(0);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await completionPool.DisposeAsync();
        }
    }

    /// <summary>
    /// Repeated waves of pathological payload shapes: null values, empty values, single bytes,
    /// near-batch-filling values, oversized values (3× BatchSize, forcing the dedicated
    /// single-record batch path), pooled non-null keys, and pooled header arrays. These are
    /// the arena-boundary and ownership-transfer paths where the original BufferMemory bypass
    /// (#1516 family) lived. Accounting must return exactly to zero after every wave, and the
    /// drained batch count must prove the oversized messages each occupied their own batch.
    /// </summary>
    [Test]
    [Timeout(120_000)]
    public async Task ExtremePayloadShapes_MixedSizesKeysAndHeaders_AccountingReturnsToZero(
        CancellationToken cancellationToken)
    {
        const int Cycles = 20;
        const int MessagesPerCycle = 120;
        const int BatchSize = 16384;
        const int OversizedPerCycle = MessagesPerCycle / 6;

        var accumulator = new RecordAccumulator(CreateOptions("leak-edge-shapes", bufferMemory: 32UL * 1024 * 1024));
        var completionPool = new ValueTaskSourcePool<RecordMetadata>();

        try
        {
            for (var cycle = 0; cycle < Cycles; cycle++)
            {
                var completionTasks = new List<Task<RecordMetadata>>();
                var callbackInvocations = 0;
                var expectedCallbacks = 0;
                Action<RecordMetadata, Exception?> callback =
                    (_, _) => Interlocked.Increment(ref callbackInvocations);

                for (var i = 0; i < MessagesPerCycle; i++)
                {
                    var value = (i % 6) switch
                    {
                        0 => PooledMemory.Null,
                        1 => new PooledMemory(ProducerDataPool.BytePool.Rent(1), 0),
                        2 => LeakGateHarness.RentPooled(1, i),
                        3 => LeakGateHarness.RentPooled(4 * 1024, i),
                        4 => LeakGateHarness.RentPooled(BatchSize - 1024, i),
                        _ => LeakGateHarness.RentPooled(3 * BatchSize, i),
                    };
                    var key = (i % 2) == 0 ? LeakGateHarness.RentPooled(KeySize, i * 17) : PooledMemory.Null;

                    Header[]? headers = null;
                    var headerCount = 0;
                    if (i % 3 == 0)
                    {
                        headers = ProducerContainerPools.Headers.Rent(2);
                        headers[0] = new Header("trace-id", TraceIdHeaderValue);
                        headers[1] = new Header("origin", (byte[]?)null);
                        headerCount = 2;
                    }

                    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    bool accepted;
                    if (i % 4 == 1)
                    {
                        var completion = completionPool.Rent();
                        completionTasks.Add(completion.Task.AsTask());
                        accepted = await accumulator.AppendAsync(
                            Topic, i % PartitionCount, timestamp, key, value,
                            headers, headerCount, completion, callback: null, cancellationToken);
                    }
                    else
                    {
                        expectedCallbacks++;
                        accepted = await accumulator.AppendAsync(
                            Topic, i % PartitionCount, timestamp, key, value,
                            headers, headerCount, completionSource: null, callback, cancellationToken);
                    }

                    if (!accepted)
                        throw new InvalidOperationException($"Cycle {cycle} message {i} rejected.");
                }

                var counters = await DrainCycleAsync(accumulator, MessagesPerCycle, failEveryNthBatch: 0, cancellationToken);
                foreach (var task in completionTasks)
                    _ = await task.WaitAsync(cancellationToken);

                await Assert.That(counters.DrainedRecords).IsEqualTo(MessagesPerCycle);
                // Each 3×BatchSize value cannot share a batch, so the wave must produce at
                // least one batch per oversized message — proof the oversized path ran.
                await Assert.That(counters.DrainedBatches).IsGreaterThanOrEqualTo(OversizedPerCycle);
                await Assert.That(Volatile.Read(ref callbackInvocations)).IsEqualTo(expectedCallbacks);
                await Assert.That(accumulator.BufferedBytes).IsEqualTo(0L);
                await Assert.That(accumulator.InFlightBatchCount).IsEqualTo(0L);
                await Assert.That(accumulator.UnsealedBatchCount).IsEqualTo(0);
                await Assert.That(accumulator.PendingAppendCountForTest).IsEqualTo(0);
            }
        }
        finally
        {
            await accumulator.DisposeAsync();
            await completionPool.DisposeAsync();
        }
    }

    /// <summary>
    /// Fire-and-forget callbacks that throw, delivered on both the success path and the
    /// failure path (every 4th batch is failed). A throwing user callback must not skip
    /// sibling callbacks in the same batch, must not skip the batch refund, and must not
    /// kill the simulated sender — every callback still fires exactly once and accounting
    /// returns to zero.
    /// </summary>
    [Test]
    [Timeout(120_000)]
    public async Task ThrowingUserCallbacks_DoNotSkipSiblingCallbacksOrRefunds(
        CancellationToken cancellationToken)
    {
        const int Cycles = 10;
        const int MessagesPerCycle = 128;
        const int FailEveryNthBatch = 4;

        var accumulator = new RecordAccumulator(CreateOptions("leak-edge-callbacks", bufferMemory: 16UL * 1024 * 1024));
        var completionPool = new ValueTaskSourcePool<RecordMetadata>();

        try
        {
            for (var cycle = 0; cycle < Cycles; cycle++)
            {
                var completionTasks = new List<Task<RecordMetadata>>();
                var callbackInvocations = 0;
                var expectedCallbacks = 0;
                Action<RecordMetadata, Exception?> countingCallback =
                    (_, _) => Interlocked.Increment(ref callbackInvocations);
                Action<RecordMetadata, Exception?> throwingCallback = (_, _) =>
                {
                    Interlocked.Increment(ref callbackInvocations);
                    throw new InvalidOperationException("Expected leak-edge callback failure");
                };

                for (var i = 0; i < MessagesPerCycle; i++)
                {
                    var value = LeakGateHarness.RentPooled(ValueSize, i);
                    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    bool accepted;
                    if ((i & 1) == 0)
                    {
                        var completion = completionPool.Rent();
                        completionTasks.Add(completion.Task.AsTask());
                        accepted = await accumulator.AppendAsync(
                            Topic, i % PartitionCount, timestamp, PooledMemory.Null, value,
                            headers: null, headerCount: 0, completion, callback: null, cancellationToken);
                    }
                    else
                    {
                        expectedCallbacks++;
                        accepted = await accumulator.AppendAsync(
                            Topic, i % PartitionCount, timestamp, PooledMemory.Null, value,
                            headers: null, headerCount: 0, completionSource: null,
                            i % 3 == 0 ? throwingCallback : countingCallback,
                            cancellationToken);
                    }

                    if (!accepted)
                        throw new InvalidOperationException($"Cycle {cycle} message {i} rejected.");
                }

                var counters = await DrainCycleAsync(accumulator, MessagesPerCycle, FailEveryNthBatch, cancellationToken);
                foreach (var task in completionTasks)
                {
                    try
                    {
                        _ = await task.WaitAsync(cancellationToken);
                    }
                    catch (ExpectedInjectedFailureException)
                    {
                        // Awaited message landed in an intentionally failed batch.
                    }
                }

                await Assert.That(Volatile.Read(ref callbackInvocations)).IsEqualTo(expectedCallbacks);
                await Assert.That(accumulator.BufferedBytes).IsEqualTo(0L);
                await Assert.That(accumulator.InFlightBatchCount).IsEqualTo(0L);
                await Assert.That(accumulator.PendingAppendCountForTest).IsEqualTo(0);
            }
        }
        finally
        {
            await accumulator.DisposeAsync();
            await completionPool.DisposeAsync();
        }
    }

    /// <summary>
    /// Repeatedly abandons flush waits — alternating between a flush cancelled mid-wait and a
    /// flush called with an already-cancelled token — then drains cleanly. Cancelled flush
    /// waiters must not accumulate claim state, strand sealed batches, or distort accounting
    /// across cycles.
    /// </summary>
    [Test]
    [Timeout(120_000)]
    public async Task CancelledFlushes_RepeatedCycles_NoResidualClaimsOrStrandedBatches(
        CancellationToken cancellationToken)
    {
        const int Cycles = 20;
        const int MessagesPerCycle = 64;

        var accumulator = new RecordAccumulator(CreateOptions("leak-edge-flush", bufferMemory: 16UL * 1024 * 1024));
        var completionPool = new ValueTaskSourcePool<RecordMetadata>();

        try
        {
            for (var cycle = 0; cycle < Cycles; cycle++)
            {
                var completionTasks = new List<Task<RecordMetadata>>();
                for (var i = 0; i < MessagesPerCycle; i++)
                {
                    var value = LeakGateHarness.RentPooled(ValueSize, i);
                    var completion = completionPool.Rent();
                    completionTasks.Add(completion.Task.AsTask());
                    var accepted = await accumulator.AppendAsync(
                        Topic, i % PartitionCount, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        PooledMemory.Null, value, headers: null, headerCount: 0,
                        completion, callback: null, cancellationToken);
                    if (!accepted)
                        throw new InvalidOperationException($"Cycle {cycle} message {i} rejected.");
                }

                // Abandon a flush: nothing drains yet, so the wait is guaranteed pending
                // when the cancellation lands (even cycles) or already cancelled (odd cycles).
                using var flushCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                try
                {
                    if ((cycle & 1) == 0)
                    {
                        var abandonedFlush = accumulator.FlushAsync(flushCts.Token).AsTask();
                        await flushCts.CancelAsync();
                        await abandonedFlush;
                    }
                    else
                    {
                        // A pre-cancelled token makes FlushAsync throw synchronously at entry,
                        // so the call itself must be inside the try.
                        await flushCts.CancelAsync();
                        await accumulator.FlushAsync(flushCts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                }

                // A clean flush + drain must fully retire the wave despite the abandoned wait.
                var counters = await DrainCycleAsync(accumulator, MessagesPerCycle, failEveryNthBatch: 0, cancellationToken);
                foreach (var task in completionTasks)
                    _ = await task.WaitAsync(cancellationToken);

                await Assert.That(counters.DrainedRecords).IsEqualTo(MessagesPerCycle);
                await Assert.That(accumulator.BufferedBytes).IsEqualTo(0L);
                await Assert.That(accumulator.InFlightBatchCount).IsEqualTo(0L);
                await Assert.That(accumulator.UnsealedBatchCount).IsEqualTo(0);
            }
        }
        finally
        {
            await accumulator.DisposeAsync();
            await completionPool.DisposeAsync();
        }
    }

    private static ProducerOptions CreateOptions(string clientId, ulong bufferMemory) => new()
    {
        BootstrapServers = ["localhost:9092"],
        ClientId = clientId,
        BufferMemory = bufferMemory,
        BatchSize = 16384,
        LingerMs = 1_000,
        MaxBlockMs = 60_000,
    };

    /// <summary>
    /// Flushes, then drains inline as the sole simulated sender until every appended record
    /// has been retired.
    /// </summary>
    private static async Task<LeakGateCounters> DrainCycleAsync(
        RecordAccumulator accumulator,
        int expectedRecords,
        int failEveryNthBatch,
        CancellationToken cancellationToken)
    {
        var counters = new LeakGateCounters();
        var flushTask = accumulator.FlushAsync(cancellationToken).AsTask();

        while (counters.DrainedRecords < expectedRecords)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!LeakGateHarness.TryDrainAndRetireOne(accumulator, counters, failEveryNthBatch))
                await accumulator.WaitForWakeupAsync(5);
        }

        await flushTask.WaitAsync(cancellationToken);
        return counters;
    }
}
