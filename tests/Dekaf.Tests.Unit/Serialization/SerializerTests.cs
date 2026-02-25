using System.Buffers;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Serialization;

public class SerializerTests
{
    private static SerializationContext CreateContext(string topic = "test") =>
        new() { Topic = topic, Component = SerializationComponent.Value };

    [Test]
    public async Task StringSerializer_SerializesAndDeserializes()
    {
        var serializer = Serializers.String;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serializer.Serialize("hello world", ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo("hello world");
    }

    [Test]
    public async Task Int32Serializer_SerializesAndDeserializes()
    {
        var serializer = Serializers.Int32;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serializer.Serialize(42, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task Int64Serializer_SerializesAndDeserializes()
    {
        var serializer = Serializers.Int64;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serializer.Serialize(9876543210L, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(9876543210L);
    }

    [Test]
    public async Task GuidSerializer_SerializesAndDeserializes()
    {
        var serializer = Serializers.Guid;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var guid = Guid.NewGuid();

        serializer.Serialize(guid, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(guid);
    }

    [Test]
    public async Task ByteArraySerializer_SerializesAndDeserializes()
    {
        var serializer = Serializers.ByteArray;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var data = new byte[] { 1, 2, 3, 4, 5 };

        serializer.Serialize(data, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEquivalentTo(data);
    }

    [Test]
    public async Task DoubleSerializer_SerializesAndDeserializes()
    {
        var serializer = Serializers.Double;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serializer.Serialize(3.14159, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(3.14159);
    }

    #region Float Serializer Tests

    [Test]
    public async Task FloatSerializer_RoundTrip_Zero()
    {
        var serializer = Serializers.Float;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serializer.Serialize(0f, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(0f);
    }

    [Test]
    public async Task FloatSerializer_RoundTrip_PositiveValue()
    {
        var serializer = Serializers.Float;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serializer.Serialize(1.5f, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(1.5f);
    }

    [Test]
    public async Task FloatSerializer_RoundTrip_NegativeValue()
    {
        var serializer = Serializers.Float;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serializer.Serialize(-1.5f, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(-1.5f);
    }

    [Test]
    public async Task FloatSerializer_RoundTrip_MinValue()
    {
        var serializer = Serializers.Float;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serializer.Serialize(float.MinValue, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(float.MinValue);
    }

    [Test]
    public async Task FloatSerializer_RoundTrip_MaxValue()
    {
        var serializer = Serializers.Float;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serializer.Serialize(float.MaxValue, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(float.MaxValue);
    }

    [Test]
    public async Task FloatSerializer_RoundTrip_NaN()
    {
        var serializer = Serializers.Float;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serializer.Serialize(float.NaN, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(float.IsNaN(result)).IsTrue();
    }

    [Test]
    public async Task FloatSerializer_RoundTrip_PositiveInfinity()
    {
        var serializer = Serializers.Float;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serializer.Serialize(float.PositiveInfinity, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(float.PositiveInfinity);
    }

    [Test]
    public async Task FloatSerializer_RoundTrip_NegativeInfinity()
    {
        var serializer = Serializers.Float;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serializer.Serialize(float.NegativeInfinity, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(float.NegativeInfinity);
    }

    #endregion

    #region DateTime Serializer Tests

    [Test]
    public async Task DateTimeSerializer_RoundTrip_MinValue()
    {
        var serializer = Serializers.DateTime;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var value = DateTime.MinValue.ToUniversalTime();

        serializer.Serialize(value, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(value);
        await Assert.That(result.Kind).IsEqualTo(DateTimeKind.Utc);
    }

    [Test]
    public async Task DateTimeSerializer_RoundTrip_MaxValue()
    {
        var serializer = Serializers.DateTime;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var value = DateTime.MaxValue.ToUniversalTime();

        serializer.Serialize(value, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(value);
        await Assert.That(result.Kind).IsEqualTo(DateTimeKind.Utc);
    }

    [Test]
    public async Task DateTimeSerializer_RoundTrip_UtcNow()
    {
        var serializer = Serializers.DateTime;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var value = DateTime.UtcNow;

        serializer.Serialize(value, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(value);
        await Assert.That(result.Kind).IsEqualTo(DateTimeKind.Utc);
    }

    [Test]
    public async Task DateTimeSerializer_RoundTrip_SpecificDate()
    {
        var serializer = Serializers.DateTime;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var value = new DateTime(2024, 6, 15, 14, 30, 45, DateTimeKind.Utc);

        serializer.Serialize(value, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(value);
        await Assert.That(result.Kind).IsEqualTo(DateTimeKind.Utc);
    }

    #endregion

    #region DateTimeOffset Serializer Tests

    [Test]
    public async Task DateTimeOffsetSerializer_RoundTrip_UtcOffset()
    {
        var serializer = Serializers.DateTimeOffset;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var value = new DateTimeOffset(2024, 6, 15, 14, 30, 45, TimeSpan.Zero);

        serializer.Serialize(value, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(value);
        await Assert.That(result.Offset).IsEqualTo(TimeSpan.Zero);
    }

    [Test]
    public async Task DateTimeOffsetSerializer_RoundTrip_PositiveOffset()
    {
        var serializer = Serializers.DateTimeOffset;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var offset = new TimeSpan(5, 30, 0);
        var value = new DateTimeOffset(2024, 6, 15, 14, 30, 45, offset);

        serializer.Serialize(value, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(value);
        await Assert.That(result.Offset).IsEqualTo(offset);
    }

    [Test]
    public async Task DateTimeOffsetSerializer_RoundTrip_NegativeOffset()
    {
        var serializer = Serializers.DateTimeOffset;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var offset = new TimeSpan(-8, 0, 0);
        var value = new DateTimeOffset(2024, 6, 15, 14, 30, 45, offset);

        serializer.Serialize(value, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(value);
        await Assert.That(result.Offset).IsEqualTo(offset);
    }

    [Test]
    public async Task DateTimeOffsetSerializer_RoundTrip_MinValue()
    {
        var serializer = Serializers.DateTimeOffset;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var value = DateTimeOffset.MinValue;

        serializer.Serialize(value, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(value);
        await Assert.That(result.Offset).IsEqualTo(value.Offset);
    }

    [Test]
    public async Task DateTimeOffsetSerializer_RoundTrip_MaxValue()
    {
        var serializer = Serializers.DateTimeOffset;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var value = DateTimeOffset.MaxValue;

        serializer.Serialize(value, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(value);
        await Assert.That(result.Offset).IsEqualTo(value.Offset);
    }

    #endregion

    #region TimeSpan Serializer Tests

    [Test]
    public async Task TimeSpanSerializer_RoundTrip_Zero()
    {
        var serializer = Serializers.TimeSpan;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serializer.Serialize(TimeSpan.Zero, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(TimeSpan.Zero);
    }

    [Test]
    public async Task TimeSpanSerializer_RoundTrip_OneHour()
    {
        var serializer = Serializers.TimeSpan;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var value = TimeSpan.FromHours(1);

        serializer.Serialize(value, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(value);
    }

    [Test]
    public async Task TimeSpanSerializer_RoundTrip_MinValue()
    {
        var serializer = Serializers.TimeSpan;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serializer.Serialize(TimeSpan.MinValue, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(TimeSpan.MinValue);
    }

    [Test]
    public async Task TimeSpanSerializer_RoundTrip_MaxValue()
    {
        var serializer = Serializers.TimeSpan;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serializer.Serialize(TimeSpan.MaxValue, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(TimeSpan.MaxValue);
    }

    [Test]
    public async Task TimeSpanSerializer_RoundTrip_NegativeValue()
    {
        var serializer = Serializers.TimeSpan;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var value = TimeSpan.FromMinutes(-90);

        serializer.Serialize(value, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result).IsEqualTo(value);
    }

    #endregion

    #region RawBytes Serializer Tests

    [Test]
    public async Task RawBytesSerializer_SingleSegment_SerializesAndDeserializes()
    {
        var serializer = Serializers.RawBytes;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var data = new byte[] { 1, 2, 3, 4, 5 };

        serializer.Serialize(data, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result.ToArray()).IsEquivalentTo(data);
    }

    [Test]
    public async Task RawBytesSerializer_SingleSegment_ReturnsZeroCopySlice()
    {
        var serializer = Serializers.RawBytes;
        var context = CreateContext();
        var originalData = new byte[] { 10, 20, 30, 40, 50 };
        var sequence = new ReadOnlySequence<byte>(originalData);

        var result = serializer.Deserialize(sequence, context);

        // Zero-copy: result should reference the same memory
        await Assert.That(result.Length).IsEqualTo(originalData.Length);

        // Verify it's the same underlying memory by checking that modifications
        // to the original array are visible in the result (proving same backing array)
        var originalFirstByte = originalData[0];
        originalData[0] = 255;
        await Assert.That(result.Span[0]).IsEqualTo((byte)255);
        originalData[0] = originalFirstByte; // Restore
    }

    [Test]
    public async Task RawBytesSerializer_MultiSegment_DeserializesToContiguousMemory()
    {
        var serializer = Serializers.RawBytes;
        var context = CreateContext();

        // Create a multi-segment ReadOnlySequence
        var segment1 = new byte[] { 1, 2, 3 };
        var segment2 = new byte[] { 4, 5, 6 };
        var segment3 = new byte[] { 7, 8, 9 };

        var firstSegment = new BufferSegment(segment1);
        var secondSegment = firstSegment.Append(segment2);
        var thirdSegment = secondSegment.Append(segment3);

        var sequence = new ReadOnlySequence<byte>(firstSegment, 0, thirdSegment, segment3.Length);

        // Verify it's actually multi-segment
        await Assert.That(sequence.IsSingleSegment).IsFalse();

        var result = serializer.Deserialize(sequence, context);

        // Should contain all bytes from all segments
        var expected = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        await Assert.That(result.ToArray()).IsEquivalentTo(expected);
    }

    [Test]
    public async Task RawBytesSerializer_EmptyData_ReturnsEmptyMemory()
    {
        var serializer = Serializers.RawBytes;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serializer.Serialize(ReadOnlyMemory<byte>.Empty, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result.Length).IsEqualTo(0);
        await Assert.That(result.IsEmpty).IsTrue();
    }

    [Test]
    public async Task RawBytesSerializer_LargePayload_1KB_SerializesAndDeserializes()
    {
        var serializer = Serializers.RawBytes;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var data = new byte[1024];
        Random.Shared.NextBytes(data);

        serializer.Serialize(data, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result.Length).IsEqualTo(1024);
        await Assert.That(result.ToArray()).IsEquivalentTo(data);
    }

    [Test]
    public async Task RawBytesSerializer_LargePayload_1MB_SerializesAndDeserializes()
    {
        var serializer = Serializers.RawBytes;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var data = new byte[1024 * 1024]; // 1MB
        Random.Shared.NextBytes(data);

        serializer.Serialize(data, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result.Length).IsEqualTo(1024 * 1024);
        await Assert.That(result.ToArray()).IsEquivalentTo(data);
    }

    [Test]
    public async Task RawBytesSerializer_SerializeFromMemory_PreservesData()
    {
        var serializer = Serializers.RawBytes;
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        // Serialize from a ReadOnlyMemory slice (not starting at 0)
        var fullArray = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        var slice = new ReadOnlyMemory<byte>(fullArray, 3, 4); // [3, 4, 5, 6]

        serializer.Serialize(slice, ref buffer, context);
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

        await Assert.That(result.ToArray()).IsEquivalentTo(new byte[] { 3, 4, 5, 6 });
    }

    /// <summary>
    /// Helper class to create multi-segment ReadOnlySequence for testing.
    /// </summary>
    private sealed class BufferSegment : ReadOnlySequenceSegment<byte>
    {
        public BufferSegment(ReadOnlyMemory<byte> memory)
        {
            Memory = memory;
        }

        public BufferSegment Append(ReadOnlyMemory<byte> memory)
        {
            var segment = new BufferSegment(memory)
            {
                RunningIndex = RunningIndex + Memory.Length
            };
            Next = segment;
            return segment;
        }
    }

    #endregion
}
