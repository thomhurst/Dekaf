using Dekaf.Consumer;
using Dekaf.Networking;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Pause, resume, seek and position calls made from rebalance callbacks run before the consumer
/// synchronizes the assignment the callback announced. These tests pin what survives that sync.
/// </summary>
public sealed partial class ConsumerAssignmentFastPathTests
{
    private static readonly TopicPartition Partition1 = new("test-topic", 1);

    [Test]
    public async Task EnsureAssignmentAsync_ConsumerAwarePauseInAssignedAfterRevokeAndReassign_SurvivesSync()
    {
        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessAsync(
            consumerAwareListener: listener,
            AssignedResponse(1, 0, 1),
            AssignedResponse(2, 0),
            AssignedResponse(3, 0, 1));

        listener.OnAssignedConsumer = static (consumer, partitions) =>
        {
            if (partitions.Contains(Partition1))
                consumer.Pause(Partition1);
        };

        await harness.HeartbeatAsync(); // revokes partition 1
        await harness.HeartbeatAsync(); // assigns partition 1 again; the callback pauses it
        await harness.Consumer.EnsureAssignmentAsync(CancellationToken.None);

        await Assert.That(harness.Consumer.Paused).Contains(Partition1);
    }

    [Test]
    public async Task EnsureAssignmentAsync_PauseInAssignedAfterRevokeAndReassign_SurvivesSync()
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
                harness.Consumer.Pause(Partition1);
        };

        await harness.HeartbeatAsync();
        await harness.HeartbeatAsync();
        await harness.Consumer.EnsureAssignmentAsync(CancellationToken.None);

        await Assert.That(harness.Consumer.Paused).Contains(Partition1);
    }

    [Test]
    public async Task EnsureAssignmentAsync_PauseInAssignedAfterFenceAndRejoin_SurvivesSync()
    {
        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessAsync(
            listener,
            AssignedResponse(1, 0, 1),
            FencedResponse(),
            AssignedResponse(1, 0, 1));

        var lost = 0;
        listener.OnLost = _ => lost++;
        listener.OnAssigned = partitions =>
        {
            if (partitions.Contains(Partition1))
                harness.Consumer.Pause(Partition1);
        };

        // Fenced on the rejoin's first heartbeat: the partitions are lost, then assigned again.
        await harness.HeartbeatAsync();
        await harness.Consumer.EnsureAssignmentAsync(CancellationToken.None);

        await Assert.That(lost).IsEqualTo(1);
        await Assert.That(harness.Consumer.Paused).Contains(Partition1);
    }

    [Test]
    public async Task EnsureAssignmentAsync_PauseFromPreviousOwnership_IsClearedOnReassign()
    {
        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessAsync(
            listener,
            AssignedResponse(1, 0, 1),
            AssignedResponse(2, 0),
            AssignedResponse(3, 0, 1));

        harness.Consumer.Pause(Partition1);
        await harness.HeartbeatAsync();
        await harness.HeartbeatAsync();
        await harness.Consumer.EnsureAssignmentAsync(CancellationToken.None);

        await Assert.That(harness.Consumer.Paused).DoesNotContain(Partition1);
    }

    [Test]
    public async Task EnsureAssignmentAsync_PauseThenResumeInAssigned_IsNotPausedAfterSync()
    {
        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessAsync(
            listener,
            AssignedResponse(1, 0, 1),
            AssignedResponse(2, 0),
            AssignedResponse(3, 0, 1));

        listener.OnAssigned = partitions =>
        {
            if (!partitions.Contains(Partition1))
                return;

            harness.Consumer.Pause(Partition1);
            harness.Consumer.Resume(Partition1);
        };

        harness.Consumer.Pause(Partition1);
        await harness.HeartbeatAsync();
        await harness.HeartbeatAsync();
        await harness.Consumer.EnsureAssignmentAsync(CancellationToken.None);

        await Assert.That(harness.Consumer.Paused).DoesNotContain(Partition1);
    }

    [Test]
    public async Task EnsureAssignmentAsync_PauseInAssigned_ThenRevokedAndReassignedBeforeSync_IsCleared()
    {
        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessAsync(
            listener,
            AssignedResponse(1, 0, 1),
            AssignedResponse(2, 0),
            AssignedResponse(3, 0, 1),
            AssignedResponse(4, 0),
            AssignedResponse(5, 0, 1));

        var assignedCalls = 0;
        listener.OnAssigned = partitions =>
        {
            // Only the first reassignment pauses; the ownership it paused ends before the sync.
            if (partitions.Contains(Partition1) && ++assignedCalls == 1)
                harness.Consumer.Pause(Partition1);
        };

        for (var i = 0; i < 4; i++)
            await harness.HeartbeatAsync();
        await harness.Consumer.EnsureAssignmentAsync(CancellationToken.None);

        await Assert.That(assignedCalls).IsEqualTo(2);
        await Assert.That(harness.Consumer.Paused).DoesNotContain(Partition1);
    }

    [Test]
    [Arguments(SeekKind.Offset)]
    [Arguments(SeekKind.Beginning)]
    [Arguments(SeekKind.End)]
    public async Task EnsureAssignmentAsync_SeekInAssignedForNewPartition_WinsOverCommittedOffset(SeekKind kind)
    {
        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessAsync(
            listener,
            AssignedResponse(1, 0),
            AssignedResponse(2, 0, 1));

        listener.OnAssigned = partitions =>
        {
            if (partitions.Contains(Partition1))
                Seek(harness.Consumer, kind);
        };

        await harness.HeartbeatAsync();
        await harness.Consumer.EnsureAssignmentAsync(CancellationToken.None);

        var expected = ExpectedSeekOffset(kind);
        await Assert.That(harness.Consumer.GetPosition(Partition1)).IsEqualTo(expected);
        await Assert.That(GetFetchPositions(harness.Consumer)[Partition1]).IsEqualTo(expected);
    }

    [Test]
    public async Task EnsureAssignmentAsync_SeekInAssignedAfterRevokeAndReassign_WinsOverCommittedOffset()
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
        await harness.Consumer.EnsureAssignmentAsync(CancellationToken.None);

        await Assert.That(harness.Consumer.GetPosition(Partition1)).IsEqualTo(42L);
        await Assert.That(GetFetchPositions(harness.Consumer)[Partition1]).IsEqualTo(42L);
    }

    [Test]
    public async Task EnsureAssignmentAsync_SeekInAssigned_ThenRevokedAndReassignedBeforeSync_IsDiscarded()
    {
        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessAsync(
            listener,
            AssignedResponse(1, 0, 1),
            AssignedResponse(2, 0),
            AssignedResponse(3, 0, 1),
            AssignedResponse(4, 0),
            AssignedResponse(5, 0, 1));

        var assignedCalls = 0;
        listener.OnAssigned = partitions =>
        {
            if (partitions.Contains(Partition1) && ++assignedCalls == 1)
                harness.Consumer.Seek(new TopicPartitionOffset(Partition1.Topic, Partition1.Partition, 42));
        };

        for (var i = 0; i < 4; i++)
            await harness.HeartbeatAsync();
        await harness.Consumer.EnsureAssignmentAsync(CancellationToken.None);

        await Assert.That(assignedCalls).IsEqualTo(2);
        await Assert.That(harness.Consumer.GetPosition(Partition1)).IsEqualTo(20L);
    }

    [Test]
    public async Task EnsureAssignmentAsync_SeekInAssignedForRetainedPartition_AppliesImmediately()
    {
        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessAsync(
            listener,
            AssignedResponse(1, 0),
            AssignedResponse(2, 0, 1));

        long? positionInCallback = null;
        listener.OnAssigned = partitions =>
        {
            // Partition 0 is retained: its position is live, so a seek applies at once.
            harness.Consumer.Seek(new TopicPartitionOffset("test-topic", 0, 7));
            positionInCallback = harness.Consumer.GetPosition(new TopicPartition("test-topic", 0));
        };

        await harness.HeartbeatAsync();
        await harness.Consumer.EnsureAssignmentAsync(CancellationToken.None);

        await Assert.That(positionInCallback).IsEqualTo(7L);
        await Assert.That(harness.Consumer.GetPosition(new TopicPartition("test-topic", 0))).IsEqualTo(7L);
    }

    [Test]
    public async Task GetPosition_InAssignedAfterRevokeAndReassign_DoesNotReportPreviousOwnership()
    {
        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessAsync(
            listener,
            AssignedResponse(1, 0, 1),
            AssignedResponse(2, 0),
            AssignedResponse(3, 0, 1));

        long? beforeSeek = -2;
        long? afterSeek = -2;
        listener.OnAssigned = partitions =>
        {
            if (!partitions.Contains(Partition1))
                return;

            beforeSeek = harness.Consumer.GetPosition(Partition1);
            harness.Consumer.Seek(new TopicPartitionOffset(Partition1.Topic, Partition1.Partition, 42));
            afterSeek = harness.Consumer.GetPosition(Partition1);
        };

        // The previous ownership consumed up to 77.
        harness.Consumer.Seek(new TopicPartitionOffset(Partition1.Topic, Partition1.Partition, 77));
        await harness.HeartbeatAsync();
        await harness.HeartbeatAsync();

        await Assert.That(beforeSeek).IsNull();
        await Assert.That(afterSeek).IsEqualTo(42L);
    }

    [Test]
    public async Task Seek_OutsideCallback_ForReassignedPartitionBeforeSync_IsReplacedByCommittedOffset()
    {
        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessAsync(
            listener,
            AssignedResponse(1, 0, 1),
            AssignedResponse(2, 0),
            AssignedResponse(3, 0, 1));

        await harness.HeartbeatAsync();
        await harness.HeartbeatAsync();

        // A seek from the application's own flow (not a callback) before the sync belongs to the
        // previous ownership: the sync starts the new ownership at the committed offset.
        harness.Consumer.Seek(new TopicPartitionOffset(Partition1.Topic, Partition1.Partition, 42));
        await harness.Consumer.EnsureAssignmentAsync(CancellationToken.None);

        await Assert.That(harness.Consumer.GetPosition(Partition1)).IsEqualTo(20L);
    }

    [Test]
    public async Task Seek_InAssignedCallbackForUnassignedPartition_WritesPositionDirectly()
    {
        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessAsync(
            listener,
            AssignedResponse(1, 0),
            AssignedResponse(2, 0, 1));

        var unassigned = new TopicPartition("other-topic", 3);
        listener.OnAssigned = _ =>
            harness.Consumer.Seek(new TopicPartitionOffset(unassigned.Topic, unassigned.Partition, 5));

        await harness.HeartbeatAsync();

        await Assert.That(harness.Consumer.GetPosition(unassigned)).IsEqualTo(5L);
        await Assert.That(harness.Consumer.GetRebalancePosition(unassigned)).IsEqualTo(5L);
    }

    [Test]
    [Arguments(false, AbandonKind.Unsubscribe)]
    [Arguments(true, AbandonKind.Unsubscribe)]
    [Arguments(false, AbandonKind.Assign)]
    [Arguments(true, AbandonKind.Assign)]
    [Arguments(false, AbandonKind.Unassign)]
    public async Task AbandonAssignmentBeforeSync_DropsSeekStagedByAssignedCallback(
        bool consumerAware,
        AbandonKind abandon)
    {
        var listener = new CallbackListener();
        await using var harness = consumerAware
            ? await CreateCallbackHarnessAsync(
                consumerAwareListener: listener,
                AssignedResponse(1, 0),
                AssignedResponse(2, 0, 1))
            : await CreateCallbackHarnessAsync(
                listener,
                AssignedResponse(1, 0),
                AssignedResponse(2, 0, 1));

        var seek = new TopicPartitionOffset(Partition1.Topic, Partition1.Partition, 42);
        listener.OnAssigned = partitions =>
        {
            if (partitions.Contains(Partition1))
                harness.Consumer.Seek(seek);
        };
        listener.OnAssignedConsumer = (consumer, partitions) =>
        {
            if (partitions.Contains(Partition1))
                consumer.Seek(seek);
        };

        // Partition 1 is announced and its callback seeks, but the consumer never synchronizes it.
        await harness.HeartbeatAsync();
        Abandon(harness.Consumer, abandon);

        await Assert.That(harness.Consumer.GetRebalancePosition(Partition1)).IsNull();

        // A later subscription assigned the same partition starts at the committed offset.
        harness.Consumer.Subscribe("test-topic");
        await harness.Consumer.EnsureAssignmentAsync(CancellationToken.None);
        await Assert.That(harness.Consumer.GetPosition(Partition1)).IsEqualTo(20L);
    }

    [Test]
    [Arguments(false, AbandonKind.Unsubscribe)]
    [Arguments(true, AbandonKind.Unsubscribe)]
    [Arguments(false, AbandonKind.Assign)]
    [Arguments(false, AbandonKind.Unassign)]
    public async Task AbandonAssignmentBeforeSync_DropsPauseMadeByAssignedCallback(
        bool consumerAware,
        AbandonKind abandon)
    {
        var listener = new CallbackListener();
        await using var harness = consumerAware
            ? await CreateCallbackHarnessAsync(
                consumerAwareListener: listener,
                AssignedResponse(1, 0),
                AssignedResponse(2, 0, 1))
            : await CreateCallbackHarnessAsync(
                listener,
                AssignedResponse(1, 0),
                AssignedResponse(2, 0, 1));

        listener.OnAssigned = partitions =>
        {
            if (partitions.Contains(Partition1))
                harness.Consumer.Pause(Partition1);
        };
        listener.OnAssignedConsumer = static (consumer, partitions) =>
        {
            if (partitions.Contains(Partition1))
                consumer.Pause(Partition1);
        };

        await harness.HeartbeatAsync();
        Abandon(harness.Consumer, abandon);

        await Assert.That(harness.Consumer.Paused).DoesNotContain(Partition1);
    }

    [Test]
    public async Task AbandonAssignmentBeforeSync_KeepsPauseOfSynchronizedPartition()
    {
        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessAsync(
            listener,
            AssignedResponse(1, 0),
            AssignedResponse(2, 0, 1));

        listener.OnAssigned = partitions =>
        {
            if (partitions.Contains(Partition1))
                harness.Consumer.Pause(Partition1);
        };

        await harness.HeartbeatAsync();
        await harness.Consumer.EnsureAssignmentAsync(CancellationToken.None);

        // Partition 1 is synchronized and paused; a manual assignment that keeps it keeps the pause.
        harness.Consumer.Assign(new TopicPartition("test-topic", 0), Partition1);

        await Assert.That(harness.Consumer.Paused).Contains(Partition1);
    }

    [Test]
    [Timeout(30_000)]
    public async Task EnsureAssignmentAsync_ResumeDuringReassignedPauseRestoration_IsNotLost(
        CancellationToken testTimeout)
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
                harness.Consumer.Pause(Partition1);
        };

        await harness.HeartbeatAsync();
        await harness.HeartbeatAsync();

        // The application resumes the partition while sync is between clearing the previous
        // ownership's pause and restoring the callback's. The resume is the later call and wins.
        Task? resume = null;
        KafkaConsumer<string, string>.AfterPartitionPauseClearedForTest = (consumer, partition) =>
        {
            if (!ReferenceEquals(consumer, harness.Consumer) || partition != Partition1 || resume is not null)
                return;

            resume = Task.Run(() => harness.Consumer.Resume(Partition1), testTimeout);
            // Bounded: a resume serialized with the restoration cannot finish until sync releases
            // it, and either order must end resumed.
            try { resume.Wait(TimeSpan.FromMilliseconds(500), testTimeout); }
            catch (OperationCanceledException) { }
        };
        try
        {
            await harness.Consumer.EnsureAssignmentAsync(CancellationToken.None);
        }
        finally
        {
            KafkaConsumer<string, string>.AfterPartitionPauseClearedForTest = null;
        }

        await Assert.That(resume).IsNotNull();
        await resume!.WaitAsync(testTimeout);
        await Assert.That(harness.Consumer.Paused).DoesNotContain(Partition1);
    }

    [Test]
    [Timeout(30_000)]
    public async Task Resume_ConcurrentWithAssignedCallbackPause_IsNotUndoneBySync(CancellationToken testTimeout)
    {
        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessAsync(
            listener,
            AssignedResponse(1, 0, 1),
            AssignedResponse(2, 0),
            AssignedResponse(3, 0, 1));

        Task? resume = null;
        listener.OnAssigned = partitions =>
        {
            if (!partitions.Contains(Partition1))
                return;

            // Another thread resumes between the callback's pause and its record of that pause.
            KafkaConsumer<string, string>.AfterPartitionPausedForTest = (consumer, partition) =>
            {
                if (!ReferenceEquals(consumer, harness.Consumer) || partition != Partition1 || resume is not null)
                    return;

                resume = Task.Run(() => harness.Consumer.Resume(Partition1), testTimeout);
                try { resume.Wait(TimeSpan.FromMilliseconds(500), testTimeout); }
                catch (OperationCanceledException) { }
            };
            try
            {
                harness.Consumer.Pause(Partition1);
            }
            finally
            {
                KafkaConsumer<string, string>.AfterPartitionPausedForTest = null;
            }
        };

        await harness.HeartbeatAsync();
        await harness.HeartbeatAsync();
        await Assert.That(resume).IsNotNull();
        await resume!.WaitAsync(testTimeout);
        await harness.Consumer.EnsureAssignmentAsync(CancellationToken.None);

        await Assert.That(harness.Consumer.Paused).DoesNotContain(Partition1);
    }

    [Test]
    public async Task EnsureAssignmentAsync_SyncRetriedAfterCancellation_KeepsReassignedCallbackPause()
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
                harness.Consumer.Pause(Partition1);
        };

        await harness.HeartbeatAsync();
        await harness.HeartbeatAsync();

        // The first sync cleans up the reassigned partition, then its offset fetch is cancelled
        // before the sync is acknowledged; the next sync repeats the cleanup.
        var fetchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        harness.Connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Interlocked.Increment(ref calls) == 1
                ? BlockUntilCancelledAsync(fetchStarted, call.ArgAt<CancellationToken>(2))
                : ValueTask.FromResult(CreateSuccessfulOffsetFetchResponse()));

        using (var cts = new CancellationTokenSource())
        {
            var sync = harness.Consumer.EnsureAssignmentAsync(cts.Token).AsTask();
            await fetchStarted.Task;
            await cts.CancelAsync();
            await Assert.That(async () => await sync).Throws<OperationCanceledException>();
        }

        await harness.Consumer.EnsureAssignmentAsync(CancellationToken.None);

        await Assert.That(harness.Consumer.Paused).Contains(Partition1);
    }

    [Test]
    public async Task EnsureAssignmentAsync_SyncRestartedAfterPositionInitialization_KeepsReassignedCallbackSeekAndPause()
    {
        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessAsync(
            listener,
            AssignedResponse(1, 0, 1),
            AssignedResponse(2, 0),
            AssignedResponse(3, 0, 1));

        listener.OnAssigned = partitions =>
        {
            if (!partitions.Contains(Partition1))
                return;

            harness.Consumer.Seek(new TopicPartitionOffset(Partition1.Topic, Partition1.Partition, 42));
            harness.Consumer.Pause(Partition1);
        };

        await harness.HeartbeatAsync();
        await harness.HeartbeatAsync();

        // A newer assignment version is published while the first sync initializes positions, so
        // that pass is not acknowledged and the next one repeats the cleanup and initialization.
        var coordinator = GetCoordinator(harness.Consumer);
        var calls = 0;
        harness.Connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    coordinator.BumpAssignmentVersionForTest();
                return ValueTask.FromResult(CreateSuccessfulOffsetFetchResponse());
            });

        await harness.Consumer.EnsureAssignmentAsync(CancellationToken.None);

        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(harness.Consumer.GetPosition(Partition1)).IsEqualTo(42L);
        await Assert.That(harness.Consumer.Paused).Contains(Partition1);
        // Acknowledged: the seek is no longer pending.
        await Assert.That(GetPendingRebalanceSeekCount(harness.Consumer)).IsEqualTo(0);
    }

    [Test]
    [Timeout(30_000)]
    public async Task Seek_FromWorkOutlivingAssignedCallback_IsNotStagedForLaterCallback(CancellationToken testTimeout)
    {
        var listener = new CallbackListener();
        await using var harness = await CreateCallbackHarnessAsync(
            listener,
            AssignedResponse(1, 0));

        var coordinator = GetCoordinator(harness.Consumer);
        var secondCallbackRunning = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool>? lateWork = null;
        listener.OnAssignedAsync = async partitions =>
        {
            if (partitions.Contains(new TopicPartition("test-topic", 0)))
            {
                // Work the first callback starts and leaves running: it seeks once the second
                // callback, which announces partition 1, is running in the same drain.
                lateWork = Task.Run(
                    async () =>
                    {
                        await secondCallbackRunning.Task;
                        var inContext = coordinator.TryGetAssignedCallbackRevocationSequence(Partition1, out _);
                        harness.Consumer.Seek(new TopicPartitionOffset(Partition1.Topic, Partition1.Partition, 42));
                        return inContext;
                    },
                    testTimeout);
                return;
            }

            secondCallbackRunning.TrySetResult();
            await lateWork!.WaitAsync(testTimeout);
        };

        await coordinator.DeliverAssignedCallbacksForTestAsync(
            [new TopicPartition("test-topic", 0)],
            [Partition1]);

        await Assert.That(await lateWork!).IsFalse();
        await Assert.That(GetPendingRebalanceSeekCount(harness.Consumer)).IsEqualTo(0);
    }

    private static async ValueTask<OffsetFetchResponse> BlockUntilCancelledAsync(
        TaskCompletionSource started,
        CancellationToken cancellationToken)
    {
        started.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("Cancellation wait completed without cancellation");
    }

    private static int GetPendingRebalanceSeekCount(KafkaConsumer<string, string> consumer) =>
        ((System.Collections.ICollection)typeof(KafkaConsumer<string, string>).GetField(
                "_pendingRebalanceSeeks",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(consumer)!).Count;

    public enum AbandonKind
    {
        Unsubscribe,
        Assign,
        Unassign
    }

    private static void Abandon(KafkaConsumer<string, string> consumer, AbandonKind kind)
    {
        switch (kind)
        {
            case AbandonKind.Unsubscribe:
                consumer.Unsubscribe();
                break;
            case AbandonKind.Assign:
                consumer.Assign(new TopicPartition("test-topic", 0));
                break;
            default:
                consumer.Unsubscribe();
                consumer.Unassign();
                break;
        }
    }

    public enum SeekKind
    {
        Offset,
        Beginning,
        End
    }

    private static void Seek(KafkaConsumer<string, string> consumer, SeekKind kind)
    {
        switch (kind)
        {
            case SeekKind.Offset:
                consumer.Seek(new TopicPartitionOffset(Partition1.Topic, Partition1.Partition, 42));
                break;
            case SeekKind.Beginning:
                consumer.SeekToBeginning(Partition1);
                break;
            default:
                consumer.SeekToEnd(Partition1);
                break;
        }
    }

    private static long ExpectedSeekOffset(SeekKind kind) => kind switch
    {
        SeekKind.Offset => 42L,
        SeekKind.Beginning => 0L,
        _ => -1L
    };

    private static ConsumerGroupHeartbeatResponse AssignedResponse(int memberEpoch, params int[] partitions) =>
        CreateHeartbeatResponse(CreateAssignment(partitions), memberEpoch);

    private static ConsumerGroupHeartbeatResponse FencedResponse() => new()
    {
        ErrorCode = Dekaf.Protocol.ErrorCode.FencedMemberEpoch,
        MemberId = "member-1",
        MemberEpoch = -1,
        HeartbeatIntervalMs = 60000
    };

    private static async Task<CallbackHarness> CreateCallbackHarnessAsync(
        CallbackListener listener,
        params ConsumerGroupHeartbeatResponse[] script) =>
        await CreateCallbackHarnessCoreAsync(listener, null, script);

    private static async Task<CallbackHarness> CreateCallbackHarnessAsync(
        IConsumerAwareRebalanceListener consumerAwareListener,
        params ConsumerGroupHeartbeatResponse[] script) =>
        await CreateCallbackHarnessCoreAsync(null, consumerAwareListener, script);

    private static async Task<CallbackHarness> CreateCallbackHarnessCoreAsync(
        IRebalanceListener? listener,
        IConsumerAwareRebalanceListener? consumerAwareListener,
        ConsumerGroupHeartbeatResponse[] script,
        AutoOffsetReset? newPartitionsReset = null,
        bool initialSync = true)
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);

        var metadataManager = CreateMetadataManager(connectionPool);
        SetupFindCoordinator(connection);
        var callCount = 0;
        connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var index = Math.Min(Interlocked.Increment(ref callCount), script.Length) - 1;
                return ValueTask.FromResult(script[index]);
            });
        SetupOffsetFetch(connection);

        var consumer = CreateGroupConsumer(
            connectionPool,
            metadataManager,
            autoOffsetResetNewPartitions: newPartitionsReset,
            rebalanceListener: listener,
            consumerAwareRebalanceListener: consumerAwareListener);
        consumer.Subscribe("test-topic");
        if (initialSync)
            await consumer.EnsureAssignmentAsync(CancellationToken.None);
        return new CallbackHarness(consumer, metadataManager, connection);
    }

    private sealed class CallbackHarness(
        KafkaConsumer<string, string> consumer,
        Dekaf.Metadata.MetadataManager metadataManager,
        IKafkaConnection connection) : IAsyncDisposable
    {
        public KafkaConsumer<string, string> Consumer { get; } = consumer;

        public IKafkaConnection Connection { get; } = connection;

        /// <summary>Runs one heartbeat round trip and delivers its rebalance callbacks.</summary>
        public async Task HeartbeatAsync()
        {
            var coordinator = GetCoordinator(Consumer);
            coordinator.RequestRejoin();
            await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        }

        public async ValueTask DisposeAsync()
        {
            await Consumer.DisposeAsync();
            await metadataManager.DisposeAsync();
        }
    }

    private sealed class CallbackListener : IRebalanceListener, IConsumerAwareRebalanceListener
    {
        public Action<IReadOnlyCollection<TopicPartition>>? OnAssigned { get; set; }

        public Action<IRebalanceConsumer, IReadOnlyCollection<TopicPartition>>? OnAssignedConsumer { get; set; }

        public Action<IReadOnlyCollection<TopicPartition>>? OnLost { get; set; }

        public Func<IReadOnlyCollection<TopicPartition>, Task>? OnAssignedAsync { get; set; }

        public async ValueTask OnPartitionsAssignedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            var list = partitions.ToList();
            OnAssigned?.Invoke(list);
            if (OnAssignedAsync is { } onAssignedAsync)
                await onAssignedAsync(list);
        }

        public ValueTask OnPartitionsRevokedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask OnPartitionsLostAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            OnLost?.Invoke(partitions.ToList());
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsAssignedAsync(
            IRebalanceConsumer consumer,
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            OnAssignedConsumer?.Invoke(consumer, partitions.ToList());
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsRevokedAsync(
            IRebalanceConsumer consumer,
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask OnPartitionsLostAsync(
            IRebalanceConsumer consumer,
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }
}
