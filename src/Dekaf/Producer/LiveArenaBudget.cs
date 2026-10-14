namespace Dekaf.Producer;

/// <summary>
/// Counts the bytes of batch arenas held by open and in-flight batches (live arenas) against an
/// allowance derived from the memory available to the GC.
/// </summary>
/// <remarks>
/// <para>
/// BufferMemory counts record bytes only, and is released when a batch is written to the socket,
/// while each batch pins a whole arena (BatchSize + 12.5% by default) until the broker
/// acknowledges it. With records spread over many partitions, live arenas can fill the GC heap
/// while BufferMemory stays nearly empty. Arenas are pinned and allocated without zeroing, so
/// their untouched pages do not show in RSS, but they still count against a container's GC heap
/// hard limit.
/// </para>
/// <para>
/// Arenas within the allowance cost nothing beyond this counter. A producer that creates a batch
/// while live arenas exceed the allowance charges that arena's capacity against its own
/// BufferMemory until the arena is released, so the existing BufferMemory backpressure bounds the
/// excess. <see cref="Acquire"/> and <see cref="Release"/> run once per batch, never per record.
/// </para>
/// </remarks>
internal sealed class LiveArenaBudget
{
    /// <summary>
    /// Live arenas may use 1/<c>AvailableMemoryDivisor</c> of the memory available to the GC
    /// before batch creation is charged against BufferMemory. Idle arenas are bounded separately,
    /// to 1/10 of it, by the arena pool.
    /// </summary>
    internal const int AvailableMemoryDivisor = 2;

    /// <summary>The budget shared by every producer in the process: the GC heap is process-wide.</summary>
    /// <remarks>
    /// Every producer construction overwrites the limit through
    /// <see cref="SetLimitFromAvailableMemory"/>, last writer wins. That is safe because the
    /// input is process-wide (the memory available to the GC, the same for every producer), so
    /// each write stores the same value, give or take changes to the GC limit itself, which the
    /// latest construction then picks up. Nothing per producer feeds the limit; per-producer
    /// bounds come from each producer's own BufferMemory.
    /// </remarks>
    internal static LiveArenaBudget Shared { get; } = new(long.MaxValue);

    private long _liveBytes;
    private long _limit;

    internal LiveArenaBudget(long limit)
    {
        _limit = limit;
    }

    /// <summary>Bytes of arenas currently held by live batches.</summary>
    public long LiveBytes => Volatile.Read(ref _liveBytes);

    /// <summary>Live arena bytes allowed before new batches are charged against BufferMemory.</summary>
    public long Limit => Volatile.Read(ref _limit);

    /// <summary>
    /// Allowance for <paramref name="availableMemoryBytes"/> (0, unknown, means no allowance limit).
    /// </summary>
    internal static long ComputeLimit(long availableMemoryBytes)
        => availableMemoryBytes > 0 ? availableMemoryBytes / AvailableMemoryDivisor : long.MaxValue;

    /// <summary>
    /// Sets the allowance from the memory available to the GC. Runs once per producer
    /// construction; every producer computes the same process-wide value.
    /// </summary>
    internal void SetLimitFromAvailableMemory(long availableMemoryBytes)
        => Volatile.Write(ref _limit, ComputeLimit(availableMemoryBytes));

    /// <summary>
    /// Counts an arena of <paramref name="bytes"/> as live. Returns false when live arenas now
    /// exceed the allowance, in which case the caller charges the arena against BufferMemory.
    /// The bytes are counted either way and must be passed to <see cref="Release"/>.
    /// </summary>
    public bool Acquire(int bytes)
        => Interlocked.Add(ref _liveBytes, bytes) <= Volatile.Read(ref _limit);

    /// <summary>Stops counting an arena passed to <see cref="Acquire"/>.</summary>
    public void Release(int bytes) => Interlocked.Add(ref _liveBytes, -bytes);
}
