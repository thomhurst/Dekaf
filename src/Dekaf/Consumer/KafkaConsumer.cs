using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Dekaf.Compression;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Producer;
using Dekaf.Serialization;
using Dekaf.Statistics;
using Microsoft.Extensions.Logging;

namespace Dekaf.Consumer;
/// <summary>
/// Thread-safe pool for CancellationTokenSource instances to avoid allocations in hot paths.
/// </summary>
internal sealed class CancellationTokenSourcePool
{
    private readonly ConcurrentBag<CancellationTokenSource> _pool = new();
    private const int MaxPoolSize = 16; // Limit pool size to prevent unbounded growth
    private int _count;

    public CancellationTokenSource Rent()
    {
        if (_pool.TryTake(out var cts))
        {
            Interlocked.Decrement(ref _count);
            if (cts.TryReset())
            {
                return cts;
            }
            // Reset failed, dispose and create new
            cts.Dispose();
        }

        return new CancellationTokenSource();
    }

    public void Return(CancellationTokenSource cts)
    {
        if (cts.IsCancellationRequested)
        {
            cts.Dispose();
            return;
        }

        // Atomic check-and-increment to prevent race condition
        var currentCount = Volatile.Read(ref _count);
        while (currentCount < MaxPoolSize)
        {
            if (Interlocked.CompareExchange(ref _count, currentCount + 1, currentCount) == currentCount)
            {
                _pool.Add(cts);
                return;
            }
            currentCount = Volatile.Read(ref _count);
        }

        // Pool is full, dispose
        cts.Dispose();
    }

    public void Clear()
    {
        while (_pool.TryTake(out var cts))
        {
            cts.Dispose();
        }
        _count = 0;
    }
}



/// <summary>
/// Holds pending fetch data for lazy record iteration.
/// Records are only parsed and deserialized when accessed.
/// </summary>
internal sealed class PendingFetchData
{
    private readonly IReadOnlyList<RecordBatch> _batches;
    private int _batchIndex = -1;
    private int _recordIndex = -1;

    public string Topic { get; }
    public int PartitionIndex { get; }

    /// <summary>
    /// Cached TopicPartition to avoid per-message allocation in consume loop.
    /// </summary>
    public TopicPartition TopicPartition { get; }

    public PendingFetchData(string topic, int partitionIndex, IReadOnlyList<RecordBatch> batches)
    {
        Topic = topic;
        PartitionIndex = partitionIndex;
        TopicPartition = new TopicPartition(topic, partitionIndex);
        _batches = batches;
    }

    public RecordBatch CurrentBatch => _batches[_batchIndex];
    public Record CurrentRecord => _batches[_batchIndex].Records[_recordIndex];

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
            if (_recordIndex < _batches[_batchIndex].Records.Count)
                return true;
            // Empty batch, try next
            _batchIndex++;
            _recordIndex = 0;
        }
        return false;
    }
}

/// <summary>
/// Kafka consumer implementation.
/// NOT thread-safe - all methods must be called from a single thread.
/// For parallel consumption, use multiple consumers in a consumer group.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public sealed class KafkaConsumer<TKey, TValue> : IKafkaConsumer<TKey, TValue>
{
    private readonly ConsumerOptions _options;
    private readonly IDeserializer<TKey> _keyDeserializer;
    private readonly IDeserializer<TValue> _valueDeserializer;
    private readonly IConnectionPool _connectionPool;
    private readonly MetadataManager _metadataManager;
    private readonly ConsumerCoordinator? _coordinator;
    private readonly CompressionCodecRegistry _compressionCodecs;
    private readonly ILogger<KafkaConsumer<TKey, TValue>>? _logger;

    private readonly HashSet<string> _subscription = [];
    private readonly HashSet<TopicPartition> _assignment = [];
    private readonly HashSet<TopicPartition> _paused = [];

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
    private readonly Dictionary<TopicPartition, long> _committed = [];
    private readonly ConcurrentDictionary<TopicPartition, WatermarkOffsets> _watermarks = new(); // Cached watermark offsets from fetch responses

    // Stored offsets for manual offset storage (when EnableAutoOffsetStore = false)
    // Uses ConcurrentDictionary for thread-safety as StoreOffset may be called from different threads
    private readonly ConcurrentDictionary<TopicPartition, long> _storedOffsets = new();

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
    private readonly object _prefetchLock = new();

    private CancellationTokenSource? _wakeupCts;
    private CancellationTokenSource? _autoCommitCts;
    private Task? _autoCommitTask;
    private int _fetchApiVersion = -1;
    private volatile bool _disposed;
    private volatile bool _closed;

    // CancellationTokenSource pool to avoid allocations in hot paths
    private readonly CancellationTokenSourcePool _ctsPool = new();

    // Statistics collection
    private readonly ConsumerStatisticsCollector _statisticsCollector = new();
    private readonly StatisticsEmitter<ConsumerStatistics>? _statisticsEmitter;

    // Cached partition grouping by broker to avoid allocations on every fetch
    // Invalidated whenever _assignment or _paused changes
    // Access to _cachedPartitionsByBroker must be synchronized via _partitionCacheLock
    private Dictionary<int, List<TopicPartition>>? _cachedPartitionsByBroker;
    private readonly object _partitionCacheLock = new();

    // Fetch request cache - reduces allocations when partition assignment is stable
    // Cache is invalidated when assignment or paused partitions change
    private readonly object _fetchCacheLock = new();
    private Dictionary<string, List<FetchRequestPartition>>? _cachedTopicPartitions;
    private List<TopicPartition>? _cachedPartitionsList;
    // Quick lookup cache to avoid creating TopicPartition structs in hot path
    private Dictionary<(string Topic, int Partition), TopicPartition>? _topicPartitionLookup;

    public KafkaConsumer(
        ConsumerOptions options,
        IDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer,
        ILoggerFactory? loggerFactory = null)
    {
        _options = options;
        _keyDeserializer = keyDeserializer;
        _valueDeserializer = valueDeserializer;
        _logger = loggerFactory?.CreateLogger<KafkaConsumer<TKey, TValue>>();

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
            loggerFactory);

        _metadataManager = new MetadataManager(
            _connectionPool,
            options.BootstrapServers,
            logger: loggerFactory?.CreateLogger<MetadataManager>());

        if (!string.IsNullOrEmpty(options.GroupId))
        {
            _coordinator = new ConsumerCoordinator(
                options,
                _connectionPool,
                _metadataManager,
                loggerFactory?.CreateLogger<ConsumerCoordinator>());
        }

        _compressionCodecs = new CompressionCodecRegistry();

        // Initialize prefetch channel - bounded by QueuedMinMessages batches
        var prefetchCapacity = Math.Max(options.QueuedMinMessages, 1);
        _prefetchChannel = Channel.CreateBounded<PendingFetchData>(new BoundedChannelOptions(prefetchCapacity * 10)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        // Start statistics emitter if configured
        if (options.StatisticsInterval.HasValue &&
            options.StatisticsInterval.Value > TimeSpan.Zero &&
            options.StatisticsHandler is not null)
        {
            _statisticsEmitter = new StatisticsEmitter<ConsumerStatistics>(
                options.StatisticsInterval.Value,
                CollectStatistics,
                options.StatisticsHandler);
        }
    }

    public IReadOnlySet<string> Subscription => _subscription;
    public IReadOnlySet<TopicPartition> Assignment => _assignment;
    public string? MemberId => _coordinator?.MemberId;
    public IReadOnlySet<TopicPartition> Paused => _paused;

    private ConsumerStatistics CollectStatistics()
    {
        var (messagesConsumed, bytesConsumed, rebalanceCount,
            fetchRequestsSent, fetchResponsesReceived, avgFetchLatencyMs) = _statisticsCollector.GetGlobalStats();

        // Calculate total lag
        long? totalLag = null;
        var topicStats = _statisticsCollector.GetTopicStatistics(GetPartitionInfo);
        foreach (var topicStat in topicStats.Values)
        {
            foreach (var partitionStat in topicStat.Partitions.Values)
            {
                if (partitionStat.HighWatermark.HasValue && partitionStat.Position.HasValue)
                {
                    var lag = partitionStat.HighWatermark.Value - partitionStat.Position.Value;
                    totalLag = (totalLag ?? 0) + Math.Max(0, lag);
                }
            }
        }

        return new ConsumerStatistics
        {
            Timestamp = DateTimeOffset.UtcNow,
            MessagesConsumed = messagesConsumed,
            BytesConsumed = bytesConsumed,
            RebalanceCount = rebalanceCount,
            AssignedPartitions = _assignment.Count,
            PausedPartitions = _paused.Count,
            TotalLag = totalLag,
            PrefetchedMessages = _pendingFetches.Count,
            PrefetchedBytes = Interlocked.Read(ref _prefetchedBytes),
            FetchRequestsSent = fetchRequestsSent,
            FetchResponsesReceived = fetchResponsesReceived,
            AvgFetchLatencyMs = avgFetchLatencyMs,
            GroupId = _options.GroupId,
            MemberId = _coordinator?.MemberId,
            GenerationId = _coordinator?.GenerationId,
            IsLeader = _coordinator?.IsLeader,
            Topics = topicStats
        };
    }

    private (long? Position, long? CommittedOffset, bool IsPaused) GetPartitionInfo((string Topic, int Partition) key)
    {
        var tp = new TopicPartition(key.Topic, key.Partition);
        return (
            _positions.TryGetValue(tp, out var pos) && pos >= 0 ? pos : null,
            _committed.GetValueOrDefault(tp, -1) >= 0 ? _committed[tp] : null,
            _paused.Contains(tp)
        );
    }

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
        _subscription.Clear();
        foreach (var topic in topics)
        {
            _subscription.Add(topic);
        }
        _assignment.Clear();
        InvalidateFetchRequestCache();
        return this;
    }

    public IKafkaConsumer<TKey, TValue> Subscribe(Func<string, bool> topicFilter)
    {
        // TODO: Implement pattern subscription
        throw new NotImplementedException("Pattern subscription not yet implemented");
    }

    public IKafkaConsumer<TKey, TValue> Unsubscribe()
    {
        _subscription.Clear();
        _assignment.Clear();
        InvalidatePartitionCache();
        InvalidateFetchRequestCache();
        return this;
    }

    public IKafkaConsumer<TKey, TValue> Assign(params TopicPartition[] partitions)
    {
        _subscription.Clear();
        _assignment.Clear();
        foreach (var partition in partitions)
        {
            _assignment.Add(partition);
        }
        InvalidatePartitionCache();
        InvalidateFetchRequestCache();
        return this;
    }

    public IKafkaConsumer<TKey, TValue> Unassign()
    {
        _assignment.Clear();
        InvalidatePartitionCache();
        InvalidateFetchRequestCache();
        return this;
    }

    public IKafkaConsumer<TKey, TValue> IncrementalAssign(IEnumerable<TopicPartitionOffset> partitions)
    {
        // Clear subscription since we're doing manual assignment
        _subscription.Clear();

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

        InvalidatePartitionCache();
        InvalidateFetchRequestCache();
        return this;
    }

    public IKafkaConsumer<TKey, TValue> IncrementalUnassign(IEnumerable<TopicPartition> partitions)
    {
        foreach (var partition in partitions)
        {
            _assignment.Remove(partition);
            _paused.Remove(partition);
            _positions.TryRemove(partition, out _);
            _fetchPositions.TryRemove(partition, out _);
            _committed.Remove(partition);
        }

        // Clear any pending fetch data for the removed partitions
        ClearFetchBufferForPartitions(partitions);

        InvalidatePartitionCache();
        InvalidateFetchRequestCache();
        return this;
    }

    public async IAsyncEnumerable<ConsumeResult<TKey, TValue>> ConsumeAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(KafkaConsumer<TKey, TValue>));

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        // Start auto-commit if enabled
        if (_options.EnableAutoCommit && _coordinator is not null)
        {
            StartAutoCommit();
        }

        // Start background prefetch if enabled (QueuedMinMessages > 1)
        var prefetchEnabled = _options.QueuedMinMessages > 1;
        if (prefetchEnabled)
        {
            StartPrefetch(cancellationToken);
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            await EnsureAssignmentAsync(cancellationToken).ConfigureAwait(false);

            if (_assignment.Count == 0)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                continue;
            }

            // Get pending data - either from prefetch channel or direct fetch
            if (_pendingFetches.Count == 0)
            {
                if (prefetchEnabled)
                {
                    // Try to read from prefetch channel
                    if (_prefetchChannel.Reader.TryRead(out var prefetched))
                    {
                        _pendingFetches.Enqueue(prefetched);
                        TrackPrefetchedBytes(prefetched, release: true);
                    }
                    else
                    {
                        // Wait for prefetch with timeout, then try direct fetch
                        // Rent CTS from pool to avoid allocation
                        var timeoutCts = _ctsPool.Rent();
                        try
                        {
                            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(_options.FetchMaxWaitMs));
                            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                            try
                            {
                                var fetched = await _prefetchChannel.Reader.ReadAsync(linkedCts.Token).ConfigureAwait(false);
                                _pendingFetches.Enqueue(fetched);
                                TrackPrefetchedBytes(fetched, release: true);
                            }
                            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                            {
                                // Prefetch not ready, continue loop
                                continue;
                            }
                        }
                        finally
                        {
                            _ctsPool.Return(timeoutCts);
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
            while (_pendingFetches.Count > 0)
            {
                var pending = _pendingFetches.Peek();

                while (pending.MoveNext())
                {
                    var record = pending.CurrentRecord;
                    var batch = pending.CurrentBatch;

                    var offset = batch.BaseOffset + record.OffsetDelta;
                    var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(
                        batch.BaseTimestamp + record.TimestampDelta);

                    var headers = GetHeaders(record.Headers);
                    var timestampType = ((int)batch.Attributes & 0x08) != 0
                        ? TimestampType.LogAppendTime
                        : TimestampType.CreateTime;

                    // Create result - deserialization happens eagerly in the constructor
                    var result = new ConsumeResult<TKey, TValue>(
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

                    // Update positions (thread-safe with ConcurrentDictionary)
                    // Use cached TopicPartition to avoid per-message allocation
                    var nextOffset = offset + 1;
                    _positions[pending.TopicPartition] = nextOffset;
                    _fetchPositions[pending.TopicPartition] = nextOffset;

                    // Track message consumed
                    var messageBytes = (record.IsKeyNull ? 0 : record.Key.Length) +
                                       (record.IsValueNull ? 0 : record.Value.Length);
                    _statisticsCollector.RecordMessageConsumed(pending.Topic, pending.PartitionIndex, messageBytes);

                    yield return result;
                }

                // This pending fetch is exhausted, remove it
                _pendingFetches.Dequeue();
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

    private async Task PrefetchLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await EnsureAssignmentAsync(cancellationToken).ConfigureAwait(false);

                if (_assignment.Count == 0)
                {
                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                // Check memory limit
                var maxBytes = (long)_options.QueuedMaxMessagesKbytes * 1024;
                if (Interlocked.Read(ref _prefetchedBytes) >= maxBytes)
                {
                    // Wait for consumer to catch up
                    await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                // Fetch records into prefetch channel
                await PrefetchRecordsAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error in prefetch loop");
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask PrefetchRecordsAsync(CancellationToken cancellationToken)
    {
        // Rent wakeup CTS from pool to avoid allocation
        var wakeupCts = _ctsPool.Rent();
        _wakeupCts = wakeupCts;

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, wakeupCts.Token);

            var partitionsByBroker = await GroupPartitionsByBrokerAsync(cancellationToken).ConfigureAwait(false);

            foreach (var (brokerId, partitions) in partitionsByBroker)
            {
                try
                {
                    await PrefetchFromBrokerAsync(brokerId, partitions, linkedCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (wakeupCts.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to prefetch from broker {BrokerId}", brokerId);
                }
            }
        }
        finally
        {
            _wakeupCts = null;
            _ctsPool.Return(wakeupCts);
        }
    }

    private async ValueTask PrefetchFromBrokerAsync(int brokerId, List<TopicPartition> partitions, CancellationToken cancellationToken)
    {
        var connection = await _connectionPool.GetConnectionAsync(brokerId, cancellationToken).ConfigureAwait(false);

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

        var request = new FetchRequest
        {
            MaxWaitMs = _options.FetchMaxWaitMs,
            MinBytes = _options.FetchMinBytes,
            MaxBytes = _options.FetchMaxBytes,
            IsolationLevel = _options.IsolationLevel,
            Topics = topicData
        };

        // Track fetch request sent
        _statisticsCollector.RecordFetchRequestSent();
        var requestStartTime = DateTimeOffset.UtcNow;

        var response = await connection.SendAsync<FetchRequest, FetchResponse>(
            request,
            (short)apiVersion,
            cancellationToken).ConfigureAwait(false);

        // Track fetch response received with latency
        var latencyMs = (long)(DateTimeOffset.UtcNow - requestStartTime).TotalMilliseconds;
        _statisticsCollector.RecordFetchResponseReceived(latencyMs);

        // Write to prefetch channel
        foreach (var topicResponse in response.Responses)
        {
            var topic = topicResponse.Topic ?? string.Empty;

            foreach (var partitionResponse in topicResponse.Partitions)
            {
                var tp = new TopicPartition(topic, partitionResponse.PartitionIndex);

                // Update watermark cache from fetch response (even on errors, watermarks may be valid)
                UpdateWatermarksFromFetchResponse(topic, partitionResponse);

                if (partitionResponse.ErrorCode != ErrorCode.None)
                {
                    _logger?.LogWarning(
                        "Prefetch error for {Topic}-{Partition}: {Error}",
                        topic, partitionResponse.PartitionIndex, partitionResponse.ErrorCode);
                    continue;
                }

                // Update high watermark from response (thread-safe with ConcurrentDictionary)
                _highWatermarks[tp] = partitionResponse.HighWatermark;

                // Update high watermark for statistics
                if (partitionResponse.HighWatermark >= 0)
                {
                    _statisticsCollector.UpdatePartitionHighWatermark(
                        topic,
                        partitionResponse.PartitionIndex,
                        partitionResponse.HighWatermark);
                }

                var hasRecords = partitionResponse.Records is not null && partitionResponse.Records.Count > 0;

                if (hasRecords)
                {
                    // We have new records - reset EOF state for this partition
                    lock (_prefetchLock)
                    {
                        _eofEmitted.Remove(tp);
                    }

                    var pending = new PendingFetchData(
                        topic,
                        partitionResponse.PartitionIndex,
                        partitionResponse.Records!);

                    // Track memory before adding to channel
                    TrackPrefetchedBytes(pending, release: false);

                    // Update fetch positions for next prefetch
                    UpdateFetchPositionsFromPrefetch(pending);

                    await _prefetchChannel.Writer.WriteAsync(pending, cancellationToken).ConfigureAwait(false);
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

    private void UpdateFetchPositionsFromPrefetch(PendingFetchData pending)
    {
        // Calculate the last offset in the prefetched data
        // We need to update fetch positions so next prefetch gets new data
        var batches = pending.GetBatches();
        if (batches.Count == 0) return;

        var lastBatch = batches[^1];
        var lastOffset = lastBatch.BaseOffset + lastBatch.LastOffsetDelta;
        var tp = new TopicPartition(pending.Topic, pending.PartitionIndex);

        // Thread-safe update using ConcurrentDictionary
        _fetchPositions.AddOrUpdate(
            tp,
            lastOffset + 1,
            (_, currentPos) => Math.Max(currentPos, lastOffset + 1));
    }

    private void TrackPrefetchedBytes(PendingFetchData pending, bool release)
    {
        // Estimate bytes from batches
        var bytes = EstimatePendingFetchBytes(pending);

        if (release)
        {
            Interlocked.Add(ref _prefetchedBytes, -bytes);
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
        // Rent CTS from pool to avoid allocation
        var timeoutCts = _ctsPool.Rent();
        try
        {
            timeoutCts.CancelAfter(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await foreach (var result in ConsumeAsync(linkedCts.Token).ConfigureAwait(false))
            {
                return result;
            }

            return null;
        }
        finally
        {
            _ctsPool.Return(timeoutCts);
        }
    }

    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_coordinator is null)
            return;

        TopicPartitionOffset[]? offsetsArray = null;
        int offsetCount;

        if (_options.EnableAutoOffsetStore)
        {
            // Auto-store mode: commit all consumed positions
            // Snapshot the concurrent dictionary to avoid race conditions during enumeration
            var positionsSnapshot = _positions.ToArray();
            offsetCount = positionsSnapshot.Length;
            if (offsetCount == 0)
                return;

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
            }
            finally
            {
                ArrayPool<TopicPartitionOffset>.Shared.Return(offsetsArray);
            }
        }
        else
        {
            // Manual store mode: commit only explicitly stored offsets
            // Take a snapshot to avoid race condition where offsets stored by another thread
            // between enumeration and removal would be lost
            var snapshot = _storedOffsets.ToArray();
            offsetCount = snapshot.Length;

            if (offsetCount == 0)
                return;

            // Rent array from pool to avoid List allocation
            offsetsArray = ArrayPool<TopicPartitionOffset>.Shared.Rent(offsetCount);
            try
            {
                for (int i = 0; i < offsetCount; i++)
                {
                    var kvp = snapshot[i];
                    offsetsArray[i] = new TopicPartitionOffset(kvp.Key.Topic, kvp.Key.Partition, kvp.Value);
                }

                // Create array segment to pass only the used portion
                var offsets = new ArraySegment<TopicPartitionOffset>(offsetsArray, 0, offsetCount);

                await _coordinator.CommitOffsetsAsync(offsets, cancellationToken).ConfigureAwait(false);

                // Update committed offsets tracking
                foreach (var offset in offsets)
                {
                    _committed[new TopicPartition(offset.Topic, offset.Partition)] = offset.Offset;
                }

                // Remove only the offsets we committed from the snapshot, preserving any
                // new offsets that were stored by other threads during the commit
                foreach (var kvp in snapshot)
                {
                    _storedOffsets.TryRemove(kvp);
                }
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

        await _coordinator.CommitOffsetsAsync(offsets, cancellationToken).ConfigureAwait(false);

        foreach (var offset in offsets)
        {
            _committed[new TopicPartition(offset.Topic, offset.Partition)] = offset.Offset;
        }
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

    /// <summary>
    /// Stores an offset for later commit. Does not commit immediately.
    /// Use with EnableAutoOffsetStore = false for manual control.
    /// </summary>
    public IKafkaConsumer<TKey, TValue> StoreOffset(TopicPartitionOffset offset)
    {
        var tp = new TopicPartition(offset.Topic, offset.Partition);
        _storedOffsets[tp] = offset.Offset;
        return this;
    }

    /// <summary>
    /// Stores the offset from a consume result for later commit.
    /// The stored offset is the next offset to consume (result.Offset + 1).
    /// </summary>
    public IKafkaConsumer<TKey, TValue> StoreOffset(ConsumeResult<TKey, TValue> result)
    {
        var tp = new TopicPartition(result.Topic, result.Partition);
        // Store the next offset to consume (current offset + 1)
        _storedOffsets[tp] = result.Offset + 1;
        return this;
    }

    private void ClearFetchBuffer()
    {
        // Clear pending fetches to discard stale records
        _pendingFetches.Clear();
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
            if (!removeSet.Contains(new TopicPartition(pending.Topic, pending.PartitionIndex)))
            {
                // Keep this item by re-enqueueing it
                _pendingFetches.Enqueue(pending);
            }
            // Items in removeSet are simply dropped
        }
    }

    public IKafkaConsumer<TKey, TValue> Pause(params TopicPartition[] partitions)
    {
        foreach (var partition in partitions)
        {
            _paused.Add(partition);
        }
        InvalidatePartitionCache();
        InvalidateFetchRequestCache();
        return this;
    }

    public IKafkaConsumer<TKey, TValue> Resume(params TopicPartition[] partitions)
    {
        foreach (var partition in partitions)
        {
            _paused.Remove(partition);
        }
        InvalidatePartitionCache();
        InvalidateFetchRequestCache();
        return this;
    }

    public void Wakeup()
    {
        _wakeupCts?.Cancel();
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
        if (_disposed)
            throw new ObjectDisposedException(nameof(KafkaConsumer<TKey, TValue>));

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var connection = await GetPartitionLeaderConnectionAsync(topicPartition, cancellationToken).ConfigureAwait(false);
        if (connection is null)
            throw new InvalidOperationException($"No leader found for partition {topicPartition}");

        var listOffsetsVersion = _metadataManager.GetNegotiatedApiVersion(
            ApiKey.ListOffsets,
            Protocol.Messages.ListOffsetsRequest.LowestSupportedVersion,
            Protocol.Messages.ListOffsetsRequest.HighestSupportedVersion);

        // Query for earliest offset (low watermark) with timestamp -2
        var earliestRequest = new Protocol.Messages.ListOffsetsRequest
        {
            ReplicaId = -1,
            IsolationLevel = _options.IsolationLevel,
            Topics =
            [
                new Protocol.Messages.ListOffsetsRequestTopic
                {
                    Name = topicPartition.Topic,
                    Partitions =
                    [
                        new Protocol.Messages.ListOffsetsRequestPartition
                        {
                            PartitionIndex = topicPartition.Partition,
                            Timestamp = -2, // Earliest
                            CurrentLeaderEpoch = -1
                        }
                    ]
                }
            ]
        };

        var earliestResponse = await connection.SendAsync<Protocol.Messages.ListOffsetsRequest, Protocol.Messages.ListOffsetsResponse>(
            earliestRequest,
            listOffsetsVersion,
            cancellationToken).ConfigureAwait(false);

        Protocol.Messages.ListOffsetsResponsePartition? earliestPartitionResponse = null;
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
        var latestRequest = new Protocol.Messages.ListOffsetsRequest
        {
            ReplicaId = -1,
            IsolationLevel = _options.IsolationLevel,
            Topics =
            [
                new Protocol.Messages.ListOffsetsRequestTopic
                {
                    Name = topicPartition.Topic,
                    Partitions =
                    [
                        new Protocol.Messages.ListOffsetsRequestPartition
                        {
                            PartitionIndex = topicPartition.Partition,
                            Timestamp = -1, // Latest
                            CurrentLeaderEpoch = -1
                        }
                    ]
                }
            ]
        };

        var latestResponse = await connection.SendAsync<Protocol.Messages.ListOffsetsRequest, Protocol.Messages.ListOffsetsResponse>(
            latestRequest,
            listOffsetsVersion,
            cancellationToken).ConfigureAwait(false);

        Protocol.Messages.ListOffsetsResponsePartition? latestPartitionResponse = null;
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

    private async ValueTask EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_metadataManager.Metadata.LastRefreshed == default)
        {
            await _metadataManager.InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask EnsureAssignmentAsync(CancellationToken cancellationToken)
    {
        if (_subscription.Count > 0 && _coordinator is not null)
        {
            await _coordinator.EnsureActiveGroupAsync(_subscription, cancellationToken).ConfigureAwait(false);

            // Check for new partitions that need initialization
            var newPartitions = new List<TopicPartition>();
            foreach (var partition in _coordinator.Assignment)
            {
                if (!_assignment.Contains(partition))
                {
                    newPartitions.Add(partition);
                }
            }

// Check for partitions that were removed (for EOF state cleanup)
            var removedPartitions = new List<TopicPartition>();
            foreach (var partition in _assignment)
            {
                if (!_coordinator.Assignment.Contains(partition))
                {
                    removedPartitions.Add(partition);
                }
            }

            // Track rebalance if assignment changed
            var assignmentChanged = _assignment.Count != _coordinator.Assignment.Count ||
                                    newPartitions.Count > 0;

            // Update assignment from coordinator
            _assignment.Clear();
            foreach (var partition in _coordinator.Assignment)
            {
                _assignment.Add(partition);
            }
            InvalidatePartitionCache();
            InvalidateFetchRequestCache();

            // Clean up state for removed partitions
            foreach (var partition in removedPartitions)
            {
                _highWatermarks.TryRemove(partition, out _);
            }

            // Clean up EOF state (still needs lock)
            lock (_prefetchLock)
            {
                foreach (var partition in removedPartitions)
                {
                    _eofEmitted.Remove(partition);
                }
            }

            // Track rebalance
            if (assignmentChanged)
            {
                _statisticsCollector.RecordRebalance();
            }

            // Initialize positions for new partitions
            if (newPartitions.Count > 0)
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
            Protocol.Messages.ListOffsetsRequest.LowestSupportedVersion,
            Protocol.Messages.ListOffsetsRequest.HighestSupportedVersion);

        var request = new Protocol.Messages.ListOffsetsRequest
        {
            ReplicaId = -1,
            IsolationLevel = _options.IsolationLevel,
            Topics =
            [
                new Protocol.Messages.ListOffsetsRequestTopic
                {
                    Name = partition.Topic,
                    Partitions =
                    [
                        new Protocol.Messages.ListOffsetsRequestPartition
                        {
                            PartitionIndex = partition.Partition,
                            Timestamp = timestamp,
                            CurrentLeaderEpoch = -1
                        }
                    ]
                }
            ]
        };

        var response = await connection.SendAsync<Protocol.Messages.ListOffsetsRequest, Protocol.Messages.ListOffsetsResponse>(
            request,
            listOffsetsVersion,
            cancellationToken).ConfigureAwait(false);

        Protocol.Messages.ListOffsetsResponsePartition? partitionResponse = null;
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
            Protocol.Messages.ListOffsetsRequest.LowestSupportedVersion,
            Protocol.Messages.ListOffsetsRequest.HighestSupportedVersion);

        var request = new Protocol.Messages.ListOffsetsRequest
        {
            ReplicaId = -1,
            IsolationLevel = _options.IsolationLevel,
            Topics =
            [
                new Protocol.Messages.ListOffsetsRequestTopic
                {
                    Name = partition.Topic,
                    Partitions =
                    [
                        new Protocol.Messages.ListOffsetsRequestPartition
                        {
                            PartitionIndex = partition.Partition,
                            Timestamp = timestamp,
                            CurrentLeaderEpoch = -1
                        }
                    ]
                }
            ]
        };

        var response = await connection.SendAsync<Protocol.Messages.ListOffsetsRequest, Protocol.Messages.ListOffsetsResponse>(
            request,
            listOffsetsVersion,
            cancellationToken).ConfigureAwait(false);

        Protocol.Messages.ListOffsetsResponsePartition? partitionResponse = null;
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

        return await _connectionPool.GetConnectionAsync(leader.NodeId, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask FetchRecordsAsync(CancellationToken cancellationToken)
    {
        // Rent wakeup CTS from pool to avoid allocation
        var wakeupCts = _ctsPool.Rent();
        _wakeupCts = wakeupCts;

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, wakeupCts.Token);

            var partitionsByBroker = await GroupPartitionsByBrokerAsync(cancellationToken).ConfigureAwait(false);

            foreach (var (brokerId, partitions) in partitionsByBroker)
            {
                try
                {
                    await FetchFromBrokerAsync(brokerId, partitions, linkedCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (wakeupCts.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to fetch from broker {BrokerId}", brokerId);
                }
            }
        }
        finally
        {
            _wakeupCts = null;
            _ctsPool.Return(wakeupCts);
        }
    }

    /// <summary>
    /// Invalidates the cached partition grouping. Called whenever _assignment or _paused changes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InvalidatePartitionCache()
    {
        lock (_partitionCacheLock)
        {
            _cachedPartitionsByBroker = null;
        }
    }

    private async ValueTask<Dictionary<int, List<TopicPartition>>> GroupPartitionsByBrokerAsync(CancellationToken cancellationToken)
    {
        // Check cache and take snapshot of assignment/paused while holding lock
        HashSet<TopicPartition> assignmentSnapshot;
        HashSet<TopicPartition> pausedSnapshot;

        lock (_partitionCacheLock)
        {
            if (_cachedPartitionsByBroker is not null)
            {
                return _cachedPartitionsByBroker;
            }

            // Take snapshot of assignment and paused sets to avoid race conditions
            // during iteration (these are regular HashSets, not thread-safe)
            assignmentSnapshot = new HashSet<TopicPartition>(_assignment);
            pausedSnapshot = new HashSet<TopicPartition>(_paused);
        }

        // Build the cache outside the lock - allocate once per assignment change
        var result = new Dictionary<int, List<TopicPartition>>();

        foreach (var partition in assignmentSnapshot)
        {
            if (pausedSnapshot.Contains(partition))
                continue;

            var leader = await _metadataManager.GetPartitionLeaderAsync(partition.Topic, partition.Partition, cancellationToken)
                .ConfigureAwait(false);

            if (leader is null)
                continue;

            if (!result.TryGetValue(leader.NodeId, out var list))
            {
                list = [];
                result[leader.NodeId] = list;
            }

            list.Add(partition);
        }

        // Cache the result - will be reused until assignment/paused changes (synchronized write)
        // Use double-checked locking: another thread may have populated cache while we were building.
        // First writer wins (has fresher metadata from earlier point in time).
        lock (_partitionCacheLock)
        {
            if (_cachedPartitionsByBroker is null)
            {
                _cachedPartitionsByBroker = result;
            }
            return _cachedPartitionsByBroker;
        }
    }

    private async ValueTask FetchFromBrokerAsync(int brokerId, List<TopicPartition> partitions, CancellationToken cancellationToken)
    {
        var connection = await _connectionPool.GetConnectionAsync(brokerId, cancellationToken).ConfigureAwait(false);

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

        var request = new FetchRequest
        {
            MaxWaitMs = _options.FetchMaxWaitMs,
            MinBytes = _options.FetchMinBytes,
            MaxBytes = _options.FetchMaxBytes,
            IsolationLevel = _options.IsolationLevel,
            Topics = topicData
        };

        // Track fetch request sent
        _statisticsCollector.RecordFetchRequestSent();
        var requestStartTime = DateTimeOffset.UtcNow;

        var response = await connection.SendAsync<FetchRequest, FetchResponse>(
            request,
            (short)apiVersion,
            cancellationToken).ConfigureAwait(false);

        // Track fetch response received with latency
        var latencyMs = (long)(DateTimeOffset.UtcNow - requestStartTime).TotalMilliseconds;
        _statisticsCollector.RecordFetchResponseReceived(latencyMs);

        // Queue pending fetch data for lazy iteration - don't parse records yet!
        foreach (var topicResponse in response.Responses)
        {
            var topic = topicResponse.Topic ?? string.Empty;

            foreach (var partitionResponse in topicResponse.Partitions)
            {
                var tp = new TopicPartition(topic, partitionResponse.PartitionIndex);

                // Update watermark cache from fetch response (even on errors, watermarks may be valid)
                UpdateWatermarksFromFetchResponse(topic, partitionResponse);

                if (partitionResponse.ErrorCode != ErrorCode.None)
                {
                    _logger?.LogWarning(
                        "Fetch error for {Topic}-{Partition}: {Error}",
                        topic, partitionResponse.PartitionIndex, partitionResponse.ErrorCode);
                    continue;
                }

                // Update high watermark from response (thread-safe with ConcurrentDictionary)
                _highWatermarks[tp] = partitionResponse.HighWatermark;

                // Update high watermark for statistics
                if (partitionResponse.HighWatermark >= 0)
                {
                    _statisticsCollector.UpdatePartitionHighWatermark(
                        topic,
                        partitionResponse.PartitionIndex,
                        partitionResponse.HighWatermark);
                }

                var hasRecords = partitionResponse.Records is not null && partitionResponse.Records.Count > 0;

                if (hasRecords)
                {
                    // We have new records - reset EOF state for this partition
                    lock (_prefetchLock)
                    {
                        _eofEmitted.Remove(tp);
                    }

                    // Queue the pending fetch data for lazy record iteration
                    _pendingFetches.Enqueue(new PendingFetchData(
                        topic,
                        partitionResponse.PartitionIndex,
                        partitionResponse.Records!));
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

    /// <summary>
    /// Returns headers directly without conversion. Returns null if empty.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IReadOnlyList<RecordHeader>? GetHeaders(IReadOnlyList<RecordHeader>? recordHeaders)
    {
        // Return null for empty to avoid exposing empty lists
        if (recordHeaders is null || recordHeaders.Count == 0)
            return null;

        // Return directly - no conversion needed, zero allocation
        return recordHeaders;
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
        Dictionary<string, List<FetchRequestPartition>>? cachedDict;
        List<TopicPartition>? cachedList;
        Dictionary<(string Topic, int Partition), TopicPartition>? tpLookup;

        lock (_fetchCacheLock)
        {
            cachedDict = _cachedTopicPartitions;
            cachedList = _cachedPartitionsList;
            tpLookup = _topicPartitionLookup;
        }

        // Check if cache is valid (same partition list as before)
        if (cachedDict is not null && cachedList is not null && tpLookup is not null && PartitionListsEqual(partitions, cachedList))
        {
            // Cache hit: clone and update fetch offsets
            // Must clone because FetchOffset changes between calls
            return CloneCacheWithUpdatedOffsets(cachedDict, tpLookup);
        }

        // Cache miss: build fresh structure
        var topicPartitions = new Dictionary<string, List<FetchRequestPartition>>();

        foreach (var p in partitions)
        {
            if (!topicPartitions.TryGetValue(p.Topic, out var list))
            {
                list = new List<FetchRequestPartition>();
                topicPartitions[p.Topic] = list;
            }

            list.Add(new FetchRequestPartition
            {
                Partition = p.Partition,
                FetchOffset = _fetchPositions.GetValueOrDefault(p, 0),
                PartitionMaxBytes = _options.MaxPartitionFetchBytes
            });
        }

        // Build result
        var result = new List<FetchRequestTopic>(topicPartitions.Count);
        foreach (var kvp in topicPartitions)
        {
            result.Add(new FetchRequestTopic
            {
                Topic = kvp.Key,
                Partitions = kvp.Value
            });
        }

        // Update cache (first writer wins to avoid overwriting fresher data)
        lock (_fetchCacheLock)
        {
            if (_cachedTopicPartitions is null)
            {
                _cachedTopicPartitions = topicPartitions;
                _cachedPartitionsList = new List<TopicPartition>(partitions);
                // Build lookup dictionary for fast TopicPartition retrieval in hot path
                _topicPartitionLookup = new Dictionary<(string Topic, int Partition), TopicPartition>(partitions.Count);
                foreach (var p in partitions)
                {
                    _topicPartitionLookup[(p.Topic, p.Partition)] = p;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Clones the cached structure with updated fetch offsets.
    /// Must clone because FetchOffset changes between fetch calls.
    /// Uses pre-cached TopicPartition lookup to avoid per-partition allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private List<FetchRequestTopic> CloneCacheWithUpdatedOffsets(
        Dictionary<string, List<FetchRequestPartition>> cachedDict,
        Dictionary<(string Topic, int Partition), TopicPartition> tpLookup)
    {
        var result = new List<FetchRequestTopic>(cachedDict.Count);

        foreach (var kvp in cachedDict)
        {
            var topic = kvp.Key;
            var cachedPartitions = kvp.Value;
            var newPartitions = new List<FetchRequestPartition>(cachedPartitions.Count);

            foreach (var p in cachedPartitions)
            {
                // Use cached TopicPartition to avoid struct allocation per partition
                var tp = tpLookup[(topic, p.Partition)];
                newPartitions.Add(new FetchRequestPartition
                {
                    Partition = p.Partition,
                    FetchOffset = _fetchPositions.GetValueOrDefault(tp, 0),
                    PartitionMaxBytes = p.PartitionMaxBytes
                });
            }

            result.Add(new FetchRequestTopic
            {
                Topic = topic,
                Partitions = newPartitions
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
            _topicPartitionLookup = null;
        }
    }

    private void StartAutoCommit()
    {
        _autoCommitCts?.Cancel();
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
                _logger?.LogWarning(ex, "Auto-commit failed");
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        // Idempotent - return early if already closed/disposed
        if (_closed || _disposed)
            return;

        _closed = true;

        _logger?.LogDebug("Closing consumer gracefully");

        // Step 1: Stop heartbeat background task
        if (_coordinator is not null)
        {
            await _coordinator.StopHeartbeatAsync().ConfigureAwait(false);
        }

        // Step 2: Stop auto-commit task
        _autoCommitCts?.Cancel();
        if (_autoCommitTask is not null)
        {
            try
            {
                await _autoCommitTask.ConfigureAwait(false);
            }
            catch
            {
                // Ignore cancellation exceptions
            }
        }

        // Step 3: Stop prefetch task
        _prefetchCts?.Cancel();
        if (_prefetchTask is not null)
        {
            try
            {
                await _prefetchTask.ConfigureAwait(false);
            }
            catch
            {
                // Ignore cancellation exceptions
            }
        }

        // Step 4: Commit pending offsets (if auto-commit enabled and we have a coordinator)
        if (_options.EnableAutoCommit && _coordinator is not null && !_positions.IsEmpty)
        {
            try
            {
                await CommitAsync(cancellationToken).ConfigureAwait(false);
                _logger?.LogDebug("Committed pending offsets during close");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to commit offsets during close");
            }
        }

        // Step 5: Send LeaveGroup request to coordinator
        if (_coordinator is not null)
        {
            try
            {
                await _coordinator.LeaveGroupAsync("Consumer closing gracefully", cancellationToken).ConfigureAwait(false);
                _logger?.LogDebug("Left consumer group during close");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to leave group during close");
            }
        }

        // Step 6: Wake up any blocked operations
        _wakeupCts?.Cancel();

        // Step 7: Clear pending fetch data
        _pendingFetches.Clear();
        while (_prefetchChannel.Reader.TryRead(out _))
        {
            // Discard prefetched data
        }

        _logger?.LogInformation("Consumer closed gracefully");
    }

    public async ValueTask<IReadOnlyDictionary<TopicPartition, long>> GetOffsetsForTimesAsync(
        IEnumerable<TopicPartitionTimestamp> timestampsToSearch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(timestampsToSearch);

        if (_disposed)
            throw new ObjectDisposedException(nameof(KafkaConsumer<TKey, TValue>));

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        // Group partitions by broker leader for efficient batch requests
        var partitionsByBroker = new Dictionary<int, List<TopicPartitionTimestamp>>();
        foreach (var tpt in timestampsToSearch)
        {
            var leader = await _metadataManager.GetPartitionLeaderAsync(tpt.Topic, tpt.Partition, cancellationToken)
                .ConfigureAwait(false);

            if (leader is null)
            {
                _logger?.LogWarning("No leader found for {Topic}-{Partition}", tpt.Topic, tpt.Partition);
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
        var connection = await _connectionPool.GetConnectionAsync(brokerId, cancellationToken).ConfigureAwait(false);

        var listOffsetsVersion = _metadataManager.GetNegotiatedApiVersion(
            ApiKey.ListOffsets,
            Protocol.Messages.ListOffsetsRequest.LowestSupportedVersion,
            Protocol.Messages.ListOffsetsRequest.HighestSupportedVersion);

        // Group partitions by topic
        var topicPartitions = new Dictionary<string, List<Protocol.Messages.ListOffsetsRequestPartition>>();
        foreach (var tpt in partitions)
        {
            if (!topicPartitions.TryGetValue(tpt.Topic, out var list))
            {
                list = [];
                topicPartitions[tpt.Topic] = list;
            }

            list.Add(new Protocol.Messages.ListOffsetsRequestPartition
            {
                PartitionIndex = tpt.Partition,
                Timestamp = tpt.Timestamp,
                CurrentLeaderEpoch = -1
            });
        }

        // Build topics list
        var topics = new List<Protocol.Messages.ListOffsetsRequestTopic>(topicPartitions.Count);
        foreach (var kvp in topicPartitions)
        {
            topics.Add(new Protocol.Messages.ListOffsetsRequestTopic
            {
                Name = kvp.Key,
                Partitions = kvp.Value
            });
        }

        var request = new Protocol.Messages.ListOffsetsRequest
        {
            ReplicaId = -1,
            IsolationLevel = _options.IsolationLevel,
            Topics = topics
        };

        var response = await connection.SendAsync<Protocol.Messages.ListOffsetsRequest, Protocol.Messages.ListOffsetsResponse>(
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
                    _logger?.LogWarning(
                        "ListOffsets error for {Topic}-{Partition}: {Error}",
                        topicName, partitionResponse.PartitionIndex, partitionResponse.ErrorCode);
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
        if (_disposed)
            return;

        _disposed = true;

        // If not already closed, perform graceful close first (but with a short timeout)
        if (!_closed)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await CloseAsync(cts.Token).ConfigureAwait(false);
            }
            catch
            {
                // Ignore errors during dispose
            }
        }

        _wakeupCts?.Cancel();
        _autoCommitCts?.Cancel();
        _prefetchCts?.Cancel();

        if (_autoCommitTask is not null)
        {
            try
            {
                await _autoCommitTask.ConfigureAwait(false);
            }
            catch
            {
                // Ignore
            }
        }

        if (_prefetchTask is not null)
        {
            try
            {
                await _prefetchTask.ConfigureAwait(false);
            }
            catch
            {
                // Ignore
            }
        }

        _autoCommitCts?.Dispose();
        _prefetchCts?.Dispose();

        // Clear and dispose CancellationTokenSource pool
        // Note: _wakeupCts is managed by the pool and should not be disposed here
        _ctsPool.Clear();

        // Clear any pending fetch data
        _pendingFetches.Clear();

        // Drain prefetch channel
        while (_prefetchChannel.Reader.TryRead(out _))
        {
            // Discard
        }

        // Dispose statistics emitter
        if (_statisticsEmitter is not null)
        {
            await _statisticsEmitter.DisposeAsync().ConfigureAwait(false);
        }

        if (_coordinator is not null)
            await _coordinator.DisposeAsync().ConfigureAwait(false);

        await _metadataManager.DisposeAsync().ConfigureAwait(false);
        await _connectionPool.DisposeAsync().ConfigureAwait(false);
    }
}
