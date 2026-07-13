using System.Buffers;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace Dekaf.Networking;

/// <summary>
/// Reads length-prefixed Kafka response frames directly from the connection's read source
/// (the raw <see cref="Socket"/> for plain TCP, the <c>SslStream</c> for TLS) into pooled
/// pooled response storage.
/// <para/>
/// <b>Why not a Pipe?</b> The previous receive path pumped socket bytes into a
/// <c>System.IO.Pipelines.Pipe</c> and then copied every frame out of the pipe's segments
/// into a pooled response array — a second full-payload memcpy on top of the kernel→user
/// copy. At consumer throughput that copy was the single largest Dekaf-only per-byte cost
/// (issue #1757). This reader keeps exactly one user-space copy, matching librdkafka:
/// once a frame's 4-byte size prefix is known, the frame body is received straight into
/// the pooled response array.
/// <para/>
/// Small frames (producer acks, heartbeats) are still batched through an internal receive
/// buffer so one socket read can carry many frames; only the portion of a frame that
/// extends past the buffered bytes is received directly into the frame's array. The
/// internal buffer is fully drained before a direct fill starts, and direct fills never
/// request more than the frame's remaining bytes, so the source is never over-read.
/// <para/>
/// <b>Threading:</b> single consumer — only the connection's receive loop calls
/// <see cref="ReadFrameAsync"/>. Reads are not cancellable; the owner interrupts an
/// in-flight read by calling <see cref="Abort"/>.
/// </summary>
internal sealed class ResponseFrameReader : IDisposable
{
    private readonly Socket? _socket;
    private readonly Stream? _stream;
    private readonly ResponseBufferPool _responseBufferPool;
    private readonly Action<int>? _onBytesReceived;

    private IMemoryOwner<byte>? _bufferOwner;
    private readonly Memory<byte> _buffer;
    private int _start;
    private int _end;

    // In-progress frame assembly state. Kept in fields (not locals) so that an EOF or a
    // faulted read mid-frame can release pooled storage via AbandonInProgressFrame.
    private byte[]? _frameArray;
    private NativeResponseBuffer? _nativeFrame;
    private int _frameSize;
    private int _frameFilled;

    private int _aborted;
    private bool _readInFlight;

    /// <param name="socket">The socket to receive from (plain TCP), or the socket underlying
    /// <paramref name="stream"/> (TLS) so <see cref="Abort"/> can close it without blocking.
    /// May be null in tests that read from a stream only.</param>
    /// <param name="stream">When non-null, reads go through this stream (TLS decryption
    /// requires the Stream abstraction). When null, reads go directly to the socket.</param>
    public ResponseFrameReader(
        Socket? socket,
        Stream? stream,
        int receiveBufferSize,
        ResponseBufferPool responseBufferPool,
        MemoryPool<byte> memoryPool,
        Action<int>? onBytesReceived = null)
    {
        if (socket is null && stream is null)
            throw new ArgumentException("Either a socket or a stream is required.");

        _socket = socket;
        _stream = stream;
        _responseBufferPool = responseBufferPool;
        _onBytesReceived = onBytesReceived;
        _bufferOwner = memoryPool.Rent(receiveBufferSize);
        _buffer = _bufferOwner.Memory;
    }

    private int BufferedByteCount => _end - _start;

    /// <summary>
    /// Reads the next complete response frame. Returns an end-of-stream result when the
    /// remote peer closes the connection (a partial frame at EOF is discarded — its pooled
    /// array is returned and the caller fails all pending requests). Throws
    /// <see cref="Errors.KafkaException"/> for invalid frame sizes and propagates I/O
    /// failures (including reads faulted by <see cref="Abort"/>).
    /// </summary>
#if NET
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
#endif
    public async ValueTask<ResponseFrame> ReadFrameAsync()
    {
        if (_frameArray is null && _nativeFrame is null)
        {
            // Accumulate the 4-byte size prefix through the internal buffer. Only ever
            // buffers ahead within the current receive; compaction moves at most 3 bytes.
            while (BufferedByteCount < 4)
            {
                if (_end == _buffer.Length)
                {
                    _buffer.Span[_start.._end].CopyTo(_buffer.Span);
                    _end -= _start;
                    _start = 0;
                }

                var read = await ReadSourceAsync(_buffer[_end..]).ConfigureAwait(false);
                if (read == 0)
                    return ResponseFrame.EndOfStream;

                _end += read;
            }

            var frameSize = BinaryPrimitives.ReadInt32BigEndian(_buffer.Span[_start..]);
            ConnectionHelper.ValidateResponseFrameSize(frameSize, _responseBufferPool.MaxArrayLength);
            _start += 4;

            if (frameSize >= ResponseBufferPool.NativeMemoryThresholdBytes)
                _nativeFrame = _responseBufferPool.RentNative(frameSize);
            else
                _frameArray = _responseBufferPool.Pool.Rent(frameSize);
            _frameSize = frameSize;

            // Copy whatever part of the payload is already buffered.
            var buffered = Math.Min(frameSize, BufferedByteCount);
            _buffer.Span.Slice(_start, buffered).CopyTo(FrameMemory.Span);
            _start += buffered;
            _frameFilled = buffered;

            if (_start == _end)
            {
                _start = 0;
                _end = 0;
            }
        }

        // Receive the remainder of the payload directly into the pooled array — no
        // intermediate buffer, so large fetch payloads are copied exactly once.
        while (_frameFilled < _frameSize)
        {
            var read = await ReadSourceAsync(FrameMemory.Slice(_frameFilled, _frameSize - _frameFilled))
                .ConfigureAwait(false);
            if (read == 0)
            {
                // EOF mid-frame: discard the partial frame; the caller fails pending requests.
                AbandonInProgressFrame();
                return ResponseFrame.EndOfStream;
            }

            _frameFilled += read;
        }

        var correlationId = BinaryPrimitives.ReadInt32BigEndian(FrameMemory.Span);
        var response = _nativeFrame is not null
            ? new PooledResponseBuffer(_nativeFrame, _frameSize)
            : new PooledResponseBuffer(_frameArray!, _frameSize, isPooled: true, pool: _responseBufferPool);
        _nativeFrame = null;
        _frameArray = null;
        return ResponseFrame.ForResponse(correlationId, response);
    }

#if NET
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
#endif
    private async ValueTask<int> ReadSourceAsync(Memory<byte> destination)
    {
        int read;
        Volatile.Write(ref _readInFlight, true);
        try
        {
            read = _stream is not null
                ? await _stream.ReadAsync(destination).ConfigureAwait(false)
                : await _socket!.ReceiveAsync(destination, SocketFlags.None).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _readInFlight, false);
        }

        if (read > 0)
            _onBytesReceived?.Invoke(read);

        return read;
    }

    /// <summary>
    /// Faults any in-flight or future source read. Idempotent and thread-safe; called from
    /// the receive-timeout timer and from disposal. Reads are deliberately not cancellable —
    /// closing the socket aborts a pending receive on all platforms without blocking the
    /// caller, the same wake-up mechanism the old read pump relied on for teardown. For
    /// stream-only readers (tests) the stream is disposed instead, the one case where this
    /// class touches the source's lifetime.
    /// </summary>
    public void Abort()
    {
        if (Interlocked.Exchange(ref _aborted, 1) != 0)
            return;

        try
        {
            if (_socket is not null)
            {
                _socket.Close(0);
            }
            else
            {
                _stream!.Dispose();
            }
        }
        catch
        {
            // Already closed/disposed — nothing left to wake.
        }
    }

    /// <summary>
    /// Returns pooled buffers. Safe to call after the receive loop has exited. If a source
    /// read is somehow still in flight (hung teardown), buffers are deliberately leaked to
    /// the GC instead of being recycled while the read could still write into them.
    /// Does not dispose the socket/stream — the connection owns those (see <see cref="Abort"/>
    /// for the stream-only exception).
    /// </summary>
    public void Dispose()
    {
        if (Volatile.Read(ref _readInFlight))
            return;

        AbandonInProgressFrame();
        _bufferOwner?.Dispose();
        _bufferOwner = null;
    }

    private void AbandonInProgressFrame()
    {
        var frameArray = _frameArray;
        var nativeFrame = _nativeFrame;
        _frameArray = null;
        _nativeFrame = null;
        if (frameArray is not null)
            _responseBufferPool.Pool.Return(frameArray);
        nativeFrame?.Return();
    }

    private Memory<byte> FrameMemory => _nativeFrame is not null
        ? _nativeFrame.Memory
        : _frameArray.AsMemory();
}

/// <summary>
/// Result of <see cref="ResponseFrameReader.ReadFrameAsync"/>: either a complete response
/// frame (correlation id + pooled payload) or an end-of-stream marker.
/// </summary>
internal readonly record struct ResponseFrame(bool IsEndOfStream, int CorrelationId, PooledResponseBuffer Buffer)
{
    public static ResponseFrame EndOfStream => new(IsEndOfStream: true, 0, default);

    public static ResponseFrame ForResponse(int correlationId, PooledResponseBuffer buffer)
        => new(IsEndOfStream: false, correlationId, buffer);
}
