using System.Buffers;
using System.Reflection;
using System.Runtime.InteropServices;
using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

public class PartitionedCompletionStorageTests
{
    [Test]
    [NotInParallel]
    public async Task UnmarkedDiscardedRecords_RetainOnlyMostRecentPair()
    {
        var lane = CreateLane();
        var references = PublishAndDiscardWithoutCompletion(lane);
        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            for (var index = 0; index < references.Length - 2; index++)
                await Assert.That(IsAlive(references[index])).IsFalse();
            await Assert.That(IsAlive(references[^2])).IsTrue();
            await Assert.That(IsAlive(references[^1])).IsTrue();
            var identity = GetCompletionIdentity(lane);
            var handles = (GCHandle[]?)typeof(CompletedOffsetRanges)
                .GetField("_weakReservations", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(identity);
            await Assert.That(handles).IsNotNull();
            foreach (var handle in handles!)
                await Assert.That(handle.IsAllocated).IsFalse();
            await Assert.That(lane.GetCommitOffset()).IsNull();
            GC.KeepAlive(lane);
        }
        finally
        {
            lane.Start(static (_, _) => default);
            await lane.StopAsync(PartitionStopPolicy.Drain, TimeSpan.FromSeconds(10));
        }
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Assert.That(IsAlive(references[^2])).IsFalse();
        await Assert.That(IsAlive(references[^1])).IsFalse();
    }

    [Test]
    [NotInParallel]
    public async Task Retirement_WithFinalizersPending_ReleasesAllReservations()
    {
        var lane = CreateLane();
        var identity = GetCompletionIdentity(lane);
        WeakReference<object>[] references;
        lock (identity)
        {
            references = PublishAndDiscardWithoutCompletion(lane);
            // Finalizers cannot remove entries while this lock is held. Retirement
            // must still obtain their targets and release the handles exactly once.
            GC.Collect();
            lane.EnableAutomaticCompletion();
        }
        GC.WaitForPendingFinalizers();
        GC.Collect();
        foreach (var reference in references)
            await Assert.That(IsAlive(reference)).IsFalse();
        await Assert.That(identity.IsRetired).IsTrue();
        await Assert.That(typeof(CompletedOffsetRanges)
            .GetField("_weakReservations", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(identity)).IsNull();
        GC.KeepAlive(lane);
    }

    [Test]
    [NotInParallel]
    [Arguments(false)]
    [Arguments(true)]
    public async Task UnreachableLane_WithCompletedRanges_DoesNotEnterSharedPool(bool pairReservations)
    {
        var references = CreateUnreachableFragmentedLane(pairReservations);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        foreach (var reference in references)
            await Assert.That(IsAlive(reference)).IsFalse();
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference<object>[] CreateUnreachableFragmentedLane(bool pairReservations)
    {
        var lane = CreateLane();
        var batch = lane.CreateCompletionBatch(2)!;
        _ = Deliver(lane, batch, 10);
        lane.MarkProcessed(Deliver(lane, batch, 12));
        lane.EndBatch(batch, 2);
        if (pairReservations)
        {
            var later = lane.CreateCompletionBatch(1)!;
            _ = Deliver(lane, later, 14);
            lane.EndBatch(later, 1);
        }
        // Long weak references detect accidental resurrection through ArrayPool
        // if a slab containing live range-owner links is returned by the finalizer.
        return [new(lane, trackResurrection: true), new(batch, trackResurrection: true)];
    }

    private static CompletedOffsetRanges GetCompletionIdentity(PartitionLane<string, string> lane) =>
        (CompletedOffsetRanges)typeof(PartitionLane<string, string>)
            .GetField("_completedRanges", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(lane)!;

    [Test]
    [NotInParallel]
    public async Task RetainedRecord_KeepsDelayedCompletionButReleasesCompletedPairMembership()
    {
        var lane = CreateLane();
        var (first, peer) = PublishRetainedRecordAndDiscardLaterBatches(lane);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Assert.That(IsAlive(peer)).IsTrue();
        lane.MarkProcessed(first);
        await Assert.That(lane.GetCommitOffset()?.Offset).IsEqualTo(11);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        // Retaining a completed record must not retain its unfinished peer.
        await Assert.That(IsAlive(peer)).IsFalse();
        lane.MarkProcessed(first);
        await Assert.That(lane.GetCommitOffset()?.Offset).IsEqualTo(11);
        lane.EnableAutomaticCompletion();
        GC.KeepAlive(first);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    // Keep TryGetTarget's temporary strong reference out of the GC-driving method.
    private static bool IsAlive(WeakReference<object> reference) => reference.TryGetTarget(out _);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static (ConsumeResult<string, string>, WeakReference<object>)
        PublishRetainedRecordAndDiscardLaterBatches(PartitionLane<string, string> lane)
    {
        var batch = lane.CreateCompletionBatch(1)!;
        var first = Deliver(lane, batch, 10);
        lane.EndBatch(batch, 1);
        var second = lane.CreateCompletionBatch(1)!;
        _ = Deliver(lane, second, 12);
        lane.EndBatch(second, 1);
        var peer = new WeakReference<object>(second);
        for (var index = 0; index < 128; index++)
        {
            batch = lane.CreateCompletionBatch(1)!;
            _ = Deliver(lane, batch, 14 + index * 2L);
            lane.EndBatch(batch, 1);
        }
        return (first, peer);
    }

    [Test]
    public async Task PartialPair_ReusesVacantMemberAndCompletesAcrossLaterReservations()
    {
        var lane = CreateLane();
        var batches = new OffsetCompletionBatch[4];
        var records = new ConsumeResult<string, string>[4];
        for (var index = 0; index < batches.Length; index++)
        {
            if (index == 2)
                lane.MarkProcessed(records[0]);
            var batch = batches[index] = lane.CreateCompletionBatch(1)!;
            records[index] = Deliver(lane, batch, 10 + index * 2L);
            lane.EndBatch(batch, 1);
        }
        lane.MarkProcessed(records[3]);
        lane.MarkProcessed(records[2]);
        await Assert.That(lane.GetCommitOffset()?.Offset).IsEqualTo(11);
        lane.MarkProcessed(records[1]);
        await Assert.That(lane.GetCommitOffset()?.Offset).IsEqualTo(17);
        foreach (var batch in batches)
            await Assert.That(batch.Nodes).IsNull();
        lane.EnableAutomaticCompletion();
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference<object>[] PublishAndDiscardWithoutCompletion(PartitionLane<string, string> lane)
    {
        var references = new WeakReference<object>[128];
        for (var index = 0; index < references.Length; index++)
        {
            var batch = lane.CreateCompletionBatch(1)!;
            var record = Deliver(lane, batch, 10 + index * 2L);
            lane.EndBatch(batch, 1);
            references[index] = new WeakReference<object>(record.ProcessingBatch!);
        }
        return references;
    }

    [Test]
    public async Task Reservation_RejectsPoolPaddingBeyondRequestedCapacity()
    {
        var lane = CreateLane();
        var batch = lane.CreateCompletionBatch(1)!;
        var record = Deliver(lane, batch, 10);
        try
        {
            await Assert.That(() => batch.PrepareRecord(10)).Throws<ArgumentOutOfRangeException>();
        }
        finally
        {
            lane.EndBatch(batch, 1);
            lane.MarkProcessed(record);
        }
    }

    [Test]
    public async Task PubliclyConstructedResult_ExplainsMissingCompletionOwnership()
    {
        var lane = CreateLane();
        var message = new ConsumeResult<string, string>("completion", 0, 10,
            "key"u8.ToArray(), false, "value"u8.ToArray(), false, null, 0,
            TimestampType.CreateTime, 1, Dekaf.Serialization.Serializers.String, Dekaf.Serialization.Serializers.String);

        await Assert.That(() => lane.MarkProcessed(message)).Throws<InvalidOperationException>()
            .WithMessage("Cannot mark a message that was not delivered through manual partition completion tracking as processed.");
        await Assert.That(lane.GetCommitOffset()).IsNull();
    }

    [Test]
    public async Task KeyDispatcherFailure_RetiresActiveAndQueuedReservations()
    {
        var lane = CreateLane();
        var batch = lane.CreateCompletionBatch(5)!;
        lane.MarkProcessed(Deliver(lane, batch, 10));
        for (var index = 1; index < 5; index++)
        {
            var message = new ConsumeResult<string, string>("completion", 0, 10 + index * 2L,
                "key", "value", null, 0, TimestampType.CreateTime, 1);
            await Assert.That(lane.TryEnqueue(message, batch)).IsTrue();
        }
        // The distinct EOF key reaches its handler only after all earlier records
        // have entered the first key's active handler or its private queue.
        await Assert.That(lane.TryEnqueue(ConsumeResult<string, string>.CreatePartitionEof("completion", 0, 19), batch)).IsTrue();
        lane.EndBatch(batch, 5);
        var dispatched = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new InvalidOperationException("Expected key handler failure");
        lane.Start((context, token) =>
        {
            var dispatcher = new KeyOrderedPartitionDispatcher<string, string>(context,
                maxBatchSize: 1, maxConcurrentHandlers: 2, maxBufferedRecords: 8,
                async (records, _) =>
                {
                    if (records[0].IsPartitionEof)
                    {
                        dispatched.TrySetResult();
                        return;
                    }
                    await release.Task;
                    throw failure;
                });
            return dispatcher.RunAsync(token);
        });
        try
        {
            await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(batch.Nodes).IsNotNull();
        }
        finally
        {
            release.TrySetResult();
            var stopped = await lane.StopAsync(PartitionStopPolicy.Drain, TimeSpan.FromSeconds(10));
            await Assert.That(stopped).IsSameReferenceAs(failure);
        }
        await Assert.That(batch.Nodes).IsNull();
        await Assert.That(lane.GetCommitOffset()?.Offset).IsEqualTo(11);
    }

    [Test]
    public async Task ReservationGrowth_RemovesCompletedBatchesBeforeProcessorExit()
    {
        var lane = CreateLane();
        var batches = new OffsetCompletionBatch[65];
        var records = new ConsumeResult<string, string>[batches.Length];
        for (var index = 0; index < batches.Length; index++)
        {
            var batch = batches[index] = lane.CreateCompletionBatch(1)!;
            records[index] = Deliver(lane, batch, 10 + index * 2L);
            lane.EndBatch(batch, 1);
        }
        // Completion removes entries from the beginning, middle, and end of the
        // retained set as it collapses fragmented ranges across many reservations.
        for (var index = 1; index < records.Length; index += 2)
            lane.MarkProcessed(records[index]);
        for (var index = 0; index < records.Length; index += 2)
            lane.MarkProcessed(records[index]);
        foreach (var batch in batches)
            await Assert.That(batch.Nodes).IsNull();
        var expectedOffset = records[^1].Offset + 1;
        await Assert.That(lane.GetCommitOffset()?.Offset).IsEqualTo(expectedOffset);
        lane.Start(static (_, _) => default);
        await lane.StopAsync(PartitionStopPolicy.Drain, TimeSpan.FromSeconds(10));
        // A publisher can race processor exit. Its rejected reservation remains
        // owned by publication until EndBatch, then returns without registration.
        var lateBatch = lane.CreateCompletionBatch(1)!;
        await Assert.That(lane.TryEnqueue(records[^1], lateBatch)).IsFalse();
        lane.EndBatch(lateBatch, 0);
        await Assert.That(lateBatch.Nodes).IsNull();
        await Assert.That(lane.GetCommitOffset()?.Offset).IsEqualTo(expectedOffset);
    }

    [Test]
    [Arguments("cancel", false)]
    [Arguments("fail", false)]
    [Arguments("return", false)]
    [Arguments("fail", true)]
    public async Task ProcessorExit_RetiresDispatchedReservationsAndCompletedRanges(string exit, bool publishing)
    {
        var lane = CreateLane();
        var firstBatch = lane.CreateCompletionBatch(2)!;
        lane.MarkProcessed(Deliver(lane, firstBatch, 10));
        var abandoned = Deliver(lane, firstBatch, 12);
        lane.EndBatch(firstBatch, 2);
        var laterBatch = lane.CreateCompletionBatch(2)!;
        lane.MarkProcessed(Deliver(lane, laterBatch, 14));
        var laterAbandoned = Deliver(lane, laterBatch, 16);
        if (!publishing) lane.EndBatch(laterBatch, 2);

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new InvalidOperationException("Expected dispatched processor failure");
        lane.Start(async (_, token) =>
        {
            started.TrySetResult();
            await release.Task.WaitAsync(token);
            if (exit == "fail") throw failure;
        });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (exit != "cancel") release.TrySetResult();
        var stopped = await lane.StopAsync(
            exit == "cancel" ? PartitionStopPolicy.Cancel : PartitionStopPolicy.Drain,
            TimeSpan.FromSeconds(10));
        if (exit == "fail") await Assert.That(stopped).IsSameReferenceAs(failure);
        else await Assert.That(stopped).IsNull();

        await Assert.That(firstBatch.Nodes).IsNull();
        if (publishing)
        {
            await Assert.That(laterBatch.Nodes).IsNotNull();
            lane.MarkProcessed(laterAbandoned);
            await Assert.That(lane.GetCommitOffset()?.Offset).IsEqualTo(11);
            lane.EndBatch(laterBatch, 2);
        }
        await Assert.That(laterBatch.Nodes).IsNull();
        await Assert.That(lane.GetCommitOffset()?.Offset).IsEqualTo(11);
        // A retained copy must not read recycled storage or resurrect progress after exit.
        lane.MarkProcessed(abandoned);
        lane.MarkProcessed(laterAbandoned);
        await Assert.That(lane.GetCommitOffset()?.Offset).IsEqualTo(11);
    }

    [Test]
    [Arguments("cancel", false)]
    [Arguments("fail", false)]
    [Arguments("return", false)]
    [Arguments("cancel", true)]
    public async Task ProcessorExit_RetiresQueuedReservationsWithoutAdvancingProgress(string exit, bool publishing)
    {
        var lane = CreateLane();
        var batch = lane.CreateCompletionBatch(32)!;
        lane.MarkProcessed(Deliver(lane, batch, 10));
        for (var index = 1; index < 32; index++)
        {
            var message = new ConsumeResult<string, string>("completion", 0, 10 + index * 2L,
                "key", "value", null, 0, TimestampType.CreateTime, 1);
            await Assert.That(lane.TryEnqueue(message, batch)).IsTrue();
        }
        await Assert.That(lane.TryEnqueue(ConsumeResult<string, string>.CreatePartitionEof("completion", 0, 73), batch)).IsTrue();
        if (!publishing) lane.EndBatch(batch, 32);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new InvalidOperationException("Expected processor failure");
        lane.Start(async (_, token) =>
        {
            started.TrySetResult();
            await release.Task.WaitAsync(token);
            if (exit == "fail") throw failure;
        });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (exit != "cancel") release.TrySetResult();
        var stopped = await lane.StopAsync(
            exit == "cancel" ? PartitionStopPolicy.Cancel : PartitionStopPolicy.Drain,
            TimeSpan.FromSeconds(10));
        if (exit == "fail") await Assert.That(stopped).IsSameReferenceAs(failure);
        else await Assert.That(stopped).IsNull();
        await Assert.That(lane.TryReadMessage(out _)).IsFalse();
        await Assert.That(lane.GetCommitOffset()?.Offset).IsEqualTo(11);
        if (publishing)
        {
            await Assert.That(batch.Nodes).IsNotNull();
            lane.EndBatch(batch, 32);
        }
        await Assert.That(batch.Nodes).IsNull();
    }

    [Test]
    public async Task TimedOutProcessor_KeepsReservationsUntilActualExit()
    {
        var lane = CreateLane();
        var batch = lane.CreateCompletionBatch(2)!;
        var active = Deliver(lane, batch, 10);
        var queued = new ConsumeResult<string, string>("completion", 0, 12,
            "key", "value", null, 0, TimestampType.CreateTime, 1);
        await Assert.That(lane.TryEnqueue(queued, batch)).IsTrue();
        lane.EndBatch(batch, 2);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lane.Start(async (context, _) =>
        {
            started.TrySetResult();
            await release.Task;
            context.MarkProcessed(active);
        });
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var stopped = await lane.StopAsync(PartitionStopPolicy.Cancel, TimeSpan.FromMilliseconds(1));
            await Assert.That(stopped).IsTypeOf<TimeoutException>();
            await Assert.That(batch.Nodes).IsNotNull();
            await Assert.That(lane.GetCommitOffset()).IsNull();
        }
        finally
        {
            release.TrySetResult();
            await lane.StopAsync(PartitionStopPolicy.Drain, TimeSpan.FromSeconds(10));
        }
        await Assert.That(batch.Nodes).IsNull();
        await Assert.That(lane.GetCommitOffset()?.Offset).IsEqualTo(11);
    }

    [Test]
    [NotInParallel]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(1024)]
    [Arguments(1025)]
    [Arguments(4097)]
    public async Task LargeReservation_InitializesOnlyPublishedChunks(int published)
    {
        const int capacity = 131072;
        // Rent and clear an exclusively owned slab, then return it to this thread's
        // pool slot. No await or parallel test may replace it before the next rent.
        var slab = ArrayPool<CompletedOffsetNode>.Shared.Rent(capacity);
        Array.Clear(slab);
        ArrayPool<CompletedOffsetNode>.Shared.Return(slab);
        var lane = CreateLane();
        var batch = lane.CreateCompletionBatch(capacity)!;
        var reusedSlab = ReferenceEquals(slab, batch.Nodes);
        var initializedBeforePublication = CountInitializedNodes(slab);
        var records = new ConsumeResult<string, string>[published];
        for (var index = 0; index < published; index++)
            records[index] = Deliver(lane, batch, 10 + index * 2L);
        var initializedAfterPublication = CountInitializedNodes(slab);
        lane.EndBatch(batch, published);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 1; index < published; index += 2)
            lane.MarkProcessed(records[index]);
        var lastEvenIndex = (published - 1) & ~1;
        for (var index = 0; index < lastEvenIndex; index += 2)
            lane.MarkProcessed(records[index]);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        // Final completion returns the slab to ArrayPool, whose per-core cold
        // bookkeeping may allocate after thread migration. Measure the reserved
        // completion path separately, then still verify final return and reuse.
        if (published > 0)
            lane.MarkProcessed(records[lastEvenIndex]);

        // A partially initialized slab must preserve its existing nodes and grow
        // its initialized prefix when the next fetch publishes another chunk.
        var firstNode = slab[0];
        var next = lane.CreateCompletionBatch(capacity)!;
        var reusedPartialSlab = ReferenceEquals(slab, next.Nodes);
        var nextPublished = published == 0 ? 0 : published + 1024;
        for (var index = 0; index < nextPublished; index++)
            lane.MarkProcessed(Deliver(lane, next, 10 + (published + index) * 2L));
        lane.EndBatch(next, nextPublished);
        var preservedNode = ReferenceEquals(firstNode, slab[0]);
        var initializedAfterReuse = CountInitializedNodes(slab);

        await Assert.That(reusedSlab && reusedPartialSlab).IsTrue();
        await Assert.That(initializedBeforePublication).IsEqualTo(0);
        await Assert.That(initializedAfterPublication).IsEqualTo((published + 1023) / 1024 * 1024);
        await Assert.That(initializedAfterReuse).IsEqualTo((nextPublished + 1023) / 1024 * 1024);
        await Assert.That(preservedNode).IsTrue();
        await Assert.That(allocated).IsEqualTo(0);
        await Assert.That(batch.Nodes).IsNull();
        await Assert.That(next.Nodes).IsNull();
        if (published != 0)
            await Assert.That(lane.GetCommitOffset()?.Offset).IsEqualTo(10 + (published + nextPublished - 1) * 2L + 1);
    }

    [Test]
    [NotInParallel]
    public async Task ConcurrentPublicationAcrossChunks_ReservesBeforeFragmentedCompletion()
    {
        const int count = 4098;
        var slab = ArrayPool<CompletedOffsetNode>.Shared.Rent(count);
        Array.Clear(slab);
        ArrayPool<CompletedOffsetNode>.Shared.Return(slab);
        var lane = CreateLane();
        var batch = lane.CreateCompletionBatch(count)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var writer = Task.Run(async () =>
        {
            var published = 0;
            try
            {
                for (; published < count; published++)
                {
                    var message = new ConsumeResult<string, string>("completion", 0, 10 + published * 2L,
                        "key", "value", null, 0, TimestampType.CreateTime, 1);
                    while (!lane.TryEnqueue(message, batch))
                        await lane.WaitToWriteAsync(timeout.Token);
                }
            }
            finally
            {
                lane.EndBatch(batch, published);
            }
        }, timeout.Token);

        for (var index = 0; index < count; index += 2)
        {
            ConsumeResult<string, string> first;
            while (!lane.TryReadMessage(out first))
                await lane.WaitToReadMessageAsync(timeout.Token);
            ConsumeResult<string, string> second;
            while (!lane.TryReadMessage(out second))
                await lane.WaitToReadMessageAsync(timeout.Token);
            lane.MarkProcessed(second);
            lane.MarkProcessed(first);
        }
        await writer.WaitAsync(timeout.Token);
        await Assert.That(lane.GetCommitOffset()?.Offset).IsEqualTo(10 + (count - 1) * 2L + 1);
        await Assert.That(batch.Nodes).IsNull();
    }

    private static int CountInitializedNodes(CompletedOffsetNode[] nodes)
    {
        var count = 0;
        foreach (var node in nodes)
        {
            if (node is not null)
                count++;
        }
        return count;
    }

    [Test]
    [Arguments(34, 2L)]
    [Arguments(258, 2L)]
    [Arguments(4098, 2L)]
    [Arguments(34, 4_294_967_296L)]
    public async Task FragmentedCompletion_UsesReservedStorageWithoutAllocating(int count, long offsetStep)
    {
        var lane = CreateLane();
        var batch = lane.CreateCompletionBatch(count)!;
        var records = new ConsumeResult<string, string>[count];
        for (var index = 0; index < count; index++)
            records[index] = Deliver(lane, batch, 10 + index * offsetStep);
        lane.EndBatch(batch, count);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 1; index < count; index += 2)
            lane.MarkProcessed(records[index]);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0);
        await Assert.That(lane.GetCommitOffset()).IsNull();
        for (var index = 0; index < count; index += 2)
            lane.MarkProcessed(records[index]);
        await Assert.That(lane.GetCommitOffset()?.Offset).IsEqualTo(records[^1].Offset + 1);
        await Assert.That(batch.Nodes).IsNull();
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    [Arguments(PackedProcessingEpoch.IndexCapacity + 1)]
    [Arguments(int.MaxValue)]
    public async Task Reservation_RejectsUnsupportedCapacityBeforeRenting(int capacity)
    {
        var lane = CreateLane();
        await Assert.That(() => lane.CreateCompletionBatch(capacity)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    [Arguments(0)]
    [Arguments(992)]
    public async Task FailedWideGapPublication_ReusesUnpublishedReservation(int completedPrefix)
    {
        var lane = CreateLane();
        var batch = lane.CreateCompletionBatch(completedPrefix + 33)!;
        for (var index = 0; index < completedPrefix; index++)
            lane.MarkProcessed(Deliver(lane, batch, (index + 1) * 4_294_967_296L));
        for (var index = 0; index < 32; index++)
        {
            var message = new ConsumeResult<string, string>("completion", 0,
                (completedPrefix + index + 1) * 4_294_967_296L, "key", "value", null, 0, TimestampType.CreateTime, 1);
            await Assert.That(lane.TryEnqueue(message, batch)).IsTrue();
        }
        var last = new ConsumeResult<string, string>("completion", 0,
            (completedPrefix + 33) * 4_294_967_296L, "key", "value", null, 0, TimestampType.CreateTime, 1);
        for (var attempt = 0; attempt < 100; attempt++)
            await Assert.That(lane.TryEnqueue(last, batch)).IsFalse();
        await Assert.That(lane.TryReadMessage(out var first)).IsTrue();
        await Assert.That(lane.TryEnqueue(last, batch)).IsTrue();
        lane.EndBatch(batch, completedPrefix + 33);
        while (lane.TryReadMessage(out var record))
            lane.MarkProcessed(record);
        await Assert.That(lane.GetCommitOffset()?.Offset)
            .IsEqualTo(completedPrefix == 0 ? (long?)null : completedPrefix * 4_294_967_296L + 1);
        lane.MarkProcessed(first);
        await Assert.That(lane.GetCommitOffset()?.Offset).IsEqualTo(last.Offset + 1);
        await Assert.That(batch.Nodes).IsNull();
    }

    [Test]
    public async Task CompletionBeforePublicationEnds_DoesNotReturnReservedStorage()
    {
        var lane = CreateLane();
        var batch = lane.CreateCompletionBatch(64)!;
        lane.MarkProcessed(Deliver(lane, batch, 10));
        await Assert.That(batch.Nodes).IsNotNull();
        lane.MarkProcessed(Deliver(lane, batch, 12));
        lane.EndBatch(batch, 2);
        await Assert.That(batch.Nodes).IsNull();
        await Assert.That(lane.GetCommitOffset()?.Offset).IsEqualTo(13);
    }

    [Test]
    public async Task CompleteTail_ReleasesLaterBatchesWhileFirstRecordRemainsPending()
    {
        var lane = CreateLane();
        var firstBatch = lane.CreateCompletionBatch(1)!;
        var first = Deliver(lane, firstBatch, 10);
        lane.EndBatch(firstBatch, 1);
        OffsetCompletionBatch? retainedRange = null;
        long offset = 10;
        for (var index = 0; index < 1000; index++)
        {
            var batch = lane.CreateCompletionBatch(16)!;
            for (var record = 0; record < 16; record++)
            {
                offset += 2;
                lane.MarkProcessed(Deliver(lane, batch, offset));
            }
            lane.EndBatch(batch, 16);
            if (index == 0)
                retainedRange = batch;
            else
                await Assert.That(batch.Nodes).IsNull();
        }
        await Assert.That(lane.GetCommitOffset()).IsNull();
        lane.MarkProcessed(first);
        await Assert.That(firstBatch.Nodes).IsNull();
        await Assert.That(retainedRange!.Nodes).IsNull();
        await Assert.That(lane.GetCommitOffset()?.Offset).IsEqualTo(offset + 1);
    }

    [Test]
    public async Task DuplicateFromReturnedBatch_DoesNotCompleteAnotherReservedBatch()
    {
        var lane = CreateLane();
        var firstBatch = lane.CreateCompletionBatch(1)!;
        var first = Deliver(lane, firstBatch, 10);
        lane.EndBatch(firstBatch, 1);
        lane.MarkProcessed(first);
        var laterBatch = lane.CreateCompletionBatch(1)!;
        var later = Deliver(lane, laterBatch, 12);
        lane.EndBatch(laterBatch, 1);
        lane.MarkProcessed(first);
        await Assert.That(laterBatch.Nodes).IsNotNull();
        await Assert.That(lane.GetCommitOffset()?.Offset).IsEqualTo(11);
        lane.MarkProcessed(later);
        await Assert.That(laterBatch.Nodes).IsNull();
    }

    [Test]
    public async Task DuplicateWideGapFromReturnedBatch_DoesNotReadRecycledStorage()
    {
        var lane = CreateLane();
        var firstBatch = lane.CreateCompletionBatch(1)!;
        var first = Deliver(lane, firstBatch, 10);
        lane.EndBatch(firstBatch, 1);
        var rangeBatch = lane.CreateCompletionBatch(1)!;
        var range = Deliver(lane, rangeBatch, 4_294_967_296L);
        lane.EndBatch(rangeBatch, 1);
        lane.MarkProcessed(range);
        var laterBatch = lane.CreateCompletionBatch(1)!;
        var later = Deliver(lane, laterBatch, 9_000_000_000L);
        lane.EndBatch(laterBatch, 1);
        lane.MarkProcessed(later);
        await Assert.That(laterBatch.Nodes).IsNull();
        lane.MarkProcessed(later);
        await Assert.That(lane.GetCommitOffset()).IsNull();
        lane.MarkProcessed(first);
        await Assert.That(lane.GetCommitOffset()?.Offset).IsEqualTo(later.Offset + 1);
        await Assert.That(rangeBatch.Nodes).IsNull();
    }

    [Test]
    public async Task RecordFromPriorProcessingEpoch_CannotAdvanceReplacementLane()
    {
        var oldLane = CreateLane();
        var oldBatch = oldLane.CreateCompletionBatch(1)!;
        var oldRecord = Deliver(oldLane, oldBatch, 10);
        oldLane.EndBatch(oldBatch, 1);
        var replacement = CreateLane();
        await Assert.That(() => replacement.MarkProcessed(oldRecord)).Throws<InvalidOperationException>();
        await Assert.That(replacement.GetCommitOffset()).IsNull();
        oldLane.MarkProcessed(oldRecord);
        await Assert.That(oldBatch.Nodes).IsNull();
    }

    private static PartitionLane<string, string> CreateLane() =>
        new(new TopicPartition("completion", 0), 32,
            static (_, _) => default, static _ => { }, static (_, _) => { });

    private static ConsumeResult<string, string> Deliver(
        PartitionLane<string, string> lane, OffsetCompletionBatch batch, long offset)
    {
        var message = new ConsumeResult<string, string>("completion", 0, offset,
            "key", "value", null, 0, TimestampType.CreateTime, 1);
        if (!lane.TryEnqueue(message, batch) || !lane.TryReadMessage(out var delivered))
            throw new InvalidOperationException("Unable to publish the test record.");
        return delivered;
    }
}
