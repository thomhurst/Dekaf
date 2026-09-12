using System.Collections.Concurrent;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public class BatchCheckpointTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PartialAndFullTraversal_CaptureOnlyConsumedOffsets(bool raw)
    {
        using var pending = CreatePending();
        var batch = new CheckpointBatch(pending, raw);
        await Assert.That(batch.Capture(out _)).IsFalse();
        await Assert.That(batch.MoveNext()).IsTrue();
        await Assert.That(batch.Capture(out var partial)).IsTrue();
        await Assert.That(partial).IsEqualTo(new TopicPartitionOffset("checkpoint", 2, 11, 3));
        await Assert.That(batch.MoveNext()).IsTrue();
        await Assert.That(batch.Capture(out var second)).IsTrue();
        await Assert.That(second.Offset).IsEqualTo(13);
        await Assert.That(batch.MoveNext()).IsFalse();
        await Assert.That(batch.Capture(out var full)).IsTrue();
        await Assert.That(full).IsEqualTo(new TopicPartitionOffset("checkpoint", 2, 16, 3));
        await Assert.That(partial.Offset).IsEqualTo(11);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PollLimit_DoesNotIncludeBufferedRecord(bool raw)
    {
        using var pending = CreatePending();
        var first = new CheckpointBatch(pending, raw, maxRecords: 1);
        await Assert.That(first.MoveNext()).IsTrue();
        await Assert.That(first.MoveNext()).IsFalse();
        await Assert.That(first.Capture(out var checkpoint)).IsTrue();
        await Assert.That(checkpoint.Offset).IsEqualTo(11);
        first.End();
        var second = new CheckpointBatch(pending, raw, maxRecords: 1);
        await Assert.That(second.Capture(out _)).IsFalse();
        await Assert.That(second.MoveNext()).IsTrue();
        await Assert.That(first.Capture(out _)).IsFalse();
        await Assert.That(second.MoveNext()).IsFalse();
        await Assert.That(second.Capture(out checkpoint)).IsTrue();
        await Assert.That(checkpoint.Offset).IsEqualTo(16);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task TrailingControlAndAbortedRecords_AdvanceCheckpoint(bool raw)
    {
        using var pending = PendingFetchData.Create("checkpoint", 2,
        [
            DataBatch(10, 3, [new Record { OffsetDelta = 0 }]),
            DataBatch(11, 4, [new Record { OffsetDelta = 0 }], RecordBatchAttributes.IsTransactional, 7),
            DataBatch(12, 5, [new Record { OffsetDelta = 0 }], RecordBatchAttributes.IsControlBatch)
        ], [new AbortedTransaction { ProducerId = 7, FirstOffset = 11 }]);
        pending.EagerParseAll();
        var batch = new CheckpointBatch(pending, raw);
        await Assert.That(batch.MoveNext()).IsTrue();
        await Assert.That(batch.MoveNext()).IsFalse();
        await Assert.That(batch.Capture(out var checkpoint)).IsTrue();
        await Assert.That(checkpoint).IsEqualTo(new TopicPartitionOffset("checkpoint", 2, 13, 5));
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ControlOnlyProgress_ExposesCheckpointAfterTraversal(bool raw)
    {
        using var pending = PendingFetchData.Create("checkpoint", 2,
            [DataBatch(20, 6, [new Record()], RecordBatchAttributes.IsControlBatch)]);
        pending.EagerParseAll();
        var batch = new CheckpointBatch(pending, raw);
        await Assert.That(batch.Capture(out _)).IsFalse();
        await Assert.That(batch.MoveNext()).IsFalse();
        await Assert.That(batch.Capture(out var checkpoint)).IsTrue();
        await Assert.That(checkpoint).IsEqualTo(new TopicPartitionOffset("checkpoint", 2, 21, 6));
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task EndAndDisposal_InvalidateAccessButNotCapturedValue(bool raw)
    {
        CheckpointBatch batch;
        TopicPartitionOffset checkpoint;
        // Dispose exactly once before checking the expired window: another test can
        // rent the returned instance while these assertions await.
        using (var pending = CreatePending())
        {
            batch = new CheckpointBatch(pending, raw);
            batch.MoveNext();
            await Assert.That(batch.Capture(out checkpoint)).IsTrue();
        }
        await Assert.That(batch.Capture(out _)).IsFalse();
        await Assert.That(checkpoint.Offset).IsEqualTo(11);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PausePreservesCheckpoint_RevocationInvalidatesAccess(bool raw)
    {
        using var pending = CreatePending();
        var epoch = new BatchIterationEpoch();
        var status = BatchIterationStatus.Continue;
        var batch = new CheckpointBatch(pending, raw,
            guard: new BatchIterationGuard(epoch, 0, _ => status));
        batch.MoveNext();
        status = BatchIterationStatus.Paused;
        epoch.Invalidate();
        var messageCount = pending.MessageCount;
        var lastYieldedOffset = pending.LastYieldedOffset;
        await Assert.That(batch.Capture(out var checkpoint)).IsTrue();
        await Assert.That(checkpoint.Offset).IsEqualTo(11);
        await Assert.That(epoch.BatchExhaustionProbePending).IsEqualTo(0);
        await Assert.That(pending.MessageCount).IsEqualTo(messageCount);
        await Assert.That(pending.LastYieldedOffset).IsEqualTo(lastYieldedOffset);
        status = BatchIterationStatus.Stopped;
        epoch.Invalidate();
        await Assert.That(batch.Capture(out _)).IsFalse();
    }

    [Test]
    public async Task FilteredWindow_OnlyCheckpointsExaminedRecords()
    {
        using var pending = CreatePending();
        var batch = new ConsumeBatch<byte[], byte[]>(pending, Serializers.ByteArray, Serializers.ByteArray,
            maxRecords: 1, recordFilter: new RejectAll());
        foreach (var _ in batch) { }
        await Assert.That(batch.Count).IsEqualTo(0);
        await Assert.That(batch.TryGetNextOffset(out var checkpoint)).IsTrue();
        await Assert.That(checkpoint.Offset).IsEqualTo(11);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task LaterWindow_InvalidatesEarlierWindowEvenWithoutExplicitEnd(bool raw)
    {
        using var pending = CreatePending();
        var first = new CheckpointBatch(pending, raw, maxRecords: 1);
        first.MoveNext();
        first.MoveNext();
        await Assert.That(first.Capture(out _)).IsTrue();
        var second = new CheckpointBatch(pending, raw);
        await Assert.That(first.Capture(out _)).IsFalse();
        await Assert.That(second.Capture(out _)).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CheckpointAccess_AllocatesNothing(bool raw)
    {
        using var pending = CreatePending();
        var batch = new CheckpointBatch(pending, raw);
        batch.MoveNext();
        batch.Capture(out _);
        var before = GC.GetAllocatedBytesForCurrentThread();
        long sum = 0;
        for (var i = 0; i < 100; i++)
        {
            if (batch.Capture(out var checkpoint))
                sum += checkpoint.Offset;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        await Assert.That(sum).IsEqualTo(1100);
        await Assert.That(allocated).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task EmptyAndEofNotifications_DoNotInventProgress(bool raw)
    {
        using var pending = PendingFetchData.Create("checkpoint", 2, []);
        var batch = new CheckpointBatch(pending, raw);
        batch.MoveNext();
        await Assert.That(batch.Capture(out _)).IsFalse();
        using var eof = PendingFetchData.CreatePartitionEof("checkpoint", 2, 100);
        var notification = new CheckpointBatch(eof, raw);
        notification.MoveNext();
        await Assert.That(notification.Capture(out _)).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task FetchBeforeRequestedOffset_DoesNotRegressCheckpoint(bool raw)
    {
        using var pending = PendingFetchData.Create("checkpoint", 2,
            [DataBatch(0, 1, [new Record()])], skipRecordsBelowOffset: 10);
        pending.EagerParseAll();
        var batch = new CheckpointBatch(pending, raw);
        await Assert.That(batch.MoveNext()).IsFalse();
        await Assert.That(batch.Capture(out _)).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task EarlierIteratorDisposal_DoesNotEndNewerCheckpointWindow(bool raw)
    {
        var pending = PendingFetchData.Create("checkpoint", 2,
            [DataBatch(10, 3, [new Record(), new Record { OffsetDelta = 1 },
                new Record { OffsetDelta = 2 }, new Record { OffsetDelta = 3 }])]);
        await using var consumer = CreateConsumer(pending);
        // Opening another logical window on the same storage reproduces the ownership
        // overlap of pooled reuse without replacing the process-wide pool in this test.
        if (raw)
        {
            await using var iterator = consumer.ConsumeRawBatchAsync().GetAsyncEnumerator();
            await Assert.That(await iterator.MoveNextAsync()).IsTrue();
            var records = iterator.Current.GetEnumerator();
            await Assert.That(records.MoveNext()).IsTrue();
            var newer = new CheckpointBatch(pending, raw, maxRecords: 1);
            await Assert.That(newer.MoveNext()).IsTrue();
            await iterator.DisposeAsync();
            await Assert.That(newer.Capture(out var checkpoint)).IsTrue();
            await Assert.That(checkpoint.Offset).IsEqualTo(12);
        }
        else
        {
            await using var iterator = consumer.ConsumeBatchAsync().GetAsyncEnumerator();
            await Assert.That(await iterator.MoveNextAsync()).IsTrue();
            var records = iterator.Current.GetEnumerator();
            await Assert.That(records.MoveNext()).IsTrue();
            var newer = new CheckpointBatch(pending, raw, maxRecords: 1);
            await Assert.That(newer.MoveNext()).IsTrue();
            await iterator.DisposeAsync();
            await Assert.That(newer.Capture(out var checkpoint)).IsTrue();
            await Assert.That(checkpoint.Offset).IsEqualTo(12);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ReusedCounterValue_DoesNotRestoreExpiredCheckpoint(bool raw)
    {
        using var pending = CreatePending();
        // Accelerate a complete counter cycle on counter-based implementations.
        // Object identity needs no counter manipulation to exercise the same stale handle.
        var counter = typeof(PendingFetchData).GetField("_checkpointVersion",
            BindingFlags.Instance | BindingFlags.NonPublic);
        counter?.SetValue(pending, 1);
        var earlier = new CheckpointBatch(pending, raw, maxRecords: 1);
        await Assert.That(earlier.MoveNext()).IsTrue();
        await Assert.That(earlier.Capture(out _)).IsTrue();

        counter?.SetValue(pending, 1);
        var current = new CheckpointBatch(pending, raw, maxRecords: 1);
        await Assert.That(current.MoveNext()).IsTrue();
        await Assert.That(current.Capture(out _)).IsTrue();
        await Assert.That(earlier.Capture(out var expired)).IsFalse();
        await Assert.That(expired).IsEqualTo(default(TopicPartitionOffset));
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task InvalidationDuringFinalStatusCheck_DiscardsCheckpoint(bool raw, bool reuseWindow)
    {
        using var pending = CreatePending();
        var checkingCheckpoint = false;
        var statusReads = 0;
        var guard = new BatchIterationGuard(null, 0, _ =>
        {
            if (checkingCheckpoint && ++statusReads == 2)
            {
                if (reuseWindow)
                    pending.BeginCheckpointWindow(new object());
                else
                    return BatchIterationStatus.Stopped;
            }
            return BatchIterationStatus.Continue;
        });
        var batch = new CheckpointBatch(pending, raw, guard: guard);
        await Assert.That(batch.MoveNext()).IsTrue();
        checkingCheckpoint = true;

        await Assert.That(batch.Capture(out var checkpoint)).IsFalse();
        await Assert.That(checkpoint).IsEqualTo(default(TopicPartitionOffset));
    }

    private static KafkaConsumer<byte[], byte[]> CreateConsumer(PendingFetchData pending)
    {
        var consumer = new KafkaConsumer<byte[], byte[]>(new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"], OffsetCommitMode = OffsetCommitMode.Manual,
            QueuedMinMessages = 1, MaxPollRecords = 1
        }, Serializers.ByteArray, Serializers.ByteArray);
        consumer.Assign(pending.TopicPartition);
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var type = consumer.GetType();
        type.GetField("_initialized", flags)!.SetValue(consumer, true);
        ((ConcurrentDictionary<TopicPartition, long>)type.GetField("_fetchPositions", flags)!
            .GetValue(consumer)!)[pending.TopicPartition] = 10;
        ((Queue<PendingFetchData>)type.GetField("_pendingFetches", flags)!.GetValue(consumer)!).Enqueue(pending);
        return consumer;
    }

    private sealed class RejectAll : IConsumerRecordFilter
    {
        public bool ShouldDeserialize(in ConsumerRecordFilterContext record) => false;
    }

    private static PendingFetchData CreatePending()
    {
        var records = new[] { new Record { OffsetDelta = 0 }, new Record { OffsetDelta = 2 } };
        var data = DataBatch(10, 3, records);
        data.LastOffsetDelta = 5;
        var pending = PendingFetchData.Create("checkpoint", 2, [data]);
        pending.EagerParseAll();
        return pending;
    }

    private static RecordBatch DataBatch(long offset, int epoch, Record[] records,
        RecordBatchAttributes attributes = default, long producerId = -1) => new()
    {
        BaseOffset = offset, PartitionLeaderEpoch = epoch, Records = records,
        LastOffsetDelta = records.Length - 1, Attributes = attributes, ProducerId = producerId
    };

    private sealed class CheckpointBatch
    {
        private readonly PendingFetchData _pending;
        private readonly ConsumeBatch<byte[], byte[]>? _typed;
        private readonly ConsumeRawBatch? _raw;
        private ConsumeBatch<byte[], byte[]>.Enumerator _typedEnumerator;
        private ConsumeRawBatch.Enumerator _rawEnumerator;

        public CheckpointBatch(PendingFetchData pending, bool raw, int maxRecords = int.MaxValue,
            BatchIterationGuard guard = default)
        {
            _pending = pending;
            if (raw)
            {
                _raw = new ConsumeRawBatch(pending, guard, maxRecords: maxRecords);
                _rawEnumerator = _raw.GetEnumerator();
            }
            else
            {
                _typed = new ConsumeBatch<byte[], byte[]>(pending, Serializers.ByteArray,
                    Serializers.ByteArray, guard, maxRecords: maxRecords);
                _typedEnumerator = _typed.GetEnumerator();
            }
        }

        public bool MoveNext() => _raw is not null ? _rawEnumerator.MoveNext() : _typedEnumerator.MoveNext();
        public bool Capture(out TopicPartitionOffset checkpoint) => _raw is not null
            ? _raw.TryGetNextOffset(out checkpoint) : _typed!.TryGetNextOffset(out checkpoint);
        public void End() => _pending.EndCheckpointWindow((object?)_raw ?? _typed);
    }
}
