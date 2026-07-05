using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;
using Dekaf.Telemetry;

namespace Dekaf.Tests.Unit.Consumer;

[NotInParallel("PartitionedConsumerRuntime")]
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
        IConsumerOffsets
    {
        private readonly object _gate = new();
        private readonly Queue<ConsumeResult<string, string>> _records = [];
        private readonly HashSet<TopicPartition> _assignment = [];
        private readonly HashSet<TopicPartition> _paused = [];
        private readonly SemaphoreSlim _changed = new(0);

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

        IReadOnlySet<TopicPartition> IConsumerPartitions.Assignment => Assignment;

        IReadOnlySet<TopicPartition> IConsumerPartitions.Paused => Paused;

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
            cancellationToken.ThrowIfCancellationRequested();
            await Task.CompletedTask.ConfigureAwait(false);
            throw new NotSupportedException();
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
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

        public ValueTask CommitAsync(
            IEnumerable<TopicPartitionOffset> offsets,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(offsets);
            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
                CommitCalls.Add(offsets.ToArray());

            return ValueTask.CompletedTask;
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
    }
}
