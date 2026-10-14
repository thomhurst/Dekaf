using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Live batch arenas (open and in-flight batches) are bounded: batches created while live arenas
/// exceed the GC-derived allowance charge their arena against BufferMemory, and an arena
/// allocation that runs out of GC heap releases idle arenas, retries, waits for in-flight
/// batches, and fails with a <see cref="ProduceException"/> instead of an OutOfMemoryException.
/// </summary>
/// <remarks>
/// Batch rentals fail for real: the tests raise the options' ArenaCapacity beyond the maximum
/// array length. Only the cold swap, growth and retry paths have test seams.
/// </remarks>
[NotInParallel(nameof(BatchArena))]
public sealed class LiveArenaBackpressureTests
{
    private const string Topic = "live-arena-topic";
    private const int PartitionCount = 64;
    private const int BatchSize = 16_384;

    // Distinct from any other test's arena size, so the injected growth failure hits only this class.
    private const int InjectedArenaCapacity = 37_089;

    // Beyond Array.MaxLength: an arena of this capacity fails to allocate for real, with the
    // OutOfMemoryException the GC throws when the heap is at its limit.
    private const int UnallocatableArenaCapacity = int.MaxValue;

    /// <summary>
    /// Changes the arena capacity the accumulator's batches rent from now on. Tests use it to make
    /// arena allocations fail (and succeed again) for real, without seams on the rent path.
    /// </summary>
    private static void SetArenaCapacity(ProducerOptions options, int arenaCapacity)
        => typeof(ProducerOptions).GetProperty(nameof(ProducerOptions.ArenaCapacity))!.SetValue(options, arenaCapacity);

    private static ProducerOptions CreateOptions(
        ulong bufferMemory = 64 * 1024,
        int maxBlockMs = 30_000,
        int arenaCapacity = 0,
        CompressionType compressionType = CompressionType.None) => new()
    {
        BootstrapServers = ["localhost:9092"],
        ClientId = "live-arena-test",
        BatchSize = BatchSize,
        ArenaCapacity = arenaCapacity,
        BufferMemory = bufferMemory,
        LingerMs = 60_000,
        MaxBlockMs = maxBlockMs,
        CompressionType = compressionType,
    };

    [Test]
    public async Task LiveArenas_WithinAllowance_AreCountedButNotCharged()
    {
        await using var accumulator = new RecordAccumulator(CreateOptions(bufferMemory: 10_000_000));
        var budget = new LiveArenaBudget(long.MaxValue);
        accumulator.UseLiveArenaBudgetForTest(budget);

        for (var partition = 0; partition < 4; partition++)
            await Assert.That(await AccumulatorTestHelpers.AppendNullRecordAsync(accumulator, Topic, partition, PartitionCount)).IsTrue();

        await Assert.That(budget.LiveBytes).IsGreaterThanOrEqualTo(4L * ProducerOptions.GetEffectiveArenaCapacity(BatchSize, 0));
        await Assert.That(accumulator.ArenaChargedBytes).IsEqualTo(0);

        await CompleteAllBatchesAsync(accumulator, partitions: 4);

        await Assert.That(budget.LiveBytes).IsEqualTo(0);
        await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
    }

    [Test]
    public async Task LiveArenaBound_Reached_BackpressuresAppends_UntilBatchesComplete()
    {
        await using var accumulator = new RecordAccumulator(CreateOptions());
        // Every arena is over the allowance, so each new batch charges its arena to BufferMemory.
        var budget = new LiveArenaBudget(0);
        accumulator.UseLiveArenaBudgetForTest(budget);

        var (blocked, blockedPartition) = await AppendUntilBlockedAsync(accumulator, CancellationToken.None);

        await Assert.That(blockedPartition).IsGreaterThan(0);
        await Assert.That(accumulator.ArenaChargedBytes).IsEqualTo(budget.LiveBytes);
        await Assert.That((ulong)accumulator.BufferedBytes).IsGreaterThan(accumulator.MaxBufferMemory);

        // Completing the in-flight batches releases their arenas and the charge, which drains the
        // queued append.
        await CompleteAllBatchesAsync(accumulator, blockedPartition);
        await Assert.That(await blocked.AsTask().WaitAsync(TimeSpan.FromSeconds(10))).IsTrue();

        await CompleteAllBatchesAsync(accumulator, blockedPartition + 1);
        await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
        await Assert.That(accumulator.ArenaChargedBytes).IsEqualTo(0);
        await Assert.That(budget.LiveBytes).IsEqualTo(0);
    }

    [Test]
    public async Task LiveArenaBound_DrainOfManyPartitions_ChargesAtMostOneArenaPastBufferMemory()
    {
        await using var accumulator = new RecordAccumulator(CreateOptions());
        // Every arena is over the allowance, so each new batch charges its arena to BufferMemory.
        var budget = new LiveArenaBudget(0);
        accumulator.UseLiveArenaBudgetForTest(budget);
        var arenaCapacity = ProducerOptions.GetEffectiveArenaCapacity(BatchSize, 0);

        var (firstBlocked, blockedPartition) = await AppendUntilBlockedAsync(accumulator, CancellationToken.None);
        await Assert.That(blockedPartition).IsGreaterThan(0);

        // Queue small records for many more partitions, each needing a batch of its own.
        var queued = new List<ValueTask<bool>> { firstBlocked };
        for (var partition = blockedPartition + 1; partition < PartitionCount; partition++)
        {
            var append = AccumulatorTestHelpers.AppendNullRecordAsync(accumulator, Topic, partition, PartitionCount);
            await Assert.That(append.IsCompleted).IsFalse();
            queued.Add(append);
        }

        // Completing the open batches frees their charges. A drain serving every queued record
        // at once would reserve only record bytes and then charge one arena per partition.
        await CompleteAllBatchesAsync(accumulator, blockedPartition);

        // Each served record opened a batch charging at least one arena, so once charges pass
        // BufferMemory the drain stops: at most BufferMemory / arena + 1 records are served.
        // (Arenas can come from the shared pool larger than requested, which only lowers this.)
        var served = queued.Count(static append => append.IsCompleted);
        await Assert.That(served)
            .IsLessThanOrEqualTo((int)((long)accumulator.MaxBufferMemory / arenaCapacity) + 1);
        await Assert.That(served).IsLessThan(queued.Count);

        // Every queued record is still appended, as earlier batches complete.
        for (var round = 0; round < PartitionCount && queued.Any(static append => !append.IsCompleted); round++)
            await CompleteAllBatchesAsync(accumulator, PartitionCount);

        foreach (var append in queued)
            await Assert.That(await append.AsTask().WaitAsync(TimeSpan.FromSeconds(10))).IsTrue();

        await CompleteAllBatchesAsync(accumulator, PartitionCount);
        await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
        await Assert.That(accumulator.ArenaChargedBytes).IsEqualTo(0);
        await Assert.That(budget.LiveBytes).IsEqualTo(0);
    }

    [Test]
    public async Task LiveArenaBound_CancellationWhileWaiting_PreventsDelivery()
    {
        await using var accumulator = new RecordAccumulator(CreateOptions());
        var budget = new LiveArenaBudget(0);
        accumulator.UseLiveArenaBudgetForTest(budget);
        using var cts = new CancellationTokenSource();

        var (blocked, blockedPartition) = await AppendUntilBlockedAsync(accumulator, cts.Token);
        await Assert.That(blockedPartition).IsGreaterThan(0);

        await cts.CancelAsync();
        await Assert.That(async () => await blocked).Throws<OperationCanceledException>();

        // Freeing every arena afterwards must not resurrect the cancelled record.
        await CompleteAllBatchesAsync(accumulator, blockedPartition);
        await AccumulatorTestHelpers.SealAllAsync(accumulator);
        await Assert.That(accumulator.TryDrainBatch(new TopicPartition(Topic, blockedPartition), out _)).IsFalse();
        await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
        await Assert.That(budget.LiveBytes).IsEqualTo(0);
    }

    [Test]
    public async Task ArenaOutOfMemory_ReleasesIdleArenas_AndRetriesOnce()
    {
        var options = CreateOptions(bufferMemory: 10_000_000, arenaCapacity: InjectedArenaCapacity);
        await using var accumulator = new RecordAccumulator(options);
        var releases = 0;
        SetArenaCapacity(options, UnallocatableArenaCapacity);
        BatchArena.AfterReleasingIdleArenasForTest = () =>
        {
            // The retry after releasing the idle arenas finds memory again.
            Interlocked.Increment(ref releases);
            SetArenaCapacity(options, InjectedArenaCapacity);
        };

        try
        {
            await Assert.That(await AccumulatorTestHelpers.AppendNullRecordAsync(accumulator, Topic, 0, PartitionCount)).IsTrue();
            await Assert.That(releases).IsEqualTo(1);
        }
        finally
        {
            BatchArena.AfterReleasingIdleArenasForTest = null;
        }
    }

    [Test]
    public async Task ArenaOutOfMemory_NothingInFlight_FailsWithProduceException()
    {
        var options = CreateOptions(bufferMemory: 10_000_000, arenaCapacity: InjectedArenaCapacity);
        await using var accumulator = new RecordAccumulator(options);
        SetArenaCapacity(options, UnallocatableArenaCapacity);

        var exception = await Assert.That(
            async () => await AccumulatorTestHelpers.AppendNullRecordAsync(accumulator, Topic, 0, PartitionCount))
            .Throws<ProduceException>();

        await Assert.That(exception!.Kind).IsEqualTo(ProduceErrorKind.ArenaAllocationFailed);
        await Assert.That(exception.Message).Contains("arena allocation failed");
        await Assert.That(exception.Message).Contains("GC heap limit");
        await Assert.That(exception.InnerException).IsTypeOf<OutOfMemoryException>();
        await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
    }

    [Test]
    public async Task ArenaOutOfMemory_WithBatchInFlight_WaitsBoundedByMaxBlock_ThenTimesOut()
    {
        var options = CreateOptions(bufferMemory: 10_000_000, maxBlockMs: 300, arenaCapacity: InjectedArenaCapacity);
        await using var accumulator = new RecordAccumulator(options);

        // One batch in flight: its completion could free an arena.
        await Assert.That(await AccumulatorTestHelpers.AppendNullRecordAsync(accumulator, Topic, 0, PartitionCount)).IsTrue();
        await AccumulatorTestHelpers.SealAllAsync(accumulator);
        await Assert.That(accumulator.InFlightBatchCount).IsEqualTo(1);

        SetArenaCapacity(options, UnallocatableArenaCapacity);
        var waiting = AccumulatorTestHelpers.AppendNullRecordAsync(accumulator, Topic, 1, PartitionCount);
        await Assert.That(waiting.IsCompleted).IsFalse();
        await Assert.That(accumulator.ArenaDebtBytes).IsGreaterThan(0);

        var exception = await Assert.That(async () => await waiting).Throws<KafkaTimeoutException>();
        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.MaxBlock);
        await Assert.That(exception.Message).Contains("arena allocation failed");
        await Assert.That(exception.Message).Contains("GC heap limit");

        // The in-flight batch exits: the debt and every reservation are released.
        SetArenaCapacity(options, InjectedArenaCapacity);
        await CompleteAllBatchesAsync(accumulator, partitions: 1);
        await Assert.That(accumulator.ArenaDebtBytes).IsEqualTo(0);
        await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
    }

    [Test]
    public async Task ArenaOutOfMemory_WithBatchInFlight_AppendsOnceAnInFlightBatchFreesItsArena()
    {
        var options = CreateOptions(bufferMemory: 10_000_000, arenaCapacity: InjectedArenaCapacity);
        await using var accumulator = new RecordAccumulator(options);

        await Assert.That(await AccumulatorTestHelpers.AppendNullRecordAsync(accumulator, Topic, 0, PartitionCount)).IsTrue();
        await AccumulatorTestHelpers.SealAllAsync(accumulator);

        SetArenaCapacity(options, UnallocatableArenaCapacity);
        var waiting = AccumulatorTestHelpers.AppendNullRecordAsync(accumulator, Topic, 1, PartitionCount);
        await Assert.That(waiting.IsCompleted).IsFalse();

        // Appends to other partitions queue behind the arena debt instead of failing.
        var queuedBehind = AccumulatorTestHelpers.AppendNullRecordAsync(accumulator, Topic, 2, PartitionCount);
        await Assert.That(queuedBehind.IsCompleted).IsFalse();

        // Memory is available again when the in-flight batch frees its arena.
        SetArenaCapacity(options, InjectedArenaCapacity);
        await CompleteAllBatchesAsync(accumulator, partitions: 1);

        await Assert.That(await waiting.AsTask().WaitAsync(TimeSpan.FromSeconds(10))).IsTrue();
        await Assert.That(await queuedBehind.AsTask().WaitAsync(TimeSpan.FromSeconds(10))).IsTrue();
        await Assert.That(accumulator.ArenaDebtBytes).IsEqualTo(0);

        await CompleteAllBatchesAsync(accumulator, partitions: 3);
        await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
    }

    [Test]
    public async Task ArenaOutOfMemory_SingleBatchInFlight_RetriesOnceItsArenaIsReleased()
    {
        var options = CreateOptions(bufferMemory: 10_000_000, arenaCapacity: InjectedArenaCapacity);
        await using var accumulator = new RecordAccumulator(options);

        // The only live arena belongs to one in-flight batch.
        await Assert.That(await AccumulatorTestHelpers.AppendNullRecordAsync(accumulator, Topic, 0, PartitionCount)).IsTrue();
        await AccumulatorTestHelpers.SealAllAsync(accumulator);
        await Assert.That(accumulator.TryDrainBatch(new TopicPartition(Topic, 0), out var inFlight)).IsTrue();

        SetArenaCapacity(options, UnallocatableArenaCapacity);
        var waiting = AccumulatorTestHelpers.AppendNullRecordAsync(accumulator, Topic, 1, PartitionCount);
        await Assert.That(waiting.IsCompleted).IsFalse();

        // A resource pin (the sender still serializing or holding the request) defers the batch's
        // cleanup, which is what frees its arena, past the point where the batch leaves the
        // pipeline. Leaving the pipeline must not retry the append while the arena is held:
        // with nothing else in flight, the retry would fail.
        await Assert.That(inFlight!.TryAcquireResourcePin(inFlight.Generation)).IsTrue();
        accumulator.ReleaseBatchMemory(inFlight);
        inFlight.CompleteSend(0, DateTimeOffset.UnixEpoch);
        accumulator.OnBatchExitsPipeline(inFlight);
        await Assert.That(waiting.IsCompleted).IsFalse();

        // Releasing the pin runs the cleanup and frees the arena: memory is available again.
        SetArenaCapacity(options, InjectedArenaCapacity);
        inFlight.ReleaseResourcePin();
        await Assert.That(await waiting.AsTask().WaitAsync(TimeSpan.FromSeconds(10))).IsTrue();

        accumulator.ReturnReadyBatch(inFlight);
        await CompleteAllBatchesAsync(accumulator, partitions: 2);
        await Assert.That(accumulator.ArenaDebtBytes).IsEqualTo(0);
        await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
    }

    [Test]
    public async Task ArenaOutOfMemory_RetryStillFailing_RequeuesWhileOtherBatchesAreInFlight()
    {
        var options = CreateOptions(bufferMemory: 10_000_000, arenaCapacity: InjectedArenaCapacity);
        await using var accumulator = new RecordAccumulator(options);

        // Two batches in flight.
        await Assert.That(await AccumulatorTestHelpers.AppendNullRecordAsync(accumulator, Topic, 0, PartitionCount)).IsTrue();
        await Assert.That(await AccumulatorTestHelpers.AppendNullRecordAsync(accumulator, Topic, 1, PartitionCount)).IsTrue();
        await AccumulatorTestHelpers.SealAllAsync(accumulator);
        await Assert.That(accumulator.InFlightBatchCount).IsEqualTo(2);

        SetArenaCapacity(options, UnallocatableArenaCapacity);
        var waiting = AccumulatorTestHelpers.AppendNullRecordAsync(accumulator, Topic, 2, PartitionCount);
        await Assert.That(waiting.IsCompleted).IsFalse();

        // The first exit drains the waiter, whose retry fails again: with a batch still in
        // flight it is queued again instead of failing.
        await CompleteAllBatchesAsync(accumulator, partitions: 1);
        await Assert.That(waiting.IsCompleted).IsFalse();
        await Assert.That(accumulator.ArenaDebtBytes).IsGreaterThan(0);

        SetArenaCapacity(options, InjectedArenaCapacity);
        await CompleteAllBatchesAsync(accumulator, partitions: 2);
        await Assert.That(await waiting.AsTask().WaitAsync(TimeSpan.FromSeconds(10))).IsTrue();

        await CompleteAllBatchesAsync(accumulator, partitions: 3);
        await Assert.That(accumulator.ArenaDebtBytes).IsEqualTo(0);
        await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
    }

    [Test]
    public async Task OversizedFirstRecordArenaOutOfMemory_WithBatchInFlight_WaitsThenAppends()
    {
        // A first record larger than the batch's arena swaps in an arena of the record's size.
        // Both the swap and its retry fail on this thread until the flag clears; appends here
        // run synchronously on the test thread, and other tests' swaps are unaffected.
        const int oversizedValueSize = 600_000;
        var testThread = Environment.CurrentManagedThreadId;
        var failSwaps = 0;
        var failedSwaps = 0;
        BatchArena.BeforeArenaSwapRentForTest = _ =>
        {
            if (Environment.CurrentManagedThreadId == testThread && Volatile.Read(ref failSwaps) != 0)
            {
                Interlocked.Increment(ref failedSwaps);
                throw InjectedOutOfMemory();
            }
        };

        try
        {
            await using var accumulator = new RecordAccumulator(CreateOptions(
                bufferMemory: 10_000_000,
                arenaCapacity: InjectedArenaCapacity));
            var budget = new LiveArenaBudget(long.MaxValue);
            accumulator.UseLiveArenaBudgetForTest(budget);

            await Assert.That(await AccumulatorTestHelpers.AppendNullRecordAsync(accumulator, Topic, 0, PartitionCount)).IsTrue();
            await AccumulatorTestHelpers.SealAllAsync(accumulator);
            await Assert.That(accumulator.InFlightBatchCount).IsEqualTo(1);

            // The batch's arena comes from the process-wide pool, where other tests leave idle
            // arenas larger than requested (the default BatchSize pools ~1.1 MB arenas). One of
            // those fits the record without a swap, so the append completes. Drop the idle
            // arenas and try the next partition until the append needs the swap.
            Volatile.Write(ref failSwaps, 1);
            ValueTask<bool> waiting = default;
            var partition = 1;
            for (; partition < PartitionCount; partition++)
            {
                BatchArena.ReleaseIdleArenasAfterOutOfMemory();
                waiting = AppendValueAsync(accumulator, partition, oversizedValueSize);
                if (Volatile.Read(ref failedSwaps) > 0)
                    break;

                await Assert.That(await waiting).IsTrue();
            }

            await Assert.That(failedSwaps).IsGreaterThan(0);
            await Assert.That(waiting.IsCompleted).IsFalse();
            await Assert.That(accumulator.ArenaDebtBytes).IsGreaterThan(0);

            Volatile.Write(ref failSwaps, 0);
            await CompleteAllBatchesAsync(accumulator, partitions: 1);
            await Assert.That(await waiting.AsTask().WaitAsync(TimeSpan.FromSeconds(10))).IsTrue();

            await CompleteAllBatchesAsync(accumulator, partitions: partition + 1);
            await Assert.That(accumulator.ArenaDebtBytes).IsEqualTo(0);
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
            await Assert.That(budget.LiveBytes).IsEqualTo(0);
        }
        finally
        {
            BatchArena.BeforeArenaSwapRentForTest = null;
        }
    }

    [Test]
    public async Task ArenaGrowth_IsCountedAndCharged_AndReleasedWithTheBatch()
    {
        await using var accumulator = new RecordAccumulator(CreateOptions(
            bufferMemory: 10_000_000,
            arenaCapacity: InjectedArenaCapacity,
            compressionType: CompressionType.Gzip));
        TrainCompressionEstimate(accumulator);
        var budget = new LiveArenaBudget(0);
        accumulator.UseLiveArenaBudgetForTest(budget);

        var partition = await OpenBatchWithFreshArenaAsync(accumulator, budget);
        var liveBeforeGrowth = budget.LiveBytes;
        for (var i = 1; i < GrowthRecordCount; i++)
            await Assert.That(await AppendValueAsync(accumulator, partition)).IsTrue();

        // The batch's arena grew past its rented capacity: the growth is counted as live and,
        // over the allowance, charged against BufferMemory too.
        await Assert.That(budget.LiveBytes).IsGreaterThan(liveBeforeGrowth);
        await Assert.That(accumulator.ArenaChargedBytes).IsEqualTo(budget.LiveBytes);

        await CompleteAllBatchesAsync(accumulator, partitions: partition + 1);
        await Assert.That(budget.LiveBytes).IsEqualTo(0);
        await Assert.That(accumulator.ArenaChargedBytes).IsEqualTo(0);
        await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
    }

    [Test]
    public async Task ArenaGrowthOutOfMemory_SealsTheBatch_AndAppendsToANewOne()
    {
        // Growth fails on this thread only: appends here run synchronously on the test thread,
        // and other tests' growths are unaffected.
        var testThread = Environment.CurrentManagedThreadId;
        var failedGrowths = 0;
        BatchArena.BeforeArenaGrowForTest = _ =>
        {
            if (Environment.CurrentManagedThreadId == testThread)
            {
                Interlocked.Increment(ref failedGrowths);
                throw InjectedOutOfMemory();
            }
        };

        try
        {
            await using var accumulator = new RecordAccumulator(CreateOptions(
                bufferMemory: 10_000_000,
                arenaCapacity: InjectedArenaCapacity,
                compressionType: CompressionType.Gzip));
            TrainCompressionEstimate(accumulator);
            var budget = new LiveArenaBudget(long.MaxValue);
            accumulator.UseLiveArenaBudgetForTest(budget);

            // Growing is optional: every append succeeds, the full batch is sealed instead.
            var partition = await OpenBatchWithFreshArenaAsync(accumulator, budget);
            for (var i = 1; i < GrowthRecordCount; i++)
                await Assert.That(await AppendValueAsync(accumulator, partition)).IsTrue();

            await Assert.That(failedGrowths).IsGreaterThan(0);

            await AccumulatorTestHelpers.SealAllAsync(accumulator);
            var batches = 0;
            var records = 0;
            while (accumulator.TryDrainBatch(new TopicPartition(Topic, partition), out var batch))
            {
                batches++;
                records += batch.RecordCount;
                accumulator.ReleaseBatchMemory(batch);
                batch.CompleteSend(0, DateTimeOffset.UnixEpoch);
                accumulator.OnBatchExitsPipeline(batch);
                accumulator.ReturnReadyBatch(batch);
            }

            await Assert.That(batches).IsEqualTo(2);
            await Assert.That(records).IsEqualTo(GrowthRecordCount);

            await CompleteAllBatchesAsync(accumulator, partitions: partition);
            await Assert.That(budget.LiveBytes).IsEqualTo(0);
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
        }
        finally
        {
            BatchArena.BeforeArenaGrowForTest = null;
        }
    }

    /// <summary>
    /// Arenas come from the process-wide pool, whose idle arenas (returned by other tests) can be
    /// larger than requested. Appends one record to fresh partitions, dropping the idle arenas
    /// first, until a batch rents an arena of exactly <see cref="InjectedArenaCapacity"/>, and
    /// returns that partition.
    /// </summary>
    private static async Task<int> OpenBatchWithFreshArenaAsync(RecordAccumulator accumulator, LiveArenaBudget budget)
    {
        for (var partition = 0; partition < PartitionCount; partition++)
        {
            BatchArena.ReleaseIdleArenasAfterOutOfMemory();
            var liveBefore = budget.LiveBytes;
            await Assert.That(await AppendValueAsync(accumulator, partition)).IsTrue();
            if (budget.LiveBytes - liveBefore == InjectedArenaCapacity)
                return partition;
        }

        throw new InvalidOperationException("No batch rented a newly created arena.");
    }

    [Test]
    public async Task UnusedRentedBatchReturn_WhileHoldingRotation_DoesNotDrainSamePartitionInline()
    {
        // LingerMs 0 seals a batch as soon as nothing of its partition is in the pipeline.
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "live-arena-test",
            BatchSize = BatchSize,
            BufferMemory = 10_000_000,
            LingerMs = 0,
            MaxBlockMs = 30_000,
        };
        await using var accumulator = new RecordAccumulator(options);
        // Every arena is over the allowance, so returning an unused rental releases a charge.
        accumulator.UseLiveArenaBudgetForTest(new LiveArenaBudget(0));
        var partition = new TopicPartition(Topic, 0);

        // X: sealed at once and left in the pipeline, so B's batch below stays open.
        await Assert.That(await AccumulatorTestHelpers.AppendNullRecordAsync(accumulator, Topic, 0, PartitionCount)).IsTrue();

        ValueTask<bool> queued = default;
        var filledBytes = 0L;
        accumulator.AfterRentBatchForTest = () =>
        {
            // A has rented a batch but not installed it. B installs its own batch first, so
            // A appends to B's batch, seals it (holding the rotation gate) and returns its
            // unused rental.
            accumulator.AfterRentBatchForTest = null;
            if (!AccumulatorTestHelpers.AppendNullRecordAsync(accumulator, Topic, 0, PartitionCount).AsTask().GetAwaiter().GetResult())
                throw new InvalidOperationException("B was not appended.");

            // X leaves the pipeline, so A's append seals B's batch.
            if (!accumulator.TryDrainBatch(partition, out var x))
                throw new InvalidOperationException("X was not sealed.");
            accumulator.ReleaseBatchMemory(x);
            x.CompleteSend(0, DateTimeOffset.UnixEpoch);
            accumulator.OnBatchExitsPipeline(x);
            accumulator.ReturnReadyBatch(x);

            // C queues behind a full BufferMemory: the unused rental's charge release frees
            // room for it, and a drain serves it, to this same partition.
            filledBytes = (long)accumulator.MaxBufferMemory - accumulator.BufferedBytes;
            if (!accumulator.TryReserveMemoryForTest((int)filledBytes))
                throw new InvalidOperationException("BufferMemory was not filled.");
            queued = AccumulatorTestHelpers.AppendNullRecordAsync(accumulator, Topic, 0, PartitionCount);
            if (queued.IsCompleted)
                throw new InvalidOperationException("C was not queued.");
        };

        // A on its own thread: an inline drain there would spin on A's own rotation gate.
        var appended = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                appended.TrySetResult(AccumulatorTestHelpers.AppendNullRecordAsync(accumulator, Topic, 0, PartitionCount)
                    .AsTask().GetAwaiter().GetResult());
            }
            catch (Exception ex)
            {
                appended.TrySetException(ex);
            }
        })
        { IsBackground = true };
        thread.Start();

        await Assert.That(await appended.Task.WaitAsync(TimeSpan.FromSeconds(10))).IsTrue();
        await Assert.That(await queued.AsTask().WaitAsync(TimeSpan.FromSeconds(10))).IsTrue();

        accumulator.ReleaseMemory((int)filledBytes);
        await CompleteAllBatchesAsync(accumulator, partitions: 1);
        await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
        await Assert.That(accumulator.ArenaChargedBytes).IsEqualTo(0);
    }

    [Test]
    public async Task SplitChildren_AreCountedAndCharged_AndReleasedWithTheirArenas()
    {
        // The rejected source is larger than the children's BatchSize, so it splits into several.
        var sourceOptions = new ProducerOptions { BootstrapServers = ["localhost:9092"], BatchSize = 64 * 1024 };
        var source = new PartitionBatch(new TopicPartition(Topic, 0), sourceOptions);
        for (var i = 0; i < 30; i++)
        {
            var value = new byte[120];
            var result = source.TryAppendFromSpans(
                timestamp: 1_000 + i,
                keyData: default,
                keyIsNull: true,
                valueData: value,
                valueIsNull: false,
                headers: null,
                headerCount: 0,
                completionSource: null,
                callback: null,
                estimatedSize: PartitionBatch.EstimateRecordSize(0, value.Length, null, 0));
            if (!result.Success)
                throw new InvalidOperationException("Test record did not fit.");
        }

        var ready = source.Complete()!;
        ready.TrySetMemoryReleased();

        await using var accumulator = new RecordAccumulator(new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = 1024,
            BufferMemory = 10_000_000,
        });
        var budget = new LiveArenaBudget(0);
        accumulator.UseLiveArenaBudgetForTest(budget);

        var children = accumulator.SplitForSenderRetry(ready, ready.Generation, static () => { })!;

        await Assert.That(children.Count).IsGreaterThan(1);
        var childArenaBytes = 0L;
        foreach (var child in children)
            childArenaBytes += child.ArenaCapacityForTest;
        // Every child holds at least a normal arena, and every one of them is counted.
        await Assert.That(childArenaBytes)
            .IsGreaterThanOrEqualTo((long)children.Count * ProducerOptions.GetEffectiveArenaCapacity(1024, 0));
        await Assert.That(budget.LiveBytes).IsEqualTo(childArenaBytes);
        await Assert.That(accumulator.ArenaChargedBytes).IsEqualTo(childArenaBytes);

        var baseOffset = 0L;
        foreach (var child in children)
        {
            child.CompleteSend(baseOffset, DateTimeOffset.UnixEpoch);
            accumulator.ReleaseBatchMemory(child);
            accumulator.OnBatchExitsPipeline(child);
            accumulator.ReturnReadyBatch(child);
            baseOffset += child.RecordCount;
        }

        await Assert.That(budget.LiveBytes).IsEqualTo(0);
        await Assert.That(accumulator.ArenaChargedBytes).IsEqualTo(0);
        await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
    }

    // Records whose values overflow one InjectedArenaCapacity arena, so the batch's arena grows.
    private const int GrowthRecordCount = 12;
    private const int GrowthValueSize = 4_000;

    /// <summary>
    /// Teaches the accumulator a strong compression ratio for <see cref="Topic"/>, so a batch's
    /// uncompressed records may grow its arena beyond the normal capacity.
    /// </summary>
    private static void TrainCompressionEstimate(RecordAccumulator accumulator)
    {
        var estimator = AccumulatorTestHelpers.GetPrivateField<CompressionRatioEstimator>(
            accumulator,
            "_compressionRatioEstimator");
        for (var i = 0; i < 200; i++)
            estimator.Update(Topic, CompressionType.Gzip, observedRatio: 0.1);
    }

    private static ValueTask<bool> AppendValueAsync(
        RecordAccumulator accumulator,
        int partition,
        int valueSize = GrowthValueSize)
    {
        var buffer = ProducerDataPool.BytePool.Rent(valueSize);
        buffer.AsSpan(0, valueSize).Fill((byte)'x');
        return accumulator.AppendAsync(
            Topic,
            partition,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            PooledMemory.Null,
            new PooledMemory(buffer, valueSize),
            headers: null,
            headerCount: 0,
            completionSource: null,
            callback: null,
            CancellationToken.None,
            PartitionCount);
    }

#pragma warning disable CA2201 // The seam injects the runtime's out-of-memory failure on purpose.
    private static OutOfMemoryException InjectedOutOfMemory() => new("Injected batch arena allocation failure.");
#pragma warning restore CA2201

    /// <summary>
    /// Appends one record to each new partition until an append queues behind backpressure.
    /// </summary>
    private static async Task<(ValueTask<bool> Blocked, int Partition)> AppendUntilBlockedAsync(
        RecordAccumulator accumulator,
        CancellationToken cancellationToken)
    {
        for (var partition = 0; partition < PartitionCount; partition++)
        {
            var append = accumulator.AppendAsync(
                Topic,
                partition,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PooledMemory.Null,
                PooledMemory.Null,
                headers: null,
                headerCount: 0,
                completionSource: null,
                callback: null,
                cancellationToken,
                PartitionCount);
            if (!append.IsCompleted)
                return (append, partition);

            await Assert.That(await append).IsTrue();
        }

        throw new InvalidOperationException("No append was back-pressured.");
    }

    /// <summary>
    /// Seals every open batch, then completes the sealed batches of partitions [0, partitions)
    /// the way the sender does: release BufferMemory, acknowledge, leave the pipeline, recycle.
    /// </summary>
    private static async Task CompleteAllBatchesAsync(RecordAccumulator accumulator, int partitions)
    {
        await AccumulatorTestHelpers.SealAllAsync(accumulator);
        for (var partition = 0; partition < partitions; partition++)
        {
            while (accumulator.TryDrainBatch(new TopicPartition(Topic, partition), out var batch))
            {
                accumulator.ReleaseBatchMemory(batch);
                batch.CompleteSend(0, DateTimeOffset.UnixEpoch);
                accumulator.OnBatchExitsPipeline(batch);
                accumulator.ReturnReadyBatch(batch);
            }
        }
    }
}
