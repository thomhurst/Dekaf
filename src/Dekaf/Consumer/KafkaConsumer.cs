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
/// Kafka consumer implementation.
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
    private readonly Dictionary<TopicPartition, long> _positions = [];
    private readonly Dictionary<TopicPartition, long> _committed = [];
    private readonly Channel<ConsumeResult<TKey, TValue>> _fetchBuffer;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private CancellationTokenSource? _wakeupCts;
    private CancellationTokenSource? _autoCommitCts;
    private Task? _autoCommitTask;
    private short _fetchApiVersion = -1;
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
                RequestTimeout = TimeSpan.FromMilliseconds(options.RequestTimeoutMs)
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

        _fetchBuffer = Channel.CreateBounded<ConsumeResult<TKey, TValue>>(new BoundedChannelOptions(options.MaxPollRecords)
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

            // Fetch records
            await FetchRecordsAsync(cancellationToken).ConfigureAwait(false);

            // Yield records from buffer
            while (_fetchBuffer.Reader.TryRead(out var result))
            {
                yield return result;
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
        _positions[new TopicPartition(offset.Topic, offset.Partition)] = offset.Offset;
        return this;
    }

    public IKafkaConsumer<TKey, TValue> SeekToBeginning(params TopicPartition[] partitions)
    {
        foreach (var partition in partitions)
        {
            _positions[partition] = 0;
        }
        return this;
    }

    public IKafkaConsumer<TKey, TValue> SeekToEnd(params TopicPartition[] partitions)
    {
        foreach (var partition in partitions)
        {
            _positions[partition] = -1; // Special value meaning end
        }
        return this;
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

            // Update assignment from coordinator
            _assignment.Clear();
            foreach (var partition in _coordinator.Assignment)
            {
                _assignment.Add(partition);
            }
        }
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
            _fetchApiVersion = FetchRequest.HighestSupportedVersion;
        }

        // Build fetch request
        var topicData = partitions
            .GroupBy(p => p.Topic)
            .Select(g => new FetchRequestTopic
            {
                Topic = g.Key,
                Partitions = g.Select(p => new FetchRequestPartition
                {
                    Partition = p.Partition,
                    FetchOffset = _positions.GetValueOrDefault(p, 0),
                    PartitionMaxBytes = _options.MaxPartitionFetchBytes
                }).ToList()
            }).ToList();

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

        // Process response
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

                if (partitionResponse.Records is null)
                    continue;

                foreach (var batch in partitionResponse.Records)
                {
                    foreach (var record in batch.Records)
                    {
                        var offset = batch.BaseOffset + record.OffsetDelta;
                        var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(
                            batch.BaseTimestamp + record.TimestampDelta);

                        var key = DeserializeKey(record.Key, topic);
                        var value = DeserializeValue(record.Value, topic);

                        var headers = record.Headers is not null
                            ? new Headers(record.Headers.Select(h => new Header(h.Key, h.Value)))
                            : null;

                        var result = new ConsumeResult<TKey, TValue>
                        {
                            Topic = topic,
                            Partition = partitionResponse.PartitionIndex,
                            Offset = offset,
                            Key = key,
                            Value = value,
                            Headers = headers,
                            Timestamp = timestamp,
                            TimestampType = ((int)batch.Attributes & 0x08) != 0
                                ? TimestampType.LogAppendTime
                                : TimestampType.CreateTime
                        };

                        await _fetchBuffer.Writer.WriteAsync(result, cancellationToken).ConfigureAwait(false);

                        // Update position
                        _positions[new TopicPartition(topic, partitionResponse.PartitionIndex)] = offset + 1;
                    }
                }
            }
        }
    }

    private TKey? DeserializeKey(byte[]? data, string topic)
    {
        if (data is null)
            return default;

        var context = new SerializationContext
        {
            Topic = topic,
            Component = SerializationComponent.Key
        };

        return _keyDeserializer.Deserialize(new ReadOnlySequence<byte>(data), context);
    }

    private TValue DeserializeValue(byte[]? data, string topic)
    {
        var context = new SerializationContext
        {
            Topic = topic,
            Component = SerializationComponent.Value
        };

        return _valueDeserializer.Deserialize(new ReadOnlySequence<byte>(data ?? []), context);
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

        _fetchBuffer.Writer.Complete();

        if (_coordinator is not null)
            await _coordinator.DisposeAsync().ConfigureAwait(false);

        await _metadataManager.DisposeAsync().ConfigureAwait(false);
        await _connectionPool.DisposeAsync().ConfigureAwait(false);

        _lock.Dispose();
    }
}
