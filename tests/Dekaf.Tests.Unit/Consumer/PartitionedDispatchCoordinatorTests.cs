using System.Threading.Tasks.Sources;
using Dekaf.Consumer;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class PartitionedDispatchCoordinatorTests
{
    [Test]
    public async Task CustomScalarComparer_SerializesEqualKeysWhileDifferentKeysProgress()
    {
        var lane = CreateLane(3);
        await Assert.That(lane.TryEnqueueForTest(CreateRecord(0, keyOverride: 10))).IsTrue();
        await Assert.That(lane.TryEnqueueForTest(CreateRecord(1, keyOverride: 20))).IsTrue();
        await Assert.That(lane.TryEnqueueForTest(CreateRecord(2, keyOverride: 11))).IsTrue();
        await lane.StopAsync(PartitionStopPolicy.Drain, Timeout.InfiniteTimeSpan);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var equalStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var differentStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var dispatcher = new KeyOrderedPartitionDispatcher<int, int>(
            new PartitionProcessorContext<int, int>(lane), 1, 2, 3,
            (records, _) =>
            {
                if (records[0].Offset == 0) return new ValueTask(releaseFirst.Task);
                if (records[0].Offset == 1) equalStarted.TrySetResult();
                else differentStarted.TrySetResult();
                return default;
            }, new KeyOrderedStorageTests.ModuloTenComparer());
        var processing = dispatcher.RunAsync(timeout.Token).AsTask();
        try
        {
            await differentStarted.Task.WaitAsync(timeout.Token);
            await Assert.That(equalStarted.Task.IsCompleted).IsFalse();
        }
        finally
        {
            releaseFirst.TrySetResult();
            await processing.WaitAsync(timeout.Token);
        }
        await Assert.That(equalStarted.Task.IsCompletedSuccessfully).IsTrue();
        await Assert.That(dispatcher.LaneCount).IsEqualTo(0);
    }

    [Test]
    public async Task AutomaticCompletion_RetiresQueuedReservationsAndSkipsFutureStorage()
    {
        var lane = CreateLane(4);
        var publishing = lane.CreateCompletionBatch(4)!;
        await Assert.That(lane.TryEnqueue(CreateRecord(0), publishing)).IsTrue();
        var finished = lane.CreateCompletionBatch(1)!;
        await Assert.That(lane.TryEnqueue(CreateRecord(1), finished)).IsTrue();
        lane.EndBatch(finished, 1);

        lane.EnableAutomaticCompletion();
        await Assert.That(finished.Nodes).IsNull();
        await Assert.That(publishing.Nodes).IsNotNull();
        // The writer can still publish from the batch that raced processor startup.
        await Assert.That(lane.TryEnqueue(CreateRecord(2), publishing)).IsTrue();
        lane.EndBatch(publishing, 2);
        await Assert.That(publishing.Nodes).IsNull();
        var automatic = lane.CreateCompletionBatch(4);
        await Assert.That(automatic).IsNull();
        await Assert.That(lane.TryEnqueue(CreateRecord(3), automatic)).IsTrue();
        lane.EndBatch(automatic, 1);
        for (var offset = 0; offset < 4; offset++)
        {
            await Assert.That(lane.TryReadMessage(out var record)).IsTrue();
            await Assert.That(record.Offset).IsEqualTo(offset);
            record.ReleaseStorage();
        }
    }

    [Test]
    [Arguments(256, 4, 100, 64)]
    [Arguments(400, 4, 100, 100)]
    [Arguments(8, 3, 4, 2)]
    [Arguments(8, 16, 10, 1)]
    public async Task ConfiguredBatchBudget_AppliesDocumentedKeyOrderedCap(
        int bufferedRecords, int concurrency, int requestedBatchSize, int expectedBatchSize)
    {
        var count = bufferedRecords + 1;
        var lane = CreateLane(count);
        for (var offset = 0; offset < count; offset++)
            await Assert.That(lane.TryEnqueueForTest(CreateRecord(offset, keyOverride: 0))).IsTrue();
        await lane.StopAsync(PartitionStopPolicy.Drain, Timeout.InfiniteTimeSpan);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handled = 0;
        var largestBatch = 0;
        var dispatcher = new KeyOrderedPartitionDispatcher<int, int>(
            new PartitionProcessorContext<int, int>(lane), requestedBatchSize, concurrency, bufferedRecords,
            (records, _) =>
            {
                var first = handled == 0;
                largestBatch = Math.Max(largestBatch, records.Count);
                foreach (var record in records)
                {
                    if (record.Offset != handled++)
                        throw new InvalidOperationException("Batch budgeting lost or reordered a record.");
                }
                // Let the coordinator buffer a full window behind the first handler,
                // so the next invocation can reach the documented cap for this key.
                return first ? new ValueTask(releaseFirst.Task) : default;
            }, automaticCompletion: true);
        var processing = dispatcher.RunAsync(CancellationToken.None).AsTask();
        try
        {
            await Assert.That(handled).IsEqualTo(1);
        }
        finally
        {
            releaseFirst.TrySetResult();
            await processing.WaitAsync(TimeSpan.FromSeconds(10));
        }
        await Assert.That(handled).IsEqualTo(count);
        await Assert.That(largestBatch).IsEqualTo(expectedBatchSize);
        await Assert.That(lane.GetCommitOffset()).IsEqualTo(new TopicPartitionOffset("dispatch", 0, count, count - 1));
    }

    [Test]
    public async Task GrowingPendingAndBatchStorage_PreservesActiveRecordsAndOrder()
    {
        const int count = 257;
        var lane = CreateLane(count);
        for (var offset = 0; offset < count; offset++)
            await Assert.That(lane.TryEnqueueForTest(CreateRecord(offset, keyOverride: 0))).IsTrue();
        await lane.StopAsync(PartitionStopPolicy.Drain, Timeout.InfiniteTimeSpan);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handled = 0;
        var largestBatch = 0;
        var dispatcher = new KeyOrderedPartitionDispatcher<int, int>(
            new PartitionProcessorContext<int, int>(lane), 32, 2, count,
            (records, _) =>
            {
                var first = handled == 0;
                largestBatch = Math.Max(largestBatch, records.Count);
                foreach (var record in records)
                {
                    if (record.Offset != handled++)
                        throw new InvalidOperationException("Storage growth lost or reordered a record.");
                }
                return first ? new ValueTask(releaseFirst.Task) : default;
            }, automaticCompletion: true);
        var processing = dispatcher.RunAsync(CancellationToken.None).AsTask();
        try
        {
            await Assert.That(handled).IsEqualTo(1);
        }
        finally
        {
            releaseFirst.TrySetResult();
            await processing.WaitAsync(TimeSpan.FromSeconds(10));
        }
        await Assert.That(handled).IsEqualTo(count);
        await Assert.That(largestBatch).IsEqualTo(32);
        await Assert.That(lane.GetCommitOffset()).IsEqualTo(new TopicPartitionOffset("dispatch", 0, count, count - 1));
    }

    [Test]
    [Arguments(1)]
    [Arguments(int.MaxValue)]
    public async Task LargeConfiguredLimits_AllocateOnlyForActiveWork(int concurrency)
    {
        var lane = CreateLane(1);
        await Assert.That(lane.TryEnqueueForTest(CreateRecord(0))).IsTrue();
        await lane.StopAsync(PartitionStopPolicy.Drain, Timeout.InfiniteTimeSpan);
        var handled = 0;
        var dispatcher = new KeyOrderedPartitionDispatcher<int, int>(
            new PartitionProcessorContext<int, int>(lane), int.MaxValue, concurrency, int.MaxValue,
            (records, _) =>
            {
                handled += records.Count;
                return default;
            }, automaticCompletion: true);
        await dispatcher.RunAsync(CancellationToken.None);
        await Assert.That(handled).IsEqualTo(1);
        await Assert.That(lane.GetCommitOffset()).IsEqualTo(new TopicPartitionOffset("dispatch", 0, 1, 0));
    }

    [Test]
    public async Task HandlerFailure_IsObservedWhileInputRemainsAvailable()
    {
        var lane = CreateLane(8);
        for (var index = 0; index < 8; index++)
            await Assert.That(lane.TryEnqueueForTest(CreateRecord(index))).IsTrue();
        await lane.StopAsync(PartitionStopPolicy.Drain, Timeout.InfiniteTimeSpan);
        var first = new TaskCompletionSource();
        var failure = new InvalidOperationException("asynchronous handler failed");
        var handled = new List<long>();
        var dispatcher = new KeyOrderedPartitionDispatcher<int, int>(
            new PartitionProcessorContext<int, int>(lane), 1, 2, 2,
            (records, _) =>
            {
                var offset = records[0].Offset;
                handled.Add(offset);
                if (offset == 0)
                    return new ValueTask(first.Task);
                if (offset == 1)
                    first.SetException(failure);
                return default;
            }, automaticCompletion: true);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await dispatcher.RunAsync(CancellationToken.None));
        await Assert.That(thrown).IsSameReferenceAs(failure);
        await Assert.That(handled).IsEquivalentTo(new long[] { 0, 1 });
    }

    [Test]
    public async Task PublishedCheckpoint_KeepsOffsetAndEpochFromSameCompletion()
    {
        var lane = CreateLane(1);
        var progress = lane.EnableAutomaticCompletion();
        using var start = new ManualResetEventSlim();
        var finished = 0;
        var writer = Task.Run(() =>
        {
            start.Wait();
            for (var offset = 1; offset <= 100000; offset++)
                progress.Publish(offset, offset);
            Volatile.Write(ref finished, 1);
        });
        var reader = Task.Run(() =>
        {
            start.Set();
            do
            {
                if (lane.GetCommitOffset() is { } checkpoint && checkpoint.Offset != checkpoint.LeaderEpoch)
                    throw new InvalidOperationException("Checkpoint mixed two publications.");
            } while (Volatile.Read(ref finished) == 0);
        });
        await Task.WhenAll(writer, reader).WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(lane.GetCommitOffset()).IsEqualTo(new TopicPartitionOffset("dispatch", 0, 100000, 100000));
    }

    [Test]
    public async Task ConcurrentCompletions_AreObservedExactlyOnceAcrossReusedWorkers()
    {
        const int rounds = 64;
        const int concurrency = 3;
        var lane = CreateLane(rounds * concurrency);
        var releases = new TaskCompletionSource[rounds * concurrency];
        var started = new TaskCompletionSource[rounds];
        for (var index = 0; index < releases.Length; index++)
        {
            releases[index] = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await Assert.That(lane.TryEnqueueForTest(CreateRecord(index))).IsTrue();
        }
        for (var index = 0; index < started.Length; index++)
            started[index] = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await lane.StopAsync(PartitionStopPolicy.Drain, Timeout.InfiniteTimeSpan);

        var handled = 0;
        var dispatcher = new KeyOrderedPartitionDispatcher<int, int>(
            new PartitionProcessorContext<int, int>(lane), 1, concurrency, concurrency,
            (records, _) =>
            {
                var offset = checked((int)records[0].Offset);
                if (++handled % concurrency == 0)
                    started[handled / concurrency - 1].TrySetResult();
                return new ValueTask(releases[offset].Task);
            }, automaticCompletion: true);
        var processing = dispatcher.RunAsync(CancellationToken.None).AsTask();
        try
        {
            for (var round = 0; round < rounds; round++)
            {
                await started[round].Task.WaitAsync(TimeSpan.FromSeconds(10));
                var first = round * concurrency;
                // Complete in reverse order. Task continuations run independently and
                // publish to the shared stack while the coordinator can drain it.
                for (var index = concurrency - 1; index >= 0; index--)
                    releases[first + index].TrySetResult();
            }
        }
        finally
        {
            foreach (var release in releases)
                release.TrySetResult();
            await processing.WaitAsync(TimeSpan.FromSeconds(10));
        }

        await Assert.That(handled).IsEqualTo(releases.Length);
        await Assert.That(lane.GetCommitOffset()).IsEqualTo(
            new TopicPartitionOffset("dispatch", 0, releases.Length, releases.Length - 1));
        await Assert.That(dispatcher.LaneCount).IsEqualTo(0);
    }

    [Test]
    public async Task StalledFirstRecord_ReusesCompletedSlotsWithoutCommittingPastGap()
    {
        var lane = CreateLane(8);
        long[] offsets = [10, 12, 20, 21, 30, 31];
        foreach (var offset in offsets)
            await Assert.That(lane.TryEnqueueForTest(CreateRecord(offset))).IsTrue();
        await lane.StopAsync(PartitionStopPolicy.Drain, Timeout.InfiniteTimeSpan);

        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handled = new List<long>();
        var dispatcher = new KeyOrderedPartitionDispatcher<int, int>(
            new PartitionProcessorContext<int, int>(lane), 1, 2, 2,
            (records, _) =>
            {
                handled.Add(records[0].Offset);
                return records[0].Offset == 10 ? new ValueTask(releaseFirst.Task) : default;
            }, automaticCompletion: true);

        var processing = dispatcher.RunAsync(CancellationToken.None).AsTask();
        try
        {
            // Later keys can progress beyond the two-record pending budget while
            // the first key remains stalled, without retaining completed history.
            await Assert.That(handled).IsEquivalentTo(offsets);
            await Assert.That(lane.GetCommitOffset()).IsNull();
            await Assert.That(lane.LastProcessedOffset).IsEqualTo(31);
            await Assert.That(processing.IsCompleted).IsFalse();
        }
        finally
        {
            releaseFirst.TrySetResult();
            await processing.WaitAsync(TimeSpan.FromSeconds(10));
        }

        await Assert.That(handled).IsEquivalentTo(offsets);
        await Assert.That(lane.GetCommitOffset()).IsEqualTo(new TopicPartitionOffset("dispatch", 0, 32, 31));
        await Assert.That(dispatcher.LaneCount).IsEqualTo(0);
    }

    [Test]
    public async Task HandlerFailure_ObservesOtherHandlerBeforeReleasingItsBatch()
    {
        var lane = CreateLane(4);
        for (var offset = 0; offset < 4; offset++)
            await Assert.That(lane.TryEnqueueForTest(CreateRecord(offset))).IsTrue();
        await lane.StopAsync(PartitionStopPolicy.Drain, Timeout.InfiniteTimeSpan);

        var failFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSecond = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new InvalidOperationException("first handler failed");
        var invoked = new List<long>();
        var secondObserved = false;
        var dispatcher = new KeyOrderedPartitionDispatcher<int, int>(
            new PartitionProcessorContext<int, int>(lane), 1, 2, 4,
            async (records, token) =>
            {
                var offset = records[0].Offset;
                invoked.Add(offset);
                if (offset == 0)
                {
                    await failFirst.Task;
                    throw failure;
                }
                if (offset == 1)
                {
                    using var registration = token.Register(() => secondCancelled.TrySetResult());
                    await releaseSecond.Task;
                    await Assert.That(records.Count).IsEqualTo(1);
                    await Assert.That(records[0].Offset).IsEqualTo(1);
                    secondObserved = true;
                }
            }, automaticCompletion: true);

        var processing = dispatcher.RunAsync(CancellationToken.None).AsTask();
        try
        {
            failFirst.TrySetResult();
            await secondCancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(processing.IsCompleted).IsFalse();
            await Assert.That(invoked).IsEquivalentTo(new long[] { 0, 1 });
        }
        finally
        {
            releaseSecond.TrySetResult();
        }

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await processing.WaitAsync(TimeSpan.FromSeconds(10)));
        await Assert.That(thrown).IsSameReferenceAs(failure);
        await Assert.That(secondObserved).IsTrue();
        await Assert.That(lane.GetCommitOffset()).IsNull();
        await Assert.That(dispatcher.LaneCount).IsEqualTo(0);
    }

    [Test]
    public async Task DistinctKeyChurn_RemovesMembershipAndPreservesEveryCheckpoint()
    {
        const int count = 513;
        var lane = CreateLane(count);
        for (var offset = 0; offset < count; offset++)
            await Assert.That(lane.TryEnqueueForTest(CreateRecord(offset))).IsTrue();
        await lane.StopAsync(PartitionStopPolicy.Drain, Timeout.InfiniteTimeSpan);
        var handled = 0;
        var dispatcher = new KeyOrderedPartitionDispatcher<int, int>(
            new PartitionProcessorContext<int, int>(lane), 1, 4, 7,
            (records, _) =>
            {
                if (records.Count != 1 || records[0].Offset != handled++)
                    throw new InvalidOperationException("A record was lost or reordered.");
                return default;
            }, automaticCompletion: true);

        var processing = dispatcher.RunAsync(CancellationToken.None);
        await Assert.That(processing.IsCompletedSuccessfully).IsTrue();
        await processing;
        await Assert.That(handled).IsEqualTo(count);
        await Assert.That(lane.GetCommitOffset()).IsEqualTo(new TopicPartitionOffset("dispatch", 0, count, count - 1));
        await Assert.That(dispatcher.LaneCount).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task CompletionCleanupFailure_ObservesEveryDetachedWorker(bool multipleFailures, bool changeHash = false)
    {
        var failure = new InvalidOperationException("key cleanup failed");
        var keys = new[] { new ThrowingHashKey(0), new ThrowingHashKey(1), new ThrowingHashKey(2) };
        var lane = new PartitionLane<ThrowingHashKey, int>(
            new TopicPartition("dispatch", 0), 3,
            static (_, _) => default, static _ => { }, static (_, _) => { });
        for (var offset = 0; offset < keys.Length; offset++)
        {
            var record = new ConsumeResult<ThrowingHashKey, int>("dispatch", 0, offset,
                keys[offset], offset, null, 0, TimestampType.CreateTime, offset);
            await Assert.That(lane.TryEnqueueForTest(record)).IsTrue();
        }
        await lane.StopAsync(PartitionStopPolicy.Drain, Timeout.InfiniteTimeSpan);
        var first = new ObservedCompletion();
        var second = new ObservedCompletion();
        var dispatcher = new KeyOrderedPartitionDispatcher<ThrowingHashKey, int>(
            new PartitionProcessorContext<ThrowingHashKey, int>(lane), 1, 3, 3,
            (records, _) =>
            {
                switch (records[0].Offset)
                {
                    case 0:
                        return first.Task;
                    case 1:
                        return second.Task;
                    default:
                        // Both callbacks run inline while the coordinator is inside
                        // this handler. The failing key is first in the detached stack.
                        if (changeHash)
                            keys[0].HashCode = 100;
                        else
                            keys[0].Failure = failure;
                        if (multipleFailures)
                        {
                            if (changeHash)
                                keys[1].HashCode = 101;
                            else
                                keys[1].Failure = new InvalidOperationException("second key cleanup failed");
                        }
                        second.Complete();
                        first.Complete();
                        return default;
                }
            }, automaticCompletion: true);

        var processing = dispatcher.RunAsync(CancellationToken.None).AsTask();
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await processing.WaitAsync(TimeSpan.FromSeconds(5)));
        if (changeHash)
            await Assert.That(thrown!.Message).IsEqualTo("A partition key changed its hash code or equality while being processed.");
        else
            await Assert.That(thrown).IsSameReferenceAs(failure);
        await Assert.That(first.Observed).IsEqualTo(1);
        await Assert.That(second.Observed).IsEqualTo(1);
        await Assert.That(dispatcher.LaneCount).IsEqualTo(0);
    }

    [Test]
    [Arguments(1)]
    [Arguments(4)]
    public async Task CancellationDuringReadyDispatch_DoesNotStartQueuedHandlers(int batchSize)
    {
        const int count = 16;
        var lane = CreateLane(count);
        for (var offset = 0; offset < count; offset++)
            await Assert.That(lane.TryEnqueueForTest(CreateRecord(offset, keyOverride: offset % 4))).IsTrue();
        await lane.StopAsync(PartitionStopPolicy.Drain, Timeout.InfiniteTimeSpan);
        using var cancellation = new CancellationTokenSource();
        var first = new ObservedCompletion();
        var calls = 0;
        var completed = new List<long>();
        var dispatcher = new KeyOrderedPartitionDispatcher<int, int>(
            new PartitionProcessorContext<int, int>(lane), batchSize, 1, count,
            (records, _) =>
            {
                calls++;
                foreach (var record in records)
                    completed.Add(record.Offset);
                if (calls == 1)
                    return first.Task;
                // Cancel inside a synchronous handler while other keys are already
                // ready. No handler may start after this invocation returns.
                cancellation.Cancel();
                return default;
            }, automaticCompletion: true);

        var processing = dispatcher.RunAsync(cancellation.Token).AsTask();
        var initiallyStarted = calls;
        var bufferedKeys = dispatcher.LaneCount;
        first.Complete();
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await processing.WaitAsync(TimeSpan.FromSeconds(10)));
        await Assert.That(initiallyStarted).IsEqualTo(1);
        // Single-record, single-worker dispatch uses one FIFO lane for every key
        // type. Batched dispatch still queues four independent keyed lanes.
        await Assert.That(bufferedKeys).IsEqualTo(batchSize == 1 ? 1 : 4);
        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(completed.Count).IsEqualTo(1 + batchSize);
        await Assert.That(first.Observed).IsEqualTo(1);
        await Assert.That(dispatcher.LaneCount).IsEqualTo(0);
        await Assert.That(lane.GetCommitOffset()).IsEqualTo(new TopicPartitionOffset("dispatch", 0, 2, 1));
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    [Arguments(false, 1)]
    [Arguments(false, 2)]
    public async Task AwaiterSetupFailure_ReleasesWorkerAndPropagatesError(bool failStatus, int callbackMode = 0)
    {
        var lane = CreateLane(1);
        await Assert.That(lane.TryEnqueueForTest(CreateRecord(0))).IsTrue();
        await lane.StopAsync(PartitionStopPolicy.Drain, Timeout.InfiniteTimeSpan);
        var failure = new InvalidOperationException("awaiter setup failed");
        var source = new ThrowingCompletion(failure, failStatus, callbackMode);
        var dispatcher = new KeyOrderedPartitionDispatcher<int, int>(
            new PartitionProcessorContext<int, int>(lane), 1, 1, 1,
            (_, _) => new ValueTask(source, 0), automaticCompletion: true);
        var processing = dispatcher.RunAsync(CancellationToken.None).AsTask();
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await processing.WaitAsync(TimeSpan.FromSeconds(2)));
        await Assert.That(thrown).IsSameReferenceAs(failure);
        source.CompleteLate();
        await Assert.That(source.Observed).IsEqualTo(callbackMode == 1 ? 1 : 0);
        await Assert.That(lane.GetCommitOffset()).IsNull();
        await Assert.That(dispatcher.LaneCount).IsEqualTo(0);
    }

    [Test]
    public async Task MutatedKeyMatchingAnotherLane_StopsBeforeDispatchingMoreRecords()
    {
        var keys = new[] { new MutableKey(0), new MutableKey(1), new MutableKey(2), new MutableKey(3) };
        var lane = new PartitionLane<MutableKey, int>(new TopicPartition("dispatch", 0), 4,
            static (_, _) => default, static _ => { }, static (_, _) => { });
        var storage = new TrackedMemory[keys.Length];
        for (var offset = 0; offset < keys.Length; offset++)
        {
            var memory = storage[offset] = new TrackedMemory();
            var batch = new RecordBatch
            {
                BaseOffset = offset, LastOffsetDelta = 0,
                Records = [new Record { Key = memory.Memory, Value = memory.Memory }]
            };
            using var pending = PendingFetchData.Create("dispatch", 0, [batch], memoryOwner: memory);
            pending.EagerParseAll();
            var records = new ConsumeBatch<MutableKey, int>(pending, new MutableKeyDeserializer(keys[offset]), Serializers.Int32).GetEnumerator();
            await Assert.That(records.MoveNext()).IsTrue();
            await Assert.That(lane.TryEnqueueForTest(records.Current)).IsTrue();
        }
        await lane.StopAsync(PartitionStopPolicy.Drain, Timeout.InfiniteTimeSpan);
        var first = new ObservedCompletion();
        var second = new ObservedCompletion();
        var handled = new List<long>();
        var dispatcher = new KeyOrderedPartitionDispatcher<MutableKey, int>(
            new PartitionProcessorContext<MutableKey, int>(lane), 1, 3, 4,
            (records, _) =>
            {
                var offset = records[0].Offset;
                handled.Add(offset);
                if (offset == 0)
                    return first.Task;
                if (offset == 1)
                    return second.Task;
                if (offset == 2)
                {
                    keys[0].Value = keys[1].Value;
                    first.Complete();
                }
                return default;
            }, automaticCompletion: true);
        var processing = dispatcher.RunAsync(CancellationToken.None).AsTask();
        second.Complete();
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await processing.WaitAsync(TimeSpan.FromSeconds(2)));
        await Assert.That(thrown!.Message).IsEqualTo("A partition key changed its hash code or equality while being processed.");
        foreach (var memory in storage)
            await Assert.That(memory.DisposeCount).IsEqualTo(1);
        await Assert.That(handled).IsEquivalentTo(new long[] { 0, 1, 2 });
        await Assert.That(first.Observed).IsEqualTo(1);
        await Assert.That(second.Observed).IsEqualTo(1);
        await Assert.That(dispatcher.LaneCount).IsEqualTo(0);
    }

    [Test]
    public async Task BinaryHashRebuildFailure_ObservesWorkersAndReleasesEveryOwner()
    {
        const int count = 9;
        var lane = new PartitionLane<ReadOnlyMemory<byte>, int>(
            new TopicPartition("dispatch", 0), count,
            static (_, _) => default, static _ => { }, static (_, _) => { });
        var storage = new TrackedMemory[count];
        for (var offset = 0; offset < count; offset++)
        {
            var memory = storage[offset] = new TrackedMemory(1024);
            memory.Bytes[^32] = (byte)offset;
            var batch = new RecordBatch
            {
                BaseOffset = offset, LastOffsetDelta = 0,
                Records = [new Record { Key = memory.Memory, Value = memory.Memory[..sizeof(int)] }]
            };
            using var pending = PendingFetchData.Create("dispatch", 0, [batch], memoryOwner: memory);
            pending.EagerParseAll();
            var records = new ConsumeBatch<ReadOnlyMemory<byte>, int>(pending, Serializers.RawBytes, Serializers.Int32).GetEnumerator();
            await Assert.That(records.MoveNext()).IsTrue();
            await Assert.That(lane.TryEnqueueForTest(records.Current)).IsTrue();
        }
        await lane.StopAsync(PartitionStopPolicy.Drain, Timeout.InfiniteTimeSpan);
        var workers = new[] { new ObservedCompletion(), new ObservedCompletion(), new ObservedCompletion() };
        var started = 0;
        var dispatcher = new KeyOrderedPartitionDispatcher<ReadOnlyMemory<byte>, int>(
            new PartitionProcessorContext<ReadOnlyMemory<byte>, int>(lane), 1, 3, count,
            (records, _) =>
            {
                var offset = checked((int)records[0].Offset);
                started++;
                // Both keys already own different lanes. Mutation makes the later
                // collision-triggered rebuild encounter duplicate keys after clearing.
                if (offset == 2)
                    storage[0].Bytes[^32] = storage[1].Bytes[^32];
                return workers[offset].Task;
            }, automaticCompletion: true);
        var processing = dispatcher.RunAsync(CancellationToken.None).AsTask();
        try
        {
            await Assert.That(started).IsEqualTo(3);
            await Assert.That(processing.IsCompleted).IsFalse();
            foreach (var memory in storage)
                await Assert.That(memory.DisposeCount).IsEqualTo(0);
        }
        finally
        {
            foreach (var worker in workers)
                worker.Complete();
        }
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await processing.WaitAsync(TimeSpan.FromSeconds(5)));
        await Assert.That(thrown!.InnerException).IsTypeOf<ArgumentException>();
        foreach (var memory in storage)
            await Assert.That(memory.DisposeCount).IsEqualTo(1);
        foreach (var worker in workers)
            await Assert.That(worker.Observed).IsEqualTo(1);
        await Assert.That(dispatcher.LaneCount).IsEqualTo(0);
    }

    private sealed class TrackedMemory(int length = sizeof(int)) : IPooledMemory
    {
        internal byte[] Bytes { get; } = new byte[length];
        public ReadOnlyMemory<byte> Memory => Bytes;
        internal int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }

    private sealed class MutableKeyDeserializer(MutableKey key) : IDeserializer<MutableKey>
    {
        public MutableKey Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) => key;
    }

    private sealed class MutableKey(int value)
    {
        internal int Value { get; set; } = value;
        public override int GetHashCode() => Value;
        public override bool Equals(object? obj) => obj is MutableKey other && other.Value == Value;
    }

    private sealed class ThrowingCompletion(Exception failure, bool failStatus, int callbackMode) : IValueTaskSource
    {
        private Action<object?>? _continuation;
        private object? _state;
        internal int Observed { get; private set; }
        public void GetResult(short token)
        {
            Observed++;
            throw failure;
        }
        public ValueTaskSourceStatus GetStatus(short token) => failStatus ? throw failure : ValueTaskSourceStatus.Pending;
        public void OnCompleted(Action<object?> continuation, object? state, short token,
            ValueTaskSourceOnCompletedFlags flags)
        {
            if (callbackMode == 1)
                continuation(state);
            else if (callbackMode == 2)
            {
                _continuation = continuation;
                _state = state;
            }
            throw failure;
        }
        internal void CompleteLate() => _continuation?.Invoke(_state);
    }
    private sealed class ThrowingHashKey(int value)
    {
        internal Exception? Failure { get; set; }
        internal int HashCode { get; set; } = value;
        public override int GetHashCode() => Failure is { } failure ? throw failure : HashCode;
        public override bool Equals(object? obj) => ReferenceEquals(this, obj);
    }

    private sealed class ObservedCompletion : IValueTaskSource
    {
        private ManualResetValueTaskSourceCore<bool> _source;
        internal int Observed { get; private set; }
        internal ValueTask Task => new(this, _source.Version);
        internal void Complete() => _source.SetResult(true);
        public void GetResult(short token)
        {
            _source.GetResult(token);
            Observed++;
        }
        public ValueTaskSourceStatus GetStatus(short token) => _source.GetStatus(token);
        public void OnCompleted(Action<object?> continuation, object? state, short token,
            ValueTaskSourceOnCompletedFlags flags) =>
            _source.OnCompleted(continuation, state, token, flags);
    }
    private static PartitionLane<int, int> CreateLane(int capacity) => new(
        new TopicPartition("dispatch", 0), capacity,
        static (_, _) => default, static _ => { }, static (_, _) => { });

    private static ConsumeResult<int, int> CreateRecord(long offset, int? keyOverride = null)
    {
        var key = new byte[sizeof(int)];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(key, keyOverride ?? checked((int)offset));
        return new ConsumeResult<int, int>("dispatch", 0, offset, key, false,
            default, true, null, 0, TimestampType.CreateTime, checked((int)offset), Serializers.Int32, null);
    }
}
