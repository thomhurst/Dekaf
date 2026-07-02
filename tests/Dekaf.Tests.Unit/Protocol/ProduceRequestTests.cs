using System.Buffers;
using Dekaf.Compression;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class ProduceRequestTests
{
    [Test]
    public async Task PartitionData_Write_EncodesRecordBatchesWithoutChangingBytes()
    {
        var batches = new[]
        {
            CreateBatch(baseOffset: 0, offsetDelta: 0, value: "one"u8.ToArray()),
            CreateBatch(baseOffset: 1, offsetDelta: 1, value: "two"u8.ToArray())
        };

        var expectedRecords = new ArrayBufferWriter<byte>();
        foreach (var batch in batches)
        {
            batch.Write(expectedRecords);
        }

        var partitionData = new ProduceRequestPartitionData
        {
            Index = 3,
            Records = batches
        };

        int index;
        int recordsLength;
        byte[] recordsBytes;
        int taggedFields;
        bool readerEnd;
        int writerBytesWritten;
        int bufferBytesWritten;
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new KafkaProtocolWriter(buffer);
            partitionData.Write(ref writer, ProduceRequest.HighestSupportedVersion);

            var reader = new KafkaProtocolReader(buffer.WrittenMemory);
            index = reader.ReadInt32();
            recordsLength = reader.ReadUnsignedVarInt() - 1;
            recordsBytes = reader.ReadRawBytes(recordsLength);
            taggedFields = reader.ReadUnsignedVarInt();
            readerEnd = reader.End;
            writerBytesWritten = writer.BytesWritten;
            bufferBytesWritten = buffer.WrittenCount;
        }

        await Assert.That(index).IsEqualTo(3);
        await Assert.That(recordsLength).IsEqualTo(expectedRecords.WrittenCount);
        await Assert.That(recordsBytes).IsEquivalentTo(expectedRecords.WrittenSpan.ToArray());
        await Assert.That(taggedFields).IsEqualTo(0);
        await Assert.That(readerEnd).IsTrue();
        await Assert.That(writerBytesWritten).IsEqualTo(bufferBytesWritten);
    }

    [Test]
    public async Task PartitionData_Write_WithInlineCompression_ReturnsTemporaryPrecompressedBuffer()
    {
        var batch = CreateBatch(baseOffset: 0, offsetDelta: 0, value: "compressed-value"u8.ToArray());
        var partitionData = new ProduceRequestPartitionData
        {
            Index = 0,
            Records = [batch],
            Compression = CompressionType.Gzip
        };

        int index;
        byte[] recordsBytes;
        int taggedFields;
        bool readerEnd;
        int writerBytesWritten;
        int bufferBytesWritten;
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new KafkaProtocolWriter(buffer);
            partitionData.Write(ref writer, ProduceRequest.HighestSupportedVersion);

            var reader = new KafkaProtocolReader(buffer.WrittenMemory);
            index = reader.ReadInt32();
            var recordsLength = reader.ReadUnsignedVarInt() - 1;
            recordsBytes = reader.ReadRawBytes(recordsLength);
            taggedFields = reader.ReadUnsignedVarInt();
            readerEnd = reader.End;
            writerBytesWritten = writer.BytesWritten;
            bufferBytesWritten = buffer.WrittenCount;
        }

        byte[] value;
        int recordCount;
        {
            var recordsReader = new KafkaProtocolReader(recordsBytes);
            using var parsedBatch = RecordBatch.Read(ref recordsReader);
            recordCount = parsedBatch.Records.Count;
            value = parsedBatch.Records[0].Value.ToArray();
        }

        await Assert.That(batch.PreCompressedRecords).IsNull();
        await Assert.That(batch.PreCompressedLength).IsEqualTo(0);
        await Assert.That(writerBytesWritten).IsEqualTo(bufferBytesWritten);
        await Assert.That(index).IsEqualTo(0);
        await Assert.That(recordCount).IsEqualTo(1);
        await Assert.That(value).IsEquivalentTo("compressed-value"u8.ToArray());
        await Assert.That(taggedFields).IsEqualTo(0);
        await Assert.That(readerEnd).IsTrue();
    }

    [Test]
    public async Task PartitionData_Write_UsesEffectiveHeaderCountForRecordsLength()
    {
        var batch = new RecordBatch
        {
            BaseOffset = 0,
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
                    Key = "key"u8.ToArray(),
                    Value = "value"u8.ToArray(),
                    Headers = null,
                    HeaderCount = 128
                }
            ]
        };
        var expectedRecords = new ArrayBufferWriter<byte>();
        batch.Write(expectedRecords);

        var partitionData = new ProduceRequestPartitionData
        {
            Index = 0,
            Records = [batch]
        };

        int index;
        int recordsLength;
        byte[] recordsBytes;
        int taggedFields;
        bool readerEnd;
        int writerBytesWritten;
        int bufferBytesWritten;
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new KafkaProtocolWriter(buffer);
            partitionData.Write(ref writer, ProduceRequest.HighestSupportedVersion);

            var reader = new KafkaProtocolReader(buffer.WrittenMemory);
            index = reader.ReadInt32();
            recordsLength = reader.ReadUnsignedVarInt() - 1;
            recordsBytes = reader.ReadRawBytes(recordsLength);
            taggedFields = reader.ReadUnsignedVarInt();
            readerEnd = reader.End;
            writerBytesWritten = writer.BytesWritten;
            bufferBytesWritten = buffer.WrittenCount;
        }

        await Assert.That(index).IsEqualTo(0);
        await Assert.That(recordsLength).IsEqualTo(expectedRecords.WrittenCount);
        await Assert.That(recordsBytes).IsEquivalentTo(expectedRecords.WrittenSpan.ToArray());
        await Assert.That(taggedFields).IsEqualTo(0);
        await Assert.That(readerEnd).IsTrue();
        await Assert.That(writerBytesWritten).IsEqualTo(bufferBytesWritten);
    }

    [Test]
    public async Task PartitionData_Write_WithCachedBodySize_EncodesRecordBatchesWithoutChangingBytes()
    {
        var batch = CreateBatchWithCachedBodySize(baseOffset: 0, offsetDelta: 0, value: "cached-value"u8.ToArray());
        var expectedRecords = new ArrayBufferWriter<byte>();
        batch.Write(expectedRecords);

        var partitionData = new ProduceRequestPartitionData
        {
            Index = 0,
            Records = [batch]
        };

        int index;
        int recordsLength;
        byte[] recordsBytes;
        int taggedFields;
        bool readerEnd;
        int writerBytesWritten;
        int bufferBytesWritten;
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new KafkaProtocolWriter(buffer);
            partitionData.Write(ref writer, ProduceRequest.HighestSupportedVersion);

            var reader = new KafkaProtocolReader(buffer.WrittenMemory);
            index = reader.ReadInt32();
            recordsLength = reader.ReadUnsignedVarInt() - 1;
            recordsBytes = reader.ReadRawBytes(recordsLength);
            taggedFields = reader.ReadUnsignedVarInt();
            readerEnd = reader.End;
            writerBytesWritten = writer.BytesWritten;
            bufferBytesWritten = buffer.WrittenCount;
        }

        await Assert.That(index).IsEqualTo(0);
        await Assert.That(recordsLength).IsEqualTo(expectedRecords.WrittenCount);
        await Assert.That(recordsBytes).IsEquivalentTo(expectedRecords.WrittenSpan.ToArray());
        await Assert.That(taggedFields).IsEqualTo(0);
        await Assert.That(readerEnd).IsTrue();
        await Assert.That(writerBytesWritten).IsEqualTo(bufferBytesWritten);
    }

    private static RecordBatch CreateBatch(long baseOffset, int offsetDelta, byte[] value)
    {
        return new RecordBatch
        {
            BaseOffset = baseOffset,
            BaseTimestamp = 1000 + offsetDelta,
            MaxTimestamp = 1000 + offsetDelta,
            LastOffsetDelta = offsetDelta,
            ProducerId = -1,
            ProducerEpoch = -1,
            BaseSequence = -1,
            Records =
            [
                new Record
                {
                    OffsetDelta = offsetDelta,
                    TimestampDelta = offsetDelta,
                    Key = "key"u8.ToArray(),
                    Value = value
                }
            ]
        };
    }

    private static RecordBatch CreateBatchWithCachedBodySize(long baseOffset, int offsetDelta, byte[] value)
    {
        var key = "cached-key"u8.ToArray();
        var headers = new[] { new Header("trace-id", "abc123"u8.ToArray()) };
        var cachedBodySize = Record.ComputeBodySize(
            offsetDelta,
            offsetDelta,
            isKeyNull: false,
            key.Length,
            isValueNull: false,
            value.Length,
            headers,
            headers.Length);

        return new RecordBatch
        {
            BaseOffset = baseOffset,
            BaseTimestamp = 1000 + offsetDelta,
            MaxTimestamp = 1000 + offsetDelta,
            LastOffsetDelta = offsetDelta,
            ProducerId = -1,
            ProducerEpoch = -1,
            BaseSequence = -1,
            Records =
            [
                new Record
                {
                    OffsetDelta = offsetDelta,
                    TimestampDelta = offsetDelta,
                    Key = key,
                    Value = value,
                    Headers = headers,
                    HeaderCount = headers.Length,
                    CachedBodySize = cachedBodySize
                }
            ]
        };
    }
}
