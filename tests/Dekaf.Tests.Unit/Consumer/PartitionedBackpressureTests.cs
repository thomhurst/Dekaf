using System.Reflection;
using System.Runtime.CompilerServices;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class PartitionedBackpressureTests
{
    [Test]
    [Arguments(PartitionBackpressureMode.AwaitCapacity)]
    [Arguments(PartitionBackpressureMode.PauseResume)]
    public async Task FullQueue_HandlerCommitsWithoutReadingAhead(PartitionBackpressureMode mode)
    {
        var consumer = new FullBatchConsumer();
        consumer.SetAssignment(new TopicPartition("backpressure", 0));
        var requestCommit = NewSignal();
        var unprocessedCommitReturned = NewSignal();
        var markProcessed = NewSignal();
        var processedCommitReturned = NewSignal();
        var releaseFirst = NewSignal();
        var allProcessed = NewSignal();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var running = consumer.RunPartitionedAsync(async (context, token) =>
        {
            await foreach (var record in context.Messages.WithCancellation(token))
            {
                if (record.Offset == 0)
                {
                    await requestCommit.Task.WaitAsync(token);
                    await context.CommitProcessedAsync(token);
                    unprocessedCommitReturned.TrySetResult();
                    await markProcessed.Task.WaitAsync(token);
                    context.MarkProcessed(record);
                    await context.CommitProcessedAsync(token);
                    processedCommitReturned.TrySetResult();
                    await releaseFirst.Task.WaitAsync(token);
                }
                else
                {
                    context.MarkProcessed(record);
                    if (record.Offset == 2)
                        allProcessed.TrySetResult();
                }
            }
        }, new PartitionedProcessingOptions
        {
            BackpressureMode = mode,
            MaxBufferedRecordsPerPartition = 1,
            CommitPolicy = PartitionCommitPolicy.UserManaged,
            StopPolicy = PartitionStopPolicy.Cancel
        }, timeout.Token).AsTask();

        try
        {
            // Seeing offset 2 deserialized proves offset 1 was queued. Offset 0's
            // handler cannot read again until both requested commits have returned.
            await consumer.ThirdRecordRead.Task.WaitAsync(timeout.Token);
            requestCommit.TrySetResult();
            await unprocessedCommitReturned.Task.WaitAsync(timeout.Token);
            await Assert.That(consumer.CommitCalls).IsEmpty();

            markProcessed.TrySetResult();
            await processedCommitReturned.Task.WaitAsync(timeout.Token);
            await Assert.That(consumer.CommitCalls.Count).IsEqualTo(1);
            await Assert.That(consumer.CommitCalls[0]).IsEquivalentTo(
                new[] { new TopicPartitionOffset("backpressure", 0, 1) });
            await Assert.That(allProcessed.Task.IsCompleted).IsFalse();

            releaseFirst.TrySetResult();
            await allProcessed.Task.WaitAsync(timeout.Token);
        }
        finally
        {
            await timeout.CancelAsync();
            try { await running; }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                await Assert.That(running.IsCanceled).IsTrue();
            }
        }
    }

    [Test]
    [Arguments(PartitionBackpressureMode.AwaitCapacity, false)]
    [Arguments(PartitionBackpressureMode.PauseResume, false)]
    [Arguments(PartitionBackpressureMode.AwaitCapacity, true)]
    [Arguments(PartitionBackpressureMode.PauseResume, true)]
    public async Task FullQueue_DrainServicesHandlerCommits(
        PartitionBackpressureMode mode, bool shutdown)
    {
        var partition = new TopicPartition("backpressure", 0);
        var consumer = new FullBatchConsumer();
        consumer.SetAssignment(partition);
        var releaseFirst = NewSignal();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var stop = new CancellationTokenSource();
        var running = consumer.RunPartitionedAsync(async (context, token) =>
        {
            await foreach (var record in context.Messages.WithCancellation(token))
            {
                if (record.Offset == 0)
                    await releaseFirst.Task.WaitAsync(token);
                context.MarkProcessed(record);
                await context.CommitProcessedAsync(token);
            }
        }, new PartitionedProcessingOptions
        {
            BackpressureMode = mode,
            MaxBufferedRecordsPerPartition = 1,
            CommitPolicy = PartitionCommitPolicy.UserManaged,
            StopPolicy = PartitionStopPolicy.Drain,
            StopTimeout = TimeSpan.FromSeconds(1)
        }, stop.Token).AsTask();

        try
        {
            await consumer.ThirdRecordRead.Task.WaitAsync(timeout.Token);
            if (shutdown)
                await stop.CancelAsync();
            else
                consumer.RevokeFromCoordinator(partition);
            releaseFirst.TrySetResult();

            if (shutdown)
            {
                await Assert.That(async () => await running.WaitAsync(timeout.Token))
                    .Throws<OperationCanceledException>();
            }
            else
            {
                var completed = await Task.WhenAny(consumer.BatchAdvanced.Task, running).WaitAsync(timeout.Token);
                if (completed == running)
                    await running;
                await consumer.BatchAdvanced.Task.WaitAsync(timeout.Token);
            }

            await Assert.That(consumer.CommitCalls.Count).IsEqualTo(2);
            await Assert.That(consumer.CommitCalls[0][0].Offset).IsEqualTo(1);
            await Assert.That(consumer.CommitCalls[1][0].Offset).IsEqualTo(2);
        }
        finally
        {
            releaseFirst.TrySetResult();
            await StopAsync(stop, running);
        }
    }

    [Test]
    [Arguments(PartitionBackpressureMode.AwaitCapacity, false)]
    [Arguments(PartitionBackpressureMode.PauseResume, false)]
    [Arguments(PartitionBackpressureMode.AwaitCapacity, true)]
    [Arguments(PartitionBackpressureMode.PauseResume, true)]
    public async Task FullQueue_RevokeOrLossStopsRouting(
        PartitionBackpressureMode mode, bool lost)
    {
        var partition = new TopicPartition("backpressure", 0);
        var consumer = new FullBatchConsumer();
        consumer.SetAssignment(partition);
        var keepHandler = NewSignal();
        var delivered = new List<long>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var running = consumer.RunPartitionedAsync(async (context, token) =>
        {
            await foreach (var record in context.Messages.WithCancellation(token))
            {
                delivered.Add(record.Offset);
                context.MarkProcessed(record);
                await keepHandler.Task.WaitAsync(token);
            }
        }, new PartitionedProcessingOptions
        {
            BackpressureMode = mode,
            MaxBufferedRecordsPerPartition = 1,
            CommitPolicy = PartitionCommitPolicy.CommitCompletedOnRevoke,
            StopPolicy = PartitionStopPolicy.Cancel
        }, timeout.Token).AsTask();

        try
        {
            await consumer.ThirdRecordRead.Task.WaitAsync(timeout.Token);
            if (lost)
                consumer.LoseFromCoordinator(partition);
            else
                consumer.RevokeFromCoordinator(partition);

            await consumer.BatchAdvanced.Task.WaitAsync(timeout.Token);
            await Assert.That(delivered).IsEquivalentTo(new long[] { 0 });
            await Assert.That(consumer.CommitCalls.Count).IsEqualTo(lost ? 0 : 1);
            if (!lost)
                await Assert.That(consumer.CommitCalls[0]).IsEquivalentTo(
                    new[] { new TopicPartitionOffset("backpressure", 0, 1) });
        }
        finally
        {
            await StopAsync(timeout, running);
        }
    }

    [Test]
    [Arguments(PartitionBackpressureMode.AwaitCapacity)]
    [Arguments(PartitionBackpressureMode.PauseResume)]
    public async Task FullQueue_UnexpectedProcessorCompletionStopsRuntime(PartitionBackpressureMode mode)
    {
        var consumer = new FullBatchConsumer();
        consumer.SetAssignment(new TopicPartition("backpressure", 0));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var running = consumer.RunPartitionedAsync(async (context, token) =>
        {
            await foreach (var _ in context.Messages.WithCancellation(token))
            {
                await consumer.ThirdRecordRead.Task.WaitAsync(token);
                return;
            }
        }, new PartitionedProcessingOptions
        {
            BackpressureMode = mode,
            MaxBufferedRecordsPerPartition = 1,
            CommitPolicy = PartitionCommitPolicy.UserManaged,
            StopPolicy = PartitionStopPolicy.Cancel
        }, timeout.Token).AsTask();

        try
        {
            await Assert.That(async () => await running.WaitAsync(timeout.Token))
                .Throws<InvalidOperationException>().WithMessageContaining("completed before the partition stopped");
            await Assert.That(consumer.CommitCalls).IsEmpty();
        }
        finally
        {
            await timeout.CancelAsync();
            try { await running; }
            catch (InvalidOperationException exception) when (exception.Message.Contains("completed before the partition stopped", StringComparison.Ordinal))
            {
                await Assert.That(running.IsFaulted).IsTrue();
            }
        }
    }

    [Test]
    [Arguments(PartitionBackpressureMode.AwaitCapacity, false)]
    [Arguments(PartitionBackpressureMode.PauseResume, false)]
    [Arguments(PartitionBackpressureMode.AwaitCapacity, true)]
    [Arguments(PartitionBackpressureMode.PauseResume, true)]
    public async Task FullQueue_CancellationOrFailureStopsRuntime(
        PartitionBackpressureMode mode, bool failHandler)
    {
        var consumer = new FullBatchConsumer();
        consumer.SetAssignment(new TopicPartition("backpressure", 0));
        var trigger = NewSignal();
        var failure = new InvalidOperationException("backpressure handler failure");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var running = consumer.RunPartitionedAsync(async (context, token) =>
        {
            await foreach (var _ in context.Messages.WithCancellation(token))
            {
                await trigger.Task.WaitAsync(token);
                throw failure;
            }
        }, new PartitionedProcessingOptions
        {
            BackpressureMode = mode,
            MaxBufferedRecordsPerPartition = 1,
            CommitPolicy = PartitionCommitPolicy.UserManaged,
            StopPolicy = PartitionStopPolicy.Cancel
        }, timeout.Token).AsTask();

        try
        {
            await consumer.ThirdRecordRead.Task.WaitAsync(timeout.Token);
            if (failHandler)
            {
                trigger.TrySetResult();
                await Assert.That(async () => await running.WaitAsync(timeout.Token))
                    .Throws<InvalidOperationException>().WithMessage(failure.Message);
            }
            else
            {
                await timeout.CancelAsync();
                await Assert.That(async () => await running).Throws<OperationCanceledException>();
            }
        }
        finally
        {
            await timeout.CancelAsync();
            try { await running; }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                await Assert.That(running.IsCanceled).IsTrue();
            }
            catch (InvalidOperationException exception) when (ReferenceEquals(exception, failure))
            {
                await Assert.That(failHandler).IsTrue();
            }
        }
    }

    [Test]
    [Arguments(PartitionBackpressureMode.AwaitCapacity)]
    [Arguments(PartitionBackpressureMode.PauseResume)]
    public async Task FullQueue_ReassignmentWaitsForOldDrainAndDropsOldBatch(PartitionBackpressureMode mode)
    {
        var partition = new TopicPartition("backpressure", 0);
        var consumer = new FullBatchConsumer();
        consumer.SetAssignment(partition);
        var releaseFirst = NewSignal();
        var oldStopped = NewSignal();
        var replacementStarted = NewSignal();
        var replacementRecords = new System.Collections.Concurrent.ConcurrentQueue<long>();
        var generation = 0;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var running = consumer.RunPartitionedAsync(async (context, token) =>
        {
            var firstGeneration = Interlocked.Increment(ref generation) == 1;
            if (!firstGeneration)
            {
                await Assert.That(oldStopped.Task.IsCompleted).IsTrue();
                replacementStarted.TrySetResult();
            }
            try
            {
                await foreach (var record in context.Messages.WithCancellation(token))
                {
                    if (firstGeneration)
                    {
                        if (record.Offset == 0)
                            await releaseFirst.Task.WaitAsync(token);
                        context.MarkProcessed(record);
                        await context.CommitProcessedAsync(token);
                    }
                    else
                    {
                        replacementRecords.Enqueue(record.Offset);
                    }
                }
            }
            finally
            {
                if (firstGeneration)
                    oldStopped.TrySetResult();
            }
        }, new PartitionedProcessingOptions
        {
            BackpressureMode = mode,
            MaxBufferedRecordsPerPartition = 1,
            CommitPolicy = PartitionCommitPolicy.UserManaged,
            StopPolicy = PartitionStopPolicy.Drain,
            StopTimeout = TimeSpan.FromSeconds(1)
        }, timeout.Token).AsTask();

        try
        {
            await consumer.ThirdRecordRead.Task.WaitAsync(timeout.Token);
            consumer.RevokeFromCoordinator(partition);
            consumer.AssignFromCoordinator(partition);
            await Assert.That(replacementStarted.Task.IsCompleted).IsFalse();
            releaseFirst.TrySetResult();
            await replacementStarted.Task.WaitAsync(timeout.Token);
            await consumer.BatchAdvanced.Task.WaitAsync(timeout.Token);
            await Assert.That(replacementRecords).IsEmpty();
            await Assert.That(consumer.CommitCalls.Count).IsEqualTo(2);
            await Assert.That(consumer.CommitCalls[1][0].Offset).IsEqualTo(2);
        }
        finally
        {
            releaseFirst.TrySetResult();
            await StopAsync(timeout, running);
        }
    }

    [Test]
    [Arguments(PartitionBackpressureMode.AwaitCapacity)]
    [Arguments(PartitionBackpressureMode.PauseResume)]
    public async Task FullQueue_ShutdownBeforeCapacityWait_DrainsQueuedHandlerCommits(PartitionBackpressureMode mode)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var stop = new CancellationTokenSource();
        using var commitQueued = new ManualResetEventSlim();
        var releaseFirst = NewSignal();
        var consumer = new FullBatchConsumer
        {
            BeforeThirdRecordReturns = () =>
            {
                // Hold the routing thread after decoding the rejected record, before
                // its capacity wait. The handler queues its commit after shutdown.
                stop.Cancel();
                releaseFirst.TrySetResult();
                commitQueued.Wait(timeout.Token);
            }
        };
        consumer.SetAssignment(new TopicPartition("backpressure", 0));
        var running = consumer.RunPartitionedAsync(async (context, token) =>
        {
            await foreach (var record in context.Messages.WithCancellation(token))
            {
                if (record.Offset == 0)
                    await releaseFirst.Task.WaitAsync(token);
                context.MarkProcessed(record);
                var commit = context.CommitProcessedAsync(token);
                commitQueued.Set();
                await commit;
            }
        }, new PartitionedProcessingOptions
        {
            BackpressureMode = mode,
            MaxBufferedRecordsPerPartition = 1,
            CommitPolicy = PartitionCommitPolicy.UserManaged,
            StopPolicy = PartitionStopPolicy.Drain,
            StopTimeout = TimeSpan.FromSeconds(1)
        }, stop.Token).AsTask();

        try
        {
            await Assert.That(async () => await running.WaitAsync(timeout.Token))
                .Throws<OperationCanceledException>();
            await Assert.That(consumer.CommitCalls.Count).IsEqualTo(2);
            await Assert.That(consumer.CommitCalls[0][0].Offset).IsEqualTo(1);
            await Assert.That(consumer.CommitCalls[1][0].Offset).IsEqualTo(2);
        }
        finally
        {
            releaseFirst.TrySetResult();
            await StopAsync(stop, running);
        }
    }

    [Test]
    [Arguments(PartitionBackpressureMode.AwaitCapacity, PartitionStopPolicy.Drain, false)]
    [Arguments(PartitionBackpressureMode.PauseResume, PartitionStopPolicy.Drain, false)]
    [Arguments(PartitionBackpressureMode.AwaitCapacity, PartitionStopPolicy.Drain, true)]
    [Arguments(PartitionBackpressureMode.PauseResume, PartitionStopPolicy.Drain, true)]
    [Arguments(PartitionBackpressureMode.AwaitCapacity, PartitionStopPolicy.Cancel, false)]
    [Arguments(PartitionBackpressureMode.PauseResume, PartitionStopPolicy.Cancel, false)]
    [Arguments(PartitionBackpressureMode.AwaitCapacity, PartitionStopPolicy.Drain, false, true)]
    [Arguments(PartitionBackpressureMode.PauseResume, PartitionStopPolicy.Drain, false, true)]
    [Arguments(PartitionBackpressureMode.AwaitCapacity, PartitionStopPolicy.Drain, false, false, true)]
    [Arguments(PartitionBackpressureMode.PauseResume, PartitionStopPolicy.Drain, false, false, true)]
    public async Task FullQueue_ShutdownDuringCommit_RespectsDrainAndHandlerCancellation(
        PartitionBackpressureMode mode, PartitionStopPolicy stopPolicy, bool cancelHandler,
        bool stallCommit = false, bool queueAssignment = false)
    {
        var consumer = new FullBatchConsumer { CommitStarted = NewSignal(), ReleaseCommit = NewSignal() };
        consumer.SetAssignment(new TopicPartition("backpressure", 0));
        var releaseFirst = NewSignal();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var stop = new CancellationTokenSource();
        using var handlerCancellation = new CancellationTokenSource();
        var assignedProcessorStarts = 0;
        var running = consumer.RunPartitionedAsync(async (context, token) =>
        {
            if (context.TopicPartition.Topic == "assigned-during-stop")
                Interlocked.Increment(ref assignedProcessorStarts);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, handlerCancellation.Token);
            await foreach (var record in context.Messages.WithCancellation(token))
            {
                if (record.Offset == 0)
                    await releaseFirst.Task.WaitAsync(token);
                context.MarkProcessed(record);
                await context.CommitProcessedAsync(linked.Token);
            }
        }, new PartitionedProcessingOptions
        {
            BackpressureMode = mode,
            MaxBufferedRecordsPerPartition = 1,
            CommitPolicy = PartitionCommitPolicy.UserManaged,
            StopPolicy = stopPolicy,
            StopTimeout = TimeSpan.FromSeconds(1)
        }, stop.Token).AsTask();

        try
        {
            await consumer.ThirdRecordRead.Task.WaitAsync(timeout.Token);
            releaseFirst.TrySetResult();
            await consumer.CommitStarted.Task.WaitAsync(timeout.Token);
            if (queueAssignment)
            {
                // Queue the control command behind the suspended commit, then
                // cancel input before allowing that cooperative commit to finish.
                consumer.AssignFromCoordinator(new TopicPartition("backpressure", 0),
                    new TopicPartition("assigned-during-stop", 0));
            }
            await stop.CancelAsync();
            if (cancelHandler)
                await handlerCancellation.CancelAsync();
            if (!stallCommit)
                consumer.ReleaseCommit.TrySetResult();

            await Assert.That(async () => await running.WaitAsync(timeout.Token))
                .Throws<OperationCanceledException>();
            await Assert.That(running.IsCanceled).IsTrue();
            await Assert.That(assignedProcessorStarts).IsEqualTo(0);
            var shouldDrain = stopPolicy == PartitionStopPolicy.Drain && !cancelHandler && !stallCommit;
            await Assert.That(consumer.CommitCalls.Count).IsEqualTo(shouldDrain ? 2 : 0);
            if (shouldDrain)
            {
                await Assert.That(consumer.CommitCalls[0][0].Offset).IsEqualTo(1);
                await Assert.That(consumer.CommitCalls[1][0].Offset).IsEqualTo(2);
            }
        }
        finally
        {
            releaseFirst.TrySetResult();
            consumer.ReleaseCommit.TrySetResult();
            await StopAsync(stop, running);
        }
    }

    [Test]
    [Arguments(PartitionBackpressureMode.AwaitCapacity)]
    [Arguments(PartitionBackpressureMode.PauseResume)]
    public async Task ShutdownDuringCommit_ProcessorFailureCancelsDrainGrace(PartitionBackpressureMode mode)
    {
        var consumer = new FullBatchConsumer { CommitStarted = NewSignal(), ReleaseCommit = NewSignal() };
        consumer.SetAssignment(new TopicPartition("backpressure", 0), new TopicPartition("failing", 0));
        var releaseFirst = NewSignal();
        var failOther = NewSignal();
        var failure = new InvalidOperationException("failure during commit grace");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var stop = new CancellationTokenSource();
        var running = consumer.RunPartitionedAsync(async (context, token) =>
        {
            if (context.TopicPartition.Topic == "failing")
            {
                await failOther.Task.WaitAsync(token);
                throw failure;
            }
            await foreach (var record in context.Messages.WithCancellation(token))
            {
                await releaseFirst.Task.WaitAsync(token);
                context.MarkProcessed(record);
                await context.CommitProcessedAsync(token);
            }
        }, new PartitionedProcessingOptions
        {
            BackpressureMode = mode,
            MaxBufferedRecordsPerPartition = 1,
            CommitPolicy = PartitionCommitPolicy.UserManaged,
            StopPolicy = PartitionStopPolicy.Drain,
            StopTimeout = TimeSpan.FromSeconds(30)
        }, stop.Token).AsTask();

        try
        {
            await consumer.ThirdRecordRead.Task.WaitAsync(timeout.Token);
            releaseFirst.TrySetResult();
            await consumer.CommitStarted.Task.WaitAsync(timeout.Token);
            await stop.CancelAsync();
            failOther.TrySetResult();
            await Assert.That(async () => await running.WaitAsync(timeout.Token))
                .Throws<InvalidOperationException>().WithMessage(failure.Message);
            await Assert.That(consumer.CommitCalls).IsEmpty();
        }
        finally
        {
            releaseFirst.TrySetResult();
            failOther.TrySetResult();
            consumer.ReleaseCommit.TrySetResult();
            await stop.CancelAsync();
            try { await running; }
            catch (InvalidOperationException exception) when (ReferenceEquals(exception, failure))
            {
                await Assert.That(running.IsFaulted).IsTrue();
            }
        }
    }

    [Test]
    [Arguments(PartitionCommitPolicy.UserManaged)]
    [Arguments(PartitionCommitPolicy.CommitCompletedOnRevoke)]
    public async Task ShutdownDeadline_CancelsEveryStalledLaneWithoutRenewingGrace(PartitionCommitPolicy commitPolicy)
    {
        const int laneCount = 3;
        var consumer = new PartitionedConsumerRuntimeTests.TestConsumer { ReleaseCommit = NewSignal() };
        consumer.SetAssignment(Enumerable.Range(0, laneCount)
            .Select(partition => new TopicPartition("deadline", partition)).ToArray());
        consumer.Enqueue(Enumerable.Range(0, laneCount)
            .Select(partition => new ConsumeResult<string, string>(
                topic: "deadline", partition: partition, offset: 0,
                keyData: default, isKeyNull: true, valueData: default, isValueNull: true,
                headers: null, timestampMs: 0, timestampType: TimestampType.NotAvailable,
                leaderEpoch: null, keyDeserializer: null, valueDeserializer: null)).ToArray());
        var started = NewSignal();
        var cancelled = NewSignal();
        var finished = NewSignal();
        var release = NewSignal();
        var startedCount = 0;
        var cancelledCount = 0;
        var finishedCount = 0;
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var stop = new CancellationTokenSource();
        var runtime = new PartitionedConsumerRuntime<string, string>(consumer, async (context, token) =>
        {
            using var registration = token.Register(() =>
            {
                if (Interlocked.Increment(ref cancelledCount) == laneCount)
                    cancelled.TrySetResult();
            });
            try
            {
                await foreach (var record in context.Messages.WithCancellation(token))
                {
                    context.MarkProcessed(record);
                    if (Interlocked.Increment(ref startedCount) == laneCount)
                        started.TrySetResult();
                    // Deliberately ignore cancellation until the test releases all handlers.
                    await release.Task;
                    break;
                }
            }
            finally
            {
                if (Interlocked.Increment(ref finishedCount) == laneCount)
                    finished.TrySetResult();
            }
        }, new PartitionedProcessingOptions
        {
            CommitPolicy = commitPolicy,
            StopPolicy = PartitionStopPolicy.Drain,
            StopTimeout = TimeSpan.FromSeconds(30)
        }, logger: null);
        var running = runtime.RunAsync(stop.Token).AsTask();
        try
        {
            await started.Task.WaitAsync(watchdog.Token);
            await stop.CancelAsync();
            // Trigger the same source as the deadline timer without relying on elapsed-time margins.
            var deadline = (CancellationTokenSource)typeof(PartitionedConsumerRuntime<string, string>)
                .GetField("_shutdownCancellation", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(runtime)!;
            await deadline.CancelAsync();
            await cancelled.Task.WaitAsync(watchdog.Token);
            await Assert.That(async () => await running.WaitAsync(watchdog.Token)).Throws<OperationCanceledException>();
            await Assert.That(consumer.CommitCalls).IsEmpty();
        }
        finally
        {
            release.TrySetResult();
            consumer.ReleaseCommit.TrySetResult();
            await stop.CancelAsync();
            try { await running; }
            catch (Exception exception) when (exception is TimeoutException or OperationCanceledException) { }
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private static async Task StopAsync(CancellationTokenSource timeout, Task running)
    {
        await timeout.CancelAsync();
        try { await running; }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            await Assert.That(running.IsCanceled).IsTrue();
        }
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class FullBatchConsumer : PartitionedConsumerRuntimeTests.TestConsumer
    {
        public Action? BeforeThirdRecordReturns { get; init; }
        public TaskCompletionSource ThirdRecordRead { get; } = NewSignal();
        public TaskCompletionSource BatchAdvanced { get; } = NewSignal();

        public override async IAsyncEnumerable<ConsumeBatch<string, string>> ConsumeBatchAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var pending = PendingFetchData.Create("backpressure", 0,
            [
                new RecordBatch
                {
                    Records =
                    [
                        new Record { OffsetDelta = 0, Value = "0"u8.ToArray() },
                        new Record { OffsetDelta = 1, Value = "1"u8.ToArray() },
                        new Record { OffsetDelta = 2, Value = "2"u8.ToArray() }
                    ]
                }
            ]);
            yield return new ConsumeBatch<string, string>(pending, Serializers.String,
                new ThirdRecordDeserializer(ThirdRecordRead, BeforeThirdRecordReturns));
            BatchAdvanced.TrySetResult();
            await NewSignal().Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class ThirdRecordDeserializer(TaskCompletionSource thirdRecordRead, Action? beforeReturn) : IDeserializer<string>
    {
        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            if (data.Span.SequenceEqual("2"u8))
            {
                thirdRecordRead.TrySetResult();
                beforeReturn?.Invoke();
            }
            return Serializers.String.Deserialize(data, context);
        }
    }
}
