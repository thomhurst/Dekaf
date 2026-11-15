using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

public sealed partial class PartitionedConsumerRuntimeTests
{
    [Test]
    public async Task Transactional_PeriodicCommit_SendsCompletedOffsetsToTransaction()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);
        var transactions = new FakeTransactions();
        var observer = new RecordingCommitObserver(consumer);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            (_, message, _) =>
            {
                transactions.Produce(message.Offset);
                return ValueTask.CompletedTask;
            },
            TransactionalOptions(transactions, observer: observer),
            cts.Token).AsTask();

        consumer.Enqueue(CreateResult(partition, 0), CreateResult(partition, 1));
        var committed = await transactions.WaitForCommitAsync(offset: 2, cts.Token).ConfigureAwait(false);
        var completed = await observer.Completed.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        await Assert.That(committed.Offsets).IsEquivalentTo(new[] { new TopicPartitionOffset("topic-a", 0, 2) });
        await Assert.That(committed.Metadata!.GroupId).IsEqualTo("group-a");
        await Assert.That(consumer.CommitCalls).IsEmpty();
        await Assert.That(completed.Reason).IsEqualTo(OffsetCommitReason.Periodic);
        transactions.AssertOutputMatchesOffsets(partition);
    }

    [Test]
    public async Task Transactional_KeyOrderedHandlers_CommitOnlyOutputOfCommittedRecords()
    {
        var partitions = new[] { new TopicPartition("topic-a", 0), new TopicPartition("topic-a", 1) };
        var consumer = new TestConsumer();
        consumer.SetAssignment(partitions);
        var transactions = new FakeTransactions();
        const int recordsPerPartition = 200;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var runTask = consumer.RunPartitionedAsync(
            async (_, message, cancellationToken) =>
            {
                // Out-of-order completion across keys, so the frontier trails completed records.
                if (message.Offset % 3 == 0)
                    await Task.Delay(1, cancellationToken).ConfigureAwait(false);
                transactions.Produce(message.Partition, message.Offset);
            },
            TransactionalOptions(
                transactions,
                TimeSpan.FromMilliseconds(5),
                PartitionedProcessingOrder.Key,
                maxConcurrentHandlersPerPartition: 4),
            cts.Token).AsTask();

        // Two waves, so at least two transactions commit.
        for (var wave = 1; wave <= 2; wave++)
        {
            var end = recordsPerPartition * wave / 2;
            for (var offset = end - (recordsPerPartition / 2); offset < end; offset++)
            {
                foreach (var partition in partitions)
                    consumer.Enqueue(CreateResult(partition, offset, key: $"key-{offset % 7}"));
            }

            foreach (var partition in partitions)
                await transactions.WaitForCommitAsync(partition, end, cts.Token).ConfigureAwait(false);
        }
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        await Assert.That(transactions.CommittedCount).IsGreaterThan(1);
        foreach (var partition in partitions)
            transactions.AssertOutputMatchesOffsets(partition);
    }

    [Test]
    public async Task Transactional_FailedCommit_AbortsAndReprocessesFromCommittedOffset()
    {
        var partition = new TopicPartition("topic-a", 0);
        // Like KafkaConsumer, a batch fetched before the rewind's seek is fetched again.
        var consumer = new TestConsumer { RedeliverUnenumeratedBatches = true };
        consumer.SetAssignment(partition);
        var transactions = new FakeTransactions();
        transactions.CommitFailures.Enqueue(new InvalidOperationException("commit failed"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            (_, message, _) =>
            {
                transactions.Produce(message.Offset);
                return ValueTask.CompletedTask;
            },
            TransactionalOptions(transactions),
            cts.Token).AsTask();

        consumer.Enqueue(CreateResult(partition, 0), CreateResult(partition, 1));
        await transactions.WaitForCommitAsync(offset: 2, cts.Token).ConfigureAwait(false);
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        await Assert.That(transactions.AbortedCount).IsGreaterThanOrEqualTo(1);
        await Assert.That(consumer.SeekCalls).Contains(new TopicPartitionOffset("topic-a", 0, 0));
        transactions.AssertOutputMatchesOffsets(partition);
    }

    [Test]
    public async Task Transactional_HandlerFailure_AbortsTransactionWithoutCommit()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);
        var transactions = new FakeTransactions();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            (_, message, _) =>
            {
                transactions.Produce(message.Offset);
                return message.Offset == 1
                    ? throw new InvalidOperationException("handler failed")
                    : ValueTask.CompletedTask;
            },
            TransactionalOptions(transactions, TimeSpan.FromMinutes(5)),
            cts.Token).AsTask();

        consumer.Enqueue(CreateResult(partition, 0), CreateResult(partition, 1));

        await Assert.That(async () => await runTask.WaitAsync(cts.Token).ConfigureAwait(false))
            .Throws<InvalidOperationException>();
        await Assert.That(transactions.CommittedCount).IsEqualTo(0);
        await Assert.That(transactions.AbortedCount).IsEqualTo(1);
    }

    [Test]
    public async Task Transactional_Revoke_CommitsRevokedOffsetsThroughTransaction()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);
        var transactions = new FakeTransactions();
        var processed = NewCompletionSource();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            (_, message, _) =>
            {
                transactions.Produce(message.Offset);
                processed.TrySetResult();
                return ValueTask.CompletedTask;
            },
            TransactionalOptions(transactions, TimeSpan.FromMinutes(5)),
            cts.Token).AsTask();

        consumer.Enqueue(CreateResult(partition, 0));
        await processed.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await consumer.RevokeFromCoordinatorAsync(partition).ConfigureAwait(false);

        // The revocation callback returns only after the transaction committed.
        await Assert.That(transactions.CommittedCount).IsEqualTo(1);
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        transactions.AssertOutputMatchesOffsets(partition);
        await Assert.That(consumer.CommitCalls).IsEmpty();
    }

    [Test]
    public async Task Transactional_LostPartition_AbortsTransaction()
    {
        var lost = new TopicPartition("topic-a", 0);
        var kept = new TopicPartition("topic-a", 1);
        var consumer = new TestConsumer();
        consumer.SetAssignment(lost, kept);
        var transactions = new FakeTransactions();
        var processedLost = NewCompletionSource();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            (_, message, _) =>
            {
                transactions.Produce(message.Partition, message.Offset);
                if (message.Partition == lost.Partition)
                    processedLost.TrySetResult();
                return ValueTask.CompletedTask;
            },
            TransactionalOptions(transactions, TimeSpan.FromMinutes(5)),
            cts.Token).AsTask();

        consumer.Enqueue(CreateResult(lost, 0));
        await processedLost.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        consumer.LoseFromCoordinator(lost);

        await transactions.WaitForAbortAsync(cts.Token).ConfigureAwait(false);
        consumer.Enqueue(CreateResult(kept, 0));
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        // The lost partition's output was aborted; only the kept partition's commit is visible.
        await Assert.That(transactions.CommittedOutput(lost)).IsEmpty();
        transactions.AssertOutputMatchesOffsets(kept);
    }

    [Test]
    public async Task Transactional_BackwardSeek_CommitsSeekTargetInTransaction()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer { RedeliverUnenumeratedBatches = true };
        consumer.SetAssignment(partition);
        consumer.Enqueue(Enumerable.Range(0, 5).Select(offset => CreateResult(partition, offset)).ToArray());
        var transactions = new FakeTransactions();
        var control = new PartitionedProcessingControl();
        var firstPass = NewCompletionSource();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            (_, message, _) =>
            {
                transactions.Produce(message.Offset);
                if (message.Offset == 4)
                    firstPass.TrySetResult();
                return ValueTask.CompletedTask;
            },
            new PartitionedProcessingOptions
            {
                Control = control,
                CommitPolicy = PartitionCommitPolicy.CommitCompletedInTransaction,
                TransactionalProducer = transactions.Producer,
                CommitInterval = TimeSpan.FromMinutes(5)
            },
            cts.Token).AsTask();

        await firstPass.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await control.SeekAsync(new TopicPartitionOffset(partition.Topic, partition.Partition, 2), cts.Token)
            .ConfigureAwait(false);

        // The seek committed the first pass's output with the seek target as the restart position.
        var committed = await transactions.WaitForCommitAsync(offset: 2, cts.Token).ConfigureAwait(false);
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        await Assert.That(committed.Offsets).IsEquivalentTo(new[] { new TopicPartitionOffset("topic-a", 0, 2) });
        await Assert.That(committed.Produced.Select(output => output.Offset)).IsEquivalentTo(new long[] { 0, 1, 2, 3, 4 });
        await Assert.That(consumer.SeekCalls.Select(offset => offset.Offset)).IsEquivalentTo(new long[] { 2 });
    }

    [Test]
    public async Task Transactional_FailedSeekCommit_FailsSeekAndRewindsSeekedPartition()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer { RedeliverUnenumeratedBatches = true };
        consumer.SetAssignment(partition);
        consumer.Enqueue(Enumerable.Range(0, 5).Select(offset => CreateResult(partition, offset)).ToArray());
        var transactions = new FakeTransactions();
        transactions.CommitFailures.Enqueue(new InvalidOperationException("commit failed"));
        var control = new PartitionedProcessingControl();
        var firstPass = NewCompletionSource();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            (_, message, _) =>
            {
                transactions.Produce(message.Offset);
                if (message.Offset == 4)
                    firstPass.TrySetResult();
                return ValueTask.CompletedTask;
            },
            TransactionalOptions(transactions, TimeSpan.FromMinutes(5), control: control),
            cts.Token).AsTask();

        await firstPass.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await Assert.That(async () => await control.SeekAsync(
                new TopicPartitionOffset(partition.Topic, partition.Partition, 3), cts.Token).ConfigureAwait(false))
            .Throws<InvalidOperationException>().WithMessage("commit failed");

        // The aborted output is produced again from the rewind position, not skipped by the seek.
        var seeks = consumer.SeekCalls.Select(offset => offset.Offset).ToArray();
        var abortedCount = transactions.AbortedCount;
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        await Assert.That(seeks).IsEquivalentTo(new long[] { 0 });
        await Assert.That(abortedCount).IsEqualTo(1);
        transactions.AssertOutputMatchesOffsets(partition);
    }

    [Test]
    public async Task Transactional_BackwardSeekWithoutCompletedRecords_CommitsSeekTarget()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer { RedeliverUnenumeratedBatches = true, CommittedOffset = 5 };
        consumer.SetAssignment(partition);
        var transactions = new FakeTransactions();
        var control = new PartitionedProcessingControl();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            (_, message, _) =>
            {
                transactions.Produce(message.Offset);
                return ValueTask.CompletedTask;
            },
            TransactionalOptions(transactions, TimeSpan.FromMinutes(5), control: control),
            cts.Token).AsTask();

        await control.SeekAsync(new TopicPartitionOffset(partition.Topic, partition.Partition, 2), cts.Token)
            .ConfigureAwait(false);
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        // With no completed record, the seek still moves the group's restart position back.
        await Assert.That(transactions.CommittedCount).IsEqualTo(1);
        var committed = await transactions.WaitForCommitAsync(offset: 2, cts.Token).ConfigureAwait(false);
        await Assert.That(committed.Offsets).IsEquivalentTo(new[] { new TopicPartitionOffset("topic-a", 0, 2) });
        await Assert.That(committed.Produced).IsEmpty();
    }

    [Test]
    public async Task Transactional_RevokeCommitThatDoesNotFinish_AbortsWithinStopTimeout()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);
        var transactions = new FakeTransactions { BlockCommits = true };
        var processed = NewCompletionSource();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            (_, message, _) =>
            {
                transactions.Produce(message.Offset);
                processed.TrySetResult();
                return ValueTask.CompletedTask;
            },
            TransactionalOptions(transactions, TimeSpan.FromMinutes(5), stopTimeout: TimeSpan.FromMilliseconds(200)),
            cts.Token).AsTask();

        consumer.Enqueue(CreateResult(partition, 0));
        await processed.Task.WaitAsync(cts.Token).ConfigureAwait(false);

        // The member holds the revocation callback, so a stuck commit gives up after StopTimeout.
        await consumer.RevokeFromCoordinatorAsync(partition).WaitAsync(TimeSpan.FromSeconds(5), cts.Token)
            .ConfigureAwait(false);
        var committedCount = transactions.CommittedCount;
        var abortedCount = transactions.AbortedCount;
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        await Assert.That(committedCount).IsEqualTo(0);
        await Assert.That(abortedCount).IsEqualTo(1);
    }

    [Test]
    public async Task Transactional_CommitProcessedAsync_IsRejected()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);
        var transactions = new FakeTransactions();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            (context, _, cancellationToken) => context.CommitProcessedAsync(cancellationToken),
            TransactionalOptions(transactions),
            cts.Token).AsTask();

        consumer.Enqueue(CreateResult(partition, 0));

        await Assert.That(async () => await runTask.WaitAsync(cts.Token).ConfigureAwait(false))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Transactional_InvalidOptions_Throw()
    {
        var consumer = new TestConsumer();
        consumer.SetAssignment(new TopicPartition("topic-a", 0));
        var producer = new FakeTransactions().Producer;
        PartitionRecordProcessor<string, string> handler = static (_, _, _) => ValueTask.CompletedTask;

        await Assert.That(async () => await consumer.RunPartitionedAsync(
                handler,
                new PartitionedProcessingOptions { CommitPolicy = PartitionCommitPolicy.CommitCompletedInTransaction }))
            .Throws<InvalidOperationException>();
        await Assert.That(async () => await consumer.RunPartitionedAsync(
                handler,
                new PartitionedProcessingOptions { TransactionalProducer = producer }))
            .Throws<InvalidOperationException>();
        await Assert.That(async () => await consumer.RunPartitionedAsync(
                handler,
                new PartitionedProcessingOptions
                {
                    CommitPolicy = PartitionCommitPolicy.CommitCompletedInTransaction,
                    TransactionalProducer = producer,
                    ErrorPolicy = PartitionWorkerErrorPolicy.StopPartition
                }))
            .Throws<InvalidOperationException>();
        await Assert.That(async () => await consumer.RunPartitionedAsync(
                static (_, _) => ValueTask.CompletedTask,
                new PartitionedProcessingOptions
                {
                    CommitPolicy = PartitionCommitPolicy.CommitCompletedInTransaction,
                    TransactionalProducer = producer
                }))
            .Throws<InvalidOperationException>();

        var groupless = new TestConsumer { HasConsumerGroup = false };
        groupless.SetAssignment(new TopicPartition("topic-a", 0));
        await Assert.That(async () => await groupless.RunPartitionedAsync(
                handler,
                new PartitionedProcessingOptions
                {
                    CommitPolicy = PartitionCommitPolicy.CommitCompletedInTransaction,
                    TransactionalProducer = producer
                }))
            .Throws<InvalidOperationException>();
    }

    private static PartitionedProcessingOptions TransactionalOptions(
        FakeTransactions transactions,
        TimeSpan? commitInterval = null,
        PartitionedProcessingOrder ordering = PartitionedProcessingOrder.Partition,
        int maxConcurrentHandlersPerPartition = 1,
        IOffsetCommitObserver? observer = null,
        PartitionedProcessingControl? control = null,
        TimeSpan? stopTimeout = null)
        => new()
        {
            Control = control,
            StopTimeout = stopTimeout ?? new PartitionedProcessingOptions().StopTimeout,
            CommitPolicy = PartitionCommitPolicy.CommitCompletedInTransaction,
            TransactionalProducer = transactions.Producer,
            CommitInterval = commitInterval ?? TimeSpan.FromMilliseconds(20),
            Ordering = ordering,
            MaxConcurrentHandlersPerPartition = maxConcurrentHandlersPerPartition,
            CommitObserver = observer
        };

    /// <summary>
    /// Records the output handlers produce into each transaction, the offsets each commit carries,
    /// and checks that committed output is exactly the records the committed offsets cover.
    /// </summary>
    private sealed class FakeTransactions
    {
        private readonly object _gate = new();
        private readonly List<FakeTransaction> _all = [];
        private FakeTransaction? _current;
        private TaskCompletionSource _changed = NewCompletionSource();

        public FakeTransactions()
        {
            Producer = new PartitionedTransactionalProducer(Begin);
        }

        public PartitionedTransactionalProducer Producer { get; }

        public Queue<Exception> CommitFailures { get; } = new();

        /// <summary>When set, commits never finish until their cancellation token fires.</summary>
        public bool BlockCommits { get; init; }

        public int CommittedCount
        {
            get
            {
                lock (_gate)
                    return _all.Count(transaction => transaction.Committed);
            }
        }

        public int AbortedCount
        {
            get
            {
                lock (_gate)
                    return _all.Count(transaction => transaction.Aborted);
            }
        }

        public void Produce(long offset) => Produce(0, offset);

        public void Produce(int partition, long offset)
        {
            lock (_gate)
            {
                if (_current is not { IsOpen: true } transaction)
                    throw new InvalidOperationException("No transaction is open.");
                transaction.Produced.Add((partition, offset));
            }
        }

        public long[] CommittedOutput(TopicPartition partition)
        {
            lock (_gate)
            {
                return _all.Where(transaction => transaction.Committed)
                    .SelectMany(transaction => transaction.Produced)
                    .Where(output => output.Partition == partition.Partition)
                    .Select(output => output.Offset)
                    .ToArray();
            }
        }

        /// <summary>Committed output is each record below the last committed offset, exactly once.</summary>
        public void AssertOutputMatchesOffsets(TopicPartition partition)
        {
            lock (_gate)
            {
                var committedOffset = 0L;
                foreach (var transaction in _all.Where(transaction => transaction.Committed))
                {
                    if (transaction.Offsets!.FirstOrDefault(offset => offset.Partition == partition.Partition) is { Topic: not null } offset)
                        committedOffset = offset.Offset;
                }

                var output = CommittedOutput(partition).Order().ToArray();
                var expected = Enumerable.Range(0, (int)committedOffset).Select(value => (long)value).ToArray();
                if (!output.SequenceEqual(expected))
                {
                    throw new InvalidOperationException(
                        $"Committed output [{string.Join(",", output)}] does not match committed offset {committedOffset} for partition {partition.Partition}.");
                }
            }
        }

        public Task<FakeTransaction> WaitForCommitAsync(long offset, CancellationToken cancellationToken)
            => WaitForCommitAsync(new TopicPartition("topic-a", 0), offset, cancellationToken);

        public async Task<FakeTransaction> WaitForCommitAsync(TopicPartition partition, long offset, CancellationToken cancellationToken)
        {
            while (true)
            {
                Task changed;
                lock (_gate)
                {
                    var committed = _all.FirstOrDefault(transaction => transaction.Committed
                        && transaction.Offsets!.Any(value => value.Partition == partition.Partition && value.Offset >= offset));
                    if (committed is not null)
                        return committed;
                    changed = _changed.Task;
                }

                await changed.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task WaitForAbortAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                Task changed;
                lock (_gate)
                {
                    if (_all.Any(transaction => transaction.Aborted))
                        return;
                    changed = _changed.Task;
                }

                await changed.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private IPartitionedTransaction Begin()
        {
            lock (_gate)
            {
                if (_current is { IsOpen: true })
                    throw new InvalidOperationException("A transaction is already open.");
                _current = new FakeTransaction(this);
                _all.Add(_current);
                return _current;
            }
        }

        private void Complete(FakeTransaction transaction, bool commit, IReadOnlyList<TopicPartitionOffset>? offsets, ConsumerGroupMetadata? metadata)
        {
            TaskCompletionSource changed;
            lock (_gate)
            {
                if (!transaction.IsOpen)
                    throw new InvalidOperationException("The transaction already ended.");
                if (commit && CommitFailures.TryDequeue(out var failure))
                    throw failure;

                transaction.Offsets = offsets?.ToArray();
                transaction.Metadata = metadata;
                if (commit)
                    transaction.Committed = true;
                else
                    transaction.Aborted = true;
                changed = _changed;
                _changed = NewCompletionSource();
            }

            changed.TrySetResult();
        }

        public sealed class FakeTransaction(FakeTransactions owner) : IPartitionedTransaction
        {
            public List<(int Partition, long Offset)> Produced { get; } = [];

            public TopicPartitionOffset[]? Offsets { get; set; }

            public ConsumerGroupMetadata? Metadata { get; set; }

            public bool Committed { get; set; }

            public bool Aborted { get; set; }

            public bool IsOpen => !Committed && !Aborted;

            public async ValueTask CommitAsync(
                IReadOnlyList<TopicPartitionOffset> offsets,
                ConsumerGroupMetadata consumerGroupMetadata,
                CancellationToken cancellationToken)
            {
                if (owner.BlockCommits)
                    await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                owner.Complete(this, commit: true, offsets, consumerGroupMetadata);
            }

            public ValueTask AbortAsync(CancellationToken cancellationToken)
            {
                owner.Complete(this, commit: false, offsets: null, metadata: null);
                return ValueTask.CompletedTask;
            }
        }
    }
}
