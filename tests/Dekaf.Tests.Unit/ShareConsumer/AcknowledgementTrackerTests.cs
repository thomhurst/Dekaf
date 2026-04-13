using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public class AcknowledgementTrackerTests
{
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
    public async Task Acknowledge_UnknownOffset_IsIgnored()
    {
        var tracker = new AcknowledgementTracker();
        var tp = new TopicPartition("topic1", 0);

        tracker.TrackDeliveredRecords(tp, 10, 12);

        // Acknowledge an offset that was never tracked
        tracker.Acknowledge(tp, 99, AcknowledgeType.Reject);

        var result = tracker.Flush();
        var batch = result[tp][0];

        // All remain Accept
        await Assert.That(batch.AcknowledgeTypes.Length).IsEqualTo(3);
        foreach (byte ackType in batch.AcknowledgeTypes)
        {
            await Assert.That(ackType).IsEqualTo((byte)AcknowledgeType.Accept);
        }
    }

    [Test]
    public async Task Acknowledge_UnknownPartition_IsIgnored()
    {
        var tracker = new AcknowledgementTracker();
        var tp = new TopicPartition("topic1", 0);
        var unknownTp = new TopicPartition("topic1", 99);

        tracker.TrackDeliveredRecords(tp, 10, 12);

        // Acknowledge on a partition that was never tracked
        tracker.Acknowledge(unknownTp, 10, AcknowledgeType.Reject);

        var result = tracker.Flush();
        await Assert.That(result).ContainsKey(tp);
        await Assert.That(result.ContainsKey(unknownTp)).IsFalse();
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
}
