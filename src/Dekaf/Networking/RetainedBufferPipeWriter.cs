using System.Buffers;
using System.IO.Pipelines;

namespace Dekaf.Networking;

/// <summary>
/// A <see cref="PipeWriter"/> that retains its internal buffer across flushes, avoiding the
/// per-flush rent/return cycle inherent in <see cref="PipeWriter"/>.<c>Create(Stream)</c>.
/// <para/>
/// <b>Problem:</b> <c>StreamPipeWriter</c> (created by <c>PipeWriter.Create(stream)</c>)
/// returns ALL segments to the <see cref="MemoryPool{T}"/> on every <c>FlushAsync</c> call,
/// then rents a fresh segment on the next <c>GetMemory</c>. Under high-throughput production
/// (especially with a single connection), this creates hundreds of thousands of rent/return
/// cycles per second. When the pool's <c>ConfigurableArrayPool</c> bucket capacity is exceeded,
/// each miss allocates a new <c>byte[]</c> on the heap — causing hundreds of GB of allocation
/// churn and severe Gen2 GC pressure.
/// <para/>
/// <b>Solution:</b> This writer holds a single contiguous buffer that grows on demand but is
/// never returned to the pool until the writer is completed. On <c>FlushAsync</c>, the buffered
/// data is written to the underlying <see cref="Stream"/> and the write position resets to zero,
/// but the buffer itself is retained for the next write.
/// <para/>
/// <b>Usage pattern:</b> <c>KafkaConnection</c> always follows the sequence
/// <c>GetMemory(length) → copy → Advance(length) → FlushAsync()</c> under a write lock,
/// with at most one unflushed write at a time. This writer is optimized for that pattern.
/// <para/>
/// <b>Memory:</b> The buffer grows to fit the largest message seen on the connection (high-water
/// mark) and is retained until the connection closes. This trades per-flush GC churn for
/// connection-lifetime memory proportional to the largest message size.
/// </summary>
internal sealed class RetainedBufferPipeWriter : PipeWriter
{
    private readonly Stream _stream;
    private readonly MemoryPool<byte> _pool;
    private readonly int _minimumBufferSize;

    private IMemoryOwner<byte>? _currentBuffer;
    private int _currentBufferSize;
    private int _bytesWritten;
    private bool _completed;

    public RetainedBufferPipeWriter(Stream stream, MemoryPool<byte> pool, int minimumBufferSize = 65536)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(pool);

        _stream = stream;
        _pool = pool;
        _minimumBufferSize = minimumBufferSize;
    }

    public override void Advance(int bytes)
    {
        ThrowIfCompleted();

        ArgumentOutOfRangeException.ThrowIfNegative(bytes);

        if (_currentBuffer is null || _bytesWritten + bytes > _currentBufferSize)
        {
            throw new InvalidOperationException("Cannot advance past the end of the buffer.");
        }

        _bytesWritten += bytes;
    }

    public override Memory<byte> GetMemory(int sizeHint = 0)
    {
        ThrowIfCompleted();
        EnsureCapacity(sizeHint);
        return _currentBuffer!.Memory[_bytesWritten..];
    }

    public override Span<byte> GetSpan(int sizeHint = 0)
    {
        ThrowIfCompleted();
        EnsureCapacity(sizeHint);
        return _currentBuffer!.Memory.Span[_bytesWritten..];
    }

    public override async ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_completed)
        {
            return new FlushResult(isCanceled: false, isCompleted: true);
        }

        if (_bytesWritten > 0)
        {
            var bytesToFlush = _bytesWritten;
            _bytesWritten = 0; // Optimistic reset — connection is closed on failure anyway.
            // Buffer is RETAINED — not returned to the pool.
            await _stream.WriteAsync(_currentBuffer!.Memory[..bytesToFlush], cancellationToken)
                .ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        return new FlushResult(isCanceled: false, isCompleted: false);
    }

    public override void CancelPendingFlush()
    {
        // No-op: cancellation is handled by the CancellationToken passed to FlushAsync.
        // StreamPipeWriter uses an internal flag for this, but KafkaConnection never calls
        // CancelPendingFlush — it uses CancellationToken-based timeout instead.
    }

    public override void Complete(Exception? exception = null)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        ReturnBuffer();
    }

    public override ValueTask CompleteAsync(Exception? exception = null)
    {
        Complete(exception);
        return default;
    }

    private void EnsureCapacity(int sizeHint)
    {
        if (sizeHint <= 0)
        {
            sizeHint = _minimumBufferSize;
        }

        int remainingCapacity = _currentBufferSize - _bytesWritten;

        if (_currentBuffer is not null && remainingCapacity >= sizeHint)
        {
            return;
        }

        // Need a larger buffer. Since we always flush before the next write,
        // _bytesWritten is typically 0 here, making the copy a no-op.
        int requiredSize = _bytesWritten + sizeHint;
        IMemoryOwner<byte> newBuffer = _pool.Rent(Math.Max(requiredSize, _minimumBufferSize));
        int newBufferSize = newBuffer.Memory.Length;

        if (_bytesWritten > 0)
        {
            // Preserve any unflushed data (defensive — shouldn't happen in normal use).
            _currentBuffer!.Memory.Span[.._bytesWritten].CopyTo(newBuffer.Memory.Span);
        }

        _currentBuffer?.Dispose();
        _currentBuffer = newBuffer;
        _currentBufferSize = newBufferSize;
    }

    private void ThrowIfCompleted()
    {
        if (_completed)
        {
            throw new InvalidOperationException("PipeWriter has been completed.");
        }
    }

    private void ReturnBuffer()
    {
        _currentBuffer?.Dispose();
        _currentBuffer = null;
        _currentBufferSize = 0;
        _bytesWritten = 0;
    }
}
