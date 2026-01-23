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
using Microsoft.Extensions.Logging;

namespace Dekaf.Consumer;

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

    public PendingFetchData(string topic, int partitionIndex, IReadOnlyList<RecordBatch> batches)
    {
        Topic = topic;
        PartitionIndex = partitionIndex;
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
    private readonly Dictionary<TopicPartition, long> _positions = [];      // Consumed position (what app has seen)
    private readonly Dictionary<TopicPartition, long> _fetchPositions = []; // Fetch position (what to fetch next)
    private readonly Dictionary<TopicPartition, long> _committed = [];

    // Stored offsets for manual offset storage (when EnableAutoOffsetStore = false)
    // Uses ConcurrentDictionary for thread-safety as StoreOffset may be called from different threads
    private readonly ConcurrentDictionary<TopicPartition, long> _storedOffsets = new();

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
    private volatile short _fetchApiVersion = -1;
    private volatile bool _disposed;
    private volatile bool _closed;

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
    }

    public IReadOnlySet<string> Subscription => _subscription;
    public IReadOnlySet<TopicPartition> Assignment => _assignment;
    public string? MemberId => _coordinator?.MemberId;
    public IReadOnlySet<TopicPartition> Paused => _paused;

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
        return this;
    }

    public IKafkaConsumer<TKey, TValue> Unassign()
    {
        _assignment.Clear();
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

        return this;
    }

    public IKafkaConsumer<TKey, TValue> IncrementalUnassign(IEnumerable<TopicPartition> partitions)
    {
        foreach (var partition in partitions)
        {
            _assignment.Remove(partition);
            _paused.Remove(partition);
            _positions.Remove(partition);
            _fetchPositions.Remove(partition);
            _committed.Remove(partition);
        }

        // Clear any pending fetch data for the removed partitions
        ClearFetchBufferForPartitions(partitions);

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
                        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(_options.FetchMaxWaitMs));
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

                    // Create result with raw data - deserialization happens lazily on Key/Value access
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

                    // Update positions
                    var tp = new TopicPartition(pending.Topic, pending.PartitionIndex);
                    _positions[tp] = offset + 1;
                    _fetchPositions[tp] = offset + 1;

                    yield return result;
                }

                // This pending fetch is exhausted, remove it
                _pendingFetches.Dequeue();
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
        _wakeupCts = new CancellationTokenSource();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _wakeupCts.Token);

        var partitionsByBroker = await GroupPartitionsByBrokerAsync(cancellationToken).ConfigureAwait(false);

        foreach (var (brokerId, partitions) in partitionsByBroker)
        {
            try
            {
                await PrefetchFromBrokerAsync(brokerId, partitions, linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_wakeupCts.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to prefetch from broker {BrokerId}", brokerId);
            }
        }
    }

    private async ValueTask PrefetchFromBrokerAsync(int brokerId, List<TopicPartition> partitions, CancellationToken cancellationToken)
    {
        var connection = await _connectionPool.GetConnectionAsync(brokerId, cancellationToken).ConfigureAwait(false);

        // Ensure API version is negotiated
        if (_fetchApiVersion < 0)
        {
            _fetchApiVersion = _metadataManager.GetNegotiatedApiVersion(
                ApiKey.Fetch,
                FetchRequest.LowestSupportedVersion,
                FetchRequest.HighestSupportedVersion);
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

        var response = await connection.SendAsync<FetchRequest, FetchResponse>(
            request,
            _fetchApiVersion,
            cancellationToken).ConfigureAwait(false);

        // Write to prefetch channel
        foreach (var topicResponse in response.Responses)
        {
            var topic = topicResponse.Topic ?? string.Empty;

            foreach (var partitionResponse in topicResponse.Partitions)
            {
                if (partitionResponse.ErrorCode != ErrorCode.None)
                {
                    _logger?.LogWarning(
                        "Prefetch error for {Topic}-{Partition}: {Error}",
                        topic, partitionResponse.PartitionIndex, partitionResponse.ErrorCode);
                    continue;
                }

                if (partitionResponse.Records is null || partitionResponse.Records.Count == 0)
                    continue;

                var pending = new PendingFetchData(
                    topic,
                    partitionResponse.PartitionIndex,
                    partitionResponse.Records);

                // Track memory before adding to channel
                TrackPrefetchedBytes(pending, release: false);

                // Update fetch positions for next prefetch
                UpdateFetchPositionsFromPrefetch(pending);

                await _prefetchChannel.Writer.WriteAsync(pending, cancellationToken).ConfigureAwait(false);
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

        lock (_prefetchLock)
        {
            var currentPos = _fetchPositions.GetValueOrDefault(tp, 0);
            if (lastOffset + 1 > currentPos)
            {
                _fetchPositions[tp] = lastOffset + 1;
            }
        }
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
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        await foreach (var result in ConsumeAsync(linkedCts.Token).ConfigureAwait(false))
        {
            return result;
        }

        return null;
    }

    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_coordinator is null)
            return;

        List<TopicPartitionOffset> offsets;

        if (_options.EnableAutoOffsetStore)
        {
            // Auto-store mode: commit all consumed positions
            offsets = new List<TopicPartitionOffset>(_positions.Count);
            foreach (var kvp in _positions)
            {
                offsets.Add(new TopicPartitionOffset(kvp.Key.Topic, kvp.Key.Partition, kvp.Value));
            }
        }
        else
        {
            // Manual store mode: commit only explicitly stored offsets
            // Take a snapshot to avoid race condition where offsets stored by another thread
            // between enumeration and removal would be lost
            var snapshot = _storedOffsets.ToArray();
            offsets = new List<TopicPartitionOffset>(snapshot.Length);
            foreach (var kvp in snapshot)
            {
                offsets.Add(new TopicPartitionOffset(kvp.Key.Topic, kvp.Key.Partition, kvp.Value));
            }

            if (offsets.Count == 0)
                return;

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

            return;
        }

        if (offsets.Count == 0)
            return;

        await _coordinator.CommitOffsetsAsync(offsets, cancellationToken).ConfigureAwait(false);

        // Update committed offsets tracking
        foreach (var offset in offsets)
        {
            _committed[new TopicPartition(offset.Topic, offset.Partition)] = offset.Offset;
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
        _positions[tp] = offset.Offset;
        _fetchPositions[tp] = offset.Offset;
        ClearFetchBuffer();
        return this;
    }

    public IKafkaConsumer<TKey, TValue> SeekToBeginning(params TopicPartition[] partitions)
    {
        foreach (var partition in partitions)
        {
            _positions[partition] = 0;
            _fetchPositions[partition] = 0;
        }
        ClearFetchBuffer();
        return this;
    }

    public IKafkaConsumer<TKey, TValue> SeekToEnd(params TopicPartition[] partitions)
    {
        foreach (var partition in partitions)
        {
            _positions[partition] = -1; // Special value meaning end
            _fetchPositions[partition] = -1; // Special value meaning end
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
    }

    private void ClearFetchBufferForPartitions(IEnumerable<TopicPartition> partitionsToRemove)
    {
        // Create a set for efficient lookup
        var removeSet = partitionsToRemove is HashSet<TopicPartition> set
            ? set
            : new HashSet<TopicPartition>(partitionsToRemove);

        if (removeSet.Count == 0)
            return;

        // We need to rebuild the queue, keeping only data for partitions not being removed
        var tempQueue = new Queue<PendingFetchData>();
        while (_pendingFetches.Count > 0)
        {
            var pending = _pendingFetches.Dequeue();
            var tp = new TopicPartition(pending.Topic, pending.PartitionIndex);
            if (!removeSet.Contains(tp))
            {
                tempQueue.Enqueue(pending);
            }
        }

        // Put back the items we want to keep
        while (tempQueue.Count > 0)
        {
            _pendingFetches.Enqueue(tempQueue.Dequeue());
        }
    }

    public IKafkaConsumer<TKey, TValue> Pause(params TopicPartition[] partitions)
    {
        foreach (var partition in partitions)
        {
            _paused.Add(partition);
        }
        return this;
    }

    public IKafkaConsumer<TKey, TValue> Resume(params TopicPartition[] partitions)
    {
        foreach (var partition in partitions)
        {
            _paused.Remove(partition);
        }
        return this;
    }

    public void Wakeup()
    {
        _wakeupCts?.Cancel();
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

            // Update assignment from coordinator
            _assignment.Clear();
            foreach (var partition in _coordinator.Assignment)
            {
                _assignment.Add(partition);
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

        var topicResponse = response.Topics.FirstOrDefault(t => t.Name == partition.Topic);
        var partitionResponse = topicResponse?.Partitions.FirstOrDefault(p => p.PartitionIndex == partition.Partition);

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

        var topicResponse = response.Topics.FirstOrDefault(t => t.Name == partition.Topic);
        var partitionResponse = topicResponse?.Partitions.FirstOrDefault(p => p.PartitionIndex == partition.Partition);

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
        _wakeupCts = new CancellationTokenSource();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _wakeupCts.Token);

        var partitionsByBroker = await GroupPartitionsByBrokerAsync(cancellationToken).ConfigureAwait(false);

        foreach (var (brokerId, partitions) in partitionsByBroker)
        {
            try
            {
                await FetchFromBrokerAsync(brokerId, partitions, linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_wakeupCts.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to fetch from broker {BrokerId}", brokerId);
            }
        }
    }

    private async ValueTask<Dictionary<int, List<TopicPartition>>> GroupPartitionsByBrokerAsync(CancellationToken cancellationToken)
    {
        var result = new Dictionary<int, List<TopicPartition>>();

        foreach (var partition in _assignment)
        {
            if (_paused.Contains(partition))
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

        return result;
    }

    private async ValueTask FetchFromBrokerAsync(int brokerId, List<TopicPartition> partitions, CancellationToken cancellationToken)
    {
        var connection = await _connectionPool.GetConnectionAsync(brokerId, cancellationToken).ConfigureAwait(false);

        // Ensure API version is negotiated
        if (_fetchApiVersion < 0)
        {
            _fetchApiVersion = _metadataManager.GetNegotiatedApiVersion(
                ApiKey.Fetch,
                FetchRequest.LowestSupportedVersion,
                FetchRequest.HighestSupportedVersion);
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

        var response = await connection.SendAsync<FetchRequest, FetchResponse>(
            request,
            _fetchApiVersion,
            cancellationToken).ConfigureAwait(false);

        // Queue pending fetch data for lazy iteration - don't parse records yet!
        foreach (var topicResponse in response.Responses)
        {
            var topic = topicResponse.Topic ?? string.Empty;

            foreach (var partitionResponse in topicResponse.Partitions)
            {
                if (partitionResponse.ErrorCode != ErrorCode.None)
                {
                    _logger?.LogWarning(
                        "Fetch error for {Topic}-{Partition}: {Error}",
                        topic, partitionResponse.PartitionIndex, partitionResponse.ErrorCode);
                    continue;
                }

                if (partitionResponse.Records is null || partitionResponse.Records.Count == 0)
                    continue;

                // Queue the pending fetch data for lazy record iteration
                _pendingFetches.Enqueue(new PendingFetchData(
                    topic,
                    partitionResponse.PartitionIndex,
                    partitionResponse.Records));
            }
        }
    }

    /// <summary>
    /// Returns headers directly without conversion. Returns null if empty.
    /// </summary>
    private static IReadOnlyList<RecordHeader>? GetHeaders(IReadOnlyList<RecordHeader>? recordHeaders)
    {
        // Return null for empty to avoid exposing empty lists
        if (recordHeaders is null || recordHeaders.Count == 0)
            return null;

        // Return directly - no conversion needed, zero allocation
        return recordHeaders;
    }

    private List<FetchRequestTopic> BuildFetchRequestTopics(List<TopicPartition> partitions)
    {
        // Group partitions by topic without using LINQ GroupBy
        Dictionary<string, List<FetchRequestPartition>>? topicPartitions = null;

        foreach (var p in partitions)
        {
            topicPartitions ??= new Dictionary<string, List<FetchRequestPartition>>();

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

        if (topicPartitions is null)
            return [];

        var result = new List<FetchRequestTopic>(topicPartitions.Count);
        foreach (var kvp in topicPartitions)
        {
            result.Add(new FetchRequestTopic
            {
                Topic = kvp.Key,
                Partitions = kvp.Value
            });
        }

        return result;
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
        if (_options.EnableAutoCommit && _coordinator is not null && _positions.Count > 0)
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
        _wakeupCts?.Dispose();
        _prefetchCts?.Dispose();

        // Clear any pending fetch data
        _pendingFetches.Clear();

        // Drain prefetch channel
        while (_prefetchChannel.Reader.TryRead(out _))
        {
            // Discard
        }

        if (_coordinator is not null)
            await _coordinator.DisposeAsync().ConfigureAwait(false);

        await _metadataManager.DisposeAsync().ConfigureAwait(false);
        await _connectionPool.DisposeAsync().ConfigureAwait(false);
    }
}
