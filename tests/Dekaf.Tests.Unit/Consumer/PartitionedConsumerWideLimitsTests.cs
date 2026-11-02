using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class PartitionedConsumerWideLimitsTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Test]
    public async Task Options_RejectNonPositiveConsumerWideLimits()
    {
        var consumer = new PartitionedConsumerRuntimeTests.TestConsumer();

        await Assert.That(async () => await consumer.RunPartitionedAsync(
                static (_, _, _) => default,
                new PartitionedProcessingOptions { MaxConcurrentHandlers = 0 }))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(async () => await consumer.RunPartitionedAsync(
                static (_, _, _) => default,
                new PartitionedProcessingOptions { MaxBufferedRecords = 0 }))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Options_HandlerLimitRequiresHandlerOverload()
    {
        var consumer = new PartitionedConsumerRuntimeTests.TestConsumer();

        await Assert.That(async () => await consumer.RunPartitionedAsync(
                static (_, _) => default,
                new PartitionedProcessingOptions { MaxConcurrentHandlers = 1 }))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task BufferBudget_SignalsCapacityAndResumesOncePerRequest()
    {
        var capacity = 0;
        var resumes = 0;
        var budget = new PartitionedBufferBudget(4, () => capacity++, () => resumes++);

        for (var i = 0; i < 4; i++)
            await Assert.That(budget.TryReserve()).IsTrue();
        await Assert.That(budget.TryReserve()).IsFalse();
        await Assert.That(budget.IsFull).IsTrue();

        budget.Release();
        await Assert.That(capacity).IsEqualTo(1);

        // Above half the limit the request waits for the buffer to drain.
        budget.RequestResume();
        await Assert.That(resumes).IsEqualTo(0);
        budget.Release();
        await Assert.That(resumes).IsEqualTo(1);
        budget.Release();
        await Assert.That(resumes).IsEqualTo(1);

        // At or below half the limit a request resumes immediately.
        budget.RequestResume();
        await Assert.That(resumes).IsEqualTo(2);
        await Assert.That(capacity).IsEqualTo(1);
        await Assert.That(budget.Count).IsEqualTo(1);
    }

    [Test]
    public async Task HandlerSlots_ServeWaitersInArrivalOrderWithoutBarging()
    {
        var slots = new HandlerSlotLimiter(1);
        var first = new AsyncHandlerSlotWaiter(slots);
        var second = new AsyncHandlerSlotWaiter(slots);
        await Assert.That(slots.TryAcquire()).IsTrue();

        var firstWait = first.AcquireAsync(CancellationToken.None).AsTask();
        var secondWait = second.AcquireAsync(CancellationToken.None).AsTask();
        await Assert.That(firstWait.IsCompleted).IsFalse();
        await Assert.That(secondWait.IsCompleted).IsFalse();

        slots.Release();
        await firstWait.WaitAsync(Timeout);
        await Assert.That(secondWait.IsCompleted).IsFalse();
        // A queued waiter keeps a new caller from taking a released slot first.
        await Assert.That(slots.TryAcquire()).IsFalse();

        first.Release();
        await secondWait.WaitAsync(Timeout);
        second.Release();
        await Assert.That(slots.Available).IsEqualTo(1);
    }

    [Test]
    public async Task HandlerSlots_CancelledWaiterDoesNotKeepSlot()
    {
        var slots = new HandlerSlotLimiter(1);
        var waiter = new AsyncHandlerSlotWaiter(slots);
        using var cancellation = new CancellationTokenSource();
        await Assert.That(slots.TryAcquire()).IsTrue();

        var waiting = waiter.AcquireAsync(cancellation.Token).AsTask();
        await cancellation.CancelAsync();
        await Assert.That(async () => await waiting.WaitAsync(Timeout)).Throws<OperationCanceledException>();

        slots.Release();
        await Assert.That(slots.Available).IsEqualTo(1);
        await Assert.That(slots.TryAcquire()).IsTrue();
    }

    [Test]
    public async Task Lanes_ShareBufferBudget()
    {
        var capacityCalls = 0;
        var limits = new PartitionedProcessingLimits(
            new PartitionedBufferBudget(3, () => capacityCalls++, static () => { }), null);
        var a = CreateLane(new TopicPartition("limits", 0), limits);
        var b = CreateLane(new TopicPartition("limits", 1), limits);

        await Assert.That(a.TryEnqueueForTest(CreateResult(a.TopicPartition, 0))).IsTrue();
        await Assert.That(a.TryEnqueueForTest(CreateResult(a.TopicPartition, 1))).IsTrue();
        await Assert.That(b.TryEnqueueForTest(CreateResult(b.TopicPartition, 0))).IsTrue();
        await Assert.That(b.TryEnqueueForTest(CreateResult(b.TopicPartition, 1))).IsFalse();
        await Assert.That(a.TryEnqueueForTest(CreateResult(a.TopicPartition, 2))).IsFalse();
        await Assert.That(limits.Buffer!.Count).IsEqualTo(3);

        await Assert.That(a.TryReadMessage(out var read)).IsTrue();
        read.ReleaseStorage();
        await Assert.That(limits.Buffer.Count).IsEqualTo(2);
        await Assert.That(capacityCalls).IsEqualTo(1);
        await Assert.That(b.TryEnqueueForTest(CreateResult(b.TopicPartition, 1))).IsTrue();
    }

    [Test]
    public async Task Lane_ReleasesBudgetForRecordsItsProcessorNeverRead()
    {
        var limits = new PartitionedProcessingLimits(
            new PartitionedBufferBudget(10, static () => { }, static () => { }), null);
        var lane = CreateLane(new TopicPartition("limits", 0), limits);
        await Assert.That(lane.TryEnqueueForTest(CreateResult(lane.TopicPartition, 0))).IsTrue();
        await Assert.That(lane.TryEnqueueForTest(CreateResult(lane.TopicPartition, 1))).IsTrue();

        lane.Start(static async (_, token) => await Task.Delay(System.Threading.Timeout.Infinite, token));
        var exception = await lane.StopAsync(PartitionStopPolicy.Cancel, Timeout);

        await Assert.That(exception).IsNull();
        await Assert.That(limits.Buffer!.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task KeyOrderedDispatcher_CountsHeldRecordsUntilDispatched(bool fail)
    {
        var limits = new PartitionedProcessingLimits(
            new PartitionedBufferBudget(10, static () => { }, static () => { }), null);
        var lane = CreateLane(new TopicPartition("limits", 0), limits);
        for (var offset = 0; offset < 4; offset++)
            await Assert.That(lane.TryEnqueueForTest(CreateResult(lane.TopicPartition, offset, $"key-{offset}"))).IsTrue();
        await lane.StopAsync(PartitionStopPolicy.Drain, System.Threading.Timeout.InfiniteTimeSpan);

        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var processed = 0;
        var dispatcher = new KeyOrderedPartitionDispatcher<string, string>(
            new PartitionProcessorContext<string, string>(lane), 1, 1, 10,
            async (records, token) =>
            {
                if (records[0].Offset == 0)
                {
                    await release.Task.WaitAsync(token);
                    if (fail)
                        throw new InvalidOperationException("handler failed");
                }

                Interlocked.Increment(ref processed);
            },
            automaticCompletion: true);

        // The coordinator reads the whole completed input before its first await. The record
        // in the handler no longer counts; the three it holds for the busy worker still do.
        var running = dispatcher.RunAsync(CancellationToken.None).AsTask();
        await Assert.That(running.IsCompleted).IsFalse();
        await Assert.That(limits.Buffer!.Count).IsEqualTo(3);

        release.TrySetResult();
        if (fail)
            await Assert.That(async () => await running.WaitAsync(Timeout)).Throws<InvalidOperationException>();
        else
            await running.WaitAsync(Timeout);

        await Assert.That(limits.Buffer.Count).IsEqualTo(0);
        await Assert.That(processed).IsEqualTo(fail ? 0 : 4);
    }

    [Test]
    public async Task KeyOrderedDispatcher_WaitsForConsumerWideHandlerSlot()
    {
        var slots = new HandlerSlotLimiter(1);
        var limits = new PartitionedProcessingLimits(null, slots);
        var lane = CreateLane(new TopicPartition("limits", 0), limits);
        for (var offset = 0; offset < 3; offset++)
            await Assert.That(lane.TryEnqueueForTest(CreateResult(lane.TopicPartition, offset, $"key-{offset}"))).IsTrue();
        await lane.StopAsync(PartitionStopPolicy.Drain, System.Threading.Timeout.InfiniteTimeSpan);

        // Another partition holds the only slot.
        await Assert.That(slots.TryAcquire()).IsTrue();
        var processed = 0;
        var dispatcher = new KeyOrderedPartitionDispatcher<string, string>(
            new PartitionProcessorContext<string, string>(lane), 1, 4, 10,
            (_, _) =>
            {
                Interlocked.Increment(ref processed);
                return default;
            },
            automaticCompletion: true);

        var running = dispatcher.RunAsync(CancellationToken.None).AsTask();
        await Assert.That(running.IsCompleted).IsFalse();
        await Assert.That(Volatile.Read(ref processed)).IsEqualTo(0);

        slots.Release();
        await running.WaitAsync(Timeout);
        await Assert.That(processed).IsEqualTo(3);
        await Assert.That(slots.Available).IsEqualTo(1);
    }

    [Test]
    public async Task PartitionOrderedRecords_EmptyLaneDrainsWithoutWaitingForHandlerSlot()
    {
        // B's last record finishes while A waits for the only slot. The slot goes to A, whose
        // handler keeps running. B has nothing queued, so a drain must not wait for A's handler.
        var slots = new HandlerSlotLimiter(1);
        var limits = new PartitionedProcessingLimits(null, slots);
        var a = CreateLane(new TopicPartition("limits", 0), limits);
        var b = CreateLane(new TopicPartition("limits", 1), limits);
        var aEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseB = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var processor = PartitionedConsumerExtensions.CreateRecordProcessor<string, string>(
            async (_, message, token) =>
            {
                var (entered, release) = message.Partition == 0 ? (aEntered, releaseA) : (bEntered, releaseB);
                entered.TrySetResult();
                await release.Task.WaitAsync(token);
            },
            new PartitionedProcessingOptions { MaxConcurrentHandlers = 1 },
            null);

        await Assert.That(b.TryEnqueueForTest(CreateResult(b.TopicPartition, 0))).IsTrue();
        b.Start(processor);
        await bEntered.Task.WaitAsync(Timeout);

        await Assert.That(a.TryEnqueueForTest(CreateResult(a.TopicPartition, 0))).IsTrue();
        a.Start(processor);
        while (slots.Queued == 0)
            await Task.Yield();

        releaseB.TrySetResult();
        await aEntered.Task.WaitAsync(Timeout);

        var bStop = await b.StopAsync(PartitionStopPolicy.Drain, Timeout);
        await Assert.That(bStop).IsNull();

        releaseA.TrySetResult();
        var aStop = await a.StopAsync(PartitionStopPolicy.Drain, Timeout);
        await Assert.That(aStop).IsNull();
        await Assert.That(slots.Available).IsEqualTo(1);
    }

    [Test]
    public async Task Discard_WakesKeyDispatcherWaitingForHandlerSlot()
    {
        // B's handler holds the only slot. A's dispatcher fills its buffer, then waits for a slot
        // with no pending read, so only marking the lane for discard can wake it.
        var slots = new HandlerSlotLimiter(1);
        var limits = new PartitionedProcessingLimits(null, slots);
        var a = CreateLane(new TopicPartition("limits", 0), limits);
        var b = CreateLane(new TopicPartition("limits", 1), limits);
        var aEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseB = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var processor = PartitionedConsumerExtensions.CreateRecordProcessor<string, string>(
            async (_, message, token) =>
            {
                if (message.Partition == 0)
                {
                    aEntered.TrySetResult();
                    return;
                }

                bEntered.TrySetResult();
                await releaseB.Task.WaitAsync(token);
            },
            new PartitionedProcessingOptions
            {
                Ordering = PartitionedProcessingOrder.Key,
                MaxConcurrentHandlers = 1,
                MaxBufferedRecordsPerPartition = 1
            },
            null);

        await Assert.That(b.TryEnqueueForTest(CreateResult(b.TopicPartition, 0, key: "k"))).IsTrue();
        b.Start(processor);
        await bEntered.Task.WaitAsync(Timeout);

        await Assert.That(a.TryEnqueueForTest(CreateResult(a.TopicPartition, 0, key: "k"))).IsTrue();
        a.Start(processor);
        while (slots.Queued == 0)
            await Task.Yield();

        a.DiscardQueuedRecords();
        var aStop = await a.StopAsync(PartitionStopPolicy.Drain, TimeSpan.FromSeconds(5));
        await Assert.That(aStop).IsNull();
        await Assert.That(aEntered.Task.IsCompleted).IsFalse();

        releaseB.TrySetResult();
        var bStop = await b.StopAsync(PartitionStopPolicy.Drain, Timeout);
        await Assert.That(bStop).IsNull();
        await Assert.That(slots.Available).IsEqualTo(1);
    }

    [Test]
    [Arguments(PartitionedProcessingOrder.Partition, false)]
    [Arguments(PartitionedProcessingOrder.Partition, true)]
    [Arguments(PartitionedProcessingOrder.Key, false)]
    [Arguments(PartitionedProcessingOrder.Key, true)]
    public async Task Run_NeverExceedsConsumerWideHandlerLimit(PartitionedProcessingOrder ordering, bool batches)
    {
        const int partitionCount = 4;
        const int recordsPerPartition = 6;
        const int limit = 2;
        var consumer = CreateConsumer(partitionCount, recordsPerPartition);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var limitReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var maxActive = 0;
        var processed = 0;

        async ValueTask HandleAsync(int count, CancellationToken token)
        {
            var now = Interlocked.Increment(ref active);
            UpdateMax(ref maxActive, now);
            if (now == limit)
                limitReached.TrySetResult();
            await gate.Task.WaitAsync(token);
            Interlocked.Decrement(ref active);
            if (Interlocked.Add(ref processed, count) == partitionCount * recordsPerPartition)
                allProcessed.TrySetResult();
        }

        var options = new PartitionedProcessingOptions
        {
            Ordering = ordering,
            MaxConcurrentHandlersPerPartition = 4,
            MaxHandlerBatchSize = 2,
            MaxConcurrentHandlers = limit
        };
        using var stop = new CancellationTokenSource();
        var running = batches
            ? consumer.RunPartitionedBatchesAsync((_, records, token) => HandleAsync(records.Count, token), options, stop.Token).AsTask()
            : consumer.RunPartitionedAsync((_, _, token) => HandleAsync(1, token), options, stop.Token).AsTask();

        try
        {
            await limitReached.Task.WaitAsync(Timeout);
            gate.TrySetResult();
            await allProcessed.Task.WaitAsync(Timeout);
            await Assert.That(Volatile.Read(ref maxActive)).IsEqualTo(limit);
        }
        finally
        {
            gate.TrySetResult();
            await StopAsync(stop, running);
        }
    }

    [Test]
    [Arguments(PartitionBackpressureMode.PauseResume, PartitionedProcessingOrder.Partition)]
    [Arguments(PartitionBackpressureMode.AwaitCapacity, PartitionedProcessingOrder.Partition)]
    [Arguments(PartitionBackpressureMode.PauseResume, PartitionedProcessingOrder.Key)]
    [Arguments(PartitionBackpressureMode.AwaitCapacity, PartitionedProcessingOrder.Key)]
    public async Task Run_ProcessesEveryPartitionUnderConsumerWideBufferLimit(
        PartitionBackpressureMode mode,
        PartitionedProcessingOrder ordering)
    {
        // A budget that leaked a single record, or a partition never resumed after the budget
        // drained, would stall this run before every record is processed.
        const int partitionCount = 3;
        const int recordsPerPartition = 40;
        var consumer = CreateConsumer(partitionCount, recordsPerPartition);
        var allProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var offsets = new ConcurrentDictionary<int, ConcurrentQueue<long>>();
        var processed = 0;

        using var stop = new CancellationTokenSource();
        var running = consumer.RunPartitionedAsync(async (context, message, _) =>
        {
            await Task.Yield();
            offsets.GetOrAdd(context.TopicPartition.Partition, static _ => new()).Enqueue(message.Offset);
            if (Interlocked.Increment(ref processed) == partitionCount * recordsPerPartition)
                allProcessed.TrySetResult();
        }, new PartitionedProcessingOptions
        {
            Ordering = ordering,
            BackpressureMode = mode,
            MaxConcurrentHandlersPerPartition = 2,
            MaxBufferedRecords = 2,
            MaxConcurrentHandlers = 2
        }, stop.Token).AsTask();

        try
        {
            await allProcessed.Task.WaitAsync(Timeout);
            for (var partition = 0; partition < partitionCount; partition++)
            {
                var seen = offsets[partition].ToArray();
                await Assert.That(seen.Length).IsEqualTo(recordsPerPartition);
                if (ordering == PartitionedProcessingOrder.Partition)
                    await Assert.That(seen).IsEquivalentTo(Enumerable.Range(0, recordsPerPartition).Select(static i => (long)i));
            }
        }
        finally
        {
            await StopAsync(stop, running);
        }
    }

    private static PartitionedConsumerRuntimeTests.TestConsumer CreateConsumer(int partitionCount, int recordsPerPartition)
    {
        var consumer = new PartitionedConsumerRuntimeTests.TestConsumer();
        var partitions = Enumerable.Range(0, partitionCount)
            .Select(static partition => new TopicPartition("limits", partition))
            .ToArray();
        consumer.SetAssignment(partitions);
        for (var offset = 0; offset < recordsPerPartition; offset++)
        {
            foreach (var partition in partitions)
                consumer.Enqueue(CreateResult(partition, offset, $"key-{offset}"));
        }

        return consumer;
    }

    private static async Task StopAsync(CancellationTokenSource stop, Task running)
    {
        await stop.CancelAsync();
        try
        {
            await running.WaitAsync(Timeout);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
        }
    }

    private static void UpdateMax(ref int max, int value)
    {
        var current = Volatile.Read(ref max);
        while (value > current)
        {
            var observed = Interlocked.CompareExchange(ref max, value, current);
            if (observed == current)
                return;
            current = observed;
        }
    }

    private static PartitionLane<string, string> CreateLane(TopicPartition partition, PartitionedProcessingLimits limits)
        => new(
            partition,
            16,
            static (_, _) => ValueTask.CompletedTask,
            static _ => { },
            static (_, exception) => throw new InvalidOperationException("Lane failed.", exception),
            limits);

    private static ConsumeResult<string, string> CreateResult(TopicPartition partition, long offset, string? key = null)
    {
        var keyData = key is null
            ? ReadOnlyMemory<byte>.Empty
            : System.Text.Encoding.UTF8.GetBytes(key);

        return new ConsumeResult<string, string>(
            topic: partition.Topic,
            partition: partition.Partition,
            offset: offset,
            keyData: keyData,
            isKeyNull: key is null,
            valueData: default,
            isValueNull: true,
            headers: null,
            timestampMs: 0,
            timestampType: TimestampType.NotAvailable,
            leaderEpoch: null,
            keyDeserializer: Serializers.String,
            valueDeserializer: null);
    }
}
