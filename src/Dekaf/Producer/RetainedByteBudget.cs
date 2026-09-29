namespace Dekaf.Producer;

/// <summary>
/// Byte ceiling for idle buffers held by a pool whose items vary in size.
/// </summary>
/// <remarks>
/// A count-bounded pool shared by producers with different batch sizes can fill slots sized
/// for small arenas with large ones. Reserving each item's bytes before it is pooled keeps the
/// idle total within the budget of the live producers, whatever the item sizes.
/// A limit of 0 means no producer has set a budget; bytes are still tracked, and the
/// pool's count bound applies alone. Reserve and release run once per pooled item, never per record.
/// </remarks>
internal sealed class RetainedByteBudget
{
    private long _retainedBytes;
    private long _limit;

    /// <summary>Bytes currently reserved by pooled items.</summary>
    public long RetainedBytes => Volatile.Read(ref _retainedBytes);

    /// <summary>Current byte limit, or 0 when none has been set.</summary>
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

    /// <summary>Releases bytes reserved by an item that left the pool.</summary>
    public void Release(int bytes)
    {
        if (bytes != 0)
            Interlocked.Add(ref _retainedBytes, -bytes);
    }
}
