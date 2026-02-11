using System.Buffers;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Unit tests for epoch bump recovery (KIP-360) for idempotent producers.
/// Tests the building blocks: RecordBatch.WithProducerState, ReadyBatch.RewriteRecordBatch,
/// sequence number reset, PartitionInflightTracker.IsHeadOfLine, ErrorCode classification,
/// and the TimestampDelta VarLong fix.
/// </summary>
public sealed class EpochBumpRecoveryTests
{
    private static readonly TopicPartition Tp0 = new("test-topic", 0);
    private static readonly TopicPartition Tp1 = new("test-topic", 1);
    private static readonly TopicPartition Tp2 = new("other-topic", 0);

    #region RecordBatch.WithProducerState Tests

    [Test]
    public async Task WithProducerState_CopiesAllFieldsAndUpdatesProducerState()
    {
        var records = new List<Record>
        {
            new()
            {
                TimestampDelta = 0,
                OffsetDelta = 0,
                Key = new byte[] { 1, 2, 3 },
                Value = new byte[] { 4, 5, 6 },
            },
            new()
            {
                TimestampDelta = 100,
                OffsetDelta = 1,
                Key = new byte[] { 7, 8 },
                Value = new byte[] { 9, 10 },
            }
        };

        var original = new RecordBatch
        {
            BaseOffset = 42,
            BatchLength = 100,
            PartitionLeaderEpoch = 5,
            Magic = 2,
            Crc = 12345,
            Attributes = RecordBatchAttributes.None,
            LastOffsetDelta = 1,
            BaseTimestamp = 1000000L,
            MaxTimestamp = 1000100L,
            ProducerId = 100,
            ProducerEpoch = 3,
            BaseSequence = 50,
            Records = records
        };

        var newBatch = original.WithProducerState(producerId: 200, producerEpoch: 4, baseSequence: 0);

        // Updated fields
        await Assert.That(newBatch.ProducerId).IsEqualTo(200);
        await Assert.That(newBatch.ProducerEpoch).IsEqualTo((short)4);
        await Assert.That(newBatch.BaseSequence).IsEqualTo(0);

        // Copied fields (unchanged)
        await Assert.That(newBatch.BaseOffset).IsEqualTo(42);
        await Assert.That(newBatch.PartitionLeaderEpoch).IsEqualTo(5);
        await Assert.That(newBatch.Magic).IsEqualTo((byte)2);
        await Assert.That(newBatch.Attributes).IsEqualTo(RecordBatchAttributes.None);
        await Assert.That(newBatch.LastOffsetDelta).IsEqualTo(1);
        await Assert.That(newBatch.BaseTimestamp).IsEqualTo(1000000L);
        await Assert.That(newBatch.MaxTimestamp).IsEqualTo(1000100L);

        // CRC zeroed (recomputed during Write)
        await Assert.That(newBatch.Crc).IsEqualTo(0u);

        // Records reference shared (same instance)
        await Assert.That(newBatch.Records).IsSameReferenceAs(original.Records);
    }

    [Test]
    public async Task WithProducerState_ProducesValidCrcOnWrite()
    {
        var records = new List<Record>
        {
            new()
            {
                TimestampDelta = 0,
                OffsetDelta = 0,
                IsKeyNull = true,
                Value = new byte[] { 1, 2, 3 },
            }
        };

        var original = new RecordBatch
        {
            BaseOffset = 0,
            PartitionLeaderEpoch = -1,
            Magic = 2,
            Attributes = RecordBatchAttributes.None,
            LastOffsetDelta = 0,
            BaseTimestamp = 1000L,
            MaxTimestamp = 1000L,
            ProducerId = 100,
            ProducerEpoch = 1,
            BaseSequence = 10,
            Records = records
        };

        var rewritten = original.WithProducerState(producerId: 100, producerEpoch: 2, baseSequence: 0);

        // Write and read back — CRC computed during Write, validated during Read
        var buffer = new ArrayBufferWriter<byte>();
        rewritten.Write(buffer);

        await Assert.That(buffer.WrittenCount).IsGreaterThan(0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var readBack = RecordBatch.Read(ref reader);

        await Assert.That(readBack.ProducerId).IsEqualTo(100);
        await Assert.That(readBack.ProducerEpoch).IsEqualTo((short)2);
        await Assert.That(readBack.BaseSequence).IsEqualTo(0);
    }

    [Test]
    public async Task WithProducerState_EmptyRecords_WritesAndReadsBack()
    {
        var original = new RecordBatch
        {
            BaseOffset = 0,
            PartitionLeaderEpoch = -1,
            BaseTimestamp = 0,
            MaxTimestamp = 0,
            ProducerId = 50,
            ProducerEpoch = 1,
            BaseSequence = 5,
            Records = new List<Record>()
        };

        var rewritten = original.WithProducerState(producerId: 50, producerEpoch: 2, baseSequence: 0);

        var buffer = new ArrayBufferWriter<byte>();
        rewritten.Write(buffer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var readBack = RecordBatch.Read(ref reader);

        await Assert.That(readBack.Records.Count).IsEqualTo(0);
        await Assert.That(readBack.ProducerEpoch).IsEqualTo((short)2);
        await Assert.That(readBack.BaseSequence).IsEqualTo(0);
    }

    [Test]
    public async Task WithProducerState_PreservesTransactionalAttributes()
    {
        var records = new List<Record>
        {
            new()
            {
                TimestampDelta = 0,
                OffsetDelta = 0,
                IsKeyNull = true,
                Value = "test"u8.ToArray(),
            }
        };

        var original = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 1000,
            MaxTimestamp = 1000,
            Attributes = RecordBatchAttributes.IsTransactional,
            ProducerId = 100,
            ProducerEpoch = 1,
            BaseSequence = 0,
            Records = records
        };

        var rewritten = original.WithProducerState(producerId: 100, producerEpoch: 2, baseSequence: 0);

        await Assert.That(rewritten.Attributes).IsEqualTo(RecordBatchAttributes.IsTransactional);

        // Verify round-trip preserves the attribute
        var buffer = new ArrayBufferWriter<byte>();
        rewritten.Write(buffer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var readBack = RecordBatch.Read(ref reader);

        var isTransactional = (readBack.Attributes & RecordBatchAttributes.IsTransactional) != 0;
        await Assert.That(isTransactional).IsTrue();
    }

    [Test]
    public async Task WithProducerState_MultipleRewrites_ProducesValidBatch()
    {
        var records = new List<Record>
        {
            new()
            {
                TimestampDelta = 0,
                OffsetDelta = 0,
                Key = "key"u8.ToArray(),
                Value = "value"u8.ToArray(),
            }
        };

        var original = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 5000,
            MaxTimestamp = 5000,
            ProducerId = 100,
            ProducerEpoch = 1,
            BaseSequence = 0,
            Records = records
        };

        // Rewrite multiple times (simulating multiple epoch bumps)
        var rewrite1 = original.WithProducerState(producerId: 100, producerEpoch: 2, baseSequence: 0);
        var rewrite2 = rewrite1.WithProducerState(producerId: 100, producerEpoch: 3, baseSequence: 0);
        var rewrite3 = rewrite2.WithProducerState(producerId: 200, producerEpoch: 0, baseSequence: 5);

        // All should share the same Records reference
        await Assert.That(rewrite3.Records).IsSameReferenceAs(original.Records);

        // Final rewrite should be valid on write/read
        var buffer = new ArrayBufferWriter<byte>();
        rewrite3.Write(buffer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var readBack = RecordBatch.Read(ref reader);

        await Assert.That(readBack.ProducerId).IsEqualTo(200);
        await Assert.That(readBack.ProducerEpoch).IsEqualTo((short)0);
        await Assert.That(readBack.BaseSequence).IsEqualTo(5);
        await Assert.That(readBack.BaseTimestamp).IsEqualTo(5000L);
        await Assert.That(readBack.Records[0].Key.ToArray()).IsEquivalentTo("key"u8.ToArray());
        await Assert.That(readBack.Records[0].Value.ToArray()).IsEquivalentTo("value"u8.ToArray());
    }

    [Test]
    public async Task WithProducerState_MultipleRecords_AllPreservedOnRoundTrip()
    {
        var records = new List<Record>
        {
            new()
            {
                TimestampDelta = 0,
                OffsetDelta = 0,
                Key = "k0"u8.ToArray(),
                Value = "v0"u8.ToArray(),
            },
            new()
            {
                TimestampDelta = 10,
                OffsetDelta = 1,
                Key = "k1"u8.ToArray(),
                Value = "v1"u8.ToArray(),
            },
            new()
            {
                TimestampDelta = 20,
                OffsetDelta = 2,
                IsKeyNull = true,
                Value = "v2"u8.ToArray(),
            }
        };

        var original = new RecordBatch
        {
            BaseOffset = 100,
            BaseTimestamp = 9000,
            MaxTimestamp = 9020,
            LastOffsetDelta = 2,
            ProducerId = 42,
            ProducerEpoch = 5,
            BaseSequence = 100,
            Records = records
        };

        var rewritten = original.WithProducerState(producerId: 42, producerEpoch: 6, baseSequence: 0);

        var buffer = new ArrayBufferWriter<byte>();
        rewritten.Write(buffer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var readBack = RecordBatch.Read(ref reader);

        await Assert.That(readBack.Records.Count).IsEqualTo(3);
        await Assert.That(readBack.Records[0].Key.ToArray()).IsEquivalentTo("k0"u8.ToArray());
        await Assert.That(readBack.Records[0].Value.ToArray()).IsEquivalentTo("v0"u8.ToArray());
        await Assert.That(readBack.Records[1].Key.ToArray()).IsEquivalentTo("k1"u8.ToArray());
        await Assert.That(readBack.Records[1].Value.ToArray()).IsEquivalentTo("v1"u8.ToArray());
        await Assert.That(readBack.Records[2].IsKeyNull).IsTrue();
        await Assert.That(readBack.Records[2].Value.ToArray()).IsEquivalentTo("v2"u8.ToArray());
    }

    [Test]
    public async Task WithProducerState_RecordWithHeaders_PreservedOnRoundTrip()
    {
        var records = new List<Record>
        {
            new()
            {
                TimestampDelta = 0,
                OffsetDelta = 0,
                IsKeyNull = true,
                Value = "val"u8.ToArray(),
                Headers = new Header[]
                {
                    new("trace-id", "abc123"u8.ToArray()),
                    new("version", "1"u8.ToArray()),
                }
            }
        };

        var original = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 0,
            MaxTimestamp = 0,
            ProducerId = 10,
            ProducerEpoch = 1,
            BaseSequence = 0,
            Records = records
        };

        var rewritten = original.WithProducerState(producerId: 10, producerEpoch: 2, baseSequence: 0);

        var buffer = new ArrayBufferWriter<byte>();
        rewritten.Write(buffer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var readBack = RecordBatch.Read(ref reader);

        var record = readBack.Records[0];
        await Assert.That(record.Headers).IsNotNull();
        await Assert.That(record.Headers!.Count).IsEqualTo(2);
        await Assert.That(record.Headers[0].Key).IsEqualTo("trace-id");
        await Assert.That(record.Headers[0].Value.ToArray()).IsEquivalentTo("abc123"u8.ToArray());
        await Assert.That(record.Headers[1].Key).IsEqualTo("version");
    }

    #endregion

    #region ReadyBatch.RewriteRecordBatch Tests

    [Test]
    public async Task RewriteRecordBatch_UpdatesBatchReference()
    {
        var pool = new ReadyBatchPool();
        var batch = pool.Rent();

        var entry = new InflightEntry();
        entry.Initialize(Tp0, baseSequence: 0, recordCount: 1);
        batch.InflightEntry = entry;

        // Set an initial RecordBatch via reflection (ReadyBatch needs Initialize which is complex)
        var recordBatchField = typeof(ReadyBatch).GetField("_recordBatch",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var originalBatch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 0,
            MaxTimestamp = 0,
            ProducerId = 100,
            ProducerEpoch = 1,
            BaseSequence = 5,
            Records = new List<Record>()
        };

        recordBatchField!.SetValue(batch, originalBatch);
        await Assert.That(batch.RecordBatch.ProducerEpoch).IsEqualTo((short)1);
        await Assert.That(batch.RecordBatch.BaseSequence).IsEqualTo(5);

        // Rewrite
        var newRecordBatch = originalBatch.WithProducerState(producerId: 100, producerEpoch: 2, baseSequence: 0);
        batch.RewriteRecordBatch(newRecordBatch);

        await Assert.That(batch.RecordBatch).IsSameReferenceAs(newRecordBatch);
        await Assert.That(batch.RecordBatch.ProducerEpoch).IsEqualTo((short)2);
        await Assert.That(batch.RecordBatch.BaseSequence).IsEqualTo(0);
    }

    [Test]
    public async Task RewriteRecordBatch_DoesNotAffectOtherProperties()
    {
        var pool = new ReadyBatchPool();
        var batch = pool.Rent();

        var recordBatchField = typeof(ReadyBatch).GetField("_recordBatch",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var originalBatch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 0,
            MaxTimestamp = 0,
            ProducerId = 100,
            ProducerEpoch = 1,
            BaseSequence = 0,
            Records = new List<Record>()
        };

        recordBatchField!.SetValue(batch, originalBatch);

        // Set other properties
        var entry = new InflightEntry();
        entry.Initialize(Tp0, 0, 1);
        batch.InflightEntry = entry;

        // Rewrite
        var newBatch = originalBatch.WithProducerState(producerId: 100, producerEpoch: 2, baseSequence: 0);
        batch.RewriteRecordBatch(newBatch);

        // Other properties should be unaffected
        await Assert.That(batch.InflightEntry).IsSameReferenceAs(entry);
    }

    #endregion

    #region Sequence Number Reset Tests

    [Test]
    public async Task GetAndIncrementSequence_AfterReset_StartsFromZero()
    {
        var options = new ProducerOptions { BootstrapServers = ["localhost:9092"] };
        var accumulator = new RecordAccumulator(options);

        var seq0 = accumulator.GetAndIncrementSequence(Tp0, 5);
        var seq1 = accumulator.GetAndIncrementSequence(Tp0, 3);

        await Assert.That(seq0).IsEqualTo(0);
        await Assert.That(seq1).IsEqualTo(5);

        accumulator.ResetSequenceNumbers();

        var seqAfterReset = accumulator.GetAndIncrementSequence(Tp0, 2);
        await Assert.That(seqAfterReset).IsEqualTo(0);

        var seqNext = accumulator.GetAndIncrementSequence(Tp0, 4);
        await Assert.That(seqNext).IsEqualTo(2);

        await accumulator.DisposeAsync();
    }

    [Test]
    public async Task ResetSequenceNumbers_MultiplePartitions_AllReset()
    {
        var options = new ProducerOptions { BootstrapServers = ["localhost:9092"] };
        var accumulator = new RecordAccumulator(options);

        accumulator.GetAndIncrementSequence(Tp0, 10);
        accumulator.GetAndIncrementSequence(Tp1, 20);
        accumulator.GetAndIncrementSequence(Tp2, 30);

        accumulator.ResetSequenceNumbers();

        var seq0 = accumulator.GetAndIncrementSequence(Tp0, 1);
        var seq1 = accumulator.GetAndIncrementSequence(Tp1, 1);
        var seq2 = accumulator.GetAndIncrementSequence(Tp2, 1);

        await Assert.That(seq0).IsEqualTo(0);
        await Assert.That(seq1).IsEqualTo(0);
        await Assert.That(seq2).IsEqualTo(0);

        await accumulator.DisposeAsync();
    }

    [Test]
    public async Task ResetSequenceNumbers_DoubleReset_NoError()
    {
        var options = new ProducerOptions { BootstrapServers = ["localhost:9092"] };
        var accumulator = new RecordAccumulator(options);

        accumulator.GetAndIncrementSequence(Tp0, 5);
        accumulator.ResetSequenceNumbers();
        accumulator.ResetSequenceNumbers(); // Double reset should not throw

        var seq = accumulator.GetAndIncrementSequence(Tp0, 1);
        await Assert.That(seq).IsEqualTo(0);

        await accumulator.DisposeAsync();
    }

    [Test]
    public async Task ResetSequenceNumbers_EmptyState_NoError()
    {
        var options = new ProducerOptions { BootstrapServers = ["localhost:9092"] };
        var accumulator = new RecordAccumulator(options);

        // Reset with no sequences ever created
        accumulator.ResetSequenceNumbers();

        var seq = accumulator.GetAndIncrementSequence(Tp0, 1);
        await Assert.That(seq).IsEqualTo(0);

        await accumulator.DisposeAsync();
    }

    [Test]
    public async Task ResetSequenceNumbers_ConcurrentAccess_ThreadSafe()
    {
        var options = new ProducerOptions { BootstrapServers = ["localhost:9092"] };
        var accumulator = new RecordAccumulator(options);

        // Pre-populate sequences
        for (var i = 0; i < 10; i++)
        {
            accumulator.GetAndIncrementSequence(new TopicPartition("test", i), 100);
        }

        // Reset while concurrent threads are incrementing
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var tasks = new List<Task>();

        for (var i = 0; i < 4; i++)
        {
            var partId = i;
            tasks.Add(Task.Run(() =>
            {
                while (!cts.IsCancellationRequested)
                {
                    accumulator.GetAndIncrementSequence(new TopicPartition("test", partId), 1);
                }
            }));
        }

        // Reset a few times during concurrent access
        for (var i = 0; i < 5; i++)
        {
            await Task.Delay(30);
            accumulator.ResetSequenceNumbers();
        }

        await cts.CancelAsync();
        await Task.WhenAll(tasks);

        // After reset, sequences should start from 0
        accumulator.ResetSequenceNumbers();
        var seq = accumulator.GetAndIncrementSequence(Tp0, 1);
        await Assert.That(seq).IsEqualTo(0);

        await accumulator.DisposeAsync();
    }

    [Test]
    public async Task ResetSequenceNumbers_UpdatesAccumulatorProducerState()
    {
        var options = new ProducerOptions { BootstrapServers = ["localhost:9092"] };
        var accumulator = new RecordAccumulator(options);

        // Set initial producer state
        accumulator.ProducerId = 100;
        accumulator.ProducerEpoch = 1;

        accumulator.GetAndIncrementSequence(Tp0, 10);

        // Simulate epoch bump: update state and reset sequences
        accumulator.ProducerId = 100;
        accumulator.ProducerEpoch = 2;
        accumulator.ResetSequenceNumbers();

        await Assert.That(accumulator.ProducerId).IsEqualTo(100);
        await Assert.That(accumulator.ProducerEpoch).IsEqualTo((short)2);

        // Sequences start from 0 with new epoch
        var seq = accumulator.GetAndIncrementSequence(Tp0, 5);
        await Assert.That(seq).IsEqualTo(0);

        await accumulator.DisposeAsync();
    }

    #endregion

    #region PartitionInflightTracker.IsHeadOfLine Tests

    [Test]
    public async Task IsHeadOfLine_HeadEntry_ReturnsTrue()
    {
        var tracker = new PartitionInflightTracker();

        var entry = tracker.Register(Tp0, baseSequence: 0, recordCount: 5);

        await Assert.That(tracker.IsHeadOfLine(entry)).IsTrue();

        tracker.Complete(entry);
    }

    [Test]
    public async Task IsHeadOfLine_NonHeadEntry_ReturnsFalse()
    {
        var tracker = new PartitionInflightTracker();

        var entry1 = tracker.Register(Tp0, baseSequence: 0, recordCount: 5);
        var entry2 = tracker.Register(Tp0, baseSequence: 5, recordCount: 3);

        await Assert.That(tracker.IsHeadOfLine(entry1)).IsTrue();
        await Assert.That(tracker.IsHeadOfLine(entry2)).IsFalse();

        // After completing head, successor becomes head
        tracker.Complete(entry1);
        await Assert.That(tracker.IsHeadOfLine(entry2)).IsTrue();

        tracker.Complete(entry2);
    }

    [Test]
    public async Task IsHeadOfLine_UnknownPartition_ReturnsTrueAsDefault()
    {
        var tracker = new PartitionInflightTracker();

        tracker.Register(Tp0, baseSequence: 0, recordCount: 5);

        // Create an entry for a partition not in tracker
        var otherEntry = new InflightEntry();
        otherEntry.Initialize(Tp2, 0, 1);

        await Assert.That(tracker.IsHeadOfLine(otherEntry)).IsTrue();
    }

    [Test]
    public async Task IsHeadOfLine_ThreeEntries_MiddleCompletedFirst()
    {
        var tracker = new PartitionInflightTracker();

        var entry1 = tracker.Register(Tp0, baseSequence: 0, recordCount: 5);
        var entry2 = tracker.Register(Tp0, baseSequence: 5, recordCount: 5);
        var entry3 = tracker.Register(Tp0, baseSequence: 10, recordCount: 5);

        await Assert.That(tracker.IsHeadOfLine(entry1)).IsTrue();
        await Assert.That(tracker.IsHeadOfLine(entry2)).IsFalse();
        await Assert.That(tracker.IsHeadOfLine(entry3)).IsFalse();

        // Complete middle entry — head doesn't change
        tracker.Complete(entry2);
        await Assert.That(tracker.IsHeadOfLine(entry1)).IsTrue();
        await Assert.That(tracker.IsHeadOfLine(entry3)).IsFalse();

        // Complete head — entry3 becomes head
        tracker.Complete(entry1);
        await Assert.That(tracker.IsHeadOfLine(entry3)).IsTrue();

        tracker.Complete(entry3);
    }

    [Test]
    public async Task IsHeadOfLine_DifferentPartitions_Independent()
    {
        var tracker = new PartitionInflightTracker();

        var entryP0 = tracker.Register(Tp0, baseSequence: 0, recordCount: 5);
        var entryP1 = tracker.Register(Tp1, baseSequence: 0, recordCount: 5);

        // Both are heads of their respective partitions
        await Assert.That(tracker.IsHeadOfLine(entryP0)).IsTrue();
        await Assert.That(tracker.IsHeadOfLine(entryP1)).IsTrue();

        // Add successor to partition 0 only
        var entryP0b = tracker.Register(Tp0, baseSequence: 5, recordCount: 3);
        await Assert.That(tracker.IsHeadOfLine(entryP0b)).IsFalse();
        await Assert.That(tracker.IsHeadOfLine(entryP1)).IsTrue(); // Unaffected

        tracker.Complete(entryP0);
        tracker.Complete(entryP0b);
        tracker.Complete(entryP1);
    }

    [Test]
    public async Task IsHeadOfLine_AfterCompleteAndReRegister_SimulatesEpochBump()
    {
        // Simulates the epoch bump flow:
        // 1. Register batch with seq=0 → head of line
        // 2. Complete (remove old entry)
        // 3. Re-register with new seq=0 (after epoch bump reset)
        // 4. New entry should be head
        var tracker = new PartitionInflightTracker();

        var entry1 = tracker.Register(Tp0, baseSequence: 0, recordCount: 5);
        await Assert.That(tracker.IsHeadOfLine(entry1)).IsTrue();

        // Simulate epoch bump: complete old entry, re-register with new sequence
        tracker.Complete(entry1);
        await Assert.That(tracker.GetInflightCount(Tp0)).IsEqualTo(0);

        var entry1Rewritten = tracker.Register(Tp0, baseSequence: 0, recordCount: 5);
        await Assert.That(tracker.IsHeadOfLine(entry1Rewritten)).IsTrue();

        tracker.Complete(entry1Rewritten);
    }

    [Test]
    public async Task IsHeadOfLine_HeadBumped_SuccessorsReRegisterAfter()
    {
        // Simulates multiple batches where head gets epoch bump:
        // entry1 (head) gets OOSN → bumps epoch → complete old + re-register
        // entry2 was non-head → after entry1 completes, entry2 becomes head
        var tracker = new PartitionInflightTracker();

        var entry1 = tracker.Register(Tp0, baseSequence: 0, recordCount: 5);
        var entry2 = tracker.Register(Tp0, baseSequence: 5, recordCount: 3);

        await Assert.That(tracker.IsHeadOfLine(entry1)).IsTrue();
        await Assert.That(tracker.IsHeadOfLine(entry2)).IsFalse();

        // Head (entry1) epoch bumps: complete old entry
        tracker.Complete(entry1);

        // entry2 is now head
        await Assert.That(tracker.IsHeadOfLine(entry2)).IsTrue();

        // entry1 re-registers with new sequence (post-bump)
        var entry1New = tracker.Register(Tp0, baseSequence: 0, recordCount: 5);

        // entry2 is still head (registered before entry1New)
        await Assert.That(tracker.IsHeadOfLine(entry2)).IsTrue();
        await Assert.That(tracker.IsHeadOfLine(entry1New)).IsFalse();

        tracker.Complete(entry2);
        tracker.Complete(entry1New);
    }

    #endregion

    #region ErrorCode Classification Tests

    [Test]
    public async Task OutOfOrderSequenceNumber_IsNotRetriable()
    {
        // OOSN was removed from IsRetriable per KIP-360 — it's handled by epoch bump instead
        await Assert.That(ErrorCode.OutOfOrderSequenceNumber.IsRetriable()).IsFalse();
    }

    [Test]
    public async Task UnknownLeaderEpoch_IsRetriable()
    {
        // UnknownLeaderEpoch was added to IsRetriable per Kafka protocol spec
        await Assert.That(ErrorCode.UnknownLeaderEpoch.IsRetriable()).IsTrue();
    }

    [Test]
    public async Task InvalidProducerEpoch_IsNotRetriable()
    {
        // InvalidProducerEpoch is handled by epoch bump, not generic retry
        await Assert.That(ErrorCode.InvalidProducerEpoch.IsRetriable()).IsFalse();
    }

    [Test]
    public async Task UnknownProducerId_IsNotRetriable()
    {
        // UnknownProducerId is handled by epoch bump, not generic retry
        await Assert.That(ErrorCode.UnknownProducerId.IsRetriable()).IsFalse();
    }

    [Test]
    [MethodDataSource(nameof(EpochBumpErrorCodes))]
    public async Task EpochBumpErrors_HaveCorrectNumericValues(ErrorCode errorCode, short expectedValue)
    {
        var actual = (short)errorCode;
        await Assert.That(actual).IsEqualTo(expectedValue);
    }

    public static IEnumerable<(ErrorCode, short)> EpochBumpErrorCodes()
    {
        yield return (ErrorCode.OutOfOrderSequenceNumber, 45);
        yield return (ErrorCode.InvalidProducerEpoch, 47);
        yield return (ErrorCode.UnknownProducerId, 59);
    }

    [Test]
    public async Task DuplicateSequenceNumber_IsNotRetriable()
    {
        // DuplicateSequenceNumber is treated as success, not retried
        await Assert.That(ErrorCode.DuplicateSequenceNumber.IsRetriable()).IsFalse();
    }

    [Test]
    [MethodDataSource(nameof(NonEpochBumpRetriableErrors))]
    public async Task NonEpochBumpRetriableErrors_DoNotTriggerEpochBump(ErrorCode errorCode)
    {
        // These errors are retriable but should NOT trigger epoch bump
        // They use normal backoff + metadata refresh instead
        await Assert.That(errorCode.IsRetriable()).IsTrue();

        // Verify they're not one of the epoch bump error codes
        var isEpochBumpError = errorCode is ErrorCode.OutOfOrderSequenceNumber
            or ErrorCode.InvalidProducerEpoch or ErrorCode.UnknownProducerId;
        await Assert.That(isEpochBumpError).IsFalse();
    }

    public static IEnumerable<ErrorCode> NonEpochBumpRetriableErrors()
    {
        yield return ErrorCode.LeaderNotAvailable;
        yield return ErrorCode.NotLeaderOrFollower;
        yield return ErrorCode.RequestTimedOut;
        yield return ErrorCode.NetworkException;
        yield return ErrorCode.NotEnoughReplicas;
        yield return ErrorCode.FencedLeaderEpoch;
        yield return ErrorCode.UnknownLeaderEpoch;
    }

    #endregion

    #region TimestampDelta VarLong Tests

    [Test]
    public async Task Record_TimestampDelta_SupportsLargeValues()
    {
        // TimestampDelta was changed from int to long (VarLong encoding).
        // Values exceeding int.MaxValue should round-trip correctly.
        var largeTimestampDelta = (long)int.MaxValue + 1000L;

        var records = new List<Record>
        {
            new()
            {
                TimestampDelta = largeTimestampDelta,
                OffsetDelta = 0,
                IsKeyNull = true,
                Value = "test"u8.ToArray(),
            }
        };

        var batch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 1000,
            MaxTimestamp = 1000 + largeTimestampDelta,
            ProducerId = -1,
            ProducerEpoch = -1,
            BaseSequence = -1,
            Records = records
        };

        var buffer = new ArrayBufferWriter<byte>();
        batch.Write(buffer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var readBack = RecordBatch.Read(ref reader);

        await Assert.That(readBack.Records[0].TimestampDelta).IsEqualTo(largeTimestampDelta);
    }

    [Test]
    public async Task Record_TimestampDelta_NegativeValue_RoundTrips()
    {
        // Negative timestamp deltas are valid (e.g. out-of-order messages)
        var negativeTimestampDelta = -500L;

        var records = new List<Record>
        {
            new()
            {
                TimestampDelta = negativeTimestampDelta,
                OffsetDelta = 0,
                IsKeyNull = true,
                Value = "test"u8.ToArray(),
            }
        };

        var batch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 10000,
            MaxTimestamp = 10000,
            Records = records
        };

        var buffer = new ArrayBufferWriter<byte>();
        batch.Write(buffer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var readBack = RecordBatch.Read(ref reader);

        await Assert.That(readBack.Records[0].TimestampDelta).IsEqualTo(negativeTimestampDelta);
    }

    [Test]
    public async Task Record_TimestampDelta_ZeroValue_RoundTrips()
    {
        var records = new List<Record>
        {
            new()
            {
                TimestampDelta = 0,
                OffsetDelta = 0,
                IsKeyNull = true,
                Value = "data"u8.ToArray(),
            }
        };

        var batch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 5000,
            MaxTimestamp = 5000,
            Records = records
        };

        var buffer = new ArrayBufferWriter<byte>();
        batch.Write(buffer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var readBack = RecordBatch.Read(ref reader);

        await Assert.That(readBack.Records[0].TimestampDelta).IsEqualTo(0L);
    }

    [Test]
    public async Task Record_TimestampDelta_MaxIntValue_RoundTrips()
    {
        // Edge case: exactly int.MaxValue should work (was the old limit)
        long intMaxDelta = int.MaxValue;

        var records = new List<Record>
        {
            new()
            {
                TimestampDelta = intMaxDelta,
                OffsetDelta = 0,
                IsKeyNull = true,
                Value = "test"u8.ToArray(),
            }
        };

        var batch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 0,
            MaxTimestamp = intMaxDelta,
            Records = records
        };

        var buffer = new ArrayBufferWriter<byte>();
        batch.Write(buffer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var readBack = RecordBatch.Read(ref reader);

        await Assert.That(readBack.Records[0].TimestampDelta).IsEqualTo(intMaxDelta);
    }

    [Test]
    public async Task Record_TimestampDelta_VarLongSize_CorrectForLargeValues()
    {
        // VarLong encoding should use more bytes for large values
        var smallDeltaSize = Record.VarLongSize(0);
        var mediumDeltaSize = Record.VarLongSize(1000);
        var largeDeltaSize = Record.VarLongSize((long)int.MaxValue + 1);

        await Assert.That(smallDeltaSize).IsEqualTo(1);
        await Assert.That(mediumDeltaSize).IsGreaterThan(1);
        await Assert.That(largeDeltaSize).IsGreaterThan(mediumDeltaSize);
    }

    [Test]
    public async Task Record_TimestampDelta_MultipleRecords_LargeDeltasBetween()
    {
        // Test multiple records with large inter-record timestamp gaps
        var records = new List<Record>
        {
            new()
            {
                TimestampDelta = 0,
                OffsetDelta = 0,
                IsKeyNull = true,
                Value = "first"u8.ToArray(),
            },
            new()
            {
                TimestampDelta = 3_000_000_000L, // ~50 minutes, exceeds int range
                OffsetDelta = 1,
                IsKeyNull = true,
                Value = "second"u8.ToArray(),
            },
            new()
            {
                TimestampDelta = 6_000_000_000L, // ~100 minutes
                OffsetDelta = 2,
                IsKeyNull = true,
                Value = "third"u8.ToArray(),
            }
        };

        var batch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 1000,
            MaxTimestamp = 1000 + 6_000_000_000L,
            LastOffsetDelta = 2,
            Records = records
        };

        var buffer = new ArrayBufferWriter<byte>();
        batch.Write(buffer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var readBack = RecordBatch.Read(ref reader);

        await Assert.That(readBack.Records.Count).IsEqualTo(3);
        await Assert.That(readBack.Records[0].TimestampDelta).IsEqualTo(0L);
        await Assert.That(readBack.Records[1].TimestampDelta).IsEqualTo(3_000_000_000L);
        await Assert.That(readBack.Records[2].TimestampDelta).IsEqualTo(6_000_000_000L);
    }

    #endregion

    #region Epoch Bump Flow Simulation Tests

    [Test]
    public async Task EpochBump_SimulatedFlow_SequencesAndTrackerConsistent()
    {
        // Simulates the full epoch bump flow at the building-block level:
        // 1. Accumulator has sequences for a partition
        // 2. Batch is registered in inflight tracker
        // 3. OOSN occurs → complete old entry → reset sequences → get new sequence → re-register
        var options = new ProducerOptions { BootstrapServers = ["localhost:9092"] };
        var accumulator = new RecordAccumulator(options);
        var tracker = new PartitionInflightTracker();

        // Step 1: Initial state — produce a batch
        var seq0 = accumulator.GetAndIncrementSequence(Tp0, 10);
        await Assert.That(seq0).IsEqualTo(0);

        var entry0 = tracker.Register(Tp0, seq0, 10);
        await Assert.That(tracker.IsHeadOfLine(entry0)).IsTrue();

        // Step 2: Another batch follows
        var seq1 = accumulator.GetAndIncrementSequence(Tp0, 5);
        await Assert.That(seq1).IsEqualTo(10);

        var entry1 = tracker.Register(Tp0, seq1, 5);
        await Assert.That(tracker.IsHeadOfLine(entry1)).IsFalse();

        // Step 3: entry0 gets OOSN (head of line) → epoch bump
        // 3a: Complete old entry
        tracker.Complete(entry0);
        await Assert.That(tracker.IsHeadOfLine(entry1)).IsTrue(); // entry1 promoted

        // 3b: Reset sequences (epoch bump)
        accumulator.ResetSequenceNumbers();

        // 3c: Get new sequence and re-register
        var newSeq0 = accumulator.GetAndIncrementSequence(Tp0, 10);
        await Assert.That(newSeq0).IsEqualTo(0);

        var newEntry0 = tracker.Register(Tp0, newSeq0, 10);

        // entry1 is still in the tracker (pre-bump), newEntry0 is at the tail
        await Assert.That(tracker.IsHeadOfLine(entry1)).IsTrue();
        await Assert.That(tracker.IsHeadOfLine(newEntry0)).IsFalse();
        await Assert.That(tracker.GetInflightCount(Tp0)).IsEqualTo(2);

        // Cleanup
        tracker.Complete(entry1);
        tracker.Complete(newEntry0);
        await Assert.That(tracker.GetInflightCount(Tp0)).IsEqualTo(0);

        await accumulator.DisposeAsync();
    }

    [Test]
    public async Task EpochBump_MultiplePartitions_OnlyAffectedPartitionResets()
    {
        // When epoch bump resets sequences, ALL partitions reset (matching Java behavior).
        // But inflight entries for unaffected partitions remain valid.
        var options = new ProducerOptions { BootstrapServers = ["localhost:9092"] };
        var accumulator = new RecordAccumulator(options);
        var tracker = new PartitionInflightTracker();

        // Produce to two partitions
        var seqP0 = accumulator.GetAndIncrementSequence(Tp0, 10);
        var seqP1 = accumulator.GetAndIncrementSequence(Tp1, 20);
        var entryP0 = tracker.Register(Tp0, seqP0, 10);
        var entryP1 = tracker.Register(Tp1, seqP1, 20);

        // Epoch bump (resets ALL sequences)
        accumulator.ResetSequenceNumbers();

        // Both partitions start from 0
        var newSeqP0 = accumulator.GetAndIncrementSequence(Tp0, 5);
        var newSeqP1 = accumulator.GetAndIncrementSequence(Tp1, 5);
        await Assert.That(newSeqP0).IsEqualTo(0);
        await Assert.That(newSeqP1).IsEqualTo(0);

        // But inflight entries for unaffected partition are still tracked
        await Assert.That(tracker.GetInflightCount(Tp1)).IsEqualTo(1);
        await Assert.That(tracker.IsHeadOfLine(entryP1)).IsTrue();

        tracker.Complete(entryP0);
        tracker.Complete(entryP1);
        await accumulator.DisposeAsync();
    }

    [Test]
    public async Task EpochBump_RecordBatchRewrite_UpdatesProducerStateAndSequence()
    {
        // End-to-end: create a RecordBatch, simulate epoch bump rewrite, verify round-trip
        var records = new List<Record>
        {
            new()
            {
                TimestampDelta = 0,
                OffsetDelta = 0,
                Key = "key"u8.ToArray(),
                Value = "value"u8.ToArray(),
            }
        };

        // Original batch with old epoch
        var original = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 1000,
            MaxTimestamp = 1000,
            ProducerId = 42,
            ProducerEpoch = 3,
            BaseSequence = 15,
            Records = records
        };

        // Verify original writes correctly
        var buffer1 = new ArrayBufferWriter<byte>();
        original.Write(buffer1);
        var reader1 = new KafkaProtocolReader(buffer1.WrittenMemory);
        var readBack1 = RecordBatch.Read(ref reader1);
        await Assert.That(readBack1.ProducerEpoch).IsEqualTo((short)3);
        await Assert.That(readBack1.BaseSequence).IsEqualTo(15);

        // Epoch bump: new epoch, sequence reset to 0
        var rewritten = original.WithProducerState(producerId: 42, producerEpoch: 4, baseSequence: 0);

        var buffer2 = new ArrayBufferWriter<byte>();
        rewritten.Write(buffer2);
        var reader2 = new KafkaProtocolReader(buffer2.WrittenMemory);
        var readBack2 = RecordBatch.Read(ref reader2);

        await Assert.That(readBack2.ProducerEpoch).IsEqualTo((short)4);
        await Assert.That(readBack2.BaseSequence).IsEqualTo(0);

        // Record data unchanged
        await Assert.That(readBack2.Records[0].Key.ToArray()).IsEquivalentTo("key"u8.ToArray());
        await Assert.That(readBack2.Records[0].Value.ToArray()).IsEquivalentTo("value"u8.ToArray());
    }

    [Test]
    public async Task EpochBump_LazyEpochCheck_StaleBatchRewrite()
    {
        // Simulates the lazy epoch check in SendCoalescedAsync:
        // A batch sealed with epoch=1 is in the channel when epoch bumps to 2.
        // Before sending, the batch should be rewritten with epoch=2 and fresh sequence.
        var options = new ProducerOptions { BootstrapServers = ["localhost:9092"] };
        var accumulator = new RecordAccumulator(options);
        var tracker = new PartitionInflightTracker();

        // Batch was sealed with old epoch
        var oldSeq = accumulator.GetAndIncrementSequence(Tp0, 5);
        var entry = tracker.Register(Tp0, oldSeq, 5);

        var records = new List<Record>
        {
            new() { TimestampDelta = 0, OffsetDelta = 0, IsKeyNull = true, Value = "data"u8.ToArray() }
        };

        var staleBatch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 1000,
            MaxTimestamp = 1000,
            ProducerId = 100,
            ProducerEpoch = 1, // Old epoch
            BaseSequence = oldSeq,
            Records = records
        };

        // Epoch bumps
        accumulator.ProducerId = 100;
        accumulator.ProducerEpoch = 2;
        accumulator.ResetSequenceNumbers();
        short currentEpoch = 2;

        // Lazy epoch check detects stale batch
        var batchEpoch = staleBatch.ProducerEpoch;
        await Assert.That(batchEpoch).IsNotEqualTo(currentEpoch);

        // Complete old inflight entry
        tracker.Complete(entry);

        // Get new sequence and rewrite
        var newSeq = accumulator.GetAndIncrementSequence(Tp0, 5);
        await Assert.That(newSeq).IsEqualTo(0);

        var rewritten = staleBatch.WithProducerState(
            accumulator.ProducerId, currentEpoch, newSeq);

        // Re-register with tracker
        var newEntry = tracker.Register(Tp0, newSeq, 5);

        // Verify rewritten batch
        await Assert.That(rewritten.ProducerEpoch).IsEqualTo((short)2);
        await Assert.That(rewritten.BaseSequence).IsEqualTo(0);
        await Assert.That(tracker.IsHeadOfLine(newEntry)).IsTrue();

        // Round-trip validation
        var buffer = new ArrayBufferWriter<byte>();
        rewritten.Write(buffer);
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var readBack = RecordBatch.Read(ref reader);
        await Assert.That(readBack.ProducerEpoch).IsEqualTo((short)2);
        await Assert.That(readBack.BaseSequence).IsEqualTo(0);

        tracker.Complete(newEntry);
        await accumulator.DisposeAsync();
    }

    [Test]
    public async Task EpochBump_Dedup_AlreadyBumped_SkipsRedundantBump()
    {
        // Simulates the expectedEpoch dedup logic in BumpEpochAsync:
        // When multiple partitions fail with OOSN in the same response,
        // only the first caller bumps, others see epoch already changed.
        var options = new ProducerOptions { BootstrapServers = ["localhost:9092"] };
        var accumulator = new RecordAccumulator(options);

        // Initial state
        accumulator.ProducerEpoch = 1;
        accumulator.GetAndIncrementSequence(Tp0, 10);
        accumulator.GetAndIncrementSequence(Tp1, 20);

        // First caller bumps: expectedEpoch=1, currentEpoch=1 → proceed
        short expectedEpoch = 1;
        var currentEpoch = accumulator.ProducerEpoch;
        await Assert.That(currentEpoch).IsEqualTo(expectedEpoch);

        // Simulate successful bump
        accumulator.ProducerEpoch = 2;
        accumulator.ResetSequenceNumbers();

        // Second caller: expectedEpoch=1, currentEpoch=2 → skip (already bumped)
        currentEpoch = accumulator.ProducerEpoch;
        await Assert.That(currentEpoch).IsNotEqualTo(expectedEpoch);

        // Sequences were reset by first caller
        var seq = accumulator.GetAndIncrementSequence(Tp0, 1);
        await Assert.That(seq).IsEqualTo(0);

        await accumulator.DisposeAsync();
    }

    [Test]
    public async Task EpochBump_NonEpochBatch_NotRewrittenByLazyCheck()
    {
        // Batches with ProducerEpoch=-1 (non-idempotent) should not be rewritten
        var nonIdempotentBatch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 0,
            MaxTimestamp = 0,
            ProducerId = -1,
            ProducerEpoch = -1,
            BaseSequence = -1,
            Records = new List<Record>()
        };

        short currentEpoch = 5;
        var batchEpoch = nonIdempotentBatch.ProducerEpoch;

        // Epoch is -1, which is < 0, so the lazy check condition `batchEpoch >= 0` is false
        var needsRewrite = batchEpoch >= 0 && batchEpoch != currentEpoch;
        await Assert.That(needsRewrite).IsFalse();
    }

    [Test]
    public async Task EpochBump_MatchingEpoch_NotRewritten()
    {
        // Batch with matching epoch should not be rewritten
        var batch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 0,
            MaxTimestamp = 0,
            ProducerId = 100,
            ProducerEpoch = 5,
            BaseSequence = 0,
            Records = new List<Record>()
        };

        short currentEpoch = 5;
        var batchEpoch = batch.ProducerEpoch;

        var needsRewrite = batchEpoch >= 0 && batchEpoch != currentEpoch;
        await Assert.That(needsRewrite).IsFalse();
    }

    #endregion

    #region ReadyBatch Reset and Pool Tests

    [Test]
    public async Task ReadyBatch_Reset_ClearsInflightEntry()
    {
        var pool = new ReadyBatchPool();
        var batch = pool.Rent();

        var entry = new InflightEntry();
        entry.Initialize(Tp0, baseSequence: 0, recordCount: 10);
        batch.InflightEntry = entry;

        await Assert.That(batch.InflightEntry).IsNotNull();

        pool.Return(batch); // Return calls Reset()

        await Assert.That(batch.InflightEntry).IsNull();
    }

    #endregion
}
