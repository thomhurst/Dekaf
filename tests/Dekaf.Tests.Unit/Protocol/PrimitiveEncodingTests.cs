using System.Buffers;
using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Tests for Kafka protocol primitive type encoding/decoding.
/// Reference: https://kafka.apache.org/protocol#protocol_types
/// </summary>
public class PrimitiveEncodingTests
{
    #region INT8 Tests

    [Test]
    public async Task Int8_RoundTrip_Zero()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt8(0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        await Assert.That(reader.ReadInt8()).IsEqualTo((sbyte)0);
    }

    [Test]
    public async Task Int8_RoundTrip_MaxValue()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt8(sbyte.MaxValue);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        await Assert.That(reader.ReadInt8()).IsEqualTo(sbyte.MaxValue);
    }

    [Test]
    public async Task Int8_RoundTrip_MinValue()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt8(sbyte.MinValue);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        await Assert.That(reader.ReadInt8()).IsEqualTo(sbyte.MinValue);
    }

    #endregion

    #region INT16 Tests

    [Test]
    public async Task Int16_BigEndian_KnownValue()
    {
        // 0x0102 = 258 in big-endian should be [0x01, 0x02]
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt16(0x0102);

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { 0x01, 0x02 });
    }

    [Test]
    public async Task Int16_RoundTrip_NegativeValue()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt16(-1);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        await Assert.That(reader.ReadInt16()).IsEqualTo((short)-1);
        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { 0xFF, 0xFF });
    }

    #endregion

    #region INT32 Tests

    [Test]
    public async Task Int32_BigEndian_KnownValue()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0x01020304);

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { 0x01, 0x02, 0x03, 0x04 });
    }

    [Test]
    public async Task Int32_RoundTrip_NegativeOne()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(-1);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        await Assert.That(reader.ReadInt32()).IsEqualTo(-1);
        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
    }

    #endregion

    #region INT64 Tests

    [Test]
    public async Task Int64_BigEndian_KnownValue()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt64(0x0102030405060708);

        await Assert.That(buffer.WrittenSpan.ToArray())
            .IsEquivalentTo(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 });
    }

    [Test]
    public async Task Int64_RoundTrip_MaxValue()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt64(long.MaxValue);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        await Assert.That(reader.ReadInt64()).IsEqualTo(long.MaxValue);
    }

    #endregion

    #region UINT16 Tests

    [Test]
    public async Task UInt16_BigEndian_KnownValue()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUInt16(0xABCD);

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { 0xAB, 0xCD });
    }

    [Test]
    public async Task UInt16_RoundTrip_MaxValue()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUInt16(ushort.MaxValue);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        await Assert.That(reader.ReadUInt16()).IsEqualTo(ushort.MaxValue);
    }

    #endregion

    #region UINT32 Tests

    [Test]
    public async Task UInt32_BigEndian_KnownValue()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUInt32(0xDEADBEEF);

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });
    }

    [Test]
    public async Task UInt32_RoundTrip_MaxValue()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUInt32(uint.MaxValue);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        await Assert.That(reader.ReadUInt32()).IsEqualTo(uint.MaxValue);
    }

    #endregion

    #region FLOAT64 Tests

    [Test]
    public async Task Float64_RoundTrip_Pi()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteFloat64(Math.PI);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        await Assert.That(reader.ReadFloat64()).IsEqualTo(Math.PI);
    }

    [Test]
    public async Task Float64_RoundTrip_NegativeInfinity()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteFloat64(double.NegativeInfinity);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        await Assert.That(reader.ReadFloat64()).IsEqualTo(double.NegativeInfinity);
    }

    #endregion

    #region UUID Tests

    [Test]
    public async Task Uuid_RoundTrip_KnownValue()
    {
        var uuid = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUuid(uuid);

        await Assert.That(buffer.WrittenSpan.Length).IsEqualTo(16);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        await Assert.That(reader.ReadUuid()).IsEqualTo(uuid);
    }

    [Test]
    public async Task Uuid_RoundTrip_Empty()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUuid(Guid.Empty);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        await Assert.That(reader.ReadUuid()).IsEqualTo(Guid.Empty);
    }

    #endregion

    #region BOOLEAN Tests

    [Test]
    public async Task Boolean_True_EncodedAsOne()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteBoolean(true);

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { 0x01 });
    }

    [Test]
    public async Task Boolean_False_EncodedAsZero()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteBoolean(false);

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { 0x00 });
    }

    [Test]
    public async Task Boolean_NonZero_ReadAsTrue()
    {
        // Per Kafka spec, any non-zero value should be read as true
        var reader = new KafkaProtocolReader(new byte[] { 0x42 });
        await Assert.That(reader.ReadBoolean()).IsTrue();
    }

    #endregion
}
