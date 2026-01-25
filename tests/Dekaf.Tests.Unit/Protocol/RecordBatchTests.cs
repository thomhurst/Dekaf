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

        // Dispose the batch
        parsedBatch.Dispose();

        // Accessing records after dispose should throw
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
}
