using System.Globalization;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Exercises KIP-848 group churn while a finite, sequenced backlog remains available.
/// Consumption permits make every churn phase deterministic without timing sleeps.
/// </summary>
[Category("ConsumerGroup")]
[SupportsKafka(400)]
public sealed class ConsumerGroupRebalanceChaosTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    private const int PartitionCount = 4;
    private const int MessagesPerPartition = 32;
    private const int CommitInterval = 4;
    private const int ChurnCycles = 2;
    private const int JointPhaseMessagesPerMember = 3;
    private const int RecoveryPhaseMessages = 3;
    private const int StaticRestartProgressMessages = 3;
    private const int MaxStaticRestartDuplicates = PartitionCount * CommitInterval * 3;
    private static readonly TimeSpan StaticMemberSessionTimeout = TimeSpan.FromSeconds(6);

    [Test]
    [Timeout(600_000)]
    public async Task DynamicMembers_ChurnUnderLoad_PreservesSequencesAndCommittedProgress(
        CancellationToken cancellationToken)
    {
        foreach (var assignor in new[] { "uniform", "range" })
        {
            await RunChurnScenarioAsync(assignor, cancellationToken);
        }
    }

    [Test]
    [Timeout(600_000)]
    public async Task StaticMember_RestartsWithinAndBeyondSessionTimeout_PreservesSequencesAndCommittedProgress(
        CancellationToken cancellationToken)
    {
        const string assignor = "uniform";
        var (topic, groupId, oracle) = await CreateScenarioAsync("rebalance-chaos-static", cancellationToken);
        var anchorInstanceId = $"anchor-{Guid.NewGuid():N}";
        var restartingInstanceId = $"restarting-{Guid.NewGuid():N}";

        await using var anchor = await CreateMemberAsync(
            topic,
            groupId,
            "static-anchor",
            assignor,
            oracle,
            cancellationToken,
            anchorInstanceId,
            StaticMemberSessionTimeout);

        anchor.Allow(1);
        await anchor.WaitForAssignmentCountAsync(PartitionCount, cancellationToken);
        await anchor.WaitForObservedCountAsync(1, cancellationToken);

        await using (var original = await CreateMemberAsync(
                         topic,
                         groupId,
                         "static-restarting-original",
                         assignor,
                         oracle,
                         cancellationToken,
                         restartingInstanceId,
                         StaticMemberSessionTimeout))
        {
            original.Allow(1);
            await Task.WhenAll(
                anchor.WaitForAssignmentCountAsync(PartitionCount / 2, cancellationToken),
                original.WaitForAssignmentCountAsync(PartitionCount / 2, cancellationToken),
                original.WaitForObservedCountAsync(1, cancellationToken));
            await original.StopAsync();
        }

        var withinTimeoutAnchorTarget = anchor.ObservedCount + StaticRestartProgressMessages;
        anchor.Allow(StaticRestartProgressMessages);

        await using (var withinTimeoutRestart = await CreateMemberAsync(
                         topic,
                         groupId,
                         "static-restarting-within-timeout",
                         assignor,
                         oracle,
                         cancellationToken,
                         restartingInstanceId,
                         StaticMemberSessionTimeout))
        {
            withinTimeoutRestart.Allow(StaticRestartProgressMessages);
            await Task.WhenAll(
                anchor.WaitForObservedCountAsync(withinTimeoutAnchorTarget, cancellationToken),
                anchor.WaitForAssignmentCountAsync(PartitionCount / 2, cancellationToken),
                withinTimeoutRestart.WaitForAssignmentCountAsync(PartitionCount / 2, cancellationToken),
                withinTimeoutRestart.WaitForObservedCountAsync(StaticRestartProgressMessages, cancellationToken));

            await AssertCommittedOffsetsAsync(groupId, oracle, cancellationToken);
            await withinTimeoutRestart.StopAsync();
        }

        var beyondTimeoutAnchorTarget = anchor.ObservedCount + StaticRestartProgressMessages;
        anchor.Allow(StaticRestartProgressMessages);
        await anchor.WaitForObservedCountAsync(beyondTimeoutAnchorTarget, cancellationToken);

        // A static leave retains its assignment. The surviving member receives every partition
        // only after the broker expires that reservation, making this a broker-driven timeout signal.
        await anchor.WaitForAssignmentCountAsync(PartitionCount, cancellationToken);
        await AssertCommittedOffsetsAsync(groupId, oracle, cancellationToken);

        await using var beyondTimeoutRestart = await CreateMemberAsync(
            topic,
            groupId,
            "static-restarting-after-timeout",
            assignor,
            oracle,
            cancellationToken,
            restartingInstanceId,
            StaticMemberSessionTimeout);

        beyondTimeoutRestart.Allow(1);
        await Task.WhenAll(
            anchor.WaitForAssignmentCountAsync(PartitionCount / 2, cancellationToken),
            beyondTimeoutRestart.WaitForAssignmentCountAsync(PartitionCount / 2, cancellationToken),
            beyondTimeoutRestart.WaitForObservedCountAsync(1, cancellationToken));

        anchor.Allow(PartitionCount * MessagesPerPartition * 2);
        beyondTimeoutRestart.Allow(PartitionCount * MessagesPerPartition * 2);
        await oracle.WaitForAllSequencesAsync(cancellationToken);
        await oracle.WaitForFinalCommitsAsync(cancellationToken);
        await Task.WhenAll(anchor.StopAsync(), beyondTimeoutRestart.StopAsync());

        await AssertCompletedScenarioAsync(
            groupId,
            oracle,
            MaxStaticRestartDuplicates,
            cancellationToken);
    }

    private async Task RunChurnScenarioAsync(string assignor, CancellationToken cancellationToken)
    {
        var (topic, groupId, oracle) = await CreateScenarioAsync(
            $"rebalance-chaos-{assignor}",
            cancellationToken);

        await using var anchor = await CreateMemberAsync(
            topic,
            groupId,
            $"{assignor}-anchor",
            assignor,
            oracle,
            cancellationToken);

        anchor.Allow(1);
        await anchor.WaitForAssignmentCountAsync(PartitionCount, cancellationToken);
        await anchor.WaitForObservedCountAsync(1, cancellationToken);

        for (var cycle = 0; cycle < ChurnCycles; cycle++)
        {
            await using var transient = await CreateMemberAsync(
                topic,
                groupId,
                $"{assignor}-transient-{cycle}",
                assignor,
                oracle,
                cancellationToken);

            transient.Allow(1);
            await transient.WaitForAnyAssignmentAsync(cancellationToken);
            await transient.WaitForObservedCountAsync(1, cancellationToken);

            var anchorTarget = anchor.ObservedCount + JointPhaseMessagesPerMember;
            var transientTarget = transient.ObservedCount + JointPhaseMessagesPerMember;
            anchor.Allow(JointPhaseMessagesPerMember);
            transient.Allow(JointPhaseMessagesPerMember);

            await Task.WhenAll(
                anchor.WaitForObservedCountAsync(anchorTarget, cancellationToken),
                transient.WaitForObservedCountAsync(transientTarget, cancellationToken));

            await transient.StopAsync();

            await AssertCommittedOffsetsAsync(groupId, oracle, cancellationToken);
            await anchor.WaitForAssignmentCountAsync(PartitionCount, cancellationToken);
            var recoveryTarget = anchor.ObservedCount + RecoveryPhaseMessages;
            anchor.Allow(RecoveryPhaseMessages);
            await anchor.WaitForObservedCountAsync(recoveryTarget, cancellationToken);
        }

        anchor.Allow(PartitionCount * MessagesPerPartition * 2);
        await oracle.WaitForAllSequencesAsync(cancellationToken);
        await oracle.WaitForFinalCommitsAsync(cancellationToken);
        await anchor.StopAsync();

        await AssertCompletedScenarioAsync(groupId, oracle, maximumDuplicateCount: null, cancellationToken);
    }

    private async Task<(string Topic, string GroupId, SequenceOracle Oracle)> CreateScenarioAsync(
        string groupIdPrefix,
        CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: PartitionCount);
        var groupId = $"{groupIdPrefix}-{Guid.NewGuid():N}";
        var oracle = new SequenceOracle(topic, PartitionCount, MessagesPerPartition, CommitInterval);

        await ConfigureTopicRetentionAsync(topic, cancellationToken);
        await ProduceSequencedBacklogAsync(topic, cancellationToken);

        return (topic, groupId, oracle);
    }

    private async Task AssertCompletedScenarioAsync(
        string groupId,
        SequenceOracle oracle,
        int? maximumDuplicateCount,
        CancellationToken cancellationToken)
    {
        await Assert.That(oracle.UniqueCount).IsEqualTo(PartitionCount * MessagesPerPartition);
        if (maximumDuplicateCount is { } maximum)
            await Assert.That(oracle.DuplicateCount).IsLessThanOrEqualTo(maximum);

        var violations = oracle.Violations;
        await Assert.That(violations).IsEmpty()
            .Because(string.Join(Environment.NewLine, violations));

        for (var partition = 0; partition < PartitionCount; partition++)
        {
            await Assert.That(oracle.GetSeenOffsets(partition))
                .IsEquivalentTo(Enumerable.Range(0, MessagesPerPartition).Select(static value => (long)value));
        }

        await AssertCommittedOffsetsAsync(groupId, oracle, cancellationToken);
    }

    private async Task ConfigureTopicRetentionAsync(string topic, CancellationToken cancellationToken)
    {
        await using var admin = KafkaContainer.CreateAdminClient();
        await admin.IncrementalAlterConfigsAsync(
            new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
            {
                [ConfigResource.Topic(topic)] = [ConfigAlter.Set("retention.ms", "600000")]
            },
            cancellationToken: cancellationToken);
    }

    private async Task ProduceSequencedBacklogAsync(string topic, CancellationToken cancellationToken)
    {
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("rebalance-chaos-seeder")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);

        for (var sequence = 0; sequence < MessagesPerPartition; sequence++)
        {
            for (var partition = 0; partition < PartitionCount; partition++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Partition = partition,
                    Key = partition.ToString(CultureInfo.InvariantCulture),
                    Value = sequence.ToString(CultureInfo.InvariantCulture)
                }, cancellationToken);
            }
        }
    }

    private async Task<ConsumerMember> CreateMemberAsync(
        string topic,
        string groupId,
        string clientId,
        string assignor,
        SequenceOracle oracle,
        CancellationToken cancellationToken,
        string? groupInstanceId = null,
        TimeSpan? sessionTimeout = null)
    {
        var assignments = new AssignmentTracker();
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId(clientId)
            .WithGroupId(groupId)
            .WithGroupRemoteAssignor(assignor)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithMaxPollRecords(1)
            .WithQueuedMinMessages(1)
            .WithRebalanceListener(assignments)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory());

        if (groupInstanceId is not null)
            builder.WithGroupInstanceId(groupInstanceId);
        if (sessionTimeout is { } timeout)
            builder.WithSessionTimeout(timeout);

        var consumer = await builder.BuildAsync(cancellationToken);

        consumer.Subscribe(topic);
        return new ConsumerMember(clientId, consumer, assignments, oracle, cancellationToken);
    }

    private async Task AssertCommittedOffsetsAsync(
        string groupId,
        SequenceOracle oracle,
        CancellationToken cancellationToken)
    {
        await using var admin = KafkaContainer.CreateAdminClient();
        var committedOffsets = await admin.ListConsumerGroupOffsetsAsync(groupId, cancellationToken);

        for (var partition = 0; partition < PartitionCount; partition++)
        {
            var (confirmed, processed) = oracle.GetProgressBounds(partition);
            if (confirmed == 0)
                continue;

            await Assert.That(committedOffsets.TryGetValue(
                    new TopicPartition(oracle.Topic, partition),
                    out var committed))
                .IsTrue();
            await Assert.That(committed).IsGreaterThanOrEqualTo(confirmed)
                .Because("Kafka may accept a commit before member shutdown or rebalance interrupts its response");
            await Assert.That(committed).IsLessThanOrEqualTo(processed)
                .Because("committed progress must not pass the contiguous processed prefix");
        }
    }

    private sealed class ConsumerMember : IAsyncDisposable
    {
        private readonly IKafkaConsumer<string, string> _consumer;
        private readonly string _clientId;
        private readonly AssignmentTracker _assignments;
        private readonly SequenceOracle _oracle;
        private readonly CancellationTokenSource _stopping;
        private readonly SemaphoreSlim _permits = new(0, int.MaxValue);
        private readonly AsyncCounter _observed = new();
        private readonly Task _runTask;
        private int _stopped;

        public ConsumerMember(
            string clientId,
            IKafkaConsumer<string, string> consumer,
            AssignmentTracker assignments,
            SequenceOracle oracle,
            CancellationToken cancellationToken)
        {
            _clientId = clientId;
            _consumer = consumer;
            _assignments = assignments;
            _oracle = oracle;
            _stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _runTask = Task.Run(RunAsync, CancellationToken.None);
        }

        public int ObservedCount => _observed.Value;

        public void Allow(int count) => _permits.Release(count);

        public Task WaitForAnyAssignmentAsync(CancellationToken cancellationToken) =>
            WaitForSignalOrCompletionAsync(
                _assignments.WaitForAnyAsync(cancellationToken),
                "Consumer completed before receiving an assignment",
                cancellationToken);

        public Task WaitForAssignmentCountAsync(int count, CancellationToken cancellationToken) =>
            WaitForSignalOrCompletionAsync(
                _assignments.WaitForCountAsync(count, cancellationToken),
                $"Consumer completed before receiving {count} partitions",
                cancellationToken);

        public Task WaitForObservedCountAsync(int count, CancellationToken cancellationToken) =>
            WaitForSignalOrCompletionAsync(
                _observed.WaitForAsync(count, cancellationToken),
                $"Consumer completed before observing {count} records",
                cancellationToken);

        public async Task StopAsync()
        {
            if (Interlocked.Exchange(ref _stopped, 1) != 0)
                return;

            try
            {
                await _stopping.CancelAsync();
                await _runTask;
            }
            finally
            {
                try
                {
                    await _consumer.DisposeAsync();
                }
                finally
                {
                    _permits.Dispose();
                    _stopping.Dispose();
                }
            }
        }

        public async ValueTask DisposeAsync() => await StopAsync();

        private async Task RunAsync()
        {
            try
            {
                await using var enumerator = _consumer
                    .ConsumeAsync(_stopping.Token)
                    .GetAsyncEnumerator(_stopping.Token);

                while (!_oracle.IsComplete)
                {
                    await _permits.WaitAsync(_stopping.Token);
                    if (!await enumerator.MoveNextAsync())
                    {
                        _stopping.Token.ThrowIfCancellationRequested();
                        throw new InvalidOperationException("Consumer stream completed before the sequence oracle");
                    }

                    var result = enumerator.Current;
                    _oracle.Record(_clientId, result);
                    _observed.Increment();
                    await _oracle.CommitIfNeededAsync(_consumer, result, _stopping.Token);
                }
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
            {
            }
        }

        private async Task WaitForSignalOrCompletionAsync(
            Task signal,
            string completionMessage,
            CancellationToken cancellationToken)
        {
            var completed = await Task.WhenAny(signal, _runTask).WaitAsync(cancellationToken);
            if (ReferenceEquals(completed, _runTask))
            {
                await _runTask;
                if (!signal.IsCompleted)
                    throw new InvalidOperationException(completionMessage);
            }

            await signal.WaitAsync(cancellationToken);
        }
    }

    private sealed class SequenceOracle
    {
        private readonly string _topic;
        private readonly int _messagesPerPartition;
        private readonly int _commitInterval;
        private readonly PartitionState[] _partitions;
        private readonly SemaphoreSlim[] _commitLocks;
        private readonly AsyncCounter _unique = new();
        private readonly AsyncCounter _finalCommits = new();
        private readonly object _violationsGate = new();
        private readonly List<string> _violations = [];
        private int _duplicateCount;

        public SequenceOracle(
            string topic,
            int partitionCount,
            int messagesPerPartition,
            int commitInterval)
        {
            _topic = topic;
            _messagesPerPartition = messagesPerPartition;
            _commitInterval = commitInterval;
            _partitions = Enumerable.Range(0, partitionCount)
                .Select(static _ => new PartitionState())
                .ToArray();
            _commitLocks = Enumerable.Range(0, partitionCount)
                .Select(static _ => new SemaphoreSlim(1, 1))
                .ToArray();
        }

        public int UniqueCount => _unique.Value;

        public int DuplicateCount => Volatile.Read(ref _duplicateCount);

        public string Topic => _topic;

        public bool IsComplete => UniqueCount == _partitions.Length * _messagesPerPartition;

        public string[] Violations
        {
            get
            {
                lock (_violationsGate)
                    return [.. _violations];
            }
        }

        public void Record(string clientId, ConsumeResult<string, string> result)
        {
            if (!string.Equals(result.Topic, _topic, StringComparison.Ordinal))
            {
                AddViolation($"Unexpected topic {result.Topic}; expected {_topic}");
                return;
            }

            if (result.Partition < 0 || result.Partition >= _partitions.Length)
            {
                AddViolation($"Unexpected partition {result.Partition}");
                return;
            }

            if (!long.TryParse(result.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence))
            {
                AddViolation($"Partition {result.Partition}: invalid sequence payload '{result.Value}'");
                return;
            }

            if (sequence != result.Offset)
                AddViolation($"Partition {result.Partition}: payload {sequence} does not match offset {result.Offset}");

            if (result.Offset < 0 || result.Offset >= _messagesPerPartition)
            {
                AddViolation($"Partition {result.Partition}: offset {result.Offset} is outside seeded range");
                return;
            }

            var state = _partitions[result.Partition];
            var unique = false;
            lock (state.Gate)
            {
                if (state.Seen.Add(result.Offset))
                {
                    unique = true;
                    if (result.Offset != state.NextExpected)
                    {
                        AddViolation(
                            $"Partition {result.Partition}: observed new offset {result.Offset} before {state.NextExpected}");
                    }

                    while (state.Seen.Contains(state.NextExpected))
                        state.NextExpected++;
                }
                else
                {
                    Interlocked.Increment(ref _duplicateCount);
                    if (result.Offset < state.CommittedExclusive)
                    {
                        AddViolation(
                            $"{clientId}, partition {result.Partition}: replayed offset {result.Offset} below committed offset {state.CommittedExclusive}");
                    }
                }
            }

            if (unique)
                _unique.Increment();
        }

        public async Task CommitIfNeededAsync(
            IKafkaConsumer<string, string> consumer,
            ConsumeResult<string, string> result,
            CancellationToken cancellationToken)
        {
            var partition = result.Partition;
            var commitLock = _commitLocks[partition];
            await commitLock.WaitAsync(cancellationToken);
            try
            {
                var state = _partitions[partition];
                long candidate;
                lock (state.Gate)
                {
                    candidate = Math.Min(state.NextExpected, result.Offset + 1);
                    if (candidate <= state.CommittedExclusive)
                        return;

                    if (candidate < _messagesPerPartition &&
                        candidate - state.CommittedExclusive < _commitInterval)
                    {
                        return;
                    }
                }

                try
                {
                    await consumer.CommitAsync(
                        [new TopicPartitionOffset(_topic, partition, candidate)],
                        cancellationToken);
                }
                catch (GroupException exception) when (IsExpectedRebalanceCommitFailure(exception.ErrorCode))
                {
                    return;
                }

                var finalCommit = false;
                lock (state.Gate)
                {
                    if (candidate < state.CommittedExclusive)
                    {
                        AddViolation(
                            $"Partition {partition}: commit rewound from {state.CommittedExclusive} to {candidate}");
                    }
                    else if (candidate > state.NextExpected)
                    {
                        AddViolation(
                            $"Partition {partition}: commit {candidate} jumped past processed offset {state.NextExpected}");
                    }
                    else
                    {
                        finalCommit = state.CommittedExclusive < _messagesPerPartition &&
                                      candidate == _messagesPerPartition;
                        state.CommittedExclusive = candidate;
                    }
                }

                if (finalCommit)
                    _finalCommits.Increment();
            }
            finally
            {
                commitLock.Release();
            }
        }

        public Task WaitForAllSequencesAsync(CancellationToken cancellationToken) =>
            _unique.WaitForAsync(_partitions.Length * _messagesPerPartition, cancellationToken);

        public Task WaitForFinalCommitsAsync(CancellationToken cancellationToken) =>
            _finalCommits.WaitForAsync(_partitions.Length, cancellationToken);

        public long[] GetSeenOffsets(int partition)
        {
            var state = _partitions[partition];
            lock (state.Gate)
                return [.. state.Seen.Order()];
        }

        public (long ConfirmedCommit, long ProcessedExclusive) GetProgressBounds(int partition)
        {
            var state = _partitions[partition];
            lock (state.Gate)
                return (state.CommittedExclusive, state.NextExpected);
        }

        private static bool IsExpectedRebalanceCommitFailure(ErrorCode? errorCode) => errorCode is
            ErrorCode.IllegalGeneration or
            ErrorCode.UnknownMemberId or
            ErrorCode.RebalanceInProgress or
            ErrorCode.FencedMemberEpoch or
            ErrorCode.StaleMemberEpoch;

        private void AddViolation(string violation)
        {
            lock (_violationsGate)
                _violations.Add(violation);
        }

        private sealed class PartitionState
        {
            public object Gate { get; } = new();
            public HashSet<long> Seen { get; } = [];
            public long NextExpected { get; set; }
            public long CommittedExclusive { get; set; }
        }
    }

    private sealed class AssignmentTracker : IRebalanceListener
    {
        private readonly object _gate = new();
        private readonly HashSet<TopicPartition> _assigned = [];
        private readonly List<AssignmentWaiter> _waiters = [];

        public ValueTask OnPartitionsAssignedAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            Update(partitions, assigned: true);
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsRevokedAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            Update(partitions, assigned: false);
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsLostAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            Update(partitions, assigned: false);
            return ValueTask.CompletedTask;
        }

        public Task WaitForAnyAsync(CancellationToken cancellationToken) =>
            WaitForAsync(static count => count > 0, cancellationToken);

        public Task WaitForCountAsync(int count, CancellationToken cancellationToken) =>
            WaitForAsync(current => current == count, cancellationToken);

        private Task WaitForAsync(Func<int, bool> predicate, CancellationToken cancellationToken)
        {
            Task signal;
            lock (_gate)
            {
                if (predicate(_assigned.Count))
                    return Task.CompletedTask;

                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiters.Add(new AssignmentWaiter(predicate, completion));
                signal = completion.Task;
            }

            return signal.WaitAsync(cancellationToken);
        }

        private void Update(IEnumerable<TopicPartition> partitions, bool assigned)
        {
            List<TaskCompletionSource>? completed = null;
            lock (_gate)
            {
                foreach (var partition in partitions)
                {
                    if (assigned)
                        _assigned.Add(partition);
                    else
                        _assigned.Remove(partition);
                }

                for (var index = _waiters.Count - 1; index >= 0; index--)
                {
                    var waiter = _waiters[index];
                    if (!waiter.Predicate(_assigned.Count))
                        continue;

                    completed ??= [];
                    completed.Add(waiter.Completion);
                    _waiters.RemoveAt(index);
                }
            }

            if (completed is null)
                return;

            foreach (var completion in completed)
                completion.TrySetResult();
        }

        private sealed record AssignmentWaiter(
            Func<int, bool> Predicate,
            TaskCompletionSource Completion);
    }

    private sealed class AsyncCounter
    {
        private readonly object _gate = new();
        private readonly List<CounterWaiter> _waiters = [];
        private int _value;

        public int Value => Volatile.Read(ref _value);

        public void Increment()
        {
            List<TaskCompletionSource>? completed = null;
            lock (_gate)
            {
                _value++;
                for (var index = _waiters.Count - 1; index >= 0; index--)
                {
                    var waiter = _waiters[index];
                    if (_value < waiter.Target)
                        continue;

                    completed ??= [];
                    completed.Add(waiter.Completion);
                    _waiters.RemoveAt(index);
                }
            }

            if (completed is null)
                return;

            foreach (var completion in completed)
                completion.TrySetResult();
        }

        public Task WaitForAsync(int target, CancellationToken cancellationToken)
        {
            Task signal;
            lock (_gate)
            {
                if (_value >= target)
                    return Task.CompletedTask;

                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiters.Add(new CounterWaiter(target, completion));
                signal = completion.Task;
            }

            return signal.WaitAsync(cancellationToken);
        }

        private sealed record CounterWaiter(int Target, TaskCompletionSource Completion);
    }
}
