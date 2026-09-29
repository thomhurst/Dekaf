namespace Dekaf.Producer;

/// <summary>
/// Byte ceiling for idle buffers held by a pool whose items vary in size.
/// </summary>
/// <remarks>
/// A count-bounded pool shared by producers with different batch sizes can fill slots sized
/// for small arenas with large ones. Reserving each item's bytes before it is pooled keeps the
/// idle total within the budget of the live producers, whatever the item sizes.
/// A limit of 0 means no producer has set a budget; bytes are still tracked, and the
/// pool's count bound applies alone. <see cref="RetainNone"/> rejects every non-empty item. Reserve and release run once per pooled item, never per record.
/// </remarks>
internal sealed class RetainedByteBudget
{
    /// <summary>Limit that rejects every non-empty item, used once no producer needs arenas.</summary>
    public const long RetainNone = -1;

    private long _retainedBytes;
    private long _limit;

    /// <summary>Bytes currently reserved by pooled items.</summary>
    public long RetainedBytes => Volatile.Read(ref _retainedBytes);

    /// <summary>Current byte limit: 0 when none has been set, <see cref="RetainNone"/> to keep nothing.</summary>
    public long Limit => Volatile.Read(ref _limit);

    /// <summary>
    /// Sets the byte limit. Lowering it does not evict pooled items; later reservations fail
    /// until releases bring the total under the new limit.
    /// </summary>
    public void SetLimit(long bytes) => Volatile.Write(ref _limit, bytes);

    /// <summary>
    /// Reserves <paramref name="bytes"/> for an item about to be pooled.
    /// Returns false, reserving nothing, when the item would exceed the limit.
    /// </summary>
    public bool TryReserve(int bytes)
    {
        if (bytes == 0)
            return true;

        var total = Interlocked.Add(ref _retainedBytes, bytes);
        var limit = Volatile.Read(ref _limit);
        if (limit == 0 || total <= limit)
            return true;

        Interlocked.Add(ref _retainedBytes, -bytes);
        return false;
    }

    /// <summary>
    /// Reserves <paramref name="bytes"/> and creates the item that will hold them. Returns null,
    /// reserving nothing, when the limit is reached. If <paramref name="create"/> throws (for
    /// example, a pinned allocation running out of memory), the reservation is released, since
    /// no pooled item would ever release it.
    /// </summary>
    public T? ReserveThenCreate<T>(int bytes, Func<int, T> create)
        where T : class
    {
        if (!TryReserve(bytes))
            return null;

        try
        {
            return create(bytes);
        }
        catch
        {
            Release(bytes);
            throw;
        }
    }

    /// <summary>Releases bytes reserved by an item that left the pool.</summary>
    public void Release(int bytes)
    {
        if (bytes != 0)
            Interlocked.Add(ref _retainedBytes, -bytes);
    }
}
