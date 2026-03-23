using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Dekaf.Producer;

/// <summary>
/// Poolable linked list node tracking a single in-flight batch for a partition.
/// Supports lazy TCS creation for coordinated retry on OutOfOrderSequenceNumber.
/// </summary>
internal sealed class InflightEntry
{
    public TopicPartition TopicPartition { get; private set; }
    public int BaseSequence { get; private set; }
    public int RecordCount { get; private set; }

    internal InflightEntry? Previous { get; set; }
    internal InflightEntry? Next { get; set; }

    // Guard against double-return to pool when FailAll and Complete race.
    // Uses Interlocked.Exchange for atomic check-and-clear without requiring the partition lock.
    private volatile int _inList;

    internal bool InList
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _inList != 0;
    }

    /// <summary>
    /// Atomically marks the entry as removed from the list.
    /// Returns true if this call performed the removal (was in list), false if already removed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryRemoveFromList() => Interlocked.Exchange(ref _inList, 0) != 0;

    /// <summary>
    /// Marks the entry as in the list. Called under the partition SpinLock during Register.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void MarkInList() => _inList = 1;

    private TaskCompletionSource? _completionSignal;

    /// <summary>
    /// Initializes the entry for use. Called after renting from pool.
    /// </summary>
    public void Initialize(TopicPartition topicPartition, int baseSequence, int recordCount)
    {
        TopicPartition = topicPartition;
        BaseSequence = baseSequence;
        RecordCount = recordCount;
    }

    /// <summary>
    /// Gets or lazily creates the completion signal TCS.
    /// Only called on OutOfOrderSequenceNumber (failure path), so zero allocation in happy path.
    /// </summary>
    internal TaskCompletionSource GetOrCreateCompletionSignal()
    {
        return _completionSignal ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Signals that this entry's batch has completed (success or failure).
    /// Wakes up any successor waiting via WaitForPredecessorAsync.
    /// </summary>
    internal void SignalComplete()
    {
        _completionSignal?.TrySetResult();
    }

    /// <summary>
    /// Signals that this entry's batch has failed with an exception.
    /// Wakes up any successor waiting via WaitForPredecessorAsync.
    /// </summary>
    internal void SignalFailed(Exception exception)
    {
        if (_completionSignal is not null)
        {
            _completionSignal.TrySetException(exception);
        }
    }

    /// <summary>
    /// Resets the entry for pool return. Clears all state.
    /// </summary>
    public void Reset()
    {
        TopicPartition = default;
        BaseSequence = 0;
        RecordCount = 0;
        Previous = null;
        Next = null;
        _inList = 0;
        _completionSignal = null;
    }
}

/// <summary>
/// Per-partition state for the inflight tracker.
/// Uses a doubly-linked list of InflightEntry nodes with a SpinLock for synchronization.
/// SpinLock eliminates Monitor overhead (no kernel transition, no object header manipulation)
/// which is ideal for the very short critical sections here (~5-10 instructions of pointer manipulation).
/// </summary>
internal sealed class PartitionState
{
    // SpinLock instead of Monitor: avoids kernel transition and object allocation for the lock.
    // Critical sections are ~5-10 instructions (pointer manipulation only), so spinning is
    // significantly cheaper than blocking. trackThreadOwnership=false avoids Thread.CurrentThread
    // overhead on every Enter/Exit.
    public SpinLock Lock = new(enableThreadOwnerTracking: false);
    public InflightEntry? Head;
    public InflightEntry? Tail;
    public int Count;
}

/// <summary>
/// Lock-free pool for InflightEntry objects.
/// Extends <see cref="ObjectPool{T}"/> for pre-warm support and miss tracking.
/// </summary>
internal sealed class InflightEntryPool(int maxPoolSize = 128)
    : ObjectPool<InflightEntry>(maxPoolSize)
{
    protected override InflightEntry Create() => new();
    protected override void Reset(InflightEntry item) => item.Reset();
}

/// <summary>
/// Tracks in-flight batches per partition using a doubly-linked list.
/// Enables coordinated retry: when a batch gets OutOfOrderSequenceNumber,
/// it waits for its predecessor to complete rather than blind backoff.
///
/// Happy path cost: 1 pool rent + SpinLock enter/exit + 2 pointer writes + pool return = ~50ns.
/// Zero heap allocation in steady state (InflightEntry pooled, TCS lazy).
/// SpinLock replaces Monitor lock to eliminate 5.2% CPU overhead from Monitor.Wait contention.
/// </summary>
internal sealed class PartitionInflightTracker
{
    private readonly ConcurrentDictionary<TopicPartition, PartitionState> _partitions = new();
    private readonly InflightEntryPool _pool;

    public PartitionInflightTracker(InflightEntryPool? pool = null)
    {
        _pool = pool ?? new InflightEntryPool();
    }

    /// <summary>
    /// Registers a batch as in-flight. Rents an entry from the pool and appends to partition's tail.
    /// Called from the single-threaded drain loop, so registration order matches sequence order.
    /// </summary>
    public InflightEntry Register(TopicPartition topicPartition, int baseSequence, int recordCount)
    {
        var entry = _pool.Rent();
        entry.Initialize(topicPartition, baseSequence, recordCount);

        var state = _partitions.GetOrAdd(topicPartition, static _ => new PartitionState());

        var lockTaken = false;
        try
        {
            state.Lock.Enter(ref lockTaken);

            entry.MarkInList();

            if (state.Tail is null)
            {
                // Empty list
                state.Head = entry;
                state.Tail = entry;
            }
            else
            {
                // Append to tail
                entry.Previous = state.Tail;
                state.Tail.Next = entry;
                state.Tail = entry;
            }

            state.Count++;
        }
        finally
        {
            if (lockTaken) state.Lock.Exit(useMemoryBarrier: false);
        }

        return entry;
    }

    /// <summary>
    /// Marks a batch as complete. Removes from linked list, signals TCS if exists, returns to pool.
    /// </summary>
    public void Complete(InflightEntry entry)
    {
        if (!_partitions.TryGetValue(entry.TopicPartition, out var state))
        {
            return;
        }

        var lockTaken = false;
        try
        {
            state.Lock.Enter(ref lockTaken);

            // Guard against double-return when FailAll and Complete race.
            // TryRemoveFromList is atomic (Interlocked.Exchange), so exactly one
            // caller wins even if FailAll cleared InList before we acquired the lock.
            if (!entry.TryRemoveFromList())
            {
                return;
            }

            // Unlink from doubly-linked list
            if (entry.Previous is not null)
            {
                entry.Previous.Next = entry.Next;
            }
            else
            {
                // Entry is head
                state.Head = entry.Next;
            }

            if (entry.Next is not null)
            {
                entry.Next.Previous = entry.Previous;
            }
            else
            {
                // Entry is tail
                state.Tail = entry.Previous;
            }

            state.Count--;
        }
        finally
        {
            if (lockTaken) state.Lock.Exit(useMemoryBarrier: false);
        }

        // Signal completion outside lock to avoid holding lock during continuations
        entry.SignalComplete();

        _pool.Return(entry);
    }

    /// <summary>
    /// Waits for the predecessor batch to complete. If no predecessor exists, completes immediately.
    /// Only called on OutOfOrderSequenceNumber (failure path), so lazy TCS allocation is acceptable.
    /// </summary>
    public async ValueTask WaitForPredecessorAsync(InflightEntry entry, CancellationToken cancellationToken)
    {
        Task? predecessorTask = null;

        if (!_partitions.TryGetValue(entry.TopicPartition, out var state))
        {
            return;
        }

        var lockTaken = false;
        try
        {
            state.Lock.Enter(ref lockTaken);

            // If the entry was removed by FailAll (e.g. epoch bump cleared
            // all entries), return immediately. Without this check, entry.Previous
            // could reference a pooled/reset entry whose TCS would never be signaled.
            if (!entry.InList)
            {
                return;
            }

            var predecessor = entry.Previous;
            if (predecessor is null)
            {
                // No predecessor — nothing to wait for
                return;
            }

            // Lazy TCS creation on predecessor (failure path only)
            predecessorTask = predecessor.GetOrCreateCompletionSignal().Task;
        }
        finally
        {
            if (lockTaken) state.Lock.Exit(useMemoryBarrier: false);
        }

        // Await outside lock with ConfigureAwait(false)
        await predecessorTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Fails all in-flight entries for a partition. Signals all with exception, clears list, returns to pool.
    /// Used during fatal errors or producer shutdown.
    /// </summary>
    public void FailAll(TopicPartition topicPartition, Exception exception)
    {
        if (!_partitions.TryGetValue(topicPartition, out var state))
        {
            return;
        }

        List<InflightEntry> entries;

        var lockTaken = false;
        try
        {
            state.Lock.Enter(ref lockTaken);

            if (state.Head is null)
            {
                return;
            }

            // Collect all entries under lock, then process outside
            entries = new List<InflightEntry>(state.Count);
            var current = state.Head;
            while (current is not null)
            {
                current.TryRemoveFromList();
                entries.Add(current);
                current = current.Next;
            }

            // Clear list
            state.Head = null;
            state.Tail = null;
            state.Count = 0;
        }
        finally
        {
            if (lockTaken) state.Lock.Exit(useMemoryBarrier: false);
        }

        // Signal and return outside lock
        foreach (var entry in entries)
        {
            entry.SignalFailed(exception);
            _pool.Return(entry);
        }
    }

    /// <summary>
    /// Returns true if the entry is head-of-line (no predecessor) for its partition.
    /// Must be checked under the partition state SpinLock for consistency.
    /// Used by epoch bump recovery to determine if a batch can trigger the bump.
    /// </summary>
    public bool IsHeadOfLine(InflightEntry entry)
    {
        if (!_partitions.TryGetValue(entry.TopicPartition, out var state))
        {
            return true; // Not tracked — treat as head-of-line
        }

        var lockTaken = false;
        try
        {
            state.Lock.Enter(ref lockTaken);
            return entry.Previous is null;
        }
        finally
        {
            if (lockTaken) state.Lock.Exit(useMemoryBarrier: false);
        }
    }

    /// <summary>
    /// Gets the number of in-flight batches for a partition. Diagnostic/testing only.
    /// </summary>
    public int GetInflightCount(TopicPartition topicPartition)
    {
        if (!_partitions.TryGetValue(topicPartition, out var state))
        {
            return 0;
        }

        var lockTaken = false;
        try
        {
            state.Lock.Enter(ref lockTaken);
            return state.Count;
        }
        finally
        {
            if (lockTaken) state.Lock.Exit(useMemoryBarrier: false);
        }
    }
}
