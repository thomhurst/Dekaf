using System.Globalization;
using System.Text;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Serialization;
using ConfluentKafka = Confluent.Kafka;
using ConfluentKafkaAdmin = Confluent.Kafka.Admin;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Exercises KIP-848, classic eager, and online-migration group churn while a finite,
/// sequenced backlog remains available.
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
    private const int GroupConfigVerificationAttempts = 10;
    private static readonly TimeSpan StaticMemberSessionTimeout = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan StaticMemberHeartbeatInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan GroupConfigVerificationDelay = TimeSpan.FromMilliseconds(500);

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
    public async Task SequenceOracle_BrokerObservedCommit_RejectsReplayBelowCommitFloor()
    {
        var oracle = new SequenceOracle("test-topic", partitionCount: 1, messagesPerPartition: 4, commitInterval: 2);
        var second = CreateSequenceResult(offset: 1);

        oracle.Record("original", CreateSequenceResult(offset: 0));
        oracle.Record("original", second);
        oracle.ObserveCommittedOffset(partition: 0, committedExclusive: 2);
        oracle.Record("restarted", second);

        await Assert.That(oracle.GetProgressBounds(0).ConfirmedCommit).IsEqualTo(2);
        await Assert.That(oracle.Violations.Any(static violation =>
            violation.Contains("replayed offset 1 below committed offset 2", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    [Timeout(600_000)]
    public async Task ClassicEagerRange_ChurnUnderLoad_PreservesSequencesAndCommittedProgress(
        CancellationToken cancellationToken)
    {
        await RunChurnScenarioAsync(
            "classic-eager-range",
            MemberProtocol.ClassicEagerRange,
            MemberProtocol.ClassicEagerRange,
            cancellationToken);
    }

    [Test]
    [Timeout(600_000)]
    [SkipWhenNativeAot("Confluent.Kafka's librdkafka delegate binding requires runtime reflection.")]
    public async Task StaticMember_RestartsWithinAndBeyondSessionTimeout_PreservesSequencesAndCommittedProgress(
        CancellationToken cancellationToken)
    {
        const string assignor = "uniform";
        var (topic, groupId, oracle) = await CreateScenarioAsync("rebalance-chaos-static", cancellationToken);
        await ConfigureAndVerifyConsumerGroupTimeoutsAsync(groupId, cancellationToken);
        var anchorInstanceId = $"anchor-{Guid.NewGuid():N}";
        var restartingInstanceId = $"restarting-{Guid.NewGuid():N}";

        await using var anchor = await CreateMemberAsync(
            topic,
            groupId,
            "static-anchor",
            assignor,
            oracle,
            cancellationToken,
            anchorInstanceId);

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
                         restartingInstanceId))
        {
            original.Allow(1);
            await Task.WhenAll(
                anchor.WaitForAssignmentCountAsync(PartitionCount / 2, cancellationToken),
                original.WaitForAssignmentCountAsync(PartitionCount / 2, cancellationToken),
                original.WaitForObservedCountAsync(1, cancellationToken));
            anchor.ResetMaxAssignmentCount();
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
                         restartingInstanceId))
        {
            withinTimeoutRestart.Allow(StaticRestartProgressMessages);
            await Task.WhenAll(
                anchor.WaitForObservedCountAsync(withinTimeoutAnchorTarget, cancellationToken),
                anchor.WaitForAssignmentCountAsync(PartitionCount / 2, cancellationToken),
                withinTimeoutRestart.WaitForAssignmentCountAsync(PartitionCount / 2, cancellationToken),
                withinTimeoutRestart.WaitForObservedCountAsync(StaticRestartProgressMessages, cancellationToken));

            await Assert.That(anchor.MaxAssignmentCount).IsEqualTo(PartitionCount / 2)
                .Because("the broker must retain the stopped static member's assignment until session timeout");
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
            restartingInstanceId);

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

    private async Task ConfigureAndVerifyConsumerGroupTimeoutsAsync(
        string groupId,
        CancellationToken cancellationToken)
    {
        using var admin = new ConfluentKafka.AdminClientBuilder(new ConfluentKafka.AdminClientConfig
        {
            BootstrapServers = KafkaContainer.BootstrapServers
        }).Build();
        var resource = new ConfluentKafkaAdmin.ConfigResource
        {
            Type = ConfluentKafkaAdmin.ResourceType.Group,
            Name = groupId
        };
        var sessionTimeout = ((int)StaticMemberSessionTimeout.TotalMilliseconds)
            .ToString(CultureInfo.InvariantCulture);
        var heartbeatInterval = ((int)StaticMemberHeartbeatInterval.TotalMilliseconds)
            .ToString(CultureInfo.InvariantCulture);

        await admin.IncrementalAlterConfigsAsync(new Dictionary<
            ConfluentKafkaAdmin.ConfigResource,
            List<ConfluentKafkaAdmin.ConfigEntry>>
        {
            [resource] =
            [
                new ConfluentKafkaAdmin.ConfigEntry
                {
                    Name = "consumer.session.timeout.ms",
                    Value = sessionTimeout,
                    IncrementalOperation = ConfluentKafkaAdmin.AlterConfigOpType.Set
                },
                new ConfluentKafkaAdmin.ConfigEntry
                {
                    Name = "consumer.heartbeat.interval.ms",
                    Value = heartbeatInterval,
                    IncrementalOperation = ConfluentKafkaAdmin.AlterConfigOpType.Set
                }
            ]
        }).WaitAsync(cancellationToken);

        // The controller can acknowledge the alteration before the coordinator observes it.
        for (var attempt = 0; attempt < GroupConfigVerificationAttempts; attempt++)
        {
            var results = await admin.DescribeConfigsAsync([resource]).WaitAsync(cancellationToken);
            var entries = results.Single().Entries;
            var sessionTimeoutMatches = entries["consumer.session.timeout.ms"].Value == sessionTimeout;
            var heartbeatIntervalMatches = entries["consumer.heartbeat.interval.ms"].Value == heartbeatInterval;

            if (sessionTimeoutMatches && heartbeatIntervalMatches)
                return;

            if (attempt == GroupConfigVerificationAttempts - 1)
            {
                await Assert.That(entries["consumer.session.timeout.ms"].Value).IsEqualTo(sessionTimeout);
                await Assert.That(entries["consumer.heartbeat.interval.ms"].Value).IsEqualTo(heartbeatInterval);
                return;
            }

            await Task.Delay(GroupConfigVerificationDelay, cancellationToken);
        }
    }

    private static ConsumeResult<string, string> CreateSequenceResult(long offset) =>
        new(
            topic: "test-topic",
            partition: 0,
            offset,
            keyData: ReadOnlyMemory<byte>.Empty,
            isKeyNull: true,
            valueData: Encoding.UTF8.GetBytes(offset.ToString(CultureInfo.InvariantCulture)),
            isValueNull: false,
            headers: null,
            timestampMs: 0,
            timestampType: TimestampType.NotAvailable,
            leaderEpoch: null,
            keyDeserializer: null,
            valueDeserializer: Serializers.String);

    [Test]
    [Timeout(600_000)]
    public async Task ClassicEagerRange_ToKip848Uniform_OnlineMigrationPreservesSequencesAndCommittedProgress(
        CancellationToken cancellationToken)
    {
        await RunChurnScenarioAsync(
            "classic-range-to-kip848-uniform",
            MemberProtocol.ClassicEagerRange,
            MemberProtocol.Kip848Uniform,
            cancellationToken);
    }

    private async Task RunChurnScenarioAsync(string assignor, CancellationToken cancellationToken)
    {
        var protocol = assignor switch
        {
            "uniform" => MemberProtocol.Kip848Uniform,
            "range" => MemberProtocol.Kip848Range,
            _ => throw new ArgumentOutOfRangeException(nameof(assignor), assignor, "Unknown KIP-848 assignor")
        };

        await RunChurnScenarioAsync($"kip848-{assignor}", protocol, protocol, cancellationToken);
    }

    private async Task RunChurnScenarioAsync(
        string scenarioName,
        MemberProtocol anchorProtocol,
        MemberProtocol transientProtocol,
        CancellationToken cancellationToken)
    {
        var (topic, groupId, oracle) = await CreateScenarioAsync(
            $"rebalance-chaos-{scenarioName}",
            cancellationToken);

        await using var admin = KafkaContainer.CreateAdminClient();

        await using var anchor = await CreateMemberAsync(
            topic,
            groupId,
            $"{scenarioName}-anchor",
            anchorProtocol,
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
                $"{scenarioName}-transient-{cycle}",
                transientProtocol,
                oracle,
                cancellationToken);

            transient.Allow(1);
            await transient.WaitForAnyAssignmentAsync(cancellationToken);
            await transient.WaitForObservedCountAsync(1, cancellationToken);
            await AssertGroupModeAsync(admin, groupId, transientProtocol, cancellationToken);

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
            await AssertGroupModeAsync(admin, groupId, anchorProtocol, cancellationToken);
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

    private async Task<IConsumerMember> CreateMemberAsync(
        string topic,
        string groupId,
        string clientId,
        MemberProtocol protocol,
        SequenceOracle oracle,
        CancellationToken cancellationToken)
    {
        if (protocol == MemberProtocol.ClassicEagerRange)
            return CreateClassicMember(topic, groupId, clientId, oracle, cancellationToken);

        return await CreateDekafMemberAsync(
            topic,
            groupId,
            clientId,
            GetRemoteAssignor(protocol),
            oracle,
            cancellationToken);
    }

    private async Task<IConsumerMember> CreateDekafMemberAsync(
        string topic,
        string groupId,
        string clientId,
        string assignor,
        SequenceOracle oracle,
        CancellationToken cancellationToken,
        string? groupInstanceId = null)
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

        var consumer = await builder.BuildAsync(cancellationToken);

        consumer.Subscribe(topic);
        return new DekafConsumerMember(clientId, consumer, assignments, oracle, cancellationToken);
    }

    private IConsumerMember CreateClassicMember(
        string topic,
        string groupId,
        string clientId,
        SequenceOracle oracle,
        CancellationToken cancellationToken)
    {
        var assignments = new AssignmentTracker();
        var consumer = new ConfluentKafka.ConsumerBuilder<string, string>(new ConfluentKafka.ConsumerConfig
        {
            BootstrapServers = KafkaContainer.BootstrapServers,
            ClientId = clientId,
            GroupId = groupId,
            GroupProtocol = ConfluentKafka.GroupProtocol.Classic,
            PartitionAssignmentStrategy = ConfluentKafka.PartitionAssignmentStrategy.Range,
            AutoOffsetReset = ConfluentKafka.AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            MaxPollRecords = 1,
            QueuedMinMessages = 1
        })
            .SetPartitionsAssignedHandler((_, partitions) =>
                assignments.Assign(partitions.Select(static partition =>
                    new TopicPartition(partition.Topic, partition.Partition.Value))))
            .SetPartitionsRevokedHandler((_, partitions) =>
                assignments.Revoke(partitions.Select(static partition =>
                    new TopicPartition(partition.Topic, partition.Partition.Value))))
            .SetPartitionsLostHandler((_, partitions) =>
                assignments.Revoke(partitions.Select(static partition =>
                    new TopicPartition(partition.Topic, partition.Partition.Value))))
            .Build();

        consumer.Subscribe(topic);
        return new ClassicConsumerMember(clientId, consumer, assignments, oracle, cancellationToken);
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
            if (!committedOffsets.TryGetValue(
                    new TopicPartition(oracle.Topic, partition),
                    out var committed))
            {
                await Assert.That(confirmed).IsEqualTo(0)
                    .Because("a locally confirmed commit must exist on the broker");
                continue;
            }

            await Assert.That(committed).IsGreaterThanOrEqualTo(confirmed)
                .Because("Kafka may accept a commit before member shutdown or rebalance interrupts its response");
            await Assert.That(committed).IsLessThanOrEqualTo(processed)
                .Because("committed progress must not pass the contiguous processed prefix");
            oracle.ObserveCommittedOffset(partition, committed);
        }
    }

    private static async Task AssertGroupModeAsync(
        IAdminClient admin,
        string groupId,
        MemberProtocol protocol,
        CancellationToken cancellationToken)
    {
        var descriptions = await admin.DescribeConsumerGroupsAsync([groupId], cancellationToken);
        await Assert.That(descriptions).ContainsKey(groupId);

        var description = descriptions[groupId];
        if (protocol == MemberProtocol.ClassicEagerRange)
        {
            await Assert.That(description.GroupEpoch).IsNull();
            await Assert.That(description.ProtocolData).IsEqualTo("range");
            return;
        }

        await Assert.That(description.GroupEpoch).IsNotNull();
        await Assert.That(description.AssignorName).IsEqualTo(GetRemoteAssignor(protocol));
    }

    private static async Task WaitForSignalOrCompletionAsync(
        Task signal,
        Task runTask,
        string completionMessage,
        CancellationToken cancellationToken)
    {
        var completed = await Task.WhenAny(signal, runTask).WaitAsync(cancellationToken);
        if (ReferenceEquals(completed, runTask))
        {
            await runTask;
            if (!signal.IsCompleted)
                throw new InvalidOperationException(completionMessage);
        }

        await signal.WaitAsync(cancellationToken);
    }

    private interface IConsumerMember : IAsyncDisposable
    {
        int ObservedCount { get; }
        void Allow(int count);
        Task WaitForAnyAssignmentAsync(CancellationToken cancellationToken);
        Task WaitForAssignmentCountAsync(int count, CancellationToken cancellationToken);
        Task WaitForObservedCountAsync(int count, CancellationToken cancellationToken);
        Task StopAsync();
    }

    private readonly record struct ObservedRecord(
        string Topic,
        int Partition,
        long Offset,
        string? Value);

    private sealed class DekafConsumerMember : IConsumerMember
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

        public DekafConsumerMember(
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

        public int MaxAssignmentCount => _assignments.MaxAssignmentCount;

        public void Allow(int count) => _permits.Release(count);

        public void ResetMaxAssignmentCount() => _assignments.ResetMaxAssignmentCount();

        public Task WaitForAnyAssignmentAsync(CancellationToken cancellationToken) =>
            WaitForSignalOrCompletionAsync(
                _assignments.WaitForAnyAsync(cancellationToken),
                _runTask,
                "Consumer completed before receiving an assignment",
                cancellationToken);

        public Task WaitForAssignmentCountAsync(int count, CancellationToken cancellationToken) =>
            WaitForSignalOrCompletionAsync(
                _assignments.WaitForCountAsync(count, cancellationToken),
                _runTask,
                $"Consumer completed before receiving {count} partitions",
                cancellationToken);

        public Task WaitForObservedCountAsync(int count, CancellationToken cancellationToken) =>
            WaitForSignalOrCompletionAsync(
                _observed.WaitForAsync(count, cancellationToken),
                _runTask,
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
                    var record = new ObservedRecord(
                        result.Topic,
                        result.Partition,
                        result.Offset,
                        result.Value);
                    _oracle.Record(_clientId, record);
                    _observed.Increment();
                    await _oracle.CommitIfNeededAsync(record, TryCommitAsync, _stopping.Token);
                }
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
            {
            }
        }

        private async ValueTask<bool> TryCommitAsync(
            TopicPartitionOffset offset,
            CancellationToken cancellationToken)
        {
            try
            {
                await _consumer.CommitAsync([offset], cancellationToken);
                return true;
            }
            catch (GroupException exception) when (IsExpectedDekafCommitFailure(exception.ErrorCode))
            {
                return false;
            }
        }
    }

    private sealed class ClassicConsumerMember : IConsumerMember
    {
        private static readonly TimeSpan EventPollInterval = TimeSpan.FromMilliseconds(50);

        private readonly string _clientId;
        private readonly ConfluentKafka.IConsumer<string, string> _consumer;
        private readonly AssignmentTracker _assignments;
        private readonly SequenceOracle _oracle;
        private readonly CancellationTokenSource _stopping;
        private readonly SemaphoreSlim _permits = new(0, int.MaxValue);
        private readonly AsyncCounter _observed = new();
        private readonly Task _runTask;
        private int _stopped;

        public ClassicConsumerMember(
            string clientId,
            ConfluentKafka.IConsumer<string, string> consumer,
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
                _runTask,
                "Classic consumer completed before receiving an assignment",
                cancellationToken);

        public Task WaitForAssignmentCountAsync(int count, CancellationToken cancellationToken) =>
            WaitForSignalOrCompletionAsync(
                _assignments.WaitForCountAsync(count, cancellationToken),
                _runTask,
                $"Classic consumer completed before receiving {count} partitions",
                cancellationToken);

        public Task WaitForObservedCountAsync(int count, CancellationToken cancellationToken) =>
            WaitForSignalOrCompletionAsync(
                _observed.WaitForAsync(count, cancellationToken),
                _runTask,
                $"Classic consumer completed before observing {count} records",
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
                _consumer.Dispose();
                _permits.Dispose();
                _stopping.Dispose();
            }
        }

        public async ValueTask DisposeAsync() => await StopAsync();

        private async Task RunAsync()
        {
            try
            {
                while (!_oracle.IsComplete)
                {
                    _stopping.Token.ThrowIfCancellationRequested();

                    if (_permits.Wait(0))
                    {
                        ResumeCurrentAssignment();
                        var result = _consumer.Consume(_stopping.Token);
                        await ObserveAsync(result);
                        if (_permits.CurrentCount == 0)
                            PauseCurrentAssignment();
                        continue;
                    }

                    PauseCurrentAssignment();
                    var eventResult = _consumer.Consume(EventPollInterval);
                    if (eventResult is null)
                        continue;

                    // A poll can install a new eager assignment and return its first record before
                    // the next pause. Record it rather than advancing the client position invisibly;
                    // the immediate pause bounds this to one record per assignment transition.
                    PauseCurrentAssignment();
                    await ObserveAsync(eventResult);
                }
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
            {
            }
            finally
            {
                _consumer.Close();
            }
        }

        private async Task ObserveAsync(ConfluentKafka.ConsumeResult<string, string> result)
        {
            var record = new ObservedRecord(
                result.Topic,
                result.Partition.Value,
                result.Offset.Value,
                result.Message.Value);
            _oracle.Record(_clientId, record);
            _observed.Increment();
            await _oracle.CommitIfNeededAsync(record, TryCommitAsync, _stopping.Token);
        }

        private void PauseCurrentAssignment()
        {
            var assignment = _consumer.Assignment;
            if (assignment.Count > 0)
                _consumer.Pause(assignment);
        }

        private void ResumeCurrentAssignment()
        {
            var assignment = _consumer.Assignment;
            if (assignment.Count > 0)
                _consumer.Resume(assignment);
        }

        private ValueTask<bool> TryCommitAsync(
            TopicPartitionOffset offset,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                _consumer.Commit(
                [
                    new ConfluentKafka.TopicPartitionOffset(
                        offset.Topic,
                        offset.Partition,
                        offset.Offset)
                ]);
                return ValueTask.FromResult(true);
            }
            catch (ConfluentKafka.KafkaException exception)
                when (IsExpectedConfluentCommitFailure(exception.Error.Code))
            {
                return ValueTask.FromResult(false);
            }
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

        public void Record(string clientId, ObservedRecord result)
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
            ObservedRecord result,
            Func<TopicPartitionOffset, CancellationToken, ValueTask<bool>> tryCommitAsync,
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

                if (!await tryCommitAsync(
                        new TopicPartitionOffset(_topic, partition, candidate),
                        cancellationToken))
                    return;

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

        public void ObserveCommittedOffset(int partition, long committedExclusive)
        {
            var state = _partitions[partition];
            var finalCommit = false;
            lock (state.Gate)
            {
                if (committedExclusive <= state.CommittedExclusive)
                    return;

                if (committedExclusive > state.NextExpected)
                {
                    AddViolation(
                        $"Partition {partition}: broker commit {committedExclusive} jumped past processed offset {state.NextExpected}");
                    return;
                }

                finalCommit = state.CommittedExclusive < _messagesPerPartition &&
                              committedExclusive == _messagesPerPartition;
                state.CommittedExclusive = committedExclusive;
            }

            if (finalCommit)
                _finalCommits.Increment();
        }

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
        private int _maxAssignmentCount;

        public int MaxAssignmentCount
        {
            get
            {
                lock (_gate)
                    return _maxAssignmentCount;
            }
        }

        public void ResetMaxAssignmentCount()
        {
            lock (_gate)
                _maxAssignmentCount = _assigned.Count;
        }

        public ValueTask OnPartitionsAssignedAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            Assign(partitions);
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsRevokedAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            Revoke(partitions);
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsLostAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            Revoke(partitions);
            return ValueTask.CompletedTask;
        }

        public void Assign(IEnumerable<TopicPartition> partitions) => Update(partitions, assigned: true);

        public void Revoke(IEnumerable<TopicPartition> partitions) => Update(partitions, assigned: false);

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

                _maxAssignmentCount = Math.Max(_maxAssignmentCount, _assigned.Count);

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

    private static string GetRemoteAssignor(MemberProtocol protocol) => protocol switch
    {
        MemberProtocol.Kip848Uniform => "uniform",
        MemberProtocol.Kip848Range => "range",
        _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "Classic members use client-side assignment")
    };

    private static bool IsExpectedDekafCommitFailure(ErrorCode? errorCode) => errorCode is
        ErrorCode.IllegalGeneration or
        ErrorCode.UnknownMemberId or
        ErrorCode.RebalanceInProgress or
        ErrorCode.FencedMemberEpoch or
        ErrorCode.StaleMemberEpoch;

    private static bool IsExpectedConfluentCommitFailure(ConfluentKafka.ErrorCode errorCode) => errorCode is
        ConfluentKafka.ErrorCode.IllegalGeneration or
        ConfluentKafka.ErrorCode.UnknownMemberId or
        ConfluentKafka.ErrorCode.RebalanceInProgress or
        ConfluentKafka.ErrorCode.FencedMemberEpoch or
        ConfluentKafka.ErrorCode.StaleMemberEpoch;

    private enum MemberProtocol
    {
        Kip848Uniform,
        Kip848Range,
        ClassicEagerRange
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
