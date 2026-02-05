using System.Buffers;
using Dekaf.Compression;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Compression;

public class NoneCompressionCodecTests
{
    [Test]
    public async Task NoneCompressionCodec_Type_ReturnsNone()
    {
        var codec = new NoneCompressionCodec();
        await Assert.That(codec.Type).IsEqualTo(CompressionType.None);
    }

    [Test]
    public async Task NoneCompressionCodec_Compress_PreservesDataExactly()
    {
        var codec = new NoneCompressionCodec();
        var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(data), compressedBuffer);

        await Assert.That(compressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(data);
    }

    [Test]
    public async Task NoneCompressionCodec_Decompress_PreservesDataExactly()
    {
        var codec = new NoneCompressionCodec();
        var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        var decompressedBuffer = new ArrayBufferWriter<byte>();
        codec.Decompress(new ReadOnlySequence<byte>(data), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(data);
    }

    [Test]
    public async Task NoneCompressionCodec_RoundTrip_EmptyData()
    {
        var codec = new NoneCompressionCodec();
        var data = Array.Empty<byte>();

        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(data), compressedBuffer);

        await Assert.That(compressedBuffer.WrittenCount).IsEqualTo(0);
    }

    [Test]
    public async Task NoneCompressionCodec_RoundTrip_LargeData()
    {
        var codec = new NoneCompressionCodec();
        var data = new byte[100_000];
        Random.Shared.NextBytes(data);

        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(data), compressedBuffer);

        await Assert.That(compressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(data);
    }

    [Test]
    public async Task NoneCompressionCodec_Compress_MultiSegment_PreservesData()
    {
        var codec = new NoneCompressionCodec();

        var segment1 = new byte[] { 1, 2, 3 };
        var segment2 = new byte[] { 4, 5, 6 };
        var segment3 = new byte[] { 7, 8, 9 };

        var firstSegment = new TestMemorySegment(segment1);
        var secondSegment = firstSegment.Append(segment2);
        var thirdSegment = secondSegment.Append(segment3);

        var sequence = new ReadOnlySequence<byte>(firstSegment, 0, thirdSegment, segment3.Length);

        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(sequence, compressedBuffer);

        var expectedData = segment1.Concat(segment2).Concat(segment3).ToArray();
        await Assert.That(compressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(expectedData);
    }

    private sealed class TestMemorySegment : ReadOnlySequenceSegment<byte>
    {
        public TestMemorySegment(ReadOnlyMemory<byte> memory)
        {
            Memory = memory;
        }

        public TestMemorySegment Append(ReadOnlyMemory<byte> memory)
        {
            var segment = new TestMemorySegment(memory)
            {
                RunningIndex = RunningIndex + Memory.Length
            };
            Next = segment;
            return segment;
        }
    }
}
