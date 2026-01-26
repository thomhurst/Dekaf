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
