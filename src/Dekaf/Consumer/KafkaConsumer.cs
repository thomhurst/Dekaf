using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Dekaf.Compression;
using Dekaf.Internal;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Producer;
using Dekaf.Serialization;
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
    // Typically 1-6 instances per fetch cycle. Soft limit avoids ConcurrentStack.Count overhead.
    private static readonly ConcurrentStack<PendingFetchData> s_pool = new();
    private static int s_poolCount;
    private const int MaxPoolSize = 128;

    private IReadOnlyList<RecordBatch> _batches = null!;
    private Dictionary<long, Queue<long>>? _abortedProducers;
    private IPooledMemory? _memoryOwner;
    private int _batchIndex = -1;
    private int _recordIndex = -1;
    private int _disposed;

    public string Topic { get; private set; } = null!;
    public int PartitionIndex { get; private set; }

    /// <summary>
    /// Cached activity name to avoid per-record string interpolation in consume loop.
    /// </summary>
    internal string ActivityName { get; private set; } = null!;

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

    /// <summary>
    /// Tracks total bytes consumed in this pending fetch.
    /// </summary>
    public long TotalBytesConsumed { get; private set; }

    /// <summary>
    /// Tracks the number of messages yielded from this pending fetch.
    /// Using long to prevent overflow in long-running scenarios with large fetches.
    /// </summary>
    public long MessageCount { get; private set; }

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
        instance.ActivityName = activityName ?? $"{topic} receive";
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
        if (s_pool.TryPop(out var instance))
        {
            Interlocked.Decrement(ref s_poolCount);
            Volatile.Write(ref instance._disposed, 0);
            return instance;
        }
        return new PendingFetchData();
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
    public Record CurrentRecord => _batches[_batchIndex].Records[_recordIndex];

    /// <summary>
    /// Updates tracking for batch-level position.
    /// Called after yielding each record.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrackConsumed(long offset, int messageBytes)
    {
        LastYieldedOffset = offset;
        TotalBytesConsumed += messageBytes;
        MessageCount++;
    }

    /// <summary>
    /// Gets all batches for memory estimation.
    /// </summary>
    public IReadOnlyList<RecordBatch> GetBatches() => _batches;

    /// <summary>
    /// Advances to the next record across all batches.
    /// Returns false when no more records are available.
    /// </summary>
    public bool MoveNext()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(PendingFetchData));

        // First call - start at first batch, first record
        if (_batchIndex < 0)
        {
            _batchIndex = 0;
            _recordIndex = 0;
            return HasCurrentRecord();
        }

        // Try next record in current batch
        _recordIndex++;
        if (_recordIndex < _batches[_batchIndex].Records.Count)
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

            if (_recordIndex < _batches[_batchIndex].Records.Count)
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

        // Dispose all batches to mark them as disposed
        foreach (var batch in _batches)
        {
            batch.Dispose();
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
        LastYieldedOffset = -1;
        TotalBytesConsumed = 0;
        MessageCount = 0;
        Topic = null!;
        ActivityName = null!;
        _abortedProducers?.Clear();

        // Return to pool (soft limit)
        if (Volatile.Read(ref s_poolCount) < MaxPoolSize)
        {
            s_pool.Push(this);
            Interlocked.Increment(ref s_poolCount);
        }
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
///     Coordinates with the user thread via the bounded <see cref="_prefetchChannel"/> and
///     <see cref="_prefetchLock"/> for EOF state.</description></item>
///   <item><description><b>Heartbeat</b> (managed by <see cref="ConsumerCoordinator"/>): Sends
///     periodic heartbeats to the group coordinator on a background thread to keep the consumer
///     alive in the group.</description></item>
///   <item><description><b>Auto-commit</b> (<see cref="AutoCommitLoopAsync"/>): Periodically
///     commits consumed offsets on a background thread when <c>OffsetCommitMode.Auto</c> is
///     enabled.</description></item>
/// </list>
/// <para>Thread-safe data structures (<see cref="ConcurrentDictionary{TKey,TValue}"/>,
/// <see cref="ConcurrentQueue{T}"/>, <see cref="System.Threading.Channels.Channel{T}"/>)
/// and locks (<see cref="_assignmentLock"/>, <see cref="_prefetchLock"/>) are used to coordinate
/// between the user thread and these background tasks.</para>
/// </remarks>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public sealed partial class KafkaConsumer<TKey, TValue> : IKafkaConsumer<TKey, TValue>, DeadLetter.IRawRecordAccessor
{
    /// <summary>
    /// Delay in milliseconds when all assigned partitions are paused, to prevent
    /// a tight spin loop that would starve CPU while still allowing responsive
    /// cancellation and timeout handling (~10 checks per second).
    /// </summary>
    private const int AllPartitionsPausedDelayMs = 100;

    private readonly ConsumerOptions _options;
    private readonly IDeserializer<TKey> _keyDeserializer;
    private readonly IDeserializer<TValue> _valueDeserializer;
    private readonly IConnectionPool _connectionPool;
    private readonly MetadataManager _metadataManager;
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
    private Func<string, bool>? _topicFilter;
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
    private readonly ConcurrentDictionary<TopicPartition, long> _fetchPositions = new(); // Fetch position (what to fetch next)
    private readonly ConcurrentDictionary<TopicPartition, long> _committed = new();
    private readonly ConcurrentDictionary<TopicPartition, WatermarkOffsets> _watermarks = new(); // Cached watermark offsets from fetch responses


    // Partition EOF tracking
    private readonly ConcurrentDictionary<TopicPartition, long> _highWatermarks = new();  // High watermark per partition (thread-safe for prefetch)
    private readonly HashSet<TopicPartition> _eofEmitted = [];               // Partitions where EOF has been emitted (still needs lock)
    private readonly ConcurrentQueue<(TopicPartition Partition, long Offset)> _pendingEofEvents = new(); // Pending EOF events to yield (thread-safe for prefetch thread)

    // Pending fetch responses for lazy record iteration
    private readonly Queue<PendingFetchData> _pendingFetches = new();

    // Background prefetch support
    private readonly Channel<PendingFetchData> _prefetchChannel;
    private CancellationTokenSource? _prefetchCts;
    private Task? _prefetchTask;
    private long _prefetchedBytes;
    // Initial count 1: at startup, memory IS available (_prefetchedBytes = 0). Starting at 0
    // causes a deadlock if the prefetch loop fills memory before the consumer reads anything —
    // the loop waits for a Release() that never comes because the consumer has no data yet.
    private readonly SemaphoreSlim _prefetchMemoryAvailable = new(1, int.MaxValue);

    // Per-broker reusable lists for collecting pending items during prefetch (avoids per-cycle allocation)
    // Keyed by brokerId since PrefetchFromBrokerAsync runs concurrently for multiple brokers
    private readonly ConcurrentDictionary<int, List<PendingFetchData>> _prefetchPendingItemsByBroker = new();

    // Lock ordering (always acquire in this order to prevent deadlocks):
    //   1. _initLock          — guards one-time initialization; never held while acquiring other locks
    //   2. _assignmentLock    — serializes assignment changes between the consume loop and prefetch loop
    //   3. _prefetchLock      — guards _eofEmitted; acquired under _assignmentLock in EnsureAssignmentAsync
    //   4. _partitionCacheLock / _fetchCacheLock — guard per-broker partition cache and fetch request
    //      cache respectively; acquired under _assignmentLock (via InvalidatePartitionCache /
    //      InvalidateFetchRequestCache) and independently; never nested with each other or with
    //      _prefetchLock
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly SemaphoreSlim _assignmentLock = new(1, 1);
    private readonly object _prefetchLock = new();

    private readonly ConcurrentDictionary<CancellationTokenSource, byte> _activeWakeupSources = new();
    private CancellationTokenSource? _autoCommitCts;
    private Task? _autoCommitTask;
    private int _fetchApiVersion = -1;
    private int _fetchRoundRobinCounter;
    private readonly ConsumerConnectionScaler? _connectionScaler;

    // Dead letter queue raw byte tracking (zero overhead when not enabled)
    private bool _rawRecordTrackingEnabled;
    private ReadOnlyMemory<byte> _currentRawKey;
    private ReadOnlyMemory<byte> _currentRawValue;

    private int _consumerDisposed;
    private int _closed;
    private volatile bool _initialized;
    private bool _prefetchEnabled;

    // CancellationTokenSource pool to avoid allocations in hot paths
    private readonly CancellationTokenSourcePool _ctsPool = new();

    // Cached metric tags per topic to avoid per-message TagList allocation
    // Plain Dictionary is safe: only accessed from the single ConsumeAsync loop thread
    private readonly Dictionary<string, System.Diagnostics.TagList> _metricTagsCache = [];

    // Cached activity names per topic to avoid repeated string interpolation in fetch paths
    // Instance-level to avoid unbounded growth with dynamic topic names across consumer instances
    private readonly ConcurrentDictionary<string, string> _activityNameCache = new();

    // Interceptors - stored as typed array for zero-allocation iteration
    private readonly IConsumerInterceptor<TKey, TValue>[]? _interceptors;

    // Cached partition grouping by broker to avoid allocations on every fetch
    // Invalidated whenever _assignment or _paused changes
    // Access to _cachedPartitionsByBroker must be synchronized via _partitionCacheLock
    private Dictionary<int, List<TopicPartition>>? _cachedPartitionsByBroker;
    private readonly object _partitionCacheLock = new();
    private int _assignmentVersion;

    // Fetch request cache - reduces allocations when partition assignment is stable
    // Cache is invalidated when assignment or paused partitions change
    private readonly object _fetchCacheLock = new();
    private Dictionary<string, List<(FetchRequestPartition Partition, TopicPartition TopicPartition)>>? _cachedTopicPartitions;
    private List<TopicPartition>? _cachedPartitionsList;

    public KafkaConsumer(
        ConsumerOptions options,
        IDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer,
        ILoggerFactory? loggerFactory = null,
        MetadataOptions? metadataOptions = null)
    {
        _options = options;
        _keyDeserializer = keyDeserializer;
        _valueDeserializer = valueDeserializer;
        _logger = loggerFactory?.CreateLogger<KafkaConsumer<TKey, TValue>>() ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<KafkaConsumer<TKey, TValue>>.Instance;

        ArgumentOutOfRangeException.ThrowIfLessThan(options.ConnectionsPerBroker, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.ConnectionsPerBroker, options.MaxConnectionsPerBroker);

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

        _connectionPool = new ConnectionPool(
            options.ClientId,
            new ConnectionOptions
            {
                UseTls = options.UseTls,
                TlsConfig = options.TlsConfig,
                RequestTimeout = TimeSpan.FromMilliseconds(options.RequestTimeoutMs),
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
            ResponseBufferPool.Create(options.FetchMaxBytes));

        _metadataManager = new MetadataManager(
            _connectionPool,
            options.BootstrapServers,
            options: metadataOptions,
            logger: loggerFactory?.CreateLogger<MetadataManager>());

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

        // Initialize prefetch channel - bounded by QueuedMinMessages batches
        var prefetchCapacity = Math.Max(options.QueuedMinMessages, 1);
        _prefetchChannel = Channel.CreateBounded<PendingFetchData>(new BoundedChannelOptions(prefetchCapacity * 10)
        {
            SingleReader = true,
            SingleWriter = false, // Multiple brokers write concurrently via PrefetchFromBrokerAsync
            FullMode = BoundedChannelFullMode.Wait
        });

        // Register this instance's lag callback with the shared static gauge.
        // The callback is invoked only during metric collection (~every 5-60s), not on the hot path.
        Diagnostics.DekafMetrics.RegisterConsumerLagCallback(ObserveConsumerLag);
    }

    public IReadOnlySet<string> Subscription => _subscriptionSnapshot;
    public IReadOnlySet<TopicPartition> Assignment => _assignmentSnapshot;
    public string? MemberId => _coordinator?.MemberId;
    public IReadOnlySet<TopicPartition> Paused => _pausedSnapshot;

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

    public IKafkaConsumer<TKey, TValue> Subscribe(params string[] topics)
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
        InvalidateFetchRequestCache();
        return this;
    }

    public IKafkaConsumer<TKey, TValue> Subscribe(Func<string, bool> topicFilter)
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
        _lastFilterRefreshTicks = 0; // Force immediate refresh on next EnsureAssignment
        InvalidatePartitionCache();
        InvalidateFetchRequestCache();
        return this;
    }

    public IKafkaConsumer<TKey, TValue> Unsubscribe()
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
        InvalidatePartitionCache();
        InvalidateFetchRequestCache();
        return this;
    }

    public IKafkaConsumer<TKey, TValue> Assign(params TopicPartition[] partitions)
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
        InvalidatePartitionCache();
        InvalidateFetchRequestCache();
        return this;
    }

    public IKafkaConsumer<TKey, TValue> Unassign()
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
        InvalidatePartitionCache();
        InvalidateFetchRequestCache();
        return this;
    }

    public IKafkaConsumer<TKey, TValue> IncrementalAssign(IEnumerable<TopicPartitionOffset> partitions)
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
                    _positions[tp] = tpo.Offset;
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
        return this;
    }

    public IKafkaConsumer<TKey, TValue> IncrementalUnassign(IEnumerable<TopicPartition> partitions)
    {
        SemaphoreHelper.AcquireOrThrowDisposed(_assignmentLock, nameof(KafkaConsumer<TKey, TValue>));
        try
        {
            foreach (var partition in partitions)
            {
                _assignment.Remove(partition);
                _paused.TryRemove(partition, out _);
                _positions.TryRemove(partition, out _);
                _fetchPositions.TryRemove(partition, out _);
                _committed.TryRemove(partition, out _);
            }

            PublishAssignmentSnapshot();
        }
        finally
        {
            SemaphoreHelper.ReleaseSafely(_assignmentLock);
        }

        // Clear any pending fetch data for the removed partitions
        ClearFetchBufferForPartitions(partitions);

        PublishPausedSnapshot();
        InvalidatePartitionCache();
        InvalidateFetchRequestCache();
        return this;
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
        if (prefetchEnabled)
        {
            StartPrefetch(cancellationToken);
        }

        while (!cancellationToken.IsCancellationRequested)
        {
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
                    // Try to read from prefetch channel, draining available items up to a bound
                    if (_prefetchChannel.Reader.TryRead(out var prefetched))
                    {
                        _pendingFetches.Enqueue(prefetched);
                        TrackPrefetchedBytes(prefetched, release: true);
                        DrainPrefetchChannel();
                    }
                    else
                    {
                        // Wait for prefetch with timeout, then try direct fetch
                        using var timeoutCts = _ctsPool.Rent();
                        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(_options.FetchMaxWaitMs));
                        using var reg = cancellationToken.CanBeCanceled
                            ? cancellationToken.Register(static s => ((CancellationTokenSource)s!).Cancel(), timeoutCts)
                            : default;

                        // Close any race window: if token was cancelled between method entry and registration
                        if (cancellationToken.IsCancellationRequested)
                            timeoutCts.Cancel();

                        try
                        {
                            // Use WaitToReadAsync + TryRead to avoid OperationCanceledException allocation on timeout.
                            // When the channel is completed with an error, WaitToReadAsync throws it directly
                            // (unlike ReadAsync which wraps in ChannelClosedException).
                            if (await _prefetchChannel.Reader.WaitToReadAsync(timeoutCts.Token).ConfigureAwait(false))
                            {
                                // TryRead cannot miss: SingleReader = true guarantees no concurrent drain
                                if (_prefetchChannel.Reader.TryRead(out var fetched))
                                {
                                    _pendingFetches.Enqueue(fetched);
                                    TrackPrefetchedBytes(fetched, release: true);
                                    DrainPrefetchChannel();
                                }
                                else
                                {
                                    System.Diagnostics.Debug.Fail("WaitToReadAsync signalled data available but TryRead returned false");
                                }
                            }
                            else
                            {
                                // Channel completed without error — prefetch pipeline has stopped.
                                // Reached when PrefetchPipelineRunner exits its loop normally
                                // (e.g., cancellation) and calls TryComplete() in its finally block.
                                break;
                            }
                        }
                        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
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
            try
            {
                while (_pendingFetches.Count > 0)
                {
                    var pending = _pendingFetches.Peek();
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
                            var batch = pending.CurrentBatch;

                            var offset = batch.BaseOffset + record.OffsetDelta;
                            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(
                                batch.BaseTimestamp + record.TimestampDelta);

                            var headers = GetHeaders(record.Headers);
                            var timestampType = ((int)batch.Attributes & 0x08) != 0
                                ? TimestampType.LogAppendTime
                                : TimestampType.CreateTime;

                            var messageBytes = (record.IsKeyNull ? 0 : record.Key.Length) +
                                               (record.IsValueNull ? 0 : record.Value.Length);

                            // Start consumer tracing activity — skip all tracing work when no listener
                            // (~2ns HasListeners() check vs ~200ns Activity creation + tag boxing per message)
                            System.Diagnostics.Activity? activity = null;
                            if (Diagnostics.DekafDiagnostics.Source.HasListeners())
                            {
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
                                headers: headers,
                                timestamp: timestamp,
                                timestampType: timestampType,
                                leaderEpoch: null,
                                keyDeserializer: _keyDeserializer,
                                valueDeserializer: _valueDeserializer);

                            pending.TrackConsumed(offset, messageBytes);

                            // Record consumer metrics — skip TagList allocation when no meter listener.
                            // Check both instruments independently since Enabled is per-instrument.
                            if (Diagnostics.DekafMetrics.MessagesReceived.Enabled || Diagnostics.DekafMetrics.BytesReceived.Enabled)
                            {
                                if (!_metricTagsCache.TryGetValue(pending.Topic, out var metricTags))
                                {
                                    metricTags = new System.Diagnostics.TagList
                                        { { Diagnostics.DekafDiagnostics.MessagingDestinationName, pending.Topic } };
                                    _metricTagsCache[pending.Topic] = metricTags;
                                }
                                Diagnostics.DekafMetrics.MessagesReceived.Add(1, metricTags);
                                Diagnostics.DekafMetrics.BytesReceived.Add(messageBytes, metricTags);
                            }

                            // Apply OnConsume interceptors before yielding to user
                            nextResult = ApplyOnConsumeInterceptors(nextResult);

                            // Store raw byte references for DLQ lazy capture (zero-copy — just memory slices)
                            if (_rawRecordTrackingEnabled)
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

                        // Update consumed position per-message so GetPosition()/CommitAsync()
                        // reflect the latest message the app has seen, even mid-batch.
                        _positions[pending.TopicPartition] = pending.LastYieldedOffset + 1;

                        yield return nextResult;
                    }

                    // Dispose last activity from this pending fetch
                    previousActivity?.Dispose();
                    previousActivity = null;

                    // Batch-level fetch position update (once per partition-fetch, not per message).
                    // _fetchPositions controls where the next fetch request starts from.
                    // In prefetch mode, the prefetch thread already advances _fetchPositions
                    // via UpdateFetchPositionsFromPrefetch — including for faulted fetches,
                    // since _fetchPositions was set at prefetch time before the fault occurred.
                    if (pending.LastYieldedOffset >= 0 && !_prefetchEnabled)
                    {
                        _fetchPositions[pending.TopicPartition] = pending.LastYieldedOffset + 1;
                    }

                    // Dequeue and dispose the pending fetch (releases pooled network buffer memory)
                    _pendingFetches.Dequeue().Dispose();
                }
            }
            finally
            {
                // Ensure activity is disposed if caller breaks out of enumeration early
                previousActivity?.Dispose();
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

    private void StartPrefetch(CancellationToken cancellationToken)
    {
        if (_prefetchTask is not null)
            return;

        _prefetchCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
                _options.QueuedMaxMessagesKbytes,
                _assignmentSnapshot.Count,
                _options.MaxPartitionFetchBytes,
                _options.FetchMaxBytes,
                _options.PrefetchPipelineDepth),
            getPrefetchedBytes: () => Interlocked.Read(ref _prefetchedBytes),
            prefetchRecords: PrefetchRecordsAsync,
            waitForMemoryAvailable: ct => _prefetchMemoryAvailable.WaitAsync(ct),
            logError: LogPrefetchLoopError,
            logMemoryLimitPaused: LogPrefetchMemoryLimitPaused,
            channelWriter: _prefetchChannel.Writer,
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
        // Rent wakeup CTS from pool — used as the combined cancellation source
        // instead of allocating a LinkedCTS
        var wakeupCts = _ctsPool.Rent();
        _activeWakeupSources.TryAdd(wakeupCts, 0);

        try
        {
            // Forward outer cancellation and wakeup into the pooled CTS via registrations
            using var reg1 = cancellationToken.CanBeCanceled
                ? cancellationToken.Register(static s => ((CancellationTokenSource)s!).Cancel(), wakeupCts)
                : default;

            // Close any race window: if token was cancelled between method entry and registration
            if (cancellationToken.IsCancellationRequested)
                wakeupCts.Cancel();

            var partitionsByBroker = await GroupPartitionsByBrokerAsync(cancellationToken).ConfigureAwait(false);

            // If all partitions are paused, delay to prevent tight spin loop
            // that would starve timeout/cancellation mechanisms of CPU time
            var brokerCount = partitionsByBroker.Count;
            if (brokerCount == 0)
            {
                await Task.Delay(AllPartitionsPausedDelayMs, cancellationToken).ConfigureAwait(false);
                return;
            }

            // Fetch from all brokers in parallel for maximum throughput
            // Use pooled array to avoid allocation per fetch cycle
            var fetchTasks = ArrayPool<Task>.Shared.Rent(brokerCount);
            try
            {
                var i = 0;
                foreach (var (brokerId, partitions) in partitionsByBroker)
                {
                    fetchTasks[i++] = PrefetchFromBrokerWithErrorHandlingAsync(
                        brokerId, partitions, wakeupCts.Token, wakeupCts.Token);
                }

                // ReadOnlySpan overload (.NET 9+) avoids internal array copy and ArraySegment boxing
                await Task.WhenAll(new ReadOnlySpan<Task>(fetchTasks, 0, brokerCount)).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<Task>.Shared.Return(fetchTasks, clearArray: true);
            }
        }
        finally
        {
            _activeWakeupSources.TryRemove(wakeupCts, out _);
            wakeupCts.Dispose();
        }
    }

    private async Task PrefetchFromBrokerWithErrorHandlingAsync(
        int brokerId,
        List<TopicPartition> partitions,
        CancellationToken linkedToken,
        CancellationToken wakeupToken)
    {
        try
        {
            await PrefetchFromBrokerAsync(brokerId, partitions, linkedToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (wakeupToken.IsCancellationRequested)
        {
            // Wakeup requested, exit silently
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

    internal static bool IsFatalPrefetchError(Exception ex) => ex is
        Errors.AuthenticationException or
        Errors.AuthorizationException or
        KafkaException { ErrorCode: not null, IsRetriable: false };

    private async ValueTask PrefetchFromBrokerAsync(int brokerId, List<TopicPartition> partitions, CancellationToken cancellationToken)
    {
        var currentConnections = _connectionScaler?.CurrentConnectionCount ?? _options.ConnectionsPerBroker;
        var fetchConnectionCount = ConsumerConnectionScaler.GetFetchConnectionCount(currentConnections);
        var connectionIndex = ConsumerConnectionScaler.GetNextFetchConnectionIndex(ref _fetchRoundRobinCounter, fetchConnectionCount);
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

        // Resolve any special offset values
        await ResolveSpecialOffsetsAsync(partitions, cancellationToken).ConfigureAwait(false);

        // Build fetch request
        var topicData = BuildFetchRequestTopics(partitions);

        var request = FetchRequest.Rent();
        request.MaxWaitMs = _options.FetchMaxWaitMs;
        request.MinBytes = _options.FetchMinBytes;
        request.MaxBytes = _options.FetchMaxBytes;
        request.IsolationLevel = _options.IsolationLevel;
        request.Topics = topicData;

        var fetchStarted = System.Diagnostics.Stopwatch.GetTimestamp();

        FetchResponse response;
        try
        {
            response = await connection.SendAsync<FetchRequest, FetchResponse>(
                request,
                (short)apiVersion,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            request.ReturnToPool();
        }

        // Record fetch round-trip duration (~3ns no-op when no listener)
        Diagnostics.DekafMetrics.FetchDuration.Record(
            System.Diagnostics.Stopwatch.GetElapsedTime(fetchStarted).TotalSeconds,
            new System.Diagnostics.TagList { { Diagnostics.DekafDiagnostics.MessagingKafkaBrokerId, brokerId } });

        // Take ownership of pooled memory from the response (if zero-copy was used)
        var memoryOwner = response.PooledMemoryOwner;
        response.PooledMemoryOwner = null; // Clear to prevent double-dispose

        // Collect pending fetch data items - we need to assign memory owner to the last one
        // since FIFO processing means the last one will be disposed last
        // Reuse list per broker across prefetch cycles to avoid per-cycle allocation
        // Each broker gets its own list since PrefetchFromBrokerAsync runs concurrently
        var pendingItems = _prefetchPendingItemsByBroker.GetOrAdd(brokerId, static _ => []);
        pendingItems.Clear();

        // Write to prefetch channel
        try
        {
        foreach (var topicResponse in response.Responses)
        {
            var topic = topicResponse.Topic ?? string.Empty;
            var activityName = _activityNameCache.GetOrAdd(topic, static t => $"{t} receive");

            foreach (var partitionResponse in topicResponse.Partitions)
            {
                var tp = new TopicPartition(topic, partitionResponse.PartitionIndex);

                // Update watermark cache from fetch response (even on errors, watermarks may be valid)
                UpdateWatermarksFromFetchResponse(topic, partitionResponse);

                if (partitionResponse.ErrorCode != ErrorCode.None)
                {
                    if (partitionResponse.ErrorCode == ErrorCode.OffsetOutOfRange)
                    {
                        // CRITICAL: Reset fetch position based on auto.offset.reset policy
                        // Without this, we would retry with the same invalid offset forever
                        var (resetTimestamp, resetName) = _options.AutoOffsetReset switch
                        {
                            AutoOffsetReset.Latest => (-1L, "latest"),
                            AutoOffsetReset.Earliest => (-2L, "earliest"),
                            AutoOffsetReset.None => throw new KafkaException(
                                ErrorCode.OffsetOutOfRange,
                                $"OffsetOutOfRange for {topic}-{partitionResponse.PartitionIndex} and auto.offset.reset is 'none'"),
                            _ => throw new InvalidOperationException($"Unknown AutoOffsetReset value: {_options.AutoOffsetReset}")
                        };
                        _fetchPositions[tp] = resetTimestamp;
                        _positions[tp] = resetTimestamp;
                        LogOffsetOutOfRangeReset(topic, partitionResponse.PartitionIndex, resetName);
                    }
                    else if (partitionResponse.ErrorCode == ErrorCode.NotLeaderOrFollower)
                    {
                        // Invalidate metadata cache to force re-discovery of leader
                        InvalidatePartitionCache();
                        LogNotLeaderOrFollower(topic, partitionResponse.PartitionIndex);
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
                    lock (_prefetchLock)
                    {
                        _eofEmitted.Remove(tp);
                    }

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
                    lock (_prefetchLock)
                    {
                        var fetchPosition = _fetchPositions.GetValueOrDefault(tp, 0);

                        // EOF condition: position >= high watermark and we haven't emitted EOF yet
                        if (fetchPosition >= partitionResponse.HighWatermark && !_eofEmitted.Contains(tp))
                        {
                            // Queue EOF event and mark as emitted
                            _pendingEofEvents.Enqueue((tp, fetchPosition));
                            _eofEmitted.Add(tp);
                        }
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
                await _prefetchChannel.Writer.WriteAsync(pendingItems[i], cancellationToken).ConfigureAwait(false);
            }
        }

        // If no pending items were created but we have a memory owner, dispose it
        memoryOwner?.Dispose();
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

        // Thread-safe update using ConcurrentDictionary
        _fetchPositions.AddOrUpdate(
            tp,
            lastOffset + 1,
            (_, currentPos) => Math.Max(currentPos, lastOffset + 1));
    }

    /// <summary>
    /// Drains additional ready items from the prefetch channel into <see cref="_pendingFetches"/>,
    /// bounded to avoid starving <see cref="EnsureAssignmentAsync"/> (and thus rebalance detection)
    /// when many partitions produce data concurrently
    /// (e.g., N partitions produce N items per FetchResponse).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrainPrefetchChannel()
    {
        // Bound: drain at most the current assignment count to keep the loop responsive.
        // With N assigned partitions, one FetchResponse produces at most N items,
        // so this drains one full response without unbounded spinning.
        var maxDrain = _assignmentSnapshot.Count;

        for (var i = 0; i < maxDrain && _prefetchChannel.Reader.TryRead(out var additional); i++)
        {
            _pendingFetches.Enqueue(additional);
            TrackPrefetchedBytes(additional, release: true);
        }
    }

    private void TrackPrefetchedBytes(PendingFetchData pending, bool release)
    {
        // Estimate bytes from batches
        var bytes = EstimatePendingFetchBytes(pending);

        if (release)
        {
            Interlocked.Add(ref _prefetchedBytes, -bytes);
            // Signal prefetch loop that memory is now available (skip for empty responses to avoid spurious wakeups)
            if (bytes > 0)
                _prefetchMemoryAvailable.Release();
        }
        else
        {
            Interlocked.Add(ref _prefetchedBytes, bytes);
        }
    }

    private static long EstimatePendingFetchBytes(PendingFetchData pending)
    {
        var batches = pending.GetBatches();
        long bytes = 0;
        foreach (var batch in batches)
        {
            bytes += batch.BatchLength;
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
                await foreach (var result in ConsumeAsync(timeoutCts.Token).ConfigureAwait(false))
                {
                    return result;
                }
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                // Timeout expired with no messages - return null instead of throwing
            }
            catch (ChannelClosedException ex) when (ex.InnerException is null && timeoutCts.IsCancellationRequested)
            {
                // Channel closed cleanly due to timeout-triggered cancellation — no messages available
            }
            return null;
        }

        // Slow path: need to link external cancellation with timeout
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await foreach (var result in ConsumeAsync(linkedCts.Token).ConfigureAwait(false))
            {
                return result;
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            // Timeout expired — no message arrived. If the external token also fired by the time
            // this filter evaluates (race under load), the internal timeout still takes priority.
        }
        catch (ChannelClosedException ex) when (ex.InnerException is null && timeoutCts.IsCancellationRequested)
        {
            // Channel closed due to timeout — same race rationale as above.
        }

        return null;
    }

    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_coordinator is null)
            return;

        TopicPartitionOffset[]? offsetsArray = null;
        int offsetCount;

        {
            // Commit all consumed positions
            // Snapshot the concurrent dictionary to avoid race conditions during enumeration
            var positionsSnapshot = _positions.ToArray();
            offsetCount = positionsSnapshot.Length;
            if (offsetCount == 0)
                return;

            LogCommitStarted(offsetCount);

            // Rent array from pool to avoid List allocation
            offsetsArray = ArrayPool<TopicPartitionOffset>.Shared.Rent(offsetCount);
            try
            {
                int index = 0;
                foreach (var kvp in positionsSnapshot)
                {
                    offsetsArray[index++] = new TopicPartitionOffset(kvp.Key.Topic, kvp.Key.Partition, kvp.Value);
                }

                // Create array segment to pass only the used portion
                var offsets = new ArraySegment<TopicPartitionOffset>(offsetsArray, 0, offsetCount);

                await _coordinator.CommitOffsetsAsync(offsets, cancellationToken).ConfigureAwait(false);

                // Update committed offsets tracking
                foreach (var offset in offsets)
                {
                    _committed[new TopicPartition(offset.Topic, offset.Partition)] = offset.Offset;
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
            _committed[new TopicPartition(offset.Topic, offset.Partition)] = offset.Offset;
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
            if (offsets.TryGetValue(partition, out offset))
            {
                _committed[partition] = offset;
                return offset;
            }
        }

        return null;
    }

    public long? GetPosition(TopicPartition partition)
    {
        return _positions.TryGetValue(partition, out var position) ? position : null;
    }

    public IKafkaConsumer<TKey, TValue> Seek(TopicPartitionOffset offset)
    {
        LogSeek(offset.Topic, offset.Partition, offset.Offset);
        var tp = new TopicPartition(offset.Topic, offset.Partition);
        // Update positions (thread-safe with ConcurrentDictionary)
        _positions[tp] = offset.Offset;
        _fetchPositions[tp] = offset.Offset;

        // Reset EOF state for this partition so it can fire again (still needs lock)
        lock (_prefetchLock)
        {
            _eofEmitted.Remove(tp);
        }
        ClearFetchBuffer();
        return this;
    }

    public IKafkaConsumer<TKey, TValue> SeekToBeginning(params TopicPartition[] partitions)
    {
        // Update positions (thread-safe with ConcurrentDictionary)
        foreach (var partition in partitions)
        {
            _positions[partition] = 0;
            _fetchPositions[partition] = 0;
        }

        // Reset EOF state (still needs lock)
        lock (_prefetchLock)
        {
            foreach (var partition in partitions)
            {
                _eofEmitted.Remove(partition);
            }
        }
        ClearFetchBuffer();
        return this;
    }

    public IKafkaConsumer<TKey, TValue> SeekToEnd(params TopicPartition[] partitions)
    {
        // Update positions (thread-safe with ConcurrentDictionary)
        foreach (var partition in partitions)
        {
            _positions[partition] = -1; // Special value meaning end
            _fetchPositions[partition] = -1; // Special value meaning end
        }

        // Reset EOF state (still needs lock)
        lock (_prefetchLock)
        {
            foreach (var partition in partitions)
            {
                _eofEmitted.Remove(partition);
            }
        }
        ClearFetchBuffer();
        return this;
    }

    private void ClearFetchBuffer()
    {
        // Dispose and clear pending fetches to release pooled memory
        while (_pendingFetches.TryDequeue(out var pending))
        {
            pending.Dispose();
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
                pending.Dispose();
            }
        }
    }

    public IKafkaConsumer<TKey, TValue> Pause(params TopicPartition[] partitions)
    {
        foreach (var partition in partitions)
        {
            _paused.TryAdd(partition, 0);
        }
        PublishPausedSnapshot();
        InvalidatePartitionCache();
        InvalidateFetchRequestCache();
        return this;
    }

    public IKafkaConsumer<TKey, TValue> Resume(params TopicPartition[] partitions)
    {
        foreach (var partition in partitions)
        {
            _paused.TryRemove(partition, out _);
        }
        PublishPausedSnapshot();
        InvalidatePartitionCache();
        InvalidateFetchRequestCache();
        return this;
    }

    public void Wakeup() => CancelAllActiveWakeupSources();

    private void CancelAllActiveWakeupSources()
    {
        foreach (var cts in _activeWakeupSources.Keys)
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

    public async ValueTask<WatermarkOffsets> QueryWatermarkOffsetsAsync(
        TopicPartition topicPartition,
        CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _consumerDisposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaConsumer<TKey, TValue>));

        ThrowIfNotInitialized();

        var connection = await GetPartitionLeaderConnectionAsync(topicPartition, cancellationToken).ConfigureAwait(false);
        if (connection is null)
            throw new InvalidOperationException($"No leader found for partition {topicPartition}");

        var listOffsetsVersion = _metadataManager.GetNegotiatedApiVersion(
            ApiKey.ListOffsets,
            ListOffsetsRequest.LowestSupportedVersion,
            ListOffsetsRequest.HighestSupportedVersion);

        // Query for earliest offset (low watermark) with timestamp -2
        var earliestRequest = new ListOffsetsRequest
        {
            ReplicaId = -1,
            IsolationLevel = _options.IsolationLevel,
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
                            Timestamp = -2, // Earliest
                            CurrentLeaderEpoch = -1
                        }
                    ]
                }
            ]
        };

        var earliestResponse = await connection.SendAsync<ListOffsetsRequest, ListOffsetsResponse>(
            earliestRequest,
            listOffsetsVersion,
            cancellationToken).ConfigureAwait(false);

        ListOffsetsResponsePartition? earliestPartitionResponse = null;
        foreach (var topic in earliestResponse.Topics)
        {
            if (topic.Name == topicPartition.Topic)
            {
                foreach (var partition in topic.Partitions)
                {
                    if (partition.PartitionIndex == topicPartition.Partition)
                    {
                        earliestPartitionResponse = partition;
                        break;
                    }
                }
                break;
            }
        }

        if (earliestPartitionResponse?.ErrorCode != ErrorCode.None)
        {
            throw new InvalidOperationException(
                $"Failed to query earliest offset for {topicPartition}: {earliestPartitionResponse?.ErrorCode}");
        }

        var lowWatermark = earliestPartitionResponse?.Offset ?? 0;

        // Query for latest offset (high watermark) with timestamp -1
        var latestRequest = new ListOffsetsRequest
        {
            ReplicaId = -1,
            IsolationLevel = _options.IsolationLevel,
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
                            Timestamp = -1, // Latest
                            CurrentLeaderEpoch = -1
                        }
                    ]
                }
            ]
        };

        var latestResponse = await connection.SendAsync<ListOffsetsRequest, ListOffsetsResponse>(
            latestRequest,
            listOffsetsVersion,
            cancellationToken).ConfigureAwait(false);

        ListOffsetsResponsePartition? latestPartitionResponse = null;
        foreach (var topic in latestResponse.Topics)
        {
            if (topic.Name == topicPartition.Topic)
            {
                foreach (var partition in topic.Partitions)
                {
                    if (partition.PartitionIndex == topicPartition.Partition)
                    {
                        latestPartitionResponse = partition;
                        break;
                    }
                }
                break;
            }
        }

        if (latestPartitionResponse?.ErrorCode != ErrorCode.None)
        {
            throw new InvalidOperationException(
                $"Failed to query latest offset for {topicPartition}: {latestPartitionResponse?.ErrorCode}");
        }

        var highWatermark = latestPartitionResponse?.Offset ?? 0;

        var watermarks = new WatermarkOffsets(lowWatermark, highWatermark);

        // Cache the result
        _watermarks[topicPartition] = watermarks;

        return watermarks;
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
    private async ValueTask<bool> RefreshFilteredTopicsAsync(CancellationToken cancellationToken)
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
        var filter = _topicFilter!;
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

    private async ValueTask EnsureAssignmentAsync(CancellationToken cancellationToken)
    {
        // Serialize the write path: both ConsumeAsync and PrefetchLoopAsync call this method
        // concurrently. Without synchronization, concurrent access to non-thread-safe
        // _assignment HashSet causes NullReferenceException during enumeration.
        // Readers use the volatile _assignmentSnapshot instead of acquiring this lock.
        await SemaphoreHelper.AcquireOrThrowDisposedAsync(_assignmentLock, nameof(KafkaConsumer<TKey, TValue>), cancellationToken).ConfigureAwait(false);
        try
        {
            // If a pattern filter is active, refresh the subscription from metadata
            if (_topicFilter is not null)
            {
                await RefreshFilteredTopicsAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!_subscription.IsEmpty && _coordinator is not null)
            {
                await _coordinator.EnsureActiveGroupAsync(_subscriptionSnapshot, cancellationToken).ConfigureAwait(false);

                // Fast path: skip all work if assignment hasn't changed (common case after stable join)
                if (_assignment.SetEquals(_coordinator.Assignment))
                    return;

                // Check for new partitions that need initialization
                List<TopicPartition>? newPartitions = null;
                foreach (var partition in _coordinator.Assignment)
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
                    if (!_coordinator.Assignment.Contains(partition))
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
                foreach (var partition in _coordinator.Assignment)
                {
                    _assignment.Add(partition);
                }
                PublishAssignmentSnapshot();
                InvalidatePartitionCache();
                InvalidateFetchRequestCache();

                // Clean up state for removed partitions
                if (removedPartitions is not null)
                {
                    foreach (var partition in removedPartitions)
                    {
                        _highWatermarks.TryRemove(partition, out _);
                        _positions.TryRemove(partition, out _);
                        _fetchPositions.TryRemove(partition, out _);
                    }

                    // Clean up EOF state (still needs lock)
                    lock (_prefetchLock)
                    {
                        foreach (var partition in removedPartitions)
                        {
                            _eofEmitted.Remove(partition);
                        }
                    }
                }

                // Initialize positions for new partitions
                if (newPartitions is { Count: > 0 })
                {
                    await InitializePositionsAsync(newPartitions, cancellationToken).ConfigureAwait(false);
                }
            }
            else if (_assignment.Count > 0)
            {
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
        }
        finally
        {
            SemaphoreHelper.ReleaseSafely(_assignmentLock);
        }
    }


    private async ValueTask InitializeManualAssignmentPositionsAsync(List<TopicPartition> partitions, CancellationToken cancellationToken)
    {
        // For manual assignment without a group, use auto offset reset to determine starting position
        foreach (var partition in partitions)
        {
            var offset = await GetResetOffsetAsync(partition, cancellationToken).ConfigureAwait(false);
            _positions[partition] = offset;
            _fetchPositions[partition] = offset;
        }
    }

    private async ValueTask InitializePositionsAsync(List<TopicPartition> partitions, CancellationToken cancellationToken)
    {
        // Fetch committed offsets for all partitions
        var committedOffsets = await _coordinator!.FetchOffsetsAsync(partitions, cancellationToken).ConfigureAwait(false);

        foreach (var partition in partitions)
        {
            if (committedOffsets.TryGetValue(partition, out var committedOffset) && committedOffset >= 0)
            {
                // Use committed offset
                _positions[partition] = committedOffset;
                _fetchPositions[partition] = committedOffset;
                _committed[partition] = committedOffset;
            }
            else
            {
                // No committed offset, use auto offset reset
                var offset = await GetResetOffsetAsync(partition, cancellationToken).ConfigureAwait(false);
                _positions[partition] = offset;
                _fetchPositions[partition] = offset;
            }
        }
    }

    private async ValueTask<long> GetResetOffsetAsync(TopicPartition partition, CancellationToken cancellationToken)
    {
        // Get the offset based on auto offset reset policy
        var timestamp = _options.AutoOffsetReset switch
        {
            AutoOffsetReset.Earliest => -2, // Earliest
            AutoOffsetReset.Latest => -1,   // Latest
            _ => -2 // Default to earliest
        };

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
                            CurrentLeaderEpoch = -1
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

        return partitionResponse?.Offset ?? 0;
    }

    private async ValueTask ResolveSpecialOffsetsAsync(List<TopicPartition> partitions, CancellationToken cancellationToken)
    {
        // Check for partitions with special offset values (-1 for end, -2 for beginning)
        // and resolve them to actual offsets using ListOffsets
        foreach (var partition in partitions)
        {
            var fetchPosition = _fetchPositions.GetValueOrDefault(partition, 0);
            if (fetchPosition == -1 || fetchPosition == -2)
            {
                // -1 = latest, -2 = earliest
                var resolvedOffset = await ResolveOffsetAsync(partition, fetchPosition, cancellationToken).ConfigureAwait(false);
                _fetchPositions[partition] = resolvedOffset;
                _positions[partition] = resolvedOffset;
            }
        }
    }

    private async ValueTask<long> ResolveOffsetAsync(TopicPartition partition, long timestamp, CancellationToken cancellationToken)
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
                            CurrentLeaderEpoch = -1
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

        return partitionResponse?.Offset ?? 0;
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
        // Rent wakeup CTS from pool to avoid allocation
        var wakeupCts = _ctsPool.Rent();
        _activeWakeupSources.TryAdd(wakeupCts, 0);

        try
        {
            // Forward outer cancellation into the pooled wakeup CTS via registration
            // instead of allocating a LinkedCTS (matches the prefetch path pattern)
            using var reg = cancellationToken.CanBeCanceled
                ? cancellationToken.Register(static s => ((CancellationTokenSource)s!).Cancel(), wakeupCts)
                : default;

            // Close any race window: if token was cancelled between method entry and registration
            if (cancellationToken.IsCancellationRequested)
                wakeupCts.Cancel();

            var partitionsByBroker = await GroupPartitionsByBrokerAsync(cancellationToken).ConfigureAwait(false);

            // If all partitions are paused, delay to prevent tight spin loop
            // that would starve timeout/cancellation mechanisms of CPU time
            var brokerCount = partitionsByBroker.Count;
            if (brokerCount == 0)
            {
                await Task.Delay(AllPartitionsPausedDelayMs, cancellationToken).ConfigureAwait(false);
                return;
            }

            // Fetch from all brokers in parallel for maximum throughput
            // Use pooled array to avoid allocation per fetch cycle
            var fetchTasks = ArrayPool<Task<List<PendingFetchData>?>>.Shared.Rent(brokerCount);
            try
            {
                var i = 0;
                foreach (var (brokerId, partitions) in partitionsByBroker)
                {
                    fetchTasks[i++] = FetchFromBrokerWithErrorHandlingAsync(
                        brokerId, partitions, wakeupCts.Token, wakeupCts.Token);
                }

                // ReadOnlySpan overload: same zero-copy benefit as above
                await Task.WhenAll(new ReadOnlySpan<Task<List<PendingFetchData>?>>(fetchTasks, 0, brokerCount)).ConfigureAwait(false);

                // Enqueue results from all brokers (now on main thread, safe for Queue)
                for (var j = 0; j < brokerCount; j++)
                {
                    var pendingItems = fetchTasks[j].Result;
                    if (pendingItems is not null)
                    {
                        foreach (var pending in pendingItems)
                        {
                            _pendingFetches.Enqueue(pending);
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
            _activeWakeupSources.TryRemove(wakeupCts, out _);
            wakeupCts.Dispose();
        }
    }

    private async Task<List<PendingFetchData>?> FetchFromBrokerWithErrorHandlingAsync(
        int brokerId,
        List<TopicPartition> partitions,
        CancellationToken linkedToken,
        CancellationToken wakeupToken)
    {
        try
        {
            return await FetchFromBrokerAsync(brokerId, partitions, linkedToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (wakeupToken.IsCancellationRequested)
        {
            // Wakeup requested, exit silently
            return null;
        }
        catch (Exception ex)
        {
            LogFetchFromBrokerError(ex, brokerId);
            return null;
        }
    }

    /// <summary>
    /// Publishes an immutable snapshot of <see cref="_assignment"/> for lock-free reads.
    /// Must be called after every mutation to <see cref="_assignment"/>.
    /// </summary>
    private void PublishAssignmentSnapshot()
    {
        _assignmentSnapshot = new HashSet<TopicPartition>(_assignment);
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
            if (_cachedPartitionsByBroker is not null)
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

        // Build the cache outside the lock - allocate once per assignment change
        // Resolve all partition leaders in parallel for maximum throughput
        var leaderTasks = ArrayPool<Task<(TopicPartition Partition, BrokerNode? Leader)>>.Shared.Rent(assignmentCount);
        try
        {
            for (var i = 0; i < assignmentCount; i++)
            {
                leaderTasks[i] = ResolvePartitionLeaderAsync(assignmentArray[i], cancellationToken);
            }

            await Task.WhenAll(new ReadOnlySpan<Task<(TopicPartition Partition, BrokerNode? Leader)>>(leaderTasks, 0, assignmentCount)).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<TopicPartition>.Shared.Return(assignmentArray, clearArray: true);
        }

        // Build result dictionary from resolved partitions (tasks already completed)
        var result = new Dictionary<int, List<TopicPartition>>();
        for (var i = 0; i < assignmentCount; i++)
        {
            var (partition, leader) = leaderTasks[i].Result;
            if (leader is null)
                continue;

            if (!result.TryGetValue(leader.NodeId, out var list))
            {
                list = [];
                result[leader.NodeId] = list;
            }

            list.Add(partition);
        }

        // Return pooled array after extracting results
        ArrayPool<Task<(TopicPartition Partition, BrokerNode? Leader)>>.Shared.Return(leaderTasks, clearArray: true);

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
                _cachedPartitionsByBroker = result;
            }
            return _cachedPartitionsByBroker ?? result;
        }
    }

    private async Task<(TopicPartition Partition, BrokerNode? Leader)> ResolvePartitionLeaderAsync(
        TopicPartition partition,
        CancellationToken cancellationToken)
    {
        var leader = await _metadataManager.GetPartitionLeaderAsync(
            partition.Topic, partition.Partition, cancellationToken).ConfigureAwait(false);
        return (partition, leader);
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
        await ResolveSpecialOffsetsAsync(partitions, cancellationToken).ConfigureAwait(false);

        // Build fetch request - use imperative code to avoid LINQ allocations
        var topicData = BuildFetchRequestTopics(partitions);

        var request = FetchRequest.Rent();
        request.MaxWaitMs = _options.FetchMaxWaitMs;
        request.MinBytes = _options.FetchMinBytes;
        request.MaxBytes = _options.FetchMaxBytes;
        request.IsolationLevel = _options.IsolationLevel;
        request.Topics = topicData;

        var fetchStarted = System.Diagnostics.Stopwatch.GetTimestamp();

        FetchResponse response;
        try
        {
            response = await connection.SendAsync<FetchRequest, FetchResponse>(
                request,
                (short)apiVersion,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            request.ReturnToPool();
        }

        // Record fetch round-trip duration (~3ns no-op when no listener)
        Diagnostics.DekafMetrics.FetchDuration.Record(
            System.Diagnostics.Stopwatch.GetElapsedTime(fetchStarted).TotalSeconds,
            new System.Diagnostics.TagList { { Diagnostics.DekafDiagnostics.MessagingKafkaBrokerId, brokerId } });

        // Take ownership of pooled memory from the response (if zero-copy was used)
        var memoryOwner = response.PooledMemoryOwner;
        response.PooledMemoryOwner = null; // Clear to prevent double-dispose

        // Collect pending fetch data items - we need to assign memory owner to the last one
        List<PendingFetchData>? pendingItems = null;

        // Queue pending fetch data for lazy iteration - don't parse records yet!
        try
        {
        foreach (var topicResponse in response.Responses)
        {
            var topic = topicResponse.Topic ?? string.Empty;
            var activityName = _activityNameCache.GetOrAdd(topic, static t => $"{t} receive");

            foreach (var partitionResponse in topicResponse.Partitions)
            {
                var tp = new TopicPartition(topic, partitionResponse.PartitionIndex);

                // Update watermark cache from fetch response (even on errors, watermarks may be valid)
                UpdateWatermarksFromFetchResponse(topic, partitionResponse);

                if (partitionResponse.ErrorCode != ErrorCode.None)
                {
                    LogFetchError(topic, partitionResponse.PartitionIndex, partitionResponse.ErrorCode);
                    continue;
                }

                // Update high watermark from response (thread-safe with ConcurrentDictionary)
                _highWatermarks[tp] = partitionResponse.HighWatermark;

                // Cache Records reference to avoid repeated Volatile.Read from the pool guard
                var records = partitionResponse.Records;

                if (records is { Count: > 0 })
                {
                    // We have new records - reset EOF state for this partition
                    lock (_prefetchLock)
                    {
                        _eofEmitted.Remove(tp);
                    }

                    // Collect pending fetch data for lazy record iteration
                    pendingItems ??= [];
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
                    lock (_prefetchLock)
                    {
                        var fetchPosition = _fetchPositions.GetValueOrDefault(tp, 0);

                        // EOF condition: position >= high watermark and we haven't emitted EOF yet
                        if (fetchPosition >= partitionResponse.HighWatermark && !_eofEmitted.Contains(tp))
                        {
                            // Queue EOF event and mark as emitted
                            _pendingEofEvents.Enqueue((tp, fetchPosition));
                            _eofEmitted.Add(tp);
                        }
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
        var shared = new RefCountedMemoryOwner(memoryOwner, pendingItems.Count);
        for (var i = 0; i < pendingItems.Count; i++)
        {
            pendingItems[i].SetMemoryOwner(shared);
        }
    }

    /// <summary>
    /// Returns headers directly without conversion. Returns null if empty.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IReadOnlyList<Header>? GetHeaders(Header[]? recordHeaders)
    {
        if (recordHeaders is null || recordHeaders.Length == 0)
            return null;

        return recordHeaders;
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

    private List<FetchRequestTopic> BuildFetchRequestTopics(List<TopicPartition> partitions)
    {
        if (partitions.Count == 0)
            return [];

        // Take snapshots of current state under lock
        Dictionary<string, List<(FetchRequestPartition Partition, TopicPartition TopicPartition)>>? cachedDict;
        List<TopicPartition>? cachedList;

        lock (_fetchCacheLock)
        {
            cachedDict = _cachedTopicPartitions;
            cachedList = _cachedPartitionsList;
        }

        // Check if cache is valid (same partition list as before)
        if (cachedDict is not null && cachedList is not null && PartitionListsEqual(partitions, cachedList))
        {
            // Cache hit: build a fresh result list with snapshot offsets under lock.
            // Each broker task gets its own FetchRequestPartition objects so that
            // concurrent calls cannot mutate offsets visible to another task.
            // This allocates per fetch cycle (per-batch), not per-message.
            return BuildFetchResult(cachedDict, _fetchPositions);
        }

        // Cache miss: build fresh structure with TopicPartition stored alongside
        var topicPartitions = new Dictionary<string, List<(FetchRequestPartition Partition, TopicPartition TopicPartition)>>();

        foreach (var p in partitions)
        {
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
                    PartitionMaxBytes = _options.MaxPartitionFetchBytes
                },
                p // Store TopicPartition for reuse in hot path
            ));
        }

        // Build result with fresh copies so the caller owns its own FetchRequestPartition
        // instances. The cached dict stores templates; each caller gets independent copies
        // to prevent any shared-state issues with concurrent PrefetchFromBrokerAsync calls.
        var result = BuildFetchResult(topicPartitions, _fetchPositions);

        // Update cache (first writer wins to avoid overwriting fresher data)
        lock (_fetchCacheLock)
        {
            if (_cachedTopicPartitions is null)
            {
                _cachedTopicPartitions = topicPartitions;
                _cachedPartitionsList = new List<TopicPartition>(partitions);
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
    internal static List<FetchRequestTopic> BuildFetchResult(
        Dictionary<string, List<(FetchRequestPartition Partition, TopicPartition TopicPartition)>> templateDict,
        ConcurrentDictionary<TopicPartition, long> fetchPositions)
    {
        var result = new List<FetchRequestTopic>(templateDict.Count);

        foreach (var kvp in templateDict)
        {
            var cachedPartitions = kvp.Value;
            var partitionList = new List<FetchRequestPartition>(cachedPartitions.Count);

            foreach (var (template, tp) in cachedPartitions)
            {
                partitionList.Add(new FetchRequestPartition
                {
                    Partition = template.Partition,
                    FetchOffset = fetchPositions.GetValueOrDefault(tp, 0),
                    CurrentLeaderEpoch = template.CurrentLeaderEpoch,
                    LastFetchedEpoch = template.LastFetchedEpoch,
                    LogStartOffset = template.LogStartOffset,
                    PartitionMaxBytes = template.PartitionMaxBytes
                });
            }

            result.Add(new FetchRequestTopic
            {
                Topic = kvp.Key,
                Partitions = partitionList
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

    private async Task StartAutoCommitAsync(CancellationToken cancellationToken)
    {
        if (_autoCommitTask is not null)
        {
            _autoCommitCts?.Cancel();
            try
            {
                await _autoCommitTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Swallow — old task may not exit promptly after cancellation
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

        // Step 2: Stop auto-commit task
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

        // Step 3: Stop prefetch task
        // Same rationale as Step 2: a mid-flight FetchAsync network operation could hang for
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

        // Step 4: Commit pending offsets (if auto-commit enabled and we have a coordinator)
        if (_options.OffsetCommitMode == OffsetCommitMode.Auto && _coordinator is not null && !_positions.IsEmpty)
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
                await _coordinator.LeaveGroupAsync("Consumer closing gracefully", cancellationToken).ConfigureAwait(false);
                LogLeftConsumerGroup();
            }
            catch (Exception ex)
            {
                LogLeaveGroupFailed(ex);
            }
        }

        // Step 6: Wake up any blocked operations
        CancelAllActiveWakeupSources();

        // Step 7: Clear pending fetch data and dispose to release pooled memory
        while (_pendingFetches.TryDequeue(out var pending))
        {
            pending.Dispose();
        }
        while (_prefetchChannel.Reader.TryRead(out var prefetched))
        {
            prefetched.Dispose();
        }

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

        // Group partitions by broker leader for efficient batch requests
        var partitionsByBroker = new Dictionary<int, List<TopicPartitionTimestamp>>();
        foreach (var tpt in timestampsToSearch)
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
                CurrentLeaderEpoch = -1
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
                    results[tp] = -1;
                    continue;
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

        CancelAllActiveWakeupSources();
        _autoCommitCts?.Cancel();
        _prefetchCts?.Cancel();

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

        // Clear and dispose CancellationTokenSource pool
        // Note: active wakeup sources are managed by the pool and should not be disposed here
        _ctsPool.Clear();

        // Clear and dispose any pending fetch data to release pooled memory
        while (_pendingFetches.TryDequeue(out var pending))
        {
            pending.Dispose();
        }

        // Drain and dispose prefetch channel items
        while (_prefetchChannel.Reader.TryRead(out var prefetched))
        {
            prefetched.Dispose();
        }

        _assignmentLock.Dispose();
        _initLock.Dispose();
        _prefetchMemoryAvailable.Dispose();

        if (_coordinator is not null)
            await _coordinator.DisposeAsync().ConfigureAwait(false);

        await _metadataManager.DisposeAsync().ConfigureAwait(false);
        await _connectionPool.DisposeAsync().ConfigureAwait(false);

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

    [LoggerMessage(Level = LogLevel.Warning, Message = "NotLeaderOrFollower for {Topic}-{Partition}, will refresh metadata")]
    private partial void LogNotLeaderOrFollower(string topic, int partition);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Prefetch error for {Topic}-{Partition}: {Error}")]
    private partial void LogPrefetchError(string topic, int partition, ErrorCode error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Pattern subscription matched {Count} topics: {Topics}")]
    private partial void LogPatternSubscriptionMatched(int count, string topics);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to fetch from broker {BrokerId}")]
    private partial void LogFetchFromBrokerError(Exception exception, int brokerId);

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
