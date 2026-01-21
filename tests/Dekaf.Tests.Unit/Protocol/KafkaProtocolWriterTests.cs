using System.Buffers;
using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Protocol;

public class KafkaProtocolWriterTests
{
    [Test]
    public async Task WriteInt16_WritesBigEndian()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt16(0x0102);

        var result = buffer.WrittenSpan.ToArray();
        await Assert.That(result).IsEquivalentTo(new byte[] { 0x01, 0x02 });
    }

    [Test]
    public async Task WriteInt32_WritesBigEndian()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0x01020304);

        var result = buffer.WrittenSpan.ToArray();
        await Assert.That(result).IsEquivalentTo(new byte[] { 0x01, 0x02, 0x03, 0x04 });
    }

    [Test]
    public async Task WriteInt64_WritesBigEndian()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt64(0x0102030405060708);

        var result = buffer.WrittenSpan.ToArray();
        await Assert.That(result).IsEquivalentTo(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 });
    }

    [Test]
    public async Task WriteVarInt_WritesZero()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteVarInt(0);

        var result = buffer.WrittenSpan.ToArray();
        await Assert.That(result).IsEquivalentTo(new byte[] { 0x00 });
    }

    [Test]
    public async Task WriteVarInt_WritesPositiveNumber()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteVarInt(1);

        var result = buffer.WrittenSpan.ToArray();
        await Assert.That(result).IsEquivalentTo(new byte[] { 0x02 }); // ZigZag encoded
    }

    [Test]
    public async Task WriteVarInt_WritesNegativeNumber()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteVarInt(-1);

        var result = buffer.WrittenSpan.ToArray();
        await Assert.That(result).IsEquivalentTo(new byte[] { 0x01 }); // ZigZag encoded
    }

    [Test]
    public async Task WriteString_WritesNullString()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteString(null);

        var result = buffer.WrittenSpan.ToArray();
        await Assert.That(result).IsEquivalentTo(new byte[] { 0xFF, 0xFF }); // -1 as int16
    }

    [Test]
    public async Task WriteString_WritesEmptyString()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteString(string.Empty);

        var result = buffer.WrittenSpan.ToArray();
        await Assert.That(result).IsEquivalentTo(new byte[] { 0x00, 0x00 }); // 0 length
    }

    [Test]
    public async Task WriteString_WritesUtf8String()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteString("test");

        var result = buffer.WrittenSpan.ToArray();
        await Assert.That(result).IsEquivalentTo(new byte[] { 0x00, 0x04, (byte)'t', (byte)'e', (byte)'s', (byte)'t' });
    }

    [Test]
    public async Task WriteCompactString_WritesUtf8String()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteCompactString("test");

        var result = buffer.WrittenSpan.ToArray();
        // Length + 1 = 5, encoded as varint
        await Assert.That(result).IsEquivalentTo(new byte[] { 0x05, (byte)'t', (byte)'e', (byte)'s', (byte)'t' });
    }

    [Test]
    public async Task WriteBoolean_WritesTrue()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteBoolean(true);

        var result = buffer.WrittenSpan.ToArray();
        await Assert.That(result).IsEquivalentTo(new byte[] { 0x01 });
    }

    [Test]
    public async Task WriteBoolean_WritesFalse()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteBoolean(false);

        var result = buffer.WrittenSpan.ToArray();
        await Assert.That(result).IsEquivalentTo(new byte[] { 0x00 });
    }

    [Test]
    public async Task BytesWritten_TracksWrittenBytes()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(42);
        writer.WriteInt16(1);

        await Assert.That(writer.BytesWritten).IsEqualTo(6);
    }
}
