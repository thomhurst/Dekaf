using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Dekaf.Compression;
using Dekaf.Errors;
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
    // LockFreeStack avoids the ConcurrentStack node allocation on every return to the pool.
    private const int DefaultMaxPoolSize = 128;
    private static int s_maxPoolSize = DefaultMaxPoolSize;
    private static LockFreeStack<PendingFetchData> s_pool = new(DefaultMaxPoolSize);
    private static readonly Lock s_resizeLock = new();

    internal static int MaxPoolSizeValue => Volatile.Read(ref s_maxPoolSize);

    internal static void RatchetPoolSize(int newSize)
    {
        InterlockedHelper.RatchetUp(ref s_maxPoolSize, newSize);

        var currentPool = Volatile.Read(ref s_pool);
        if (currentPool.Capacity < newSize)
        {
            lock (s_resizeLock)
            {
                currentPool = Volatile.Read(ref s_pool);
                if (currentPool.Capacity < newSize)
                {
                    var newPool = new LockFreeStack<PendingFetchData>(newSize);
                    // Drain existing pool into the new one. A thread holding a stale
                    // reference to currentPool may return an item after this drain but
                    // before the Volatile.Write below. That item is not migrated and can
                    // be GC'd. This is acceptable because resize runs only when consumer
                    // assignment raises the high-water mark, not per message; a one-time
                    // loss is recovered on demand via the miss path.
                    while (currentPool.TryPop(out var item))
                        newPool.TryPush(item);
                    Volatile.Write(ref s_pool, newPool);
                }
            }
        }
    }

    private IReadOnlyList<RecordBatch> _batches = null!;
    private Dictionary<long, Queue<long>>? _abortedProducers;
    private IPooledMemory? _memoryOwner;
    private int _batchIndex = -1;
    private int _recordIndex = -1;
    private int _disposed;
    private int _headerGeneration;
    private bool _eagerParsed;

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
    /// Tracks total bytes consumed in this pending fetch.
    /// </summary>
    public long TotalBytesConsumed { get; private set; }

    /// <summary>
    /// Tracks the number of messages yielded from this pending fetch.
    /// Using long to prevent overflow in long-running scenarios with large fetches.
    /// </summary>
    public long MessageCount { get; private set; }

    private long _emittedMessageCount;
    private long _emittedBytesConsumed;

    private PendingFetchData() { }

    /// <summary>
    /// Rents a PendingFetchData from the pool and initializes it with the given parameters.
    /// </summary>
    public static PendingFetchData Create(string topic, int partitionIndex, IReadOnlyList<RecordBatch> batches,
        IReadOnlyList<AbortedTransaction>? abortedTransactions = null,
        IPooledMemory? memoryOwner = null,
        string? activityName = null)
    {
        var instance = Rent();
        instance.Topic = topic;
        instance.PartitionIndex = partitionIndex;
        instance._activityName = activityName;
        instance.TopicPartition = new TopicPartition(topic, partitionIndex);
        instance._batches = batches;
        instance._memoryOwner = memoryOwner;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PendingFetchData Rent()
    {
        if (Volatile.Read(ref s_pool).TryPop(out var instance))
        {
            Volatile.Write(ref instance._disposed, 0);
            return instance;
        }
        return new PendingFetchData();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private string CreateActivityName()
    {
        var activityName = string.Concat(Topic, " receive");
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

    internal bool IsHeaderGenerationActive(int generation) =>
        Volatile.Read(ref _disposed) == 0 && Volatile.Read(ref _headerGeneration) == generation;

    /// <summary>
    /// Gets the current record via direct array access, bypassing the LazyRecordList
    /// indexer overhead (Volatile.Read + disposed check + EnsureParsedUpTo per access).
    /// Safe because EagerParseAll() is called before iteration begins.
    /// </summary>
    public Record CurrentRecord
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _currentRecordsArray is not null
            ? _currentRecordsArray[_recordIndex]
            : _currentRecords![_recordIndex];
    }

    // Cached batch state updated only on batch transitions, amortizing per-record cost.
    // _currentRecordsArray bypasses IReadOnlyList<Record> virtual dispatch + LazyRecordList
    // indexer overhead by caching the underlying Record[] directly.
    private Record[]? _currentRecordsArray;
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
    /// Eagerly parses all records in all batches at once.
    /// Call before sequential consumption to avoid per-record lazy parse overhead
    /// (disposed check + bounds check + EnsureParsedUpTo call per indexer access).
    /// This is a per-batch cost amortized over all records in the batch.
    /// </summary>
    public void EagerParseAll()
    {
        if (_eagerParsed)
            return;

        for (var i = 0; i < _batches.Count; i++)
        {
            if (_batches[i].Records is Protocol.Records.LazyRecordList lazyList)
            {
                lazyList.EnsureAllParsed();
            }
        }
        _eagerParsed = true;
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

        // Cache raw array for direct indexing (bypasses LazyRecordList indexer overhead).
        _currentRecordsArray = records is Protocol.Records.LazyRecordList lazyList
            ? lazyList.GetParsedArray()
            : null;

        CurrentBaseOffset = batch.BaseOffset;
        CurrentPartitionLeaderEpoch = batch.PartitionLeaderEpoch;
        CurrentBaseTimestamp = batch.BaseTimestamp;
        var attrs = batch.Attributes;
        CurrentTimestampType = (attrs & RecordBatchAttributes.TimestampTypeLogAppendTime) != 0
            ? TimestampType.LogAppendTime
            : TimestampType.CreateTime;
    }

    /// <summary>
    /// Advances to the next record across all batches.
    /// Returns false when no more records are available.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        Debug.Assert(Volatile.Read(ref _disposed) == 0, "MoveNext() called after Dispose()");

        // First call - start at first batch, first record
        if (_batchIndex < 0)
        {
            _batchIndex = 0;
            _recordIndex = 0;
            return HasCurrentRecord();
        }

        // Try next record in current batch (uses cached count to avoid Records property access)
        _recordIndex++;
        if (_recordIndex < _currentRecordsCount)
            return true;

        // Move to next batch
        _batchIndex++;
        _recordIndex = 0;
        return HasCurrentRecord();
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

        // Dispose all batches to mark them as disposed.
        // Indexing avoids boxing/enumerator allocation from the IReadOnlyList<T> interface.
        var batches = _batches;
        for (var i = 0; i < batches.Count; i++)
        {
            batches[i].Dispose();
        }

        // Return the batch list to the pool for reuse
        if (_batches is List<RecordBatch> batchList)
        {
            FetchResponsePartition.ReturnRecordBatchList(batchList);
        }

        // Release the pooled network buffer
        _memoryOwner?.Dispose();

        // Reset state for reuse
        _batches = null!;
        _memoryOwner = null;
        _batchIndex = -1;
        _recordIndex = -1;
        _currentRecordsArray = null;
        _currentRecords = null;
        _currentRecordsCount = 0;
        _eagerParsed = false;
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
        LastYieldedOffset = -1;
        LastYieldedLeaderEpoch = -1;
        TotalBytesConsumed = 0;
        MessageCount = 0;
        _emittedMessageCount = 0;
        _emittedBytesConsumed = 0;
        PartitionIndex = 0;
        TopicPartition = default;
        Topic = null!;
        _activityName = null;
        _abortedProducers?.Clear();

        Volatile.Read(ref s_pool).TryPush(this);
    }
}

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
    IConsumerPositions,
    IConsumerPartitions,
    IConsumerOffsets,
    DeadLetter.IRawRecordAccessor,
    IBudgetedInstance
{
    /// <summary>
    /// Delay in milliseconds when all assigned partitions are paused, to prevent
    /// a tight spin loop that would starve CPU while still allowing responsive
    /// cancellation and timeout handling (~10 checks per second).
    /// </summary>
    private const int AllPartitionsPausedDelayMs = 100;

    private readonly ConsumerOptions _options;

    // Current budget limit in bytes (mutated live by DekafMemoryBudget rebalancing).
    private long _currentQueuedMaxBytes;

    internal ulong CurrentQueuedMaxBytes => (ulong)Volatile.Read(ref _currentQueuedMaxBytes);

    void IBudgetedInstance.OnBudgetChanged(ulong newLimit)
    {
        Interlocked.Exchange(ref _currentQueuedMaxBytes, (long)newLimit);
    }

    private readonly IDeserializer<TKey> _keyDeserializer;
    private readonly IDeserializer<TValue> _valueDeserializer;
    private readonly IConnectionPool _connectionPool;
    private readonly MetadataManager _metadataManager;
    private readonly ClientTelemetryMetricCollector _telemetryMetricCollector;
    private readonly ClientTelemetryManager _telemetryManager;
    private readonly IDekafMemoryBudget _memoryBudget;
    private readonly bool _ownsInfrastructure;
    private readonly ConsumerCoordinator? _coordinator;
    private readonly CompressionCodecRegistry _compressionCodecs;
    private readonly ILogger _logger;

    private readonly ConcurrentDictionary<string, byte> _subscription = new();
    private readonly HashSet<TopicPartition> _assignment = [];
    private volatile IReadOnlySet<TopicPartition> _assignmentSnapshot = new HashSet<TopicPartition>();
    private readonly ConcurrentDictionary<TopicPartition, byte> _paused = new();
    private volatile IReadOnlySet<string> _subscriptionSnapshot = new HashSet<string>();
    private volatile IReadOnlySet<TopicPartition> _pausedSnapshot = new HashSet<TopicPartition>();

    // Pattern subscription support
    private volatile Func<string, bool>? _topicFilter;
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
    private readonly ConcurrentDictionary<TopicPartition, long> _dirtyPositions = new(); // Positions changed since last successful commit
    private readonly ConcurrentDictionary<TopicPartition, long> _fetchPositions = new(); // Fetch position (what to fetch next)
    // Last consumed record-batch leader epoch, sent as FetchRequest.LastFetchedEpoch.
    private readonly ConcurrentDictionary<TopicPartition, int> _lastConsumedLeaderEpochs = new();
    private readonly ConcurrentDictionary<TopicPartition, long> _committed = new();
    private readonly ConcurrentDictionary<TopicPartition, WatermarkOffsets> _watermarks = new(); // Cached watermark offsets from fetch responses


    // Partition EOF tracking
    private readonly ConcurrentDictionary<TopicPartition, long> _highWatermarks = new();  // High watermark per partition (thread-safe for prefetch)
    private readonly ConcurrentDictionary<TopicPartition, byte> _eofEmitted = new(); // Partitions where EOF has been emitted (lock-free)
    private readonly ConcurrentQueue<(TopicPartition Partition, long Offset)> _pendingEofEvents = new(); // Pending EOF events to yield (thread-safe for prefetch thread)

    // Pending fetch responses for lazy record iteration
    private readonly Queue<PendingFetchData> _pendingFetches = new();
    private readonly ConcurrentQueue<Exception> _pendingFetchExceptions = new();

    // Incremented whenever queued fetch data is disposed (Seek/Assign clear the buffer).
    // ConsumeAsync iterates the front of _pendingFetches while it is still queued, and
    // user code at the yield point can trigger such a clear; the version check lets the
    // iterator detect that its current fetch was disposed underneath it.
    private int _pendingFetchesVersion;

    // Background prefetch support
    private readonly MpscFetchBuffer _prefetchBuffer;
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
    // Stale entries from scaled-down connections are pruned lazily in PrefetchRecordsAsync.
    private readonly ConcurrentDictionary<(int BrokerId, int ConnectionIndex), List<PendingFetchData>> _prefetchPendingItemsByBroker = new();
    private readonly ConcurrentDictionary<(int BrokerId, int ConnectionIndex), FetchSessionHandler> _fetchSessions = new();

    // Lock ordering (always acquire in this order to prevent deadlocks):
    //   1. _initLock          — guards one-time initialization; never held while acquiring other locks
    //   2. _assignmentLock    — serializes assignment changes between the consume loop and prefetch loop
    //   3. _partitionCacheLock / _fetchCacheLock — guard per-broker partition cache and fetch request
    //      cache respectively; acquired under _assignmentLock (via InvalidatePartitionCache /
    //      InvalidateFetchRequestCache) and independently; never nested with each other
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly SemaphoreSlim _assignmentLock = new(1, 1);

    private readonly ConcurrentDictionary<CancellationTokenSource, byte> _activeConsumeCancellationSources = new();
    private readonly CancellationTokenSource _leaderRefreshCts = new();
    private CancellationTokenSource? _autoCommitCts;
    private Task? _autoCommitTask;
    private int _fetchApiVersion = -1;
    private readonly ConsumerConnectionScaler? _connectionScaler;
    private readonly AdaptiveFetchSizer? _adaptiveFetchSizer;

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

    // Cached metric tags per topic to avoid per-message TagList allocation
    // Plain Dictionary is safe: only accessed from the single ConsumeAsync loop thread
    private readonly Dictionary<string, System.Diagnostics.TagList> _metricTagsCache = [];
    private readonly ConcurrentDictionary<int, TagList> _fetchDurationMetricTagsCache = new();

    private const long EarliestOffsetTimestamp = -2;
    private const long LatestOffsetTimestamp = -1;

    // Cached activity names per topic to avoid repeated string interpolation in fetch paths
    // Instance-level to avoid unbounded growth with dynamic topic names across consumer instances
    private readonly ConcurrentDictionary<string, string> _activityNameCache = new();

    // Interceptors - stored as typed array for zero-allocation iteration
    private readonly IConsumerInterceptor<TKey, TValue>[]? _interceptors;

    // Incremental fetch responses can omit unchanged empty partitions; EOF mode needs those
    // empty partition responses to emit partition EOF events.
    private bool ShouldUseFetchSessions => _options.EnableFetchSessions && !_options.EnablePartitionEof;

    // Cached partition grouping by broker to avoid allocations on every fetch
    // Invalidated whenever _assignment, _paused, or preferred read replicas change.
    // Access to _cachedPartitionsByBroker must be synchronized via _partitionCacheLock
    private Dictionary<int, List<TopicPartition>>? _cachedPartitionsByBroker;
    private readonly object _partitionCacheLock = new();
    private int _assignmentVersion;

    // Fetch request cache - reduces allocations when partition assignment is stable
    // Cache is invalidated when assignment or paused partitions change
    private readonly object _fetchCacheLock = new();
    private Dictionary<string, List<(FetchRequestPartition Partition, TopicPartition TopicPartition)>>? _cachedTopicPartitions;
    private List<TopicPartition>? _cachedPartitionsList;
    private readonly ConcurrentDictionary<TopicPartition, PreferredReadReplicaState> _preferredReadReplicas = new();
    private readonly object _leaderRefreshTasksLock = new();
    private readonly ConcurrentDictionary<string, Task> _pendingLeaderRefreshTasks = new();
    private int _assignmentEnsureVersion;
    private int _lastManualAssignmentEnsureVersion = -1;
    private int _lastCoordinatorAssignmentVersion = -1;

    private static readonly long s_preferredReadReplicaMaxAgeTimestampDelta =
        (long)(TimeSpan.FromMinutes(5).TotalSeconds * Stopwatch.Frequency);

    private readonly record struct PreferredReadReplicaState(
        int ReplicaId,
        DateTimeOffset MetadataLastRefreshed,
        long ExpiresAtTimestamp);

    public KafkaConsumer(
        ConsumerOptions options,
        IDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer,
        ILoggerFactory? loggerFactory = null,
        MetadataOptions? metadataOptions = null)
        : this(options, keyDeserializer, valueDeserializer,
            CreateInfrastructure(options, loggerFactory, metadataOptions),
            loggerFactory,
            ownsInfrastructure: true,
            DekafMemoryBudget.Global)
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
            (connectionPool, metadataManager, new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Consumer)),
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
        ILoggerFactory? loggerFactory = null)
        : this(options, keyDeserializer, valueDeserializer,
            (connectionPool, metadataManager, new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Consumer)),
            loggerFactory,
            ownsInfrastructure: false,
            memoryBudget)
    {
    }

    private static (IConnectionPool, MetadataManager, ClientTelemetryMetricCollector) CreateInfrastructure(
        ConsumerOptions options, ILoggerFactory? loggerFactory, MetadataOptions? metadataOptions)
    {
        var telemetryMetricCollector = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Consumer);
        var connectionPool = new ConnectionPool(
            options.ClientId,
            new ConnectionOptions
            {
                UseTls = options.UseTls,
                TlsConfig = options.TlsConfig,
                RequestTimeout = TimeSpan.FromMilliseconds(options.RequestTimeoutMs),
                ReconnectBackoff = TimeSpan.FromMilliseconds(options.ReconnectBackoffMs),
                ReconnectBackoffMax = TimeSpan.FromMilliseconds(options.ReconnectBackoffMaxMs),
                SaslMechanism = options.SaslMechanism,
                SaslUsername = options.SaslUsername,
                SaslPassword = options.SaslPassword,
                GssapiConfig = options.GssapiConfig,
                OAuthBearerConfig = options.OAuthBearerConfig,
                OAuthBearerTokenProvider = options.OAuthBearerTokenProvider,
                SendBufferSize = options.SocketSendBufferBytes,
                ReceiveBufferSize = options.SocketReceiveBufferBytes
            },
            loggerFactory,
            connectionsPerBroker: options.ConnectionsPerBroker,
            ResponseBufferPool.Create(options.FetchMaxBytes),
            telemetryMetricCollector: telemetryMetricCollector);

        var metadataManager = new MetadataManager(
            connectionPool,
            options.BootstrapServers,
            options: metadataOptions,
            logger: loggerFactory?.CreateLogger<MetadataManager>());

        return (connectionPool, metadataManager, telemetryMetricCollector);
    }

    private KafkaConsumer(
        ConsumerOptions options,
        IDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer,
        (IConnectionPool Pool, MetadataManager Metadata, ClientTelemetryMetricCollector TelemetryMetricCollector) infrastructure,
        ILoggerFactory? loggerFactory,
        bool ownsInfrastructure,
        IDekafMemoryBudget memoryBudget)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.ConnectionsPerBroker, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.ConnectionsPerBroker, options.MaxConnectionsPerBroker);
        AutoOffsetResetStrategy.ValidateOptions(options);

        _options = options;
        _currentQueuedMaxBytes = (long)((ulong)options.QueuedMaxMessagesKbytes * 1024UL);
        _keyDeserializer = keyDeserializer;
        _valueDeserializer = valueDeserializer;

        // Derive consumer pool sizes from configuration
        // Use 64 as default partition count estimate — covers most workloads.
        // PendingFetchData uses a ratchet, so this only increases over time.
        var consumerSizes = PoolSizing.ForConsumer(maxPartitionCount: 64);
        PendingFetchData.RatchetPoolSize(consumerSizes.FetchDataPool);
        _ctsPool = new CancellationTokenSourcePool(consumerSizes.CancellationTokenSources);
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
        }

        _connectionPool = infrastructure.Pool;
        _metadataManager = infrastructure.Metadata;
        _ownsInfrastructure = ownsInfrastructure;
        _memoryBudget = memoryBudget;
        _telemetryMetricCollector = infrastructure.TelemetryMetricCollector;
        _telemetryMetricCollector.RegisterMetricsForSubscription(options.ApplicationMetrics);
        _telemetryManager = new ClientTelemetryManager(
            _connectionPool,
            _metadataManager,
            loggerFactory?.CreateLogger<ClientTelemetryManager>(),
            _telemetryMetricCollector);

        _compressionCodecs = CompressionCodecRegistry.Default;

        // Initialize adaptive connection scaler if configured (before coordinator, which needs the connection count)
        if (options.EnableAdaptiveConnections && options.MaxConnectionsPerBroker > options.ConnectionsPerBroker)
        {
            _connectionScaler = new ConsumerConnectionScaler(
                initialConnectionCount: options.ConnectionsPerBroker,
                maxConnectionCount: options.MaxConnectionsPerBroker,
                scaleUpAsync: async ct =>
                {
                    var newCount = _connectionScaler!.CurrentConnectionCount;
                    foreach (var broker in _metadataManager.Metadata.GetBrokers())
                        await _connectionPool.ScaleConnectionGroupAsync(broker.NodeId, newCount, ct).ConfigureAwait(false);
                },
                scaleDownAsync: async ct =>
                {
                    var newCount = _connectionScaler!.CurrentConnectionCount;
                    foreach (var broker in _metadataManager.Metadata.GetBrokers())
                        await _connectionPool.ShrinkConnectionGroupAsync(broker.NodeId, newCount, ct).ConfigureAwait(false);
                },
                logError: ex => _logger.LogWarning(ex, "Adaptive connection scaling operation failed"));
        }

        // Initialize adaptive fetch sizer if configured
        if (options.EnableAdaptiveFetchSizing)
        {
            var sizingOptions = options.AdaptiveFetchSizingOptions ?? new AdaptiveFetchSizingOptions
            {
                InitialPartitionFetchBytes = options.MaxPartitionFetchBytes,
                InitialFetchMaxBytes = options.FetchMaxBytes
            };
            _adaptiveFetchSizer = new AdaptiveFetchSizer(sizingOptions);
        }

        if (!string.IsNullOrEmpty(options.GroupId))
        {
            _coordinator = new ConsumerCoordinator(
                options,
                _connectionPool,
                _metadataManager,
                loggerFactory?.CreateLogger<ConsumerCoordinator>(),
                getConnectionCount: _connectionScaler is not null
                    ? () => _connectionScaler.CurrentConnectionCount
                    : null);
        }

        _prefetchBuffer = new MpscFetchBuffer(CalculatePrefetchBufferCapacity(options));

        // Register this instance's lag callback with the shared static gauge.
        // The callback is invoked only during metric collection (~every 5-60s), not on the hot path.
        Diagnostics.DekafMetrics.RegisterConsumerLagCallback(ObserveConsumerLag);
    }

    public IReadOnlySet<string> Subscription => _subscriptionSnapshot;
    public IReadOnlySet<TopicPartition> Assignment => _assignmentSnapshot;
    public string? MemberId => _coordinator?.MemberId;
    public IReadOnlySet<TopicPartition> Paused => _pausedSnapshot;
    public IConsumerPositions Positions => this;
    public IConsumerPartitions Partitions => this;
    public IConsumerOffsets Offsets => this;

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
            if (_coordinator is null || string.IsNullOrEmpty(_options.GroupId))
                return null;

            // Return null if not yet joined (no member ID assigned)
            if (string.IsNullOrEmpty(_coordinator.MemberId))
                return null;

            // Return null if generation ID is invalid (not yet in a stable group)
            if (_coordinator.GenerationId < 0)
                return null;

            return new ConsumerGroupMetadata
            {
                GroupId = _options.GroupId,
                GenerationId = _coordinator.GenerationId,
                MemberId = _coordinator.MemberId,
                GroupInstanceId = _options.GroupInstanceId
            };
        }
    }

    public void Subscribe(params string[] topics)
    {
        _topicFilter = null;
        _subscription.Clear();
        foreach (var topic in topics)
        {
            _subscription.TryAdd(topic, 0);
        }
        PublishSubscriptionSnapshot();
        SemaphoreHelper.AcquireOrThrowDisposed(_assignmentLock, nameof(KafkaConsumer<TKey, TValue>));
        try
        {
            _assignment.Clear();
            PublishAssignmentSnapshot();
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

        InvalidateFetchRequestCache();
    }

    public void Subscribe(Func<string, bool> topicFilter)
    {
        ArgumentNullException.ThrowIfNull(topicFilter);

        _topicFilter = topicFilter;
        _subscription.Clear();
        PublishSubscriptionSnapshot();
        SemaphoreHelper.AcquireOrThrowDisposed(_assignmentLock, nameof(KafkaConsumer<TKey, TValue>));
        try
        {
            _assignment.Clear();
            PublishAssignmentSnapshot();
        }
        finally
        {
            SemaphoreHelper.ReleaseSafely(_assignmentLock);
        }

        // Clear stale fetched data (same rationale as Assign).
        ClearFetchBuffer();

        _lastFilterRefreshTicks = 0; // Force immediate refresh on next EnsureAssignment
        InvalidatePartitionCache();
        InvalidateFetchRequestCache();
    }

    public void Unsubscribe()
    {
        _topicFilter = null;
        _subscription.Clear();
        PublishSubscriptionSnapshot();
        SemaphoreHelper.AcquireOrThrowDisposed(_assignmentLock, nameof(KafkaConsumer<TKey, TValue>));
        try
        {
            _assignment.Clear();
            PublishAssignmentSnapshot();
        }
        finally
        {
            SemaphoreHelper.ReleaseSafely(_assignmentLock);
        }

        // Clear stale fetched data (same rationale as Assign).
        ClearFetchBuffer();

        InvalidatePartitionCache();
        InvalidateFetchRequestCache();
    }

    public void Assign(params TopicPartition[] partitions)
    {
        _subscription.Clear();
        PublishSubscriptionSnapshot();
        SemaphoreHelper.AcquireOrThrowDisposed(_assignmentLock, nameof(KafkaConsumer<TKey, TValue>));
        try
        {
            _assignment.Clear();
            foreach (var partition in partitions)
            {
                _assignment.Add(partition);
            }
            PublishAssignmentSnapshot();
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

        InvalidatePartitionCache();
        InvalidateFetchRequestCache();
    }

    public void Unassign()
    {
        SemaphoreHelper.AcquireOrThrowDisposed(_assignmentLock, nameof(KafkaConsumer<TKey, TValue>));
        try
        {
            _assignment.Clear();
            PublishAssignmentSnapshot();
        }
        finally
        {
            SemaphoreHelper.ReleaseSafely(_assignmentLock);
        }

        // Clear stale fetched data (same rationale as Assign).
        ClearFetchBuffer();

        InvalidatePartitionCache();
        InvalidateFetchRequestCache();
    }

    public void IncrementalAssign(IEnumerable<TopicPartitionOffset> partitions)
    {
        // Clear subscription since we're doing manual assignment
        _subscription.Clear();
        PublishSubscriptionSnapshot();

        SemaphoreHelper.AcquireOrThrowDisposed(_assignmentLock, nameof(KafkaConsumer<TKey, TValue>));
        try
        {
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
                    _fetchPositions[tp] = tpo.Offset;
                }
                // Otherwise, positions will be initialized lazily based on auto.offset.reset
            }

            PublishAssignmentSnapshot();
        }
        finally
        {
            SemaphoreHelper.ReleaseSafely(_assignmentLock);
        }
        InvalidatePartitionCache();
        InvalidateFetchRequestCache();
    }

    public void IncrementalUnassign(IEnumerable<TopicPartition> partitions)
    {
        // Materialize once: the enumerable is iterated three times (assignment removal,
        // state cleanup, fetch buffer clear) and a forward-only sequence would silently
        // yield zero items on the second and third passes.
        var partitionList = partitions as IReadOnlyList<TopicPartition> ?? partitions.ToList();

        var hadPaused = false;
        SemaphoreHelper.AcquireOrThrowDisposed(_assignmentLock, nameof(KafkaConsumer<TKey, TValue>));
        try
        {
            foreach (var partition in partitionList)
            {
                _assignment.Remove(partition);
            }
            hadPaused = RemovePartitionState(partitionList);

            PublishAssignmentSnapshot();
        }
        finally
        {
            SemaphoreHelper.ReleaseSafely(_assignmentLock);
        }

        // Clear any pending fetch data for the removed partitions
        ClearFetchBufferForPartitions(partitionList);

        if (hadPaused)
            PublishPausedSnapshot();
        InvalidatePartitionCache();
        InvalidateFetchRequestCache();
    }

    public async IAsyncEnumerable<ConsumeResult<TKey, TValue>> ConsumeAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
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
            ThrowPendingFetchException();

            await EnsureAssignmentAsync(cancellationToken).ConfigureAwait(false);

            if (_assignmentSnapshot.Count == 0)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                continue;
            }

            // Get pending data - either from prefetch channel or direct fetch
            if (_pendingFetches.Count == 0)
            {
                if (prefetchEnabled)
                {
                    // Try to read from prefetch buffer, draining available items up to a bound
                    if (_prefetchBuffer.TryRead(out var prefetched))
                    {
                        _pendingFetches.Enqueue(prefetched);
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
                                    _pendingFetches.Enqueue(fetched);
                                    TrackPrefetchedBytes(fetched, release: true);
                                    DrainPrefetchBuffer();
                                }
                                else
                                {
                                    System.Diagnostics.Debug.Fail("WaitToRead signalled data available but TryRead returned false");
                                }
                            }
                            else if (_prefetchBuffer.IsCompleted)
                            {
                                // Buffer completed without error — prefetch pipeline has stopped.
                                // Reached when PrefetchPipelineRunner exits its loop normally
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

                ThrowPendingFetchException();
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

                while (_pendingFetches.Count > 0)
                {
                    var pending = _pendingFetches.Peek();
                    var pendingFetchesVersion = Volatile.Read(ref _pendingFetchesVersion);
                    long? batchProcessingStarted = _adaptiveFetchSizer is not null
                        ? System.Diagnostics.Stopwatch.GetTimestamp() : null;

                    // Eagerly parse all records in this fetch's batches upfront.
                    // This converts per-record lazy parse overhead (disposed check +
                    // bounds check + EnsureParsedUpTo per indexer call) into a single
                    // batch-level cost. Parsing is cache-friendly in a tight loop.
                    pending.EagerParseAll();

                    // Compiler requires definite assignment; always assigned inside try before yield.
                    ConsumeResult<TKey, TValue> nextResult = default!;

                    while (true)
                    {
                        // Wrap MoveNext + record parsing in try-catch so a corrupted fetch
                        // does not kill the consumer permanently. The yield must be outside
                        // the try block (CS1626), so we build the result first then yield below.
                        try
                        {
                            if (!pending.MoveNext())
                                break;

                            // Dispose previous message's activity (captures user processing time)
                            previousActivity?.Dispose();
                            previousActivity = null;

                            var record = pending.CurrentRecord;

                            // Use cached batch properties (updated once per batch transition
                            // in MoveNext) to avoid per-message property access overhead on
                            // RecordBatch (Volatile.Read + disposed check per access).
                            var offset = pending.CurrentBaseOffset + record.OffsetDelta;
                            var timestampMs = pending.CurrentBaseTimestamp + record.TimestampDelta;

                            // Use batch-level cached timestamp type (computed once per batch
                            // transition in CacheCurrentBatchState, not per message)
                            var timestampType = pending.CurrentTimestampType;

                            var messageBytes = (record.IsKeyNull ? 0 : record.Key.Length) +
                                               (record.IsValueNull ? 0 : record.Value.Length);

                            // Start consumer tracing activity — skip all tracing work when no listener
                            // (~2ns HasListeners() check vs ~200ns Activity creation + tag boxing per message)
                            // Uses hoisted hasTraceListeners to avoid per-message virtual dispatch
                            System.Diagnostics.Activity? activity = null;
                            if (hasTraceListeners)
                            {
                                var headers = LazyConsumeHeaders.Create(
                                    record.Headers,
                                    record.HeaderCount,
                                    pending,
                                    pending.HeaderGeneration);
                                activity = StartConsumeActivity(pending, headers, offset, messageBytes);
                                if (activity is not null)
                                    previousActivity = activity;
                            }

                            // Create result - deserialization happens eagerly in the constructor
                            nextResult = new ConsumeResult<TKey, TValue>(
                                topic: pending.Topic,
                                partition: pending.PartitionIndex,
                                offset: offset,
                                keyData: record.Key,
                                isKeyNull: record.IsKeyNull,
                                valueData: record.Value,
                                isValueNull: record.IsValueNull,
                                pooledHeaders: record.Headers,
                                pooledHeaderCount: record.HeaderCount,
                                headerOwner: pending,
                                timestampMs: timestampMs,
                                timestampType: timestampType,
                                leaderEpoch: pending.CurrentPartitionLeaderEpoch >= 0 ? pending.CurrentPartitionLeaderEpoch : null,
                                keyDeserializer: _keyDeserializer,
                                valueDeserializer: _valueDeserializer);

                            pending.TrackConsumed(offset, messageBytes);
                            // Position dictionaries are flushed once per fetch; manual readers
                            // flush this pending state on demand in GetPosition()/CommitAsync().

                            // Apply OnConsume interceptors before yielding to user
                            // Uses hoisted hasInterceptors to skip method call when no interceptors
                            if (hasInterceptors)
                                nextResult = ApplyOnConsumeInterceptors(nextResult);

                            // Store raw byte references for DLQ lazy capture (zero-copy — just memory slices)
                            if (rawTrackingEnabled)
                            {
                                _currentRawKey = record.IsKeyNull ? ReadOnlyMemory<byte>.Empty : record.Key;
                                _currentRawValue = record.IsValueNull ? ReadOnlyMemory<byte>.Empty : record.Value;
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex) when (ex is InsufficientDataException or ArgumentOutOfRangeException or MalformedProtocolDataException)
                        {
                            // Protocol-layer data errors from corrupted/truncated wire data should not
                            // kill the consumer. User-facing exceptions (deserializer errors, etc.)
                            // propagate normally to the caller.
                            previousActivity?.Dispose();
                            previousActivity = null;
                            LogRecordParsingError(ex, pending.Topic, pending.PartitionIndex);
                            break;
                        }

                        yield return nextResult;

                        // User code at the yield point may have called Seek/Assign, which
                        // clears _pendingFetches and disposes `pending` while it is still
                        // being iterated here; touching it again would read disposed
                        // pooled buffers.
                        if (Volatile.Read(ref _pendingFetchesVersion) != pendingFetchesVersion)
                            break;
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

                    // Record consumer metrics per-fetch instead of per-message.
                    // PendingFetchData already tracks MessageCount and TotalBytesConsumed,
                    // so we batch the Counter<T>.Add calls (virtual dispatch + bucket lookup)
                    // into a single pair per partition-fetch instead of per message.
                    if (metricsEnabled && pending.MessageCount > 0)
                        EmitFetchMetrics(pending);

                    // Report batch processing time to the adaptive fetch sizer (per-batch, not per-message)
                    if (batchProcessingStarted.HasValue)
                    {
                        var processingDuration = System.Diagnostics.Stopwatch.GetElapsedTime(batchProcessingStarted.Value);
                        _adaptiveFetchSizer!.ReportProcessingComplete(processingDuration);
                    }

                    // Dequeue and dispose the pending fetch (releases pooled network buffer memory)
                    _pendingFetches.Dequeue().Dispose();
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
            while (_pendingEofEvents.TryDequeue(out var eofEvent))
            {
                yield return ConsumeResult<TKey, TValue>.CreatePartitionEof(
                    eofEvent.Partition.Topic,
                    eofEvent.Partition.Partition,
                    eofEvent.Offset);
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

        ThrowIfNotInitialized();

        // Start auto-commit if enabled (only in Auto mode)
        if (_options.OffsetCommitMode == OffsetCommitMode.Auto && _coordinator is not null)
        {
            await StartAutoCommitAsync(cancellationToken).ConfigureAwait(false);
        }

        // Start background prefetch if enabled (QueuedMinMessages > 1)
        bool prefetchEnabled = _options.QueuedMinMessages > 1;
        _prefetchEnabled = prefetchEnabled;
        if (prefetchEnabled && !cancellationToken.IsCancellationRequested)
        {
            StartPrefetch();
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            ThrowPendingFetchException();

            await EnsureAssignmentAsync(cancellationToken).ConfigureAwait(false);

            if (_assignmentSnapshot.Count == 0)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                continue;
            }

            // Get pending data - either from prefetch channel or direct fetch
            if (_pendingFetches.Count == 0)
            {
                if (prefetchEnabled)
                {
                    // Try to read from prefetch buffer, draining available items up to a bound
                    if (_prefetchBuffer.TryRead(out PendingFetchData? prefetched))
                    {
                        _pendingFetches.Enqueue(prefetched);
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
                                    _pendingFetches.Enqueue(fetched);
                                    TrackPrefetchedBytes(fetched, release: true);
                                    DrainPrefetchBuffer();
                                }
                                else
                                {
                                    System.Diagnostics.Debug.Fail("WaitToRead signalled data available but TryRead returned false");
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

                ThrowPendingFetchException();
            }

            // Yield batches from pending fetches
            bool metricsEnabled = Diagnostics.DekafMetrics.MessagesReceived.Enabled
                                  || Diagnostics.DekafMetrics.BytesReceived.Enabled;

            while (_pendingFetches.Count > 0)
            {
                PendingFetchData pending = _pendingFetches.Dequeue();
                long? batchProcessingStarted = _adaptiveFetchSizer is not null
                    ? System.Diagnostics.Stopwatch.GetTimestamp() : null;

                try
                {
                    // Eagerly parse all records upfront for cache-friendly access
                    pending.EagerParseAll();

                    // Yield the batch to the caller for synchronous iteration
                    ConsumeBatch<TKey, TValue> batch = new(pending, _keyDeserializer, _valueDeserializer);
                    yield return batch;

                    // After caller finishes iterating: update positions once per batch
                    FlushConsumedPositions(pending);

                    // Record consumer metrics per-fetch
                    if (metricsEnabled && pending.MessageCount > 0)
                    {
                        EmitFetchMetrics(pending);
                    }

                    // Report batch processing time to the adaptive fetch sizer
                    if (batchProcessingStarted.HasValue)
                    {
                        TimeSpan processingDuration = System.Diagnostics.Stopwatch.GetElapsedTime(batchProcessingStarted.Value);
                        _adaptiveFetchSizer!.ReportProcessingComplete(processingDuration);
                    }
                }
                finally
                {
                    pending.Dispose();
                }
            }

            // Drain any pending EOF events (batch API does not surface partition EOF;
            // callers who need EOF notification should use ConsumeAsync instead)
            while (_pendingEofEvents.TryDequeue(out _))
            {
                // Discarded — EOF is informational and not relevant for batch throughput
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

        // Start background prefetch if enabled (QueuedMinMessages > 1)
        bool prefetchEnabled = _options.QueuedMinMessages > 1;
        _prefetchEnabled = prefetchEnabled;
        if (prefetchEnabled && !cancellationToken.IsCancellationRequested)
        {
            StartPrefetch();
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            ThrowPendingFetchException();

            await EnsureAssignmentAsync(cancellationToken).ConfigureAwait(false);

            if (_assignmentSnapshot.Count == 0)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                continue;
            }

            // Get pending data - either from prefetch channel or direct fetch
            if (_pendingFetches.Count == 0)
            {
                if (prefetchEnabled)
                {
                    // Try to read from prefetch buffer, draining available items up to a bound
                    if (_prefetchBuffer.TryRead(out PendingFetchData? prefetched))
                    {
                        _pendingFetches.Enqueue(prefetched);
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
                                    _pendingFetches.Enqueue(fetched);
                                    TrackPrefetchedBytes(fetched, release: true);
                                    DrainPrefetchBuffer();
                                }
                                else
                                {
                                    System.Diagnostics.Debug.Fail("WaitToRead signalled data available but TryRead returned false");
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

                ThrowPendingFetchException();
            }

            // Yield raw batches from pending fetches
            bool metricsEnabled = Diagnostics.DekafMetrics.MessagesReceived.Enabled
                                  || Diagnostics.DekafMetrics.BytesReceived.Enabled;

            while (_pendingFetches.Count > 0)
            {
                PendingFetchData pending = _pendingFetches.Dequeue();
                long? batchProcessingStarted = _adaptiveFetchSizer is not null
                    ? System.Diagnostics.Stopwatch.GetTimestamp() : null;

                try
                {
                    // Eagerly parse all records upfront for cache-friendly access
                    pending.EagerParseAll();

                    // Yield the raw batch to the caller for synchronous iteration
                    ConsumeRawBatch batch = new(pending);
                    yield return batch;

                    // After caller finishes iterating: update positions once per batch
                    FlushConsumedPositions(pending);

                    // Record consumer metrics per-fetch
                    if (metricsEnabled && pending.MessageCount > 0)
                    {
                        EmitFetchMetrics(pending);
                    }

                    // Report batch processing time to the adaptive fetch sizer
                    if (batchProcessingStarted.HasValue)
                    {
                        TimeSpan processingDuration = System.Diagnostics.Stopwatch.GetElapsedTime(batchProcessingStarted.Value);
                        _adaptiveFetchSizer!.ReportProcessingComplete(processingDuration);
                    }
                }
                finally
                {
                    pending.Dispose();
                }
            }

            // Drain any pending EOF events (raw batch API does not surface partition EOF;
            // callers who need EOF notification should use ConsumeAsync instead)
            while (_pendingEofEvents.TryDequeue(out _))
            {
                // Discarded — EOF is informational and not relevant for raw batch throughput
            }
        }
    }

    private void StartPrefetch()
    {
        if (_prefetchTask is not null)
            return;

        _prefetchCts = new CancellationTokenSource();
        _prefetchTask = PrefetchLoopAsync(_prefetchCts.Token);
    }

    private Task PrefetchLoopAsync(CancellationToken cancellationToken)
    {
        // Delegate to the extracted PrefetchPipelineRunner for testability.
        // See PrefetchPipelineRunner.cs for the pipelining invariants and memory limit notes.
        var runner = new PrefetchPipelineRunner(
            ensureAssignment: EnsureAssignmentAsync,
            getAssignmentCount: () => _assignmentSnapshot.Count,
            getMaxBytes: () => CalculatePrefetchMaxBytes(
                (int)Math.Min(CurrentQueuedMaxBytes / 1024, int.MaxValue),
                _assignmentSnapshot.Count,
                _adaptiveFetchSizer?.CurrentPartitionFetchBytes ?? _options.MaxPartitionFetchBytes,
                _adaptiveFetchSizer?.CurrentFetchMaxBytes ?? _options.FetchMaxBytes,
                _options.PrefetchPipelineDepth),
            getPrefetchedBytes: () => Interlocked.Read(ref _prefetchedBytes),
            prefetchRecords: PrefetchRecordsAsync,
            waitForMemoryAvailable: ct => _prefetchMemoryAvailable.WaitAsync(ct),
            logError: LogPrefetchLoopError,
            logMemoryLimitPaused: LogPrefetchMemoryLimitPaused,
            onComplete: error => _prefetchBuffer.Complete(error),
            pipelineDepth: _options.PrefetchPipelineDepth,
            onIterationComplete: _connectionScaler is null ? null : (inFlightCount, pipelineDepth) =>
            {
                _connectionScaler.ReportPipelineUtilization(inFlightCount, pipelineDepth);
                _connectionScaler.MaybeScale();
            });

        return runner.RunAsync(cancellationToken);
    }

    private async ValueTask PrefetchRecordsAsync(CancellationToken cancellationToken)
    {
        // Rent a CTS from the pool for the combined consume cancellation source
        // instead of allocating a LinkedCTS
        var consumeCts = _ctsPool.Rent();
        _activeConsumeCancellationSources.TryAdd(consumeCts, 0);

        try
        {
            // Forward outer cancellation into the pooled CTS via registration
            using var reg1 = cancellationToken.CanBeCanceled
                ? cancellationToken.Register(static s => ((CancellationTokenSource)s!).Cancel(), consumeCts)
                : default;

            // Close any race window: if token was cancelled between method entry and registration
            if (cancellationToken.IsCancellationRequested)
                consumeCts.Cancel();

            var partitionsByBroker = await GroupPartitionsByBrokerAsync(cancellationToken).ConfigureAwait(false);
            var fetchSessionSnapshot = ShouldUseFetchSessions && !_fetchSessions.IsEmpty
                ? _fetchSessions.ToArray()
                : Array.Empty<KeyValuePair<(int BrokerId, int ConnectionIndex), FetchSessionHandler>>();
            var currentConnections = _connectionScaler?.CurrentConnectionCount ?? _options.ConnectionsPerBroker;
            var fetchConnectionCount = ConsumerConnectionScaler.GetFetchConnectionCount(currentConnections);

            // If all partitions are paused, delay to prevent tight spin loop
            // that would starve timeout/cancellation mechanisms of CPU time
            var brokerCount = partitionsByBroker.Count;
            if (brokerCount == 0 && fetchSessionSnapshot.Length == 0)
            {
                await Task.Delay(AllPartitionsPausedDelayMs, cancellationToken).ConfigureAwait(false);
                return;
            }

            // Fetch from all brokers in parallel for maximum throughput.
            // When multiple fetch connections are available, split each broker's partitions
            // across connections to issue concurrent fetches to the same broker.

            // Lazily prune stale entries from scaled-down connections.
            // Enumerate entries directly to avoid the allocating .Keys snapshot.
            foreach (var entry in _prefetchPendingItemsByBroker)
            {
                var key = entry.Key;
                if (key.ConnectionIndex >= fetchConnectionCount)
                    _prefetchPendingItemsByBroker.TryRemove(key, out _);
            }

            // Upper bound: each broker can produce up to fetchConnectionCount tasks
            var maxTasks = (brokerCount * fetchConnectionCount) + fetchSessionSnapshot.Length;
            var fetchTasks = ArrayPool<Task>.Shared.Rent(maxTasks);
            try
            {
                var taskCount = 0;
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

                        fetchTasks[taskCount++] = PrefetchFromBrokerWithErrorHandlingAsync(
                            brokerId, partitions, startIndex, count, connectionIndex,
                            consumeCts.Token, consumeCts.Token);
                    }
                }

                foreach (var (key, handler) in fetchSessionSnapshot)
                {
                    if (!handler.HasActiveSession)
                    {
                        _fetchSessions.TryRemove(key, out _);
                        continue;
                    }

                    if (key.ConnectionIndex >= fetchConnectionCount)
                    {
                        _fetchSessions.TryRemove(key, out _);
                        continue;
                    }

                    if (scheduledFetchSessions is not null && scheduledFetchSessions.Contains(key))
                        continue;

                    fetchTasks[taskCount++] = PrefetchFromBrokerWithErrorHandlingAsync(
                        key.BrokerId, [], 0, 0, key.ConnectionIndex,
                        consumeCts.Token, consumeCts.Token);
                }

                if (taskCount == 0)
                {
                    await Task.Delay(AllPartitionsPausedDelayMs, cancellationToken).ConfigureAwait(false);
                    return;
                }

                // Timing wraps the entire parallel fetch cycle (all brokers via Task.WhenAll)
                // rather than individual broker fetches. This avoids a data race on the timing
                // fields from concurrent PrefetchFromBrokerAsync calls, and correctly measures
                // the bottleneck (slowest broker) which is the right signal for adaptive sizing.
                _adaptiveFetchSizer?.RecordFetchStart();

                // ReadOnlySpan overload (.NET 9+) avoids internal array copy and ArraySegment boxing
                await Task.WhenAll(new ReadOnlySpan<Task>(fetchTasks, 0, taskCount)).ConfigureAwait(false);

                _adaptiveFetchSizer?.RecordFetchEnd();
            }
            finally
            {
                ArrayPool<Task>.Shared.Return(fetchTasks, clearArray: true);
            }
        }
        finally
        {
            _activeConsumeCancellationSources.TryRemove(consumeCts, out _);
            consumeCts.Dispose();
        }
    }

    private async Task PrefetchFromBrokerWithErrorHandlingAsync(
        int brokerId,
        List<TopicPartition> partitions,
        int partitionStartIndex,
        int partitionCount,
        int connectionIndex,
        CancellationToken linkedToken,
        CancellationToken consumeCancellationToken)
    {
        try
        {
            await PrefetchFromBrokerAsync(brokerId, partitions, partitionStartIndex, partitionCount, connectionIndex, linkedToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (consumeCancellationToken.IsCancellationRequested)
        {
            // Consume cancellation requested, exit silently
        }
        catch (Exception ex) when (IsFatalPrefetchError(ex))
        {
            // Non-recoverable errors that should propagate to PrefetchPipelineRunner's
            // consecutive error counter. See IsFatalPrefetchError for classification logic.
            LogFatalPrefetchError(ex, brokerId);
            throw;
        }
        catch (Exception ex)
        {
            // Transient errors (connection timeouts, broker unavailable, etc.) — log and
            // suppress. The pipeline retries on the next cycle, and in multi-broker setups
            // other brokers may still succeed in this cycle.
            ClearPreferredReadReplicasForBroker(brokerId, partitions, partitionStartIndex, partitionCount);
            LogPrefetchFromBrokerError(ex, brokerId);
        }
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
    /// Calculates the effective prefetch memory budget, auto-scaling above the configured
    /// maximum when the partition count and pipeline depth require more headroom.
    /// </summary>
    internal static long CalculatePrefetchMaxBytes(
        int queuedMaxMessagesKbytes,
        int partitionCount,
        int maxPartitionFetchBytes,
        int fetchMaxBytes,
        int pipelineDepth)
    {
        var configuredMax = (long)queuedMaxMessagesKbytes * 1024;
        // Auto-scale: ensure the budget fits (pipelineDepth + 1) full fetch responses.
        // The +1 provides headroom so the consume loop can drain a completed response
        // while the pipeline keeps its full depth of in-flight fetches.
        // A single fetch response is capped at FetchMaxBytes by the broker, even when
        // partitionCount * MaxPartitionFetchBytes would exceed it.
        var fetchResponseSize = Math.Min(
            (long)partitionCount * maxPartitionFetchBytes,
            fetchMaxBytes);
        var minRequired = fetchResponseSize * (pipelineDepth + 1);
        return Math.Max(configuredMax, minRequired);
    }

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

    private async ValueTask PrefetchFromBrokerAsync(
        int brokerId,
        List<TopicPartition> partitions,
        int partitionStartIndex,
        int partitionCount,
        int connectionIndex,
        CancellationToken cancellationToken)
    {
        var connection = await _connectionPool.GetConnectionByIndexAsync(brokerId, connectionIndex, cancellationToken).ConfigureAwait(false);

        // Ensure API version is negotiated (thread-safe initialization)
        var apiVersion = _fetchApiVersion;
        if (apiVersion < 0)
        {
            apiVersion = _metadataManager.GetNegotiatedApiVersion(
                ApiKey.Fetch,
                FetchRequest.LowestSupportedVersion,
                FetchRequest.HighestSupportedVersion);
            Interlocked.CompareExchange(ref _fetchApiVersion, apiVersion, -1);
            apiVersion = _fetchApiVersion;
        }

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
            // Build fetch request — pass index range to avoid GetRange allocation
            var topicData = BuildFetchRequestTopics(partitions, partitionStartIndex, partitionCount);
            FetchSessionBuildResult? fetchSessionBuild = fetchSessionHandler?.Build(topicData, _metadataManager.Metadata);

            var request = FetchRequest.Rent();
            request.MaxWaitMs = _options.FetchMaxWaitMs;
            request.MinBytes = _options.FetchMinBytes;
            request.MaxBytes = _adaptiveFetchSizer?.CurrentFetchMaxBytes ?? _options.FetchMaxBytes;
            request.IsolationLevel = _options.IsolationLevel;
            request.RackId = _options.ClientRack;
            request.Topics = fetchSessionBuild?.Topics ?? topicData;
            request.ForgottenTopicsData = fetchSessionBuild?.ForgottenTopicsData;
            request.SessionId = fetchSessionBuild?.SessionId ?? 0;
            request.SessionEpoch = fetchSessionBuild?.SessionEpoch ?? -1;

            var fetchStarted = System.Diagnostics.Stopwatch.GetTimestamp();

            FetchResponse response;
            try
            {
                response = await connection.SendAsync<FetchRequest, FetchResponse>(
                    request,
                    (short)apiVersion,
                    cancellationToken).ConfigureAwait(false);
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

            // Write to prefetch channel
            try
            {
                foreach (var topicResponse in response.Responses)
                {
                    var topic = ResolveTopicName(topicResponse);
                    var activityName = _activityNameCache.GetOrAdd(topic, static t => $"{t} receive");

                    foreach (var partitionResponse in topicResponse.Partitions)
                    {
                        var tp = new TopicPartition(topic, partitionResponse.PartitionIndex);

                        // Update watermark cache from fetch response (even on errors, watermarks may be valid)
                        UpdateWatermarksFromFetchResponse(topic, partitionResponse);
                        UpdatePreferredReadReplica(topic, partitionResponse);

                        if (partitionResponse.DivergingEpoch is not null)
                        {
                            ResetToDivergingEpoch(topic, partitionResponse);
                            continue;
                        }

                        if (partitionResponse.ErrorCode != ErrorCode.None)
                        {
                            if (partitionResponse.ErrorCode == ErrorCode.OffsetOutOfRange)
                            {
                                await ResetOffsetOutOfRangeAsync(tp, cancellationToken).ConfigureAwait(false);
                            }
                            else if (IsLeaderEpochRefreshError(partitionResponse.ErrorCode))
                            {
                                await HandleLeaderEpochRefreshAsync(
                                    topic,
                                    partitionResponse,
                                    response.NodeEndpoints).ConfigureAwait(false);
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
                            // We have new records - reset EOF state for this partition
                            _eofEmitted.TryRemove(tp, out _);

                            var pending = PendingFetchData.Create(
                                topic,
                                partitionResponse.PartitionIndex,
                                records,
                                partitionResponse.AbortedTransactions,
                                activityName: activityName);

                            // Track memory before adding to channel
                            TrackPrefetchedBytes(pending, release: false);

                            // Update fetch positions for next prefetch
                            UpdateFetchPositionsFromPrefetch(pending);

                            // Collect for later - we'll assign memory owner to the last one
                            pendingItems.Add(pending);
                        }
                        else if (_options.EnablePartitionEof)
                        {
                            // No records returned - check if we're at EOF
                            var fetchPosition = _fetchPositions.GetValueOrDefault(tp, 0);

                            // EOF condition: position >= high watermark and we haven't emitted EOF yet
                            if (fetchPosition >= partitionResponse.HighWatermark && _eofEmitted.TryAdd(tp, 0))
                            {
                                // Queue EOF event and mark as emitted
                                _pendingEofEvents.Enqueue((tp, fetchPosition));
                            }
                        }
                    }
                }
            }
            finally
            {
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

                for (var i = 0; i < pendingItems.Count; i++)
                {
                    while (!_prefetchBuffer.TryWrite(pendingItems[i]))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await _prefetchBuffer.WaitToWriteAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            // If no pending items were created but we have a memory owner, dispose it
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
    private void FlushConsumedPositions(PendingFetchData pending)
    {
        if (!TryGetConsumedPosition(pending, out var tp, out var nextOffset, out var leaderEpoch))
            return;

        SetPosition(tp, nextOffset, dirty: true);
        SetLastConsumedLeaderEpoch(tp, leaderEpoch);

        if (!_prefetchEnabled)
        {
            _fetchPositions[tp] = nextOffset;
        }
    }

    private void FlushActiveConsumedPosition()
    {
        if (_pendingFetches.Count == 0)
            return;

        var pending = _pendingFetches.Peek();
        if (TryGetConsumedPosition(pending, out var tp, out var nextOffset, out var leaderEpoch))
        {
            SetPosition(tp, nextOffset, dirty: true);
            SetLastConsumedLeaderEpoch(tp, leaderEpoch);
        }
    }

    private bool TryGetActiveConsumedPosition(TopicPartition partition, out long position, out int leaderEpoch)
    {
        if (_pendingFetches.Count == 0)
        {
            position = 0;
            leaderEpoch = -1;
            return false;
        }

        var pending = _pendingFetches.Peek();
        if (!TryGetConsumedPosition(pending, out var tp, out position, out leaderEpoch) || !tp.Equals(partition))
        {
            position = 0;
            leaderEpoch = -1;
            return false;
        }

        return true;
    }

    private static bool TryGetConsumedPosition(
        PendingFetchData pending,
        out TopicPartition partition,
        out long nextOffset,
        out int leaderEpoch)
    {
        if (pending.LastYieldedOffset < 0)
        {
            partition = default;
            nextOffset = 0;
            leaderEpoch = -1;
            return false;
        }

        partition = pending.TopicPartition;
        nextOffset = pending.LastYieldedOffset + 1;
        leaderEpoch = pending.LastYieldedLeaderEpoch;
        return true;
    }

    private void SetPosition(TopicPartition partition, long position, bool dirty)
    {
        _positions[partition] = position;
        if (dirty)
            _dirtyPositions[partition] = position;
        else
            _dirtyPositions.TryRemove(partition, out _);
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

    private void ClearDirtyPositionIfCommitted(TopicPartition partition, long committedOffset)
    {
        _dirtyPositions.TryRemove(new KeyValuePair<TopicPartition, long>(partition, committedOffset));
    }

    private void MarkOffsetCommitted(TopicPartition partition, long committedOffset)
    {
        _committed[partition] = committedOffset;
        ClearDirtyPositionIfCommitted(partition, committedOffset);
    }

    private void UpdateFetchPositionsFromPrefetch(PendingFetchData pending)
    {
        // Calculate the last offset in the prefetched data
        // We need to update fetch positions so next prefetch gets new data
        var batches = pending.GetBatches();
        if (batches.Count == 0) return;

        var lastBatch = batches[^1];
        var lastOffset = lastBatch.BaseOffset + lastBatch.LastOffsetDelta;
        var tp = pending.TopicPartition;

        // Thread-safe update using ConcurrentDictionary — must use AddOrUpdate (not TryGetValue
        // + indexer) because seek/reset operations on other threads can write to _fetchPositions
        // concurrently, and AddOrUpdate's CAS loop prevents TOCTOU races from overwriting a
        // concurrent seek-forward with a stale prefetch offset.
        // Uses the factoryArgument overload with static lambdas to avoid closure allocation.
        var nextOffset = lastOffset + 1;
        _fetchPositions.AddOrUpdate(
            tp,
            static (_, nextOffset) => nextOffset,
            static (_, currentPos, nextOffset) => Math.Max(currentPos, nextOffset),
            nextOffset);
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
            _pendingFetches.Enqueue(additional);
            TrackPrefetchedBytes(additional, release: true);
        }
    }

    private async ValueTask<bool> WaitForPrefetchDataAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_prefetchBuffer.HasDataAvailable())
            return true;

        return await _prefetchBuffer.WaitToReadAsync(_options.FetchMaxWaitMs, cancellationToken)
            .ConfigureAwait(false);
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
        foreach (var batch in batches)
        {
            bytes += batch.BatchLength + PerBatchHeaderOverhead;
        }
        return bytes;
    }

    public async ValueTask<ConsumeResult<TKey, TValue>?> ConsumeOneAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = _ctsPool.Rent();
        timeoutCts.CancelAfter(timeout);

        // Fast path: if no external cancellation, use timeout CTS directly (avoids allocation)
        if (!cancellationToken.CanBeCanceled)
        {
            try
            {
                return await ConsumeOneCoreAsync(timeoutCts.Token).ConfigureAwait(false);
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
            return await ConsumeOneCoreAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            // Timeout expired — no message arrived. If the external token also fired by the time
            // this filter evaluates (race under load), the internal timeout still takes priority.
        }

        return null;
    }

    private async ValueTask<ConsumeResult<TKey, TValue>?> ConsumeOneCoreAsync(CancellationToken cancellationToken)
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
            ThrowPendingFetchException();

            await EnsureAssignmentAsync(cancellationToken).ConfigureAwait(false);

            if (_assignmentSnapshot.Count == 0)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (_pendingFetches.Count == 0)
            {
                if (!await FillPendingFetchesForSingleConsumeAsync(prefetchEnabled, cancellationToken)
                    .ConfigureAwait(false))
                {
                    return null;
                }

                ThrowPendingFetchException();
            }

            if (TryConsumeOneFromPendingFetches(out var result))
                return result;

            if (_pendingEofEvents.TryDequeue(out var eofEvent))
            {
                return ConsumeResult<TKey, TValue>.CreatePartitionEof(
                    eofEvent.Partition.Topic,
                    eofEvent.Partition.Partition,
                    eofEvent.Offset);
            }
        }

        return null;
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
            _pendingFetches.Enqueue(prefetched);
            TrackPrefetchedBytes(prefetched, release: true);
            DrainPrefetchBuffer();
            return true;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (_prefetchBuffer.WaitToRead(_options.FetchMaxWaitMs, cancellationToken))
            {
                if (_prefetchBuffer.TryRead(out var fetched))
                {
                    _pendingFetches.Enqueue(fetched);
                    TrackPrefetchedBytes(fetched, release: true);
                    DrainPrefetchBuffer();
                }
                else
                {
                    System.Diagnostics.Debug.Fail("WaitToRead signalled data available but TryRead returned false");
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

    private bool TryConsumeOneFromPendingFetches(out ConsumeResult<TKey, TValue> result)
    {
        result = default!;

        var metricsEnabled = Diagnostics.DekafMetrics.MessagesReceived.Enabled
                             || Diagnostics.DekafMetrics.BytesReceived.Enabled;
        var hasTraceListeners = Diagnostics.DekafDiagnostics.Source.HasListeners();
        var hasInterceptors = _interceptors is not null;
        var rawTrackingEnabled = _rawRecordTrackingEnabled;

        while (_pendingFetches.Count > 0)
        {
            var pending = _pendingFetches.Peek();
            long? batchProcessingStarted = _adaptiveFetchSizer is not null
                ? System.Diagnostics.Stopwatch.GetTimestamp() : null;

            pending.EagerParseAll();

            System.Diagnostics.Activity? activity = null;
            try
            {
                while (true)
                {
                    try
                    {
                        if (!pending.MoveNext())
                            break;

                        var record = pending.CurrentRecord;
                        var offset = pending.CurrentBaseOffset + record.OffsetDelta;
                        var timestampMs = pending.CurrentBaseTimestamp + record.TimestampDelta;
                        var timestampType = pending.CurrentTimestampType;
                        var messageBytes = (record.IsKeyNull ? 0 : record.Key.Length) +
                                           (record.IsValueNull ? 0 : record.Value.Length);

                        if (hasTraceListeners)
                        {
                            var headers = LazyConsumeHeaders.Create(
                                record.Headers,
                                record.HeaderCount,
                                pending,
                                pending.HeaderGeneration);
                            activity = StartConsumeActivity(pending, headers, offset, messageBytes);
                        }

                        result = new ConsumeResult<TKey, TValue>(
                            topic: pending.Topic,
                            partition: pending.PartitionIndex,
                            offset: offset,
                            keyData: record.Key,
                            isKeyNull: record.IsKeyNull,
                            valueData: record.Value,
                            isValueNull: record.IsValueNull,
                            pooledHeaders: record.Headers,
                            pooledHeaderCount: record.HeaderCount,
                            headerOwner: pending,
                            timestampMs: timestampMs,
                            timestampType: timestampType,
                            leaderEpoch: pending.CurrentPartitionLeaderEpoch >= 0 ? pending.CurrentPartitionLeaderEpoch : null,
                            keyDeserializer: _keyDeserializer,
                            valueDeserializer: _valueDeserializer);

                        pending.TrackConsumed(offset, messageBytes);

                        if (hasInterceptors)
                            result = ApplyOnConsumeInterceptors(result);

                        if (rawTrackingEnabled)
                        {
                            _currentRawKey = record.IsKeyNull ? ReadOnlyMemory<byte>.Empty : record.Key;
                            _currentRawValue = record.IsValueNull ? ReadOnlyMemory<byte>.Empty : record.Value;
                        }

                        return true;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex) when (ex is InsufficientDataException or ArgumentOutOfRangeException or MalformedProtocolDataException)
                    {
                        LogRecordParsingError(ex, pending.Topic, pending.PartitionIndex);
                        break;
                    }
                }
            }
            finally
            {
                activity?.Dispose();
                FlushConsumedPositions(pending);

                if (metricsEnabled && pending.MessageCount > 0)
                    EmitFetchMetrics(pending);
            }

            if (batchProcessingStarted.HasValue)
            {
                var processingDuration = System.Diagnostics.Stopwatch.GetElapsedTime(batchProcessingStarted.Value);
                _adaptiveFetchSizer!.ReportProcessingComplete(processingDuration);
            }

            _pendingFetches.Dequeue().Dispose();
        }

        return false;
    }

    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_options.OffsetCommitMode == OffsetCommitMode.Manual)
            FlushActiveConsumedPosition();

        if (_coordinator is null)
            return;

        TopicPartitionOffset[]? offsetsArray = null;
        int offsetCount;

        {
            // Commit only positions that changed since the last successful commit.
            // Snapshot the concurrent dictionary to avoid race conditions during enumeration
            var dirtyPositionsSnapshot = _dirtyPositions.ToArray();
            offsetCount = dirtyPositionsSnapshot.Length;
            if (offsetCount == 0)
                return;

            LogCommitStarted(offsetCount);

            // Rent array from pool to avoid List allocation
            offsetsArray = ArrayPool<TopicPartitionOffset>.Shared.Rent(offsetCount);
            try
            {
                int index = 0;
                foreach (var kvp in dirtyPositionsSnapshot)
                {
                    offsetsArray[index++] = new TopicPartitionOffset(
                        kvp.Key.Topic,
                        kvp.Key.Partition,
                        kvp.Value,
                        GetLastConsumedLeaderEpoch(kvp.Key));
                }

                // Create array segment to pass only the used portion
                var offsets = new ArraySegment<TopicPartitionOffset>(offsetsArray, 0, offsetCount);

                await _coordinator.CommitOffsetsAsync(offsets, cancellationToken).ConfigureAwait(false);

                // Update committed offsets tracking
                foreach (var offset in offsets)
                {
                    var partition = new TopicPartition(offset.Topic, offset.Partition);
                    MarkOffsetCommitted(partition, offset.Offset);
                }

                // Invoke OnCommit interceptors - wrap array as ArraySegment to avoid Span→Array copy
                InvokeOnCommitInterceptors(new ArraySegment<TopicPartitionOffset>(offsetsArray, 0, offsetCount));
            }
            finally
            {
                ArrayPool<TopicPartitionOffset>.Shared.Return(offsetsArray);
            }
        }
    }

    public async ValueTask CommitAsync(IEnumerable<TopicPartitionOffset> offsets, CancellationToken cancellationToken = default)
    {
        if (_coordinator is null)
            return;

        // Materialize to list to allow iteration for both commit tracking and interceptors
        var offsetsList = offsets as IReadOnlyList<TopicPartitionOffset> ?? offsets.ToArray();

        await _coordinator.CommitOffsetsAsync(offsetsList, cancellationToken).ConfigureAwait(false);

        foreach (var offset in offsetsList)
        {
            var partition = new TopicPartition(offset.Topic, offset.Partition);
            MarkOffsetCommitted(partition, offset.Offset);
        }

        // Invoke OnCommit interceptors
        InvokeOnCommitInterceptors(offsetsList);
    }

    public async ValueTask<long?> GetCommittedOffsetAsync(TopicPartition partition, CancellationToken cancellationToken = default)
    {
        if (_committed.TryGetValue(partition, out var offset))
            return offset;

        if (_coordinator is not null)
        {
            var offsets = await _coordinator.FetchOffsetsAsync([partition], cancellationToken).ConfigureAwait(false);
            if (offsets.TryGetValue(partition, out var committedOffset))
            {
                offset = committedOffset.Offset;
                _committed[partition] = offset;
                if (committedOffset.LeaderEpoch >= 0)
                    SetLastConsumedLeaderEpoch(partition, committedOffset.LeaderEpoch);
                else
                    ClearLastConsumedLeaderEpoch(partition);
                return offset;
            }
        }

        return null;
    }

    public long? GetPosition(TopicPartition partition)
    {
        if (_options.OffsetCommitMode == OffsetCommitMode.Manual
            && TryGetActiveConsumedPosition(partition, out var activePosition, out var leaderEpoch))
        {
            SetPosition(partition, activePosition, dirty: true);
            SetLastConsumedLeaderEpoch(partition, leaderEpoch);
            return activePosition;
        }

        return _positions.TryGetValue(partition, out var position) ? position : null;
    }

    private void EmitFetchMetrics(PendingFetchData fetch)
    {
        if (!fetch.TryConsumeMetricDelta(out var messageCount, out var bytesConsumed))
            return;

        if (!_metricTagsCache.TryGetValue(fetch.Topic, out var metricTags))
        {
            metricTags = new System.Diagnostics.TagList
                { { Diagnostics.DekafDiagnostics.MessagingDestinationName, fetch.Topic } };
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
            static id => new TagList { { Diagnostics.DekafDiagnostics.MessagingKafkaBrokerId, id } });

    public void Seek(TopicPartitionOffset offset)
    {
        LogSeek(offset.Topic, offset.Partition, offset.Offset);
        var tp = new TopicPartition(offset.Topic, offset.Partition);
        // Update positions (thread-safe with ConcurrentDictionary)
        SetPosition(tp, offset.Offset, dirty: true);
        if (offset.LeaderEpoch >= 0)
            SetLastConsumedLeaderEpoch(tp, offset.LeaderEpoch);
        else
            ClearLastConsumedLeaderEpoch(tp);
        _fetchPositions[tp] = offset.Offset;

        // Reset EOF state for this partition so it can fire again
        _eofEmitted.TryRemove(tp, out _);
        ClearFetchBuffer();
    }

    public void SeekToBeginning(params TopicPartition[] partitions)
    {
        // Update positions (thread-safe with ConcurrentDictionary)
        foreach (var partition in partitions)
        {
            SetPosition(partition, 0, dirty: true);
            ClearLastConsumedLeaderEpoch(partition);
            _fetchPositions[partition] = 0;
        }

        // Reset EOF state
        foreach (var partition in partitions)
        {
            _eofEmitted.TryRemove(partition, out _);
        }
        ClearFetchBuffer();
    }

    public void SeekToEnd(params TopicPartition[] partitions)
    {
        // Update positions (thread-safe with ConcurrentDictionary)
        foreach (var partition in partitions)
        {
            SetPosition(partition, -1, dirty: true); // Special value meaning end
            ClearLastConsumedLeaderEpoch(partition);
            _fetchPositions[partition] = -1; // Special value meaning end
        }

        // Reset EOF state
        foreach (var partition in partitions)
        {
            _eofEmitted.TryRemove(partition, out _);
        }
        ClearFetchBuffer();
    }

    /// <summary>
    /// Removes per-partition tracking state for the given partitions.
    /// Returns true if any partition was in the paused set.
    /// </summary>
    private bool RemovePartitionState(IEnumerable<TopicPartition> partitions)
    {
        var hadPaused = false;
        foreach (var partition in partitions)
        {
            _positions.TryRemove(partition, out _);
            _dirtyPositions.TryRemove(partition, out _);
            _fetchPositions.TryRemove(partition, out _);
            _lastConsumedLeaderEpochs.TryRemove(partition, out _);
            _committed.TryRemove(partition, out _);
            _highWatermarks.TryRemove(partition, out _);
            _watermarks.TryRemove(partition, out _);
            _eofEmitted.TryRemove(partition, out _);
            hadPaused |= _paused.TryRemove(partition, out _);
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
        pending.Dispose();
    }

    private void ClearFetchBuffer()
    {
        // Dispose and clear pending fetches to release pooled memory
        while (_pendingFetches.TryDequeue(out var pending))
        {
            DisposeQueuedFetch(pending);
        }
        // Also drain prefetched items that haven't been moved to _pendingFetches yet.
        // Without this, stale data from old partitions would surface after reassignment.
        while (_prefetchBuffer.TryRead(out var prefetched))
        {
            TrackPrefetchedBytes(prefetched, release: true);
            prefetched.Dispose();
        }
        // Clear pending EOF events as they are stale after buffer clear
        _pendingEofEvents.Clear();
    }

    private void ClearFetchBufferForPartitions(IEnumerable<TopicPartition> partitionsToRemove)
    {
        // Create a set for efficient lookup
        var removeSet = partitionsToRemove is HashSet<TopicPartition> set
            ? set
            : new HashSet<TopicPartition>(partitionsToRemove);

        if (removeSet.Count == 0)
            return;

        // Filter in-place without allocating a temporary queue
        // Dequeue all items and re-enqueue only those we want to keep
        var count = _pendingFetches.Count;

        for (var i = 0; i < count; i++)
        {
            var pending = _pendingFetches.Dequeue();

            // Check if this partition should be kept
            // Build TopicPartition inline for the Contains check
            if (!removeSet.Contains(pending.TopicPartition))
            {
                // Keep this item by re-enqueueing it
                _pendingFetches.Enqueue(pending);
            }
            else
            {
                // Dispose removed items to release pooled memory
                DisposeQueuedFetch(pending);
            }
        }

        // Also drain prefetch buffer items for revoked partitions.
        // Without this, stale data from revoked partitions sitting in the prefetch
        // buffer would be consumed after an incremental unassign (cooperative rebalance),
        // causing data for partitions no longer owned by this consumer to be yielded.
        DrainPrefetchBufferForPartitions(removeSet);
    }

    private void DrainPrefetchBufferForPartitions(HashSet<TopicPartition> partitionsToRemove)
    {
        DrainBufferForPartitions(
            _prefetchBuffer,
            partitionsToRemove,
            _pendingFetches,
            pending => TrackPrefetchedBytes(pending, release: true));
    }

    /// <summary>
    /// Drains a prefetch buffer, disposing items whose partition is in <paramref name="partitionsToRemove"/>
    /// and enqueuing retained items into <paramref name="retainedQueue"/>.
    /// </summary>
    /// <remarks>
    /// O(n) over prefetch buffer items — acceptable because rebalance is infrequent (not hot path).
    /// Items for retained partitions move to <paramref name="retainedQueue"/>; revoked items are disposed.
    /// Retained items are appended after existing queue items. This is acceptable
    /// because Kafka consumption is sequential per partition and cross-partition ordering is
    /// not guaranteed — so the relative order between partitions does not matter.
    /// </remarks>
    internal static void DrainBufferForPartitions(
        MpscFetchBuffer buffer,
        HashSet<TopicPartition> partitionsToRemove,
        Queue<PendingFetchData> retainedQueue,
        Action<PendingFetchData> onItemRemoved)
    {
        while (buffer.TryRead(out var prefetched))
        {
            if (partitionsToRemove.Contains(prefetched.TopicPartition))
            {
                onItemRemoved(prefetched);
                prefetched.Dispose();
            }
            else
            {
                // Retained items are appended after existing queue items. This is safe because
                // fetch positions are tracked independently (_fetchPositions), not inferred from
                // queue order — so interleaving with existing _pendingFetches items is harmless.
                retainedQueue.Enqueue(prefetched);
                onItemRemoved(prefetched);
            }
        }
    }

    public void Pause(params TopicPartition[] partitions)
    {
        foreach (var partition in partitions)
        {
            _paused.TryAdd(partition, 0);
        }
        PublishPausedSnapshot();
        InvalidatePartitionCache();
        InvalidateFetchRequestCache();
    }

    public void Resume(params TopicPartition[] partitions)
    {
        foreach (var partition in partitions)
        {
            _paused.TryRemove(partition, out _);
        }
        PublishPausedSnapshot();
        InvalidatePartitionCache();
        InvalidateFetchRequestCache();
    }

    private void CancelActiveConsumeOperations()
    {
        foreach (var cts in _activeConsumeCancellationSources.Keys)
        {
            try { cts.Cancel(); }
            catch (ObjectDisposedException) { }
        }
    }

    public WatermarkOffsets? GetWatermarkOffsets(TopicPartition topicPartition)
    {
        if (_watermarks.TryGetValue(topicPartition, out var watermarks))
            return watermarks;
        return null;
    }

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

    public async ValueTask<WatermarkOffsets> QueryWatermarkOffsetsAsync(
        TopicPartition topicPartition,
        CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _consumerDisposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaConsumer<TKey, TValue>));

        ThrowIfNotInitialized();

        return await RetryHelper.WithRetryAsync(async () =>
        {
            var connection = await GetPartitionLeaderConnectionAsync(topicPartition, cancellationToken).ConfigureAwait(false);
            if (connection is null)
                throw new KafkaException(ErrorCode.LeaderNotAvailable, $"No leader found for partition {topicPartition}");

            var listOffsetsVersion = _metadataManager.GetNegotiatedApiVersion(
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

            var earliestResponseTask = connection.SendAsync<ListOffsetsRequest, ListOffsetsResponse>(
                earliestRequest,
                listOffsetsVersion,
                cancellationToken).AsTask();

            var latestResponseTask = connection.SendAsync<ListOffsetsRequest, ListOffsetsResponse>(
                latestRequest,
                listOffsetsVersion,
                cancellationToken).AsTask();

            await Task.WhenAll(earliestResponseTask, latestResponseTask).ConfigureAwait(false);

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

            // Cache the result
            _watermarks[topicPartition] = watermarks;

            return watermarks;
        }, _metadataManager, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _consumerDisposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaConsumer<TKey, TValue>));

        // Fast path: already initialized (volatile read provides acquire semantics)
        if (_initialized)
            return;

        await SemaphoreHelper.AcquireOrThrowDisposedAsync(_initLock, nameof(KafkaConsumer<TKey, TValue>), cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring lock
            if (_initialized)
                return;

            await _metadataManager.InitializeAsync(cancellationToken).ConfigureAwait(false);
            await _telemetryManager.StartAsync(cancellationToken).ConfigureAwait(false);
            _initialized = true;
        }
        finally
        {
            SemaphoreHelper.ReleaseSafely(_initLock);
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
        const long refreshIntervalTicks = 30 * TimeSpan.TicksPerSecond;

        var now = Environment.TickCount64;
        var lastRefresh = Volatile.Read(ref _lastFilterRefreshTicks);

        // Rate-limit: skip if we refreshed recently (unless this is the first call)
        if (lastRefresh != 0 && (now - lastRefresh) < (refreshIntervalTicks / TimeSpan.TicksPerMillisecond))
        {
            return false;
        }

        Volatile.Write(ref _lastFilterRefreshTicks, now);

        // Refresh metadata to get all topics (null = all topics)
        await _metadataManager.RefreshMetadataAsync(cancellationToken).ConfigureAwait(false);

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

        var subscriptionSnapshot = _subscriptionSnapshot;
        var coordinator = _coordinator;
        if (subscriptionSnapshot.Count != 0 && coordinator is not null)
        {
            await coordinator.EnsureActiveGroupAsync(subscriptionSnapshot, cancellationToken).ConfigureAwait(false);

            var coordinatorAssignmentVersion = coordinator.AssignmentVersion;
            if (Volatile.Read(ref _lastCoordinatorAssignmentVersion) == coordinatorAssignmentVersion)
                return;
        }
        else if (IsManualAssignmentEnsureCurrent())
        {
            return;
        }

        // Serialize the write path: both ConsumeAsync and PrefetchLoopAsync call this method
        // concurrently. Without synchronization, concurrent access to non-thread-safe
        // _assignment HashSet causes NullReferenceException during enumeration.
        // Readers use the volatile _assignmentSnapshot instead of acquiring this lock.
        await SemaphoreHelper.AcquireOrThrowDisposedAsync(_assignmentLock, nameof(KafkaConsumer<TKey, TValue>), cancellationToken).ConfigureAwait(false);
        try
        {
            coordinator = _coordinator;
            if (!_subscription.IsEmpty && coordinator is not null)
            {
                var coordinatorAssignmentVersion = coordinator.AssignmentVersion;
                var coordinatorAssignment = coordinator.Assignment;

                // Fast path: skip all work if assignment hasn't changed (common case after stable join)
                if (_assignment.SetEquals(coordinatorAssignment))
                {
                    Volatile.Write(ref _lastCoordinatorAssignmentVersion, coordinatorAssignmentVersion);
                    return;
                }

                // Check for new partitions that need initialization
                List<TopicPartition>? newPartitions = null;
                foreach (var partition in coordinatorAssignment)
                {
                    if (!_assignment.Contains(partition))
                    {
                        newPartitions ??= new List<TopicPartition>();
                        newPartitions.Add(partition);
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

                if (newPartitions is { Count: > 0 })
                    LogPartitionsAdded(newPartitions.Count);
                if (removedPartitions is { Count: > 0 })
                    LogPartitionsRemoved(removedPartitions.Count);

                // Update assignment from coordinator
                _assignment.Clear();
                foreach (var partition in coordinatorAssignment)
                {
                    _assignment.Add(partition);
                }
                PublishAssignmentSnapshot();
                InvalidatePartitionCache();
                InvalidateFetchRequestCache();

                // Ratchet pool sizes based on actual partition count
                RatchetConsumerPoolSizes(_assignment.Count);

                // Clean up state for removed partitions
                if (removedPartitions is not null)
                {
                    if (RemovePartitionState(removedPartitions))
                        PublishPausedSnapshot();
                }

                // Initialize positions for new partitions
                if (newPartitions is { Count: > 0 })
                {
                    await InitializePositionsAsync(newPartitions, cancellationToken).ConfigureAwait(false);
                }

                Volatile.Write(ref _lastCoordinatorAssignmentVersion, coordinatorAssignmentVersion);
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
            SemaphoreHelper.ReleaseSafely(_assignmentLock);
        }
    }

    private bool IsManualAssignmentEnsureCurrent()
    {
        if (_topicFilter is not null || !_subscription.IsEmpty)
            return false;

        var assignmentEnsureVersion = Volatile.Read(ref _assignmentEnsureVersion);
        return Volatile.Read(ref _lastManualAssignmentEnsureVersion) == assignmentEnsureVersion;
    }

    private async ValueTask InitializeManualAssignmentPositionsAsync(List<TopicPartition> partitions, CancellationToken cancellationToken)
    {
        // For manual assignment without a group, use auto offset reset to determine starting position
        foreach (var partition in partitions)
        {
            var offset = await GetResetOffsetAsync(partition, cancellationToken).ConfigureAwait(false);
            SetPosition(partition, offset, dirty: false);
            ClearLastConsumedLeaderEpoch(partition);
            _fetchPositions[partition] = offset;
        }
    }

    private async ValueTask InitializePositionsAsync(List<TopicPartition> partitions, CancellationToken cancellationToken)
    {
        // Fetch committed offsets for all partitions
        var committedOffsets = await _coordinator!.FetchOffsetsAsync(partitions, cancellationToken).ConfigureAwait(false);

        foreach (var partition in partitions)
        {
            if (committedOffsets.TryGetValue(partition, out var committedOffset) && committedOffset.Offset >= 0)
            {
                // Use committed offset
                SetPosition(partition, committedOffset.Offset, dirty: false);
                if (committedOffset.LeaderEpoch >= 0)
                    SetLastConsumedLeaderEpoch(partition, committedOffset.LeaderEpoch);
                else
                    ClearLastConsumedLeaderEpoch(partition);
                _fetchPositions[partition] = committedOffset.Offset;
                _committed[partition] = committedOffset.Offset;
            }
            else
            {
                // No committed offset, use auto offset reset
                var offset = await GetResetOffsetAsync(partition, cancellationToken).ConfigureAwait(false);
                SetPosition(partition, offset, dirty: false);
                ClearLastConsumedLeaderEpoch(partition);
                _fetchPositions[partition] = offset;
            }
        }
    }

    private async ValueTask<long> GetResetOffsetAsync(TopicPartition partition, CancellationToken cancellationToken)
    {
        var timestamp = AutoOffsetResetStrategy.GetListOffsetsTimestamp(_options, DateTimeOffset.UtcNow, partition);
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
            var fetchPosition = _fetchPositions.GetValueOrDefault(partition, 0);
            if (fetchPosition == -1 || fetchPosition == -2)
            {
                // -1 = latest, -2 = earliest
                var resolvedOffset = await ResolveOffsetAsync(partition, fetchPosition, cancellationToken).ConfigureAwait(false);
                _fetchPositions[partition] = resolvedOffset;
                SetPosition(partition, resolvedOffset, dirty: false);
                ClearLastConsumedLeaderEpoch(partition);
            }
        }
    }

    private ValueTask<long> ResolveOffsetAsync(TopicPartition partition, long timestamp, CancellationToken cancellationToken)
    {
        return RetryHelper.WithRetryAsync(async () =>
        {
            var connection = await GetPartitionLeaderConnectionAsync(partition, cancellationToken).ConfigureAwait(false);
            if (connection is null)
                return 0;

            var listOffsetsVersion = _metadataManager.GetNegotiatedApiVersion(
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

            var response = await connection.SendAsync<ListOffsetsRequest, ListOffsetsResponse>(
                request,
                listOffsetsVersion,
                cancellationToken).ConfigureAwait(false);

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

            return partitionResponse?.Offset ?? 0;
        }, _metadataManager, cancellationToken);
    }

    private async ValueTask<IKafkaConnection?> GetPartitionLeaderConnectionAsync(TopicPartition partition, CancellationToken cancellationToken)
    {
        var leader = await _metadataManager.GetPartitionLeaderAsync(partition.Topic, partition.Partition, cancellationToken)
            .ConfigureAwait(false);

        if (leader is null)
            return null;

        return await _connectionPool.GetConnectionByIndexAsync(leader.NodeId, 0, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask FetchRecordsAsync(CancellationToken cancellationToken)
    {
        // Rent a CTS from the pool to avoid allocating a LinkedCTS
        var consumeCts = _ctsPool.Rent();
        _activeConsumeCancellationSources.TryAdd(consumeCts, 0);

        try
        {
            // Forward outer cancellation into the pooled CTS via registration
            // instead of allocating a LinkedCTS (matches the prefetch path pattern)
            using var reg = cancellationToken.CanBeCanceled
                ? cancellationToken.Register(static s => ((CancellationTokenSource)s!).Cancel(), consumeCts)
                : default;

            // Close any race window: if token was cancelled between method entry and registration
            if (cancellationToken.IsCancellationRequested)
                consumeCts.Cancel();

            var partitionsByBroker = await GroupPartitionsByBrokerAsync(cancellationToken).ConfigureAwait(false);
            var fetchSessionSnapshot = ShouldUseFetchSessions && !_fetchSessions.IsEmpty
                ? _fetchSessions.ToArray()
                : Array.Empty<KeyValuePair<(int BrokerId, int ConnectionIndex), FetchSessionHandler>>();

            // If all partitions are paused, delay to prevent tight spin loop
            // that would starve timeout/cancellation mechanisms of CPU time
            var brokerCount = partitionsByBroker.Count;
            if (brokerCount == 0 && fetchSessionSnapshot.Length == 0)
            {
                await Task.Delay(AllPartitionsPausedDelayMs, cancellationToken).ConfigureAwait(false);
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
                        brokerId, partitions, consumeCts.Token, consumeCts.Token);
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
                        key.BrokerId, [], consumeCts.Token, consumeCts.Token);
                }

                if (taskCount == 0)
                {
                    await Task.Delay(AllPartitionsPausedDelayMs, cancellationToken).ConfigureAwait(false);
                    return;
                }

                // Timing wraps the entire parallel fetch cycle (all brokers via Task.WhenAll)
                // rather than individual broker fetches. This avoids a data race on the timing
                // fields from concurrent FetchFromBrokerAsync calls, and correctly measures
                // the bottleneck (slowest broker) which is the right signal for adaptive sizing.
                _adaptiveFetchSizer?.RecordFetchStart();

                // ReadOnlySpan overload: same zero-copy benefit as above
                await Task.WhenAll(new ReadOnlySpan<Task<List<PendingFetchData>?>>(fetchTasks, 0, taskCount)).ConfigureAwait(false);

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
                                _pendingFetches.Enqueue(pending);
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
            consumeCts.Dispose();
        }
    }

    private async Task<List<PendingFetchData>?> FetchFromBrokerWithErrorHandlingAsync(
        int brokerId,
        List<TopicPartition> partitions,
        CancellationToken linkedToken,
        CancellationToken consumeCancellationToken)
    {
        try
        {
            return await FetchFromBrokerAsync(brokerId, partitions, linkedToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (consumeCancellationToken.IsCancellationRequested)
        {
            // Consume cancellation requested, exit silently
            return null;
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
    /// Also marks manual assignment state dirty and invalidates the coordinator assignment
    /// fast path, so all assignment mutation paths must use this method.
    /// </summary>
    private void PublishAssignmentSnapshot()
    {
        _assignmentSnapshot = new HashSet<TopicPartition>(_assignment);
        Interlocked.Increment(ref _assignmentEnsureVersion);
        Volatile.Write(ref _lastCoordinatorAssignmentVersion, -1);
    }

    /// <summary>
    /// Publishes an immutable snapshot of <see cref="_subscription"/> for lock-free reads.
    /// Must be called after every mutation to <see cref="_subscription"/>.
    /// </summary>
    private void PublishSubscriptionSnapshot()
    {
        _subscriptionSnapshot = _subscription.Keys.ToHashSet();
    }

    /// <summary>
    /// Publishes an immutable snapshot of <see cref="_paused"/> for lock-free reads.
    /// Must be called after every mutation to <see cref="_paused"/>.
    /// </summary>
    private void PublishPausedSnapshot()
    {
        _pausedSnapshot = _paused.Keys.ToHashSet();
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

    private async ValueTask<Dictionary<int, List<TopicPartition>>> GroupPartitionsByBrokerAsync(CancellationToken cancellationToken)
    {
        // Check cache and capture version to detect concurrent invalidation
        int capturedVersion;
        TopicPartition[] assignmentArray;
        int assignmentCount;

        lock (_partitionCacheLock)
        {
            if (_cachedPartitionsByBroker is not null && _preferredReadReplicas.IsEmpty)
            {
                return _cachedPartitionsByBroker;
            }

            // Capture version and copy assignment/paused data under lock
            // Uses the immutable snapshot for thread-safe enumeration without _assignmentLock
            capturedVersion = Volatile.Read(ref _assignmentVersion);
            var snapshot = _assignmentSnapshot;
            var maxPartitions = snapshot.Count;
            assignmentArray = ArrayPool<TopicPartition>.Shared.Rent(maxPartitions);
            assignmentCount = 0;

            foreach (var partition in snapshot)
            {
                if (!_paused.ContainsKey(partition))
                {
                    assignmentArray[assignmentCount++] = partition;
                }
            }
        }

        // Build the cache outside the lock - allocate once per assignment change.
        // Resolve all partition fetch brokers in parallel for maximum throughput.
        var brokerTasks = ArrayPool<Task<(TopicPartition Partition, BrokerNode? Broker)>>.Shared.Rent(assignmentCount);
        try
        {
            for (var i = 0; i < assignmentCount; i++)
            {
                brokerTasks[i] = ResolvePartitionFetchBrokerAsync(assignmentArray[i], cancellationToken);
            }

            await Task.WhenAll(new ReadOnlySpan<Task<(TopicPartition Partition, BrokerNode? Broker)>>(brokerTasks, 0, assignmentCount)).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<TopicPartition>.Shared.Return(assignmentArray, clearArray: true);
        }

        // Build result dictionary from resolved partitions (tasks already completed)
        var result = new Dictionary<int, List<TopicPartition>>();
        for (var i = 0; i < assignmentCount; i++)
        {
            var (partition, broker) = brokerTasks[i].Result;
            if (broker is null)
                continue;

            if (!result.TryGetValue(broker.NodeId, out var list))
            {
                list = [];
                result[broker.NodeId] = list;
            }

            list.Add(partition);
        }

        // Return pooled array after extracting results
        ArrayPool<Task<(TopicPartition Partition, BrokerNode? Broker)>>.Shared.Return(brokerTasks, clearArray: true);

        // Cache the result - will be reused until assignment/paused changes.
        // Don't cache empty results: an empty dictionary means partition leaders
        // couldn't be resolved (metadata not yet available). Caching it would prevent
        // recovery when metadata becomes available, causing the prefetch loop to spin
        // forever producing nothing.
        lock (_partitionCacheLock)
        {
            if (result.Count > 0 && _cachedPartitionsByBroker is null
                && _preferredReadReplicas.IsEmpty
                && Volatile.Read(ref _assignmentVersion) == capturedVersion)
            {
                _cachedPartitionsByBroker = result;
            }
            return _cachedPartitionsByBroker ?? result;
        }
    }

    private async Task<(TopicPartition Partition, BrokerNode? Broker)> ResolvePartitionFetchBrokerAsync(
        TopicPartition partition,
        CancellationToken cancellationToken)
    {
        var leader = await _metadataManager.GetPartitionLeaderAsync(
            partition.Topic, partition.Partition, cancellationToken).ConfigureAwait(false);
        if (leader is null)
            return (partition, null);

        return (partition, GetPreferredReadReplicaBrokerOrLeader(partition, leader));
    }

    private BrokerNode GetPreferredReadReplicaBrokerOrLeader(TopicPartition partition, BrokerNode leader)
    {
        if (string.IsNullOrEmpty(_options.ClientRack)
            || !_preferredReadReplicas.TryGetValue(partition, out var preferred))
        {
            return leader;
        }

        var now = Stopwatch.GetTimestamp();
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
        return preferredBroker;
    }

    private async ValueTask<List<PendingFetchData>?> FetchFromBrokerAsync(int brokerId, List<TopicPartition> partitions, CancellationToken cancellationToken)
    {
        var connection = await _connectionPool.GetConnectionByIndexAsync(brokerId, 0, cancellationToken).ConfigureAwait(false);

        // Ensure API version is negotiated (thread-safe initialization)
        var apiVersion = _fetchApiVersion;
        if (apiVersion < 0)
        {
            apiVersion = _metadataManager.GetNegotiatedApiVersion(
                ApiKey.Fetch,
                FetchRequest.LowestSupportedVersion,
                FetchRequest.HighestSupportedVersion);
            Interlocked.CompareExchange(ref _fetchApiVersion, apiVersion, -1);
            apiVersion = _fetchApiVersion;
        }

        // Resolve any special offset values (-1 for end, -2 for beginning) before fetching
        await ResolveSpecialOffsetsAsync(partitions, 0, partitions.Count, cancellationToken).ConfigureAwait(false);

        // Build fetch request - use imperative code to avoid LINQ allocations
        var topicData = BuildFetchRequestTopics(partitions, 0, partitions.Count);
        FetchSessionHandler? fetchSessionHandler = null;
        FetchSessionBuildResult? fetchSessionBuild = null;
        if (ShouldUseFetchSessions && apiVersion >= 7)
        {
            fetchSessionHandler = _fetchSessions.GetOrAdd((brokerId, 0), static _ => new FetchSessionHandler());
            fetchSessionBuild = fetchSessionHandler.Build(topicData, _metadataManager.Metadata);
        }

        var request = FetchRequest.Rent();
        request.MaxWaitMs = _options.FetchMaxWaitMs;
        request.MinBytes = _options.FetchMinBytes;
        request.MaxBytes = _adaptiveFetchSizer?.CurrentFetchMaxBytes ?? _options.FetchMaxBytes;
        request.IsolationLevel = _options.IsolationLevel;
        request.RackId = _options.ClientRack;
        request.Topics = fetchSessionBuild?.Topics ?? topicData;
        request.ForgottenTopicsData = fetchSessionBuild?.ForgottenTopicsData;
        request.SessionId = fetchSessionBuild?.SessionId ?? 0;
        request.SessionEpoch = fetchSessionBuild?.SessionEpoch ?? -1;

        var fetchStarted = System.Diagnostics.Stopwatch.GetTimestamp();

        FetchResponse response;
        try
        {
            response = await connection.SendAsync<FetchRequest, FetchResponse>(
                request,
                (short)apiVersion,
                cancellationToken).ConfigureAwait(false);
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

        // Queue pending fetch data for lazy iteration - don't parse records yet!
        try
        {
            foreach (var topicResponse in response.Responses)
            {
                var topic = ResolveTopicName(topicResponse);
                var activityName = _activityNameCache.GetOrAdd(topic, static t => $"{t} receive");

                foreach (var partitionResponse in topicResponse.Partitions)
                {
                    var tp = new TopicPartition(topic, partitionResponse.PartitionIndex);

                    // Update watermark cache from fetch response (even on errors, watermarks may be valid)
                    UpdateWatermarksFromFetchResponse(topic, partitionResponse);
                    UpdatePreferredReadReplica(topic, partitionResponse);

                    if (partitionResponse.DivergingEpoch is not null)
                    {
                        ResetToDivergingEpoch(topic, partitionResponse);
                        continue;
                    }

                    if (partitionResponse.ErrorCode != ErrorCode.None)
                    {
                        if (partitionResponse.ErrorCode == ErrorCode.OffsetOutOfRange)
                        {
                            await ResetOffsetOutOfRangeAsync(tp, cancellationToken).ConfigureAwait(false);
                        }
                        else if (IsLeaderEpochRefreshError(partitionResponse.ErrorCode))
                        {
                            await HandleLeaderEpochRefreshAsync(
                                topic,
                                partitionResponse,
                                response.NodeEndpoints).ConfigureAwait(false);
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
                        // We have new records - reset EOF state for this partition
                        _eofEmitted.TryRemove(tp, out _);

                        // Collect pending fetch data for lazy record iteration
                        pendingItems ??= ConsumerFetchPools.RentPendingFetchDataList();
                        pendingItems.Add(PendingFetchData.Create(
                            topic,
                            partitionResponse.PartitionIndex,
                            records,
                            partitionResponse.AbortedTransactions,
                            activityName: activityName));
                    }
                    else if (_options.EnablePartitionEof)
                    {
                        // No records returned - check if we're at EOF
                        var fetchPosition = _fetchPositions.GetValueOrDefault(tp, 0);

                        // EOF condition: position >= high watermark and we haven't emitted EOF yet
                        if (fetchPosition >= partitionResponse.HighWatermark && _eofEmitted.TryAdd(tp, 0))
                        {
                            // Queue EOF event and mark as emitted
                            _pendingEofEvents.Enqueue((tp, fetchPosition));
                        }
                    }
                }
            }
        }
        finally
        {
            // Return the response and its nested objects to their pools.
            // Data has been transferred to PendingFetchData; the response wrappers are no longer needed.
            response.ReturnToPool();
        }

        if (pendingItems is not null && pendingItems.Count > 0 && memoryOwner is not null)
        {
            AssignSharedMemoryOwner(pendingItems, memoryOwner);
            memoryOwner = null; // Transferred
        }

        // If no pending items were created but we have a memory owner, dispose it
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

    /// <summary>
    /// Creates and configures a tracing Activity for consume operations.
    /// Separated from the hot path to avoid inlining overhead when no listeners are active.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private System.Diagnostics.Activity? StartConsumeActivity(
        PendingFetchData pending,
        IReadOnlyList<Header>? headers,
        long offset,
        int messageBytes)
    {
        // Use span links (not parent-child) per OTel messaging semantic conventions:
        // the consumer span gets its own trace root, linked to the producer span.
        var producerContext = Diagnostics.TraceContextPropagator.ExtractTraceContext(headers);
        System.Diagnostics.Activity? activity;
        var savedActivity = System.Diagnostics.Activity.Current;
        System.Diagnostics.Activity.Current = null;
        try
        {
            if (producerContext.HasValue)
            {
                activity = Diagnostics.DekafDiagnostics.Source.StartActivity(
                    pending.ActivityName,
                    System.Diagnostics.ActivityKind.Consumer,
                    parentContext: default(System.Diagnostics.ActivityContext),
                    tags: null,
                    links: [new System.Diagnostics.ActivityLink(producerContext.Value)]);
            }
            else
            {
                activity = Diagnostics.DekafDiagnostics.Source.StartActivity(
                    pending.ActivityName,
                    System.Diagnostics.ActivityKind.Consumer);
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
            activity.SetTag(Diagnostics.DekafDiagnostics.MessagingOperationType, "receive");
            activity.SetTag(Diagnostics.DekafDiagnostics.MessagingDestinationPartitionId, pending.PartitionIndex);
            activity.SetTag(Diagnostics.DekafDiagnostics.MessagingMessageOffset, offset);
            activity.SetTag(Diagnostics.DekafDiagnostics.MessagingMessageBodySize, messageBytes);
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
        foreach (var interceptor in _interceptors!)
        {
            try
            {
                result = interceptor.OnConsume(result);
            }
            catch (Exception ex)
            {
                LogInterceptorOnConsumeError(ex, interceptor.GetType().Name);
            }
        }
        return result;
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
    private void UpdateWatermarksFromFetchResponse(string topic, FetchResponsePartition partitionResponse)
    {
        // Only update if we have valid watermark data
        // HighWatermark is the next offset to be written (end of log)
        // LogStartOffset is the earliest available offset (start of log, may be > 0 due to retention)
        if (partitionResponse.HighWatermark >= 0)
        {
            var tp = new TopicPartition(topic, partitionResponse.PartitionIndex);
            var low = partitionResponse.LogStartOffset >= 0 ? partitionResponse.LogStartOffset : 0;
            _watermarks[tp] = new WatermarkOffsets(low, partitionResponse.HighWatermark);
        }
    }

    private int GetCurrentLeaderEpoch(TopicPartition partition) =>
        _metadataManager.Metadata.GetPartitionInfo(partition.Topic, partition.Partition)?.LeaderEpoch ?? -1;

    private int GetLastConsumedLeaderEpoch(TopicPartition partition) =>
        _lastConsumedLeaderEpochs.GetValueOrDefault(partition, -1);

    private void ThrowPendingFetchException()
    {
        if (_pendingFetchExceptions.TryDequeue(out var exception))
            throw exception;
    }

    private void QueueFetchException(Exception exception)
    {
        _pendingFetchExceptions.Enqueue(exception);
    }

    private void ResetToDivergingEpoch(string topic, FetchResponsePartition partitionResponse)
    {
        if (partitionResponse.DivergingEpoch is not { } divergingEpoch)
            return;

        var partition = new TopicPartition(topic, partitionResponse.PartitionIndex);
        _fetchPositions[partition] = divergingEpoch.EndOffset;
        SetPosition(partition, divergingEpoch.EndOffset, dirty: false);
        SetLastConsumedLeaderEpoch(partition, divergingEpoch.Epoch);
        QueueFetchException(new Errors.ConsumeException(
            ErrorCode.OffsetOutOfRange,
            $"Log truncation detected for {topic}-{partitionResponse.PartitionIndex}; reset fetch position to offset {divergingEpoch.EndOffset} at leader epoch {divergingEpoch.Epoch}.",
            isRetriable: true));
    }

    private async ValueTask ResetOffsetOutOfRangeAsync(
        TopicPartition partition,
        CancellationToken cancellationToken)
    {
        // Reset fetch position based on auto.offset.reset policy. Without this, the
        // consumer would retry the same invalid offset forever.
        var resetTimestamp = AutoOffsetResetStrategy.GetListOffsetsTimestamp(_options, DateTimeOffset.UtcNow, partition);
        if (resetTimestamp == LatestOffsetTimestamp || resetTimestamp == EarliestOffsetTimestamp)
        {
            _fetchPositions[partition] = resetTimestamp;
            SetPosition(partition, resetTimestamp, dirty: false);
            ClearLastConsumedLeaderEpoch(partition);
        }
        else
        {
            var resetOffset = await ResolveAutoResetOffsetAsync(partition, resetTimestamp, cancellationToken).ConfigureAwait(false);
            _fetchPositions[partition] = resetOffset;
            SetPosition(partition, resetOffset, dirty: false);
            ClearLastConsumedLeaderEpoch(partition);
        }

        LogOffsetOutOfRangeReset(partition.Topic, partition.Partition, GetAutoOffsetResetName());
    }

    private static bool IsLeaderEpochRefreshError(ErrorCode errorCode) =>
        errorCode is ErrorCode.NotLeaderOrFollower or ErrorCode.FencedLeaderEpoch or ErrorCode.UnknownLeaderEpoch;

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

        return ValueTask.CompletedTask;
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
        List<TopicPartition> partitions, int startIndex, int count)
    {
        if (count == 0)
            return ConsumerFetchPools.RentFetchRequestTopicList(0);

        var isFullList = startIndex == 0 && count == partitions.Count;

        // Take snapshots of current state under lock
        Dictionary<string, List<(FetchRequestPartition Partition, TopicPartition TopicPartition)>>? cachedDict;
        List<TopicPartition>? cachedList;

        lock (_fetchCacheLock)
        {
            cachedDict = _cachedTopicPartitions;
            cachedList = _cachedPartitionsList;
        }

        // Check if cache is valid (same partition list as before) — only for full-list case.
        // When fetchConnectionCount > 1, all calls use subranges so the cache is bypassed.
        // This is intentional: the cache avoids rebuilding the topic→partition dictionary when
        // partitions are stable, but subranges change per-connection and aren't worth caching.
        if (isFullList && cachedDict is not null && cachedList is not null && PartitionListsEqual(partitions, cachedList))
        {
            // Cache hit: build a fresh result list with snapshot offsets under lock.
            // Each broker task gets its own FetchRequestPartition objects so that
            // concurrent calls cannot mutate offsets visible to another task.
            // This allocates per fetch cycle (per-batch), not per-message.
            return BuildFetchResult(
                cachedDict,
                _fetchPositions,
                _adaptiveFetchSizer?.CurrentPartitionFetchBytes,
                _metadataManager.Metadata,
                _lastConsumedLeaderEpochs);
        }

        // Cache miss (or subrange): build fresh structure with TopicPartition stored alongside
        var topicPartitions = new Dictionary<string, List<(FetchRequestPartition Partition, TopicPartition TopicPartition)>>();

        var endIndex = startIndex + count;
        for (var i = startIndex; i < endIndex; i++)
        {
            var p = partitions[i];
            if (!topicPartitions.TryGetValue(p.Topic, out var list))
            {
                list = [];
                topicPartitions[p.Topic] = list;
            }

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
        var result = BuildFetchResult(
            topicPartitions,
            _fetchPositions,
            _adaptiveFetchSizer?.CurrentPartitionFetchBytes,
            _metadataManager.Metadata,
            _lastConsumedLeaderEpochs);

        // Update cache (first writer wins to avoid overwriting fresher data) — only for full-list case
        if (isFullList)
        {
            lock (_fetchCacheLock)
            {
                if (_cachedTopicPartitions is null)
                {
                    _cachedTopicPartitions = topicPartitions;
                    _cachedPartitionsList = new List<TopicPartition>(partitions);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Builds a fresh <see cref="FetchRequestTopic"/> list from a partition template dictionary.
    /// Works for both the shared cache and freshly-built dictionaries (cache-miss path) —
    /// in both cases, each call creates new <see cref="FetchRequestPartition"/> objects with
    /// snapshot offsets from <paramref name="fetchPositions"/>, so concurrent callers
    /// cannot observe each other's offset values.
    /// Allocation is per fetch cycle (per-batch), not per-message.
    /// </summary>
    private string ResolveTopicName(FetchResponseTopic topicResponse)
    {
        if (topicResponse.TopicId != Guid.Empty)
        {
            var resolved = _metadataManager.Metadata.GetTopic(topicResponse.TopicId)?.Name;
            if (resolved is not null)
            {
                return resolved;
            }

            // TopicId present but not in local metadata — fall back to topic name from response
            LogUnknownTopicId(topicResponse.TopicId);
            return topicResponse.Topic ?? string.Empty;
        }

        return topicResponse.Topic ?? string.Empty;
    }

    internal static List<FetchRequestTopic> BuildFetchResult(
        Dictionary<string, List<(FetchRequestPartition Partition, TopicPartition TopicPartition)>> templateDict,
        ConcurrentDictionary<TopicPartition, long> fetchPositions,
        int? adaptivePartitionMaxBytes = null,
        ClusterMetadata? clusterMetadata = null,
        ConcurrentDictionary<TopicPartition, int>? lastConsumedLeaderEpochs = null)
    {
        var result = ConsumerFetchPools.RentFetchRequestTopicList(templateDict.Count);

        foreach (var kvp in templateDict)
        {
            var cachedPartitions = kvp.Value;
            var partitionList = ConsumerFetchPools.RentFetchRequestPartitionList(cachedPartitions.Count);

            foreach (var (template, tp) in cachedPartitions)
            {
                var currentLeaderEpoch = clusterMetadata?.GetPartitionInfo(tp.Topic, tp.Partition)?.LeaderEpoch ?? -1;
                partitionList.Add(new FetchRequestPartition
                {
                    Partition = template.Partition,
                    FetchOffset = fetchPositions.GetValueOrDefault(tp, 0),
                    CurrentLeaderEpoch = currentLeaderEpoch,
                    LastFetchedEpoch = lastConsumedLeaderEpochs?.GetValueOrDefault(tp, -1) ?? -1,
                    LogStartOffset = template.LogStartOffset,
                    PartitionMaxBytes = adaptivePartitionMaxBytes ?? template.PartitionMaxBytes
                });
            }

            result.Add(new FetchRequestTopic
            {
                Topic = kvp.Key,
                TopicId = clusterMetadata?.GetTopic(kvp.Key)?.TopicId ?? Guid.Empty,
                Partitions = partitionList
            });
        }

        return result;
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

    /// <summary>
    /// Order-independent partition list equality check.
    /// Uses O(n²) comparison for small lists (allocation-free), O(n) HashSet for larger lists.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool PartitionListsEqual(List<TopicPartition> a, List<TopicPartition> b)
    {
        if (a.Count != b.Count)
            return false;

        // For small lists, use O(n²) comparison to avoid HashSet allocation
        if (a.Count <= 16)
        {
            foreach (var partitionA in a)
            {
                var found = false;
                foreach (var partitionB in b)
                {
                    if (partitionA.Topic == partitionB.Topic && partitionA.Partition == partitionB.Partition)
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
        var setB = new HashSet<TopicPartition>(b);
        foreach (var partitionA in a)
        {
            if (!setB.Contains(partitionA))
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
            _cachedTopicPartitions = null;
            _cachedPartitionsList = null;
        }
    }

    /// <summary>
    /// Ratchets process-global consumer pool sizes based on actual partition count.
    /// Only <see cref="PendingFetchData"/> is ratcheted — the per-instance CTS pool
    /// is fixed at construction and cannot be resized after creation.
    /// </summary>
    private static void RatchetConsumerPoolSizes(int partitionCount)
    {
        var sizes = PoolSizing.ForConsumer(partitionCount);
        PendingFetchData.RatchetPoolSize(sizes.FetchDataPool);
    }

    private async Task StartAutoCommitAsync(CancellationToken cancellationToken)
    {
        var currentTask = _autoCommitTask;
        if (currentTask is { IsCompleted: false })
            return;

        if (currentTask is not null)
        {
            try
            {
                await currentTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Swallow — old task may have completed from cancellation or fault.
            }
            _autoCommitCts?.Dispose();
        }

        _autoCommitCts = new CancellationTokenSource();
        _autoCommitTask = AutoCommitLoopAsync(_autoCommitCts.Token);
    }

    private async Task AutoCommitLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.AutoCommitIntervalMs, cancellationToken).ConfigureAwait(false);

                // Only commit if coordinator is stable (fully joined)
                if (_coordinator is null || _coordinator.State != CoordinatorState.Stable)
                    continue;

                await CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogAutoCommitFailed(ex);
                try
                {
                    await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                    await CommitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch { /* Best effort — will retry on next interval */ }
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        // Idempotent - return early if already closed/disposed
        if (Interlocked.Exchange(ref _closed, 1) != 0 || Volatile.Read(ref _consumerDisposed) != 0)
            return;

        await CloseAsyncCore(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Core teardown logic shared by <see cref="CloseAsync"/> and <see cref="DisposeAsync"/>.
    /// Callers must ensure this is invoked at most once via an atomic CAS on <c>_closed</c>.
    /// </summary>
    private async ValueTask CloseAsyncCore(CancellationToken cancellationToken)
    {
        LogClosingConsumer();

        // Step 1: Stop heartbeat background task
        if (_coordinator is not null)
        {
            await _coordinator.StopHeartbeatAsync().ConfigureAwait(false);
        }

        // Step 2: Stop leader-refresh tasks before metadata dependencies are disposed
        _leaderRefreshCts.Cancel();
        await WaitForLeaderRefreshTasksAsync(cancellationToken).ConfigureAwait(false);

        // Step 3: Stop auto-commit task
        // Use WaitAsync with both a hard timeout and the caller's cancellation token so that
        // DisposeAsync's 30s CTS actually bounds this step. Without this, a mid-flight
        // CommitAsync (which waits up to RequestTimeoutMs=30s for network I/O) would cause
        // CloseAsync to hang for the full network timeout, chaining across multiple consumers
        // during sequential `await using` disposal and exceeding test timeouts.
        _autoCommitCts?.Cancel();
        if (_autoCommitTask is not null)
        {
            try
            {
                await _autoCommitTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Ignore — task may not exit promptly after cancellation
            }
        }

        // Step 4: Stop prefetch task
        // Same rationale as Step 3: a mid-flight FetchAsync network operation could hang for
        // up to RequestTimeoutMs without the WaitAsync timeout bounding it.
        _prefetchCts?.Cancel();
        if (_prefetchTask is not null)
        {
            try
            {
                await _prefetchTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Ignore — task may not exit promptly after cancellation
            }
        }

        // Step 5: Commit pending offsets (if auto-commit enabled and we have a coordinator)
        if (_options.OffsetCommitMode == OffsetCommitMode.Auto && _coordinator is not null && !_dirtyPositions.IsEmpty)
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    await CommitAsync(cancellationToken).ConfigureAwait(false);
                    LogCommittedPendingOffsets();
                    break;
                }
                catch (OperationCanceledException)
                {
                    break; // Caller cancelled — don't retry
                }
                catch (Exception ex)
                {
                    LogCommitOffsetsDuringCloseFailed(ex, attempt + 1);
                    if (attempt < 2)
                        await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        // Step 5: Send LeaveGroup request to coordinator
        if (_coordinator is not null)
        {
            try
            {
                await _coordinator.LeaveGroupAsync(cancellationToken).ConfigureAwait(false);
                LogLeftConsumerGroup();
            }
            catch (Exception ex)
            {
                LogLeaveGroupFailed(ex);
            }
        }

        // Step 6: Cancel any blocked fetch operations
        CancelActiveConsumeOperations();

        // Step 7: Clear pending fetch data and dispose to release pooled memory
        while (_pendingFetches.TryDequeue(out var pending))
        {
            pending.Dispose();
        }
        while (_prefetchBuffer.TryRead(out var prefetched))
        {
            TrackPrefetchedBytes(prefetched, release: true);
            prefetched.Dispose();
        }

        await _telemetryManager.StopAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);

        LogConsumerClosed();
    }

    public async ValueTask<IReadOnlyDictionary<TopicPartition, long>> GetOffsetsForTimesAsync(
        IEnumerable<TopicPartitionTimestamp> timestampsToSearch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(timestampsToSearch);

        if (Volatile.Read(ref _consumerDisposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaConsumer<TKey, TValue>));

        ThrowIfNotInitialized();

        var timestamps = timestampsToSearch as IReadOnlyList<TopicPartitionTimestamp>
            ?? [.. timestampsToSearch];

        return await RetryHelper.WithRetryAsync<IReadOnlyDictionary<TopicPartition, long>>(async () =>
        {
            // Group partitions by broker leader for efficient batch requests.
            // Retrying the whole operation re-groups after metadata refreshes.
            var partitionsByBroker = new Dictionary<int, List<TopicPartitionTimestamp>>();
            foreach (var tpt in timestamps)
            {
                var leader = await _metadataManager.GetPartitionLeaderAsync(tpt.Topic, tpt.Partition, cancellationToken)
                    .ConfigureAwait(false);

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
                var brokerResults = await GetOffsetsForTimesFromBrokerAsync(brokerId, partitions, cancellationToken)
                    .ConfigureAwait(false);

                foreach (var kvp in brokerResults)
                {
                    results[kvp.Key] = kvp.Value;
                }
            }

            return results;
        }, _metadataManager, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Dictionary<TopicPartition, long>> GetOffsetsForTimesFromBrokerAsync(
        int brokerId,
        List<TopicPartitionTimestamp> partitions,
        CancellationToken cancellationToken)
    {
        var connection = await _connectionPool.GetConnectionByIndexAsync(brokerId, 0, cancellationToken).ConfigureAwait(false);

        var listOffsetsVersion = _metadataManager.GetNegotiatedApiVersion(
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

        var response = await connection.SendAsync<ListOffsetsRequest, ListOffsetsResponse>(
            request,
            listOffsetsVersion,
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

        var disposeStart = System.Diagnostics.Stopwatch.GetTimestamp();
        LogConsumerDisposing();

        // Unregister lag callback so the OTel SDK no longer invokes it on this disposed instance
        Diagnostics.DekafMetrics.UnregisterConsumerLagCallback(ObserveConsumerLag);

        // If not already closed, perform graceful close first
        // Use 30 seconds to allow CommitAsync (which may take up to RequestTimeoutMs=30s) to complete
        // Interlocked.Exchange prevents the TOCTOU gap where both CloseAsync and DisposeAsync
        // could race to run teardown concurrently when using Volatile.Read + separate CloseAsync CAS.
        if (Interlocked.Exchange(ref _closed, 1) == 0)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await CloseAsyncCore(cts.Token).ConfigureAwait(false);
            }
            catch
            {
                // Ignore errors during dispose
            }
        }

        var closeElapsedMs = System.Diagnostics.Stopwatch.GetElapsedTime(disposeStart).TotalMilliseconds;
        LogConsumerCloseCompleted(closeElapsedMs);

        CancelActiveConsumeOperations();
        _leaderRefreshCts.Cancel();
        _autoCommitCts?.Cancel();
        _prefetchCts?.Cancel();

        await WaitForLeaderRefreshTasksAsync().ConfigureAwait(false);

        if (_autoCommitTask is not null)
        {
            try
            {
                await _autoCommitTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch
            {
                // Ignore — task may not exit promptly after cancellation
            }
        }

        if (_prefetchTask is not null)
        {
            try
            {
                await _prefetchTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch
            {
                // Ignore — task may not exit promptly after cancellation
            }
        }

        _autoCommitCts?.Dispose();
        _prefetchCts?.Dispose();
        _leaderRefreshCts.Dispose();

        // Clear and dispose CancellationTokenSource pool
        // Note: active consume cancellation sources are managed by the pool and should not be disposed here
        _ctsPool.Clear();

        // Clear and dispose any pending fetch data to release pooled memory
        while (_pendingFetches.TryDequeue(out var pending))
        {
            pending.Dispose();
        }

        // Drain and dispose prefetch buffer items
        while (_prefetchBuffer.TryRead(out var prefetched))
        {
            TrackPrefetchedBytes(prefetched, release: true);
            prefetched.Dispose();
        }

        _assignmentLock.Dispose();
        _initLock.Dispose();
        _prefetchMemoryAvailable.Dispose();

        if (_coordinator is not null)
            await _coordinator.DisposeAsync().ConfigureAwait(false);

        await _telemetryManager.DisposeAsync().ConfigureAwait(false);
        if (_ownsInfrastructure)
        {
            await _metadataManager.DisposeAsync().ConfigureAwait(false);
            await _connectionPool.DisposeAsync().ConfigureAwait(false);
        }

        var disposeElapsedMs = System.Diagnostics.Stopwatch.GetElapsedTime(disposeStart).TotalMilliseconds;
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error in prefetch loop")]
    private partial void LogPrefetchLoopError(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Fatal error prefetching from broker {BrokerId}")]
    private partial void LogFatalPrefetchError(Exception exception, int brokerId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to prefetch from broker {BrokerId}")]
    private partial void LogPrefetchFromBrokerError(Exception exception, int brokerId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OffsetOutOfRange for {Topic}-{Partition}, resetting to {Reset}")]
    private partial void LogOffsetOutOfRangeReset(string topic, int partition, string reset);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Error} for {Topic}-{Partition}, refreshing leader metadata")]
    private partial void LogLeaderEpochRefresh(string topic, int partition, ErrorCode error);

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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Closing consumer gracefully")]
    private partial void LogClosingConsumer();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Committed pending offsets during close")]
    private partial void LogCommittedPendingOffsets();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to commit offsets during close (attempt {Attempt}/3)")]
    private partial void LogCommitOffsetsDuringCloseFailed(Exception exception, int attempt);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Left consumer group during close")]
    private partial void LogLeftConsumerGroup();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to leave group during close")]
    private partial void LogLeaveGroupFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Consumer closed gracefully")]
    private partial void LogConsumerClosed();

    [LoggerMessage(Level = LogLevel.Warning, Message = "No leader found for {Topic}-{Partition}")]
    private partial void LogNoLeaderFound(string topic, int partition);

    [LoggerMessage(Level = LogLevel.Warning, Message = "TopicId {TopicId} not found in local metadata, falling back to topic name from response")]
    private partial void LogUnknownTopicId(Guid topicId);

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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consumer disposing: beginning shutdown")]
    private partial void LogConsumerDisposing();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consumer close phase completed in {ElapsedMs:F0}ms")]
    private partial void LogConsumerCloseCompleted(double elapsedMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Consumer disposed in {ElapsedMs:F0}ms")]
    private partial void LogConsumerDisposed(double elapsedMs);

    #endregion

    #region IRawRecordAccessor

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
