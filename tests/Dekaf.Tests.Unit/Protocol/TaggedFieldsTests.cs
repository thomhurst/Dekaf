using System.Buffers;
using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Tests for Kafka protocol tagged fields encoding.
/// Reference: https://kafka.apache.org/protocol#protocol_types
///
/// TAG_BUFFER format:
/// - UNSIGNED_VARINT: number of tagged fields
/// - For each tagged field:
///   - UNSIGNED_VARINT: tag number
///   - UNSIGNED_VARINT: data size in bytes
///   - BYTES: raw data
/// </summary>
public class TaggedFieldsTests
{
    #region Empty Tagged Fields Tests

    [Test]
    public async Task EmptyTaggedFields_EncodedAsZero()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteEmptyTaggedFields();

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { 0x00 });
    }

    [Test]
    public async Task EmptyTaggedFields_CanBeSkipped()
    {
        var data = new byte[] { 0x00 };
        var reader = new KafkaProtocolReader(data);
        reader.SkipTaggedFields();
        var isEnd = reader.End;

        await Assert.That(isEnd).IsTrue();
    }

    #endregion

    #region Skip Tagged Fields Tests

    [Test]
    public async Task SkipTaggedFields_SingleField()
    {
        // One tagged field with tag=0, size=2, data=[0x01, 0x02]
        var data = new byte[]
        {
            0x01,       // 1 field
            0x00,       // tag = 0
            0x02,       // size = 2
            0x01, 0x02  // data
        };
        var reader = new KafkaProtocolReader(data);
        reader.SkipTaggedFields();
        var isEnd = reader.End;

        await Assert.That(isEnd).IsTrue();
    }

    [Test]
    public async Task SkipTaggedFields_MultipleFields()
    {
        // Two tagged fields
        var data = new byte[]
        {
            0x02,             // 2 fields
            0x00,             // tag = 0
            0x02,             // size = 2
            0xAA, 0xBB,       // data
            0x01,             // tag = 1
            0x03,             // size = 3
            0xCC, 0xDD, 0xEE  // data
        };
        var reader = new KafkaProtocolReader(data);
        reader.SkipTaggedFields();
        var isEnd = reader.End;

        await Assert.That(isEnd).IsTrue();
    }

    [Test]
    public async Task SkipTaggedFields_LargeFieldSize()
    {
        // One tagged field with size > 127 (requires multi-byte varint)
        var largeData = new byte[200];
        Array.Fill(largeData, (byte)0x42);

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        // Build the tagged field structure manually
        writer.WriteUnsignedVarInt(1);    // 1 field
        writer.WriteUnsignedVarInt(0);    // tag = 0
        writer.WriteUnsignedVarInt(200);  // size = 200
        writer.WriteRawBytes(largeData);  // data

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        reader.SkipTaggedFields();
        var isEnd = reader.End;

        await Assert.That(isEnd).IsTrue();
    }

    [Test]
    public async Task SkipTaggedFields_PreservesRemainingData()
    {
        // Tagged fields followed by more data
        var data = new byte[]
        {
            0x01,        // 1 field
            0x00,        // tag = 0
            0x01,        // size = 1
            0xFF,        // data
            0xDE, 0xAD   // remaining data (should not be consumed)
        };
        var reader = new KafkaProtocolReader(data);
        reader.SkipTaggedFields();
        var remaining = reader.Remaining;
        var byte1 = reader.ReadUInt8();
        var byte2 = reader.ReadUInt8();

        await Assert.That(remaining).IsEqualTo(2);
        await Assert.That(byte1).IsEqualTo((byte)0xDE);
        await Assert.That(byte2).IsEqualTo((byte)0xAD);
    }

    #endregion

    #region Tagged Fields in Context Tests

    [Test]
    public async Task TaggedFields_InResponseHeader_V1()
    {
        // Simulating a v1 response header with tagged fields
        var data = new byte[]
        {
            0x00, 0x00, 0x00, 0x2A, // CorrelationId = 42
            0x00                     // Empty tagged fields
        };
        var reader = new KafkaProtocolReader(data);
        var header = ResponseHeader.Read(ref reader, headerVersion: 1);
        var isEnd = reader.End;

        await Assert.That(header.CorrelationId).IsEqualTo(42);
        await Assert.That(isEnd).IsTrue();
    }

    [Test]
    public async Task TaggedFields_InResponseHeader_V1_WithUnknownTags()
    {
        // v1 response header with unknown tagged fields that should be skipped
        var data = new byte[]
        {
            0x00, 0x00, 0x00, 0x2A, // CorrelationId = 42
            0x02,                   // 2 tagged fields
            0x05,                   // tag = 5 (unknown)
            0x02,                   // size = 2
            0xAA, 0xBB,             // data
            0x0A,                   // tag = 10 (unknown)
            0x01,                   // size = 1
            0xFF                    // data
        };
        var reader = new KafkaProtocolReader(data);
        var header = ResponseHeader.Read(ref reader, headerVersion: 1);
        var isEnd = reader.End;

        await Assert.That(header.CorrelationId).IsEqualTo(42);
        await Assert.That(isEnd).IsTrue();
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task SkipTaggedFields_ZeroSizeField()
    {
        // Tagged field with 0-byte data
        var data = new byte[]
        {
            0x01, // 1 field
            0x00, // tag = 0
            0x00  // size = 0 (no data)
        };
        var reader = new KafkaProtocolReader(data);
        reader.SkipTaggedFields();
        var isEnd = reader.End;

        await Assert.That(isEnd).IsTrue();
    }

    [Test]
    public async Task SkipTaggedFields_LargeTagNumber()
    {
        // Tagged field with large tag number (multi-byte varint)
        var data = new byte[]
        {
            0x01,       // 1 field
            0x80, 0x01, // tag = 128 (multi-byte varint)
            0x01,       // size = 1
            0xAB        // data
        };
        var reader = new KafkaProtocolReader(data);
        reader.SkipTaggedFields();
        var isEnd = reader.End;

        await Assert.That(isEnd).IsTrue();
    }

    #endregion
}
