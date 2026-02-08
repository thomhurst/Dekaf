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
    // Set to true by Register, checked and cleared by Complete/FailAll under lock.
    internal bool InList { get; set; }

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
        InList = false;
        _completionSignal = null;
    }
}

/// <summary>
/// Per-partition state for the inflight tracker.
/// Uses a doubly-linked list of InflightEntry nodes.
/// </summary>
internal sealed class PartitionState
{
    public readonly object Lock = new();
    public InflightEntry? Head;
    public InflightEntry? Tail;
    public int Count;
}

/// <summary>
/// Lock-free pool for InflightEntry objects, following ReadyBatchPool pattern.
/// Uses ConcurrentStack for thread-safe rent/return without locks.
/// </summary>
internal sealed class InflightEntryPool
{
    private readonly ConcurrentStack<InflightEntry> _pool = new();
    private readonly int _maxPoolSize;

    public InflightEntryPool(int maxPoolSize = 128)
    {
        _maxPoolSize = maxPoolSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public InflightEntry Rent()
    {
        if (_pool.TryPop(out var entry))
        {
            return entry;
        }

        return new InflightEntry();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(InflightEntry entry)
    {
        entry.Reset();
        if (_pool.Count < _maxPoolSize)
        {
            _pool.Push(entry);
        }
    }
}

/// <summary>
/// Tracks in-flight batches per partition using a doubly-linked list.
/// Enables coordinated retry: when a batch gets OutOfOrderSequenceNumber,
/// it waits for its predecessor to complete rather than blind backoff.
///
/// Happy path cost: 1 pool rent + lock + 2 pointer writes + unlock + pool return = ~100ns.
/// Zero heap allocation in steady state (InflightEntry pooled, TCS lazy).
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

        lock (state.Lock)
        {
            entry.InList = true;

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

        lock (state.Lock)
        {
            // Guard against double-return when FailAll and Complete race
            if (!entry.InList)
            {
                return;
            }

            entry.InList = false;

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

        lock (state.Lock)
        {
            var predecessor = entry.Previous;
            if (predecessor is null)
            {
                // No predecessor — nothing to wait for
                return;
            }

            // Lazy TCS creation on predecessor (failure path only)
            predecessorTask = predecessor.GetOrCreateCompletionSignal().Task;
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

        lock (state.Lock)
        {
            if (state.Head is null)
            {
                return;
            }

            // Collect all entries under lock, then process outside
            entries = new List<InflightEntry>(state.Count);
            var current = state.Head;
            while (current is not null)
            {
                current.InList = false;
                entries.Add(current);
                current = current.Next;
            }

            // Clear list
            state.Head = null;
            state.Tail = null;
            state.Count = 0;
        }

        // Signal and return outside lock
        foreach (var entry in entries)
        {
            entry.SignalFailed(exception);
            _pool.Return(entry);
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

        lock (state.Lock)
        {
            return state.Count;
        }
    }
}
