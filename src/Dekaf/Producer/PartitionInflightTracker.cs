using System.Collections.Concurrent;
using System.Diagnostics;
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

    /// <summary>
    /// Per-partition state, stored at registration to avoid dictionary lookup races with pruning.
    /// </summary>
    internal PartitionState? State { get; set; }

    internal InflightEntry? Previous { get; set; }
    internal InflightEntry? Next { get; set; }

    // Guard against double-return to pool when FailAll and Complete race.
    // Protected by the per-partition SpinLock — both Complete and FailAll acquire
    // the same lock before checking/clearing this flag, so plain bool is sufficient.
    internal bool InList;

    /// <summary>
    /// Checks and clears the InList flag under the partition lock.
    /// Returns true if this call performed the removal (was in list), false if already removed.
    /// Must be called while holding the partition SpinLock.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryRemoveFromList()
    {
        if (!InList) return false;
        InList = false;
        return true;
    }

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
        State = null;
        Previous = null;
        Next = null;
        InList = false;
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
    // Do not copy this field — SpinLock is a mutable struct. Always access via the PartitionState reference.
    internal SpinLock Lock = new(enableThreadOwnerTracking: false);
    public InflightEntry? Head;
    public InflightEntry? Tail;
    public int Count;

    /// <summary>
    /// Stopwatch ticks when this partition last became idle (Count dropped to 0).
    /// Set via Volatile.Write in Complete/FailAll; read by the pruning timer.
    /// Zero means the partition is active (has in-flight entries).
    /// </summary>
    public long LastIdleTicks;
}

/// <summary>
/// Lock-free pool for InflightEntry objects.
/// Extends <see cref="ObjectPool{T}"/> for pre-warm support and miss tracking.
/// Default size 1024: with MaxInFlightRequestsPerConnection=5 and coalesced batches
/// containing 10-50 partitions each, the steady-state in-flight count can reach
/// 5 * 50 = 250 entries per connection. Multiple BrokerSenders multiply this further.
/// The previous size of 128 caused frequent pool misses under high-throughput idempotent
/// workloads, creating mid-lived InflightEntry objects that survived Gen0 and got
/// promoted to Gen2 before dying — contributing to pathological Gen2 GC pressure.
/// </summary>
internal sealed class InflightEntryPool(int maxPoolSize = 1024)
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
///
/// Idle partition states are pruned by a background timer (5-minute TTL) to prevent
/// unbounded dictionary growth in producers with dynamic topic routing.
/// </summary>
internal sealed class PartitionInflightTracker : IDisposable
{
    private static readonly long PruneTtlTicks = Stopwatch.Frequency * 300; // 5 minutes
    private static readonly TimeSpan PruneInterval = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<TopicPartition, PartitionState> _partitions = new();
    private readonly InflightEntryPool _pool;
    private readonly Timer? _pruneTimer;

    public PartitionInflightTracker(InflightEntryPool? pool = null)
    {
        _pool = pool ?? new InflightEntryPool();
        _pruneTimer = new Timer(
            static state => ((PartitionInflightTracker)state!).PruneStalePartitions(),
            this,
            PruneInterval,
            PruneInterval);
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
        entry.State = state;

        var lockTaken = false;
        try
        {
            state.Lock.Enter(ref lockTaken);

            entry.InList = true;

            // Clear idle timestamp — partition is now active
            Volatile.Write(ref state.LastIdleTicks, 0);

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
            if (lockTaken) state.Lock.Exit();
        }

        return entry;
    }

    /// <summary>
    /// Marks a batch as complete. Removes from linked list, signals TCS if exists, returns to pool.
    /// Uses the stored PartitionState reference to avoid dictionary lookup races with pruning.
    /// </summary>
    public void Complete(InflightEntry entry)
    {
        var state = entry.State;
        if (state is null)
        {
            return;
        }

        var becameIdle = false;
        var lockTaken = false;
        try
        {
            state.Lock.Enter(ref lockTaken);

            // Guard against double-return when FailAll and Complete race.
            // Both Complete and FailAll acquire the same SpinLock, so TryRemoveFromList
            // is already serialised: exactly one caller will see InList == true.
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
            becameIdle = state.Count == 0;
        }
        finally
        {
            if (lockTaken) state.Lock.Exit();
        }

        // Written outside the lock (unlike Register's write inside the lock) because
        // becameIdle was already determined atomically with Count-- under the SpinLock.
        if (becameIdle)
        {
            Volatile.Write(ref state.LastIdleTicks, Stopwatch.GetTimestamp());
        }

        // Signal completion outside lock to avoid holding lock during continuations
        entry.SignalComplete();

        _pool.Return(entry);
    }

    /// <summary>
    /// Waits for the predecessor batch to complete. If no predecessor exists, completes immediately.
    /// Only called on OutOfOrderSequenceNumber (failure path), so lazy TCS allocation is acceptable.
    /// Uses the stored PartitionState reference to avoid dictionary lookup races with pruning.
    /// </summary>
    public async ValueTask WaitForPredecessorAsync(InflightEntry entry, CancellationToken cancellationToken)
    {
        Task? predecessorTask = null;

        var state = entry.State;
        if (state is null)
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
            if (lockTaken) state.Lock.Exit();
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
            if (lockTaken) state.Lock.Exit();
        }

        Volatile.Write(ref state.LastIdleTicks, Stopwatch.GetTimestamp());

        // Signal and return outside lock
        foreach (var entry in entries)
        {
            entry.SignalFailed(exception);
            _pool.Return(entry);
        }
    }

    /// <summary>
    /// Returns true if the entry is head-of-line (no predecessor) for its partition.
    /// Uses the stored PartitionState reference to avoid dictionary lookup races with pruning.
    /// Used by epoch bump recovery to determine if a batch can trigger the bump.
    /// </summary>
    public bool IsHeadOfLine(InflightEntry entry)
    {
        var state = entry.State;
        if (state is null)
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
            if (lockTaken) state.Lock.Exit();
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
            if (lockTaken) state.Lock.Exit();
        }
    }

    /// <summary>
    /// Gets the number of tracked partitions in the dictionary. Diagnostic/testing only.
    /// </summary>
    public int GetTrackedPartitionCount() => _partitions.Count;

    /// <summary>
    /// Prunes partition states that have been idle for longer than the TTL.
    /// Called by the background timer every 5 minutes.
    /// </summary>
    private void PruneStalePartitions()
    {
        PruneWithCutoff(Stopwatch.GetTimestamp() - PruneTtlTicks);
    }

    /// <summary>
    /// Prunes partition states idle before <paramref name="cutoffTicks"/>.
    /// Exposed for deterministic testing — production code uses <see cref="PruneStalePartitions"/>.
    /// </summary>
    internal void PruneWithCutoff(long cutoffTicks)
    {
        foreach (var kvp in _partitions)
        {
            var state = kvp.Value;
            var lastIdle = Volatile.Read(ref state.LastIdleTicks);

            // Skip active partitions (LastIdleTicks == 0 means in-flight entries exist)
            // or partitions that became idle after the cutoff.
            if (lastIdle == 0 || lastIdle > cutoffTicks)
            {
                continue;
            }

            // Double-check under lock: a concurrent Register may have reactivated this partition.
            var shouldRemove = false;
            var lockTaken = false;
            try
            {
                state.Lock.Enter(ref lockTaken);

                if (state.Count != 0)
                {
                    continue;
                }

                // Re-read inside the lock in case Register cleared it concurrently
                lastIdle = Volatile.Read(ref state.LastIdleTicks);
                if (lastIdle == 0 || lastIdle > cutoffTicks)
                {
                    continue;
                }

                shouldRemove = true;
            }
            finally
            {
                if (lockTaken) state.Lock.Exit();
            }

            // Remove outside the SpinLock to avoid nesting SpinLock + ConcurrentDictionary's
            // internal lock. Uses the KeyValuePair overload so removal only succeeds if the
            // dictionary still holds the same PartitionState reference — prevents a TOCTOU race
            // where a concurrent Register reuses the same key with the existing state object.
            if (shouldRemove)
            {
                _partitions.TryRemove(kvp);
            }
        }
    }

    public void Dispose()
    {
        _pruneTimer?.Dispose();
    }
}
