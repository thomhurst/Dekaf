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
    public void Produce(ProducerMessage<TKey, TValue> message)
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
    public void Produce(string topic, TKey? key, TValue value)
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
        Produce(new ProducerMessage<TKey, TValue> { Topic = topic, Key = key, Value = value });
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
        // Check if metadata is initialized (sync check)
        if (_metadataManager.Metadata.LastRefreshed == default)
        {
            return false; // Need async initialization
        }

        // Try to get topic metadata from cache
        if (!_metadataManager.TryGetCachedTopicMetadata(topic, out var topicInfo) || topicInfo is null)
        {
            return false; // Cache miss, need async refresh
        }

        if (topicInfo.PartitionCount == 0)
        {
            return false; // Invalid topic state, let async path handle error
        }

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
    /// Core synchronous fire-and-forget produce logic that works directly from parameters.
    /// Avoids ProducerMessage allocation entirely.
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
        var key = PooledMemory.Null;
        var valueMemory = PooledMemory.Null;
        RecordHeader[]? pooledHeaderArray = null;

        try
        {
            // Serialize key and value
            var keyIsNull = keyObj is null;
            key = keyIsNull ? PooledMemory.Null : SerializeKeyToPooled(keyObj!, topic, headers);
            valueMemory = SerializeValueToPooled(value, topic, headers);

            // Determine partition
            var partition = partitionOverride
                ?? _partitioner.Partition(topic, key.Span, keyIsNull, topicInfo.PartitionCount);

            // Get timestamp
            var timestamp = timestampOverride ?? DateTimeOffset.UtcNow;
            var timestampMs = timestamp.ToUnixTimeMilliseconds();

            // Convert headers
            IReadOnlyList<RecordHeader>? recordHeaders = null;
            if (headers is not null && headers.Count > 0)
            {
                recordHeaders = ConvertHeaders(headers, out pooledHeaderArray);
            }

            // Append to accumulator synchronously - no completion source needed
            if (!_accumulator.TryAppendFireAndForget(
                topic,
                partition,
                timestampMs,
                key,
                valueMemory,
                recordHeaders,
                pooledHeaderArray))
            {
                // Accumulator is disposed - cleanup and throw
                CleanupPooledResources(key, valueMemory, pooledHeaderArray);
                throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));
            }

            // SUCCESS: Memory ownership transferred to batch.
            var messageBytes = key.Length + valueMemory.Length;
            key = PooledMemory.Null;
            valueMemory = PooledMemory.Null;
            pooledHeaderArray = null;

            // Track message produced using fast path (global counters only)
            _statisticsCollector.RecordMessageProducedFast(messageBytes);
        }
        catch
        {
            // Cleanup pooled resources on ANY exception to prevent leaks.
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
    /// </summary>
    private void ProduceSyncCoreFireAndForget(
        ProducerMessage<TKey, TValue> message,
        TopicInfo topicInfo)
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

            // Get timestamp
            var timestamp = message.Timestamp ?? DateTimeOffset.UtcNow;
            var timestampMs = timestamp.ToUnixTimeMilliseconds();

            // Convert headers
            IReadOnlyList<RecordHeader>? recordHeaders = null;
            if (message.Headers is not null && message.Headers.Count > 0)
            {
                recordHeaders = ConvertHeaders(message.Headers, out pooledHeaderArray);
            }

            // Append to accumulator synchronously - no completion source needed
            if (!_accumulator.TryAppendFireAndForget(
                message.Topic,
                partition,
                timestampMs,
                key,
                value,
                recordHeaders,
                pooledHeaderArray))
            {
                // Accumulator is disposed - cleanup and throw
                CleanupPooledResources(key, value, pooledHeaderArray);
                throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));
            }

            // SUCCESS: Memory ownership transferred to batch.
            // Clear local references to prevent double-return if catch block executes.
            // The batch now owns these arrays and will return them to the pool.
            var messageBytes = key.Length + value.Length;
            key = PooledMemory.Null;
            value = PooledMemory.Null;
            pooledHeaderArray = null;

            // Track message produced using fast path (global counters only)
            _statisticsCollector.RecordMessageProducedFast(messageBytes);
        }
        catch
        {
            // Cleanup pooled resources on ANY exception to prevent leaks.
            // After successful append, key/value/pooledHeaderArray are cleared above,
            // so this is a no-op in the success case (prevents double-return to pool).
            CleanupPooledResources(key, value, pooledHeaderArray);
            throw;
        }
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

            // Get timestamp
            var timestamp = message.Timestamp ?? DateTimeOffset.UtcNow;
            var timestampMs = timestamp.ToUnixTimeMilliseconds();

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
    public void Produce(ProducerMessage<TKey, TValue> message, Action<RecordMetadata?, Exception?> deliveryHandler)
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
    private bool TryProduceSyncWithHandler(ProducerMessage<TKey, TValue> message, Action<RecordMetadata?, Exception?> deliveryHandler)
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
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.LingerMs > 0 ? _options.LingerMs : 100, cancellationToken)
                    .ConfigureAwait(false);
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
    /// Serializes the key directly into pooled memory using PooledBufferWriter.
    /// This eliminates the double-copy overhead of the previous ArrayBufferWriter approach.
    /// </summary>
    private PooledMemory SerializeKeyToPooled(TKey key, string topic, Headers? headers)
    {
        var writer = new PooledBufferWriter(initialCapacity: 256);
        try
        {
            // Reuse thread-local context by updating fields (zero-allocation)
            t_serializationContext.Topic = topic;
            t_serializationContext.Component = SerializationComponent.Key;
            t_serializationContext.Headers = headers;
            _keySerializer.Serialize(key, writer, t_serializationContext);

            // Transfer ownership - no copy needed
            return writer.ToPooledMemory();
        }
        catch
        {
            // Return buffer to pool on error to prevent leaks
            writer.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Serializes the value directly into pooled memory using PooledBufferWriter.
    /// This eliminates the double-copy overhead of the previous ArrayBufferWriter approach.
    /// </summary>
    private PooledMemory SerializeValueToPooled(TValue value, string topic, Headers? headers)
    {
        var writer = new PooledBufferWriter(initialCapacity: 256);
        try
        {
            // Reuse thread-local context by updating fields (zero-allocation)
            t_serializationContext.Topic = topic;
            t_serializationContext.Component = SerializationComponent.Value;
            t_serializationContext.Headers = headers;
            _valueSerializer.Serialize(value, writer, t_serializationContext);

            // Transfer ownership - no copy needed
            return writer.ToPooledMemory();
        }
        catch
        {
            // Return buffer to pool on error to prevent leaks
            writer.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Converts Headers to RecordHeaders with minimal allocations.
    /// Allocates an array directly instead of using List to avoid the List wrapper allocation.
    /// For small header counts (<=16), allocates a small array on the heap.
    /// For larger counts (>16), uses ArrayPool to avoid allocating large arrays that could pressure GC.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IReadOnlyList<RecordHeader> ConvertHeaders(Headers headers, out RecordHeader[]? pooledArray)
    {
        const int PoolingThreshold = 16;
        var count = headers.Count;
        pooledArray = null;

        RecordHeader[] result;
        if (count <= PoolingThreshold)
        {
            // Small header count: allocate array directly (one allocation vs List's two allocations)
            result = new RecordHeader[count];
        }
        else
        {
            // Large header count: rent from pool to avoid large array allocations
            result = ArrayPool<RecordHeader>.Shared.Rent(count);
            pooledArray = result; // Track for returning to pool later
        }

        var index = 0;
        foreach (var h in headers)
        {
            result[index++] = new RecordHeader
            {
                Key = h.Key,
                Value = h.Value,
                IsValueNull = h.IsValueNull
            };
        }

        // If pooled, wrap in HeaderListWrapper to expose only the valid portion
        if (pooledArray is not null)
        {
            return new HeaderListWrapper(result, count);
        }

        return result;
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
/// A buffer writer that writes directly to an ArrayPool-rented array.
/// Eliminates the double-copy overhead of using ArrayBufferWriter followed by pool rental.
/// </summary>
/// <remarks>
/// <para>
/// This struct implements IBufferWriter&lt;byte&gt; and manages its own pooled array.
/// When serialization is complete, call ToPooledMemory() to get ownership of the array.
/// The caller is responsible for returning the array to the pool (via PooledMemory.Return()).
/// </para>
/// <para>
/// Note: This is intentionally not a ref struct because it must be passable to
/// ISerializer&lt;T&gt;.Serialize(T, IBufferWriter&lt;byte&gt;, ...) which expects an interface.
/// Ref structs cannot be boxed for interface dispatch without changing the public API.
/// To prevent misuse, this struct tracks ownership state and throws ObjectDisposedException
/// if methods are called after ToPooledMemory() or Dispose().
/// </para>
/// </remarks>
internal struct PooledBufferWriter : IBufferWriter<byte>
{
    private byte[]? _buffer;
    private int _written;
    private bool _ownershipTransferred;

    /// <summary>
    /// Creates a new PooledBufferWriter with the specified initial capacity.
    /// </summary>
    /// <param name="initialCapacity">Initial buffer size. Defaults to 256 bytes.</param>
    public PooledBufferWriter(int initialCapacity = 256)
    {
        _buffer = ArrayPool<byte>.Shared.Rent(initialCapacity);
        _written = 0;
        _ownershipTransferred = false;
    }

    /// <summary>
    /// Gets the number of bytes written to the buffer.
    /// </summary>
    public readonly int WrittenCount => _written;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly void ThrowIfDisposed()
    {
        if (_ownershipTransferred || _buffer is null)
            throw new ObjectDisposedException(nameof(PooledBufferWriter), "Buffer ownership has been transferred or disposed.");
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (_written + count > _buffer!.Length)
            throw new InvalidOperationException("Cannot advance past the end of the buffer");

        _written += count;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        ThrowIfDisposed();
        EnsureCapacity(sizeHint);
        return _buffer!.AsMemory(_written);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        ThrowIfDisposed();
        EnsureCapacity(sizeHint);
        return _buffer!.AsSpan(_written);
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
        ThrowIfDisposed();
        var result = new PooledMemory(_buffer!, _written);
        // Mark ownership transferred and clear reference
        _ownershipTransferred = true;
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
        if (_buffer is not null && !_ownershipTransferred)
        {
            ArrayPool<byte>.Shared.Return(_buffer, clearArray: true);
            _buffer = null;
            _written = 0;
            _ownershipTransferred = true;
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
