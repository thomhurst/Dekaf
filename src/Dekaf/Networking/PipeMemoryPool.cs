using System.Buffers;

namespace Dekaf.Networking;

/// <summary>
/// A <see cref="MemoryPool{T}"/> backed by a dedicated <see cref="ArrayPool{T}"/>
/// (not the shared instance). When disposed, all arrays that have been returned to
/// this pool are dropped — the underlying <see cref="ArrayPool{T}"/> becomes eligible
/// for GC along with all its retained buffers.
/// <para/>
/// <b>Why not <see cref="MemoryPool{T}.Shared"/>?</b>
/// <c>MemoryPool&lt;byte&gt;.Shared</c> wraps <c>ArrayPool&lt;byte&gt;.Shared</c>, which
/// is a <c>TlsOverPerCoreLockedStacksArrayPool</c>. It retains returned arrays in
/// per-thread (TLS) and per-core stacks that grow to accommodate the access pattern
/// but never shrink. With multiple Kafka broker connections, each connection's read
/// pump thread adds to the shared pool's retained set. Over time this causes continuous
/// WorkingSet growth proportional to the number of brokers — even though individual
/// connections properly return their buffers.
/// <para/>
/// By giving each connection its own <see cref="PipeMemoryPool"/>, the retained arrays
/// are scoped to the connection's lifetime. When the connection is disposed, the pool
/// is disposed and all retained memory becomes eligible for GC collection, preventing
/// the cross-connection accumulation that causes WorkingSet growth in multi-broker
/// producer scenarios.
/// </summary>
internal sealed class PipeMemoryPool : MemoryPool<byte>
{
    private readonly ArrayPool<byte> _pool;
    private int _disposed;

    /// <summary>
    /// Creates a new pool with a dedicated <see cref="ArrayPool{T}"/> instance.
    /// </summary>
    /// <param name="maxArrayLength">Maximum size of arrays that the pool will cache.
    /// Larger requests fall through to new allocations. Defaults to 4 MB to cover
    /// ProduceRequests with 1 MB batches plus header/framing overhead, and coalesced
    /// multi-batch requests.</param>
    /// <param name="maxArraysPerBucket">Maximum number of arrays to retain per size bucket.
    /// Must be large enough to cover the concurrent segment demand from both the input
    /// pipe (read pump creating ~64 KB segments that stay alive until AdvanceTo) and the
    /// output PipeWriter (large GetMemory calls for serialized requests). With pipelined
    /// ProduceResponses (idempotent producers), the input pipe can hold 16-32 active
    /// segments simultaneously. Defaults to 32 to prevent pool overflow allocations under
    /// high-throughput pipelining.</param>
    public PipeMemoryPool(int maxArrayLength = 4 * 1024 * 1024, int maxArraysPerBucket = 32)
    {
        _pool = ArrayPool<byte>.Create(maxArrayLength, maxArraysPerBucket);
    }

    // Returns int.MaxValue to match MemoryPool<byte>.Shared behavior.
    // System.IO.Pipelines does not rely on MaxBufferSize for sizing decisions;
    // actual caching is bounded by the maxArrayLength (4 MB) passed to ArrayPool.Create.
    public override int MaxBufferSize => int.MaxValue;

    public override IMemoryOwner<byte> Rent(int minBufferSize = -1)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        if (minBufferSize < 0)
            minBufferSize = 4096;

        return new PooledMemoryOwner(_pool, minBufferSize);
    }

    protected override void Dispose(bool disposing)
    {
        // Mark as disposed. The underlying ArrayPool<byte>.Create() instance
        // has no Dispose method — it will be collected by the GC along with all
        // its retained arrays once no more IMemoryOwner references are alive.
        Volatile.Write(ref _disposed, 1);
    }

    /// <summary>
    /// Lightweight <see cref="IMemoryOwner{T}"/> that rents from and returns to
    /// the parent pool's dedicated <see cref="ArrayPool{T}"/>.
    /// </summary>
    private sealed class PooledMemoryOwner : IMemoryOwner<byte>
    {
        private readonly ArrayPool<byte> _pool;
        private byte[]? _array;

        public PooledMemoryOwner(ArrayPool<byte> pool, int minBufferSize)
        {
            _pool = pool;
            _array = pool.Rent(minBufferSize);
        }

        public Memory<byte> Memory
        {
            get
            {
                var array = _array;
                ObjectDisposedException.ThrowIf(array is null, this);
                return array;
            }
        }

        public void Dispose()
        {
            var array = Interlocked.Exchange(ref _array, null);
            if (array is not null)
            {
                _pool.Return(array);
            }
        }
    }
}
