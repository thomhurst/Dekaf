using Dekaf.Producer;

namespace Dekaf.Protocol;

/// <summary>
/// Wraps an <see cref="IPooledMemory"/> with reference counting so the underlying
/// pooled buffer is only returned when all consumers have released their reference.
/// </summary>
/// <remarks>
/// This is used when a single network buffer (IPooledMemory) is shared across multiple
/// <c>PendingFetchData</c> items from the same fetch response. Each item holds a reference
/// to this wrapper and calls <see cref="Dispose"/> independently. The underlying memory is
/// only released when the last reference is disposed, ensuring order-independent disposal safety.
/// </remarks>
internal sealed class RefCountedMemoryOwner : IPooledMemory
{
    private static readonly RefCountedMemoryOwnerPool s_pool = new();

    private IPooledMemory? _inner;
    private int _refCount;
    private int _pooled;

    private RefCountedMemoryOwner() { }

    public RefCountedMemoryOwner(IPooledMemory inner, int initialRefCount)
    {
        Initialize(inner, initialRefCount, pooled: false);
    }

    internal static RefCountedMemoryOwner Create(IPooledMemory inner, int initialRefCount)
    {
        var owner = s_pool.Rent();
        owner.Initialize(inner, initialRefCount, pooled: true);
        return owner;
    }

    private void Initialize(IPooledMemory inner, int initialRefCount, bool pooled)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(initialRefCount, 1);
        _inner = inner;
        _refCount = initialRefCount;
        _pooled = pooled ? 1 : 0;
    }

    public ReadOnlyMemory<byte> Memory => _inner?.Memory
        ?? throw new ObjectDisposedException(nameof(RefCountedMemoryOwner));

    public void Dispose()
    {
        var newCount = Interlocked.Decrement(ref _refCount);

        if (newCount == 0)
        {
            _inner?.Dispose();
            if (Volatile.Read(ref _pooled) != 0)
                s_pool.Return(this);
        }
        else if (newCount < 0)
        {
            // Already fully disposed — increment back to prevent further underflow.
            Interlocked.Increment(ref _refCount);
        }
    }

    private sealed class RefCountedMemoryOwnerPool() : ObjectPool<RefCountedMemoryOwner>(maxPoolSize: 128)
    {
        protected override RefCountedMemoryOwner Create() => new();

        protected override void Reset(RefCountedMemoryOwner item)
        {
            item._inner = null;
            item._refCount = 0;
            item._pooled = 0;
        }
    }
}
