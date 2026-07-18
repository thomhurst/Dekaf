using System.Buffers;
using Dekaf.Internal;

namespace Dekaf.Producer;

/// <summary>
/// Pooled, append-only batch storage that rents data in small chunks. Individual records
/// remain contiguous while the completed batch is exposed as a zero-copy sequence.
/// </summary>
internal sealed class IncrementalBatchBuffer
{
    internal const int MinimumChunkSize = 128;
    internal const int MaximumChunkSize = 16 * 1024;

    private const int BufferPoolSize = 1024;
    private const int SegmentPoolSize = 8192;
    private static readonly LockFreeStack<IncrementalBatchBuffer> s_buffers = new(BufferPoolSize);
    private static readonly LockFreeStack<ChunkSegment> s_segments = new(SegmentPoolSize);
    private static readonly RatchetableArrayPool<byte> s_chunkArrays = new(
        maxArrayLength: 4 * 1024 * 1024,
        initialArraysPerBucket: BatchArena.DefaultPoolSize);

    private ChunkSegment? _head;
    private ChunkSegment? _tail;
    private int _chunkSize;
    private int _length;
    private ChunkSegment? _lastAllocationSegment;
    private int _lastAllocationOffset;
    private int _lastAllocationLength;

    private IncrementalBatchBuffer(int batchSize) => Reset(batchSize);

    internal int Length => _length;

    internal static void RatchetPoolSize(int arraysPerBucket) =>
        s_chunkArrays.RatchetBucketCapacity(Math.Min(BufferPoolSize, arraysPerBucket));

    internal static void PreWarm(int count, int batchSize)
    {
        count = Math.Min(BufferPoolSize, count);
        var chunkSize = GetChunkSize(batchSize);
        var arrays = new byte[count][];
        for (var i = 0; i < count; i++)
            arrays[i] = s_chunkArrays.Pool.Rent(chunkSize);

        for (var i = 0; i < count; i++)
            s_chunkArrays.Pool.Return(arrays[i], clearArray: false);

        while (s_segments.Count < count && s_segments.TryPush(new ChunkSegment())) { }
        while (s_buffers.Count < count && s_buffers.TryPush(new IncrementalBatchBuffer(batchSize))) { }
    }

    internal int RetainedCapacity
    {
        get
        {
            var capacity = 0;
            for (var segment = _head; segment is not null; segment = segment.NextSegment)
                capacity = checked(capacity + segment.Buffer.Length);
            return capacity;
        }
    }

    internal static IncrementalBatchBuffer Rent(int batchSize)
    {
        if (!s_buffers.TryPop(out var buffer))
            return new IncrementalBatchBuffer(batchSize);

        buffer.Reset(batchSize);
        return buffer;
    }

    internal static void ReturnToPool(IncrementalBatchBuffer buffer)
    {
        buffer.ReturnChunks();
        s_buffers.TryPush(buffer);
    }

    internal void Allocate(
        int length,
        out byte[] buffer,
        out int bufferOffset,
        out int logicalOffset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        var segment = _tail;
        if (segment is null || segment.Capacity - segment.Used < length)
        {
            segment = RentSegment(Math.Max(_chunkSize, length));
            if (_tail is null)
            {
                _head = segment;
            }
            else
            {
                _tail.AttachNext(segment);
                segment.Previous = _tail;
            }

            _tail = segment;
        }

        logicalOffset = _length;
        bufferOffset = segment.Used;
        buffer = segment.Buffer;
        segment.SetUsed(checked(segment.Used + length));
        _length = checked(_length + length);
        _lastAllocationSegment = segment;
        _lastAllocationOffset = bufferOffset;
        _lastAllocationLength = length;
    }

    internal bool TryRewindLastAllocation(int logicalOffset, int length)
    {
        var segment = _lastAllocationSegment;
        if (segment is null
            || logicalOffset != _length - length
            || length != _lastAllocationLength
            || _lastAllocationOffset + length != segment.Used)
        {
            return false;
        }

        segment.SetUsed(_lastAllocationOffset);
        _length -= length;
        _lastAllocationSegment = null;
        _lastAllocationLength = 0;
        _lastAllocationOffset = 0;

        if (segment.Used == 0)
        {
            var previous = segment.Previous;
            if (previous is null)
                _head = null;
            else
                previous.DetachNext();

            _tail = previous;
            ReturnSegment(segment);
        }

        return true;
    }

    internal ReadOnlySequence<byte> AsReadOnlySequence()
    {
        if (_head is null || _tail is null || _length == 0)
            return ReadOnlySequence<byte>.Empty;

        for (var segment = _head; segment is not null; segment = segment.NextSegment)
            segment.CommitMemory();

        return new ReadOnlySequence<byte>(_head, 0, _tail, _tail.Used);
    }

    private void Reset(int batchSize)
    {
        _chunkSize = GetChunkSize(batchSize);
        _length = 0;
        _lastAllocationSegment = null;
        _lastAllocationOffset = 0;
        _lastAllocationLength = 0;
    }

    private static int GetChunkSize(int batchSize) =>
        Math.Clamp(Math.Min(MaximumChunkSize, batchSize), MinimumChunkSize, MaximumChunkSize);

    private static ChunkSegment RentSegment(int capacity)
    {
        if (!s_segments.TryPop(out var segment))
            segment = new ChunkSegment();

        segment.Initialize(s_chunkArrays.Pool.Rent(capacity));
        return segment;
    }

    private static void ReturnSegment(ChunkSegment segment)
    {
        var buffer = segment.Release();
        s_chunkArrays.Pool.Return(buffer, clearArray: false);
        s_segments.TryPush(segment);
    }

    private void ReturnChunks()
    {
        var segment = _head;
        while (segment is not null)
        {
            var next = segment.NextSegment;
            ReturnSegment(segment);
            segment = next;
        }

        _head = null;
        _tail = null;
        _length = 0;
        _lastAllocationSegment = null;
        _lastAllocationOffset = 0;
        _lastAllocationLength = 0;
    }

    private sealed class ChunkSegment : ReadOnlySequenceSegment<byte>
    {
        internal byte[] Buffer { get; private set; } = [];
        internal int Capacity => Buffer.Length;
        internal int Used { get; private set; }
        internal ChunkSegment? Previous { get; set; }
        internal ChunkSegment? NextSegment => (ChunkSegment?)Next;

        internal void Initialize(byte[] buffer)
        {
            Buffer = buffer;
            Used = 0;
            Previous = null;
            Next = null;
            RunningIndex = 0;
            Memory = buffer.AsMemory(0, 0);
        }

        internal void SetUsed(int used)
        {
            Used = used;
        }

        internal void CommitMemory() => Memory = Buffer.AsMemory(0, Used);

        internal void AttachNext(ChunkSegment next)
        {
            next.RunningIndex = RunningIndex + Used;
            Next = next;
        }

        internal void DetachNext() => Next = null;

        internal byte[] Release()
        {
            var buffer = Buffer;
            Buffer = [];
            Used = 0;
            Previous = null;
            Next = null;
            RunningIndex = 0;
            Memory = default;
            return buffer;
        }
    }
}
