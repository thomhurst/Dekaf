using System.Buffers;
using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Tests for Kafka protocol string and bytes encoding.
/// Reference: https://kafka.apache.org/protocol#protocol_types
///
/// STRING: INT16 length prefix + UTF-8 bytes
/// NULLABLE_STRING: -1 length = null
/// COMPACT_STRING: UNSIGNED_VARINT (length + 1) prefix + UTF-8 bytes
/// COMPACT_NULLABLE_STRING: 0 = null, otherwise length + 1
///
/// BYTES: INT32 length prefix + raw bytes
/// NULLABLE_BYTES: -1 length = null
/// COMPACT_BYTES: UNSIGNED_VARINT (length + 1) prefix + raw bytes
/// COMPACT_NULLABLE_BYTES: 0 = null, otherwise length + 1
/// </summary>
public class StringBytesEncodingTests
{
    #region STRING (Legacy) Tests

    [Test]
    public async Task String_Empty_EncodedWithZeroLength()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteString(string.Empty);

        // INT16 length = 0
        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { 0x00, 0x00 });
    }

    [Test]
    public async Task String_SimpleAscii_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteString("test");

        // INT16 length = 4, then "test"
        await Assert.That(buffer.WrittenSpan.ToArray())
            .IsEquivalentTo(new byte[] { 0x00, 0x04, (byte)'t', (byte)'e', (byte)'s', (byte)'t' });
    }

    [Test]
    public async Task String_Utf8_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteString("日本語"); // 9 bytes in UTF-8

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        await Assert.That(reader.ReadString()).IsEqualTo("日本語");
    }

    [Test]
    public async Task NullableString_Null_EncodedAsMinusOne()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteString(null);

        // INT16 length = -1 (0xFFFF)
        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { 0xFF, 0xFF });
    }

    [Test]
    public async Task NullableString_Null_RoundTrip()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteString(null);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        await Assert.That(reader.ReadString()).IsNull();
    }

    #endregion

    #region COMPACT_STRING Tests

    [Test]
    public async Task CompactString_Empty_EncodedWithLengthPlusOne()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteCompactString(string.Empty);

        // length + 1 = 1 (0x01)
        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { 0x01 });
    }

    [Test]
    public async Task CompactString_SimpleAscii_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteCompactString("test");

        // length + 1 = 5 (0x05), then "test"
        await Assert.That(buffer.WrittenSpan.ToArray())
            .IsEquivalentTo(new byte[] { 0x05, (byte)'t', (byte)'e', (byte)'s', (byte)'t' });
    }

    [Test]
    public async Task CompactNullableString_Null_EncodedAsZero()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteCompactString(null);

        // 0 = null
        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { 0x00 });
    }

    [Test]
    public async Task CompactString_Null_RoundTrip()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteCompactString(null);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        await Assert.That(reader.ReadCompactString()).IsNull();
    }

    [Test]
    public async Task CompactString_LongString_MultiByteLength()
    {
        // String longer than 126 chars requires multi-byte length encoding
        var longString = new string('a', 200);
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteCompactString(longString);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        await Assert.That(reader.ReadCompactString()).IsEqualTo(longString);
    }

    #endregion

    #region BYTES (Legacy) Tests

    [Test]
    public async Task Bytes_Empty_EncodedWithZeroLength()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteBytes([]);

        // INT32 length = 0
        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { 0x00, 0x00, 0x00, 0x00 });
    }

    [Test]
    public async Task Bytes_Data_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteBytes(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

        // INT32 length = 4, then data
        await Assert.That(buffer.WrittenSpan.ToArray())
            .IsEquivalentTo(new byte[] { 0x00, 0x00, 0x00, 0x04, 0xDE, 0xAD, 0xBE, 0xEF });
    }

    [Test]
    public async Task NullableBytes_Null_EncodedAsMinusOne()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteNullableBytes([], isNull: true);

        // INT32 length = -1
        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
    }

    [Test]
    public async Task NullableBytes_Null_RoundTrip()
    {
        var data = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
        var reader = new KafkaProtocolReader(data);
        await Assert.That(reader.ReadBytes()).IsNull();
    }

    [Test]
    public async Task Bytes_RoundTrip()
    {
        var originalData = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteBytes(originalData);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        await Assert.That(reader.ReadBytes()).IsEquivalentTo(originalData);
    }

    #endregion

    #region COMPACT_BYTES Tests

    [Test]
    public async Task CompactBytes_Empty_EncodedWithLengthPlusOne()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteCompactBytes([]);

        // length + 1 = 1
        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { 0x01 });
    }

    [Test]
    public async Task CompactBytes_Data_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteCompactBytes(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

        // length + 1 = 5 (0x05), then data
        await Assert.That(buffer.WrittenSpan.ToArray())
            .IsEquivalentTo(new byte[] { 0x05, 0xDE, 0xAD, 0xBE, 0xEF });
    }

    [Test]
    public async Task CompactNullableBytes_Null_EncodedAsZero()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteCompactNullableBytes([], isNull: true);

        // 0 = null
        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { 0x00 });
    }

    [Test]
    public async Task CompactBytes_Null_RoundTrip()
    {
        var data = new byte[] { 0x00 };
        var reader = new KafkaProtocolReader(data);
        await Assert.That(reader.ReadCompactBytes()).IsNull();
    }

    [Test]
    public async Task CompactBytes_RoundTrip()
    {
        var originalData = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteCompactBytes(originalData);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        await Assert.That(reader.ReadCompactBytes()).IsEquivalentTo(originalData);
    }

    #endregion

    #region Raw Bytes Tests

    [Test]
    public async Task RawBytes_NoLengthPrefix()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteRawBytes(new byte[] { 0x01, 0x02, 0x03 });

        // No length prefix, just raw bytes
        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { 0x01, 0x02, 0x03 });
    }

    [Test]
    public async Task RawBytes_Empty_WritesNothing()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteRawBytes([]);

        await Assert.That(buffer.WrittenCount).IsEqualTo(0);
    }

    #endregion
}
