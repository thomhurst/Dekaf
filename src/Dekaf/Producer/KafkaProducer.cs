using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Dekaf.Compression;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Dekaf.Statistics;
using Microsoft.Extensions.Logging;

namespace Dekaf.Producer;

/// <summary>
/// Kafka producer implementation.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public sealed class KafkaProducer<TKey, TValue> : IKafkaProducer<TKey, TValue>
{
    private readonly ProducerOptions _options;
    private readonly ISerializer<TKey> _keySerializer;
    private readonly ISerializer<TValue> _valueSerializer;
    private readonly IPartitioner _partitioner;
    private readonly IConnectionPool _connectionPool;
    private readonly MetadataManager _metadataManager;
    private readonly RecordAccumulator _accumulator;
    private readonly CompressionCodecRegistry _compressionCodecs;
    private readonly ILogger<KafkaProducer<TKey, TValue>>? _logger;

    private readonly CancellationTokenSource _senderCts;
    private readonly Task _senderTask;
    private readonly Task _lingerTask;
    private readonly PeriodicTimer _lingerTimer;


    // Channel-based worker pool for thread-safe produce operations
    private readonly Channel<ProduceWorkItem<TKey, TValue>> _workChannel;
    private readonly Task[] _workerTasks;
    private readonly int _workerCount;

    private int _produceApiVersion = -1;
    private volatile bool _disposed;

    // Statistics collection
    private readonly ProducerStatisticsCollector _statisticsCollector = new();
    private readonly StatisticsEmitter<ProducerStatistics>? _statisticsEmitter;

    // Pool for PooledValueTaskSource to avoid per-message allocations
    // Unlike TaskCompletionSource, these can be reset and reused
    private readonly ValueTaskSourcePool<RecordMetadata> _valueTaskSourcePool;

    // Thread-local reusable SerializationContext to avoid per-message allocations
    // Since SerializationContext contains reference types (Topic, Headers), copying it
    // involves copying those references. Using ThreadStatic avoids repeated struct creation.
    //
    // ThreadStatic initialization: Default struct initialization (all fields = null/default) is safe.
    // The struct is updated via property setters before each use, so initial null values don't matter.
    // Reference type fields (string Topic, Headers? Headers) start as null and are explicitly set
    // before passing to serializers, avoiding any uninitialized state issues.
    [ThreadStatic]
    private static SerializationContext t_serializationContext;

    // Thread-local cached timestamp for fire-and-forget produces.
    // DateTimeOffset.UtcNow is expensive (~15-30ns). By caching the timestamp and refreshing
    // it approximately every millisecond, we can reduce overhead in high-throughput scenarios.
    // For fire-and-forget, precise per-message timestamps aren't critical since:
    // 1. Records store timestamp deltas from batch base timestamp
    // 2. Batches are sent within LingerMs (typically 0-5ms)
    [ThreadStatic]
    private static long t_cachedTimestampMs;
    [ThreadStatic]
    private static long t_cachedTimestampTicks;

    // Thread-local cached topic metadata for fire-and-forget produces.
    // Avoids MetadataManager dictionary lookups and IsStale() checks for consecutive
    // messages to the same topic. Staleness is checked periodically (~1 second) rather
    // than on every message, since metadata is typically valid for minutes.
    [ThreadStatic]
    private static string? t_cachedTopicName;
    [ThreadStatic]
    private static TopicInfo? t_cachedTopicInfo;
    [ThreadStatic]
    private static long t_cachedTopicValidUntilTicks;
    [ThreadStatic]
    private static MetadataManager? t_cachedMetadataManager;

    // Thread-local reusable serialization buffers to avoid PooledBufferWriter creation per message.
    // These buffers grow as needed and are reused across messages on the same thread.
    // After serialization, data is copied to a right-sized pooled buffer for the batch.
    // This trades a small copy for eliminating buffer allocation overhead.
    [ThreadStatic]
    private static byte[]? t_keySerializationBuffer;
    [ThreadStatic]
    private static byte[]? t_valueSerializationBuffer;

    // Default sizes match typical key/value sizes to avoid growth in common cases
    private const int DefaultKeyBufferSize = 512;
    private const int DefaultValueBufferSize = 2048;

    public KafkaProducer(
        ProducerOptions options,
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerializer,
        ILoggerFactory? loggerFactory = null)
    {
        _options = options;
        _keySerializer = keySerializer;
        _valueSerializer = valueSerializer;
        _logger = loggerFactory?.CreateLogger<KafkaProducer<TKey, TValue>>();

        // Initialize ValueTaskSource pool with configured size
        _valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>(options.ValueTaskSourcePoolSize);

        _partitioner = options.Partitioner switch
        {
            PartitionerType.Sticky => new StickyPartitioner(),
            PartitionerType.RoundRobin => new RoundRobinPartitioner(),
            _ => new DefaultPartitioner()
        };

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
            options.ConnectionsPerBroker);

        _metadataManager = new MetadataManager(
            _connectionPool,
            options.BootstrapServers,
            logger: loggerFactory?.CreateLogger<MetadataManager>());

        _accumulator = new RecordAccumulator(options);
        _compressionCodecs = new CompressionCodecRegistry();

        _senderCts = new CancellationTokenSource();
        // Use 1ms check interval for low-latency awaited produces.
        // ShouldFlush() is smart: it returns true immediately for batches with completion
        // sources (awaited produces), but waits for full LingerMs for fire-and-forget batches.
        // This provides low latency for ProduceAsync while maintaining efficient batching for Send.
        _lingerTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(1));
        _senderTask = SenderLoopAsync(_senderCts.Token);
        _lingerTask = LingerLoopAsync(_senderCts.Token);

        // Set up worker pool for thread-safe produce operations
        // Use unbounded channel to avoid artificial backpressure that can cause deadlocks
        // Natural backpressure happens when Kafka can't keep up (TCP flow control, slow responses)
        // This matches Confluent.Kafka's approach (queue.buffering.max.messages = 100,000)
        _workerCount = Environment.ProcessorCount;
        _workChannel = Channel.CreateUnbounded<ProduceWorkItem<TKey, TValue>>(
            new UnboundedChannelOptions
            {
                SingleReader = false,   // Multiple workers read
                SingleWriter = false    // Multiple callers write
            });

        // Start worker tasks
        _workerTasks = new Task[_workerCount];
        for (var i = 0; i < _workerCount; i++)
        {
            _workerTasks[i] = ProcessWorkAsync(_senderCts.Token);
        }

        // Start statistics emitter if configured
        if (options.StatisticsInterval.HasValue &&
            options.StatisticsInterval.Value > TimeSpan.Zero &&
            options.StatisticsHandler is not null)
        {
            _statisticsEmitter = new StatisticsEmitter<ProducerStatistics>(
                options.StatisticsInterval.Value,
                CollectStatistics,
                options.StatisticsHandler);
        }
    }

    private ProducerStatistics CollectStatistics()
    {
        var (messagesProduced, messagesDelivered, messagesFailed, bytesProduced,
            requestsSent, responsesReceived, retries, avgLatencyMs) = _statisticsCollector.GetGlobalStats();

        return new ProducerStatistics
        {
            Timestamp = DateTimeOffset.UtcNow,
            MessagesProduced = messagesProduced,
            MessagesDelivered = messagesDelivered,
            MessagesFailed = messagesFailed,
            BytesProduced = bytesProduced,
            QueuedMessages = (int)(messagesProduced - messagesDelivered - messagesFailed),
            RequestsSent = requestsSent,
            ResponsesReceived = responsesReceived,
            Retries = retries,
            AvgRequestLatencyMs = avgLatencyMs,
            Topics = _statisticsCollector.GetTopicStatistics()
        };
    }

    public ValueTask<RecordMetadata> ProduceAsync(
        ProducerMessage<TKey, TValue> message,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));

        // Fast path: Try synchronous produce if metadata is initialized and cached.
        // This bypasses channel overhead for 99%+ of calls after warmup.
        if (TryProduceSyncForAsync(message, out var completion))
        {
            // Return the ValueTask directly - no async state machine needed
            return completion!.Task;
        }

        // Slow path: Fall back to channel-based async processing.
        // This handles first-time metadata initialization or cache misses.
        return ProduceAsyncSlow(message, cancellationToken);
    }

    /// <summary>
    /// Attempts synchronous produce for awaited ProduceAsync when metadata is cached.
    /// Returns true if successful with the completion source to await.
    /// Unlike TryProduceSync, this version throws exceptions for awaited callers.
    /// Uses thread-local metadata cache for maximum performance on hot path.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryProduceSyncForAsync(ProducerMessage<TKey, TValue> message, out PooledValueTaskSource<RecordMetadata>? completion)
    {
        completion = null;

        // Check if metadata is initialized (sync check)
        if (_metadataManager.Metadata.LastRefreshed == default)
        {
            return false; // Need async initialization
        }

        // FAST PATH: Check thread-local cached topic metadata first.
        // Avoids MetadataManager dictionary lookup for consecutive messages to the same topic.
        TopicInfo? topicInfo;
        if (!TryGetCachedTopicInfo(message.Topic, out topicInfo))
        {
            // Cache miss - try MetadataManager
            if (!_metadataManager.TryGetCachedTopicMetadata(message.Topic, out topicInfo) || topicInfo is null)
            {
                return false; // Cache miss, need async refresh
            }

            // Update thread-local cache for next call
            UpdateCachedTopicInfo(message.Topic, topicInfo);
        }

        if (topicInfo!.PartitionCount == 0)
        {
            return false; // Invalid topic state, let async path handle error
        }

        // All checks passed - we can proceed synchronously
        completion = _valueTaskSourcePool.Rent();
        try
        {
            ProduceSyncCore(message, topicInfo, completion);
        }
        catch (Exception ex)
        {
            // If ProduceSyncCore throws before setting result/exception on completion,
            // the rented completion would be leaked (never awaited = never returned to pool).
            // Set the exception on the completion so the caller can await it and it gets
            // properly returned to the pool.
            // Note: ProduceSyncCore may have already called TrySetException, so use Try variant.
            // Don't re-throw here - let the caller await the ValueTask and get the exception.
            completion.TrySetException(ex);
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async ValueTask<RecordMetadata> ProduceAsyncSlow(
        ProducerMessage<TKey, TValue> message,
        CancellationToken cancellationToken)
    {
        // Rent completion source from pool - it will auto-return when awaited
        var completion = _valueTaskSourcePool.Rent();

        var workItem = new ProduceWorkItem<TKey, TValue>(message, completion, cancellationToken);

        // Write to channel (backpressure if full)
        await _workChannel.Writer.WriteAsync(workItem, cancellationToken).ConfigureAwait(false);

        // Await the result - source auto-returns to pool when GetResult() is called
        return await completion.Task.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Send(ProducerMessage<TKey, TValue> message)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));

        // Fast path: Try synchronous fire-and-forget produce if metadata is cached
        // This bypasses PooledValueTaskSource rental entirely for fire-and-forget operations
        if (TryProduceSyncFireAndForget(message))
        {
            return;
        }

        // Slow path: Fall back to channel-based async processing
        // This handles the case where metadata isn't initialized or cached
        var completion = _valueTaskSourcePool.Rent();
        var workItem = new ProduceWorkItem<TKey, TValue>(message, completion, CancellationToken.None);

        if (!_workChannel.Writer.TryWrite(workItem))
        {
            completion.TrySetException(new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>)));
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));
        }
    }

    /// <inheritdoc />
    public void Send(string topic, TKey? key, TValue value)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));

        // Fast path: Try synchronous fire-and-forget produce if metadata is cached
        // This bypasses both ProducerMessage allocation and PooledValueTaskSource rental
        if (TryProduceSyncFireAndForgetDirect(topic, key, value, partition: null, timestamp: null, headers: null))
        {
            return;
        }

        // Slow path: Fall back to the message-based overload
        // This allocates a ProducerMessage but handles metadata initialization
        Send(new ProducerMessage<TKey, TValue> { Topic = topic, Key = key, Value = value });
    }

    /// <summary>
    /// Attempts synchronous fire-and-forget produce directly from parameters when metadata is cached.
    /// This is the fastest path as it avoids both ProducerMessage and PooledValueTaskSource allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryProduceSyncFireAndForgetDirect(
        string topic,
        TKey? key,
        TValue value,
        int? partition,
        DateTimeOffset? timestamp,
        Headers? headers)
    {
        // FAST PATH: Check thread-local cached topic metadata.
        // Avoids MetadataManager dictionary lookup and IsStale() check for consecutive
        // messages to the same topic. Revalidate staleness periodically (~1 second).
        if (TryGetCachedTopicInfo(topic, out var topicInfo))
        {
            try
            {
                ProduceSyncCoreFireAndForgetDirect(topic, key, value, partition, timestamp, headers, topicInfo!);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Fire-and-forget produce failed for topic {Topic}", topic);
            }
            return true;
        }

        // SLOW PATH: Full metadata check with caching
        // Check if metadata is initialized (sync check)
        if (_metadataManager.Metadata.LastRefreshed == default)
        {
            return false; // Need async initialization
        }

        // Try to get topic metadata from cache
        if (!_metadataManager.TryGetCachedTopicMetadata(topic, out topicInfo) || topicInfo is null)
        {
            return false; // Cache miss, need async refresh
        }

        if (topicInfo.PartitionCount == 0)
        {
            return false; // Invalid topic state, let async path handle error
        }

        // Update thread-local cache for next call
        UpdateCachedTopicInfo(topic, topicInfo);

        // All checks passed - we can proceed synchronously without completion tracking
        try
        {
            ProduceSyncCoreFireAndForgetDirect(topic, key, value, partition, timestamp, headers, topicInfo);
        }
        catch (Exception ex)
        {
            // Fire-and-forget: swallow exception but log for diagnostics
            // This matches Confluent.Kafka behavior where Produce() doesn't throw
            // for production errors in fire-and-forget mode
            _logger?.LogDebug(ex, "Fire-and-forget produce failed for topic {Topic}", topic);
        }
        return true;
    }

    /// <summary>
    /// Tries to get cached topic info from thread-local cache.
    /// Returns true if valid cached metadata exists, false if cache miss or expired.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetCachedTopicInfo(string topic, out TopicInfo? topicInfo)
    {
        topicInfo = null;

        // Early exit if disposed - prevents using stale cache from disposed producer
        if (_disposed)
            return false;

        // Check if cache is for this metadata manager, topic, and still valid
        // Use signed comparison to handle TickCount64 wraparound (every ~292 million years)
        var currentTicks = Environment.TickCount64;
        if (t_cachedMetadataManager == _metadataManager &&
            t_cachedTopicName == topic &&
            t_cachedTopicInfo is not null &&
            (t_cachedTopicValidUntilTicks - currentTicks) > 0)
        {
            topicInfo = t_cachedTopicInfo;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Updates the thread-local topic metadata cache.
    /// Cache validity is ~1 second, which is acceptable since metadata is typically
    /// valid for minutes and the async path handles refresh if truly stale.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateCachedTopicInfo(string topic, TopicInfo topicInfo)
    {
        t_cachedMetadataManager = _metadataManager;
        t_cachedTopicName = topic;
        t_cachedTopicInfo = topicInfo;
        // Cache valid for ~1 second (1000 ticks) - enough for high-throughput bursts
        // while still detecting stale metadata reasonably quickly
        t_cachedTopicValidUntilTicks = Environment.TickCount64 + 1000;
    }

    /// <summary>
    /// Core synchronous fire-and-forget produce logic that works directly from parameters.
    /// Avoids ProducerMessage allocation entirely.
    ///
    /// Design: True fast path / slow path split
    /// - Fast path: Check cached batch, append if space available (no side effects)
    /// - Slow path: Handle all complexity (batch creation, rotation, dictionary ops)
    /// </summary>
    private void ProduceSyncCoreFireAndForgetDirect(
        string topic,
        TKey? keyObj,
        TValue value,
        int? partitionOverride,
        DateTimeOffset? timestampOverride,
        Headers? headers,
        TopicInfo topicInfo)
    {
        // Step 1: Serialize to thread-local buffers (no allocation, reused across messages)
        var keyIsNull = keyObj is null;
        int keyLength = 0;

        if (!keyIsNull)
        {
            var keyWriter = new ReusableBufferWriter(ref t_keySerializationBuffer, DefaultKeyBufferSize);
            t_serializationContext.Topic = topic;
            t_serializationContext.Component = SerializationComponent.Key;
            t_serializationContext.Headers = headers;
            _keySerializer.Serialize(keyObj!, ref keyWriter, t_serializationContext);
            keyWriter.UpdateBufferRef(ref t_keySerializationBuffer);
            keyLength = keyWriter.WrittenCount;
        }

        var valueWriter = new ReusableBufferWriter(ref t_valueSerializationBuffer, DefaultValueBufferSize);
        t_serializationContext.Topic = topic;
        t_serializationContext.Component = SerializationComponent.Value;
        t_serializationContext.Headers = headers;
        _valueSerializer.Serialize(value, ref valueWriter, t_serializationContext);
        valueWriter.UpdateBufferRef(ref t_valueSerializationBuffer);
        var valueLength = valueWriter.WrittenCount;

        // Step 2: Compute partition using serialized key bytes
        var keySpan = keyIsNull ? ReadOnlySpan<byte>.Empty : t_keySerializationBuffer.AsSpan(0, keyLength);
        var partition = partitionOverride
            ?? _partitioner.Partition(topic, keySpan, keyIsNull, topicInfo.PartitionCount);

        // Step 3: Get timestamp
        var timestampMs = timestampOverride?.ToUnixTimeMilliseconds() ?? GetFastTimestampMs();

        // Step 4: Convert headers
        IReadOnlyList<RecordHeader>? recordHeaders = null;
        RecordHeader[]? pooledHeaderArray = null;
        if (headers is not null && headers.Count > 0)
        {
            recordHeaders = ConvertHeaders(headers, out pooledHeaderArray);
        }

        // Step 5: FAST PATH - Try to append to cached batch with arena (no side effects)
        // This succeeds when: same partition as recent message AND batch has space
        if (TryAppendToArenaFast(topic, partition, timestampMs, keyIsNull, keyLength, valueLength, recordHeaders, ref pooledHeaderArray))
        {
            _statisticsCollector.RecordMessageProducedFast(keyLength + valueLength);
            return;
        }

        // Step 6: SLOW PATH - Handle all complexity
        AppendWithSlowPath(topic, partition, timestampMs, keyIsNull, keyLength, valueLength, recordHeaders, pooledHeaderArray);
        _statisticsCollector.RecordMessageProducedFast(keyLength + valueLength);
    }

    /// <summary>
    /// FAST PATH: Try to append to an existing batch's arena.
    /// No side effects - if anything is wrong, just return false.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryAppendToArenaFast(
        string topic,
        int partition,
        long timestampMs,
        bool keyIsNull,
        int keyLength,
        int valueLength,
        IReadOnlyList<RecordHeader>? recordHeaders,
        ref RecordHeader[]? pooledHeaderArray)
    {
        // Check thread-local batch cache first (avoids dictionary lookup)
        var cachedBatch = GetCachedBatch(topic, partition);
        if (cachedBatch is null)
        {
            return false; // No cached batch, use slow path
        }

        var arena = cachedBatch.Arena;
        if (arena is null)
        {
            return false; // Batch completed, use slow path
        }

        // Calculate total size needed and check if it fits
        var totalSize = keyLength + valueLength;
        if (arena.RemainingCapacity < totalSize)
        {
            return false; // Not enough space, use slow path (which will rotate)
        }

        // Allocate key in arena
        ArenaSlice keySlice = default;
        if (!keyIsNull && keyLength > 0)
        {
            if (!arena.TryAllocate(keyLength, out var keySpan, out var keyOffset))
            {
                return false; // Allocation failed, use slow path
            }
            t_keySerializationBuffer.AsSpan(0, keyLength).CopyTo(keySpan);
            keySlice = new ArenaSlice(keyOffset, keyLength);
        }

        // Allocate value in arena
        if (!arena.TryAllocate(valueLength, out var valueSpan, out var valueOffset))
        {
            return false; // Allocation failed (shouldn't happen after size check, but be safe)
        }
        t_valueSerializationBuffer.AsSpan(0, valueLength).CopyTo(valueSpan);
        var valueSlice = new ArenaSlice(valueOffset, valueLength);

        // Append using arena-based method
        var result = cachedBatch.TryAppendFromArena(timestampMs, keySlice, keyIsNull, valueSlice, recordHeaders, pooledHeaderArray);

        if (!result.Success)
        {
            return false; // Batch full or completed, use slow path
        }

        // Success - batch now owns the pooled header array
        pooledHeaderArray = null;
        return true;
    }

    /// <summary>
    /// Gets a cached batch for the given topic/partition from thread-local cache.
    /// Returns null if no valid cached batch exists.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PartitionBatch? GetCachedBatch(string topic, int partition)
    {
        // Use multi-partition cache from accumulator
        if (!_accumulator.TryGetBatch(topic, partition, out var batch))
        {
            return null;
        }
        return batch;
    }

    /// <summary>
    /// SLOW PATH: Handles all complexity - batch creation, rotation, dictionary operations.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AppendWithSlowPath(
        string topic,
        int partition,
        long timestampMs,
        bool keyIsNull,
        int keyLength,
        int valueLength,
        IReadOnlyList<RecordHeader>? recordHeaders,
        RecordHeader[]? pooledHeaderArray)
    {
        var key = PooledMemory.Null;
        var valueMemory = PooledMemory.Null;

        try
        {
            // Copy key from thread-local to pooled array
            if (!keyIsNull && keyLength > 0)
            {
                var keyArray = ArrayPool<byte>.Shared.Rent(keyLength);
                t_keySerializationBuffer.AsSpan(0, keyLength).CopyTo(keyArray);
                key = new PooledMemory(keyArray, keyLength);
            }

            // Copy value from thread-local to pooled array
            if (valueLength > 0)
            {
                var valueArray = ArrayPool<byte>.Shared.Rent(valueLength);
                t_valueSerializationBuffer.AsSpan(0, valueLength).CopyTo(valueArray);
                valueMemory = new PooledMemory(valueArray, valueLength);
            }

            // Append to accumulator
            if (!_accumulator.TryAppendFireAndForget(
                topic,
                partition,
                timestampMs,
                key,
                valueMemory,
                recordHeaders,
                pooledHeaderArray))
            {
                CleanupPooledResources(key, valueMemory, pooledHeaderArray);
                throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));
            }

            // Success - ownership transferred, clear local refs
            key = PooledMemory.Null;
            valueMemory = PooledMemory.Null;
        }
        catch
        {
            CleanupPooledResources(key, valueMemory, pooledHeaderArray);
            throw;
        }
    }

    /// <summary>
    /// Attempts synchronous fire-and-forget produce when metadata is initialized and cached.
    /// This is the fastest path for fire-and-forget operations as it:
    /// 1. Does NOT rent a PooledValueTaskSource
    /// 2. Does NOT store the completion source in the batch
    /// 3. Completely bypasses result tracking overhead
    /// Returns true if successful, false if async path is needed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryProduceSyncFireAndForget(ProducerMessage<TKey, TValue> message)
    {
        // FAST PATH: Check thread-local cached topic metadata.
        if (TryGetCachedTopicInfo(message.Topic, out var topicInfo))
        {
            try
            {
                ProduceSyncCoreFireAndForget(message, topicInfo!);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Fire-and-forget produce failed for topic {Topic}", message.Topic);
            }
            return true;
        }

        // SLOW PATH: Full metadata check with caching
        // Check if metadata is initialized (sync check)
        if (_metadataManager.Metadata.LastRefreshed == default)
        {
            return false; // Need async initialization
        }

        // Try to get topic metadata from cache
        if (!_metadataManager.TryGetCachedTopicMetadata(message.Topic, out topicInfo) || topicInfo is null)
        {
            return false; // Cache miss, need async refresh
        }

        if (topicInfo.PartitionCount == 0)
        {
            return false; // Invalid topic state, let async path handle error
        }

        // Update thread-local cache for next call
        UpdateCachedTopicInfo(message.Topic, topicInfo);

        // All checks passed - we can proceed synchronously without completion tracking
        try
        {
            ProduceSyncCoreFireAndForget(message, topicInfo);
        }
        catch (Exception ex)
        {
            // Fire-and-forget: swallow exception but log for diagnostics
            // This matches Confluent.Kafka behavior where Produce() doesn't throw
            // for production errors in fire-and-forget mode
            _logger?.LogDebug(ex, "Fire-and-forget produce failed for topic {Topic}", message.Topic);
        }
        return true;
    }

    /// <summary>
    /// Core synchronous fire-and-forget produce logic that skips completion source tracking.
    /// This eliminates the overhead of PooledValueTaskSource rental and result handling.
    /// Delegates to the direct parameters version to share the optimized arena path.
    /// </summary>
    private void ProduceSyncCoreFireAndForget(
        ProducerMessage<TKey, TValue> message,
        TopicInfo topicInfo)
    {
        // Delegate to the direct parameters version which has the optimized arena path
        ProduceSyncCoreFireAndForgetDirect(
            message.Topic,
            message.Key,
            message.Value,
            message.Partition,
            message.Timestamp,
            message.Headers,
            topicInfo);
    }

    /// <summary>
    /// Attempts synchronous produce when metadata is initialized and cached.
    /// Returns true if successful, false if async path is needed.
    /// For fire-and-forget, exceptions are captured in the completion source, not thrown.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryProduceSync(ProducerMessage<TKey, TValue> message)
    {
        // Check if metadata is initialized (sync check)
        if (_metadataManager.Metadata.LastRefreshed == default)
        {
            return false; // Need async initialization
        }

        // Try to get topic metadata from cache
        if (!_metadataManager.TryGetCachedTopicMetadata(message.Topic, out var topicInfo) || topicInfo is null)
        {
            return false; // Cache miss, need async refresh
        }

        if (topicInfo.PartitionCount == 0)
        {
            return false; // Invalid topic state, let async path handle error
        }

        // All checks passed - we can proceed synchronously
        var completion = _valueTaskSourcePool.Rent();
        try
        {
            ProduceSyncCore(message, topicInfo, completion);
        }
        catch (Exception ex)
        {
            // Fire-and-forget: capture exception in completion source, don't throw
            // This matches Confluent.Kafka behavior where Produce() doesn't throw
            // for production errors - they're delivered via delivery report callback
            completion.TrySetException(ex);
        }
        return true;
    }

    /// <summary>
    /// Core synchronous produce logic shared between TryProduceSync and TryProduceSyncWithHandler.
    /// Handles serialization, partitioning, and accumulator append with proper resource cleanup.
    /// </summary>
    private void ProduceSyncCore(
        ProducerMessage<TKey, TValue> message,
        TopicInfo topicInfo,
        PooledValueTaskSource<RecordMetadata> completion)
    {
        var key = PooledMemory.Null;
        var value = PooledMemory.Null;
        RecordHeader[]? pooledHeaderArray = null;

        try
        {
            // Serialize key and value
            var keyIsNull = message.Key is null;
            key = keyIsNull ? PooledMemory.Null : SerializeKeyToPooled(message.Key!, message.Topic, message.Headers);
            value = SerializeValueToPooled(message.Value, message.Topic, message.Headers);

            // Determine partition
            var partition = message.Partition
                ?? _partitioner.Partition(message.Topic, key.Span, keyIsNull, topicInfo.PartitionCount);

            // Get timestamp - use fast cached timestamp when no override provided
            var timestampMs = message.Timestamp?.ToUnixTimeMilliseconds() ?? GetFastTimestampMs();

            // Convert headers
            IReadOnlyList<RecordHeader>? recordHeaders = null;
            if (message.Headers is not null && message.Headers.Count > 0)
            {
                recordHeaders = ConvertHeaders(message.Headers, out pooledHeaderArray);
            }

            // Append to accumulator synchronously
            if (!_accumulator.TryAppendSync(
                message.Topic,
                partition,
                timestampMs,
                key,
                value,
                recordHeaders,
                pooledHeaderArray,
                completion))
            {
                // Accumulator is disposed - cleanup and throw
                CleanupPooledResources(key, value, pooledHeaderArray);
                var disposedException = new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));
                completion.TrySetException(disposedException);
                throw disposedException;
            }

            // Track message produced
            var messageBytes = key.Length + value.Length;
            _statisticsCollector.RecordMessageProduced(message.Topic, partition, messageBytes);
        }
        catch (Exception ex) when (ex is not ObjectDisposedException)
        {
            // Cleanup resources on any exception (except ObjectDisposedException which already cleaned up)
            CleanupPooledResources(key, value, pooledHeaderArray);
            completion.TrySetException(ex);
            throw;
        }
    }

    /// <summary>
    /// Cleans up pooled resources in exception paths.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CleanupPooledResources(PooledMemory key, PooledMemory value, RecordHeader[]? pooledHeaderArray)
    {
        key.Return();
        value.Return();
        if (pooledHeaderArray is not null)
        {
            ArrayPool<RecordHeader>.Shared.Return(pooledHeaderArray);
        }
    }

    /// <inheritdoc />
    public void Send(ProducerMessage<TKey, TValue> message, Action<RecordMetadata, Exception?> deliveryHandler)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));

        ArgumentNullException.ThrowIfNull(deliveryHandler);

        // Fast path: Try synchronous produce if metadata is initialized and cached
        if (TryProduceSyncWithHandler(message, deliveryHandler))
        {
            return;
        }

        // Slow path: Fall back to channel-based async processing
        var completion = _valueTaskSourcePool.Rent();
        completion.SetDeliveryHandler(deliveryHandler);
        _ = AwaitDeliveryAsync(completion);

        var workItem = new ProduceWorkItem<TKey, TValue>(message, completion, CancellationToken.None);

        if (!_workChannel.Writer.TryWrite(workItem))
        {
            completion.TrySetException(new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>)));
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));
        }
    }

    /// <summary>
    /// Attempts synchronous produce with delivery handler when metadata is initialized and cached.
    /// Returns true if successful, false if async path is needed.
    /// For fire-and-forget, exceptions are delivered via the callback, not thrown.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryProduceSyncWithHandler(ProducerMessage<TKey, TValue> message, Action<RecordMetadata, Exception?> deliveryHandler)
    {
        // Check if metadata is initialized
        if (_metadataManager.Metadata.LastRefreshed == default)
        {
            return false;
        }

        // Try to get topic metadata from cache
        if (!_metadataManager.TryGetCachedTopicMetadata(message.Topic, out var topicInfo) || topicInfo is null)
        {
            return false;
        }

        if (topicInfo.PartitionCount == 0)
        {
            return false;
        }

        // All checks passed - proceed synchronously
        var completion = _valueTaskSourcePool.Rent();
        completion.SetDeliveryHandler(deliveryHandler);
        _ = AwaitDeliveryAsync(completion);

        try
        {
            ProduceSyncCore(message, topicInfo, completion);
        }
        catch (Exception ex)
        {
            // Fire-and-forget: capture exception in completion source
            // The delivery handler will be invoked with the exception
            completion.TrySetException(ex);
        }
        return true;
    }

    /// <summary>
    /// Awaits the completion source to trigger the delivery handler and auto-return to pool.
    /// </summary>
    private static async Task AwaitDeliveryAsync(PooledValueTaskSource<RecordMetadata> completion)
    {
        try
        {
            // Awaiting triggers GetResult() which invokes the handler and returns to pool
            await completion.Task.ConfigureAwait(false);
        }
        catch
        {
            // Exception is passed to handler via GetResult() - swallow here
        }
    }

    private async Task ProcessWorkAsync(CancellationToken cancellationToken)
    {
        await foreach (var work in _workChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                // ProduceInternalAsync adds the completion to the batch
                // The batch will complete it when sent - no need to set result here
                await ProduceInternalAsync(work.Message, work.Completion, work.CancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (work.CancellationToken.IsCancellationRequested)
            {
                work.Completion.TrySetCanceled(work.CancellationToken);
            }
            catch (Exception ex)
            {
                work.Completion.TrySetException(ex);
            }
        }
    }

    private async ValueTask ProduceInternalAsync(
        ProducerMessage<TKey, TValue> message,
        PooledValueTaskSource<RecordMetadata> completion,
        CancellationToken cancellationToken)
    {
        // Ensure metadata is initialized
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        // Fast path: try to get topic metadata from cache synchronously
        // This avoids async state machine overhead for 99%+ of calls
        TopicInfo? topicInfo;
        if (!_metadataManager.TryGetCachedTopicMetadata(message.Topic, out topicInfo))
        {
            // Slow path: cache miss, need async refresh
            topicInfo = await _metadataManager.GetTopicMetadataAsync(message.Topic, cancellationToken)
                .ConfigureAwait(false);
        }

        if (topicInfo is null)
        {
            throw new InvalidOperationException($"Topic '{message.Topic}' not found");
        }

        if (topicInfo.PartitionCount == 0)
        {
            throw new InvalidOperationException($"Topic '{message.Topic}' has no partitions. Error code: {topicInfo.ErrorCode}");
        }

        // Serialize key and value to pooled memory (returned to pool when batch completes)
        var keyIsNull = message.Key is null;
        var key = keyIsNull ? PooledMemory.Null : SerializeKeyToPooled(message.Key!, message.Topic, message.Headers);
        var value = SerializeValueToPooled(message.Value, message.Topic, message.Headers);

        // Determine partition
        var partition = message.Partition
            ?? _partitioner.Partition(message.Topic, key.Span, keyIsNull, topicInfo.PartitionCount);

        // Get timestamp
        var timestamp = message.Timestamp ?? DateTimeOffset.UtcNow;
        var timestampMs = timestamp.ToUnixTimeMilliseconds();

        // Convert headers with minimal allocations
        IReadOnlyList<RecordHeader>? recordHeaders = null;
        RecordHeader[]? pooledHeaderArray = null;
        if (message.Headers is not null && message.Headers.Count > 0)
        {
            recordHeaders = ConvertHeaders(message.Headers, out pooledHeaderArray);
        }

        // Append to accumulator - passes completion through to batch
        // The batch will complete the TCS when sent, so we don't await anything here
        // Pass topic and partition separately to avoid TopicPartition allocation
        var result = await _accumulator.AppendAsync(
            message.Topic,
            partition,
            timestampMs,
            key,
            value,
            recordHeaders,
            pooledHeaderArray,
            completion,
            cancellationToken).ConfigureAwait(false);

        if (!result.Success)
        {
            // Return pooled array before throwing to avoid resource leak
            if (pooledHeaderArray is not null)
            {
                ArrayPool<RecordHeader>.Shared.Return(pooledHeaderArray);
            }
            throw new InvalidOperationException("Failed to append record");
        }

        // Track message produced (key + value bytes)
        var messageBytes = key.Length + value.Length;
        _statisticsCollector.RecordMessageProduced(message.Topic, partition, messageBytes);

        // No await here - completion will be set by the batch when it's sent
    }

    public ValueTask<RecordMetadata> ProduceAsync(
        string topic,
        TKey? key,
        TValue value,
        CancellationToken cancellationToken = default)
    {
        return ProduceAsync(new ProducerMessage<TKey, TValue>
        {
            Topic = topic,
            Key = key,
            Value = value
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RecordMetadata[]> ProduceAllAsync(
        IEnumerable<ProducerMessage<TKey, TValue>> messages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        // Convert to list to get count and allow multiple enumeration
        var messageList = messages as IList<ProducerMessage<TKey, TValue>> ?? messages.ToList();
        if (messageList.Count == 0)
        {
            return [];
        }

        // Convert ValueTask to Task for each message and await all
        var tasks = new Task<RecordMetadata>[messageList.Count];
        for (var i = 0; i < messageList.Count; i++)
        {
            tasks[i] = ProduceAsync(messageList[i], cancellationToken).AsTask();
        }

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<RecordMetadata[]> ProduceAllAsync(
        string topic,
        IEnumerable<(TKey? Key, TValue Value)> messages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(messages);

        // Convert to list to get count and allow multiple enumeration
        var messageList = messages as IList<(TKey? Key, TValue Value)> ?? messages.ToList();
        if (messageList.Count == 0)
        {
            return [];
        }

        // Convert ValueTask to Task for each message and await all
        var tasks = new Task<RecordMetadata>[messageList.Count];
        for (var i = 0; i < messageList.Count; i++)
        {
            var (key, value) = messageList[i];
            tasks[i] = ProduceAsync(topic, key, value, cancellationToken).AsTask();
        }

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        await _accumulator.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public ITransaction<TKey, TValue> BeginTransaction()
    {
        if (string.IsNullOrEmpty(_options.TransactionalId))
        {
            throw new InvalidOperationException("Producer is not transactional. Set TransactionalId in options.");
        }

        return new Transaction<TKey, TValue>(this);
    }

    public ValueTask InitTransactionsAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Implement transaction initialization
        throw new NotImplementedException();
    }

    private async Task SenderLoopAsync(CancellationToken cancellationToken)
    {
        await foreach (var batch in _accumulator.GetReadyBatchesAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                await SendBatchAsync(batch, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send batch to {Topic}-{Partition}",
                    batch.TopicPartition.Topic, batch.TopicPartition.Partition);

                // Track batch failure (if not already tracked in SendBatchAsync)
                // This handles cases where the exception occurred before SendBatchAsync could track it
                _statisticsCollector.RecordBatchFailed(
                    batch.TopicPartition.Topic,
                    batch.TopicPartition.Partition,
                    batch.CompletionSourcesCount);

                batch.Fail(ex);
            }
        }
    }

    private async Task LingerLoopAsync(CancellationToken cancellationToken)
    {
        // Use PeriodicTimer instead of Task.Delay to avoid allocations on each tick.
        // PeriodicTimer.WaitForNextTickAsync is allocation-free after the timer is constructed.
        try
        {
            while (await _lingerTimer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    await _accumulator.ExpireLingerAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in linger loop");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during shutdown
        }
    }

    private async Task SendBatchAsync(ReadyBatch batch, CancellationToken cancellationToken)
    {
        var leader = await _metadataManager.GetPartitionLeaderAsync(
            batch.TopicPartition.Topic,
            batch.TopicPartition.Partition,
            cancellationToken).ConfigureAwait(false);

        if (leader is null)
        {
            throw new InvalidOperationException(
                $"No leader for {batch.TopicPartition.Topic}-{batch.TopicPartition.Partition}");
        }

        var connection = await _connectionPool.GetConnectionAsync(leader.NodeId, cancellationToken)
            .ConfigureAwait(false);

        // Ensure API version is negotiated (thread-safe initialization)
        var apiVersion = _produceApiVersion;
        if (apiVersion < 0)
        {
            apiVersion = _metadataManager.GetNegotiatedApiVersion(
                ApiKey.Produce,
                ProduceRequest.LowestSupportedVersion,
                ProduceRequest.HighestSupportedVersion);
            // Use Interlocked to avoid racing with other threads
            Interlocked.CompareExchange(ref _produceApiVersion, apiVersion, -1);
            // Re-read in case another thread won the race
            apiVersion = _produceApiVersion;
        }

        // Capture topic name locally to ensure it doesn't change
        var expectedTopic = batch.TopicPartition.Topic;
        var expectedPartition = batch.TopicPartition.Partition;

        var request = new ProduceRequest
        {
            Acks = (short)_options.Acks,
            TimeoutMs = _options.RequestTimeoutMs,
            TransactionalId = _options.TransactionalId,
            TopicData =
            [
                new ProduceRequestTopicData
                {
                    Name = expectedTopic,
                    PartitionData =
                    [
                        new ProduceRequestPartitionData
                        {
                            Index = expectedPartition,
                            Records = [batch.RecordBatch],
                            Compression = _options.CompressionType
                        }
                    ]
                }
            ]
        };

        // Sanity check: verify the request was built correctly
        System.Diagnostics.Debug.Assert(
            request.TopicData[0].Name == expectedTopic,
            $"Request topic mismatch: expected '{expectedTopic}', got '{request.TopicData[0].Name}'");

        var messageCount = batch.CompletionSourcesCount;
        var requestStartTime = DateTimeOffset.UtcNow;

        // Track request sent
        _statisticsCollector.RecordRequestSent();

        // Handle Acks.None (fire-and-forget) - broker doesn't send response
        if (_options.Acks == Acks.None)
        {
            await connection.SendFireAndForgetAsync<ProduceRequest, ProduceResponse>(
                request,
                (short)apiVersion,
                cancellationToken).ConfigureAwait(false);

            // Track batch delivered (fire-and-forget assumes success)
            _statisticsCollector.RecordBatchDelivered(
                batch.TopicPartition.Topic,
                batch.TopicPartition.Partition,
                messageCount);

            // Complete with synthetic metadata since we don't get a response
            // Offset is unknown (-1) for fire-and-forget
            batch.Complete(-1, DateTimeOffset.UtcNow);
            return;
        }

        var response = await connection.SendAsync<ProduceRequest, ProduceResponse>(
            request,
            (short)apiVersion,
            cancellationToken).ConfigureAwait(false);

        // Track response received with latency
        var latencyMs = (long)(DateTimeOffset.UtcNow - requestStartTime).TotalMilliseconds;
        _statisticsCollector.RecordResponseReceived(latencyMs);

        // Process response - use imperative loops to avoid LINQ allocations
        ProduceResponseTopicData? topicResponse = null;
        foreach (var topic in response.Responses)
        {
            if (topic.Name == expectedTopic)
            {
                topicResponse = topic;
                break;
            }
        }

        ProduceResponsePartitionData? partitionResponse = null;
        if (topicResponse is not null)
        {
            foreach (var partition in topicResponse.PartitionResponses)
            {
                if (partition.Index == expectedPartition)
                {
                    partitionResponse = partition;
                    break;
                }
            }
        }

        if (partitionResponse is null)
        {
            // Track batch failed
            _statisticsCollector.RecordBatchFailed(
                batch.TopicPartition.Topic,
                batch.TopicPartition.Partition,
                messageCount);

            // Build diagnostic message (avoid LINQ for zero-allocation principle)
            var sb = new System.Text.StringBuilder();
            sb.Append("No response for partition. Expected: ");
            sb.Append(expectedTopic);
            sb.Append('[');
            sb.Append(expectedPartition);
            sb.Append("], Request topic: ");
            sb.Append(request.TopicData[0].Name);
            sb.Append('[');
            sb.Append(request.TopicData[0].PartitionData[0].Index);
            sb.Append("], Response: ");
            for (var i = 0; i < response.Responses.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                var t = response.Responses[i];
                sb.Append(t.Name);
                sb.Append('[');
                for (var j = 0; j < t.PartitionResponses.Count; j++)
                {
                    if (j > 0) sb.Append(',');
                    sb.Append(t.PartitionResponses[j].Index);
                }
                sb.Append(']');
            }
            sb.Append(". API version: ");
            sb.Append(apiVersion);
            sb.Append(", Broker: ");
            sb.Append(connection.Host);
            sb.Append(':');
            sb.Append(connection.Port);
            sb.Append(", Connection #");
            sb.Append(connection.ConnectionInstanceId);

            throw new InvalidOperationException(sb.ToString());
        }

        if (partitionResponse.ErrorCode != ErrorCode.None)
        {
            // Track batch failed
            _statisticsCollector.RecordBatchFailed(
                batch.TopicPartition.Topic,
                batch.TopicPartition.Partition,
                messageCount);
            throw new KafkaException(partitionResponse.ErrorCode,
                $"Produce failed: {partitionResponse.ErrorCode}");
        }

        // Track batch delivered
        _statisticsCollector.RecordBatchDelivered(
            batch.TopicPartition.Topic,
            batch.TopicPartition.Partition,
            messageCount);

        var timestamp = partitionResponse.LogAppendTimeMs > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(partitionResponse.LogAppendTimeMs)
            : DateTimeOffset.UtcNow;

        batch.Complete(partitionResponse.BaseOffset, timestamp);
    }

    /// <summary>
    /// Ensures metadata is initialized. Uses inline check to avoid async state machine
    /// overhead when metadata is already initialized (which is 99%+ of calls).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        // Fast path: metadata already initialized - return completed ValueTask (no allocation)
        if (_metadataManager.Metadata.LastRefreshed != default)
        {
            return ValueTask.CompletedTask;
        }

        // Slow path: need to initialize metadata
        return _metadataManager.InitializeAsync(cancellationToken);
    }

    /// <summary>
    /// Serializes the key using a thread-local reusable buffer.
    /// Avoids PooledBufferWriter creation per message by reusing the same buffer.
    /// Data is copied to a right-sized pooled buffer for the batch.
    /// </summary>
    private PooledMemory SerializeKeyToPooled(TKey key, string topic, Headers? headers)
    {
        // Use thread-local buffer to avoid per-message allocation
        var writer = new ReusableBufferWriter(ref t_keySerializationBuffer, DefaultKeyBufferSize);

        // Reuse thread-local context by updating fields (zero-allocation)
        t_serializationContext.Topic = topic;
        t_serializationContext.Component = SerializationComponent.Key;
        t_serializationContext.Headers = headers;
        _keySerializer.Serialize(key, ref writer, t_serializationContext);

        // Update buffer ref in case it grew during serialization
        writer.UpdateBufferRef(ref t_keySerializationBuffer);

        // Copy to right-sized pooled buffer for batch storage
        return writer.ToPooledMemory();
    }

    /// <summary>
    /// Serializes the value using a thread-local reusable buffer.
    /// Avoids PooledBufferWriter creation per message by reusing the same buffer.
    /// Data is copied to a right-sized pooled buffer for the batch.
    /// </summary>
    private PooledMemory SerializeValueToPooled(TValue value, string topic, Headers? headers)
    {
        // Use thread-local buffer to avoid per-message allocation
        var writer = new ReusableBufferWriter(ref t_valueSerializationBuffer, DefaultValueBufferSize);

        // Reuse thread-local context by updating fields (zero-allocation)
        t_serializationContext.Topic = topic;
        t_serializationContext.Component = SerializationComponent.Value;
        t_serializationContext.Headers = headers;
        _valueSerializer.Serialize(value, ref writer, t_serializationContext);

        // Update buffer ref in case it grew during serialization
        writer.UpdateBufferRef(ref t_valueSerializationBuffer);

        // Copy to right-sized pooled buffer for batch storage
        return writer.ToPooledMemory();
    }

    /// <summary>
    /// Gets a fast cached timestamp in milliseconds for fire-and-forget operations.
    /// Refreshes the cache approximately every millisecond to balance accuracy and performance.
    /// This is ~10x faster than DateTimeOffset.UtcNow for high-throughput scenarios.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long GetFastTimestampMs()
    {
        // Use Environment.TickCount64 (cheap counter) to determine if we need to refresh.
        // TickCount64 increments every ~15.6ms on Windows, ~1ms on Linux, but checking
        // the difference is still much cheaper than calling DateTimeOffset.UtcNow.
        var currentTicks = Environment.TickCount64;

        // Refresh if more than ~1ms has passed (or on first call when t_cachedTimestampTicks is 0)
        if (currentTicks - t_cachedTimestampTicks > 1 || t_cachedTimestampTicks == 0)
        {
            t_cachedTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            t_cachedTimestampTicks = currentTicks;
        }

        return t_cachedTimestampMs;
    }

    /// <summary>
    /// Converts Headers to RecordHeaders with minimal allocations.
    /// Always uses ArrayPool to avoid per-message heap allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IReadOnlyList<RecordHeader> ConvertHeaders(Headers headers, out RecordHeader[]? pooledArray)
    {
        var count = headers.Count;

        // Always pool to eliminate per-message allocations
        var result = ArrayPool<RecordHeader>.Shared.Rent(count);
        pooledArray = result;

        // Use index-based iteration to avoid enumerator boxing allocation
        for (var i = 0; i < count; i++)
        {
            var h = headers[i];
            result[i] = new RecordHeader
            {
                Key = h.Key,
                Value = h.Value,
                IsValueNull = h.IsValueNull
            };
        }

        // Always wrap in HeaderListWrapper to expose only the valid portion
        return new HeaderListWrapper(result, count);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Graceful shutdown: flush all pending messages to Kafka before closing.
        // CloseTimeoutMs controls how long to wait (0 = no timeout, wait indefinitely).
        var hasTimeout = _options.CloseTimeoutMs > 0;
        using var shutdownCts = hasTimeout
            ? new CancellationTokenSource(TimeSpan.FromMilliseconds(_options.CloseTimeoutMs))
            : new CancellationTokenSource();
        var gracefulShutdown = true;

        try
        {
            // 1. Complete the work channel (no more writes accepted)
            _workChannel.Writer.Complete();

            // 2. Wait for workers to drain - they'll process remaining items and exit
            //    when the channel is empty and completed
            if (hasTimeout)
                await Task.WhenAll(_workerTasks).WaitAsync(shutdownCts.Token).ConfigureAwait(false);
            else
                await Task.WhenAll(_workerTasks).ConfigureAwait(false);

            // 3. Flush accumulator and complete its channel - sender will process remaining batches
            await _accumulator.CloseAsync(shutdownCts.Token).ConfigureAwait(false);

            // 4. Cancel linger loop (no longer needed) but let sender finish
            _senderCts.Cancel();

            // 5. Wait for sender to drain remaining batches
            if (hasTimeout)
                await _senderTask.WaitAsync(shutdownCts.Token).ConfigureAwait(false);
            else
                await _senderTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown timed out - fall back to forceful shutdown
            gracefulShutdown = false;
            _logger?.LogWarning("Graceful shutdown timed out after {Timeout}ms, forcing disposal", _options.CloseTimeoutMs);
        }
        catch
        {
            gracefulShutdown = false;
        }

        if (!gracefulShutdown)
        {
            // Forceful shutdown: cancel everything and fail pending batches
            _senderCts.Cancel();

            try
            {
                await Task.WhenAll(_workerTasks).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch
            {
                // Ignore
            }
        }

        // Wait for linger task to exit (it should be quick after cancellation)
        try
        {
            await _lingerTask.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        }
        catch
        {
            // Ignore
        }

        _senderCts.Dispose();
        _lingerTimer.Dispose();

        // Dispose statistics emitter
        if (_statisticsEmitter is not null)
        {
            await _statisticsEmitter.DisposeAsync().ConfigureAwait(false);
        }

        // Dispose accumulator - this will fail any remaining batches if graceful shutdown failed
        await _accumulator.DisposeAsync().ConfigureAwait(false);

        // Dispose ValueTaskSource pool - prevents resource leaks
        await _valueTaskSourcePool.DisposeAsync().ConfigureAwait(false);

        await _metadataManager.DisposeAsync().ConfigureAwait(false);
        await _connectionPool.DisposeAsync().ConfigureAwait(false);
    }
}

/// <summary>
/// Work item for the producer worker pool.
/// Changed from struct to class to avoid multiple copies during channel operations.
/// As a class, the work item is allocated once and a single reference is passed through the channel,
/// eliminating struct copy overhead and any potential boxing issues.
/// </summary>
internal sealed class ProduceWorkItem<TKey, TValue>
{
    public ProducerMessage<TKey, TValue> Message { get; }
    public PooledValueTaskSource<RecordMetadata> Completion { get; }
    public CancellationToken CancellationToken { get; }

    public ProduceWorkItem(
        ProducerMessage<TKey, TValue> message,
        PooledValueTaskSource<RecordMetadata> completion,
        CancellationToken cancellationToken)
    {
        Message = message;
        Completion = completion;
        CancellationToken = cancellationToken;
    }
}

/// <summary>
/// Transaction implementation.
/// </summary>
internal sealed class Transaction<TKey, TValue> : ITransaction<TKey, TValue>
{
    private readonly KafkaProducer<TKey, TValue> _producer;
    private bool _committed;
    private bool _aborted;

    public Transaction(KafkaProducer<TKey, TValue> producer)
    {
        _producer = producer;
    }

    public ValueTask<RecordMetadata> ProduceAsync(
        ProducerMessage<TKey, TValue> message,
        CancellationToken cancellationToken = default)
    {
        if (_committed || _aborted)
            throw new InvalidOperationException("Transaction is already completed");

        return _producer.ProduceAsync(message, cancellationToken);
    }

    public ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_committed || _aborted)
            throw new InvalidOperationException("Transaction is already completed");

        _committed = true;
        // TODO: Implement EndTxn
        return ValueTask.CompletedTask;
    }

    public ValueTask AbortAsync(CancellationToken cancellationToken = default)
    {
        if (_committed || _aborted)
            throw new InvalidOperationException("Transaction is already completed");

        _aborted = true;
        // TODO: Implement EndTxn
        return ValueTask.CompletedTask;
    }

    public ValueTask SendOffsetsToTransactionAsync(
        IEnumerable<TopicPartitionOffset> offsets,
        string consumerGroupId,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement TxnOffsetCommit
        throw new NotImplementedException();
    }

    public ValueTask DisposeAsync()
    {
        if (!_committed && !_aborted)
        {
            // Abort on dispose if not completed
            return AbortAsync();
        }
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// A buffer writer that writes to a provided reusable buffer.
/// Used with thread-local buffers to avoid per-message ArrayPool rentals.
/// After serialization, ToPooledMemory() copies data to a right-sized pooled buffer.
/// </summary>
internal ref struct ReusableBufferWriter : IBufferWriter<byte>
{
    private byte[] _buffer;
    private int _written;

    public ReusableBufferWriter(ref byte[]? buffer, int initialCapacity)
    {
        _buffer = buffer ??= new byte[initialCapacity];
        _written = 0;
    }

    public readonly int WrittenCount => _written;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        _written += count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsMemory(_written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsSpan(_written);
    }

    /// <summary>
    /// Copies written data to a right-sized pooled buffer and returns it as PooledMemory.
    /// The reusable buffer remains available for the next serialization.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PooledMemory ToPooledMemory()
    {
        if (_written == 0)
            return new PooledMemory(null, 0, isNull: false);

        // Rent exact-size buffer to minimize memory usage in batches
        var pooledArray = ArrayPool<byte>.Shared.Rent(_written);
        _buffer.AsSpan(0, _written).CopyTo(pooledArray);
        return new PooledMemory(pooledArray, _written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int sizeHint)
    {
        if (sizeHint < 1)
            sizeHint = 1;

        var remaining = _buffer.Length - _written;
        if (remaining < sizeHint)
        {
            Grow(sizeHint);
        }
    }

    // Maximum buffer size to prevent unbounded growth (1MB)
    private const int MaxBufferSize = 1024 * 1024;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int sizeHint)
    {
        var requiredSize = _written + sizeHint;

        // Cap growth at MaxBufferSize to prevent unbounded memory usage.
        // If a message exceeds 1MB, we still allocate enough for it but won't
        // persist that large buffer for reuse (it will be replaced on next message).
        var newSize = Math.Min(MaxBufferSize, Math.Max(_buffer.Length * 2, requiredSize));

        // If required size exceeds our cap, allocate exactly what's needed
        if (requiredSize > newSize)
        {
            newSize = requiredSize;
        }

        var newBuffer = new byte[newSize];
        _buffer.AsSpan(0, _written).CopyTo(newBuffer);
        _buffer = newBuffer;
    }

    /// <summary>
    /// Updates the caller's buffer reference if growth occurred.
    /// Call this after serialization to preserve the grown buffer for reuse.
    /// If the buffer grew beyond MaxBufferSize, it's discarded to prevent
    /// permanent memory bloat from occasional large messages.
    /// </summary>
    public void UpdateBufferRef(ref byte[]? buffer)
    {
        // Don't persist oversized buffers - let them be GC'd
        // This prevents a single large message from permanently increasing memory
        if (_buffer.Length <= MaxBufferSize)
        {
            buffer = _buffer;
        }
        // else: buffer keeps its previous (smaller) value, oversized _buffer will be GC'd
    }
}

/// <summary>
/// A buffer writer that writes directly to an ArrayPool-rented array.
/// Eliminates the double-copy overhead of using ArrayBufferWriter followed by pool rental.
/// </summary>
/// <remarks>
/// <para>
/// This ref struct implements IBufferWriter&lt;byte&gt; and manages its own pooled array.
/// When serialization is complete, call ToPooledMemory() to get ownership of the array.
/// The caller is responsible for returning the array to the pool (via PooledMemory.Return()).
/// </para>
/// <para>
/// Being a ref struct provides compile-time safety: the buffer cannot be copied, stored in
/// fields, or escape the current scope, preventing resource management issues like double-return
/// to the pool or use-after-disposal.
/// </para>
/// </remarks>
internal ref struct PooledBufferWriter : IBufferWriter<byte>
{
    private byte[]? _buffer;
    private int _written;

    /// <summary>
    /// Creates a new PooledBufferWriter with the specified initial capacity.
    /// </summary>
    /// <param name="initialCapacity">Initial buffer size. Defaults to 256 bytes.</param>
    public PooledBufferWriter(int initialCapacity = 256)
    {
        _buffer = ArrayPool<byte>.Shared.Rent(initialCapacity);
        _written = 0;
    }

    /// <summary>
    /// Gets the number of bytes written to the buffer.
    /// </summary>
    public readonly int WrittenCount => _written;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (_buffer is null)
            throw new ObjectDisposedException(nameof(PooledBufferWriter));

        if (_written + count > _buffer.Length)
            throw new InvalidOperationException("Cannot advance past the end of the buffer");

        _written += count;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        if (_buffer is null)
            throw new ObjectDisposedException(nameof(PooledBufferWriter));

        EnsureCapacity(sizeHint);
        return _buffer.AsMemory(_written);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        if (_buffer is null)
            throw new ObjectDisposedException(nameof(PooledBufferWriter));

        EnsureCapacity(sizeHint);
        return _buffer.AsSpan(_written);
    }

    /// <summary>
    /// Converts the written data to a PooledMemory and transfers ownership of the buffer.
    /// After calling this method, this PooledBufferWriter instance should not be used.
    /// </summary>
    /// <returns>A PooledMemory containing the serialized data.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if ownership has already been transferred or disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PooledMemory ToPooledMemory()
    {
        if (_buffer is null)
            throw new ObjectDisposedException(nameof(PooledBufferWriter));

        var result = new PooledMemory(_buffer, _written);
        // Clear reference - ownership transferred to caller
        _buffer = null;
        _written = 0;
        return result;
    }

    /// <summary>
    /// Returns the buffer to the pool without creating a PooledMemory.
    /// Use this in error paths to prevent leaks.
    /// </summary>
    public void Dispose()
    {
        if (_buffer is not null)
        {
            ArrayPool<byte>.Shared.Return(_buffer, clearArray: true);
            _buffer = null;
            _written = 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int sizeHint)
    {
        if (sizeHint < 1)
            sizeHint = 1;

        var remaining = _buffer!.Length - _written;
        if (remaining < sizeHint)
        {
            Grow(sizeHint);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int sizeHint)
    {
        // Calculate new size: at least double, but ensure it fits the requested size
        // Use checked arithmetic to detect overflow and cap at Array.MaxLength
        var currentLength = _buffer!.Length;
        int newSize;
        try
        {
            var doubled = checked(currentLength * 2);
            var required = checked(_written + sizeHint);
            newSize = Math.Max(doubled, required);
        }
        catch (OverflowException)
        {
            // On overflow, use the maximum array size
            newSize = Array.MaxLength;
        }

        // Ensure we don't exceed the maximum array size
        if (newSize > Array.MaxLength)
            newSize = Array.MaxLength;

        // If we can't grow anymore and current capacity isn't enough, throw
        if (newSize <= currentLength)
            throw new InvalidOperationException("Cannot grow buffer: maximum size reached.");

        var newBuffer = ArrayPool<byte>.Shared.Rent(newSize);

        // Copy existing data
        _buffer.AsSpan(0, _written).CopyTo(newBuffer);

        // Return old buffer
        ArrayPool<byte>.Shared.Return(_buffer, clearArray: true);
        _buffer = newBuffer;
    }
}

/// <summary>
/// Zero-allocation wrapper around a pooled array that implements IReadOnlyList.
/// The array is returned to the pool when the wrapper is no longer needed.
/// This struct is used to avoid List allocations in the producer hot path.
/// </summary>
internal readonly struct HeaderListWrapper : IReadOnlyList<RecordHeader>
{
    private readonly RecordHeader[] _array;
    private readonly int _count;

    public HeaderListWrapper(RecordHeader[] array, int count)
    {
        _array = array;
        _count = count;
    }

    public RecordHeader this[int index]
    {
        get
        {
            if (index < 0 || index >= _count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _array[index];
        }
    }

    public int Count => _count;

    public Enumerator GetEnumerator() => new(_array, _count);

    IEnumerator<RecordHeader> IEnumerable<RecordHeader>.GetEnumerator() => GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator : IEnumerator<RecordHeader>
    {
        private readonly RecordHeader[] _array;
        private readonly int _count;
        private int _index;

        public Enumerator(RecordHeader[] array, int count)
        {
            _array = array;
            _count = count;
            _index = -1;
        }

        public RecordHeader Current => _array[_index];

        object System.Collections.IEnumerator.Current => Current;

        public bool MoveNext() => ++_index < _count;

        public void Reset() => _index = -1;

        public void Dispose() { }
    }
}
