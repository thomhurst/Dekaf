using System.Globalization;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol;
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
    private const int CrashPhaseMessages = 12;
    private static readonly TimeSpan CrashClientReadyTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan MaxPollEvictionInterval = TimeSpan.FromSeconds(2);
    private static readonly HashSet<string> ExpectedCommitFailureNames = new(StringComparer.Ordinal)
    {
        nameof(ErrorCode.IllegalGeneration),
        nameof(ErrorCode.UnknownMemberId),
        nameof(ErrorCode.RebalanceInProgress),
        nameof(ErrorCode.FencedMemberEpoch),
        nameof(ErrorCode.StaleMemberEpoch)
    };

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
        var second = CreateSequenceRecord(offset: 1);

        oracle.Record("original", CreateSequenceRecord(offset: 0));
        oracle.Record("original", second);
        oracle.ObserveCommittedOffset(partition: 0, committedExclusive: 2);
        oracle.Record("restarted", second);

        await Assert.That(oracle.GetProgressBounds(0).ConfirmedCommit).IsEqualTo(2);
        await Assert.That(oracle.Violations.Any(static violation =>
            violation.Contains("replayed offset 1 below committed offset 2", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    [Timeout(600_000)]
    [SkipWhenNativeAot("Confluent.Kafka native delegate binding requires runtime reflection.")]
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

        await using var anchor = await CreateDekafMemberAsync(
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

        await using (var original = await CreateDekafMemberAsync(
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

        await using (var withinTimeoutRestart = await CreateDekafMemberAsync(
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

        await using var beyondTimeoutRestart = await CreateDekafMemberAsync(
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

    private static ObservedRecord CreateSequenceRecord(long offset) =>
        new("test-topic", 0, offset, offset.ToString(CultureInfo.InvariantCulture));

    [Test]
    [Timeout(600_000)]
    [SkipWhenNativeAot("Confluent.Kafka native delegate binding requires runtime reflection.")]
    public async Task ClassicEagerRange_ToKip848Uniform_OnlineMigrationPreservesSequencesAndCommittedProgress(
        CancellationToken cancellationToken)
    {
        await RunChurnScenarioAsync(
            "classic-range-to-kip848-uniform",
            MemberProtocol.ClassicEagerRange,
            MemberProtocol.Kip848Uniform,
            cancellationToken);
    }

    [Test]
    [Timeout(600_000)]
    [SkipWhenNativeAot("Confluent.Kafka native delegate binding requires runtime reflection.")]
    public async Task Kip848Uniform_ToClassicEagerRange_OnlineMigrationPreservesSequencesAndCommittedProgress(
        CancellationToken cancellationToken)
    {
        await RunChurnScenarioAsync(
            "kip848-uniform-to-classic-range",
            MemberProtocol.Kip848Uniform,
            MemberProtocol.ClassicEagerRange,
            cancellationToken,
            stopAnchorAfterJointPhase: true);
    }

    [Test]
    public async Task ConsumerMember_Stop_DisposesSharedResourcesWhenConsumerDisposalFails()
    {
        var member = new ThrowingDisposeConsumerMember();

        await Assert.That(member.StopAsync).Throws<InvalidOperationException>();
        await Assert.That(() => member.Allow(1)).Throws<ObjectDisposedException>();
    }

    [Test]
    [Timeout(600_000)]
    public async Task AbruptMemberLoss_AfterSessionExpiry_PreservesSequencesAndCommittedProgress(
        CancellationToken cancellationToken)
    {
        foreach (var assignor in new[] { "uniform", "range" })
        {
            await RunAbruptMemberLossScenarioAsync(assignor, cancellationToken);
        }
    }

    [Test]
    [Timeout(600_000)]
    public async Task MaxPollIntervalEvictsStalledMemberWithoutAdvancingItsCommits(
        CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: PartitionCount);
        var groupId = $"rebalance-max-poll-{Guid.NewGuid():N}";
        var oracle = new SequenceOracle(topic, PartitionCount, MessagesPerPartition, CommitInterval);

        await ProduceSequencedBacklogAsync(topic, cancellationToken);
        await using var admin = KafkaContainer.CreateAdminClient();
        await using var stalled = await CreateDekafMemberAsync(
            topic,
            groupId,
            "max-poll-stalled",
            "uniform",
            oracle,
            cancellationToken,
            maxPollInterval: MaxPollEvictionInterval);

        stalled.Allow(1);
        await stalled.WaitForAssignmentCountAsync(PartitionCount, cancellationToken);
        await stalled.WaitForObservedCountAsync(1, cancellationToken);

        await using var healthy = await CreateDekafMemberAsync(
            topic,
            groupId,
            "max-poll-healthy",
            "uniform",
            oracle,
            cancellationToken);

        healthy.Allow(1);
        await healthy.WaitForAnyAssignmentAsync(cancellationToken);
        await healthy.WaitForObservedCountAsync(1, cancellationToken);
        await healthy.WaitForAssignmentCountAsync(PartitionCount, cancellationToken);

        var descriptions = await admin.DescribeConsumerGroupsAsync([groupId], cancellationToken);
        var description = descriptions[groupId];
        await Assert.That(description.Members).Count().IsEqualTo(1);
        await Assert.That(description.Members[0].ClientId).IsEqualTo("max-poll-healthy");

        var staleCommit = await Assert.That(async () =>
                await stalled.CommitAsync(
                    new TopicPartitionOffset(topic, 0, MessagesPerPartition),
                    cancellationToken))
            .Throws<GroupException>();
        await Assert.That(IsExpectedCommitFailure(staleCommit!.ErrorCode)).IsTrue();

        healthy.Allow(PartitionCount * MessagesPerPartition * 2);
        await oracle.WaitForAllSequencesAsync(cancellationToken);
        await oracle.WaitForFinalCommitsAsync(cancellationToken);
        await healthy.StopAsync();
        await stalled.StopAsync();

        await Assert.That(oracle.UniqueCount).IsEqualTo(PartitionCount * MessagesPerPartition);
        await Assert.That(oracle.DuplicateCount).IsLessThanOrEqualTo(CommitInterval);
        var violations = oracle.Violations;
        await Assert.That(violations).IsEmpty()
            .Because(string.Join(Environment.NewLine, violations));
        await AssertCommittedOffsetsAsync(groupId, oracle, cancellationToken);
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
        CancellationToken cancellationToken,
        bool stopAnchorAfterJointPhase = false)
    {
        var (topic, groupId, oracle) = await CreateScenarioAsync(
            $"rebalance-chaos-{scenarioName}",
            cancellationToken);
        var jointGroupProtocol = anchorProtocol == MemberProtocol.ClassicEagerRange
            ? transientProtocol
            : anchorProtocol;

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
            var anchorCountBeforeJoin = anchor.ObservedCount;
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
            await AssertGroupModeAsync(admin, groupId, jointGroupProtocol, cancellationToken);
            await Assert.That(anchor.ObservedCount).IsEqualTo(anchorCountBeforeJoin)
                .Because("group joins must not consume records without an anchor permit");

            var anchorTarget = anchor.ObservedCount + JointPhaseMessagesPerMember;
            var transientTarget = transient.ObservedCount + JointPhaseMessagesPerMember;
            anchor.Allow(JointPhaseMessagesPerMember);
            transient.Allow(JointPhaseMessagesPerMember);

            await Task.WhenAll(
                anchor.WaitForObservedCountAsync(anchorTarget, cancellationToken),
                transient.WaitForObservedCountAsync(transientTarget, cancellationToken));

            if (stopAnchorAfterJointPhase)
            {
                // This one-way downgrade removes the anchor, so it deliberately completes
                // after one joint phase rather than starting another anchor churn cycle.
                await anchor.StopAsync();

                await transient.WaitForAssignmentCountAsync(PartitionCount, cancellationToken);
                await AssertGroupModeAsync(admin, groupId, transientProtocol, cancellationToken);
                await Assert.That(transient.ObservedCount).IsEqualTo(transientTarget)
                    .Because("Consumer-to-Classic migration must not consume records without a permit");
                await AssertCommittedOffsetsAsync(groupId, oracle, cancellationToken);

                var downgradeRecoveryTarget = transient.ObservedCount + RecoveryPhaseMessages;
                transient.Allow(RecoveryPhaseMessages);
                await transient.WaitForObservedCountAsync(downgradeRecoveryTarget, cancellationToken);

                await CompleteScenarioAsync(transient, groupId, oracle, cancellationToken);
                return;
            }

            await transient.StopAsync();

            await AssertCommittedOffsetsAsync(groupId, oracle, cancellationToken);
            await anchor.WaitForAssignmentCountAsync(PartitionCount, cancellationToken);
            await AssertGroupModeAsync(admin, groupId, anchorProtocol, cancellationToken);
            await Assert.That(anchor.ObservedCount).IsEqualTo(anchorTarget)
                .Because("group leaves must not consume records without an anchor permit");
            var recoveryTarget = anchor.ObservedCount + RecoveryPhaseMessages;
            anchor.Allow(RecoveryPhaseMessages);
            await anchor.WaitForObservedCountAsync(recoveryTarget, cancellationToken);
        }

        await CompleteScenarioAsync(anchor, groupId, oracle, cancellationToken);
    }

    private async Task CompleteScenarioAsync(
        IConsumerMember survivor,
        string groupId,
        SequenceOracle oracle,
        CancellationToken cancellationToken)
    {
        survivor.Allow(PartitionCount * MessagesPerPartition * 2);
        await oracle.WaitForAllSequencesAsync(cancellationToken);
        await oracle.WaitForFinalCommitsAsync(cancellationToken);
        await survivor.StopAsync();

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

    private async Task RunAbruptMemberLossScenarioAsync(
        string assignor,
        CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: PartitionCount);
        var groupId = $"rebalance-crash-{assignor}-{Guid.NewGuid():N}";
        var oracle = new SequenceOracle(topic, PartitionCount, MessagesPerPartition, CommitInterval);

        await using var admin = KafkaContainer.CreateAdminClient();
        await admin.IncrementalAlterConfigsAsync(
            new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
            {
                [ConfigResource.Topic(topic)] = [ConfigAlter.Set("retention.ms", "600000")]
            },
            cancellationToken: cancellationToken);
        await ProduceSequencedBacklogAsync(topic, cancellationToken);

        await using var survivor = await CreateDekafMemberAsync(
            topic,
            groupId,
            $"{assignor}-survivor",
            assignor,
            oracle,
            cancellationToken);

        survivor.Allow(1);
        await survivor.WaitForAssignmentCountAsync(PartitionCount, cancellationToken);
        await survivor.WaitForObservedCountAsync(1, cancellationToken);

        var crashedClientId = $"{assignor}-crashed";
        await using var crashedMember = ConsumerGroupCrashClientProcess.Start(
            KafkaContainer.BootstrapServers,
            topic,
            groupId,
            crashedClientId,
            assignor);
        var crashedObservation = await crashedMember.WaitUntilReadyAsync(
            CrashClientReadyTimeout,
            cancellationToken);

        await survivor.WaitForAssignmentCountBelowAsync(PartitionCount, cancellationToken);
        var crashedPartition = new TopicPartition(topic, crashedObservation.Partition);
        await survivor.WaitForPartitionUnassignedAsync(crashedPartition, cancellationToken);
        oracle.Record(
            crashedClientId,
            crashedObservation.Partition,
            crashedObservation.CommittedOffset,
            crashedObservation.CommittedValue);
        oracle.Record(
            crashedClientId,
            crashedObservation.Partition,
            crashedObservation.Offset,
            crashedObservation.Value);
        await AssertGroupMemberPresenceAsync(
            admin,
            groupId,
            crashedClientId,
            expected: true,
            cancellationToken);
        await AssertObservationIsUncommittedAsync(
            admin,
            groupId,
            topic,
            crashedObservation,
            cancellationToken);

        var survivorJointTarget = survivor.ObservedCount + JointPhaseMessagesPerMember;
        survivor.Allow(JointPhaseMessagesPerMember);
        await survivor.WaitForObservedCountAsync(survivorJointTarget, cancellationToken);

        var processingAcrossCrash = survivor.PauseNextRecord();
        var crashPhaseProgressTarget = survivor.ObservedCount + CrashPhaseMessages;
        survivor.Allow(CrashPhaseMessages);
        await processingAcrossCrash.WaitUntilPausedAsync(cancellationToken);

        var committedAtCrash = await CaptureAndAssertCommittedOffsetsAsync(
            admin,
            groupId,
            oracle,
            cancellationToken);
        oracle.BeginCrashWindow(committedAtCrash);
        await Assert.That(oracle.IsInCrashWindow(
                crashedObservation.Partition,
                crashedObservation.Offset))
            .IsTrue()
            .Because("the child record must be inside the committed/processed window captured at kill");

        await crashedMember.TerminateAsync(cancellationToken);

        // A force-killed KIP-848 member remains registered until its broker-side
        // session expires; no LeaveGroup or terminal heartbeat removed it.
        await AssertGroupMemberPresenceAsync(
            admin,
            groupId,
            crashedClientId,
            expected: true,
            cancellationToken);

        processingAcrossCrash.Release();
        await survivor.WaitForObservedCountAsync(crashPhaseProgressTarget, cancellationToken);
        await AssertGroupMemberPresenceAsync(
            admin,
            groupId,
            crashedClientId,
            expected: true,
            cancellationToken);

        // This waiter is created only after the child took the partition and the
        // survivor reported it revoked, so an old full-assignment signal cannot
        // masquerade as recovery.
        await survivor.WaitForPartitionAssignedAsync(crashedPartition, cancellationToken);
        await survivor.WaitForAssignmentCountAsync(PartitionCount, cancellationToken);

        // Start the post-expiry admin probe only after full reassignment shows the
        // coordinator transition has settled, then reuse it for both checkpoints.
        await using var postExpiryAdmin = KafkaContainer.CreateAdminClient();
        await AssertGroupMemberPresenceAsync(
            postExpiryAdmin,
            groupId,
            crashedClientId,
            expected: false,
            cancellationToken);
        var committedAfterExpiry = await CaptureAndAssertCommittedOffsetsAsync(
            postExpiryAdmin,
            groupId,
            oracle,
            cancellationToken,
            committedAtCrash);

        var remainingPermits = PartitionCount * MessagesPerPartition * 2;
        survivor.Allow(remainingPermits);
        await oracle.WaitForAllSequencesAsync(cancellationToken);
        await oracle.WaitForFinalCommitsAsync(cancellationToken);

        await Assert.That(oracle.WasDuplicatedAfterCrash(
                crashedObservation.Partition,
                crashedObservation.Offset))
            .IsTrue()
            .Because("the crashed member's last uncommitted record must be replayed after session expiry");

        await survivor.StopAsync();

        await Assert.That(oracle.UniqueCount).IsEqualTo(PartitionCount * MessagesPerPartition);
        var violations = oracle.Violations;
        await Assert.That(violations).IsEmpty()
            .Because(string.Join(Environment.NewLine, violations));

        for (var partition = 0; partition < PartitionCount; partition++)
        {
            await Assert.That(oracle.GetSeenOffsets(partition))
                .IsEquivalentTo(Enumerable.Range(0, MessagesPerPartition).Select(static value => (long)value));
        }

        await CaptureAndAssertCommittedOffsetsAsync(
            postExpiryAdmin,
            groupId,
            oracle,
            cancellationToken,
            committedAfterExpiry);
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

    private async Task<DekafConsumerMember> CreateDekafMemberAsync(
        string topic,
        string groupId,
        string clientId,
        string assignor,
        SequenceOracle oracle,
        CancellationToken cancellationToken,
        string? groupInstanceId = null,
        TimeSpan? maxPollInterval = null)
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

        if (maxPollInterval is { } interval)
            builder.WithMaxPollInterval(interval);

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

    private static async Task<long[]> CaptureAndAssertCommittedOffsetsAsync(
        IAdminClient admin,
        string groupId,
        SequenceOracle oracle,
        CancellationToken cancellationToken,
        IReadOnlyList<long>? previousBrokerOffsets = null)
    {
        var committedOffsets = await admin.ListConsumerGroupOffsetsAsync(groupId, cancellationToken);
        var actualOffsets = new long[PartitionCount];

        for (var partition = 0; partition < PartitionCount; partition++)
        {
            var (explicitCommit, processedExclusive) = oracle.GetProgressBounds(partition);
            var actual = committedOffsets.GetValueOrDefault(
                new TopicPartition(oracle.Topic, partition),
                0);
            actualOffsets[partition] = actual;
            await Assert.That(actual)
                .IsLessThanOrEqualTo(processedExclusive)
                .Because($"partition {partition} broker commit cannot jump ahead of processed records");
            await Assert.That(actual)
                .IsGreaterThanOrEqualTo(explicitCommit)
                .Because($"partition {partition} broker commit cannot trail a successful explicit commit");

            if (previousBrokerOffsets is not null)
            {
                await Assert.That(actual)
                    .IsGreaterThanOrEqualTo(previousBrokerOffsets[partition])
                    .Because($"partition {partition} broker commit cannot rewind between checkpoints");
            }

            oracle.ObserveCommittedOffset(partition, actual);
        }

        return actualOffsets;
    }

    private static async Task AssertObservationIsUncommittedAsync(
        IAdminClient admin,
        string groupId,
        string topic,
        ConsumerGroupCrashObservation observation,
        CancellationToken cancellationToken)
    {
        var committedOffsets = await admin.ListConsumerGroupOffsetsAsync(groupId, cancellationToken);
        var committedExclusive = committedOffsets.GetValueOrDefault(
            new TopicPartition(topic, observation.Partition),
            0);

        await Assert.That(observation.Offset)
            .IsEqualTo(observation.CommittedOffset + 1)
            .Because("the child must commit one record immediately before its crash record");
        await Assert.That(committedExclusive)
            .IsEqualTo(observation.Offset)
            .Because("the crash record must be the first offset after the child's durable commit");
    }

    private static async Task AssertGroupMemberPresenceAsync(
        IAdminClient admin,
        string groupId,
        string clientId,
        bool expected,
        CancellationToken cancellationToken)
    {
        var descriptions = await admin.DescribeConsumerGroupsAsync([groupId], cancellationToken);
        await Assert.That(descriptions).ContainsKey(groupId);

        var description = descriptions[groupId];
        var containsMember = description.Members.Any(member =>
            string.Equals(member.ClientId, clientId, StringComparison.Ordinal));

        await Assert.That(containsMember)
            .IsEqualTo(expected)
            .Because($"group members: {string.Join(", ", description.Members.Select(member => member.ClientId))}");
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
        int MaxAssignmentCount { get; }
        void Allow(int count);
        void ResetMaxAssignmentCount();
        RecordProcessingBarrier PauseNextRecord();
        Task WaitForAnyAssignmentAsync(CancellationToken cancellationToken);
        Task WaitForAssignmentCountAsync(int count, CancellationToken cancellationToken);
        Task WaitForAssignmentCountBelowAsync(int count, CancellationToken cancellationToken);
        Task WaitForPartitionAssignedAsync(TopicPartition partition, CancellationToken cancellationToken);
        Task WaitForPartitionUnassignedAsync(TopicPartition partition, CancellationToken cancellationToken);
        Task WaitForObservedCountAsync(int count, CancellationToken cancellationToken);
        Task StopAsync();
    }

    private abstract class ConsumerMemberBase : IConsumerMember
    {
        private readonly string _memberDescription;
        private readonly AssignmentTracker _assignments;
        private readonly CancellationTokenSource _stopping;
        private readonly SemaphoreSlim _permits = new(0, int.MaxValue);
        private readonly AsyncCounter _observed = new();
        private Task? _runTask;
        private RecordProcessingBarrier? _nextRecordBarrier;
        private int _stopped;

        protected ConsumerMemberBase(
            string memberDescription,
            string clientId,
            AssignmentTracker assignments,
            SequenceOracle oracle,
            CancellationToken cancellationToken)
        {
            _memberDescription = memberDescription;
            ClientId = clientId;
            _assignments = assignments;
            Oracle = oracle;
            _stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        protected string ClientId { get; }
        protected SequenceOracle Oracle { get; }
        protected CancellationToken StoppingToken => _stopping.Token;
        protected SemaphoreSlim Permits => _permits;
        protected int PermitCount => _permits.CurrentCount;

        public int ObservedCount => _observed.Value;

        public int MaxAssignmentCount => _assignments.MaxAssignmentCount;

        public void Allow(int count) => _permits.Release(count);

        public void ResetMaxAssignmentCount() => _assignments.ResetMaxAssignmentCount();

        public RecordProcessingBarrier PauseNextRecord()
        {
            var barrier = new RecordProcessingBarrier();
            if (Interlocked.CompareExchange(ref _nextRecordBarrier, barrier, null) is not null)
                throw new InvalidOperationException("A record-processing barrier is already pending.");

            return barrier;
        }

        public Task WaitForAnyAssignmentAsync(CancellationToken cancellationToken) =>
            WaitForSignalOrCompletionAsync(
                _assignments.WaitForAnyAsync(cancellationToken),
                GetRunTask(),
                $"{_memberDescription} completed before receiving an assignment",
                cancellationToken);

        public Task WaitForAssignmentCountAsync(int count, CancellationToken cancellationToken) =>
            WaitForSignalOrCompletionAsync(
                _assignments.WaitForCountAsync(count, cancellationToken),
                GetRunTask(),
                $"{_memberDescription} completed before receiving {count} partitions",
                cancellationToken);

        public Task WaitForAssignmentCountBelowAsync(int count, CancellationToken cancellationToken) =>
            WaitForSignalOrCompletionAsync(
                _assignments.WaitForCountBelowAsync(count, cancellationToken),
                GetRunTask(),
                $"{_memberDescription} completed before its assignment dropped below {count} partitions",
                cancellationToken);

        public Task WaitForPartitionAssignedAsync(
            TopicPartition partition,
            CancellationToken cancellationToken) =>
            WaitForSignalOrCompletionAsync(
                _assignments.WaitForPartitionAssignedAsync(partition, cancellationToken),
                GetRunTask(),
                $"{_memberDescription} completed before {partition} was assigned",
                cancellationToken);

        public Task WaitForPartitionUnassignedAsync(
            TopicPartition partition,
            CancellationToken cancellationToken) =>
            WaitForSignalOrCompletionAsync(
                _assignments.WaitForPartitionUnassignedAsync(partition, cancellationToken),
                GetRunTask(),
                $"{_memberDescription} completed before {partition} was revoked",
                cancellationToken);

        public Task WaitForObservedCountAsync(int count, CancellationToken cancellationToken) =>
            WaitForSignalOrCompletionAsync(
                _observed.WaitForAsync(count, cancellationToken),
                GetRunTask(),
                $"{_memberDescription} completed before observing {count} records",
                cancellationToken);

        public async Task StopAsync()
        {
            if (Interlocked.Exchange(ref _stopped, 1) != 0)
                return;

            try
            {
                await _stopping.CancelAsync();
                await GetRunTask();
            }
            finally
            {
                try
                {
                    await DisposeConsumerAsync();
                }
                finally
                {
                    _permits.Dispose();
                    _stopping.Dispose();
                }
            }
        }

        public async ValueTask DisposeAsync() => await StopAsync();

        protected void StartRunTask(bool useLongRunningThread = false)
        {
            _runTask = useLongRunningThread
                ? Task.Factory.StartNew(
                        RunAsync,
                        CancellationToken.None,
                        TaskCreationOptions.LongRunning,
                        TaskScheduler.Default)
                    .Unwrap()
                : Task.Run(RunAsync, CancellationToken.None);
        }

        protected void IncrementObserved() => _observed.Increment();

        protected async Task WaitForPendingRecordBarrierAsync()
        {
            var barrier = Interlocked.Exchange(ref _nextRecordBarrier, null);
            if (barrier is not null)
                await barrier.PauseAsync(StoppingToken);
        }

        protected abstract Task RunAsync();
        protected abstract ValueTask DisposeConsumerAsync();

        private Task GetRunTask() => _runTask
            ?? throw new InvalidOperationException($"{_memberDescription} run task has not started");
    }

    private readonly record struct ObservedRecord(
        string Topic,
        int Partition,
        long Offset,
        string? Value);

    private sealed class RecordProcessingBarrier
    {
        private readonly TaskCompletionSource _paused = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _released = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitUntilPausedAsync(CancellationToken cancellationToken) =>
            _paused.Task.WaitAsync(cancellationToken);

        public void Release() => _released.TrySetResult();

        public async Task PauseAsync(CancellationToken cancellationToken)
        {
            _paused.TrySetResult();
            await _released.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class DekafConsumerMember : ConsumerMemberBase
    {
        private readonly IKafkaConsumer<string, string> _consumer;

        public DekafConsumerMember(
            string clientId,
            IKafkaConsumer<string, string> consumer,
            AssignmentTracker assignments,
            SequenceOracle oracle,
            CancellationToken cancellationToken)
            : base("Consumer", clientId, assignments, oracle, cancellationToken)
        {
            _consumer = consumer;
            StartRunTask();
        }

        protected override async Task RunAsync()
        {
            try
            {
                await using var enumerator = _consumer
                    .ConsumeAsync(StoppingToken)
                    .GetAsyncEnumerator(StoppingToken);

                while (!Oracle.IsComplete)
                {
                    await Permits.WaitAsync(StoppingToken);
                    if (!await enumerator.MoveNextAsync())
                    {
                        StoppingToken.ThrowIfCancellationRequested();
                        throw new InvalidOperationException("Consumer stream completed before the sequence oracle");
                    }

                    var result = enumerator.Current;
                    await WaitForPendingRecordBarrierAsync();

                    var record = new ObservedRecord(
                        result.Topic,
                        result.Partition,
                        result.Offset,
                        result.Value);
                    Oracle.Record(ClientId, record);
                    await Oracle.CommitIfNeededAsync(record, TryCommitAsync, StoppingToken);
                    IncrementObserved();
                }
            }
            catch (OperationCanceledException) when (StoppingToken.IsCancellationRequested)
            {
            }
        }

        protected override ValueTask DisposeConsumerAsync() => _consumer.DisposeAsync();

        public ValueTask CommitAsync(
            TopicPartitionOffset offset,
            CancellationToken cancellationToken) =>
            _consumer.CommitAsync([offset], cancellationToken);

        private async ValueTask<bool> TryCommitAsync(
            TopicPartitionOffset offset,
            CancellationToken cancellationToken)
        {
            try
            {
                await _consumer.CommitAsync([offset], cancellationToken);
                return true;
            }
            catch (GroupException exception) when (
                exception.ErrorCode is { } errorCode && IsExpectedCommitFailure(errorCode))
            {
                return false;
            }
        }
    }

    private sealed class ClassicConsumerMember : ConsumerMemberBase
    {
        private static readonly TimeSpan EventPollInterval = TimeSpan.FromMilliseconds(50);

        private readonly ConfluentKafka.IConsumer<string, string> _consumer;

        public ClassicConsumerMember(
            string clientId,
            ConfluentKafka.IConsumer<string, string> consumer,
            AssignmentTracker assignments,
            SequenceOracle oracle,
            CancellationToken cancellationToken)
            : base("Classic consumer", clientId, assignments, oracle, cancellationToken)
        {
            _consumer = consumer;
            StartRunTask(useLongRunningThread: true);
        }

        protected override async Task RunAsync()
        {
            try
            {
                while (!Oracle.IsComplete)
                {
                    StoppingToken.ThrowIfCancellationRequested();

                    if (Permits.Wait(0))
                    {
                        ResumeCurrentAssignment();
                        var result = _consumer.Consume(StoppingToken);
                        await ObserveAsync(result);
                        if (PermitCount == 0)
                            PauseCurrentAssignment();
                        continue;
                    }

                    PauseCurrentAssignment();
                    var eventResult = _consumer.Consume(EventPollInterval);
                    if (eventResult is null)
                        continue;

                    // A poll can install a new eager assignment and return its first record before
                    // the next pause. Rewind it so permit-gated consumption observes it later.
                    PauseCurrentAssignment();
                    RewindIdlePollResult(eventResult);
                }
            }
            catch (OperationCanceledException) when (StoppingToken.IsCancellationRequested)
            {
            }
            finally
            {
                _consumer.Close();
            }
        }

        protected override ValueTask DisposeConsumerAsync()
        {
            _consumer.Dispose();
            return ValueTask.CompletedTask;
        }

        private async Task ObserveAsync(ConfluentKafka.ConsumeResult<string, string> result)
        {
            await WaitForPendingRecordBarrierAsync();

            var record = new ObservedRecord(
                result.Topic,
                result.Partition.Value,
                result.Offset.Value,
                result.Message.Value);
            Oracle.Record(ClientId, record);
            await Oracle.CommitIfNeededAsync(record, TryCommitAsync, StoppingToken);
            IncrementObserved();
        }

        private void RewindIdlePollResult(ConfluentKafka.ConsumeResult<string, string> result)
        {
            try
            {
                _consumer.Seek(result.TopicPartitionOffset);
            }
            catch (Exception exception) when (
                exception is ConfluentKafka.KafkaException or InvalidOperationException)
            {
                if (!_consumer.Assignment.Contains(result.TopicPartition))
                    return;

                throw new InvalidOperationException(
                    $"Classic consumer could not rewind {result.TopicPartitionOffset} after an idle poll",
                    exception);
            }
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
            catch (ConfluentKafka.TopicPartitionOffsetException exception)
                when (ContainsOnlyExpectedConfluentCommitFailures(exception.Results))
            {
                return ValueTask.FromResult(false);
            }
            catch (ConfluentKafka.KafkaException exception)
                when (IsExpectedCommitFailure(exception.Error.Code))
            {
                return ValueTask.FromResult(false);
            }
        }
    }

    private sealed class ThrowingDisposeConsumerMember : ConsumerMemberBase
    {
        public ThrowingDisposeConsumerMember()
            : base(
                "Test consumer",
                "test-client",
                new AssignmentTracker(),
                new SequenceOracle("test-topic", 1, 1, 1),
                CancellationToken.None)
        {
            StartRunTask();
        }

        protected override Task RunAsync() => Task.CompletedTask;

        protected override ValueTask DisposeConsumerAsync() =>
            ValueTask.FromException(new InvalidOperationException("Expected disposal failure"));
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

            Record(clientId, result.Partition, result.Offset, result.Value);
        }

        public void Record(
            string clientId,
            int partition,
            long offset,
            string? value)
        {
            if (partition < 0 || partition >= _partitions.Length)
            {
                AddViolation($"Unexpected partition {partition}");
                return;
            }

            if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence))
            {
                AddViolation($"Partition {partition}: invalid sequence payload '{value}'");
                return;
            }

            if (sequence != offset)
                AddViolation($"Partition {partition}: payload {sequence} does not match offset {offset}");

            if (offset < 0 || offset >= _messagesPerPartition)
            {
                AddViolation($"Partition {partition}: offset {offset} is outside seeded range");
                return;
            }

            var state = _partitions[partition];
            var unique = false;
            lock (state.Gate)
            {
                if (state.Seen.Add(offset))
                {
                    unique = true;
                    if (offset != state.NextExpected)
                    {
                        AddViolation(
                            $"Partition {partition}: observed new offset {offset} before {state.NextExpected}");
                    }

                    while (state.Seen.Contains(state.NextExpected))
                        state.NextExpected++;
                }
                else
                {
                    Interlocked.Increment(ref _duplicateCount);
                    if (state.CrashWindowActive)
                    {
                        state.PostCrashDuplicates.Add(offset);
                        if (offset < state.CrashCommittedExclusive ||
                            offset >= state.CrashProcessedExclusive)
                        {
                            AddViolation(
                                $"{clientId}, partition {partition}: post-crash replay {offset} outside " +
                                $"uncommitted window [{state.CrashCommittedExclusive}, " +
                                $"{state.CrashProcessedExclusive})");
                        }
                    }

                    if (offset < state.CommittedExclusive)
                    {
                        AddViolation(
                            $"{clientId}, partition {partition}: replayed offset {offset} below committed offset {state.CommittedExclusive}");
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

        public void BeginCrashWindow(IReadOnlyList<long> brokerCommittedOffsets)
        {
            if (brokerCommittedOffsets.Count != _partitions.Length)
                throw new ArgumentException("A committed offset is required for every partition.", nameof(brokerCommittedOffsets));

            for (var partition = 0; partition < _partitions.Length; partition++)
            {
                var state = _partitions[partition];
                lock (state.Gate)
                {
                    state.CrashCommittedExclusive = brokerCommittedOffsets[partition];
                    state.CrashProcessedExclusive = state.NextExpected;
                    state.CrashWindowActive = true;
                }
            }
        }

        public bool IsInCrashWindow(int partition, long offset)
        {
            var state = _partitions[partition];
            lock (state.Gate)
            {
                return state.CrashWindowActive &&
                       offset >= state.CrashCommittedExclusive &&
                       offset < state.CrashProcessedExclusive;
            }
        }

        public bool WasDuplicatedAfterCrash(int partition, long offset)
        {
            var state = _partitions[partition];
            lock (state.Gate)
                return state.PostCrashDuplicates.Contains(offset);
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
            public HashSet<long> PostCrashDuplicates { get; } = [];
            public long NextExpected { get; set; }
            public long CommittedExclusive { get; set; }
            public long CrashCommittedExclusive { get; set; }
            public long CrashProcessedExclusive { get; set; }
            public bool CrashWindowActive { get; set; }
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
            WaitForAsync(static assigned => assigned.Count > 0, cancellationToken);

        public Task WaitForCountAsync(int count, CancellationToken cancellationToken) =>
            WaitForAsync(assigned => assigned.Count == count, cancellationToken);

        public Task WaitForCountBelowAsync(int count, CancellationToken cancellationToken) =>
            WaitForAsync(assigned => assigned.Count < count, cancellationToken);

        public Task WaitForPartitionAssignedAsync(
            TopicPartition partition,
            CancellationToken cancellationToken) =>
            WaitForAsync(assigned => assigned.Contains(partition), cancellationToken);

        public Task WaitForPartitionUnassignedAsync(
            TopicPartition partition,
            CancellationToken cancellationToken) =>
            WaitForAsync(assigned => !assigned.Contains(partition), cancellationToken);

        private Task WaitForAsync(
            Func<HashSet<TopicPartition>, bool> predicate,
            CancellationToken cancellationToken)
        {
            Task signal;
            lock (_gate)
            {
                if (predicate(_assigned))
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
                    if (!waiter.Predicate(_assigned))
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
            Func<HashSet<TopicPartition>, bool> Predicate,
            TaskCompletionSource Completion);
    }

    private static string GetRemoteAssignor(MemberProtocol protocol) => protocol switch
    {
        MemberProtocol.Kip848Uniform => "uniform",
        MemberProtocol.Kip848Range => "range",
        _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "Classic members use client-side assignment")
    };

    private static bool IsExpectedCommitFailure<TError>(TError errorCode)
        where TError : struct, Enum =>
        ExpectedCommitFailureNames.Contains(errorCode.ToString());

    private static bool ContainsOnlyExpectedConfluentCommitFailures(
        IReadOnlyCollection<ConfluentKafka.TopicPartitionOffsetError> results) =>
        results.Any(static result => result.Error.IsError) &&
        results.All(static result =>
            !result.Error.IsError || IsExpectedCommitFailure(result.Error.Code));

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
