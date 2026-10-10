using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Dekaf.Errors;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Dekaf.Compression;
using Dekaf.Diagnostics;
using Dekaf.Internal;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Retry;
using Dekaf.Serialization;
using Dekaf.Telemetry;
using Microsoft.Extensions.Logging;
using CancellationTokenSourcePool = Reservoir.CancellationTokenSourcePool;
#if !NET9_0_OR_GREATER
using StringSet = System.Collections.Generic.IReadOnlyCollection<string>;
using TopicPartitionSet = System.Collections.Generic.IReadOnlyCollection<Dekaf.TopicPartition>;
#else
using StringSet = System.Collections.Generic.IReadOnlySet<string>;
using TopicPartitionSet = System.Collections.Generic.IReadOnlySet<Dekaf.TopicPartition>;
#endif

namespace Dekaf.Consumer;

/// <summary>
/// Holds pending fetch data for lazy record iteration.
/// Records are only parsed and deserialized when accessed.
/// </summary>
/// <remarks>
/// Implements IDisposable to release pooled memory from the network buffer.
/// When using zero-copy parsing, all RecordBatches share a reference to the pooled network buffer.
/// The memory owner is stored here and disposed after all records have been consumed.
/// </remarks>
internal sealed class PendingFetchData : IDisposable
{
    // Pool for reusing PendingFetchData instances to eliminate per-partition-per-fetch allocation.
    private const int DefaultMaxPoolSize = 128;
    // Slabs outlive an individual PendingFetchData use, but not the pooled object itself.
    // Bound retention per size bucket so deep prefetch cannot pin one large slab per item;
    // PoolSizing ratchets the depth for high-partition workloads.
    private const int DefaultMaxParsedRecordSlabsPerBucket = 16;
    private static int s_maxPoolSize = DefaultMaxPoolSize;
    private static PendingFetchDataPoolState s_poolState = CreatePool(DefaultMaxPoolSize);
    private static Reservoir.ObjectPool<PendingFetchData, PendingFetchDataPolicy> s_pool =
        s_poolState.Pool;
    private static PendingFetchDataPoolState? s_retiredPool;
    private static int s_maxParsedRecordSlabsPerBucket = DefaultMaxParsedRecordSlabsPerBucket;
    private static ArrayPool<Record> s_parsedRecordSlabPool = ArrayPool<Record>.Create(
        RecordBatch.MaxReasonableLazyRecordCount,
        DefaultMaxParsedRecordSlabsPerBucket);
    private static readonly Lock s_resizeLock = new();

    internal static int MaxPoolSizeValue => Volatile.Read(ref s_maxPoolSize);
    internal static int MaxParsedRecordSlabsPerBucketValue =>
        Volatile.Read(ref s_maxParsedRecordSlabsPerBucket);

    internal static void RatchetPoolSize(int newSize) =>
        RatchetPoolSize(newSize, PoolSizing.ForConsumerParsedRecordSlabs(newSize));

    internal static void RatchetPoolSize(int newSize, int desiredSlabDepth)
    {
        InterlockedHelper.RatchetUp(ref s_maxPoolSize, newSize);

        var currentPool = Volatile.Read(ref s_pool);
        if (currentPool.MaximumRetained < newSize ||
            Volatile.Read(ref s_maxParsedRecordSlabsPerBucket) < desiredSlabDepth)
        {
            lock (s_resizeLock)
            {
                currentPool = Volatile.Read(ref s_pool);
                if (currentPool.MaximumRetained < newSize)
                {
                    var currentState = s_poolState;
                    var replacementState = CreatePool(newSize);
                    Volatile.Write(ref currentState.MigrationTarget, replacementState);
                    s_poolState = replacementState;
                    Volatile.Write(ref s_pool, replacementState.Pool);

                    if (s_retiredPool is { } retiredPool)
                    {
                        retiredPool.Pool.Clear();
                        retiredPool.Pool.Dispose();
                    }
                    currentPool.Clear();
                    // Keep one old generation reachable for returns that captured it before
                    // publication; the next ratchet forwards them and releases its storage.
                    s_retiredPool = currentState;
                }

                if (Volatile.Read(ref s_maxParsedRecordSlabsPerBucket) < desiredSlabDepth)
                {
                    var newSlabPool = ArrayPool<Record>.Create(
                        RecordBatch.MaxReasonableLazyRecordCount,
                        desiredSlabDepth);
                    Volatile.Write(ref s_parsedRecordSlabPool, newSlabPool);
                    Volatile.Write(ref s_maxParsedRecordSlabsPerBucket, desiredSlabDepth);
                }
            }
        }
    }

    private IReadOnlyList<RecordBatch> _batches = null!;
    private Dictionary<long, Queue<long>>? _abortedProducers;
    private IPooledMemory? _memoryOwner;
    private Record[]? _parsedRecordSlab;
    private ArrayPool<Record>? _parsedRecordSlabOwner;
    private int _parsedRecordSlabLength;
    private int _batchIndex = -1;
    private int _recordIndex = -1;
    private int _disposed;
    private int _referenceCount = 1;
    private int _headerGeneration;
    private object? _checkpointOwner;
    private bool _eagerParsed;
    private bool _hasBufferedCurrent;

    // Only CreateError assigns this; instances carrying an error never parse, so
    // EagerParseAll may check _eagerParsed before draining it. A second writer would
    // break that ordering — keep the invariant if one is ever added.
    private ConsumeException? _error;

    public string Topic { get; private set; } = null!;
    public int PartitionIndex { get; private set; }

    /// <summary>
    /// Cached activity name for tracing. Lazily created so no-listener consume paths avoid
    /// the per-fetch string allocation.
    /// </summary>
    private string? _activityName;

    internal string ActivityName
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var activityName = _activityName;
            return activityName ?? CreateActivityName();
        }
    }

    /// <summary>
    /// Cached TopicPartition to avoid per-message allocation in consume loop.
    /// </summary>
    public TopicPartition TopicPartition { get; private set; }

    /// <summary>
    /// Tracks the last offset yielded for batch position updates.
    /// Updated as records are consumed, used for final position update when fetch is exhausted.
    /// Inspired by librdkafka's batch-level position tracking.
    /// </summary>
    /// <remarks>
    /// Thread-safety: Not required. PendingFetchData is consumed sequentially by the single
    /// consumer poll loop thread. Each instance handles one partition's fetch response.
    /// </remarks>
    public long LastYieldedOffset { get; private set; } = -1;
    public int LastYieldedLeaderEpoch { get; private set; } = -1;

    /// <summary>
    /// Highest yielded offset the application has demonstrably moved past — set when the
    /// caller requests the next record/batch after a yield. Offsets at or below this value
    /// are safe to stage for auto-commit (at-least-once); the gap between
    /// <see cref="ProvenOffset"/> and <see cref="LastYieldedOffset"/> is the in-doubt record
    /// whose processing may have failed, and must not be staged on unwind.
    /// </summary>
    public long ProvenOffset { get; private set; } = -1;
    public int ProvenLeaderEpoch { get; private set; } = -1;

    internal long CheckpointInitialMessageCount { get; private set; }
    internal long CheckpointStartOffset => _skipRecordsBelowOffset;

    internal void BeginCheckpointWindow(object owner)
    {
        CheckpointInitialMessageCount = MessageCount;
        // The batch already exists. Its identity cannot wrap or match a stale window.
        Volatile.Write(ref _checkpointOwner, owner);
    }

    internal bool IsCheckpointWindowActive(object owner) =>
        Volatile.Read(ref _disposed) == 0 && ReferenceEquals(Volatile.Read(ref _checkpointOwner), owner);

    internal void EndCheckpointWindow(object? owner)
    {
        // A suspended iterator can outlive this pooled instance's original rental.
        // End only its own window, even if a newer owner begins between checks.
        if (owner is not null)
            Interlocked.CompareExchange(ref _checkpointOwner, null, owner);
    }

    /// <summary>
    /// Marks everything yielded so far as processed. Called on the consume paths at the
    /// point the application requests more data (next MoveNextAsync/ConsumeOne call),
    /// which proves the previously yielded record or batch was handled.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MarkYieldedProcessed()
    {
        ProvenOffset = LastYieldedOffset;
        ProvenLeaderEpoch = LastYieldedLeaderEpoch;
    }

    /// <summary>
    /// Tracks total bytes consumed in this pending fetch.
    /// </summary>
    public long TotalBytesConsumed { get; private set; }

    /// <summary>
    /// Tracks the number of messages yielded from this pending fetch.
    /// Using long to prevent overflow in long-running scenarios with large fetches.
    /// </summary>
    public long MessageCount { get; private set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GetMaximumRecordCount(int limit)
        => Math.Min(limit, _maximumRecordCount);

    internal bool IsExhausted { get; private set; }

    // Iteration cursor captured when this fetch is yielded as a batch. Kept on the pooled
    // instance so the batch iterators' state machines carry no extra per-stream state.
    private int _yieldBatchIndex;
    private int _yieldRecordIndex;
    private bool _yieldBuffered;
    private bool _yieldExhausted;

    /// <summary>
    /// Captures where record iteration stands just before the fetch is yielded as a batch.
    /// Once per batch, never per record.
    /// </summary>
    internal void CaptureYieldCursor()
    {
        _yieldBatchIndex = _batchIndex;
        _yieldRecordIndex = _recordIndex;
        _yieldBuffered = _hasBufferedCurrent;
        _yieldExhausted = IsExhausted;
    }

    /// <summary>
    /// Whether no record has been read since <see cref="CaptureYieldCursor"/>: the caller
    /// skipped the batch. Creating an enumerator without calling MoveNext does not move it.
    /// </summary>
    internal bool IsAtYieldCursor =>
        _batchIndex == _yieldBatchIndex
        && _recordIndex == _yieldRecordIndex
        && _hasBufferedCurrent == _yieldBuffered
        && IsExhausted == _yieldExhausted;
    internal long FetchEndOffsetExclusive
    {
        get => _fetchEndOffsetExclusive;
        private set => _fetchEndOffsetExclusive = value;
    }
    internal int FetchEndLeaderEpoch
    {
        get => _fetchEndLeaderEpoch;
        private set => _fetchEndLeaderEpoch = value;
    }
    internal bool ReachedSnapshotEnd
    {
        get => _reachedSnapshotEnd;
        private set => _reachedSnapshotEnd = value;
    }
    internal long SnapshotEndOffset
    {
        get => _snapshotEndOffset;
        private set => _snapshotEndOffset = value;
    }
    internal bool IsSnapshotEnd => SnapshotEndOffset >= 0;
    internal bool IsPartitionEof => _partitionEofOffset >= 0;
    internal long? PartitionEofOffset => IsPartitionEof ? _partitionEofOffset : null;

    private long _emittedMessageCount;
    private long _emittedBytesConsumed;

    /// <summary>
    /// Records with offsets below this value are skipped when iteration starts.
    /// The broker returns whole record batches, so a fetch positioned mid-batch
    /// (e.g. resuming from a committed offset that falls inside a batch) receives
    /// leading records below the requested offset; yielding them would re-deliver
    /// records the application already consumed. -1 disables skipping.
    /// </summary>
    private long _skipRecordsBelowOffset = -1;

    /// <summary>Raises the publication floor and suppresses progress from fully covered responses.</summary>
    internal void RaiseStartOffset(long offset)
    {
        Debug.Assert(_batchIndex < 0, "The delivery floor must be set before publication.");
        _skipRecordsBelowOffset = Math.Max(_skipRecordsBelowOffset, offset);
        if (FetchEndOffsetExclusive >= 0 && FetchEndOffsetExclusive <= _skipRecordsBelowOffset)
        {
            // A fully covered response has no new records or position/snapshot progress.
            // Keep it queued so shared memory is released in publication order.
            IsExhausted = true;
            FetchEndOffsetExclusive = -1;
            FetchEndLeaderEpoch = -1;
            ReachedSnapshotEnd = false;
        }
    }

    private PendingFetchData() { }

    /// <summary>
    /// Rents a PendingFetchData from the pool and initializes it with the given parameters.
    /// </summary>
    public static PendingFetchData Create(string topic, int partitionIndex, IReadOnlyList<RecordBatch> batches,
        IReadOnlyList<AbortedTransaction>? abortedTransactions = null,
        IPooledMemory? memoryOwner = null,
        string? activityName = null,
        long skipRecordsBelowOffset = -1,
        long stopAtOffsetExclusive = -1,
        long ownershipStart = 0)
    {
        var instance = Rent();
        WriteGeneration(ref instance._ownershipStart, ownershipStart);
        // Fetches of records take ordered generations, so a record's generation (which its
        // ConsumeResult already carries) also tells whether it was fetched before an ownership
        // of its partition began. Once per fetched partition batch.
        var fetchGeneration = NextFetchGeneration();
        WriteGeneration(ref instance._fetchGeneration, fetchGeneration);
        Volatile.Write(ref instance._headerGeneration, (int)fetchGeneration);
        instance.Topic = topic;
        instance.PartitionIndex = partitionIndex;
        instance._activityName = activityName;
        instance.TopicPartition = new TopicPartition(topic, partitionIndex);
        instance._batches = batches;
        instance._memoryOwner = memoryOwner;
        instance._skipRecordsBelowOffset = skipRecordsBelowOffset;
        instance._stopAtOffsetExclusive = stopAtOffsetExclusive;

        if (batches.Count > 0)
        {
            var lastBatch = batches[^1];
            var actualEndOffset = lastBatch.BaseOffset + lastBatch.LastOffsetDelta + 1;
            instance.ReachedSnapshotEnd = stopAtOffsetExclusive >= 0
                                          && actualEndOffset >= stopAtOffsetExclusive;
            instance.FetchEndOffsetExclusive = stopAtOffsetExclusive >= 0
                ? Math.Min(actualEndOffset, stopAtOffsetExclusive)
                : actualEndOffset;
            instance.FetchEndLeaderEpoch = FindFetchEndLeaderEpoch(
                batches,
                instance.FetchEndOffsetExclusive,
                lastBatch,
                actualEndOffset);
        }

        // Reuse the owner-attachment traversal. Poll-batch construction and eager
        // parsing must not add another scan over all record batches.
        long maximumRecordCount = 0;
        for (var i = 0; i < batches.Count; i++)
        {
            var batch = batches[i];
            batch.AttachConsumerPoolOwner(instance, instance.HeaderGeneration);
            maximumRecordCount += batch.RecordCountUpperBound;
        }
        instance._maximumRecordCount = (int)Math.Clamp(maximumRecordCount, 1, int.MaxValue);

        if (abortedTransactions is { Count: > 0 })
        {
            instance._abortedProducers ??= new Dictionary<long, Queue<long>>();
            // AbortedTransactions is sorted by FirstOffset per the Kafka protocol
            foreach (var at in abortedTransactions)
            {
                if (!instance._abortedProducers.TryGetValue(at.ProducerId, out var queue))
                {
                    queue = new Queue<long>();
                    instance._abortedProducers[at.ProducerId] = queue;
                }
                queue.Enqueue(at.FirstOffset);
            }
        }

        return instance;
    }

    private static int FindFetchEndLeaderEpoch(
        IReadOnlyList<RecordBatch> batches,
        long fetchEndOffsetExclusive,
        RecordBatch lastBatch,
        long actualEndOffset)
    {
        if (fetchEndOffsetExclusive == actualEndOffset)
            return lastBatch.PartitionLeaderEpoch;

        // Find the last batch beginning before the exclusive boundary. Fetch responses are
        // offset-ordered; binary search avoids adding a second O(n) pass to fetch processing.
        var low = 0;
        var high = batches.Count - 1;
        var leaderEpoch = -1;
        while (low <= high)
        {
            var middle = low + ((high - low) >> 1);
            var batch = batches[middle];
            if (batch.BaseOffset < fetchEndOffsetExclusive)
            {
                leaderEpoch = batch.PartitionLeaderEpoch;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return leaderEpoch;
    }

    public static PendingFetchData CreateSnapshotEnd(
        string topic,
        int partitionIndex,
        long endOffset,
        SnapshotConsumeState? snapshot = null)
    {
        var instance = Rent();
        instance.Topic = topic;
        instance.PartitionIndex = partitionIndex;
        instance.TopicPartition = new TopicPartition(topic, partitionIndex);
        instance._batches = Array.Empty<RecordBatch>();
        instance.SnapshotEndOffset = endOffset;
        instance._snapshotMarkerState = snapshot;
        return instance;
    }

    public static PendingFetchData CreatePartitionEof(
        string topic,
        int partitionIndex,
        long endOffset)
    {
        var instance = Rent();
        instance.Topic = topic;
        instance.PartitionIndex = partitionIndex;
        instance.TopicPartition = new TopicPartition(topic, partitionIndex);
        instance._batches = Array.Empty<RecordBatch>();
        instance._partitionEofOffset = endOffset;
        return instance;
    }

    private void AttachParsedRecordSlab(IReadOnlyList<RecordBatch> batches, int batchCount)
    {
        var totalRecordCount = 0;

        for (var i = 0; i < batchCount; i++)
        {
            var recordCount = batches[i].UnparsedLazyRecordCount;
            if (recordCount < 0 || recordCount > RecordBatch.MaxReasonableLazyRecordCount - totalRecordCount)
                return;
            totalRecordCount += recordCount;
        }

        if (totalRecordCount == 0)
            return;

        var slabPool = Volatile.Read(ref s_parsedRecordSlabPool);
        // No rent-side clear: ReleaseReference scrubs the used range before every return.
        var slab = slabPool.Rent(totalRecordCount);
        _parsedRecordSlab = slab;
        _parsedRecordSlabOwner = slabPool;
        _parsedRecordSlabLength = totalRecordCount;
        var offset = 0;
        for (var i = 0; i < batchCount; i++)
        {
            var batch = batches[i];
            var recordCount = batch.UnparsedLazyRecordCount;
            batch.UseParsedRecordSlab(slab, offset);
            offset += recordCount;
        }

        Debug.Assert(offset == totalRecordCount, "Slab slices must exactly cover the range ReleaseReference clears.");
    }

    public static PendingFetchData CreateError(string topic, int partitionIndex, ConsumeException error)
    {
        var instance = Rent();
        instance.Topic = topic;
        instance.PartitionIndex = partitionIndex;
        instance.TopicPartition = new TopicPartition(topic, partitionIndex);
        instance._batches = Array.Empty<RecordBatch>();
        instance._error = error;
        return instance;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PendingFetchData Rent()
    {
        var instance = Volatile.Read(ref s_pool).Rent();
        Volatile.Write(ref instance._referenceCount, 1);
        Volatile.Write(ref instance._disposed, 0);
        return instance;
    }

    internal RetentionLease RetainForIteration()
    {
        Debug.Assert(
            Volatile.Read(ref _disposed) == 0,
            "RetainForIteration called after Dispose.");
        Interlocked.Increment(ref _referenceCount);
        return new RetentionLease(this);
    }

    internal readonly struct RetentionLease(PendingFetchData owner) : IDisposable
    {
        public void Dispose() => owner.ReleaseReference();
    }

    internal void RetainForProcessing()
    {
        Debug.Assert(Volatile.Read(ref _referenceCount) > 0);
        Interlocked.Increment(ref _referenceCount);
    }

    internal void ReleaseAfterProcessing() => ReleaseReference();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private string CreateActivityName()
    {
        var activityName = Diagnostics.DekafDiagnostics.ProcessSpanName(Topic);
        _activityName = activityName;
        return activityName;
    }

    /// <summary>
    /// Attaches a memory owner to this instance, avoiding the need to create a new PendingFetchData.
    /// </summary>
    public void SetMemoryOwner(IPooledMemory memoryOwner)
    {
        Debug.Assert(_memoryOwner is null, "SetMemoryOwner called when a memory owner is already set. This indicates a bug — the previous owner would be silently overwritten and leaked.");
        _memoryOwner = memoryOwner;
    }

    public RecordBatch CurrentBatch => _batches[_batchIndex];

    internal int HeaderGeneration => Volatile.Read(ref _headerGeneration);

    // Process-wide and 64-bit, so it never wraps. A result keeps only the low 32 bits (its
    // header generation, which keeps ConsumeResult's size); while its fetch is not reused the full
    // value is read from the fetch (TryGetFetchGeneration), otherwise ExpandFetchGeneration
    // restores it from the counter.
    private static long s_fetchGeneration;

    // This use's full generation; its low 32 bits are the header generation it started with.
    private long _fetchGeneration;

    // The ownership start (S) of the partition when this fetch was created: offsets stored for
    // its records are tagged with it, so they only commit under that ownership.
    private long _ownershipStart;

    /// <summary>
    /// The ownership start this fetch was created under, while the fetch is still in the use the
    /// header generation identifies. False once it was disposed or reused.
    /// </summary>
    internal long OwnershipStart => ReadGeneration(ref _ownershipStart);

    internal bool TryGetOwnershipStart(int headerGeneration, out long ownershipStart)
    {
        ownershipStart = ReadGeneration(ref _ownershipStart);
        return (int)ReadGeneration(ref _fetchGeneration) == headerGeneration
               && Volatile.Read(ref _headerGeneration) == headerGeneration;
    }

    /// <summary>
    /// The full generation of a result built from this fetch, while the fetch is still in that
    /// use: its header generation still matches. False once the fetch was disposed or reused.
    /// </summary>
    internal bool TryGetFetchGeneration(int headerGeneration, out long fetchGeneration)
    {
        fetchGeneration = ReadGeneration(ref _fetchGeneration);
        return (int)fetchGeneration == headerGeneration
               && Volatile.Read(ref _headerGeneration) == headerGeneration;
    }

    /// <summary>The full generation and the ownership start, with one check of the fetch's use.</summary>
    internal bool TryGetFetchGeneration(int headerGeneration, out long fetchGeneration, out long ownershipStart)
    {
        fetchGeneration = ReadGeneration(ref _fetchGeneration);
        ownershipStart = ReadGeneration(ref _ownershipStart);
        return (int)fetchGeneration == headerGeneration
               && Volatile.Read(ref _headerGeneration) == headerGeneration;
    }

    private static void WriteGeneration(ref long field, long value)
    {
        if (IntPtr.Size == 8)
            Volatile.Write(ref field, value);
        else
            Interlocked.Exchange(ref field, value);
    }

    /// <summary>
    /// The latest generation taken: by a record-bearing fetch, a coordinator revocation, or an
    /// ownership start. Later ones are larger.
    /// </summary>
    internal static long CurrentFetchGeneration => ReadGeneration(ref s_fetchGeneration);

    /// <summary>A new, larger generation whose low 32 bits are never 0 (0 marks results without a fetch).</summary>
    internal static long NextFetchGeneration()
    {
        while (true)
        {
            var generation = Interlocked.Increment(ref s_fetchGeneration);
            if ((int)generation != 0)
                return generation;
        }
    }

    /// <summary>
    /// The full generation of a delivered record from the low 32 bits its result carries: the
    /// latest generation with those bits not after the current one. Exact for any record fetched
    /// within the last 2^32 generations; a result retained longer reads as newer.
    /// </summary>
    internal static long ExpandFetchGeneration(int lowBits)
    {
        var current = CurrentFetchGeneration;
        return current - unchecked((uint)((int)current - lowBits));
    }

    /// <summary>Atomic on 32-bit processes too; a plain volatile read on 64-bit ones.</summary>
    internal static long ReadGeneration(ref long field)
        => IntPtr.Size == 8 ? Volatile.Read(ref field) : Interlocked.Read(ref field);

    /// <summary>Moves the generation counter forward to <paramref name="generation"/> (tests only).</summary>
    internal static void AdvanceFetchGenerationForTest(long generation)
    {
        var current = CurrentFetchGeneration;
        while (current < generation)
        {
            var observed = Interlocked.CompareExchange(ref s_fetchGeneration, generation, current);
            if (observed == current)
                return;
            current = observed;
        }
    }

    internal bool IsHeaderGenerationActive(int generation) =>
        Volatile.Read(ref _referenceCount) > 0 && Volatile.Read(ref _headerGeneration) == generation;

    /// <summary>
    /// Gets the current record via direct array access, bypassing lazy record-list
    /// indexer overhead (Volatile.Read + disposed check + EnsureParsedUpTo per access).
    /// Safe because EagerParseAll() is called before iteration begins.
    /// Returns by readonly reference so consume loops avoid copying the ~80-byte
    /// Record struct per message; the reference is only valid until the next
    /// MoveNext/Dispose call.
    /// </summary>
    public ref readonly Record CurrentRecord
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var array = _currentRecordsArray;
            if (array is not null)
                return ref array[_currentRecordsArrayOffset + _recordIndex];

            _fallbackCurrentRecord = _currentRecords![_recordIndex];
            return ref _fallbackCurrentRecord;
        }
    }

    // Staging slot for non-array record lists. Kept as a lazy per-access path on purpose:
    // fault-modelling lists (and any list that throws from its indexer) must fail at the
    // faulted record so the good prefix is still yielded — an eager per-batch copy would
    // move the throw ahead of those yields and break mid-batch fault recovery.
    private Record _fallbackCurrentRecord;

    // Cached batch state updated only on batch transitions, amortizing per-record cost.
    // _currentRecordsArray bypasses IReadOnlyList<Record> virtual dispatch + lazy parsing
    // indexer overhead by caching the underlying Record[] directly.
    private Record[]? _currentRecordsArray;
    private int _currentRecordsArrayOffset;
    private IReadOnlyList<Record>? _currentRecords;
    private int _currentRecordsCount;

    /// <summary>
    /// Cached batch properties for the current batch.
    /// Updated only on batch transitions to avoid per-message property access overhead.
    /// </summary>
    internal long CurrentBaseOffset { get; private set; }
    internal int CurrentPartitionLeaderEpoch { get; private set; } = -1;
    internal long CurrentBaseTimestamp { get; private set; }
    /// <summary>
    /// Cached timestamp type for the current batch.
    /// Computed once per batch transition instead of per-message.
    /// </summary>
    internal TimestampType CurrentTimestampType { get; private set; }

    /// <summary>
    /// Updates tracking for batch-level position.
    /// Called after yielding each record.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrackConsumed(long offset, int messageBytes)
    {
        LastYieldedOffset = offset;
        LastYieldedLeaderEpoch = CurrentPartitionLeaderEpoch;
        TotalBytesConsumed += messageBytes;
        MessageCount++;
    }

    public bool TryConsumeMetricDelta(out long messageCount, out long bytesConsumed)
    {
        messageCount = MessageCount - _emittedMessageCount;
        if (messageCount <= 0)
        {
            bytesConsumed = 0;
            return false;
        }

        bytesConsumed = TotalBytesConsumed - _emittedBytesConsumed;
        _emittedMessageCount = MessageCount;
        _emittedBytesConsumed = TotalBytesConsumed;
        return true;
    }

    /// <summary>
    /// Gets all batches for memory estimation.
    /// </summary>
    public IReadOnlyList<RecordBatch> GetBatches() => _batches;

    /// <summary>
    /// Eagerly parses all records in consumable batches at once. Snapshot batches wholly at
    /// or beyond the captured end remain unparsed because they can never produce a record.
    /// Call before sequential consumption to avoid per-record lazy parse overhead
    /// (disposed check + bounds check + EnsureParsedUpTo call per indexer access).
    /// This is a per-batch cost amortized over all records in the batch.
    /// </summary>
    public void EagerParseAll(RecordHeaderRoutingPlan? headerRoutingPlan = null)
    {
        // Check _eagerParsed first: it is only set after a successful parse, and _error
        // is only set on instances that never parse (CreateError), so the order swap is
        // safe and spares the already-parsed steady state a full-fence Interlocked per poll.
        if (_eagerParsed)
        {
            if (headerRoutingPlan is not null)
                ConfigureHeaderRouting(headerRoutingPlan);
            return;
        }

        if (Interlocked.Exchange(ref _error, null) is { } error)
            throw error;

        var batchCount = _batches.Count;
        if (_stopAtOffsetExclusive >= 0)
        {
            for (var i = 0; i < batchCount; i++)
            {
                if (_batches[i].BaseOffset < _stopAtOffsetExclusive)
                    continue;

                batchCount = i;
                break;
            }
        }

        AttachParsedRecordSlab(_batches, batchCount);

        try
        {
            for (var i = 0; i < batchCount; i++)
            {
                var batch = _batches[i];
                batch.ConfigureHeaderRouting(headerRoutingPlan);
                batch.EnsureAllRecordsParsed();
                if (batch.Records is Protocol.Records.LazyRecordList lazyList)
                    lazyList.EnsureAllParsed();
            }
        }
        catch (Exception ex) when (ex is InsufficientDataException or MalformedProtocolDataException)
        {
            throw new ConsumeException($"Failed to parse record batch for {Topic}-{PartitionIndex}.", ex);
        }

        _eagerParsed = true;
    }

    internal void ConfigureHeaderRouting(RecordHeaderRoutingPlan headerRoutingPlan)
    {
        for (var index = 0; index < _batches.Count; index++)
            _batches[index].ConfigureHeaderRouting(headerRoutingPlan);
    }

    /// <summary>
    /// Caches batch-level state (Records array, BaseOffset, BaseTimestamp, TimestampType)
    /// so per-message access avoids repeated property indirection through RecordBatch.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CacheCurrentBatchState()
    {
        var batch = _batches[_batchIndex];
        var records = batch.Records;
        _currentRecords = records;
        _currentRecordsCount = records.Count;

        // Cache raw array for direct indexing (bypasses lazy-list indexer overhead).
        // Lists that resolve to no array (fault-modelling doubles) stay on the lazy
        // per-access indexer via _fallbackCurrentRecord.
        _currentRecordsArray = batch.GetParsedRecordsArray()
            ?? records as Record[]
            ?? (records is Protocol.Records.LazyRecordList lazyList ? lazyList.GetParsedArray() : null);
        _currentRecordsArrayOffset = batch.GetParsedRecordsOffset();

        CurrentBaseOffset = batch.BaseOffset;
        if (_stopAtOffsetExclusive >= 0
            && CurrentBaseOffset + batch.LastOffsetDelta >= _stopAtOffsetExclusive)
        {
            _currentRecordsCount = FindSnapshotRecordCount(records);
        }

        CurrentPartitionLeaderEpoch = batch.PartitionLeaderEpoch;
        CurrentBaseTimestamp = batch.BaseTimestamp;
        var attrs = batch.Attributes;
        CurrentTimestampType = (attrs & RecordBatchAttributes.TimestampTypeLogAppendTime) != 0
            ? TimestampType.LogAppendTime
            : TimestampType.CreateTime;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private int FindSnapshotRecordCount(IReadOnlyList<Record> records)
    {
        if (CurrentBaseOffset >= _stopAtOffsetExclusive)
            return 0;

        var low = 0;
        var high = _currentRecordsCount;
        // Offset deltas are monotonic within a Kafka record batch, so a lower-bound
        // search avoids walking every record in the excluded suffix.
        while (low < high)
        {
            var middle = low + ((high - low) >> 1);
            var offsetDelta = _currentRecordsArray is { } recordsArray
                ? recordsArray[_currentRecordsArrayOffset + middle].OffsetDelta
                : records[middle].OffsetDelta;
            if (CurrentBaseOffset + offsetDelta < _stopAtOffsetExclusive)
                low = middle + 1;
            else
                high = middle;
        }

        return low;
    }

    /// <summary>
    /// Advances to the next record across all batches.
    /// Returns false when no more records are available.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        Debug.Assert(Volatile.Read(ref _disposed) == 0, "MoveNext() called after Dispose()");

        if (IsExhausted)
            return false;

        if (_hasBufferedCurrent)
        {
            _hasBufferedCurrent = false;
            return true;
        }

        // First call - start at first batch, first record
        if (_batchIndex < 0)
        {
            _batchIndex = 0;
            _recordIndex = 0;
            if (!HasCurrentRecordOrMarkExhausted())
                return false;

            return _skipRecordsBelowOffset < 0 || SkipRecordsBelowStartOffset();
        }

        // Try next record in current batch (uses cached count to avoid Records property access)
        _recordIndex++;
        if (_recordIndex < _currentRecordsCount)
            return true;

        // Move to next batch
        _batchIndex++;
        _recordIndex = 0;
        return HasCurrentRecordOrMarkExhausted();
    }

    /// <summary>
    /// Advances to the next record and makes the following <see cref="MoveNext"/> return it.
    /// Used when a bounded batch reaches its record limit and must distinguish
    /// "more records remain" from "fetch exhausted" without dropping the next record.
    /// </summary>
    internal bool TryBufferNext()
    {
        if (IsExhausted)
            return false;

        if (_hasBufferedCurrent)
            return true;

        if (!MoveNext())
            return false;

        _hasBufferedCurrent = true;
        return true;
    }

    /// <summary>
    /// Makes the next <see cref="MoveNext"/> replay the current record. Used when a
    /// concurrent control-plane change suppresses delivery after the record was read.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void BufferCurrentForRedelivery()
    {
        Debug.Assert(!_hasBufferedCurrent, "Current record is already buffered.");
        _hasBufferedCurrent = true;
    }

    private bool HasCurrentRecordOrMarkExhausted()
    {
        if (HasCurrentRecord())
            return true;

        IsExhausted = true;
        return false;
    }

    /// <summary>
    /// Advances past leading records below the delivery floor. This can span multiple
    /// batches when a response overlaps previously published data. It runs only when
    /// iteration starts and adds no per-record check to steady-state iteration.
    /// Parsed arrays use lower-bound search; other lists retain sequential fault timing.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool SkipRecordsBelowStartOffset()
    {
        while (CurrentBaseOffset + CurrentRecord.OffsetDelta < _skipRecordsBelowOffset)
        {
            if (_currentRecordsArray is { } records)
            {
                // Kafka offset deltas are ordered but may have gaps after compaction.
                // Search the parsed batch slice instead of visiting every excluded record.
                var low = _recordIndex + 1;
                var high = _currentRecordsCount;
                while (low < high)
                {
                    var middle = low + ((high - low) >> 1);
                    if (CurrentBaseOffset + records[_currentRecordsArrayOffset + middle].OffsetDelta < _skipRecordsBelowOffset)
                        low = middle + 1;
                    else
                        high = middle;
                }
                _recordIndex = low;
            }
            else
            {
                // Non-array lists retain sequential fault timing: looking ahead could
                // throw before a valid included record has been delivered.
                _recordIndex++;
            }
            if (_recordIndex < _currentRecordsCount)
                continue;

            _batchIndex++;
            _recordIndex = 0;
            if (!HasCurrentRecordOrMarkExhausted())
                return false;
        }

        return true;
    }

    private bool HasCurrentRecord()
    {
        while (_batchIndex < _batches.Count)
        {
            // Skip aborted transaction data batches and control batches (commit/abort markers).
            // Control batches also advance the aborted transaction tracking state.
            if (ShouldSkipBatch(_batches[_batchIndex]))
            {
                _batchIndex++;
                _recordIndex = 0;
                continue;
            }

            // Cache batch-level state once per batch transition to avoid
            // per-message property indirection through RecordBatch.Records.
            CacheCurrentBatchState();

            if (_recordIndex < _currentRecordsCount)
                return true;
            // Empty batch, try next
            _batchIndex++;
            _recordIndex = 0;
        }
        return false;
    }

    /// <summary>
    /// Determines whether a batch should be skipped based on aborted transaction state.
    /// Control batches (commit/abort markers) are always skipped.
    /// Transactional data batches from aborted producers are skipped.
    /// </summary>
    private bool ShouldSkipBatch(RecordBatch batch)
    {
        var attrs = batch.Attributes;

        // Control batches (commit/abort markers): never yield to consumer.
        // When encountering an abort control batch for an aborted producer,
        // advance the tracking state so subsequent committed batches from the
        // same producer are correctly included. Only dequeue when the control
        // batch offset is at or past the tracked first offset — this ensures
        // commit markers (which precede the aborted range) don't prematurely
        // consume queue entries.
        if ((attrs & RecordBatchAttributes.IsControlBatch) != 0)
        {
            if (_abortedProducers is not null &&
                _abortedProducers.TryGetValue(batch.ProducerId, out var queue) &&
                queue.Count > 0 &&
                batch.BaseOffset >= queue.Peek())
            {
                queue.Dequeue();
                if (queue.Count == 0)
                    _abortedProducers.Remove(batch.ProducerId);
            }
            return true;
        }

        // Transactional data batches: skip if from an aborted transaction.
        if ((attrs & RecordBatchAttributes.IsTransactional) != 0 &&
            _abortedProducers is not null &&
            _abortedProducers.TryGetValue(batch.ProducerId, out var q) &&
            q.Count > 0 &&
            batch.BaseOffset >= q.Peek())
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Disposes all record batches, releases the pooled network buffer memory,
    /// and returns this instance to the pool for reuse.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        ReleaseReference();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReleaseReference()
    {
        var remaining = Interlocked.Decrement(ref _referenceCount);
        Debug.Assert(remaining >= 0, "PendingFetchData reference count underflow.");
        if (remaining == 0)
            ReleaseStorage();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ReleaseStorage()
    {
        // Dispose all batches to mark them as disposed.
        // Indexing avoids boxing/enumerator allocation from the IReadOnlyList<T> interface.
        var batches = _batches;
        for (var i = 0; i < batches.Count; i++)
        {
            batches[i].DisposeAndReturnConsumerBatch(this, HeaderGeneration);
        }

        var parsedRecordSlab = _parsedRecordSlab;
        var parsedRecordSlabOwner = _parsedRecordSlabOwner;
        var parsedRecordSlabLength = _parsedRecordSlabLength;
        _parsedRecordSlab = null;
        _parsedRecordSlabOwner = null;
        _parsedRecordSlabLength = 0;
        if (parsedRecordSlab is not null)
        {
            // Records reference pooled Header[] arrays and frame slices, so clear exactly the used
            // range before the slab re-enters its pool (the batches above have already returned
            // their header arrays); the pool's own clearArray would memset the whole rounded-up bucket.
            parsedRecordSlab.AsSpan(0, parsedRecordSlabLength).Clear();
            // The owner is recorded in the same step that rents the slab (EagerParseAll), so a
            // non-null slab always has one.
            parsedRecordSlabOwner!.Return(parsedRecordSlab, clearArray: false);
        }

        // Return the batch list to the pool for reuse
        if (_batches is List<RecordBatch> batchList)
        {
            FetchResponsePartition.ReturnRecordBatchList(batchList);
        }

        // Release the pooled network buffer
        _memoryOwner?.Dispose();

        // Reset state for reuse
        _snapshotMarkerState?.ReleaseEndMarker(TopicPartition);
        _snapshotMarkerState = null;
        _batches = null!;
        _memoryOwner = null;
        _batchIndex = -1;
        _recordIndex = -1;
        _currentRecordsArray = null;
        _currentRecordsArrayOffset = 0;
        _currentRecords = null;
        _currentRecordsCount = 0;
        _fallbackCurrentRecord = default;
        _eagerParsed = false;
        _hasBufferedCurrent = false;
        _yieldBatchIndex = -1;
        _yieldRecordIndex = -1;
        _yieldBuffered = false;
        _yieldExhausted = false;
        _error = null;
        unchecked
        {
            _headerGeneration++;
            if (_headerGeneration == 0)
                _headerGeneration = 1;
        }
        CurrentBaseOffset = 0;
        CurrentPartitionLeaderEpoch = -1;
        CurrentBaseTimestamp = 0;
        CurrentTimestampType = default;
        _checkpointOwner = null;
        LastYieldedOffset = -1;
        LastYieldedLeaderEpoch = -1;
        ProvenOffset = -1;
        ProvenLeaderEpoch = -1;
        TotalBytesConsumed = 0;
        MessageCount = 0;
        IsExhausted = false;
        FetchEndOffsetExclusive = -1;
        FetchEndLeaderEpoch = -1;
        ReachedSnapshotEnd = false;
        SnapshotEndOffset = -1;
        _partitionEofOffset = -1;
        _emittedMessageCount = 0;
        _emittedBytesConsumed = 0;
        _skipRecordsBelowOffset = -1;
        _stopAtOffsetExclusive = -1;
        PartitionIndex = 0;
        TopicPartition = default;
        Topic = null!;
        _activityName = null;
        _abortedProducers?.Clear();

        Volatile.Read(ref s_pool).Return(this);
    }

    private static PendingFetchDataPoolState CreatePool(int capacity)
    {
        var state = new PendingFetchDataPoolState();
        // The fetch loop creates entries that the consumer thread later disposes.
        state.Pool = new Reservoir.ObjectPool<PendingFetchData, PendingFetchDataPolicy>(
            new PendingFetchDataPolicy(state),
            capacity,
            threadLocalFastPath: false);
        return state;
    }

    // Snapshot and filtered-fetch state stays at the cold tail so adding bounded-consume
    // support does not move the cache-hot record iteration fields in this pooled object.
    private long _fetchEndOffsetExclusive = -1;
    private long _snapshotEndOffset = -1;
    private long _partitionEofOffset = -1;
    private long _stopAtOffsetExclusive = -1;
    private int _fetchEndLeaderEpoch = -1;
    private bool _reachedSnapshotEnd;
    private SnapshotConsumeState? _snapshotMarkerState;
    private int _maximumRecordCount = 1;

    private sealed class PendingFetchDataPoolState
    {
        public Reservoir.ObjectPool<PendingFetchData, PendingFetchDataPolicy> Pool = null!;
        public PendingFetchDataPoolState? MigrationTarget;
    }

    private readonly struct PendingFetchDataPolicy(PendingFetchDataPoolState state)
        : Reservoir.IPooledObjectDestroyPolicy<PendingFetchData>,
          Reservoir.INonThrowingResetPolicy
    {
        public PendingFetchData Create() => new();

        public bool TryReset(PendingFetchData item) => true;

        public void Destroy(PendingFetchData item)
        {
            var target = Volatile.Read(ref state.MigrationTarget);
            if (target is null)
                return;

            while (Volatile.Read(ref target.MigrationTarget) is { } next)
                target = next;

            target.Pool.Return(item);
        }
    }
}

internal sealed class SnapshotConsumeState
{
    private readonly object _gate = new();
    private readonly Dictionary<TopicPartition, long> _endOffsets;
    private readonly Dictionary<TopicPartition, long> _startOffsets;
    private readonly TopicPartitionSet _assignment;
    private readonly TopicPartitionSet _paused;
    private readonly HashSet<TopicPartition> _completed = [];
    private readonly HashSet<TopicPartition> _queuedEndMarkers = [];
    private int _remaining;
    private int _consumerStateInvalidated;

    public SnapshotConsumeState(
        Dictionary<TopicPartition, long> endOffsets,
        TopicPartitionSet? assignment = null,
        TopicPartitionSet? paused = null,
        Dictionary<TopicPartition, long>? startOffsets = null)
    {
        _endOffsets = endOffsets;
        _startOffsets = startOffsets ?? [];
        _assignment = assignment ?? new HashSet<TopicPartition>(endOffsets.Keys);
        _paused = paused ?? new HashSet<TopicPartition>();
        _remaining = endOffsets.Count;
    }

    public bool IsComplete => Volatile.Read(ref _remaining) == 0;
    public TopicPartitionSet Assignment => _assignment;
    public Dictionary<TopicPartition, long> StartOffsets => _startOffsets;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetEndOffset(TopicPartition partition, out long endOffset) =>
        _endOffsets.TryGetValue(partition, out endOffset);

    public bool IsPartitionComplete(TopicPartition partition)
    {
        lock (_gate)
            return _completed.Contains(partition);
    }

    public bool Complete(TopicPartition partition)
    {
        lock (_gate)
        {
            if (!_endOffsets.ContainsKey(partition) || !_completed.Add(partition))
                return false;

            Volatile.Write(ref _remaining, _remaining - 1);
            return true;
        }
    }

    public bool TryQueueEndMarker(TopicPartition partition, long visibleEndOffset, out long endOffset)
    {
        if (!_endOffsets.TryGetValue(partition, out endOffset) || visibleEndOffset < endOffset)
            return false;

        lock (_gate)
        {
            return !_completed.Contains(partition) && _queuedEndMarkers.Add(partition);
        }
    }

    public void ReleaseEndMarker(TopicPartition partition)
    {
        lock (_gate)
            _queuedEndMarkers.Remove(partition);
    }

    public void InvalidateConsumerState() => Volatile.Write(ref _consumerStateInvalidated, 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ThrowIfConsumerStateChanged(
        TopicPartitionSet assignment,
        TopicPartitionSet paused)
    {
        if (Volatile.Read(ref _consumerStateInvalidated) != 0
            || !ReferenceEquals(_assignment, assignment)
            || !ReferenceEquals(_paused, paused))
        {
            throw new SnapshotStateChangedException();
        }
    }
}

internal sealed class SnapshotStateChangedException()
    : InvalidOperationException(
        "The consumer assignment or pause state changed while a snapshot enumeration was active.");

internal static class ConsumerFetchPools
{
    private static readonly PendingFetchDataListPool s_pendingFetchDataLists = new();
    private static readonly FetchRequestTopicListPool s_fetchRequestTopicLists = new();
    private static readonly FetchRequestPartitionListPool s_fetchRequestPartitionLists = new();

    internal static List<PendingFetchData> RentPendingFetchDataList()
    {
        var list = s_pendingFetchDataLists.Rent();
        return list;
    }

    internal static void ReturnPendingFetchDataList(List<PendingFetchData> list)
        => s_pendingFetchDataLists.Return(list);

    internal static List<FetchRequestTopic> RentFetchRequestTopicList(int capacity)
    {
        var list = s_fetchRequestTopicLists.Rent();
        list.EnsureCapacity(capacity);
        return list;
    }

    internal static List<FetchRequestPartition> RentFetchRequestPartitionList(int capacity)
    {
        var list = s_fetchRequestPartitionLists.Rent();
        list.EnsureCapacity(capacity);
        return list;
    }

    internal static void ReturnFetchRequestPartitionList(List<FetchRequestPartition> list)
        => s_fetchRequestPartitionLists.Return(list);

    internal static void ReturnFetchRequestTopics(List<FetchRequestTopic> topics)
    {
        for (var i = 0; i < topics.Count; i++)
        {
            if (topics[i].Partitions is List<FetchRequestPartition> partitions)
                s_fetchRequestPartitionLists.Return(partitions);
        }

        s_fetchRequestTopicLists.Return(topics);
    }

    private sealed class PendingFetchDataListPool() : ObjectPool<List<PendingFetchData>>(maxPoolSize: 64)
    {
        protected override List<PendingFetchData> Create() => [];

        protected override void Reset(List<PendingFetchData> item) => item.Clear();
    }

    private sealed class FetchRequestTopicListPool() : ObjectPool<List<FetchRequestTopic>>(maxPoolSize: 64)
    {
        protected override List<FetchRequestTopic> Create() => [];

        protected override void Reset(List<FetchRequestTopic> item) => item.Clear();
    }

    private sealed class FetchRequestPartitionListPool() : ObjectPool<List<FetchRequestPartition>>(maxPoolSize: 128)
    {
        protected override List<FetchRequestPartition> Create() => [];

        protected override void Reset(List<FetchRequestPartition> item) => item.Clear();
    }
}

/// <summary>
/// Kafka consumer implementation.
/// </summary>
/// <remarks>
/// <para><b>Thread-safety:</b> User-facing API methods (<see cref="ConsumeAsync"/>,
/// <see cref="Subscribe(string[])"/>, <see cref="Assign(TopicPartition[])"/>, <see cref="Seek"/>,
/// <see cref="CommitAsync(CancellationToken)"/>, etc.) are NOT thread-safe and must be called from
/// a single application thread. For parallel consumption, use multiple consumers in a consumer group.</para>
/// <para>However, internal background tasks run concurrently with the user thread:</para>
/// <list type="bullet">
///   <item><description><b>Prefetch loop</b> (<see cref="PrefetchLoopAsync"/>): Runs on a background
///     thread when <c>QueuedMinMessages &gt; 1</c>, fetching records ahead of the consume loop.
///     Coordinates with the user thread via the bounded <see cref="_prefetchBuffer"/> and
///     lock-free <see cref="_eofEmitted"/> for EOF state.</description></item>
///   <item><description><b>Heartbeat</b> (managed by <see cref="ConsumerCoordinator"/>): Sends
///     periodic heartbeats to the group coordinator on a background thread to keep the consumer
///     alive in the group.</description></item>
///   <item><description><b>Auto-commit</b> (<see cref="AutoCommitLoopAsync"/>): Periodically
///     commits consumed offsets on a background thread when <c>OffsetCommitMode.Auto</c> is
///     enabled.</description></item>
/// </list>
/// <para>Thread-safe data structures (<see cref="ConcurrentDictionary{TKey,TValue}"/>,
/// <see cref="ConcurrentQueue{T}"/>, <see cref="System.Threading.Channels.Channel{T}"/>)
/// and locks (<see cref="_assignmentLock"/>) are used to coordinate
/// between the user thread and these background tasks.</para>
/// </remarks>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public sealed partial class KafkaConsumer<TKey, TValue> :
    IKafkaConsumer<TKey, TValue>,
    IKafkaClientInstanceIdentity,
    IKafkaClientStatusProvider,
    IBoundedKafkaConsumer<TKey, TValue>,
    IConsumerGroupLiveness,
    IConsumerPositions,
    IConsumerCommittedOffsets,
    IConsumerLag,
    IRequestWriteSequenceSource,
    IClientTelemetrySource,
    IConsumerPartitions,
    IConsumerOffsets,
    IConsumerRebalanceEventSource,
    IConsumerRecordOwnership<TKey, TValue>,
    IConsumerLoggerFactorySource,
    IConsumerOffsetStoreTimingConfiguration,
    IConsumerBatchOffsetStore,
    DeadLetter.IRawRecordAccessor,
    IBudgetedInstance
{
    internal ValueTask CloseConnectionsForTestingAsync() => _connectionPool.CloseAllAsync();

    /// <summary>
    /// Delay in milliseconds when all assigned partitions are paused, to prevent
    /// a tight spin loop that would starve CPU while still allowing responsive
    /// cancellation and timeout handling (~10 checks per second).
    /// </summary>
    private const int AllPartitionsPausedDelayMs = 100;

    /// <summary>
    /// How many streamed records the buffered drain yields between cancellation/refresh
    /// checks. Internal so benchmarks can keep their seeded record totals off this
    /// boundary instead of hard-coding a copy that drifts.
    /// </summary>
    internal const int PollRefreshRecordInterval = 32;

    private const int MaxConsecutivePrefetchErrors = 50;
    private const int MaxConsecutiveEmptyParsedFetches = 3;
    private const int MaxRepeatedDeterministicPrefetchFailures = 3;
    private const int MaxDeserializerPreparationAttemptsPerComponent = 2;
    private const long FilterRefreshIntervalMilliseconds = 30_000;
    private readonly ConsumerOptions _options;

    /// <summary>
    /// The effective options this consumer was built with. Exposed for tests that
    /// verify builder/preset-to-options mapping.
    /// </summary>
    internal ConsumerOptions Options => _options;

    // Current budget limit in bytes (mutated live by DekafMemoryBudget rebalancing).
    private long _currentQueuedMaxBytes;

    internal ulong CurrentQueuedMaxBytes => (ulong)Volatile.Read(ref _currentQueuedMaxBytes);

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        private NoopDisposable()
        {
        }

        public void Dispose()
        {
        }
    }

    private readonly struct ApiTimeoutScope : IDisposable
    {
        private readonly CancellationTokenSource _timeoutSource;
        private readonly CancellationToken _callerToken;
        private readonly long _startedAt;
        private readonly int _timeoutMs;

        public ApiTimeoutScope(int timeoutMs, CancellationToken callerToken)
        {
            _timeoutMs = timeoutMs;
            _callerToken = callerToken;
            _startedAt = Stopwatch.GetTimestamp();
            _timeoutSource = callerToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(callerToken)
                : new CancellationTokenSource();
            _timeoutSource.CancelAfter(timeoutMs);
        }

        public CancellationToken Token => _timeoutSource.Token;

        public bool DefaultTimeoutExpired =>
            !_callerToken.IsCancellationRequested && _timeoutSource.IsCancellationRequested;

        public KafkaTimeoutException CreateTimeoutException(string operation, Exception innerException)
        {
            // A retried operation reports the failure it was still hitting as the cancellation's
            // cause (RetryHelper deadline mode); that failure is what the caller needs to see.
            if (innerException is OperationCanceledException { InnerException: { } cause })
                innerException = cause;

            var configured = TimeSpan.FromMilliseconds(_timeoutMs);
            return new KafkaTimeoutException(
                TimeoutKind.Api,
                Stopwatch.GetElapsedTime(_startedAt),
                configured,
                $"Consumer API operation '{operation}' did not complete within default API timeout ({_timeoutMs}ms)",
                innerException);
        }

        public void Dispose() => _timeoutSource.Dispose();
    }

    void IBudgetedInstance.OnBudgetChanged(ulong newLimit)
    {
        Interlocked.Exchange(ref _currentQueuedMaxBytes, (long)newLimit);
        RatchetRecordWrapperPools(_assignmentSnapshot.Count);
    }

    private readonly IDeserializer<TKey> _keyDeserializer;
    private readonly IDeserializer<TValue> _valueDeserializer;
    private readonly bool _hasRecordHeaderDeserializers;
    private readonly RecordHeaderRoutingPlan? _recordHeaderRoutingPlan;
    private readonly Headers? _recordHeaderDeserializationHeaders;
    // Non-null when the user configured an IAsyncDeserializer for that component (issue #2309:
    // deserializers that perform per-record I/O, e.g. envelope decryption with short-lived keys).
    // When either is set, ConsumeAsync/ConsumeOneAsync await deserialization per record before
    // constructing the ConsumeResult; the corresponding sync slot holds a throwing
    // AsyncOnlyDeserializerPlaceholder and ConsumeBatchAsync (synchronous iteration) throws.
    private readonly IAsyncDeserializer<TKey>? _asyncKeyDeserializer;
    private readonly IAsyncDeserializer<TValue>? _asyncValueDeserializer;
    private readonly bool _asyncKeyUsesRecordHeaders;
    private readonly bool _asyncValueUsesRecordHeaders;
    private readonly bool _hasAsyncDeserializers;
    private readonly Headers? _asyncDeserializationHeaders;
    private readonly IAsyncDeserializerPreparer<TKey>? _keyDeserializerPreparer;
    private readonly IAsyncDeserializerPreparer<TValue>? _valueDeserializerPreparer;
    private readonly bool _hasDeserializerPreparers;
    private readonly IConnectionPool _connectionPool;
    private readonly MetadataManager _metadataManager;
    private readonly ClientTelemetryMetricCollector _telemetryMetricCollector;
    private readonly ClientTelemetryManager _telemetryManager;
    private readonly FetchBufferMemoryPool _fetchBufferMemoryPool;
    private readonly Diagnostics.ConsumerFetchBufferStateSource _fetchBufferMetricSource;
    private readonly IDekafMemoryBudget _memoryBudget;
    private readonly bool _ownsInfrastructure;
    private readonly ConsumerCoordinator? _coordinator;
    private readonly Func<bool> _tryRecordPollFast;
    private readonly CompressionCodecRegistry _compressionCodecs;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ILogger _logger;

    // _subscription is the authoritative store; _subscriptionSnapshot (below) is its
    // lock-free read side, republished after every mutation. Hot paths must read the
    // snapshot: ConcurrentDictionary.Count/IsEmpty acquire every stripe lock (issue
    // #2211). The _paused/_pausedSnapshot pair follows the same split.
    private readonly ConcurrentDictionary<string, byte> _subscription = new();
    private readonly HashSet<TopicPartition> _assignment = [];
    private volatile HashSet<TopicPartition> _assignmentSnapshot = [];
    private readonly ConcurrentDictionary<TopicPartition, byte> _paused = new();
    private volatile StringSet _subscriptionSnapshot = new HashSet<string>();
    private volatile TopicPartitionSet _pausedSnapshot = new HashSet<TopicPartition>();
    private int _pausedSnapshotVersion;

    // Pattern subscription support
    private volatile Func<string, bool>? _topicFilter;
    private volatile string? _topicPattern;
    private long _lastFilterRefreshTicks;

    // Thread-safety notes:
    // - _positions and _fetchPositions use ConcurrentDictionary for thread-safe reads/writes
    // - Individual operations (e.g., dict[key] = value) are atomic, but sequences like:
    //     _positions[tp] = offset;
    //     _fetchPositions[tp] = offset;
    //   are NOT atomic across both dictionaries. This is a benign race - the worst case is
    //   a single fetch using a stale position before being updated by the next operation.
    //   Adding locks would defeat the purpose of lock-free consumption.
    private readonly ConcurrentDictionary<TopicPartition, long> _positions = new();      // Consumed position (what app has seen)
    // The stored offset of each partition with the ownership it was stored under (the partition's
    // ownership start S for a group-managed consumer, 0 otherwise) and its leader epoch, in one
    // slot per partition updated in place. A commit takes a dirty slot only when its ownership is
    // the partition's current one, so a store validated under an ended ownership never commits.
    private readonly ConcurrentDictionary<TopicPartition, StoredOffsetSlot> _storedOffsetSlots = new();
    private static readonly Func<TopicPartition, StoredOffsetSlot> s_createStoredOffsetSlot = static _ => new StoredOffsetSlot();
    // Group-managed (subscribed) consumer; republished with the subscription snapshot.
    private volatile bool _isGroupManaged;
    private readonly ConcurrentDictionary<TopicPartition, long> _fetchPositions = new(); // Fetch position (what to fetch next)
    // Seeks and pauses an OnPartitionsAssigned callback makes for a partition it announced belong to
    // the ownership that callback starts, which assignment sync has not initialized yet.
    //
    // Three assignments are in play and must not be confused:
    //   published     the coordinator's current assignment and version (callbacks announce it)
    //   synchronized  _assignment/_assignmentSnapshot: what a sync pass has applied, possibly a pass
    //                 later cancelled, failed or superseded before its acknowledgement
    //   acknowledged  _acknowledgedCoordinatorAssignment: the last pass the coordinator confirmed
    //                 was still the published version (AcknowledgeAssignmentSync returned true).
    //                 It names partitions; ownership identity is the coordinator's monotonic
    //                 revocation sequence: P's acknowledged ownership has ended when P's latest
    //                 revocation sequence is newer than the ones acknowledged passes covered
    // Callback state is completed only by an acknowledged pass, and abandon decides which pauses
    // survive from the acknowledged assignment, never from a merely synchronized one.
    //
    // Per partition P, the state and every transition (tested by RebalanceCallbackStateTransitions):
    //
    //   _pendingRebalanceSeeks[P]      seek staged by such a callback (either listener kind)
    //   _rebalancePausedPartitions[P]  the callback paused P: true while that ownership is current,
    //                                  false once P is revoked or lost after the callback
    //   _unacknowledgedAppliedRebalanceSeeks[P]  the staged seek a sync pass applied, not yet acked
    //
    //   callback Seek      -> staged, unless P was revoked since the callback's notification
    //                         (StageRebalanceSeek); applied at once if P is already synchronized
    //   callback Pause     -> _paused[P] and marker true, one step under _pauseStateLock and the
    //                         revocation lock
    //   stale callback     -> P revoked or lost since the callback's notification (even if the
    //                         callback since synchronized a newer ownership): its Pause, Resume and
    //                         Seek of P are no-ops, decided before anything changes
    //   revoke / lost      -> staged seek dropped; marker true -> false (the pause stays in _paused
    //                         until cleanup, which knows whether P was ever synchronized)
    //   Resume             -> _paused[P] and marker removed together, under _pauseStateLock
    //                         (not from a stale callback)
    //   sync cleanup       -> P revoked and not reassigned: all of it removed (RemovePartitionState).
    //                         P reassigned: previous ownership's pause and position cleared; marker
    //                         true restores the pause and stays (a retried pass restores it again);
    //                         marker false is removed; the staged seek stays
    //   sync position init -> staged seek applied but kept staged; recorded as applied (latest per P)
    //   sync retry         -> (cancelled, failed, superseded) revocations restored; the next pass
    //                         repeats cleanup and initialization with the same marker and seek
    //   sync ack           -> only when the coordinator confirms the pass's version is still the
    //                         published one: applied seeks removed if unchanged, the acknowledged
    //                         assignment recorded. A pass superseded between its version check and
    //                         the ack (classification-only or real change) completes nothing.
    //                         Markers left are inert (the next revocation flips them false, cleanup
    //                         or Resume removes them)
    //   reclassified       -> (newly expanded, reinitialized) staged seek and marker kept: same
    //                         ownership
    //   queued callback    -> stages only if the heartbeat whose response published its assignment
    //                         was stamped with the current subscription generation: any abandon
    //                         after that request was built (before or after publication, or during
    //                         the callback) ends its staging. The membership and heartbeat loop
    //                         outlive an abandon; their responses answer the abandoned subscription
    //                         until the consumer ensures the group with a newer one
    //   abandon            -> Unsubscribe, Subscribe (topics, filter or pattern), Assign, Unassign,
    //                         IncrementalAssign before sync: every staged seek and marker dropped,
    //                         the pause of a marked partition removed unless the marker is current
    //                         and P is in the acknowledged assignment and not revoked since (the
    //                         pause was made under the acknowledged ownership), the acknowledged
    //                         assignment reset, and a running assigned
    //                         callback's staging ended (its later seeks, pauses and position reads
    //                         act on the consumer directly)
    //   close              -> as abandon, after the synchronized partitions are removed
    //   abandon also drops the revocation-sequence bookkeeping (pruned on the coordinator too):
    //                         manual assignment never acknowledges a group sync
    private readonly ConcurrentDictionary<TopicPartition, TopicPartitionOffset> _pendingRebalanceSeeks = new();
    private readonly ConcurrentDictionary<TopicPartition, bool> _rebalancePausedPartitions = new();
    // Guarded by _assignmentLock. At most one entry per partition, so retries do not accumulate.
    private readonly Dictionary<TopicPartition, TopicPartitionOffset> _unacknowledgedAppliedRebalanceSeeks = [];
    // The group assignment of the last sync pass the coordinator confirmed as current (see the
    // states above); empty after an abandon. Replaced, never mutated. Written under _assignmentLock.
    private volatile HashSet<TopicPartition> _acknowledgedCoordinatorAssignment = [];
    // Revocation sequences (the coordinator's, monotonic) by partition: drained by sync passes
    // since the last acknowledged one, and covered by acknowledged passes. A partition was revoked
    // since its acknowledged ownership when the coordinator's latest revocation sequence for it is
    // newer than the acknowledged one. A superseded pass's drained sequences are covered by the
    // next acknowledged pass, whichever path acknowledges it. Entries covered with no newer
    // revocation are pruned on both sides at acknowledgement. Guarded by _assignmentLock.
    private readonly Dictionary<TopicPartition, long> _drainedRevocationSequences = [];
    private readonly Dictionary<TopicPartition, long> _acknowledgedRevocationSequences = [];
    // Serializes each Pause/Resume with assignment cleanup's clear-then-restore of a partition's
    // pause, so neither interleaves inside the other's check-then-act. Taken before
    // _coordinatorRevokedPartitionsPendingFetchClearLock, never while holding it. Not per message.
    private readonly object _pauseStateLock = new();
    // Last consumed record-batch leader epoch. Sent as FetchRequest.LastFetchedEpoch while the
    // fetch position is the consumed position (no prefetch, or just after a seek/reset).
    private readonly ConcurrentDictionary<TopicPartition, int> _lastConsumedLeaderEpochs = new();
    // Leader epoch of the batch that ended at the prefetch position, which runs ahead of the
    // consumed one. Kept with that position (see FetchedLeaderEpoch) so an epoch recorded for
    // another position is never paired with the current one.
    private readonly ConcurrentDictionary<TopicPartition, FetchedLeaderEpoch> _lastFetchedLeaderEpochs = new();
    // OffsetFetch snapshots must not replace commits that complete after the request starts.
    private readonly ConcurrentDictionary<TopicPartition, CommittedOffsetCacheEntry> _committed = new();
    private long _committedOffsetGeneration;
    private readonly ConcurrentDictionary<TopicPartition, WatermarkCacheEntry> _watermarks = new(); // Cached watermark offsets from fetch responses
    private readonly ConcurrentDictionary<TopicPartition, int> _watermarkAssignmentVersions = new();
    // Fetch generation at which each assigned partition's current ownership began, and the latest
    // ownership start or end: a record fetched after it, with no coordinator change since the last
    // sync, belongs to a current ownership without further lookups.
    // Record ownership invariants. One process-wide 64-bit counter (PendingFetchData's fetch
    // generation) orders three kinds of events; each takes a new, larger value:
    //   F  a record-bearing fetch is created (the value its records carry, low 32 bits);
    //   R  the coordinator publishes a revocation or loss of partitions (NotifyRevoking, before
    //      any revocation callback runs, before the revocation is enqueued for sync);
    //   S  assignment sync publishes a partition's ownership start (new or assigned again).
    // State, per partition:
    //   _ownershipStartGenerations[p] = S of the current ownership; absent iff p is unassigned.
    //   _pendingRevocations[p] = latest R not yet applied by a sync. An immutable map, replaced
    //      under _pendingRevocationsLock and published with one volatile write, so an R becomes
    //      visible for all its partitions at once (that write is its linearization point). Raised
    //      only by the R hook (never lowered); an entry is removed only by the sync publication
    //      that drained exactly that R or a later one (ForgetDrainedRevocations compares with the
    //      drained entry's own generation, never with the counter, so an R taken but not yet
    //      enqueued during a drain survives it). The sync forgets it last, under _snapshotStateGate,
    //      after it cleared the old ownership's stored offsets and positions; a stored-offset
    //      commit decides ownership under that gate too, so it never sees stale state as owned.
    //   _latestOwnershipChangeFetchGeneration = max over every R and S (only raised; an R raises
    //      it before its map is published).
    // Stored offsets carry the ownership they were stored under (one StoredOffsetSlot per
    //   partition: S of the record's fetch, or of the partition at an explicit store/seek; 0 when
    //   not group-managed). A slot never takes a store of an earlier ownership than it holds
    //   (starts only grow). A commit takes a slot only if its ownership is the partition's current
    //   S, so a store validated under an ownership that ended before the write landed never
    //   commits; such a slot is marked clean by the commit path.
    // Rewinds the hosted service makes (pause and seek) are decided and applied in one step under
    //   _snapshotStateGate (RewindIfOwned), so an ownership change cannot fall between them.
    // Member epoch: the coordinator publishes a heartbeat's new member epoch only after it has
    //   recorded that heartbeat's revocations (R). A stored-offset commit reads the epoch first
    //   and decides ownership after, so with the new epoch it also sees the revocation pending;
    //   with the old one the broker rejects it (StaleMemberEpoch) once the revocation completed.
    // f is the full 64-bit generation: read from the record's fetch while that fetch is in the
    //   same use, else expanded from the 32 bits the result keeps (exact within 2^32 generations).
    // Classification of a record of p with fetch generation f (f = 0: not from a fetch, unknown):
    //   Ended             p unassigned, or f < S(p) (fetched under an earlier ownership);
    //   RevocationPending an unapplied R exists for p;
    //   Owned             otherwise.
    // Fast path: no pending R at all, f > latest change, and the coordinator's assignment version
    //   equals the last synced one implies Owned (no S or R since the fetch, nothing unapplied).
    //   An R disables it with the same volatile write that makes the R visible to the slow path.
    // Every writer below goes through StartOwnership, EndOwnership, RecordCoordinatorRevocation
    // or ForgetDrainedRevocations; every reader through GetRecordOwnership, IsRevocationPending or
    // IsFetchedUnderCurrentSynchronizedOwnership.
    private readonly ConcurrentDictionary<TopicPartition, long> _ownershipStartGenerations = new();
    private readonly object _pendingRevocationsLock = new();
    // Empty: the sentinel itself, so the fast path checks it with one reference comparison.
    private static readonly Dictionary<TopicPartition, long> s_noPendingRevocations = [];
    private volatile Dictionary<TopicPartition, long> _pendingRevocations = s_noPendingRevocations;
    private long _latestOwnershipChangeFetchGeneration;
    // Deterministic test seam: runs in RecordCoordinatorRevocation after the new map is built and
    // before it is published.
    internal static Action<object>? BeforePendingRevocationsPublishedForTest;


    // Partition EOF tracking
    private readonly ConcurrentDictionary<TopicPartition, long> _highWatermarks = new();  // High watermark per partition (thread-safe for prefetch)
    private readonly ConcurrentDictionary<TopicPartition, byte> _eofEmitted = new(); // Partitions where EOF has been emitted (lock-free)
    private readonly ConcurrentQueue<(TopicPartition Partition, long Offset)> _pendingEofEvents = new(); // Pending EOF events to yield (thread-safe for prefetch thread)

    // Pending fetch responses for lazy record iteration
    private readonly Queue<PendingFetchData> _pendingFetches = new();
    // Foreground-owned holding queue for already-fetched paused partitions. Moving a fetch
    // here preserves its iterator, pooled storage, offsets, and per-partition order.
    private readonly Queue<PendingFetchData> _pausedPendingFetches = new();
    private readonly Queue<PendingFetchData> _pendingFetchScratch = new();
    // Reused by the consume loop to rewind a skipped batch without allocating a set.
    private readonly HashSet<TopicPartition> _skippedBatchPartitions = [];
    // Partitions whose skipped batch could not be released. Their fetches wait in
    // _heldSkippedFetches during a batch loop and are offered again after a bounded wait.
    private readonly HashSet<TopicPartition> _heldSkippedPartitions = [];
    private readonly Queue<PendingFetchData> _heldSkippedFetches = new();
    // Partitions with a queued fetch, built only when an EOF drain starts with fetches queued.
    private readonly HashSet<TopicPartition> _eofHoldPartitions = [];
    // Batch-loop state kept on the consumer, not hoisted into the async iterators, so the
    // per-stream iterator allocation does not grow. Only the consume loop touches them.
    private int _eofDrainRemaining;
    // Per partition: queued EOFs below this offset were superseded by records published after
    // them. Written under the invalidation lock (or on the consumer thread for direct fetches).
    private readonly ConcurrentDictionary<TopicPartition, long> _eofSupersededBelow = new();
    // Set after a bound is written to _eofSupersededBelow, cleared before a full clear. The EOF
    // drain reads it instead of ConcurrentDictionary.IsEmpty, which takes every bucket lock when
    // the dictionary is empty: the normal path's case, and it ran once per delivered EOF.
    private volatile bool _hasEofSupersededBounds;
    // Set by CompleteBatchPoll and reset by BeginBatchStream. Safe as a consumer field only
    // because the batch APIs are single-consumer: one stream enumerates at a time.
    private bool _batchLoopExitRequested;
    private int _observedPausedSnapshotVersion;
    private int _recordIterationEpochSeed;
    private int _pendingFetchDepth;
    // ConsumeOne callbacks run on the documented single application thread. If one
    // reentrantly clears the queue, defer this fetch's disposal until all borrowed
    // record memory is out of user code. Plain references avoid per-message atomics.
    private PendingFetchData? _activeConsumeOneFetch;
    private PendingFetchData? _deferredConsumeOneFetchDisposal;
    // Auto-commit reads this snapshot from its background task instead of touching _pendingFetches.
    private string? _activeConsumedTopic;
    private int _activeConsumedPartition;
    private long _activeConsumedPosition;
    private int _activeConsumedLeaderEpoch = -1;
    // The ownership start of the fetch the active position came from, written with the partition
    // (the per-record fast path rewrites only the position; every ownership change clears the
    // snapshot first). Lets a commit flush store the position under the ownership it was
    // consumed in, after the fetch itself left the queue.
    private long _activeConsumedOwnership;
    private int _activeConsumedPositionVersion;

    // Incremented whenever queued fetch data is disposed (Seek/Assign clear the buffer).
    // ConsumeAsync iterates the front of _pendingFetches while it is still queued, and
    // user code at the yield point can trigger such a clear; the version check lets the
    // iterator detect that its current fetch was disposed underneath it.
    // Coarse-grained by design: unrelated partition clears may restart the current
    // iteration, avoiding per-partition tracking on the per-message hot path.
    private int _pendingFetchesVersion;
    // Generates fetch epochs. Full clears advance the global minimum; partition clears advance
    // only that partition's minimum so an unrelated broker response can still be consumed.
    private int _fetchBufferEpoch;
    private int _minimumFetchBufferEpoch;
    private readonly ConcurrentDictionary<TopicPartition, int> _minimumFetchBufferEpochsByPartition = new();
    // Fetch-clear publications share this partition set because they must stop record iteration
    // before the poll loop clears stale data. Version and source let topic recovery claim one exact
    // old-identity marker without consuming an explicit position change. The flags avoid dictionary
    // work on the normal path.
    private readonly object _coordinatorRevokedPartitionsPendingFetchClearLock = new();
    private readonly ConcurrentDictionary<TopicPartition, long> _coordinatorRevokedPartitionsPendingFetchClear = new();
    private readonly Dictionary<TopicPartition, PendingFetchClearMarkerSource> _pendingFetchClearMarkerSources = [];
    // Keep newer marker evidence until an in-flight topic-identity reset validates it.
    private readonly HashSet<TopicPartition> _topicIdentityResetPartitions = [];
    private readonly ConcurrentDictionary<TopicPartition, (long EndOffset, int Epoch)> _pendingDivergingEpochResets = new();
    private long _pendingFetchClearVersion;
    private int _stagedDivergingEpochResetBatches;
    private int _coordinatorRevokedPartitionsPendingFetchClearMarkerPresent;
    private int _coordinatorRevokedPartitionsPendingFetchClearPending;
    private readonly BatchIterationEpoch _batchIterationEpoch = new();

    private enum PendingFetchClearMarkerSource : byte
    {
        PositionChange,
        CoordinatorRevocation,
        DivergingEpoch
    }

    // Background prefetch support
    private readonly MpscFetchBuffer _prefetchBuffer;
    private readonly object _prefetchStartLock = new();
    private CancellationTokenSource? _prefetchCts;
    private Task? _prefetchTask;
    private long _prefetchedBytes;
    // Initial count 1: at startup, memory IS available (_prefetchedBytes = 0). Starting at 0
    // causes a deadlock if the prefetch loop fills memory before the consumer reads anything —
    // the loop waits for a Release() that never comes because the consumer has no data yet.
    // Max count 1: the semaphore is an edge-triggered memory-available signal, not a permit
    // counter. Extra releases while nobody is waiting must not accumulate and later spin the
    // prefetch loop through stale permits.
    private readonly SemaphoreSlim _prefetchMemoryAvailable = new(1, 1);

    // Per-fetch reusable lists for collecting pending items during prefetch (avoids per-cycle allocation)
    // Keyed by (brokerId, connectionIndex) since PrefetchFromBrokerAsync runs concurrently
    // for multiple brokers AND multiple connections to the same broker.
    // Stale entries from scaled-down connections are pruned lazily before dispatching broker prefetches.
    private readonly ConcurrentDictionary<(int BrokerId, int ConnectionIndex), List<PendingFetchData>> _prefetchPendingItemsByBroker = new();
    private readonly BrokerPrefetchScheduler _brokerPrefetchScheduler = new();
    private readonly ConcurrentDictionary<(int BrokerId, int ConnectionIndex), FetchSessionHandler> _fetchSessions = new();
    // KIP-74 request-order cursor. KIP-227 rotates incremental-session responses on the broker;
    // this cursor orders full requests, including session creation and reset, per connection.
    private readonly ConcurrentDictionary<(int BrokerId, int ConnectionIndex), FetchPartitionOrderState>
        _fetchPartitionOrderStates = new();
    private readonly StuckFetchPositionTracker _stuckFetchPositionTracker = new(MaxConsecutiveEmptyParsedFetches);
    private readonly PrefetchFailureTracker _prefetchFailureTracker;

    // Lock ordering (always acquire in this order to prevent deadlocks):
    //   1. _initLock          — guards one-time initialization; never held while acquiring other locks
    //   2. _assignmentLock    — serializes assignment changes between the consume loop and prefetch loop
    //   3. _snapshotStateGate — publishes assignment/snapshot state; may also be acquired independently,
    //      but never acquire _assignmentLock while holding it
    //   4. _autoCommitStartLock / _prefetchStartLock — guard background loop start/stop snapshots;
    //      never held while awaiting. When both are needed, acquire auto-commit before prefetch.
    //   5. Offset reset acquires _coordinatorRevokedPartitionsPendingFetchClearLock, then
    //      ClusterMetadata.UpdateLock, then (when invalidating routing) _partitionCacheLock.
    //      Metadata writers never acquire consumer locks; preserve this direction.
    //   6. _partitionCacheLock / _fetchCacheLock — guard per-broker partition cache and fetch request
    //      cache respectively; acquired under _assignmentLock (via InvalidatePartitionCache /
    //      InvalidateFetchRequestCache) and independently; never nested with each other
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly SemaphoreSlim _assignmentLock = new(1, 1);

    private readonly ConcurrentDictionary<CancellationTokenSource, byte> _activeConsumeCancellationSources = new();
    private CancellationTokenSource? _pausedDirectFetchCancellationSource;
    private readonly CancellationTokenSource _leaderRefreshCts = new();
    private readonly object _autoCommitStartLock = new();
    private CancellationTokenSource? _autoCommitCts;
    private Task? _autoCommitTask;
    private readonly ConsumerConnectionScaler? _connectionScaler;
    private int _appliedConnectionCount;
    private Task? _connectionRoutingTransitionTask;
    private readonly ConcurrentDictionary<Task, byte> _retiredConnectionDisposalTasks = new();
    private readonly AdaptiveFetchSizer? _adaptiveFetchSizer;
    private int _adaptiveFetchMemoryPressureSignals;

    // Dead letter queue raw byte tracking (zero overhead when not enabled)
    private bool _rawRecordTrackingEnabled;
    private ReadOnlyMemory<byte> _currentRawKey;
    private ReadOnlyMemory<byte> _currentRawValue;

    private int _consumerDisposed;
    private int _closed;
    private volatile bool _initialized;
    private volatile bool _prefetchEnabled;

    // CancellationTokenSource pool to avoid allocations in hot paths
    private readonly CancellationTokenSourcePool _ctsPool;
    private readonly Action<TopicPartition, long, int>? _storeOffsetOnDelivery;
    private readonly Action<PendingFetchData, long> _rewindBatchAfterDeliveryFailure;

    // Cached metric tags per topic to avoid per-message TagList allocation
    // Plain Dictionary is safe: only accessed from the single ConsumeAsync loop thread
    private readonly Dictionary<string, System.Diagnostics.TagList> _metricTagsCache = [];
    private readonly ConcurrentDictionary<int, TagList> _fetchDurationMetricTagsCache = new();

    private const long EarliestOffsetTimestamp = -2;
    private const long LatestOffsetTimestamp = -1;
    private const long NoPendingFetchClearVersion = 0;

    // Cached activity names per topic to avoid repeated string interpolation in fetch paths
    // Instance-level to avoid unbounded growth with dynamic topic names across consumer instances
    private readonly ConcurrentDictionary<string, string> _activityNameCache = new();
    private readonly ConcurrentDictionary<string, string> _pollActivityNameCache = new();

    // Interceptors - stored as typed array for zero-allocation iteration
    private readonly IConsumerInterceptor<TKey, TValue>[]? _interceptors;
    private readonly Func<ConsumeResult<TKey, TValue>, ConsumeResult<TKey, TValue>>? _onBatchConsume;

    // Incremental fetch responses can omit unchanged empty partitions; EOF mode needs those
    // empty partition responses to emit partition EOF events.
    private bool ShouldUseFetchSessions => _options.EnableFetchSessions && !_options.EnablePartitionEof;

    // Cached partition grouping by broker to avoid allocations on every fetch.
    // Rebuilt when assignment, preferred replicas, or metadata freshness make it stale;
    // pause/resume updates it incrementally when the cache is already populated.
    // Access to _cachedPartitionsByBroker must be synchronized via _partitionCacheLock
    private PartitionBrokerCacheEntry? _cachedPartitionsByBroker;
    private readonly object _partitionCacheLock = new();
    private int _assignmentVersion;

    // Fetch request cache - reduces allocations when partition assignment is stable.
    // Keyed by partition subrange so adaptive multi-connection fetches can reuse templates.
    // Cache is invalidated when assignment or paused partitions change.
    private readonly object _fetchCacheLock = new();
    private readonly Dictionary<FetchRequestCacheKey, FetchRequestTemplateCacheEntry> _fetchRequestTemplateCache = [];
    private readonly ConcurrentDictionary<TopicPartition, PreferredReadReplicaState> _preferredReadReplicas = new();
    private readonly object _leaderRefreshTasksLock = new();
    private readonly ConcurrentDictionary<string, Task> _pendingLeaderRefreshTasks = new();
    // Topic identity checks run once per immutable metadata snapshot, never per message.
    // The semaphore serializes the consume and prefetch loops when a new snapshot arrives.
    private readonly SemaphoreSlim _topicIdentityLock = new(1, 1);
    private readonly Dictionary<string, Guid> _observedTopicIds = [];
    // A metadata snapshot means observation is current. An assignment snapshot is an invalidation token.
    private object? _observedTopicIdentityMarker;
    private int _assignmentEnsureVersion;
    private int _lastManualAssignmentEnsureVersion = -1;
    private int _lastCoordinatorAssignmentVersion = -1;
    // Commit state of the partitions the consumer held when it last ended its group membership
    // (Unsubscribe or a switch to manual assignment), which cleared them. Consumed per partition
    // by the revocations of that membership and by CommitAsync; inert once the membership has
    // changed. Control plane.
    private LeaveCommitSnapshot? _departingOffsets;
    // The capture EndGroupMembership started, completed by the clear that follows it in the same
    // call (ClearFetchBuffer adds the fetches it discards); null otherwise.
    private LeaveCommitSnapshot? _leaveFetchCapture;
    // Deterministic test seam for assignment/revocation snapshot races.
    internal Action? BeforeCoordinatorAssignmentSnapshotForTest { get; set; }

    // Runs after a sync pass's last version check, just before it acknowledges the sync.
    internal Action? BeforeAssignmentSyncAcknowledgedForTest { get; set; }

    // Runs after EnsureAssignmentAsync captured the subscription, before it ensures group membership.
    internal Action? BeforeEnsureActiveGroupForTest { get; set; }

    // Runs once the coordinator confirmed a sync pass, before the consumer completes it.
    internal Action? AfterAssignmentSyncAcknowledgedForTest { get; set; }
    // Deterministic test seam: runs in assignment sync after the partitions assigned again are
    // identified and before revoked-partition state is cleaned up. It receives the consumer, so
    // a static keeps the instance layout unchanged and parallel tests can filter by it.
    internal static Action<object>? BeforeRevokedPartitionStateCleanupForTest;
    // Test hooks inside the pause state transitions: after assignment cleanup removes a partition's
    // pause, and after Pause adds one. They receive the consumer and the partition.
    internal static Action<object, TopicPartition>? AfterPartitionPauseClearedForTest;
    internal static Action<object, TopicPartition>? AfterPartitionPausedForTest;

    internal int UnacknowledgedAppliedRebalanceSeekCountForTest => _unacknowledgedAppliedRebalanceSeeks.Count;

    internal int PendingRebalanceSeekCountForTest => _pendingRebalanceSeeks.Count;

    internal int RebalancePausedPartitionCountForTest => _rebalancePausedPartitions.Count;

    internal int RevocationSequenceTrackingCountForTest =>
        _acknowledgedRevocationSequences.Count + _drainedRevocationSequences.Count;

    internal bool IsRevokedSinceAcknowledgedForTest(TopicPartition partition) =>
        IsRevokedSinceAcknowledged(partition);

    /// <summary>
    /// Whether the partition was revoked or lost after the revocations the acknowledged passes
    /// covered: the acknowledged assignment then names an ownership that has ended. Caller holds
    /// <c>_assignmentLock</c>.
    /// </summary>
    private bool IsRevokedSinceAcknowledged(TopicPartition partition) =>
        _coordinator is { } coordinator
        && coordinator.GetLastRevocationSequence(partition)
            > (_acknowledgedRevocationSequences.TryGetValue(partition, out var acknowledged) ? acknowledged : 0);

    // Deterministic test seam: runs on the commit path after each stored offset's ownership is
    // evaluated, so a test can change ownership the way a concurrent assignment sync would.
    internal static Action<object>? AfterStoredOffsetOwnershipEvaluatedForTest;
    // Deterministic test seam: runs in a commit's flush after the active consumed position is
    // read and before it is stored, so a test can revoke and reassign the partition in between.
    internal static Action<object>? AfterActiveConsumedPositionReadForTest;
    // Deterministic test seam: runs in assignment sync right after it publishes the assignment,
    // under the snapshot gate, before the old ownership's state is cleaned up.
    internal static Action<object>? AfterAssignmentSyncPublishedForTest;
    // Deterministic test seam: runs in StoreOffset(ConsumeResult) after the record's ownership was
    // checked and before the offset is written.
    internal static Action<object>? AfterStoreOffsetOwnershipCheckedForTest;
    // Thread-local storage keeps the production consumer's instance layout unchanged.
    [ThreadStatic]
    internal static Action? BeforeOffsetResetCommitForTest;
    // Deterministic test seam for watermark creation/assignment races. Thread-local static
    // storage avoids changing the production consumer's instance layout.
    [ThreadStatic]
    private static Action? _beforeWatermarkCacheEntryCreationForTest;
    internal static Action? BeforeWatermarkCacheEntryCreationForTest
    {
        get => _beforeWatermarkCacheEntryCreationForTest;
        set => _beforeWatermarkCacheEntryCreationForTest = value;
    }
    // Foreground-applied pause state. Keep at the cold field tail so the normal consume layout
    // remains stable; only queue admission/reconciliation reads or writes it.
    private TopicPartitionSet _deliveryPausedSnapshot = new HashSet<TopicPartition>();
    // Prevent Resume from cancelling a pooled CTS after it has been returned and re-rented.
    private readonly object _pausedDirectFetchCancellationSourceLock = new();
    // Cold capability state stays at the tail so normal consume field layout remains stable.
    private SnapshotConsumeState? _activeSnapshot;
    private int _snapshotOperationActive;
    private readonly object _snapshotStateGate = new();
    // Captured before each fetch/ListOffsets request so delayed responses cannot regress the cache.
    private long _watermarkUpdateSequence;
    // Cold-path FIFO for bounding snapshots retained after unassignment or direct queries.
    private Queue<RetainedWatermarkSnapshot>? _retainedWatermarkSnapshots;

    private static readonly long s_preferredReadReplicaMaxAgeTimestampDelta =
        (long)(TimeSpan.FromMinutes(5).TotalSeconds * Stopwatch.Frequency);
    private const long NoPreferredReplicaExpiry = long.MaxValue;
    private const int MaxRetainedUnassignedWatermarkSnapshots = 256;

    private readonly record struct PartitionBrokerCacheEntry(
        Dictionary<int, List<TopicPartition>> PartitionsByBroker,
        long PreferredReplicaExpiresAtTimestamp,
        DateTimeOffset MetadataLastRefreshed);

    private readonly record struct PartitionFetchBrokerResolution(
        TopicPartition Partition,
        BrokerNode? Broker,
        long PreferredReplicaExpiresAtTimestamp);

    private readonly record struct PreferredReadReplicaState(
        int ReplicaId,
        DateTimeOffset MetadataLastRefreshed,
        long ExpiresAtTimestamp);

    private readonly record struct CommittedOffsetCacheEntry(long Offset, long Generation);

    public KafkaConsumer(
        ConsumerOptions options,
        IDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer,
        ILoggerFactory? loggerFactory = null,
        MetadataOptions? metadataOptions = null,
        IAsyncDeserializer<TKey>? asyncKeyDeserializer = null,
        IAsyncDeserializer<TValue>? asyncValueDeserializer = null)
        : this(options, keyDeserializer, valueDeserializer,
            CreateInfrastructure(options, loggerFactory, metadataOptions),
            loggerFactory,
            ownsInfrastructure: true,
            DekafMemoryBudget.Global,
            asyncKeyDeserializer,
            asyncValueDeserializer)
    {
    }

    /// <summary>
    /// Internal constructor for unit testing — accepts pre-built infrastructure dependencies
    /// so tests can inject mock <see cref="IConnectionPool"/> and <see cref="MetadataManager"/>.
    /// </summary>
    internal KafkaConsumer(
        ConsumerOptions options,
        IDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer,
        IConnectionPool connectionPool,
        MetadataManager metadataManager,
        ILoggerFactory? loggerFactory = null)
        : this(options, keyDeserializer, valueDeserializer,
            (
                connectionPool,
                metadataManager,
                new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Consumer),
                new FetchBufferMemoryPool(options.FetchBufferMemoryBytes)),
            loggerFactory,
            ownsInfrastructure: true,
            DekafMemoryBudget.Global)
    {
    }

    internal KafkaConsumer(
        ConsumerOptions options,
        IDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer,
        IConnectionPool connectionPool,
        MetadataManager metadataManager,
        IDekafMemoryBudget memoryBudget,
        ILoggerFactory? loggerFactory = null,
        IAsyncDeserializer<TKey>? asyncKeyDeserializer = null,
        IAsyncDeserializer<TValue>? asyncValueDeserializer = null)
        : this(options, keyDeserializer, valueDeserializer,
            (
                connectionPool,
                metadataManager,
                new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Consumer),
                new FetchBufferMemoryPool(options.FetchBufferMemoryBytes)),
            loggerFactory,
            ownsInfrastructure: false,
            memoryBudget,
            asyncKeyDeserializer,
            asyncValueDeserializer)
    {
    }

    private static (
        IConnectionPool,
        MetadataManager,
        ClientTelemetryMetricCollector,
        FetchBufferMemoryPool) CreateInfrastructure(
        ConsumerOptions options, ILoggerFactory? loggerFactory, MetadataOptions? metadataOptions)
    {
        var reconnectBackoffMaxMs = ReconnectBackoffValidation.ResolveMaximumMilliseconds(
            options.ReconnectBackoffMs,
            options.ReconnectBackoffMaxMs,
            options.IsReconnectBackoffMsConfigured,
            options.IsReconnectBackoffMaxMsConfigured);
        ValidateFetchBufferMemory(options);
        ValidateDefaultApiTimeout(options);
        var telemetryMetricCollector = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Consumer);
        var fetchBufferMemoryPool = new FetchBufferMemoryPool(options.FetchBufferMemoryBytes);
        var connectionPool = new ConnectionPool(
            options.ClientId,
            new ConnectionOptions
            {
                UseTls = options.UseTls,
                TlsConfig = options.TlsConfig,
                RemoteCertificateValidationCallback = options.RemoteCertificateValidationCallback,
                ConnectionTimeout = options.ConnectionTimeout,
                ConnectionTimeoutMax = options.ConnectionTimeoutMax,
                EnableTcpKeepAlive = options.EnableTcpKeepAlive,
                TcpKeepAliveTime = options.TcpKeepAliveTime,
                TcpKeepAliveInterval = options.TcpKeepAliveInterval,
                TcpKeepAliveRetryCount = options.TcpKeepAliveRetryCount,
                RequestTimeout = TimeSpan.FromMilliseconds(options.RequestTimeoutMs),
                ReconnectBackoff = TimeSpan.FromMilliseconds(options.ReconnectBackoffMs),
                ReconnectBackoffMax = TimeSpan.FromMilliseconds(reconnectBackoffMaxMs),
                ConnectionsMaxIdleMs = options.ConnectionsMaxIdleMs,
                SaslMechanism = options.SaslMechanism,
                SaslUsername = options.SaslUsername,
                SaslPassword = options.SaslPassword,
                SaslCredentialProvider = options.SaslCredentialProvider,
                SaslScramTokenAuth = options.SaslScramTokenAuth,
                SaslScramMaxIterations = options.SaslScramMaxIterations,
                GssapiConfig = options.GssapiConfig,
                OAuthBearerConfig = options.OAuthBearerConfig,
                OAuthBearerTokenProvider = options.OAuthBearerTokenProvider,
                AwsMskIamConfig = options.AwsMskIamConfig,
                SendBufferSize = options.SocketSendBufferBytes,
                ReceiveBufferSize = options.SocketReceiveBufferBytes,
                ClientDnsLookup = options.ClientDnsLookup,
                DnsResolver = options.DnsResolver
            },
            loggerFactory,
            connectionsPerBroker: options.ConnectionsPerBroker,
            CreateResponseBufferPool(options),
            telemetryMetricCollector: telemetryMetricCollector,
            responseMemoryAdmissionsEnabled: true);

        metadataOptions ??= new MetadataOptions
        {
            MetadataRecoveryStrategy = options.MetadataRecoveryStrategy,
            MetadataClusterCheckEnabled = options.MetadataClusterCheckEnabled,
            RetryBackoffMs = options.RetryBackoffMs,
            RetryBackoffMaxMs = options.RetryBackoffMaxMs,
            BootstrapResolveTimeoutMs = options.BootstrapResolveTimeoutMs
        };
        var metadataManager = new MetadataManager(
            connectionPool,
            options.BootstrapServers,
            options: metadataOptions,
            logger: loggerFactory?.CreateLogger<MetadataManager>());

        return (connectionPool, metadataManager, telemetryMetricCollector, fetchBufferMemoryPool);
    }

    private static ResponseBufferPool CreateResponseBufferPool(ConsumerOptions options)
    {
        var responseBufferWorkingSet = PoolSizing.ForConsumerResponseBuffers(
            options.BootstrapServers.Count,
            options.PrefetchPipelineDepth,
            options.MaxConnectionsPerBroker);
        return ResponseBufferPool.Create(
            CalculateMaximumFetchResponsePayloadBytes(options),
            managedArraysPerBucket: responseBufferWorkingSet,
            maxRetainedNativeBuffers: responseBufferWorkingSet);
    }

    internal static int CalculateMaximumFetchResponsePayloadBytes(ConsumerOptions options)
    {
        var maximumFetchBytes = options.FetchMaxBytes;
        var maximumPartitionFetchBytes = options.MaxPartitionFetchBytes;

        if (options.EnableAdaptiveFetchSizing)
        {
            var adaptiveOptions = ResolveAdaptiveFetchSizingOptions(options);
            maximumFetchBytes = Math.Max(maximumFetchBytes, adaptiveOptions.MaxFetchMaxBytes);
            maximumPartitionFetchBytes = Math.Max(
                maximumPartitionFetchBytes,
                adaptiveOptions.MaxPartitionFetchBytes);
        }

        // KIP-74 makes FetchRequest.MaxBytes a soft cap: the first record batch from the
        // first non-empty partition is returned even when it crosses the total limit.
        // Reserve one maximum partition batch beyond the total response maximum.
        return (int)Math.Min(
            (long)maximumFetchBytes + maximumPartitionFetchBytes,
            int.MaxValue);
    }

    private static AdaptiveFetchSizingOptions ResolveAdaptiveFetchSizingOptions(ConsumerOptions options) =>
        options.AdaptiveFetchSizingOptions ?? new AdaptiveFetchSizingOptions
        {
            InitialPartitionFetchBytes = options.MaxPartitionFetchBytes,
            InitialFetchMaxBytes = options.FetchMaxBytes
        };

    internal static void ValidateFetchBufferMemory(ConsumerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.FetchBufferMemoryBytes, 1);

        if (options.FetchBufferMemoryBytes < options.FetchMaxBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.FetchBufferMemoryBytes,
                $"Fetch buffer memory must be at least FetchMaxBytes ({options.FetchMaxBytes})");
        }
    }

    internal static void ValidateDefaultApiTimeout(ConsumerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.DefaultApiTimeoutMs, 1);
    }

    internal static void ValidatePartitionStopTimeout(ConsumerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.PartitionStopTimeout < TimeSpan.FromMilliseconds(1)
            || options.PartitionStopTimeout.TotalMilliseconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.PartitionStopTimeout,
                "Partition stop timeout must be between one millisecond and Int32.MaxValue milliseconds");
        }
    }

    private KafkaConsumer(
        ConsumerOptions options,
        IDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer,
        (
            IConnectionPool Pool,
            MetadataManager Metadata,
            ClientTelemetryMetricCollector TelemetryMetricCollector,
            FetchBufferMemoryPool FetchBufferMemoryPool) infrastructure,
        ILoggerFactory? loggerFactory,
        bool ownsInfrastructure,
        IDekafMemoryBudget memoryBudget,
        IAsyncDeserializer<TKey>? asyncKeyDeserializer = null,
        IAsyncDeserializer<TValue>? asyncValueDeserializer = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.ConnectionsPerBroker, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.ConnectionsPerBroker, options.MaxConnectionsPerBroker);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxPollRecords, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxPollIntervalMs, 1);
        ValidateFetchBufferMemory(options);
        ValidateDefaultApiTimeout(options);
        ValidatePartitionStopTimeout(options);
        AutoOffsetResetStrategy.ValidateOptions(options);

        _options = options;
        _fetchBufferMemoryPool = infrastructure.FetchBufferMemoryPool;
        _fetchBufferMetricSource = new Diagnostics.ConsumerFetchBufferStateSource(
            _fetchBufferMemoryPool,
            options.ClientId,
            options.GroupId);
        ExponentialRetryBackoff.Validate(options.RetryBackoffMs, options.RetryBackoffMaxMs);
        _prefetchFailureTracker = new PrefetchFailureTracker(
            MaxRepeatedDeterministicPrefetchFailures,
            options.RetryBackoffMs,
            options.RetryBackoffMaxMs);
        _currentQueuedMaxBytes = (long)((ulong)options.QueuedMaxMessagesKbytes * 1024UL);
        // When an async deserializer is configured for a component, its sync slot is forced to
        // the throwing placeholder here — by construction, not by builder convention — so a
        // direct constructor caller can never pair an async deserializer with a live sync one.
        _keyDeserializer = asyncKeyDeserializer is null
            ? RecordHeaderDeserializer.WrapIfNeeded(keyDeserializer)
            : AsyncOnlyDeserializerPlaceholder<TKey>.Instance;
        _valueDeserializer = asyncValueDeserializer is null
            ? RecordHeaderDeserializer.WrapIfNeeded(valueDeserializer)
            : AsyncOnlyDeserializerPlaceholder<TValue>.Instance;
        _recordHeaderRoutingPlan = RecordHeaderRoutingPlan.Create(
            _keyDeserializer,
            _valueDeserializer);
        _recordHeaderDeserializationHeaders = _recordHeaderRoutingPlan?.NeedsMaterializedHeaders is true
            ? new Headers(2)
            : null;
        _asyncKeyDeserializer = asyncKeyDeserializer;
        _asyncValueDeserializer = asyncValueDeserializer;
        _asyncKeyUsesRecordHeaders = asyncKeyDeserializer is
            IRecordHeaderDeserializer { ConsumesRecordHeaders: true };
        _asyncValueUsesRecordHeaders = asyncValueDeserializer is
            IRecordHeaderDeserializer { ConsumesRecordHeaders: true };
        _hasRecordHeaderDeserializers = _recordHeaderRoutingPlan is not null
                                        || _asyncKeyUsesRecordHeaders
                                        || _asyncValueUsesRecordHeaders;
        _hasAsyncDeserializers = asyncKeyDeserializer is not null || asyncValueDeserializer is not null;
        _asyncDeserializationHeaders = _hasAsyncDeserializers && _hasRecordHeaderDeserializers
            ? new Headers(2)
            : null;
        _keyDeserializerPreparer = _keyDeserializer is IAsyncDeserializerPreparer<TKey> keyDeserializerPreparer &&
            _keyDeserializer is not IAsyncDeserializerPreparationRequirement { RequiresPreparation: false }
                ? keyDeserializerPreparer
                : null;
        _valueDeserializerPreparer = _valueDeserializer is IAsyncDeserializerPreparer<TValue> valueDeserializerPreparer &&
            _valueDeserializer is not IAsyncDeserializerPreparationRequirement { RequiresPreparation: false }
                ? valueDeserializerPreparer
                : null;
        _hasDeserializerPreparers = _keyDeserializerPreparer is not null || _valueDeserializerPreparer is not null;

        // Derive consumer pool sizes from configuration
        // Use 64 as default partition count estimate — covers most workloads.
        // PendingFetchData uses a ratchet, so this only increases over time.
        var consumerSizes = PoolSizing.ForConsumer(maxPartitionCount: 64);
        PendingFetchData.RatchetPoolSize(
            consumerSizes.FetchDataPool,
            consumerSizes.ParsedRecordSlabsPerBucket);
        RatchetRecordWrapperPools(partitionCount: 64);
        _ctsPool = new CancellationTokenSourcePool(consumerSizes.CancellationTokenSources);
        _storeOffsetOnDelivery = options.EnableAutoOffsetStore
                                 && options.OffsetStoreTiming == OffsetStoreTiming.OnDelivery
            ? StoreOffsetCore
            : null;
        _rewindBatchAfterDeliveryFailure = RewindAfterDeliveryFailure;
        _logger = loggerFactory?.CreateLogger<KafkaConsumer<TKey, TValue>>() ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<KafkaConsumer<TKey, TValue>>.Instance;

        GcConfigurationCheck.WarnIfWorkstationGc(_logger);

        // Initialize interceptors from options
        if (options.Interceptors is { Count: > 0 })
        {
            var interceptors = new IConsumerInterceptor<TKey, TValue>[options.Interceptors.Count];
            for (var i = 0; i < options.Interceptors.Count; i++)
            {
                interceptors[i] = (IConsumerInterceptor<TKey, TValue>)options.Interceptors[i];
            }
            _interceptors = interceptors;
            _onBatchConsume = ApplyOnConsumeInterceptorsSlow;
        }

        _connectionPool = infrastructure.Pool;
        _metadataManager = infrastructure.Metadata;
        _ownsInfrastructure = ownsInfrastructure;
        _memoryBudget = memoryBudget;
        _telemetryMetricCollector = infrastructure.TelemetryMetricCollector;
        if (!ownsInfrastructure && _connectionPool is ConnectionPool sharedPool)
            _telemetryMetricCollector.ConnectionCreationTotalProvider = sharedPool.GetConnectionCreationTotal;
        _telemetryMetricCollector.RegisterMetricsForSubscription(options.ApplicationMetrics);
        _telemetryMetricCollector.ResourceAttributesProvider = CaptureTelemetryResourceAttributes;
        if (_telemetryMetricCollector.StandardMetrics is { } standardMetrics)
            standardMetrics.AssignedPartitionCountProvider = () => _assignmentSnapshot.Count;
        _loggerFactory = loggerFactory;
        _telemetryManager = new ClientTelemetryManager(
            _connectionPool,
            _metadataManager,
            loggerFactory?.CreateLogger<ClientTelemetryManager>(),
            _telemetryMetricCollector);

        _compressionCodecs = CompressionCodecRegistry.Default;
        _appliedConnectionCount = options.ConnectionsPerBroker;

        // Initialize adaptive connection scaler if configured (before coordinator, which needs the connection count)
        if (options.EnableAdaptiveConnections && options.MaxConnectionsPerBroker > options.ConnectionsPerBroker)
        {
            // Shared consumers may narrow local fetch routing, but cannot retire sockets
            // that sibling clients still use from the shared connection pool.
            _connectionScaler = new ConsumerConnectionScaler(
                initialConnectionCount: options.ConnectionsPerBroker,
                maxConnectionCount: options.MaxConnectionsPerBroker,
                scaleUpAsync: ct => BeginConnectionRoutingTransitionAsync(scaleDown: false, ct),
                scaleDownAsync: ct => BeginConnectionRoutingTransitionAsync(scaleDown: true, ct),
                logError: ex => _logger.LogWarning(ex, "Adaptive connection scaling operation failed"));
        }

        // Initialize adaptive fetch sizer if configured
        if (options.EnableAdaptiveFetchSizing)
        {
            _adaptiveFetchSizer = new AdaptiveFetchSizer(ResolveAdaptiveFetchSizingOptions(options));
            RatchetRecordWrapperPools(partitionCount: 64);
        }

        if (!string.IsNullOrEmpty(options.GroupId))
        {
            _coordinator = new ConsumerCoordinator(
                options,
                _connectionPool,
                _metadataManager,
                loggerFactory?.CreateLogger<ConsumerCoordinator>(),
                getConnectionCount: _connectionScaler is not null
                    ? () => Volatile.Read(ref _appliedConnectionCount)
                    : null,
                onPartitionsRevoked: null,
                onPartitionsRevoking: QueueCoordinatorRevokedPartitionsForFetchClear,
                onPartitionsRevokedAsync: CommitRevokedOffsetsAsync,
                // One scope and seek delegate per rebalance callback, never per message.
                // The view's seek and position go through Seek and GetPosition, which consult the
                // running callback's context: staged while it is live, direct once the callback's
                // assignment was abandoned, ignored once its ownership has ended.
                createRebalanceConsumerScope: (assignment, newlyAssigned, _) =>
                    new RebalanceConsumerScope<TKey, TValue>(
                        this,
                        assignment,
                        newlyAssigned),
                onPartitionsRevokingAt: RecordCoordinatorRevocation)
            {
                TelemetryMetricCollector = _telemetryMetricCollector,
                SynchronizesAssignment = true
            };
        }

        _tryRecordPollFast = _coordinator is { } coordinator
            ? coordinator.TryRecordPollFast
            : static () => true;

        _prefetchBuffer = new MpscFetchBuffer(CalculatePrefetchBufferCapacity(options));

        // Register this instance's lag callback with the shared static gauge.
        // The callback is invoked only during metric collection (~every 5-60s), not on the hot path.
        Diagnostics.DekafMetrics.RegisterConsumerLagCallback(ObserveConsumerLag);
        Diagnostics.DekafMetrics.RegisterConsumerFetchBufferState(_fetchBufferMetricSource);
    }

    private ValueTask BeginConnectionRoutingTransitionAsync(
        bool scaleDown,
        CancellationToken cancellationToken)
    {
        var targetCount = _connectionScaler!.CurrentConnectionCount;
        var transition = ApplyConnectionRoutingTransitionAsync(
            targetCount,
            scaleDown,
            cancellationToken);
        Volatile.Write(ref _connectionRoutingTransitionTask, transition);
        return new ValueTask(transition);
    }

    private async Task ApplyConnectionRoutingTransitionAsync(
        int targetCount,
        bool scaleDown,
        CancellationToken cancellationToken)
    {
        // Routing width changes remap partitions across fetch connections. Drain every
        // request issued with the old mapping before publishing the new width, otherwise
        // old and new connections can fetch overlapping offsets for the same partition.
        var drainError = await _brokerPrefetchScheduler
            .DrainAllSafelyAsync(LogPrefetchLoopError, IsFatalPrefetchError)
            .ConfigureAwait(false);
        if (drainError is not null)
            ExceptionDispatchInfo.Capture(drainError).Throw();

        cancellationToken.ThrowIfCancellationRequested();
        if (scaleDown)
        {
            // The narrower mapping is valid against both the old and target pool sizes.
            // Publish it before physical shrink so a partial broker failure cannot leave
            // routing pointed at a connection index another broker already retired.
            Volatile.Write(ref _appliedConnectionCount, targetCount);
            if (_ownsInfrastructure)
                await ScaleDownOwnedConnectionGroupsAsync(targetCount, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            try
            {
                foreach (var broker in _metadataManager.Metadata.GetBrokers())
                {
                    await _connectionPool.ScaleConnectionGroupAsync(
                        broker.NodeId,
                        targetCount,
                        cancellationToken).ConfigureAwait(false);
                }

                Volatile.Write(ref _appliedConnectionCount, targetCount);
            }
            catch
            {
                // The new routing width was never published. Roll scaler bookkeeping
                // back so sustained saturation can retry, including failures at max.
                _connectionScaler!.RollbackFailedScaleUp(targetCount);
                throw;
            }
        }
    }

    private async ValueTask ScaleDownOwnedConnectionGroupsAsync(
        int targetCount,
        CancellationToken cancellationToken)
    {
        var brokers = _metadataManager.Metadata.GetBrokers();
        var scaleDownTasks = new Task[brokers.Count];
        for (var i = 0; i < brokers.Count; i++)
            scaleDownTasks[i] = ScaleDownOwnedConnectionGroupAsync(
                brokers[i].NodeId,
                targetCount,
                cancellationToken).AsTask();

        await Task.WhenAll(scaleDownTasks).ConfigureAwait(false);
    }

    private async ValueTask ScaleDownOwnedConnectionGroupAsync(
        int brokerId,
        int newCount,
        CancellationToken cancellationToken)
    {
        var removedConnection = await _connectionPool.ShrinkConnectionGroupAsync(
            brokerId,
            newCount,
            cancellationToken).ConfigureAwait(false);
        if (removedConnection is null)
            return;

        // Pool detachment completes the routing transition. Drain leases and operations in
        // the background so a slow retired socket cannot pause all consumer prefetch.
        StartRetiredConnectionDisposal(removedConnection);
    }

    private void StartRetiredConnectionDisposal(IKafkaConnection connection)
    {
        var disposalTask = RetiredConnectionDisposer.DrainAndDisposeAsync(
            connection,
            CancellationToken.None).AsTask();
        _retiredConnectionDisposalTasks.TryAdd(disposalTask, 0);
        _ = disposalTask.ContinueWith(
            static (task, state) =>
                ((KafkaConsumer<TKey, TValue>)state!).ObserveRetiredConnectionDisposal(task),
            this,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void ObserveRetiredConnectionDisposal(Task task)
    {
        _retiredConnectionDisposalTasks.TryRemove(task, out _);
        _ = task.Exception;
    }

    public StringSet Subscription => _subscriptionSnapshot;
    public string? SubscriptionPattern => _topicPattern;
    public TopicPartitionSet Assignment => _assignmentSnapshot;

    internal ConsumerDiagnosticSnapshot CaptureDiagnosticSnapshot()
    {
        var assignment = _assignmentSnapshot
            .OrderBy(partition => partition.Topic, StringComparer.Ordinal)
            .ThenBy(partition => partition.Partition)
            .Select(partition => new ConsumerTopicPartitionDiagnostic(partition.Topic, partition.Partition))
            .ToArray();
        var fetchPositions = _fetchPositions
            .OrderBy(entry => entry.Key.Topic, StringComparer.Ordinal)
            .ThenBy(entry => entry.Key.Partition)
            .Select(entry => new ConsumerPartitionOffsetDiagnostic(
                entry.Key.Topic,
                entry.Key.Partition,
                entry.Value))
            .ToArray();
        var pendingRevocations = _coordinatorRevokedPartitionsPendingFetchClear.Keys
            .OrderBy(partition => partition.Topic, StringComparer.Ordinal)
            .ThenBy(partition => partition.Partition)
            .Select(partition => new ConsumerTopicPartitionDiagnostic(partition.Topic, partition.Partition))
            .ToArray();
        var minimumEpochs = _minimumFetchBufferEpochsByPartition
            .OrderBy(entry => entry.Key.Topic, StringComparer.Ordinal)
            .ThenBy(entry => entry.Key.Partition)
            .Select(entry => new ConsumerPartitionEpochDiagnostic(
                entry.Key.Topic,
                entry.Key.Partition,
                entry.Value))
            .ToArray();
        var divergingEpochResets = _pendingDivergingEpochResets
            .OrderBy(entry => entry.Key.Topic, StringComparer.Ordinal)
            .ThenBy(entry => entry.Key.Partition)
            .Select(entry => new ConsumerDivergingEpochResetDiagnostic(
                entry.Key.Topic,
                entry.Key.Partition,
                entry.Value.EndOffset,
                entry.Value.Epoch))
            .ToArray();
        var pendingFetchDepth = Volatile.Read(ref _pendingFetchDepth);
        var prefetchBufferDepth = _prefetchBuffer.Count;

        return new ConsumerDiagnosticSnapshot
        {
            CapturedAtUtc = DateTimeOffset.UtcNow,
            FetchPositions = fetchPositions,
            Assignment = assignment,
            PrefetchedBytes = Interlocked.Read(ref _prefetchedBytes),
            PendingFetchDepth = pendingFetchDepth,
            PrefetchBufferDepth = prefetchBufferDepth,
            PrefetchDepth = pendingFetchDepth + prefetchBufferDepth,
            PendingRevocations = pendingRevocations,
            PendingRevocationMarkerPresent =
                Volatile.Read(ref _coordinatorRevokedPartitionsPendingFetchClearMarkerPresent) != 0,
            PendingRevocationClearPending =
                Volatile.Read(ref _coordinatorRevokedPartitionsPendingFetchClearPending) != 0,
            PendingDivergingEpochResets = divergingEpochResets,
            FetchBufferEpoch = Volatile.Read(ref _fetchBufferEpoch),
            MinimumFetchBufferEpoch = Volatile.Read(ref _minimumFetchBufferEpoch),
            MinimumFetchBufferEpochsByPartition = minimumEpochs,
            AdaptivePartitionFetchBytes = _adaptiveFetchSizer?.CurrentPartitionFetchBytes,
            AdaptiveFetchMaxBytes = _adaptiveFetchSizer?.CurrentFetchMaxBytes,
            ConnectionReapEvents = _connectionPool is IConnectionPoolDiagnostics diagnostics
                ? [.. diagnostics.GetConnectionReapDiagnosticsSnapshot()]
                : []
        };
    }

    public string? MemberId => _coordinator?.MemberId;

    /// <inheritdoc />
    public string? ClusterId => _metadataManager.ClusterId;

    private ClientTelemetryResourceAttributes CaptureTelemetryResourceAttributes() =>
        new(
            ClientRack: _options.ClientRack,
            GroupId: _options.GroupId,
            GroupInstanceId: string.IsNullOrEmpty(_options.GroupId) ? null : _options.GroupInstanceId,
            GroupMemberId: _coordinator?.CaptureTelemetryMemberId());

    /// <inheritdoc />
    public Guid? ClientInstanceId => _telemetryManager.ClientInstanceId;

    /// <inheritdoc />
    public KafkaClientStatus GetStatus()
    {
        var stopped = Volatile.Read(ref _closed) != 0 || Volatile.Read(ref _consumerDisposed) != 0;
        var hasConsumerGroup = _topicFilter is not null
            || _topicPattern is not null
            || _subscriptionSnapshot.Count != 0;
        var consumerGroup = hasConsumerGroup
            ? _coordinator?.CaptureGroupStatus()
            : null;
        if (consumerGroup is null)
        {
            var assignment = _assignmentSnapshot;
            consumerGroup = new ConsumerGroupStatus
            {
                HasConsumerGroup = false,
                State = CoordinatorState.Unjoined,
                CoordinatorId = -1,
                GenerationOrMemberEpoch = -1,
                HeartbeatInterval = TimeSpan.Zero,
                Assignment = KafkaClientStatusFactory.CopyAssignment(assignment, assignment.Count)
            };
        }

        return KafkaClientStatusFactory.Capture(
            KafkaClientRole.Consumer,
            _connectionPool,
            _metadataManager,
            stopped,
            clientInstanceId: ClientInstanceId,
            consumerGroup: consumerGroup);
    }

    public TopicPartitionSet Paused => _pausedSnapshot;
    public IConsumerPositions Positions => this;
    public IConsumerPartitions Partitions => this;
    public IConsumerOffsets Offsets => this;

    ConsumerGroupLiveness IConsumerGroupLiveness.GroupLiveness => _coordinator is null
        ? new ConsumerGroupLiveness(
            HasConsumerGroup: false,
            IsJoined: false,
            IsStopped: Volatile.Read(ref _closed) != 0 || Volatile.Read(ref _consumerDisposed) != 0,
            TimeSinceLastHeartbeat: null,
            HeartbeatInterval: TimeSpan.Zero,
            LastHeartbeatFailure: null)
        : _coordinator.CaptureGroupLiveness(
            Volatile.Read(ref _closed) != 0 || Volatile.Read(ref _consumerDisposed) != 0,
            _topicFilter is not null || _topicPattern is not null || _subscriptionSnapshot.Count != 0);

    IDisposable IConsumerRebalanceEventSource.RegisterRuntimeRebalanceListener(IRebalanceListener listener)
    {
        return _coordinator?.RegisterRuntimeRebalanceListener(listener) ?? NoopDisposable.Instance;
    }

    ILoggerFactory? IConsumerLoggerFactorySource.LoggerFactory => _loggerFactory;

    OffsetCommitMode IConsumerCommitConfiguration.OffsetCommitMode => _options.OffsetCommitMode;

    bool IConsumerCommitConfiguration.EnableAutoOffsetStore => _options.EnableAutoOffsetStore;

    bool IConsumerCommitConfiguration.HasConsumerGroup => !string.IsNullOrEmpty(_options.GroupId);

    bool IConsumerOffsetStoreTimingConfiguration.StoresOffsetsOnDelivery =>
        _options.EnableAutoOffsetStore &&
        _options.OffsetStoreTiming == OffsetStoreTiming.OnDelivery;

    /// <inheritdoc />
    public void RegisterMetricForSubscription(ApplicationTelemetryMetric metric)
    {
        if (Volatile.Read(ref _consumerDisposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaConsumer<TKey, TValue>));

        _telemetryMetricCollector.RegisterMetricForSubscription(metric);
    }

    /// <inheritdoc />
    public void UnregisterMetricFromSubscription(string name)
    {
        if (Volatile.Read(ref _consumerDisposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaConsumer<TKey, TValue>));

        _telemetryMetricCollector.UnregisterMetricFromSubscription(name);
    }

    /// <summary>
    /// Forces the coordinator to rejoin the group on the next <see cref="EnsureAssignmentAsync"/> call.
    /// No-op when the consumer has no group coordinator (manual assignment mode).
    /// </summary>
    internal void RequestRejoin() => _coordinator?.RequestRejoin();

    /// <summary>
    /// Gets the consumer group metadata for use with transactional producers.
    /// Returns null if not part of a consumer group or if the group has not yet been joined.
    /// </summary>
    public ConsumerGroupMetadata? ConsumerGroupMetadata
    {
        get
        {
            // Return null if not part of a consumer group
            var groupId = _options.GroupId;
            if (_coordinator is null || string.IsNullOrEmpty(groupId))
                return null;

            // Return null if not yet joined (no member ID assigned)
            var memberId = _coordinator.MemberId;
            if (string.IsNullOrEmpty(memberId))
                return null;

            // Return null if generation ID is invalid (not yet in a stable group)
            if (_coordinator.GenerationId < 0)
                return null;

            return new ConsumerGroupMetadata
            {
                GroupId = groupId!,
                GenerationId = _coordinator.GenerationId,
                MemberId = memberId!,
                GroupInstanceId = _options.GroupInstanceId
            };
        }
    }

    public void Subscribe(params string[] topics)
    {
        _coordinator?.ResumeGroupMembership();
        _topicFilter = null;
        _topicPattern = null;
        _subscription.Clear();
        foreach (var topic in topics)
        {
            _subscription.TryAdd(topic, 0);
        }

        PublishSubscriptionAndClearAssignment(invalidatePartitionCache: false);
    }

    public void Subscribe(Func<string, bool> topicFilter)
    {
        ArgumentNullException.ThrowIfNull(topicFilter);

        _coordinator?.ResumeGroupMembership();
        _topicFilter = topicFilter;
        _topicPattern = null;
        _subscription.Clear();
        _lastFilterRefreshTicks = 0; // Force immediate refresh on next EnsureAssignment
        PublishSubscriptionAndClearAssignment(invalidatePartitionCache: true);
    }

    public void SubscribePattern(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new ArgumentException("Subscription pattern must be specified.", nameof(pattern));
        }

        if (string.IsNullOrWhiteSpace(_options.GroupId))
        {
            throw new InvalidOperationException("Server-side regex subscriptions require a consumer group ID.");
        }

        _coordinator?.ResumeGroupMembership();
        _topicFilter = null;
        _topicPattern = pattern;
        _subscription.Clear();

        PublishSubscriptionAndClearAssignment(invalidatePartitionCache: true);
    }

    public void Unsubscribe()
    {
        // Before the subscription is cleared, so a poll that read it earlier cannot rejoin. The
        // coordinator revokes the owned partitions and leaves the group in the background.
        EndGroupMembership();
        try
        {
            UnsubscribeCore();
        }
        finally
        {
            CompleteGroupMembershipEnd();
        }
    }

    private void UnsubscribeCore()
    {
        _topicFilter = null;
        _topicPattern = null;
        _subscription.Clear();
        PublishSubscriptionAndClearAssignment(invalidatePartitionCache: true);
    }

    /// <summary>
    /// Asks the coordinator to leave the group, then drops every seek an OnPartitionsAssigned
    /// callback staged: all of them belong to the membership that is ending, including ones for
    /// partitions not yet synchronized. The coordinator records the leave first, so a callback
    /// still queued or running cannot stage another one (WasRevokedSince, checked under the same
    /// lock). Control plane; runs once per Unsubscribe or manual assignment call.
    /// </summary>
    private void EndGroupMembership()
    {
        if (_coordinator is not { } coordinator)
            return;

        var groupSubscribed = _topicFilter is not null || _topicPattern is not null || _subscriptionSnapshot.Count != 0;

        // First: whatever follows, the member leaves. The leave delivers no revocation until the
        // caller releases it (ReleaseLeaveRequest, in its finally) once the capture is complete.
        coordinator.RequestLeaveGroup(holdUntilReleased: groupSubscribed);

        // Unsubscribe clears the departing partitions' positions and stored offsets right after
        // this, before the revocations still to be delivered for them run: the leave's, and any a
        // heartbeat published earlier whose callback has not run yet (the coordinator no longer
        // lists those partitions, but the consumer still holds them). What their commits would
        // have sent is captured for every partition the consumer holds, and they take it per
        // partition (see CommitAsync and CommitRevokedOffsetsAsync). Read here: the stored offsets
        // and the published consumed position, both safe from any thread. The fetches the consume
        // loop owns are read as the clear that follows dequeues them (ClearFetchBuffer). A later
        // switch covered by the same membership's leave keeps the first capture: it holds only
        // manual partitions.
        if (groupSubscribed)
        {
            var membershipVersion = coordinator.MembershipVersion;
            var existing = Volatile.Read(ref _departingOffsets);
            if (existing is null || existing.MembershipVersion != membershipVersion)
            {
                var held = new HashSet<TopicPartition>(_assignmentSnapshot);
                held.UnionWith(coordinator.Assignment);
                var capture = CaptureLeaveCommitSnapshot(held, membershipVersion);
                Volatile.Write(ref _departingOffsets, capture);
                Volatile.Write(ref _leaveFetchCapture, capture);
            }
        }

        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
            _pendingRebalanceSeeks.Clear();
    }

    /// <summary>
    /// Ends the capture <see cref="EndGroupMembership"/> started and lets the leave deliver its
    /// revocations. Called in a finally by every caller of EndGroupMembership.
    /// </summary>
    private void CompleteGroupMembershipEnd()
    {
        Volatile.Write(ref _leaveFetchCapture, null);
        _coordinator?.ReleaseLeaveRequest();
    }

    /// <summary>
    /// The offsets the leave's revocation commits for the departing partitions: <see cref="Proven"/>,
    /// the stored offsets of processed records (the automatic revoked-offset commit), and
    /// <see cref="Explicit"/>, which also vouches for the last yielded record, as a parameterless
    /// <see cref="CommitAsync(CancellationToken)"/> does.
    /// </summary>
    private sealed class LeaveCommitSnapshot(
        int membershipVersion,
        HashSet<TopicPartition> held,
        ConcurrentDictionary<TopicPartition, TopicPartitionOffset> proven,
        ConcurrentDictionary<TopicPartition, TopicPartitionOffset> vouched)
    {
        /// <summary>The membership the offsets belong to; they are never sent under another.</summary>
        public int MembershipVersion { get; } = membershipVersion;

        /// <summary>The partitions the consumer held when it ended its membership. Read-only.</summary>
        public HashSet<TopicPartition> Held { get; } = held;

        /// <summary>
        /// Serializes reading, committing and removing these offsets, so a commit never sends an
        /// offset lower than one another commit of them already sent.
        /// </summary>
        public SemaphoreSlim CommitLock { get; } = new(1, 1);

        public ConcurrentDictionary<TopicPartition, TopicPartitionOffset> Proven { get; } = proven;

        public ConcurrentDictionary<TopicPartition, TopicPartitionOffset> Explicit { get; } = vouched;
    }

    /// <remarks>
    /// Reads only: the vouched offsets are computed from the consumed positions, never staged in
    /// the shared stored-offset map, where a concurrent auto-commit could send an offset past a
    /// record the application is still processing. Touches no fetch the consume loop owns; those
    /// are added by <see cref="CaptureDepartingFetchPositions"/> as the clear dequeues them.
    /// </remarks>
    private LeaveCommitSnapshot CaptureLeaveCommitSnapshot(HashSet<TopicPartition> held, int membershipVersion)
    {
        var proven = new ConcurrentDictionary<TopicPartition, TopicPartitionOffset>();
        foreach (var offset in SnapshotStoredOffsets(held))
            proven[new TopicPartition(offset.Topic, offset.Partition)] = offset;

        var vouched = new ConcurrentDictionary<TopicPartition, TopicPartitionOffset>(proven);

        // What a parameterless CommitAsync would stage for the record being processed: the
        // auto-commit consumed-position snapshot is published for readers on any thread.
        if (_options.EnableAutoOffsetStore
            && _options.OffsetCommitMode == OffsetCommitMode.Auto
            && TryReadActiveConsumedPosition(out var partition, out var position, out var leaderEpoch, out _)
            && held.Contains(partition))
        {
            AddDepartingPosition(vouched, partition, position, leaderEpoch);
        }

        return new LeaveCommitSnapshot(membershipVersion, held, proven, vouched);
    }

    /// <summary>
    /// Adds what a fetch the clear is discarding holds for a departing partition: the processed
    /// records not staged yet (stored offsets advance at fetch boundaries; the close commit
    /// flushes the head fetch for the same reason), and everything yielded for a vouching commit.
    /// Called by <see cref="ClearFetchBuffer"/> for each fetch it dequeues, before it is disposed.
    /// </summary>
    private void CaptureDepartingFetchPositions(LeaveCommitSnapshot capture, PendingFetchData pending)
    {
        if (!_options.EnableAutoOffsetStore || !capture.Held.Contains(pending.TopicPartition))
            return;

        if (pending.ProvenOffset >= 0)
        {
            AddDepartingPosition(capture.Proven, pending.TopicPartition, pending.ProvenOffset + 1, pending.ProvenLeaderEpoch);
            AddDepartingPosition(capture.Explicit, pending.TopicPartition, pending.ProvenOffset + 1, pending.ProvenLeaderEpoch);
        }

        if (TryGetConsumedPosition(pending, out var partition, out var position, out var leaderEpoch, includeFilteredProgress: false))
            AddDepartingPosition(capture.Explicit, partition, position, leaderEpoch);
    }

    private static void AddDepartingPosition(
        ConcurrentDictionary<TopicPartition, TopicPartitionOffset> offsets,
        TopicPartition partition,
        long position,
        int leaderEpoch)
    {
        var offset = new TopicPartitionOffset(partition.Topic, partition.Partition, position, leaderEpoch);
        while (true)
        {
            if (!offsets.TryGetValue(partition, out var existing))
            {
                if (offsets.TryAdd(partition, offset))
                    return;

                continue;
            }

            if (existing.Offset >= position || offsets.TryUpdate(partition, offset, existing))
                return;
        }
    }

    /// <summary>
    /// The departing offsets of the membership that is current, if any; a capture of an earlier
    /// membership is dropped.
    /// </summary>
    private LeaveCommitSnapshot? GetCurrentDepartingOffsets()
    {
        var state = Volatile.Read(ref _departingOffsets);
        if (state is null || _coordinator is not { } coordinator)
            return null;

        if (state.MembershipVersion == coordinator.MembershipVersion)
            return state;

        Interlocked.CompareExchange(ref _departingOffsets, null, state);
        return null;
    }

    /// <summary>
    /// Copies the departing offsets of <paramref name="partitions"/> (all when null): the
    /// processed ones, or with <paramref name="vouched"/> the ones a parameterless commit vouches
    /// for. Entries leave the capture only once a commit of them has succeeded.
    /// </summary>
    private static TopicPartitionOffset[]? PeekDepartingOffsets(
        LeaveCommitSnapshot state,
        bool vouched,
        IReadOnlyList<TopicPartition>? partitions)
    {
        var source = vouched ? state.Explicit : state.Proven;
        List<TopicPartitionOffset>? offsets = null;
        if (partitions is null)
        {
            foreach (var entry in source)
                (offsets ??= []).Add(entry.Value);
        }
        else
        {
            for (var i = 0; i < partitions.Count; i++)
            {
                if (source.TryGetValue(partitions[i], out var offset))
                    (offsets ??= []).Add(offset);
            }
        }

        return offsets?.ToArray();
    }

    /// <summary>
    /// Removes committed departing offsets, each only if unchanged since it was read. A vouched
    /// offset also covers the processed one at or below it.
    /// </summary>
    private static void RemoveCommittedDepartingOffsets(
        LeaveCommitSnapshot state,
        TopicPartitionOffset[] offsets,
        bool vouched)
    {
        var source = vouched ? state.Explicit : state.Proven;
        foreach (var offset in offsets)
        {
            var partition = new TopicPartition(offset.Topic, offset.Partition);
            ((ICollection<KeyValuePair<TopicPartition, TopicPartitionOffset>>)source)
                .Remove(new KeyValuePair<TopicPartition, TopicPartitionOffset>(partition, offset));

            if (vouched
                && state.Proven.TryGetValue(partition, out var proven)
                && proven.Offset <= offset.Offset)
            {
                ((ICollection<KeyValuePair<TopicPartition, TopicPartitionOffset>>)state.Proven)
                    .Remove(new KeyValuePair<TopicPartition, TopicPartitionOffset>(partition, proven));
            }
        }
    }

    private TopicPartitionOffset[] SnapshotStoredOffsets(TopicPartitionSet partitions)
    {
        List<TopicPartitionOffset>? offsets = null;
        foreach (var entry in _dirtyStoredOffsets)
        {
            if (partitions.Contains(entry.Key))
            {
                (offsets ??= []).Add(new TopicPartitionOffset(
                    entry.Key.Topic,
                    entry.Key.Partition,
                    entry.Value,
                    GetStoredOffsetLeaderEpoch(entry.Key)));
            }
        }

        return offsets?.ToArray() ?? [];
    }

    /// <summary>
    /// Commits the departing offsets of <paramref name="partitions"/> (all when null) under the
    /// membership they belong to, and removes them once committed; a cancelled or failed attempt
    /// leaves them for the retry. Reading, committing and removing run under the capture's commit
    /// lock, so concurrent commits of them (the revoked-offset commit and an explicit
    /// CommitAsync) never send an offset lower than one already sent. Never waits for the leave:
    /// the departing member's identity is the one that must commit them.
    /// </summary>
    private async ValueTask<bool> CommitDepartingOffsetsAsync(
        bool vouched,
        IReadOnlyList<TopicPartition>? partitions,
        bool retryUntilApiTimeout,
        CancellationToken cancellationToken)
    {
        if (GetCurrentDepartingOffsets() is not { } state)
            return false;

        await state.CommitLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!ReferenceEquals(GetCurrentDepartingOffsets(), state)
                || PeekDepartingOffsets(state, vouched, partitions) is not { } offsets)
            {
                return false;
            }

            await GetCommitCoordinator()
                .CommitOffsetsAsync(offsets, retryUntilApiTimeout, state.MembershipVersion, cancellationToken)
                .ConfigureAwait(false);

            RemoveCommittedDepartingOffsets(state, offsets, vouched);
            InvokeOnCommitInterceptors(offsets);
            return true;
        }
        finally
        {
            state.CommitLock.Release();
        }
    }

    private void PublishSubscriptionAndClearAssignment(bool invalidatePartitionCache)
    {
        PublishSubscriptionSnapshot();
        var hadPaused = false;
        SemaphoreHelper.AcquireOrThrowDisposed(_assignmentLock, nameof(KafkaConsumer<TKey, TValue>));
        try
        {
            var previousAssignment = _assignmentSnapshot;
            lock (_snapshotStateGate)
            {
                _assignment.Clear();
                PublishAssignmentSnapshot();
                hadPaused = RemovePartitionState(previousAssignment);
                hadPaused |= DiscardUnsynchronizedRebalanceState(_acknowledgedCoordinatorAssignment);
            }
        }
        finally
        {
            SemaphoreHelper.ReleaseSafely(_assignmentLock);
        }

        // Clear stale fetched data (same rationale as Assign).
        // NOTE: This runs after releasing _assignmentLock, so there is a small race window
        // where the prefetch worker could write new valid items into _prefetchBuffer between
        // lock release and this call. Those items get discarded here. Correctness is maintained
        // because the worker will re-fetch them on the next iteration, but there is a small
        // one-time latency cost for the discarded prefetch.
        ClearFetchBuffer();

        if (hadPaused)
            PublishPausedSnapshot();

        if (invalidatePartitionCache)
            InvalidatePartitionCache();

        InvalidateFetchRequestCache();
    }

    public void Assign(params TopicPartition[] partitions)
    {
        ThrowIfNewPartitionResetUsesManualAssignment();
        // Manual assignment ends group membership, as Unsubscribe does.
        EndGroupMembership();
        try
        {
            AssignCore(partitions);
        }
        finally
        {
            CompleteGroupMembershipEnd();
        }
    }

    private void AssignCore(TopicPartition[] partitions)
    {
        _topicFilter = null;
        _topicPattern = null;
        _subscription.Clear();
        PublishSubscriptionSnapshot();
        var hadPaused = false;
        SemaphoreHelper.AcquireOrThrowDisposed(_assignmentLock, nameof(KafkaConsumer<TKey, TValue>));
        try
        {
            var previousAssignment = _assignmentSnapshot;
            lock (_snapshotStateGate)
            {
                _assignment.Clear();
                foreach (var partition in partitions)
                {
                    _assignment.Add(partition);
                }

                List<TopicPartition>? removedPartitions = null;
                foreach (var partition in previousAssignment)
                {
                    if (!_assignment.Contains(partition))
                        (removedPartitions ??= []).Add(partition);
                }

                PublishAssignmentSnapshot();
                if (removedPartitions is not null)
                    hadPaused = RemovePartitionState(removedPartitions);
                hadPaused |= DiscardUnsynchronizedRebalanceState(_acknowledgedCoordinatorAssignment);
            }
        }
        finally
        {
            SemaphoreHelper.ReleaseSafely(_assignmentLock);
        }

        // Clear stale fetched data from the previous assignment. Without this,
        // PendingFetchData (and prefetch channel items) from old partitions would
        // be yielded by the next ConsumeAsync call as if they belonged to the new
        // assignment, causing "partition data misrouted" failures when the caller
        // re-assigns and iterates (e.g., consume-per-partition patterns).
        ClearFetchBuffer();

        if (hadPaused)
            PublishPausedSnapshot();

        InvalidatePartitionCache();
        InvalidateFetchRequestCache();
    }

    public void Unassign()
    {
        var hadPaused = false;
        SemaphoreHelper.AcquireOrThrowDisposed(_assignmentLock, nameof(KafkaConsumer<TKey, TValue>));
        try
        {
            var previousAssignment = _assignmentSnapshot;
            lock (_snapshotStateGate)
            {
                _assignment.Clear();
                PublishAssignmentSnapshot();
                hadPaused = RemovePartitionState(previousAssignment);
                hadPaused |= DiscardUnsynchronizedRebalanceState(_acknowledgedCoordinatorAssignment);
            }
        }
        finally
        {
            SemaphoreHelper.ReleaseSafely(_assignmentLock);
        }

        // Clear stale fetched data (same rationale as Assign).
        ClearFetchBuffer();

        if (hadPaused)
            PublishPausedSnapshot();

        InvalidatePartitionCache();
        InvalidateFetchRequestCache();
    }

    public void IncrementalAssign(IEnumerable<TopicPartitionOffset> partitions)
    {
        ThrowIfNewPartitionResetUsesManualAssignment();
        // Clear subscription since we're doing manual assignment; that ends group membership.
        EndGroupMembership();
        try
        {
            IncrementalAssignCore(partitions);
        }
        finally
        {
            CompleteGroupMembershipEnd();
        }
    }

    private void IncrementalAssignCore(IEnumerable<TopicPartitionOffset> partitions)
    {
        _topicFilter = null;
        _topicPattern = null;
        _subscription.Clear();
        PublishSubscriptionSnapshot();

        var hadPaused = false;
        SemaphoreHelper.AcquireOrThrowDisposed(_assignmentLock, nameof(KafkaConsumer<TKey, TValue>));
        try
        {
            lock (_snapshotStateGate)
            {
                hadPaused = DiscardUnsynchronizedRebalanceState(_acknowledgedCoordinatorAssignment);
                foreach (var tpo in partitions)
                {
                    var tp = new TopicPartition(tpo.Topic, tpo.Partition);
                    _assignment.Add(tp);

                    // If an offset is specified (>= 0), set the position
                    if (tpo.Offset >= 0)
                    {
                        SetPosition(tp, tpo.Offset, dirty: false);
                        if (tpo.LeaderEpoch >= 0)
                            SetLastConsumedLeaderEpoch(tp, tpo.LeaderEpoch);
                        else
                            ClearLastConsumedLeaderEpoch(tp);
                        SetFetchPosition(tp, tpo.Offset);
                    }
                    // Otherwise, positions will be initialized lazily based on auto.offset.reset
                }

                PublishAssignmentSnapshot();
            }
        }
        finally
        {
            SemaphoreHelper.ReleaseSafely(_assignmentLock);
        }

        if (hadPaused)
            PublishPausedSnapshot();

        InvalidatePartitionCache();
        InvalidateFetchRequestCache();
    }

    private void ThrowIfNewPartitionResetUsesManualAssignment()
    {
        if (_options.AutoOffsetResetNewPartitions is not null)
        {
            throw new InvalidOperationException(
                $"{nameof(ConsumerOptions.AutoOffsetResetNewPartitions)} requires a consumer group subscription " +
                "and cannot be used with manual assignment.");
        }
    }

    public void IncrementalUnassign(IEnumerable<TopicPartition> partitions)
    {
        // Materialize once: the enumerable is iterated three times (assignment removal,
        // state cleanup, fetch buffer clear) and a forward-only sequence would silently
        // yield zero items on the second and third passes.
        var partitionList = partitions as IReadOnlyList<TopicPartition> ?? partitions.ToList();

        RemoveAssignedPartitions(partitionList, clearAll: false, stagePendingClear: true);
    }

    private void RemoveAssignedPartitions(
        IReadOnlyCollection<TopicPartition> partitions,
        bool clearAll,
        bool stagePendingClear = false)
    {
        if (partitions.Count == 0)
            return;

        var hadPaused = false;
        SemaphoreHelper.AcquireOrThrowDisposed(_assignmentLock, nameof(KafkaConsumer<TKey, TValue>));
        try
        {
            if (clearAll)
            {
                _assignment.Clear();
            }
            else
            {
                foreach (var partition in partitions)
                {
                    _assignment.Remove(partition);
                }
            }

            lock (_snapshotStateGate)
            {
                PublishAssignmentSnapshot();
                hadPaused = RemovePartitionState(partitions);

                // Close: nothing is synchronized any more, so callback state goes too.
                if (clearAll)
                    hadPaused |= DiscardUnsynchronizedRebalanceState(synchronizedAssignment: []);
            }
        }
        finally
        {
            SemaphoreHelper.ReleaseSafely(_assignmentLock);
        }

        // Clear any pending fetch data for the removed partitions
        ClearFetchBufferForPartitions(
            partitions,
            invalidateAllFetches: clearAll,
            stagePendingClear: stagePendingClear);

        if (hadPaused)
            PublishPausedSnapshot();
        InvalidatePartitionCache();
        InvalidateFetchRequestCache();
    }

    public async IAsyncEnumerable<ConsumeResult<TKey, TValue>> ConsumeSnapshotAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _consumerDisposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaConsumer<TKey, TValue>));

        ThrowIfNotInitialized();
        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
        {
            if (Interlocked.Exchange(ref _snapshotOperationActive, 1) != 0)
            {
                throw new InvalidOperationException(
                    "Only one snapshot enumeration can be active on a consumer.");
            }
        }

        SnapshotConsumeState? snapshot = null;
        try
        {
            await RecordPollAsync(cancellationToken).ConfigureAwait(false);
            await EnsureAssignmentForPollAsync(cancellationToken).ConfigureAwait(false);
            ProvePriorDeliveryForSnapshot();
            snapshot = await CaptureSnapshotStateAsync(cancellationToken).ConfigureAwait(false);
            if (snapshot.IsComplete)
                yield break;

            Volatile.Write(ref _activeSnapshot, snapshot);
            ResetFetchBufferForSnapshot(snapshot);
            InvalidatePartitionCache();
            await foreach (var result in ConsumeAsync(cancellationToken).ConfigureAwait(false))
            {
                // ConsumeAsync validates snapshot state and advances the delivered position
                // atomically. Its yield is the delivery linearization point; revalidating here
                // could reject a record whose position has already advanced.
                var partition = new TopicPartition(result.Topic, result.Partition);
                if (!snapshot.TryGetEndOffset(partition, out var endOffset))
                {
                    throw new InvalidOperationException(
                        "The consumer assignment changed while a snapshot enumeration was active.");
                }

                if (result.IsPartitionEof)
                {
                    if (result.Offset >= endOffset)
                    {
                        CompleteSnapshotPartition(
                            snapshot,
                            partition,
                            endOffset,
                            leaderEpoch: -1);
                    }

                    if (snapshot.IsComplete)
                        yield break;
                    continue;
                }

                if (result.Offset >= endOffset)
                {
                    CompleteSnapshotPartition(
                        snapshot,
                        partition,
                        endOffset,
                        result.LeaderEpoch ?? -1);
                    if (snapshot.IsComplete)
                        yield break;
                    continue;
                }

                yield return result;

                if (result.Offset + 1 >= endOffset)
                {
                    CompleteSnapshotPartition(
                        snapshot,
                        partition,
                        endOffset,
                        result.LeaderEpoch ?? -1);
                    if (snapshot.IsComplete)
                        yield break;
                }
            }
        }
        finally
        {
            try
            {
                if (snapshot is not null
                    && ReferenceEquals(
                        Interlocked.CompareExchange(ref _activeSnapshot, null, snapshot),
                        snapshot))
                {
                    if (!snapshot.IsComplete)
                        ResetFetchBufferAfterAbortedSnapshot(snapshot);

                    InvalidatePartitionCache();
                }
            }
            finally
            {
                Volatile.Write(ref _snapshotOperationActive, 0);
            }
        }
    }

    public async ValueTask<TopicPartitionOffset> SeekToTailAsync(
        TopicPartition partition,
        int offsetCount,
        CancellationToken cancellationToken = default)
    {
        if (offsetCount < 0)
            throw new ArgumentOutOfRangeException(nameof(offsetCount), "Offset count cannot be negative.");

        var watermarks = await QueryWatermarkOffsetsAsync(partition, cancellationToken).ConfigureAwait(false);
        var offset = Math.Max(watermarks.Low, watermarks.High - offsetCount);
        var resolved = new TopicPartitionOffset(partition.Topic, partition.Partition, offset);
        Seek(resolved);
        return resolved;
    }

    private async ValueTask<SnapshotConsumeState> CaptureSnapshotStateAsync(
        CancellationToken cancellationToken)
    {
        var assignment = _assignmentSnapshot;
        var paused = _pausedSnapshot;
        var partitions = new TopicPartition[assignment.Count];
        var index = 0;
        foreach (var partition in assignment)
        {
            if (paused.Contains(partition))
            {
                throw new InvalidOperationException(
                    $"Cannot start a snapshot while assigned partition {partition} is paused.");
            }

            partitions[index++] = partition;
        }

        var endOffsets = new Dictionary<TopicPartition, long>(partitions.Length);
        var startOffsets = new Dictionary<TopicPartition, long>(partitions.Length);
        for (var i = 0; i < partitions.Length; i++)
        {
            var partition = partitions[i];
            var watermarks = await QueryWatermarkOffsetsAsync(partition, cancellationToken)
                .ConfigureAwait(false);
            endOffsets.Add(partition, watermarks.High);
            startOffsets.Add(
                partition,
                GetPosition(partition)
                ?? _fetchPositions.GetValueOrDefault(partition, watermarks.Low));
        }

        var snapshot = new SnapshotConsumeState(endOffsets, assignment, paused, startOffsets);
        ThrowIfSnapshotStateChanged(snapshot);

        for (var i = 0; i < partitions.Length; i++)
        {
            var partition = partitions[i];
            var endOffset = endOffsets[partition];
            var startOffset = startOffsets[partition];
            if (endOffset == 0
                || startOffset >= endOffset)
            {
                snapshot.Complete(partition);
            }
        }

        return snapshot;
    }

    private void ResetFetchBufferForSnapshot(SnapshotConsumeState snapshot)
    {
        // Capture may overlap an unbounded prefetch that includes records beyond a partition's
        // fixed end. Invalidate those responses and restart from the user-visible positions so
        // excluded records never reach deserializers.
        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
        {
            ClearFetchBufferForPartitions(snapshot.Assignment, stagePendingClear: true);
            foreach (var (partition, offset) in snapshot.StartOffsets)
            {
                SetFetchPosition(partition, offset);
                _eofEmitted.TryRemove(partition, out _);
            }
        }
    }

    private void ProvePriorDeliveryForSnapshot()
    {
        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
        {
            if (_pendingFetches.Count == 0)
                return;

            var priorPending = _pendingFetches.Peek();
            priorPending.MarkYieldedProcessed();
            FlushConsumedPositions(priorPending);
        }
    }

    private void ResetFetchBufferAfterAbortedSnapshot(SnapshotConsumeState snapshot)
    {
        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
        {
            // Assignment changes own their buffer reset and position initialization.
            if (!ReferenceEquals(snapshot.Assignment, _assignmentSnapshot))
                return;

            var resumeOffsets = new Dictionary<TopicPartition, long>(snapshot.StartOffsets.Count);
            foreach (var (partition, startOffset) in snapshot.StartOffsets)
                resumeOffsets[partition] = GetPosition(partition) ?? startOffset;

            ClearFetchBufferForPartitions(snapshot.Assignment, stagePendingClear: true);
            foreach (var (partition, offset) in resumeOffsets)
            {
                SetFetchPosition(partition, offset);
                _eofEmitted.TryRemove(partition, out _);
            }
        }
    }

    private void CompleteSnapshotPartition(
        SnapshotConsumeState snapshot,
        TopicPartition partition,
        long endOffset,
        int leaderEpoch)
    {
        lock (_snapshotStateGate)
        {
            ThrowIfSnapshotStateChanged(snapshot);
            if (!snapshot.Complete(partition))
                return;

            LogSeek(partition.Topic, partition.Partition, endOffset);
            lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
            {
                SeekLocked(new TopicPartitionOffset(
                    partition.Topic,
                    partition.Partition,
                    endOffset,
                    leaderEpoch));
            }
        }
        InvalidatePartitionCache();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfSnapshotStateChanged(SnapshotConsumeState snapshot)
    {
        snapshot.ThrowIfConsumerStateChanged(_assignmentSnapshot, _pausedSnapshot);

        var coordinator = _coordinator;
        if (coordinator is not null &&
            coordinator.AssignmentVersion != Volatile.Read(ref _lastCoordinatorAssignmentVersion))
        {
            throw new SnapshotStateChangedException();
        }
    }

#pragma warning disable CS8424 // Preserve shipped metadata; the returned async-iterator core performs token merging.
    public IAsyncEnumerable<ConsumeResult<TKey, TValue>> ConsumeAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
#pragma warning restore CS8424
    {
        if (_options.RecordFilter is null)
        {
            return _hasRecordHeaderDeserializers
                ? ConsumeAsyncCore<NoRecordFilterMode, RecordHeaderMode>(cancellationToken)
                : ConsumeAsyncCore<NoRecordFilterMode, NoRecordHeaderMode>(cancellationToken);
        }

        return _hasRecordHeaderDeserializers
            ? ConsumeAsyncCore<RecordFilterMode, RecordHeaderMode>(cancellationToken)
            : ConsumeAsyncCore<RecordFilterMode, NoRecordHeaderMode>(cancellationToken);
    }

    private async IAsyncEnumerable<ConsumeResult<TKey, TValue>> ConsumeAsyncCore<
        TRecordFilterMode,
        TRecordHeaderMode>(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where TRecordFilterMode : struct
        where TRecordHeaderMode : struct
    {
        if (Volatile.Read(ref _consumerDisposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaConsumer<TKey, TValue>));

        ThrowIfNotInitialized();

        // Start auto-commit if enabled (only in Auto mode)
        if (_options.OffsetCommitMode == OffsetCommitMode.Auto && _coordinator is not null)
        {
            await StartAutoCommitAsync(cancellationToken).ConfigureAwait(false);
        }

        // Start background prefetch if enabled (QueuedMinMessages > 1)
        var prefetchEnabled = _options.QueuedMinMessages > 1;
        _prefetchEnabled = prefetchEnabled;
        if (prefetchEnabled && !cancellationToken.IsCancellationRequested)
        {
            StartPrefetch();
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            await RecordPollAsync(cancellationToken).ConfigureAwait(false);

            await EnsureAssignmentForPollAsync(cancellationToken).ConfigureAwait(false);
            RecoverAndClearFetchBufferForPendingCoordinatorRevocations();
            // An odd seed forces slow validation when a marker is published but not yet clearable.
            // A later publication changes the captured epoch and reaches the same slow path.
            var recordIterationEpochSeed = Volatile.Read(ref _batchIterationEpoch.Version);
            if (Volatile.Read(ref _coordinatorRevokedPartitionsPendingFetchClearMarkerPresent) != 0)
                recordIterationEpochSeed |= 1;
            _recordIterationEpochSeed = recordIterationEpochSeed;

            var activeSnapshot = Volatile.Read(ref _activeSnapshot);
            if (activeSnapshot is not null)
                ThrowIfSnapshotStateChanged(activeSnapshot);

            if (_assignmentSnapshot.Count == 0)
            {
                await DelayForForegroundPollAsync(100, cancellationToken).ConfigureAwait(false);
                continue;
            }

            PreparePendingFetchesForDelivery();

            // Get pending data - either from prefetch channel or direct fetch
            if (_pendingFetches.Count == 0)
            {
                if (prefetchEnabled)
                {
                    // Try to read from prefetch buffer, draining available items up to a bound
                    if (_prefetchBuffer.TryRead(out var prefetched))
                    {
                        EnqueuePendingFetch(prefetched);
                        TrackPrefetchedBytes(prefetched, release: true);
                        DrainPrefetchBuffer();
                    }
                    else
                    {
                        // Use a synchronous zero-timeout recheck before async wait so the
                        // idle path does not hold a thread-pool thread indefinitely.
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            // WaitToRead throws stored completion errors directly.
                            if (await WaitForPrefetchDataAsync(cancellationToken).ConfigureAwait(false))
                            {
                                if (_prefetchBuffer.TryRead(out var fetched))
                                {
                                    EnqueuePendingFetch(fetched);
                                    TrackPrefetchedBytes(fetched, release: true);
                                    DrainPrefetchBuffer();
                                }
                            }
                            else if (_prefetchBuffer.IsCompleted)
                            {
                                // Buffer completed without error — prefetch has stopped.
                                // Reached when the broker prefetch loop exits normally
                                // (e.g., cancellation) and calls Complete() in its finally block.
                                break;
                            }
                        }
                        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                        {
                            // Prefetch not ready - check for EOF events before continuing
                            // (EOF events are queued by prefetch loop when partition is caught up)
                        }
                    }
                }
                else
                {
                    // Direct fetch (no prefetching)
                    await FetchRecordsAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            // Yield records lazily from pending fetches
            System.Diagnostics.Activity? previousActivity = null;
            // Hoist invariant boolean checks outside try so they're accessible in finally.
            // These values are stable during a single ConsumeAsync iteration (snapshotted
            // once per call, not per message) and are each a virtual/interface dispatch or
            // volatile read that adds ~2-5ns per message at 100K+ msg/s.
            var metricsEnabled = Diagnostics.DekafMetrics.MessagesReceived.Enabled
                                 || Diagnostics.DekafMetrics.BytesReceived.Enabled;
            try
            {
                var hasTraceListeners = Diagnostics.DekafDiagnostics.Source.HasListeners();
                var hasInterceptors = _interceptors is not null;
                var rawTrackingEnabled = _rawRecordTrackingEnabled;
                var hasAsyncDeserializers = _hasAsyncDeserializers;
                var hasDeserializerPreparers = _hasDeserializerPreparers;
                var recordFilter = typeof(TRecordFilterMode) == typeof(RecordFilterMode)
                    ? _options.RecordFilter
                    : null;
                var recordsUntilPollRefresh = PollRefreshRecordInterval;

                while (_pendingFetches.Count > 0)
                {
                    // Start from the last recovered epoch. A control-plane publication during
                    // a prefetch wait then forces validation before the first record is delivered.
                    var recordIterationVersion = _recordIterationEpochSeed;
                    PreparePendingFetchesForDelivery();
                    if (_pendingFetches.Count == 0)
                        break;

                    var pending = _pendingFetches.Peek();
                    // Retain once per fetch, not per record. A deserializer can synchronously
                    // Seek/Assign and dispose the queued owner while its record bytes are in use.
                    using var pendingRetention = pending.RetainForIteration();
                    // A fresh ConsumeAsync stream is another pull and therefore proves any
                    // record retained when a previous stream was disposed at its yield point.
                    pending.MarkYieldedProcessed();
                    var pendingFetchesVersion = Volatile.Read(ref _pendingFetchesVersion);
                    var pausedDuringDelivery = false;
                    long? batchProcessingStarted = _adaptiveFetchSizer is not null
                        ? Stopwatch.GetTimestamp() : null;

                    if (pending.IsSnapshotEnd)
                    {
                        _pendingEofEvents.Enqueue((pending.TopicPartition, pending.SnapshotEndOffset));
                        DequeuePendingFetch().Dispose();
                        continue;
                    }

                    // Eagerly parse all records in this fetch's batches upfront.
                    // This converts per-record lazy parse overhead (disposed check +
                    // bounds check + EnsureParsedUpTo per indexer call) into a single
                    // batch-level cost. Parsing is cache-friendly in a tight loop.
                    EagerParsePendingOrRemove(pending);

                    // Compiler requires definite assignment; always assigned inside try before yield.
                    ConsumeResult<TKey, TValue> nextResult = default!;

                    while (true)
                    {
                        var iterationStatus = GetRecordIterationStatus(
                            pending.TopicPartition,
                            ref recordIterationVersion);
                        if (iterationStatus != RecordIterationStatus.Continue)
                        {
                            pausedDuringDelivery = iterationStatus == RecordIterationStatus.Paused;
                            HandleStoppedRecordIteration(pending.TopicPartition, pausedDuringDelivery);
                            break;
                        }

                        // Wrap MoveNext + record parsing in try-catch so a corrupted fetch
                        // does not kill the consumer permanently. The yield must be outside
                        // the try block (CS1626), so we build the result first then yield below.
                        var readingProtocolData = true;
                        var offset = -1L;
                        var runningInterceptor = false;
                        var runningFilter = false;
                        var filteredRecordRejected = false;
                        try
                        {
                            if (!pending.MoveNext())
                                break;

                            // Dispose previous message's activity (captures user processing time)
                            previousActivity?.Dispose();
                            previousActivity = null;

                            ref readonly var record = ref pending.CurrentRecord;

                            // Use cached batch properties (updated once per batch transition
                            // in MoveNext) to avoid per-message property access overhead on
                            // RecordBatch (Volatile.Read + disposed check per access).
                            offset = pending.CurrentBaseOffset + record.OffsetDelta;
                            var timestampMs = pending.CurrentBaseTimestamp + record.TimestampDelta;
                            var timestampType = pending.CurrentTimestampType;
                            var topicPartition = pending.TopicPartition;
                            // Hoist every record field before tracing or deserialization can run
                            // user code that Seek/Assigns and recycles the pooled batch storage.
                            var keyData = record.Key;
                            var valueData = record.Value;
                            var isKeyNull = record.IsKeyNull;
                            var isValueNull = record.IsValueNull;
                            var pooledHeaders = record.Headers;
                            var pooledHeaderCount = record.HeaderCount;
                            var headerRouting = record.CreateHeaderRoutingLookup(
                                _recordHeaderRoutingPlan);
                            var messageBytes = (isKeyNull ? 0 : keyData.Length) +
                                               (isValueNull ? 0 : valueData.Length);

                            if (recordFilter is not null)
                            {
                                readingProtocolData = false;
                                var leaderEpoch = pending.CurrentPartitionLeaderEpoch >= 0
                                    ? (int?)pending.CurrentPartitionLeaderEpoch
                                    : null;
                                var filterContext = new ConsumerRecordFilterContext(
                                    pending.Topic,
                                    pending.PartitionIndex,
                                    offset,
                                    timestampMs,
                                    timestampType,
                                    leaderEpoch,
                                    keyData,
                                    isKeyNull,
                                    valueData,
                                    isValueNull,
                                    pooledHeaders.AsSpan(0, pooledHeaderCount));
                                runningFilter = true;
                                var shouldDeserialize = recordFilter.ShouldDeserialize(in filterContext);
                                runningFilter = false;
                                cancellationToken.ThrowIfCancellationRequested();

                                iterationStatus = GetRecordIterationStatus(
                                    topicPartition,
                                    ref recordIterationVersion);
                                if (iterationStatus != RecordIterationStatus.Continue)
                                {
                                    pausedDuringDelivery = iterationStatus == RecordIterationStatus.Paused;
                                    HandleStoppedRecordIteration(topicPartition, pausedDuringDelivery);
                                    if (pausedDuringDelivery)
                                        pending.BufferCurrentForRedelivery();
                                    break;
                                }

                                if (!shouldDeserialize)
                                {
                                    if (activeSnapshot is not null)
                                    {
                                        iterationStatus = TrackSnapshotConsumedPosition(
                                            activeSnapshot,
                                            pending,
                                            offset,
                                            messageBytes,
                                            ref recordIterationVersion);
                                        if (iterationStatus != RecordIterationStatus.Continue)
                                        {
                                            pausedDuringDelivery = iterationStatus == RecordIterationStatus.Paused;
                                            HandleStoppedRecordIteration(topicPartition, pausedDuringDelivery);
                                            if (pausedDuringDelivery)
                                                pending.BufferCurrentForRedelivery();
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        TrackConsumedPosition(pending, offset, messageBytes);
                                    }
                                    pending.MarkYieldedProcessed();
                                    filteredRecordRejected = true;
                                    goto FilteredRecordComplete;
                                }
                            }

                            // Start consumer tracing activity — skip all tracing work when no listener
                            // (~2ns HasListeners() check vs ~200ns Activity creation + tag boxing per message)
                            // Uses hoisted hasTraceListeners to avoid per-message virtual dispatch
                            if (hasTraceListeners)
                            {
                                var headers = LazyConsumeHeaders.Create(
                                    pooledHeaders,
                                    pooledHeaderCount,
                                    pending,
                                    pending.HeaderGeneration);
                                previousActivity = StartConsumeActivity(
                                    pending, headers, offset, isValueNull, isProcessSpan: true);
                            }

                            // Create result - deserialization happens eagerly in the constructor,
                            // or is awaited here when async deserializers are configured. The record
                            // memory referenced by `record` stays valid across the await: no user
                            // code other than the deserializer itself runs on this consumer until
                            // the result is yielded (see IAsyncDeserializer memory contract).
                            // Exceptions from user deserializers must never be classified as wire corruption.
                            readingProtocolData = false;
                            if (hasAsyncDeserializers)
                            {
                                nextResult = await CreateResultWithAsyncDeserializationAsync(
                                    pending,
                                    offset,
                                    keyData,
                                    isKeyNull,
                                    valueData,
                                    isValueNull,
                                    pooledHeaders,
                                    pooledHeaderCount,
                                    headerRouting,
                                    timestampMs,
                                    timestampType,
                                    pending.CurrentPartitionLeaderEpoch >= 0 ? pending.CurrentPartitionLeaderEpoch : null,
                                    cancellationToken).ConfigureAwait(false);
                            }
                            else if (hasDeserializerPreparers)
                            {
                                var leaderEpoch = pending.CurrentPartitionLeaderEpoch >= 0
                                    ? (int?)pending.CurrentPartitionLeaderEpoch
                                    : null;
                                PreparedDeserializerKey? preparedKey = null;
                                if (!TryCreateResultWithPreparedDeserialization<DeserializeKeyMode>(
                                        pending,
                                        offset,
                                        keyData,
                                        isKeyNull,
                                        valueData,
                                        isValueNull,
                                        pooledHeaders,
                                        pooledHeaderCount,
                                        in headerRouting,
                                        timestampMs,
                                        timestampType,
                                        leaderEpoch,
                                        ref preparedKey,
                                        out nextResult))
                                {
                                    var keyPreparationAttempts = 0;
                                    var valuePreparationAttempts = 0;
                                    while (true)
                                    {
                                        var component = GetRequiredPreparationComponent(
                                            pending,
                                            offset,
                                            isKeyNull,
                                            preparedKey);
                                        ReserveDeserializerPreparationAttempt(
                                            component,
                                            ref keyPreparationAttempts,
                                            ref valuePreparationAttempts);
                                        await PrepareRecordDeserializerAsync(
                                                pending,
                                                offset,
                                                keyData,
                                                isKeyNull,
                                                valueData,
                                                isValueNull,
                                                pooledHeaders,
                                                pooledHeaderCount,
                                                timestampMs,
                                                timestampType,
                                                headerRouting,
                                                component,
                                                cancellationToken)
                                            .ConfigureAwait(false);
                                        if (TryCreateResultAfterPreparation(
                                                pending,
                                                offset,
                                                keyData,
                                                isKeyNull,
                                                valueData,
                                                isValueNull,
                                                pooledHeaders,
                                                pooledHeaderCount,
                                                in headerRouting,
                                                timestampMs,
                                                timestampType,
                                                leaderEpoch,
                                                ref preparedKey,
                                                out nextResult))
                                        {
                                            break;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (typeof(TRecordHeaderMode) == typeof(RecordHeaderMode))
                                {
                                    nextResult = ConsumeResult<TKey, TValue>.CreateWithHeaderRouting(
                                        pending.Topic,
                                        pending.PartitionIndex,
                                        offset,
                                        keyData,
                                        isKeyNull,
                                        valueData,
                                        isValueNull,
                                        pooledHeaders,
                                        pooledHeaderCount,
                                        in headerRouting,
                                        pending,
                                        timestampMs,
                                        timestampType,
                                        pending.CurrentPartitionLeaderEpoch >= 0
                                            ? pending.CurrentPartitionLeaderEpoch
                                            : null,
                                        _recordHeaderDeserializationHeaders,
                                        _keyDeserializer,
                                        _valueDeserializer);
                                }
                                else
                                {
                                    nextResult = new ConsumeResult<TKey, TValue>(
                                        topic: pending.Topic,
                                        partition: pending.PartitionIndex,
                                        offset: offset,
                                        keyData: keyData,
                                        isKeyNull: isKeyNull,
                                        valueData: valueData,
                                        isValueNull: isValueNull,
                                        pooledHeaders: pooledHeaders,
                                        pooledHeaderCount: pooledHeaderCount,
                                        headerOwner: pending,
                                        timestampMs: timestampMs,
                                        timestampType: timestampType,
                                        leaderEpoch: pending.CurrentPartitionLeaderEpoch >= 0 ? pending.CurrentPartitionLeaderEpoch : null,
                                        keyDeserializer: _keyDeserializer,
                                        valueDeserializer: _valueDeserializer);
                                }
                            }

                            iterationStatus = GetRecordIterationStatus(
                                topicPartition,
                                ref recordIterationVersion);
                            if (iterationStatus != RecordIterationStatus.Continue)
                            {
                                pausedDuringDelivery = iterationStatus == RecordIterationStatus.Paused;
                                HandleStoppedRecordIteration(topicPartition, pausedDuringDelivery);
                                if (pausedDuringDelivery)
                                    pending.BufferCurrentForRedelivery();
                                break;
                            }

                            // Apply OnConsume interceptors before yielding to user
                            // Uses hoisted hasInterceptors to skip method call when no interceptors
                            if (hasInterceptors)
                            {
                                runningInterceptor = true;
                                nextResult = ApplyOnConsumeInterceptors(nextResult);
                                runningInterceptor = false;

                                // An interceptor can run long enough for background prefetch to
                                // publish a divergence reset. Do not mark that stale record consumed.
                                iterationStatus = GetRecordIterationStatus(
                                    topicPartition,
                                    ref recordIterationVersion);
                                if (iterationStatus != RecordIterationStatus.Continue)
                                {
                                    pausedDuringDelivery = iterationStatus == RecordIterationStatus.Paused;
                                    HandleStoppedRecordIteration(topicPartition, pausedDuringDelivery);
                                    if (pausedDuringDelivery)
                                        pending.BufferCurrentForRedelivery();
                                    break;
                                }
                            }

                            if (activeSnapshot is not null)
                            {
                                iterationStatus = TrackSnapshotConsumedPosition(
                                    activeSnapshot,
                                    pending,
                                    offset,
                                    messageBytes,
                                    ref recordIterationVersion);
                                if (iterationStatus != RecordIterationStatus.Continue)
                                {
                                    pausedDuringDelivery = iterationStatus == RecordIterationStatus.Paused;
                                    HandleStoppedRecordIteration(topicPartition, pausedDuringDelivery);
                                    if (pausedDuringDelivery)
                                        pending.BufferCurrentForRedelivery();
                                    break;
                                }
                            }
                            else
                            {
                                TrackConsumedPosition(pending, offset, messageBytes);
                            }
                            // Committable state advances only at fetch-boundary flushes; see
                            // AutoCommitLoopAsync for the offset-safety contract this preserves.

                            // Store raw byte references for DLQ lazy capture (zero-copy — just memory slices)
                            if (rawTrackingEnabled)
                            {
                                _currentRawKey = NormalizeRawRecordBytes(keyData, isKeyNull);
                                _currentRawValue = NormalizeRawRecordBytes(valueData, isValueNull);
                            }
                        }
                        catch (OperationCanceledException) when (readingProtocolData)
                        {
                            throw;
                        }
                        catch (Exception ex) when (
                            readingProtocolData && ProtocolDataErrorClassifier.IsProtocolDataError(ex))
                        {
                            // Protocol-layer data errors from corrupted/truncated wire data should not
                            // kill the consumer. User-facing exceptions (deserializer errors, etc.)
                            // propagate normally to the caller.
                            previousActivity?.Dispose();
                            previousActivity = null;
                            LogRecordParsingError(ex, pending.Topic, pending.PartitionIndex);
                            break;
                        }
                        catch (SnapshotStateChangedException)
                        {
                            throw;
                        }
                        catch (Exception ex) when (!readingProtocolData)
                        {
                            ThrowAfterDeliveryFailure(
                                pending, offset, runningInterceptor || runningFilter, hasAsyncDeserializers, ex);
                            throw;
                        }

                    FilteredRecordComplete:
                        if (filteredRecordRejected)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (--recordsUntilPollRefresh == 0)
                            {
                                await RecordPollAsync(cancellationToken).ConfigureAwait(false);
                                recordsUntilPollRefresh = PollRefreshRecordInterval;
                            }

                            continue;
                        }

                        yield return nextResult;

                        // User code at the yield point may have called Seek/Assign, which
                        // clears _pendingFetches and disposes `pending` while it is still
                        // being iterated here; touching it again would read disposed
                        // pooled buffers.
                        if (Volatile.Read(ref _pendingFetchesVersion) != pendingFetchesVersion)
                            break;

                        // Reaching this line means the caller requested the next record, which
                        // proves the record yielded above was processed. Enumerator disposal
                        // (loop-body exception, break) resumes directly into finally blocks and
                        // never executes this, leaving the in-doubt record unproven.
                        pending.MarkYieldedProcessed();

                        if (--recordsUntilPollRefresh == 0)
                        {
                            await RecordPollAsync(cancellationToken).ConfigureAwait(false);
                            recordsUntilPollRefresh = PollRefreshRecordInterval;
                        }
                    }

                    // Dispose last activity from this pending fetch
                    previousActivity?.Dispose();
                    previousActivity = null;

                    // `pending` was disposed by a buffer clear (Seek/Assign at a yield
                    // point); skip position/metric flushes that would read it and
                    // re-evaluate the (rebuilt) queue.
                    if (Volatile.Read(ref _pendingFetchesVersion) != pendingFetchesVersion)
                        break;

                    // Batch-level position flush. _positions and _fetchPositions are updated
                    // once per partition-fetch (in prefetch mode it is managed by UpdateFetchPositionsFromPrefetch).
                    FlushConsumedPositions(pending);

                    if (pending.ReachedSnapshotEnd)
                    {
                        _pendingEofEvents.Enqueue((
                            pending.TopicPartition,
                            pending.FetchEndOffsetExclusive));
                    }

                    // Record consumer metrics per-fetch instead of per-message.
                    // PendingFetchData already tracks MessageCount and TotalBytesConsumed,
                    // so we batch the Counter<T>.Add calls (virtual dispatch + bucket lookup)
                    // into a single pair per partition-fetch instead of per message.
                    if (metricsEnabled && pending.MessageCount > 0)
                        EmitFetchMetrics(pending);

                    // Report batch processing time to the adaptive fetch sizer (per-batch, not per-message)
                    if (batchProcessingStarted.HasValue)
                    {
                        var processingDuration = Stopwatch.GetElapsedTime(batchProcessingStarted.Value);
                        ReportAdaptiveProcessingComplete(processingDuration);
                    }

                    if (pausedDuringDelivery)
                    {
                        // Preserve the current iterator and any buffered current record.
                        MovePendingFetchToPaused(pending);
                        continue;
                    }

                    // Dequeue and dispose the pending fetch (releases pooled network buffer memory)
                    DequeuePendingFetch().Dispose();
                }
            }
            finally
            {
                // Ensure activity is disposed if caller breaks out of enumeration early
                previousActivity?.Dispose();

                // Flush positions and metrics for any partially-iterated pending fetch.
                // Only relevant when the caller breaks early or an exception propagates;
                // on normal loop exit _pendingFetches is empty and this is a no-op.
                if (_pendingFetches.Count > 0)
                {
                    var current = _pendingFetches.Peek();
                    FlushConsumedPositions(current);

                    if (metricsEnabled && current.MessageCount > 0)
                        EmitFetchMetrics(current);
                }
            }

            // Yield any pending EOF events (thread-safe with ConcurrentQueue)
            while (TryDequeueCurrentEof(out var eofEvent))
            {
                yield return ConsumeResult<TKey, TValue>.CreatePartitionEof(
                    eofEvent.Partition.Topic,
                    eofEvent.Partition.Partition,
                    eofEvent.Offset);
                await RecordPollAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async IAsyncEnumerable<ConsumeBatch<TKey, TValue>> ConsumeBatchAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _consumerDisposed) != 0)
        {
            throw new ObjectDisposedException(nameof(KafkaConsumer<TKey, TValue>));
        }

        if (_hasAsyncDeserializers)
        {
            throw new NotSupportedException(
                "ConsumeBatchAsync does not support asynchronous deserializers because batch records " +
                "are deserialized during synchronous iteration. Use ConsumeAsync or ConsumeOneAsync, " +
                "or configure synchronous IDeserializer implementations.");
        }

        ThrowIfNotInitialized();

        // Start auto-commit if enabled (only in Auto mode)
        if (_options.OffsetCommitMode == OffsetCommitMode.Auto && _coordinator is not null)
        {
            await StartAutoCommitAsync(cancellationToken).ConfigureAwait(false);
        }

        BeginBatchStream();

        // Start background prefetch if enabled (QueuedMinMessages > 1)
        bool prefetchEnabled = _options.QueuedMinMessages > 1;
        _prefetchEnabled = prefetchEnabled;
        if (prefetchEnabled && !cancellationToken.IsCancellationRequested)
        {
            StartPrefetch();
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            await RecordPollAsync(cancellationToken).ConfigureAwait(false);

            await EnsureAssignmentForPollAsync(cancellationToken).ConfigureAwait(false);
            RecoverAndClearFetchBufferForPendingCoordinatorRevocations();

            if (_assignmentSnapshot.Count == 0)
            {
                await DelayForForegroundPollAsync(100, cancellationToken).ConfigureAwait(false);
                continue;
            }

            PreparePendingFetchesForDelivery();

            // Get pending data - either from prefetch channel or direct fetch
            if (_heldSkippedPartitions.Count > 0
                && _pendingFetches.Count > 0
                && _heldSkippedPartitions.Contains(_pendingFetches.Peek().TopicPartition))
            {
                // Only skipped fetches that could not be released remain at the head.
                await WaitForSkippedFetchRetryAsync(prefetchEnabled, cancellationToken).ConfigureAwait(false);
            }
            else if (_pendingFetches.Count == 0)
            {
                if (prefetchEnabled)
                {
                    // Try to read from prefetch buffer, draining available items up to a bound
                    if (_prefetchBuffer.TryRead(out PendingFetchData? prefetched))
                    {
                        EnqueuePendingFetch(prefetched);
                        TrackPrefetchedBytes(prefetched, release: true);
                        DrainPrefetchBuffer();
                    }
                    else
                    {
                        // Use a synchronous zero-timeout recheck before async wait so the
                        // idle path does not hold a thread-pool thread indefinitely.
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            if (await WaitForPrefetchDataAsync(cancellationToken).ConfigureAwait(false))
                            {
                                if (_prefetchBuffer.TryRead(out PendingFetchData? fetched))
                                {
                                    EnqueuePendingFetch(fetched);
                                    TrackPrefetchedBytes(fetched, release: true);
                                    DrainPrefetchBuffer();
                                }
                            }
                            else if (_prefetchBuffer.IsCompleted)
                            {
                                break;
                            }
                        }
                        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                        {
                            // Prefetch not ready - check for EOF events before continuing
                        }
                    }
                }
                else
                {
                    // Direct fetch (no prefetching)
                    await FetchRecordsAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            // Yield batches from pending fetches
            bool metricsEnabled = Diagnostics.DekafMetrics.MessagesReceived.Enabled
                                  || Diagnostics.DekafMetrics.BytesReceived.Enabled;

            try
            {
                while (_pendingFetches.Count > 0)
                {
                    PreparePendingFetchesForDelivery();
                    if (_pendingFetches.Count == 0)
                        break;

                    if (ClearFetchBufferForPendingCoordinatorRevocations())
                        continue;

                    if (TryDiscardExhaustedPendingFetch()
                        || TryDiscardReleasedPendingFetch()
                        || TrySetAsideHeldPendingFetch())
                    {
                        continue;
                    }

                    PendingFetchData pending = _pendingFetches.Peek();

                    pending.MarkYieldedProcessed();
                    int pendingFetchesVersion = Volatile.Read(ref _pendingFetchesVersion);
                    var resumedAfterYield = false;
                    ConsumeBatch<TKey, TValue>? batch = null;
                    long? batchProcessingStarted = _adaptiveFetchSizer is not null
                        ? Stopwatch.GetTimestamp() : null;

                    // User callbacks can seek or revoke this fetch during synchronous batch
                    // iteration. Retain once across the yield, including its final cleanup.
                    using var interceptorRetention = _onBatchConsume is null
                        ? (PendingFetchData.RetentionLease?)null
                        : pending.RetainForIteration();
                    try
                    {
                        // Eagerly parse all records upfront for cache-friendly access
                        pending.EagerParseAll(_recordHeaderRoutingPlan);

                        // Yield the batch to the caller for synchronous iteration
                        var batchIterationVersion = Volatile.Read(ref _batchIterationEpoch.Version);
                        batch = new ConsumeBatch<TKey, TValue>(
                            pending,
                            _keyDeserializer,
                            _valueDeserializer,
                            new BatchIterationGuard(
                                _batchIterationEpoch,
                                batchIterationVersion,
                                GetBatchIterationStatus),
                            _storeOffsetOnDelivery,
                            _options.MaxPollRecords,
                            _rewindBatchAfterDeliveryFailure,
                            _options.RecordFilter,
                            _recordHeaderRoutingPlan,
                            _tryRecordPollFast,
                            _onBatchConsume);
                        pending.CaptureYieldCursor();
                        yield return batch;
                        pending.EndCheckpointWindow(batch);
                        // Resumption = the caller requested the next batch, proving this one was
                        // processed. Enumerator disposal skips straight to the finally block.
                        resumedAfterYield = true;
                        await RecordPollAsync(cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        if (!resumedAfterYield)
                            pending.EndCheckpointWindow(batch);
                        _batchLoopExitRequested = CompleteBatchPoll(
                            pending,
                            pendingFetchesVersion,
                            metricsEnabled,
                            batchProcessingStarted,
                            disposePending: batch is null,
                            yieldedBatchProcessed: resumedAfterYield);
                    }

                    // A skipped batch that could not be released returns to the outer loop, so
                    // pause parking, revocation handling or the held-fetch wait run before any
                    // further delivery instead of spinning on the same queued fetch.
                    if (_batchLoopExitRequested)
                        break;
                }
            }
            finally
            {
                ReleaseSkippedPartitions();
                RestoreHeldSkippedFetches();
            }

            PrepareEofDelivery();
            while (TryDequeueDeliverableEof(out var eofEvent))
            {
                using var eofPending = PendingFetchData.CreatePartitionEof(
                    eofEvent.Partition.Topic,
                    eofEvent.Partition.Partition,
                    eofEvent.Offset);
                yield return new ConsumeBatch<TKey, TValue>(
                    eofPending,
                    _keyDeserializer,
                    _valueDeserializer);
                await RecordPollAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async IAsyncEnumerable<ConsumeRawBatch> ConsumeRawBatchAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _consumerDisposed) != 0)
        {
            throw new ObjectDisposedException(nameof(KafkaConsumer<TKey, TValue>));
        }

        ThrowIfNotInitialized();

        // Start auto-commit if enabled (only in Auto mode)
        if (_options.OffsetCommitMode == OffsetCommitMode.Auto && _coordinator is not null)
        {
            await StartAutoCommitAsync(cancellationToken).ConfigureAwait(false);
        }

        BeginBatchStream();

        // Start background prefetch if enabled (QueuedMinMessages > 1)
        bool prefetchEnabled = _options.QueuedMinMessages > 1;
        _prefetchEnabled = prefetchEnabled;
        if (prefetchEnabled && !cancellationToken.IsCancellationRequested)
        {
            StartPrefetch();
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            await RecordPollAsync(cancellationToken).ConfigureAwait(false);

            await EnsureAssignmentForPollAsync(cancellationToken).ConfigureAwait(false);
            RecoverAndClearFetchBufferForPendingCoordinatorRevocations();

            if (_assignmentSnapshot.Count == 0)
            {
                await DelayForForegroundPollAsync(100, cancellationToken).ConfigureAwait(false);
                continue;
            }

            PreparePendingFetchesForDelivery();

            // Get pending data - either from prefetch channel or direct fetch
            if (_heldSkippedPartitions.Count > 0
                && _pendingFetches.Count > 0
                && _heldSkippedPartitions.Contains(_pendingFetches.Peek().TopicPartition))
            {
                // Only skipped fetches that could not be released remain at the head.
                await WaitForSkippedFetchRetryAsync(prefetchEnabled, cancellationToken).ConfigureAwait(false);
            }
            else if (_pendingFetches.Count == 0)
            {
                if (prefetchEnabled)
                {
                    // Try to read from prefetch buffer, draining available items up to a bound
                    if (_prefetchBuffer.TryRead(out PendingFetchData? prefetched))
                    {
                        EnqueuePendingFetch(prefetched);
                        TrackPrefetchedBytes(prefetched, release: true);
                        DrainPrefetchBuffer();
                    }
                    else
                    {
                        // Use a synchronous zero-timeout recheck before async wait so the
                        // idle path does not hold a thread-pool thread indefinitely.
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            if (await WaitForPrefetchDataAsync(cancellationToken).ConfigureAwait(false))
                            {
                                if (_prefetchBuffer.TryRead(out PendingFetchData? fetched))
                                {
                                    EnqueuePendingFetch(fetched);
                                    TrackPrefetchedBytes(fetched, release: true);
                                    DrainPrefetchBuffer();
                                }
                            }
                            else if (_prefetchBuffer.IsCompleted)
                            {
                                break;
                            }
                        }
                        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                        {
                            // Prefetch not ready - check for EOF events before continuing
                        }
                    }
                }
                else
                {
                    // Direct fetch (no prefetching)
                    await FetchRecordsAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            // Yield raw batches from pending fetches
            bool metricsEnabled = Diagnostics.DekafMetrics.MessagesReceived.Enabled
                                  || Diagnostics.DekafMetrics.BytesReceived.Enabled;

            try
            {
                while (_pendingFetches.Count > 0)
                {
                    PreparePendingFetchesForDelivery();
                    if (_pendingFetches.Count == 0)
                        break;

                    if (ClearFetchBufferForPendingCoordinatorRevocations())
                        continue;

                    if (TryDiscardExhaustedPendingFetch()
                        || TryDiscardReleasedPendingFetch()
                        || TrySetAsideHeldPendingFetch())
                    {
                        continue;
                    }

                    PendingFetchData pending = _pendingFetches.Peek();

                    pending.MarkYieldedProcessed();
                    int pendingFetchesVersion = Volatile.Read(ref _pendingFetchesVersion);
                    var resumedAfterYield = false;
                    ConsumeRawBatch? batch = null;
                    long? batchProcessingStarted = _adaptiveFetchSizer is not null
                        ? Stopwatch.GetTimestamp() : null;

                    try
                    {
                        // Eagerly parse all records upfront for cache-friendly access
                        pending.EagerParseAll();

                        // Yield the raw batch to the caller for synchronous iteration
                        var batchIterationVersion = Volatile.Read(ref _batchIterationEpoch.Version);
                        batch = new ConsumeRawBatch(
                            pending,
                            new BatchIterationGuard(
                                _batchIterationEpoch,
                                batchIterationVersion,
                                GetBatchIterationStatus),
                            _storeOffsetOnDelivery,
                            _options.MaxPollRecords);
                        pending.CaptureYieldCursor();
                        yield return batch;
                        pending.EndCheckpointWindow(batch);
                        // Resumption = the caller requested the next batch, proving this one was
                        // processed. Enumerator disposal skips straight to the finally block.
                        resumedAfterYield = true;
                        await RecordPollAsync(cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        if (!resumedAfterYield)
                            pending.EndCheckpointWindow(batch);
                        _batchLoopExitRequested = CompleteBatchPoll(
                            pending,
                            pendingFetchesVersion,
                            metricsEnabled,
                            batchProcessingStarted,
                            disposePending: batch is null,
                            yieldedBatchProcessed: resumedAfterYield);
                    }

                    // A skipped batch that could not be released returns to the outer loop, so
                    // pause parking, revocation handling or the held-fetch wait run before any
                    // further delivery instead of spinning on the same queued fetch.
                    if (_batchLoopExitRequested)
                        break;
                }
            }
            finally
            {
                ReleaseSkippedPartitions();
                RestoreHeldSkippedFetches();
            }

            PrepareEofDelivery();
            while (TryDequeueDeliverableEof(out var eofEvent))
            {
                using var eofPending = PendingFetchData.CreatePartitionEof(
                    eofEvent.Partition.Topic,
                    eofEvent.Partition.Partition,
                    eofEvent.Offset);
                yield return new ConsumeRawBatch(eofPending);
                await RecordPollAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <returns>
    /// <see langword="true"/> when the caller skipped this batch and it can be neither released
    /// nor held (paused, or owned by a staged seek or revocation). The batch loop must then
    /// return to its outer poll loop before delivering again. A released or held skip returns
    /// false, and the loop keeps delivering other partitions.
    /// </returns>
    private bool CompleteBatchPoll(
        PendingFetchData pending,
        int pendingFetchesVersion,
        bool metricsEnabled,
        long? batchProcessingStarted,
        bool disposePending,
        bool yieldedBatchProcessed)
    {
        var exhaustionProbePending = Interlocked.Exchange(
            ref _batchIterationEpoch.BatchExhaustionProbePending,
            0) != 0;

        if (Volatile.Read(ref _pendingFetchesVersion) != pendingFetchesVersion)
            return false;

        if (_pendingFetches.Count == 0 || !ReferenceEquals(_pendingFetches.Peek(), pending))
            return false;

        if (exhaustionProbePending)
            pending.TryBufferNext();

        if (yieldedBatchProcessed)
            pending.MarkYieldedProcessed();

        FlushConsumedPositions(pending);

        if (metricsEnabled && pending.MessageCount > 0)
            EmitFetchMetrics(pending);

        if (batchProcessingStarted.HasValue)
        {
            TimeSpan processingDuration = Stopwatch.GetElapsedTime(batchProcessingStarted.Value);
            ReportAdaptiveProcessingComplete(processingDuration);
        }

        // Keep a fully-enumerated but unproven batch queued when the caller breaks the outer
        // stream. A following explicit CommitAsync can then vouch for that clean handoff.
        if (disposePending
            || (pending.IsExhausted && yieldedBatchProcessed)
            || !IsCurrentlyAssigned(pending.TopicPartition))
        {
            DisposeQueuedFetch(DequeuePendingFetch());
            return false;
        }

        // Checked once per batch. An unmoved iteration cursor means the caller never read a
        // record: it skipped the batch. Iteration the consumer itself stopped after a read
        // (pause during delivery) moved the cursor and keeps its buffered redelivery path.
        if (!yieldedBatchProcessed || !pending.IsAtYieldCursor)
            return false;

        return !TryReleaseSkippedBatch(pending);
    }

    /// <summary>
    /// Releases a batch the caller resumed past without consuming, so its records are
    /// redelivered from the partition position. Re-yielding the queued fetch instead would
    /// spin on it forever, starving the fetches queued behind it and the assignment sync
    /// that only runs between deliveries. The skip proved nothing, so neither the position
    /// nor the stored offset moves.
    /// The skipped fetch is dropped now, and the partition is recorded. Its later queued
    /// fetches are dropped as they reach the queue head, and
    /// <see cref="ReleaseSkippedPartitions"/> sweeps the remaining buffers and rewinds every
    /// recorded partition once when the batch loop exits. Skipping K of N partitions is
    /// therefore O(N + K), not one buffer sweep per skip. Runs only on a skip, never per record.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryReleaseSkippedBatch(PendingFetchData pending)
    {
        var partition = pending.TopicPartition;

        // Paused data stays parked for Resume; the next delivery boundary moves it aside.
        if (_paused.ContainsKey(partition))
            return false;

        // Snapshot reads own their bounded positions, and Seek rejects changes during them.
        if (pending.IsSnapshotEnd || Volatile.Read(ref _snapshotOperationActive) != 0)
        {
            HoldSkippedFetch();
            return true;
        }

        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
        {
            // A staged seek or revocation owns this partition's queued data.
            if (HasPendingFetchClear(partition))
                return false;

            // Prefetch runs ahead of the consumed position. Without a known position the
            // queued data cannot be refetched safely, so it stays queued for redelivery.
            if (_prefetchEnabled
                && (!_positions.TryGetValue(partition, out var position) || position < 0))
            {
                HoldSkippedFetch();
                return true;
            }
        }

        _skippedBatchPartitions.Add(partition);
        DisposeQueuedFetch(DequeuePendingFetch());
        return true;
    }

    /// <summary>
    /// Drops a queued fetch of a partition released earlier in this batch loop. One set lookup
    /// per batch, and only while a release is pending.
    /// </summary>
    private bool TryDiscardReleasedPendingFetch()
    {
        if (_skippedBatchPartitions.Count == 0
            || _pendingFetches.Count == 0
            || !_skippedBatchPartitions.Contains(_pendingFetches.Peek().TopicPartition))
        {
            return false;
        }

        DisposeQueuedFetch(DequeuePendingFetch());
        return true;
    }

    /// <summary>
    /// Completes every skipped-batch release recorded by this batch loop, in one pass. Under
    /// the invalidation lock that Seek and RewindAfterDeliveryFailure use, it invalidates the
    /// partitions' fetch epochs and drops their queued, paused, prefetched and EOF data.
    /// It then rewinds each fetch position to the consumer position. Fetches started before
    /// this point carry an older epoch and are dropped at publication. Runs when the loop exits,
    /// including on disposal or an exception; with no release pending it is one count check.
    /// </summary>
    private void ReleaseSkippedPartitions()
    {
        if (_skippedBatchPartitions.Count == 0)
            return;

        var partitions = _skippedBatchPartitions;
        try
        {
            lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
            {
                ClearFetchBufferForPartitions(partitions);

                foreach (var partition in partitions)
                {
                    // A seek or revocation staged since the skip owns the replacement position.
                    if (HasPendingFetchClear(partition) || !IsCurrentlyAssigned(partition))
                        continue;

                    // Direct fetches already resume from the consumed position.
                    if (_positions.TryGetValue(partition, out var position) && position >= 0)
                        SetFetchPosition(partition, position);
                    _eofEmitted.TryRemove(partition, out _);
                }
            }
        }
        finally
        {
            partitions.Clear();
        }
    }

    /// <summary>
    /// Starts one EOF drain and records how many queued events it may check. A skip leaves the
    /// batch loop with fetches still queued, and prefetch reports EOF at its fetch position,
    /// ahead of those records. Only then is the set of partitions with a queued fetch built,
    /// once per drain, so each EOF check is O(1). On the normal path the queue is empty and
    /// this reads two counts.
    /// </summary>
    private void PrepareEofDelivery()
    {
        if (_eofHoldPartitions.Count > 0)
            _eofHoldPartitions.Clear();

        if (_pendingEofEvents.IsEmpty)
        {
            _eofDrainRemaining = 0;
            return;
        }

        if (_pendingFetches.Count > 0)
        {
            foreach (var queued in _pendingFetches)
                _eofHoldPartitions.Add(queued.TopicPartition);
        }

        _eofDrainRemaining = _pendingEofEvents.Count;
    }

    /// <summary>
    /// Per delivered EOF: one lock-free position lookup. The superseded-bound lookup runs only
    /// once records have superseded a queued EOF; before that it is one field read.
    /// </summary>
    private bool IsSupersededEof(TopicPartition partition, long offset) =>
        (_hasEofSupersededBounds
            && _eofSupersededBelow.TryGetValue(partition, out var below)
            && offset < below)
        || (_positions.TryGetValue(partition, out var position) && offset < position);

    /// <summary>
    /// Dequeues the next partition EOF for the record-at-a-time APIs, skipping any superseded
    /// while queued. The batch APIs use <see cref="TryDequeueDeliverableEof"/>, which also
    /// holds an EOF behind its partition's queued fetches.
    /// </summary>
    private bool TryDequeueCurrentEof(out (TopicPartition Partition, long Offset) eofEvent)
    {
        while (_pendingEofEvents.TryDequeue(out eofEvent))
        {
            if (!IsSupersededEof(eofEvent.Partition, eofEvent.Offset))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Dequeues the next partition EOF the batch APIs may deliver. An EOF for a partition that
    /// still had a queued fetch when the drain started is put back until those records are
    /// delivered. Released partitions already had their EOF dropped and re-derive it after
    /// the refetch.
    /// </summary>
    private bool TryDequeueDeliverableEof(out (TopicPartition Partition, long Offset) eofEvent)
    {
        while (_eofDrainRemaining-- > 0 && _pendingEofEvents.TryDequeue(out eofEvent))
        {
            if (_eofHoldPartitions.Count == 0 || !_eofHoldPartitions.Contains(eofEvent.Partition))
            {
                // Superseded while queued: records past this EOF were published or consumed.
                if (IsSupersededEof(eofEvent.Partition, eofEvent.Offset))
                    continue;

                return true;
            }

            _pendingEofEvents.Enqueue(eofEvent);
        }

        if (_eofHoldPartitions.Count > 0)
            _eofHoldPartitions.Clear();
        eofEvent = default;
        return false;
    }

    /// <summary>
    /// Keeps a skipped fetch at the queue head that cannot be refetched. Its partition is
    /// recorded as held and the fetch is set aside. Later fetches of the partition are set
    /// aside as they reach the head, so other partitions are delivered first. When the batch
    /// loop exits, <see cref="RestoreHeldSkippedFetches"/> queues them again behind everything
    /// else, in order, and the poll loop waits before re-offering them. O(1) per skip.
    /// </summary>
    private void HoldSkippedFetch()
    {
        _heldSkippedPartitions.Add(_pendingFetches.Peek().TopicPartition);
        _heldSkippedFetches.Enqueue(_pendingFetches.Dequeue());
    }

    /// <summary>
    /// Sets aside a queued fetch of a held partition. One count check per batch on the normal
    /// path; one set lookup per batch while a hold is active.
    /// </summary>
    private bool TrySetAsideHeldPendingFetch()
    {
        if (_heldSkippedPartitions.Count == 0
            || _pendingFetches.Count == 0
            || !_heldSkippedPartitions.Contains(_pendingFetches.Peek().TopicPartition))
        {
            return false;
        }

        _heldSkippedFetches.Enqueue(_pendingFetches.Dequeue());
        return true;
    }

    /// <summary>
    /// Queues set-aside held fetches again when the batch loop exits, behind every other queued
    /// fetch. If the loop exited early, later fetches of held partitions may still be queued.
    /// One stable pass moves them behind the set-aside ones so per-partition order holds. Uses
    /// only reused queues. With no held fetch it is one count check.
    /// </summary>
    private void RestoreHeldSkippedFetches()
    {
        if (_heldSkippedFetches.Count == 0)
            return;

        if (_pendingFetches.Count > 0)
        {
            var count = _pendingFetches.Count;
            for (var i = 0; i < count; i++)
            {
                var queued = _pendingFetches.Dequeue();
                if (_heldSkippedPartitions.Contains(queued.TopicPartition))
                    _heldSkippedFetches.Enqueue(queued);
                else
                    _pendingFetches.Enqueue(queued);
            }
        }

        while (_heldSkippedFetches.TryDequeue(out var held))
            _pendingFetches.Enqueue(held);
    }

    /// <summary>
    /// Runs when only held (skipped, unreleasable) fetches remain at the head: waits a bounded
    /// poll interval instead of re-yielding immediately, pulls in newly prefetched data, then
    /// re-offers the held fetches. Bounds a skip-everything caller to a few batches per second.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private async ValueTask WaitForSkippedFetchRetryAsync(bool prefetchEnabled, CancellationToken cancellationToken)
    {
        await DelayForForegroundPollAsync(
            Math.Min(AllPartitionsPausedDelayMs, Math.Max(1, _options.FetchMaxWaitMs)),
            cancellationToken).ConfigureAwait(false);

        // New data for other partitions queues behind the held fetches and is offered first
        // once the held partition is skipped again.
        if (prefetchEnabled)
            DrainPrefetchBuffer();

        _heldSkippedPartitions.Clear();
    }

    /// <summary>
    /// Resets batch-loop state when a batch stream starts. Releases and set-aside held fetches
    /// are always completed by the batch loop's <c>finally</c>, including on break, exception or
    /// cancellation. Only the hold marks outlive a stream, until the next retry wait. A new
    /// stream offers held fetches again immediately instead of inheriting that wait.
    /// </summary>
    private void BeginBatchStream()
    {
        _heldSkippedPartitions.Clear();
        _batchLoopExitRequested = false;
        _eofDrainRemaining = 0;
    }

    private bool TryDiscardExhaustedPendingFetch()
    {
        if (_pendingFetches.Count == 0)
            return false;

        var pending = _pendingFetches.Peek();
        if (!pending.IsExhausted)
            return false;

        pending.MarkYieldedProcessed();
        FlushConsumedPositions(pending);
        DisposeQueuedFetch(DequeuePendingFetch());
        return true;
    }

    private void StartPrefetch()
    {
        if (Volatile.Read(ref _consumerDisposed) != 0 || Volatile.Read(ref _closed) != 0)
            return;

        // Prefetch is a lifetime loop. A non-null terminal task represents a stopped
        // consumer path and must not be silently restarted, unlike the auto-commit loop.
        if (HasPrefetchStarted())
            return;

        lock (_prefetchStartLock)
        {
            if (Volatile.Read(ref _consumerDisposed) != 0 || Volatile.Read(ref _closed) != 0)
                return;

            if (HasPrefetchStarted())
                return;

            _prefetchCts = new CancellationTokenSource();
            _prefetchTask = PrefetchLoopAsync(_prefetchCts.Token);
        }
    }

    private async Task PrefetchLoopAsync(CancellationToken cancellationToken)
    {
        var consecutiveErrors = 0;
        Exception? completionError = null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await EnsureAssignmentAsync(cancellationToken).ConfigureAwait(false);
                    TryRecoverMissingPendingFetchClearMarkers();
                    if (HasPendingCoordinatorRevocations())
                    {
                        // Only the user consume loop owns _pendingFetches. Let it discard
                        // revoked data before prefetch can advance a reassigned position.
                        await Task.Delay(AllPartitionsPausedDelayMs, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    if (await WaitForConnectionRoutingTransitionAsync().ConfigureAwait(false))
                        continue;

                    var drained = await _brokerPrefetchScheduler.DrainCompletedAsync().ConfigureAwait(false);
                    if (PrefetchLoopControl.ShouldResetConsecutiveErrors(drained))
                        consecutiveErrors = 0;

                    if (_assignmentSnapshot.Count == 0)
                    {
                        var drainError = await _brokerPrefetchScheduler
                            .DrainAllSafelyAsync(LogPrefetchLoopError, IsFatalPrefetchError)
                            .ConfigureAwait(false);
                        if (drainError is not null)
                            ExceptionDispatchInfo.Capture(drainError).Throw();

                        await Task.Delay(AllPartitionsPausedDelayMs, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    var maxBytes = CalculatePrefetchMaxBytes(CurrentQueuedMaxBytes);
                    var currentPrefetchedBytes = Interlocked.Read(ref _prefetchedBytes);
                    if (PrefetchLoopControl.ShouldWaitForMemory(currentPrefetchedBytes, maxBytes))
                    {
                        Interlocked.Increment(ref _adaptiveFetchMemoryPressureSignals);
                        LogPrefetchMemoryLimitPaused(currentPrefetchedBytes, maxBytes);
                        await _prefetchMemoryAvailable.WaitAsync(cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    var (started, targetCount) = await DispatchReadyBrokerPrefetchesAsync(cancellationToken)
                        .ConfigureAwait(false);
                    var decision = PrefetchLoopControl.DecideAfterDispatch(
                        started,
                        targetCount,
                        _brokerPrefetchScheduler.HasInFlight);

                    if (decision.Action == PrefetchLoopAction.WaitForAny)
                    {
                        ReportBrokerPrefetchBacklog(decision.ReportBacklog);
                        if (decision.RecordFetchWait)
                            _adaptiveFetchSizer?.RecordFetchStart();
                        await _brokerPrefetchScheduler.WaitForAnyAsync(cancellationToken).ConfigureAwait(false);
                        if (decision.RecordFetchWait)
                            _adaptiveFetchSizer?.RecordFetchEnd();
                        ReportBrokerPrefetchBacklog(decision.ReportBacklog);
                    }
                    else if (decision.Action == PrefetchLoopAction.DelayNoWork)
                    {
                        ReportBrokerPrefetchBacklog(isBacklogged: false);
                        await Task.Delay(AllPartitionsPausedDelayMs, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        ReportBrokerPrefetchBacklog(isBacklogged: false);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex) when (IsFatalPrefetchError(ex))
                {
                    LogPrefetchLoopError(ex);
                    completionError = ex;
                    break;
                }
                catch (Exception ex)
                {
                    consecutiveErrors = PrefetchLoopControl.RecordConsecutiveError(consecutiveErrors, ex);
                    LogPrefetchLoopError(ex);

                    if (PrefetchLoopControl.ShouldBreakOnConsecutiveError(
                        consecutiveErrors,
                        MaxConsecutivePrefetchErrors))
                    {
                        completionError = new KafkaException(
                            ErrorCode.UnknownServerError,
                            $"Prefetch loop failed {consecutiveErrors} consecutive times, last error: {ex.Message}",
                            ex);
                        break;
                    }

                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            if (completionError is not null)
                _prefetchCts?.Cancel();

            var drainError = await _brokerPrefetchScheduler
                .DrainAllSafelyAsync(LogPrefetchLoopError, IsFatalPrefetchError)
                .ConfigureAwait(false);
            if (completionError is null && drainError is not null && IsFatalPrefetchError(drainError))
                completionError = drainError;

            _prefetchBuffer.Complete(completionError);
        }
    }

    private async ValueTask<bool> WaitForConnectionRoutingTransitionAsync()
    {
        var transition = Volatile.Read(ref _connectionRoutingTransitionTask);
        if (transition is null)
            return false;

        try
        {
            await transition.ConfigureAwait(false);
        }
        finally
        {
            _ = Interlocked.CompareExchange(ref _connectionRoutingTransitionTask, null, transition);
        }

        return true;
    }

    private async ValueTask<(int Started, int TargetCount)> DispatchReadyBrokerPrefetchesAsync(CancellationToken cancellationToken)
    {
        var partitionsByBroker = await GroupPartitionsByBrokerAsync(cancellationToken).ConfigureAwait(false);
        partitionsByBroker = ExcludePartitionsPendingFetchClear(partitionsByBroker);
        var fetchSessionSnapshot = ShouldUseFetchSessions && !_fetchSessions.IsEmpty
            ? _fetchSessions.ToArray()
            : Array.Empty<KeyValuePair<(int BrokerId, int ConnectionIndex), FetchSessionHandler>>();
        var currentConnections = Volatile.Read(ref _appliedConnectionCount);
        var fetchConnectionCount = ConsumerConnectionScaler.GetFetchConnectionCount(currentConnections);

        // Lazily prune stale entries from scaled-down connections.
        // Enumerate entries directly to avoid the allocating .Keys snapshot.
        foreach (var entry in _prefetchPendingItemsByBroker)
        {
            var key = entry.Key;
            if (key.ConnectionIndex >= fetchConnectionCount)
                _prefetchPendingItemsByBroker.TryRemove(key, out _);
        }

        var started = 0;
        var targetCount = 0;
        HashSet<(int BrokerId, int ConnectionIndex)>? scheduledFetchSessions = fetchSessionSnapshot.Length == 0 ? null : [];

        // Stack-allocate group ranges — bounded by MaxFetchConnectionsPerBroker
        Span<(int StartIndex, int Count)> groups = stackalloc (int, int)[Math.Min(fetchConnectionCount, ConsumerConnectionScaler.MaxFetchConnectionsPerBroker)];

        foreach (var (brokerId, partitions) in partitionsByBroker)
        {
            var groupCount = ConsumerConnectionScaler.SplitPartitionsAcrossConnections(
                partitions.Count, fetchConnectionCount, groups);

            for (var g = 0; g < groupCount; g++)
            {
                var (startIndex, count) = groups[g];
                // Deterministic: group g always maps to connection g, ensuring stable
                // (brokerId, connectionIndex) keys across cycles for pendingItems lists
                var connectionIndex = g % fetchConnectionCount;
                scheduledFetchSessions?.Add((brokerId, connectionIndex));
                targetCount++;

                if (TryStartBrokerPrefetch(
                    brokerId,
                    partitions,
                    startIndex,
                    count,
                    connectionIndex,
                    cancellationToken))
                {
                    started++;
                }
            }
        }

        foreach (var (key, handler) in fetchSessionSnapshot)
        {
            if (!handler.HasActiveSession)
            {
                _fetchSessions.TryRemove(key, out _);
                continue;
            }

            var isScaledDownConnection = key.ConnectionIndex >= fetchConnectionCount;
            if (!isScaledDownConnection
                && scheduledFetchSessions is not null
                && scheduledFetchSessions.Contains(key))
            {
                continue;
            }

            // Owned consumers physically removed indices beyond the current group, so
            // asking the pool for one would modulo-wrap the close onto a live connection.
            // Shared consumers only narrow local routing and retain those physical slots.
            if (_ownsInfrastructure && key.ConnectionIndex >= currentConnections)
            {
                _fetchSessions.TryRemove(key, out _);
                continue;
            }

            // Shared consumers retain every physical connection. Owned consumers remove the
            // old coordination connection, so the former highest fetch connection also remains
            // open as the new coordination connection. Close either stale session explicitly.
            targetCount++;
            if (TryStartBrokerPrefetch(
                key.BrokerId,
                [],
                0,
                0,
                key.ConnectionIndex,
                cancellationToken))
            {
                started++;
            }
        }

        return (started, targetCount);
    }

    private Dictionary<int, List<TopicPartition>> ExcludePartitionsPendingFetchClear(
        Dictionary<int, List<TopicPartition>> partitionsByBroker)
    {
        TryRecoverMissingPendingFetchClearMarkers();
        if (Volatile.Read(ref _coordinatorRevokedPartitionsPendingFetchClearMarkerPresent) == 0)
            return partitionsByBroker;

        var filtered = new Dictionary<int, List<TopicPartition>>(partitionsByBroker.Count);
        foreach (var (brokerId, partitions) in partitionsByBroker)
        {
            List<TopicPartition>? retained = null;
            foreach (var partition in partitions)
            {
                if (!_coordinatorRevokedPartitionsPendingFetchClear.ContainsKey(partition))
                    (retained ??= []).Add(partition);
            }

            if (retained is { Count: > 0 })
                filtered[brokerId] = retained;
        }

        return filtered;
    }

    private bool TryStartBrokerPrefetch(
        int brokerId,
        List<TopicPartition> partitions,
        int partitionStartIndex,
        int partitionCount,
        int connectionIndex,
        CancellationToken cancellationToken)
    {
        var fetchBufferEpoch = Volatile.Read(ref _fetchBufferEpoch);
        return _brokerPrefetchScheduler.TryStart(
            (brokerId, connectionIndex),
            () => RunBrokerPrefetchAsync(
                brokerId,
                partitions,
                partitionStartIndex,
                partitionCount,
                connectionIndex,
                fetchBufferEpoch,
                cancellationToken));
    }

    private async Task RunBrokerPrefetchAsync(
        int brokerId,
        List<TopicPartition> partitions,
        int partitionStartIndex,
        int partitionCount,
        int connectionIndex,
        int fetchBufferEpoch,
        CancellationToken cancellationToken)
    {
        // Rent a CTS from the pool for the combined consume cancellation source
        // instead of allocating a LinkedCTS.
        var consumeCts = _ctsPool.Rent();
        _activeConsumeCancellationSources.TryAdd(consumeCts, 0);

        try
        {
            using var reg = cancellationToken.CanBeCanceled
                ? cancellationToken.Register(static s => ((CancellationTokenSource)s!).Cancel(), consumeCts)
                : default;

            if (cancellationToken.IsCancellationRequested)
                consumeCts.Cancel();

            await PrefetchFromBrokerWithErrorHandlingAsync(
                brokerId,
                partitions,
                partitionStartIndex,
                partitionCount,
                connectionIndex,
                fetchBufferEpoch,
                consumeCts.Token,
                consumeCts.Token).ConfigureAwait(false);
        }
        finally
        {
            _activeConsumeCancellationSources.TryRemove(consumeCts, out _);
            consumeCts.Dispose();
        }
    }

    private void ReportBrokerPrefetchBacklog(bool isBacklogged)
    {
        if (_connectionScaler is null)
            return;

        _connectionScaler.ReportPipelineUtilization(
            isBacklogged ? 1 : 0,
            pipelineDepth: 1);
        _connectionScaler.MaybeScale();
    }

    private async Task PrefetchFromBrokerWithErrorHandlingAsync(
        int brokerId,
        List<TopicPartition> partitions,
        int partitionStartIndex,
        int partitionCount,
        int connectionIndex,
        int fetchBufferEpoch,
        CancellationToken linkedToken,
        CancellationToken consumeCancellationToken)
    {
        var failureKey = new PrefetchFailureKey(brokerId, connectionIndex);

        try
        {
            await PrefetchFromBrokerAsync(
                brokerId,
                partitions,
                partitionStartIndex,
                partitionCount,
                connectionIndex,
                fetchBufferEpoch,
                linkedToken).ConfigureAwait(false);
            _prefetchFailureTracker.Reset(failureKey);
        }
        catch (OperationCanceledException) when (consumeCancellationToken.IsCancellationRequested)
        {
            _prefetchFailureTracker.Reset(failureKey);
            // Consume cancellation requested, exit silently
        }
        catch (Exception ex) when (IsFatalPrefetchError(ex))
        {
            _prefetchFailureTracker.Reset(failureKey);
            // Non-recoverable errors that should propagate to the broker prefetch loop's
            // consecutive error counter. See IsFatalPrefetchError for classification logic.
            LogFatalPrefetchError(ex, brokerId);
            throw;
        }
        catch (Exception ex)
        {
            var positions = CapturePrefetchPositions(
                partitions,
                partitionStartIndex,
                partitionCount);
            var deterministic = ProtocolDataErrorClassifier.IsProtocolDataError(ex);
            var decision = _prefetchFailureTracker.Observe(
                failureKey,
                positions,
                deterministic);

            ClearPreferredReadReplicasForBroker(brokerId, partitions, partitionStartIndex, partitionCount);
            var logLevel = !deterministic && TransportFailureClassifier.IsSocketLevelFailure(ex)
                ? LogLevel.Warning
                : LogLevel.Error;
            LogPrefetchFromBrokerError(ex, brokerId, logLevel);

            if (decision.IsTerminal)
            {
                var terminalError = new Errors.ConsumeException(
                    ErrorCode.CorruptMessage,
                    $"Broker {brokerId} connection {connectionIndex} failed to parse the same fetch positions " +
                    $"{decision.Count} consecutive times: {ex.Message}",
                    isRetriable: false,
                    ex);
                LogFatalPrefetchError(terminalError, brokerId);
                throw terminalError;
            }

            await Task.Delay(decision.DelayMs, linkedToken).ConfigureAwait(false);
        }
    }

    private PrefetchPosition[] CapturePrefetchPositions(
        List<TopicPartition> partitions,
        int partitionStartIndex,
        int partitionCount)
    {
        var positions = new PrefetchPosition[partitionCount];
        var endIndex = partitionStartIndex + partitionCount;
        for (var i = partitionStartIndex; i < endIndex; i++)
        {
            var partition = partitions[i];
            var offset = _fetchPositions.GetValueOrDefault(partition, long.MinValue);
            positions[i - partitionStartIndex] = new PrefetchPosition(partition, offset);
        }

        return positions;
    }

    /// <summary>
    /// Determines whether a prefetch error is fatal (should propagate to the pipeline runner)
    /// or transient (should be suppressed and retried).
    /// </summary>
    /// <remarks>
    /// Auth* exceptions are matched explicitly because they may lack an ErrorCode.
    /// Networking-layer KafkaExceptions (connection timeouts, broker unavailable) have no
    /// ErrorCode and are always transient — the ErrorCode guard excludes them.
    /// </remarks>
    /// <summary>
    /// Calculates the exact configured queued-record prefetch budget.
    /// </summary>
    internal static long CalculatePrefetchMaxBytes(ulong configuredBytes) =>
        configuredBytes > long.MaxValue ? long.MaxValue : (long)configuredBytes;

    private int CurrentFetchMaxBytes =>
        _adaptiveFetchSizer?.CurrentFetchMaxBytes ?? _options.FetchMaxBytes;

    internal static int CalculatePrefetchBufferCapacity(ConsumerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return PoolSizing.ForConsumerPrefetchBuffer(
            options.QueuedMaxMessagesKbytes,
            options.MaxPartitionFetchBytes,
            options.FetchMaxBytes,
            options.PrefetchPipelineDepth,
            options.ConnectionsPerBroker);
    }

    internal static bool IsFatalPrefetchError(Exception ex) => ex is
        Errors.AuthenticationException or
        Errors.AuthorizationException or
        KafkaException { ErrorCode: not null, IsRetriable: false };

    /// <summary>
    /// Failures of the best-effort pattern-subscription refresh that repeating it cannot fix.
    /// Everything else (no broker reachable, reset or refused connections, DNS, timeouts) is
    /// logged and retried at the next refresh.
    /// </summary>
    internal static bool IsFatalFilterRefreshError(Exception ex) =>
        IsFatalPrefetchError(ex)
        || ex is Errors.BrokerVersionException
            or Errors.BootstrapResolutionException
            or ObjectDisposedException;

    private async ValueTask PrefetchFromBrokerAsync(
        int brokerId,
        List<TopicPartition> partitions,
        int partitionStartIndex,
        int partitionCount,
        int connectionIndex,
        int fetchBufferEpoch,
        CancellationToken cancellationToken)
    {
        using var connectionLease = await _connectionPool.LeaseConnectionByIndexAsync(
            brokerId,
            connectionIndex,
            cancellationToken).ConfigureAwait(false);
        var connection = connectionLease.Connection;

        var apiVersion = _metadataManager.GetNegotiatedApiVersion(
            connection,
            ApiKey.Fetch,
            FetchRequest.LowestSupportedVersion,
            FetchRequest.HighestSupportedVersion);

        // Resolve any special offset values — pass index range to avoid GetRange allocation
        await ResolveSpecialOffsetsAsync(partitions, partitionStartIndex, partitionCount, cancellationToken).ConfigureAwait(false);

        FetchSessionHandler? fetchSessionHandler = null;
        if (ShouldUseFetchSessions && apiVersion >= 7)
        {
            fetchSessionHandler = _fetchSessions.GetOrAdd((brokerId, connectionIndex), static _ => new FetchSessionHandler());
            await fetchSessionHandler.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var fetchMaxBytes = CurrentFetchMaxBytes;
            // Build fetch request — pass index range to avoid GetRange allocation
            var topicData = BuildFetchRequestTopicsForConnectionWithSnapshot(
                partitions,
                partitionStartIndex,
                partitionCount,
                brokerId,
                connectionIndex,
                out var requestMetadataSnapshot);
            FetchSessionBuildResult? fetchSessionBuild = fetchSessionHandler?.BuildFromSnapshot(
                topicData,
                requestMetadataSnapshot);

            var request = FetchRequest.Rent();
            request.MaxWaitMs = _options.FetchMaxWaitMs;
            request.MinBytes = _options.FetchMinBytes;
            request.MaxBytes = fetchMaxBytes;
            request.CheckCrcs = _options.CheckCrcs;
            request.ResponseMemoryPool = _fetchBufferMemoryPool;
            request.IsolationLevel = _options.IsolationLevel;
            request.RackId = _options.ClientRack;
            request.Topics = fetchSessionBuild?.Topics ?? topicData;
            request.ForgottenTopicsData = fetchSessionBuild?.ForgottenTopicsData;
            request.SessionId = fetchSessionBuild?.SessionId ?? 0;
            request.SessionEpoch = fetchSessionBuild?.SessionEpoch ?? -1;

            var fetchStarted = Stopwatch.GetTimestamp();
            long watermarkUpdateSequence;

            FetchResponse response;
            try
            {
                response = await SendWithWatermarkWriteSequenceAsync<FetchRequest, FetchResponse>(
                    connection,
                    request,
                    (short)apiVersion,
                    cancellationToken).ConfigureAwait(false);
                watermarkUpdateSequence = ((IRequestWriteSequenceTarget)request).WriteSequence;
            }
            catch
            {
                fetchSessionHandler?.HandleError();
                throw;
            }
            finally
            {
                request.ReturnToPool();
                ConsumerFetchPools.ReturnFetchRequestTopics(topicData);
            }

            RecordFetchDuration(fetchStarted, brokerId);

            // Take ownership of pooled memory from the response (if zero-copy was used)
            var memoryOwner = response.PooledMemoryOwner;
            response.PooledMemoryOwner = null; // Clear to prevent double-dispose

            if (response.ErrorCode != ErrorCode.None)
            {
                fetchSessionHandler?.HandleResponse(response);
                ClearPreferredReadReplicasForBroker(brokerId, partitions, partitionStartIndex, partitionCount);
                LogFetchSessionError(brokerId, response.ErrorCode);
                response.ReturnToPool();
                memoryOwner?.Dispose();
                return;
            }

            fetchSessionHandler?.HandleResponse(response);

            // Collect pending fetch data items - we need to assign memory owner to the last one
            // since FIFO processing means the last one will be disposed last
            // Reuse list per (broker, connection) across prefetch cycles to avoid per-cycle allocation
            // Each (broker, connection) pair gets its own list since PrefetchFromBrokerAsync runs
            // concurrently for different brokers AND different connections to the same broker
            var pendingItems = _prefetchPendingItemsByBroker.GetOrAdd((brokerId, connectionIndex), static _ => []);
            pendingItems.Clear();
            var queuedDivergingEpochReset = false;
            Dictionary<string, Guid>? topicIdentityRefreshes = null;

            // Write to prefetch channel
            try
            {
                foreach (var topicResponse in response.Responses)
                {
                    var topic = ResolveTopicName(
                        topicResponse,
                        requestMetadataSnapshot,
                        fetchSessionHandler);
                    if (string.IsNullOrEmpty(topic))
                        continue;

                    var activityName = _activityNameCache.GetOrAdd(topic, static t => Diagnostics.DekafDiagnostics.ProcessSpanName(t));

                    foreach (var partitionResponse in topicResponse.Partitions)
                    {
                        var tp = new TopicPartition(topic, partitionResponse.PartitionIndex);
                        if (ShouldDropStaleFetchPartition(tp, fetchBufferEpoch))
                            continue;

                        // Update watermark cache from fetch response (even on errors, watermarks may be valid)
                        UpdateWatermarksFromFetchResponse(
                            tp,
                            partitionResponse,
                            fetchBufferEpoch,
                            GetLeaderEpoch(requestMetadataSnapshot, tp),
                            watermarkUpdateSequence);
                        UpdatePreferredReadReplica(topic, partitionResponse);

                        if (partitionResponse.RecordParseError is { } parseError)
                        {
                            pendingItems.Add(PendingFetchData.CreateError(
                                topic,
                                partitionResponse.PartitionIndex,
                                new ConsumeException(
                                    $"Failed to parse record batch for {topic}-{partitionResponse.PartitionIndex}",
                                    parseError)));
                            continue;
                        }

                        if (partitionResponse.DivergingEpoch is not null)
                        {
                            _stuckFetchPositionTracker.Reset(tp);
                            if (ResetToDivergingEpoch(
                                topic,
                                partitionResponse,
                                fetchBufferEpoch,
                                startsBatch: !queuedDivergingEpochReset))
                            {
                                queuedDivergingEpochReset = true;
                            }
                            continue;
                        }

                        if (partitionResponse.ErrorCode != ErrorCode.None)
                        {
                            _stuckFetchPositionTracker.Reset(tp);
                            if (partitionResponse.ErrorCode == ErrorCode.OffsetOutOfRange)
                            {
                                await HandleFetchOffsetOutOfRangeAsync(
                                    tp, brokerId, requestMetadataSnapshot, fetchBufferEpoch, cancellationToken).ConfigureAwait(false);
                            }
                            else if (IsLeaderEpochRefreshError(partitionResponse.ErrorCode))
                            {
                                await HandleLeaderEpochRefreshAsync(
                                    topic,
                                    partitionResponse,
                                    response.NodeEndpoints).ConfigureAwait(false);
                            }
                            else if (IsTopicIdentityRefreshError(partitionResponse.ErrorCode))
                            {
                                QueueTopicIdentityRefresh(
                                    ref topicIdentityRefreshes,
                                    topic,
                                    topicResponse.TopicId,
                                    tp);
                            }
                            else
                            {
                                LogPrefetchError(topic, partitionResponse.PartitionIndex, partitionResponse.ErrorCode);
                            }
                            continue;
                        }

                        // Update high watermark from response (thread-safe with ConcurrentDictionary)
                        _highWatermarks[tp] = partitionResponse.HighWatermark;

                        // Cache Records reference to avoid repeated Volatile.Read from the pool guard
                        var records = partitionResponse.Records;

                        if (records is { Count: > 0 })
                        {
                            _stuckFetchPositionTracker.Reset(tp);
                            // EOF is re-armed when these records are published, under the lock
                            // that serializes publication with EOF reporting.

                            var pending = PendingFetchData.Create(
                                topic,
                                partitionResponse.PartitionIndex,
                                records,
                                partitionResponse.AbortedTransactions,
                                activityName: activityName,
                                skipRecordsBelowOffset: _fetchPositions.GetValueOrDefault(tp, -1),
                                stopAtOffsetExclusive: GetSnapshotEndOffset(tp),
                                ownershipStart: _ownershipStartGenerations.GetValueOrDefault(tp));

                            // Collect for later - we'll assign memory owner to the last one
                            pendingItems.Add(pending);
                        }
                        else
                        {
                            var stuckError = HandleEmptyFetchResponse(tp, records, partitionResponse.HighWatermark, fetchBufferEpoch);
                            if (stuckError is not null)
                            {
                                DisposePendingFetches(pendingItems);
                                throw stuckError;
                            }

                            if (TryCreateSnapshotEndMarker(tp, partitionResponse) is { } marker)
                                pendingItems.Add(marker);
                        }
                    }
                }

                if (topicIdentityRefreshes is not null)
                {
                    await HandleTopicIdentityRefreshesAsync(
                        topicIdentityRefreshes,
                        fetchSessionHandler,
                        cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                DisposePendingFetches(pendingItems);
                memoryOwner?.Dispose();
                memoryOwner = null;
                throw;
            }
            finally
            {
                if (queuedDivergingEpochReset)
                    CompleteDivergingEpochResets();

                // Return the response and its nested objects to their pools.
                // Data has been transferred to PendingFetchData; the response wrappers are no longer needed.
                response.ReturnToPool();
            }

            // Write all pending items to the channel, with shared memory owner
            if (pendingItems.Count > 0)
            {
                if (memoryOwner is not null)
                {
                    AssignSharedMemoryOwner(pendingItems, memoryOwner);
                    memoryOwner = null; // Transferred
                }

                await WritePrefetchedItemsAsync(pendingItems, fetchBufferEpoch, cancellationToken).ConfigureAwait(false);
            }

            memoryOwner?.Dispose();
        }
        finally
        {
            fetchSessionHandler?.Release();
        }
    }

    /// <summary>
    /// Updates <see cref="_positions"/> and, when not in prefetch mode, <see cref="_fetchPositions"/>
    /// from the given pending fetch data. Called at batch boundaries and in finally blocks.
    /// In prefetch mode, <see cref="_fetchPositions"/> is managed by <see cref="UpdateFetchPositionsFromPrefetch"/>.
    /// </summary>
    private bool FlushConsumedPositions(PendingFetchData pending)
    {
        // A staged reset/revocation invalidates unread protocol progress from this fetch.
        // Preserve only records already delivered; filtered/control offsets from the stale
        // response must not overwrite the replacement position.
        var includeFilteredProgress = !HasPendingFetchClear(pending.TopicPartition);
        if (!TryGetConsumedPosition(
                pending,
                out var tp,
                out var nextOffset,
                out var leaderEpoch,
                includeFilteredProgress))
            return false;

        // Positions always advance past everything yielded (delivery semantics), but the
        // committable stored offset advances only past records the application has
        // demonstrably processed (ProvenOffset — see MarkYieldedProcessed). On an unwind
        // the in-doubt record between ProvenOffset and LastYieldedOffset is therefore
        // never staged, which is what makes the default at-least-once. In OnDelivery
        // mode every yielded offset was already staged at delivery, so staging the full
        // yielded range here is idempotent.
        _positions[tp] = nextOffset;
        SetLastConsumedLeaderEpoch(tp, leaderEpoch);

        var fullyTraversed = pending.IsExhausted && pending.FetchEndOffsetExclusive >= 0;
        var allYieldedRecordsProven = pending.ProvenOffset == pending.LastYieldedOffset;
        var stagedFullRange = true;
        if (_options.EnableAutoOffsetStore)
        {
            if (EagerOffsetStore || (fullyTraversed && allYieldedRecordsProven))
            {
                StoreOffsetCore(pending, nextOffset, leaderEpoch);
            }
            else
            {
                if (pending.ProvenOffset >= 0)
                    StoreOffsetCore(pending, pending.ProvenOffset + 1, pending.ProvenLeaderEpoch);
                stagedFullRange = pending.ProvenOffset + 1 >= nextOffset;
            }
        }

        if (!_prefetchEnabled)
        {
            // Without prefetch the fetch position is the consumed position, paired with the
            // consumed epoch set above; no prefetch epoch is ever recorded in this mode.
            _fetchPositions[tp] = nextOffset;
        }

        // Keep the active consumed position alive while an in-doubt record remains unstaged so a
        // later explicit CommitAsync can still vouch for it (break → CommitAsync → close).
        if (stagedFullRange)
            ClearActiveConsumedPosition(tp);

        return true;
    }

    private bool EagerOffsetStore => _options.OffsetStoreTiming == OffsetStoreTiming.OnDelivery;

    /// <summary>
    /// Full-staging position apply used by explicit-commit paths (<see cref="CommitAsync(CancellationToken)"/>):
    /// stages everything yielded, including a record the application may still be holding.
    /// An explicit commit is the caller vouching for everything delivered so far.
    /// </summary>
    private void ApplyConsumedPosition(TopicPartition partition, long ownership, long nextOffset, int leaderEpoch)
    {
        _positions[partition] = nextOffset;
        SetLastConsumedLeaderEpoch(partition, leaderEpoch);
        // Stored under the ownership the position was consumed in, not the current one: a
        // revocation and reassignment since the position was read must not make it the new
        // ownership's offset. 0 for a consumer that is not group-managed.
        if (_options.EnableAutoOffsetStore)
            StoreOffsetCore(partition, IsGroupManagedSubscription() ? ownership : 0, nextOffset, leaderEpoch);

        if (!_prefetchEnabled)
        {
            // Paired with the consumed epoch set above, as in FlushConsumedPositions.
            _fetchPositions[partition] = nextOffset;
        }
    }

    private bool FlushActiveConsumedPosition()
    {
        if (_options.OffsetCommitMode == OffsetCommitMode.Auto)
        {
            if (TryReadActiveConsumedPositionWithOwnership(
                    out var partition, out var nextOffset, out var leaderEpoch, out var ownership, out var version))
            {
                PendingFetchData? activePending = _pendingFetches.Count > 0
                    ? _pendingFetches.Peek()
                    : null;
                if (activePending is null
                    || (activePending.TopicPartition.Equals(partition)
                        && activePending.LastYieldedOffset + 1 == nextOffset
                        && activePending.LastYieldedLeaderEpoch == leaderEpoch))
                {
                    activePending?.MarkYieldedProcessed();
                    AfterActiveConsumedPositionReadForTest?.Invoke(this);
                    ApplyConsumedPosition(partition, activePending?.OwnershipStart ?? ownership, nextOffset, leaderEpoch);
                    ClearActiveConsumedPosition(partition, nextOffset, version);
                    return true;
                }

                // A batch API may have advanced the same pending fetch without publishing
                // a new record snapshot. Discard the stale snapshot and vouch for the fetch.
                ClearActiveConsumedPosition(partition, nextOffset, version);
            }
        }

        if (_pendingFetches.Count == 0)
            return false;

        // Explicit commit: the caller vouches for everything yielded so far, including a
        // record still being processed. Mark it proven so the flush stages it.
        var pending = _pendingFetches.Peek();
        pending.MarkYieldedProcessed();
        var flushed = FlushConsumedPositions(pending);

        if (pending.IsExhausted
            && _pendingFetches.Count > 0
            && ReferenceEquals(_pendingFetches.Peek(), pending))
        {
            DisposeQueuedFetch(DequeuePendingFetch());
        }

        return flushed;
    }

    /// <summary>
    /// An explicit commit vouches for delivered records even when pause reconciliation
    /// has moved their fetches out of the active queue. This control path may scan the
    /// paused queue; normal delivery never pays this cost.
    /// </summary>
    private void FlushPausedConsumedPositions()
    {
        foreach (var pending in _pausedPendingFetches)
        {
            pending.MarkYieldedProcessed();
            FlushConsumedPositions(pending);
        }
    }

    private bool TryGetActiveConsumedPosition(
        TopicPartition partition,
        out long position,
        out int leaderEpoch,
        bool includeFilteredProgress = true)
    {
        if (_options.OffsetCommitMode == OffsetCommitMode.Auto)
        {
            if (!TryReadActiveConsumedPosition(out var activePartition, out position, out leaderEpoch, out _)
                || !activePartition.Equals(partition))
            {
                position = 0;
                leaderEpoch = -1;
                return false;
            }

            return true;
        }

        if (_pendingFetches.Count == 0)
        {
            position = 0;
            leaderEpoch = -1;
            return false;
        }

        var pending = _pendingFetches.Peek();
        if (!TryGetConsumedPosition(
                pending,
                out var tp,
                out position,
                out leaderEpoch,
                includeFilteredProgress)
            || !tp.Equals(partition))
        {
            position = 0;
            leaderEpoch = -1;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Records the yielded record in the pending fetch and, in auto-commit mode, publishes
    /// the next position to the active consumed snapshot. The snapshot is read only by
    /// <see cref="CommitAsync(CancellationToken)"/> and <see cref="GetPosition"/> —
    /// never by the background auto-commit loop — so publishing before the record is
    /// yielded cannot make it committable early. Under <see cref="OffsetStoreTiming.OnDelivery"/>
    /// the offset additionally becomes committable here, by design (at-most-once).
    /// </summary>
    private void TrackConsumedPosition(PendingFetchData pending, long offset, int messageBytes)
    {
        pending.TrackConsumed(offset, messageBytes);

        if (_options.OffsetCommitMode == OffsetCommitMode.Auto)
        {
            PublishActiveConsumedPositionCore(
                pending.TopicPartition, offset + 1, pending.LastYieldedLeaderEpoch, pending.OwnershipStart);
        }

        // OnDelivery (at-most-once) staging: the offset becomes committable the moment the
        // record is handed out, before processing. Opt-in via WithAtMostOnceProcessing —
        // costs per-message dictionary writes that the default fetch-boundary flush avoids.
        if (_options.EnableAutoOffsetStore && EagerOffsetStore)
        {
            StoreOffsetCore(pending, offset + 1, pending.LastYieldedLeaderEpoch);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private RecordIterationStatus TrackSnapshotConsumedPosition(
        SnapshotConsumeState snapshot,
        PendingFetchData pending,
        long offset,
        int messageBytes,
        ref int recordIterationVersion)
    {
        while (true)
        {
            if (_batchIterationEpoch.TryBeginSnapshotDelivery(recordIterationVersion))
            {
                try
                {
                    ThrowIfSnapshotStateChanged(snapshot);
                    TrackConsumedPosition(pending, offset, messageBytes);
                    return RecordIterationStatus.Continue;
                }
                finally
                {
                    recordIterationVersion = _batchIterationEpoch.EndSnapshotDelivery(recordIterationVersion);
                }
            }

            var status = GetRecordIterationStatus(pending.TopicPartition, ref recordIterationVersion);
            if (status != RecordIterationStatus.Continue)
                return status;
        }
    }

    /// <summary>Publishes a position consumed under the partition's current ownership.</summary>
    private void PublishActiveConsumedPosition(TopicPartition partition, long position, int leaderEpoch)
        => PublishActiveConsumedPositionCore(partition, position, leaderEpoch, GetCurrentStoreOwnership(partition));

    private void PublishActiveConsumedPositionCore(TopicPartition partition, long position, int leaderEpoch, long ownership)
    {
        var observedVersion = Volatile.Read(ref _activeConsumedPositionVersion);
        if ((observedVersion & 1) == 0
            && Volatile.Read(ref _activeConsumedPartition) == partition.Partition
            && Volatile.Read(ref _activeConsumedLeaderEpoch) == leaderEpoch
            && string.Equals(
                Volatile.Read(ref _activeConsumedTopic),
                partition.Topic,
                StringComparison.Ordinal))
        {
            Volatile.Write(ref _activeConsumedPosition, position);

            if (Volatile.Read(ref _activeConsumedPositionVersion) == observedVersion)
                return;
        }

        var version = BeginActiveConsumedPositionWrite();
        Volatile.Write(ref _activeConsumedTopic, partition.Topic);
        Volatile.Write(ref _activeConsumedPartition, partition.Partition);
        Volatile.Write(ref _activeConsumedPosition, position);
        Volatile.Write(ref _activeConsumedLeaderEpoch, leaderEpoch);
        Volatile.Write(ref _activeConsumedOwnership, ownership);
        Volatile.Write(ref _activeConsumedPositionVersion, version + 2);
    }

    private int BeginActiveConsumedPositionWrite()
    {
        var spin = new SpinWait();
        while (true)
        {
            var version = Volatile.Read(ref _activeConsumedPositionVersion);
            if ((version & 1) != 0)
            {
                spin.SpinOnce();
                continue;
            }

            if (Interlocked.CompareExchange(ref _activeConsumedPositionVersion, version + 1, version) == version)
                return version;

            spin.SpinOnce();
        }
    }

    private bool TryReadActiveConsumedPosition(
        out TopicPartition partition,
        out long position,
        out int leaderEpoch,
        out int version)
        => TryReadActiveConsumedPositionWithOwnership(out partition, out position, out leaderEpoch, out _, out version);

    private bool TryReadActiveConsumedPositionWithOwnership(
        out TopicPartition partition,
        out long position,
        out int leaderEpoch,
        out long ownership,
        out int version)
    {
        ownership = 0;
        var spin = new SpinWait();
        for (var attempt = 0; attempt < 4; attempt++)
        {
            version = Volatile.Read(ref _activeConsumedPositionVersion);
            if ((version & 1) != 0)
            {
                spin.SpinOnce();
                continue;
            }

            var topic = Volatile.Read(ref _activeConsumedTopic);
            var partitionIndex = Volatile.Read(ref _activeConsumedPartition);
            position = Volatile.Read(ref _activeConsumedPosition);
            leaderEpoch = Volatile.Read(ref _activeConsumedLeaderEpoch);
            ownership = Volatile.Read(ref _activeConsumedOwnership);
            var observedVersion = Volatile.Read(ref _activeConsumedPositionVersion);
            if (version != observedVersion || (observedVersion & 1) != 0)
            {
                spin.SpinOnce();
                continue;
            }

            if (topic is null)
            {
                partition = default;
                position = 0;
                leaderEpoch = -1;
                return false;
            }

            partition = new TopicPartition(topic, partitionIndex);
            return true;
        }

        partition = default;
        position = 0;
        leaderEpoch = -1;
        version = 0;
        return false;
    }

    private void ClearActiveConsumedPosition(TopicPartition partition, long position)
    {
        if (!TryReadActiveConsumedPosition(
                out var activePartition,
                out var activePosition,
                out _,
                out var version)
            || !activePartition.Equals(partition)
            || activePosition != position)
        {
            return;
        }

        ClearActiveConsumedPosition(partition, position, version);
    }

    private void ClearActiveConsumedPosition(TopicPartition partition)
    {
        if (!TryReadActiveConsumedPosition(
                out var activePartition,
                out var activePosition,
                out _,
                out var version)
            || !activePartition.Equals(partition))
        {
            return;
        }

        ClearActiveConsumedPosition(activePartition, activePosition, version);
    }

    private void ClearActiveConsumedPosition()
    {
        if (TryReadActiveConsumedPosition(
                out var partition,
                out var position,
                out _,
                out var version))
        {
            ClearActiveConsumedPosition(partition, position, version);
        }
    }

    private void ClearActiveConsumedPosition(TopicPartition partition, long position, int version)
    {
        if (Interlocked.CompareExchange(ref _activeConsumedPositionVersion, version + 1, version) != version)
            return;

        if (string.Equals(_activeConsumedTopic, partition.Topic, StringComparison.Ordinal)
            && _activeConsumedPartition == partition.Partition
            && _activeConsumedPosition == position)
        {
            Volatile.Write(ref _activeConsumedTopic, null);
            Volatile.Write(ref _activeConsumedPosition, 0);
            Volatile.Write(ref _activeConsumedLeaderEpoch, -1);
        }

        Volatile.Write(ref _activeConsumedPositionVersion, version + 2);
    }

    private static bool TryGetConsumedPosition(
        PendingFetchData pending,
        out TopicPartition partition,
        out long nextOffset,
        out int leaderEpoch,
        bool includeFilteredProgress = true)
    {
        var fullyTraversed = includeFilteredProgress
                             && pending.IsExhausted
                             && pending.FetchEndOffsetExclusive >= 0;
        if (pending.LastYieldedOffset < 0 && !fullyTraversed)
        {
            partition = default;
            nextOffset = 0;
            leaderEpoch = -1;
            return false;
        }

        partition = pending.TopicPartition;
        if (fullyTraversed && pending.FetchEndOffsetExclusive > pending.LastYieldedOffset + 1)
        {
            nextOffset = pending.FetchEndOffsetExclusive;
            leaderEpoch = pending.FetchEndLeaderEpoch;
        }
        else
        {
            nextOffset = pending.LastYieldedOffset + 1;
            leaderEpoch = pending.LastYieldedLeaderEpoch;
        }
        return true;
    }

    private void SetPosition(TopicPartition partition, long position, bool dirty)
    {
        _positions[partition] = position;
        if (dirty)
        {
            if (_options.EnableAutoOffsetStore)
                StoreOffsetCore(partition, position, GetLastConsumedLeaderEpoch(partition));
        }
        else
        {
            ClearStoredOffset(partition);
        }
    }

    /// <summary>
    /// Stores under the partition's current ownership (explicit stores, seeks). A group-managed
    /// consumer stores nothing for a partition it does not own: such an offset never commits, and
    /// keeping it would leave a slot behind for a partition no rebalance removes again.
    /// </summary>
    private void StoreOffsetCore(TopicPartition partition, long offset, int leaderEpoch)
    {
        long ownership = 0;
        if (IsGroupManagedSubscription())
        {
            ownership = _ownershipStartGenerations.GetValueOrDefault(partition);
            if (ownership == 0)
                return;
        }

        StoreOffsetCore(partition, ownership, offset, leaderEpoch);
    }

    /// <summary>Stores a fetched record's progress under the ownership its fetch was created in.</summary>
    private void StoreOffsetCore(PendingFetchData pending, long offset, int leaderEpoch)
        => StoreOffsetCore(
            pending.TopicPartition,
            IsGroupManagedSubscription() ? pending.OwnershipStart : 0,
            offset,
            leaderEpoch);

    /// <summary>
    /// One lookup of the partition's slot (created on the partition's first store) and an
    /// in-place update. A write into a slot <see cref="RemoveStoredOffset"/> removed after this
    /// lookup is lost with the slot: the partition has left the assignment since.
    /// </summary>
    private void StoreOffsetCore(TopicPartition partition, long ownership, long offset, int leaderEpoch)
    {
        if (!_storedOffsetSlots.TryGetValue(partition, out var slot) || !slot.TryStore(ownership, offset, leaderEpoch))
            StoreOffsetInAddedSlot(partition, ownership, offset, leaderEpoch);
    }

    /// <summary>
    /// The first store of a partition's ownership, or a store that met a retired slot. A store
    /// validated under an ownership that ended before this write can run after assignment sync
    /// removed the partition's slot, and would add one for a partition nothing removes again. So
    /// once written, the ownership is checked again (sync ends it before it removes the slot), and
    /// a slot that still holds only the ended ownership's store is retired and removed, by
    /// instance: a store of a newer ownership either landed first (the slot is kept) or finds the
    /// slot retired and adds a fresh one. Per partition per ownership, not per message.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void StoreOffsetInAddedSlot(TopicPartition partition, long ownership, long offset, int leaderEpoch)
    {
        while (true)
        {
            var slot = _storedOffsetSlots.GetOrAdd(partition, s_createStoredOffsetSlot);
            if (!slot.TryStore(ownership, offset, leaderEpoch))
            {
                // Retired by a store whose ownership had ended: finish its removal and retry.
                _storedOffsetSlots.TryRemove(new KeyValuePair<TopicPartition, StoredOffsetSlot>(partition, slot));
                continue;
            }

            if (ownership != 0
                && _ownershipStartGenerations.GetValueOrDefault(partition) != ownership
                && slot.TryRetire(ownership))
            {
                _storedOffsetSlots.TryRemove(new KeyValuePair<TopicPartition, StoredOffsetSlot>(partition, slot));
            }

            return;
        }
    }

    /// <summary>
    /// The ownership tag a store made now belongs to: the partition's ownership start for a
    /// group-managed consumer (0 while unassigned, which never commits), 0 otherwise.
    /// </summary>
    private long GetCurrentStoreOwnership(TopicPartition partition)
        => IsGroupManagedSubscription() ? _ownershipStartGenerations.GetValueOrDefault(partition) : 0;

    private void ClearStoredOffset(TopicPartition partition)
    {
        if (_storedOffsetSlots.TryGetValue(partition, out var slot))
            slot.Clear();
    }

    /// <summary>
    /// Drops the slot of a partition that left the assignment, so a long-running consumer keeps
    /// slots only for the partitions it holds (a pattern subscription over transient topics would
    /// otherwise keep one for every partition it ever stored). Commits and commit acknowledgements
    /// find slots through the map only, so a store that looked the slot up earlier changes nothing
    /// anyone reads, and the next store of a reassigned partition creates a fresh slot. Per
    /// partition per rebalance.
    /// </summary>
    private void RemoveStoredOffset(TopicPartition partition)
        => _storedOffsetSlots.TryRemove(partition, out _);

    /// <summary>Dirty stored offsets by partition, for tests: those of the current ownership.</summary>
    internal IReadOnlyDictionary<TopicPartition, long> DirtyStoredOffsetsForTest
    {
        get
        {
            var offsets = new Dictionary<TopicPartition, long>();
            foreach (var (partition, slot) in _storedOffsetSlots)
            {
                if (slot.TryReadDirty(out var ownership, out var offset, out _)
                    && ownership == GetCurrentStoreOwnership(partition))
                {
                    offsets[partition] = offset;
                }
            }

            return offsets;
        }
    }

    /// <summary>
    /// A partition's stored offset, the ownership it was stored under, its leader epoch and whether
    /// it changed since the last successful commit. Writers take the slot with one CAS (a store is
    /// otherwise uncontended: the consume loop or the application) and readers retry while a write
    /// is in progress, so a reader always sees one store's values together. Never allocates.
    /// </summary>
    private sealed class StoredOffsetSlot
    {
        private int _sequence; // odd while a writer holds the slot
        private long _ownership;
        private long _offset;
        private int _leaderEpoch = -1;
        private bool _dirty;
        private bool _retired; // removed (or being removed) from the map: stores go to a new slot

        /// <summary>
        /// Stores, unless the slot already holds a later ownership's offset: ownership starts only
        /// grow, so such a write was validated under an ownership that has ended. 0 (not
        /// group-managed) always stores. False only when the slot is retired; nothing was written.
        /// </summary>
        public bool TryStore(long ownership, long offset, int leaderEpoch)
        {
            var sequence = Enter();
            var retired = _retired;
            if (!retired && (ownership == 0 || ownership >= _ownership))
            {
                _ownership = ownership;
                _offset = offset;
                _leaderEpoch = leaderEpoch;
                _dirty = true;
            }

            Exit(sequence);
            return !retired;
        }

        /// <summary>
        /// Retires the slot if it holds the ended <paramref name="ownership"/>'s store and nothing
        /// newer, so no later store can land in it once it leaves the map.
        /// </summary>
        public bool TryRetire(long ownership)
        {
            var sequence = Enter();
            var retire = _ownership == ownership;
            if (retire)
                _retired = true;
            Exit(sequence);
            return retire;
        }

        public void Clear()
        {
            var sequence = Enter();
            _dirty = false;
            _ownership = 0;
            Exit(sequence);
        }

        /// <summary>Marks the slot clean if it still holds exactly what was committed.</summary>
        public void ClearIfUnchanged(long ownership, long offset)
        {
            var sequence = Enter();
            if (_dirty && _ownership == ownership && _offset == offset)
                _dirty = false;
            Exit(sequence);
        }

        public bool TryReadDirty(out long ownership, out long offset, out int leaderEpoch)
        {
            var spinner = new SpinWait();
            while (true)
            {
                var sequence = Volatile.Read(ref _sequence);
                if ((sequence & 1) == 0)
                {
                    var dirty = _dirty;
                    ownership = _ownership;
                    offset = _offset;
                    leaderEpoch = _leaderEpoch;
                    Interlocked.MemoryBarrier();
                    if (Volatile.Read(ref _sequence) == sequence)
                        return dirty;
                }

                spinner.SpinOnce();
            }
        }

        private int Enter()
        {
            var spinner = new SpinWait();
            while (true)
            {
                var sequence = Volatile.Read(ref _sequence);
                if ((sequence & 1) == 0 && Interlocked.CompareExchange(ref _sequence, sequence + 1, sequence) == sequence)
                    return sequence + 1;
                spinner.SpinOnce();
            }
        }

        private void Exit(int sequence) => Volatile.Write(ref _sequence, sequence + 1);
    }

    private void SetLastConsumedLeaderEpoch(TopicPartition partition, int leaderEpoch)
    {
        if (leaderEpoch >= 0)
        {
            _lastConsumedLeaderEpochs[partition] = leaderEpoch;
            return;
        }

        ClearLastConsumedLeaderEpoch(partition);
    }

    private void ClearLastConsumedLeaderEpoch(TopicPartition partition)
    {
        _lastConsumedLeaderEpochs.TryRemove(partition, out _);
    }

    /// <summary>
    /// Replaces the fetch position from outside the prefetch loop (seek, reset, position
    /// initialization, rewind). Such a write puts the fetch position back on the consumed
    /// position, so the epoch recorded for the prefetch position no longer describes it and
    /// the consumed epoch the caller set or cleared applies again.
    /// </summary>
    private void SetFetchPosition(TopicPartition partition, long offset)
    {
        _fetchPositions[partition] = offset;
        if (_lastFetchedLeaderEpochs.TryGetValue(partition, out var fetched))
            fetched.Clear();
    }

    /// <summary>
    /// FetchRequest.LastFetchedEpoch is the leader epoch of the record at FetchOffset - 1. The
    /// prefetch position runs ahead of the consumed position, so pairing it with the consumed
    /// epoch makes the broker answer DivergingEpoch whenever a leader change falls between the
    /// two, although nothing was truncated. The epoch recorded at the prefetch advance is used
    /// when it belongs to this fetch offset; one recorded for another offset (a concurrent
    /// advance or reposition) validates nothing for this request rather than the wrong epoch.
    /// Without a recorded epoch the fetch position is the consumed position.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ResolveLastFetchedEpoch(
        TopicPartition partition,
        long fetchOffset,
        ConcurrentDictionary<TopicPartition, int>? lastConsumedLeaderEpochs,
        ConcurrentDictionary<TopicPartition, FetchedLeaderEpoch>? lastFetchedLeaderEpochs)
    {
        if (lastFetchedLeaderEpochs is not null
            && lastFetchedLeaderEpochs.TryGetValue(partition, out var fetched)
            && fetched.TryResolve(fetchOffset, out var fetchedEpoch))
        {
            return fetchedEpoch;
        }

        return lastConsumedLeaderEpochs?.GetValueOrDefault(partition, -1) ?? -1;
    }

    private void MarkOffsetCommitted(TopicPartition partition, long committedOffset)
        => MarkOffsetCommitted(partition, GetCurrentStoreOwnership(partition), committedOffset);

    private void MarkOffsetCommitted(TopicPartition partition, long ownership, long committedOffset)
    {
        _ = TryCacheCommittedOffset(
            partition,
            committedOffset,
            Interlocked.Increment(ref _committedOffsetGeneration));
        if (_storedOffsetSlots.TryGetValue(partition, out var slot))
            slot.ClearIfUnchanged(ownership, committedOffset);
    }

    internal void UpdateFetchPositionsFromPrefetch(
        TopicPartition partition,
        long nextOffset,
        int leaderEpoch,
        int fetchBufferEpoch)
    {
        if (nextOffset < 0)
            return;

        // Thread-safe update using ConcurrentDictionary — must use AddOrUpdate (not TryGetValue
        // + indexer) because seek/reset operations on other threads can write to _fetchPositions
        // concurrently, and AddOrUpdate's CAS loop prevents TOCTOU races from overwriting a
        // concurrent seek-forward with a stale prefetch offset.
        // Uses the factoryArgument overload with static lambdas to avoid closure allocation.
        var update = (Consumer: this, FetchBufferEpoch: fetchBufferEpoch, NextOffset: nextOffset);
        var fetchPosition = _fetchPositions.AddOrUpdate(
            partition,
            static (_, update) => update.NextOffset,
            static (partition, currentPos, update) =>
                update.Consumer.ShouldDropStaleFetchPartition(partition, update.FetchBufferEpoch)
                    ? currentPos
                    : Math.Max(currentPos, update.NextOffset),
            update);

        // Record the epoch only for the position this response produced. A position that stayed
        // further ahead keeps the epoch of the response that moved it there. Once per published
        // partition response, never per record; the publishing lock keeps records from overlapping.
        if (fetchPosition == nextOffset)
        {
            _lastFetchedLeaderEpochs
                .GetOrAdd(partition, static _ => new FetchedLeaderEpoch())
                .Record(nextOffset, leaderEpoch);
        }
    }

    internal Errors.ConsumeException? HandleEmptyFetchResponse(
        TopicPartition partition,
        IReadOnlyList<RecordBatch>? records,
        long highWatermark,
        int fetchBufferEpoch)
    {
        // FetchResponsePartition creates Records only when record bytes were present.
        // A non-null empty list therefore means parsing produced no complete batches.
        if (records is not { Count: 0 }
            || !_fetchPositions.TryGetValue(partition, out var fetchPosition))
        {
            _stuckFetchPositionTracker.Reset(partition);
            if (!_options.EnablePartitionEof)
                return null;

            fetchPosition = _fetchPositions.GetValueOrDefault(partition, 0);
        }
        else if (_stuckFetchPositionTracker.ObserveEmptyParsedFetch(partition, fetchPosition) is { } stuckError)
        {
            return stuckError;
        }

        if (_options.EnablePartitionEof && fetchPosition >= highWatermark)
            TryQueuePartitionEof(partition, highWatermark, fetchBufferEpoch);

        return null;
    }

    /// <summary>
    /// Re-arms partition EOF because records for the partition are being published. Prefetch
    /// calls it under the invalidation lock, which <see cref="TryQueuePartitionEof"/> also
    /// holds, after the publication's stale-epoch check; direct fetches call it on the consumer
    /// thread after every response of the cycle has been handled. Overlapping responses (replica routing, connection
    /// changes) therefore cannot interleave: an EOF reported before these records loses its
    /// marker here, so the next real EOF is reported; an EOF reported after them sees the
    /// advanced fetch position. A stale response is dropped before reaching this point and can
    /// never clear a current marker. Once per published partition response, never per record;
    /// with EOF disabled it is one field read.
    /// </summary>
    private void RearmPartitionEofForPublishedRecords(
        TopicPartition partition,
        bool hasRecords,
        long publishedEndExclusive)
    {
        // An EOF reported before these records is superseded by them: its offset is at most the
        // publication floor, below the records' end. Record that bound instead of searching the
        // EOF queue; the drain skips queued EOFs below it. O(1), and only when a marker existed.
        // A response fully covered by earlier publications (RaiseStartOffset sets its end to -1)
        // adds no records past the reported EOF, so the marker stays and no duplicate follows.
        if (!hasRecords
            || publishedEndExclusive < 0
            || !_options.EnablePartitionEof
            || !_eofEmitted.TryRemove(partition, out _))
        {
            return;
        }

        if (!_eofSupersededBelow.TryGetValue(partition, out var below) || below < publishedEndExclusive)
            _eofSupersededBelow[partition] = publishedEndExclusive;
        _hasEofSupersededBounds = true;
    }

    /// <summary>
    /// Queues a partition EOF from a fetch response. Seek, revocation and skipped-batch release
    /// invalidate the partition's fetch epoch and drop its queued EOF under this lock. An EOF
    /// from a response that passed its stale check before that invalidation must not be queued
    /// afterwards, ahead of the records the new position refetches. Revalidating here, and
    /// reading the position here, closes that window as prefetched record publication does.
    /// Runs only when a response reaches the high watermark, never per record, and takes the
    /// lock only until the partition's EOF has been reported.
    /// </summary>
    private void TryQueuePartitionEof(TopicPartition partition, long highWatermark, int fetchBufferEpoch)
    {
        // Lock-free fast path for partitions idling at the high watermark: their EOF is already
        // reported, so unrelated fetch handlers never serialize here. A clear (seek, revocation,
        // release) that races this read only defers the EOF to the next empty response, which
        // derives it again; once the marker is gone the locked path below runs.
        if (_eofEmitted.ContainsKey(partition))
            return;

        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
        {
            var fetchPosition = _fetchPositions.GetValueOrDefault(partition, 0);
            if (ShouldDropStaleFetchPartition(partition, fetchBufferEpoch)
                || fetchPosition < highWatermark
                || !_eofEmitted.TryAdd(partition, 0))
            {
                return;
            }

            _pendingEofEvents.Enqueue((partition, fetchPosition));
        }
    }

    private PendingFetchData? TryCreateSnapshotEndMarker(
        TopicPartition partition,
        FetchResponsePartition response)
    {
        var snapshot = Volatile.Read(ref _activeSnapshot);
        if (snapshot is null)
            return null;

        var visibleEndOffset = _options.IsolationLevel == IsolationLevel.ReadCommitted
                               && response.LastStableOffset >= 0
            ? response.LastStableOffset
            : response.HighWatermark;
        return snapshot.TryQueueEndMarker(partition, visibleEndOffset, out var endOffset)
            ? PendingFetchData.CreateSnapshotEnd(
                partition.Topic,
                partition.Partition,
                endOffset,
                snapshot)
            : null;
    }

    private long GetSnapshotEndOffset(TopicPartition partition)
    {
        var snapshot = Volatile.Read(ref _activeSnapshot);
        return snapshot is not null && snapshot.TryGetEndOffset(partition, out var endOffset)
            ? endOffset
            : -1;
    }

    /// <summary>
    /// Drains additional ready items from the prefetch buffer into <see cref="_pendingFetches"/>,
    /// bounded to avoid starving <see cref="EnsureAssignmentAsync"/> (and thus rebalance detection)
    /// when many partitions produce data concurrently
    /// (e.g., N partitions produce N items per FetchResponse).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrainPrefetchBuffer()
    {
        // Bound: drain at most the current assignment count to keep the loop responsive.
        // With N assigned partitions, one FetchResponse produces at most N items,
        // so this drains one full response without unbounded spinning.
        var maxDrain = _assignmentSnapshot.Count;

        for (var i = 0; i < maxDrain && _prefetchBuffer.TryRead(out var additional); i++)
        {
            EnqueuePendingFetch(additional);
            TrackPrefetchedBytes(additional, release: true);
        }
    }

    private async ValueTask<bool> WaitForPrefetchDataAsync(CancellationToken cancellationToken)
    {
        _coordinator?.BeginForegroundPollActivity();
        _telemetryMetricCollector.StandardMetrics?.BeginPollWait();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_prefetchBuffer.HasDataAvailable())
                return true;

            var observedPausedSnapshotVersion = _observedPausedSnapshotVersion;
            var wait = _prefetchBuffer.WaitToReadAsync(_options.FetchMaxWaitMs, cancellationToken);
            if (!wait.IsCompleted
                && Volatile.Read(ref _pausedSnapshotVersion) != observedPausedSnapshotVersion)
            {
                // Resume can publish immediately before the buffer installs its waiter.
                // Recheck after registration so that race cannot delay retained data.
                _prefetchBuffer.SignalReader();
            }

            return await wait.ConfigureAwait(false);
        }
        finally
        {
            _coordinator?.EndForegroundPollActivity();
            _telemetryMetricCollector.StandardMetrics?.EndPollWait();
        }
    }

    private void TrackPrefetchedBytes(PendingFetchData pending, bool release)
    {
        // Estimate bytes from batches
        var bytes = EstimatePendingFetchBytes(pending);

        if (release)
        {
            Interlocked.Add(ref _prefetchedBytes, -bytes);
            // Signal prefetch loop that memory is now available (skip for empty responses to avoid spurious signals)
            if (bytes > 0)
                SignalPrefetchMemoryAvailable();
        }
        else
        {
            Interlocked.Add(ref _prefetchedBytes, bytes);
        }
    }

    private void SignalPrefetchMemoryAvailable()
    {
        if (_prefetchMemoryAvailable.CurrentCount != 0)
            return;

        try
        {
            _prefetchMemoryAvailable.Release();
        }
        catch (SemaphoreFullException)
        {
            // Another release won the race after CurrentCount was observed as 0.
        }
    }

    /// <summary>
    /// Per-partition response overhead in a FetchResponse (Kafka FetchResponse API version 11+):
    /// partition header (4 bytes partition index, 2 bytes error code, 8 bytes high watermark,
    /// 8 bytes last stable offset, 8 bytes log start offset, 4 bytes aborted transactions count,
    /// 4 bytes record set size) = ~38 bytes.
    /// Plus per-batch header overhead (baseOffset + batchLength prefix = 12 bytes) not included in BatchLength.
    /// </summary>
    private const int PerPartitionResponseOverhead = 38;
    private const int PerBatchHeaderOverhead = 12; // baseOffset(8) + batchLength(4)

    internal static long EstimatePendingFetchBytes(PendingFetchData pending)
    {
        var batches = pending.GetBatches();
        long bytes = PerPartitionResponseOverhead;
        for (var i = 0; i < batches.Count; i++)
        {
            var batch = batches[i];
            bytes += batch.BatchLength + PerBatchHeaderOverhead;
        }
        return bytes;
    }

    public ValueTask<ConsumeResult<TKey, TValue>?> ConsumeOneAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var timeoutMilliseconds = ValidateConsumeOneTimeout(timeout);

        // Synchronous buffered fast path: no async state machine, so a buffered record
        // costs one ValueTask wrap instead of builder Start/SetResult plus the state
        // machine's struct spills (~35% of the per-poll budget, issue #2211). Exceptions
        // from validation and the buffered drain throw synchronously rather than faulting
        // the returned ValueTask — the standard idiom for a sync-completing ValueTask method.
        var pollRecorded = false;
        long? bufferedDrainStarted = null;
        // Per-record async deserializers cannot use the buffered fast path. Prepared synchronous
        // deserializers can; only a cold preparation miss routes through ConsumeOneCoreAsync.
        if (!_hasAsyncDeserializers
            && !RequiresRuntimeTimeoutValidation(timeoutMilliseconds)
            && CanUseBufferedConsumeOneFastPath(cancellationToken))
        {
            long pollTimestamp = 0;
            pollRecorded = _coordinator?.TryRecordPollFast(out pollTimestamp) ?? true;
            if (pollRecorded)
            {
                // Reuse the coordinator's timestamp read when it took one this poll.
                bufferedDrainStarted = pollTimestamp != 0 ? pollTimestamp : Stopwatch.GetTimestamp();
                var bufferedDrainDeadline = CalculateConsumeOneDeadline(
                    timeoutMilliseconds,
                    bufferedDrainStarted.Value);
                var requiresAsyncPreparation = false;
                PreparedDeserializerKey? preparedKey = null;
                var consumedBufferedRecord = _hasDeserializerPreparers
                    ? TryConsumeOneFromPendingFetchesWithPreparationCancellable(
                        out var bufferedResult,
                        out requiresAsyncPreparation,
                        out preparedKey,
                        bufferedDrainDeadline,
                        cancellationToken)
                    : TryConsumeOneFromPendingFetchesCancellable(
                        out bufferedResult,
                        bufferedDrainDeadline,
                        cancellationToken);
                if (consumedBufferedRecord)
                    return new ValueTask<ConsumeResult<TKey, TValue>?>(bufferedResult);

                if (requiresAsyncPreparation)
                {
                    return ConsumeOneWithTimeoutAfterPreparationMissAsync(
                        timeout,
                        bufferedDrainStarted,
                        pollRecorded,
                        preparedKey,
                        cancellationToken);
                }

                if (TryDequeuePendingEofResult(out var eofResult))
                    return new ValueTask<ConsumeResult<TKey, TValue>?>(eofResult);
            }
        }

        // Preserve non-blocking poll semantics: an immediately buffered accepted record may
        // win above, but a zero-timeout miss must not enter a CancelAfter(0) scheduling race.
        if (IsNonBlockingConsumeOneTimeout(timeout))
            return new ValueTask<ConsumeResult<TKey, TValue>?>((ConsumeResult<TKey, TValue>?)null);

        return ConsumeOneWithTimeoutAsync(
            timeout,
            bufferedDrainStarted,
            pollRecorded,
            default,
            cancellationToken);
    }

#if NET
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
#endif
    private async ValueTask<ConsumeResult<TKey, TValue>?> ConsumeOneWithTimeoutAsync(
        TimeSpan timeout,
        long? bufferedDrainStarted,
        bool pollRecorded,
        PreparedDeserializerKey? preparedKey,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = _ctsPool.Rent();
        timeoutCts.CancelAfter(CalculateRemainingConsumeOneTimeout(timeout, bufferedDrainStarted));

        // Fast path: if no external cancellation, use timeout CTS directly (avoids allocation)
        if (!cancellationToken.CanBeCanceled)
        {
            try
            {
                return await ConsumeOneCoreAsync(pollRecorded, preparedKey, timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                // Timeout expired with no messages - return null instead of throwing
            }
            return null;
        }

        // Slow path: need to link external cancellation with timeout
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            return await ConsumeOneCoreAsync(pollRecorded, preparedKey, linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            // Timeout expired — no message arrived. If the external token also fired by the time
            // this filter evaluates (race under load), the internal timeout still takes priority.
        }

        return null;
    }

    private async ValueTask<ConsumeResult<TKey, TValue>?> ConsumeOneWithTimeoutAfterPreparationMissAsync(
        TimeSpan timeout,
        long? bufferedDrainStarted,
        bool pollRecorded,
        PreparedDeserializerKey? preparedKey,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ConsumeOneWithTimeoutAsync(
                    timeout,
                    bufferedDrainStarted,
                    pollRecorded,
                    preparedKey,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            preparedKey?.DisposePreparationActivity();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long ValidateConsumeOneTimeout(TimeSpan timeout)
    {
        const long MaxSupportedTimeoutMilliseconds = 0xfffffffe;
        var timeoutMilliseconds = (long)timeout.TotalMilliseconds;
        if (timeoutMilliseconds < -1 || timeoutMilliseconds > MaxSupportedTimeoutMilliseconds)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        return timeoutMilliseconds;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsNonBlockingConsumeOneTimeout(TimeSpan timeout) =>
        timeout == TimeSpan.Zero;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool RequiresRuntimeTimeoutValidation(long timeoutMilliseconds)
    {
#if NETSTANDARD2_0
        // Legacy runtimes consuming this asset may cap CancelAfter at Int32.MaxValue.
        // Route ambiguous values through the CTS path so the actual runtime decides.
        return timeoutMilliseconds > int.MaxValue;
#else
        return false;
#endif
    }

    internal static TimeSpan CalculateRemainingConsumeOneTimeout(TimeSpan timeout, long? bufferedDrainStarted)
    {
        if (bufferedDrainStarted is not { } startedAt || timeout < TimeSpan.Zero)
            return timeout;

        var remaining = timeout - Stopwatch.GetElapsedTime(startedAt);
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long CalculateConsumeOneDeadline(long timeoutMilliseconds, long startedAt) =>
        timeoutMilliseconds < 0
            ? 0
            : startedAt + (timeoutMilliseconds * Stopwatch.Frequency / 1_000);

    private bool CanUseBufferedConsumeOneFastPath(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested
            || Volatile.Read(ref _consumerDisposed) != 0
            || !_initialized
            || _pendingFetches.Count == 0
            || Volatile.Read(ref _batchIterationEpoch.ConsumeOneDeliveryChangesPending) != 0)
        {
            return false;
        }

        if (IsTopicFilterRefreshDue())
            return false;

        if (_options.OffsetCommitMode == OffsetCommitMode.Auto
            && _coordinator is not null
            && !IsAutoCommitRunning())
        {
            return false;
        }

        if (_options.QueuedMinMessages > 1 && !HasPrefetchStarted())
            return false;

        var pending = _pendingFetches.Peek();
        if (!IsCurrentlyAssigned(pending.TopicPartition))
            return false;

        var coordinator = _coordinator;
        if ((_subscriptionSnapshot.Count != 0 || _topicPattern is not null) && coordinator is not null)
        {
            return IsCoordinatorAssignmentSyncCurrent(coordinator, out _);
        }

        return IsManualAssignmentEnsureCurrent();
    }

    private bool IsTopicFilterRefreshDue()
        => _topicFilter is not null && IsFilterRefreshDue();

    private bool IsFilterRefreshDue()
    {
        var lastRefresh = Volatile.Read(ref _lastFilterRefreshTicks);
        return lastRefresh == 0
               || Dekaf.MonotonicClock.GetMilliseconds() - lastRefresh >= FilterRefreshIntervalMilliseconds;
    }

    private bool HasPrefetchStarted() => Volatile.Read(ref _prefetchTask) is not null;

    private bool IsAutoCommitRunning() => Volatile.Read(ref _autoCommitTask) is { IsCompleted: false };

    private bool IsCoordinatorAssignmentSyncCurrent(
        ConsumerCoordinator coordinator,
        out int assignmentVersion)
    {
        assignmentVersion = coordinator.AssignmentVersion;
        return Volatile.Read(ref _lastCoordinatorAssignmentVersion) == assignmentVersion
               && coordinator.IsAssignmentSyncCurrent(assignmentVersion);
    }

#if NET
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
#endif
    private async ValueTask<ConsumeResult<TKey, TValue>?> ConsumeOneCoreAsync(
        bool pollRecorded,
        PreparedDeserializerKey? preparedKey,
        CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _consumerDisposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaConsumer<TKey, TValue>));

        ThrowIfNotInitialized();

        if (_options.OffsetCommitMode == OffsetCommitMode.Auto && _coordinator is not null)
        {
            await StartAutoCommitAsync(cancellationToken).ConfigureAwait(false);
        }

        var prefetchEnabled = _options.QueuedMinMessages > 1;
        _prefetchEnabled = prefetchEnabled;
        if (prefetchEnabled && !cancellationToken.IsCancellationRequested)
        {
            StartPrefetch();
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            if (pollRecorded)
            {
                pollRecorded = false;
            }
            else
            {
                await RecordPollAsync(cancellationToken).ConfigureAwait(false);
            }

            await EnsureAssignmentForPollAsync(cancellationToken).ConfigureAwait(false);
            PrepareConsumeOneDelivery();

            if (_assignmentSnapshot.Count == 0)
            {
                await DelayForForegroundPollAsync(100, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (_pendingFetches.Count == 0)
            {
                if (!await FillPendingFetchesForSingleConsumeAsync(prefetchEnabled, cancellationToken)
                    .ConfigureAwait(false))
                {
                    return null;
                }
            }

            if (_hasAsyncDeserializers || _hasDeserializerPreparers)
            {
                var asyncResult = await ConsumeOneFromPendingFetchesAsync(preparedKey, cancellationToken)
                    .ConfigureAwait(false);
                if (asyncResult is not null)
                    return asyncResult;
            }
            else if (TryConsumeOneFromPendingFetchesCancellable(
                         out var result,
                         timeoutDeadline: 0,
                         cancellationToken))
            {
                return result;
            }

            if (TryDequeuePendingEofResult(out var eofResult))
                return eofResult;
        }

        return null;
    }

    private bool TryDequeuePendingEofResult(out ConsumeResult<TKey, TValue> result)
    {
        if (TryDequeueCurrentEof(out var eofEvent))
        {
            result = ConsumeResult<TKey, TValue>.CreatePartitionEof(
                eofEvent.Partition.Topic,
                eofEvent.Partition.Partition,
                eofEvent.Offset);
            return true;
        }

        result = default;
        return false;
    }

    private async ValueTask<bool> FillPendingFetchesForSingleConsumeAsync(bool prefetchEnabled, CancellationToken cancellationToken)
    {
        if (!prefetchEnabled)
        {
            await FetchRecordsAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (_prefetchBuffer.TryRead(out var prefetched))
        {
            EnqueuePendingFetch(prefetched);
            TrackPrefetchedBytes(prefetched, release: true);
            DrainPrefetchBuffer();
            return true;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (await WaitForPrefetchDataAsync(cancellationToken).ConfigureAwait(false))
            {
                if (_prefetchBuffer.TryRead(out var fetched))
                {
                    EnqueuePendingFetch(fetched);
                    TrackPrefetchedBytes(fetched, release: true);
                    DrainPrefetchBuffer();
                }
            }
            else if (_prefetchBuffer.IsCompleted)
            {
                return false;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Prefetch was not ready before FetchMaxWaitMs. The caller loop will check EOF and retry.
        }

        return true;
    }

    private void EagerParsePendingOrRemove(PendingFetchData pending)
    {
        try
        {
            pending.EagerParseAll(_recordHeaderRoutingPlan);
        }
        catch
        {
            if (_pendingFetches.Count > 0
                && ReferenceEquals(_pendingFetches.Peek(), pending))
            {
                DisposeQueuedFetch(_pendingFetches.Dequeue());
            }

            throw;
        }
    }

    private readonly struct SynchronousDeserializerMode;
    private readonly struct PreparedDeserializerMode;
    private readonly struct DeserializeKeyMode;
    private readonly struct RetainedKeyMode;
    private readonly struct NoRecordFilterMode;
    private readonly struct RecordFilterMode;
    private readonly struct NoRecordHeaderMode;
    private readonly struct RecordHeaderMode;

    private sealed class PreparedDeserializerKey
    {
        private readonly PendingFetchData? _pending;
        private readonly long _offset;
        private readonly int _pendingGeneration;
        private System.Diagnostics.Activity? _preparationActivity;

        internal PreparedDeserializerKey(
            PendingFetchData pending,
            long offset,
            System.Diagnostics.Activity preparationActivity)
        {
            _pending = pending;
            _offset = offset;
            _pendingGeneration = pending.HeaderGeneration;
            _preparationActivity = preparationActivity;
        }

        internal PreparedDeserializerKey(PendingFetchData pending, long offset, TKey? value)
        {
            _pending = pending;
            _offset = offset;
            _pendingGeneration = pending.HeaderGeneration;
            Value = value;
        }

        internal TKey? Value { get; }

        internal bool Matches(PendingFetchData pending, long offset) =>
            ReferenceEquals(_pending, pending)
            && _offset == offset
            && _pendingGeneration == pending.HeaderGeneration;

        internal void RetainPreparationActivity(System.Diagnostics.Activity activity) =>
            _preparationActivity = activity;

        internal System.Diagnostics.Activity? TakePreparationActivity(
            PendingFetchData pending,
            long offset)
        {
            if (!Matches(pending, offset))
            {
                DisposePreparationActivity();
                return null;
            }

            var activity = _preparationActivity;
            _preparationActivity = null;
            return activity;
        }

        internal void DisposePreparationActivity()
        {
            _preparationActivity?.Dispose();
            _preparationActivity = null;
        }
    }

    private bool TryConsumeOneFromPendingFetches(out ConsumeResult<TKey, TValue> result) =>
        TryConsumeOneFromPendingFetchesCancellable(
            out result,
            timeoutDeadline: 0,
            CancellationToken.None);

    private bool TryConsumeOneFromPendingFetchesCancellable(
        out ConsumeResult<TKey, TValue> result,
        long timeoutDeadline,
        CancellationToken cancellationToken) =>
        _options.RecordFilter is null
            ? TryConsumeOneFromPendingFetchesForFilterMode<
                SynchronousDeserializerMode,
                NoRecordFilterMode>(
                out result, out _, out _,
                timeoutDeadline,
                cancellationToken)
            : TryConsumeOneFromPendingFetchesForFilterMode<
                SynchronousDeserializerMode,
                RecordFilterMode>(
                out result, out _, out _,
                timeoutDeadline,
                cancellationToken);

    private bool TryConsumeOneFromPendingFetchesWithPreparation(
        out ConsumeResult<TKey, TValue> result,
        out bool requiresAsyncPreparation,
        out PreparedDeserializerKey? preparedKey) =>
        TryConsumeOneFromPendingFetchesWithPreparationCancellable(
            out result,
            out requiresAsyncPreparation,
            out preparedKey,
            timeoutDeadline: 0,
            CancellationToken.None);

    private bool TryConsumeOneFromPendingFetchesWithPreparationCancellable(
        out ConsumeResult<TKey, TValue> result,
        out bool requiresAsyncPreparation,
        out PreparedDeserializerKey? preparedKey,
        long timeoutDeadline,
        CancellationToken cancellationToken) =>
        _options.RecordFilter is null
            ? TryConsumeOneFromPendingFetchesForFilterMode<
                PreparedDeserializerMode,
                NoRecordFilterMode>(
                out result,
                out requiresAsyncPreparation,
                out preparedKey,
                timeoutDeadline,
                cancellationToken)
            : TryConsumeOneFromPendingFetchesForFilterMode<
                PreparedDeserializerMode,
                RecordFilterMode>(
                out result,
                out requiresAsyncPreparation,
                out preparedKey,
                timeoutDeadline,
                cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryConsumeOneFromPendingFetchesForFilterMode<
        TDeserializerMode,
        TRecordFilterMode>(
        out ConsumeResult<TKey, TValue> result,
        out bool requiresAsyncPreparation,
        out PreparedDeserializerKey? preparedKey,
        long timeoutDeadline,
        CancellationToken cancellationToken)
        where TDeserializerMode : struct
        where TRecordFilterMode : struct =>
        _hasRecordHeaderDeserializers
            ? TryConsumeOneFromPendingFetchesCore<
                TDeserializerMode,
                TRecordFilterMode,
                RecordHeaderMode>(
                out result,
                out requiresAsyncPreparation,
                out preparedKey,
                timeoutDeadline,
                cancellationToken)
            : TryConsumeOneFromPendingFetchesCore<
                TDeserializerMode,
                TRecordFilterMode,
                NoRecordHeaderMode>(
                out result,
                out requiresAsyncPreparation,
                out preparedKey,
                timeoutDeadline,
                cancellationToken);

    private bool TryConsumeOneFromPendingFetchesCore<
        TDeserializerMode,
        TRecordFilterMode,
        TRecordHeaderMode>(
        out ConsumeResult<TKey, TValue> result,
        out bool requiresAsyncPreparation,
        out PreparedDeserializerKey? preparedKey,
        long timeoutDeadline,
        CancellationToken cancellationToken)
        where TDeserializerMode : struct
        where TRecordFilterMode : struct
        where TRecordHeaderMode : struct
    {
        result = default!;
        requiresAsyncPreparation = false;
        preparedKey = null;

        var metricsEnabled = Diagnostics.DekafMetrics.MessagesReceived.Enabled
                             || Diagnostics.DekafMetrics.BytesReceived.Enabled;
        var hasTraceListeners = Diagnostics.DekafDiagnostics.Source.HasListeners();
        var hasInterceptors = _interceptors is not null;
        var rawTrackingEnabled = _rawRecordTrackingEnabled;
        var recordFilter = typeof(TRecordFilterMode) == typeof(RecordFilterMode)
            ? _options.RecordFilter
            : null;
        var recordsUntilPollRefresh = PollRefreshRecordInterval;

        // A new consume call proves the record returned by the previous call was processed
        // (poll contract), so everything yielded from the head fetch becomes committable.
        if (_pendingFetches.Count > 0)
            _pendingFetches.Peek().MarkYieldedProcessed();

        while (_pendingFetches.Count > 0)
        {
            if (ClearFetchBufferForPendingCoordinatorRevocations())
                continue;

            var pending = _pendingFetches.Peek();
            long? batchProcessingStarted = _adaptiveFetchSizer is not null
                ? Stopwatch.GetTimestamp() : null;

            EagerParsePendingOrRemove(pending);

            System.Diagnostics.Activity? activity = null;
            var pendingDisposed = false;
            var pendingRemoved = false;
            try
            {
                while (true)
                {
                    var readingProtocolData = true;
                    var offset = -1L;
                    var runningInterceptor = false;
                    var runningFilter = false;
                    try
                    {
                        if (!pending.MoveNext())
                            break;

                        ref readonly var record = ref pending.CurrentRecord;
                        offset = pending.CurrentBaseOffset + record.OffsetDelta;
                        var timestampMs = pending.CurrentBaseTimestamp + record.TimestampDelta;
                        var timestampType = pending.CurrentTimestampType;
                        // Hoist every record field used below: `record` references pooled
                        // batch storage, and everything past this point (activity listeners,
                        // deserializers, interceptors) can run user code that Seeks/Assigns
                        // and recycles that storage.
                        var keyData = record.Key;
                        var valueData = record.Value;
                        var isKeyNull = record.IsKeyNull;
                        var isValueNull = record.IsValueNull;
                        var pooledHeaders = record.Headers;
                        var pooledHeaderCount = record.HeaderCount;
                        var headerRouting = record.CreateHeaderRoutingLookup(
                            _recordHeaderRoutingPlan);
                        var messageBytes = (isKeyNull ? 0 : keyData.Length) +
                                           (isValueNull ? 0 : valueData.Length);

                        if (recordFilter is not null)
                        {
                            readingProtocolData = false;
                            var filterContext = new ConsumerRecordFilterContext(
                                pending.Topic,
                                pending.PartitionIndex,
                                offset,
                                timestampMs,
                                timestampType,
                                pending.CurrentPartitionLeaderEpoch >= 0
                                    ? pending.CurrentPartitionLeaderEpoch
                                    : null,
                                keyData,
                                isKeyNull,
                                valueData,
                                isValueNull,
                                pooledHeaders.AsSpan(0, pooledHeaderCount));
                            BeginConsumeOneFetchUse(pending);
                            runningFilter = true;
                            var shouldDeserialize = recordFilter.ShouldDeserialize(in filterContext);
                            runningFilter = false;
                            pendingDisposed |= EndConsumeOneFetchUse(pending);
                            cancellationToken.ThrowIfCancellationRequested();
                            if (pendingDisposed)
                                break;

                            var filterIterationStatus = GetConsumeOneDeliveryStatus(pending.TopicPartition);
                            if (filterIterationStatus != RecordIterationStatus.Continue)
                            {
                                var pausedDuringDelivery = filterIterationStatus == RecordIterationStatus.Paused;
                                pendingRemoved = StopConsumeOneDelivery(pending, pausedDuringDelivery);
                                break;
                            }

                            if (!shouldDeserialize)
                            {
                                TrackConsumedPosition(pending, offset, messageBytes);
                                pending.MarkYieldedProcessed();
                                readingProtocolData = true;
                                if (ShouldYieldAfterFilteredRecord(
                                        timeoutDeadline,
                                        ref recordsUntilPollRefresh,
                                        cancellationToken))
                                {
                                    return false;
                                }
                                continue;
                            }
                        }

                        if (hasTraceListeners)
                        {
                            activity = StartProtectedConsumeOneActivity(
                                pending,
                                pooledHeaders,
                                pooledHeaderCount,
                                offset,
                                isValueNull,
                                out pendingDisposed);

                            if (pendingDisposed)
                                break;
                        }

                        // User deserialization begins in this constructor. Its exceptions
                        // must propagate even when their type also occurs in protocol codecs.
                        readingProtocolData = false;
                        BeginConsumeOneFetchUse(pending);
                        if (typeof(TDeserializerMode) == typeof(PreparedDeserializerMode))
                        {
                            if (!TryCreateResultWithPreparedDeserialization<DeserializeKeyMode>(
                                    pending,
                                    offset,
                                    keyData,
                                    isKeyNull,
                                    valueData,
                                    isValueNull,
                                    pooledHeaders,
                                    pooledHeaderCount,
                                    headerRouting,
                                    timestampMs,
                                    timestampType,
                                    pending.CurrentPartitionLeaderEpoch >= 0
                                        ? pending.CurrentPartitionLeaderEpoch
                                        : null,
                                    ref preparedKey,
                                    out result))
                            {
                                pendingDisposed |= EndConsumeOneFetchUse(pending);
                                if (pendingDisposed)
                                    break;

                                pending.BufferCurrentForRedelivery();
                                requiresAsyncPreparation = true;
                                if (activity is not null)
                                {
                                    if (preparedKey is null)
                                        preparedKey = new PreparedDeserializerKey(pending, offset, activity);
                                    else
                                        preparedKey.RetainPreparationActivity(activity);
                                    activity = null;
                                }
                                return false;
                            }
                        }
                        else
                        {
                            if (typeof(TRecordHeaderMode) == typeof(RecordHeaderMode))
                            {
                                result = ConsumeResult<TKey, TValue>.CreateWithHeaderRouting(
                                    pending.Topic,
                                    pending.PartitionIndex,
                                    offset,
                                    keyData,
                                    isKeyNull,
                                    valueData,
                                    isValueNull,
                                    pooledHeaders,
                                    pooledHeaderCount,
                                    headerRouting,
                                    pending,
                                    timestampMs,
                                    timestampType,
                                    pending.CurrentPartitionLeaderEpoch >= 0
                                        ? pending.CurrentPartitionLeaderEpoch
                                        : null,
                                    _recordHeaderDeserializationHeaders,
                                    _keyDeserializer,
                                    _valueDeserializer);
                            }
                            else
                            {
                                result = new ConsumeResult<TKey, TValue>(
                                    topic: pending.Topic,
                                    partition: pending.PartitionIndex,
                                    offset: offset,
                                    keyData: keyData,
                                    isKeyNull: isKeyNull,
                                    valueData: valueData,
                                    isValueNull: isValueNull,
                                    pooledHeaders: pooledHeaders,
                                    pooledHeaderCount: pooledHeaderCount,
                                    headerOwner: pending,
                                    timestampMs: timestampMs,
                                    timestampType: timestampType,
                                    leaderEpoch: pending.CurrentPartitionLeaderEpoch >= 0 ? pending.CurrentPartitionLeaderEpoch : null,
                                    keyDeserializer: _keyDeserializer,
                                    valueDeserializer: _valueDeserializer);
                            }
                        }
                        pendingDisposed |= EndConsumeOneFetchUse(pending);

                        if (pendingDisposed)
                            break;

                        var iterationStatus = GetConsumeOneDeliveryStatus(pending.TopicPartition);
                        if (iterationStatus != RecordIterationStatus.Continue)
                        {
                            var pausedDuringDelivery = iterationStatus == RecordIterationStatus.Paused;
                            pendingRemoved = StopConsumeOneDelivery(pending, pausedDuringDelivery);
                            break;
                        }

                        if (hasInterceptors)
                        {
                            BeginConsumeOneFetchUse(pending);
                            runningInterceptor = true;
                            result = ApplyOnConsumeInterceptors(result);
                            runningInterceptor = false;
                            pendingDisposed |= EndConsumeOneFetchUse(pending);

                            if (pendingDisposed)
                                break;

                            iterationStatus = GetConsumeOneDeliveryStatus(pending.TopicPartition);
                            if (iterationStatus != RecordIterationStatus.Continue)
                            {
                                var pausedDuringDelivery = iterationStatus == RecordIterationStatus.Paused;
                                pendingRemoved = StopConsumeOneDelivery(pending, pausedDuringDelivery);
                                break;
                            }
                        }

                        TrackConsumedPosition(pending, offset, messageBytes);

                        if (rawTrackingEnabled)
                        {
                            _currentRawKey = NormalizeRawRecordBytes(keyData, isKeyNull);
                            _currentRawValue = NormalizeRawRecordBytes(valueData, isValueNull);
                        }

                        return true;
                    }
                    catch (OperationCanceledException) when (readingProtocolData)
                    {
                        throw;
                    }
                    catch (Exception ex) when (
                        readingProtocolData && ProtocolDataErrorClassifier.IsProtocolDataError(ex))
                    {
                        LogRecordParsingError(ex, pending.Topic, pending.PartitionIndex);
                        break;
                    }
                    catch (Exception ex) when (!readingProtocolData)
                    {
                        ThrowAfterDeliveryFailure(
                            pending, offset, runningInterceptor || runningFilter, hasAsyncDeserializers: false, ex);
                        throw;
                    }
                }
            }
            finally
            {
                activity?.Dispose();
            }

            if (pendingDisposed || pendingRemoved)
                continue;

            FlushConsumedPositions(pending);

            if (metricsEnabled && pending.MessageCount > 0)
                EmitFetchMetrics(pending);

            if (batchProcessingStarted.HasValue)
            {
                var processingDuration = Stopwatch.GetElapsedTime(batchProcessingStarted.Value);
                ReportAdaptiveProcessingComplete(processingDuration);
            }

            DequeuePendingFetch().Dispose();
        }

        return false;
    }

    /// <summary>
    /// Asynchronous-deserialization sibling of <see cref="TryConsumeOneFromPendingFetches"/>,
    /// used when an <see cref="IAsyncDeserializer{T}"/> or a cold
    /// <see cref="IAsyncDeserializerPreparer{T}"/> is configured. Keep the drain
    /// bookkeeping (poll contract, fetch-clear checks, rewind, metrics, disposal) in sync with
    /// the synchronous method — the only intended difference is that deserialization is awaited
    /// before the result is constructed, and interceptors therefore run after that await.
    /// Returns null when no record is buffered.
    /// </summary>
    private ValueTask<ConsumeResult<TKey, TValue>?> ConsumeOneFromPendingFetchesAsync(
        PreparedDeserializerKey? preparedKey,
        CancellationToken cancellationToken) =>
        _options.RecordFilter is null
            ? ConsumeOneFromPendingFetchesCoreAsync<NoRecordFilterMode>(preparedKey, cancellationToken)
            : ConsumeOneFromPendingFetchesCoreAsync<RecordFilterMode>(preparedKey, cancellationToken);

#if NET
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
#endif
    private async ValueTask<ConsumeResult<TKey, TValue>?> ConsumeOneFromPendingFetchesCoreAsync<TRecordFilterMode>(
        PreparedDeserializerKey? preparedKey,
        CancellationToken cancellationToken)
        where TRecordFilterMode : struct
    {
        var metricsEnabled = Diagnostics.DekafMetrics.MessagesReceived.Enabled
                             || Diagnostics.DekafMetrics.BytesReceived.Enabled;
        var hasTraceListeners = Diagnostics.DekafDiagnostics.Source.HasListeners();
        var hasInterceptors = _interceptors is not null;
        var rawTrackingEnabled = _rawRecordTrackingEnabled;
        var recordFilter = typeof(TRecordFilterMode) == typeof(RecordFilterMode)
            ? _options.RecordFilter
            : null;
        var recordsUntilPollRefresh = PollRefreshRecordInterval;

        // A new consume call proves the record returned by the previous call was processed
        // (poll contract), so everything yielded from the head fetch becomes committable.
        if (_pendingFetches.Count > 0)
            _pendingFetches.Peek().MarkYieldedProcessed();

        while (_pendingFetches.Count > 0)
        {
            if (ClearFetchBufferForPendingCoordinatorRevocations())
                continue;

            var pending = _pendingFetches.Peek();
            long? batchProcessingStarted = _adaptiveFetchSizer is not null
                ? Stopwatch.GetTimestamp() : null;

            EagerParsePendingOrRemove(pending);

            // lgtm[cs/missed-using-statement] Activity can be acquired after loop entry and reassigned.
            System.Diagnostics.Activity? activity = null;
            var pendingDisposed = false;
            var pendingRemoved = false;
            try
            {
                while (true)
                {
                    var readingProtocolData = true;
                    var offset = -1L;
                    try
                    {
                        if (!pending.MoveNext())
                            break;

                        var record = pending.CurrentRecord;
                        offset = pending.CurrentBaseOffset + record.OffsetDelta;
                        if (preparedKey is not null && activity is null)
                            activity = preparedKey.TakePreparationActivity(pending, offset);
                        var timestampMs = pending.CurrentBaseTimestamp + record.TimestampDelta;
                        var timestampType = pending.CurrentTimestampType;
                        // Hoist every record field used below into locals before the await:
                        // `record` references pooled batch storage, and the deserializer or an
                        // interceptor can run user code that Seeks/Assigns and recycles it.
                        var keyData = record.Key;
                        var valueData = record.Value;
                        var isKeyNull = record.IsKeyNull;
                        var isValueNull = record.IsValueNull;
                        var pooledHeaders = record.Headers;
                        var pooledHeaderCount = record.HeaderCount;
                        var headerRouting = record.CreateHeaderRoutingLookup(
                            _recordHeaderRoutingPlan);
                        var messageBytes = (isKeyNull ? 0 : keyData.Length) +
                                           (isValueNull ? 0 : valueData.Length);

                        if (recordFilter is not null)
                        {
                            readingProtocolData = false;
                            var filterContext = new ConsumerRecordFilterContext(
                                pending.Topic,
                                pending.PartitionIndex,
                                offset,
                                timestampMs,
                                timestampType,
                                pending.CurrentPartitionLeaderEpoch >= 0
                                    ? pending.CurrentPartitionLeaderEpoch
                                    : null,
                                keyData,
                                isKeyNull,
                                valueData,
                                isValueNull,
                                pooledHeaders.AsSpan(0, pooledHeaderCount));
                            BeginConsumeOneFetchUse(pending);
                            var shouldDeserialize = recordFilter.ShouldDeserialize(in filterContext);
                            pendingDisposed |= EndConsumeOneFetchUse(pending);
                            cancellationToken.ThrowIfCancellationRequested();
                            if (pendingDisposed)
                                break;

                            var filterIterationStatus = GetConsumeOneDeliveryStatus(pending.TopicPartition);
                            if (filterIterationStatus != RecordIterationStatus.Continue)
                            {
                                var pausedDuringDelivery = filterIterationStatus == RecordIterationStatus.Paused;
                                pendingRemoved = StopConsumeOneDelivery(pending, pausedDuringDelivery);
                                break;
                            }

                            if (!shouldDeserialize)
                            {
                                TrackConsumedPosition(pending, offset, messageBytes);
                                pending.MarkYieldedProcessed();
                                readingProtocolData = true;
                                if (ShouldYieldAfterFilteredRecord(
                                        timeoutDeadline: 0,
                                        ref recordsUntilPollRefresh,
                                        cancellationToken))
                                {
                                    return null;
                                }
                                continue;
                            }
                        }

                        if (hasTraceListeners && activity is null)
                        {
                            activity = StartProtectedConsumeOneActivity(
                                pending,
                                pooledHeaders,
                                pooledHeaderCount,
                                offset,
                                isValueNull,
                                out pendingDisposed);

                            if (pendingDisposed)
                                break;
                        }

                        // User deserialization begins in this await. Its exceptions must
                        // propagate even when their type also occurs in protocol codecs.
                        readingProtocolData = false;
                        BeginConsumeOneFetchUse(pending);
                        int? leaderEpoch = pending.CurrentPartitionLeaderEpoch >= 0
                            ? pending.CurrentPartitionLeaderEpoch
                            : null;
                        ConsumeResult<TKey, TValue> result;
                        if (_hasAsyncDeserializers)
                        {
                            result = await CreateResultWithAsyncDeserializationAsync(
                                    pending,
                                    offset,
                                    keyData,
                                    isKeyNull,
                                    valueData,
                                    isValueNull,
                                    pooledHeaders,
                                    pooledHeaderCount,
                                    headerRouting,
                                    timestampMs,
                                    timestampType,
                                    leaderEpoch,
                                    cancellationToken)
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            var keyPreparationAttempts = 0;
                            var valuePreparationAttempts = 0;
                            while (true)
                            {
                                if (TryCreateResultAfterPreparation(
                                        pending,
                                        offset,
                                        keyData,
                                        isKeyNull,
                                        valueData,
                                        isValueNull,
                                        pooledHeaders,
                                        pooledHeaderCount,
                                        in headerRouting,
                                        timestampMs,
                                        timestampType,
                                        leaderEpoch,
                                        ref preparedKey,
                                        out result))
                                {
                                    break;
                                }

                                var component = GetRequiredPreparationComponent(
                                    pending,
                                    offset,
                                    isKeyNull,
                                    preparedKey);
                                ReserveDeserializerPreparationAttempt(
                                    component,
                                    ref keyPreparationAttempts,
                                    ref valuePreparationAttempts);
                                await PrepareRecordDeserializerAsync(
                                        pending,
                                        offset,
                                        keyData,
                                        isKeyNull,
                                        valueData,
                                        isValueNull,
                                        pooledHeaders,
                                        pooledHeaderCount,
                                        timestampMs,
                                        timestampType,
                                        headerRouting,
                                        component,
                                        cancellationToken)
                                    .ConfigureAwait(false);
                            }
                        }
                        pendingDisposed |= EndConsumeOneFetchUse(pending);

                        if (pendingDisposed)
                            break;

                        var iterationStatus = GetConsumeOneDeliveryStatus(pending.TopicPartition);
                        if (iterationStatus != RecordIterationStatus.Continue)
                        {
                            var pausedDuringDelivery = iterationStatus == RecordIterationStatus.Paused;
                            pendingRemoved = StopConsumeOneDelivery(pending, pausedDuringDelivery);
                            break;
                        }

                        if (hasInterceptors)
                        {
                            BeginConsumeOneFetchUse(pending);
                            result = ApplyOnConsumeInterceptors(result);
                            pendingDisposed |= EndConsumeOneFetchUse(pending);

                            if (pendingDisposed)
                                break;

                            iterationStatus = GetConsumeOneDeliveryStatus(pending.TopicPartition);
                            if (iterationStatus != RecordIterationStatus.Continue)
                            {
                                var pausedDuringDelivery = iterationStatus == RecordIterationStatus.Paused;
                                pendingRemoved = StopConsumeOneDelivery(pending, pausedDuringDelivery);
                                break;
                            }
                        }

                        TrackConsumedPosition(pending, offset, messageBytes);

                        if (rawTrackingEnabled)
                        {
                            _currentRawKey = NormalizeRawRecordBytes(keyData, isKeyNull);
                            _currentRawValue = NormalizeRawRecordBytes(valueData, isValueNull);
                        }

                        return result;
                    }
                    catch (OperationCanceledException) when (readingProtocolData)
                    {
                        throw;
                    }
                    catch (Exception ex) when (
                        readingProtocolData && ProtocolDataErrorClassifier.IsProtocolDataError(ex))
                    {
                        LogRecordParsingError(ex, pending.Topic, pending.PartitionIndex);
                        break;
                    }
                    catch (Exception) when (!readingProtocolData)
                    {
                        try
                        {
                            if (!HasPendingFetchClear(pending.TopicPartition))
                                RewindAfterDeliveryFailure(pending, offset);
                        }
                        finally
                        {
                            EndConsumeOneFetchUseIfActive(pending);
                        }
                        throw;
                    }
                }
            }
            finally
            {
                activity?.Dispose();
            }

            if (pendingDisposed || pendingRemoved)
                continue;

            FlushConsumedPositions(pending);

            if (metricsEnabled && pending.MessageCount > 0)
                EmitFetchMetrics(pending);

            if (batchProcessingStarted.HasValue)
            {
                var processingDuration = Stopwatch.GetElapsedTime(batchProcessingStarted.Value);
                ReportAdaptiveProcessingComplete(processingDuration);
            }

            DequeuePendingFetch().Dispose();
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ShouldYieldAfterFilteredRecord(
        long timeoutDeadline,
        ref int recordsUntilPollRefresh,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (timeoutDeadline != 0 && Stopwatch.GetTimestamp() >= timeoutDeadline)
               || --recordsUntilPollRefresh == 0;
    }

    /// <summary>
    /// Deserializes a record via the configured <see cref="IAsyncDeserializer{T}"/> implementations
    /// (falling back to the synchronous deserializer for a component without one in mixed
    /// configurations) and constructs the result from the pre-deserialized values. Null-ness
    /// semantics mirror the eager ConsumeResult constructor: null keys skip the deserializer,
    /// null values invoke it with empty data and <c>IsNull = true</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowAfterDeliveryFailure(
        PendingFetchData pending,
        long offset,
        bool runningUserCallback,
        bool hasAsyncDeserializers,
        Exception exception)
    {
        try
        {
            if (!runningUserCallback
                && !hasAsyncDeserializers
                && exception is not OperationCanceledException
                && exception is not RecordDeserializationException)
            {
                ref readonly var record = ref pending.CurrentRecord;
                exception = ConsumeResult<TKey, TValue>.CreateDeserializationException(
                    ConsumeResult<TKey, TValue>.LastDeserializationOrigin,
                    pending.Topic,
                    pending.PartitionIndex,
                    offset,
                    pending.CurrentBaseTimestamp + record.TimestampDelta,
                    pending.CurrentTimestampType,
                    record.Key,
                    record.IsKeyNull,
                    record.Value,
                    record.IsValueNull,
                    headers: null,
                    record.Headers,
                    record.HeaderCount,
                    exception);
            }

            if (!HasPendingFetchClear(pending.TopicPartition))
                RewindAfterDeliveryFailure(pending, offset);
        }
        finally
        {
            EndConsumeOneFetchUseIfActive(pending);
        }

        ExceptionDispatchInfo.Capture(exception).Throw();
    }

    private bool TryCreateResultAfterPreparation(
        PendingFetchData pending,
        long offset,
        ReadOnlyMemory<byte> keyData,
        bool isKeyNull,
        ReadOnlyMemory<byte> valueData,
        bool isValueNull,
        Header[]? pooledHeaders,
        int pooledHeaderCount,
        in RecordHeaderRoutingLookup headerRouting,
        long timestampMs,
        TimestampType timestampType,
        int? leaderEpoch,
        ref PreparedDeserializerKey? preparedKey,
        out ConsumeResult<TKey, TValue> result)
    {
        if (preparedKey?.Matches(pending, offset) == true)
        {
            return TryCreateResultWithPreparedDeserialization<RetainedKeyMode>(
                pending,
                offset,
                keyData,
                isKeyNull,
                valueData,
                isValueNull,
                pooledHeaders,
                pooledHeaderCount,
                in headerRouting,
                timestampMs,
                timestampType,
                leaderEpoch,
                ref preparedKey,
                out result);
        }

        return TryCreateResultWithPreparedDeserialization<DeserializeKeyMode>(
            pending,
            offset,
            keyData,
            isKeyNull,
            valueData,
            isValueNull,
            pooledHeaders,
            pooledHeaderCount,
            in headerRouting,
            timestampMs,
            timestampType,
            leaderEpoch,
            ref preparedKey,
            out result);
    }

    private bool TryCreateResultWithPreparedDeserialization<TPreparedKeyMode>(
        PendingFetchData pending,
        long offset,
        ReadOnlyMemory<byte> keyData,
        bool isKeyNull,
        ReadOnlyMemory<byte> valueData,
        bool isValueNull,
        Header[]? pooledHeaders,
        int pooledHeaderCount,
        in RecordHeaderRoutingLookup headerRouting,
        long timestampMs,
        TimestampType timestampType,
        int? leaderEpoch,
        ref PreparedDeserializerKey? preparedKey,
        out ConsumeResult<TKey, TValue> result)
        where TPreparedKeyMode : struct
    {
        var topic = pending.Topic;
        var materializedHeaders = _recordHeaderDeserializationHeaders;
        if (materializedHeaders is not null)
            headerRouting.CopyTo(materializedHeaders);
        TKey? key = default;
        if (!isKeyNull)
        {
            if (typeof(TPreparedKeyMode) == typeof(RetainedKeyMode))
            {
                key = preparedKey!.Value;
            }
            else
            {
                var keyContext = new SerializationContext
                {
                    Topic = topic,
                    Component = SerializationComponent.Key,
                    Headers = headerRouting.KeyRequiresMaterializedHeaders
                        ? materializedHeaders
                        : null,
                    KeyData = ReadOnlyMemory<byte>.Empty,
                    IsNull = false
                };
                try
                {
                    if (_keyDeserializerPreparer is { } keyPreparer)
                    {
                        if (!TryDeserializePrepared(
                                keyPreparer,
                                keyData,
                                keyContext,
                                in headerRouting,
                                out key))
                        {
                            result = default!;
                            return false;
                        }
                    }
                    else
                    {
                        key = RecordHeaderDeserializer.Deserialize(
                            _keyDeserializer,
                            keyData,
                            keyContext,
                            in headerRouting);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw ConsumeResult<TKey, TValue>.CreateDeserializationException(
                        DeserializationExceptionOrigin.Key,
                        topic,
                        pending.PartitionIndex,
                        offset,
                        timestampMs,
                        timestampType,
                        keyData,
                        isKeyNull,
                        valueData,
                        isValueNull,
                        headers: null,
                        pooledHeaders,
                        pooledHeaderCount,
                        ex);
                }
            }
        }

        var valueContext = new SerializationContext
        {
            Topic = topic,
            Component = SerializationComponent.Value,
            Headers = headerRouting.ValueRequiresMaterializedHeaders
                ? materializedHeaders
                : null,
            KeyData = SerializationContext.NormalizeKeyData(keyData, isKeyNull),
            IsNull = isValueNull
        };
        var valueBytes = isValueNull ? ReadOnlyMemory<byte>.Empty : valueData;
        TValue value;
        try
        {
            if (_valueDeserializerPreparer is { } valuePreparer)
            {
                if (!TryDeserializePrepared(
                        valuePreparer,
                        valueBytes,
                        valueContext,
                        in headerRouting,
                        out value))
                {
                    if (!isKeyNull && typeof(TPreparedKeyMode) == typeof(DeserializeKeyMode))
                        preparedKey = new PreparedDeserializerKey(pending, offset, key);

                    result = default!;
                    return false;
                }
            }
            else
            {
                value = RecordHeaderDeserializer.Deserialize(
                    _valueDeserializer,
                    valueBytes,
                    valueContext,
                    in headerRouting);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw ConsumeResult<TKey, TValue>.CreateDeserializationException(
                DeserializationExceptionOrigin.Value,
                topic,
                pending.PartitionIndex,
                offset,
                timestampMs,
                timestampType,
                keyData,
                isKeyNull,
                valueData,
                isValueNull,
                headers: null,
                pooledHeaders,
                pooledHeaderCount,
                ex);
        }

        result = new ConsumeResult<TKey, TValue>(
            topic,
            pending.PartitionIndex,
            offset,
            key,
            value,
            pooledHeaders,
            pooledHeaderCount,
            pending,
            timestampMs,
            timestampType,
            leaderEpoch,
            isKeyNull);
        return true;
    }

    private SerializationComponent GetRequiredPreparationComponent(
        PendingFetchData pending,
        long offset,
        bool isKeyNull,
        PreparedDeserializerKey? preparedKey) =>
        preparedKey?.Matches(pending, offset) == true || isKeyNull || _keyDeserializerPreparer is null
            ? SerializationComponent.Value
            : SerializationComponent.Key;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryDeserializePrepared<T>(
        IAsyncDeserializerPreparer<T> preparer,
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        in RecordHeaderRoutingLookup headers,
        out T value) =>
        preparer is IRecordHeaderAsyncDeserializerPreparer<T> headerPreparer
            ? headerPreparer.TryDeserialize(data, context, in headers, out value)
            : preparer.TryDeserialize(data, context, out value);

    private static ValueTask PrepareDeserializerAsync<T>(
        IAsyncDeserializerPreparer<T> preparer,
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        RecordHeaderRoutingLookup headers,
        CancellationToken cancellationToken) =>
        preparer is IRecordHeaderAsyncDeserializerPreparer<T> headerPreparer
            ? headerPreparer.PrepareAsync(data, context, headers, cancellationToken)
            : preparer.PrepareAsync(data, context, cancellationToken);

    private static void ReserveDeserializerPreparationAttempt(
        SerializationComponent component,
        ref int keyPreparationAttempts,
        ref int valuePreparationAttempts)
    {
        ref var attempts = ref (component == SerializationComponent.Key
            ? ref keyPreparationAttempts
            : ref valuePreparationAttempts);
        if (attempts >= MaxDeserializerPreparationAttemptsPerComponent)
        {
            throw new InvalidOperationException(
                "Deserializer remained unprepared after PrepareAsync completed.");
        }

        attempts++;
    }

    private async ValueTask PrepareRecordDeserializerAsync(
        PendingFetchData pending,
        long offset,
        ReadOnlyMemory<byte> keyData,
        bool isKeyNull,
        ReadOnlyMemory<byte> valueData,
        bool isValueNull,
        Header[]? pooledHeaders,
        int pooledHeaderCount,
        long timestampMs,
        TimestampType timestampType,
        RecordHeaderRoutingLookup headerRouting,
        SerializationComponent component,
        CancellationToken cancellationToken)
    {
        if (component == SerializationComponent.Key)
        {
            var keyPreparer = _keyDeserializerPreparer
                ?? throw new InvalidOperationException("Key deserializer does not support preparation.");
            var keyContext = new SerializationContext
            {
                Topic = pending.Topic,
                Component = SerializationComponent.Key,
                KeyData = ReadOnlyMemory<byte>.Empty,
                IsNull = false
            };
            try
            {
                await PrepareDeserializerAsync(
                        keyPreparer,
                        keyData,
                        keyContext,
                        headerRouting,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw ConsumeResult<TKey, TValue>.CreateDeserializationException(
                    DeserializationExceptionOrigin.Key,
                    pending.Topic,
                    pending.PartitionIndex,
                    offset,
                    timestampMs,
                    timestampType,
                    keyData,
                    isKeyNull,
                    valueData,
                    isValueNull,
                    headers: null,
                    pooledHeaders,
                    pooledHeaderCount,
                    ex);
            }

            return;
        }

        var valuePreparer = _valueDeserializerPreparer
            ?? throw new InvalidOperationException("Value deserializer does not support preparation.");

        var valueContext = new SerializationContext
        {
            Topic = pending.Topic,
            Component = SerializationComponent.Value,
            KeyData = SerializationContext.NormalizeKeyData(keyData, isKeyNull),
            IsNull = isValueNull
        };
        try
        {
            await PrepareDeserializerAsync(
                    valuePreparer,
                    isValueNull ? ReadOnlyMemory<byte>.Empty : valueData,
                    valueContext,
                    headerRouting,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw ConsumeResult<TKey, TValue>.CreateDeserializationException(
                DeserializationExceptionOrigin.Value,
                pending.Topic,
                pending.PartitionIndex,
                offset,
                timestampMs,
                timestampType,
                keyData,
                isKeyNull,
                valueData,
                isValueNull,
                headers: null,
                pooledHeaders,
                pooledHeaderCount,
                ex);
        }
    }

#if NET
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
#endif
    private async ValueTask<ConsumeResult<TKey, TValue>> CreateResultWithAsyncDeserializationAsync(
        PendingFetchData pending,
        long offset,
        ReadOnlyMemory<byte> keyData,
        bool isKeyNull,
        ReadOnlyMemory<byte> valueData,
        bool isValueNull,
        Header[]? pooledHeaders,
        int pooledHeaderCount,
        RecordHeaderRoutingLookup headerRouting,
        long timestampMs,
        TimestampType timestampType,
        int? leaderEpoch,
        CancellationToken cancellationToken)
    {
        var topic = pending.Topic;
        var partition = pending.PartitionIndex;
        var keyUsesRecordHeaders = _asyncKeyDeserializer is not null
            ? _asyncKeyUsesRecordHeaders
            : headerRouting.KeyRequiresMaterializedHeaders;
        var valueUsesRecordHeaders = _asyncValueDeserializer is not null
            ? _asyncValueUsesRecordHeaders
            : headerRouting.ValueRequiresMaterializedHeaders;
        var materializedHeaders = keyUsesRecordHeaders || valueUsesRecordHeaders
            ? _asyncDeserializationHeaders!
            : null;
        if (materializedHeaders is not null)
            headerRouting.CopyTo(materializedHeaders);

        TKey? key = default;
        if (!isKeyNull)
        {
            var keyContext = new SerializationContext
            {
                Topic = topic,
                Component = SerializationComponent.Key,
                Headers = keyUsesRecordHeaders ? materializedHeaders : null,
                KeyData = ReadOnlyMemory<byte>.Empty,
                IsNull = false
            };
            try
            {
                if (_asyncKeyDeserializer is not null)
                {
                    key = await _asyncKeyDeserializer.DeserializeAsync(
                            keyData,
                            keyContext,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                else if (_keyDeserializerPreparer is { } keyPreparer)
                {
                    for (var preparationAttempt = 0;
                         !TryDeserializePrepared(
                            keyPreparer,
                            keyData,
                            keyContext,
                            in headerRouting,
                            out key);
                         preparationAttempt++)
                    {
                        if (preparationAttempt >= 2)
                        {
                            throw new InvalidOperationException(
                                "Deserializer remained unprepared after PrepareAsync completed.");
                        }

                        await PrepareDeserializerAsync(
                                keyPreparer,
                                keyData,
                                keyContext,
                                headerRouting,
                                cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
                else
                {
                    key = RecordHeaderDeserializer.Deserialize(
                        _keyDeserializer,
                        keyData,
                        keyContext,
                        in headerRouting);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new RecordDeserializationException(
                    DeserializationExceptionOrigin.Key,
                    new TopicPartition(topic, partition),
                    offset,
                    timestampMs,
                    timestampType,
                    keyData,
                    isKeyNull,
                    valueData,
                    isValueNull,
                    pooledHeaders,
                    pooledHeaderCount,
                    ex);
            }
        }

        var valueContext = new SerializationContext
        {
            Topic = topic,
            Component = SerializationComponent.Value,
            Headers = valueUsesRecordHeaders ? materializedHeaders : null,
            KeyData = SerializationContext.NormalizeKeyData(keyData, isKeyNull),
            IsNull = isValueNull
        };
        var valueBytes = isValueNull ? ReadOnlyMemory<byte>.Empty : valueData;
        TValue value;
        try
        {
            if (_asyncValueDeserializer is not null)
            {
                value = await _asyncValueDeserializer.DeserializeAsync(
                        valueBytes,
                        valueContext,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (_valueDeserializerPreparer is { } valuePreparer)
            {
                for (var preparationAttempt = 0;
                     !TryDeserializePrepared(
                        valuePreparer,
                        valueBytes,
                        valueContext,
                        in headerRouting,
                        out value);
                     preparationAttempt++)
                {
                    if (preparationAttempt >= 2)
                    {
                        throw new InvalidOperationException(
                            "Deserializer remained unprepared after PrepareAsync completed.");
                    }

                    await PrepareDeserializerAsync(
                            valuePreparer,
                            valueBytes,
                            valueContext,
                            headerRouting,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            else
            {
                value = RecordHeaderDeserializer.Deserialize(
                    _valueDeserializer,
                    valueBytes,
                    valueContext,
                    in headerRouting);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new RecordDeserializationException(
                DeserializationExceptionOrigin.Value,
                new TopicPartition(topic, partition),
                offset,
                timestampMs,
                timestampType,
                keyData,
                isKeyNull,
                valueData,
                isValueNull,
                pooledHeaders,
                pooledHeaderCount,
                ex);
        }

        return new ConsumeResult<TKey, TValue>(
            topic,
            partition,
            offset,
            key,
            value,
            pooledHeaders,
            pooledHeaderCount,
            pending,
            timestampMs,
            timestampType,
            leaderEpoch,
            isKeyNull);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private System.Diagnostics.Activity? StartProtectedConsumeOneActivity(
        PendingFetchData pending,
        Header[]? pooledHeaders,
        int pooledHeaderCount,
        long offset,
        bool isValueNull,
        out bool pendingDisposed)
    {
        BeginConsumeOneFetchUse(pending);
        try
        {
            var headers = LazyConsumeHeaders.Create(
                pooledHeaders,
                pooledHeaderCount,
                pending,
                pending.HeaderGeneration);
            return StartConsumeActivity(
                pending,
                headers,
                offset,
                isValueNull,
                isProcessSpan: false);
        }
        finally
        {
            pendingDisposed = EndConsumeOneFetchUse(pending);
        }
    }

    /// <summary>
    /// Discards records following a deserializer or consume-interceptor failure and resumes
    /// the partition at the failed record. Otherwise a later consume could prove a higher
    /// offset from the same fetch and commit past the record that was never delivered.
    /// </summary>
    private void RewindAfterDeliveryFailure(PendingFetchData pending, long failedOffset)
    {
        var partition = pending.TopicPartition;

        // Keep fetch invalidation and the replacement position atomic with background
        // prefetch publication, matching Seek's ordering guarantee.
        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
        {
            // Preserve the successfully processed prefix before disposing this fetch.
            FlushConsumedPositions(pending);
            ClearFetchBufferForPartitions([partition]);
            _positions[partition] = failedOffset;
            SetFetchPosition(partition, failedOffset);
            _eofEmitted.TryRemove(partition, out _);
        }
    }

    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        var coordinator = GetCommitCoordinator();
        using var apiTimeout = new ApiTimeoutScope(_options.DefaultApiTimeoutMs, cancellationToken);
        try
        {
            // The partitions the consumer gave up when it ended its group membership (cleared at
            // once, e.g. from OnPartitionsRevoked of the leave): commit what they held then,
            // under the membership that is leaving.
            await CommitDepartingOffsetsAsync(
                    vouched: true,
                    partitions: null,
                    retryUntilApiTimeout: true,
                    apiTimeout.Token)
                .ConfigureAwait(false);

            // From a revoke callback of the leave: the departing offsets are all it commits. The
            // consumer's live offsets belong to its manual assignment, committed by commits made
            // outside the callback, after the leave, without the departing member's identity.
            if (coordinator.IsInsideLeaveRevocation)
            {
                // The leave stopped waiting for this callback and completed: its offsets were
                // discarded with the membership, whose partitions may have moved on.
                if (!coordinator.IsLeaveInProgress)
                    LogLeaveCallbackCommitAfterLeave(_options.RebalanceTimeoutMs);
                return;
            }

            // The consumer's own offsets are committed after a leave in progress, without the
            // departing member's identity.
            await coordinator.WaitForLeaveBeforeCommitAsync(apiTimeout.Token).ConfigureAwait(false);

            // Read before staging: offsets staged under a membership that a fence and rejoin
            // replace before the send are rejected rather than sent under the new member's identity.
            var membershipVersion = coordinator.MembershipVersion;
            StageExplicitCommitOffsets();

            await CommitStoredOffsetsAsync(
                    partitions: null,
                    apiTimeout.Token,
                    retryUntilApiTimeout: true,
                    membershipVersion)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (apiTimeout.DefaultTimeoutExpired)
        {
            throw apiTimeout.CreateTimeoutException(nameof(CommitAsync), ex);
        }
    }

    /// <summary>
    /// Close-scope commit: stages offsets already proven processed but not yet flushed
    /// (e.g. mid-fetch ConsumeOne usage) without vouching for the in-doubt last yielded
    /// record the way an explicit <see cref="CommitAsync(CancellationToken)"/> does — the
    /// consumer may be closing precisely because that record's processing failed.
    /// The background auto-commit loop uses a third, narrower scope: already-staged
    /// offsets only, via <see cref="CommitStoredOffsetsAsync(CancellationToken)"/> directly.
    /// </summary>
    private async ValueTask<bool> CommitProvenOffsetsAsync(CancellationToken cancellationToken)
    {
        if (_pendingFetches.Count > 0)
        {
            FlushConsumedPositions(_pendingFetches.Peek());
        }

        return await CommitStoredOffsetsAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private ValueTask<bool> CommitStoredOffsetsAsync(CancellationToken cancellationToken) =>
        CommitStoredOffsetsAsync(partitions: null, cancellationToken);

    private async ValueTask<bool> CommitStoredOffsetsAsync(
        TopicPartitionSet? partitions,
        CancellationToken cancellationToken,
        bool retryUntilApiTimeout = false,
        int? membershipVersion = null)
    {
        if (_coordinator is null)
            return false;

        // Auto-commit and close commits of the consumer's own offsets run after a leave in
        // progress (see WaitForLeaveBeforeCommitAsync); a revocation's commit does not wait.
        if (membershipVersion is null)
            await _coordinator.WaitForLeaveBeforeCommitAsync(cancellationToken).ConfigureAwait(false);

        TopicPartitionOffset[]? offsetsArray = null;
        int offsetCount;

        // Read before the snapshot: offsets taken under a membership that a fence and rejoin
        // replace before the send are rejected rather than sent under the new member's identity.
        var commitMembershipVersion = membershipVersion ?? _coordinator.MembershipVersion;
        var pinsOwnershipEpoch = partitions is null && IsGroupManagedSubscription();

        for (var attempt = 1; ; attempt++)
        {
            // Read before ownership is decided and sent with the commit: a revocation completed
            // between the decision and the send changes the member epoch, so the broker rejects
            // the commit instead of applying a revoked partition's offset over its new owner's.
            int? ownershipMemberEpoch = pinsOwnershipEpoch ? _coordinator.GenerationId : null;

            // Commit only offsets that changed since the last successful commit.
            // Snapshot the concurrent dictionary to avoid race conditions during enumeration
            List<StoredOffsetSnapshot> dirtyOffsetsSnapshot;
            if (partitions is null && IsGroupManagedSubscription())
            {
                // Snapshot and decide ownership under the gate assignment sync publishes and cleans
                // up under, so the assignment, positions and ownership starts read are consistent.
                // Commit path only.
                lock (_snapshotStateGate)
                    dirtyOffsetsSnapshot = SnapshotCommittableStoredOffsets(partitions);
            }
            else
            {
                dirtyOffsetsSnapshot = SnapshotCommittableStoredOffsets(partitions);
            }
            offsetCount = dirtyOffsetsSnapshot.Count;
            if (offsetCount == 0)
                return false;

            LogCommitStarted(offsetCount);

            // Rent array from pool to avoid List allocation
            offsetsArray = ArrayPool<TopicPartitionOffset>.Shared.Rent(offsetCount);
            try
            {
                int index = 0;
                foreach (var stored in dirtyOffsetsSnapshot)
                {
                    offsetsArray[index++] = new TopicPartitionOffset(
                        stored.Partition.Topic,
                        stored.Partition.Partition,
                        stored.Offset,
                        stored.LeaderEpoch);
                }

                // Create array segment to pass only the used portion
                var offsets = new ArraySegment<TopicPartitionOffset>(offsetsArray, 0, offsetCount);

                try
                {
                    await _coordinator.CommitOffsetsAsync(
                            offsets,
                            retryUntilApiTimeout,
                            commitMembershipVersion,
                            cancellationToken,
                            ownershipMemberEpoch)
                        .ConfigureAwait(false);
                }
                catch (Errors.GroupException ex) when (ownershipMemberEpoch is { } staleEpoch
                                                     && ex.ErrorCode == ErrorCode.StaleMemberEpoch)
                {
                    if (attempt >= MaxOwnershipEpochCommitAttempts)
                    {
                        throw new Errors.GroupException(ErrorCode.StaleMemberEpoch, ex.Message, isRetriable: true)
                        {
                            GroupId = ex.GroupId
                        };
                    }

                    // Decide ownership again under the refreshed epoch.
                    await _coordinator.WaitForCommitMemberEpochRefreshAsync(staleEpoch, cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                // Update committed offsets tracking
                foreach (var stored in dirtyOffsetsSnapshot)
                    MarkOffsetCommitted(stored.Partition, stored.Ownership, stored.Offset);

                // Invoke OnCommit interceptors - wrap array as ArraySegment to avoid Span→Array copy
                InvokeOnCommitInterceptors(new ArraySegment<TopicPartitionOffset>(offsetsArray, 0, offsetCount));
            }
            finally
            {
                ArrayPool<TopicPartitionOffset>.Shared.Return(offsetsArray);
            }

            return true;
        }
    }

    // A commit rejected because the member's assignment changed after ownership was decided is
    // decided and sent again, at most this many times in all.
    private const int MaxOwnershipEpochCommitAttempts = 3;

    private bool IsGroupManagedSubscription() => _isGroupManaged;

    /// <summary>
    /// A group-managed consumer commits stored offsets only for partitions it owns, as Java's
    /// <c>commitSync()</c> commits only assigned partitions' positions. Seek and StoreOffset do not
    /// check ownership and run concurrently with assignment sync, so an offset stored for a
    /// partition after sync revoked it (or before sync initialized its new ownership) would
    /// otherwise be committed over the progress of the member that owns it now. An owned
    /// partition has an initialized fetch position; position initialization replaces anything
    /// stored before it. Offsets stored for unowned partitions are skipped. Commit path only.
    /// </summary>
    private List<StoredOffsetSnapshot> SnapshotCommittableStoredOffsets(TopicPartitionSet? partitions)
    {
        // One pass, one ownership evaluation per offset: assignment sync changes fetch positions
        // concurrently, so a second evaluation could disagree with the first.
        var groupManaged = IsGroupManagedSubscription();
        var assignment = _assignmentSnapshot;
        var committable = new List<StoredOffsetSnapshot>();
        foreach (var (partition, slot) in _storedOffsetSlots)
        {
            if (partitions is not null && !partitions.Contains(partition))
                continue;
            if (!slot.TryReadDirty(out var ownership, out var offset, out var leaderEpoch))
                continue;

            // Stored under an ended ownership (or while unassigned): it never commits, and the slot
            // is marked clean unless a newer store landed meanwhile.
            if (ownership != GetCurrentStoreOwnership(partition))
            {
                LogStoredOffsetOfUnownedPartitionNotCommitted(partition.Topic, partition.Partition, offset);
                slot.ClearIfUnchanged(ownership, offset);
                continue;
            }

            // The revocation commit (explicit partitions) runs while the partitions are still owned.
            var isOwned = partitions is not null || !groupManaged || IsOwnedForCommit(assignment, partition);
            AfterStoredOffsetOwnershipEvaluatedForTest?.Invoke(this);
            if (!isOwned)
            {
                // Pending revocation or position not initialized yet: stays for later.
                LogStoredOffsetOfUnownedPartitionNotCommitted(partition.Topic, partition.Partition, offset);
                continue;
            }

            committable.Add(new StoredOffsetSnapshot(partition, ownership, offset, leaderEpoch));
        }

        return committable;
    }

    private readonly record struct StoredOffsetSnapshot(TopicPartition Partition, long Ownership, long Offset, int LeaderEpoch);

    // A partition whose revocation awaits sync is committed by the revocation commit only, which
    // passes its partitions explicitly: a later commit could follow it with an offset stored
    // after the revocation, over the progress of the partition's next owner.
    private bool IsOwnedForCommit(HashSet<TopicPartition> assignment, TopicPartition partition)
        => assignment.Contains(partition)
            && _fetchPositions.ContainsKey(partition)
            && !IsRevocationPending(partition);

    private async ValueTask CommitRevokedOffsetsAsync(
        IReadOnlyList<TopicPartition> partitions,
        CancellationToken cancellationToken)
    {
        if (_options.OffsetCommitMode != OffsetCommitMode.Auto)
            return;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.RebalanceTimeoutMs);

        await CommitRevokedOffsetsWithTimeoutAsync(partitions, cancellationToken, timeout.Token)
            .ConfigureAwait(false);
    }

    private async ValueTask CommitRevokedOffsetsWithTimeoutAsync(
        IReadOnlyList<TopicPartition> partitions,
        CancellationToken cancellationToken,
        CancellationToken commitCancellationToken)
    {
        try
        {
            // Partitions the consumer cleared when the application ended its group membership
            // (this is the leave's revocation, or one a heartbeat published before it): commit
            // the processed offsets they held then.
            var committed = await CommitDepartingOffsetsAsync(
                    vouched: false,
                    partitions,
                    retryUntilApiTimeout: false,
                    commitCancellationToken)
                .ConfigureAwait(false);

            // During a leave the departing offsets are all this membership has to commit: the
            // live stored offsets now belong to the consumer's manual assignment (which may
            // retain these partitions) and must not be sent under the departing member.
            if (GetCommitCoordinator().IsLeaveInProgress)
            {
                if (committed)
                    LogCommittedRevokedOffsets();
                return;
            }

            // The revoked membership's own commit: it never waits for a leave (the version is
            // passed), and is rejected if that membership has ended.
            var revoked = partitions.ToHashSet();
            if (await CommitStoredOffsetsAsync(
                        revoked,
                        commitCancellationToken,
                        membershipVersion: GetCommitCoordinator().MembershipVersion)
                    .ConfigureAwait(false)
                || committed)
                LogCommittedRevokedOffsets();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogCommitRevokedOffsetsTimedOut(_options.RebalanceTimeoutMs);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogCommitRevokedOffsetsFailed(ex);
        }
    }

    public async ValueTask CommitAsync(IEnumerable<TopicPartitionOffset> offsets, CancellationToken cancellationToken = default)
    {
        var coordinator = GetCommitCoordinator();
        using var apiTimeout = new ApiTimeoutScope(_options.DefaultApiTimeoutMs, cancellationToken);
        try
        {
            // From a revoke callback the leave stopped waiting for: the membership these offsets
            // belong to has ended and its partitions may already be another member's.
            if (coordinator.IsInsideLeaveRevocation && !coordinator.IsLeaveInProgress)
            {
                LogLeaveCallbackCommitAfterLeave(_options.RebalanceTimeoutMs);
                return;
            }

            // After a leave in progress (a switch to manual assignment): these offsets are not the
            // departing member's to commit, and its identity is about to be reset.
            await coordinator.WaitForLeaveBeforeCommitAsync(apiTimeout.Token).ConfigureAwait(false);

            // Read before the offsets are enumerated: offsets produced under a membership that a
            // fence and rejoin replace before the send are rejected rather than sent under the new
            // member's identity.
            var membershipVersion = coordinator.MembershipVersion;

            // Materialize to list to allow iteration for both commit tracking and interceptors
            var offsetsList = offsets as IReadOnlyList<TopicPartitionOffset> ?? offsets.ToArray();

            await coordinator.CommitOffsetsAsync(offsetsList, retryUntilApiTimeout: true, membershipVersion, apiTimeout.Token)
                .ConfigureAwait(false);

            foreach (var offset in offsetsList)
            {
                var partition = new TopicPartition(offset.Topic, offset.Partition);
                MarkOffsetCommitted(partition, offset.Offset);
            }

            // Invoke OnCommit interceptors
            InvokeOnCommitInterceptors(offsetsList);
        }
        catch (OperationCanceledException ex) when (apiTimeout.DefaultTimeoutExpired)
        {
            throw apiTimeout.CreateTimeoutException(nameof(CommitAsync), ex);
        }
    }

    private ConsumerCoordinator GetCommitCoordinator() => _coordinator
        ?? throw new InvalidOperationException(
            "Offset commits require a consumer group. Configure one with ConsumerBuilder.WithGroupId(...).");

    /// <summary>
    /// Explicit commit staging: the caller vouches for everything yielded so far, including
    /// a record still being processed.
    /// </summary>
    private void StageExplicitCommitOffsets()
    {
        FlushActiveConsumedPosition();
        FlushPausedConsumedPositions();
    }

    public void StoreOffset(ConsumeResult<TKey, TValue> result)
    {
        if (result.IsPartitionEof)
            return;

        var partition = new TopicPartition(result.Topic, result.Partition);
        long ownership = 0;
        if (IsGroupManagedSubscription() && _coordinator is { } coordinator)
        {
            // The store is tagged with the ownership the check found the record in: if a rebalance
            // ends that ownership before the write lands, it can never be committed.
            var fetchGeneration = result.ResolveFetchGeneration(out var fetchOwnershipStart);
            if (fetchOwnershipStart != 0 && IsFetchedUnderCurrentSynchronizedOwnership(coordinator, fetchGeneration))
            {
                ownership = fetchOwnershipStart;
            }
            else if (!TryGetOwnershipForStore(partition, fetchGeneration, out ownership))
            {
                LogStoredOffsetOfEndedOwnershipIgnored(partition.Topic, partition.Partition, result.Offset);
                return;
            }

            AfterStoreOffsetOwnershipCheckedForTest?.Invoke(this);
        }

        StoreOffsetCore(partition, ownership, checked(result.Offset + 1), result.LeaderEpoch ?? -1);
    }

    /// <summary>
    /// The ownership a record that the fast path could not decide belongs to, when it is Owned:
    /// one lookup of the partition's ownership start.
    /// </summary>
    private bool TryGetOwnershipForStore(TopicPartition partition, long fetchGeneration, out long ownership)
        => _ownershipStartGenerations.TryGetValue(partition, out ownership)
           && (fetchGeneration == 0 || fetchGeneration >= ownership)
           && !IsRevocationPending(partition);

    /// <summary>
    /// Steady-state fast path of the ownership check (four volatile reads): no revocation is
    /// pending, the record was fetched after the latest ownership start, end or revocation, so its
    /// partition is still assigned under the same ownership, and the coordinator has changed
    /// nothing since the last assignment sync.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsFetchedUnderCurrentSynchronizedOwnership(ConsumerCoordinator coordinator, long fetchGeneration)
        => fetchGeneration != 0
            && ReferenceEquals(_pendingRevocations, s_noPendingRevocations)
            && fetchGeneration > PendingFetchData.ReadGeneration(ref _latestOwnershipChangeFetchGeneration)
            && coordinator.AssignmentVersion == Volatile.Read(ref _lastCoordinatorAssignmentVersion);

    RecordOwnership IConsumerRecordOwnership<TKey, TValue>.RewindIfOwned(
        in ConsumeResult<TKey, TValue> result,
        TopicPartitionOffset rewindTo,
        bool whileRevocationPending)
    {
        var partition = new TopicPartition(result.Topic, result.Partition);
        // Decided and applied under the gate assignment sync publishes and cleans up under: an
        // ownership change cannot fall between the check and the pause and seek. Failure paths only.
        lock (_snapshotStateGate)
        {
            var ownership = GetRecordOwnership(partition, result.ResolveFetchGeneration());
            if (ownership == RecordOwnership.Ended
                || ownership == RecordOwnership.RevocationPending && !whileRevocationPending)
            {
                return ownership;
            }

            Pause(partition);
            Seek(rewindTo);
            return ownership;
        }
    }

    RecordOwnership IConsumerRecordOwnership<TKey, TValue>.GetRecordOwnership(in ConsumeResult<TKey, TValue> result)
    {
        var fetchGeneration = result.ResolveFetchGeneration();
        if (_coordinator is { } coordinator
            && IsFetchedUnderCurrentSynchronizedOwnership(coordinator, fetchGeneration))
        {
            return RecordOwnership.Owned;
        }

        return GetRecordOwnership(new TopicPartition(result.Topic, result.Partition), fetchGeneration);
    }

    /// <summary>
    /// Ownership of a delivered record. <list type="bullet">
    /// <item>Ended: the partition is unassigned, or the record was fetched before the partition's
    /// current ownership began (revoked or lost and assigned again, ABA). A record with fetch
    /// generation 0 was not delivered from a fetch (a constructed result); only the assignment
    /// decides for it.</item>
    /// <item>RevocationPending: the coordinator revoked or lost the partition (published before
    /// any revocation callback runs), and no assignment sync that drained that revocation has
    /// started the partition's next ownership or removed it yet. This holds while the coordinator
    /// already assigned the partition again and its revocation queue was drained.</item>
    /// </list>
    /// </summary>
    private RecordOwnership GetRecordOwnership(TopicPartition partition, long fetchGeneration)
    {
        // Absent when unassigned.
        if (!_ownershipStartGenerations.TryGetValue(partition, out var ownershipStart)
            || fetchGeneration != 0 && fetchGeneration < ownershipStart)
        {
            return RecordOwnership.Ended;
        }

        return IsRevocationPending(partition)
            ? RecordOwnership.RevocationPending
            : RecordOwnership.Owned;
    }

    private bool IsRevocationPending(TopicPartition partition)
        => _pendingRevocations.ContainsKey(partition);

    /// <summary>
    /// R: the coordinator published a revocation or loss of these partitions with
    /// <paramref name="generation"/>, before any revocation callback runs and before it enqueues
    /// the revocation for assignment sync.
    /// </summary>
    private void RecordCoordinatorRevocation(IReadOnlyList<TopicPartition> partitions, long generation)
    {
        // Rebalance path only: one copy of the (small) pending map per revocation.
        lock (_pendingRevocationsLock)
        {
            var pending = new Dictionary<TopicPartition, long>(_pendingRevocations);
            foreach (var partition in partitions)
            {
                // Raised, never lowered: a concurrent later revocation wins.
                if (!pending.TryGetValue(partition, out var current) || current < generation)
                    pending[partition] = generation;
            }

            // Before the map: records fetched before this R stop taking the fast path no later.
            WriteLatestOwnershipChange(generation);
            BeforePendingRevocationsPublishedForTest?.Invoke(this);
            _pendingRevocations = pending;
        }
    }

    /// <summary>S: records fetched before <paramref name="generation"/> belong to an earlier ownership.</summary>
    private void StartOwnership(TopicPartition partition, long generation)
    {
        _ownershipStartGenerations[partition] = generation;
        WriteLatestOwnershipChange(generation);
    }

    private void EndOwnership(TopicPartition partition, long generation)
    {
        _ownershipStartGenerations.TryRemove(partition, out _);
        WriteLatestOwnershipChange(generation);
    }

    /// <summary>
    /// The sync publication applied these drained revocations: forgets each pending revocation
    /// whose generation is not after the drained entry's. A later revocation stays pending.
    /// </summary>
    private void ForgetDrainedRevocations(IReadOnlyDictionary<TopicPartition, long> drained)
    {
        lock (_pendingRevocationsLock)
        {
            Dictionary<TopicPartition, long>? remaining = null;
            foreach (var (partition, drainedGeneration) in drained)
            {
                if (_pendingRevocations.TryGetValue(partition, out var pending) && pending <= drainedGeneration)
                    (remaining ??= new Dictionary<TopicPartition, long>(_pendingRevocations)).Remove(partition);
            }

            if (remaining is not null)
                _pendingRevocations = remaining.Count == 0 ? s_noPendingRevocations : remaining;
        }
    }

    private void WriteLatestOwnershipChange(long generation)
    {
        // Only moves forward: concurrent writers (sync, revocation hook) never lower it.
        var current = PendingFetchData.ReadGeneration(ref _latestOwnershipChangeFetchGeneration);
        while (current < generation)
        {
            var observed = Interlocked.CompareExchange(ref _latestOwnershipChangeFetchGeneration, generation, current);
            if (observed == current)
                return;
            current = observed;
        }
    }


    public void StoreOffset(TopicPartitionOffset offset)
    {
        TopicPartitionOffsetValidator.Validate(offset, nameof(offset));
        StoreOffsetCore(new TopicPartition(offset.Topic, offset.Partition), offset.Offset, offset.LeaderEpoch);
    }

    public void StoreOffsets<TOffsets>(TOffsets offsets)
        where TOffsets : IReadOnlyList<TopicPartitionOffset>
    {
        if (offsets is null)
            throw new ArgumentNullException(nameof(offsets));

        var count = offsets.Count;
        for (var index = 0; index < count; index++)
            TopicPartitionOffsetValidator.Validate(offsets[index], nameof(offsets));

        for (var index = 0; index < count; index++)
        {
            var offset = offsets[index];
            StoreOffsetCore(new TopicPartition(offset.Topic, offset.Partition), offset.Offset, offset.LeaderEpoch);
        }
    }

    public void StoreOffsets(ReadOnlySpan<TopicPartitionOffset> offsets)
    {
        for (var index = 0; index < offsets.Length; index++)
            TopicPartitionOffsetValidator.Validate(offsets[index], nameof(offsets));

        for (var index = 0; index < offsets.Length; index++)
        {
            ref readonly var offset = ref offsets[index];
            StoreOffsetCore(new TopicPartition(offset.Topic, offset.Partition), offset.Offset, offset.LeaderEpoch);
        }
    }

    public async ValueTask<long?> GetCommittedOffsetAsync(TopicPartition partition, CancellationToken cancellationToken = default)
    {
        if (_committed.TryGetValue(partition, out var cached))
            return cached.Offset;

        if (_coordinator is null)
            return null;

        var cacheGeneration = Interlocked.Increment(ref _committedOffsetGeneration);
        using var apiTimeout = new ApiTimeoutScope(_options.DefaultApiTimeoutMs, cancellationToken);
        try
        {
            var offsets = await _coordinator.FetchOffsetsAsync([partition], apiTimeout.Token).ConfigureAwait(false);
            if (offsets.TryGetValue(partition, out var committedOffset))
            {
                if (TryCacheCommittedOffset(partition, committedOffset.Offset, cacheGeneration))
                    TrySeedLeaderEpochFromCommittedLookup(partition, committedOffset.LeaderEpoch);
                return committedOffset.Offset;
            }
        }
        catch (OperationCanceledException ex) when (apiTimeout.DefaultTimeoutExpired)
        {
            throw apiTimeout.CreateTimeoutException(nameof(GetCommittedOffsetAsync), ex);
        }

        return null;
    }

    public async ValueTask<IReadOnlyDictionary<TopicPartition, TopicPartitionOffset>> GetCommittedOffsetsAsync(
        IReadOnlyCollection<TopicPartition> partitions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(partitions);

        if (partitions.Count == 0 || _coordinator is null)
            return new Dictionary<TopicPartition, TopicPartitionOffset>();

        var cacheGeneration = Interlocked.Increment(ref _committedOffsetGeneration);
        using var apiTimeout = new ApiTimeoutScope(_options.DefaultApiTimeoutMs, cancellationToken);
        try
        {
            var offsets = await _coordinator.FetchOffsetsAsync(partitions, apiTimeout.Token).ConfigureAwait(false);
            foreach (var partition in partitions)
            {
                if (offsets.TryGetValue(partition, out var committedOffset))
                {
                    if (TryCacheCommittedOffset(partition, committedOffset.Offset, cacheGeneration))
                        TrySeedLeaderEpochFromCommittedLookup(partition, committedOffset.LeaderEpoch);
                }
                else
                    RemoveCachedCommittedOffset(partition, cacheGeneration);
            }

            return offsets;
        }
        catch (OperationCanceledException ex) when (apiTimeout.DefaultTimeoutExpired)
        {
            throw apiTimeout.CreateTimeoutException(nameof(GetCommittedOffsetsAsync), ex);
        }
    }

    // Fetch candidates carry their request-start generation. A later commit has a newer
    // generation and wins the dictionary CAS regardless of continuation scheduling.
    private bool TryCacheCommittedOffset(
        TopicPartition partition,
        long committedOffset,
        long generation)
    {
        var candidate = new CommittedOffsetCacheEntry(committedOffset, generation);
        var published = _committed.AddOrUpdate(
            partition,
            static (_, candidate) => candidate,
            static (_, current, candidate) =>
                current.Generation > candidate.Generation ? current : candidate,
            candidate);
        return published.Generation == generation;
    }

    private void TrySeedLeaderEpochFromCommittedLookup(TopicPartition partition, int leaderEpoch)
    {
        if (leaderEpoch < 0 || _fetchPositions.ContainsKey(partition))
            return;

        _lastConsumedLeaderEpochs.TryAdd(partition, leaderEpoch);
    }

    private void RemoveCachedCommittedOffset(TopicPartition partition, long generation)
    {
        while (_committed.TryGetValue(partition, out var cached))
        {
            if (cached.Generation > generation ||
                _committed.TryRemove(new KeyValuePair<TopicPartition, CommittedOffsetCacheEntry>(partition, cached)))
            {
                return;
            }
        }
    }

    public long? GetPosition(TopicPartition partition)
    {
        if (_coordinator is { IsDeliveringAssignedCallback: true } coordinator
            && coordinator.TryGetAssignedCallbackRevocationSequence(partition, out _))
        {
            return GetAssignedCallbackPosition(coordinator, partition);
        }

        if (TryGetActiveConsumedPosition(partition, out var activePosition, out var leaderEpoch))
        {
            // Materialize the snapshot into _positions for consistency, but do NOT stage it
            // for commit: the position includes the record currently being processed, and a
            // read-only query must not make an unproven record committable.
            _positions[partition] = activePosition;
            SetLastConsumedLeaderEpoch(partition, leaderEpoch);
            return activePosition;
        }

        return _positions.TryGetValue(partition, out var position) ? position : null;
    }

    private long? GetPositionWithoutCaching(TopicPartition partition) =>
        TryGetActiveConsumedPosition(partition, out var activePosition, out _)
            ? activePosition
            : _positions.TryGetValue(partition, out var position) ? position : null;

    internal long? GetRebalancePosition(TopicPartition partition) =>
        _pendingRebalanceSeeks.TryGetValue(partition, out var pending)
            ? pending.Offset
            : GetPosition(partition);

    /// <summary>
    /// The position of a partition an OnPartitionsAssigned callback announced, read from inside that
    /// callback: the seek it staged, otherwise null until assignment sync initializes the position.
    /// A position held before then belongs to the previous ownership of a partition revoked or lost
    /// and assigned again, and the sync replaces it.
    /// </summary>
    private long? GetAssignedCallbackPosition(ConsumerCoordinator coordinator, TopicPartition partition)
    {
        if (_pendingRebalanceSeeks.TryGetValue(partition, out var pending))
            return pending.Offset;

        // A callback that polls synchronizes its own assignment (the poll does not wait for the
        // callback it runs in); the position is then the new ownership's.
        if (!_acknowledgedCoordinatorAssignment.Contains(partition)
            || !IsCoordinatorAssignmentSyncCurrent(coordinator, out _))
        {
            return null;
        }

        return GetPositionWithoutCaching(partition);
    }

    /// <param name="offset">The seek an OnPartitionsAssigned callback requested.</param>
    /// <param name="revocationSequence">
    /// The coordinator's revocation sequence when the callback's notification was published. A seek
    /// for a partition revoked or lost since then is discarded: that ownership has ended, and a
    /// later assignment of the partition must not start at it.
    /// </param>
    /// <param name="fromAssignedCallback">
    /// Called from an OnPartitionsAssigned callback's own flow: the callback must still be live once
    /// the assignment lock is held. An abandon (which takes that lock) may have ended its staging
    /// while this call waited for it; then nothing is staged and false is returned, so the caller
    /// seeks directly.
    /// </param>
    /// <returns>False only when <paramref name="fromAssignedCallback"/> and the callback is no longer live.</returns>
    internal bool StageRebalanceSeek(
        TopicPartitionOffset offset,
        long revocationSequence = long.MaxValue,
        bool fromAssignedCallback = false)
    {
        var partition = new TopicPartition(offset.Topic, offset.Partition);
        if (fromAssignedCallback)
            BeforeStageRebalanceSeekLockForTest?.Invoke();
        SemaphoreHelper.AcquireOrThrowDisposed(
            _assignmentLock,
            nameof(KafkaConsumer<TKey, TValue>));
        try
        {
            // The revocation hook drops pending seeks under this lock after the coordinator
            // records the revocation, so checking and staging under it cannot keep a stale seek.
            lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
            {
                // Rechecked immediately before the write, under the lock abandon holds.
                if (fromAssignedCallback
                    && _coordinator?.TryGetAssignedCallbackRevocationSequence(partition, out _) != true)
                {
                    return false;
                }

                if (_coordinator?.WasRevokedSince(partition, revocationSequence) == true)
                {
                    LogStaleRebalanceCallbackCallIgnored(nameof(Seek), partition.Topic, partition.Partition);
                    return true;
                }

                _pendingRebalanceSeeks[partition] = offset;
            }

            if (_coordinator is { } coordinator
                && _acknowledgedCoordinatorAssignment.Contains(partition)
                && IsCoordinatorAssignmentSyncCurrent(coordinator, out _))
            {
                ApplyPendingRebalanceSeek(partition);
            }

            return true;
        }
        finally
        {
            SemaphoreHelper.ReleaseSafely(_assignmentLock);
        }
    }

    // Runs when a callback's seek is about to wait for the assignment lock.
    internal Action? BeforeStageRebalanceSeekLockForTest { get; set; }

    private void EmitFetchMetrics(PendingFetchData fetch)
    {
        if (!fetch.TryConsumeMetricDelta(out var messageCount, out var bytesConsumed))
            return;

        if (!_metricTagsCache.TryGetValue(fetch.Topic, out var metricTags))
        {
            metricTags = Diagnostics.DekafDiagnostics.ClientMetricTags(
                Diagnostics.DekafDiagnostics.OperationNamePoll, fetch.Topic);
            _metricTagsCache[fetch.Topic] = metricTags;
        }
        Diagnostics.DekafMetrics.MessagesReceived.Add(messageCount, metricTags);
        Diagnostics.DekafMetrics.BytesReceived.Add(bytesConsumed, metricTags);
    }

    private void RecordFetchDuration(long fetchStarted, int brokerId)
    {
        if (!Diagnostics.DekafMetrics.FetchDuration.Enabled)
            return;

        Diagnostics.DekafMetrics.FetchDuration.Record(
            Stopwatch.GetElapsedTime(fetchStarted).TotalSeconds,
            GetFetchDurationMetricTags(brokerId));
    }

    private TagList GetFetchDurationMetricTags(int brokerId) =>
        _fetchDurationMetricTagsCache.GetOrAdd(
            brokerId,
            static id => new TagList { { Diagnostics.DekafDiagnostics.DekafBrokerId, id } });

    public void Seek(TopicPartitionOffset offset)
    {
        LogSeek(offset.Topic, offset.Partition, offset.Offset);
        if (_coordinator is { IsDeliveringAssignedCallback: true } coordinator
            && coordinator.TryGetAssignedCallbackRevocationSequence(
                new TopicPartition(offset.Topic, offset.Partition),
                out var revocationSequence))
        {
            // Called from OnPartitionsAssigned for a partition it announced: written now, the
            // position would be replaced when assignment sync initializes the partition. Unless the
            // assignment was abandoned meanwhile: then the seek applies directly, below.
            if (StageRebalanceSeek(offset, revocationSequence, fromAssignedCallback: true))
                return;
        }

        // Keep invalidation, buffer drain, and position replacement atomic with prefetch publication.
        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
        {
            ThrowIfSnapshotOperationActive();
            SeekLocked(offset);
        }
    }

    private void SeekLocked(TopicPartitionOffset offset)
    {
        var partition = new TopicPartition(offset.Topic, offset.Partition);
        ClearFetchBufferForPartitions([partition], stagePendingClear: true);

        if (offset.LeaderEpoch >= 0)
            SetLastConsumedLeaderEpoch(partition, offset.LeaderEpoch);
        else
            ClearLastConsumedLeaderEpoch(partition);
        SetPosition(partition, offset.Offset, dirty: true);
        SetFetchPosition(partition, offset.Offset);
        _eofEmitted.TryRemove(partition, out _);
    }

    private void ThrowIfSnapshotOperationActive()
    {
        if (Volatile.Read(ref _snapshotOperationActive) != 0)
        {
            throw new InvalidOperationException(
                "Cannot change consumer position while a snapshot enumeration is active.");
        }
    }

    public void SeekToBeginning(params TopicPartition[] partitions)
    {
        if (!TryStageAssignedCallbackSeeks(ref partitions, offset: 0))
            return;

        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
        {
            ThrowIfSnapshotOperationActive();
            ClearFetchBufferForPartitions(partitions, stagePendingClear: true);

            foreach (var partition in partitions)
            {
                ClearLastConsumedLeaderEpoch(partition);
                SetPosition(partition, 0, dirty: true);
                SetFetchPosition(partition, 0);
                _eofEmitted.TryRemove(partition, out _);
            }
        }
    }

    public void SeekToEnd(params TopicPartition[] partitions)
    {
        if (!TryStageAssignedCallbackSeeks(ref partitions, offset: -1))
            return;

        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
        {
            ThrowIfSnapshotOperationActive();
            ClearFetchBufferForPartitions(partitions, stagePendingClear: true);

            foreach (var partition in partitions)
            {
                ClearLastConsumedLeaderEpoch(partition);
                SetPosition(partition, -1, dirty: true); // Special value meaning end
                SetFetchPosition(partition, -1); // Special value meaning end
                _eofEmitted.TryRemove(partition, out _);
            }
        }
    }

    /// <summary>
    /// Stages the seeks an OnPartitionsAssigned callback makes for partitions it announced (see
    /// <see cref="Seek"/>) and narrows <paramref name="partitions"/> to the rest. Returns false when
    /// it staged every partition. Allocates only when called from such a callback.
    /// </summary>
    private bool TryStageAssignedCallbackSeeks(ref TopicPartition[] partitions, long offset)
    {
        if (_coordinator is not { IsDeliveringAssignedCallback: true } coordinator || partitions is null)
            return true;

        List<TopicPartition>? remaining = null;
        for (var i = 0; i < partitions.Length; i++)
        {
            var partition = partitions[i];
            if (coordinator.TryGetAssignedCallbackRevocationSequence(partition, out var revocationSequence)
                && StageRebalanceSeek(
                    new TopicPartitionOffset(partition.Topic, partition.Partition, offset),
                    revocationSequence,
                    fromAssignedCallback: true))
            {
                if (remaining is null)
                {
                    remaining = new List<TopicPartition>(partitions.Length);
                    for (var j = 0; j < i; j++)
                        remaining.Add(partitions[j]);
                }
            }
            else
            {
                remaining?.Add(partition);
            }
        }

        if (remaining is null)
            return true;

        partitions = remaining.ToArray();
        return partitions.Length != 0;
    }

    /// <summary>
    /// Whether <paramref name="partition"/> is one an OnPartitionsAssigned callback running on this
    /// flow announced (<paramref name="isCallbackPartition"/>), and if so whether that ownership has
    /// already been revoked or lost (the return value): a stale callback's Pause, Resume and Seek
    /// for it are no-ops. Caller holds <c>_coordinatorRevokedPartitionsPendingFetchClearLock</c>,
    /// under which the revocation hook runs, so the answer holds until the caller releases it.
    /// </summary>
    private bool IsStaleAssignedCallbackPartitionLocked(TopicPartition partition, out bool isCallbackPartition)
    {
        if (_coordinator is not { IsDeliveringAssignedCallback: true } coordinator
            || !coordinator.TryGetAssignedCallbackRevocationSequence(partition, out var revocationSequence))
        {
            isCallbackPartition = false;
            return false;
        }

        isCallbackPartition = true;
        return coordinator.WasRevokedSince(partition, revocationSequence);
    }

    /// <summary>
    /// Drops what OnPartitionsAssigned callbacks left for an assignment the consumer abandons
    /// (unsubscribe, or a switch to manual assignment) before synchronizing it: every staged seek,
    /// and the pause of each partition such a callback paused that is not in
    /// <paramref name="synchronizedAssignment"/> (whose state the caller handles). A later
    /// subscription assigned the same partitions must not inherit them. Caller holds
    /// <c>_assignmentLock</c>, so no sync runs concurrently. Returns true if the paused set changed.
    /// </summary>
    private bool DiscardUnsynchronizedRebalanceState(HashSet<TopicPartition> synchronizedAssignment)
    {
        // A callback still running (this may be its own call) must not stage anything further.
        _coordinator?.EndAssignedCallbackStaging();
        _acknowledgedCoordinatorAssignment = [];
        var hadPaused = false;
        lock (_pauseStateLock)
        {
            lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
            {
                _pendingRebalanceSeeks.Clear();
                _unacknowledgedAppliedRebalanceSeeks.Clear();
                foreach (var entry in _rebalancePausedPartitions)
                {
                    _rebalancePausedPartitions.TryRemove(entry.Key, out _);

                    // A callback pause survives only when made under the acknowledged ownership:
                    // still current, and the partition not revoked since that acknowledgement.
                    if (!entry.Value
                        || !synchronizedAssignment.Contains(entry.Key)
                        || IsRevokedSinceAcknowledged(entry.Key))
                    {
                        hadPaused |= _paused.TryRemove(entry.Key, out _);
                    }
                }
            }
        }

        // The pause decisions above were the last use of the revocation bookkeeping for the
        // abandoned group assignment; a manual assignment never acknowledges a group sync, so drop
        // it here or it would only grow. Everything a sync drained is covered on both sides.
        ForgetRevocationSequences();

        return hadPaused;
    }

    /// <summary>
    /// Removes per-partition tracking state for the given partitions.
    /// Returns true if any partition was in the paused set.
    /// </summary>
    private bool RemovePartitionState(
        IEnumerable<TopicPartition> partitions,
        HashSet<TopicPartition>? retainSeeks = null)
    {
        var hadPaused = false;
        foreach (var partition in partitions)
        {
            var reassigned = retainSeeks is not null && retainSeeks.Contains(partition);
            ClearActiveConsumedPosition(partition);
            _positions.TryRemove(partition, out _);
            RemoveStoredOffset(partition);
            _fetchPositions.TryRemove(partition, out _);
            if (!reassigned)
                _pendingRebalanceSeeks.TryRemove(partition, out _);
            _minimumFetchBufferEpochsByPartition.TryRemove(partition, out _);
            _lastConsumedLeaderEpochs.TryRemove(partition, out _);
            _lastFetchedLeaderEpochs.TryRemove(partition, out _);
            _committed.TryRemove(partition, out _);
            _highWatermarks.TryRemove(partition, out _);
            // IConsumerOffsets exposes the last cached broker watermarks even while
            // unassigned. A later assignment removes them before position reuse.
            _eofEmitted.TryRemove(partition, out _);
            // The previous ownership's pause ends with it; a pause the new assignment's
            // OnPartitionsAssigned made is kept, like its staged seek. Under the pause lock, so a
            // concurrent Resume lands before the clear or after the restore, never between.
            lock (_pauseStateLock)
            {
                hadPaused |= _paused.TryRemove(partition, out _);
                AfterPartitionPauseClearedForTest?.Invoke(this, partition);

                // A reassigned partition keeps a current marker: a sync pass that is retried before
                // it is acknowledged repeats this cleanup and must restore the pause again.
                if (reassigned
                    && _rebalancePausedPartitions.TryGetValue(partition, out var current)
                    && current)
                {
                    _paused.TryAdd(partition, 0);
                    hadPaused = true;
                }
                else
                {
                    _rebalancePausedPartitions.TryRemove(partition, out _);
                }
            }
        }
        return hadPaused;
    }

    /// <summary>
    /// Disposes a fetch that was (or may still be) queued in _pendingFetches. Any queued
    /// fetch can be the one an in-flight ConsumeAsync iteration holds (the iterator Peeks
    /// and leaves it queued), so the version bump is what lets the iterator detect the
    /// disposal instead of reading disposed pooled buffers. All disposal of queued fetches
    /// must go through this method.
    /// </summary>
    private void DisposeQueuedFetch(PendingFetchData pending)
    {
        Interlocked.Increment(ref _pendingFetchesVersion);
        if (ReferenceEquals(pending, _activeConsumeOneFetch))
        {
            Debug.Assert(
                _deferredConsumeOneFetchDisposal is null
                || ReferenceEquals(_deferredConsumeOneFetchDisposal, pending),
                "A different ConsumeOne fetch already awaits deferred disposal.");
            _deferredConsumeOneFetchDisposal = pending;
            return;
        }

        pending.Dispose();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BeginConsumeOneFetchUse(PendingFetchData pending)
    {
        Debug.Assert(_activeConsumeOneFetch is null, "Nested ConsumeOne fetch use is unsupported.");
        _activeConsumeOneFetch = pending;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool EndConsumeOneFetchUse(PendingFetchData pending)
    {
        Debug.Assert(
            ReferenceEquals(_activeConsumeOneFetch, pending),
            "ConsumeOne fetch-use scope ended out of order.");
        _activeConsumeOneFetch = null;

        if (!ReferenceEquals(_deferredConsumeOneFetchDisposal, pending))
            return false;

        return CompleteDeferredConsumeOneFetchDisposal(pending);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool CompleteDeferredConsumeOneFetchDisposal(PendingFetchData pending)
    {
        _deferredConsumeOneFetchDisposal = null;
        pending.Dispose();
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool EndConsumeOneFetchUseIfActive(PendingFetchData pending) =>
        ReferenceEquals(_activeConsumeOneFetch, pending)
        && EndConsumeOneFetchUse(pending);

    private void EnqueuePendingFetch(PendingFetchData pending)
    {
        // Route from the foreground-applied snapshot, not the concurrently published one.
        // A publication that splits a multi-item drain is reconciled afterward as one ordered
        // queue transition, preserving per-partition FIFO across the pause boundary.
        if (_deliveryPausedSnapshot.Contains(pending.TopicPartition))
            _pausedPendingFetches.Enqueue(pending);
        else
            _pendingFetches.Enqueue(pending);
        Interlocked.Increment(ref _pendingFetchDepth);
    }

    private PendingFetchData DequeuePendingFetch()
    {
        var pending = _pendingFetches.Dequeue();
        Interlocked.Decrement(ref _pendingFetchDepth);
        return pending;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MovePendingFetchToPaused(PendingFetchData pending)
    {
        Debug.Assert(_pendingFetches.Count > 0 && ReferenceEquals(_pendingFetches.Peek(), pending));

        // The inner batch iterator can discover pause only when probing past its final
        // record. Complete that probe before parking so resume does not expose an empty
        // batch. The outer iterator will observe the queue-version change and return
        // without touching the parked fetch.
        if (Interlocked.Exchange(ref _batchIterationEpoch.BatchExhaustionProbePending, 0) != 0)
            pending.TryBufferNext();

        FlushConsumedPositions(pending);
        _pausedPendingFetches.Enqueue(_pendingFetches.Dequeue());
    }

    /// <summary>
    /// Applies pause/resume changes on the foreground consumer thread. Queues are scanned
    /// only after a control-plane snapshot change; the steady-state path is one version read.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PreparePendingFetchesForDelivery()
    {
        var pausedSnapshotVersion = Volatile.Read(ref _pausedSnapshotVersion);
        if (pausedSnapshotVersion != _observedPausedSnapshotVersion)
            ApplyPausedSnapshotToPendingFetches(pausedSnapshotVersion);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void PrepareConsumeOneDelivery()
    {
        if (Volatile.Read(ref _batchIterationEpoch.ConsumeOneDeliveryChangesPending) == 0)
        {
            RecoverAndClearFetchBufferForPendingCoordinatorRevocations();
            PreparePendingFetchesForDelivery();
            return;
        }

        // A new ConsumeOne call proves the prior result before pause reconciliation can
        // move its fetch out of the active queue. Generic queue reconciliation must not
        // infer processing for streaming APIs, whose continuation owns that proof.
        if (_pendingFetches.Count > 0)
            _pendingFetches.Peek().MarkYieldedProcessed();

        while (true)
        {
            var expectedVersion = _batchIterationEpoch.CaptureStableVersion();
            RecoverAndClearFetchBufferForPendingCoordinatorRevocations();
            PreparePendingFetchesForDelivery();
            if (_batchIterationEpoch.TryAcknowledgeConsumeOneDeliveryChanges(expectedVersion))
                return;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ApplyPausedSnapshotToPendingFetches(int pausedSnapshotVersion)
    {
        var paused = _pausedSnapshot;

        // Older deferred fetches must precede any newer fetches for the same partition.
        // Cross-partition order is unspecified, so temporarily move the active queue aside,
        // restore resumed data first, then append the active queue without allocating.
        while (_pendingFetches.TryDequeue(out var activePending))
            _pendingFetchScratch.Enqueue(activePending);

        var count = _pausedPendingFetches.Count;
        // Fetches set aside by a held skip during this batch loop are older than any of their
        // partition's fetches still queued, so they rejoin ahead of the active queue. Held
        // partitions stay held, and the loop sets them aside again when they reach the head.
        var heldCount = _heldSkippedFetches.Count;
        for (var i = 0; i < count; i++)
        {
            var pending = _pausedPendingFetches.Dequeue();
            if (paused.Contains(pending.TopicPartition))
                _pausedPendingFetches.Enqueue(pending);
            else
                _pendingFetches.Enqueue(pending);
        }

        for (var i = 0; i < heldCount; i++)
            _pendingFetches.Enqueue(_heldSkippedFetches.Dequeue());

        while (_pendingFetchScratch.TryDequeue(out var retainedPending))
            _pendingFetches.Enqueue(retainedPending);

        // Pause changes are infrequent control-plane operations. Scan once here so all
        // already-queued fetches for newly paused partitions retain their original order.
        count = _pendingFetches.Count;
        for (var i = 0; i < count; i++)
        {
            var pending = _pendingFetches.Peek();
            if (paused.Contains(pending.TopicPartition))
                MovePendingFetchToPaused(pending);
            else
                _pendingFetches.Enqueue(_pendingFetches.Dequeue());
        }

        _deliveryPausedSnapshot = paused;
        _observedPausedSnapshotVersion = pausedSnapshotVersion;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private RecordIterationStatus GetRecordIterationStatus(
        TopicPartition partition,
        ref int observedVersion)
    {
        var currentVersion = Volatile.Read(ref _batchIterationEpoch.Version);
        if ((currentVersion & 1) == 0 && currentVersion == observedVersion)
            return RecordIterationStatus.Continue;

        return GetRecordIterationStatusSlow(partition, ref observedVersion);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private RecordIterationStatus GetConsumeOneDeliveryStatus(TopicPartition partition)
    {
        if (Volatile.Read(ref _batchIterationEpoch.ConsumeOneDeliveryChangesPending) == 0)
            return RecordIterationStatus.Continue;

        return GetConsumeOneDeliveryStatusSlow(partition);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private RecordIterationStatus GetConsumeOneDeliveryStatusSlow(TopicPartition partition)
    {
        var observedVersion = 0;
        return GetRecordIterationStatusSlow(partition, ref observedVersion);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private RecordIterationStatus GetRecordIterationStatusSlow(
        TopicPartition partition,
        ref int observedVersion)
    {
        var spin = new SpinWait();
        while (true)
        {
            var currentVersion = Volatile.Read(ref _batchIterationEpoch.Version);
            if ((currentVersion & 1) != 0)
            {
                spin.SpinOnce();
                continue;
            }

            var canContinue = CanContinueBatchIterationWithPause(partition, out var pausedAtVersion);

            if (Volatile.Read(ref _batchIterationEpoch.Version) == currentVersion)
            {
                if (!canContinue)
                {
                    return pausedAtVersion
                        ? RecordIterationStatus.Paused
                        : RecordIterationStatus.Stopped;
                }

                observedVersion = currentVersion;
                return RecordIterationStatus.Continue;
            }
        }
    }

    private enum RecordIterationStatus : byte
    {
        Continue,
        Paused,
        Stopped
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void HandleStoppedRecordIteration(TopicPartition partition, bool paused)
    {
        if (paused || !HasPendingFetchClear(partition))
            return;

        // Preserve the existing revocation/divergence recovery path. It owns discard;
        // Pause owns preservation only while the partition remains assigned.
        ClearFetchBufferForPendingCoordinatorRevocations();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool StopConsumeOneDelivery(PendingFetchData pending, bool paused)
    {
        HandleStoppedRecordIteration(pending.TopicPartition, paused);
        if (!paused)
        {
            return _pendingFetches.Count == 0
                   || !ReferenceEquals(_pendingFetches.Peek(), pending);
        }

        pending.BufferCurrentForRedelivery();
        MovePendingFetchToPaused(pending);
        return true;
    }

    private void StagePendingFetchClear(TopicPartition partition)
    {
        // Publish before synchronous buffer disposal so an in-flight consume callback
        // observes the clear without adding another fence to the per-record hot path.
        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
        {
            SetPendingFetchClearMarkerLocked(partition, PendingFetchClearMarkerSource.PositionChange);

            _batchIterationEpoch.BeginPublication();
            try
            {
                Volatile.Write(ref _coordinatorRevokedPartitionsPendingFetchClearMarkerPresent, 1);
                Volatile.Write(ref _coordinatorRevokedPartitionsPendingFetchClearPending, 1);
            }
            finally
            {
                _batchIterationEpoch.EndPublication();
            }
        }
    }

    private void SetPendingFetchClearMarkerLocked(
        TopicPartition partition,
        PendingFetchClearMarkerSource source)
    {
        var version = Interlocked.Increment(ref _pendingFetchClearVersion);
        if (version == NoPendingFetchClearVersion)
            version = Interlocked.Increment(ref _pendingFetchClearVersion);

        _coordinatorRevokedPartitionsPendingFetchClear[partition] = version;
        _pendingFetchClearMarkerSources[partition] = source;
    }

    private void ClearFetchBuffer()
    {
        ClearActiveConsumedPosition();
        _stuckFetchPositionTracker.Clear();

        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
        {
            InvalidateAllFetchesLocked();
            _pendingDivergingEpochResets.Clear();
        }

        // A group membership being ended in this call: what the discarded fetches hold for the
        // departing partitions goes into its capture before they are disposed.
        var leaveCapture = Volatile.Read(ref _leaveFetchCapture);

        // Dispose and clear pending fetches to release pooled memory
        while (_pendingFetches.TryDequeue(out var pending))
        {
            Interlocked.Decrement(ref _pendingFetchDepth);
            if (leaveCapture is not null)
                CaptureDepartingFetchPositions(leaveCapture, pending);
            StagePendingFetchClear(pending.TopicPartition);
            DisposeQueuedFetch(pending);
        }
        while (_pausedPendingFetches.TryDequeue(out var pausedPending))
        {
            Interlocked.Decrement(ref _pendingFetchDepth);
            if (leaveCapture is not null)
                CaptureDepartingFetchPositions(leaveCapture, pausedPending);
            StagePendingFetchClear(pausedPending.TopicPartition);
            DisposeQueuedFetch(pausedPending);
        }
        while (_heldSkippedFetches.TryDequeue(out var heldPending))
        {
            Interlocked.Decrement(ref _pendingFetchDepth);
            StagePendingFetchClear(heldPending.TopicPartition);
            DisposeQueuedFetch(heldPending);
        }
        _heldSkippedPartitions.Clear();
        // Also drain prefetched items that haven't been moved to _pendingFetches yet.
        // Without this, stale data from old partitions would surface after reassignment.
        while (_prefetchBuffer.TryRead(out var prefetched))
        {
            TrackPrefetchedBytes(prefetched, release: true);
            prefetched.Dispose();
        }
        // Clear pending EOF events as they are stale after buffer clear
        _pendingEofEvents.Clear();
        // Flag first: a bound written concurrently either survives with the flag set again, or
        // is cleared with it. The flag may stay set over an empty dictionary, never the reverse.
        _hasEofSupersededBounds = false;
        _eofSupersededBelow.Clear();
    }

    private void ClearFetchBufferForPartitions(
        IEnumerable<TopicPartition> partitionsToRemove,
        bool invalidateAllFetches = false,
        bool stagePendingClear = false,
        bool preserveDivergingEpochResets = false)
    {
        // Create a set for efficient lookup
        var removeSet = partitionsToRemove is HashSet<TopicPartition> set
            ? set
            : new HashSet<TopicPartition>(partitionsToRemove);

        if (removeSet.Count == 0)
            return;

        // Freeze pause routing for this ordered drain. A concurrent Pause/Resume is
        // reconciled at the next delivery boundary instead of splitting retained
        // fetches between the active and paused queues.
        PreparePendingFetchesForDelivery();

        foreach (var partition in removeSet)
        {
            ClearActiveConsumedPosition(partition);
            _stuckFetchPositionTracker.Reset(partition);
            // The partition's held data is dropped below; a reassignment must not inherit the hold.
            _heldSkippedPartitions.Remove(partition);
        }

        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
        {
            if (invalidateAllFetches)
                InvalidateAllFetchesLocked();
            else
                InvalidateFetchesForPartitionSetLocked(removeSet);

            if (!preserveDivergingEpochResets)
            {
                foreach (var partition in removeSet)
                    _pendingDivergingEpochResets.TryRemove(partition, out _);
            }
        }

        // Filter in-place without allocating a temporary queue
        // Dequeue all items and re-enqueue only those we want to keep
        var count = _pendingFetches.Count;

        for (var i = 0; i < count; i++)
        {
            var pending = DequeuePendingFetch();

            // Check if this partition should be kept
            // Build TopicPartition inline for the Contains check
            if (!removeSet.Contains(pending.TopicPartition))
            {
                // Keep this item by re-enqueueing it
                EnqueuePendingFetch(pending);
            }
            else
            {
                // Dispose removed items to release pooled memory
                if (stagePendingClear)
                    StagePendingFetchClear(pending.TopicPartition);
                DisposeQueuedFetch(pending);
            }
        }

        count = _heldSkippedFetches.Count;
        for (var i = 0; i < count; i++)
        {
            var pending = _heldSkippedFetches.Dequeue();
            if (!removeSet.Contains(pending.TopicPartition))
            {
                _heldSkippedFetches.Enqueue(pending);
            }
            else
            {
                Interlocked.Decrement(ref _pendingFetchDepth);
                if (stagePendingClear)
                    StagePendingFetchClear(pending.TopicPartition);
                DisposeQueuedFetch(pending);
            }
        }

        count = _pausedPendingFetches.Count;
        for (var i = 0; i < count; i++)
        {
            var pending = _pausedPendingFetches.Dequeue();
            if (!removeSet.Contains(pending.TopicPartition))
            {
                _pausedPendingFetches.Enqueue(pending);
            }
            else
            {
                Interlocked.Decrement(ref _pendingFetchDepth);
                if (stagePendingClear)
                    StagePendingFetchClear(pending.TopicPartition);
                DisposeQueuedFetch(pending);
            }
        }

        // Also drain prefetch buffer items for revoked partitions.
        // Without this, stale data from revoked partitions sitting in the prefetch
        // buffer would be consumed after an incremental unassign (cooperative rebalance),
        // causing data for partitions no longer owned by this consumer to be yielded.
        DrainPrefetchBufferForPartitionsCore(removeSet);

        ClearPendingEofEventsForPartitions(removeSet);
    }

    private void ClearPendingEofEventsForPartitions(HashSet<TopicPartition> partitionsToRemove)
    {
        // A replaced position (seek, revocation, release) starts a new EOF history.
        if (_hasEofSupersededBounds)
        {
            foreach (var partition in partitionsToRemove)
                _eofSupersededBelow.TryRemove(partition, out _);
        }

        // Seek, revocation and every skipped-batch release land here: once per control operation
        // or batch loop, never per publication. With no queued EOF this is one read; otherwise
        // rotate the queue once in place instead of copying it out.
        if (_pendingEofEvents.IsEmpty)
            return;

        var count = _pendingEofEvents.Count;
        for (var i = 0; i < count && _pendingEofEvents.TryDequeue(out var eofEvent); i++)
        {
            if (!partitionsToRemove.Contains(eofEvent.Partition))
                _pendingEofEvents.Enqueue(eofEvent);
        }
    }

    /// <summary>The coordinator's revocation hook: a revocation or loss has just been recorded.</summary>
    private void QueueCoordinatorRevokedPartitionsForFetchClear(IReadOnlyList<TopicPartition> partitions) =>
        QueueCoordinatorRevokedPartitionsForFetchClear(partitions, retainSeeks: null);

    /// <param name="partitions">The partitions whose fetches and buffered records are invalidated.</param>
    /// <param name="retainSeeks">
    /// Partitions whose pending rebalance seek is kept: assignment sync passes the partitions
    /// assigned again after a revocation, whose seek came from the new assignment's callback.
    /// </param>
    private void QueueCoordinatorRevokedPartitionsForFetchClear(
        IReadOnlyList<TopicPartition> partitions,
        HashSet<TopicPartition>? retainSeeks)
    {
        Volatile.Read(ref _activeSnapshot)?.InvalidateConsumerState();

        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
        {
            foreach (var partition in partitions)
            {
                // Coordinator revocation supersedes any correction from the old assignment.
                // Keep the revocation marker so the consume loop still drains stale buffers.
                if (retainSeeks is null || !retainSeeks.Contains(partition))
                {
                    _pendingRebalanceSeeks.TryRemove(partition, out _);
                    // The callback's ownership has ended: sync must not restore its pause, but
                    // cleanup still needs to know the pause came from it.
                    _rebalancePausedPartitions.TryUpdate(partition, false, true);
                }

                _pendingDivergingEpochResets.TryRemove(partition, out _);
                SetPendingFetchClearMarkerLocked(
                    partition,
                    PendingFetchClearMarkerSource.CoordinatorRevocation);
            }

            // Invalidate broker fetches that started before the revocation immediately.
            // Waiting for the consume loop to drain the queued clear lets an ABA reassignment
            // make a stale response look current and advance the reinitialized fetch position.
            InvalidateFetchesForPartitionsLocked(partitions);
            _batchIterationEpoch.BeginPublication();
            try
            {
                Volatile.Write(ref _coordinatorRevokedPartitionsPendingFetchClearMarkerPresent, 1);
                Volatile.Write(ref _coordinatorRevokedPartitionsPendingFetchClearPending, 1);
            }
            finally
            {
                _batchIterationEpoch.EndPublication();
            }
        }
    }

    private bool StageDivergingEpochReset(
        TopicPartition partition,
        long endOffset,
        int epoch,
        int fetchBufferEpoch,
        bool startsBatch)
    {
        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
            return TryQueueDivergingEpochResetLocked(
                partition,
                endOffset,
                epoch,
                fetchBufferEpoch,
                startsBatch);
    }

    private bool TryQueueDivergingEpochResetLocked(
        TopicPartition partition,
        long endOffset,
        int epoch,
        int fetchBufferEpoch,
        bool startsBatch)
    {
        if (ShouldDropStaleFetchPartition(partition, fetchBufferEpoch))
            return false;

        _pendingDivergingEpochResets[partition] = (endOffset, epoch);
        SetPendingFetchClearMarkerLocked(partition, PendingFetchClearMarkerSource.DivergingEpoch);
        if (startsBatch)
            _stagedDivergingEpochResetBatches++;
        _batchIterationEpoch.BeginPublication();
        try
        {
            Volatile.Read(ref _activeSnapshot)?.InvalidateConsumerState();
            Volatile.Write(ref _coordinatorRevokedPartitionsPendingFetchClearMarkerPresent, 1);
        }
        finally
        {
            _batchIterationEpoch.EndPublication();
        }
        return true;
    }

    private void CompleteDivergingEpochResets()
    {
        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
        {
            if (_stagedDivergingEpochResetBatches > 0)
                _stagedDivergingEpochResetBatches--;

            if (_stagedDivergingEpochResetBatches == 0)
                Volatile.Write(ref _coordinatorRevokedPartitionsPendingFetchClearPending, 1);
        }
    }

    private bool ClearFetchBufferForPendingCoordinatorRevocations()
    {
        if (!HasPendingCoordinatorRevocations())
            return false;

        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
            return ClearFetchBufferForPendingCoordinatorRevocationsLocked();
    }

    private bool ClearFetchBufferForPendingCoordinatorRevocationsLocked()
    {
        if (!HasPendingCoordinatorRevocations())
            return false;

        if (_coordinatorRevokedPartitionsPendingFetchClear.IsEmpty)
        {
            Volatile.Write(ref _coordinatorRevokedPartitionsPendingFetchClearMarkerPresent, 0);
            Volatile.Write(ref _coordinatorRevokedPartitionsPendingFetchClearPending, 0);
            return false;
        }

        var partitionsToRemove = _coordinatorRevokedPartitionsPendingFetchClear.Keys.ToHashSet();
        HashSet<TopicPartition>? reservedPartitions = null;
        foreach (var partition in _topicIdentityResetPartitions)
        {
            if (partitionsToRemove.Remove(partition))
                (reservedPartitions ??= []).Add(partition);
        }

        // A newer marker on a reserved partition must remain available to reject the
        // in-flight identity reset, but its stale queued fetch still must be discarded.
        if (reservedPartitions is not null)
        {
            ClearFetchBufferForPartitions(
                reservedPartitions,
                preserveDivergingEpochResets: true);
        }

        if (partitionsToRemove.Count == 0)
            return reservedPartitions is not null;

        // Keep partitions marked while clearing. Background prefetches use this
        // marker to avoid advancing positions for data that will be discarded.
        var logTruncationException = ApplyPendingDivergingEpochResets(partitionsToRemove);
        ClearFetchBufferForPartitions(partitionsToRemove);

        foreach (var partition in partitionsToRemove)
        {
            _coordinatorRevokedPartitionsPendingFetchClear.TryRemove(partition, out _);
            _pendingFetchClearMarkerSources.Remove(partition);
        }

        var markersRemain = !_coordinatorRevokedPartitionsPendingFetchClear.IsEmpty;
        Volatile.Write(
            ref _coordinatorRevokedPartitionsPendingFetchClearMarkerPresent,
            markersRemain ? 1 : 0);
        Volatile.Write(
            ref _coordinatorRevokedPartitionsPendingFetchClearPending,
            markersRemain ? 1 : 0);

        if (logTruncationException is not null)
            throw logTruncationException;

        return true;
    }

    private bool RecoverAndClearFetchBufferForPendingCoordinatorRevocations()
    {
        if (!HasPendingCoordinatorRevocations()
            && _coordinatorRevokedPartitionsPendingFetchClear.IsEmpty)
            return false;

        bool recovered;
        bool cleared;
        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
        {
            recovered = TryRecoverMissingPendingFetchClearMarkersLocked();
            cleared = ClearFetchBufferForPendingCoordinatorRevocationsLocked();
        }

        if (recovered)
            LogRecoveredPendingFetchClearInvariant();

        return cleared;
    }

    private LogTruncationException? ApplyPendingDivergingEpochResets(
        IReadOnlyCollection<TopicPartition> partitions)
    {
        List<TopicPartitionOffset>? truncationOffsets = null;
        foreach (var partition in partitions)
        {
            if (!_pendingDivergingEpochResets.TryRemove(partition, out var reset))
                continue;

            if (_options.AutoOffsetReset == AutoOffsetReset.None)
            {
                (truncationOffsets ??= []).Add(new TopicPartitionOffset(
                    partition.Topic,
                    partition.Partition,
                    reset.EndOffset,
                    reset.Epoch));
                continue;
            }

            // Refetch unread common-prefix records. Cap positions from the corrected epoch at
            // its boundary, but preserve records already delivered from a newer leader epoch.
            var resumeOffset = BoundDivergingResumeOffset(
                _positions.GetValueOrDefault(partition, reset.EndOffset),
                GetLastConsumedLeaderEpoch(partition),
                reset);

            if (TryGetActiveConsumedPosition(
                    partition,
                    out var activePosition,
                    out var activeLeaderEpoch,
                    includeFilteredProgress: false))
            {
                resumeOffset = Math.Max(
                    resumeOffset,
                    BoundDivergingResumeOffset(activePosition, activeLeaderEpoch, reset));
            }

            foreach (var pending in _pendingFetches)
            {
                // The reset invalidates the fetch epoch. Preserve only delivered records;
                // filtered/control progress from that stale fetch cannot override the broker's
                // divergence boundary.
                if (TryGetConsumedPosition(
                        pending,
                        out var pendingPartition,
                        out var nextOffset,
                        out var pendingLeaderEpoch,
                        includeFilteredProgress: false)
                    && pendingPartition.Equals(partition))
                {
                    resumeOffset = Math.Max(
                        resumeOffset,
                        BoundDivergingResumeOffset(nextOffset, pendingLeaderEpoch, reset));
                }
            }

            foreach (var pending in _pausedPendingFetches)
            {
                if (TryGetConsumedPosition(
                        pending,
                        out var pendingPartition,
                        out var nextOffset,
                        out var pendingLeaderEpoch)
                    && pendingPartition.Equals(partition))
                {
                    resumeOffset = Math.Max(
                        resumeOffset,
                        BoundDivergingResumeOffset(nextOffset, pendingLeaderEpoch, reset));
                }
            }

            SetFetchPosition(partition, resumeOffset);
            SetPosition(partition, resumeOffset, dirty: false);
            // Clearing the pending fetch bypasses its normal position flush. Discard the
            // matching auto-commit snapshot so a later commit/position read cannot restore
            // the pre-divergence leader epoch after the reset below clears it.
            ClearActiveConsumedPosition(partition);
            // The diverging epoch is the last common epoch, not the new leader epoch.
            // Reusing it would make every subsequent fetch report the same divergence.
            ClearLastConsumedLeaderEpoch(partition);
            LogDivergingEpochReset(
                partition.Topic,
                partition.Partition,
                resumeOffset,
                reset.EndOffset,
                reset.Epoch);
        }

        return truncationOffsets is null ? null : new LogTruncationException(truncationOffsets);
    }

    private static long BoundDivergingResumeOffset(
        long nextOffset,
        int consumedLeaderEpoch,
        (long EndOffset, int Epoch) reset) =>
        nextOffset > reset.EndOffset && consumedLeaderEpoch <= reset.Epoch
            ? reset.EndOffset
            : nextOffset;

    private bool HasPendingCoordinatorRevocations() =>
        Volatile.Read(ref _coordinatorRevokedPartitionsPendingFetchClearPending) != 0;

    private bool IsCurrentlyAssigned(TopicPartition partition)
    {
        return _assignmentSnapshot.Contains(partition);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HasPendingFetchClear(TopicPartition partition) =>
        Volatile.Read(ref _coordinatorRevokedPartitionsPendingFetchClearMarkerPresent) != 0
        && _coordinatorRevokedPartitionsPendingFetchClear.ContainsKey(partition);

    private bool TryRecoverMissingPendingFetchClearMarkers()
    {
        if (Volatile.Read(ref _coordinatorRevokedPartitionsPendingFetchClearMarkerPresent) != 0
            && Volatile.Read(ref _coordinatorRevokedPartitionsPendingFetchClearPending) != 0)
            return false;

        if (_coordinatorRevokedPartitionsPendingFetchClear.IsEmpty)
            return false;

        bool recovered;
        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
            recovered = TryRecoverMissingPendingFetchClearMarkersLocked();

        if (!recovered)
            return false;

        LogRecoveredPendingFetchClearInvariant();
        return true;
    }

    private bool TryRecoverMissingPendingFetchClearMarkersLocked()
    {
        if (_coordinatorRevokedPartitionsPendingFetchClear.IsEmpty)
            return false;

        var recovered = false;
        if (Volatile.Read(ref _coordinatorRevokedPartitionsPendingFetchClearMarkerPresent) == 0)
        {
            _batchIterationEpoch.BeginPublication();
            try
            {
                Volatile.Write(ref _coordinatorRevokedPartitionsPendingFetchClearMarkerPresent, 1);
            }
            finally
            {
                _batchIterationEpoch.EndPublication();
            }
            recovered = true;
        }

        if (_stagedDivergingEpochResetBatches == 0
            && Volatile.Read(ref _coordinatorRevokedPartitionsPendingFetchClearPending) == 0)
        {
            Volatile.Write(ref _coordinatorRevokedPartitionsPendingFetchClearPending, 1);
            recovered = true;
        }

        return recovered;
    }

    private BatchIterationStatus GetBatchIterationStatus(TopicPartition partition)
    {
        var canContinue = CanContinueBatchIterationWithPause(partition, out var paused);
        return canContinue
            ? BatchIterationStatus.Continue
            : paused
                ? BatchIterationStatus.Paused
                : BatchIterationStatus.Stopped;
    }

    private bool CanContinueBatchIterationWithPause(TopicPartition partition, out bool paused)
    {
        // Revocation/diverging-epoch discard owns invalid data even when Pause also
        // contains the partition. Only a still-valid paused record may be redelivered.
        if (HasPendingFetchClear(partition))
        {
            paused = false;
            return false;
        }

        if (!IsCurrentlyAssigned(partition))
        {
            paused = false;
            return false;
        }

        paused = _pausedSnapshot.Contains(partition);
        return !paused;
    }

    private bool IsFetchBufferEpochStale(TopicPartition partition, int fetchBufferEpoch) =>
        fetchBufferEpoch < Volatile.Read(ref _minimumFetchBufferEpoch)
        || (_minimumFetchBufferEpochsByPartition.TryGetValue(partition, out var minimumEpoch)
            && fetchBufferEpoch < minimumEpoch);

    private int GetMinimumFetchBufferEpoch(TopicPartition partition)
    {
        var minimumEpoch = Volatile.Read(ref _minimumFetchBufferEpoch);
        return _minimumFetchBufferEpochsByPartition.TryGetValue(partition, out var partitionMinimumEpoch)
            && partitionMinimumEpoch > minimumEpoch
                ? partitionMinimumEpoch
                : minimumEpoch;
    }

    private bool ShouldDropStaleFetchPartition(TopicPartition partition, int fetchBufferEpoch) =>
        IsFetchBufferEpochStale(partition, fetchBufferEpoch)
        || _coordinatorRevokedPartitionsPendingFetchClear.ContainsKey(partition)
        || !IsCurrentlyAssigned(partition);

    private void InvalidateAllFetchesLocked()
    {
        var minimumEpoch = Interlocked.Increment(ref _fetchBufferEpoch);
        Volatile.Write(ref _minimumFetchBufferEpoch, minimumEpoch);
        _minimumFetchBufferEpochsByPartition.Clear();
    }

    // Set variant: the struct enumerator keeps seek and skipped-batch release allocation-free.
    private void InvalidateFetchesForPartitionSetLocked(HashSet<TopicPartition> partitions)
    {
        var minimumEpoch = Interlocked.Increment(ref _fetchBufferEpoch);
        foreach (var partition in partitions)
            _minimumFetchBufferEpochsByPartition[partition] = minimumEpoch;
    }

    private void InvalidateFetchesForPartitionsLocked(IEnumerable<TopicPartition> partitions)
    {
        var minimumEpoch = Interlocked.Increment(ref _fetchBufferEpoch);
        foreach (var partition in partitions)
            _minimumFetchBufferEpochsByPartition[partition] = minimumEpoch;
    }

    private async ValueTask WritePrefetchedItemsAsync(
        IReadOnlyList<PendingFetchData> pendingItems,
        int fetchBufferEpoch,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < pendingItems.Count; i++)
        {
            var pending = pendingItems[i];
            var tracked = false;
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
                    {
                        // Seek uses this same lock, so a fetch is either published before its
                        // invalidation and drained, or observes the new epoch and is dropped.
                        if (ShouldDropStaleFetchPartition(pending.TopicPartition, fetchBufferEpoch))
                        {
                            if (tracked)
                                TrackPrefetchedBytes(pending, release: true);
                            pending.Dispose();
                            break;
                        }

                        if (!tracked)
                        {
                            TrackPrefetchedBytes(pending, release: false);
                            tracked = true;
                        }

                        // Replica routing can leave overlapping responses in flight.
                        // Refresh the floor at publication, after earlier responses have
                        // advanced the fetch position, including after a full-buffer wait.
                        var partition = pending.TopicPartition;
                        pending.RaiseStartOffset(_fetchPositions.GetValueOrDefault(partition, -1));
                        var nextOffset = pending.FetchEndOffsetExclusive;
                        var nextOffsetLeaderEpoch = pending.FetchEndLeaderEpoch;
                        var hasRecords = pending.GetBatches().Count > 0;
                        if (_prefetchBuffer.TryWrite(pending))
                        {
                            RearmPartitionEofForPublishedRecords(partition, hasRecords, nextOffset);
                            // The reader can dispose pending immediately after TryWrite.
                            // Use captured values, and never advance for an unpublished item.
                            UpdateFetchPositionsFromPrefetch(
                                partition, nextOffset, nextOffsetLeaderEpoch, fetchBufferEpoch);
                            break;
                        }
                    }

                    await _prefetchBuffer.WaitToWriteAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                if (tracked)
                    TrackPrefetchedBytes(pending, release: true);

                for (var j = i; j < pendingItems.Count; j++)
                    pendingItems[j].Dispose();

                throw;
            }
        }
    }

    private void DrainPrefetchBufferForPartitions(HashSet<TopicPartition> partitionsToRemove)
    {
        PreparePendingFetchesForDelivery();
        DrainPrefetchBufferForPartitionsCore(partitionsToRemove);
    }

    private void DrainPrefetchBufferForPartitionsCore(HashSet<TopicPartition> partitionsToRemove)
    {
        // O(n) over the prefetch buffer is acceptable on this infrequent rebalance path.
        while (_prefetchBuffer.TryRead(out var prefetched))
        {
            TrackPrefetchedBytes(prefetched, release: true);
            if (partitionsToRemove.Contains(prefetched.TopicPartition))
                prefetched.Dispose();
            else
                EnqueuePendingFetch(prefetched);
        }
    }

    public void Pause(params TopicPartition[] partitions)
    {
        ArgumentNullException.ThrowIfNull(partitions);

        List<TopicPartition>? changedPartitions = null;
        lock (_pauseStateLock)
        {
            foreach (var partition in partitions)
            {
                if (_coordinator is not { IsDeliveringAssignedCallback: true })
                {
                    if (_paused.TryAdd(partition, 0))
                    {
                        AfterPartitionPausedForTest?.Invoke(this, partition);
                        (changedPartitions ??= []).Add(partition);
                    }

                    continue;
                }

                // Decided before anything changes, and atomic with the revocation hook: a pause
                // from a callback whose ownership has ended is a no-op; otherwise the pause and its
                // marker are one step, so the hook flips the marker of a pause it races with.
                lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
                {
                    if (IsStaleAssignedCallbackPartitionLocked(partition, out var isCallbackPartition))
                    {
                        LogStaleRebalanceCallbackCallIgnored(nameof(Pause), partition.Topic, partition.Partition);
                        continue;
                    }

                    if (_paused.TryAdd(partition, 0))
                    {
                        AfterPartitionPausedForTest?.Invoke(this, partition);
                        (changedPartitions ??= []).Add(partition);
                    }

                    // Also when already paused: that pause may be the previous ownership's, which
                    // sync clears.
                    if (isCallbackPartition)
                        _rebalancePausedPartitions[partition] = true;
                }
            }
        }

        if (changedPartitions is null)
            return;

        PublishPausedSnapshot();
        UpdateCachesForPausedPartitions(changedPartitions);
    }

    public void Resume(params TopicPartition[] partitions)
    {
        ArgumentNullException.ThrowIfNull(partitions);

        List<TopicPartition>? changedPartitions = null;
        lock (_pauseStateLock)
        {
            foreach (var partition in partitions)
            {
                if (_coordinator is { IsDeliveringAssignedCallback: true })
                {
                    // A callback whose ownership has ended must not resume the new ownership.
                    bool stale;
                    lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
                        stale = IsStaleAssignedCallbackPartitionLocked(partition, out _);
                    if (stale)
                    {
                        LogStaleRebalanceCallbackCallIgnored(nameof(Resume), partition.Topic, partition.Partition);
                        continue;
                    }
                }

                if (_paused.TryRemove(partition, out _))
                {
                    _rebalancePausedPartitions.TryRemove(partition, out _);
                    (changedPartitions ??= []).Add(partition);
                }
            }
        }

        if (changedPartitions is null)
            return;

        PublishPausedSnapshot();
        UpdateCachesForResumedPartitions(changedPartitions);
        WakePausedDirectFetch();
        _prefetchBuffer.SignalReader();
    }

    private void WakePausedDirectFetch()
    {
        lock (_pausedDirectFetchCancellationSourceLock)
        {
            _pausedDirectFetchCancellationSource?.Cancel();
        }
    }

    private void CancelActiveConsumeOperations()
    {
        foreach (var cts in _activeConsumeCancellationSources.Keys)
        {
            try { cts.Cancel(); }
            catch (ObjectDisposedException) { continue; }
        }
    }

    public WatermarkOffsets? GetWatermarkOffsets(TopicPartition topicPartition)
    {
        return _watermarks.TryGetValue(topicPartition, out var entry)
            ? entry.ReadWatermarks()
            : null;
    }

    public long? GetCurrentLag(TopicPartition partition)
    {
        if (Volatile.Read(ref _consumerDisposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaConsumer<TKey, TValue>));

        var assignmentVersion = Volatile.Read(ref _assignmentEnsureVersion);
        if (!_assignmentSnapshot.Contains(partition)
            || GetPositionWithoutCaching(partition) is not { } position
            || position < 0
            || !_watermarks.TryGetValue(partition, out var watermarks))
        {
            return null;
        }

        return CalculateLagIfAssignmentUnchanged(assignmentVersion, position, watermarks);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long? CalculateLagIfAssignmentUnchanged(
        int assignmentVersion,
        long position,
        WatermarkCacheEntry watermarks)
    {
        var lag = CalculateLag(position, watermarks.ReadLagEndOffset());
        return assignmentVersion == Volatile.Read(ref _assignmentEnsureVersion) ? lag : null;
    }

    public ValueTask<long?> QueryCurrentLagAsync(
        TopicPartition partition,
        CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _consumerDisposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaConsumer<TKey, TValue>));

        ThrowIfNotInitialized();
        cancellationToken.ThrowIfCancellationRequested();

        if (!_assignmentSnapshot.Contains(partition)
            || GetPositionWithoutCaching(partition) is not { } position
            || position < 0
            || !_watermarkAssignmentVersions.TryGetValue(partition, out var assignmentVersion))
        {
            return new ValueTask<long?>((long?)null);
        }

        return QueryCurrentLagCoreAsync(partition, assignmentVersion, cancellationToken);
    }

    private async ValueTask<long?> QueryCurrentLagCoreAsync(
        TopicPartition partition,
        int assignmentVersion,
        CancellationToken cancellationToken)
    {
        var latestOffset = await QueryLatestOffsetCoreAsync(partition, cancellationToken)
            .ConfigureAwait(false);
        lock (_snapshotStateGate)
        {
            if (!_assignmentSnapshot.Contains(partition)
                || GetPositionWithoutCaching(partition) is not { } position
                || position < 0
                || !_watermarkAssignmentVersions.TryGetValue(partition, out var currentVersion)
                || currentVersion != assignmentVersion)
            {
                return null;
            }

            UpdateCachedLagEndOffset(
                partition,
                latestOffset.Offset,
                latestOffset.LeaderEpoch,
                latestOffset.UpdateSequence);
            return CalculateLag(position, latestOffset.Offset);
        }
    }

    private static long CalculateLag(long position, long endOffset) =>
        position >= endOffset ? 0 : endOffset - position;

    private static ListOffsetsRequest CreateWatermarkListOffsetsRequest(
        TopicPartition topicPartition,
        IsolationLevel isolationLevel,
        long timestamp,
        int currentLeaderEpoch) => new()
        {
            ReplicaId = -1,
            IsolationLevel = isolationLevel,
            Topics =
            [
                new ListOffsetsRequestTopic
                {
                    Name = topicPartition.Topic,
                    Partitions =
                    [
                        new ListOffsetsRequestPartition
                        {
                            PartitionIndex = topicPartition.Partition,
                            Timestamp = timestamp,
                            CurrentLeaderEpoch = currentLeaderEpoch
                        }
                    ]
                }
            ]
        };

    private static ListOffsetsResponsePartition? FindListOffsetsPartition(
        ListOffsetsResponse response,
        TopicPartition topicPartition)
    {
        foreach (var topic in response.Topics)
        {
            if (topic.Name != topicPartition.Topic)
                continue;

            foreach (var partition in topic.Partitions)
            {
                if (partition.PartitionIndex == topicPartition.Partition)
                    return partition;
            }

            return null;
        }

        return null;
    }

    private ValueTask<TResponse> SendWithWatermarkWriteSequenceAsync<TRequest, TResponse>(
        IKafkaConnection connection,
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken)
        where TRequest : IKafkaRequest<TResponse>, IRequestWriteSequenceTarget
        where TResponse : IKafkaResponse
    {
        request.WriteSequenceSource = this;
        if (connection is IKafkaRequestWriteObserverConnection writeObserverConnection)
        {
            return writeObserverConnection.SendWithWriteObservationAsync<TRequest, TResponse>(
                request,
                apiVersion,
                request.RequestWriteStarted,
                cancellationToken);
        }

        request.RequestWriteStarted();
        return connection.SendWithClientTelemetryAsync<TRequest, TResponse>(request, apiVersion, _telemetryMetricCollector, cancellationToken);
    }

    long IRequestWriteSequenceSource.NextRequestWriteSequence() =>
        Interlocked.Increment(ref _watermarkUpdateSequence);

    ClientTelemetryMetricCollector IClientTelemetrySource.TelemetryMetricCollector => _telemetryMetricCollector;

    private async ValueTask<(long Offset, int LeaderEpoch, long UpdateSequence)> QueryLatestOffsetCoreAsync(
        TopicPartition topicPartition,
        CancellationToken cancellationToken)
    {
        using var apiTimeout = new ApiTimeoutScope(_options.DefaultApiTimeoutMs, cancellationToken);
        try
        {
            return await RetryHelper.WithRetryAsync(async () =>
            {
                var connectionLease = await GetPartitionLeaderControlConnectionAsync(
                        topicPartition,
                        apiTimeout.Token)
                    .ConfigureAwait(false);
                if (connectionLease is null)
                    throw new KafkaException(ErrorCode.LeaderNotAvailable, $"No leader found for partition {topicPartition}");
                using var lease = connectionLease.Value;
                var connection = lease.Connection;

                var listOffsetsVersion = _metadataManager.GetNegotiatedApiVersion(
                    connection,
                    ApiKey.ListOffsets,
                    ListOffsetsRequest.LowestSupportedVersion,
                    ListOffsetsRequest.HighestSupportedVersion);
                var request = CreateWatermarkListOffsetsRequest(
                    topicPartition,
                    _options.IsolationLevel,
                    LatestOffsetTimestamp,
                    GetCurrentLeaderEpoch(topicPartition));
                var response = await SendWithWatermarkWriteSequenceAsync<ListOffsetsRequest, ListOffsetsResponse>(
                        connection,
                        request,
                        listOffsetsVersion,
                        apiTimeout.Token)
                    .ConfigureAwait(false);
                var watermarkUpdateSequence = ((IRequestWriteSequenceTarget)request).WriteSequence;
                var partitionResponse = FindListOffsetsPartition(response, topicPartition);

                if (partitionResponse is null)
                {
                    throw new KafkaException(
                        ErrorCode.UnknownServerError,
                        $"Failed to query latest offset for {topicPartition}: missing partition response");
                }

                if (partitionResponse.ErrorCode != ErrorCode.None)
                {
                    throw KafkaException.FromErrorCode(
                        partitionResponse.ErrorCode,
                        $"Failed to query latest offset for {topicPartition}: {partitionResponse.ErrorCode}");
                }

                return (partitionResponse.Offset, partitionResponse.LeaderEpoch, watermarkUpdateSequence);
            }, _metadataManager, apiTimeout.Token, _options.RetryBackoffMs, _options.RetryBackoffMaxMs,
                deadline: OffsetLookupDeadline(nameof(QueryCurrentLagAsync), Timeout.InfiniteTimeSpan))
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (apiTimeout.DefaultTimeoutExpired)
        {
            throw apiTimeout.CreateTimeoutException(nameof(QueryCurrentLagAsync), ex);
        }
    }

    public ValueTask<WatermarkOffsets> QueryWatermarkOffsetsAsync(
        TopicPartition topicPartition,
        CancellationToken cancellationToken = default)
    {
        int? assignmentGeneration = _watermarkAssignmentVersions.TryGetValue(topicPartition, out var generation)
            ? generation
            : null;
        return QueryWatermarkOffsetsCoreAsync(
            topicPartition,
            cacheResult: true,
            assignmentGeneration,
            cancellationToken);
    }

    private async ValueTask<WatermarkOffsets> QueryWatermarkOffsetsCoreAsync(
        TopicPartition topicPartition,
        bool cacheResult,
        int? assignmentGeneration,
        CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _consumerDisposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaConsumer<TKey, TValue>));

        ThrowIfNotInitialized();

        using var apiTimeout = new ApiTimeoutScope(_options.DefaultApiTimeoutMs, cancellationToken);
        try
        {
            return await RetryHelper.WithRetryAsync(async () =>
            {
                var connectionLease = await GetPartitionLeaderControlConnectionAsync(topicPartition, apiTimeout.Token)
                    .ConfigureAwait(false);
                if (connectionLease is null)
                    throw new KafkaException(ErrorCode.LeaderNotAvailable, $"No leader found for partition {topicPartition}");
                using var lease = connectionLease.Value;
                var connection = lease.Connection;
                var topicId = GetTopicId(
                    _metadataManager.Metadata.CaptureSnapshot(),
                    topicPartition.Topic);

                var listOffsetsVersion = _metadataManager.GetNegotiatedApiVersion(
                    connection,
                    ApiKey.ListOffsets,
                    ListOffsetsRequest.LowestSupportedVersion,
                    ListOffsetsRequest.HighestSupportedVersion);

                var currentLeaderEpoch = GetCurrentLeaderEpoch(topicPartition);
                var earliestRequest = CreateWatermarkListOffsetsRequest(
                    topicPartition,
                    _options.IsolationLevel,
                    EarliestOffsetTimestamp,
                    currentLeaderEpoch);
                var latestRequest = CreateWatermarkListOffsetsRequest(
                    topicPartition,
                    _options.IsolationLevel,
                    LatestOffsetTimestamp,
                    currentLeaderEpoch);

                var earliestResponseTask = connection.SendWithClientTelemetryAsync<ListOffsetsRequest, ListOffsetsResponse>(
                    earliestRequest,
                    listOffsetsVersion, _telemetryMetricCollector,
                    apiTimeout.Token).AsTask();

                var latestResponseTask = SendWithWatermarkWriteSequenceAsync<ListOffsetsRequest, ListOffsetsResponse>(
                    connection,
                    latestRequest,
                    listOffsetsVersion,
                    apiTimeout.Token).AsTask();

                await Task.WhenAll(earliestResponseTask, latestResponseTask).ConfigureAwait(false);
                var watermarkUpdateSequence = ((IRequestWriteSequenceTarget)latestRequest).WriteSequence;

                var earliestPartitionResponse = FindListOffsetsPartition(earliestResponseTask.Result, topicPartition);

                if (earliestPartitionResponse is null)
                    throw new KafkaException(ErrorCode.UnknownServerError,
                        $"Failed to query earliest offset for {topicPartition}: missing partition response");

                if (earliestPartitionResponse.ErrorCode != ErrorCode.None)
                {
                    throw KafkaException.FromErrorCode(earliestPartitionResponse.ErrorCode,
                        $"Failed to query earliest offset for {topicPartition}: {earliestPartitionResponse.ErrorCode}");
                }

                var lowWatermark = earliestPartitionResponse.Offset;

                var latestPartitionResponse = FindListOffsetsPartition(latestResponseTask.Result, topicPartition);

                if (latestPartitionResponse is null)
                    throw new KafkaException(ErrorCode.UnknownServerError,
                        $"Failed to query latest offset for {topicPartition}: missing partition response");

                if (latestPartitionResponse.ErrorCode != ErrorCode.None)
                {
                    throw KafkaException.FromErrorCode(latestPartitionResponse.ErrorCode,
                        $"Failed to query latest offset for {topicPartition}: {latestPartitionResponse.ErrorCode}");
                }

                var highWatermark = latestPartitionResponse.Offset;

                var watermarks = new WatermarkOffsets(lowWatermark, highWatermark);

                if (cacheResult)
                {
                    var cacheUpdated = UpdateQueriedCachedWatermarks(
                        topicPartition,
                        lowWatermark,
                        highWatermark,
                        highWatermark,
                        assignmentGeneration,
                        latestPartitionResponse.LeaderEpoch,
                        watermarkUpdateSequence,
                        topicId);
                    if (!cacheUpdated && assignmentGeneration is null)
                    {
                        // ListOffsets responses do not carry topic IDs. Refresh identity only
                        // when a regressive unassigned snapshot was rejected as stale.
                        var refreshedTopicId = GetTopicId(
                            _metadataManager.Metadata.CaptureSnapshot(),
                            topicPartition.Topic);
                        if (refreshedTopicId == topicId)
                        {
                            await _metadataManager.RefreshMetadataAsync(
                                    [topicPartition.Topic],
                                    forceRefresh: true,
                                    cancellationToken: apiTimeout.Token)
                                .ConfigureAwait(false);
                            refreshedTopicId = GetTopicId(
                                _metadataManager.Metadata.CaptureSnapshot(),
                                topicPartition.Topic);
                        }

                        if (refreshedTopicId != topicId)
                        {
                            _ = UpdateQueriedCachedWatermarks(
                                topicPartition,
                                lowWatermark,
                                highWatermark,
                                highWatermark,
                                assignmentGeneration,
                                latestPartitionResponse.LeaderEpoch,
                                watermarkUpdateSequence,
                                refreshedTopicId);
                        }
                    }
                }

                return watermarks;
            }, _metadataManager, apiTimeout.Token, _options.RetryBackoffMs, _options.RetryBackoffMaxMs,
                deadline: OffsetLookupDeadline(nameof(QueryWatermarkOffsetsAsync), Timeout.InfiniteTimeSpan))
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (apiTimeout.DefaultTimeoutExpired)
        {
            throw apiTimeout.CreateTimeoutException(nameof(QueryWatermarkOffsetsAsync), ex);
        }
    }

    private bool UpdateQueriedCachedWatermarks(
        TopicPartition partition,
        long low,
        long high,
        long lagEndOffset,
        int? assignmentGeneration,
        int leaderEpoch,
        long watermarkUpdateSequence,
        Guid topicId)
    {
        lock (_snapshotStateGate)
        {
            if (assignmentGeneration is { } expectedGeneration)
            {
                if (!_assignmentSnapshot.Contains(partition)
                    || !_watermarkAssignmentVersions.TryGetValue(partition, out var currentGeneration)
                    || currentGeneration != expectedGeneration)
                {
                    return false;
                }
            }
            else if (_assignmentSnapshot.Contains(partition)
                     || _watermarkAssignmentVersions.ContainsKey(partition))
            {
                return false;
            }

            if (assignmentGeneration is null)
            {
                var retainedEntry = new WatermarkCacheEntry(
                    low,
                    high,
                    lagEndOffset,
                    GetMinimumFetchBufferEpoch(partition),
                    leaderEpoch,
                    watermarkUpdateSequence,
                    topicId);
                if (_watermarks.TryGetValue(partition, out var existingEntry))
                {
                    if (!existingEntry.TryReplaceWithNewerSnapshot(
                            _watermarks,
                            partition,
                            retainedEntry))
                    {
                        return false;
                    }
                }
                else
                {
                    _watermarks[partition] = retainedEntry;
                }

                RetainUnassignedWatermarkSnapshot(partition, retainedEntry, _assignmentSnapshot);
                return true;
            }

            if (_watermarks.TryGetValue(partition, out var entry))
            {
                entry.Update(
                    low,
                    high,
                    lagEndOffset,
                    GetMinimumFetchBufferEpoch(partition),
                    leaderEpoch,
                    watermarkUpdateSequence);
                return true;
            }

            _watermarks.TryAdd(
                partition,
                new WatermarkCacheEntry(
                    low,
                    high,
                    lagEndOffset,
                    GetMinimumFetchBufferEpoch(partition),
                    leaderEpoch,
                    watermarkUpdateSequence,
                    topicId));
            return true;
        }
    }

    // Must run under _snapshotStateGate. Every unassigned cache entry owns a current
    // ticket. Refreshes replace the entry, so stale tickets cannot remove newer values.
    private void RetainUnassignedWatermarkSnapshot(
        TopicPartition partition,
        WatermarkCacheEntry entry,
        TopicPartitionSet assignmentSnapshot)
    {
        entry.AdvanceMinimumFetchBufferEpoch(GetMinimumFetchBufferEpoch(partition));
        var retained = _retainedWatermarkSnapshots ??=
            new Queue<RetainedWatermarkSnapshot>(MaxRetainedUnassignedWatermarkSnapshots + 1);
        retained.Enqueue(new RetainedWatermarkSnapshot(partition, entry));

        while (retained.Count > MaxRetainedUnassignedWatermarkSnapshots)
        {
            var expired = retained.Dequeue();
            if (!assignmentSnapshot.Contains(expired.Partition))
            {
                _watermarks.TryRemove(
                    new KeyValuePair<TopicPartition, WatermarkCacheEntry>(
                        expired.Partition,
                        expired.Entry));
            }
        }
    }

    private readonly record struct RetainedWatermarkSnapshot(
        TopicPartition Partition,
        WatermarkCacheEntry Entry);

    /// <inheritdoc />
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _consumerDisposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaConsumer<TKey, TValue>));

        // Fast path: already initialized (volatile read provides acquire semantics)
        if (_initialized)
            return;

        using var apiTimeout = new ApiTimeoutScope(_options.DefaultApiTimeoutMs, cancellationToken);
        try
        {
            await SemaphoreHelper.AcquireOrThrowDisposedAsync(
                _initLock,
                nameof(KafkaConsumer<TKey, TValue>),
                apiTimeout.Token).ConfigureAwait(false);
            try
            {
                // Double-check after acquiring lock
                if (_initialized)
                    return;

                await _metadataManager.InitializeAsync(apiTimeout.Token).ConfigureAwait(false);
                await _telemetryManager.StartAsync(apiTimeout.Token).ConfigureAwait(false);
                _initialized = true;
            }
            finally
            {
                SemaphoreHelper.ReleaseSafely(_initLock);
            }
        }
        catch (OperationCanceledException ex) when (apiTimeout.DefaultTimeoutExpired)
        {
            throw apiTimeout.CreateTimeoutException(nameof(InitializeAsync), ex);
        }
    }

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> if the consumer has not been initialized.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfNotInitialized()
    {
        if (!_initialized)
            ThrowNotInitialized();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowNotInitialized()
    {
        throw new InvalidOperationException(
            "Call InitializeAsync() or use BuildAsync() before consuming messages.");
    }

    /// <summary>
    /// Refreshes the subscription topics based on the current topic filter.
    /// Rate-limited to avoid excessive metadata requests (30 second interval).
    /// </summary>
    /// <returns>True if the subscription changed.</returns>
    private async ValueTask<bool> RefreshFilteredTopicsAsync(Func<string, bool> filter, CancellationToken cancellationToken)
    {
        if (!IsFilterRefreshDue())
            return false;

        var now = Dekaf.MonotonicClock.GetMilliseconds();

        Volatile.Write(ref _lastFilterRefreshTicks, now);

        // Refresh metadata to get all topics (null = all topics)
        try
        {
            await _metadataManager.RefreshMetadataAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!IsFatalFilterRefreshError(ex))
        {
            // This refresh is best-effort and runs on the foreground poll: a cluster-wide blip
            // must not throw a transport failure out of ConsumeAsync. The caller's cancellation
            // wins over a failure that raced it.
            cancellationToken.ThrowIfCancellationRequested();

            // An established subscription stays usable until the next regular refresh. Without
            // one the consumer has nothing to fetch, so try again after a retry backoff.
            var currentCount = _subscriptionSnapshot.Count;
            long retryMs = currentCount == 0 ? _options.RetryBackoffMaxMs : FilterRefreshIntervalMilliseconds;
            Volatile.Write(ref _lastFilterRefreshTicks, now - FilterRefreshIntervalMilliseconds + retryMs);
            LogPatternSubscriptionRefreshFailed(ex, currentCount, retryMs);
            return false;
        }

        var allTopics = _metadataManager.Metadata.GetTopics();
        var changed = false;

        // Build new subscription from matching topics
        var newTopics = new HashSet<string>();
        foreach (var topic in allTopics)
        {
            // Skip internal topics (e.g., __consumer_offsets, __transaction_state)
            if (topic.IsInternal)
            {
                continue;
            }

            if (filter(topic.Name))
            {
                newTopics.Add(topic.Name);
            }
        }

        // Check if subscription changed (use volatile snapshot — already published, avoids allocation)
        var currentKeys = _subscriptionSnapshot;
        if (newTopics.Count != currentKeys.Count || !newTopics.SetEquals(currentKeys))
        {
            _subscription.Clear();
            foreach (var topic in newTopics)
            {
                _subscription.TryAdd(topic, 0);
            }
            PublishSubscriptionSnapshot();
            changed = true;

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                var topics = string.Join(", ", _subscription.Keys);
                LogPatternSubscriptionMatched(_subscription.Count, topics);
            }
        }

        return changed;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask RecordPollAsync(CancellationToken cancellationToken)
        => _coordinator is { } coordinator
            ? coordinator.RecordPollAsync(cancellationToken)
            : ValueTask.CompletedTask;

    internal async ValueTask EnsureAssignmentForPollAsync(CancellationToken cancellationToken)
    {
        var coordinator = _coordinator;
        coordinator?.BeginForegroundPollActivity();
        try
        {
            var assignment = EnsureAssignmentAsync(cancellationToken);
            if (assignment.IsCompletedSuccessfully)
            {
                assignment.GetAwaiter().GetResult();
                return;
            }

            // This helper is also reached by buffered asynchronous deserializers.
            // Record only an actual assignment wait, never a clock read per record.
            _telemetryMetricCollector.StandardMetrics?.BeginPollWait();
            try
            {
                await assignment.ConfigureAwait(false);
            }
            finally
            {
                _telemetryMetricCollector.StandardMetrics?.EndPollWait();
            }
        }
        finally
        {
            coordinator?.EndForegroundPollActivity();
        }
    }

    internal async ValueTask DelayForForegroundPollAsync(int milliseconds, CancellationToken cancellationToken)
    {
        var coordinator = _coordinator;
        coordinator?.BeginForegroundPollActivity();
        _telemetryMetricCollector.StandardMetrics?.BeginPollWait();
        try
        {
            await Task.Delay(milliseconds, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            coordinator?.EndForegroundPollActivity();
            _telemetryMetricCollector.StandardMetrics?.EndPollWait();
        }
    }

    internal async ValueTask EnsureAssignmentAsync(CancellationToken cancellationToken)
    {
        // Refresh pattern subscription BEFORE acquiring the lock — RefreshFilteredTopicsAsync
        // only touches thread-safe structures (ConcurrentDictionary, volatile snapshots,
        // MetadataManager with its own locking) and involves a network call that would block
        // both the consume loop and prefetch loop if done under _assignmentLock.
        // Capture to local to avoid TOCTOU: Subscribe() can set _topicFilter = null concurrently.
        var topicFilter = _topicFilter;
        if (topicFilter is not null)
        {
            await RefreshFilteredTopicsAsync(topicFilter, cancellationToken).ConfigureAwait(false);
        }

        var coordinator = _coordinator;
        // Before the subscription: a subscription change after this read makes the generation stale.
        var subscriptionGeneration = coordinator?.SubscriptionGeneration ?? 0;
        var subscriptionSnapshot = _subscriptionSnapshot;
        var topicPattern = _topicPattern;
        if ((subscriptionSnapshot.Count != 0 || topicPattern is not null) && coordinator is not null)
        {
            BeforeEnsureActiveGroupForTest?.Invoke();
            await coordinator.EnsureActiveGroupAsync(
                    subscriptionSnapshot,
                    topicPattern,
                    subscriptionGeneration,
                    cancellationToken)
                .ConfigureAwait(false);

            if (IsCoordinatorAssignmentSyncCurrent(coordinator, out var coordinatorAssignmentVersion))
            {
                coordinator.AcknowledgeAssignmentSync(coordinatorAssignmentVersion);
                return;
            }
        }
        else if (IsManualAssignmentEnsureCurrent())
        {
            return;
        }

        // Serialize the write path: both ConsumeAsync and PrefetchLoopAsync call this method
        // concurrently. Without synchronization, concurrent access to non-thread-safe
        // _assignment HashSet causes NullReferenceException during enumeration.
        // Readers use the volatile _assignmentSnapshot instead of acquiring this lock.
        var rejoinRequired = false;
        while (true)
        {
            // Position initialization found the member had left the group. Rejoin here, outside
            // the assignment lock: the rejoin delivers rebalance callbacks, and a callback's seek
            // takes that lock.
            if (rejoinRequired && coordinator is not null)
            {
                rejoinRequired = false;
                subscriptionGeneration = coordinator.SubscriptionGeneration;
                subscriptionSnapshot = _subscriptionSnapshot;
                topicPattern = _topicPattern;
                if (subscriptionSnapshot.Count != 0 || topicPattern is not null)
                {
                    await coordinator.EnsureActiveGroupAsync(
                            subscriptionSnapshot,
                            topicPattern,
                            subscriptionGeneration,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            // A new assignment is synchronized only after its OnPartitionsAssigned has run, so a
            // seek the callback stages is applied before fetching starts. Waited for before the
            // assignment lock: the callback's seek takes that lock.
            if (coordinator is not null)
                await coordinator.WaitForAssignmentCallbacksAsync(cancellationToken).ConfigureAwait(false);

            await SemaphoreHelper.AcquireOrThrowDisposedAsync(_assignmentLock, nameof(KafkaConsumer<TKey, TValue>), cancellationToken).ConfigureAwait(false);
            (ConsumerCoordinator Coordinator, HashSet<TopicPartition> Partitions)? unacknowledgedCoordinatorRevocations = null;
            try
            {
                coordinator = _coordinator;
                if ((!_subscription.IsEmpty || topicPattern is not null) && coordinator is not null)
                {
                    // A newer assignment was published after the wait above: wait for its
                    // callbacks too before taking the snapshot.
                    if (coordinator.HasPendingAssignmentCallbacks())
                        continue;

                    BeforeCoordinatorAssignmentSnapshotForTest?.Invoke();
                    var (
                        coordinatorAssignment,
                        coordinatorAssignmentVersion,
                        coordinatorRevocations,
                        newlyExpandedPartitions,
                        coordinatorRevocationSequences) =
                        await coordinator.GetAssignmentSnapshotAndDrainRevocationsAsync(cancellationToken)
                            .ConfigureAwait(false);
                    if (coordinatorRevocationSequences is not null)
                    {
                        foreach (var drained in coordinatorRevocationSequences)
                        {
                            if (!_drainedRevocationSequences.TryGetValue(drained.Key, out var current)
                                || current < drained.Value)
                            {
                                _drainedRevocationSequences[drained.Key] = drained.Value;
                            }
                        }
                    }
                    var drainedRevocations = coordinatorRevocations is null ? null : coordinator.DrainedRevocationGenerations;
                    if (coordinatorRevocations is not null)
                    {
                        unacknowledgedCoordinatorRevocations = (coordinator, coordinatorRevocations);
                    }

                    // A broker can classify expanded partitions even when the client has not
                    // configured a distinct reset policy. Acknowledge that advisory metadata,
                    // but keep initialized positions and buffered data untouched.
                    if (_options.AutoOffsetResetNewPartitions is null
                        && newlyExpandedPartitions.Count != 0)
                    {
                        coordinator.AcknowledgeInitializedPartitions(
                            newlyExpandedPartitions,
                            coordinatorAssignmentVersion);
                        newlyExpandedPartitions = [];
                    }

                    // Set equality alone is insufficient: the assignment can change away and back
                    // between polls. Unseen revocations require stale-fetch cleanup and position reset.
                    if (_assignment.SetEquals(coordinatorAssignment)
                        && coordinatorRevocations is null
                        && newlyExpandedPartitions.Count == 0
                        && HasInitializedFetchPositions(coordinatorAssignment))
                    {
                        if (coordinator.AssignmentVersion != coordinatorAssignmentVersion)
                            continue;

                        Volatile.Write(ref _lastCoordinatorAssignmentVersion, coordinatorAssignmentVersion);
                        if (coordinator.AcknowledgeAssignmentSync(coordinatorAssignmentVersion))
                            CompleteAcknowledgedSync();
                        return;
                    }

                    // Check for new partitions that need initialization
                    List<TopicPartition>? newPartitions = null;
                    List<TopicPartition>? reclassifiedPartitions = null;
                    foreach (var partition in coordinatorAssignment)
                    {
                        var wasAssigned = _assignment.Contains(partition);
                        var hadFetchPosition = _fetchPositions.ContainsKey(partition);
                        var wasNewlyExpanded = newlyExpandedPartitions.Contains(partition);
                        if (!wasAssigned || !hadFetchPosition || wasNewlyExpanded)
                        {
                            newPartitions ??= new List<TopicPartition>();
                            newPartitions.Add(partition);

                            if (wasAssigned && hadFetchPosition && wasNewlyExpanded)
                            {
                                reclassifiedPartitions ??= new List<TopicPartition>();
                                reclassifiedPartitions.Add(partition);
                            }
                        }
                    }

                    // Check for partitions that were removed (for EOF state cleanup)
                    List<TopicPartition>? removedPartitions = null;
                    foreach (var partition in _assignment)
                    {
                        if (!coordinatorAssignment.Contains(partition))
                        {
                            removedPartitions ??= new List<TopicPartition>();
                            removedPartitions.Add(partition);
                        }
                    }

                    if (coordinatorRevocations is not null)
                    {
                        foreach (var partition in coordinatorRevocations)
                        {
                            if (removedPartitions is null || !removedPartitions.Contains(partition))
                                (removedPartitions ??= []).Add(partition);

                            if (!coordinatorAssignment.Contains(partition))
                                continue;

                            if (newPartitions is null || !newPartitions.Contains(partition))
                                (newPartitions ??= []).Add(partition);
                        }
                    }

                    if (newPartitions is { Count: > 0 })
                        LogPartitionsAdded(newPartitions.Count);
                    if (removedPartitions is { Count: > 0 })
                        LogPartitionsRemoved(removedPartitions.Count);

                    // A seek staged for a partition that was revoked and then assigned again
                    // came from the new assignment's OnPartitionsAssigned: publishing the
                    // revocation dropped any seek staged before it, and StageRebalanceSeek
                    // discards one from a callback that predates the revocation. The cleanup below
                    // leaves it in place so position initialization applies it. It is never
                    // removed and put back, so a revocation published meanwhile drops it for good.
                    HashSet<TopicPartition>? reassignedPartitions = null;
                    if (coordinatorRevocations is not null)
                    {
                        foreach (var partition in coordinatorRevocations)
                        {
                            if (coordinatorAssignment.Contains(partition))
                                (reassignedPartitions ??= []).Add(partition);
                        }
                    }

                    BeforeRevokedPartitionStateCleanupForTest?.Invoke(this);

                    // Invalidate in-flight fetches before publishing an ABA reassignment or
                    // reinitializing its position. The consume loop owns the actual buffer drain.
                    if (removedPartitions is not null)
                        QueueCoordinatorRevokedPartitionsForFetchClear(removedPartitions, reassignedPartitions);
                    if (reclassifiedPartitions is not null)
                    {
                        // Reclassification keeps ownership: its staged seek and pause marker stay.
                        var retained = new HashSet<TopicPartition>(reclassifiedPartitions);
                        if (reassignedPartitions is not null)
                            retained.UnionWith(reassignedPartitions);
                        QueueCoordinatorRevokedPartitionsForFetchClear(reclassifiedPartitions, retained);
                    }

                    // Update assignment from coordinator
                    _assignment.Clear();
                    foreach (var partition in coordinatorAssignment)
                    {
                        _assignment.Add(partition);
                    }
                    lock (_snapshotStateGate)
                    {
                        PublishAssignmentSnapshotCore(newPartitions);
                        AfterAssignmentSyncPublishedForTest?.Invoke(this);

                        // Position initialization replaces whatever was stored for a partition
                        // before this ownership; drop it now so a commit before initialization
                        // cannot send an offset a Seek or StoreOffset left while it was unowned.
                        if (newPartitions is not null)
                        {
                            foreach (var partition in newPartitions)
                                ClearStoredOffset(partition);
                        }

                        // Clean up state for removed partitions while lag-query cache publication
                        // is excluded from the assignment transition.
                        if (removedPartitions is not null
                            && RemovePartitionState(removedPartitions, reassignedPartitions))
                            PublishPausedSnapshot();

                        // Last: the drained revocations stay pending until the old ownership's
                        // stored offsets and positions are gone. Stored-offset commits decide
                        // ownership under this same gate, so they see either the revocation
                        // pending or the cleaned state, never the stale state as owned.
                        if (drainedRevocations is not null)
                            ForgetDrainedRevocations(drainedRevocations);
                    }

                    InvalidatePartitionCache();
                    InvalidateFetchRequestCache();

                    // Ratchet pool sizes based on actual partition count
                    RatchetConsumerPoolSizes(_assignment.Count);

                    // Initialize positions for new partitions
                    if (newPartitions is { Count: > 0 })
                    {
                        try
                        {
                            await InitializePositionsAsync(
                                    newPartitions,
                                    newlyExpandedPartitions,
                                    coordinatorAssignmentVersion,
                                    cancellationToken)
                                .ConfigureAwait(false);
                        }
                        catch (GroupRejoinRequiredException)
                        {
                            // The committed-offset fetch was fenced. Its recovery would rejoin
                            // and run rebalance callbacks under this lock, so release the lock
                            // and rejoin at the top of the loop instead.
                            rejoinRequired = true;
                            continue;
                        }
                    }

                    // A heartbeat can publish a newer assignment while positions initialize.
                    // Synchronize the latest snapshot before publishing this pass as current.
                    if (coordinator.AssignmentVersion != coordinatorAssignmentVersion)
                        continue;

                    BeforeAssignmentSyncAcknowledgedForTest?.Invoke();
                    Volatile.Write(ref _lastCoordinatorAssignmentVersion, coordinatorAssignmentVersion);
                    // A newer version published after the check above supersedes this pass: its
                    // callback state stays pending for the pass that synchronizes that version.
                    if (coordinator.AcknowledgeAssignmentSync(coordinatorAssignmentVersion))
                    {
                        AfterAssignmentSyncAcknowledgedForTest?.Invoke();
                        CompleteAcknowledgedSync();
                    }
                    unacknowledgedCoordinatorRevocations = null;
                }
                else
                {
                    if (_assignment.Count > 0)
                    {
                        // Ratchet pool sizes based on actual partition count (manual assignment)
                        RatchetConsumerPoolSizes(_assignment.Count);

                        // Manual assignment - initialize positions for partitions that don't have positions yet
                        List<TopicPartition>? uninitializedPartitions = null;
                        foreach (var p in _assignment)
                        {
                            if (!_fetchPositions.ContainsKey(p))
                            {
                                uninitializedPartitions ??= new List<TopicPartition>();
                                uninitializedPartitions.Add(p);
                            }
                        }

                        if (uninitializedPartitions is not null)
                        {
                            await InitializeManualAssignmentPositionsAsync(uninitializedPartitions, cancellationToken).ConfigureAwait(false);
                        }
                    }

                    Volatile.Write(ref _lastManualAssignmentEnsureVersion, Volatile.Read(ref _assignmentEnsureVersion));
                }
            }
            finally
            {
                // Position initialization and assignment-version publication acknowledge the drain.
                // Restore on failure so the next sync repeats revocation cleanup and initialization.
                if (unacknowledgedCoordinatorRevocations is { } revocations)
                    revocations.Coordinator.RestoreRevokedPartitionsSinceLastSync(revocations.Partitions);

                SemaphoreHelper.ReleaseSafely(_assignmentLock);
            }

            return;
        }
    }

    private bool IsManualAssignmentEnsureCurrent()
    {
        // Read the published snapshot, not _subscription: ConcurrentDictionary.IsEmpty
        // acquires every stripe lock, which dominated the buffered ConsumeOne fast path
        // (~45% of per-poll CPU, issue #2211). The snapshot is republished after every
        // subscription mutation, and CanUseBufferedConsumeOneFastPath already keys its
        // subscription-vs-manual routing off the same snapshot.
        if (_topicFilter is not null || _topicPattern is not null || _subscriptionSnapshot.Count != 0)
            return false;

        var assignmentEnsureVersion = Volatile.Read(ref _assignmentEnsureVersion);
        return Volatile.Read(ref _lastManualAssignmentEnsureVersion) == assignmentEnsureVersion;
    }

    private bool HasInitializedFetchPositions(TopicPartitionSet partitions)
    {
        foreach (var partition in partitions)
        {
            if (!_fetchPositions.ContainsKey(partition))
                return false;
        }

        return true;
    }

    private async ValueTask InitializeManualAssignmentPositionsAsync(List<TopicPartition> partitions, CancellationToken cancellationToken)
    {
        // For manual assignment without a group, use auto offset reset to determine starting position
        foreach (var partition in partitions)
        {
            var offset = await GetResetOffsetAsync(partition, cancellationToken).ConfigureAwait(false);
            SetPosition(partition, offset, dirty: false);
            ClearLastConsumedLeaderEpoch(partition);
            SetFetchPosition(partition, offset);
        }
    }

    private async ValueTask InitializePositionsAsync(
        List<TopicPartition> partitions,
        HashSet<TopicPartition> newlyExpandedPartitions,
        int assignmentVersion,
        CancellationToken cancellationToken)
    {
        var coordinator = _coordinator!;
        var cacheGeneration = Interlocked.Increment(ref _committedOffsetGeneration);
        List<TopicPartition>? initializedNewPartitions = null;
        try
        {
            // Fetch committed offsets for all partitions
            // Runs under the assignment lock, so a fenced fetch must not rejoin (and run rebalance
            // callbacks) here; EnsureAssignmentAsync rejoins once the lock is released.
            var committedOffsets = await coordinator.FetchOffsetsAsync(
                    partitions,
                    rejoinOnMembershipLoss: false,
                    cancellationToken)
                .ConfigureAwait(false);

            foreach (var partition in partitions)
            {
                var isNewlyExpanded = newlyExpandedPartitions.Contains(partition);
                if (committedOffsets.TryGetValue(partition, out var committedOffset) && committedOffset.Offset >= 0)
                {
                    // Use committed offset
                    SetPosition(partition, committedOffset.Offset, dirty: false);
                    if (committedOffset.LeaderEpoch >= 0)
                        SetLastConsumedLeaderEpoch(partition, committedOffset.LeaderEpoch);
                    else
                        ClearLastConsumedLeaderEpoch(partition);
                    SetFetchPosition(partition, committedOffset.Offset);
                    _ = TryCacheCommittedOffset(partition, committedOffset.Offset, cacheGeneration);
                }
                else
                {
                    // No committed offset, use auto offset reset
                    var offset = await GetResetOffsetAsync(
                            partition,
                            cancellationToken,
                            isNewlyExpanded)
                        .ConfigureAwait(false);
                    SetPosition(partition, offset, dirty: false);
                    ClearLastConsumedLeaderEpoch(partition);
                    SetFetchPosition(partition, offset);
                }

                ApplyPendingRebalanceSeek(partition, untilSyncAcknowledged: true);

                if (isNewlyExpanded)
                    (initializedNewPartitions ??= []).Add(partition);
            }
        }
        finally
        {
            if (initializedNewPartitions is not null)
                coordinator.AcknowledgeInitializedPartitions(initializedNewPartitions, assignmentVersion);
        }
    }

    /// <param name="untilSyncAcknowledged">
    /// Assignment sync passes true: the seek stays staged until the sync is acknowledged
    /// (<see cref="CompleteAcknowledgedSync"/>), so a retried pass applies it again.
    /// </param>
    private void ApplyPendingRebalanceSeek(TopicPartition partition, bool untilSyncAcknowledged = false)
    {
        TopicPartitionOffset offset;
        if (untilSyncAcknowledged)
        {
            if (!_pendingRebalanceSeeks.TryGetValue(partition, out offset))
                return;
            _unacknowledgedAppliedRebalanceSeeks[partition] = offset;
        }
        else if (!_pendingRebalanceSeeks.TryRemove(partition, out offset))
        {
            return;
        }

        // Keep invalidation, buffer drain, and position replacement atomic with prefetch publication.
        // The immediate path can run after this partition has already prefetched records.
        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
        {
            ClearFetchBufferForPartitions([partition]);

            if (offset.LeaderEpoch >= 0)
                SetLastConsumedLeaderEpoch(partition, offset.LeaderEpoch);
            else
                ClearLastConsumedLeaderEpoch(partition);
            SetPosition(partition, offset.Offset, dirty: true);
            SetFetchPosition(partition, offset.Offset);
            _eofEmitted.TryRemove(partition, out _);
        }
    }

    /// <summary>
    /// Drops the revocation bookkeeping on abandon: entries a sync drained are pruned on the
    /// coordinator too (unless revoked again since). Caller holds <c>_assignmentLock</c>. Runs per
    /// abandon, never per message.
    /// </summary>
    private void ForgetRevocationSequences()
    {
        if (_coordinator is { } coordinator)
        {
            foreach (var drained in _drainedRevocationSequences)
                coordinator.PruneRevocationSequence(drained.Key, drained.Value);
            foreach (var acknowledged in _acknowledgedRevocationSequences)
                coordinator.PruneRevocationSequence(acknowledged.Key, acknowledged.Value);
            coordinator.ForgetQueuedRevocationSequences();
        }

        _drainedRevocationSequences.Clear();
        _acknowledgedRevocationSequences.Clear();
    }

    /// <summary>
    /// The coordinator confirmed this sync pass: the acknowledged assignment and revocation
    /// sequences advance, and the staged seeks it applied are no longer pending. Removed only
    /// while unchanged, so a seek a later callback staged for the same partition is kept. Caller
    /// holds <c>_assignmentLock</c>. Runs per acknowledged sync, never per message.
    /// </summary>
    private void CompleteAcknowledgedSync()
    {
        _acknowledgedCoordinatorAssignment = _assignmentSnapshot;

        // Every revocation drained so far is synchronized now (an acknowledgement requires the
        // queue to be empty): the acknowledged assignment is the ownership that followed it. A
        // revocation recorded since carries a newer sequence and stays uncovered.
        // Covered revocations are forgotten on both sides (absent reads as 0, which answers the
        // same), so this bookkeeping stays bounded by revocations not yet acknowledged.
        var coordinator = _coordinator;
        foreach (var drained in _drainedRevocationSequences)
        {
            if (coordinator is not null && coordinator.GetLastRevocationSequence(drained.Key) <= drained.Value)
            {
                coordinator.PruneRevocationSequence(drained.Key, drained.Value);
                _acknowledgedRevocationSequences.Remove(drained.Key);
            }
            else if (!_acknowledgedRevocationSequences.TryGetValue(drained.Key, out var acknowledged)
                || acknowledged < drained.Value)
            {
                // Revoked again since: keep what was covered until a later sync covers the rest.
                _acknowledgedRevocationSequences[drained.Key] = drained.Value;
            }
        }

        _drainedRevocationSequences.Clear();
        if (_unacknowledgedAppliedRebalanceSeeks.Count == 0)
            return;

        var pending = (ICollection<KeyValuePair<TopicPartition, TopicPartitionOffset>>)_pendingRebalanceSeeks;
        foreach (var applied in _unacknowledgedAppliedRebalanceSeeks)
            pending.Remove(applied);

        _unacknowledgedAppliedRebalanceSeeks.Clear();
    }

    private async ValueTask<long> GetResetOffsetAsync(
        TopicPartition partition,
        CancellationToken cancellationToken,
        bool isNewPartition = false)
    {
        var timestamp = AutoOffsetResetStrategy.GetListOffsetsTimestamp(
            _options,
            DateTimeOffset.UtcNow,
            partition,
            isNewPartition);
        return await ResolveAutoResetOffsetAsync(partition, timestamp, cancellationToken).ConfigureAwait(false);
    }

    private string GetAutoOffsetResetName() =>
        _options.AutoOffsetReset == AutoOffsetReset.ByDuration
            ? $"by_duration:{_options.AutoOffsetResetDuration}"
            : _options.AutoOffsetReset.ToString().ToLowerInvariant();

    private async ValueTask<long> ResolveAutoResetOffsetAsync(
        TopicPartition partition,
        long timestamp,
        CancellationToken cancellationToken)
    {
        var offset = await ResolveOffsetAsync(partition, timestamp, cancellationToken).ConfigureAwait(false);
        if (timestamp >= 0 && offset == TopicPartitionTimestamp.Latest)
        {
            return await ResolveOffsetAsync(partition, TopicPartitionTimestamp.Latest, cancellationToken)
                .ConfigureAwait(false);
        }

        return offset;
    }

    private async ValueTask ResolveSpecialOffsetsAsync(
        List<TopicPartition> partitions, int startIndex, int count, CancellationToken cancellationToken)
    {
        // Check for partitions with special offset values (-1 for end, -2 for beginning)
        // and resolve them to actual offsets using ListOffsets
        var endIndex = startIndex + count;
        for (var i = startIndex; i < endIndex; i++)
        {
            var partition = partitions[i];
            if (!_fetchPositions.TryGetValue(partition, out var fetchPosition))
                continue;

            if (fetchPosition == -1 || fetchPosition == -2)
            {
                // -1 = latest, -2 = earliest
                var resolvedOffset = await ResolveOffsetAsync(partition, fetchPosition, cancellationToken).ConfigureAwait(false);
                SetFetchPosition(partition, resolvedOffset);
                SetPosition(partition, resolvedOffset, dirty: false);
                ClearLastConsumedLeaderEpoch(partition);
            }
        }
    }

    private ValueTask<long> ResolveOffsetAsync(TopicPartition partition, long timestamp, CancellationToken cancellationToken)
    {
        return RetryHelper.WithRetryAsync(async () =>
        {
            var connectionLease = await GetPartitionLeaderControlConnectionAsync(partition, cancellationToken)
                .ConfigureAwait(false);
            if (connectionLease is null)
                throw CreateOffsetResolutionUnavailableException(partition);
            using var lease = connectionLease.Value;
            var connection = lease.Connection;

            var listOffsetsVersion = _metadataManager.GetNegotiatedApiVersion(
                connection,
                ApiKey.ListOffsets,
                ListOffsetsRequest.LowestSupportedVersion,
                ListOffsetsRequest.HighestSupportedVersion);

            var request = new ListOffsetsRequest
            {
                ReplicaId = -1,
                IsolationLevel = _options.IsolationLevel,
                Topics =
                [
                    new ListOffsetsRequestTopic
                    {
                        Name = partition.Topic,
                        Partitions =
                        [
                            new ListOffsetsRequestPartition
                            {
                                PartitionIndex = partition.Partition,
                                Timestamp = timestamp,
                                CurrentLeaderEpoch = GetCurrentLeaderEpoch(partition)
                            }
                        ]
                    }
                ]
            };

            ListOffsetsResponse response;
            try
            {
                response = await connection.SendWithClientTelemetryAsync<ListOffsetsRequest, ListOffsetsResponse>(
                    request,
                    listOffsetsVersion, _telemetryMetricCollector,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException ex)
            {
                throw new KafkaException(
                    ErrorCode.RequestTimedOut,
                    $"ListOffsets request timed out for {partition}.",
                    ex);
            }

            ListOffsetsResponsePartition? partitionResponse = null;
            foreach (var topic in response.Topics)
            {
                if (topic.Name == partition.Topic)
                {
                    foreach (var p in topic.Partitions)
                    {
                        if (p.PartitionIndex == partition.Partition)
                        {
                            partitionResponse = p;
                            break;
                        }
                    }
                    break;
                }
            }

            if (partitionResponse is not null && partitionResponse.ErrorCode != ErrorCode.None)
            {
                throw new Errors.ConsumeException(partitionResponse.ErrorCode,
                    $"ListOffsets failed for {partition}: {partitionResponse.ErrorCode}");
            }

            if (partitionResponse is null)
            {
                throw new KafkaException(
                    ErrorCode.UnknownTopicOrPartition,
                    $"ListOffsets response did not contain {partition}.");
            }

            return partitionResponse.Offset;
        }, _metadataManager, cancellationToken, _options.RetryBackoffMs, _options.RetryBackoffMaxMs,
            // Position initialization and offset reset run on the application's poll with no
            // aggregate deadline of their own. A leader that refuses connections while cluster
            // metadata still names it is retried for one request timeout, and the final error is
            // typed, instead of three quick attempts ending in a raw socket exception.
            deadline: OffsetLookupDeadline(
                "ListOffsets",
                TimeSpan.FromMilliseconds(_options.RequestTimeoutMs)));
    }

    // Deadline-mode retry (see RetryDeadline) for idempotent offset lookups.
    private RetryDeadline OffsetLookupDeadline(string operation, TimeSpan budget) =>
        new(operation, budget, () => Volatile.Read(ref _consumerDisposed) != 0);

    internal static KafkaException CreateOffsetResolutionUnavailableException(TopicPartition partition) =>
        new(
            ErrorCode.LeaderNotAvailable,
            $"No partition leader connection is available to resolve the offset for {partition}.");

    private async ValueTask<KafkaConnectionLease?> GetPartitionLeaderControlConnectionAsync(
        TopicPartition partition,
        CancellationToken cancellationToken)
    {
        var leader = await _metadataManager.GetPartitionLeaderAsync(partition.Topic, partition.Partition, cancellationToken)
            .ConfigureAwait(false);

        if (leader is null)
            return null;

        // Keep offset-control requests off fetch connections. A delayed fetch response must not
        // block assignment position initialization or watermark queries during a rebalance.
        var connectionCount = Volatile.Read(ref _appliedConnectionCount);
        var connectionIndex = ConsumerCoordinator.GetCoordinationConnectionIndex(connectionCount);
        return await _connectionPool.LeaseConnectionByIndexAsync(leader.NodeId, connectionIndex, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask FetchRecordsAsync(CancellationToken cancellationToken)
    {
        // Rent a CTS from the pool to avoid allocating a LinkedCTS
        var consumeCts = _ctsPool.Rent();
        _activeConsumeCancellationSources.TryAdd(consumeCts, 0);

        try
        {
            _coordinator?.BeginForegroundPollActivity();
            _telemetryMetricCollector.StandardMetrics?.BeginPollWait();

            // Forward outer cancellation into the pooled CTS via registration
            // instead of allocating a LinkedCTS (matches the prefetch path pattern)
            using var reg = cancellationToken.CanBeCanceled
                ? cancellationToken.Register(static s => ((CancellationTokenSource)s!).Cancel(), consumeCts)
                : default;

            // Close any race window: if token was cancelled between method entry and registration
            if (cancellationToken.IsCancellationRequested)
                consumeCts.Cancel();

            var pausedSnapshotVersion = Volatile.Read(ref _pausedSnapshotVersion);
            var partitionsByBroker = await GroupPartitionsByBrokerAsync(cancellationToken).ConfigureAwait(false);
            var fetchBufferEpoch = Volatile.Read(ref _fetchBufferEpoch);
            var fetchSessionSnapshot = ShouldUseFetchSessions && !_fetchSessions.IsEmpty
                ? _fetchSessions.ToArray()
                : Array.Empty<KeyValuePair<(int BrokerId, int ConnectionIndex), FetchSessionHandler>>();

            // If all partitions are paused, delay to prevent tight spin loop
            // that would starve timeout/cancellation mechanisms of CPU time
            var brokerCount = partitionsByBroker.Count;
            if (brokerCount == 0 && fetchSessionSnapshot.Length == 0)
            {
                await DelayPausedDirectFetchAsync(
                    pausedSnapshotVersion,
                    consumeCts,
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            // Fetch from all brokers in parallel for maximum throughput
            // Use pooled array to avoid allocation per fetch cycle
            var fetchTasks = ArrayPool<Task<List<PendingFetchData>?>>.Shared.Rent(brokerCount + fetchSessionSnapshot.Length);
            try
            {
                var taskCount = 0;
                HashSet<int>? scheduledFetchSessionBrokers = fetchSessionSnapshot.Length == 0 ? null : [];
                foreach (var (brokerId, partitions) in partitionsByBroker)
                {
                    scheduledFetchSessionBrokers?.Add(brokerId);
                    fetchTasks[taskCount++] = FetchFromBrokerWithErrorHandlingAsync(
                        brokerId,
                        partitions,
                        fetchBufferEpoch,
                        consumeCts.Token,
                        consumeCts.Token);
                }

                foreach (var (key, handler) in fetchSessionSnapshot)
                {
                    if (!handler.HasActiveSession)
                    {
                        _fetchSessions.TryRemove(key, out _);
                        continue;
                    }

                    if (key.ConnectionIndex != 0)
                    {
                        _fetchSessions.TryRemove(key, out _);
                        continue;
                    }

                    if (scheduledFetchSessionBrokers is not null && scheduledFetchSessionBrokers.Contains(key.BrokerId))
                        continue;

                    fetchTasks[taskCount++] = FetchFromBrokerWithErrorHandlingAsync(
                        key.BrokerId,
                        [],
                        fetchBufferEpoch,
                        consumeCts.Token,
                        consumeCts.Token);
                }

                if (taskCount == 0)
                {
                    await DelayPausedDirectFetchAsync(
                        pausedSnapshotVersion,
                        consumeCts,
                        cancellationToken).ConfigureAwait(false);
                    return;
                }

                // Timing wraps the entire parallel fetch cycle (all brokers via Task.WhenAll)
                // rather than individual broker fetches. This avoids a data race on the timing
                // fields from concurrent FetchFromBrokerAsync calls, and correctly measures
                // the bottleneck (slowest broker) which is the right signal for adaptive sizing.
                _adaptiveFetchSizer?.RecordFetchStart();

                try
                {
#if NETSTANDARD2_0
                    await Task.WhenAll(fetchTasks.Take(taskCount)).ConfigureAwait(false);
#else
                    // ReadOnlySpan overload: same zero-copy benefit as above
                    await Task.WhenAll(new ReadOnlySpan<Task<List<PendingFetchData>?>>(fetchTasks, 0, taskCount)).ConfigureAwait(false);
#endif
                }
                catch
                {
                    DisposeCompletedFetchResults(fetchTasks, taskCount);
                    throw;
                }

                _adaptiveFetchSizer?.RecordFetchEnd();

                // Enqueue results from all brokers (now on main thread, safe for Queue)
                for (var j = 0; j < taskCount; j++)
                {
                    var pendingItems = fetchTasks[j].Result;
                    if (pendingItems is not null)
                    {
                        try
                        {
                            foreach (var pending in pendingItems)
                            {
                                if (ShouldDropStaleFetchPartition(pending.TopicPartition, fetchBufferEpoch))
                                {
                                    pending.Dispose();
                                    continue;
                                }

                                // Direct fetches complete before this loop, so no EOF report can
                                // interleave with this publication on the consumer thread.
                                RearmPartitionEofForPublishedRecords(
                                    pending.TopicPartition,
                                    pending.GetBatches().Count > 0,
                                    pending.FetchEndOffsetExclusive);
                                EnqueuePendingFetch(pending);
                            }
                        }
                        finally
                        {
                            ConsumerFetchPools.ReturnPendingFetchDataList(pendingItems);
                        }
                    }
                }
            }
            finally
            {
                ArrayPool<Task<List<PendingFetchData>?>>.Shared.Return(fetchTasks, clearArray: true);
            }
        }
        finally
        {
            _activeConsumeCancellationSources.TryRemove(consumeCts, out _);
            lock (_pausedDirectFetchCancellationSourceLock)
            {
                if (ReferenceEquals(_pausedDirectFetchCancellationSource, consumeCts))
                    _pausedDirectFetchCancellationSource = null;

                consumeCts.Dispose();
            }
            _coordinator?.EndForegroundPollActivity();
            _telemetryMetricCollector.StandardMetrics?.EndPollWait();
        }
    }

    private async ValueTask DelayPausedDirectFetchAsync(
        int pausedSnapshotVersion,
        CancellationTokenSource consumeCts,
        CancellationToken cancellationToken)
    {
        lock (_pausedDirectFetchCancellationSourceLock)
        {
            _pausedDirectFetchCancellationSource = consumeCts;
        }

        // Resume may publish before the waiter is visible. The version check closes
        // that race; a later Resume cancels consumeCts through the published field.
        if (Volatile.Read(ref _pausedSnapshotVersion) != pausedSnapshotVersion)
            return;

#if NET
        var delay = Task.Delay(AllPartitionsPausedDelayMs, consumeCts.Token);
        await delay.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (delay.IsCanceled && (cancellationToken.IsCancellationRequested
            || Volatile.Read(ref _pausedSnapshotVersion) == pausedSnapshotVersion))
        {
            // Propagate caller/shutdown cancellation. Resume only wakes the delay;
            // avoid constructing and throwing an exception for that routine signal.
            await delay.ConfigureAwait(false);
        }
#else
        // The netstandard polyfill suppresses cancellation by catching it. Keep
        // the original await here rather than adding its extra async wrapper.
        try
        {
            await Task.Delay(AllPartitionsPausedDelayMs, consumeCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested
            && Volatile.Read(ref _pausedSnapshotVersion) != pausedSnapshotVersion)
        {
            // Resume is a control-plane wake, not caller cancellation.
        }
#endif
    }

    internal static void DisposeCompletedFetchResults(
        Task<List<PendingFetchData>?>[] fetchTasks,
        int taskCount)
    {
        for (var i = 0; i < taskCount; i++)
        {
            var task = fetchTasks[i];
            if (task.Status != TaskStatus.RanToCompletion || task.Result is not { } pendingItems)
                continue;

            DisposePendingFetches(pendingItems);
            ConsumerFetchPools.ReturnPendingFetchDataList(pendingItems);
        }
    }

    private async Task<List<PendingFetchData>?> FetchFromBrokerWithErrorHandlingAsync(
        int brokerId,
        List<TopicPartition> partitions,
        int fetchBufferEpoch,
        CancellationToken linkedToken,
        CancellationToken consumeCancellationToken)
    {
        try
        {
            return await FetchFromBrokerAsync(
                    brokerId,
                    partitions,
                    fetchBufferEpoch,
                    linkedToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (consumeCancellationToken.IsCancellationRequested)
        {
            // Consume cancellation requested, exit silently
            return null;
        }
        catch (Exception ex) when (IsFatalPrefetchError(ex))
        {
            LogFatalPrefetchError(ex, brokerId);
            throw;
        }
        catch (Exception ex)
        {
            ClearPreferredReadReplicasForBroker(brokerId, partitions);
            LogFetchFromBrokerError(ex, brokerId);
            return null;
        }
    }

    /// <summary>
    /// Publishes an immutable snapshot of <see cref="_assignment"/> for lock-free reads.
    /// Must be called after every mutation to <see cref="_assignment"/>.
    /// Rotates watermark cache generations only for partitions whose ownership changed.
    /// Also marks manual assignment state dirty and invalidates the coordinator assignment
    /// fast path, so all assignment mutation paths must use this method.
    /// </summary>
    private void PublishAssignmentSnapshot() => PublishAssignmentSnapshotCore(reassignedPartitions: null);

    private void PublishAssignmentSnapshotCore(IReadOnlyCollection<TopicPartition>? reassignedPartitions)
    {
        var assignmentSnapshot = new HashSet<TopicPartition>(_assignment);
        lock (_snapshotStateGate)
        {
            var previousAssignment = _assignmentSnapshot;
            var assignmentVersion = Interlocked.Increment(ref _assignmentEnsureVersion);
            // Records fetched up to this generation belong to an earlier ownership of the
            // partitions whose ownership starts here.
            var ownershipStart = PendingFetchData.NextFetchGeneration();

            foreach (var partition in previousAssignment)
            {
                if (assignmentSnapshot.Contains(partition))
                    continue;

                EndOwnership(partition, ownershipStart);
                _watermarkAssignmentVersions.TryRemove(partition, out _);
                if (_watermarks.TryGetValue(partition, out var entry))
                    RetainUnassignedWatermarkSnapshot(partition, entry, assignmentSnapshot);
            }

            foreach (var partition in assignmentSnapshot)
            {
                if (previousAssignment.Contains(partition))
                    continue;

                StartOwnership(partition, ownershipStart);
                _watermarkAssignmentVersions[partition] = assignmentVersion;
                _watermarks.TryRemove(partition, out _);
            }

            if (reassignedPartitions is not null)
            {
                foreach (var partition in reassignedPartitions)
                {
                    if (!assignmentSnapshot.Contains(partition))
                        continue;

                    StartOwnership(partition, ownershipStart);
                    _watermarkAssignmentVersions[partition] = assignmentVersion;
                    _watermarks.TryRemove(partition, out _);
                }
            }

            _batchIterationEpoch.BeginPublication();
            try
            {
                _assignmentSnapshot = assignmentSnapshot;
            }
            finally
            {
                _batchIterationEpoch.EndPublication();
            }

        }
        Volatile.Write(ref _lastCoordinatorAssignmentVersion, -1);
        Volatile.Write(ref _observedTopicIdentityMarker, assignmentSnapshot);
    }

    /// <summary>
    /// Publishes an immutable snapshot of <see cref="_subscription"/> for lock-free reads.
    /// Must be called after every mutation to <see cref="_subscription"/>.
    /// </summary>
    private void PublishSubscriptionSnapshot()
    {
        var snapshot = _subscription.Keys.ToHashSet();
        _subscriptionSnapshot = snapshot;
        _isGroupManaged = _topicFilter is not null || _topicPattern is not null || snapshot.Count != 0;
    }

    /// <summary>
    /// Publishes an immutable snapshot of <see cref="_paused"/> for lock-free reads.
    /// Must be called after every mutation to <see cref="_paused"/>.
    /// </summary>
    private void PublishPausedSnapshot()
    {
        lock (_snapshotStateGate)
        {
            _batchIterationEpoch.BeginPublication();
            try
            {
                _pausedSnapshot = _paused.Keys.ToHashSet();
                Interlocked.Increment(ref _pausedSnapshotVersion);
            }
            finally
            {
                _batchIterationEpoch.EndPublication();
            }
        }
    }

    /// <summary>
    /// Invalidates the cached partition grouping. Called whenever _assignment or _paused changes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InvalidatePartitionCache()
    {
        Interlocked.Increment(ref _assignmentVersion);
        lock (_partitionCacheLock)
        {
            _cachedPartitionsByBroker = null;
        }
    }

    private void UpdateCachesForPausedPartitions(IReadOnlyList<TopicPartition> partitions)
    {
        Interlocked.Increment(ref _assignmentVersion);
        RemovePausedPartitionsFromPartitionCache(partitions);
        InvalidateFetchRequestCache();
    }

    private void UpdateCachesForResumedPartitions(IReadOnlyList<TopicPartition> partitions)
    {
        Interlocked.Increment(ref _assignmentVersion);
        AddResumedPartitionsToPartitionCache(partitions);
        InvalidateFetchRequestCache();
    }

    private void RemovePausedPartitionsFromPartitionCache(IReadOnlyList<TopicPartition> partitions)
    {
        lock (_partitionCacheLock)
        {
            var cachedEntry = _cachedPartitionsByBroker;
            if (cachedEntry is null)
                return;

            var cached = cachedEntry.Value;
            var partitionsByBroker = cached.PartitionsByBroker;
            var updated = new Dictionary<int, List<TopicPartition>>(partitionsByBroker.Count);
            var changed = false;
            foreach (var kvp in partitionsByBroker)
            {
                var filtered = RemovePartitions(kvp.Value, partitions);
                if (filtered is null)
                {
                    updated.Add(kvp.Key, kvp.Value);
                }
                else if (filtered.Count > 0)
                {
                    changed = true;
                    updated.Add(kvp.Key, filtered);
                }
                else
                {
                    changed = true;
                }
            }

            if (changed)
                _cachedPartitionsByBroker = new PartitionBrokerCacheEntry(
                    updated,
                    cached.PreferredReplicaExpiresAtTimestamp,
                    cached.MetadataLastRefreshed);
        }
    }

    private void AddResumedPartitionsToPartitionCache(IReadOnlyList<TopicPartition> partitions)
    {
        lock (_partitionCacheLock)
        {
            var cachedEntry = _cachedPartitionsByBroker;
            if (cachedEntry is null)
                return;

            var cached = cachedEntry.Value;
            if (!_preferredReadReplicas.IsEmpty)
            {
                _cachedPartitionsByBroker = null;
                return;
            }

            Dictionary<int, List<TopicPartition>>? updated = null;
            var assignment = _assignmentSnapshot;
            foreach (var partition in partitions)
            {
                if (_paused.ContainsKey(partition) || !ContainsPartition(assignment, partition))
                    continue;

                var leader = _metadataManager.TryGetCachedPartitionLeader(partition.Topic, partition.Partition);
                if (leader is null)
                {
                    _cachedPartitionsByBroker = null;
                    return;
                }

                updated ??= new Dictionary<int, List<TopicPartition>>(cached.PartitionsByBroker);
                if (!updated.TryGetValue(leader.NodeId, out var brokerPartitions))
                {
                    updated[leader.NodeId] = [partition];
                    continue;
                }

                if (ContainsPartition(brokerPartitions, partition))
                    continue;

                var replacement = new List<TopicPartition>(brokerPartitions.Count + 1);
                replacement.AddRange(brokerPartitions);
                replacement.Add(partition);
                updated[leader.NodeId] = replacement;
            }

            if (updated is not null)
                _cachedPartitionsByBroker = new PartitionBrokerCacheEntry(
                    updated,
                    cached.PreferredReplicaExpiresAtTimestamp,
                    cached.MetadataLastRefreshed);
        }
    }

    private async ValueTask<Dictionary<int, List<TopicPartition>>> GroupPartitionsByBrokerAsync(CancellationToken cancellationToken)
    {
        await HandleTopicIdentityChangesAsync(cancellationToken).ConfigureAwait(false);

        // Check cache and capture version to detect concurrent invalidation
        int capturedVersion;
        TopicPartition[] assignmentArray;
        int assignmentCount;
        var now = Stopwatch.GetTimestamp();

        lock (_partitionCacheLock)
        {
            if (_cachedPartitionsByBroker is { } cached)
            {
                if (IsPartitionBrokerCacheValid(cached, now))
                    return cached.PartitionsByBroker;

                _cachedPartitionsByBroker = null;
            }

            // Capture version and copy assignment/paused data under lock
            // Uses the immutable snapshot for thread-safe enumeration without _assignmentLock
            capturedVersion = Volatile.Read(ref _assignmentVersion);
            var snapshot = _assignmentSnapshot;
            var maxPartitions = snapshot.Count;
            assignmentArray = ArrayPool<TopicPartition>.Shared.Rent(maxPartitions);
            assignmentCount = 0;
            var activeSnapshot = Volatile.Read(ref _activeSnapshot);

            foreach (var partition in snapshot)
            {
                if (!_paused.ContainsKey(partition)
                    && (activeSnapshot is null || !activeSnapshot.IsPartitionComplete(partition)))
                {
                    assignmentArray[assignmentCount++] = partition;
                }
            }
        }

        var result = new Dictionary<int, List<TopicPartition>>();
        var preferredReplicaExpiresAtTimestamp = NoPreferredReplicaExpiry;
        Task<PartitionFetchBrokerResolution>[]? brokerTasks = null;
        var brokerTaskCount = 0;
        try
        {
            for (var i = 0; i < assignmentCount; i++)
            {
                var partition = assignmentArray[i];
                if (TryResolvePartitionFetchBroker(
                        partition,
                        now,
                        out var broker,
                        out var partitionPreferredReplicaExpiresAtTimestamp))
                {
                    AddPartitionFetchBroker(
                        result,
                        new PartitionFetchBrokerResolution(
                            partition,
                            broker,
                            partitionPreferredReplicaExpiresAtTimestamp),
                        ref preferredReplicaExpiresAtTimestamp);
                    continue;
                }

                brokerTasks ??= ArrayPool<Task<PartitionFetchBrokerResolution>>.Shared.Rent(assignmentCount - i);
                brokerTasks[brokerTaskCount++] = ResolvePartitionFetchBrokerAsync(partition, cancellationToken);
            }

            if (brokerTaskCount > 0)
            {
#if NETSTANDARD2_0
                await Task.WhenAll(brokerTasks!.Take(brokerTaskCount)).ConfigureAwait(false);
#else
                await Task.WhenAll(new ReadOnlySpan<Task<PartitionFetchBrokerResolution>>(brokerTasks!, 0, brokerTaskCount)).ConfigureAwait(false);
#endif

                for (var i = 0; i < brokerTaskCount; i++)
                {
                    AddPartitionFetchBroker(result, brokerTasks![i].Result, ref preferredReplicaExpiresAtTimestamp);
                }
            }
        }
        finally
        {
            ArrayPool<TopicPartition>.Shared.Return(assignmentArray, clearArray: true);
            if (brokerTasks is not null)
                ArrayPool<Task<PartitionFetchBrokerResolution>>.Shared.Return(brokerTasks, clearArray: true);
        }

        // Cache the result - will be reused until assignment/paused changes.
        // Don't cache empty results: an empty dictionary means partition leaders
        // couldn't be resolved (metadata not yet available). Caching it would prevent
        // recovery when metadata becomes available, causing the prefetch loop to spin
        // forever producing nothing.
        lock (_partitionCacheLock)
        {
            if (result.Count > 0 && _cachedPartitionsByBroker is null
                && Volatile.Read(ref _assignmentVersion) == capturedVersion)
            {
                _cachedPartitionsByBroker = new PartitionBrokerCacheEntry(
                    result,
                    preferredReplicaExpiresAtTimestamp,
                    _metadataManager.Metadata.LastRefreshed);
            }

            if (_cachedPartitionsByBroker is { } cached
                && IsPartitionBrokerCacheValid(cached, Stopwatch.GetTimestamp()))
            {
                return cached.PartitionsByBroker;
            }

            return result;
        }
    }

    private ValueTask HandleTopicIdentityChangesAsync(
        CancellationToken cancellationToken,
        string? rejectedTopic = null,
        Guid rejectedTopicId = default)
    {
        var metadataSnapshot = _metadataManager.Metadata.CaptureSnapshot();
        if (ReferenceEquals(metadataSnapshot, Volatile.Read(ref _observedTopicIdentityMarker)))
            return ValueTask.CompletedTask;

        return HandleTopicIdentityChangesSlowAsync(
            rejectedTopic,
            rejectedTopicId,
            rejectedTopics: null,
            cancellationToken: cancellationToken);
    }

    private ValueTask HandleRejectedTopicIdentityChangesAsync(
        Dictionary<string, Guid> rejectedTopics,
        CancellationToken cancellationToken) =>
        HandleTopicIdentityChangesSlowAsync(
            rejectedTopic: null,
            rejectedTopicId: default,
            rejectedTopics: rejectedTopics,
            cancellationToken: cancellationToken);

    private async ValueTask HandleTopicIdentityChangesSlowAsync(
        string? rejectedTopic,
        Guid rejectedTopicId,
        Dictionary<string, Guid>? rejectedTopics,
        CancellationToken cancellationToken)
    {
        await SemaphoreHelper.AcquireOrThrowDisposedAsync(
            _topicIdentityLock,
            nameof(KafkaConsumer<TKey, TValue>),
            cancellationToken).ConfigureAwait(false);
        try
        {
            var metadataSnapshot = _metadataManager.Metadata.CaptureSnapshot();
            var observedTopicIdentityMarker = Volatile.Read(ref _observedTopicIdentityMarker);
            if (rejectedTopics is null
                && rejectedTopic is not { Length: > 0 }
                && ReferenceEquals(metadataSnapshot, observedTopicIdentityMarker))
                return;

            if (rejectedTopic is { Length: > 0 } &&
                rejectedTopicId != Guid.Empty &&
                !_observedTopicIds.ContainsKey(rejectedTopic))
            {
                _observedTopicIds[rejectedTopic] = rejectedTopicId;
            }

            if (rejectedTopics is not null)
            {
                foreach (var (topic, topicId) in rejectedTopics)
                {
                    if (topicId != Guid.Empty && !_observedTopicIds.ContainsKey(topic))
                        _observedTopicIds[topic] = topicId;
                }
            }

            Dictionary<string, (Guid Previous, Guid Current)>? changedTopics = null;
            var assignedTopics = new HashSet<string>(StringComparer.Ordinal);
            var assignment = _assignmentSnapshot;
            foreach (var partition in assignment)
            {
                assignedTopics.Add(partition.Topic);

                if (!metadataSnapshot.Topics.TryGetValue(partition.Topic, out var topicInfo) ||
                    topicInfo.TopicId == Guid.Empty)
                {
                    continue;
                }

                if (_observedTopicIds.TryGetValue(partition.Topic, out var previousTopicId) &&
                    previousTopicId != Guid.Empty &&
                    previousTopicId != topicInfo.TopicId)
                {
                    changedTopics ??= [];
                    changedTopics[partition.Topic] = (previousTopicId, topicInfo.TopicId);
                }
                else
                {
                    _observedTopicIds[partition.Topic] = topicInfo.TopicId;
                }
            }
            PruneObservedTopicIds(assignedTopics);

            if (changedTopics is not null)
            {
                var recreatedPartitions = new HashSet<TopicPartition>();
                foreach (var partition in assignment)
                {
                    if (changedTopics.ContainsKey(partition.Topic))
                        recreatedPartitions.Add(partition);
                }

                InvalidateWatermarkCacheGenerations(recreatedPartitions);

                Dictionary<TopicPartition, long>? preexistingPendingFetchClearVersions = null;
                Task[]? resetTasks = null;
                var resetTaskCount = 0;

                try
                {
                    preexistingPendingFetchClearVersions =
                        CapturePendingFetchClearVersionsAndClearFetchBuffer(recreatedPartitions);
                    InvalidatePartitionCache();
                    InvalidateFetchRequestCache();
                    var fetchBufferEpoch = Volatile.Read(ref _fetchBufferEpoch);

                    foreach (var partition in recreatedPartitions)
                    {
                        ClearStoredOffset(partition);
                        _pendingRebalanceSeeks.TryRemove(partition, out _);
                        _committed.TryRemove(partition, out _);
                        _highWatermarks.TryRemove(partition, out _);
                        _eofEmitted.TryRemove(partition, out _);

                        var topicInfo = metadataSnapshot.Topics[partition.Topic];
                        if ((uint)partition.Partition >= (uint)topicInfo.PartitionCount)
                        {
                            _positions.TryRemove(partition, out _);
                            _fetchPositions.TryRemove(partition, out _);
                            ClearLastConsumedLeaderEpoch(partition);
                            _lastFetchedLeaderEpochs.TryRemove(partition, out _);
                            continue;
                        }

                        var identities = changedTopics[partition.Topic];
                        // Only the exact fetch-clear marker captured before this recovery belongs
                        // to the old topic identity. New marker versions reject ABA invalidations
                        // even if assignment cleanup removes their fetch-epoch minimum.
                        var reset = ResetOffsetOutOfRangeAsync(
                            partition,
                            fetchBufferEpoch,
                            cancellationToken,
                            allowedPendingFetchClearVersion:
                                preexistingPendingFetchClearVersions?.GetValueOrDefault(partition)
                                ?? NoPendingFetchClearVersion);
                        if (reset.IsCompletedSuccessfully)
                        {
                            reset.GetAwaiter().GetResult();
                            LogTopicIdentityReset(partition, identities);
                            continue;
                        }

                        resetTasks ??= ArrayPool<Task>.Shared.Rent(recreatedPartitions.Count);
                        resetTasks[resetTaskCount++] = CompleteTopicIdentityResetAsync(
                            reset,
                            partition,
                            identities);
                    }

#if NETSTANDARD2_0
                    if (resetTaskCount > 0)
                        await Task.WhenAll(resetTasks!.Take(resetTaskCount)).ConfigureAwait(false);
#else
                    if (resetTaskCount > 0)
                    {
                        await Task.WhenAll(
                            new ReadOnlySpan<Task>(resetTasks!, 0, resetTaskCount)).ConfigureAwait(false);
                    }
#endif

                    RotateWatermarkCacheGenerations(recreatedPartitions);
                }
                finally
                {
                    if (resetTasks is not null)
                        ArrayPool<Task>.Shared.Return(resetTasks, clearArray: true);

                    ReleaseTopicIdentityResetReservations(recreatedPartitions);
                }

                foreach (var (topic, identities) in changedTopics)
                    _observedTopicIds[topic] = identities.Current;

                // Publish recreated-topic state before allowing a background poll to rejoin.
                // Rejoining first can stage a temporary revocation; the reset's stale-fetch
                // guard then skips the reset and leaves the old topic's committed position.
                _coordinator?.RequestRejoin();
            }

            // Publish only if no assignment snapshot replaced the marker while resets awaited.
            Interlocked.CompareExchange(
                ref _observedTopicIdentityMarker,
                metadataSnapshot,
                observedTopicIdentityMarker);
        }
        finally
        {
            SemaphoreHelper.ReleaseSafely(_topicIdentityLock);
        }
    }

    private void RotateWatermarkCacheGenerations(HashSet<TopicPartition> recreatedPartitions)
    {
        lock (_snapshotStateGate)
        {
            var assignmentVersion = Interlocked.Increment(ref _assignmentEnsureVersion);
            foreach (var partition in recreatedPartitions)
            {
                if (!_assignmentSnapshot.Contains(partition))
                    continue;

                _watermarkAssignmentVersions[partition] = assignmentVersion;
                _watermarks.TryRemove(partition, out _);
            }
        }
    }

    private void InvalidateWatermarkCacheGenerations(HashSet<TopicPartition> recreatedPartitions)
    {
        lock (_snapshotStateGate)
        {
            Interlocked.Increment(ref _assignmentEnsureVersion);
            foreach (var partition in recreatedPartitions)
            {
                if (!_assignmentSnapshot.Contains(partition))
                    continue;

                _watermarkAssignmentVersions.TryRemove(partition, out _);
                _watermarks.TryRemove(partition, out _);
            }
        }
    }

    private Dictionary<TopicPartition, long>? CapturePendingFetchClearVersionsAndClearFetchBuffer(
        HashSet<TopicPartition> partitions)
    {
        Dictionary<TopicPartition, long>? versions = null;
        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
        {
            foreach (var partition in partitions)
                _topicIdentityResetPartitions.Add(partition);

            foreach (var partition in partitions)
            {
                if (_coordinatorRevokedPartitionsPendingFetchClear.TryGetValue(partition, out var version)
                    && _pendingFetchClearMarkerSources.TryGetValue(partition, out var source)
                    && source is PendingFetchClearMarkerSource.CoordinatorRevocation
                        or PendingFetchClearMarkerSource.DivergingEpoch)
                {
                    (versions ??= [])[partition] = version;
                }
            }

            // Claim old-identity markers before a background drain can invalidate the
            // freshly captured epoch. The lock stays held across the ordered clear, as
            // it does for the ordinary pending-revocation drain path.
            ClearFetchBufferForPartitions(partitions);

            if (versions is not null)
            {
                foreach (var (partition, version) in versions)
                {
                    if (_coordinatorRevokedPartitionsPendingFetchClear.TryGetValue(partition, out var currentVersion)
                        && currentVersion == version)
                    {
                        _coordinatorRevokedPartitionsPendingFetchClear.TryRemove(partition, out _);
                        _pendingFetchClearMarkerSources.Remove(partition);
                    }
                }
            }

            if (_coordinatorRevokedPartitionsPendingFetchClear.IsEmpty)
            {
                Volatile.Write(ref _coordinatorRevokedPartitionsPendingFetchClearMarkerPresent, 0);
                Volatile.Write(ref _coordinatorRevokedPartitionsPendingFetchClearPending, 0);
            }
        }

        return versions;
    }

    private void ReleaseTopicIdentityResetReservations(HashSet<TopicPartition> partitions)
    {
        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
        {
            foreach (var partition in partitions)
                _topicIdentityResetPartitions.Remove(partition);
        }
    }

    private void PruneObservedTopicIds(HashSet<string> assignedTopics)
    {
        List<string>? removedTopics = null;
        foreach (var topic in _observedTopicIds.Keys)
        {
            if (assignedTopics.Contains(topic))
                continue;

            removedTopics ??= [];
            removedTopics.Add(topic);
        }

        if (removedTopics is null)
            return;

        foreach (var topic in removedTopics)
            _observedTopicIds.Remove(topic);
    }

    private async Task CompleteTopicIdentityResetAsync(
        ValueTask reset,
        TopicPartition partition,
        (Guid Previous, Guid Current) identities)
    {
        await reset.ConfigureAwait(false);
        LogTopicIdentityReset(partition, identities);
    }

    private void LogTopicIdentityReset(
        TopicPartition partition,
        (Guid Previous, Guid Current) identities)
    {
        var fetchPosition = _fetchPositions.GetValueOrDefault(partition, long.MinValue);
        LogTopicIdentityReset(
            partition.Topic,
            partition.Partition,
            identities.Previous,
            identities.Current,
            fetchPosition);
    }

    private bool IsPartitionBrokerCacheValid(PartitionBrokerCacheEntry cached, long now)
    {
        if (cached.PreferredReplicaExpiresAtTimestamp == NoPreferredReplicaExpiry)
            return true;

        return cached.PreferredReplicaExpiresAtTimestamp > now
            && cached.MetadataLastRefreshed == _metadataManager.Metadata.LastRefreshed;
    }

    private static void AddPartitionFetchBroker(
        Dictionary<int, List<TopicPartition>> result,
        PartitionFetchBrokerResolution resolution,
        ref long preferredReplicaExpiresAtTimestamp)
    {
        if (resolution.Broker is null)
            return;

        if (resolution.PreferredReplicaExpiresAtTimestamp < preferredReplicaExpiresAtTimestamp)
            preferredReplicaExpiresAtTimestamp = resolution.PreferredReplicaExpiresAtTimestamp;

        if (!result.TryGetValue(resolution.Broker.NodeId, out var list))
        {
            list = [];
            result[resolution.Broker.NodeId] = list;
        }

        list.Add(resolution.Partition);
    }

    private bool TryResolvePartitionFetchBroker(
        TopicPartition partition,
        long now,
        out BrokerNode? broker,
        out long preferredReplicaExpiresAtTimestamp)
    {
        preferredReplicaExpiresAtTimestamp = NoPreferredReplicaExpiry;
        var leader = _metadataManager.TryGetCachedPartitionLeader(partition.Topic, partition.Partition);
        if (leader is null)
        {
            broker = null;
            return false;
        }

        broker = GetPreferredReadReplicaBrokerOrLeader(partition, leader, now, out preferredReplicaExpiresAtTimestamp);
        return true;
    }

    private async Task<PartitionFetchBrokerResolution> ResolvePartitionFetchBrokerAsync(
        TopicPartition partition,
        CancellationToken cancellationToken)
    {
        var leader = await _metadataManager.GetPartitionLeaderAsync(
            partition.Topic, partition.Partition, cancellationToken).ConfigureAwait(false);
        if (leader is null)
            return new PartitionFetchBrokerResolution(partition, null, NoPreferredReplicaExpiry);

        var broker = GetPreferredReadReplicaBrokerOrLeader(
            partition,
            leader,
            Stopwatch.GetTimestamp(),
            out var preferredReplicaExpiresAtTimestamp);
        return new PartitionFetchBrokerResolution(partition, broker, preferredReplicaExpiresAtTimestamp);
    }

    private BrokerNode GetPreferredReadReplicaBrokerOrLeader(
        TopicPartition partition,
        BrokerNode leader,
        long now,
        out long preferredReplicaExpiresAtTimestamp)
    {
        preferredReplicaExpiresAtTimestamp = NoPreferredReplicaExpiry;

        if (string.IsNullOrEmpty(_options.ClientRack)
            || !_preferredReadReplicas.TryGetValue(partition, out var preferred))
        {
            return leader;
        }

        var metadata = _metadataManager.Metadata;
        if (preferred.ExpiresAtTimestamp <= now
            || preferred.MetadataLastRefreshed != metadata.LastRefreshed)
        {
            ClearPreferredReadReplica(partition);
            return leader;
        }

        var preferredBroker = metadata.GetBroker(preferred.ReplicaId);
        if (preferredBroker is null)
        {
            ClearPreferredReadReplica(partition);
            return leader;
        }

        LogUsingPreferredReadReplica(partition.Topic, partition.Partition, preferred.ReplicaId, leader.NodeId);
        preferredReplicaExpiresAtTimestamp = preferred.ExpiresAtTimestamp;
        return preferredBroker;
    }

    private async ValueTask<List<PendingFetchData>?> FetchFromBrokerAsync(
        int brokerId,
        List<TopicPartition> partitions,
        int fetchBufferEpoch,
        CancellationToken cancellationToken)
    {
        var fetchMaxBytes = CurrentFetchMaxBytes;
        using var connectionLease = await _connectionPool.LeaseConnectionByIndexAsync(
            brokerId,
            0,
            cancellationToken).ConfigureAwait(false);
        var connection = connectionLease.Connection;

        var apiVersion = _metadataManager.GetNegotiatedApiVersion(
            connection,
            ApiKey.Fetch,
            FetchRequest.LowestSupportedVersion,
            FetchRequest.HighestSupportedVersion);

        // Resolve any special offset values (-1 for end, -2 for beginning) before fetching
        await ResolveSpecialOffsetsAsync(partitions, 0, partitions.Count, cancellationToken).ConfigureAwait(false);

        // Build fetch request - use imperative code to avoid LINQ allocations
        var topicData = BuildFetchRequestTopicsWithSnapshot(
            partitions,
            0,
            partitions.Count,
            brokerId,
            out var requestMetadataSnapshot);
        FetchSessionHandler? fetchSessionHandler = null;
        FetchSessionBuildResult? fetchSessionBuild = null;
        if (ShouldUseFetchSessions && apiVersion >= 7)
        {
            fetchSessionHandler = _fetchSessions.GetOrAdd((brokerId, 0), static _ => new FetchSessionHandler());
            fetchSessionBuild = fetchSessionHandler.BuildFromSnapshot(topicData, requestMetadataSnapshot);
        }

        var request = FetchRequest.Rent();
        request.MaxWaitMs = _options.FetchMaxWaitMs;
        request.MinBytes = _options.FetchMinBytes;
        request.MaxBytes = fetchMaxBytes;
        request.CheckCrcs = _options.CheckCrcs;
        request.ResponseMemoryPool = _fetchBufferMemoryPool;
        request.IsolationLevel = _options.IsolationLevel;
        request.RackId = _options.ClientRack;
        request.Topics = fetchSessionBuild?.Topics ?? topicData;
        request.ForgottenTopicsData = fetchSessionBuild?.ForgottenTopicsData;
        request.SessionId = fetchSessionBuild?.SessionId ?? 0;
        request.SessionEpoch = fetchSessionBuild?.SessionEpoch ?? -1;

        var fetchStarted = Stopwatch.GetTimestamp();
        long watermarkUpdateSequence;

        FetchResponse response;
        try
        {
            response = await SendWithWatermarkWriteSequenceAsync<FetchRequest, FetchResponse>(
                connection,
                request,
                (short)apiVersion,
                cancellationToken).ConfigureAwait(false);
            watermarkUpdateSequence = ((IRequestWriteSequenceTarget)request).WriteSequence;
        }
        catch
        {
            fetchSessionHandler?.HandleError();
            throw;
        }
        finally
        {
            request.ReturnToPool();
            ConsumerFetchPools.ReturnFetchRequestTopics(topicData);
        }

        RecordFetchDuration(fetchStarted, brokerId);

        // Take ownership of pooled memory from the response (if zero-copy was used)
        var memoryOwner = response.PooledMemoryOwner;
        response.PooledMemoryOwner = null; // Clear to prevent double-dispose

        if (response.ErrorCode != ErrorCode.None)
        {
            fetchSessionHandler?.HandleResponse(response);
            ClearPreferredReadReplicasForBroker(brokerId, partitions);
            LogFetchSessionError(brokerId, response.ErrorCode);
            response.ReturnToPool();
            memoryOwner?.Dispose();
            return null;
        }

        fetchSessionHandler?.HandleResponse(response);

        // Collect pending fetch data items - we need to assign memory owner to the last one
        List<PendingFetchData>? pendingItems = null;
        var queuedDivergingEpochReset = false;
        Dictionary<string, Guid>? topicIdentityRefreshes = null;

        // Queue pending fetch data for lazy iteration - don't parse records yet!
        try
        {
            foreach (var topicResponse in response.Responses)
            {
                var topic = ResolveTopicName(
                    topicResponse,
                    requestMetadataSnapshot,
                    fetchSessionHandler);
                if (string.IsNullOrEmpty(topic))
                    continue;

                var activityName = _activityNameCache.GetOrAdd(topic, static t => Diagnostics.DekafDiagnostics.ProcessSpanName(t));

                foreach (var partitionResponse in topicResponse.Partitions)
                {
                    var tp = new TopicPartition(topic, partitionResponse.PartitionIndex);
                    if (ShouldDropStaleFetchPartition(tp, fetchBufferEpoch))
                        continue;

                    // Update watermark cache from fetch response (even on errors, watermarks may be valid)
                    UpdateWatermarksFromFetchResponse(
                        tp,
                        partitionResponse,
                        fetchBufferEpoch,
                        GetLeaderEpoch(requestMetadataSnapshot, tp),
                        watermarkUpdateSequence);
                    UpdatePreferredReadReplica(topic, partitionResponse);

                    if (partitionResponse.RecordParseError is { } parseError)
                    {
                        pendingItems ??= ConsumerFetchPools.RentPendingFetchDataList();
                        pendingItems.Add(PendingFetchData.CreateError(
                            topic,
                            partitionResponse.PartitionIndex,
                            new ConsumeException(
                                $"Failed to parse record batch for {topic}-{partitionResponse.PartitionIndex}",
                                parseError)));
                        continue;
                    }

                    if (partitionResponse.DivergingEpoch is not null)
                    {
                        _stuckFetchPositionTracker.Reset(tp);
                        if (ResetToDivergingEpoch(
                            topic,
                            partitionResponse,
                            fetchBufferEpoch,
                            startsBatch: !queuedDivergingEpochReset))
                        {
                            queuedDivergingEpochReset = true;
                        }
                        continue;
                    }

                    if (partitionResponse.ErrorCode != ErrorCode.None)
                    {
                        _stuckFetchPositionTracker.Reset(tp);
                        if (partitionResponse.ErrorCode == ErrorCode.OffsetOutOfRange)
                        {
                            await HandleFetchOffsetOutOfRangeAsync(
                                tp, brokerId, requestMetadataSnapshot, fetchBufferEpoch, cancellationToken).ConfigureAwait(false);
                        }
                        else if (IsLeaderEpochRefreshError(partitionResponse.ErrorCode))
                        {
                            await HandleLeaderEpochRefreshAsync(
                                topic,
                                partitionResponse,
                                response.NodeEndpoints).ConfigureAwait(false);
                        }
                        else if (IsTopicIdentityRefreshError(partitionResponse.ErrorCode))
                        {
                            QueueTopicIdentityRefresh(
                                ref topicIdentityRefreshes,
                                topic,
                                topicResponse.TopicId,
                                tp);
                        }
                        else
                        {
                            LogFetchError(topic, partitionResponse.PartitionIndex, partitionResponse.ErrorCode);
                        }
                        continue;
                    }

                    // Update high watermark from response (thread-safe with ConcurrentDictionary)
                    _highWatermarks[tp] = partitionResponse.HighWatermark;

                    // Cache Records reference to avoid repeated Volatile.Read from the pool guard
                    var records = partitionResponse.Records;

                    if (records is { Count: > 0 })
                    {
                        _stuckFetchPositionTracker.Reset(tp);
                        // EOF is re-armed when these records are queued for delivery.

                        // Collect pending fetch data for lazy record iteration
                        pendingItems ??= ConsumerFetchPools.RentPendingFetchDataList();
                        pendingItems.Add(PendingFetchData.Create(
                            topic,
                            partitionResponse.PartitionIndex,
                            records,
                            partitionResponse.AbortedTransactions,
                            activityName: activityName,
                            skipRecordsBelowOffset: _fetchPositions.GetValueOrDefault(tp, -1),
                            stopAtOffsetExclusive: GetSnapshotEndOffset(tp),
                            ownershipStart: _ownershipStartGenerations.GetValueOrDefault(tp)));
                    }
                    else
                    {
                        var stuckError = HandleEmptyFetchResponse(tp, records, partitionResponse.HighWatermark, fetchBufferEpoch);
                        if (stuckError is not null)
                        {
                            if (pendingItems is not null)
                            {
                                DisposePendingFetches(pendingItems);
                                ConsumerFetchPools.ReturnPendingFetchDataList(pendingItems);
                                pendingItems = null;
                            }

                            throw stuckError;
                        }

                        if (TryCreateSnapshotEndMarker(tp, partitionResponse) is { } marker)
                        {
                            pendingItems ??= ConsumerFetchPools.RentPendingFetchDataList();
                            pendingItems.Add(marker);
                        }
                    }
                }
            }

            if (topicIdentityRefreshes is not null)
            {
                await HandleTopicIdentityRefreshesAsync(
                    topicIdentityRefreshes,
                    fetchSessionHandler,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            if (pendingItems is not null)
            {
                DisposePendingFetches(pendingItems);
                ConsumerFetchPools.ReturnPendingFetchDataList(pendingItems);
                pendingItems = null;
            }

            memoryOwner?.Dispose();
            memoryOwner = null;
            throw;
        }
        finally
        {
            if (queuedDivergingEpochReset)
                CompleteDivergingEpochResets();

            // Return the response and its nested objects to their pools.
            // Data has been transferred to PendingFetchData; the response wrappers are no longer needed.
            response.ReturnToPool();
        }

        if (pendingItems is { Count: > 0 } && memoryOwner is not null)
        {
            AssignSharedMemoryOwner(pendingItems, memoryOwner);
            memoryOwner = null; // Transferred
        }

        memoryOwner?.Dispose();

        return pendingItems;
    }

    /// <summary>
    /// Assigns a ref-counted memory owner to all pending items so the underlying pooled buffer
    /// is only returned when every item has been disposed, regardless of disposal order.
    /// </summary>
    private static void AssignSharedMemoryOwner(List<PendingFetchData> pendingItems, IPooledMemory memoryOwner)
    {
        var shared = RefCountedMemoryOwner.Create(memoryOwner, pendingItems.Count);
        for (var i = 0; i < pendingItems.Count; i++)
        {
            pendingItems[i].SetMemoryOwner(shared);
        }
    }

    private static void DisposePendingFetches(List<PendingFetchData> pendingItems)
    {
        for (var i = 0; i < pendingItems.Count; i++)
        {
            pendingItems[i].Dispose();
        }

        pendingItems.Clear();
    }

    /// <summary>
    /// Creates and configures a tracing Activity for consume operations.
    /// Separated from the hot path to avoid inlining overhead when no listeners are active.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private System.Diagnostics.Activity? StartConsumeActivity(
        PendingFetchData pending,
        IReadOnlyList<Header>? headers,
        long offset,
        bool isTombstone,
        bool isProcessSpan)
    {
        // Use span links (not parent-child) per OTel messaging semantic conventions:
        // the consumer span gets its own trace root, linked to the producer span.
        // Two flavors, matching the activity's actual lifetime at the call site:
        // - Streaming ConsumeAsync (isProcessSpan): the activity stays open until the
        //   next MoveNext, so its duration covers the caller's handling of the record —
        //   a "process" span (CONSUMER kind). Note it is NOT Activity.Current inside the
        //   caller's loop body (Current is restored below, and AsyncLocal changes made
        //   here would not flow out of the iterator anyway), so handler-created spans do
        //   not parent under it — they correlate via duration overlap only.
        // - ConsumeOne (sync + async): the activity ends in a finally before the record
        //   is returned, covering only delivery/deserialization — a "receive" span
        //   named "poll" (CLIENT kind). Labeling it "process" would report
        //   non-processing spans as processing; labeling the streaming span "receive"
        //   would report handling time as poll latency.
        var producerContext = Diagnostics.TraceContextPropagator.ExtractTraceContext(headers);
        var activityName = isProcessSpan
            ? pending.ActivityName
            : _pollActivityNameCache.GetOrAdd(pending.Topic, static t => Diagnostics.DekafDiagnostics.PollSpanName(t));
        var activityKind = isProcessSpan
            ? System.Diagnostics.ActivityKind.Consumer
            : System.Diagnostics.ActivityKind.Client;
        System.Diagnostics.Activity? activity;
        var savedActivity = System.Diagnostics.Activity.Current;
        System.Diagnostics.Activity.Current = null;
        try
        {
            if (producerContext.HasValue)
            {
                activity = Diagnostics.DekafDiagnostics.Source.StartActivity(
                    activityName,
                    activityKind,
                    parentContext: default(System.Diagnostics.ActivityContext),
                    tags: null,
                    links: [new System.Diagnostics.ActivityLink(producerContext.Value)]);
            }
            else
            {
                activity = Diagnostics.DekafDiagnostics.Source.StartActivity(
                    activityName,
                    activityKind);
            }
        }
        finally
        {
            System.Diagnostics.Activity.Current = savedActivity;
        }

        if (activity is not null)
        {
            activity.SetTag(Diagnostics.DekafDiagnostics.MessagingSystem, Diagnostics.DekafDiagnostics.MessagingSystemValue);
            activity.SetTag(Diagnostics.DekafDiagnostics.MessagingDestinationName, pending.Topic);
            activity.SetTag(Diagnostics.DekafDiagnostics.MessagingOperationName, isProcessSpan
                ? Diagnostics.DekafDiagnostics.OperationNameProcess
                : Diagnostics.DekafDiagnostics.OperationNamePoll);
            activity.SetTag(Diagnostics.DekafDiagnostics.MessagingOperationType, isProcessSpan
                ? Diagnostics.DekafDiagnostics.OperationTypeProcess
                : Diagnostics.DekafDiagnostics.OperationTypeReceive);
            activity.SetTag(Diagnostics.DekafDiagnostics.MessagingDestinationPartitionId, pending.PartitionIndex);
            activity.SetTag(Diagnostics.DekafDiagnostics.MessagingKafkaOffset, offset);
            // messaging.message.body.size is Opt-In in the OTel messaging conventions.
            // Dekaf does not expose that opt-in, so avoid its tag node and per-record int box.
            if (isTombstone)
                activity.SetTag(Diagnostics.DekafDiagnostics.MessagingKafkaTombstone, Diagnostics.DekafDiagnostics.BoxedTrue);
            var clusterId = _metadataManager.ClusterId;
            if (clusterId is not null)
                activity.SetTag(Diagnostics.DekafDiagnostics.MessagingKafkaClusterId, clusterId);
            if (_options.ClientId is not null)
                activity.SetTag(Diagnostics.DekafDiagnostics.MessagingClientId, _options.ClientId);
            if (_options.GroupId is not null)
                activity.SetTag(Diagnostics.DekafDiagnostics.MessagingConsumerGroupName, _options.GroupId);
        }

        return activity;
    }

    /// <summary>
    /// Applies OnConsume interceptors to a consume result before yielding to the user.
    /// Interceptor exceptions are caught and logged - the original result is used on failure.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ConsumeResult<TKey, TValue> ApplyOnConsumeInterceptors(ConsumeResult<TKey, TValue> result)
    {
        if (_interceptors is null)
            return result;

        return ApplyOnConsumeInterceptorsSlow(result);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private ConsumeResult<TKey, TValue> ApplyOnConsumeInterceptorsSlow(ConsumeResult<TKey, TValue> result)
    {
        // Keep chain updates in a local. Writing the by-value struct parameter can
        // require GC write barriers because its incoming storage is passed by reference.
        var current = result;
        foreach (var interceptor in _interceptors!)
        {
            try
            {
                var replacement = interceptor.OnConsume(current);
                ConsumeResult<TKey, TValue>.PreserveStorageOwner(ref replacement, in current);
                current = replacement;
            }
            catch (Exception ex)
            {
                LogInterceptorOnConsumeError(ex, interceptor.GetType().Name);
            }
        }
        return current;
    }

    /// <summary>
    /// Invokes OnCommit on all interceptors.
    /// Interceptor exceptions are caught and logged.
    /// </summary>
    private void InvokeOnCommitInterceptors(IReadOnlyList<TopicPartitionOffset> offsets)
    {
        if (_interceptors is null)
            return;

        foreach (var interceptor in _interceptors)
        {
            try
            {
                interceptor.OnCommit(offsets);
            }
            catch (Exception ex)
            {
                LogInterceptorOnCommitError(ex, interceptor.GetType().Name);
            }
        }
    }


    /// <summary>
    /// Updates the watermark cache from a fetch response partition.
    /// The fetch response contains HighWatermark and LogStartOffset which correspond to
    /// the high and low watermarks respectively.
    /// </summary>
    private void UpdateWatermarksFromFetchResponse(
        TopicPartition partition,
        FetchResponsePartition partitionResponse,
        int fetchBufferEpoch,
        int leaderEpoch,
        long watermarkUpdateSequence)
    {
        // Only update if we have valid watermark data
        // HighWatermark is the next offset to be written (end of log)
        // LogStartOffset is the earliest available offset (start of log, may be > 0 due to retention)
        if (partitionResponse.HighWatermark >= 0)
        {
            var low = partitionResponse.LogStartOffset >= 0 ? partitionResponse.LogStartOffset : 0;
            var lagEndOffset = _options.IsolationLevel == IsolationLevel.ReadCommitted
                && partitionResponse.LastStableOffset >= 0
                    ? partitionResponse.LastStableOffset
                    : partitionResponse.HighWatermark;
            UpdateCachedWatermarks(
                partition,
                low,
                partitionResponse.HighWatermark,
                lagEndOffset,
                fetchBufferEpoch,
                leaderEpoch,
                watermarkUpdateSequence);
        }
    }

    private void UpdateCachedWatermarks(
        TopicPartition partition,
        long low,
        long high,
        long lagEndOffset,
        int fetchBufferEpoch,
        int leaderEpoch,
        long watermarkUpdateSequence)
    {
        if (!_watermarks.TryGetValue(partition, out var entry))
        {
            CreateOrUpdateCachedWatermarksSlow(
                partition,
                low,
                high,
                lagEndOffset,
                fetchBufferEpoch,
                leaderEpoch,
                watermarkUpdateSequence);
            return;
        }

        entry.Update(
            low,
            high,
            lagEndOffset,
            fetchBufferEpoch,
            leaderEpoch,
            watermarkUpdateSequence);
    }

    // Called only after QueryCurrentLagCoreAsync revalidates assignment under
    // _snapshotStateGate. A lag-only query must not fabricate public watermarks.
    private void UpdateCachedLagEndOffset(
        TopicPartition partition,
        long lagEndOffset,
        int leaderEpoch,
        long watermarkUpdateSequence)
    {
        if (_watermarks.TryGetValue(partition, out var entry))
        {
            entry.UpdateLagEndOffset(lagEndOffset, leaderEpoch, watermarkUpdateSequence);
            return;
        }

        _watermarks.TryAdd(
            partition,
            new WatermarkCacheEntry(
                lagEndOffset,
                GetMinimumFetchBufferEpoch(partition),
                leaderEpoch,
                watermarkUpdateSequence,
                GetTopicId(
                    _metadataManager.Metadata.CaptureSnapshot(),
                    partition.Topic)));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void CreateOrUpdateCachedWatermarksSlow(
        TopicPartition partition,
        long low,
        long high,
        long lagEndOffset,
        int fetchBufferEpoch,
        int leaderEpoch,
        long watermarkUpdateSequence)
    {
        BeforeWatermarkCacheEntryCreationForTest?.Invoke();
        WatermarkCacheEntry entry;
        while (true)
        {
            lock (_snapshotStateGate)
            {
                if (IsFetchBufferEpochStale(partition, fetchBufferEpoch)
                    || (_assignmentSnapshot.Contains(partition)
                        && !_watermarkAssignmentVersions.ContainsKey(partition)))
                    return;

                if (_watermarks.TryGetValue(partition, out entry!))
                    break;

                if (_watermarks.TryAdd(
                    partition,
                    new WatermarkCacheEntry(
                        low,
                        high,
                        lagEndOffset,
                        fetchBufferEpoch,
                        leaderEpoch,
                        watermarkUpdateSequence,
                        GetTopicId(
                            _metadataManager.Metadata.CaptureSnapshot(),
                            partition.Topic))))
                {
                    return;
                }
            }
        }

        entry.Update(
            low,
            high,
            lagEndOffset,
            fetchBufferEpoch,
            leaderEpoch,
            watermarkUpdateSequence);
    }

    private sealed class WatermarkCacheEntry
    {
        private const long UnknownWatermarkOffset = -1;
        private const int UnknownLeaderEpoch = -1;

        // Allocate once per partition, then update in place. The version is a seqlock:
        // odd while a writer owns the entry, even when readers can take a coherent snapshot.
        // Writers also reject each field set when its response predates the last applied request.
        private int _version;
        // Uses the padding beside _version and is read only while state is divergent.
        private int _watermarkOffsetsUpdateSequenceLow;
        private int _leaderEpoch;
        private int _minimumFetchBufferEpoch;
        private readonly Guid _topicId;
        private long _low;
        private long _high;
        private long _lagEndOffset;
        // The low 63 bits hold lag freshness; the sign bit marks divergence.
        // During divergence, the public-watermark sequence's low 32 bits live in
        // the padding field above. Known leader epochs use monotonic offsets;
        // unknown epochs retain sequence ordering as a compatibility fallback.
        private long _watermarkUpdateState;

        public WatermarkCacheEntry(
            long lagEndOffset,
            int minimumFetchBufferEpoch,
            int leaderEpoch,
            long watermarkUpdateSequence,
            Guid topicId)
        {
            _low = UnknownWatermarkOffset;
            _high = UnknownWatermarkOffset;
            _lagEndOffset = lagEndOffset;
            _leaderEpoch = leaderEpoch;
            _minimumFetchBufferEpoch = minimumFetchBufferEpoch;
            _topicId = topicId;
            _watermarkUpdateState = long.MinValue | watermarkUpdateSequence;
        }

        public WatermarkCacheEntry(
            long low,
            long high,
            long lagEndOffset,
            int minimumFetchBufferEpoch,
            int leaderEpoch,
            long watermarkUpdateSequence,
            Guid topicId)
        {
            _low = low;
            _high = high;
            _lagEndOffset = lagEndOffset;
            _leaderEpoch = leaderEpoch;
            _minimumFetchBufferEpoch = minimumFetchBufferEpoch;
            _topicId = topicId;
            _watermarkUpdateState = watermarkUpdateSequence;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(
            long low,
            long high,
            long lagEndOffset,
            int fetchBufferEpoch,
            int leaderEpoch,
            long watermarkUpdateSequence)
        {
            var spinner = new SpinWait();
            while (true)
            {
                var version = Volatile.Read(ref _version);
                if ((version & 1) != 0
                    || Interlocked.CompareExchange(ref _version, version + 1, version) != version)
                {
                    spinner.SpinOnce();
                    continue;
                }

                if (fetchBufferEpoch < _minimumFetchBufferEpoch)
                {
                    Volatile.Write(ref _version, version + 2);
                    return;
                }

                var currentLeaderEpoch = _leaderEpoch;
                if (leaderEpoch == currentLeaderEpoch)
                {
                    if (leaderEpoch != UnknownLeaderEpoch)
                    {
                        if (low < _low || high < _high || lagEndOffset < _lagEndOffset)
                            UpdateSameLeaderEpoch(low, high, lagEndOffset);
                        else
                        {
                            Volatile.Write(ref _low, low);
                            Volatile.Write(ref _high, high);
                            Volatile.Write(ref _lagEndOffset, lagEndOffset);
                        }

                        Volatile.Write(ref _version, version + 2);
                        return;
                    }
                }
                else if (currentLeaderEpoch != UnknownLeaderEpoch
                         && (leaderEpoch == UnknownLeaderEpoch || leaderEpoch < currentLeaderEpoch))
                {
                    Volatile.Write(ref _version, version + 2);
                    return;
                }

                if ((ulong)watermarkUpdateSequence < (ulong)_watermarkUpdateState
                    && leaderEpoch <= currentLeaderEpoch)
                {
                    UpdateStale(low, high, lagEndOffset, watermarkUpdateSequence);

                    Volatile.Write(ref _version, version + 2);
                    return;
                }

                Volatile.Write(ref _low, low);
                Volatile.Write(ref _high, high);
                Volatile.Write(ref _lagEndOffset, lagEndOffset);
                _leaderEpoch = leaderEpoch;
                _watermarkUpdateState = watermarkUpdateSequence;
                Volatile.Write(ref _version, version + 2);
                return;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void UpdateSameLeaderEpoch(long low, long high, long lagEndOffset)
        {
            if (low > _low)
                Volatile.Write(ref _low, low);
            if (high > _high)
                Volatile.Write(ref _high, high);
            if (lagEndOffset > _lagEndOffset)
                Volatile.Write(ref _lagEndOffset, lagEndOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateLagEndOffset(
            long lagEndOffset,
            int leaderEpoch,
            long watermarkUpdateSequence)
        {
            var spinner = new SpinWait();
            while (true)
            {
                var version = Volatile.Read(ref _version);
                if ((version & 1) != 0
                    || Interlocked.CompareExchange(ref _version, version + 1, version) != version)
                {
                    spinner.SpinOnce();
                    continue;
                }

                var currentLeaderEpoch = _leaderEpoch;
                if (currentLeaderEpoch != UnknownLeaderEpoch
                    && leaderEpoch == UnknownLeaderEpoch)
                {
                    Volatile.Write(ref _version, version + 2);
                    return;
                }

                if (leaderEpoch != UnknownLeaderEpoch
                    && currentLeaderEpoch != UnknownLeaderEpoch)
                {
                    if (leaderEpoch < currentLeaderEpoch
                        || (leaderEpoch == currentLeaderEpoch && lagEndOffset < _lagEndOffset))
                    {
                        Volatile.Write(ref _version, version + 2);
                        return;
                    }

                    if (leaderEpoch > currentLeaderEpoch)
                    {
                        Volatile.Write(ref _low, UnknownWatermarkOffset);
                        Volatile.Write(ref _high, UnknownWatermarkOffset);
                    }

                    Volatile.Write(ref _lagEndOffset, lagEndOffset);
                    _leaderEpoch = leaderEpoch;
                    _watermarkUpdateState = long.MinValue | Math.Max(
                        _watermarkUpdateState & long.MaxValue,
                        watermarkUpdateSequence);
                    Volatile.Write(ref _version, version + 2);
                    return;
                }

                var updateState = _watermarkUpdateState;
                var lagEndOffsetUpdateSequence = updateState & long.MaxValue;
                if (watermarkUpdateSequence >= lagEndOffsetUpdateSequence)
                {
                    if (updateState >= 0)
                        _watermarkOffsetsUpdateSequenceLow = (int)lagEndOffsetUpdateSequence;

                    Volatile.Write(ref _lagEndOffset, lagEndOffset);
                    _leaderEpoch = leaderEpoch;
                    _watermarkUpdateState = long.MinValue | watermarkUpdateSequence;
                }
                Volatile.Write(ref _version, version + 2);
                return;
            }
        }

        public void AdvanceMinimumFetchBufferEpoch(int minimumFetchBufferEpoch)
        {
            var spinner = new SpinWait();
            while (true)
            {
                var version = Volatile.Read(ref _version);
                if ((version & 1) != 0
                    || Interlocked.CompareExchange(ref _version, version + 1, version) != version)
                {
                    spinner.SpinOnce();
                    continue;
                }

                if (minimumFetchBufferEpoch > _minimumFetchBufferEpoch)
                    _minimumFetchBufferEpoch = minimumFetchBufferEpoch;
                Volatile.Write(ref _version, version + 2);
                return;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void UpdateStale(
            long low,
            long high,
            long lagEndOffset,
            long watermarkUpdateSequence)
        {
            var updateState = _watermarkUpdateState;
            if (updateState >= 0)
                return;

            var lagEndOffsetUpdateSequence = updateState & long.MaxValue;
            if (watermarkUpdateSequence >= lagEndOffsetUpdateSequence)
            {
                Volatile.Write(ref _low, low);
                Volatile.Write(ref _high, high);
                Volatile.Write(ref _lagEndOffset, lagEndOffset);
                _watermarkUpdateState = watermarkUpdateSequence;
                return;
            }

            var watermarkOffsetsUpdateSequence = (lagEndOffsetUpdateSequence & ~uint.MaxValue)
                | (uint)_watermarkOffsetsUpdateSequenceLow;
            if (watermarkOffsetsUpdateSequence > lagEndOffsetUpdateSequence)
                watermarkOffsetsUpdateSequence -= 1L << 32;

            if (_low != UnknownWatermarkOffset
                && watermarkUpdateSequence < watermarkOffsetsUpdateSequence)
            {
                return;
            }

            Volatile.Write(ref _low, low);
            Volatile.Write(ref _high, high);
            _watermarkOffsetsUpdateSequenceLow = (int)watermarkUpdateSequence;
        }

        public bool TryReplaceWithNewerSnapshot(
            ConcurrentDictionary<TopicPartition, WatermarkCacheEntry> watermarks,
            TopicPartition partition,
            WatermarkCacheEntry replacement)
        {
            var spinner = new SpinWait();
            while (true)
            {
                var version = Volatile.Read(ref _version);
                if ((version & 1) != 0
                    || Interlocked.CompareExchange(ref _version, version + 1, version) != version)
                {
                    spinner.SpinOnce();
                    continue;
                }

                var replaced = CanReplaceWith(replacement)
                    && watermarks.TryUpdate(partition, replacement, this);
                Volatile.Write(ref _version, version + 2);
                return replaced;
            }
        }

        private bool CanReplaceWith(WatermarkCacheEntry replacement)
        {
            if (replacement._topicId != Guid.Empty
                && replacement._topicId != _topicId)
            {
                return (ulong)replacement._watermarkUpdateState
                       >= (ulong)(_watermarkUpdateState & long.MaxValue);
            }

            var currentLeaderEpoch = _leaderEpoch;
            var replacementLeaderEpoch = replacement._leaderEpoch;
            if (currentLeaderEpoch != UnknownLeaderEpoch)
            {
                if (replacementLeaderEpoch == UnknownLeaderEpoch)
                    return false;

                return replacementLeaderEpoch > currentLeaderEpoch
                       || (replacementLeaderEpoch == currentLeaderEpoch
                           && replacement._low >= _low
                           && replacement._high >= _high
                           && replacement._lagEndOffset >= _lagEndOffset);
            }

            return replacement._watermarkUpdateState >= (_watermarkUpdateState & long.MaxValue);
        }

        public WatermarkOffsets? ReadWatermarks()
        {
            var spinner = new SpinWait();
            while (true)
            {
                var version = Volatile.Read(ref _version);
                if ((version & 1) != 0)
                {
                    spinner.SpinOnce();
                    continue;
                }

                var low = Volatile.Read(ref _low);
                var high = Volatile.Read(ref _high);
                if (version == Volatile.Read(ref _version))
                {
                    return low >= 0 && high >= 0
                        ? new WatermarkOffsets(low, high)
                        : null;
                }

                spinner.SpinOnce();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadLagEndOffset() => IntPtr.Size == sizeof(long)
            ? Volatile.Read(ref _lagEndOffset)
            : ReadLagEndOffset32Bit();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private long ReadLagEndOffset32Bit()
        {
            var spinner = new SpinWait();
            while (true)
            {
                var version = Volatile.Read(ref _version);
                if ((version & 1) != 0)
                {
                    spinner.SpinOnce();
                    continue;
                }

                var lagEndOffset = Volatile.Read(ref _lagEndOffset);
                if (version == Volatile.Read(ref _version))
                    return lagEndOffset;

                spinner.SpinOnce();
            }
        }
    }

    private int GetCurrentLeaderEpoch(TopicPartition partition) =>
        _metadataManager.Metadata.GetPartitionInfo(partition.Topic, partition.Partition)?.LeaderEpoch ?? -1;

    private int GetLastConsumedLeaderEpoch(TopicPartition partition) =>
        _lastConsumedLeaderEpochs.GetValueOrDefault(partition, -1);



    private bool ResetToDivergingEpoch(
        string topic,
        FetchResponsePartition partitionResponse,
        int fetchBufferEpoch,
        bool startsBatch)
    {
        if (partitionResponse.DivergingEpoch is not { } divergingEpoch)
            return false;

        var partition = new TopicPartition(topic, partitionResponse.PartitionIndex);
        if (ShouldDropStaleFetchPartition(partition, fetchBufferEpoch))
            return false;

        return StageDivergingEpochReset(
            partition,
            divergingEpoch.EndOffset,
            divergingEpoch.Epoch,
            fetchBufferEpoch,
            startsBatch);
    }

    private ValueTask HandleFetchOffsetOutOfRangeAsync(
        TopicPartition partition,
        int brokerId,
        ClusterMetadataSnapshot requestMetadataSnapshot,
        int fetchBufferEpoch,
        CancellationToken cancellationToken)
    {
        // Replica preferences were already cleared on the error. Classify the actual
        // destination against the request snapshot, not that mutable preference: a
        // follower's missing offset requires a leader retry at the unchanged position.
        if (!requestMetadataSnapshot.PartitionsByTopicIndex.TryGetValue(partition.Topic, out var partitions)
            || (uint)partition.Partition >= (uint)partitions.Length
            || partitions[partition.Partition] is not { } requestLeader
            || requestLeader.LeaderId != brokerId)
        {
            // Metadata may change after broker grouping without a preferred replica
            // to clear. Rebuild routing before retrying the current leader.
            InvalidatePartitionCache();
            return default;
        }

        return ResetOffsetOutOfRangeAsync(
            partition, fetchBufferEpoch, cancellationToken, expectedLeader: requestLeader,
            expectedMetadataSnapshot: requestMetadataSnapshot);
    }

    private ValueTask ResetOffsetOutOfRangeAsync(
        TopicPartition partition,
        int fetchBufferEpoch,
        CancellationToken cancellationToken,
        long allowedPendingFetchClearVersion = NoPendingFetchClearVersion,
        PartitionInfo? expectedLeader = null,
        ClusterMetadataSnapshot? expectedMetadataSnapshot = null)
    {
        var policy = _options.AutoOffsetReset;
        if (policy is not (AutoOffsetReset.Earliest or AutoOffsetReset.Latest))
        {
            if (policy == AutoOffsetReset.None)
            {
                // Deciding to throw needs the same atomic leader/assignment validation as a reset.
                TryApplyOffsetReset(partition, fetchBufferEpoch, allowedPendingFetchClearVersion,
                    resetOffset: null, expectedLeader, expectedMetadataSnapshot);
                return default;
            }

            return ResolveOffsetOutOfRangeAsync(
                partition, fetchBufferEpoch, allowedPendingFetchClearVersion,
                expectedLeader, cancellationToken);
        }

        // Immediate resets need no clock or asynchronous lookup. Validate once under
        // the position-write lock; duration lookups still validate before and after awaiting.
        var resetOffset = policy == AutoOffsetReset.Earliest ? EarliestOffsetTimestamp : LatestOffsetTimestamp;
        if (TryApplyOffsetReset(
                partition, fetchBufferEpoch, allowedPendingFetchClearVersion,
                resetOffset, expectedLeader, expectedMetadataSnapshot))
        {
            LogOffsetOutOfRangeReset(partition.Topic, partition.Partition, GetAutoOffsetResetName());
        }
        return default;
    }

    private async ValueTask ResolveOffsetOutOfRangeAsync(
        TopicPartition partition,
        int fetchBufferEpoch,
        long allowedPendingFetchClearVersion,
        PartitionInfo? expectedLeader,
        CancellationToken cancellationToken)
    {
        // Reset fetch position based on auto.offset.reset policy. Without this, the
        // consumer would retry the same invalid offset forever.
        if (ShouldDropOffsetReset(
                partition,
                fetchBufferEpoch,
                allowedPendingFetchClearVersion,
                expectedLeader))
            return;

        var resetTimestamp = AutoOffsetResetStrategy.GetListOffsetsTimestamp(_options, DateTimeOffset.UtcNow, partition);
        var resetOffset = resetTimestamp;
        if (resetTimestamp != LatestOffsetTimestamp && resetTimestamp != EarliestOffsetTimestamp)
            resetOffset = await ResolveAutoResetOffsetAsync(partition, resetTimestamp, cancellationToken).ConfigureAwait(false);

        if (!TryApplyOffsetReset(
                partition,
                fetchBufferEpoch,
                allowedPendingFetchClearVersion,
                resetOffset,
                expectedLeader))
            return;

        LogOffsetOutOfRangeReset(partition.Topic, partition.Partition, GetAutoOffsetResetName());
    }

    private bool TryApplyOffsetReset(
        TopicPartition partition,
        int fetchBufferEpoch,
        long allowedPendingFetchClearVersion,
        long? resetOffset,
        PartitionInfo? expectedLeader,
        ClusterMetadataSnapshot? expectedMetadataSnapshot = null)
    {
        // Keep the final validation and position write atomic with diverging-epoch staging.
        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
        {
            // Only an authoritative leader error needs metadata synchronization.
            // Writers never take the consumer lock, so the order stays one-way.
            // Successful fetches and follower retries do not enter this reset path.
            var metadataLock = expectedLeader is null ? null : _metadataManager.Metadata.UpdateLock;
            var metadataLockTaken = false;
            try
            {
                if (metadataLock is not null)
                    Monitor.Enter(metadataLock, ref metadataLockTaken);
                if (ShouldDropOffsetReset(
                        partition,
                        fetchBufferEpoch,
                        allowedPendingFetchClearVersion,
                        expectedLeader, expectedMetadataSnapshot))
                    return false;

                BeforeOffsetResetCommitForTest?.Invoke();
                var offset = resetOffset ?? AutoOffsetResetStrategy.GetListOffsetsTimestamp(
                    _options, DateTimeOffset.UtcNow, partition);
                SetFetchPosition(partition, offset);
                SetPosition(partition, offset, dirty: false);
                ClearLastConsumedLeaderEpoch(partition);
                return true;
            }
            finally
            {
                if (metadataLockTaken)
                    Monitor.Exit(metadataLock!);
            }
        }
    }

    private bool ShouldDropOffsetReset(
        TopicPartition partition,
        int fetchBufferEpoch,
        long allowedPendingFetchClearVersion,
        PartitionInfo? expectedLeader,
        ClusterMetadataSnapshot? expectedMetadataSnapshot = null)
    {
        if (IsFetchBufferEpochStale(partition, fetchBufferEpoch)
            || PendingFetchClearVersionChanged(partition, allowedPendingFetchClearVersion)
            || !IsCurrentlyAssigned(partition))
            return true;

        if (expectedLeader is not null
            && (expectedMetadataSnapshot is null
                || !ReferenceEquals(_metadataManager.Metadata.CaptureSnapshot(), expectedMetadataSnapshot))
            && !IsCurrentOffsetResetLeader(partition, expectedLeader))
        {
            // A leader change after request construction also invalidates cached
            // broker grouping, including after an asynchronous duration lookup.
            InvalidatePartitionCache();
            return true;
        }

        return false;
    }

    private bool IsCurrentOffsetResetLeader(TopicPartition partition, PartitionInfo expectedLeader)
    {
        // Recheck after an asynchronous duration-based lookup as well as before it.
        var current = _metadataManager.Metadata.GetPartitionInfo(partition.Topic, partition.Partition);
        return current is not null
            && current.LeaderId == expectedLeader.LeaderId
            && current.LeaderEpoch == expectedLeader.LeaderEpoch;
    }

    private bool PendingFetchClearVersionChanged(
        TopicPartition partition,
        long allowedPendingFetchClearVersion) =>
        _coordinatorRevokedPartitionsPendingFetchClear.TryGetValue(partition, out var currentVersion)
        && currentVersion != allowedPendingFetchClearVersion;

    private static bool IsLeaderEpochRefreshError(ErrorCode errorCode) =>
        errorCode is ErrorCode.NotLeaderOrFollower or ErrorCode.FencedLeaderEpoch or ErrorCode.UnknownLeaderEpoch;

    private static bool IsTopicIdentityRefreshError(ErrorCode errorCode) =>
        errorCode is ErrorCode.UnknownTopicId or ErrorCode.UnknownTopicOrPartition;

    private void QueueTopicIdentityRefresh(
        ref Dictionary<string, Guid>? topicIdentityRefreshes,
        string topic,
        Guid rejectedTopicId,
        TopicPartition partition)
    {
        ClearPreferredReadReplica(partition);
        topicIdentityRefreshes ??= new Dictionary<string, Guid>(StringComparer.Ordinal);
        topicIdentityRefreshes.TryAdd(topic, rejectedTopicId);
    }

    private async ValueTask HandleTopicIdentityRefreshesAsync(
        Dictionary<string, Guid> rejectedTopics,
        FetchSessionHandler? fetchSessionHandler,
        CancellationToken cancellationToken)
    {
        fetchSessionHandler?.HandleError();

        await _metadataManager.RefreshMetadataAsync(
            rejectedTopics.Keys,
            forceRefresh: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        InvalidatePartitionCache();
        await HandleRejectedTopicIdentityChangesAsync(rejectedTopics, cancellationToken)
            .ConfigureAwait(false);
    }

    private ValueTask HandleLeaderEpochRefreshAsync(
        string topic,
        FetchResponsePartition partitionResponse,
        IReadOnlyList<NodeEndpoint> nodeEndpoints)
    {
        ClearPreferredReadReplica(new TopicPartition(topic, partitionResponse.PartitionIndex));

        var currentLeader = partitionResponse.CurrentLeader;
        var endpoint = currentLeader is null
            ? null
            : LeaderDiscoveryFields.FindNodeEndpoint(nodeEndpoints, currentLeader.LeaderId);

        var updated = currentLeader is not null
            && _metadataManager.TryUpdatePartitionLeader(
                topic,
                partitionResponse.PartitionIndex,
                currentLeader.LeaderId,
                currentLeader.LeaderEpoch,
                endpoint);

        InvalidatePartitionCache();
        LogLeaderEpochRefresh(topic, partitionResponse.PartitionIndex, partitionResponse.ErrorCode);

        if (!updated)
            ScheduleLeaderRefresh(topic);

        return default;
    }

    private void UpdatePreferredReadReplica(string topic, FetchResponsePartition partitionResponse)
    {
        var partition = new TopicPartition(topic, partitionResponse.PartitionIndex);

        if (partitionResponse.ErrorCode != ErrorCode.None)
        {
            ClearPreferredReadReplica(partition);
            return;
        }

        if (string.IsNullOrEmpty(_options.ClientRack)
            || partitionResponse.PreferredReadReplica < 0)
        {
            ClearPreferredReadReplica(partition);
            return;
        }

        var metadata = _metadataManager.Metadata;
        var partitionInfo = metadata.GetPartitionInfo(topic, partitionResponse.PartitionIndex);
        if (partitionInfo is not null && partitionResponse.PreferredReadReplica == partitionInfo.LeaderId)
        {
            ClearPreferredReadReplica(partition);
            return;
        }

        if (metadata.GetBroker(partitionResponse.PreferredReadReplica) is null)
        {
            ClearPreferredReadReplica(partition);
            return;
        }

        var state = new PreferredReadReplicaState(
            partitionResponse.PreferredReadReplica,
            metadata.LastRefreshed,
            Stopwatch.GetTimestamp() + s_preferredReadReplicaMaxAgeTimestampDelta);

        var changed = !_preferredReadReplicas.TryGetValue(partition, out var existing)
            || existing.ReplicaId != state.ReplicaId;

        _preferredReadReplicas[partition] = state;

        if (changed)
        {
            InvalidatePartitionCache();
            LogPreferredReadReplicaSelected(topic, partitionResponse.PartitionIndex, state.ReplicaId);
        }
    }

    private void ClearPreferredReadReplica(TopicPartition partition)
    {
        if (_preferredReadReplicas.TryRemove(partition, out var existing))
        {
            InvalidatePartitionCache();
            LogPreferredReadReplicaCleared(partition.Topic, partition.Partition, existing.ReplicaId);
        }
    }

    private void ClearPreferredReadReplicasForBroker(int brokerId, IReadOnlyList<TopicPartition> partitions)
        => ClearPreferredReadReplicasForBroker(brokerId, partitions, 0, partitions.Count);

    private void ClearPreferredReadReplicasForBroker(
        int brokerId,
        IReadOnlyList<TopicPartition> partitions,
        int startIndex,
        int count)
    {
        var endIndex = startIndex + count;
        for (var i = startIndex; i < endIndex; i++)
        {
            var partition = partitions[i];
            if (_preferredReadReplicas.TryGetValue(partition, out var existing)
                && existing.ReplicaId == brokerId)
            {
                ClearPreferredReadReplica(partition);
            }
        }
    }

    private void ScheduleLeaderRefresh(string topic)
    {
        if (Volatile.Read(ref _consumerDisposed) != 0 || Volatile.Read(ref _closed) != 0)
            return;

        TaskCompletionSource refreshCompletion;
        CancellationToken cancellationToken;

        lock (_leaderRefreshTasksLock)
        {
            if (Volatile.Read(ref _consumerDisposed) != 0 || Volatile.Read(ref _closed) != 0)
                return;

            if (_pendingLeaderRefreshTasks.ContainsKey(topic))
                return;

            try
            {
                cancellationToken = _leaderRefreshCts.Token;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            refreshCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingLeaderRefreshTasks[topic] = refreshCompletion.Task;
        }

        _ = ExecuteLeaderRefreshAsync(topic, refreshCompletion, cancellationToken);
    }

    private async Task ExecuteLeaderRefreshAsync(
        string topic,
        TaskCompletionSource refreshCompletion,
        CancellationToken cancellationToken)
    {
        try
        {
            await _metadataManager.RefreshMetadataAsync([topic], forceRefresh: true, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            // Best-effort refresh; the next fetch cycle will retry leader resolution.
        }
        finally
        {
            lock (_leaderRefreshTasksLock)
            {
                if (_pendingLeaderRefreshTasks.TryGetValue(topic, out var task)
                    && ReferenceEquals(task, refreshCompletion.Task))
                {
                    _pendingLeaderRefreshTasks.TryRemove(topic, out _);
                }
            }

            refreshCompletion.TrySetResult();
        }
    }

    private async ValueTask WaitForLeaderRefreshTasksAsync(CancellationToken cancellationToken = default)
    {
        Task[] refreshTasks;
        lock (_leaderRefreshTasksLock)
        {
            if (_pendingLeaderRefreshTasks.IsEmpty)
                return;

            refreshTasks = [.. _pendingLeaderRefreshTasks.Values];
        }

        try
        {
            await Task.WhenAll(refreshTasks)
                .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            // Leader refresh is best-effort. Shutdown cancels the refresh token before this wait;
            // if it still outlives the bound, teardown continues and the refresh task observes
            // any late dependency-disposal exception internally.
        }
    }

    private List<FetchRequestTopic> BuildFetchRequestTopics(
        List<TopicPartition> partitions, int startIndex, int count, int brokerId)
        => BuildFetchRequestTopicsForConnection(partitions, startIndex, count, brokerId, connectionIndex: 0);

    private List<FetchRequestTopic> BuildFetchRequestTopicsWithSnapshot(
        List<TopicPartition> partitions,
        int startIndex,
        int count,
        int brokerId,
        out ClusterMetadataSnapshot metadataSnapshot)
        => BuildFetchRequestTopicsForConnectionWithSnapshot(
            partitions,
            startIndex,
            count,
            brokerId,
            connectionIndex: 0,
            out metadataSnapshot);

    private List<FetchRequestTopic> BuildFetchRequestTopicsForConnection(
        List<TopicPartition> partitions, int startIndex, int count, int brokerId, int connectionIndex)
    {
        var metadataSnapshot = _metadataManager.Metadata.CaptureSnapshot();
        return BuildFetchRequestTopicsForConnectionCore(
            partitions,
            startIndex,
            count,
            brokerId,
            connectionIndex,
            metadataSnapshot);
    }

    private List<FetchRequestTopic> BuildFetchRequestTopicsForConnectionWithSnapshot(
        List<TopicPartition> partitions,
        int startIndex,
        int count,
        int brokerId,
        int connectionIndex,
        out ClusterMetadataSnapshot metadataSnapshot)
    {
        metadataSnapshot = _metadataManager.Metadata.CaptureSnapshot();
        return BuildFetchRequestTopicsForConnectionCore(
            partitions,
            startIndex,
            count,
            brokerId,
            connectionIndex,
            metadataSnapshot);
    }

    private List<FetchRequestTopic> BuildFetchRequestTopicsForConnectionCore(
        List<TopicPartition> partitions,
        int startIndex,
        int count,
        int brokerId,
        int connectionIndex,
        ClusterMetadataSnapshot metadataSnapshot)
    {
        if (count == 0)
            return ConsumerFetchPools.RentFetchRequestTopicList(0);

        var orderState = _fetchPartitionOrderStates.GetOrAdd(
            (brokerId, connectionIndex),
            static _ => new FetchPartitionOrderState());
        var rotation = orderState.GetAndAdvance();
        var cacheKey = new FetchRequestCacheKey(brokerId, startIndex, count);

        // Take snapshots of current state under lock
        FetchRequestTemplateCacheEntry? cachedEntry;

        lock (_fetchCacheLock)
        {
            _fetchRequestTemplateCache.TryGetValue(cacheKey, out cachedEntry);
        }

        // Check if cache is valid for this range. Multi-connection fetches use deterministic
        // subranges, so stable assignments can reuse one template per connection group.
        if (cachedEntry is not null
            && PartitionRangeEquals(partitions, startIndex, count, cachedEntry.Partitions))
        {
            var cachedRotationStart = cachedEntry.RotationStarts[(int)((uint)rotation % (uint)cachedEntry.RotationStarts.Count)];
            // Cache hit: build a fresh result list with snapshot offsets under lock.
            // Each broker task gets its own FetchRequestPartition objects so that
            // concurrent calls cannot mutate offsets visible to another task.
            // This allocates per fetch cycle (per-batch), not per-message.
            return cachedRotationStart == default
                ? BuildFetchResult(
                    cachedEntry.TopicPartitions,
                    _fetchPositions,
                    _adaptiveFetchSizer?.CurrentPartitionFetchBytes,
                    metadataSnapshot: metadataSnapshot,
                    lastConsumedLeaderEpochs: _lastConsumedLeaderEpochs,
                    lastFetchedLeaderEpochs: _lastFetchedLeaderEpochs)
                : BuildRotatedFetchResult(
                    cachedEntry.TopicPartitions,
                    _fetchPositions,
                    cachedEntry.TopicOrder,
                    cachedRotationStart.TopicIndex,
                    cachedRotationStart.PartitionIndex,
                    _adaptiveFetchSizer?.CurrentPartitionFetchBytes,
                    metadataSnapshot: metadataSnapshot,
                    lastConsumedLeaderEpochs: _lastConsumedLeaderEpochs,
                    lastFetchedLeaderEpochs: _lastFetchedLeaderEpochs);
        }

        // Cache miss: build fresh structure with TopicPartition stored alongside.
        var topicPartitions = new Dictionary<string, List<(FetchRequestPartition Partition, TopicPartition TopicPartition)>>();
        var topicIndexes = new Dictionary<string, int>();
        var topicOrder = new List<string>();
        var rotationStarts = new List<(int TopicIndex, int PartitionIndex)>(count);
        var rangePartitions = new List<TopicPartition>(count);

        var endIndex = startIndex + count;
        for (var i = startIndex; i < endIndex; i++)
        {
            var p = partitions[i];
            rangePartitions.Add(p);
            if (!topicPartitions.TryGetValue(p.Topic, out var list))
            {
                list = [];
                topicPartitions[p.Topic] = list;
                topicIndexes[p.Topic] = topicOrder.Count;
                topicOrder.Add(p.Topic);
            }

            rotationStarts.Add((topicIndexes[p.Topic], list.Count));

            list.Add((
                new FetchRequestPartition
                {
                    Partition = p.Partition,
                    FetchOffset = 0, // Placeholder; BuildFetchResult reads fresh from _fetchPositions
                    PartitionMaxBytes = _adaptiveFetchSizer?.CurrentPartitionFetchBytes ?? _options.MaxPartitionFetchBytes
                },
                p // Store TopicPartition for reuse in hot path
            ));
        }

        // Build result with fresh copies so the caller owns its own FetchRequestPartition
        // instances. The cached dict stores templates; each caller gets independent copies
        // to prevent any shared-state issues with concurrent PrefetchFromBrokerAsync calls.
        var newRotationStart = rotationStarts[(int)((uint)rotation % (uint)rotationStarts.Count)];
        var result = newRotationStart == default
            ? BuildFetchResult(
                topicPartitions,
                _fetchPositions,
                _adaptiveFetchSizer?.CurrentPartitionFetchBytes,
                metadataSnapshot: metadataSnapshot,
                lastConsumedLeaderEpochs: _lastConsumedLeaderEpochs,
                lastFetchedLeaderEpochs: _lastFetchedLeaderEpochs)
            : BuildRotatedFetchResult(
                topicPartitions,
                _fetchPositions,
                topicOrder,
                newRotationStart.TopicIndex,
                newRotationStart.PartitionIndex,
                _adaptiveFetchSizer?.CurrentPartitionFetchBytes,
                metadataSnapshot: metadataSnapshot,
                lastConsumedLeaderEpochs: _lastConsumedLeaderEpochs,
                lastFetchedLeaderEpochs: _lastFetchedLeaderEpochs);

        // Update cache if this range is new or the cached partition range is stale.
        lock (_fetchCacheLock)
        {
            if (!_fetchRequestTemplateCache.TryGetValue(cacheKey, out var currentEntry)
                || !PartitionRangeEquals(partitions, startIndex, count, currentEntry.Partitions))
            {
                _fetchRequestTemplateCache[cacheKey] = new FetchRequestTemplateCacheEntry(
                    rangePartitions,
                    topicPartitions,
                    topicOrder,
                    rotationStarts);
            }
        }

        return result;
    }

    private readonly record struct FetchRequestCacheKey(int BrokerId, int StartIndex, int Count);

    private sealed class FetchRequestTemplateCacheEntry(
        List<TopicPartition> partitions,
        Dictionary<string, List<(FetchRequestPartition Partition, TopicPartition TopicPartition)>> topicPartitions,
        List<string> topicOrder,
        List<(int TopicIndex, int PartitionIndex)> rotationStarts)
    {
        public List<TopicPartition> Partitions { get; } = partitions;

        public Dictionary<string, List<(FetchRequestPartition Partition, TopicPartition TopicPartition)>> TopicPartitions { get; }
            = topicPartitions;

        public List<string> TopicOrder { get; } = topicOrder;

        public List<(int TopicIndex, int PartitionIndex)> RotationStarts { get; } = rotationStarts;
    }

    private sealed class FetchPartitionOrderState
    {
        private int _nextRotation = -1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetAndAdvance() => Interlocked.Increment(ref _nextRotation);
    }

    /// <summary>
    /// Resolves the topic name for a fetch response topic. Topic-ID-keyed responses are mapped
    /// through the request metadata snapshot, then the fetch session's ID-to-name cache; an
    /// unresolvable ID falls back to the name carried on the response.
    /// </summary>
    private string ResolveTopicName(
        FetchResponseTopic topicResponse,
        ClusterMetadataSnapshot requestMetadataSnapshot,
        FetchSessionHandler? fetchSessionHandler)
    {
        if (topicResponse.TopicId != Guid.Empty)
        {
            if (requestMetadataSnapshot.TopicsById.TryGetValue(topicResponse.TopicId, out var requestTopic))
            {
                return requestTopic.Name;
            }

            if (fetchSessionHandler?.TryResolveResponseTopicName(topicResponse.TopicId, out var sessionTopic) == true)
            {
                return sessionTopic;
            }

            LogUnknownTopicId(topicResponse.TopicId);
            return topicResponse.Topic ?? string.Empty;
        }

        return topicResponse.Topic ?? string.Empty;
    }

    /// <summary>
    /// Builds a fresh <see cref="FetchRequestTopic"/> list from a partition template dictionary.
    /// Works for both the shared cache and freshly-built dictionaries (cache-miss path) —
    /// in both cases, each call creates new <see cref="FetchRequestPartition"/> objects with
    /// snapshot offsets from <paramref name="fetchPositions"/>, so concurrent callers
    /// cannot observe each other's offset values.
    /// Allocation is per fetch cycle (per-batch), not per-message.
    /// </summary>
    internal static List<FetchRequestTopic> BuildFetchResult(
        Dictionary<string, List<(FetchRequestPartition Partition, TopicPartition TopicPartition)>> templateDict,
        ConcurrentDictionary<TopicPartition, long> fetchPositions,
        int? adaptivePartitionMaxBytes = null,
        ClusterMetadata? clusterMetadata = null,
        ConcurrentDictionary<TopicPartition, int>? lastConsumedLeaderEpochs = null,
        ClusterMetadataSnapshot? metadataSnapshot = null,
        ConcurrentDictionary<TopicPartition, FetchedLeaderEpoch>? lastFetchedLeaderEpochs = null)
    {
        metadataSnapshot ??= clusterMetadata?.CaptureSnapshot();
        var result = ConsumerFetchPools.RentFetchRequestTopicList(templateDict.Count);

        foreach (var kvp in templateDict)
        {
            var cachedPartitions = kvp.Value;
            var partitionList = ConsumerFetchPools.RentFetchRequestPartitionList(cachedPartitions.Count);

            foreach (var (template, tp) in cachedPartitions)
            {
                if (!fetchPositions.TryGetValue(tp, out var fetchOffset))
                    continue;

                var currentLeaderEpoch = GetLeaderEpoch(metadataSnapshot, tp);
                partitionList.Add(new FetchRequestPartition
                {
                    Partition = template.Partition,
                    FetchOffset = fetchOffset,
                    CurrentLeaderEpoch = currentLeaderEpoch,
                    LastFetchedEpoch = ResolveLastFetchedEpoch(
                        tp, fetchOffset, lastConsumedLeaderEpochs, lastFetchedLeaderEpochs),
                    LogStartOffset = template.LogStartOffset,
                    PartitionMaxBytes = adaptivePartitionMaxBytes ?? template.PartitionMaxBytes
                });
            }

            if (partitionList.Count == 0)
            {
                ConsumerFetchPools.ReturnFetchRequestPartitionList(partitionList);
                continue;
            }

            result.Add(new FetchRequestTopic
            {
                Topic = kvp.Key,
                TopicId = GetTopicId(metadataSnapshot, kvp.Key),
                Partitions = partitionList
            });
        }

        return result;
    }

    internal static List<FetchRequestTopic> BuildRotatedFetchResult(
        Dictionary<string, List<(FetchRequestPartition Partition, TopicPartition TopicPartition)>> templateDict,
        ConcurrentDictionary<TopicPartition, long> fetchPositions,
        List<string> topicOrder,
        int topicRotation,
        int partitionRotation,
        int? adaptivePartitionMaxBytes = null,
        ClusterMetadata? clusterMetadata = null,
        ConcurrentDictionary<TopicPartition, int>? lastConsumedLeaderEpochs = null,
        ClusterMetadataSnapshot? metadataSnapshot = null,
        ConcurrentDictionary<TopicPartition, FetchedLeaderEpoch>? lastFetchedLeaderEpochs = null)
    {
        metadataSnapshot ??= clusterMetadata?.CaptureSnapshot();
        var result = ConsumerFetchPools.RentFetchRequestTopicList(templateDict.Count);
        var topicCount = topicOrder.Count;
        var topicStart = (int)((uint)topicRotation % (uint)topicCount);
        for (var topicOffset = 0; topicOffset < topicCount; topicOffset++)
        {
            var topic = topicOrder[(topicStart + topicOffset) % topicCount];
            AddFetchRequestTopic(
                result,
                topic,
                templateDict[topic],
                fetchPositions,
                adaptivePartitionMaxBytes,
                metadataSnapshot,
                lastConsumedLeaderEpochs,
                lastFetchedLeaderEpochs,
                partitionRotation);
        }

        return result;
    }

    private static void AddFetchRequestTopic(
        List<FetchRequestTopic> result,
        string topic,
        List<(FetchRequestPartition Partition, TopicPartition TopicPartition)> cachedPartitions,
        ConcurrentDictionary<TopicPartition, long> fetchPositions,
        int? adaptivePartitionMaxBytes,
        ClusterMetadataSnapshot? metadataSnapshot,
        ConcurrentDictionary<TopicPartition, int>? lastConsumedLeaderEpochs,
        ConcurrentDictionary<TopicPartition, FetchedLeaderEpoch>? lastFetchedLeaderEpochs,
        int partitionRotation)
    {
        var partitionList = ConsumerFetchPools.RentFetchRequestPartitionList(cachedPartitions.Count);
        var partitionCount = cachedPartitions.Count;
        var partitionStart = (int)((uint)partitionRotation % (uint)partitionCount);

        for (var partitionIndex = partitionStart; partitionIndex < partitionCount; partitionIndex++)
            AddFetchRequestPartition(
                partitionList,
                cachedPartitions[partitionIndex],
                fetchPositions,
                adaptivePartitionMaxBytes,
                metadataSnapshot,
                lastConsumedLeaderEpochs,
                lastFetchedLeaderEpochs);

        for (var partitionIndex = 0; partitionIndex < partitionStart; partitionIndex++)
            AddFetchRequestPartition(
                partitionList,
                cachedPartitions[partitionIndex],
                fetchPositions,
                adaptivePartitionMaxBytes,
                metadataSnapshot,
                lastConsumedLeaderEpochs,
                lastFetchedLeaderEpochs);

        if (partitionList.Count == 0)
        {
            ConsumerFetchPools.ReturnFetchRequestPartitionList(partitionList);
            return;
        }

        result.Add(new FetchRequestTopic
        {
            Topic = topic,
            TopicId = GetTopicId(metadataSnapshot, topic),
            Partitions = partitionList
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddFetchRequestPartition(
        List<FetchRequestPartition> partitionList,
        (FetchRequestPartition Partition, TopicPartition TopicPartition) cachedPartition,
        ConcurrentDictionary<TopicPartition, long> fetchPositions,
        int? adaptivePartitionMaxBytes,
        ClusterMetadataSnapshot? metadataSnapshot,
        ConcurrentDictionary<TopicPartition, int>? lastConsumedLeaderEpochs,
        ConcurrentDictionary<TopicPartition, FetchedLeaderEpoch>? lastFetchedLeaderEpochs)
    {
        var (template, tp) = cachedPartition;
        if (!fetchPositions.TryGetValue(tp, out var fetchOffset))
            return;

        var currentLeaderEpoch = GetLeaderEpoch(metadataSnapshot, tp);
        partitionList.Add(new FetchRequestPartition
        {
            Partition = template.Partition,
            FetchOffset = fetchOffset,
            CurrentLeaderEpoch = currentLeaderEpoch,
            LastFetchedEpoch = ResolveLastFetchedEpoch(
                tp, fetchOffset, lastConsumedLeaderEpochs, lastFetchedLeaderEpochs),
            LogStartOffset = template.LogStartOffset,
            PartitionMaxBytes = adaptivePartitionMaxBytes ?? template.PartitionMaxBytes
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Guid GetTopicId(ClusterMetadataSnapshot? metadataSnapshot, string topic) =>
        metadataSnapshot is not null && metadataSnapshot.Topics.TryGetValue(topic, out var topicInfo)
            ? topicInfo.TopicId
            : Guid.Empty;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetLeaderEpoch(ClusterMetadataSnapshot? metadataSnapshot, TopicPartition partition)
    {
        if (metadataSnapshot is null
            || !metadataSnapshot.PartitionsByTopicIndex.TryGetValue(partition.Topic, out var partitions)
            || (uint)partition.Partition >= (uint)partitions.Length)
        {
            return -1;
        }

        return partitions[partition.Partition]?.LeaderEpoch ?? -1;
    }

    internal static List<ForgottenTopic> BuildForgottenTopicsData(
        Dictionary<string, List<int>> forgottenPartitions,
        ClusterMetadata? clusterMetadata = null)
    {
        var result = new List<ForgottenTopic>(forgottenPartitions.Count);

        foreach (var kvp in forgottenPartitions)
        {
            result.Add(new ForgottenTopic
            {
                Topic = kvp.Key,
                TopicId = clusterMetadata?.GetTopic(kvp.Key)?.TopicId ?? Guid.Empty,
                Partitions = [.. kvp.Value]
            });
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool PartitionRangeEquals(
        List<TopicPartition> partitions,
        int startIndex,
        int count,
        List<TopicPartition> cached)
    {
        if (count != cached.Count)
            return false;

        // For small lists, use O(n²) comparison to avoid HashSet allocation
        if (count <= 16)
        {
            var endIndex = startIndex + count;
            for (var i = startIndex; i < endIndex; i++)
            {
                var partition = partitions[i];
                var found = false;
                foreach (var cachedPartition in cached)
                {
                    if (partition.Topic == cachedPartition.Topic && partition.Partition == cachedPartition.Partition)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    return false;
            }
            return true;
        }

        // For larger lists, use HashSet for O(n) comparison
        var cachedSet = new HashSet<TopicPartition>(cached);
        var rangeEndIndex = startIndex + count;
        for (var i = startIndex; i < rangeEndIndex; i++)
        {
            if (!cachedSet.Contains(partitions[i]))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Invalidates the fetch request cache. Called when assignment or paused partitions change.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InvalidateFetchRequestCache()
    {
        lock (_fetchCacheLock)
        {
            _fetchRequestTemplateCache.Clear();
        }
    }

    private static List<TopicPartition>? RemovePartitions(
        List<TopicPartition> source,
        IReadOnlyList<TopicPartition> partitions)
    {
        List<TopicPartition>? updated = null;
        for (var i = 0; i < source.Count; i++)
        {
            var partition = source[i];
            if (ContainsPartition(partitions, partition))
            {
                if (updated is null)
                {
                    updated = new List<TopicPartition>(source.Count - 1);
                    for (var j = 0; j < i; j++)
                        updated.Add(source[j]);
                }

                continue;
            }

            updated?.Add(partition);
        }

        return updated;
    }

    private static bool ContainsPartition(IEnumerable<TopicPartition> partitions, TopicPartition partition)
    {
        foreach (var candidate in partitions)
        {
            if (candidate == partition)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Ratchets process-global consumer pool sizes based on actual partition count.
    /// Pending fetch and record wrapper pools grow process-wide; the per-instance
    /// CTS pool is fixed at construction and cannot be resized after creation.
    /// </summary>
    private void RatchetConsumerPoolSizes(int partitionCount)
    {
        var sizes = PoolSizing.ForConsumer(partitionCount);
        PendingFetchData.RatchetPoolSize(
            sizes.FetchDataPool,
            sizes.ParsedRecordSlabsPerBucket);
        RatchetRecordWrapperPools(partitionCount);
    }

    private void RatchetRecordWrapperPools(int partitionCount)
    {
        var prefetchMaxBytes = CalculatePrefetchMaxBytes(CurrentQueuedMaxBytes);
        var wrapperPoolSize = PoolSizing.ForConsumerRecordWrappers(prefetchMaxBytes);
        RecordBatch.RatchetPoolSize(wrapperPoolSize);
        LazyRecordList.RatchetPoolSize(wrapperPoolSize);
    }

    private void ReportAdaptiveProcessingComplete(TimeSpan processingDuration)
    {
        var sizer = _adaptiveFetchSizer;
        if (sizer is null)
            return;

        var previousPartitionFetchBytes = sizer.CurrentPartitionFetchBytes;
        var previousFetchMaxBytes = sizer.CurrentFetchMaxBytes;

        var pressureSignals = Interlocked.Exchange(ref _adaptiveFetchMemoryPressureSignals, 0);
        if (pressureSignals == 0)
        {
            sizer.ReportProcessingComplete(processingDuration);
        }
        else
        {
            for (var i = 0; i < pressureSignals; i++)
                sizer.ReportMemoryPressure();
        }

        if (sizer.CurrentPartitionFetchBytes > previousPartitionFetchBytes
            || sizer.CurrentFetchMaxBytes > previousFetchMaxBytes)
        {
            RatchetRecordWrapperPools(_assignmentSnapshot.Count);
        }
    }

    private async Task StartAutoCommitAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _consumerDisposed) != 0 || Volatile.Read(ref _closed) != 0)
            return;

        if (IsAutoCommitRunning())
            return;

        Task? oldTask;
        CancellationTokenSource? oldCts;
        lock (_autoCommitStartLock)
        {
            if (Volatile.Read(ref _consumerDisposed) != 0 || Volatile.Read(ref _closed) != 0)
                return;

            var currentTask = _autoCommitTask;
            if (IsAutoCommitRunning())
                return;

            oldTask = currentTask;
            oldCts = _autoCommitCts;

            _autoCommitCts = new CancellationTokenSource();
            _autoCommitTask = AutoCommitLoopAsync(_autoCommitCts.Token);
        }

        if (oldTask is not null)
        {
            try
            {
                await oldTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Swallow — old task may have completed from cancellation or fault.
            }
        }

        oldCts?.Dispose();
    }

    /// <summary>
    /// Background auto-commit timer loop. Offset-safety contract: this loop commits only
    /// stored offsets (<see cref="CommitStoredOffsetsAsync(CancellationToken)"/>), which are populated at
    /// fetch-boundary position flushes — i.e. only for records the caller has already
    /// iterated past. It never reads the active consumed snapshot, so a record that has
    /// been yielded but not yet processed (or prefetched but not yet yielded) can never be
    /// committed by this loop. Commits are skipped unless the coordinator is Stable, which
    /// prevents committing with a stale generation mid-rebalance. On failure it retries
    /// once after the configured request backoff, then waits for the next interval.
    /// </summary>
    private async Task AutoCommitLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.AutoCommitIntervalMs, cancellationToken).ConfigureAwait(false);
                await TryAutoCommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task TryAutoCommitAsync(CancellationToken cancellationToken)
    {
        // Only commit if coordinator is stable (fully joined)
        if (_coordinator is null || _coordinator.State != CoordinatorState.Stable)
            return;

        try
        {
            await CommitStoredOffsetsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogAutoCommitFailed(ex);
            try
            {
                var delayMs = ExponentialRetryBackoff.CalculateDelayMilliseconds(
                    _options.RetryBackoffMs,
                    _options.RetryBackoffMaxMs,
                    failureCount: 1);
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                await CommitStoredOffsetsAsync(cancellationToken).ConfigureAwait(false);
            }
            catch { /* Best effort — will retry on next interval */ }
        }
    }

    /// <inheritdoc />
    public ValueTask CloseAsync(CancellationToken cancellationToken = default) =>
        CloseAsync(new ConsumerCloseOptions(), cancellationToken);

    /// <inheritdoc />
    public async ValueTask CloseAsync(
        ConsumerCloseOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Enum.IsDefined(options.GroupMembershipOperation))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.GroupMembershipOperation,
                "The group membership operation is invalid.");
        }

        // Idempotent - return early if already closed/disposed
        if (Interlocked.Exchange(ref _closed, 1) != 0 || Volatile.Read(ref _consumerDisposed) != 0)
            return;

        using var apiTimeout = new ApiTimeoutScope(_options.DefaultApiTimeoutMs, cancellationToken);
        try
        {
            await CloseAsyncCore(options, _options.DefaultApiTimeoutMs, apiTimeout.Token).ConfigureAwait(false);
            apiTimeout.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException ex) when (apiTimeout.DefaultTimeoutExpired)
        {
            throw apiTimeout.CreateTimeoutException(nameof(CloseAsync), ex);
        }
    }

    /// <summary>
    /// Core teardown logic shared by <see cref="CloseAsync(CancellationToken)"/> and <see cref="DisposeAsync"/>.
    /// Callers must ensure this is invoked at most once via an atomic CAS on <c>_closed</c>.
    /// </summary>
    /// <param name="options">How the member leaves the group.</param>
    /// <param name="closeTimeoutMs">
    /// The budget that bounds <paramref name="cancellationToken"/>; part of it is kept back for
    /// the LeaveGroup request so the final commit cannot use it all.
    /// </param>
    /// <param name="cancellationToken">Cancelled by the caller or once the close budget runs out.</param>
    private async ValueTask CloseAsyncCore(
        ConsumerCloseOptions options,
        int closeTimeoutMs,
        CancellationToken cancellationToken)
    {
        var closeStartedAt = Stopwatch.GetTimestamp();
        LogClosingConsumer();
        _coordinator?.BeginClose();

        // Step 1: Stop the prefetch task first. It runs assignment synchronization, which could
        // otherwise rejoin a fenced member and start a new heartbeat after the steps below
        // stopped it. Its WaitAsync is bounded like step 4's: a mid-flight FetchAsync network
        // operation could hang for up to RequestTimeoutMs. The coordinator also refuses any
        // join once close has begun, whichever path asks.
        Task? prefetchTask;
        CancellationTokenSource? prefetchCts;
        lock (_prefetchStartLock)
        {
            prefetchCts = _prefetchCts;
            prefetchTask = _prefetchTask;
        }

        prefetchCts?.Cancel();
        if (prefetchTask is not null)
        {
            try
            {
                await prefetchTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Ignore — task may not exit promptly after cancellation
            }
        }

        // Step 2: Stop heartbeat background task
        if (_coordinator is not null)
        {
            await _coordinator.StopHeartbeatAsyncCore(cancellationToken).ConfigureAwait(false);

            // Rebalance callbacks the heartbeat stop interrupted (a fenced member's
            // OnPartitionsLost, say) are delivered now, whether or not a leave is sent.
            await _coordinator.InvokePendingRebalanceCallbacksUnlessCancelledAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        // Step 3: Stop leader-refresh tasks before metadata dependencies are disposed
        _leaderRefreshCts.Cancel();
        await WaitForLeaderRefreshTasksAsync(cancellationToken).ConfigureAwait(false);

        // Step 4: Stop auto-commit task
        // Use WaitAsync with both a hard timeout and the caller's cancellation token so that
        // DisposeAsync's 30s CTS actually bounds this step. Without this, a mid-flight
        // CommitAsync (which waits up to RequestTimeoutMs=30s for network I/O) would cause
        // CloseAsync to hang for the full network timeout, chaining across multiple consumers
        // during sequential `await using` disposal and exceeding test timeouts.
        Task? autoCommitTask;
        CancellationTokenSource? autoCommitCts;
        lock (_autoCommitStartLock)
        {
            autoCommitCts = _autoCommitCts;
            autoCommitTask = _autoCommitTask;
        }

        autoCommitCts?.Cancel();
        if (autoCommitTask is not null)
        {
            try
            {
                await autoCommitTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Ignore — task may not exit promptly after cancellation
            }
        }

        // Partitions the coordinator revoked or reported lost since the last assignment sync (a
        // fence's loss the drain in step 2 delivered, say). Their stored offsets all predate that
        // loss: records of a partition assigned again are consumed only after a sync, which
        // resets its position. So they are dropped, as the sync would drop them, and the shutdown
        // commit cannot send them. Only the ones the coordinator does not own again are left out
        // of the stop notification: a partition assigned again has its resources set up by the
        // assigned callback, and they are stopped like any other.
        HashSet<TopicPartition>? noLongerOwned = null;
        if (_coordinator?.PeekPartitionsRevokedSinceLastSync() is { } revokedSinceSync)
        {
            foreach (var partition in revokedSinceSync)
                ClearStoredOffset(partition);

            revokedSinceSync.ExceptWith(_coordinator.Assignment);
            if (revokedSinceSync.Count > 0)
                noLongerOwned = revokedSinceSync;
        }

        // Step 5: Notify partition-scoped resources of normal stop before final commit/leave.
        var partitionStopCancellation = await InvokePartitionStopListenerAsync(noLongerOwned, cancellationToken)
            .ConfigureAwait(false);

        var leavesGroup = _coordinator is not null &&
            options.GroupMembershipOperation != ConsumerGroupMembershipOperation.RemainInGroup;
        // Time is kept back only when a leave request will actually be sent: a member that never
        // joined (or uses manual assignment) sends none, so its commit keeps the whole budget.
        var sendsLeaveRequest = leavesGroup && _coordinator!.CanSendLeaveRequest;
        var leaveReserveMs = GetCloseLeaveGroupReserveMs(closeTimeoutMs);

        // Step 6: Commit pending offsets (if auto-commit enabled and we have a coordinator).
        // A coordinator that answers nothing would let the commit wait out the whole close
        // budget, and the leave after it would then be cancelled before it was sent. So when the
        // member is leaving, the commit stops early enough to keep leaveReserveMs of the budget
        // for step 7, as Java bounds its close commit and still sends the leave best-effort.
        if (_options.OffsetCommitMode == OffsetCommitMode.Auto && _coordinator is not null)
        {
            using var commitTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (sendsLeaveRequest && closeTimeoutMs > 0)
            {
                var elapsedMs = (long)Stopwatch.GetElapsedTime(closeStartedAt).TotalMilliseconds;
                commitTimeout.CancelAfter((int)Math.Max(0, closeTimeoutMs - leaveReserveMs - elapsedMs));
            }

            await CommitPendingOffsetsOnCloseAsync(commitTimeout.Token).ConfigureAwait(false);
            if (commitTimeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                LogCloseCommitStoppedForLeave(leaveReserveMs);
        }

        // Step 7: Send LeaveGroup request to coordinator. Once the request is on the wire the
        // coordinator acts on it whether or not the response is awaited, so close's token only
        // stops the wait for the response. Getting the request onto the wire (stopping the
        // heartbeat, leasing the connection, writing) is not cut short by it: whenever close is
        // cancelled (by the caller, or by its budget running out), before or during the leave,
        // that part gets leaveReserveMs more, rather than the leave being dropped and the member
        // holding its partitions until the session timeout. The earlier steps catch their own
        // cancellation, so a cancelled close always reaches this step. Rebalance callbacks still
        // queued are delivered only until close is cancelled; after that the leave goes first and
        // disposal delivers them.
        if (leavesGroup)
        {
            using var leaveTimeout = new CancellationTokenSource();
            using var leaveGrace = cancellationToken.Register(
                static state =>
                {
                    var (timeout, graceMs) = ((CancellationTokenSource, int))state!;
                    timeout.CancelAfter(graceMs);
                },
                (leaveTimeout, leaveReserveMs));
            try
            {
                await _coordinator!.LeaveGroupAsync(
                    options.GroupMembershipOperation,
                    leaveTimeout.Token,
                    cancellationToken).ConfigureAwait(false);
                LogLeftConsumerGroup();
            }
            catch (Exception ex)
            {
                LogLeaveGroupFailed(ex);
            }
        }

        // Step 8: Cancel any blocked fetch operations
        CancelActiveConsumeOperations();

        // Step 9: Clear local assignment and per-partition state
        ClearAssignmentAfterClose();

        // Step 10: Clear pending fetch data and dispose to release pooled memory
        while (_pendingFetches.TryDequeue(out var pending))
        {
            Interlocked.Decrement(ref _pendingFetchDepth);
            pending.Dispose();
        }
        while (_pausedPendingFetches.TryDequeue(out var pausedPending))
        {
            Interlocked.Decrement(ref _pendingFetchDepth);
            pausedPending.Dispose();
        }
        while (_heldSkippedFetches.TryDequeue(out var heldPending))
        {
            Interlocked.Decrement(ref _pendingFetchDepth);
            heldPending.Dispose();
        }
        while (_prefetchBuffer.TryRead(out var prefetched))
        {
            TrackPrefetchedBytes(prefetched, release: true);
            prefetched.Dispose();
        }

        await _telemetryManager.StopAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);

        LogConsumerClosed();

        if (partitionStopCancellation is not null)
            ExceptionDispatchInfo.Capture(partitionStopCancellation).Throw();
    }

    // The part of the close budget kept for LeaveGroup (see CloseAsyncCore step 6). At most half
    // the budget, so a short budget still leaves the commit at least the other half.
    private const int CloseLeaveGroupReserveMaxMs = 5_000;

    private static int GetCloseLeaveGroupReserveMs(int closeTimeoutMs) =>
        closeTimeoutMs <= 0
            ? CloseLeaveGroupReserveMaxMs
            : Math.Max(1, Math.Min(CloseLeaveGroupReserveMaxMs, closeTimeoutMs / 2));

    /// <summary>
    /// The final commit of close: up to three attempts with backoff. Never throws; a failure or
    /// a cancellation (including one during the backoff) ends it so that the leave and the
    /// remaining cleanup steps still run.
    /// </summary>
    private async ValueTask CommitPendingOffsetsOnCloseAsync(CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                // Close commits proven offsets only — never the in-doubt last yielded
                // record. Callers that processed everything and want a clean handoff
                // should call CommitAsync() before CloseAsync().
                if (await CommitProvenOffsetsAsync(cancellationToken).ConfigureAwait(false))
                {
                    LogCommittedPendingOffsets();
                }

                return;
            }
            catch (OperationCanceledException)
            {
                return; // Cancelled or out of budget — don't retry
            }
            catch (Exception ex)
            {
                LogCommitOffsetsDuringCloseFailed(ex, attempt);
                if (attempt == maxAttempts)
                    return;
            }

            try
            {
                var delayMs = ExponentialRetryBackoff.CalculateDelayMilliseconds(
                    _options.RetryBackoffMs,
                    _options.RetryBackoffMaxMs,
                    attempt);
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async ValueTask<OperationCanceledException?> InvokePartitionStopListenerAsync(
        HashSet<TopicPartition>? noLongerOwned,
        CancellationToken cancellationToken)
    {
        if (_options.RebalanceListener is not IPartitionStopListener listener)
            return null;

        var partitions = noLongerOwned is null
            ? _assignmentSnapshot.ToArray()
            : _assignmentSnapshot.Where(partition => !noLongerOwned.Contains(partition)).ToArray();
        if (partitions.Length == 0)
            return null;

        LogPartitionStopListenerCall(partitions.Length);
        using var listenerTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        listenerTimeout.CancelAfter(_options.PartitionStopTimeout);
        try
        {
            var listenerTask = listener.OnPartitionsStoppedAsync(partitions, listenerTimeout.Token).AsTask();
            await listenerTask.WaitAsync(listenerTimeout.Token).ConfigureAwait(false);
            return null;
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested && listenerTimeout.IsCancellationRequested)
        {
            LogPartitionStopListenerTimedOut(_options.PartitionStopTimeout);
            return null;
        }
        catch (OperationCanceledException ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            LogPartitionStopListenerCallbackError(ex);
            return null;
        }
    }

    private void ClearAssignmentAfterClose()
    {
        var assignment = _assignmentSnapshot;
        if (assignment.Count != 0)
        {
            RemoveAssignedPartitions(assignment, clearAll: true);
            return;
        }

        // Nothing was ever synchronized, but an OnPartitionsAssigned callback may still have staged
        // seeks or paused partitions for the assignment it announced.
        var hadPaused = false;
        SemaphoreHelper.AcquireOrThrowDisposed(_assignmentLock, nameof(KafkaConsumer<TKey, TValue>));
        try
        {
            lock (_snapshotStateGate)
                hadPaused = DiscardUnsynchronizedRebalanceState(synchronizedAssignment: []);
        }
        finally
        {
            SemaphoreHelper.ReleaseSafely(_assignmentLock);
        }

        if (hadPaused)
        {
            PublishPausedSnapshot();
            InvalidatePartitionCache();
            InvalidateFetchRequestCache();
        }
    }

    public async ValueTask<IReadOnlyDictionary<TopicPartition, long>> GetOffsetsForTimesAsync(
        IEnumerable<TopicPartitionTimestamp> timestampsToSearch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(timestampsToSearch);

        if (Volatile.Read(ref _consumerDisposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaConsumer<TKey, TValue>));

        ThrowIfNotInitialized();

        using var apiTimeout = new ApiTimeoutScope(_options.DefaultApiTimeoutMs, cancellationToken);
        try
        {
            var timestamps = timestampsToSearch as IReadOnlyList<TopicPartitionTimestamp>
                ?? [.. timestampsToSearch];

            return await RetryHelper.WithRetryAsync<IReadOnlyDictionary<TopicPartition, long>>(async () =>
            {
                // Group partitions by broker leader for efficient batch requests.
                // Retrying the whole operation re-groups after metadata refreshes.
                var partitionsByBroker = new Dictionary<int, List<TopicPartitionTimestamp>>();
                foreach (var tpt in timestamps)
                {
                    var leader = await _metadataManager.GetPartitionLeaderAsync(
                        tpt.Topic,
                        tpt.Partition,
                        apiTimeout.Token).ConfigureAwait(false);

                    if (leader is null)
                    {
                        LogNoLeaderFound(tpt.Topic, tpt.Partition);
                        continue;
                    }

                    if (!partitionsByBroker.TryGetValue(leader.NodeId, out var list))
                    {
                        list = [];
                        partitionsByBroker[leader.NodeId] = list;
                    }

                    list.Add(tpt);
                }

                var results = new Dictionary<TopicPartition, long>();

                // Send ListOffsets requests to each broker
                foreach (var (brokerId, partitions) in partitionsByBroker)
                {
                    var brokerResults = await GetOffsetsForTimesFromBrokerAsync(
                        brokerId,
                        partitions,
                        apiTimeout.Token).ConfigureAwait(false);

                    foreach (var kvp in brokerResults)
                    {
                        results[kvp.Key] = kvp.Value;
                    }
                }

                return results;
            }, _metadataManager, apiTimeout.Token, _options.RetryBackoffMs, _options.RetryBackoffMaxMs,
                deadline: OffsetLookupDeadline(nameof(GetOffsetsForTimesAsync), Timeout.InfiniteTimeSpan))
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (apiTimeout.DefaultTimeoutExpired)
        {
            throw apiTimeout.CreateTimeoutException(nameof(GetOffsetsForTimesAsync), ex);
        }
    }

    private async ValueTask<Dictionary<TopicPartition, long>> GetOffsetsForTimesFromBrokerAsync(
        int brokerId,
        List<TopicPartitionTimestamp> partitions,
        CancellationToken cancellationToken)
    {
        using var connectionLease = await _connectionPool.LeaseConnectionByIndexAsync(
            brokerId,
            0,
            cancellationToken).ConfigureAwait(false);
        var connection = connectionLease.Connection;

        var listOffsetsVersion = _metadataManager.GetNegotiatedApiVersion(
            connection,
            ApiKey.ListOffsets,
            ListOffsetsRequest.LowestSupportedVersion,
            ListOffsetsRequest.HighestSupportedVersion);

        // Group partitions by topic
        var topicPartitions = new Dictionary<string, List<ListOffsetsRequestPartition>>();
        foreach (var tpt in partitions)
        {
            if (!topicPartitions.TryGetValue(tpt.Topic, out var list))
            {
                list = [];
                topicPartitions[tpt.Topic] = list;
            }

            list.Add(new ListOffsetsRequestPartition
            {
                PartitionIndex = tpt.Partition,
                Timestamp = tpt.Timestamp,
                CurrentLeaderEpoch = GetCurrentLeaderEpoch(tpt.TopicPartition)
            });
        }

        // Build topics list
        var topics = new List<ListOffsetsRequestTopic>(topicPartitions.Count);
        foreach (var kvp in topicPartitions)
        {
            topics.Add(new ListOffsetsRequestTopic
            {
                Name = kvp.Key,
                Partitions = kvp.Value
            });
        }

        var request = new ListOffsetsRequest
        {
            ReplicaId = -1,
            IsolationLevel = _options.IsolationLevel,
            Topics = topics
        };

        var response = await connection.SendWithClientTelemetryAsync<ListOffsetsRequest, ListOffsetsResponse>(
            request,
            listOffsetsVersion, _telemetryMetricCollector,
            cancellationToken).ConfigureAwait(false);

        var results = new Dictionary<TopicPartition, long>();

        foreach (var topicResponse in response.Topics)
        {
            var topicName = topicResponse.Name;

            foreach (var partitionResponse in topicResponse.Partitions)
            {
                var tp = new TopicPartition(topicName, partitionResponse.PartitionIndex);

                if (partitionResponse.ErrorCode != ErrorCode.None)
                {
                    LogListOffsetsError(topicName, partitionResponse.PartitionIndex, partitionResponse.ErrorCode);
                    throw new Errors.ConsumeException(partitionResponse.ErrorCode,
                        $"ListOffsets failed for {topicName}-{partitionResponse.PartitionIndex}: {partitionResponse.ErrorCode}");
                }

                results[tp] = partitionResponse.Offset;
            }
        }

        return results;
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _consumerDisposed, 1) != 0)
            return;

        if (_options.IsAutoTuned)
            _memoryBudget.UnregisterConsumer(this);
        else
            _memoryBudget.ReleaseExplicit((ulong)_options.QueuedMaxMessagesKbytes * 1024);

        var disposeStart = Stopwatch.GetTimestamp();
        LogConsumerDisposing();

        // Unregister lag callback so the OTel SDK no longer invokes it on this disposed instance
        Diagnostics.DekafMetrics.UnregisterConsumerLagCallback(ObserveConsumerLag);
        Diagnostics.DekafMetrics.UnregisterConsumerFetchBufferState(_fetchBufferMetricSource);

        // If not already closed, perform graceful close first
        // Preserve the existing 30-second disposal cap while honoring a shorter configured API timeout.
        // Interlocked.Exchange prevents the TOCTOU gap where both CloseAsync and DisposeAsync
        // could race to run teardown concurrently when using Volatile.Read + separate CloseAsync CAS.
        if (Interlocked.Exchange(ref _closed, 1) == 0)
        {
            try
            {
                var closeTimeoutMs = Math.Min(_options.DefaultApiTimeoutMs, 30_000);
                using var cts = new CancellationTokenSource(closeTimeoutMs);
                await CloseAsyncCore(new ConsumerCloseOptions(), closeTimeoutMs, cts.Token).ConfigureAwait(false);
            }
            catch
            {
                // Ignore errors during dispose
            }
        }

        var closeElapsedMs = Stopwatch.GetElapsedTime(disposeStart).TotalMilliseconds;
        LogConsumerCloseCompleted(closeElapsedMs);

        CancelActiveConsumeOperations();
        _leaderRefreshCts.Cancel();
        Task? autoCommitTask;
        Task? prefetchTask;
        CancellationTokenSource? autoCommitCts;
        CancellationTokenSource? prefetchCts;
        lock (_autoCommitStartLock)
        {
            autoCommitCts = _autoCommitCts;
            autoCommitTask = _autoCommitTask;
        }
        lock (_prefetchStartLock)
        {
            prefetchCts = _prefetchCts;
            prefetchTask = _prefetchTask;
        }

        autoCommitCts?.Cancel();
        prefetchCts?.Cancel();

        await WaitForLeaderRefreshTasksAsync().ConfigureAwait(false);

        if (autoCommitTask is not null)
        {
            try
            {
                await autoCommitTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch
            {
                // Ignore — task may not exit promptly after cancellation
            }
        }

        if (prefetchTask is not null)
        {
            try
            {
                await prefetchTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch
            {
                // Ignore — task may not exit promptly after cancellation
            }
        }

        if (_connectionScaler is not null)
        {
            if (_ownsInfrastructure)
                await _connectionScaler.StopAndDrainAsync().ConfigureAwait(false);
            else
                await _connectionScaler.StopAndDrainAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            _connectionScaler.Dispose();
        }

        var retiredConnectionDisposals = _retiredConnectionDisposalTasks.Keys.ToArray();
        if (retiredConnectionDisposals.Length > 0)
        {
            try
            {
                await Task.WhenAll(retiredConnectionDisposals).ConfigureAwait(false);
            }
            catch
            {
                // Completion continuations observe individual disposal failures.
            }
        }

        autoCommitCts?.Dispose();
        prefetchCts?.Dispose();
        _leaderRefreshCts.Dispose();

        // Dispose CancellationTokenSource pool
        // Note: active consume cancellation sources are managed by the pool and should not be disposed here
        _ctsPool.Dispose();

        // Clear and dispose any pending fetch data to release pooled memory
        while (_pendingFetches.TryDequeue(out var pending))
        {
            Interlocked.Decrement(ref _pendingFetchDepth);
            pending.Dispose();
        }
        while (_pausedPendingFetches.TryDequeue(out var pausedPending))
        {
            Interlocked.Decrement(ref _pendingFetchDepth);
            pausedPending.Dispose();
        }
        while (_heldSkippedFetches.TryDequeue(out var heldPending))
        {
            Interlocked.Decrement(ref _pendingFetchDepth);
            heldPending.Dispose();
        }

        // Drain and dispose prefetch buffer items
        while (_prefetchBuffer.TryRead(out var prefetched))
        {
            TrackPrefetchedBytes(prefetched, release: true);
            prefetched.Dispose();
        }

        _prefetchBuffer.Dispose(prefetchTask);

        _fetchBufferMemoryPool.Dispose();

        _assignmentLock.Dispose();
        _initLock.Dispose();
        _topicIdentityLock.Dispose();
        _prefetchMemoryAvailable.Dispose();

        if (_coordinator is not null)
            await _coordinator.DisposeAsync().ConfigureAwait(false);

        await _telemetryManager.DisposeAsync().ConfigureAwait(false);
        if (_ownsInfrastructure)
        {
            await _metadataManager.DisposeAsync().ConfigureAwait(false);
            await _connectionPool.DisposeAsync().ConfigureAwait(false);
        }

        var disposeElapsedMs = Stopwatch.GetElapsedTime(disposeStart).TotalMilliseconds;
        LogConsumerDisposed(disposeElapsedMs);
    }

    #region Metrics

    /// <summary>
    /// Callback for the observable consumer lag gauge. Invoked only during metric collection
    /// (typically every 5-60s by an OTel exporter), not on the hot path.
    /// Returns one measurement per assigned partition: highWatermark - consumedPosition.
    /// </summary>
    private IEnumerable<Measurement<long>> ObserveConsumerLag()
    {
        foreach (var (tp, highWatermark) in _highWatermarks)
        {
            // Use consumed position (ConcurrentDictionary — safe for cross-thread reads).
            var consumedPosition = _positions.GetValueOrDefault(tp, 0);

            var lag = Math.Max(0, highWatermark - consumedPosition);

            yield return new Measurement<long>(lag,
                new System.Diagnostics.TagList
                {
                    { Diagnostics.DekafDiagnostics.MessagingDestinationName, tp.Topic },
                    { Diagnostics.DekafDiagnostics.MessagingDestinationPartitionId, tp.Partition },
                    { Diagnostics.DekafDiagnostics.MessagingConsumerGroupName, _options.GroupId }
                });
        }
    }

    #endregion

    #region Logging

    [LoggerMessage(Level = LogLevel.Warning, Message = "CommitAsync from a rebalance callback of Unsubscribe or a switch to manual assignment ran after the group was left (the callback overran the {RebalanceTimeoutMs} ms rebalance timeout); nothing was committed because the partitions may already belong to another member")]
    private partial void LogLeaveCallbackCommitAfterLeave(int rebalanceTimeoutMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error in prefetch loop")]
    private partial void LogPrefetchLoopError(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Fatal error prefetching from broker {BrokerId}")]
    private partial void LogFatalPrefetchError(Exception exception, int brokerId);

    [LoggerMessage(Message = "Failed to prefetch from broker {BrokerId}")]
    private partial void LogPrefetchFromBrokerError(Exception exception, int brokerId, LogLevel logLevel);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OffsetOutOfRange for {Topic}-{Partition}, resetting to {Reset}")]
    private partial void LogOffsetOutOfRangeReset(string topic, int partition, string reset);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Error} for {Topic}-{Partition}, refreshing leader metadata")]
    private partial void LogLeaderEpochRefresh(string topic, int partition, ErrorCode error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Log truncation detected for {Topic}-{Partition}; reset fetch position to offset {ResumeOffset} from broker correction {EndOffset} at leader epoch {LeaderEpoch}")]
    private partial void LogDivergingEpochReset(
        string topic,
        int partition,
        long resumeOffset,
        long endOffset,
        int leaderEpoch);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Recovered pending fetch-clear state with missing coordination markers")]
    private partial void LogRecoveredPendingFetchClearInvariant();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Preferred read replica {ReplicaId} selected for {Topic}-{Partition}")]
    private partial void LogPreferredReadReplicaSelected(string topic, int partition, int replicaId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Preferred read replica {ReplicaId} cleared for {Topic}-{Partition}")]
    private partial void LogPreferredReadReplicaCleared(string topic, int partition, int replicaId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Fetching {Topic}-{Partition} from preferred read replica {ReplicaId} instead of leader {LeaderId}")]
    private partial void LogUsingPreferredReadReplica(string topic, int partition, int replicaId, int leaderId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Prefetch error for {Topic}-{Partition}: {Error}")]
    private partial void LogPrefetchError(string topic, int partition, ErrorCode error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Pattern subscription matched {Count} topics: {Topics}")]
    private partial void LogPatternSubscriptionMatched(int count, string topics);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pattern subscription refresh failed; keeping the current {Count} matched topics and retrying in {RetryMs} ms")]
    private partial void LogPatternSubscriptionRefreshFailed(Exception exception, int count, long retryMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to fetch from broker {BrokerId}")]
    private partial void LogFetchFromBrokerError(Exception exception, int brokerId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Fetch session error from broker {BrokerId}: {Error}")]
    private partial void LogFetchSessionError(int brokerId, ErrorCode error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Fetch error for {Topic}-{Partition}: {Error}")]
    private partial void LogFetchError(string topic, int partition, ErrorCode error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Record parsing error for {Topic}-{Partition}, discarding fetch data and continuing")]
    private partial void LogRecordParsingError(Exception exception, string topic, int partition);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Consumer interceptor {Interceptor} OnConsume threw an exception")]
    private partial void LogInterceptorOnConsumeError(Exception exception, string interceptor);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Consumer interceptor {Interceptor} OnCommit threw an exception")]
    private partial void LogInterceptorOnCommitError(Exception exception, string interceptor);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Auto-commit failed, retrying once")]
    private partial void LogAutoCommitFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Committed stored offsets before partition revocation")]
    private partial void LogCommittedRevokedOffsets();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Not committing stored offset {Offset} for {Topic}[{Partition}]: the partition is not owned by this member")]
    private partial void LogStoredOffsetOfUnownedPartitionNotCommitted(string topic, int partition, long offset);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Ignoring StoreOffset for {Topic}[{Partition}]@{Offset}: the record was fetched under an ownership of the partition that has ended")]
    private partial void LogStoredOffsetOfEndedOwnershipIgnored(string topic, int partition, long offset);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Commit of revoked offsets timed out after {TimeoutMs}ms; continuing rebalance")]
    private partial void LogCommitRevokedOffsetsTimedOut(int timeoutMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to commit revoked offsets; continuing rebalance")]
    private partial void LogCommitRevokedOffsetsFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Closing consumer gracefully")]
    private partial void LogClosingConsumer();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Calling OnPartitionsStopped for {PartitionCount} partitions")]
    private partial void LogPartitionStopListenerCall(int partitionCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "OnPartitionsStopped partition stop listener callback threw an exception")]
    private partial void LogPartitionStopListenerCallbackError(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OnPartitionsStopped exceeded timeout {Timeout}; continuing shutdown")]
    private partial void LogPartitionStopListenerTimedOut(TimeSpan timeout);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Committed pending offsets during close")]
    private partial void LogCommittedPendingOffsets();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to commit offsets during close (attempt {Attempt}/3)")]
    private partial void LogCommitOffsetsDuringCloseFailed(Exception exception, int attempt);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Left consumer group during close")]
    private partial void LogLeftConsumerGroup();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Offset commit during close did not finish in time; stopped it to keep {LeaveReserveMs}ms of the close timeout for leaving the group")]
    private partial void LogCloseCommitStoppedForLeave(int leaveReserveMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to leave group during close")]
    private partial void LogLeaveGroupFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Consumer closed gracefully")]
    private partial void LogConsumerClosed();

    [LoggerMessage(Level = LogLevel.Warning, Message = "No leader found for {Topic}-{Partition}")]
    private partial void LogNoLeaderFound(string topic, int partition);

    [LoggerMessage(Level = LogLevel.Warning, Message = "TopicId {TopicId} not found in local metadata, falling back to topic name from response")]
    private partial void LogUnknownTopicId(Guid topicId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Topic identity changed for {Topic}-{Partition} from {PreviousTopicId} to {CurrentTopicId}; reset fetch position to {FetchPosition}")]
    private partial void LogTopicIdentityReset(
        string topic,
        int partition,
        Guid previousTopicId,
        Guid currentTopicId,
        long fetchPosition);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ListOffsets error for {Topic}-{Partition}: {Error}")]
    private partial void LogListOffsetsError(string topic, int partition, ErrorCode error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Prefetch paused: memory limit reached ({CurrentBytes}/{MaxBytes} bytes)")]
    private partial void LogPrefetchMemoryLimitPaused(long currentBytes, long maxBytes);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Assignment change: {Count} partitions added")]
    private partial void LogPartitionsAdded(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Assignment change: {Count} partitions removed")]
    private partial void LogPartitionsRemoved(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Committing offsets for {PartitionCount} partitions")]
    private partial void LogCommitStarted(int partitionCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Seeking {Topic}-{Partition} to offset {Offset}")]
    private partial void LogSeek(string topic, int partition, long offset);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Ignoring {Operation} of {Topic}-{Partition} from an OnPartitionsAssigned callback whose ownership of the partition has already ended")]
    private partial void LogStaleRebalanceCallbackCallIgnored(string operation, string topic, int partition);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consumer disposing: beginning shutdown")]
    private partial void LogConsumerDisposing();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consumer close phase completed in {ElapsedMs:F0}ms")]
    private partial void LogConsumerCloseCompleted(double elapsedMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Consumer disposed in {ElapsedMs:F0}ms")]
    private partial void LogConsumerDisposed(double elapsedMs);

    #endregion

    #region IRawRecordAccessor

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReadOnlyMemory<byte> NormalizeRawRecordBytes(ReadOnlyMemory<byte> bytes, bool isNull)
    {
        if (isNull)
            return default;
        return bytes.IsEmpty ? Array.Empty<byte>() : bytes;
    }

    void DeadLetter.IRawRecordAccessor.EnableRawRecordTracking()
    {
        _rawRecordTrackingEnabled = true;
    }

    bool DeadLetter.IRawRecordAccessor.TryGetCurrentRawRecord(
        out ReadOnlyMemory<byte> rawKey, out ReadOnlyMemory<byte> rawValue)
    {
        if (!_rawRecordTrackingEnabled)
        {
            rawKey = default;
            rawValue = default;
            return false;
        }

        rawKey = _currentRawKey;
        rawValue = _currentRawValue;
        return true;
    }

    #endregion
}
