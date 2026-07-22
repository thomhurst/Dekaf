using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Deterministic leak gates for the produce path. Each test runs repeated
/// produce → flush → drain → complete waves through a <see cref="RecordAccumulator"/>
/// with a simulated sender and asserts that every resource-accounting invariant
/// returns exactly to zero after every wave.
/// </summary>
/// <remarks>
/// Leak classes this gate catches, and how:
/// <list type="bullet">
/// <item><description>BufferMemory refund leaks (the #1516/#2199 family): a missed or
/// double-counted <c>ReleaseBatchMemory</c> leaves <c>BufferedBytes</c> non-zero after the
/// wave completes — caught on the first leaking cycle, not after gigabytes of drift.</description></item>
/// <item><description>Pipeline tracking leaks: <c>InFlightBatchCount</c> or the pending-append
/// queue not draining to zero means batches or admissions are stranded.</description></item>
/// <item><description>Pool imbalance (the #2187 family): rent/return counters must match
/// exactly with zero duplicate returns in this race-free harness. Note these counter
/// assertions are Debug-only — <c>ProducerDebugCounters</c> is compiled out of Release
/// builds, so this class of check runs on Debug executions, not in Release CI.</description></item>
/// <item><description>Rooted-object leaks (completed batches or completion sources retained
/// anywhere): steady-state managed-heap growth across cycles, measured after full GC, must
/// stay near zero. Historical leaks in this class measured tens of MB at this message volume
/// (e.g. ~2 KB/message string caching would show as ~100 MB here).</description></item>
/// </list>
/// Unlike the integration-level <c>BufferMemoryStressTests</c> (15 s wall clock, 2 GB
/// threshold, requires Docker), these run broker-free in seconds and fail on first drift.
/// </remarks>
[NotInParallel]
public sealed class ProducerMemoryLeakGateTests
{
    private const string Topic = "leak-gate";
    private const int PartitionCount = 4;
    private const int MessagesPerCycle = 512;
    private const int AwaitedPerCycle = MessagesPerCycle / 2;
    private const int ValueSize = 1024;

    [Test]
    [Timeout(120_000)]
    public async Task RepeatedProduceDrainCycles_AccountingReturnsToZero_EveryCycle(
        CancellationToken cancellationToken)
    {
        const int CycleCount = 50;

        var accumulator = new RecordAccumulator(CreateOptions());
        var completionPool = new ValueTaskSourcePool<RecordMetadata>();
        ProducerDebugCounters.Reset();

        var totalBatches = 0;
        try
        {
            for (var cycle = 0; cycle < CycleCount; cycle++)
            {
                var result = await RunProduceDrainCycleAsync(accumulator, completionPool, cancellationToken);
                totalBatches += result.DrainedBatches;

                // Every wave must retire completely: any residue here is a leak that
                // compounds per wave in a long-running producer.
                await Assert.That(result.DrainedRecords).IsEqualTo(MessagesPerCycle);
                await Assert.That(result.CallbackInvocations).IsEqualTo(MessagesPerCycle - AwaitedPerCycle);
                await Assert.That(accumulator.BufferedBytes).IsEqualTo(0L);
                await Assert.That(accumulator.InFlightBatchCount).IsEqualTo(0L);
                await Assert.That(accumulator.UnsealedBatchCount).IsEqualTo(0);
                await Assert.That(accumulator.PendingAppendCountForTest).IsEqualTo(0);
            }

#if DEBUG
            var counters = ProducerDebugCounters.GetSnapshot();
            const int TotalMessages = CycleCount * MessagesPerCycle;
            const int AwaitedMessages = CycleCount * AwaitedPerCycle;
            await Assert.That(counters.MessagesAppended).IsEqualTo(TotalMessages);
            await Assert.That(counters.CompletionSourcesStoredInBatch).IsEqualTo(AwaitedMessages);
            await Assert.That(counters.CompletionSourcesCompleted).IsEqualTo(AwaitedMessages);
            await Assert.That(counters.CompletionSourcesFailed).IsEqualTo(0);
            await Assert.That(counters.ReadyBatchesRented).IsEqualTo(totalBatches);
            await Assert.That(counters.ReadyBatchesReturned).IsEqualTo(totalBatches);
            await Assert.That(counters.ReadyBatchDuplicateReturns).IsEqualTo(0);
            await Assert.That(counters.BatchesSentSuccessfully).IsEqualTo(totalBatches);
            await Assert.That(counters.BatchesFailed).IsEqualTo(0);
#endif
        }
        finally
        {
            await accumulator.DisposeAsync();
            await completionPool.DisposeAsync();
            ProducerDebugCounters.Reset();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task RepeatedProduceDrainCycles_ManagedHeapDoesNotGrow(
        CancellationToken cancellationToken)
    {
        const int WarmupCycles = 8;
        const int MeasuredCycles = 100;

        // Pool population, channel segments, and arena growth all happen during warmup;
        // the measured region below is pure steady-state reuse and must retain ~nothing.
        // A real rooted-object leak at this volume (51,200 messages, ~50 MB of payload)
        // measures tens of MB; the 8 MB bound leaves generous headroom for GC bookkeeping
        // while staying an order of magnitude below any historical produce-path leak.
        const long MaxRetainedGrowthBytes = 8 * 1024 * 1024;

        var accumulator = new RecordAccumulator(CreateOptions());
        var completionPool = new ValueTaskSourcePool<RecordMetadata>();

        try
        {
            for (var cycle = 0; cycle < WarmupCycles; cycle++)
                await RunProduceDrainCycleAsync(accumulator, completionPool, cancellationToken);

            var baseline = LeakGateHarness.GetStableHeapBytes();

            for (var cycle = 0; cycle < MeasuredCycles; cycle++)
                await RunProduceDrainCycleAsync(accumulator, completionPool, cancellationToken);

            var retainedGrowth = LeakGateHarness.GetStableHeapBytes() - baseline;

            await Assert.That(accumulator.BufferedBytes).IsEqualTo(0L);
            await Assert.That(retainedGrowth).IsLessThan(MaxRetainedGrowthBytes);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await completionPool.DisposeAsync();
        }
    }

    private static ProducerOptions CreateOptions() => new()
    {
        BootstrapServers = ["localhost:9092"],
        ClientId = "leak-gate",
        BufferMemory = 16UL * 1024 * 1024,
        BatchSize = 16384,
        LingerMs = 1_000,
    };

    /// <summary>
    /// One full produce wave: append (half awaited, half fire-and-forget with callbacks),
    /// flush, then drain inline as the simulated sender via
    /// <see cref="LeakGateHarness.TryDrainAndRetireOne"/>.
    /// </summary>
    private static async Task<LeakGateCounters> RunProduceDrainCycleAsync(
        RecordAccumulator accumulator,
        ValueTaskSourcePool<RecordMetadata> completionPool,
        CancellationToken cancellationToken)
    {
        var counters = new LeakGateCounters();
        var completionTasks = new List<Task<RecordMetadata>>(AwaitedPerCycle);
        Action<RecordMetadata, Exception?> callback =
            (_, _) => Interlocked.Increment(ref counters.CallbackInvocations);

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
                accepted = await accumulator.AppendAsync(
                    Topic, i % PartitionCount, timestamp, PooledMemory.Null, value,
                    headers: null, headerCount: 0, completionSource: null, callback, cancellationToken);
            }

            if (!accepted)
                throw new InvalidOperationException($"Append {i} rejected — accumulator disposed?");
        }

        // Flush seals every current batch; drain inline as the simulated sender.
        var flushTask = accumulator.FlushAsync(cancellationToken).AsTask();

        while (counters.DrainedRecords < MessagesPerCycle)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!LeakGateHarness.TryDrainAndRetireOne(accumulator, counters, failEveryNthBatch: 0))
                await accumulator.WaitForWakeupAsync(10);
        }

        await flushTask.WaitAsync(cancellationToken);

        foreach (var task in completionTasks)
            _ = await task.WaitAsync(cancellationToken);

        return counters;
    }
}
