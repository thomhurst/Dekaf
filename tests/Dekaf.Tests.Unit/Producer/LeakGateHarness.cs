using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Shared simulated-sender harness for the produce-path leak gates
/// (<c>ProducerMemoryLeakGateTests</c>, <c>ProducerLeakUnderLoadTests</c>,
/// <c>ProducerLeakEdgeCaseTests</c>). Centralizes the two pieces of
/// production-mirroring knowledge the gates' validity depends on: the batch
/// retirement sequence (<see cref="RetireBatch"/>) and the wakeup-signal
/// contract (<see cref="DrainWaitMode"/>).
/// </summary>
internal static class LeakGateHarness
{
    /// <summary>
    /// Retires one drained batch exactly the way <c>BrokerSender</c>'s cleanup path does:
    /// CompleteSend/Fail → ReleaseBatchMemory → OnBatchExitsPipeline → ReturnReadyBatch.
    /// The leak gates' validity depends on this mirror staying faithful to the production
    /// sequence — if the sender's retirement contract changes, change it here, once.
    /// </summary>
    public static void RetireBatch(
        RecordAccumulator accumulator,
        ReadyBatch batch,
        long offset,
        Exception? failWith = null)
    {
        if (failWith is not null)
            batch.Fail(failWith);
        else
            batch.CompleteSend(offset, DateTimeOffset.UtcNow);

        accumulator.ReleaseBatchMemory(batch);
        accumulator.OnBatchExitsPipeline(batch);
        accumulator.ReturnReadyBatch(batch);
    }

    /// <summary>
    /// Drains one batch if available and retires it, failing every
    /// <paramref name="failEveryNthBatch"/>th batch with <see cref="ExpectedInjectedFailureException"/>
    /// (0 = never fail). Returns false when nothing was drainable.
    /// </summary>
    public static bool TryDrainAndRetireOne(
        RecordAccumulator accumulator,
        LeakGateCounters counters,
        int failEveryNthBatch)
    {
        if (!accumulator.TryDrainBatch(out var batch))
            return false;

        var sequence = Interlocked.Increment(ref counters.DrainSequence);
        var recordCount = batch!.RecordBatch.Records.Count;

        Exception? failWith = null;
        if (failEveryNthBatch > 0 && sequence % failEveryNthBatch == 0)
        {
            failWith = new ExpectedInjectedFailureException();
            Interlocked.Increment(ref counters.FailedBatchCount);
        }

        RetireBatch(accumulator, batch, sequence, failWith);

        Interlocked.Increment(ref counters.DrainedBatchCount);
        Interlocked.Add(ref counters.DrainedRecordCount, recordCount);
        return true;
    }

    /// <summary>
    /// Simulated sender loop: drains and retires batches until
    /// <see cref="LeakGateCounters.RequestStop"/> is called and the pipeline is empty.
    /// Tolerates the accumulator being disposed underneath it.
    /// </summary>
    public static async Task DrainUntilStoppedAsync(
        RecordAccumulator accumulator,
        LeakGateCounters counters,
        int failEveryNthBatch,
        DrainWaitMode waitMode,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool drained;
            try
            {
                drained = TryDrainAndRetireOne(accumulator, counters, failEveryNthBatch);
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            if (drained)
                continue;
            if (counters.StopRequested)
                break;

            if (waitMode == DrainWaitMode.WakeupSignal)
                await accumulator.WaitForWakeupAsync(5);
            else
                await Task.Delay(1, cancellationToken);
        }
    }

    /// <summary>
    /// Appends until <paramref name="messageCount"/> messages are accepted, the token is
    /// cancelled, or the accumulator rejects the append (disposal). Even-indexed messages
    /// are awaited via a rented completion source; odd-indexed messages are fire-and-forget
    /// with a callback that increments <see cref="LeakGateCounters.CallbackInvocations"/>.
    /// A completion task is recorded only after the append is accepted, so an append that
    /// throws leaves nothing to await.
    /// </summary>
    public static async Task<AppenderResult> RunAppenderAsync(
        RecordAccumulator accumulator,
        ValueTaskSourcePool<RecordMetadata> completionPool,
        LeakGateCounters counters,
        int appenderId,
        int messageCount,
        int partitionCount,
        int valueSize,
        string topic,
        CancellationToken cancellationToken)
    {
        var completionTasks = new List<Task<RecordMetadata>>();
        var acceptedTotal = 0;
        var acceptedFireAndForget = 0;
        var wasCancelled = false;
        Action<RecordMetadata, Exception?> callback =
            (_, _) => Interlocked.Increment(ref counters.CallbackInvocations);

        for (var i = 0; i < messageCount; i++)
        {
            var value = RentPooled(valueSize, appenderId * 31 + i);
            var partition = (appenderId + i * 7) % partitionCount;
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            try
            {
                bool accepted;
                if ((i & 1) == 0)
                {
                    var completion = completionPool.Rent();
                    var completionTask = completion.Task.AsTask();
                    accepted = await accumulator.AppendAsync(
                        topic, partition, timestamp, PooledMemory.Null, value,
                        headers: null, headerCount: 0, completion, callback: null, cancellationToken);
                    if (accepted)
                        completionTasks.Add(completionTask);
                }
                else
                {
                    accepted = await accumulator.AppendAsync(
                        topic, partition, timestamp, PooledMemory.Null, value,
                        headers: null, headerCount: 0, completionSource: null,
                        callback, cancellationToken);
                    if (accepted)
                        acceptedFireAndForget++;
                }

                if (!accepted)
                    break;

                acceptedTotal++;
                Interlocked.Increment(ref counters.AcceptedTotal);
            }
            catch (OperationCanceledException)
            {
                wasCancelled = true;
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }

        return new AppenderResult(acceptedTotal, acceptedFireAndForget, wasCancelled, completionTasks);
    }

    /// <summary>
    /// Waits until at least one appender is provably in the slow-path memory reservation
    /// wait (the mechanism gated saturation tests need), escaping if every appender
    /// finishes without pressure so a mis-sized scenario fails on its assertions instead
    /// of burning the test timeout.
    /// </summary>
    public static Task WaitForSaturationAsync(
        RecordAccumulator accumulator,
        IReadOnlyList<Task> appenderTasks,
        CancellationToken cancellationToken)
    {
        return TestWait.UntilAsync(
            () => accumulator.BufferPressureEvents > 0 || appenderTasks.All(static t => t.IsCompleted),
            cancellationToken,
            TimeSpan.FromMilliseconds(1));
    }

    /// <summary>
    /// Managed-heap size after a full stabilization pass. <c>GC.GetTotalMemory(true)</c>
    /// alone collects but does not wait for pending finalizers, so objects queued for
    /// finalization by earlier tests in the same process can drift a raw before/after
    /// delta; the collect → wait → collect dance removes that noise.
    /// </summary>
    public static long GetStableHeapBytes()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return GC.GetTotalMemory(forceFullCollection: true);
    }

    /// <summary>Rents a pooled payload of <paramref name="size"/> bytes filled from the seed.</summary>
    public static PooledMemory RentPooled(int size, int seed)
    {
        var buffer = ProducerDataPool.BytePool.Rent(size);
        buffer.AsSpan(0, size).Fill((byte)seed);
        return new PooledMemory(buffer, size);
    }
}

/// <summary>
/// How the drain loop idles when the pipeline is momentarily empty.
/// <see cref="RecordAccumulator.WaitForWakeupAsync"/> is backed by a single-waiter pooled
/// ValueTask source that is torn down by disposal, so <see cref="WakeupSignal"/> is valid
/// only for the sole signal-awaiting drainer on an accumulator that outlives the loop
/// (production sender loops exit via their own cancellation before disposal, so they never
/// violate this). Any additional concurrent drainer, and any drainer that may outlive the
/// accumulator, must use <see cref="Poll"/>.
/// </summary>
internal enum DrainWaitMode
{
    WakeupSignal,
    Poll,
}

/// <summary>Shared mutable counters for one leak-gate scenario run.</summary>
internal sealed class LeakGateCounters
{
    public int AcceptedTotal;
    public int CallbackInvocations;
    public int DrainSequence;
    public int DrainedBatchCount;
    public int DrainedRecordCount;
    public int FailedBatchCount;
    private int _stopRequested;

    public int DrainedBatches => Volatile.Read(ref DrainedBatchCount);
    public int DrainedRecords => Volatile.Read(ref DrainedRecordCount);
    public int FailedBatches => Volatile.Read(ref FailedBatchCount);
    public bool StopRequested => Volatile.Read(ref _stopRequested) != 0;

    public void RequestStop() => Volatile.Write(ref _stopRequested, 1);
}

/// <summary>Per-appender outcome from <see cref="LeakGateHarness.RunAppenderAsync"/>.</summary>
internal sealed record AppenderResult(
    int AcceptedTotal,
    int AcceptedFireAndForget,
    bool WasCancelled,
    List<Task<RecordMetadata>> CompletionTasks);

/// <summary>Marker exception for intentionally failed batches in leak-gate scenarios.</summary>
internal sealed class ExpectedInjectedFailureException : Exception;
