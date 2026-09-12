using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public class AcknowledgementTrackerTests
{
    [Test]
    public async Task Flush_DoesNotBorrowOrClearOutstandingPollContainers()
    {
        var tracker = new AcknowledgementTracker();
        var partition = new TopicPartition("topic", 0);
        tracker.TrackDeliveredRecords(partition, 10, 12);
        var poll = tracker.FlushForPoll();
        tracker.TrackDeliveredRecords(partition, 20, 22);
        var owned = tracker.Flush();
        tracker.ReturnPollBatches(owned);
        await Assert.That(poll[partition][0].FirstOffset).IsEqualTo(10);
        tracker.ReturnPollBatches(poll);
        tracker.TrackDeliveredRecords(partition, 30, 32);
        var nextPoll = tracker.FlushForPoll();
        tracker.ReturnPollBatches(nextPoll);
        await Assert.That(owned[partition][0].FirstOffset).IsEqualTo(20);
        await Assert.That(owned[partition][0].LastOffset).IsEqualTo(22);
    }

    [Test]
    public async Task FlushForPoll_DoesNotReuseAnOutstandingSnapshot()
    {
        var tracker = new AcknowledgementTracker();
        var partition = new TopicPartition("topic", 0);
        tracker.TrackDeliveredRecords(partition, 10, 12);
        var first = tracker.FlushForPoll();
        tracker.TrackDeliveredRecords(partition, 20, 22);
        var second = tracker.FlushForPoll();
        tracker.ReturnPollBatches(second);
        await Assert.That(first[partition][0].FirstOffset).IsEqualTo(10);
        tracker.ReturnPollBatches(first);
        await Assert.That(second[partition][0].FirstOffset).IsEqualTo(20);
    }

    [Test]
    public async Task FlushForPoll_MaterializationFailureReturnsTemporaryContainers()
    {
        var tracker = new AcknowledgementTracker();
        var partition = new TopicPartition("topic", 0);
        tracker.TrackDeliveredRecords(partition, 0, 1);
        tracker.TrackDeliveredRecords(new("topic", 1), 0, long.MaxValue);
        await Assert.That(() => tracker.FlushForPoll()).Throws<OverflowException>();
        await Assert.That(tracker.HasPending).IsFalse();
        tracker.TrackDeliveredRecords(partition, 20, 22);
        var snapshot = tracker.FlushForPoll();
        await Assert.That(snapshot.Count).IsEqualTo(1);
        await Assert.That(snapshot[partition][0].FirstOffset).IsEqualTo(20);
        tracker.ReturnPollBatches(snapshot);
    }

    [Test]
    [Arguments(AcknowledgeType.Accept)]
    [Arguments(AcknowledgeType.Reject)]
    [Arguments(AcknowledgeType.Renew)]
    public async Task ReleaseRange_RetryPreservesEveryOffsetAndNewExplicitOutcome(AcknowledgeType outcome)
    {
        var tracker = new AcknowledgementTracker();
        var partition = new TopicPartition("topic", 0);
        tracker.ReleaseUndeliveredRecords(partition, 100, 227);
        var submitted = tracker.Flush();
        var release = submitted[partition][0];
        await Assert.That(release.AcknowledgeTypes).IsEquivalentTo(new byte[] { 2 });
        await Assert.That(release.FirstOffset).IsEqualTo(100);
        await Assert.That(release.LastOffset).IsEqualTo(227);

        tracker.Acknowledge(partition, 150, outcome, requireTracked: false);
        tracker.RequeueAcks(submitted);
        var retried = tracker.Flush()[partition];
        var offsets = new ShareAcknowledgedOffsets(retried);
        await Assert.That(offsets.Length).IsEqualTo(128);
        foreach (var batch in retried)
            for (var index = 0; index < batch.OffsetCount; index++)
                await Assert.That(batch.GetAcknowledgeType(index)).IsEqualTo(
                    (byte)(batch.FirstOffset + index == 150 ? outcome : AcknowledgeType.Release));
        await Assert.That(release.AcknowledgeTypes).IsEquivalentTo(new byte[] { 2 });
    }

    [Test]
    [Arguments(1)]
    [Arguments(64)]
    public async Task Flush_ReusedStateDoesNotChangePreviousBatchesOrCarryExplicitOutcomes(int partitionCount)
    {
        var tracker = new AcknowledgementTracker();
        var first = new TopicPartition("first", 0);
        var second = new TopicPartition("second", 1);
        tracker.TrackDeliveredRecords(first, 10, 12);
        tracker.Acknowledge(first, 11, AcknowledgeType.Reject);
        for (var index = 1; index < partitionCount; index++)
            tracker.TrackDeliveredRecords(new TopicPartition("first", index), 10, 12);
        var original = tracker.Flush();

        tracker.TrackDeliveredRecords(second, 40, 42);
        var released = tracker.Flush(releaseImplicit: true);
        tracker.TrackDeliveredRecords(first, 50, 50);
        var final = tracker.Flush();

        await Assert.That(original.Count).IsEqualTo(partitionCount);
        await Assert.That(original[first][0].FirstOffset).IsEqualTo(10);
        await Assert.That(original[first][0].LastOffset).IsEqualTo(12);
        await Assert.That(original[first][0].AcknowledgeTypes[0]).IsEqualTo((byte)AcknowledgeType.Accept);
        await Assert.That(original[first][0].AcknowledgeTypes[1]).IsEqualTo((byte)AcknowledgeType.Reject);
        await Assert.That(original[first][0].AcknowledgeTypes[2]).IsEqualTo((byte)AcknowledgeType.Accept);
        await Assert.That(released.Count).IsEqualTo(1);
        await Assert.That(released[second][0].AcknowledgeTypes).IsEquivalentTo(new byte[] { 2, 2, 2 });
        await Assert.That(final.Count).IsEqualTo(1);
        await Assert.That(final[first][0].FirstOffset).IsEqualTo(50);
        await Assert.That(final[first][0].AcknowledgeTypes).IsEquivalentTo(new byte[] { 1 });
        await Assert.That(tracker.HasPending).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Flush_MaterializationFailureDetachesPendingState(bool shrinkBeforeFailure)
    {
        var tracker = new AcknowledgementTracker();
        if (shrinkBeforeFailure)
        {
            for (var index = 0; index < 64; index++)
                tracker.TrackDeliveredRecords(new TopicPartition("previous", index), 0, 0);
            tracker.Flush();
        }
        var valid = new TopicPartition("valid", 0);
        var oversized = new TopicPartition("oversized", 1);
        tracker.TrackDeliveredRecords(valid, 0, 0);
        tracker.TrackDeliveredRecords(oversized, 0, long.MaxValue);
        await Assert.That(() => tracker.Flush()).Throws<OverflowException>();
        await Assert.That(tracker.HasPending).IsFalse();

        tracker.TrackDeliveredRecords(valid, 50, 50);
        var result = tracker.Flush();
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[valid][0].FirstOffset).IsEqualTo(50);
        await Assert.That(result[valid][0].AcknowledgeTypes).IsEquivalentTo(new byte[] { 1 });
    }

    [Test]
    public async Task CloseFlush_ReleasesImplicitAndPreservesExplicitOutcomes()
    {
        var tracker = new AcknowledgementTracker();
        var partition = new TopicPartition("topic", 0);
        tracker.TrackDeliveredRecords(partition, 0, 4);
        tracker.Acknowledge(partition, 1, AcknowledgeType.Accept);
        tracker.Acknowledge(partition, 2, AcknowledgeType.Release);
        tracker.Acknowledge(partition, 3, AcknowledgeType.Reject);
        tracker.Acknowledge(partition, 4, AcknowledgeType.Renew);

        var types = tracker.Flush(releaseImplicit: true)[partition][0].AcknowledgeTypes;

        byte[] expected = [2, 1, 2, 3, 4];
        await Assert.That(types).IsEquivalentTo(expected);
        await Assert.That(types[0]).IsEqualTo((byte)AcknowledgeType.Release);
        await Assert.That(types[1]).IsEqualTo((byte)AcknowledgeType.Accept);
    }

    [Test]
    public async Task CloseFlush_RetriedImplicitCommit_PreservesSubmittedAccept()
    {
        var tracker = new AcknowledgementTracker();
        var partition = new TopicPartition("topic", 0);
        tracker.TrackDeliveredRecords(partition, 0, 0);
        var submitted = tracker.Flush();
        tracker.RequeueAcks(submitted);
        tracker.TrackDeliveredRecords(partition, 1, 1);

        var types = tracker.Flush(releaseImplicit: true)[partition][0].AcknowledgeTypes;

        await Assert.That(types[0]).IsEqualTo((byte)AcknowledgeType.Accept);
        await Assert.That(types[1]).IsEqualTo((byte)AcknowledgeType.Release);
    }

    [Test]
    public async Task CloseFlush_RetriedRelease_RemainsReleaseOnOrdinaryCommit()
    {
        var tracker = new AcknowledgementTracker();
        var partition = new TopicPartition("topic", 0);
        tracker.TrackDeliveredRecords(partition, 0, 0);
        tracker.RequeueAcks(tracker.Flush(releaseImplicit: true));

        var types = tracker.Flush()[partition][0].AcknowledgeTypes;

        await Assert.That(types[0]).IsEqualTo((byte)AcknowledgeType.Release);
    }

    [Test]
    public async Task TrackDeliveredRecords_AllDefaultToAccept()
    {
        var tracker = new AcknowledgementTracker();
        var tp = new TopicPartition("topic1", 0);

        tracker.TrackDeliveredRecords(tp, 10, 14);

        var result = tracker.Flush();

        await Assert.That(result).ContainsKey(tp);
        await Assert.That(result[tp].Count).IsEqualTo(1);

        var batch = result[tp][0];
        await Assert.That(batch.FirstOffset).IsEqualTo(10);
        await Assert.That(batch.LastOffset).IsEqualTo(14);
        await Assert.That(batch.AcknowledgeTypes.Length).IsEqualTo(5);

        // All should be Accept (1)
        foreach (byte ackType in batch.AcknowledgeTypes)
        {
            await Assert.That(ackType).IsEqualTo((byte)AcknowledgeType.Accept);
        }
    }

    [Test]
    public async Task Acknowledge_ChangesSpecificOffset()
    {
        var tracker = new AcknowledgementTracker();
        var tp = new TopicPartition("topic1", 0);

        tracker.TrackDeliveredRecords(tp, 10, 14);
        tracker.Acknowledge(tp, 12, AcknowledgeType.Release);

        var result = tracker.Flush();
        var batch = result[tp][0];

        await Assert.That(batch.AcknowledgeTypes[0]).IsEqualTo((byte)AcknowledgeType.Accept);
        await Assert.That(batch.AcknowledgeTypes[1]).IsEqualTo((byte)AcknowledgeType.Accept);
        await Assert.That(batch.AcknowledgeTypes[2]).IsEqualTo((byte)AcknowledgeType.Release);
        await Assert.That(batch.AcknowledgeTypes[3]).IsEqualTo((byte)AcknowledgeType.Accept);
        await Assert.That(batch.AcknowledgeTypes[4]).IsEqualTo((byte)AcknowledgeType.Accept);
    }

    [Test]
    public async Task Acknowledge_Reject_SetsCorrectType()
    {
        var tracker = new AcknowledgementTracker();
        var tp = new TopicPartition("topic1", 0);

        tracker.TrackDeliveredRecords(tp, 5, 7);
        tracker.Acknowledge(tp, 6, AcknowledgeType.Reject);

        var result = tracker.Flush();
        var batch = result[tp][0];

        await Assert.That(batch.AcknowledgeTypes[1]).IsEqualTo((byte)AcknowledgeType.Reject);
    }

    [Test]
    public async Task Acknowledge_UnknownOffset_Throws()
    {
        var tracker = new AcknowledgementTracker();
        var tp = new TopicPartition("topic1", 0);

        tracker.TrackDeliveredRecords(tp, 10, 12);

        // Acknowledge an offset that was never tracked — should throw
        await Assert.That(() => tracker.Acknowledge(tp, 99, AcknowledgeType.Reject))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Acknowledge_UnknownPartition_Throws()
    {
        var tracker = new AcknowledgementTracker();
        var tp = new TopicPartition("topic1", 0);
        var unknownTp = new TopicPartition("topic1", 99);

        tracker.TrackDeliveredRecords(tp, 10, 12);

        // Acknowledge on a partition that was never tracked — should throw
        await Assert.That(() => tracker.Acknowledge(unknownTp, 10, AcknowledgeType.Reject))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Acknowledge_UntrackedAllowed_AddsExplicitAck()
    {
        var tracker = new AcknowledgementTracker();
        var tp = new TopicPartition("topic1", 0);

        tracker.Acknowledge(tp, 10, AcknowledgeType.Release, requireTracked: false);

        var result = tracker.Flush();
        var batch = result[tp][0];

        await Assert.That(batch.FirstOffset).IsEqualTo(10);
        await Assert.That(batch.LastOffset).IsEqualTo(10);
        await Assert.That(batch.AcknowledgeTypes.Length).IsEqualTo(1);
        await Assert.That(batch.AcknowledgeTypes[0]).IsEqualTo((byte)AcknowledgeType.Release);
    }

    [Test]
    public async Task Flush_ClearsTrackedState()
    {
        var tracker = new AcknowledgementTracker();
        var tp = new TopicPartition("topic1", 0);

        tracker.TrackDeliveredRecords(tp, 10, 12);

        var firstFlush = tracker.Flush();
        await Assert.That(firstFlush).ContainsKey(tp);

        // Second flush should be empty
        var secondFlush = tracker.Flush();
        await Assert.That(secondFlush.Count).IsEqualTo(0);
    }

    [Test]
    public async Task HasPending_TrueAfterTrack_FalseAfterFlush()
    {
        var tracker = new AcknowledgementTracker();
        var tp = new TopicPartition("topic1", 0);

        await Assert.That(tracker.HasPending).IsFalse();

        tracker.TrackDeliveredRecords(tp, 0, 2);
        await Assert.That(tracker.HasPending).IsTrue();

        tracker.Flush();
        await Assert.That(tracker.HasPending).IsFalse();
    }

    [Test]
    public async Task NonConsecutiveOffsets_ProducesSeparateBatches()
    {
        var tracker = new AcknowledgementTracker();
        var tp = new TopicPartition("topic1", 0);

        // Track two non-consecutive ranges
        tracker.TrackDeliveredRecords(tp, 10, 12);
        tracker.TrackDeliveredRecords(tp, 20, 22);

        var result = tracker.Flush();

        await Assert.That(result[tp].Count).IsEqualTo(2);

        var batch1 = result[tp][0];
        await Assert.That(batch1.FirstOffset).IsEqualTo(10);
        await Assert.That(batch1.LastOffset).IsEqualTo(12);
        await Assert.That(batch1.AcknowledgeTypes.Length).IsEqualTo(3);

        var batch2 = result[tp][1];
        await Assert.That(batch2.FirstOffset).IsEqualTo(20);
        await Assert.That(batch2.LastOffset).IsEqualTo(22);
        await Assert.That(batch2.AcknowledgeTypes.Length).IsEqualTo(3);
    }

    [Test]
    public async Task MultiplePartitions_TrackedIndependently()
    {
        var tracker = new AcknowledgementTracker();
        var tp0 = new TopicPartition("topic1", 0);
        var tp1 = new TopicPartition("topic1", 1);

        tracker.TrackDeliveredRecords(tp0, 0, 2);
        tracker.TrackDeliveredRecords(tp1, 100, 102);

        tracker.Acknowledge(tp0, 1, AcknowledgeType.Release);
        tracker.Acknowledge(tp1, 101, AcknowledgeType.Reject);

        var result = tracker.Flush();

        await Assert.That(result.Count).IsEqualTo(2);

        await Assert.That(result[tp0][0].AcknowledgeTypes[1]).IsEqualTo((byte)AcknowledgeType.Release);
        await Assert.That(result[tp1][0].AcknowledgeTypes[1]).IsEqualTo((byte)AcknowledgeType.Reject);
    }

    [Test]
    public async Task SingleOffset_ProducesSingleElementBatch()
    {
        var tracker = new AcknowledgementTracker();
        var tp = new TopicPartition("topic1", 0);

        tracker.TrackDeliveredRecords(tp, 42, 42);

        var result = tracker.Flush();

        await Assert.That(result[tp].Count).IsEqualTo(1);

        var batch = result[tp][0];
        await Assert.That(batch.FirstOffset).IsEqualTo(42);
        await Assert.That(batch.LastOffset).IsEqualTo(42);
        await Assert.That(batch.AcknowledgeTypes.Length).IsEqualTo(1);
        await Assert.That(batch.AcknowledgeTypes[0]).IsEqualTo((byte)AcknowledgeType.Accept);
    }

    [Test]
    public async Task Acknowledge_Renew_SetsCorrectType()
    {
        var tracker = new AcknowledgementTracker();
        var tp = new TopicPartition("topic1", 0);

        tracker.TrackDeliveredRecords(tp, 0, 2);
        tracker.Acknowledge(tp, 1, AcknowledgeType.Renew);

        var result = tracker.Flush();

        await Assert.That(result[tp][0].AcknowledgeTypes[1]).IsEqualTo((byte)AcknowledgeType.Renew);
    }

    [Test]
    public async Task ConsecutiveRanges_MergedIntoSingleBatch()
    {
        var tracker = new AcknowledgementTracker();
        var tp = new TopicPartition("topic1", 0);

        // Track two consecutive ranges — they should merge into one batch
        tracker.TrackDeliveredRecords(tp, 10, 12);
        tracker.TrackDeliveredRecords(tp, 13, 15);

        var result = tracker.Flush();

        await Assert.That(result[tp].Count).IsEqualTo(1);

        var batch = result[tp][0];
        await Assert.That(batch.FirstOffset).IsEqualTo(10);
        await Assert.That(batch.LastOffset).IsEqualTo(15);
        await Assert.That(batch.AcknowledgeTypes.Length).IsEqualTo(6);
    }

    [Test]
    public async Task MultipleDifferentTopics_TrackedIndependently()
    {
        var tracker = new AcknowledgementTracker();
        var tp1 = new TopicPartition("topic-a", 0);
        var tp2 = new TopicPartition("topic-b", 0);

        tracker.TrackDeliveredRecords(tp1, 0, 1);
        tracker.TrackDeliveredRecords(tp2, 0, 1);

        tracker.Acknowledge(tp1, 0, AcknowledgeType.Reject);

        var result = tracker.Flush();

        // topic-a offset 0 should be Reject
        await Assert.That(result[tp1][0].AcknowledgeTypes[0]).IsEqualTo((byte)AcknowledgeType.Reject);
        // topic-b offset 0 should still be Accept
        await Assert.That(result[tp2][0].AcknowledgeTypes[0]).IsEqualTo((byte)AcknowledgeType.Accept);
    }

    [Test]
    public async Task EmptyTracker_FlushReturnsEmpty()
    {
        var tracker = new AcknowledgementTracker();

        var result = tracker.Flush();

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Acknowledge_OverwritesPreviousAck()
    {
        var tracker = new AcknowledgementTracker();
        var tp = new TopicPartition("topic1", 0);

        tracker.TrackDeliveredRecords(tp, 10, 12);

        tracker.Acknowledge(tp, 11, AcknowledgeType.Release);
        tracker.Acknowledge(tp, 11, AcknowledgeType.Reject);

        var result = tracker.Flush();

        // Last ack wins
        await Assert.That(result[tp][0].AcknowledgeTypes[1]).IsEqualTo((byte)AcknowledgeType.Reject);
    }

    [Test]
    public async Task RequeueAcks_RestoresFlushDataBackIntoTracker()
    {
        var tracker = new AcknowledgementTracker();
        var tp = new TopicPartition("topic1", 0);

        tracker.TrackDeliveredRecords(tp, 10, 12);
        tracker.Acknowledge(tp, 11, AcknowledgeType.Release);

        var flushed = tracker.Flush();
        await Assert.That(tracker.HasPending).IsFalse();

        // Re-queue the flushed data
        tracker.RequeueAcks(flushed);
        await Assert.That(tracker.HasPending).IsTrue();

        // Flush again — should produce the same batches
        var result = tracker.Flush();
        await Assert.That(result).ContainsKey(tp);

        var batch = result[tp][0];
        await Assert.That(batch.FirstOffset).IsEqualTo(10);
        await Assert.That(batch.LastOffset).IsEqualTo(12);
        await Assert.That(batch.AcknowledgeTypes[0]).IsEqualTo((byte)AcknowledgeType.Accept);
        await Assert.That(batch.AcknowledgeTypes[1]).IsEqualTo((byte)AcknowledgeType.Release);
        await Assert.That(batch.AcknowledgeTypes[2]).IsEqualTo((byte)AcknowledgeType.Accept);
    }

    [Test]
    public async Task RequeueAcks_PreservesNewerExplicitAckOverRequeued()
    {
        var tracker = new AcknowledgementTracker();
        var tp = new TopicPartition("topic1", 0);

        // Track and flush initial records (all Accept by default)
        tracker.TrackDeliveredRecords(tp, 10, 12);
        var flushed = tracker.Flush();

        // Simulate: new records delivered for same offsets (e.g., redelivery after failed commit)
        tracker.TrackDeliveredRecords(tp, 10, 12);
        // User explicitly rejects offset 11
        tracker.Acknowledge(tp, 11, AcknowledgeType.Reject);

        // Now re-queue the stale flushed data (all Accept) — TryAdd should NOT overwrite
        // the newer Reject that was set after the flush
        tracker.RequeueAcks(flushed);

        var result = tracker.Flush();
        await Assert.That(result).ContainsKey(tp);

        var batch = result[tp][0];
        await Assert.That(batch.FirstOffset).IsEqualTo(10);
        await Assert.That(batch.LastOffset).IsEqualTo(12);
        await Assert.That(batch.AcknowledgeTypes[0]).IsEqualTo((byte)AcknowledgeType.Accept);
        // The explicit Reject should survive — TryAdd preserves the existing entry
        await Assert.That(batch.AcknowledgeTypes[1]).IsEqualTo((byte)AcknowledgeType.Reject);
        await Assert.That(batch.AcknowledgeTypes[2]).IsEqualTo((byte)AcknowledgeType.Accept);
    }

    [Test]
    public async Task RequeueAcks_PreservesOlderRangeWhenNewerRangeAlreadyTracked()
    {
        var tracker = new AcknowledgementTracker();
        var tp = new TopicPartition("topic1", 0);

        tracker.TrackDeliveredRecords(tp, 50, 60);
        var flushed = tracker.Flush();

        tracker.TrackDeliveredRecords(tp, 100, 110);
        tracker.RequeueAcks(flushed);

        var result = tracker.Flush();
        await Assert.That(result).ContainsKey(tp);
        await Assert.That(result[tp].Count).IsEqualTo(2);

        var olderBatch = result[tp][0];
        await Assert.That(olderBatch.FirstOffset).IsEqualTo(50);
        await Assert.That(olderBatch.LastOffset).IsEqualTo(60);
        await Assert.That(olderBatch.AcknowledgeTypes.Length).IsEqualTo(11);

        foreach (var ackType in olderBatch.AcknowledgeTypes)
        {
            await Assert.That(ackType).IsEqualTo((byte)AcknowledgeType.Accept);
        }

        var newerBatch = result[tp][1];
        await Assert.That(newerBatch.FirstOffset).IsEqualTo(100);
        await Assert.That(newerBatch.LastOffset).IsEqualTo(110);
        await Assert.That(newerBatch.AcknowledgeTypes.Length).IsEqualTo(11);
    }

    [Test]
    public async Task RequeueAcks_PreservesOlderRangeAndExplicitGapAck()
    {
        var tracker = new AcknowledgementTracker();
        var tp = new TopicPartition("topic1", 0);

        tracker.TrackDeliveredRecords(tp, 50, 60);
        var flushed = tracker.Flush();

        tracker.TrackDeliveredRecords(tp, 100, 110);
        tracker.Acknowledge(tp, 75, AcknowledgeType.Release, requireTracked: false);
        tracker.RequeueAcks(flushed);

        var result = tracker.Flush();
        await Assert.That(result).ContainsKey(tp);
        await Assert.That(result[tp].Count).IsEqualTo(3);

        var olderBatch = result[tp][0];
        await Assert.That(olderBatch.FirstOffset).IsEqualTo(50);
        await Assert.That(olderBatch.LastOffset).IsEqualTo(60);

        var gapBatch = result[tp][1];
        await Assert.That(gapBatch.FirstOffset).IsEqualTo(75);
        await Assert.That(gapBatch.LastOffset).IsEqualTo(75);
        await Assert.That(gapBatch.AcknowledgeTypes[0]).IsEqualTo((byte)AcknowledgeType.Release);

        var newerBatch = result[tp][2];
        await Assert.That(newerBatch.FirstOffset).IsEqualTo(100);
        await Assert.That(newerBatch.LastOffset).IsEqualTo(110);
    }

    [Test]
    public async Task TrackDeliveredRecords_OverwritesRequeuedDifferentTypeRange()
    {
        var tracker = new AcknowledgementTracker();
        var tp = new TopicPartition("topic1", 0);

        tracker.TrackDeliveredRecords(tp, 10, 10);
        tracker.Acknowledge(tp, 10, AcknowledgeType.Reject);
        var flushed = tracker.Flush();

        tracker.RequeueAcks(flushed);
        tracker.TrackDeliveredRecords(tp, 10, 10);

        var result = tracker.Flush();
        await Assert.That(result).ContainsKey(tp);
        await Assert.That(result[tp].Count).IsEqualTo(1);

        var batch = result[tp][0];
        await Assert.That(batch.FirstOffset).IsEqualTo(10);
        await Assert.That(batch.LastOffset).IsEqualTo(10);
        await Assert.That(batch.AcknowledgeTypes[0]).IsEqualTo((byte)AcknowledgeType.Accept);
    }

    [Test]
    public async Task TrackDeliveredRecords_SplitsRequeuedDifferentTypeOverlap()
    {
        var tracker = new AcknowledgementTracker();
        var tp = new TopicPartition("topic1", 0);

        tracker.TrackDeliveredRecords(tp, 10, 14);
        for (var offset = 10; offset <= 14; offset++)
        {
            tracker.Acknowledge(tp, offset, AcknowledgeType.Reject);
        }

        var flushed = tracker.Flush();

        tracker.RequeueAcks(flushed);
        tracker.TrackDeliveredRecords(tp, 12, 16);

        var result = tracker.Flush();
        await Assert.That(result).ContainsKey(tp);
        await Assert.That(result[tp].Count).IsEqualTo(1);

        var batch = result[tp][0];
        await Assert.That(batch.FirstOffset).IsEqualTo(10);
        await Assert.That(batch.LastOffset).IsEqualTo(16);
        for (var i = 0; i < 2; i++)
        {
            await Assert.That(batch.AcknowledgeTypes[i]).IsEqualTo((byte)AcknowledgeType.Reject);
        }

        for (var i = 2; i < batch.AcknowledgeTypes.Length; i++)
        {
            await Assert.That(batch.AcknowledgeTypes[i]).IsEqualTo((byte)AcknowledgeType.Accept);
        }
    }
}
