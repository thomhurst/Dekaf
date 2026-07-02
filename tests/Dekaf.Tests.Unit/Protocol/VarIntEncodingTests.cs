using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Tests for Kafka protocol variable-length integer encoding.
/// Reference: https://kafka.apache.org/protocol#protocol_types
///
/// VARINT/VARLONG use ZigZag encoding:
/// - Positive n encoded as 2*n
/// - Negative n encoded as 2*|n| - 1
///
/// UNSIGNED_VARINT is plain unsigned varint (no ZigZag).
/// </summary>
public class VarIntEncodingTests
{
    #region VARINT (ZigZag) Tests

    [Test]
    [Arguments(0, new byte[] { 0x00 })]
    [Arguments(1, new byte[] { 0x02 })]
    [Arguments(-1, new byte[] { 0x01 })]
    [Arguments(2, new byte[] { 0x04 })]
    [Arguments(-2, new byte[] { 0x03 })]
    [Arguments(64, new byte[] { 0x80, 0x01 })]
    [Arguments(-64, new byte[] { 0x7F })]
    [Arguments(300, new byte[] { 0xD8, 0x04 })]
    public async Task VarInt_ZigZagEncoding_KnownValues(int value, byte[] expectedBytes)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteVarInt(value);

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expectedBytes);
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(-1)]
    [Arguments(127)]
    [Arguments(-128)]
    [Arguments(8192)]      // ZigZag → 16384 = first 3-byte varint (exercises 2→3 byte boundary)
    [Arguments(-8193)]     // ZigZag → 16385
    [Arguments(16383)]
    [Arguments(-16384)]
    [Arguments(int.MaxValue)]
    [Arguments(int.MinValue)]
    public async Task VarInt_RoundTrip(int value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteVarInt(value);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        await Assert.That(reader.ReadVarInt()).IsEqualTo(value);
    }

    #endregion

    #region VARLONG (ZigZag) Tests

    [Test]
    [Arguments(0L, new byte[] { 0x00 })]
    [Arguments(1L, new byte[] { 0x02 })]
    [Arguments(-1L, new byte[] { 0x01 })]
    [Arguments(64L, new byte[] { 0x80, 0x01 })]
    [Arguments(-64L, new byte[] { 0x7F })]
    public async Task VarLong_ZigZagEncoding_KnownValues(long value, byte[] expectedBytes)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteVarLong(value);

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expectedBytes);
    }

    [Test]
    [Arguments(0L)]
    [Arguments(1L)]
    [Arguments(-1L)]
    [Arguments(8192L)]     // ZigZag → 16384 = first 3-byte varint (exercises 2→3 byte boundary)
    [Arguments(-8193L)]    // ZigZag → 16385 (just past boundary)
    [Arguments(1048575L)]  // ZigZag → 2097150 = last 3-byte varint
    [Arguments(long.MaxValue)]
    [Arguments(long.MinValue)]
    public async Task VarLong_RoundTrip(long value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteVarLong(value);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        await Assert.That(reader.ReadVarLong()).IsEqualTo(value);
    }

    [Test]
    [Arguments(0L, 1)]
    [Arguments(-64L, 1)]
    [Arguments(64L, 2)]
    [Arguments(-8192L, 2)]
    [Arguments(8192L, 3)]
    [Arguments(long.MaxValue, 10)]
    [Arguments(long.MinValue, 10)]
    public async Task VarLong_ByteLength_Boundaries(long value, int expectedLength)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteVarLong(value);

        await Assert.That(buffer.WrittenCount).IsEqualTo(expectedLength);
    }

    #endregion

    #region UNSIGNED_VARINT Tests

    [Test]
    [Arguments(0, new byte[] { 0x00 })]
    [Arguments(1, new byte[] { 0x01 })]
    [Arguments(127, new byte[] { 0x7F })]
    [Arguments(128, new byte[] { 0x80, 0x01 })]
    [Arguments(255, new byte[] { 0xFF, 0x01 })]
    [Arguments(300, new byte[] { 0xAC, 0x02 })]
    [Arguments(16384, new byte[] { 0x80, 0x80, 0x01 })]
    public async Task UnsignedVarInt_Encoding_KnownValues(int value, byte[] expectedBytes)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUnsignedVarInt(value);

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expectedBytes);
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(127)]
    [Arguments(128)]
    [Arguments(16383)]
    [Arguments(16384)]
    [Arguments(int.MaxValue)]
    public async Task UnsignedVarInt_RoundTrip(int value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUnsignedVarInt(value);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        await Assert.That(reader.ReadUnsignedVarInt()).IsEqualTo(value);
    }

    #endregion

    #region Byte Length Tests

    [Test]
    public async Task VarInt_SingleByte_Range()
    {
        // Single byte varints can encode values with ZigZag: -64 to 63
        for (var i = -64; i <= 63; i++)
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new KafkaProtocolWriter(buffer);
            writer.WriteVarInt(i);
            await Assert.That(buffer.WrittenCount).IsEqualTo(1);
        }
    }

    [Test]
    public async Task UnsignedVarInt_SingleByte_Range()
    {
        // Single byte unsigned varints: 0 to 127
        for (var i = 0; i <= 127; i++)
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new KafkaProtocolWriter(buffer);
            writer.WriteUnsignedVarInt(i);
            await Assert.That(buffer.WrittenCount).IsEqualTo(1);
        }
    }

    [Test]
    public async Task UnsignedVarInt_TwoBytes_StartsAt128()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUnsignedVarInt(128);
        await Assert.That(buffer.WrittenCount).IsEqualTo(2);
    }

    [Test]
    [Arguments(0, 1)]
    [Arguments(127, 1)]
    [Arguments(128, 2)]
    [Arguments(16383, 2)]
    [Arguments(16384, 3)]
    [Arguments(2097151, 3)]
    [Arguments(2097152, 4)]
    [Arguments(int.MaxValue, 5)]
    public async Task UnsignedVarInt_ByteLength_Boundaries(int value, int expectedLength)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteUnsignedVarInt(value);

        await Assert.That(buffer.WrittenCount).IsEqualTo(expectedLength);
    }

    [Test]
    [Arguments(0u, 1)]
    [Arguments(127u, 1)]
    [Arguments(128u, 2)]
    [Arguments(16383u, 2)]
    [Arguments(16384u, 3)]
    [Arguments(2097151u, 3)]
    [Arguments(2097152u, 4)]
    [Arguments(268435455u, 4)]
    [Arguments(268435456u, 5)]
    [Arguments(uint.MaxValue, 5)]
    public async Task VarUIntSize_Boundaries(uint value, int expectedLength)
    {
        await Assert.That(Record.VarUIntSize(value)).IsEqualTo(expectedLength);
    }

    [Test]
    [Arguments(0ul, 1)]
    [Arguments(127ul, 1)]
    [Arguments(128ul, 2)]
    [Arguments(16383ul, 2)]
    [Arguments(16384ul, 3)]
    [Arguments(2097151ul, 3)]
    [Arguments(2097152ul, 4)]
    [Arguments(268435455ul, 4)]
    [Arguments(268435456ul, 5)]
    [Arguments(34359738367ul, 5)]
    [Arguments(34359738368ul, 6)]
    [Arguments(4398046511103ul, 6)]
    [Arguments(4398046511104ul, 7)]
    [Arguments(562949953421311ul, 7)]
    [Arguments(562949953421312ul, 8)]
    [Arguments(72057594037927935ul, 8)]
    [Arguments(72057594037927936ul, 9)]
    [Arguments(9223372036854775807ul, 9)]
    [Arguments(9223372036854775808ul, 10)]
    [Arguments(ulong.MaxValue, 10)]
    public async Task VarULongSize_Boundaries(ulong value, int expectedLength)
    {
        await Assert.That(Record.VarULongSize(value)).IsEqualTo(expectedLength);
    }

    #endregion
}
