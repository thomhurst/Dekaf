using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class PartitionedDispatchCoordinatorTests
{
    [Test]
    public async Task GrowingPendingAndBatchStorage_PreservesActiveRecordsAndOrder()
    {
        const int count = 257;
        var lane = CreateLane(count);
        for (var offset = 0; offset < count; offset++)
            await Assert.That(lane.TryEnqueue(CreateRecord(offset, keyOverride: 0))).IsTrue();
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
        await Assert.That(lane.TryEnqueue(CreateRecord(0))).IsTrue();
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
            await Assert.That(lane.TryEnqueue(CreateRecord(index))).IsTrue();
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
            await Assert.That(lane.TryEnqueue(CreateRecord(index))).IsTrue();
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
            await Assert.That(lane.TryEnqueue(CreateRecord(offset))).IsTrue();
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
            await Assert.That(lane.TryEnqueue(CreateRecord(offset))).IsTrue();
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
            await Assert.That(lane.TryEnqueue(CreateRecord(offset))).IsTrue();
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
