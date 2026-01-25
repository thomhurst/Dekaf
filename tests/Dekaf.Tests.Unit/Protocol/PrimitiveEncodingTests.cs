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

    #region Multi-Segment Sequence Tests (Slow Path)

    /// <summary>
    /// Creates a multi-segment ReadOnlySequence by splitting data at the specified position.
    /// This forces the reader to use the slow path that handles data spanning multiple segments.
    /// </summary>
    private static ReadOnlySequence<byte> CreateMultiSegmentSequence(byte[] data, int splitAt)
    {
        if (splitAt <= 0 || splitAt >= data.Length)
            throw new ArgumentException("Split position must be within data bounds", nameof(splitAt));

        var segment1 = new TestSegment(data.AsMemory(0, splitAt));
        var segment2 = new TestSegment(data.AsMemory(splitAt));
        segment1.SetNext(segment2);

        return new ReadOnlySequence<byte>(segment1, 0, segment2, segment2.Memory.Length);
    }

    /// <summary>
    /// Helper class to create a linked list of memory segments for multi-segment sequences.
    /// </summary>
    private sealed class TestSegment : ReadOnlySequenceSegment<byte>
    {
        public TestSegment(ReadOnlyMemory<byte> memory)
        {
            Memory = memory;
        }

        public void SetNext(TestSegment next)
        {
            Next = next;
            next.RunningIndex = RunningIndex + Memory.Length;
        }
    }

    [Test]
    public async Task Int16_MultiSegment_ReadsCorrectly()
    {
        // Create data for Int16 (2 bytes) split across segments
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt16(0x1234);

        // Split after first byte to force slow path
        var sequence = CreateMultiSegmentSequence(buffer.WrittenSpan.ToArray(), 1);
        var reader = new KafkaProtocolReader(sequence);

        await Assert.That(reader.ReadInt16()).IsEqualTo((short)0x1234);
    }

    [Test]
    public async Task Int32_MultiSegment_ReadsCorrectly()
    {
        // Create data for Int32 (4 bytes) split across segments
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0x12345678);

        // Split in the middle to force slow path
        var sequence = CreateMultiSegmentSequence(buffer.WrittenSpan.ToArray(), 2);
        var reader = new KafkaProtocolReader(sequence);

        await Assert.That(reader.ReadInt32()).IsEqualTo(0x12345678);
    }

    [Test]
    public async Task Int64_MultiSegment_ReadsCorrectly()
    {
        // Create data for Int64 (8 bytes) split across segments
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt64(0x123456789ABCDEF0);

        // Split in the middle to force slow path
        var sequence = CreateMultiSegmentSequence(buffer.WrittenSpan.ToArray(), 4);
        var reader = new KafkaProtocolReader(sequence);

        await Assert.That(reader.ReadInt64()).IsEqualTo(0x123456789ABCDEF0);
    }

    [Test]
    public async Task UInt16_MultiSegment_ReadsCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUInt16(0xABCD);

        var sequence = CreateMultiSegmentSequence(buffer.WrittenSpan.ToArray(), 1);
        var reader = new KafkaProtocolReader(sequence);

        await Assert.That(reader.ReadUInt16()).IsEqualTo((ushort)0xABCD);
    }

    [Test]
    public async Task UInt32_MultiSegment_ReadsCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUInt32(0xDEADBEEF);

        var sequence = CreateMultiSegmentSequence(buffer.WrittenSpan.ToArray(), 2);
        var reader = new KafkaProtocolReader(sequence);

        await Assert.That(reader.ReadUInt32()).IsEqualTo(0xDEADBEEF);
    }

    [Test]
    public async Task Float64_MultiSegment_ReadsCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteFloat64(Math.E);

        var sequence = CreateMultiSegmentSequence(buffer.WrittenSpan.ToArray(), 3);
        var reader = new KafkaProtocolReader(sequence);

        await Assert.That(reader.ReadFloat64()).IsEqualTo(Math.E);
    }

    [Test]
    public async Task Uuid_MultiSegment_ReadsCorrectly()
    {
        var uuid = Guid.Parse("12345678-1234-1234-1234-123456789ABC");
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUuid(uuid);

        // Split in the middle of the 16-byte UUID
        var sequence = CreateMultiSegmentSequence(buffer.WrittenSpan.ToArray(), 8);
        var reader = new KafkaProtocolReader(sequence);

        await Assert.That(reader.ReadUuid()).IsEqualTo(uuid);
    }

    [Test]
    public async Task MultipleReads_MultiSegment_WorkCorrectly()
    {
        // Create a buffer with multiple values that will span segments
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt16(0x1234);
        writer.WriteInt32(0x56789ABC);
        writer.WriteInt64(0x0102030405060708);

        // Split at position 3 (middle of the Int32)
        var sequence = CreateMultiSegmentSequence(buffer.WrittenSpan.ToArray(), 3);
        var reader = new KafkaProtocolReader(sequence);

        // Read all values first (ref struct cannot be preserved across await)
        var int16Value = reader.ReadInt16();
        var int32Value = reader.ReadInt32();
        var int64Value = reader.ReadInt64();

        await Assert.That(int16Value).IsEqualTo((short)0x1234);
        await Assert.That(int32Value).IsEqualTo(0x56789ABC);
        await Assert.That(int64Value).IsEqualTo(0x0102030405060708);
    }

    #endregion
}
