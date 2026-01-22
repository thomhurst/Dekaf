using System.Buffers;
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

    // Pending fetch responses for lazy record iteration
    private readonly Queue<PendingFetchData> _pendingFetches = new();

    private CancellationTokenSource? _wakeupCts;
    private CancellationTokenSource? _autoCommitCts;
    private Task? _autoCommitTask;
    private volatile short _fetchApiVersion = -1;
    private volatile bool _disposed;

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
                RequestTimeout = TimeSpan.FromMilliseconds(options.RequestTimeoutMs),
                SaslMechanism = options.SaslMechanism,
                SaslUsername = options.SaslUsername,
                SaslPassword = options.SaslPassword
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
    }

    public IReadOnlySet<string> Subscription => _subscription;
    public IReadOnlySet<TopicPartition> Assignment => _assignment;
    public string? MemberId => _coordinator?.MemberId;
    public IReadOnlySet<TopicPartition> Paused => _paused;

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

        while (!cancellationToken.IsCancellationRequested)
        {
            await EnsureAssignmentAsync(cancellationToken).ConfigureAwait(false);

            if (_assignment.Count == 0)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                continue;
            }

            // If no pending records, fetch more
            if (_pendingFetches.Count == 0)
            {
                await FetchRecordsAsync(cancellationToken).ConfigureAwait(false);
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

                    var key = DeserializeKey(record.Key, record.IsKeyNull, pending.Topic);
                    var value = DeserializeValue(record.Value, record.IsValueNull, pending.Topic);
                    var headers = ConvertHeaders(record.Headers);

                    var result = new ConsumeResult<TKey, TValue>
                    {
                        Topic = pending.Topic,
                        Partition = pending.PartitionIndex,
                        Offset = offset,
                        Key = key,
                        Value = value,
                        Headers = headers,
                        Timestamp = timestamp,
                        TimestampType = ((int)batch.Attributes & 0x08) != 0
                            ? TimestampType.LogAppendTime
                            : TimestampType.CreateTime
                    };

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

        var offsets = _positions.Select(kvp => new TopicPartitionOffset(kvp.Key.Topic, kvp.Key.Partition, kvp.Value));
        await _coordinator.CommitOffsetsAsync(offsets, cancellationToken).ConfigureAwait(false);

        foreach (var kvp in _positions)
        {
            _committed[kvp.Key] = kvp.Value;
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
        return _positions.GetValueOrDefault(partition);
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

    private void ClearFetchBuffer()
    {
        // Clear pending fetches to discard stale records
        _pendingFetches.Clear();
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

    private TKey? DeserializeKey(ReadOnlyMemory<byte> data, bool isNull, string topic)
    {
        if (isNull)
            return default;

        var context = new SerializationContext
        {
            Topic = topic,
            Component = SerializationComponent.Key
        };

        return _keyDeserializer.Deserialize(new ReadOnlySequence<byte>(data), context);
    }

    private TValue DeserializeValue(ReadOnlyMemory<byte> data, bool isNull, string topic)
    {
        var context = new SerializationContext
        {
            Topic = topic,
            Component = SerializationComponent.Value
        };

        if (isNull)
            return _valueDeserializer.Deserialize(ReadOnlySequence<byte>.Empty, context);

        return _valueDeserializer.Deserialize(new ReadOnlySequence<byte>(data), context);
    }

    private static Headers? ConvertHeaders(IReadOnlyList<RecordHeader>? recordHeaders)
    {
        if (recordHeaders is null || recordHeaders.Count == 0)
            return null;

        var headers = new Headers(recordHeaders.Count);
        foreach (var h in recordHeaders)
        {
            // Convert ReadOnlyMemory<byte> to byte[]? for Header
            headers.Add(new Header(h.Key, h.IsValueNull ? null : h.Value.ToArray()));
        }
        return headers;
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

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        _wakeupCts?.Cancel();
        _autoCommitCts?.Cancel();

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

        _autoCommitCts?.Dispose();
        _wakeupCts?.Dispose();

        // Clear any pending fetch data
        _pendingFetches.Clear();

        if (_coordinator is not null)
            await _coordinator.DisposeAsync().ConfigureAwait(false);

        await _metadataManager.DisposeAsync().ConfigureAwait(false);
        await _connectionPool.DisposeAsync().ConfigureAwait(false);
    }
}
