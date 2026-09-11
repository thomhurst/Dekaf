using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed class ShareConsumeBatchTests
{
    [Test]
    public async Task Parser_TruncatedTailPreservesCompleteRecords()
    {
        var raw = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(raw);
        new Record { OffsetDelta = 0, IsKeyNull = true, IsValueNull = true }.Write(ref writer);
        var tail = new ArrayBufferWriter<byte>();
        var tailWriter = new KafkaProtocolWriter(tail);
        new Record { OffsetDelta = 1, IsKeyNull = true, Value = "partial"u8.ToArray() }.Write(ref tailWriter);
        writer.WriteRawBytes(tail.WrittenSpan[..^3]);
        await using var consumer = CreateConsumer(ShareAcknowledgementMode.Implicit);
        using var batch = await consumer.ParseRecordBatchAsync(
            new TopicPartition("batch", 0), WrapRawRecords(raw.WrittenMemory, 2), Acquired(100, 101), 2, default);
        await Assert.That(batch.Count).IsEqualTo(1);
        var records = batch.GetEnumerator();
        await Assert.That(records.MoveNext()).IsTrue();
        await Assert.That(records.Current.Offset).IsEqualTo(100);
        await Assert.That(records.Current.IsKeyNull).IsTrue();
        await Assert.That(records.Current.IsValueNull).IsTrue();
    }

    [Test]
    public async Task Parser_InteriorCorruptionDoesNotBecomeTailTruncation()
    {
        var body = new ArrayBufferWriter<byte>();
        var bodyWriter = new KafkaProtocolWriter(body);
        bodyWriter.WriteInt8(0);
        bodyWriter.WriteVarLong(0);
        bodyWriter.WriteVarInt(1);
        bodyWriter.WriteVarInt(-1);
        bodyWriter.WriteVarInt(-1);
        bodyWriter.WriteVarInt(1);
        bodyWriter.WriteVarInt(-1); // A null header key is invalid inside a complete record body.
        bodyWriter.WriteVarInt(-1);
        var raw = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(raw);
        new Record { OffsetDelta = 0, IsKeyNull = true, IsValueNull = true }.Write(ref writer);
        writer.WriteVarInt(body.WrittenCount);
        writer.WriteRawBytes(body.WrittenSpan);
        new Record { OffsetDelta = 2, IsKeyNull = true, IsValueNull = true }.Write(ref writer);
        await using var consumer = CreateConsumer(ShareAcknowledgementMode.Implicit);
        await Assert.That(async () => await consumer.ParseRecordBatchAsync(
            new TopicPartition("batch", 0), WrapRawRecords(raw.WrittenMemory, 3), Acquired(100, 102), 3, default))
            .Throws<MalformedProtocolDataException>();
    }

    [Test]
    [NotInParallel]
    public async Task CancelledPreparation_ReturnsSourceEvenWhenPreparerIgnoresCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await using var consumer = CreateConsumer(ShareAcknowledgementMode.Explicit,
            value: new CancellingDeserializer(cancellation));
        var source = CreateSource(1);
        RecordBatch.BeginTrackingPoolReturnsForCurrentThread();
        var cancelled = false;
        int returned;
        try
        {
            using var batch = await consumer.ParseRecordBatchAsync(
                new TopicPartition("batch", 0), source, Acquired(100, 100), 1, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        finally
        {
            returned = RecordBatch.EndTrackingPoolReturnsForCurrentThread();
        }
        await Assert.That(cancelled).IsTrue();
        await Assert.That(returned).IsEqualTo(1);
    }

    [Test]
    [NotInParallel]
    public async Task Tracker_DisposingUnreadLeaseReturnsSourceExactlyOnce()
    {
        await using var consumer = CreateConsumer(ShareAcknowledgementMode.Explicit);
        using var tracker = new ShareBatchAcknowledgements<int, int>();
        using var batch = await consumer.ParseRecordBatchAsync(
            new TopicPartition("batch", 0), CreateSource(3), Acquired(100, 102), 3, default);
        tracker.Register(batch);
        int returned;
        RecordBatch.BeginTrackingPoolReturnsForCurrentThread();
        try
        {
            batch.Dispose();
            batch.Dispose();
        }
        finally
        {
            returned = RecordBatch.EndTrackingPoolReturnsForCurrentThread();
        }
        await Assert.That(returned).IsEqualTo(1);
        await Assert.That(tracker.RetainedBatchCount).IsEqualTo(0);
    }

    [Test]
    [Arguments(ShareAcknowledgementMode.Implicit)]
    [Arguments(ShareAcknowledgementMode.Explicit)]
    public async Task Tracker_ClosedLeasePreservesPendingAcknowledgementUntilCompletion(ShareAcknowledgementMode mode)
    {
        await using var consumer = CreateConsumer(mode);
        using var tracker = new ShareBatchAcknowledgements<int, int>();
        using var batch = await consumer.ParseRecordBatchAsync(
            new TopicPartition("batch", 0), CreateSource(3), Acquired(100, 102), 3, default);
        tracker.Register(batch);
        var records = batch.GetEnumerator();
        await Assert.That(records.MoveNext()).IsTrue();
        if (mode == ShareAcknowledgementMode.Explicit)
            batch.Acknowledge(records.Current);
        batch.Dispose();
        await Assert.That(tracker.RetainedBatchCount).IsEqualTo(1);
        await Assert.That(batch.Storage.TrackedCount).IsEqualTo(1);
        var pending = tracker.Flush();
        var wire = pending[new TopicPartition("batch", 0)];
        await Assert.That(wire).HasSingleItem();
        await Assert.That(wire[0].FirstOffset).IsEqualTo(100);
        await Assert.That(wire[0].LastOffset).IsEqualTo(100);
        tracker.ApplySuccessfulAcknowledgements(pending);
        await Assert.That(tracker.RetainedBatchCount).IsEqualTo(0);
    }

    [Test]
    public async Task Tracker_SparseAcknowledgementsDoNotAcknowledgeUndeliveredOffsets()
    {
        await using var consumer = CreateConsumer(ShareAcknowledgementMode.Explicit);
        using var tracker = new ShareBatchAcknowledgements<int, int>();
        using var batch = await consumer.ParseRecordBatchAsync(
            new TopicPartition("batch", 0), CreateSource(3), Acquired(100, 102), 3, default);
        tracker.Register(batch);
        foreach (var record in batch)
        {
            if (record.Offset != 101)
                batch.Acknowledge(record);
        }
        var wire = tracker.Flush();
        var submitted = wire[new TopicPartition("batch", 0)];
        await Assert.That(submitted).HasSingleItem();
        await Assert.That(submitted[0].AcknowledgeTypes).IsEquivalentTo(new byte[] { 1, 0, 1 });
        tracker.ApplySuccessfulAcknowledgements(wire);
        batch.Dispose();
        tracker.Prune();
        await Assert.That(tracker.RetainedBatchCount).IsEqualTo(0);
    }

    [Test]
    public async Task Tracker_NewAcquisitionSupersedesOldPendingDisposition()
    {
        await using var consumer = CreateConsumer(ShareAcknowledgementMode.Explicit);
        using var tracker = new ShareBatchAcknowledgements<int, int>();
        using var first = await consumer.ParseRecordBatchAsync(
            new TopicPartition("batch", 0), CreateSource(3), Acquired(100, 102), 3, default);
        tracker.Register(first);
        var firstRecords = first.GetEnumerator();
        _ = firstRecords.MoveNext();
        var old = firstRecords.Current;
        first.Acknowledge(old, AcknowledgeType.Reject);
        using var second = await consumer.ParseRecordBatchAsync(
            new TopicPartition("batch", 0), CreateSource(3), Acquired(100, 102), 3, default);
        tracker.Register(second);
        await Assert.That(() => first.Acknowledge(old)).Throws<InvalidOperationException>();
        var secondRecords = second.GetEnumerator();
        _ = secondRecords.MoveNext();
        second.Acknowledge(secondRecords.Current, AcknowledgeType.Release);
        var wire = tracker.Flush();
        await Assert.That(wire[new TopicPartition("batch", 0)][0].AcknowledgeTypes)
            .IsEquivalentTo(new byte[] { 2 });
        await Assert.That(tracker.RetainedBatchCount).IsEqualTo(1);
    }

    [Test]
    public async Task Tracker_AssignmentLossEndsAcquisitionAndClearAllowsResubscription()
    {
        await using var consumer = CreateConsumer(ShareAcknowledgementMode.Explicit);
        using var tracker = new ShareBatchAcknowledgements<int, int>();
        using var first = await consumer.ParseRecordBatchAsync(
            new TopicPartition("batch", 0), CreateSource(1), Acquired(100, 100), 1, default);
        tracker.Register(first);
        var records = first.GetEnumerator();
        _ = records.MoveNext();
        first.Acknowledge(records.Current, AcknowledgeType.Renew);
        tracker.ApplySuccessfulAcknowledgements(tracker.Flush());
        tracker.RemoveOutsideAssignment(new HashSet<TopicPartition>());
        await Assert.That(tracker.GetReplays(10)).IsNull();
        await Assert.That(tracker.RetainedBatchCount).IsEqualTo(0);
        await Assert.That(() => first.Acknowledge(records.Current)).Throws<InvalidOperationException>();
        tracker.Clear();
        using var second = await consumer.ParseRecordBatchAsync(
            new TopicPartition("batch", 0), CreateSource(1), Acquired(100, 100), 1, default);
        tracker.Register(second);
        var newRecords = second.GetEnumerator();
        _ = newRecords.MoveNext();
        second.Acknowledge(newRecords.Current);
        await Assert.That(tracker.Flush().Count).IsEqualTo(1);
    }

    [Test]
    public async Task Parser_RejectsDuplicateOffsetsBeforeTracking()
    {
        await using var consumer = CreateConsumer(ShareAcknowledgementMode.Explicit);
        await Assert.That(async () => await consumer.ParseRecordBatchAsync(
            new TopicPartition("batch", 0), CreateSource(3, [0, 0, 2]), Acquired(100, 102), 3, default))
            .Throws<MalformedProtocolDataException>();
    }

    [Test]
    [Arguments(ShareAcknowledgementMode.Implicit)]
    [Arguments(ShareAcknowledgementMode.Explicit)]
    public async Task PartialEnumeration_TracksOnlyDeliveredRecords(ShareAcknowledgementMode mode)
    {
        await using var consumer = CreateConsumer(mode);
        using var batch = await consumer.ParseRecordBatchAsync(
            new TopicPartition("batch", 0), CreateSource(3), Acquired(100, 102), 3, default);
        await Assert.That(batch.Count).IsEqualTo(3);
        var iterator = batch.GetEnumerator();
        await Assert.That(iterator.MoveNext()).IsTrue();
        var record = iterator.Current;
        await Assert.That(record.Offset).IsEqualTo(100);
        await Assert.That(record.Key).IsEqualTo(0);
        await Assert.That(record.Value).IsEqualTo(1);
        await Assert.That(record.DeliveryCount).IsEqualTo(4);
        var headers = record.Headers.GetEnumerator();
        await Assert.That(headers.MoveNext()).IsTrue();
        await Assert.That(headers.Current.KeyUtf8.Span.SequenceEqual("kind"u8)).IsTrue();
        await Assert.That(headers.Current.Value.Span.SequenceEqual("test"u8)).IsTrue();

        await Assert.That(batch.Storage.PendingCount).IsEqualTo(mode == ShareAcknowledgementMode.Implicit ? 1 : 0);
        batch.Acknowledge(record, AcknowledgeType.Release);
        await Assert.That(batch.Storage.PendingCount).IsEqualTo(1);
        await Assert.That(batch.Storage.Entries[0].PendingAcknowledgement).IsEqualTo((byte)AcknowledgeType.Release);
        await Assert.That(batch.Storage.Entries[1].Delivered).IsFalse();
        batch.Dispose();
        await Assert.That(() => record.Value).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task AcquiredRangesAndPollLimit_FilterBeforeDelivery()
    {
        await using var consumer = CreateConsumer(ShareAcknowledgementMode.Explicit);
        using var batch = await consumer.ParseRecordBatchAsync(
            new TopicPartition("batch", 0), CreateSource(5), Acquired(101, 104), 2, default);
        await Assert.That(batch.Count).IsEqualTo(2);
        var iterator = batch.GetEnumerator();
        await Assert.That(iterator.MoveNext()).IsTrue();
        await Assert.That(iterator.Current.Offset).IsEqualTo(101);
        await Assert.That(iterator.MoveNext()).IsTrue();
        await Assert.That(iterator.Current.Offset).IsEqualTo(102);
        await Assert.That(iterator.MoveNext()).IsFalse();
    }

    [Test]
    public async Task Acknowledge_RejectsForeignRecordsAndImplicitRenew()
    {
        await using var consumer = CreateConsumer(ShareAcknowledgementMode.Implicit);
        using var first = await consumer.ParseRecordBatchAsync(
            new TopicPartition("batch", 0), CreateSource(1), Acquired(100, 100), 1, default);
        using var second = await consumer.ParseRecordBatchAsync(
            new TopicPartition("batch", 0), CreateSource(1), Acquired(100, 100), 1, default);
        var iterator = first.GetEnumerator();
        _ = iterator.MoveNext();
        var record = iterator.Current;
        await Assert.That(() => second.Acknowledge(record)).Throws<ArgumentException>();
        await Assert.That(() => first.Acknowledge(record, AcknowledgeType.Renew)).Throws<InvalidOperationException>();
        await Assert.That(() => first.Acknowledge(record, AcknowledgeType.Gap)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ColdValuePreparation_DecodesEachKeyOnceAndPreservesBorrowedPayload()
    {
        var key = new CountingDeserializer();
        var value = new ColdDeserializer();
        await using var consumer = CreateConsumer(ShareAcknowledgementMode.Explicit, key, value);
        using var batch = await consumer.ParseRecordBatchAsync(
            new TopicPartition("batch", 0), CreateSource(3), Acquired(100, 102), 3, default);
        await Assert.That(key.Calls).IsEqualTo(3);
        await Assert.That(value.Preparations).IsEqualTo(3);
        var iterator = batch.GetEnumerator();
        var expected = 1;
        while (iterator.MoveNext())
            await Assert.That(iterator.Current.Value).IsEqualTo(expected++);
        await Assert.That(expected).IsEqualTo(4);
    }

    [Test]
    public async Task Tracker_CloseReleasesOnlyDeliveredImplicitRecords()
    {
        await using var consumer = CreateConsumer(ShareAcknowledgementMode.Implicit);
        using var tracker = new ShareBatchAcknowledgements<int, int>();
        using var batch = await consumer.ParseRecordBatchAsync(
            new TopicPartition("batch", 0), CreateSource(3), Acquired(100, 102), 3, default);
        tracker.Register(batch);
        var iterator = batch.GetEnumerator();
        _ = iterator.MoveNext();
        batch.Dispose();
        var wire = tracker.Flush(releaseImplicit: true);
        var acknowledgements = wire[new TopicPartition("batch", 0)];
        await Assert.That(acknowledgements.Count).IsEqualTo(1);
        await Assert.That(acknowledgements[0].FirstOffset).IsEqualTo(100);
        await Assert.That(acknowledgements[0].LastOffset).IsEqualTo(100);
        await Assert.That(acknowledgements[0].AcknowledgeTypes).IsEquivalentTo(new byte[] { 2 });
        tracker.ApplySuccessfulAcknowledgements(wire);
        await Assert.That(tracker.RetainedBatchCount).IsEqualTo(0);
    }

    [Test]
    public async Task Tracker_FirstAcknowledgementsForDistinctOffsets_AllocateZeroBytes()
    {
        await using var consumer = CreateConsumer(ShareAcknowledgementMode.Explicit);
        using var tracker = new ShareBatchAcknowledgements<int, int>();
        using var batch = await consumer.ParseRecordBatchAsync(
            new TopicPartition("batch", 0), CreateSource(256), Acquired(100, 355), 256, default);
        tracker.Register(batch);
        var iterator = batch.GetEnumerator();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var count = 0;
        while (iterator.MoveNext())
        {
            batch.Acknowledge(iterator.Current, count % 2 == 0 ? AcknowledgeType.Accept : AcknowledgeType.Release);
            count++;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        await Assert.That(count).IsEqualTo(256);
        await Assert.That(allocated).IsEqualTo(0);
        var wire = tracker.Flush();
        var outcomes = wire[new TopicPartition("batch", 0)][0].AcknowledgeTypes;
        await Assert.That(outcomes.Length).IsEqualTo(256);
        for (var index = 0; index < outcomes.Length; index++)
            await Assert.That(outcomes[index]).IsEqualTo((byte)(index % 2 == 0 ? 1 : 2));
    }

    [Test]
    public async Task Tracker_RetryPreservesNewerDisposition()
    {
        await using var consumer = CreateConsumer(ShareAcknowledgementMode.Explicit);
        using var tracker = new ShareBatchAcknowledgements<int, int>();
        using var batch = await consumer.ParseRecordBatchAsync(
            new TopicPartition("batch", 0), CreateSource(1), Acquired(100, 100), 1, default);
        tracker.Register(batch);
        var iterator = batch.GetEnumerator();
        _ = iterator.MoveNext();
        var record = iterator.Current;
        batch.Acknowledge(record, AcknowledgeType.Release);
        var first = tracker.Flush();
        batch.Acknowledge(record, AcknowledgeType.Reject);
        tracker.RequeueAcknowledgements(first);
        var second = tracker.Flush();
        await Assert.That(second[new TopicPartition("batch", 0)][0].AcknowledgeTypes)
            .IsEquivalentTo(new byte[] { 3 });
        tracker.ApplySuccessfulAcknowledgements(second);
        await Assert.That(tracker.HasPending).IsFalse();
        await Assert.That(tracker.RetainedBatchCount).IsEqualTo(0);
        await Assert.That(() => batch.Acknowledge(record)).Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments(false, false, AcknowledgeType.Accept)]
    [Arguments(false, true, AcknowledgeType.Accept)]
    [Arguments(true, false, AcknowledgeType.Accept)]
    [Arguments(true, true, AcknowledgeType.Accept)]
    [Arguments(false, false, AcknowledgeType.Renew)]
    [Arguments(false, true, AcknowledgeType.Renew)]
    [Arguments(true, false, AcknowledgeType.Renew)]
    [Arguments(true, true, AcknowledgeType.Renew)]
    public async Task Tracker_OlderCompletionCannotSettleNewerSubmission(
        bool redelivered, bool olderSucceeded, AcknowledgeType disposition)
    {
        await using var consumer = CreateConsumer(ShareAcknowledgementMode.Explicit);
        using var tracker = new ShareBatchAcknowledgements<int, int>();
        var partition = new TopicPartition("batch", 0);
        using var original = await consumer.ParseRecordBatchAsync(
            partition, CreateSource(1), Acquired(100, 100), 1, default);
        tracker.Register(original);
        var iterator = original.GetEnumerator();
        _ = iterator.MoveNext();
        var originalRecord = iterator.Current;
        original.Acknowledge(originalRecord, disposition);
        var older = tracker.Flush();

        using var replacement = redelivered ? await consumer.ParseRecordBatchAsync(
            partition, CreateSource(1), Acquired(100, 100), 1, default) : null;
        var current = replacement ?? original;
        var currentRecord = originalRecord;
        if (replacement is not null)
        {
            tracker.Register(replacement);
            iterator = replacement.GetEnumerator();
            await Assert.That(iterator.MoveNext()).IsTrue();
            currentRecord = iterator.Current;
        }
        current.Acknowledge(currentRecord, disposition);
        var newer = tracker.Flush();

        if (olderSucceeded) tracker.ApplySuccessfulAcknowledgements(older);
        else tracker.RequeueAcknowledgements(older);
        await Assert.That(tracker.HasPending).IsFalse();
        await Assert.That(tracker.RetainedBatchCount).IsEqualTo(1);
        await Assert.That(tracker.GetReplays(1)).IsNull();

        // Only the newer request's failure may queue its retry. Both wire requests
        // have the same offset and acknowledgement type, but distinct submissions.
        tracker.RequeueAcknowledgements(newer);
        await Assert.That(tracker.HasPending).IsTrue();
        var retry = tracker.Flush();
        await Assert.That(retry[partition][0].AcknowledgeTypes)
            .IsEquivalentTo(new byte[] { (byte)disposition });
        tracker.ApplySuccessfulAcknowledgements(retry);
        await Assert.That(tracker.HasPending).IsFalse();
        if (disposition == AcknowledgeType.Renew)
        {
            var replays = tracker.GetReplays(1)!;
            await Assert.That(replays.Count).IsEqualTo(1);
            using var replay = replays[0];
            var replayIterator = replay.GetEnumerator();
            await Assert.That(replayIterator.MoveNext()).IsTrue();
            replay.Acknowledge(replayIterator.Current, AcknowledgeType.Accept);
            tracker.ApplySuccessfulAcknowledgements(tracker.Flush());
        }
        await Assert.That(tracker.RetainedBatchCount).IsEqualTo(0);
    }

    [Test]
    public async Task Tracker_RenewalRetainsPayloadAfterOriginalLeaseCloses()
    {
        await using var consumer = CreateConsumer(ShareAcknowledgementMode.Explicit);
        using var tracker = new ShareBatchAcknowledgements<int, int>();
        using var batch = await consumer.ParseRecordBatchAsync(
            new TopicPartition("batch", 0), CreateSource(3), Acquired(100, 102), 3, default);
        tracker.Register(batch);
        var iterator = batch.GetEnumerator();
        _ = iterator.MoveNext();
        batch.Acknowledge(iterator.Current, AcknowledgeType.Renew);
        var renewal = tracker.Flush();
        batch.Dispose();
        tracker.ApplySuccessfulAcknowledgements(renewal);
        await Assert.That(tracker.RetainedBatchCount).IsEqualTo(1);
        var replays = tracker.GetReplays(3)!;
        await Assert.That(replays.Count).IsEqualTo(1);
        using var replay = replays[0];
        var replayIterator = replay.GetEnumerator();
        await Assert.That(replayIterator.MoveNext()).IsTrue();
        await Assert.That(replayIterator.Current.Offset).IsEqualTo(100);
        await Assert.That(replayIterator.Current.Value).IsEqualTo(1);
        await Assert.That(replayIterator.Current.DeliveryCount).IsEqualTo(4);
        replay.Acknowledge(replayIterator.Current);
        await Assert.That(replayIterator.MoveNext()).IsFalse();
        tracker.ApplySuccessfulAcknowledgements(tracker.Flush());
        await Assert.That(tracker.RetainedBatchCount).IsEqualTo(0);
    }

    [Test]
    public async Task Tracker_BoundedReplayPreservesUnreadRecordsAndEarlierRenewals()
    {
        await using var consumer = CreateConsumer(ShareAcknowledgementMode.Explicit);
        using var tracker = new ShareBatchAcknowledgements<int, int>();
        using var batch = await consumer.ParseRecordBatchAsync(
            new TopicPartition("batch", 0), CreateSource(7), Acquired(100, 106), 7, default);
        tracker.Register(batch);
        foreach (var record in batch)
        {
            batch.Acknowledge(record, AcknowledgeType.Renew);
        }
        tracker.ApplySuccessfulAcknowledgements(tracker.Flush());

        using (var unread = tracker.GetReplays(2)![0])
            await Assert.That(unread.Count).IsEqualTo(2);

        using (var first = tracker.GetReplays(2)![0])
        {
            var next = 100L;
            foreach (var record in first)
            {
                await Assert.That(record.Offset).IsEqualTo(next++);
                first.Acknowledge(record, AcknowledgeType.Renew);
            }
        }
        // Keep the first two renewals in flight while later chunks advance.
        var pendingRenewal = tracker.Flush();
        for (var offset = 102L; offset <= 106;)
        {
            using var replay = tracker.GetReplays(2)![0];
            foreach (var record in replay)
            {
                await Assert.That(record.Offset).IsEqualTo(offset++);
                replay.Acknowledge(record);
            }
            tracker.ApplySuccessfulAcknowledgements(tracker.Flush());
        }
        await Assert.That(tracker.GetReplays(2)).IsNull();
        tracker.ApplySuccessfulAcknowledgements(pendingRenewal);
        using (var renewed = tracker.GetReplays(2)![0])
        {
            var next = 100L;
            foreach (var record in renewed)
            {
                await Assert.That(record.Offset).IsEqualTo(next++);
                renewed.Acknowledge(record);
            }
            await Assert.That(next).IsEqualTo(102);
        }
        tracker.ApplySuccessfulAcknowledgements(tracker.Flush());
        await Assert.That(tracker.GetReplays(2)).IsNull();
        await Assert.That(tracker.RetainedBatchCount).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ColdKeyPreparation_ResumesWithSynchronousOrColdValue(bool coldValue)
    {
        var key = new ColdDeserializer();
        var cold = new ColdDeserializer();
        var synchronous = new CountingDeserializer();
        await using var consumer = CreateConsumer(ShareAcknowledgementMode.Explicit,
            key, coldValue ? cold : synchronous);
        using var batch = await consumer.ParseRecordBatchAsync(
            new TopicPartition("batch", 0), CreateSource(3), Acquired(100, 102), 3, default);
        await Assert.That(key.Preparations).IsEqualTo(3);
        await Assert.That(cold.Preparations).IsEqualTo(coldValue ? 3 : 0);
        await Assert.That(synchronous.Calls).IsEqualTo(coldValue ? 0 : 3);
        var iterator = batch.GetEnumerator();
        var expected = 0;
        while (iterator.MoveNext())
        {
            var record = iterator.Current;
            await Assert.That(record.Key).IsEqualTo(expected);
            await Assert.That(record.Value).IsEqualTo(expected + 1);
            await Assert.That(record.Offset).IsEqualTo(100L + expected);
            await Assert.That(record.Headers.Count).IsEqualTo(1);
            expected++;
        }
        await Assert.That(expected).IsEqualTo(3);
    }

    [Test]
    [Arguments(ShareAcknowledgementMode.Implicit, false)]
    [Arguments(ShareAcknowledgementMode.Implicit, true)]
    [Arguments(ShareAcknowledgementMode.Explicit, false)]
    [Arguments(ShareAcknowledgementMode.Explicit, true)]
    public async Task Tracker_PendingStateFollowsSubmissionRetryAndRegistration(
        ShareAcknowledgementMode mode, bool deliverBeforeRegistration)
    {
        await using var consumer = CreateConsumer(mode);
        using var tracker = new ShareBatchAcknowledgements<int, int>();
        using var batch = await consumer.ParseRecordBatchAsync(
            new TopicPartition("batch", 0), CreateSource(2), Acquired(100, 101), 2, default);
        if (!deliverBeforeRegistration)
            tracker.Register(batch);
        foreach (var record in batch)
        {
            batch.Acknowledge(record, AcknowledgeType.Release);
            batch.Acknowledge(record, AcknowledgeType.Reject);
        }
        if (deliverBeforeRegistration)
            tracker.Register(batch);
        await Assert.That(tracker.HasPending).IsTrue();
        var first = tracker.Flush();
        await Assert.That(tracker.HasPending).IsFalse();
        tracker.RequeueAcknowledgements(first);
        await Assert.That(tracker.HasPending).IsTrue();
        var retry = tracker.Flush();
        await Assert.That(tracker.HasPending).IsFalse();
        tracker.ApplySuccessfulAcknowledgements(retry);
        await Assert.That(tracker.HasPending).IsFalse();
        await Assert.That(tracker.RetainedBatchCount).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Tracker_RemovedStorageCannotRestorePendingState(bool clearAll)
    {
        await using var consumer = CreateConsumer(ShareAcknowledgementMode.Implicit);
        using var tracker = new ShareBatchAcknowledgements<int, int>();
        using var first = await consumer.ParseRecordBatchAsync(
            new TopicPartition("first", 0), CreateSource(2), Acquired(100, 101), 2, default);
        using var second = await consumer.ParseRecordBatchAsync(
            new TopicPartition("second", 0), CreateSource(1), Acquired(100, 100), 1, default);
        tracker.Register(first);
        tracker.Register(second);
        var iterator = first.GetEnumerator();
        _ = iterator.MoveNext();
        await Assert.That(tracker.HasPending).IsTrue();
        if (clearAll)
            tracker.Clear();
        else
            tracker.RemoveOutsideAssignment(new HashSet<TopicPartition> { second.TopicPartition });
        await Assert.That(tracker.HasPending).IsFalse();
        // The caller still holds the removed lease. Reading its unread tail must
        // not make the tracker's pending aggregate include detached storage.
        _ = iterator.MoveNext();
        await Assert.That(tracker.HasPending).IsFalse();
        if (!clearAll)
        {
            var remaining = second.GetEnumerator();
            _ = remaining.MoveNext();
            await Assert.That(tracker.HasPending).IsTrue();
            tracker.ApplySuccessfulAcknowledgements(tracker.Flush());
            await Assert.That(tracker.HasPending).IsFalse();
        }
    }

    [Test]
    public async Task Tracker_SupersededLeaseDuringDeliveryDoesNotLeavePendingState()
    {
        await using var consumer = CreateConsumer(ShareAcknowledgementMode.Implicit);
        using var tracker = new ShareBatchAcknowledgements<int, int>();
        using var original = await consumer.ParseRecordBatchAsync(
            new TopicPartition("batch", 0), CreateSource(1), Acquired(100, 100), 1, default);
        using var replacement = await consumer.ParseRecordBatchAsync(
            original.TopicPartition, CreateSource(1), Acquired(100, 100), 1, default);
        tracker.Register(original);
        tracker.Register(replacement);
        var stale = original.GetEnumerator();
        _ = stale.MoveNext();
        await Assert.That(tracker.HasPending).IsFalse();
        var current = replacement.GetEnumerator();
        _ = current.MoveNext();
        await Assert.That(tracker.HasPending).IsTrue();
        tracker.ApplySuccessfulAcknowledgements(tracker.Flush());
        await Assert.That(tracker.HasPending).IsFalse();
        await Assert.That(tracker.RetainedBatchCount).IsEqualTo(0);
    }

    [Test]
    public async Task Tracker_ReacquisitionReplacesCompletedHolesAndPreservesRenewals()
    {
        await using var consumer = CreateConsumer(ShareAcknowledgementMode.Explicit);
        using var tracker = new ShareBatchAcknowledgements<int, int>();
        using var original = await consumer.ParseRecordBatchAsync(
            new TopicPartition("batch", 0), CreateSource(8), Acquired(100, 107), 8, default);
        tracker.Register(original);
        foreach (var record in original)
            original.Acknowledge(record, AcknowledgeType.Renew);
        tracker.ApplySuccessfulAcknowledgements(tracker.Flush());
        original.Dispose();
        var firstReplay = tracker.GetReplays(8)!;
        foreach (var replay in firstReplay)
        {
            foreach (var record in replay)
                if (record.Offset % 2 == 0)
                    replay.Acknowledge(record);
            replay.Dispose();
        }
        tracker.ApplySuccessfulAcknowledgements(tracker.Flush());

        ShareFetchAcquiredRecords[] acquired =
        [
            new() { FirstOffset = 100, LastOffset = 100, DeliveryCount = 2 },
            new() { FirstOffset = 102, LastOffset = 102, DeliveryCount = 2 },
            new() { FirstOffset = 104, LastOffset = 104, DeliveryCount = 2 },
            new() { FirstOffset = 106, LastOffset = 106, DeliveryCount = 2 }
        ];
        using var replacement = await consumer.ParseRecordBatchAsync(
            original.TopicPartition, CreateSource(8), acquired, 8, default);
        tracker.Register(replacement);
        foreach (var record in replacement)
            replacement.Acknowledge(record, AcknowledgeType.Release);
        var wire = tracker.Flush();
        await Assert.That(wire[original.TopicPartition][0].AcknowledgeTypes)
            .IsEquivalentTo(new byte[] { 2, 0, 2, 0, 2, 0, 2 });
        tracker.ApplySuccessfulAcknowledgements(wire);
        replacement.Dispose();

        var offsets = new List<long>();
        foreach (var replay in tracker.GetReplays(8)!)
        {
            foreach (var record in replay)
            {
                offsets.Add(record.Offset);
                replay.Acknowledge(record);
            }
            replay.Dispose();
        }
        await Assert.That(offsets).IsEquivalentTo(new long[] { 101, 103, 105, 107 });
        tracker.ApplySuccessfulAcknowledgements(tracker.Flush());
        await Assert.That(tracker.RetainedBatchCount).IsEqualTo(0);
        await Assert.That(tracker.HasPending).IsFalse();
    }

    [Test]
    public async Task Tracker_LateAcknowledgementInOpenLeaseExtendsFlushBounds()
    {
        await using var consumer = CreateConsumer(ShareAcknowledgementMode.Explicit);
        using var tracker = new ShareBatchAcknowledgements<int, int>();
        using var batch = await consumer.ParseRecordBatchAsync(
            new TopicPartition("batch", 0), CreateSource(3), Acquired(100, 102), 3, default);
        tracker.Register(batch);
        var records = batch.GetEnumerator();
        _ = records.MoveNext();
        batch.Acknowledge(records.Current);
        tracker.ApplySuccessfulAcknowledgements(tracker.Flush());
        _ = records.MoveNext();
        _ = records.MoveNext();
        batch.Acknowledge(records.Current, AcknowledgeType.Release);
        var wire = tracker.Flush();
        await Assert.That(wire[batch.TopicPartition][0].FirstOffset).IsEqualTo(102);
        await Assert.That(wire[batch.TopicPartition][0].AcknowledgeTypes).IsEquivalentTo(new byte[] { 2 });
        tracker.ApplySuccessfulAcknowledgements(wire);
        batch.Dispose();
        await Assert.That(tracker.RetainedBatchCount).IsEqualTo(0);
    }

    [Test]
    public async Task Tracker_CompletedTailDoesNotGrowIndexBehindLongLivedRenewal()
    {
        await using var consumer = CreateConsumer(ShareAcknowledgementMode.Explicit);
        using var tracker = new ShareBatchAcknowledgements<int, int>();
        var partition = new TopicPartition("batch", 0);
        using var retained = await consumer.ParseRecordBatchAsync(
            partition, CreateSource(1), Acquired(100, 100), 1, default);
        tracker.Register(retained);
        foreach (var record in retained)
            retained.Acknowledge(record, AcknowledgeType.Renew);
        tracker.ApplySuccessfulAcknowledgements(tracker.Flush());
        retained.Dispose();
        for (var offset = 101; offset < 1125; offset++)
        {
            var source = CreateSource(1);
            source.BaseOffset = offset;
            using var batch = await consumer.ParseRecordBatchAsync(
                partition, source, Acquired(offset, offset), 1, default);
            tracker.Register(batch);
            foreach (var record in batch)
                batch.Acknowledge(record);
            batch.Dispose();
            tracker.ApplySuccessfulAcknowledgements(tracker.Flush());
        }
        await Assert.That(tracker.RetainedBatchCount).IsEqualTo(1);
        var index = retained.Storage.PartitionIndex!;
        var slots = (Array)index.GetType().GetField("_records",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(index)!;
        await Assert.That(slots.Length).IsLessThanOrEqualTo(32);
        foreach (var replay in tracker.GetReplays(1)!)
        {
            foreach (var record in replay)
                replay.Acknowledge(record);
            replay.Dispose();
        }
        tracker.ApplySuccessfulAcknowledgements(tracker.Flush());
        await Assert.That(tracker.RetainedBatchCount).IsEqualTo(0);
    }

    [Test]
    public async Task Tracker_OriginalLeaseCanCloseWhileRenewalReplayRemainsOpen()
    {
        await using var consumer = CreateConsumer(ShareAcknowledgementMode.Explicit);
        using var tracker = new ShareBatchAcknowledgements<int, int>();
        using var batch = await consumer.ParseRecordBatchAsync(
            new TopicPartition("batch", 0), CreateSource(3), Acquired(100, 102), 3, default);
        tracker.Register(batch);
        var records = batch.GetEnumerator();
        _ = records.MoveNext();
        batch.Acknowledge(records.Current, AcknowledgeType.Renew);
        tracker.ApplySuccessfulAcknowledgements(tracker.Flush());
        var replay = tracker.GetReplays(1)![0];
        batch.Dispose();
        await Assert.That(batch.Storage.TrackedCount).IsEqualTo(1);
        var renewed = replay.GetEnumerator();
        await Assert.That(renewed.MoveNext()).IsTrue();
        await Assert.That(renewed.Current.Offset).IsEqualTo(100);
        replay.Acknowledge(renewed.Current);
        replay.Dispose();
        tracker.ApplySuccessfulAcknowledgements(tracker.Flush());
        await Assert.That(tracker.RetainedBatchCount).IsEqualTo(0);
    }

    private static KafkaShareConsumer<int, int> CreateConsumer(
        ShareAcknowledgementMode mode, IDeserializer<int>? key = null, IDeserializer<int>? value = null) =>
        new(new ShareConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = "batch-parser",
            AcknowledgementMode = mode
        }, key ?? Serializers.Int32, value ?? Serializers.Int32);

    private static ShareFetchAcquiredRecords[] Acquired(long first, long last) =>
        [new ShareFetchAcquiredRecords { FirstOffset = first, LastOffset = last, DeliveryCount = 4 }];

    private static RecordBatch CreateSource(int count, int[]? offsets = null)
    {
        var records = new Record[count];
        for (var index = 0; index < count; index++)
        {
            var key = new ArrayBufferWriter<byte>();
            var value = new ArrayBufferWriter<byte>();
            Serializers.Int32.Serialize(index, ref key, default);
            Serializers.Int32.Serialize(index + 1, ref value, default);
            records[index] = new Record
            {
                OffsetDelta = offsets?[index] ?? index,
                TimestampDelta = index,
                Key = key.WrittenMemory,
                Value = value.WrittenMemory,
                Headers = [new Header("kind", "test"u8.ToArray())],
                HeaderCount = 1
            };
        }
        using var source = new RecordBatch { BaseOffset = 100, BaseTimestamp = 1000, Records = records };
        var output = new ArrayBufferWriter<byte>();
        source.Write(output);
        var reader = new KafkaProtocolReader(output.WrittenMemory);
        return RecordBatch.Read(ref reader);
    }

    private static RecordBatch WrapRawRecords(ReadOnlyMemory<byte> raw, int declaredCount)
    {
        using var source = new RecordBatch { BaseOffset = 100, Records = new Record[declaredCount] };
        source.SetPreEncodedRecords(raw);
        var buffer = new ArrayBufferWriter<byte>();
        source.Write(buffer);
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        return RecordBatch.Read(ref reader);
    }

    private sealed class CountingDeserializer : IDeserializer<int>
    {
        internal int Calls;
        public int Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            Calls++;
            return Serializers.Int32.Deserialize(data, context);
        }
    }

    private sealed class CancellingDeserializer(CancellationTokenSource cancellation)
        : IDeserializer<int>, IAsyncDeserializerPreparer<int>
    {
        private bool _prepared;
        public int Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) => 1;
        public bool TryDeserialize(ReadOnlyMemory<byte> data, SerializationContext context, out int value)
        {
            value = 1;
            return _prepared;
        }
        public ValueTask PrepareAsync(ReadOnlyMemory<byte> data, SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            cancellation.Cancel();
            _prepared = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ColdDeserializer : IDeserializer<int>, IAsyncDeserializerPreparer<int>
    {
        private int _prepared = -1;
        internal int Preparations;
        public int Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
            throw new InvalidOperationException("The preparation-aware path must be used.");

        public bool TryDeserialize(ReadOnlyMemory<byte> data, SerializationContext context, out int value)
        {
            value = Serializers.Int32.Deserialize(data, context);
            return value == _prepared;
        }

        public async ValueTask PrepareAsync(ReadOnlyMemory<byte> data, SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            _prepared = Serializers.Int32.Deserialize(data, context);
            Preparations++;
        }
    }
}
