using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Dekaf.Telemetry;
using Microsoft.Extensions.Logging;

namespace Dekaf.Tests.Unit.Consumer;

// Full isolation: these tests assert timing-sensitive partition runtime scheduling.
[NotInParallel]
public sealed class PartitionedConsumerRuntimeTests
{
    [Test]
    public async Task RunPartitionedAsync_RoutesAssignedPartitionsInOrderAndConcurrently()
    {
        var firstPartition = new TopicPartition("topic-a", 0);
        var secondPartition = new TopicPartition("topic-a", 1);
        var consumer = new TestConsumer();
        consumer.SetAssignment(firstPartition, secondPartition);

        var processed = new ConcurrentDictionary<TopicPartition, List<long>>();
        var firstPartitionStarted = NewCompletionSource();
        var releaseFirstPartition = NewCompletionSource();
        var secondPartitionProcessed = NewCompletionSource();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            async (context, cancellationToken) =>
            {
                await foreach (var message in context.Messages.WithCancellation(cancellationToken))
                {
                    if (context.TopicPartition == firstPartition && message.Offset == 0)
                    {
                        firstPartitionStarted.SetResult();
                        await releaseFirstPartition.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                    }

                    var offsets = processed.GetOrAdd(context.TopicPartition, static _ => []);
                    lock (offsets)
                        offsets.Add(message.Offset);

                    context.MarkProcessed(message);

                    if (context.TopicPartition == secondPartition)
                        secondPartitionProcessed.SetResult();
                }
            },
            new PartitionedProcessingOptions { MaxBufferedRecordsPerPartition = 4 },
            cts.Token).AsTask();

        consumer.Enqueue(
            CreateResult(firstPartition, 0),
            CreateResult(firstPartition, 1),
            CreateResult(secondPartition, 0));

        await firstPartitionStarted.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await secondPartitionProcessed.Task.WaitAsync(cts.Token).ConfigureAwait(false);

        releaseFirstPartition.SetResult();

        await Assert.That(() => Snapshot(processed, firstPartition))
            .Eventually(offsets => offsets.IsEquivalentTo(new long[] { 0, 1 }), TimeSpan.FromSeconds(5));

        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);
    }

    [Test]
    public async Task RunPartitionedAsync_RoutesViaConsumeBatchAsync()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);
        var processed = new ConcurrentBag<long>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            async (context, cancellationToken) =>
            {
                await foreach (var message in context.Messages.WithCancellation(cancellationToken))
                {
                    processed.Add(message.Offset);
                    context.MarkProcessed(message);
                }
            },
            new PartitionedProcessingOptions { MaxBufferedRecordsPerPartition = 8 },
            cts.Token).AsTask();

        consumer.Enqueue(
            CreateResult(partition, 0),
            CreateResult(partition, 1),
            CreateResult(partition, 2));

        await Assert.That(() => processed.Count)
            .Eventually(count => count.IsEqualTo(3), TimeSpan.FromSeconds(5));

        await Assert.That(consumer.ConsumeOneCalls).IsEqualTo(0);
        await Assert.That(consumer.ConsumeBatchCalls).IsGreaterThan(0);

        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);
    }

    [Test]
    public async Task RunPartitionedAsync_PauseResumeWhenLaneIsFull()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);

        var processorStarted = NewCompletionSource();
        var allowRead = NewCompletionSource();
        var processed = new ConcurrentBag<long>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            async (context, cancellationToken) =>
            {
                processorStarted.SetResult();
                await allowRead.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

                await foreach (var message in context.Messages.WithCancellation(cancellationToken))
                {
                    processed.Add(message.Offset);
                    context.MarkProcessed(message);
                }
            },
            new PartitionedProcessingOptions { MaxBufferedRecordsPerPartition = 1 },
            cts.Token).AsTask();

        await processorStarted.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        consumer.Enqueue(CreateResult(partition, 0), CreateResult(partition, 1));

        await Assert.That(() => consumer.PauseCalls.Count)
            .Eventually(count => count.IsGreaterThanOrEqualTo(1), TimeSpan.FromSeconds(5));

        allowRead.SetResult();

        await Assert.That(() => consumer.ResumeCalls.Count)
            .Eventually(count => count.IsGreaterThanOrEqualTo(1), TimeSpan.FromSeconds(5));
        await Assert.That(() => processed.Count)
            .Eventually(count => count.IsEqualTo(2), TimeSpan.FromSeconds(5));

        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);
    }

    [Test]
    public async Task RunPartitionedAsync_RevokedPartitionStopsRoutingAndCommitsProcessedOnly()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);

        var processorStarted = NewCompletionSource();
        var processedFirst = NewCompletionSource();
        var processed = new ConcurrentBag<long>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            async (context, cancellationToken) =>
            {
                processorStarted.SetResult();
                await foreach (var message in context.Messages.WithCancellation(cancellationToken))
                {
                    processed.Add(message.Offset);
                    context.MarkProcessed(message);
                    processedFirst.TrySetResult();
                }
            },
            new PartitionedProcessingOptions
            {
                MaxBufferedRecordsPerPartition = 4,
                CommitPolicy = PartitionCommitPolicy.CommitCompletedOnRevoke
            },
            cts.Token).AsTask();

        await processorStarted.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        consumer.Enqueue(CreateResult(partition, 0));
        await processedFirst.Task.WaitAsync(cts.Token).ConfigureAwait(false);

        consumer.SetAssignment();
        consumer.Enqueue(CreateResult(partition, 1));

        await Assert.That(() => consumer.CommitCalls.SelectMany(static offsets => offsets).ToArray())
            .Eventually(offsets => offsets.IsEquivalentTo(new[] { new TopicPartitionOffset("topic-a", 0, 1) }), TimeSpan.FromSeconds(5));

        await Task.Delay(250, cts.Token).ConfigureAwait(false);
        await Assert.That(processed.ToArray()).IsEquivalentTo(new long[] { 0 });

        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);
    }

    [Test]
    public async Task RunPartitionedAsync_StopPolicyCancelCancelsActiveWorkers()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);

        var started = NewCompletionSource();
        var cancelled = NewCompletionSource();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            async (_, cancellationToken) =>
            {
                started.SetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    cancelled.SetResult();
                }
            },
            new PartitionedProcessingOptions
            {
                StopPolicy = PartitionStopPolicy.Cancel,
                StopTimeout = TimeSpan.FromSeconds(5)
            },
            cts.Token).AsTask();

        await started.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await cts.CancelAsync().ConfigureAwait(false);

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await runTask.ConfigureAwait(false));
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
    }

    [Test]
    public async Task RunPartitionedAsync_ProcessorFailurePropagates()
    {
        var consumer = new TestConsumer();
        consumer.SetAssignment(new TopicPartition("topic-a", 0));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await Assert.That(async () => await consumer.RunPartitionedAsync<string, string>(
                (_, _) => throw new InvalidOperationException("worker failed"),
                cancellationToken: cts.Token))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task RunPartitionedAsync_LostPartitionCancelsWithoutCommit()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);

        var processorStarted = NewCompletionSource();
        var processed = NewCompletionSource();
        var stopped = NewCompletionSource();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            async (context, cancellationToken) =>
            {
                processorStarted.SetResult();
                try
                {
                    await foreach (var message in context.Messages.WithCancellation(cancellationToken))
                    {
                        context.MarkProcessed(message);
                        processed.SetResult();
                    }
                }
                finally
                {
                    stopped.SetResult();
                }
            },
            new PartitionedProcessingOptions
            {
                CommitPolicy = PartitionCommitPolicy.CommitCompletedOnRevoke,
                StopPolicy = PartitionStopPolicy.Drain
            },
            cts.Token).AsTask();

        await processorStarted.Task.WaitAsync(cts.Token).ConfigureAwait(false);

        consumer.Enqueue(CreateResult(partition, 0));
        await processed.Task.WaitAsync(cts.Token).ConfigureAwait(false);

        consumer.LoseFromCoordinator(partition);

        await stopped.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await Assert.That(consumer.CommitCalls).IsEmpty();

        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);
    }

    [Test]
    public async Task RunPartitionedAsync_RapidReassignRestartsPartitionLane()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);

        var starts = 0;
        var secondLaneStarted = NewCompletionSource();
        var processedAfterReassign = NewCompletionSource();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            async (context, cancellationToken) =>
            {
                if (Interlocked.Increment(ref starts) == 2)
                    secondLaneStarted.SetResult();

                await foreach (var message in context.Messages.WithCancellation(cancellationToken))
                {
                    context.MarkProcessed(message);
                    processedAfterReassign.SetResult();
                }
            },
            new PartitionedProcessingOptions
            {
                CommitPolicy = PartitionCommitPolicy.UserManaged,
                StopPolicy = PartitionStopPolicy.Drain
            },
            cts.Token).AsTask();

        await Assert.That(() => Volatile.Read(ref starts))
            .Eventually(count => count.IsEqualTo(1), TimeSpan.FromSeconds(5));

        consumer.RevokeFromCoordinator(partition);
        consumer.AssignFromCoordinator(partition);

        await secondLaneStarted.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        consumer.Enqueue(CreateResult(partition, 1));
        await processedAfterReassign.Task.WaitAsync(cts.Token).ConfigureAwait(false);

        await Assert.That(Volatile.Read(ref starts)).IsEqualTo(2);
        await Assert.That(consumer.CommitCalls).IsEmpty();

        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);
    }

    [Test]
    public async Task RunPartitionedAsync_StopPartitionPausesUntilPartitionLeavesAssignment()
    {
        var partition = new TopicPartition("topic-a", 0);
        var loggerFactory = new CapturingLoggerFactory();
        var consumer = new TestConsumer { LoggerFactory = loggerFactory };
        consumer.SetAssignment(partition);

        var attempts = 0;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync<string, string>(
            (_, _) =>
            {
                Interlocked.Increment(ref attempts);
                throw new InvalidOperationException("partition failed");
            },
            new PartitionedProcessingOptions
            {
                ErrorPolicy = PartitionWorkerErrorPolicy.StopPartition
            },
            cts.Token).AsTask();

        await Assert.That(() => Volatile.Read(ref attempts))
            .Eventually(count => count.IsEqualTo(1), TimeSpan.FromSeconds(5));
        await Assert.That(() => consumer.Paused.Contains(partition))
            .Eventually(paused => paused.IsTrue(), TimeSpan.FromSeconds(5));
        await Assert.That(() => loggerFactory.Entries.Any(
                static entry => entry.LogLevel == LogLevel.Warning
                    && entry.Exception is InvalidOperationException
                    && entry.Message.Contains("pausing partition", StringComparison.Ordinal)))
            .Eventually(logged => logged.IsTrue(), TimeSpan.FromSeconds(5));

        consumer.RevokeFromCoordinator(partition);
        consumer.AssignFromCoordinator(partition);

        await Assert.That(() => Volatile.Read(ref attempts))
            .Eventually(count => count.IsEqualTo(2), TimeSpan.FromSeconds(5));

        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);
    }

    [Test]
    public async Task RunPartitionedAsync_IgnoreRestartsPartitionLane()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);

        var attempts = 0;
        var processed = NewCompletionSource();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            async (context, cancellationToken) =>
            {
                if (Interlocked.Increment(ref attempts) == 1)
                    throw new InvalidOperationException("ignored failure");

                await foreach (var message in context.Messages.WithCancellation(cancellationToken))
                {
                    context.MarkProcessed(message);
                    processed.SetResult();
                }
            },
            new PartitionedProcessingOptions
            {
                ErrorPolicy = PartitionWorkerErrorPolicy.Ignore
            },
            cts.Token).AsTask();

        await Assert.That(() => Volatile.Read(ref attempts))
            .Eventually(count => count.IsEqualTo(2), TimeSpan.FromSeconds(5));

        consumer.Enqueue(CreateResult(partition, 0));

        await processed.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await Assert.That(consumer.PauseCalls).IsEmpty();

        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);
    }

    [Test]
    public async Task RunPartitionedAsync_IgnoreBacksOffAndLogsPersistentFailures()
    {
        var partition = new TopicPartition("topic-a", 0);
        var loggerFactory = new CapturingLoggerFactory();
        var consumer = new TestConsumer { LoggerFactory = loggerFactory };
        consumer.SetAssignment(partition);

        var attempts = 0;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync<string, string>(
            (_, _) =>
            {
                Interlocked.Increment(ref attempts);
                throw new InvalidOperationException("still failing");
            },
            new PartitionedProcessingOptions
            {
                ErrorPolicy = PartitionWorkerErrorPolicy.Ignore,
                IgnoreRestartBackoff = TimeSpan.FromSeconds(30),
                IgnoreRestartBackoffMax = TimeSpan.FromSeconds(30)
            },
            cts.Token).AsTask();

        await Assert.That(() => Volatile.Read(ref attempts))
            .Eventually(count => count.IsEqualTo(1), TimeSpan.FromSeconds(5));
        await Assert.That(() => loggerFactory.Entries.Any(
                static entry => entry.LogLevel == LogLevel.Warning
                    && entry.Exception is InvalidOperationException
                    && entry.Message.Contains("restarting after", StringComparison.Ordinal)
                    && entry.Message.Contains("attempt 1", StringComparison.Ordinal)))
            .Eventually(logged => logged.IsTrue(), TimeSpan.FromSeconds(5));

        await Task.Delay(100, cts.Token).ConfigureAwait(false);
        await Assert.That(Volatile.Read(ref attempts)).IsEqualTo(1);

        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);
    }

    [Test]
    public async Task RunPartitionedAsync_IgnoreBackoffDoesNotStallHealthyPartitions()
    {
        var failingPartition = new TopicPartition("topic-a", 0);
        var healthyPartition = new TopicPartition("topic-a", 1);
        var loggerFactory = new CapturingLoggerFactory();
        var consumer = new TestConsumer { LoggerFactory = loggerFactory };
        consumer.SetAssignment(failingPartition, healthyPartition);

        var failingAttempts = 0;
        var healthyProcessed = NewCompletionSource();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            async (context, cancellationToken) =>
            {
                if (context.TopicPartition == failingPartition)
                {
                    Interlocked.Increment(ref failingAttempts);
                    throw new InvalidOperationException("still failing");
                }

                await foreach (var message in context.Messages.WithCancellation(cancellationToken))
                {
                    context.MarkProcessed(message);
                    healthyProcessed.TrySetResult();
                }
            },
            new PartitionedProcessingOptions
            {
                ErrorPolicy = PartitionWorkerErrorPolicy.Ignore,
                IgnoreRestartBackoff = TimeSpan.FromSeconds(30),
                IgnoreRestartBackoffMax = TimeSpan.FromSeconds(30)
            },
            cts.Token).AsTask();

        await Assert.That(() => Volatile.Read(ref failingAttempts))
            .Eventually(count => count.IsEqualTo(1), TimeSpan.FromSeconds(5));
        await Assert.That(() => loggerFactory.Entries.Any(
                static entry => entry.LogLevel == LogLevel.Warning
                    && entry.Message.Contains("restarting after", StringComparison.Ordinal)))
            .Eventually(logged => logged.IsTrue(), TimeSpan.FromSeconds(5));

        consumer.Enqueue(CreateResult(healthyPartition, 0));

        await healthyProcessed.Task.WaitAsync(TimeSpan.FromSeconds(2), cts.Token).ConfigureAwait(false);
        await Assert.That(Volatile.Read(ref failingAttempts)).IsEqualTo(1);

        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);
    }

    [Test]
    public async Task RunPartitionedAsync_RejectsZeroIgnoreRestartBackoff()
    {
        var consumer = new TestConsumer();

        async Task Act()
        {
            await consumer.RunPartitionedAsync<string, string>(
                static (_, _) => ValueTask.CompletedTask,
                new PartitionedProcessingOptions
                {
                    ErrorPolicy = PartitionWorkerErrorPolicy.Ignore,
                    IgnoreRestartBackoff = TimeSpan.Zero
                });
        }

        await Assert.That(Act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task RunPartitionedAsync_StopTimeoutOnRevokePropagates()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);

        var started = NewCompletionSource();
        var release = NewCompletionSource();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            async (_, _) =>
            {
                started.SetResult();
                await release.Task.ConfigureAwait(false);
            },
            new PartitionedProcessingOptions
            {
                StopTimeout = TimeSpan.FromMilliseconds(50)
            },
            cts.Token).AsTask();

        await started.Task.WaitAsync(cts.Token).ConfigureAwait(false);

        consumer.SetAssignment();

        await Assert.ThrowsAsync<TimeoutException>(async () => await runTask.ConfigureAwait(false));
        release.SetResult();
    }

    [Test]
    public async Task RunPartitionedAsync_ShutdownCommitUsesStopTimeout()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer
        {
            CommitStarted = NewCompletionSource(),
            ReleaseCommit = NewCompletionSource()
        };
        consumer.SetAssignment(partition);

        var processorStarted = NewCompletionSource();
        var processed = NewCompletionSource();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            async (context, cancellationToken) =>
            {
                processorStarted.SetResult();
                await foreach (var message in context.Messages.WithCancellation(cancellationToken))
                {
                    context.MarkProcessed(message);
                    processed.SetResult();
                }
            },
            new PartitionedProcessingOptions
            {
                CommitPolicy = PartitionCommitPolicy.CommitCompletedOnRevoke,
                StopTimeout = TimeSpan.FromMilliseconds(50)
            },
            cts.Token).AsTask();

        await processorStarted.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        consumer.Enqueue(CreateResult(partition, 0));
        await processed.Task.WaitAsync(cts.Token).ConfigureAwait(false);

        await cts.CancelAsync().ConfigureAwait(false);

        await consumer.CommitStarted!.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await runTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false));

        consumer.ReleaseCommit!.SetResult();
    }

    [Test]
    public async Task CommitProcessedAsync_CommitsCurrentPartitionOffset()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);

        var processorStarted = NewCompletionSource();
        var committed = NewCompletionSource();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            async (context, cancellationToken) =>
            {
                processorStarted.SetResult();
                await foreach (var message in context.Messages.WithCancellation(cancellationToken))
                {
                    context.MarkProcessed(message);
                    await context.CommitProcessedAsync(cancellationToken).ConfigureAwait(false);
                    committed.SetResult();
                }
            },
            new PartitionedProcessingOptions
            {
                CommitPolicy = PartitionCommitPolicy.UserManaged
            },
            cts.Token).AsTask();

        await processorStarted.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        consumer.Enqueue(CreateResult(partition, 4, leaderEpoch: 7));

        await committed.Task.WaitAsync(cts.Token).ConfigureAwait(false);

        await Assert.That(consumer.CommitCalls.Single()).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 0, 5, 7)
        ]);

        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);
    }

    private static async ValueTask StopRuntimeAsync(
        CancellationTokenSource cancellationTokenSource,
        Task runTask)
    {
        await cancellationTokenSource.CancelAsync().ConfigureAwait(false);

        try
        {
            await runTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static TaskCompletionSource NewCompletionSource()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static long[] Snapshot(
        ConcurrentDictionary<TopicPartition, List<long>> processed,
        TopicPartition partition)
    {
        if (!processed.TryGetValue(partition, out var offsets))
            return [];

        lock (offsets)
            return offsets.ToArray();
    }

    private static ConsumeResult<string, string> CreateResult(
        TopicPartition partition,
        long offset,
        int? leaderEpoch = null)
    {
        return new ConsumeResult<string, string>(
            topic: partition.Topic,
            partition: partition.Partition,
            offset: offset,
            keyData: default,
            isKeyNull: true,
            valueData: default,
            isValueNull: true,
            headers: null,
            timestampMs: 0,
            timestampType: TimestampType.NotAvailable,
            leaderEpoch: leaderEpoch,
            keyDeserializer: null,
            valueDeserializer: null);
    }

    private sealed class TestConsumer :
        IKafkaConsumer<string, string>,
        IConsumerPositions,
        IConsumerPartitions,
        IConsumerOffsets,
        IConsumerRebalanceEventSource,
        IConsumerLoggerFactorySource
    {
        private readonly object _gate = new();
        private readonly Queue<ConsumeResult<string, string>> _records = [];
        private readonly HashSet<TopicPartition> _assignment = [];
        private readonly HashSet<TopicPartition> _paused = [];
        private readonly List<IRebalanceListener> _rebalanceListeners = [];
        private readonly SemaphoreSlim _changed = new(0);
        private int _consumeOneCalls;
        private int _consumeBatchCalls;

        public IReadOnlySet<string> Subscription => new HashSet<string>(StringComparer.Ordinal);

        public string? SubscriptionPattern => null;

        public IReadOnlySet<TopicPartition> Assignment
        {
            get
            {
                lock (_gate)
                    return _assignment.ToHashSet();
            }
        }

        public IReadOnlySet<TopicPartition> Paused
        {
            get
            {
                lock (_gate)
                    return _paused.ToHashSet();
            }
        }

        public string? MemberId => "member-a";

        public ConsumerGroupMetadata? ConsumerGroupMetadata => new()
        {
            GroupId = "group-a",
            GenerationId = 1,
            MemberId = "member-a"
        };

        public IConsumerPositions Positions => this;

        public IConsumerPartitions Partitions => this;

        public IConsumerOffsets Offsets => this;

        public List<TopicPartition[]> PauseCalls { get; } = [];

        public List<TopicPartition[]> ResumeCalls { get; } = [];

        public List<TopicPartitionOffset[]> CommitCalls { get; } = [];

        public int ConsumeOneCalls => Volatile.Read(ref _consumeOneCalls);

        public int ConsumeBatchCalls => Volatile.Read(ref _consumeBatchCalls);

        public TaskCompletionSource? CommitStarted { get; init; }

        public TaskCompletionSource? ReleaseCommit { get; init; }

        public ILoggerFactory? LoggerFactory { get; init; }

        IReadOnlySet<TopicPartition> IConsumerPartitions.Assignment => Assignment;

        IReadOnlySet<TopicPartition> IConsumerPartitions.Paused => Paused;

        IDisposable IConsumerRebalanceEventSource.RegisterRuntimeRebalanceListener(IRebalanceListener listener)
        {
            lock (_gate)
                _rebalanceListeners.Add(listener);

            return new RebalanceRegistration(this, listener);
        }

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public void Subscribe(params string[] topics)
            => throw new NotSupportedException();

        public void Subscribe(Func<string, bool> topicFilter)
            => throw new NotSupportedException();

        public void SubscribePattern(string pattern)
            => throw new NotSupportedException();

        public void Unsubscribe()
            => throw new NotSupportedException();

        public async IAsyncEnumerable<ConsumeResult<string, string>> ConsumeAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await ConsumeOneAsync(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                if (result.HasValue)
                    yield return result.Value;
            }
        }

        public async ValueTask<ConsumeResult<string, string>?> ConsumeOneAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _consumeOneCalls);

            var deadline = timeout == Timeout.InfiniteTimeSpan
                ? DateTimeOffset.MaxValue
                : DateTimeOffset.UtcNow.Add(timeout);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (TryDequeueAvailable(out var result))
                    return result;

                if (timeout == TimeSpan.Zero)
                    return null;

                if (timeout == Timeout.InfiniteTimeSpan)
                {
                    await _changed.WaitAsync(cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var remaining = deadline - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero)
                    return null;

                if (!await _changed.WaitAsync(remaining, cancellationToken).ConfigureAwait(false))
                    return null;
            }
        }

        public async IAsyncEnumerable<ConsumeBatch<string, string>> ConsumeBatchAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _consumeBatchCalls);

            while (!cancellationToken.IsCancellationRequested)
            {
                if (!TryDequeueBatch(out var results))
                {
                    await _changed.WaitAsync(cancellationToken).ConfigureAwait(false);
                    continue;
                }

                using var pending = CreatePendingFetchData(results);
                yield return new ConsumeBatch<string, string>(
                    pending,
                    Serializers.String,
                    Serializers.String,
                    IsAssigned);
            }
        }

        public async IAsyncEnumerable<ConsumeRawBatch> ConsumeRawBatchAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.CompletedTask.ConfigureAwait(false);
            throw new NotSupportedException();
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }

        public void RegisterMetricForSubscription(ApplicationTelemetryMetric metric)
        {
        }

        public void UnregisterMetricFromSubscription(string name)
        {
        }

        public ValueTask CommitAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }

        public async ValueTask CommitAsync(
            IEnumerable<TopicPartitionOffset> offsets,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(offsets);
            cancellationToken.ThrowIfCancellationRequested();

            CommitStarted?.TrySetResult();
            if (ReleaseCommit is not null)
                await ReleaseCommit.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

            lock (_gate)
                CommitCalls.Add(offsets.ToArray());
        }

        public void StoreOffset(ConsumeResult<string, string> result)
        {
        }

        public void StoreOffset(TopicPartitionOffset offset)
        {
        }

        public ValueTask CloseAsync(CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;

        public ValueTask<long?> GetCommittedOffsetAsync(
            TopicPartition partition,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<long?>(null);

        public long? GetPosition(TopicPartition partition)
            => null;

        public void Seek(TopicPartitionOffset offset)
        {
        }

        public void SeekToBeginning(params TopicPartition[] partitions)
        {
        }

        public void SeekToEnd(params TopicPartition[] partitions)
        {
        }

        public void Assign(params TopicPartition[] partitions)
            => SetAssignment(partitions);

        public void Unassign()
            => SetAssignment();

        public void IncrementalAssign(IEnumerable<TopicPartitionOffset> partitions)
        {
            ArgumentNullException.ThrowIfNull(partitions);

            lock (_gate)
            {
                foreach (var offset in partitions)
                    _assignment.Add(new TopicPartition(offset.Topic, offset.Partition));
            }

            _changed.Release();
        }

        public void IncrementalUnassign(IEnumerable<TopicPartition> partitions)
        {
            ArgumentNullException.ThrowIfNull(partitions);

            lock (_gate)
            {
                foreach (var partition in partitions)
                    _assignment.Remove(partition);
            }

            _changed.Release();
        }

        public void Pause(params TopicPartition[] partitions)
        {
            ArgumentNullException.ThrowIfNull(partitions);

            lock (_gate)
            {
                PauseCalls.Add(partitions.ToArray());
                foreach (var partition in partitions)
                    _paused.Add(partition);
            }

            _changed.Release();
        }

        public void Resume(params TopicPartition[] partitions)
        {
            ArgumentNullException.ThrowIfNull(partitions);

            lock (_gate)
            {
                ResumeCalls.Add(partitions.ToArray());
                foreach (var partition in partitions)
                    _paused.Remove(partition);
            }

            _changed.Release();
        }

        public ValueTask<IReadOnlyDictionary<TopicPartition, long>> GetOffsetsForTimesAsync(
            IEnumerable<TopicPartitionTimestamp> timestampsToSearch,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyDictionary<TopicPartition, long>>(new Dictionary<TopicPartition, long>());

        public WatermarkOffsets? GetWatermarkOffsets(TopicPartition topicPartition)
            => null;

        public ValueTask<WatermarkOffsets> QueryWatermarkOffsetsAsync(
            TopicPartition topicPartition,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new WatermarkOffsets(0, 0));

        public void SetAssignment(params TopicPartition[] partitions)
        {
            lock (_gate)
            {
                _assignment.Clear();
                _paused.RemoveWhere(partition => !partitions.Contains(partition));
                foreach (var partition in partitions)
                    _assignment.Add(partition);
            }

            _changed.Release();
        }

        public void AssignFromCoordinator(params TopicPartition[] partitions)
        {
            SetAssignment(partitions);
            FireRebalanceListener(listener => listener.OnPartitionsAssignedAsync(partitions, CancellationToken.None));
        }

        public void RevokeFromCoordinator(params TopicPartition[] partitions)
        {
            RemoveAssignment(partitions);
            FireRebalanceListener(listener => listener.OnPartitionsRevokedAsync(partitions, CancellationToken.None));
        }

        public void LoseFromCoordinator(params TopicPartition[] partitions)
        {
            RemoveAssignment(partitions);
            FireRebalanceListener(listener => listener.OnPartitionsLostAsync(partitions, CancellationToken.None));
        }

        public void Enqueue(params ConsumeResult<string, string>[] results)
        {
            lock (_gate)
            {
                foreach (var result in results)
                    _records.Enqueue(result);
            }

            _changed.Release(results.Length);
        }

        private bool TryDequeueAvailable(out ConsumeResult<string, string> result)
        {
            lock (_gate)
            {
                var count = _records.Count;
                for (var i = 0; i < count; i++)
                {
                    var candidate = _records.Dequeue();
                    var partition = new TopicPartition(candidate.Topic, candidate.Partition);
                    if (_assignment.Contains(partition) && !_paused.Contains(partition))
                    {
                        result = candidate;
                        return true;
                    }

                    _records.Enqueue(candidate);
                }
            }

            result = default;
            return false;
        }

        private bool TryDequeueBatch(out List<ConsumeResult<string, string>> results)
        {
            lock (_gate)
            {
                var count = _records.Count;
                TopicPartition batchPartition = default;
                var hasBatchPartition = false;
                results = [];

                for (var i = 0; i < count; i++)
                {
                    var candidate = _records.Dequeue();
                    var partition = new TopicPartition(candidate.Topic, candidate.Partition);
                    if (_assignment.Contains(partition)
                        && !_paused.Contains(partition)
                        && (!hasBatchPartition || partition == batchPartition))
                    {
                        if (!hasBatchPartition)
                        {
                            batchPartition = partition;
                            hasBatchPartition = true;
                        }

                        results.Add(candidate);
                        continue;
                    }

                    _records.Enqueue(candidate);
                }

                return results.Count != 0;
            }
        }

        private bool IsAssigned(TopicPartition partition)
        {
            lock (_gate)
                return _assignment.Contains(partition);
        }

        private static PendingFetchData CreatePendingFetchData(IReadOnlyList<ConsumeResult<string, string>> results)
        {
            var first = results[0];
            var baseOffset = first.Offset;
            var baseTimestamp = first.TimestampMs;
            var records = new Record[results.Count];

            for (var i = 0; i < results.Count; i++)
            {
                var result = results[i];
                records[i] = new Record
                {
                    OffsetDelta = checked((int)(result.Offset - baseOffset)),
                    TimestampDelta = result.TimestampMs - baseTimestamp,
                    IsKeyNull = true,
                    IsValueNull = true
                };
            }

            var recordBatch = new RecordBatch
            {
                BaseOffset = baseOffset,
                BaseTimestamp = baseTimestamp,
                MaxTimestamp = results[^1].TimestampMs,
                LastOffsetDelta = records[^1].OffsetDelta,
                PartitionLeaderEpoch = first.LeaderEpoch ?? -1,
                Records = records
            };

            var pending = PendingFetchData.Create(
                first.Topic,
                first.Partition,
                new List<RecordBatch> { recordBatch });
            pending.EagerParseAll();
            return pending;
        }

        private void RemoveAssignment(params TopicPartition[] partitions)
        {
            lock (_gate)
            {
                foreach (var partition in partitions)
                {
                    _assignment.Remove(partition);
                    _paused.Remove(partition);
                }
            }

            _changed.Release();
        }

        private void RemoveRebalanceListener(IRebalanceListener listener)
        {
            lock (_gate)
                _rebalanceListeners.Remove(listener);
        }

        private void FireRebalanceListener(Func<IRebalanceListener, ValueTask> callback)
        {
            IRebalanceListener[] listeners;
            lock (_gate)
                listeners = _rebalanceListeners.ToArray();

            foreach (var listener in listeners)
                callback(listener).AsTask().GetAwaiter().GetResult();
        }

        private sealed class RebalanceRegistration(
            TestConsumer consumer,
            IRebalanceListener listener) : IDisposable
        {
            private int _disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                    consumer.RemoveRebalanceListener(listener);
            }
        }
    }

    private sealed class CapturingLoggerFactory : ILoggerFactory
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Entries);

        public void AddProvider(ILoggerProvider provider) { }

        public void Dispose() { }
    }

    private sealed class CapturingLogger(ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed record LogEntry(LogLevel LogLevel, string Message, Exception? Exception);
}
