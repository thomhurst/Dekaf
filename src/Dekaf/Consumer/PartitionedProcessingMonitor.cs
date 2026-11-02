namespace Dekaf.Consumer;

/// <summary>
/// Reports in-flight work of a running partitioned consumer, for health checks and liveness probes.
/// </summary>
/// <remarks>
/// <para>
/// Pass an instance through <see cref="PartitionedProcessingOptions.Monitor"/> and read it from any
/// thread while the run is active. One monitor observes one run at a time; starting a second run
/// with a monitor that is still attached throws <see cref="InvalidOperationException"/>.
/// </para>
/// <para>
/// Monitoring costs one timestamp per partition per consumed batch and nothing per record. Runs
/// without a monitor record no timestamps.
/// </para>
/// </remarks>
public sealed class PartitionedProcessingMonitor
{
    private readonly List<IOutstandingRecordSource> _sources = [];
    private int _attached;

    /// <summary>
    /// Gets the time the runtime received the oldest record that is buffered or being processed
    /// and has not been marked processed, or <see langword="null"/> when no record is outstanding
    /// or no run is attached.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A record is outstanding from the moment the runtime receives the batch that contains it
    /// until it and every earlier record of its partition are marked processed. Record and batch
    /// handlers mark records processed when they return. A <see cref="PartitionProcessor{TKey, TValue}"/>
    /// marks them through <see cref="PartitionProcessorContext{TKey, TValue}.MarkProcessed"/>; records it never
    /// marks stay outstanding until their partition stops.
    /// </para>
    /// <para>
    /// Unlike a consumer-wide "last progress" time, this value does not advance while one
    /// partition is stuck and others keep processing. Records of a partition count until its lane
    /// stops: after revocation, loss, failure, or shutdown, once the processor exits or
    /// <see cref="PartitionedProcessingOptions.StopTimeout"/> elapses.
    /// </para>
    /// <para>
    /// Each partition keeps at most 1,024 batch arrival times. When more batches wait behind one
    /// unfinished record, neighbouring batches share the earlier time, so the value can be earlier
    /// than a record's actual arrival but never later.
    /// </para>
    /// <para>Reading the value does not allocate and never blocks the consumer's routing loop.</para>
    /// </remarks>
    public DateTimeOffset? OldestOutstandingRecordAt
    {
        get
        {
            var oldest = long.MaxValue;
            lock (_sources)
            {
                for (var i = 0; i < _sources.Count; i++)
                {
                    if (_sources[i].TryGetOldestOutstandingUtcTicks(out var ticks) && ticks < oldest)
                        oldest = ticks;
                }
            }

            return oldest == long.MaxValue ? null : new DateTimeOffset(oldest, TimeSpan.Zero);
        }
    }

    internal void Attach()
    {
        if (Interlocked.CompareExchange(ref _attached, 1, 0) != 0)
            throw new InvalidOperationException("The partitioned processing monitor is already attached to a running consumer.");
    }

    internal void Detach()
    {
        lock (_sources)
            _sources.Clear();
        Volatile.Write(ref _attached, 0);
    }

    internal void Register(IOutstandingRecordSource source)
    {
        lock (_sources)
            _sources.Add(source);
    }

    internal void Unregister(IOutstandingRecordSource source)
    {
        lock (_sources)
            _sources.Remove(source);
    }
}

/// <summary>A partition lane whose outstanding records a <see cref="PartitionedProcessingMonitor"/> reports.</summary>
internal interface IOutstandingRecordSource
{
    bool TryGetOldestOutstandingUtcTicks(out long utcTicks);
}

/// <summary>
/// Arrival times of one lane's records, one entry per consumed batch. Each entry holds the last
/// offset the lane queued from a batch and the time the runtime received the batch. The oldest
/// outstanding record's arrival time is that of the first entry whose last offset is at or after
/// the lane's first unprocessed offset, so completed batches and offset gaps between batches are
/// never reported.
/// </summary>
/// <remarks>
/// <para>
/// The runtime loop is the only writer. It publishes every change under a sequence counter, so a
/// reader never blocks the writer: a reader that overlaps a change retries.
/// </para>
/// <para>
/// The writer prunes completed entries only when the ring is full, so pruning is amortized. While
/// an unfinished record holds the completion frontier, completed entries behind it cannot be
/// pruned. The ring then grows up to <see cref="MaximumCapacity"/> entries and afterwards merges
/// adjacent entries, keeping the earlier arrival time. Memory stays bounded, and a reported
/// arrival time can be earlier, never later, than the actual one.
/// </para>
/// </remarks>
internal sealed class OutstandingRecordTimeline
{
    internal const int InitialCapacity = 8;
    internal const int MaximumCapacity = 1024;

    // The tail entry's batch may still be queuing records; it ends at the lane's enqueue position.
    private const long OpenLastOffset = long.MaxValue;

    private Entry[] _entries = new Entry[InitialCapacity];
    private int _head;
    private int _count;
    private int _sequence;

    /// <summary>True when the next append must prune, grow or merge. Read only by the writer.</summary>
    public bool IsFull => _count == _entries.Length;

    /// <summary>The ring's current capacity. Read only by the writer and tests.</summary>
    internal int Capacity => _entries.Length;

    /// <summary>
    /// Appends a batch received at <paramref name="utcTicks"/> before any of its records is queued,
    /// and closes the previous batch at <paramref name="lastEnqueuedOffset"/>, the last offset
    /// queued before it.
    /// </summary>
    public void Append(long lastEnqueuedOffset, long utcTicks)
    {
        BeginWrite();
        var entries = _entries;
        if (_count == entries.Length)
        {
            if (entries.Length < MaximumCapacity)
                entries = Grow(entries);
            else
                Merge(entries);
        }

        if (_count > 0)
            entries[(_head + _count - 1) % entries.Length].LastOffset = lastEnqueuedOffset;

        entries[(_head + _count) % entries.Length] = new Entry(OpenLastOffset, utcTicks);
        _count++;
        EndWrite();
    }

    /// <summary>
    /// Removes entries whose records all precede <paramref name="nextUnprocessedOffset"/>. Removes
    /// every entry when <paramref name="lastEnqueuedOffset"/> precedes it too.
    /// </summary>
    public void Prune(long nextUnprocessedOffset, long lastEnqueuedOffset)
    {
        BeginWrite();
        if (nextUnprocessedOffset > lastEnqueuedOffset)
        {
            _head = 0;
            _count = 0;
        }
        else
        {
            var entries = _entries;
            while (_count > 0 && entries[_head].LastOffset < nextUnprocessedOffset)
            {
                _head = (_head + 1) % entries.Length;
                _count--;
            }
        }

        EndWrite();
    }

    /// <summary>
    /// Gets the arrival time of the first batch with a record at or after <paramref name="offset"/>.
    /// Safe to call concurrently with the writer; does not allocate.
    /// </summary>
    public bool TryGetArrivalUtcTicks(long offset, out long utcTicks)
    {
        var spinner = new SpinWait();
        while (true)
        {
            var sequence = Volatile.Read(ref _sequence);
            if ((sequence & 1) == 0)
            {
                var found = TryFind(offset, out utcTicks);
                // Keep the reads above from moving after the second sequence read.
                if (Interlocked.CompareExchange(ref _sequence, 0, 0) == sequence)
                    return found;
            }

            spinner.SpinOnce();
        }
    }

    private bool TryFind(long offset, out long utcTicks)
    {
        // A torn snapshot is discarded by the caller; indexing stays inside the array read here.
        var entries = _entries;
        var head = _head;
        var count = Math.Min(_count, entries.Length);
        for (var i = 0; i < count; i++)
        {
            ref readonly var entry = ref entries[(head + i) % entries.Length];
            if (entry.LastOffset >= offset)
            {
                utcTicks = entry.UtcTicks;
                return true;
            }
        }

        utcTicks = 0;
        return false;
    }

    private void BeginWrite() => Interlocked.Increment(ref _sequence);

    private void EndWrite() => Volatile.Write(ref _sequence, _sequence + 1);

    private Entry[] Grow(Entry[] entries)
    {
        var grown = new Entry[entries.Length * 2];
        for (var i = 0; i < _count; i++)
            grown[i] = entries[(_head + i) % entries.Length];

        _entries = grown;
        _head = 0;
        return grown;
    }

    // Halves the entry count in place by merging neighbours. Logical slot i is written after
    // slots 2i and 2i + 1 are read, and every slot below 2i was read earlier.
    private void Merge(Entry[] entries)
    {
        var merged = 0;
        for (var i = 0; i < _count; i += 2, merged++)
        {
            var first = entries[(_head + i) % entries.Length];
            if (i + 1 < _count)
                first.LastOffset = entries[(_head + i + 1) % entries.Length].LastOffset;
            entries[(_head + merged) % entries.Length] = first;
        }

        _count = merged;
    }

    private struct Entry(long lastOffset, long utcTicks)
    {
        public long LastOffset = lastOffset;
        public readonly long UtcTicks = utcTicks;
    }
}
