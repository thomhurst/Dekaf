using System.Buffers.Binary;
using Dekaf.Errors;
using Dekaf.Networking;

namespace Dekaf.Tests.Unit.Networking;

/// <summary>
/// Tests for <see cref="ResponseFrameReader"/>, which frames length-prefixed responses
/// directly from the read source into pooled arrays (one user-space copy, issue #1757).
/// </summary>
public sealed class ResponseFrameReaderTests
{
    [Test]
    public async Task ReadFrameAsync_SingleCompleteFrame_ReturnsCorrelationIdAndPayload()
    {
        var frame = BuildFrame(correlationId: 42, payloadSize: 100);
        using var reader = CreateReader(out _, frame);

        var result = await reader.ReadFrameAsync();

        await Assert.That(result.IsEndOfStream).IsFalse();
        await Assert.That(result.CorrelationId).IsEqualTo(42);
        await AssertPayloadAsync(result, payloadSize: 100);
    }

    [Test]
    public async Task ReadFrameAsync_MultipleFramesInOneChunk_ReadsSequentially()
    {
        var chunk = Concat(
            BuildFrame(correlationId: 1, payloadSize: 20),
            BuildFrame(correlationId: 2, payloadSize: 30),
            BuildFrame(correlationId: 3, payloadSize: 40));
        using var reader = CreateReader(out _, chunk);

        foreach (var (expectedId, expectedSize) in new[] { (1, 20), (2, 30), (3, 40) })
        {
            var result = await reader.ReadFrameAsync();
            await Assert.That(result.IsEndOfStream).IsFalse();
            await Assert.That(result.CorrelationId).IsEqualTo(expectedId);
            await AssertPayloadAsync(result, expectedSize);
        }
    }

    [Test]
    public async Task ReadFrameAsync_SizePrefixSplitAcrossReads_AssemblesFrame()
    {
        var frame = BuildFrame(correlationId: 7, payloadSize: 50);
        using var reader = CreateReader(out _, frame[..2], frame[2..6], frame[6..]);

        var result = await reader.ReadFrameAsync();

        await Assert.That(result.CorrelationId).IsEqualTo(7);
        await AssertPayloadAsync(result, payloadSize: 50);
    }

    [Test]
    public async Task ReadFrameAsync_BodyDeliveredInManyChunks_AssemblesFrame()
    {
        var frame = BuildFrame(correlationId: 9, payloadSize: 1000);
        var chunks = new List<byte[]>();
        for (var offset = 0; offset < frame.Length; offset += 100)
            chunks.Add(frame[offset..Math.Min(offset + 100, frame.Length)]);
        using var reader = CreateReader(out _, chunks.ToArray());

        var result = await reader.ReadFrameAsync();

        await Assert.That(result.CorrelationId).IsEqualTo(9);
        await AssertPayloadAsync(result, payloadSize: 1000);
    }

    [Test]
    public async Task ReadFrameAsync_FrameLargerThanReceiveBuffer_DirectFillPreservesPayload()
    {
        // A 16-byte receive buffer forces the bulk of the payload through the
        // direct-into-pooled-array path.
        var frame = BuildFrame(correlationId: 11, payloadSize: 5000);
        using var reader = CreateReader(out _, receiveBufferSize: 16, chunks: [frame]);

        var result = await reader.ReadFrameAsync();

        await Assert.That(result.CorrelationId).IsEqualTo(11);
        await AssertPayloadAsync(result, payloadSize: 5000);
    }

    [Test]
    public async Task ReadFrameAsync_TrailingPartialFrame_CompletesOnNextChunk()
    {
        var first = BuildFrame(correlationId: 1, payloadSize: 20);
        var second = BuildFrame(correlationId: 2, payloadSize: 20);
        // First chunk: complete first frame + 2 bytes of the second frame's size prefix.
        using var reader = CreateReader(out _, Concat(first, second[..2]), second[2..]);

        var resultOne = await reader.ReadFrameAsync();
        var resultTwo = await reader.ReadFrameAsync();

        await Assert.That(resultOne.CorrelationId).IsEqualTo(1);
        await Assert.That(resultTwo.CorrelationId).IsEqualTo(2);
        await AssertPayloadAsync(resultTwo, payloadSize: 20);
    }

    [Test]
    public async Task ReadFrameAsync_EofBeforeAnyData_ReturnsEndOfStream()
    {
        using var reader = CreateReader(out _);

        var result = await reader.ReadFrameAsync();

        await Assert.That(result.IsEndOfStream).IsTrue();
    }

    [Test]
    public async Task ReadFrameAsync_EofWithPartialSizePrefix_ReturnsEndOfStream()
    {
        using var reader = CreateReader(out _, new byte[] { 0, 0 });

        var result = await reader.ReadFrameAsync();

        await Assert.That(result.IsEndOfStream).IsTrue();
    }

    [Test]
    public async Task ReadFrameAsync_EofMidFrameBody_ReturnsEndOfStream()
    {
        var frame = BuildFrame(correlationId: 5, payloadSize: 1000);
        using var reader = CreateReader(out _, frame[..200]);

        var result = await reader.ReadFrameAsync();

        await Assert.That(result.IsEndOfStream).IsTrue();
    }

    [Test]
    public async Task ReadFrameAsync_NegativeFrameSize_ThrowsKafkaException()
    {
        var invalid = new byte[8];
        BinaryPrimitives.WriteInt32BigEndian(invalid, -1);
        using var reader = CreateReader(out _, invalid);

        var thrown = await Assert.ThrowsAsync<KafkaException>(async () => await reader.ReadFrameAsync());

        await Assert.That(thrown!.Message).Contains("Invalid response frame size -1");
    }

    [Test]
    public async Task ReadFrameAsync_FrameSizeAbovePoolLimit_ThrowsKafkaException()
    {
        var frame = BuildFrame(correlationId: 1, payloadSize: 17);
        using var stream = new ScriptedReadStream([frame]);
        using var reader = ResponseFrameTestHelpers.CreateReader(
            stream, responseBufferPool: new ResponseBufferPool(maxArrayLength: 16));

        var thrown = await Assert.ThrowsAsync<KafkaException>(async () => await reader.ReadFrameAsync());

        await Assert.That(thrown!.Message).Contains("Invalid response frame size 17");
    }

    [Test]
    public async Task ReadFrameAsync_MaxCorrelationId_Preserved()
    {
        var frame = BuildFrame(int.MaxValue, payloadSize: 10);
        using var reader = CreateReader(out _, frame);

        var result = await reader.ReadFrameAsync();

        await Assert.That(result.CorrelationId).IsEqualTo(int.MaxValue);
    }

    [Test]
    public async Task ReadFrameAsync_InvokesBytesReceivedCallbackPerRead()
    {
        var frame = BuildFrame(correlationId: 3, payloadSize: 300);
        using var stream = new ScriptedReadStream([frame[..100], frame[100..]]);
        var observed = new List<int>();
        using var reader = ResponseFrameTestHelpers.CreateReader(stream, onBytesReceived: observed.Add);

        var result = await reader.ReadFrameAsync();

        await Assert.That(result.CorrelationId).IsEqualTo(3);
        await Assert.That(observed.Sum()).IsEqualTo(frame.Length);
    }

    [Test]
    public async Task Abort_WithPendingRead_FaultsTheRead()
    {
        using var stream = new ScriptedReadStream([], blockAfterChunks: true);
        using var reader = ResponseFrameTestHelpers.CreateReader(stream);

        var readTask = reader.ReadFrameAsync().AsTask();
        await stream.ReadStarted.WaitAsync(TimeSpan.FromSeconds(5));

        reader.Abort();

        await Assert.That(async () => await readTask.WaitAsync(TimeSpan.FromSeconds(5)))
            .ThrowsException();
    }

    [Test]
    public void Dispose_IsIdempotent()
    {
        var reader = CreateReader(out _, BuildFrame(1, 10));

        reader.Dispose();
        reader.Dispose();
    }

    [Test]
    public async Task ValidateResponseFrameSize_RejectsInvalidSizes()
    {
        foreach (var size in new[] { int.MinValue, -1, 0, 3, 17, int.MaxValue })
        {
            await Assert.That(() => ConnectionHelper.ValidateResponseFrameSize(size, maxFrameSize: 16))
                .Throws<KafkaException>();
        }
    }

    [Test]
    public void ValidateResponseFrameSize_AllowsMinimumAndMaximumSizes()
    {
        ConnectionHelper.ValidateResponseFrameSize(frameSize: 4, maxFrameSize: 16);
        ConnectionHelper.ValidateResponseFrameSize(frameSize: 16, maxFrameSize: 16);
    }

    private static ResponseFrameReader CreateReader(
        out ScriptedReadStream stream,
        params byte[][] chunks)
        => CreateReader(out stream, receiveBufferSize: 4096, chunks);

    private static ResponseFrameReader CreateReader(
        out ScriptedReadStream stream,
        int receiveBufferSize,
        byte[][] chunks)
    {
        stream = new ScriptedReadStream(chunks);
        return ResponseFrameTestHelpers.CreateReader(stream, receiveBufferSize);
    }

    private static byte[] BuildFrame(int correlationId, int payloadSize)
        => ResponseFrameTestHelpers.BuildFrame(correlationId, payloadSize);

    private static async Task AssertPayloadAsync(ResponseFrame result, int payloadSize)
    {
        var payload = result.Buffer.Data;
        await Assert.That(payload.Length).IsEqualTo(payloadSize);

        // Skip the correlation id; the remaining bytes carry BuildFrame's pattern
        // (offset by the 4-byte size prefix that is not part of the payload).
        for (var i = 4; i < payloadSize; i++)
        {
            if (payload.Span[i] != (byte)((i + 4) % 251))
                throw new InvalidOperationException($"Payload mismatch at offset {i}");
        }

        result.Buffer.Dispose();
    }

    private static byte[] Concat(params byte[][] chunks)
    {
        var combined = new byte[chunks.Sum(static c => c.Length)];
        var offset = 0;
        foreach (var chunk in chunks)
        {
            chunk.CopyTo(combined, offset);
            offset += chunk.Length;
        }

        return combined;
    }

}

/// <summary>
/// Shared construction helpers for <see cref="ResponseFrameReader"/> tests, so the
/// frame-encoding convention and the reader wiring live in exactly one place.
/// </summary>
internal static class ResponseFrameTestHelpers
{
    private static readonly PipeMemoryPool SharedMemoryPool = new();

    public static ResponseFrameReader CreateReader(
        Stream stream,
        int receiveBufferSize = 4096,
        ResponseBufferPool? responseBufferPool = null,
        Action<int>? onBytesReceived = null)
        => new(
            socket: null,
            stream,
            receiveBufferSize,
            responseBufferPool ?? ResponseBufferPool.Default,
            SharedMemoryPool,
            onBytesReceived);

    /// <summary>
    /// Builds a length-prefixed response frame: 4-byte size, 4-byte correlation id, then a
    /// deterministic byte pattern.
    /// </summary>
    public static byte[] BuildFrame(int correlationId, int payloadSize)
    {
        var frame = new byte[4 + payloadSize];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(frame, payloadSize);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(4), correlationId);
        for (var i = 8; i < frame.Length; i++)
            frame[i] = (byte)(i % 251);
        return frame;
    }
}

/// <summary>
/// A read-only stream that serves queued chunks. Each read returns at most the
/// requested byte count (never discarding chunk remainders), mirroring socket
/// semantics. After the chunks are exhausted it either reports EOF or blocks
/// until disposed (to simulate an idle connection).
/// </summary>
internal sealed class ScriptedReadStream(
    byte[][] chunks,
    bool blockAfterChunks = false,
    bool runContinuationsAsynchronously = true) : Stream
{
    private readonly Queue<byte[]> _chunks = new(chunks);
    private readonly TaskCompletionSource _readStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _disposed = new(
        runContinuationsAsynchronously
            ? TaskCreationOptions.RunContinuationsAsynchronously
            : TaskCreationOptions.None);
    private byte[]? _current;
    private int _currentOffset;

    public Task ReadStarted => _readStarted.Task;

    public bool IsDisposed => _disposed.Task.IsCompleted;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        _readStarted.TrySetResult();

        if (_current is null || _currentOffset == _current.Length)
        {
            while (!_chunks.TryDequeue(out _current))
            {
                if (!blockAfterChunks)
                    return 0; // EOF

                await _disposed.Task.WaitAsync(cancellationToken);
                throw new ObjectDisposedException(nameof(ScriptedReadStream));
            }

            _currentOffset = 0;
        }

        var count = Math.Min(buffer.Length, _current.Length - _currentOffset);
        _current.AsMemory(_currentOffset, count).CopyTo(buffer);
        _currentOffset += count;
        return count;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override int Read(byte[] buffer, int offset, int count)
        => ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

    protected override void Dispose(bool disposing)
    {
        _disposed.TrySetResult();
        base.Dispose(disposing);
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
