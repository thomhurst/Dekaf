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
    private readonly IPooledMemory _inner;
    private int _refCount;

    public RefCountedMemoryOwner(IPooledMemory inner, int initialRefCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(initialRefCount, 1);
        _inner = inner;
        _refCount = initialRefCount;
    }

    public ReadOnlyMemory<byte> Memory => _inner.Memory;

    public void Dispose()
    {
        var newCount = Interlocked.Decrement(ref _refCount);

        if (newCount == 0)
        {
            _inner.Dispose();
        }
        else if (newCount < 0)
        {
            // Already fully disposed — increment back to prevent further underflow.
            Interlocked.Increment(ref _refCount);
        }
    }
}
