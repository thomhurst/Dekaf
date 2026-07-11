using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;
using Dekaf.Networking;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures <see cref="ResponseFrameReader"/> — the direct-read receive path introduced for
/// issue #1757 that frames responses straight into pooled arrays with exactly one user-space
/// copy (the old path additionally copied every payload from the pipe into the pooled array;
/// see <see cref="ReceiveLoopCopyBenchmark"/> for the isolated cost of that second copy).
///
/// The source stream serves pre-built frames in 64KB slices, mimicking socket receive sizes.
/// Steady-state reads should be allocation-free: frames that complete synchronously never
/// box the async state machine, and payload arrays come from the response buffer pool.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class ResponseFrameReaderBenchmarks
{
    private const int ReceiveChunkSize = 65_536;

    private PipeMemoryPool _memoryPool = null!;
    private ChunkedReadStream _smallFramesStream = null!;
    private ResponseFrameReader _smallFramesReader = null!;
    private int _smallFrameCount;
    private ChunkedReadStream _largeFramesStream = null!;
    private ResponseFrameReader _largeFramesReader = null!;
    private int _largeFrameCount;

    [GlobalSetup]
    public void Setup()
    {
        _memoryPool = new PipeMemoryPool();

        // Producer-ack-like traffic: many small frames per receive chunk.
        (var smallBlob, _smallFrameCount) = BuildFrames(frameCount: 10_000, payloadSize: 100);
        _smallFramesStream = new ChunkedReadStream(smallBlob, ReceiveChunkSize);
        _smallFramesReader = CreateReader(_smallFramesStream);

        // Fetch-like traffic: 1MB payloads that flow through the direct-fill path.
        (var largeBlob, _largeFrameCount) = BuildFrames(frameCount: 64, payloadSize: 1_048_576);
        _largeFramesStream = new ChunkedReadStream(largeBlob, ReceiveChunkSize);
        _largeFramesReader = CreateReader(_largeFramesStream);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _smallFramesReader.Dispose();
        _largeFramesReader.Dispose();
        _memoryPool.Dispose();
    }

    [Benchmark]
    public async Task<long> SmallFrames_100B()
        => await ReadAllFramesAsync(_smallFramesStream, _smallFramesReader, _smallFrameCount);

    [Benchmark]
    public async Task<long> LargeFrames_1MB()
        => await ReadAllFramesAsync(_largeFramesStream, _largeFramesReader, _largeFrameCount);

    private ResponseFrameReader CreateReader(ChunkedReadStream stream)
        => new(
            socket: null,
            stream,
            receiveBufferSize: ReceiveChunkSize,
            ResponseBufferPool.Default,
            _memoryPool);

    /// <summary>
    /// Reads every frame in the blob. The reader and stream live in <see cref="Setup"/> so
    /// the measured op (and its <c>[MemoryDiagnoser]</c> Allocated column) reflects
    /// steady-state framing only, not pool/reader construction.
    /// </summary>
    private static async Task<long> ReadAllFramesAsync(ChunkedReadStream stream, ResponseFrameReader reader, int expectedFrames)
    {
        stream.Reset();

        long totalBytes = 0;
        for (var i = 0; i < expectedFrames; i++)
        {
            var frame = await reader.ReadFrameAsync();
            if (frame.IsEndOfStream)
                throw new InvalidOperationException("Unexpected EOF");

            totalBytes += frame.Buffer.Length;
            frame.Buffer.Dispose();
        }

        return totalBytes;
    }

    private static (byte[] Blob, int FrameCount) BuildFrames(int frameCount, int payloadSize)
    {
        var frameLength = 4 + payloadSize;
        var blob = new byte[(long)frameCount * frameLength];
        for (var i = 0; i < frameCount; i++)
        {
            var frame = blob.AsSpan(i * frameLength, frameLength);
            BinaryPrimitives.WriteInt32BigEndian(frame, payloadSize);
            BinaryPrimitives.WriteInt32BigEndian(frame[4..], i); // correlation id
            for (var j = 8; j < frameLength; j++)
                frame[j] = (byte)j;
        }

        return (blob, frameCount);
    }

    /// <summary>
    /// Serves a fixed blob in bounded slices, mimicking per-recv sizes from a socket.
    /// Reads complete synchronously so the benchmark isolates framing + copy costs.
    /// </summary>
    private sealed class ChunkedReadStream(byte[] blob, int chunkSize) : Stream
    {
        private int _position;

        public void Reset() => _position = 0;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => blob.Length;
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => new(Read(buffer.Span));

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => Task.FromResult(Read(buffer.AsSpan(offset, count)));

        public override int Read(byte[] buffer, int offset, int count)
            => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> destination)
        {
            var count = Math.Min(Math.Min(destination.Length, chunkSize), blob.Length - _position);
            blob.AsSpan(_position, count).CopyTo(destination);
            _position += count;
            return count;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
