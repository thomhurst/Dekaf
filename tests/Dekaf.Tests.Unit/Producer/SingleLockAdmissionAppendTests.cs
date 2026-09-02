using System.Reflection;
using System.Text;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Covers the single-lock span append fast path (admission-lease decision, BufferMemory
/// reservation, encode and commit under one partition-lock hold) with the per-broker admission
/// window enabled — the default configuration. The older admission tests drive the pooled-memory
/// entry points, which never take this path.
/// </summary>
public sealed class SingleLockAdmissionAppendTests
{
    private const string Topic = "single-lock-topic";
    private const int LeaderNodeId = 7;

    private static ProducerOptions CreateOptions(
        long capOverride,
        int batchSize,
        ulong bufferMemory = 64L * 1024 * 1024,
        int lingerMs = 60_000) => new()
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "single-lock-admission",
            BufferMemory = bufferMemory,
            BatchSize = batchSize,
            LingerMs = lingerMs,
            MaxBlockMs = 30_000,
            DeliveryLatencyTargetMs = 10,
            UnackedByteBudgetCapOverride = capOverride,
        };

    private static RecordAccumulator CreateAccumulator(ProducerOptions options)
        => new(options, resolveLeaderId: static (_, _) => LeaderNodeId);

    private static ValueTask<bool> AppendSpansAsync(
        RecordAccumulator accumulator,
        ReadOnlySpan<byte> value,
        int partition = 0,
        CancellationToken cancellationToken = default)
        => accumulator.AppendFromSpansAsync(
            Topic,
            partition,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            keyData: default,
            keyIsNull: true,
            valueData: value,
            valueIsNull: false,
            headers: null,
            headerCount: 0,
            callback: null,
            cancellationToken);

    private static async ValueTask SealAllAsync(RecordAccumulator accumulator)
    {
        var method = typeof(RecordAccumulator).GetMethod(
            "SealBatchesAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (ValueTask)method.Invoke(accumulator, [true, CancellationToken.None])!;
    }

    private static List<ReadyBatch> DrainAll(RecordAccumulator accumulator)
    {
        var batches = new List<ReadyBatch>();
        while (accumulator.TryDrainPublishedBatch(out var batch))
            batches.Add(batch);
        return batches;
    }

    private static int CountRecords(IEnumerable<ReadyBatch> batches)
        => batches.Sum(static batch => batch.RecordBatch.Records.Count);

    private static void RetireAll(RecordAccumulator accumulator, IEnumerable<ReadyBatch> batches)
    {
        var offset = 0;
        foreach (var batch in batches)
            LeakGateHarness.RetireBatch(accumulator, batch, offset++);
    }

    [Test]
    public async Task FastPath_ConsumesOneBatchLease_AndSealRefundsUnusedCredit()
    {
        // BatchSize below the 64 KB quantum cap: one lease per batch covers every record in it.
        const int batchSize = 4_096;
        var accumulator = CreateAccumulator(CreateOptions(capOverride: 1_000_000, batchSize));
        var value = new byte[100];

        try
        {
            var budget = accumulator.GetBrokerUnackedBudget(LeaderNodeId)!;

            // First record creates the batch on the two-phase path and takes the batch lease.
            await Assert.That(await AppendSpansAsync(accumulator, value)).IsTrue();
            await Assert.That(budget.UnackedBytes).IsEqualTo(batchSize);

            // Later records commit on the single-lock path from local credit: no new lease.
            for (var i = 0; i < 10; i++)
                await Assert.That(await AppendSpansAsync(accumulator, value)).IsTrue();
            await Assert.That(budget.UnackedBytes).IsEqualTo(batchSize);

            await SealAllAsync(accumulator);
            var batches = DrainAll(accumulator);
            await Assert.That(batches.Count).IsEqualTo(1);
            await Assert.That(CountRecords(batches)).IsEqualTo(11);

            // Seal charges the bytes actually written and hands the unused credit back.
            await Assert.That(budget.UnackedBytes).IsEqualTo(batches[0].DataSize);
            await Assert.That(budget.UnackedBytes).IsLessThan(batchSize);

            RetireAll(accumulator, batches);
            await Assert.That(budget.UnackedBytes).IsEqualTo(0);
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task FastPath_ReplenishesLease_WhenBatchLocalCreditIsExhausted()
    {
        // BatchSize above the 64 KB quantum cap: the batch outgrows its first lease and the
        // single-lock path must reserve the next quantum itself.
        const int batchSize = 128 * 1024;
        const int quantum = 64 * 1024;
        var accumulator = CreateAccumulator(CreateOptions(capOverride: 100_000_000, batchSize));
        var value = new byte[1_000];

        try
        {
            var budget = accumulator.GetBrokerUnackedBudget(LeaderNodeId)!;

            for (var i = 0; i < 100; i++)
                await Assert.That(await AppendSpansAsync(accumulator, value)).IsTrue();

            // ~100 KB of records: the first lease (64 KB) ran out and exactly one more was taken.
            await Assert.That(budget.UnackedBytes).IsEqualTo(2L * quantum);
            await Assert.That(accumulator.TryGetBatch(Topic, 0, out var batch)).IsTrue();
            await Assert.That(batch!.RecordCount).IsEqualTo(100);

            await SealAllAsync(accumulator);
            var batches = DrainAll(accumulator);
            await Assert.That(CountRecords(batches)).IsEqualTo(100);
            await Assert.That(budget.UnackedBytes).IsEqualTo(batches.Sum(static b => (long)b.DataSize));

            RetireAll(accumulator, batches);
            await Assert.That(budget.UnackedBytes).IsEqualTo(0);
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    [Timeout(60_000)]
    public async Task ConcurrentFastPathAppends_SamePartition_CommitEveryRecordAndBalanceAccounting(
        CancellationToken cancellationToken)
    {
        // The broker window opens from one BatchSize and only grows with acknowledgements, so a
        // sender stand-in must retire sealed batches while eight threads contend for one
        // partition's lock; every record must land exactly once and both ledgers must return
        // to zero.
        const int threadCount = 8;
        const int appendsPerThread = 500;
        const int batchSize = 16 * 1024;
        var accumulator = CreateAccumulator(CreateOptions(capOverride: 1_000_000_000, batchSize));
        var value = Encoding.UTF8.GetBytes(new string('v', 200));

        try
        {
            var budget = accumulator.GetBrokerUnackedBudget(LeaderNodeId)!;
            var retired = 0L;
            using var drainerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var drainer = Task.Run(() => RetireUntilCancelled(accumulator, drainerCts.Token, ref retired), CancellationToken.None);

            var appended = 0;
            var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(async () =>
            {
                for (var i = 0; i < appendsPerThread; i++)
                {
                    if (await AppendSpansAsync(accumulator, value, partition: 0, cancellationToken))
                        Interlocked.Increment(ref appended);
                }
            }, cancellationToken)).ToArray();
            await Task.WhenAll(tasks);

            await Assert.That(appended).IsEqualTo(threadCount * appendsPerThread);

            await SealAllAsync(accumulator);
            await WaitForRetiredAsync(threadCount * appendsPerThread, () => Volatile.Read(ref retired), cancellationToken);
            drainerCts.Cancel();
            await drainer;

            await Assert.That(Volatile.Read(ref retired)).IsEqualTo(threadCount * appendsPerThread);
            await Assert.That(budget.UnackedBytes).IsEqualTo(0);
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
            await Assert.That(budget.AccountingUnderflowCount).IsEqualTo(0);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    /// <summary>Sender stand-in: retires every sealed batch the production way until cancelled.</summary>
    private static void RetireUntilCancelled(RecordAccumulator accumulator, CancellationToken cancellationToken, ref long retired)
    {
        var offset = 0L;
        var spinner = new SpinWait();
        while (!cancellationToken.IsCancellationRequested)
        {
            if (accumulator.TryDrainPublishedBatch(out var batch))
            {
                var recordCount = batch.RecordBatch.Records.Count;
                LeakGateHarness.RetireBatch(accumulator, batch, offset++);
                Interlocked.Add(ref retired, recordCount);
                spinner.Reset();
            }
            else
            {
                spinner.SpinOnce();
            }
        }
    }

    private static async Task WaitForRetiredAsync(long expected, Func<long> retired, CancellationToken cancellationToken)
    {
        while (retired() < expected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(5, cancellationToken);
        }
    }

    [Test]
    [Timeout(60_000)]
    public async Task ConcurrentFastPathAppends_TinyBufferMemory_NeverExceedsLimitAndDrainToZero(
        CancellationToken cancellationToken)
    {
        // BufferMemory holds three batches; appends must alternate between the single-lock fast
        // path and BufferMemory backpressure without losing a record or leaking a refund.
        const int threadCount = 4;
        const int appendsPerThread = 400;
        const int batchSize = 8 * 1024;
        var options = CreateOptions(
            capOverride: 1_000_000_000,
            batchSize,
            bufferMemory: 3UL * batchSize,
            lingerMs: 0);
        var accumulator = CreateAccumulator(options);
        var value = new byte[300];

        try
        {
            var budget = accumulator.GetBrokerUnackedBudget(LeaderNodeId)!;
            var retired = 0L;
            using var drainerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var drainer = Task.Run(() => RetireUntilCancelled(accumulator, drainerCts.Token, ref retired), CancellationToken.None);

            var appended = 0;
            var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(async () =>
            {
                for (var i = 0; i < appendsPerThread; i++)
                {
                    if (await AppendSpansAsync(accumulator, value, partition: 0, cancellationToken))
                        Interlocked.Increment(ref appended);
                }
            }, cancellationToken)).ToArray();
            await Task.WhenAll(tasks);

            await Assert.That(appended).IsEqualTo(threadCount * appendsPerThread);

            // Seal the trailing partial batch and let the drainer retire everything.
            await SealAllAsync(accumulator);
            await WaitForRetiredAsync(threadCount * appendsPerThread, () => Volatile.Read(ref retired), cancellationToken);
            drainerCts.Cancel();
            await drainer;

            await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
            await Assert.That(budget.UnackedBytes).IsEqualTo(0);
            await Assert.That(budget.AccountingUnderflowCount).IsEqualTo(0);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task FastPath_BlockedByBrokerBudget_FallsBackAndRecordsAdmissionBlock()
    {
        // Cap of 1 byte: the first sealed batch puts the broker over budget.
        const int batchSize = 4_096;
        var accumulator = CreateAccumulator(CreateOptions(capOverride: 1, batchSize));
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var value = new byte[100];

        try
        {
            var budget = accumulator.GetBrokerUnackedBudget(LeaderNodeId)!;
            await Assert.That(await AppendSpansAsync(accumulator, value)).IsTrue();
            await SealAllAsync(accumulator);
            await Assert.That(budget.IsOverBudget()).IsTrue();

            var blockEventsBefore = budget.AdmissionBlockEvents;
            var completion = pool.Rent();
            var admitted = accumulator.TryAppendFromSpansWithCompletion(
                Topic, 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                keyData: default, keyIsNull: true, value, valueIsNull: false,
                headers: null, headerCount: 0, completion);

            await Assert.That(admitted).IsFalse();
            await Assert.That(budget.AdmissionBlockEvents).IsGreaterThan(blockEventsBefore);
            pool.Return(completion);

            // Nothing was reserved by the refused attempt.
            var sealedBatches = DrainAll(accumulator);
            await Assert.That(accumulator.BufferedBytes)
                .IsEqualTo(sealedBatches.Sum(static b => (long)b.DataSize));

            RetireAll(accumulator, sealedBatches);
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
            await Assert.That(budget.UnackedBytes).IsEqualTo(0);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(30_000)]
    public async Task QueuedSpanAppend_LandsInNewBatch_AfterRotationAndChargedBatchExit(
        CancellationToken cancellationToken)
    {
        const int batchSize = 4_096;
        var accumulator = CreateAccumulator(CreateOptions(capOverride: 1, batchSize));
        var value = new byte[100];

        try
        {
            var budget = accumulator.GetBrokerUnackedBudget(LeaderNodeId)!;
            await Assert.That(await AppendSpansAsync(accumulator, value, partition: 0, cancellationToken)).IsTrue();
            await SealAllAsync(accumulator);
            var charged = DrainAll(accumulator);
            await Assert.That(charged.Count).IsEqualTo(1);
            await Assert.That(budget.IsOverBudget()).IsTrue();

            // Over budget: the fast path yields and the record queues behind admission.
            var blocked = AppendSpansAsync(accumulator, value, partition: 0, cancellationToken);
            await Assert.That(blocked.IsCompleted).IsFalse();

            // A later fast-path record must not leapfrog the queued one.
            var later = AppendSpansAsync(accumulator, value, partition: 0, cancellationToken);
            await Assert.That(later.IsCompleted).IsFalse();

            // Rotation while blocked: the current batch (if any) is sealed away, then the charged
            // batch exits the pipeline and reopens the window for the queued records.
            await SealAllAsync(accumulator);
            RetireAll(accumulator, charged);

            await Assert.That(await blocked).IsTrue();
            await Assert.That(await later).IsTrue();

            await SealAllAsync(accumulator);
            var landed = DrainAll(accumulator);
            await Assert.That(CountRecords(landed)).IsEqualTo(2);

            RetireAll(accumulator, landed);
            await Assert.That(budget.UnackedBytes).IsEqualTo(0);
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }
}
