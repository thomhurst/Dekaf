using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Networking;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures bounded-response frame discard when source reads suspend. Discarded frames
/// bypass response-buffer rental but still traverse the asynchronous receive path.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class ResponseFrameDiscardPoolingBenchmarks
{
    private const int FrameCount = 20_000;
    private const int PayloadSize = 100;

    private PipeMemoryPool _memoryPool = null!;
    private YieldingReadStream _stream = null!;
    private ResponseFrameReader _reader = null!;

    [GlobalSetup]
    public void Setup()
    {
        var blob = new byte[FrameCount * (PayloadSize + 4)];
        for (var i = 0; i < FrameCount; i++)
        {
            var frame = blob.AsSpan(i * (PayloadSize + 4), PayloadSize + 4);
            BinaryPrimitives.WriteInt32BigEndian(frame, PayloadSize);
            BinaryPrimitives.WriteInt32BigEndian(frame[4..], i);
        }

        _memoryPool = new PipeMemoryPool();
        _stream = new YieldingReadStream(blob, chunkSize: 16);
        _reader = new ResponseFrameReader(
            socket: null,
            _stream,
            receiveBufferSize: 64,
            ResponseBufferPool.Default,
            _memoryPool);
    }

    [Benchmark(OperationsPerInvoke = FrameCount)]
    [InvocationCount(1)]
    public async Task DiscardFrames()
    {
        _stream.Reset();
        for (var i = 0; i < FrameCount; i++)
        {
            var frame = await _reader.ReadBoundedFrameAsync(
                static _ => ResponseFrameAdmission.Discarded).ConfigureAwait(false);
            if (!frame.IsDiscarded)
                throw new InvalidOperationException();
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _reader.Dispose();
        _memoryPool.Dispose();
    }

    private sealed class YieldingReadStream(byte[] blob, int chunkSize) : Stream
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

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            var count = Math.Min(Math.Min(buffer.Length, chunkSize), blob.Length - _position);
            blob.AsMemory(_position, count).CopyTo(buffer);
            _position += count;
            return count;
        }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
