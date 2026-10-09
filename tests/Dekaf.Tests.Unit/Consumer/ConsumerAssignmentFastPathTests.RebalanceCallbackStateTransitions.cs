using Dekaf.Consumer;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// One row per transition of the OnPartitionsAssigned seek/pause state machine documented on
/// <c>KafkaConsumer._pendingRebalanceSeeks</c>. In every row the first OnPartitionsAssigned that
/// announces partition 1 pauses it and seeks it to 42 (its committed offset is 20), unless the row
/// says otherwise. Every row ends with no staged or unacknowledged seek left behind.
/// </summary>
public sealed partial class ConsumerAssignmentFastPathTests
{
    public enum CallbackTransition
    {
        NewPartitionSynced,
        ReassignedSynced,
        RevokedBeforeSync,
        RevokedThenReassignedBeforeSync,
        LostThenReassignedBeforeSync,
        ResumedBeforeSync,
        ResumedAfterSync,
        UnsubscribedBeforeSync,
        AssignedManuallyBeforeSync,
        RevokedThenAssignedManuallyBeforeSync,
        UnassignedBeforeSync,
        IncrementallyAssignedBeforeSync,
        SyncCancelledThenRetried,
        SyncSupersededThenRetried,
        PreviousOwnershipPauseReassigned,
        ClosedBeforeSync,
        CancelledAfterPublishThenAssignedManually,
        CancelledAfterPublishThenResubscribed,
        CancelledAfterPublishThenClosed,
        SupersededByClassificationBeforeAck,
        SupersededByReassignmentBeforeAck
    }

    [Test]
    [Timeout(60_000)]
    [Arguments(CallbackTransition.NewPartitionSynced, true, 42L)]
    [Arguments(CallbackTransition.ReassignedSynced, true, 42L)]
    [Arguments(CallbackTransition.RevokedBeforeSync, false, null)]
    [Arguments(CallbackTransition.RevokedThenReassignedBeforeSync, false, 20L)]
    [Arguments(CallbackTransition.LostThenReassignedBeforeSync, false, 20L)]
    [Arguments(CallbackTransition.ResumedBeforeSync, false, 42L)]
    [Arguments(CallbackTransition.ResumedAfterSync, false, 42L)]
    [Arguments(CallbackTransition.UnsubscribedBeforeSync, false, 20L)]
    [Arguments(CallbackTransition.AssignedManuallyBeforeSync, false, null)]
    [Arguments(CallbackTransition.RevokedThenAssignedManuallyBeforeSync, false, null)]
    [Arguments(CallbackTransition.UnassignedBeforeSync, false, null)]
    [Arguments(CallbackTransition.IncrementallyAssignedBeforeSync, false, null)]
    [Arguments(CallbackTransition.SyncCancelledThenRetried, true, 42L)]
    [Arguments(CallbackTransition.SyncSupersededThenRetried, true, 42L)]
    [Arguments(CallbackTransition.PreviousOwnershipPauseReassigned, false, 20L)]
    [Arguments(CallbackTransition.ClosedBeforeSync, false, null)]
    [Arguments(CallbackTransition.CancelledAfterPublishThenAssignedManually, false, null)]
    [Arguments(CallbackTransition.CancelledAfterPublishThenResubscribed, false, 20L)]
    [Arguments(CallbackTransition.CancelledAfterPublishThenClosed, false, null)]
    [Arguments(CallbackTransition.SupersededByClassificationBeforeAck, true, 42L)]
    [Arguments(CallbackTransition.SupersededByReassignmentBeforeAck, false, 20L)]
    public async Task RebalanceCallbackStateTransitions(
        CallbackTransition transition,
        bool expectPaused,
        long? expectPosition,
        CancellationToken testTimeout)
    {
        var aba = transition is CallbackTransition.ReassignedSynced
            or CallbackTransition.SyncCancelledThenRetried
            or CallbackTransition.SyncSupersededThenRetried
            or CallbackTransition.PreviousOwnershipPauseReassigned;
        ConsumerGroupHeartbeatResponse[] script = transition switch
        {
            _ when aba => [AssignedResponse(1, 0, 1), AssignedResponse(2, 0), AssignedResponse(3, 0, 1)],
            CallbackTransition.RevokedBeforeSync or CallbackTransition.RevokedThenAssignedManuallyBeforeSync =>
                [AssignedResponse(1, 0), AssignedResponse(2, 0, 1), AssignedResponse(3, 0)],
            CallbackTransition.RevokedThenReassignedBeforeSync =>
                [AssignedResponse(1, 0), AssignedResponse(2, 0, 1), AssignedResponse(3, 0), AssignedResponse(4, 0, 1)],
            CallbackTransition.LostThenReassignedBeforeSync =>
                [AssignedResponse(1, 0), AssignedResponse(2, 0, 1), FencedResponse(), AssignedResponse(1, 0, 1)],
            _ => [AssignedResponse(1, 0), AssignedResponse(2, 0, 1)]
        };
        var heartbeats = transition switch
        {
            _ when aba => 2,
            CallbackTransition.RevokedBeforeSync or CallbackTransition.RevokedThenAssignedManuallyBeforeSync => 2,
            CallbackTransition.RevokedThenReassignedBeforeSync => 3,
            CallbackTransition.LostThenReassignedBeforeSync => 2,
            _ => 1
        };

        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessCoreAsync(
            listener,
            consumerAwareListener: null,
            script,
            newPartitionsReset: transition == CallbackTransition.SupersededByClassificationBeforeAck
                ? AutoOffsetReset.Earliest
                : null);
        var consumer = harness.Consumer;
        var acted = false;
        if (transition != CallbackTransition.PreviousOwnershipPauseReassigned)
        {
            listener.OnAssigned = partitions =>
            {
                if (acted || !partitions.Contains(Partition1))
                    return;

                acted = true;
                consumer.Pause(Partition1);
                consumer.Seek(new TopicPartitionOffset(Partition1.Topic, Partition1.Partition, 42));
            };
        }
        else
        {
            consumer.Pause(Partition1);
        }

        for (var i = 0; i < heartbeats; i++)
            await harness.HeartbeatAsync();

        var sync = true;
        switch (transition)
        {
            case CallbackTransition.ResumedBeforeSync:
                consumer.Resume(Partition1);
                break;
            case CallbackTransition.UnsubscribedBeforeSync:
                consumer.Unsubscribe();
                consumer.Subscribe("test-topic");
                break;
            case CallbackTransition.AssignedManuallyBeforeSync:
            case CallbackTransition.RevokedThenAssignedManuallyBeforeSync:
                consumer.Assign(new TopicPartition("test-topic", 0), Partition1);
                sync = false;
                break;
            case CallbackTransition.UnassignedBeforeSync:
                consumer.Unassign();
                sync = false;
                break;
            case CallbackTransition.IncrementallyAssignedBeforeSync:
                consumer.IncrementalAssign([new TopicPartitionOffset("test-topic", 0, 5)]);
                sync = false;
                break;
            case CallbackTransition.SyncCancelledThenRetried:
                await CancelFirstSyncDuringOffsetFetchAsync(harness);
                break;
            case CallbackTransition.SyncSupersededThenRetried:
                SupersedeFirstSyncPasses(harness, passes: 1);
                break;
            case CallbackTransition.ClosedBeforeSync:
                await consumer.CloseAsync(testTimeout);
                sync = false;
                break;
            case CallbackTransition.CancelledAfterPublishThenAssignedManually:
                await CancelFirstSyncDuringOffsetFetchAsync(harness);
                consumer.Assign(new TopicPartition("test-topic", 0), Partition1);
                sync = false;
                break;
            case CallbackTransition.CancelledAfterPublishThenResubscribed:
                await CancelFirstSyncDuringOffsetFetchAsync(harness);
                consumer.Unsubscribe();
                consumer.Subscribe("test-topic");
                break;
            case CallbackTransition.CancelledAfterPublishThenClosed:
                await CancelFirstSyncDuringOffsetFetchAsync(harness);
                await consumer.CloseAsync(testTimeout);
                sync = false;
                break;
            case CallbackTransition.SupersededByClassificationBeforeAck:
            case CallbackTransition.SupersededByReassignmentBeforeAck:
                // A heartbeat publishes a newer version after the pass's last version check and
                // before its acknowledgement; the next sync must still start from the callback seek.
                var coordinator = GetCoordinator(consumer);
                var superseded = false;
                consumer.BeforeAssignmentSyncAcknowledgedForTest = () =>
                {
                    if (superseded)
                        return;
                    superseded = true;
                    if (transition == CallbackTransition.SupersededByClassificationBeforeAck)
                        coordinator.MarkNewlyExpandedForTest(Partition1);
                    else
                        coordinator.RevokeAndReassignForTest(Partition1);
                };
                await consumer.EnsureAssignmentAsync(CancellationToken.None);
                await Assert.That(superseded).IsTrue();
                break;
        }

        if (sync)
            await consumer.EnsureAssignmentAsync(CancellationToken.None);
        if (transition == CallbackTransition.ResumedAfterSync)
            consumer.Resume(Partition1);

        if (transition != CallbackTransition.PreviousOwnershipPauseReassigned)
            await Assert.That(acted).IsTrue();
        await Assert.That(consumer.Paused.Contains(Partition1)).IsEqualTo(expectPaused);
        if (expectPosition is not null)
            await Assert.That(consumer.GetPosition(Partition1)).IsEqualTo(expectPosition);
        else if (sync)
            await Assert.That(consumer.Assignment).DoesNotContain(Partition1);
        await Assert.That(consumer.PendingRebalanceSeekCountForTest).IsEqualTo(0);
        await Assert.That(consumer.UnacknowledgedAppliedRebalanceSeekCountForTest).IsEqualTo(0);
    }

    public enum AbandonInCallback
    {
        Assign,
        Unassign,
        IncrementalAssign,
        Unsubscribe,
        SubscribeTopics,
        SubscribePattern
    }

    /// <summary>
    /// A callback that abandons the group assignment it announced (switches to manual assignment or
    /// changes the subscription) ends its staging: later seeks, pauses, resumes and position reads in
    /// that callback act on the consumer directly.
    /// </summary>
    [Test]
    [Arguments(AbandonInCallback.Assign)]
    [Arguments(AbandonInCallback.Unassign)]
    [Arguments(AbandonInCallback.IncrementalAssign)]
    [Arguments(AbandonInCallback.Unsubscribe)]
    [Arguments(AbandonInCallback.SubscribeTopics)]
    [Arguments(AbandonInCallback.SubscribePattern)]
    public async Task RebalanceCallbackStateTransitions_AbandonInsideCallback_EndsStaging(AbandonInCallback abandon)
    {
        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessAsync(
            listener,
            AssignedResponse(1, 0),
            AssignedResponse(2, 0, 1));
        var consumer = harness.Consumer;
        var coordinator = GetCoordinator(consumer);
        var partition0 = new TopicPartition("test-topic", 0);

        bool? stagingAfterAbandon = null;
        long? positionAfterSeek = null;
        var pausedAfterResume = true;
        listener.OnAssigned = partitions =>
        {
            if (!partitions.Contains(Partition1) || stagingAfterAbandon is not null)
                return;

            switch (abandon)
            {
                case AbandonInCallback.Assign:
                    consumer.Assign(partition0, Partition1);
                    break;
                case AbandonInCallback.Unassign:
                    consumer.Unassign();
                    break;
                case AbandonInCallback.IncrementalAssign:
                    consumer.IncrementalAssign([new TopicPartitionOffset(Partition1.Topic, Partition1.Partition, 5)]);
                    break;
                case AbandonInCallback.Unsubscribe:
                    consumer.Unsubscribe();
                    break;
                case AbandonInCallback.SubscribeTopics:
                    consumer.Subscribe("test-topic");
                    break;
                default:
                    consumer.SubscribePattern("test-.*");
                    break;
            }

            stagingAfterAbandon = coordinator.TryGetAssignedCallbackRevocationSequence(Partition1, out _);
            consumer.Seek(new TopicPartitionOffset(Partition1.Topic, Partition1.Partition, 42));
            positionAfterSeek = consumer.GetPosition(Partition1);
            consumer.Pause(Partition1);
            consumer.Resume(Partition1);
            pausedAfterResume = consumer.Paused.Contains(Partition1);
            consumer.Pause(Partition1);
        };

        await harness.HeartbeatAsync();

        await Assert.That(stagingAfterAbandon).IsNotNull();
        await Assert.That(stagingAfterAbandon!.Value).IsFalse();
        await Assert.That(positionAfterSeek).IsEqualTo(42L);
        await Assert.That(pausedAfterResume).IsFalse();
        await Assert.That(consumer.Paused).Contains(Partition1);
        await Assert.That(consumer.PendingRebalanceSeekCountForTest).IsEqualTo(0);
        if (abandon == AbandonInCallback.Assign)
        {
            // Manual assignment initializes only partitions without a position; the seek is it.
            await consumer.EnsureAssignmentAsync(CancellationToken.None);
            await Assert.That(consumer.GetPosition(Partition1)).IsEqualTo(42L);
            await Assert.That(consumer.Paused).Contains(Partition1);
        }
    }

    /// <summary>
    /// The first assignment's callback pauses and/or seeks, then the consumer is closed or disposed
    /// before it ever synchronizes: nothing the callback staged survives.
    /// </summary>
    [Test]
    [Timeout(60_000)]
    [Arguments(true, false, false)]
    [Arguments(false, true, false)]
    [Arguments(true, true, false)]
    [Arguments(true, false, true)]
    [Arguments(false, true, true)]
    [Arguments(true, true, true)]
    public async Task RebalanceCallbackStateTransitions_EndedBeforeFirstSync_DropsCallbackState(
        bool pause,
        bool seek,
        bool disposeWithoutClose,
        CancellationToken testTimeout)
    {
        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessCoreAsync(
            listener,
            consumerAwareListener: null,
            [AssignedResponse(1, 0, 1)],
            initialSync: false);
        var consumer = harness.Consumer;
        var acted = false;
        listener.OnAssigned = partitions =>
        {
            if (!partitions.Contains(Partition1))
                return;

            acted = true;
            if (pause)
                consumer.Pause(Partition1);
            if (seek)
                consumer.Seek(new TopicPartitionOffset(Partition1.Topic, Partition1.Partition, 42));
        };

        await harness.HeartbeatAsync();
        await Assert.That(acted).IsTrue();
        await Assert.That(consumer.Assignment).IsEmpty();

        if (disposeWithoutClose)
            await consumer.DisposeAsync();
        else
            await consumer.CloseAsync(testTimeout);

        await Assert.That(consumer.Paused).DoesNotContain(Partition1);
        await Assert.That(consumer.PendingRebalanceSeekCountForTest).IsEqualTo(0);
        await Assert.That(consumer.RebalancePausedPartitionCountForTest).IsEqualTo(0);
    }

    public enum StaleCallbackAction
    {
        Pause,
        Seek,
        PauseThenResume,
        Resume
    }

    public enum StaleCallbackEnd
    {
        Sync,
        Unsubscribe,
        Assign,
        Close
    }

    /// <summary>
    /// The partition is revoked (and assigned straight back by a notification not yet delivered)
    /// while the callback that announced it is still running; the callback then pauses, seeks or
    /// pauses and resumes it. That ownership has ended, so nothing it does survives the next sync or
    /// an abandon.
    /// </summary>
    [Test]
    [Timeout(60_000)]
    [MatrixDataSource]
    public async Task RebalanceCallbackStateTransitions_StaleCallbackActsAfterRevocation_LeavesNothing(
        [Matrix] StaleCallbackAction action,
        [Matrix] StaleCallbackEnd end,
        CancellationToken testTimeout)
    {
        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessAsync(
            listener,
            AssignedResponse(1, 0),
            AssignedResponse(2, 0, 1));
        var consumer = harness.Consumer;
        var coordinator = GetCoordinator(consumer);
        var acted = false;
        listener.OnAssigned = partitions =>
        {
            if (acted || !partitions.Contains(Partition1))
                return;

            acted = true;
            coordinator.RevokeAndReassignForTest(Partition1);
            switch (action)
            {
                case StaleCallbackAction.Pause:
                    consumer.Pause(Partition1);
                    break;
                case StaleCallbackAction.Seek:
                    consumer.Seek(new TopicPartitionOffset(Partition1.Topic, Partition1.Partition, 42));
                    break;
                case StaleCallbackAction.Resume:
                    consumer.Resume(Partition1);
                    break;
                default:
                    consumer.Pause(Partition1);
                    consumer.Resume(Partition1);
                    break;
            }
        };

        await harness.HeartbeatAsync();
        await Assert.That(acted).IsTrue();

        switch (end)
        {
            case StaleCallbackEnd.Sync:
                await consumer.EnsureAssignmentAsync(CancellationToken.None);
                await Assert.That(consumer.GetPosition(Partition1)).IsEqualTo(20L);
                break;
            case StaleCallbackEnd.Unsubscribe:
                consumer.Unsubscribe();
                break;
            case StaleCallbackEnd.Assign:
                consumer.Assign(new TopicPartition("test-topic", 0), Partition1);
                break;
            default:
                await consumer.CloseAsync(testTimeout);
                break;
        }

        await Assert.That(consumer.Paused).DoesNotContain(Partition1);
        await Assert.That(consumer.PendingRebalanceSeekCountForTest).IsEqualTo(0);
        await Assert.That(consumer.RebalancePausedPartitionCountForTest).IsEqualTo(0);
    }

    /// <summary>
    /// The callback polls, which synchronizes a newer revoke-and-reassign of its partition (so the
    /// callback's ownership has ended and the new one is acknowledged), then pauses, resumes or seeks
    /// it. A stale callback's call is a no-op: it neither pauses nor resumes the new ownership, nor
    /// moves its position.
    /// </summary>
    [Test]
    [Timeout(60_000)]
    [Arguments(StaleCallbackAction.Pause)]
    [Arguments(StaleCallbackAction.Seek)]
    [Arguments(StaleCallbackAction.Resume)]
    public async Task RebalanceCallbackStateTransitions_StaleCallbackActsAfterNewerSync_IsNoOp(
        StaleCallbackAction action,
        CancellationToken testTimeout)
    {
        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessAsync(
            listener,
            AssignedResponse(1, 0),
            AssignedResponse(2, 0, 1));
        var consumer = harness.Consumer;
        var coordinator = GetCoordinator(consumer);
        var acted = false;
        listener.OnAssignedAsync = async partitions =>
        {
            if (acted || !partitions.Contains(Partition1))
                return;

            acted = true;
            coordinator.RevokeAndReassignForTest(Partition1);
            await consumer.EnsureAssignmentAsync(testTimeout);
            if (action == StaleCallbackAction.Resume)
            {
                // The new ownership is paused by the application, outside any callback.
                Task pause;
                using (ExecutionContext.SuppressFlow())
                    pause = Task.Run(() => consumer.Pause(Partition1), testTimeout);
                await pause;
            }

            switch (action)
            {
                case StaleCallbackAction.Pause:
                    consumer.Pause(Partition1);
                    break;
                case StaleCallbackAction.Seek:
                    consumer.Seek(new TopicPartitionOffset(Partition1.Topic, Partition1.Partition, 42));
                    break;
                default:
                    consumer.Resume(Partition1);
                    break;
            }
        };

        await harness.HeartbeatAsync();
        await consumer.EnsureAssignmentAsync(testTimeout);

        await Assert.That(acted).IsTrue();
        await Assert.That(consumer.Assignment).Contains(Partition1);
        await Assert.That(consumer.Paused.Contains(Partition1)).IsEqualTo(action == StaleCallbackAction.Resume);
        await Assert.That(consumer.GetPosition(Partition1)).IsEqualTo(20L);
        await Assert.That(consumer.PendingRebalanceSeekCountForTest).IsEqualTo(0);
    }

    public enum AbandonAfterReassign
    {
        Assign,
        IncrementalAssign,
        Unsubscribe,
        Close
    }

    /// <summary>
    /// The partition's ownership was acknowledged, then revoked and assigned straight back; the new
    /// ownership's callback pauses and seeks it, and the consumer abandons the assignment before
    /// synchronizing it. The acknowledged assignment still names the partition, but the pause and
    /// seek belong to the newer, never-synchronized ownership: neither survives.
    /// </summary>
    [Test]
    [Timeout(60_000)]
    [Arguments(AbandonAfterReassign.Assign)]
    [Arguments(AbandonAfterReassign.IncrementalAssign)]
    [Arguments(AbandonAfterReassign.Unsubscribe)]
    [Arguments(AbandonAfterReassign.Close)]
    public async Task RebalanceCallbackStateTransitions_AbandonAfterReassignOfAcknowledgedPartition_DropsNewOwnershipState(
        AbandonAfterReassign abandon,
        CancellationToken testTimeout)
    {
        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessAsync(
            listener,
            AssignedResponse(1, 0, 1),
            AssignedResponse(2, 0),
            AssignedResponse(3, 0, 1));
        var consumer = harness.Consumer;
        listener.OnAssigned = partitions =>
        {
            if (!partitions.Contains(Partition1))
                return;

            consumer.Pause(Partition1);
            consumer.Seek(new TopicPartitionOffset(Partition1.Topic, Partition1.Partition, 42));
        };

        await harness.HeartbeatAsync();
        await harness.HeartbeatAsync();

        switch (abandon)
        {
            case AbandonAfterReassign.Assign:
                consumer.Assign(new TopicPartition("test-topic", 0), Partition1);
                break;
            case AbandonAfterReassign.IncrementalAssign:
                consumer.IncrementalAssign([new TopicPartitionOffset("test-topic", 0, 5)]);
                break;
            case AbandonAfterReassign.Unsubscribe:
                consumer.Unsubscribe();
                break;
            default:
                await consumer.CloseAsync(testTimeout);
                break;
        }

        await Assert.That(consumer.Paused).DoesNotContain(Partition1);
        await Assert.That(consumer.PendingRebalanceSeekCountForTest).IsEqualTo(0);
        await Assert.That(consumer.RebalancePausedPartitionCountForTest).IsEqualTo(0);
    }

    /// <summary>
    /// A consumer-aware callback abandons the group assignment through the captured consumer, then
    /// keeps using its <see cref="IRebalanceConsumer"/> view: the view's seek, position, pause and
    /// resume act on the consumer directly, as the captured consumer's do.
    /// </summary>
    [Test]
    public async Task RebalanceCallbackStateTransitions_ConsumerAwareViewAfterAbandonInsideCallback_ActsDirectly()
    {
        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessAsync(
            consumerAwareListener: listener,
            AssignedResponse(1, 0),
            AssignedResponse(2, 0, 1));
        var consumer = harness.Consumer;
        long? positionAfterSeek = null;
        var pausedAfterResume = true;
        var acted = false;
        listener.OnAssignedConsumer = (view, partitions) =>
        {
            if (acted || !partitions.Contains(Partition1))
                return;

            acted = true;
            consumer.Assign(new TopicPartition("test-topic", 0), Partition1);
            view.Seek(new TopicPartitionOffset(Partition1.Topic, Partition1.Partition, 42));
            positionAfterSeek = view.GetPosition(Partition1);
            view.Pause(Partition1);
            view.Resume(Partition1);
            pausedAfterResume = consumer.Paused.Contains(Partition1);
            view.Pause(Partition1);
        };

        await harness.HeartbeatAsync();

        await Assert.That(acted).IsTrue();
        await Assert.That(positionAfterSeek).IsEqualTo(42L);
        await Assert.That(pausedAfterResume).IsFalse();
        await Assert.That(consumer.PendingRebalanceSeekCountForTest).IsEqualTo(0);
        await Assert.That(consumer.RebalancePausedPartitionCountForTest).IsEqualTo(0);

        await consumer.EnsureAssignmentAsync(CancellationToken.None);
        await Assert.That(consumer.GetPosition(Partition1)).IsEqualTo(42L);
        await Assert.That(consumer.Paused).Contains(Partition1);
    }

    public enum AbandonBeforeCallbackStart
    {
        Assign,
        Unsubscribe
    }

    /// <summary>
    /// The assigned notification is queued, the application abandons the assignment, and only then
    /// does the callback run: it belongs to the abandoned assignment, so its seek and pause act on
    /// the consumer directly instead of being staged.
    /// </summary>
    [Test]
    [Arguments(AbandonBeforeCallbackStart.Assign)]
    [Arguments(AbandonBeforeCallbackStart.Unsubscribe)]
    public async Task RebalanceCallbackStateTransitions_AbandonBetweenQueueAndCallback_EndsStaging(
        AbandonBeforeCallbackStart abandon)
    {
        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessAsync(listener, AssignedResponse(1, 0));
        var consumer = harness.Consumer;
        var coordinator = GetCoordinator(consumer);
        bool? staging = null;
        listener.OnAssigned = partitions =>
        {
            if (!partitions.Contains(Partition1))
                return;

            staging = coordinator.TryGetAssignedCallbackRevocationSequence(Partition1, out _);
            consumer.Seek(new TopicPartitionOffset(Partition1.Topic, Partition1.Partition, 42));
            consumer.Pause(Partition1);
        };

        coordinator.QueueAssignedCallbacksForTest([Partition1]);
        if (abandon == AbandonBeforeCallbackStart.Assign)
            consumer.Assign(new TopicPartition("test-topic", 0), Partition1);
        else
            consumer.Unsubscribe();
        await coordinator.DeliverQueuedCallbacksForTestAsync();

        await Assert.That(staging).IsNotNull();
        await Assert.That(staging!.Value).IsFalse();
        await Assert.That(consumer.PendingRebalanceSeekCountForTest).IsEqualTo(0);
        await Assert.That(consumer.RebalancePausedPartitionCountForTest).IsEqualTo(0);
        await Assert.That(consumer.GetPosition(Partition1)).IsEqualTo(42L);
    }

    /// <summary>
    /// The partition is revoked again after the coordinator confirmed a sync pass but before the
    /// consumer recorded it, then a newer callback pauses it. The acknowledged pass must not cover
    /// that newer revocation, so abandoning the assignment drops the newer ownership's pause.
    /// </summary>
    [Test]
    public async Task RebalanceCallbackStateTransitions_RevokedBetweenAckAndCompletion_NewerPauseDoesNotSurviveAbandon()
    {
        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessAsync(
            listener,
            AssignedResponse(1, 0, 1),
            AssignedResponse(2, 0),
            AssignedResponse(3, 0, 1));
        var consumer = harness.Consumer;
        var coordinator = GetCoordinator(consumer);

        await harness.HeartbeatAsync();
        await harness.HeartbeatAsync();

        var revoked = false;
        consumer.AfterAssignmentSyncAcknowledgedForTest = () =>
        {
            if (revoked)
                return;
            revoked = true;
            coordinator.RevokeAndReassignForTest(Partition1);
        };
        try
        {
            await consumer.EnsureAssignmentAsync(CancellationToken.None);
        }
        finally
        {
            consumer.AfterAssignmentSyncAcknowledgedForTest = null;
        }

        await Assert.That(revoked).IsTrue();

        // The newer ownership's callback pauses the partition; the consumer then switches to manual
        // assignment before synchronizing it.
        listener.OnAssigned = partitions =>
        {
            if (partitions.Contains(Partition1))
                consumer.Pause(Partition1);
        };
        await coordinator.DeliverAssignedCallbacksForTestAsync([Partition1]);
        consumer.Assign(new TopicPartition("test-topic", 0), Partition1);

        await Assert.That(consumer.Paused).DoesNotContain(Partition1);
    }

    /// <summary>
    /// A sync pass drains a revocation and is superseded before its acknowledgement; a later pass
    /// is acknowledged without draining anything (unchanged or reclassification path). That later
    /// acknowledgement covers the earlier drained revocation, so a pause the reassigning callback
    /// made under the now-acknowledged ownership survives a manual assignment keeping it.
    /// </summary>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RebalanceCallbackStateTransitions_SupersededAckThenLaterAck_CoversDrainedRevocation(bool reclassify)
    {
        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessCoreAsync(
            listener,
            consumerAwareListener: null,
            [AssignedResponse(1, 0, 1), AssignedResponse(2, 0), AssignedResponse(3, 0, 1)],
            newPartitionsReset: reclassify ? AutoOffsetReset.Earliest : null);
        var consumer = harness.Consumer;
        var coordinator = GetCoordinator(consumer);
        listener.OnAssigned = partitions =>
        {
            if (partitions.Contains(Partition1))
                consumer.Pause(Partition1);
        };

        await harness.HeartbeatAsync();
        await harness.HeartbeatAsync();

        var superseded = false;
        consumer.BeforeAssignmentSyncAcknowledgedForTest = () =>
        {
            if (superseded)
                return;
            superseded = true;
            if (reclassify)
                coordinator.MarkNewlyExpandedForTest(Partition1);
            else
                coordinator.BumpAssignmentVersionForTest();
        };
        try
        {
            await consumer.EnsureAssignmentAsync(CancellationToken.None);
            await consumer.EnsureAssignmentAsync(CancellationToken.None);
        }
        finally
        {
            consumer.BeforeAssignmentSyncAcknowledgedForTest = null;
        }

        await Assert.That(superseded).IsTrue();
        await Assert.That(consumer.Paused).Contains(Partition1);
        await Assert.That(consumer.IsRevokedSinceAcknowledgedForTest(Partition1)).IsFalse();

        if (reclassify)
            return; // A new-partition reset policy rules out manual assignment.

        consumer.Assign(new TopicPartition("test-topic", 0), Partition1);

        await Assert.That(consumer.Paused).Contains(Partition1);
    }

    public enum AbandonAfterPublish
    {
        Assign,
        Unsubscribe
    }

    /// <summary>
    /// A heartbeat publishes an assignment and the application abandons the group assignment before
    /// the heartbeat queues the assignment's callbacks. The callback belongs to the abandoned
    /// assignment: its seek and pause act on the consumer directly.
    /// </summary>
    [Test]
    [Arguments(AbandonAfterPublish.Assign)]
    [Arguments(AbandonAfterPublish.Unsubscribe)]
    public async Task RebalanceCallbackStateTransitions_AbandonBetweenPublishAndQueue_EndsStaging(
        AbandonAfterPublish abandon)
    {
        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessAsync(
            listener,
            AssignedResponse(1, 0),
            AssignedResponse(2, 0, 1));
        var consumer = harness.Consumer;
        var coordinator = GetCoordinator(consumer);
        bool? staging = null;
        listener.OnAssigned = partitions =>
        {
            if (!partitions.Contains(Partition1))
                return;

            staging = coordinator.TryGetAssignedCallbackRevocationSequence(Partition1, out _);
            consumer.Seek(new TopicPartitionOffset(Partition1.Topic, Partition1.Partition, 42));
            consumer.Pause(Partition1);
        };

        var abandoned = false;
        coordinator.AfterAssignmentPublishedForTest = () =>
        {
            if (abandoned)
                return;
            abandoned = true;
            if (abandon == AbandonAfterPublish.Assign)
                consumer.Assign(new TopicPartition("test-topic", 0), Partition1);
            else
                consumer.Unsubscribe();
        };
        try
        {
            await harness.HeartbeatAsync();
        }
        finally
        {
            coordinator.AfterAssignmentPublishedForTest = null;
        }

        await Assert.That(abandoned).IsTrue();
        await Assert.That(staging).IsNotNull();
        await Assert.That(staging!.Value).IsFalse();
        await Assert.That(consumer.PendingRebalanceSeekCountForTest).IsEqualTo(0);
        await Assert.That(consumer.RebalancePausedPartitionCountForTest).IsEqualTo(0);
    }

    /// <summary>
    /// Revocation bookkeeping is pruned once an acknowledged sync covers it: churning many distinct
    /// partitions through revoke and acknowledgement leaves it bounded by outstanding revocations,
    /// not by every partition ever revoked.
    /// </summary>
    [Test]
    [Timeout(120_000)]
    public async Task RebalanceCallbackStateTransitions_RevocationChurn_TrackingStaysBounded(CancellationToken testTimeout)
    {
        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessAsync(listener, AssignedResponse(1, 0));
        var consumer = harness.Consumer;
        var coordinator = GetCoordinator(consumer);

        const int churned = 10_000;
        const int perSync = 500;
        for (var start = 0; start < churned; start += perSync)
        {
            for (var partition = start; partition < start + perSync; partition++)
                coordinator.RevokeAndReassignForTest(new TopicPartition("churn-topic", partition));
            await consumer.EnsureAssignmentAsync(testTimeout);
        }

        await Assert.That(coordinator.RevocationSequenceTrackingCountForTest).IsEqualTo(0);
        await Assert.That(consumer.RevocationSequenceTrackingCountForTest).IsEqualTo(0);

        // A revocation not yet covered by an acknowledged sync is still tracked.
        coordinator.RevokeAndReassignForTest(Partition1);
        await Assert.That(consumer.IsRevokedSinceAcknowledgedForTest(Partition1)).IsTrue();
        await consumer.EnsureAssignmentAsync(testTimeout);
        await Assert.That(consumer.IsRevokedSinceAcknowledgedForTest(Partition1)).IsFalse();
        await Assert.That(coordinator.RevocationSequenceTrackingCountForTest).IsEqualTo(0);
    }

    [Test]
    public async Task EnsureAssignmentAsync_RepeatedlySupersededSync_TracksOneAppliedSeekPerPartition()
    {
        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessAsync(
            listener,
            AssignedResponse(1, 0, 1),
            AssignedResponse(2, 0),
            AssignedResponse(3, 0, 1));

        listener.OnAssigned = partitions =>
        {
            if (partitions.Contains(Partition1))
                harness.Consumer.Seek(new TopicPartitionOffset(Partition1.Topic, Partition1.Partition, 42));
        };

        await harness.HeartbeatAsync();
        await harness.HeartbeatAsync();

        const int passes = 5;
        var maxTracked = SupersedeFirstSyncPasses(harness, passes);
        await harness.Consumer.EnsureAssignmentAsync(CancellationToken.None);

        await Assert.That(maxTracked()).IsEqualTo(1);
        await Assert.That(harness.Consumer.GetPosition(Partition1)).IsEqualTo(42L);
        await Assert.That(harness.Consumer.UnacknowledgedAppliedRebalanceSeekCountForTest).IsEqualTo(0);
        await Assert.That(harness.Consumer.PendingRebalanceSeekCountForTest).IsEqualTo(0);
    }

    /// <summary>
    /// The first <paramref name="passes"/> sync passes initialize positions and are then superseded by
    /// a newer assignment version, so each is retried. Returns the largest number of applied seeks
    /// tracked when a pass starts its offset fetch.
    /// </summary>
    private static Func<int> SupersedeFirstSyncPasses(CallbackHarness harness, int passes)
    {
        var coordinator = GetCoordinator(harness.Consumer);
        var calls = 0;
        var maxTracked = 0;
        harness.Connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                maxTracked = Math.Max(maxTracked, harness.Consumer.UnacknowledgedAppliedRebalanceSeekCountForTest);
                if (Interlocked.Increment(ref calls) <= passes)
                    coordinator.BumpAssignmentVersionForTest();
                return ValueTask.FromResult(CreateSuccessfulOffsetFetchResponse());
            });
        return () => Math.Max(maxTracked, harness.Consumer.UnacknowledgedAppliedRebalanceSeekCountForTest);
    }

    private static async Task CancelFirstSyncDuringOffsetFetchAsync(CallbackHarness harness)
    {
        var fetchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        harness.Connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Interlocked.Increment(ref calls) == 1
                ? BlockUntilCancelledAsync(fetchStarted, call.ArgAt<CancellationToken>(2))
                : ValueTask.FromResult(CreateSuccessfulOffsetFetchResponse()));

        using var cts = new CancellationTokenSource();
        var sync = harness.Consumer.EnsureAssignmentAsync(cts.Token).AsTask();
        await fetchStarted.Task;
        await cts.CancelAsync();
        await Assert.That(async () => await sync).Throws<OperationCanceledException>();
    }
}
