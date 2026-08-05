using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using Dekaf.Compression;
using Dekaf.Compression.Snappy;
using Dekaf.Protocol.Records;
using Dekaf.Tests.Unit.Protocol;

namespace Dekaf.Tests.Unit.Compression;

public class SnappyCompressionCodecTests
{
    [Test]
    public async Task Type_ReturnsSnappy()
    {
        var codec = new SnappyCompressionCodec();

        await Assert.That(codec.Type).IsEqualTo(CompressionType.Snappy);
    }

    [Test]
    public async Task CompressDecompress_RoundTrip_SmallData()
    {
        var codec = new SnappyCompressionCodec();
        var original = "Hello, Kafka with Snappy compression!"u8.ToArray();
        var compressedBuffer = new ArrayBufferWriter<byte>();
        var decompressedBuffer = new ArrayBufferWriter<byte>();

        codec.Compress(new ReadOnlySequence<byte>(original), compressedBuffer);
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(original);
    }

    [Test]
    public async Task CompressDecompress_RoundTrip_LargeData()
    {
        var codec = new SnappyCompressionCodec(blockSize: 1024);
        var original = new byte[10000];
        Random.Shared.NextBytes(original);

        var compressedBuffer = new ArrayBufferWriter<byte>();
        var decompressedBuffer = new ArrayBufferWriter<byte>();

        codec.Compress(new ReadOnlySequence<byte>(original), compressedBuffer);
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(original);
    }

    [Test]
    public async Task CompressDecompress_RoundTrip_RepetitiveData()
    {
        // Repetitive data compresses well
        var codec = new SnappyCompressionCodec();
        var original = Encoding.UTF8.GetBytes(new string('A', 10000));

        var compressedBuffer = new ArrayBufferWriter<byte>();
        var decompressedBuffer = new ArrayBufferWriter<byte>();

        codec.Compress(new ReadOnlySequence<byte>(original), compressedBuffer);
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(original);
        // Compressed should be smaller for repetitive data
        await Assert.That(compressedBuffer.WrittenCount).IsLessThan(original.Length);
    }

    [Test]
    public async Task Compress_ProducesXerialMagicHeader()
    {
        var codec = new SnappyCompressionCodec();
        var data = "test"u8.ToArray();
        var compressedBuffer = new ArrayBufferWriter<byte>();

        codec.Compress(new ReadOnlySequence<byte>(data), compressedBuffer);

        // Xerial magic header: 0x82 0x53 0x4e 0x41 0x50 0x50 0x59 0x00
        var expectedMagic = new byte[] { 0x82, 0x53, 0x4e, 0x41, 0x50, 0x50, 0x59, 0x00 };
        await Assert.That(compressedBuffer.WrittenSpan.Slice(0, 8).ToArray()).IsEquivalentTo(expectedMagic);
    }

    [Test]
    public async Task Compress_DefaultBlockSize_UsesSingleBlockForOneMiB()
    {
        var codec = new SnappyCompressionCodec();
        var original = Enumerable.Range(0, 1024 * 1024).Select(static i => (byte)i).ToArray();
        var compressedBuffer = new ArrayBufferWriter<byte>();

        codec.Compress(new ReadOnlySequence<byte>(original), compressedBuffer);

        var blockCount = CountXerialBlocks(compressedBuffer.WrittenSpan);
        await Assert.That(blockCount).IsEqualTo(1);
    }

    [Test]
    public async Task Decompress_MalformedRawPayload_Throws()
    {
        var codec = new SnappyCompressionCodec();
        var invalidData = new byte[] { 0x80 }; // Truncated raw Snappy uncompressed-length varint.
        var decompressedBuffer = new ArrayBufferWriter<byte>();

        await Assert.That(() => codec.Decompress(new ReadOnlySequence<byte>(invalidData), decompressedBuffer))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Decompress_UnterminatedRawLength_ThrowsInvalidData()
    {
        var codec = new SnappyCompressionCodec();
        var invalidData = new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80 };
        var decompressedBuffer = new ArrayBufferWriter<byte>();

        await Assert.That(() => codec.Decompress(new ReadOnlySequence<byte>(invalidData), decompressedBuffer))
            .ThrowsExactly<InvalidDataException>();
    }

    [Test]
    public async Task Decompress_DestinationThrowsInvalidOperation_PreservesException()
    {
        var codec = new SnappyCompressionCodec();
        var original = "destination failure"u8.ToArray();
        var compressed = new byte[Snappier.Snappy.GetMaxCompressedLength(original.Length)];
        var compressedLength = Snappier.Snappy.Compress(original, compressed);
        var expected = new InvalidOperationException("Cannot grow buffer: maximum size reached.");

        var exception = await Assert.That(() => codec.Decompress(
                new ReadOnlySequence<byte>(compressed.AsMemory(0, compressedLength)),
                new InvalidOperationBufferWriter(expected)))
            .ThrowsExactly<InvalidOperationException>();

        await Assert.That(exception).IsSameReferenceAs(expected);
    }

    [Test]
    public async Task Decompress_TruncatedXerialHeader_Throws()
    {
        var codec = new SnappyCompressionCodec();
        // Complete xerial magic, but no version or minimum-compatible-version fields.
        var truncatedData = new byte[] { 0x82, 0x53, 0x4e, 0x41, 0x50, 0x50, 0x59, 0x00 };
        var decompressedBuffer = new ArrayBufferWriter<byte>();

        await Assert.That(() => codec.Decompress(new ReadOnlySequence<byte>(truncatedData), decompressedBuffer))
            .Throws<InvalidDataException>()
            .WithMessage("Snappy data too short for xerial header.");
    }

    [Test]
    public async Task CompressDecompress_EmptyData()
    {
        var codec = new SnappyCompressionCodec();
        var original = Array.Empty<byte>();

        var compressedBuffer = new ArrayBufferWriter<byte>();
        var decompressedBuffer = new ArrayBufferWriter<byte>();

        codec.Compress(new ReadOnlySequence<byte>(original), compressedBuffer);
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenCount).IsEqualTo(0);
    }

    [Test]
    public async Task CompressDecompress_MultipleBlocks()
    {
        // Use small block size to force multiple blocks
        var codec = new SnappyCompressionCodec(blockSize: 100);
        var original = new byte[500];
        Random.Shared.NextBytes(original);

        var compressedBuffer = new ArrayBufferWriter<byte>();
        var decompressedBuffer = new ArrayBufferWriter<byte>();

        codec.Compress(new ReadOnlySequence<byte>(original), compressedBuffer);
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(original);
    }

    [Test]
    public async Task Decompress_RawSnappyPayload_PreservesData()
    {
        var codec = new SnappyCompressionCodec();
        var original = Encoding.UTF8.GetBytes(new string('R', 10_000));
        var compressed = new byte[Snappier.Snappy.GetMaxCompressedLength(original.Length)];
        var compressedLength = Snappier.Snappy.Compress(original, compressed);
        var decompressedBuffer = new ArrayBufferWriter<byte>();

        codec.Decompress(
            new ReadOnlySequence<byte>(compressed.AsMemory(0, compressedLength)),
            decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(original);
    }

    [Test]
    public async Task Constructor_InvalidBlockSize_Throws()
    {
        await Assert.That(() => new SnappyCompressionCodec(blockSize: 0))
            .Throws<ArgumentOutOfRangeException>();

        await Assert.That(() => new SnappyCompressionCodec(blockSize: -1))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AddSnappy_RegistersCodec()
    {
        var registry = new CompressionCodecRegistry();

        registry.AddSnappy();

        await Assert.That(registry.IsSupported(CompressionType.Snappy)).IsTrue();
        await Assert.That(registry.GetCodec(CompressionType.Snappy)).IsTypeOf<SnappyCompressionCodec>();
    }

    [Test]
    public async Task AddSnappy_WithCustomBlockSize_RegistersCodec()
    {
        var registry = new CompressionCodecRegistry();

        registry.AddSnappy(blockSize: 32768);

        await Assert.That(registry.IsSupported(CompressionType.Snappy)).IsTrue();
    }

    [Test]
    public async Task CompressDecompress_MultiSegmentSequence()
    {
        var codec = new SnappyCompressionCodec();

        // Create a multi-segment ReadOnlySequence
        var segment1 = new byte[] { 1, 2, 3, 4, 5 };
        var segment2 = new byte[] { 6, 7, 8, 9, 10 };
        var firstSegment = new TestMemorySegment<byte>(segment1);
        var lastSegment = firstSegment.Append(segment2);
        var multiSegmentSequence = new ReadOnlySequence<byte>(firstSegment, 0, lastSegment, segment2.Length);

        var compressedBuffer = new ArrayBufferWriter<byte>();
        var decompressedBuffer = new ArrayBufferWriter<byte>();

        codec.Compress(multiSegmentSequence, compressedBuffer);
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        var expected = segment1.Concat(segment2).ToArray();
        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(expected);
    }

    [Test]
    public async Task Decompress_MultiSegmentCompressedInput_RoundTrips()
    {
        var codec = new SnappyCompressionCodec(blockSize: 1024);
        var original = Enumerable.Range(0, 10_000).Select(static i => (byte)i).ToArray();

        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(original), compressedBuffer);
        var compressed = compressedBuffer.WrittenMemory;

        var firstSegment = new TestMemorySegment<byte>(compressed[..5]);
        var secondSegment = firstSegment.Append(compressed.Slice(5, 20));
        var thirdSegment = secondSegment.Append(compressed[25..]);
        var compressedSequence = new ReadOnlySequence<byte>(
            firstSegment,
            0,
            thirdSegment,
            thirdSegment.Memory.Length);

        var decompressedBuffer = new ArrayBufferWriter<byte>();
        codec.Decompress(compressedSequence, decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(original);
    }

    [Test]
    public async Task Compress_ReusedCodec_ProducesIdenticalOutputToFreshInstance()
    {
        // Pins that the codec's thread-cached scratch state (multi-segment compression
        // buffer, decompression destination tracker) never leaks between operations.
        // Guards the reuse contract that #2352 (pooled Snappier instances) must preserve.
        var payload = new byte[8192];
        new Random(42).NextBytes(payload);
        var reused = new SnappyCompressionCodec(blockSize: 1024);

        var reusedOutput = new ArrayBufferWriter<byte>(payload.Length + 128);
        for (var cycle = 0; cycle < 3; cycle++)
        {
            reusedOutput = new ArrayBufferWriter<byte>(payload.Length + 128);
            reused.Compress(CreateMultiSegmentSequence(payload), reusedOutput);
            var warmDecompressed = new ArrayBufferWriter<byte>(payload.Length);
            reused.Decompress(new ReadOnlySequence<byte>(reusedOutput.WrittenMemory), warmDecompressed);

            await Assert.That(warmDecompressed.WrittenSpan.SequenceEqual(payload)).IsTrue();
        }

        var freshOutput = new ArrayBufferWriter<byte>(payload.Length + 128);
        new SnappyCompressionCodec(blockSize: 1024).Compress(CreateMultiSegmentSequence(payload), freshOutput);

        await Assert.That(reusedOutput.WrittenSpan.SequenceEqual(freshOutput.WrittenSpan)).IsTrue();
    }

    [Test]
    public async Task CompressDecompress_ParallelWorkers_DoNotCorruptEachOther()
    {
        // Pins thread-safety of the codec's [ThreadStatic] scratch state: concurrent
        // batches compressed and decompressed through one shared codec instance must
        // round-trip independently. Catches cross-thread corruption if the thread-local
        // caches are ever replaced with a shared pool without proper ownership handoff.
        var codec = new SnappyCompressionCodec(blockSize: 4096);
        var failures = 0;

        Parallel.For(0, 8, worker =>
        {
            var random = new Random(worker);
            for (var iteration = 0; iteration < 50; iteration++)
            {
                var payload = new byte[random.Next(1, 16384)];
                random.NextBytes(payload);
                var source = iteration % 2 == 0
                    ? new ReadOnlySequence<byte>(payload)
                    : CreateMultiSegmentSequence(payload);

                var compressed = new ArrayBufferWriter<byte>(payload.Length + 128);
                codec.Compress(source, compressed);
                var decompressed = new ArrayBufferWriter<byte>(payload.Length);
                codec.Decompress(new ReadOnlySequence<byte>(compressed.WrittenMemory), decompressed);

                if (!decompressed.WrittenSpan.SequenceEqual(payload))
                {
                    Interlocked.Increment(ref failures);
                }
            }
        });

        await Assert.That(failures).IsEqualTo(0);
    }

    private static ReadOnlySequence<byte> CreateMultiSegmentSequence(byte[] payload)
        => payload.Length < 2
            ? new ReadOnlySequence<byte>(payload)
            : SequenceTestHelpers.CreateMultiSegmentSequence(payload, payload.Length / 2);

    private static int CountXerialBlocks(ReadOnlySpan<byte> compressed)
    {
        const int headerSize = 16;
        var offset = headerSize;
        var blocks = 0;

        while (offset < compressed.Length)
        {
            var blockLength = BinaryPrimitives.ReadInt32BigEndian(compressed.Slice(offset, 4));
            offset += 4 + blockLength;
            blocks++;
        }

        return blocks;
    }

    private sealed class InvalidOperationBufferWriter(InvalidOperationException exception) : IBufferWriter<byte>
    {
        public void Advance(int count) => throw exception;

        public Memory<byte> GetMemory(int sizeHint = 0) => throw exception;

        public Span<byte> GetSpan(int sizeHint = 0) => throw exception;
    }
}

/// <summary>
/// Helper class for creating multi-segment ReadOnlySequence for testing.
/// </summary>
internal sealed class TestMemorySegment<T> : ReadOnlySequenceSegment<T>
{
    public TestMemorySegment(ReadOnlyMemory<T> memory)
    {
        Memory = memory;
    }

    public TestMemorySegment<T> Append(ReadOnlyMemory<T> memory)
    {
        var segment = new TestMemorySegment<T>(memory)
        {
            RunningIndex = RunningIndex + Memory.Length
        };
        Next = segment;
        return segment;
    }
}
