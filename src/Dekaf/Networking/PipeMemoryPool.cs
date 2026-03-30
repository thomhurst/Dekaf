using System.Buffers;
using System.Collections.Concurrent;

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
/// <b>Shared pool design:</b> A single <see cref="PipeMemoryPool"/> is shared across all
/// connections managed by a <see cref="ConnectionPool"/>. This bounds total retained memory
/// to one set of array buckets (maxArraysPerBucket × bucketCount) regardless of how many
/// connections exist. Without sharing, each connection independently retained up to
/// <c>maxArraysPerBucket</c> arrays per size class — with 3 brokers × 10 connections/broker
/// = 30 independent pools, each retaining up to 32 arrays in large buckets, causing
/// multi-GB WorkingSet growth. With a shared pool, the same 32 array slots are recycled
/// across all connections, capping total retention at ~128 MB for the 4 MB bucket.
/// <para/>
/// Connections created outside a <see cref="ConnectionPool"/> (e.g., in tests) fall back
/// to creating their own per-connection pool for backward compatibility.
/// </summary>
internal sealed class PipeMemoryPool : MemoryPool<byte>
{
    private readonly ArrayPool<byte> _pool;
    private readonly ConcurrentStack<PooledMemoryOwner> _ownerPool = new();
    private int _ownerPoolCount;
    private int _disposed;

    /// <summary>
    /// Maximum number of <see cref="PooledMemoryOwner"/> wrapper objects to retain.
    /// With pipelined sends, the input pipe can hold 16-32 active segments and the output
    /// PipeWriter uses 1-2 segments per write. 64 covers peak concurrent demand with headroom
    /// for burst scenarios, while bounding retained wrapper objects to ~2 KB total.
    /// </summary>
    private const int MaxOwnerPoolSize = 64;

    /// <summary>
    /// Creates a new pool with a dedicated <see cref="ArrayPool{T}"/> instance.
    /// </summary>
    /// <param name="maxArrayLength">Maximum size of arrays that the pool will cache.
    /// Larger requests fall through to new allocations. Defaults to 4 MB to cover
    /// ProduceRequests with the default 1 MB batch size (see <c>ProducerOptions.BatchSize</c>)
    /// plus header/framing overhead, and coalesced multi-batch requests. If BatchSize is
    /// increased beyond ~3.5 MB, this value should be increased accordingly.</param>
    /// <param name="maxArraysPerBucket">Maximum number of arrays to retain per size bucket.
    /// Must be large enough to cover the concurrent segment demand from both the input
    /// pipe (read pump creating ~64 KB segments that stay alive until AdvanceTo) and the
    /// output PipeWriter (large GetMemory calls for serialized requests). With pipelined
    /// ProduceResponses (idempotent producers), the input pipe can hold 16-32 active
    /// segments simultaneously. Defaults to 32 to prevent pool overflow allocations under
    /// high-throughput pipelining.
    /// <para/>
    /// <b>Memory tradeoff:</b> <c>ArrayPool.Create()</c> applies the same bucket count to all
    /// size classes. <c>ConfigurableArrayPool</c> pre-allocates a fixed-size reference array per
    /// bucket (32 slots), but the actual <c>byte[]</c> arrays are only allocated when rented and
    /// cached when returned — idle buckets hold null slots, not memory. In practice, only the
    /// small buckets (64 KB) fill to capacity; the large buckets (4 MB) rarely cache more than
    /// 1-2 arrays because there are far fewer concurrent large allocations. Peak per-connection
    /// retention is bounded by connection lifetime — when the connection is disposed, all retained
    /// arrays become GC-eligible.</param>
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

        if (_ownerPool.TryPop(out var owner))
        {
            // Note: the decrement happens after the pop, so _ownerPoolCount can transiently
            // overstate the actual number of pooled items. This is an acceptable approximation —
            // the count is only used to bound pool growth, not for correctness.
            Interlocked.Decrement(ref _ownerPoolCount);
            owner.Initialize(minBufferSize);
            return owner;
        }

        return new PooledMemoryOwner(this, minBufferSize);
    }

    /// <summary>
    /// Returns a <see cref="PooledMemoryOwner"/> wrapper to the pool for reuse.
    /// Called from <see cref="PooledMemoryOwner.Dispose"/> after the byte array has been
    /// returned to the <see cref="ArrayPool{T}"/>. Eliminates per-Rent() heap allocation
    /// of the wrapper object, which was a significant source of GC pressure with pipelined
    /// sends (hundreds of Rent/Dispose cycles per second per connection).
    /// </summary>
    private void ReturnOwner(PooledMemoryOwner owner)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        if (Interlocked.Increment(ref _ownerPoolCount) <= MaxOwnerPoolSize)
        {
            _ownerPool.Push(owner);

            // Guard against dispose racing between the check above and the push.
            // If Dispose() ran between our initial check and the Push, the drain loop
            // in Dispose may have already completed — re-drain to avoid leaking the owner.
            if (Volatile.Read(ref _disposed) != 0)
            {
                while (_ownerPool.TryPop(out _)) { }
            }
        }
        else
        {
            Interlocked.Decrement(ref _ownerPoolCount);
        }
    }

    protected override void Dispose(bool disposing)
    {
        // Mark as disposed. The underlying ArrayPool<byte>.Create() instance
        // has no Dispose method — it will be collected by the GC along with all
        // its retained arrays once no more IMemoryOwner references are alive.
        Volatile.Write(ref _disposed, 1);

        // Drain the owner pool to allow GC of wrapper objects
        while (_ownerPool.TryPop(out _))
        {
            // Discard
        }
    }

    /// <summary>
    /// Lightweight <see cref="IMemoryOwner{T}"/> that rents from and returns to
    /// the parent pool's dedicated <see cref="ArrayPool{T}"/>. The wrapper object itself
    /// is pooled by <see cref="PipeMemoryPool"/> to eliminate per-Rent() heap allocation.
    /// </summary>
    private sealed class PooledMemoryOwner : IMemoryOwner<byte>
    {
        private readonly PipeMemoryPool _parentPool;
        private byte[]? _array;

        public PooledMemoryOwner(PipeMemoryPool parentPool, int minBufferSize)
        {
            _parentPool = parentPool;
            _array = parentPool._pool.Rent(minBufferSize);
        }

        /// <summary>
        /// Re-initializes the owner for reuse after being returned to the wrapper pool.
        /// </summary>
        public void Initialize(int minBufferSize)
        {
            _array = _parentPool._pool.Rent(minBufferSize);
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
                _parentPool._pool.Return(array);
                _parentPool.ReturnOwner(this);
            }
        }
    }
}
