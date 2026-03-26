using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Tests for Kafka RecordBatch v2 format encoding/decoding.
/// Reference: https://kafka.apache.org/documentation/#recordbatch
/// </summary>
public class RecordBatchTests
{
    #region RecordBatch Structure Tests

    [Test]
    public async Task RecordBatch_MagicByte_IsTwo()
    {
        var batch = new RecordBatch
        {
            BaseOffset = 0,
            Records = []
        };

        await Assert.That(batch.Magic).IsEqualTo((byte)2);
    }

    [Test]
    public async Task RecordBatch_DefaultProducerIdAndEpoch()
    {
        var batch = new RecordBatch
        {
            BaseOffset = 0,
            Records = []
        };

        await Assert.That(batch.ProducerId).IsEqualTo(-1L);
        await Assert.That(batch.ProducerEpoch).IsEqualTo((short)-1);
        await Assert.That(batch.BaseSequence).IsEqualTo(-1);
    }

    #endregion

    #region RecordBatch Write Tests

    [Test]
    public async Task RecordBatch_Write_CorrectStructure()
    {
        var buffer = new ArrayBufferWriter<byte>();

        var batch = new RecordBatch
        {
            BaseOffset = 0,
            PartitionLeaderEpoch = -1,
            BaseTimestamp = 1000,
            MaxTimestamp = 1000,
            ProducerId = -1,
            ProducerEpoch = -1,
            BaseSequence = -1,
            Records =
            [
                new Record
                {
                    OffsetDelta = 0,
                    TimestampDelta = 0,
                    IsKeyNull = true,
                    Value = "test"u8.ToArray()
                }
            ]
        };

        batch.Write(buffer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        // Read and verify header fields (no awaits between reader operations)
        var baseOffset = reader.ReadInt64();
        var batchLength = reader.ReadInt32();
        var partitionLeaderEpoch = reader.ReadInt32();
        var magic = reader.ReadUInt8();
        var crc = reader.ReadInt32();
        var attributes = reader.ReadInt16();
        var lastOffsetDelta = reader.ReadInt32();
        var baseTimestamp = reader.ReadInt64();
        var maxTimestamp = reader.ReadInt64();
        var producerId = reader.ReadInt64();
        var producerEpoch = reader.ReadInt16();
        var baseSequence = reader.ReadInt32();
        var recordCount = reader.ReadInt32();

        await Assert.That(baseOffset).IsEqualTo(0L);
        await Assert.That(batchLength).IsGreaterThan(0);
        await Assert.That(partitionLeaderEpoch).IsEqualTo(-1);
        await Assert.That(magic).IsEqualTo((byte)2);
        await Assert.That(baseTimestamp).IsEqualTo(1000L);
        await Assert.That(producerId).IsEqualTo(-1L);
        await Assert.That(producerEpoch).IsEqualTo((short)-1);
        await Assert.That(baseSequence).IsEqualTo(-1);
        await Assert.That(recordCount).IsEqualTo(1);
    }

    #endregion

    #region RecordBatch RoundTrip Tests

    [Test]
    public async Task RecordBatch_RoundTrip_SingleRecord()
    {
        var buffer = new ArrayBufferWriter<byte>();

        var originalBatch = new RecordBatch
        {
            BaseOffset = 42,
            PartitionLeaderEpoch = 1,
            BaseTimestamp = 1234567890000,
            MaxTimestamp = 1234567890000,
            ProducerId = 123,
            ProducerEpoch = 0,
            BaseSequence = 0,
            LastOffsetDelta = 0,
            Records =
            [
                new Record
                {
                    OffsetDelta = 0,
                    TimestampDelta = 0,
                    Key = "key"u8.ToArray(),
                    Value = "value"u8.ToArray()
                }
            ]
        };

        originalBatch.Write(buffer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var parsedBatch = RecordBatch.Read(ref reader);

        await Assert.That(parsedBatch.BaseOffset).IsEqualTo(42L);
        await Assert.That(parsedBatch.PartitionLeaderEpoch).IsEqualTo(1);
        await Assert.That(parsedBatch.Magic).IsEqualTo((byte)2);
        await Assert.That(parsedBatch.BaseTimestamp).IsEqualTo(1234567890000L);
        await Assert.That(parsedBatch.ProducerId).IsEqualTo(123L);
        await Assert.That(parsedBatch.ProducerEpoch).IsEqualTo((short)0);
        await Assert.That(parsedBatch.BaseSequence).IsEqualTo(0);
        await Assert.That(parsedBatch.Records.Count).IsEqualTo(1);

        var record = parsedBatch.Records[0];
        await Assert.That(record.IsKeyNull).IsFalse();
        await Assert.That(record.Key.ToArray()).IsEquivalentTo("key"u8.ToArray());
        await Assert.That(record.IsValueNull).IsFalse();
        await Assert.That(record.Value.ToArray()).IsEquivalentTo("value"u8.ToArray());
    }

    [Test]
    public async Task RecordBatch_RoundTrip_MultipleRecords()
    {
        var buffer = new ArrayBufferWriter<byte>();

        var originalBatch = new RecordBatch
        {
            BaseOffset = 100,
            PartitionLeaderEpoch = 0,
            BaseTimestamp = 1000,
            MaxTimestamp = 1002,
            LastOffsetDelta = 2,
            Records =
            [
                new Record
                {
                    OffsetDelta = 0,
                    TimestampDelta = 0,
                    Key = "k1"u8.ToArray(),
                    Value = "v1"u8.ToArray()
                },
                new Record
                {
                    OffsetDelta = 1,
                    TimestampDelta = 1,
                    Key = "k2"u8.ToArray(),
                    Value = "v2"u8.ToArray()
                },
                new Record
                {
                    OffsetDelta = 2,
                    TimestampDelta = 2,
                    Key = "k3"u8.ToArray(),
                    Value = "v3"u8.ToArray()
                }
            ]
        };

        originalBatch.Write(buffer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var parsedBatch = RecordBatch.Read(ref reader);

        await Assert.That(parsedBatch.Records.Count).IsEqualTo(3);
        await Assert.That(parsedBatch.Records[0].Key.ToArray()).IsEquivalentTo("k1"u8.ToArray());
        await Assert.That(parsedBatch.Records[1].Key.ToArray()).IsEquivalentTo("k2"u8.ToArray());
        await Assert.That(parsedBatch.Records[2].Key.ToArray()).IsEquivalentTo("k3"u8.ToArray());
    }

    [Test]
    public async Task RecordBatch_RoundTrip_NullKey()
    {
        var buffer = new ArrayBufferWriter<byte>();

        var originalBatch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 0,
            MaxTimestamp = 0,
            Records =
            [
                new Record
                {
                    OffsetDelta = 0,
                    TimestampDelta = 0,
                    IsKeyNull = true,
                    Value = "value"u8.ToArray()
                }
            ]
        };

        originalBatch.Write(buffer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var parsedBatch = RecordBatch.Read(ref reader);

        await Assert.That(parsedBatch.Records[0].IsKeyNull).IsTrue();
        await Assert.That(parsedBatch.Records[0].IsValueNull).IsFalse();
        await Assert.That(parsedBatch.Records[0].Value.ToArray()).IsEquivalentTo("value"u8.ToArray());
    }

    #endregion

    #region Attributes Tests

    [Test]
    public async Task RecordBatchAttributes_CompressionBits()
    {
        // Compression is stored in bits 0-2
        var compressionNone = (int)RecordBatchAttributes.CompressionNone & 0x07;
        var compressionGzip = (int)RecordBatchAttributes.CompressionGzip & 0x07;
        var compressionSnappy = (int)RecordBatchAttributes.CompressionSnappy & 0x07;
        var compressionLz4 = (int)RecordBatchAttributes.CompressionLz4 & 0x07;
        var compressionZstd = (int)RecordBatchAttributes.CompressionZstd & 0x07;

        await Assert.That(compressionNone).IsEqualTo(0);
        await Assert.That(compressionGzip).IsEqualTo(1);
        await Assert.That(compressionSnappy).IsEqualTo(2);
        await Assert.That(compressionLz4).IsEqualTo(3);
        await Assert.That(compressionZstd).IsEqualTo(4);
    }

    [Test]
    public async Task RecordBatchAttributes_TimestampTypeBit()
    {
        // Timestamp type is bit 3
        var timestampType = (int)RecordBatchAttributes.TimestampTypeLogAppendTime;
        await Assert.That(timestampType).IsEqualTo(0x08);
    }

    [Test]
    public async Task RecordBatchAttributes_TransactionalBit()
    {
        // Transactional is bit 4
        var transactional = (int)RecordBatchAttributes.IsTransactional;
        await Assert.That(transactional).IsEqualTo(0x10);
    }

    #endregion

    #region Zero-Copy Memory Management Tests

    [Test]
    [NotInParallel("RecordBatchPool")]
    public async Task RecordBatch_Dispose_DisposesLazyRecordList()
    {
        var buffer = new ArrayBufferWriter<byte>();

        var originalBatch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 0,
            MaxTimestamp = 0,
            Records =
            [
                new Record
                {
                    OffsetDelta = 0,
                    TimestampDelta = 0,
                    Key = "key"u8.ToArray(),
                    Value = "value"u8.ToArray()
                }
            ]
        };

        originalBatch.Write(buffer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var parsedBatch = RecordBatch.Read(ref reader);

        // Access record before dispose
        var record = parsedBatch.Records[0];
        await Assert.That(record.Key.ToArray()).IsEquivalentTo("key"u8.ToArray());

        // Dispose the batch (returns to pool, clearing Records reference)
        parsedBatch.Dispose();

        // Accessing records after dispose should throw ObjectDisposedException
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
        {
            _ = parsedBatch.Records[0];
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task RecordBatch_Dispose_IsIdempotent()
    {
        var buffer = new ArrayBufferWriter<byte>();

        var originalBatch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 0,
            MaxTimestamp = 0,
            Records = [new Record { OffsetDelta = 0, TimestampDelta = 0, IsKeyNull = true, Value = "v"u8.ToArray() }]
        };

        originalBatch.Write(buffer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var parsedBatch = RecordBatch.Read(ref reader);

        // Multiple disposes should not throw
        parsedBatch.Dispose();
        parsedBatch.Dispose();
        parsedBatch.Dispose();

        // If we get here without exception, the test passed
        await Task.CompletedTask;
    }

    [Test]
    public async Task RecordBatch_WithPooledMemoryContext_MarksMemoryAsUsed()
    {
        var buffer = new ArrayBufferWriter<byte>();

        var originalBatch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 0,
            MaxTimestamp = 0,
            Records = [new Record { OffsetDelta = 0, TimestampDelta = 0, IsKeyNull = true, Value = "test"u8.ToArray() }]
        };

        originalBatch.Write(buffer);

        // Create a mock pooled memory
        var mockMemory = new MockPooledMemory(buffer.WrittenMemory.ToArray());

        // Set up the parsing context
        ResponseParsingContext.SetPooledMemory(mockMemory);
        try
        {
            var reader = new KafkaProtocolReader(buffer.WrittenMemory);
            var parsedBatch = RecordBatch.Read(ref reader);

            // Memory should be marked as used (not taken - ownership transferred later)
            await Assert.That(ResponseParsingContext.WasMemoryUsed).IsTrue();

            // Taking the memory should return it
            var takenMemory = ResponseParsingContext.TakePooledMemory();
            await Assert.That(takenMemory).IsEqualTo(mockMemory);

            // Disposing the taken memory disposes the mock
            takenMemory!.Dispose();
            await Assert.That(mockMemory.IsDisposed).IsTrue();

            // Disposing the batch should not throw (it no longer owns the memory)
            parsedBatch.Dispose();
        }
        finally
        {
            ResponseParsingContext.Reset();
        }
    }

    [Test]
    public async Task RecordBatch_WithoutPooledMemoryContext_CopiesData()
    {
        // Ensure no stale thread-local state from a prior test on the same thread
        ResponseParsingContext.Reset();

        var buffer = new ArrayBufferWriter<byte>();

        var originalBatch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 0,
            MaxTimestamp = 0,
            Records = [new Record { OffsetDelta = 0, TimestampDelta = 0, IsKeyNull = true, Value = "test"u8.ToArray() }]
        };

        originalBatch.Write(buffer);

        // Parse without any context - should copy data
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var parsedBatch = RecordBatch.Read(ref reader);

        // Access records to trigger lazy parsing
        var record = parsedBatch.Records[0];
        await Assert.That(record.Value.ToArray()).IsEquivalentTo("test"u8.ToArray());

        // Dispose should not throw even without memory owner
        parsedBatch.Dispose();

        // If we get here without exception, the test passed
        await Task.CompletedTask;
    }

    [Test]
    public async Task ResponseParsingContext_TakePooledMemory_RequiresMemoryUsed()
    {
        var mockMemory = new MockPooledMemory(new byte[100]);

        ResponseParsingContext.SetPooledMemory(mockMemory);
        try
        {
            // Without marking as used, take should return null
            var taken1 = ResponseParsingContext.TakePooledMemory();
            await Assert.That(taken1).IsNull();

            // Mark as used
            ResponseParsingContext.MarkMemoryUsed();
            await Assert.That(ResponseParsingContext.WasMemoryUsed).IsTrue();

            // Now take should succeed
            var taken2 = ResponseParsingContext.TakePooledMemory();
            await Assert.That(taken2).IsNotNull();
        }
        finally
        {
            ResponseParsingContext.Reset();
        }
    }

    [Test]
    public async Task ResponseParsingContext_Reset_ClearsState()
    {
        var mockMemory = new MockPooledMemory(new byte[100]);

        ResponseParsingContext.SetPooledMemory(mockMemory);
        ResponseParsingContext.MarkMemoryUsed();
        await Assert.That(ResponseParsingContext.WasMemoryUsed).IsTrue();

        ResponseParsingContext.Reset();

        await Assert.That(ResponseParsingContext.WasMemoryUsed).IsFalse();
        await Assert.That(ResponseParsingContext.HasPooledMemory).IsFalse();
    }

    /// <summary>
    /// Mock implementation of IPooledMemory for testing.
    /// </summary>
    private sealed class MockPooledMemory : IPooledMemory
    {
        private readonly byte[] _data;

        public MockPooledMemory(byte[] data) => _data = data;

        public ReadOnlyMemory<byte> Memory => _data;
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

    #endregion

    #region PreCompress Tests

    [Test]
    public async Task PreCompress_ThenWithProducerState_TransfersDataToNewBatch()
    {
        var batch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 1000,
            MaxTimestamp = 1000,
            ProducerId = 1,
            ProducerEpoch = 0,
            BaseSequence = 0,
            Records =
            [
                new Record
                {
                    OffsetDelta = 0,
                    TimestampDelta = 0,
                    Key = "key"u8.ToArray(),
                    Value = "value"u8.ToArray()
                }
            ]
        };

        // Pre-compress with Gzip (built-in codec)
        batch.PreCompress(CompressionType.Gzip, null);

        await Assert.That(batch.PreCompressedRecords).IsNotNull();
        await Assert.That(batch.PreCompressedLength).IsGreaterThan(0);
        await Assert.That(batch.PreCompressedType).IsEqualTo(CompressionType.Gzip);

        // Transfer to new batch via WithProducerState
        var newBatch = batch.WithProducerState(42, 1, 10);

        // New batch should have the pre-compressed data
        await Assert.That(newBatch.PreCompressedRecords).IsNotNull();
        await Assert.That(newBatch.PreCompressedLength).IsGreaterThan(0);
        await Assert.That(newBatch.PreCompressedType).IsEqualTo(CompressionType.Gzip);
        await Assert.That(newBatch.ProducerId).IsEqualTo(42L);
        await Assert.That(newBatch.ProducerEpoch).IsEqualTo((short)1);
        await Assert.That(newBatch.BaseSequence).IsEqualTo(10);

        // Old batch should have cleared pre-compressed data
        await Assert.That(batch.PreCompressedRecords).IsNull();
        await Assert.That(batch.PreCompressedLength).IsEqualTo(0);
        await Assert.That(batch.PreCompressedType).IsEqualTo(CompressionType.None);

        // Clean up
        newBatch.Dispose();
    }

    [Test]
    public async Task ReturnPreCompressedBuffer_CalledTwice_IsIdempotent()
    {
        var batch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 0,
            MaxTimestamp = 0,
            Records =
            [
                new Record
                {
                    OffsetDelta = 0,
                    TimestampDelta = 0,
                    Key = "key"u8.ToArray(),
                    Value = "value"u8.ToArray()
                }
            ]
        };

        // Pre-compress to populate the buffer
        batch.PreCompress(CompressionType.Gzip, null);
        await Assert.That(batch.PreCompressedRecords).IsNotNull();

        // First call should clear it
        batch.ReturnPreCompressedBuffer();
        await Assert.That(batch.PreCompressedRecords).IsNull();
        await Assert.That(batch.PreCompressedLength).IsEqualTo(0);

        // Second call should be safe (no-op, no double-return to ArrayPool)
        batch.ReturnPreCompressedBuffer();
        await Assert.That(batch.PreCompressedRecords).IsNull();

        // Dispose should also be safe after explicit return
        batch.Dispose();
    }

    [Test]
    public async Task Write_WithPreCompressedData_ProducesSameOutputAsInlineCompression()
    {
        var records = new Record[]
        {
            new()
            {
                OffsetDelta = 0,
                TimestampDelta = 0,
                Key = "key1"u8.ToArray(),
                Value = "value1"u8.ToArray()
            },
            new()
            {
                OffsetDelta = 1,
                TimestampDelta = 1,
                Key = "key2"u8.ToArray(),
                Value = "value2"u8.ToArray()
            }
        };

        // Batch 1: Write with inline compression (no pre-compress)
        var batch1 = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 1000,
            MaxTimestamp = 1001,
            LastOffsetDelta = 1,
            ProducerId = -1,
            ProducerEpoch = -1,
            BaseSequence = -1,
            Records = records
        };

        var inlineBuffer = new ArrayBufferWriter<byte>();
        batch1.Write(inlineBuffer, CompressionType.Gzip);

        // Batch 2: Write with pre-compressed data
        var batch2 = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 1000,
            MaxTimestamp = 1001,
            LastOffsetDelta = 1,
            ProducerId = -1,
            ProducerEpoch = -1,
            BaseSequence = -1,
            Records = records
        };

        batch2.PreCompress(CompressionType.Gzip, null);
        var preCompressedBuffer = new ArrayBufferWriter<byte>();
        batch2.Write(preCompressedBuffer);

        // Both should produce identical output (same compression, same data)
        await Assert.That(preCompressedBuffer.WrittenSpan.ToArray())
            .IsEquivalentTo(inlineBuffer.WrittenSpan.ToArray());

        // Clean up
        batch2.Dispose();
    }

    #endregion

    #region Pool Reuse on Consumer Read Path Tests

    [Test]
    public async Task RecordBatch_Read_ReturnsBatchFromPool()
    {
        // Create a batch, write it, then read it back — the read path should use RentFromPool()
        var buffer = new ArrayBufferWriter<byte>();

        var originalBatch = new RecordBatch
        {
            BaseOffset = 10,
            BaseTimestamp = 5000,
            MaxTimestamp = 5000,
            Records =
            [
                new Record
                {
                    OffsetDelta = 0,
                    TimestampDelta = 0,
                    Key = "k"u8.ToArray(),
                    Value = "v"u8.ToArray()
                }
            ]
        };

        originalBatch.Write(buffer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var parsedBatch = RecordBatch.Read(ref reader);

        // Verify the parsed batch has correct data
        await Assert.That(parsedBatch.BaseOffset).IsEqualTo(10L);
        await Assert.That(parsedBatch.BaseTimestamp).IsEqualTo(5000L);
        await Assert.That(parsedBatch.Records.Count).IsEqualTo(1);
        await Assert.That(parsedBatch.Records[0].Key.ToArray()).IsEquivalentTo("k"u8.ToArray());

        // Dispose returns to pool
        parsedBatch.Dispose();

        // Read again — should reuse the pooled batch instance
        var reader2 = new KafkaProtocolReader(buffer.WrittenMemory);
        var reusedBatch = RecordBatch.Read(ref reader2);

        // The reused batch should have fresh data, not stale state
        await Assert.That(reusedBatch.BaseOffset).IsEqualTo(10L);
        await Assert.That(reusedBatch.Records.Count).IsEqualTo(1);

        reusedBatch.Dispose();
    }

    [Test]
    public async Task RecordBatch_Dispose_ReturnsBatchToPoolForReuse()
    {
        var buffer = new ArrayBufferWriter<byte>();

        var originalBatch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 0,
            MaxTimestamp = 0,
            Records = [new Record { OffsetDelta = 0, TimestampDelta = 0, IsKeyNull = true, Value = "v"u8.ToArray() }]
        };

        originalBatch.Write(buffer);

        // Read a batch (this rents from pool)
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var parsedBatch = RecordBatch.Read(ref reader);

        // Access records to ensure they work
        await Assert.That(parsedBatch.Records[0].Value.ToArray()).IsEquivalentTo("v"u8.ToArray());

        // Dispose returns to pool
        parsedBatch.Dispose();

        // Rent from pool — should get back the same instance (or another pooled one)
        var rentedBatch = RecordBatch.RentFromPool();

        // The rented batch should have reset state (defaults)
        await Assert.That(rentedBatch.BaseOffset).IsEqualTo(0L);
        await Assert.That(rentedBatch.BatchLength).IsEqualTo(0);
        await Assert.That(rentedBatch.PartitionLeaderEpoch).IsEqualTo(-1);
        await Assert.That(rentedBatch.Magic).IsEqualTo((byte)2);
        await Assert.That(rentedBatch.ProducerId).IsEqualTo(-1L);
        await Assert.That(rentedBatch.ProducerEpoch).IsEqualTo((short)-1);
        await Assert.That(rentedBatch.BaseSequence).IsEqualTo(-1);

        // Clean up
        rentedBatch.ReturnToPool();
    }

    [Test]
    public async Task RecordBatch_Dispose_CalledTwice_DoesNotDoubleReturnToPool()
    {
        var buffer = new ArrayBufferWriter<byte>();

        var originalBatch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 0,
            MaxTimestamp = 0,
            Records = [new Record { OffsetDelta = 0, TimestampDelta = 0, IsKeyNull = true, Value = "v"u8.ToArray() }]
        };

        originalBatch.Write(buffer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var parsedBatch = RecordBatch.Read(ref reader);

        // Double dispose should not throw or corrupt the pool
        parsedBatch.Dispose();
        parsedBatch.Dispose();

        // Pool should still be functional — rent and return without issues
        var batch = RecordBatch.RentFromPool();
        await Assert.That(batch).IsNotNull();
        batch.ReturnToPool();
    }

    [Test]
    public async Task RecordBatch_PooledBatch_ClearsStaleRecordsOnReturn()
    {
        var buffer = new ArrayBufferWriter<byte>();

        var originalBatch = new RecordBatch
        {
            BaseOffset = 99,
            BaseTimestamp = 9999,
            MaxTimestamp = 9999,
            ProducerId = 42,
            ProducerEpoch = 7,
            BaseSequence = 100,
            Attributes = RecordBatchAttributes.IsTransactional,
            Records =
            [
                new Record
                {
                    OffsetDelta = 0,
                    TimestampDelta = 0,
                    Key = "stale-key"u8.ToArray(),
                    Value = "stale-value"u8.ToArray()
                }
            ]
        };

        originalBatch.Write(buffer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var parsedBatch = RecordBatch.Read(ref reader);

        // Verify stale data is present
        await Assert.That(parsedBatch.BaseOffset).IsEqualTo(99L);
        await Assert.That(parsedBatch.ProducerId).IsEqualTo(42L);

        // Dispose returns to pool, clearing all state
        parsedBatch.Dispose();

        // Rent from pool — all fields should be reset to defaults
        var freshBatch = RecordBatch.RentFromPool();
        await Assert.That(freshBatch.BaseOffset).IsEqualTo(0L);
        await Assert.That(freshBatch.BatchLength).IsEqualTo(0);
        await Assert.That(freshBatch.ProducerId).IsEqualTo(-1L);
        await Assert.That(freshBatch.ProducerEpoch).IsEqualTo((short)-1);
        await Assert.That(freshBatch.BaseSequence).IsEqualTo(-1);
        await Assert.That(freshBatch.Attributes).IsEqualTo(RecordBatchAttributes.None);
        await Assert.That(freshBatch.BaseTimestamp).IsEqualTo(0L);
        await Assert.That(freshBatch.MaxTimestamp).IsEqualTo(0L);

        freshBatch.ReturnToPool();
    }

    [Test]
    public async Task RecordBatch_MultipleReadDisposeCycles_ReusePooledBatches()
    {
        var buffer = new ArrayBufferWriter<byte>();

        var originalBatch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 0,
            MaxTimestamp = 0,
            Records = [new Record { OffsetDelta = 0, TimestampDelta = 0, IsKeyNull = true, Value = "test"u8.ToArray() }]
        };

        originalBatch.Write(buffer);

        // Perform multiple read-dispose cycles to exercise pool reuse
        for (var i = 0; i < 10; i++)
        {
            var reader = new KafkaProtocolReader(buffer.WrittenMemory);
            var batch = RecordBatch.Read(ref reader);

            await Assert.That(batch.Records.Count).IsEqualTo(1);
            await Assert.That(batch.Records[0].Value.ToArray()).IsEquivalentTo("test"u8.ToArray());

            batch.Dispose();
        }
    }

    #endregion
}
