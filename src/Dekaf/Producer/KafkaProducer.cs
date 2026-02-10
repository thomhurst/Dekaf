using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Dekaf.Compression;
using Dekaf.Errors;
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

    // Per-broker sender threads: each broker gets a dedicated BrokerSender with its own
    // channel and send loop. Partition gates (capacity=1) serialize sends per partition,
    // while the per-broker in-flight semaphore enables pipelining across different partitions.
    private readonly ConcurrentDictionary<int, BrokerSender> _brokerSenders = new();
    private readonly ConcurrentDictionary<TopicPartition, SemaphoreSlim> _partitionSendGates = new();


    // Explicit initialization: users must call InitializeAsync() or use BuildAsync() before producing.
    private volatile bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private int _produceApiVersion = -1;
    internal volatile bool _disposed;

    // Idempotent / transaction state
    // Memory ordering: _idempotentInitialized is volatile (acquire/release semantics).
    // InitIdempotentProducerAsync sets _producerId, _producerEpoch, _accumulator.ProducerId/Epoch
    // BEFORE writing _idempotentInitialized = true (volatile write = release fence).
    // The fast path reads _idempotentInitialized (volatile read = acquire fence) BEFORE
    // any dependent reads, guaranteeing visibility of all prior writes.
    private long _producerId = -1;
    private short _producerEpoch = -1;
    private volatile bool _idempotentInitialized;
    private int _transactionCoordinatorId = -1;
    internal volatile TransactionState _transactionState = TransactionState.Uninitialized;
    private readonly SemaphoreSlim _transactionLock = new(1, 1);
    internal readonly HashSet<TopicPartition> _partitionsInTransaction = [];

    // In-flight batch tracker for coordinated retry with multiple in-flight batches per partition.
    // Only initialized when idempotence is enabled (sequence numbers guarantee ordering at broker).
    // When null, partition gates use SemaphoreSlim(1,1) for single in-flight batch per partition.
    private readonly PartitionInflightTracker? _inflightTracker;

    // Statistics collection
    private readonly ProducerStatisticsCollector _statisticsCollector = new();
    private readonly StatisticsEmitter<ProducerStatistics>? _statisticsEmitter;

    // Pool for PooledValueTaskSource to avoid per-message allocations
    // Unlike TaskCompletionSource, these can be reset and reused
    private readonly ValueTaskSourcePool<RecordMetadata> _valueTaskSourcePool;

    // Interceptors - stored as typed array for zero-allocation iteration
    private readonly IProducerInterceptor<TKey, TValue>[]? _interceptors;

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
    // Avoids MetadataManager dictionary lookups for consecutive messages to the same topic.
    // Cache validity is time-bounded (~1 second) to pick up background metadata refreshes.
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
        ILoggerFactory? loggerFactory = null,
        Metadata.MetadataOptions? metadataOptions = null)
    {
        _options = options;
        _keySerializer = keySerializer;
        _valueSerializer = valueSerializer;
        _logger = loggerFactory?.CreateLogger<KafkaProducer<TKey, TValue>>();

        // Initialize interceptors from options
        if (options.Interceptors is { Count: > 0 })
        {
            var interceptors = new IProducerInterceptor<TKey, TValue>[options.Interceptors.Count];
            for (var i = 0; i < options.Interceptors.Count; i++)
            {
                interceptors[i] = (IProducerInterceptor<TKey, TValue>)options.Interceptors[i];
            }
            _interceptors = interceptors;
        }

        // Initialize ValueTaskSource pool with configured size
        _valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>(options.ValueTaskSourcePoolSize);

        _partitioner = options.CustomPartitioner ?? options.Partitioner switch
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
            options: metadataOptions,
            logger: loggerFactory?.CreateLogger<MetadataManager>());

        _accumulator = new RecordAccumulator(options);
        _compressionCodecs = CreateCompressionCodecRegistry(options);

        // Initialize inflight tracker for idempotent producers to enable multiple in-flight
        // batches per partition. With idempotence, the broker uses sequence numbers to guarantee
        // ordering, so multiple batches can be in-flight simultaneously. The tracker enables
        // coordinated retry on OutOfOrderSequenceNumber instead of blind backoff.
        if (options.EnableIdempotence)
        {
            _inflightTracker = new PartitionInflightTracker();
        }

        _senderCts = new CancellationTokenSource();
        // Use 1ms check interval for low-latency awaited produces.
        // ShouldFlush() is smart: it returns true immediately for batches with completion
        // sources (awaited produces), but waits for full LingerMs for fire-and-forget batches.
        // This provides low latency for ProduceAsync while maintaining efficient batching for Send.
        _lingerTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(1));
        _senderTask = SenderLoopAsync(_senderCts.Token);
        _lingerTask = LingerLoopAsync(_senderCts.Token);

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

    private static CompressionCodecRegistry CreateCompressionCodecRegistry(ProducerOptions options)
    {
        // Start from the global Default registry which includes codecs auto-registered
        // by compression packages (e.g. Dekaf.Compression.Lz4) via [ModuleInitializer].
        var registry = CompressionCodecRegistry.Default;

        if (options.CompressionLevel.HasValue)
        {
            var level = options.CompressionLevel.Value;

            // Set the default compression level hint on the registry so external codec
            // extensions (AddLz4, AddZstd) can use it as a fallback when registering
            registry.DefaultCompressionLevel = level;

            // Re-register the built-in Gzip codec with the specified level.
            // The GzipCompressionCodec(int) constructor validates the range (0-9).
            if (options.CompressionType == CompressionType.Gzip)
            {
                registry.Register(new Compression.GzipCompressionCodec(level));
            }
        }

        return registry;
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

    /// <summary>
    /// Asynchronously produces a message to the specified topic.
    /// </summary>
    /// <param name="message">The message to produce.</param>
    /// <param name="cancellationToken">
    /// Cancellation token that can cancel the wait at any point.
    /// <para>
    /// <b>Before message is appended:</b> Cancellation prevents the message from being sent.<br/>
    /// <b>After message is appended:</b> Cancellation stops the caller's wait, but the message
    /// WILL still be delivered to Kafka. This allows callers to implement timeouts without
    /// blocking indefinitely, while ensuring no data loss.
    /// </para>
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask{RecordMetadata}"/> representing the produce operation.
    /// The result contains metadata about the produced message (topic, partition, offset).
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown if the cancellation token is cancelled (either before append or while waiting for delivery).
    /// If thrown after append, the message will still be delivered to Kafka.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if the producer has been disposed.
    /// </exception>
    public ValueTask<RecordMetadata> ProduceAsync(
        ProducerMessage<TKey, TValue> message,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));

        ThrowIfNotInitialized();

        // Check cancellation upfront before any work
        cancellationToken.ThrowIfCancellationRequested();

        // Apply OnSend interceptors before serialization
        message = ApplyOnSendInterceptors(message);

        // Fast path: Try synchronous produce if metadata is initialized and cached.
        // This bypasses channel overhead for 99%+ of calls after warmup.
        if (TryProduceSyncForAsync(message, out var completion))
        {
            // POST-QUEUE: Message appended to batch, committed to being sent
            // Message WILL be delivered, but caller can stop waiting via cancellation token.
            if (cancellationToken.CanBeCanceled)
            {
                return AwaitWithCancellation(completion!, cancellationToken);
            }
            return completion!.Task;
        }

        // Slow path: Fall back to channel-based async processing.
        // This handles first-time metadata initialization or cache misses.
        return ProduceAsyncSlow(message, cancellationToken);
    }

    /// <summary>
    /// Awaits a completion source with cancellation support.
    /// If cancellation fires, sets the completion source to cancelled state so that
    /// GetResult() is called and the pool item is properly returned.
    /// The message delivery continues in background regardless.
    /// </summary>
    private static async ValueTask<RecordMetadata> AwaitWithCancellation(
        PooledValueTaskSource<RecordMetadata> completion,
        CancellationToken cancellationToken)
    {
        // Capture both completion and token in a tuple for the callback
        var state = (completion, cancellationToken);
        var registration = cancellationToken.Register(
            static s =>
            {
                var (comp, token) = ((PooledValueTaskSource<RecordMetadata>, CancellationToken))s!;
                comp.TrySetCanceled(token);
            },
            state);

        try
        {
            return await completion.Task.ConfigureAwait(false);
        }
        finally
        {
            await registration.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Attempts synchronous produce for awaited ProduceAsync when metadata is cached.
    /// Returns true if successful with the completion source to await.
    /// Throws exceptions for awaited callers (unlike fire-and-forget which captures them).
    /// Uses thread-local metadata cache for maximum performance on hot path.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryProduceSyncForAsync(ProducerMessage<TKey, TValue> message, out PooledValueTaskSource<RecordMetadata>? completion)
    {
        completion = null;

        // Check if metadata is initialized (sync check).
        // Callers ensure initialization via InitializeAsync, so this only
        // returns false on the very first call before init completes.
        if (_metadataManager.Metadata.LastRefreshed == default)
        {
            return false;
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
        // Retry fast path - metadata should already be initialized via InitializeAsync()
        if (TryProduceSyncForAsync(message, out var fastCompletion))
        {
            if (cancellationToken.CanBeCanceled)
            {
                return await AwaitWithCancellation(fastCompletion!, cancellationToken).ConfigureAwait(false);
            }
            return await fastCompletion!.Task.ConfigureAwait(false);
        }

        // Topic cache miss — fetch topic metadata inline and produce
        var completion = _valueTaskSourcePool.Rent();
        try
        {
            await ProduceInternalAsync(message, completion, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            completion.TrySetCanceled(cancellationToken);
        }
        catch (Exception ex)
        {
            completion.TrySetException(ex);
        }

        if (cancellationToken.CanBeCanceled)
        {
            return await AwaitWithCancellation(completion, cancellationToken).ConfigureAwait(false);
        }
        return await completion.Task.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Send(ProducerMessage<TKey, TValue> message)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));

        ThrowIfNotInitialized();

        // Apply OnSend interceptors before serialization
        message = ApplyOnSendInterceptors(message);

        // Fast path: synchronous fire-and-forget produce with cached metadata (99%+ of calls).
        if (TryProduceSyncFireAndForget(message))
        {
            return;
        }

        // Topic cache miss — fetch topic metadata synchronously and produce.
        // This is a one-time cost per new topic.
        try
        {
            var topicInfo = FetchTopicMetadataSync(message.Topic);
            ProduceSyncCoreFireAndForgetDirect(
                message.Topic,
                message.Key,
                message.Value,
                message.Partition,
                message.Timestamp,
                message.Headers,
                topicInfo);
        }
        catch (TimeoutException)
        {
            // BufferMemory backpressure or metadata timeout must propagate
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Fire-and-forget produce failed for topic {Topic} (topic metadata fetch)", message.Topic);
        }
    }

    /// <inheritdoc />
    public void Send(string topic, TKey? key, TValue value)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));

        ThrowIfNotInitialized();

        // When interceptors are present, we must create a ProducerMessage so interceptors can operate
        if (_interceptors is not null)
        {
            Send(new ProducerMessage<TKey, TValue> { Topic = topic, Key = key, Value = value });
            return;
        }

        // Fast path: synchronous fire-and-forget produce with cached metadata.
        if (TryProduceSyncFireAndForgetDirect(topic, key, value, partition: null, timestamp: null, headers: null))
        {
            return;
        }

        // Topic cache miss — fall back to the message-based overload which handles sync topic fetch.
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
        // Avoids MetadataManager dictionary lookup for consecutive messages to the same topic.
        // Cache refreshes periodically (~1 second) to pick up background metadata updates.
        if (TryGetCachedTopicInfo(topic, out var topicInfo))
        {
            try
            {
                ProduceSyncCoreFireAndForgetDirect(topic, key, value, partition, timestamp, headers, topicInfo!);
            }
            catch (TimeoutException)
            {
                // CRITICAL: BufferMemory backpressure timeout must propagate to caller
                // This indicates producer is faster than network can drain
                throw;
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
        catch (TimeoutException)
        {
            // CRITICAL: BufferMemory backpressure timeout must propagate to caller
            // This indicates producer is faster than network can drain
            throw;
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

        var valueIsNull = value is null;
        int valueLength = 0;

        if (!valueIsNull)
        {
            var valueWriter = new ReusableBufferWriter(ref t_valueSerializationBuffer, DefaultValueBufferSize);
            t_serializationContext.Topic = topic;
            t_serializationContext.Component = SerializationComponent.Value;
            t_serializationContext.Headers = headers;
            _valueSerializer.Serialize(value!, ref valueWriter, t_serializationContext);
            valueWriter.UpdateBufferRef(ref t_valueSerializationBuffer);
            valueLength = valueWriter.WrittenCount;
        }

        // Step 2: Compute partition using serialized key bytes
        var keySpan = keyIsNull ? ReadOnlySpan<byte>.Empty : t_keySerializationBuffer.AsSpan(0, keyLength);
        var partition = partitionOverride
            ?? _partitioner.Partition(topic, keySpan, keyIsNull, topicInfo.PartitionCount);

        // Step 3: Get timestamp
        var timestampMs = timestampOverride?.ToUnixTimeMilliseconds() ?? GetFastTimestampMs();

        // Step 4: Convert headers
        IReadOnlyList<Header>? recordHeaders = null;
        Header[]? pooledHeaderArray = null;
        if (headers is not null && headers.Count > 0)
        {
            recordHeaders = ConvertHeaders(headers, out pooledHeaderArray);
        }

        // Step 5: FAST PATH - Try to append to cached batch with arena (no side effects)
        // This succeeds when: same partition as recent message AND batch has space
        if (TryAppendToArenaFast(topic, partition, timestampMs, keyIsNull, keyLength, valueIsNull, valueLength, recordHeaders, ref pooledHeaderArray))
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
        bool valueIsNull,
        int valueLength,
        IReadOnlyList<Header>? recordHeaders,
        ref Header[]? pooledHeaderArray)
    {
        // STEP 1: Calculate record size BEFORE any work
        var recordSize = PartitionBatch.EstimateRecordSize(keyLength, valueLength, recordHeaders);

        // STEP 2: Try to reserve BufferMemory (non-blocking check)
        if (!_accumulator.TryReserveMemory(recordSize))
        {
            return false; // Buffer full - fall back to slow path with backpressure
        }

        // STEP 3: Memory reserved - must ensure cleanup on ANY failure from this point
        try
        {
            // Check thread-local batch cache first (avoids dictionary lookup)
            var cachedBatch = GetCachedBatch(topic, partition);
            if (cachedBatch is null)
            {
                _accumulator.ReleaseMemory(recordSize);
                return false; // No cached batch, use slow path
            }

            var arena = cachedBatch.Arena;
            if (arena is null)
            {
                _accumulator.ReleaseMemory(recordSize);
                return false; // Batch completed, use slow path
            }

            // Calculate total size needed for key + value combined
            var totalSize = keyLength + valueLength;

            // Single CAS allocation for both key and value together
            // This reduces atomic operations from 2 per message to 1
            if (!arena.TryAllocate(totalSize, out var combinedSpan, out var combinedOffset))
            {
                _accumulator.ReleaseMemory(recordSize);
                return false; // Allocation failed, use slow path
            }

            // Split the combined allocation into key and value slices
            ArenaSlice keySlice = default;
            ArenaSlice valueSlice = default;

            if (!keyIsNull && keyLength > 0)
            {
                // Copy key to first part of combined allocation
                t_keySerializationBuffer.AsSpan(0, keyLength).CopyTo(combinedSpan.Slice(0, keyLength));
                keySlice = new ArenaSlice(combinedOffset, keyLength);

                if (!valueIsNull)
                {
                    // Copy value to second part
                    t_valueSerializationBuffer.AsSpan(0, valueLength).CopyTo(combinedSpan.Slice(keyLength, valueLength));
                    valueSlice = new ArenaSlice(combinedOffset + keyLength, valueLength);
                }
            }
            else if (!valueIsNull)
            {
                // No key - value uses entire allocation
                t_valueSerializationBuffer.AsSpan(0, valueLength).CopyTo(combinedSpan);
                valueSlice = new ArenaSlice(combinedOffset, valueLength);
            }

            // Append using arena-based method
            var result = cachedBatch.TryAppendFromArena(timestampMs, keySlice, keyIsNull, valueSlice, valueIsNull, recordHeaders, pooledHeaderArray);

            if (!result.Success)
            {
                _accumulator.ReleaseMemory(recordSize);
                return false; // Batch full or completed, use slow path
            }

            // Success - batch now owns the pooled header array
            // Memory stays reserved until batch completes (no ReleaseMemory call)
            pooledHeaderArray = null;
            return true;
        }
        catch
        {
            // CRITICAL: If any exception occurs after memory reservation, release it
            // Prevents permanent BufferMemory leak
            _accumulator.ReleaseMemory(recordSize);
            throw;
        }
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
        IReadOnlyList<Header>? recordHeaders,
        Header[]? pooledHeaderArray)
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
                ProduceSyncCoreFireAndForgetDirect(
                    message.Topic,
                    message.Key,
                    message.Value,
                    message.Partition,
                    message.Timestamp,
                    message.Headers,
                    topicInfo!);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Fire-and-forget produce failed for topic {Topic}", message.Topic);
            }
            return true;
        }

        // Metadata manager cache check (callers guarantee metadata is initialized via InitializeAsync).
        if (!_metadataManager.TryGetCachedTopicMetadata(message.Topic, out topicInfo) || topicInfo is null)
        {
            return false; // Topic cache miss — caller will fetch inline
        }

        if (topicInfo.PartitionCount == 0)
        {
            return false;
        }

        // Update thread-local cache for next call
        UpdateCachedTopicInfo(message.Topic, topicInfo);

        try
        {
            ProduceSyncCoreFireAndForgetDirect(
                message.Topic,
                message.Key,
                message.Value,
                message.Partition,
                message.Timestamp,
                message.Headers,
                topicInfo);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Fire-and-forget produce failed for topic {Topic}", message.Topic);
        }
        return true;
    }

    /// <summary>
    /// Core synchronous produce logic shared between TryProduceSyncForAsync and TryProduceSyncWithHandler.
    /// Handles serialization, partitioning, and accumulator append with proper resource cleanup.
    /// </summary>
    private void ProduceSyncCore(
        ProducerMessage<TKey, TValue> message,
        TopicInfo topicInfo,
        PooledValueTaskSource<RecordMetadata> completion)
    {
        var key = PooledMemory.Null;
        var value = PooledMemory.Null;
        Header[]? pooledHeaderArray = null;

        try
        {
            // Serialize key and value
            var keyIsNull = message.Key is null;
            key = keyIsNull ? PooledMemory.Null : SerializeKeyToPooled(message.Key!, message.Topic, message.Headers);
            var valueIsNull = message.Value is null;
            value = valueIsNull ? PooledMemory.Null : SerializeValueToPooled(message.Value!, message.Topic, message.Headers);

            // Determine partition
            var partition = message.Partition
                ?? _partitioner.Partition(message.Topic, key.Span, keyIsNull, topicInfo.PartitionCount);

            // Get timestamp - use fast cached timestamp when no override provided
            var timestampMs = message.Timestamp?.ToUnixTimeMilliseconds() ?? GetFastTimestampMs();

            // Convert headers
            IReadOnlyList<Header>? recordHeaders = null;
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
    private static void CleanupPooledResources(PooledMemory key, PooledMemory value, Header[]? pooledHeaderArray)
    {
        key.Return();
        value.Return();
        if (pooledHeaderArray is not null)
        {
            ArrayPool<Header>.Shared.Return(pooledHeaderArray);
        }
    }

    /// <summary>
    /// Applies OnSend interceptors to the message before serialization.
    /// Interceptor exceptions are caught and logged - the original message is used on failure.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ProducerMessage<TKey, TValue> ApplyOnSendInterceptors(ProducerMessage<TKey, TValue> message)
    {
        if (_interceptors is null)
            return message;

        return ApplyOnSendInterceptorsSlow(message);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private ProducerMessage<TKey, TValue> ApplyOnSendInterceptorsSlow(ProducerMessage<TKey, TValue> message)
    {
        foreach (var interceptor in _interceptors!)
        {
            try
            {
                message = interceptor.OnSend(message);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Producer interceptor {Interceptor} OnSend threw an exception",
                    interceptor.GetType().Name);
            }
        }
        return message;
    }

    /// <summary>
    /// Invokes OnAcknowledgement on all interceptors.
    /// Interceptor exceptions are caught and logged.
    /// </summary>
    internal void InvokeOnAcknowledgement(RecordMetadata metadata, Exception? exception)
    {
        if (_interceptors is null)
            return;

        foreach (var interceptor in _interceptors)
        {
            try
            {
                interceptor.OnAcknowledgement(metadata, exception);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Producer interceptor {Interceptor} OnAcknowledgement threw an exception",
                    interceptor.GetType().Name);
            }
        }
    }

    /// <summary>
    /// Invokes OnAcknowledgement interceptors for all messages in a batch.
    /// </summary>
    private void InvokeOnAcknowledgementForBatch(
        TopicPartition topicPartition,
        long baseOffset,
        DateTimeOffset timestamp,
        int messageCount,
        Exception? exception)
    {
        if (_interceptors is null)
            return;

        for (var i = 0; i < messageCount; i++)
        {
            var metadata = new RecordMetadata
            {
                Topic = topicPartition.Topic,
                Partition = topicPartition.Partition,
                Offset = baseOffset >= 0 ? baseOffset + i : -1,
                Timestamp = timestamp
            };
            InvokeOnAcknowledgement(metadata, exception);
        }
    }

    /// <inheritdoc />
    public void Send(ProducerMessage<TKey, TValue> message, Action<RecordMetadata, Exception?> deliveryHandler)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));

        ThrowIfNotInitialized();

        ArgumentNullException.ThrowIfNull(deliveryHandler);

        // Apply OnSend interceptors before serialization
        message = ApplyOnSendInterceptors(message);

        // Fast path: synchronous produce with handler using cached metadata (99%+ of calls).
        if (TryProduceSyncWithHandler(message, deliveryHandler))
        {
            return;
        }

        // Topic cache miss — fetch topic metadata synchronously and retry.
        FetchTopicMetadataSync(message.Topic);

        if (!TryProduceSyncWithHandler(message, deliveryHandler))
        {
            throw new InvalidOperationException($"Failed to produce to topic '{message.Topic}'");
        }
    }

    /// <summary>
    /// Attempts synchronous produce with delivery handler when metadata is initialized and cached.
    /// Returns true if successful, false if async path is needed.
    /// Uses batch-embedded callbacks for zero-allocation - callbacks are invoked inline on sender thread.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryProduceSyncWithHandler(ProducerMessage<TKey, TValue> message, Action<RecordMetadata, Exception?> deliveryHandler)
    {
        // Callers guarantee metadata is initialized via InitializeAsync.
        if (!_metadataManager.TryGetCachedTopicMetadata(message.Topic, out var topicInfo) || topicInfo is null)
        {
            return false; // Topic cache miss — caller will fetch inline
        }

        if (topicInfo.PartitionCount == 0)
        {
            return false;
        }

        // All checks passed - proceed synchronously using direct callback path (no PooledValueTaskSource needed)
        try
        {
            ProduceSyncCoreWithCallbackDirect(
                message.Topic,
                message.Key,
                message.Value,
                message.Partition,
                message.Timestamp,
                message.Headers,
                topicInfo,
                deliveryHandler);
        }
        catch (Exception ex)
        {
            // Deliver exception to callback - don't throw for fire-and-forget style
            try { deliveryHandler(default, ex); } catch { /* Swallow callback exceptions */ }
        }
        return true;
    }

    /// <summary>
    /// Core produce logic with delivery callback stored directly in the batch.
    /// Similar to ProduceSyncCoreFireAndForgetDirect but tracks callbacks for delivery notification.
    /// Callbacks are invoked inline on the sender thread when the batch completes.
    /// </summary>
    private void ProduceSyncCoreWithCallbackDirect(
        string topic,
        TKey? keyObj,
        TValue value,
        int? partitionOverride,
        DateTimeOffset? timestampOverride,
        Headers? headers,
        TopicInfo topicInfo,
        Action<RecordMetadata, Exception?> callback)
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

        var valueIsNull = value is null;
        int valueLength = 0;

        if (!valueIsNull)
        {
            var valueWriter = new ReusableBufferWriter(ref t_valueSerializationBuffer, DefaultValueBufferSize);
            t_serializationContext.Topic = topic;
            t_serializationContext.Component = SerializationComponent.Value;
            t_serializationContext.Headers = headers;
            _valueSerializer.Serialize(value!, ref valueWriter, t_serializationContext);
            valueWriter.UpdateBufferRef(ref t_valueSerializationBuffer);
            valueLength = valueWriter.WrittenCount;
        }

        // Step 2: Compute partition using serialized key bytes
        var keySpan = keyIsNull ? ReadOnlySpan<byte>.Empty : t_keySerializationBuffer.AsSpan(0, keyLength);
        var partition = partitionOverride
            ?? _partitioner.Partition(topic, keySpan, keyIsNull, topicInfo.PartitionCount);

        // Step 3: Get timestamp
        var timestampMs = timestampOverride?.ToUnixTimeMilliseconds() ?? GetFastTimestampMs();

        // Step 4: Convert headers
        IReadOnlyList<Header>? recordHeaders = null;
        Header[]? pooledHeaderArray = null;
        if (headers is not null && headers.Count > 0)
        {
            recordHeaders = ConvertHeaders(headers, out pooledHeaderArray);
        }

        // Step 5: FAST PATH - Try to append to cached batch with arena and callback
        if (TryAppendToArenaFastWithCallback(topic, partition, timestampMs, keyIsNull, keyLength, valueIsNull, valueLength, recordHeaders, ref pooledHeaderArray, callback))
        {
            _statisticsCollector.RecordMessageProducedFast(keyLength + valueLength);
            return;
        }

        // Step 6: SLOW PATH - Handle all complexity
        AppendWithSlowPathWithCallback(topic, partition, timestampMs, keyIsNull, keyLength, valueLength, recordHeaders, pooledHeaderArray, callback);
        _statisticsCollector.RecordMessageProducedFast(keyLength + valueLength);
    }

    /// <summary>
    /// FAST PATH with callback: Try to append to an existing batch's arena with a delivery callback.
    /// No side effects - if anything is wrong, just return false.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryAppendToArenaFastWithCallback(
        string topic,
        int partition,
        long timestampMs,
        bool keyIsNull,
        int keyLength,
        bool valueIsNull,
        int valueLength,
        IReadOnlyList<Header>? recordHeaders,
        ref Header[]? pooledHeaderArray,
        Action<RecordMetadata, Exception?> callback)
    {
        // STEP 1: Calculate record size BEFORE any work
        var recordSize = PartitionBatch.EstimateRecordSize(keyLength, valueLength, recordHeaders);

        // STEP 2: Try to reserve BufferMemory (non-blocking check)
        if (!_accumulator.TryReserveMemory(recordSize))
        {
            return false; // Buffer full - fall back to slow path with backpressure
        }

        // STEP 3: Memory reserved - must ensure cleanup on ANY failure from this point
        try
        {
            // Check thread-local batch cache first (avoids dictionary lookup)
            var cachedBatch = GetCachedBatch(topic, partition);
            if (cachedBatch is null)
            {
                _accumulator.ReleaseMemory(recordSize);
                return false; // No cached batch, use slow path
            }

            var arena = cachedBatch.Arena;
            if (arena is null)
            {
                _accumulator.ReleaseMemory(recordSize);
                return false; // Batch completed, use slow path
            }

            // Calculate total size needed for key + value combined
            var totalSize = keyLength + valueLength;

            // Single CAS allocation for both key and value together
            // This reduces atomic operations from 2 per message to 1
            if (!arena.TryAllocate(totalSize, out var combinedSpan, out var combinedOffset))
            {
                _accumulator.ReleaseMemory(recordSize);
                return false; // Allocation failed, use slow path
            }

            // Split the combined allocation into key and value slices
            ArenaSlice keySlice = default;
            ArenaSlice valueSlice = default;

            if (!keyIsNull && keyLength > 0)
            {
                // Copy key to first part of combined allocation
                t_keySerializationBuffer.AsSpan(0, keyLength).CopyTo(combinedSpan.Slice(0, keyLength));
                keySlice = new ArenaSlice(combinedOffset, keyLength);

                if (!valueIsNull)
                {
                    // Copy value to second part
                    t_valueSerializationBuffer.AsSpan(0, valueLength).CopyTo(combinedSpan.Slice(keyLength, valueLength));
                    valueSlice = new ArenaSlice(combinedOffset + keyLength, valueLength);
                }
            }
            else if (!valueIsNull)
            {
                // No key - value uses entire allocation
                t_valueSerializationBuffer.AsSpan(0, valueLength).CopyTo(combinedSpan);
                valueSlice = new ArenaSlice(combinedOffset, valueLength);
            }

            // Append using arena-based method with callback
            var result = cachedBatch.TryAppendFromArenaWithCallback(timestampMs, keySlice, keyIsNull, valueSlice, valueIsNull, recordHeaders, pooledHeaderArray, callback);

            if (!result.Success)
            {
                _accumulator.ReleaseMemory(recordSize);
                return false; // Batch full or completed, use slow path
            }

            // Success - batch now owns the pooled header array
            // Memory stays reserved until batch completes (no ReleaseMemory call)
            pooledHeaderArray = null;
            return true;
        }
        catch
        {
            // CRITICAL: If any exception occurs after memory reservation, release it
            _accumulator.ReleaseMemory(recordSize);
            throw;
        }
    }

    /// <summary>
    /// SLOW PATH with callback: Copy to pooled arrays and append via accumulator.
    /// </summary>
    private void AppendWithSlowPathWithCallback(
        string topic,
        int partition,
        long timestampMs,
        bool keyIsNull,
        int keyLength,
        int valueLength,
        IReadOnlyList<Header>? recordHeaders,
        Header[]? pooledHeaderArray,
        Action<RecordMetadata, Exception?> callback)
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

            // Append to accumulator with callback
            if (!_accumulator.TryAppendWithCallback(
                topic,
                partition,
                timestampMs,
                key,
                valueMemory,
                recordHeaders,
                pooledHeaderArray,
                callback))
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

    private async ValueTask ProduceInternalAsync(
        ProducerMessage<TKey, TValue> message,
        PooledValueTaskSource<RecordMetadata> completion,
        CancellationToken cancellationToken)
    {
        // Fast path: try to get topic metadata from cache synchronously
        // This avoids async state machine overhead for 99%+ of calls
        TopicInfo? topicInfo;
        if (!_metadataManager.TryGetCachedTopicMetadata(message.Topic, out topicInfo))
        {
            // Slow path: cache miss, need async refresh with MaxBlockMs timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.MaxBlockMs);

            try
            {
                topicInfo = await _metadataManager.GetTopicMetadataAsync(message.Topic, timeoutCts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Failed to fetch metadata for topic '{message.Topic}' within max.block.ms ({_options.MaxBlockMs}ms). " +
                    $"Ensure the topic exists and the Kafka cluster is reachable.");
            }
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
        var valueIsNull = message.Value is null;
        var value = valueIsNull ? PooledMemory.Null : SerializeValueToPooled(message.Value!, message.Topic, message.Headers);

        // Determine partition
        var partition = message.Partition
            ?? _partitioner.Partition(message.Topic, key.Span, keyIsNull, topicInfo.PartitionCount);

        // Get timestamp
        var timestamp = message.Timestamp ?? DateTimeOffset.UtcNow;
        var timestampMs = timestamp.ToUnixTimeMilliseconds();

        // Convert headers with minimal allocations
        IReadOnlyList<Header>? recordHeaders = null;
        Header[]? pooledHeaderArray = null;
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
                ArrayPool<Header>.Shared.Return(pooledHeaderArray);
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

    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfNotInitialized();

        // No channel to drain — all produce paths append directly to the accumulator.
        return _accumulator.FlushAsync(cancellationToken);
    }

    public ITransaction<TKey, TValue> BeginTransaction()
    {
        if (string.IsNullOrEmpty(_options.TransactionalId))
        {
            throw new InvalidOperationException("Producer is not transactional. Set TransactionalId in options.");
        }

        if (_transactionState == TransactionState.Uninitialized)
        {
            throw new InvalidOperationException(
                "Transactions not initialized. Call InitTransactionsAsync() before BeginTransaction().");
        }

        if (_transactionState == TransactionState.FatalError)
        {
            throw new InvalidOperationException(
                "Producer is in a fatal error state and cannot begin new transactions.");
        }

        if (_transactionState == TransactionState.InTransaction)
        {
            throw new InvalidOperationException(
                "A transaction is already in progress. Commit or abort it before starting a new one.");
        }

        if (_transactionState != TransactionState.Ready)
        {
            throw new InvalidOperationException(
                $"Cannot begin transaction in state: {_transactionState}");
        }

        _transactionState = TransactionState.InTransaction;
        _partitionsInTransaction.Clear();

        return new Transaction<TKey, TValue>(this);
    }

    public async ValueTask InitTransactionsAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_options.TransactionalId))
        {
            throw new InvalidOperationException(
                "Cannot initialize transactions: TransactionalId is not set in producer options.");
        }

        ThrowIfNotInitialized();

        await _transactionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {

            // Step 1: Find the transaction coordinator
            await FindTransactionCoordinatorAsync(cancellationToken).ConfigureAwait(false);

            // Step 2: Initialize the producer ID via the coordinator (with retries for retriable errors)
            const int maxInitRetries = 10;
            var initRetryDelayMs = 100;

            for (var initAttempt = 0; initAttempt < maxInitRetries; initAttempt++)
            {
                var connection = await _connectionPool.GetConnectionAsync(_transactionCoordinatorId, cancellationToken)
                    .ConfigureAwait(false);

                var initProducerIdVersion = _metadataManager.GetNegotiatedApiVersion(
                    ApiKey.InitProducerId,
                    InitProducerIdRequest.LowestSupportedVersion,
                    InitProducerIdRequest.HighestSupportedVersion);

                var request = new InitProducerIdRequest
                {
                    TransactionalId = _options.TransactionalId,
                    TransactionTimeoutMs = _options.TransactionTimeoutMs,
                    ProducerId = _producerId,
                    ProducerEpoch = _producerEpoch
                };

                var response = (InitProducerIdResponse)await connection
                    .SendAsync<InitProducerIdRequest, InitProducerIdResponse>(
                        request, initProducerIdVersion, cancellationToken)
                    .ConfigureAwait(false);

                if (response.ErrorCode == ErrorCode.ProducerFenced ||
                    response.ErrorCode == ErrorCode.TransactionalIdAuthorizationFailed)
                {
                    _transactionState = TransactionState.FatalError;
                    throw new TransactionException(response.ErrorCode,
                        $"InitProducerId failed with fatal error: {response.ErrorCode}")
                    {
                        TransactionalId = _options.TransactionalId
                    };
                }

                if (response.ErrorCode == ErrorCode.CoordinatorLoadInProgress ||
                    response.ErrorCode == ErrorCode.CoordinatorNotAvailable)
                {
                    _logger?.LogDebug(
                        "InitProducerId retriable error ({ErrorCode}, attempt {Attempt}/{MaxRetries}), retrying in {Delay}ms",
                        response.ErrorCode, initAttempt + 1, maxInitRetries, initRetryDelayMs);

                    await Task.Delay(initRetryDelayMs, cancellationToken).ConfigureAwait(false);
                    initRetryDelayMs = Math.Min(initRetryDelayMs * 2, 2000);
                    continue;
                }

                if (response.ErrorCode == ErrorCode.NotCoordinator)
                {
                    _logger?.LogDebug(
                        "InitProducerId got NotCoordinator (attempt {Attempt}/{MaxRetries}), re-discovering coordinator",
                        initAttempt + 1, maxInitRetries);

                    await Task.Delay(initRetryDelayMs, cancellationToken).ConfigureAwait(false);
                    initRetryDelayMs = Math.Min(initRetryDelayMs * 2, 2000);

                    // Re-discover the transaction coordinator
                    await FindTransactionCoordinatorAsync(cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (response.ErrorCode != ErrorCode.None)
                {
                    throw new TransactionException(response.ErrorCode,
                        $"InitProducerId failed: {response.ErrorCode}")
                    {
                        TransactionalId = _options.TransactionalId
                    };
                }

                // Success
                _producerId = response.ProducerId;
                _producerEpoch = response.ProducerEpoch;

                // Wire the producer ID/epoch into the accumulator for RecordBatch headers
                _accumulator.ProducerId = _producerId;
                _accumulator.ProducerEpoch = _producerEpoch;
                _accumulator.IsTransactional = true;

                // Reset sequence numbers for new epoch
                _accumulator.ResetSequenceNumbers();

                _transactionState = TransactionState.Ready;

                _logger?.LogDebug(
                    "Initialized transactions: ProducerId={ProducerId}, Epoch={Epoch}",
                    _producerId, _producerEpoch);
                return;
            }

            throw new TransactionException(ErrorCode.CoordinatorLoadInProgress,
                $"InitProducerId failed after {maxInitRetries} retries")
            {
                TransactionalId = _options.TransactionalId
            };
        }
        finally
        {
            _transactionLock.Release();
        }
    }

    private async ValueTask FindTransactionCoordinatorAsync(CancellationToken cancellationToken)
    {
        var brokers = _metadataManager.Metadata.GetBrokers();
        if (brokers.Count == 0)
        {
            throw new InvalidOperationException("No brokers available");
        }

        var request = new FindCoordinatorRequest
        {
            Key = _options.TransactionalId!,
            KeyType = CoordinatorType.Transaction
        };

        const int maxRetries = 5;
        var retryDelayMs = 100;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            var connection = await _connectionPool.GetConnectionAsync(brokers[0].NodeId, cancellationToken)
                .ConfigureAwait(false);

            var findCoordinatorVersion = _metadataManager.GetNegotiatedApiVersion(
                ApiKey.FindCoordinator,
                FindCoordinatorRequest.LowestSupportedVersion,
                FindCoordinatorRequest.HighestSupportedVersion);

            var response = await connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                request, findCoordinatorVersion, cancellationToken).ConfigureAwait(false);

            int nodeId;
            string host;
            int port;
            ErrorCode errorCode;

            if (response.Coordinators is { Count: > 0 })
            {
                var coordinator = response.Coordinators[0];
                errorCode = coordinator.ErrorCode;
                nodeId = coordinator.NodeId;
                host = coordinator.Host;
                port = coordinator.Port;
            }
            else
            {
                errorCode = response.ErrorCode;
                nodeId = response.NodeId;
                host = response.Host ?? throw new InvalidOperationException("Coordinator host is null");
                port = response.Port;
            }

            if (errorCode is ErrorCode.CoordinatorNotAvailable or ErrorCode.NotCoordinator)
            {
                _logger?.LogDebug(
                    "Transaction coordinator not available ({ErrorCode}, attempt {Attempt}/{MaxRetries}), retrying in {Delay}ms",
                    errorCode, attempt + 1, maxRetries, retryDelayMs);

                await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                retryDelayMs = Math.Min(retryDelayMs * 2, 1000);
                continue;
            }

            if (errorCode != ErrorCode.None)
            {
                throw new TransactionException(errorCode,
                    $"FindCoordinator for transaction failed: {errorCode}")
                {
                    TransactionalId = _options.TransactionalId
                };
            }

            _transactionCoordinatorId = nodeId;
            _connectionPool.RegisterBroker(nodeId, host, port);

            _logger?.LogDebug("Found transaction coordinator {NodeId} for {TransactionalId}",
                _transactionCoordinatorId, _options.TransactionalId);
            return;
        }

        throw new TransactionException(ErrorCode.CoordinatorNotAvailable,
            $"FindCoordinator for transaction failed after {maxRetries} retries")
        {
            TransactionalId = _options.TransactionalId
        };
    }

    internal async ValueTask AddPartitionsToTransactionAsync(
        IReadOnlyList<TopicPartition> partitions,
        CancellationToken cancellationToken)
    {
        // Group partitions by topic
        var topicPartitions = new Dictionary<string, List<int>>();
        foreach (var tp in partitions)
        {
            if (!topicPartitions.TryGetValue(tp.Topic, out var list))
            {
                list = [];
                topicPartitions[tp.Topic] = list;
            }
            list.Add(tp.Partition);
        }

        var topics = new List<AddPartitionsToTxnTopic>(topicPartitions.Count);
        foreach (var kvp in topicPartitions)
        {
            topics.Add(new AddPartitionsToTxnTopic
            {
                Name = kvp.Key,
                Partitions = kvp.Value
            });
        }

        var apiVersion = _metadataManager.GetNegotiatedApiVersion(
            ApiKey.AddPartitionsToTxn,
            AddPartitionsToTxnRequest.LowestSupportedVersion,
            AddPartitionsToTxnRequest.HighestSupportedVersion);

        var request = new AddPartitionsToTxnRequest
        {
            TransactionalId = _options.TransactionalId!,
            ProducerId = _producerId,
            ProducerEpoch = _producerEpoch,
            Topics = topics
        };

        const int maxRetries = 5;
        var retryDelayMs = 100;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            var connection = await _connectionPool.GetConnectionAsync(_transactionCoordinatorId, cancellationToken)
                .ConfigureAwait(false);

            var response = (AddPartitionsToTxnResponse)await connection
                .SendAsync<AddPartitionsToTxnRequest, AddPartitionsToTxnResponse>(
                    request, apiVersion, cancellationToken)
                .ConfigureAwait(false);

            // Check for retriable errors in the response
            var hasRetriableError = false;
            ErrorCode? firstNonRetriableError = null;
            string? errorContext = null;

            foreach (var topicResult in response.Results)
            {
                foreach (var partitionResult in topicResult.Partitions)
                {
                    if (partitionResult.ErrorCode == ErrorCode.None)
                        continue;

                    if (partitionResult.ErrorCode is ErrorCode.ConcurrentTransactions
                        or ErrorCode.CoordinatorLoadInProgress
                        or ErrorCode.CoordinatorNotAvailable)
                    {
                        hasRetriableError = true;
                    }
                    else if (partitionResult.ErrorCode == ErrorCode.NotCoordinator)
                    {
                        hasRetriableError = true;
                        // Re-discover coordinator on next retry
                        await FindTransactionCoordinatorAsync(cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        firstNonRetriableError = partitionResult.ErrorCode;
                        errorContext = $"{topicResult.Name}-{partitionResult.PartitionIndex}";
                    }
                }
            }

            if (firstNonRetriableError.HasValue)
            {
                throw new TransactionException(firstNonRetriableError.Value,
                    $"AddPartitionsToTxn failed for {errorContext}: {firstNonRetriableError.Value}")
                {
                    TransactionalId = _options.TransactionalId
                };
            }

            if (!hasRetriableError)
            {
                return; // Success
            }

            _logger?.LogDebug(
                "AddPartitionsToTxn retriable error (attempt {Attempt}/{MaxRetries}), retrying in {Delay}ms",
                attempt + 1, maxRetries, retryDelayMs);

            await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
            retryDelayMs = Math.Min(retryDelayMs * 2, 2000);
        }

        throw new TransactionException(ErrorCode.ConcurrentTransactions,
            $"AddPartitionsToTxn failed after {maxRetries} retries")
        {
            TransactionalId = _options.TransactionalId
        };
    }

    internal async ValueTask EndTransactionAsync(bool committed, CancellationToken cancellationToken)
    {
        const int maxRetries = 5;
        var retryDelayMs = 100;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            var connection = await _connectionPool.GetConnectionAsync(_transactionCoordinatorId, cancellationToken)
                .ConfigureAwait(false);

            var apiVersion = _metadataManager.GetNegotiatedApiVersion(
                ApiKey.EndTxn,
                EndTxnRequest.LowestSupportedVersion,
                EndTxnRequest.HighestSupportedVersion);

            var request = new EndTxnRequest
            {
                TransactionalId = _options.TransactionalId!,
                ProducerId = _producerId,
                ProducerEpoch = _producerEpoch,
                Committed = committed
            };

            var response = (EndTxnResponse)await connection
                .SendAsync<EndTxnRequest, EndTxnResponse>(
                    request, apiVersion, cancellationToken)
                .ConfigureAwait(false);

            if (response.ErrorCode == ErrorCode.None)
            {
                return;
            }

            if (response.ErrorCode == ErrorCode.ProducerFenced ||
                response.ErrorCode == ErrorCode.TransactionalIdAuthorizationFailed)
            {
                _transactionState = TransactionState.FatalError;
                throw new TransactionException(response.ErrorCode,
                    $"EndTxn ({(committed ? "commit" : "abort")}) failed: {response.ErrorCode}")
                {
                    TransactionalId = _options.TransactionalId
                };
            }

            if (response.ErrorCode is ErrorCode.CoordinatorLoadInProgress
                or ErrorCode.CoordinatorNotAvailable
                or ErrorCode.ConcurrentTransactions)
            {
                _logger?.LogDebug(
                    "EndTxn retriable error ({ErrorCode}, attempt {Attempt}/{MaxRetries}), retrying in {Delay}ms",
                    response.ErrorCode, attempt + 1, maxRetries, retryDelayMs);

                await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                retryDelayMs = Math.Min(retryDelayMs * 2, 2000);
                continue;
            }

            if (response.ErrorCode == ErrorCode.NotCoordinator)
            {
                _logger?.LogDebug(
                    "EndTxn got NotCoordinator (attempt {Attempt}/{MaxRetries}), re-discovering coordinator",
                    attempt + 1, maxRetries);

                await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                retryDelayMs = Math.Min(retryDelayMs * 2, 2000);
                await FindTransactionCoordinatorAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            throw new TransactionException(response.ErrorCode,
                $"EndTxn ({(committed ? "commit" : "abort")}) failed: {response.ErrorCode}")
            {
                TransactionalId = _options.TransactionalId
            };
        }

        throw new TransactionException(ErrorCode.CoordinatorLoadInProgress,
            $"EndTxn ({(committed ? "commit" : "abort")}) failed after {maxRetries} retries")
        {
            TransactionalId = _options.TransactionalId
        };
    }

    internal async ValueTask SendOffsetsToTransactionInternalAsync(
        IEnumerable<TopicPartitionOffset> offsets,
        string consumerGroupId,
        CancellationToken cancellationToken)
    {
        // Step 1: Add offsets to the transaction via the transaction coordinator
        var connection = await _connectionPool.GetConnectionAsync(_transactionCoordinatorId, cancellationToken)
            .ConfigureAwait(false);

        var addOffsetsVersion = _metadataManager.GetNegotiatedApiVersion(
            ApiKey.AddOffsetsToTxn,
            AddOffsetsToTxnRequest.LowestSupportedVersion,
            AddOffsetsToTxnRequest.HighestSupportedVersion);

        var addOffsetsRequest = new AddOffsetsToTxnRequest
        {
            TransactionalId = _options.TransactionalId!,
            ProducerId = _producerId,
            ProducerEpoch = _producerEpoch,
            GroupId = consumerGroupId
        };

        var addOffsetsResponse = (AddOffsetsToTxnResponse)await connection
            .SendAsync<AddOffsetsToTxnRequest, AddOffsetsToTxnResponse>(
                addOffsetsRequest, addOffsetsVersion, cancellationToken)
            .ConfigureAwait(false);

        if (addOffsetsResponse.ErrorCode != ErrorCode.None)
        {
            throw new TransactionException(addOffsetsResponse.ErrorCode,
                $"AddOffsetsToTxn failed: {addOffsetsResponse.ErrorCode}")
            {
                TransactionalId = _options.TransactionalId
            };
        }

        // Step 2: Find the group coordinator
        var brokers = _metadataManager.Metadata.GetBrokers();
        var brokerConnection = await _connectionPool.GetConnectionAsync(brokers[0].NodeId, cancellationToken)
            .ConfigureAwait(false);

        var findCoordVersion = _metadataManager.GetNegotiatedApiVersion(
            ApiKey.FindCoordinator,
            FindCoordinatorRequest.LowestSupportedVersion,
            FindCoordinatorRequest.HighestSupportedVersion);

        var findCoordRequest = new FindCoordinatorRequest
        {
            Key = consumerGroupId,
            KeyType = CoordinatorType.Group
        };

        var findCoordResponse = await brokerConnection
            .SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                findCoordRequest, findCoordVersion, cancellationToken)
            .ConfigureAwait(false);

        int groupCoordinatorId;
        ErrorCode findError;

        if (findCoordResponse.Coordinators is { Count: > 0 })
        {
            var coord = findCoordResponse.Coordinators[0];
            findError = coord.ErrorCode;
            groupCoordinatorId = coord.NodeId;
            _connectionPool.RegisterBroker(coord.NodeId, coord.Host, coord.Port);
        }
        else
        {
            findError = findCoordResponse.ErrorCode;
            groupCoordinatorId = findCoordResponse.NodeId;
            if (findCoordResponse.Host is not null)
            {
                _connectionPool.RegisterBroker(findCoordResponse.NodeId,
                    findCoordResponse.Host, findCoordResponse.Port);
            }
        }

        if (findError != ErrorCode.None)
        {
            throw new TransactionException(findError,
                $"FindCoordinator for consumer group '{consumerGroupId}' failed: {findError}")
            {
                TransactionalId = _options.TransactionalId
            };
        }

        // Step 3: Send TxnOffsetCommit to the group coordinator
        var groupConnection = await _connectionPool.GetConnectionAsync(groupCoordinatorId, cancellationToken)
            .ConfigureAwait(false);

        var txnOffsetCommitVersion = _metadataManager.GetNegotiatedApiVersion(
            ApiKey.TxnOffsetCommit,
            TxnOffsetCommitRequest.LowestSupportedVersion,
            TxnOffsetCommitRequest.HighestSupportedVersion);

        // Group offsets by topic
        var topicOffsets = new Dictionary<string, List<TxnOffsetCommitRequestPartition>>();
        foreach (var offset in offsets)
        {
            if (!topicOffsets.TryGetValue(offset.Topic, out var list))
            {
                list = [];
                topicOffsets[offset.Topic] = list;
            }
            list.Add(new TxnOffsetCommitRequestPartition
            {
                PartitionIndex = offset.Partition,
                CommittedOffset = offset.Offset
            });
        }

        var txnTopics = new List<TxnOffsetCommitRequestTopic>(topicOffsets.Count);
        foreach (var kvp in topicOffsets)
        {
            txnTopics.Add(new TxnOffsetCommitRequestTopic
            {
                Name = kvp.Key,
                Partitions = kvp.Value
            });
        }

        var txnOffsetCommitRequest = new TxnOffsetCommitRequest
        {
            TransactionalId = _options.TransactionalId!,
            GroupId = consumerGroupId,
            ProducerId = _producerId,
            ProducerEpoch = _producerEpoch,
            Topics = txnTopics
        };

        var txnOffsetCommitResponse = (TxnOffsetCommitResponse)await groupConnection
            .SendAsync<TxnOffsetCommitRequest, TxnOffsetCommitResponse>(
                txnOffsetCommitRequest, txnOffsetCommitVersion, cancellationToken)
            .ConfigureAwait(false);

        // Check for errors
        foreach (var topicResult in txnOffsetCommitResponse.Topics)
        {
            foreach (var partitionResult in topicResult.Partitions)
            {
                if (partitionResult.ErrorCode != ErrorCode.None)
                {
                    throw new TransactionException(partitionResult.ErrorCode,
                        $"TxnOffsetCommit failed for {topicResult.Name}-{partitionResult.PartitionIndex}: {partitionResult.ErrorCode}")
                    {
                        TransactionalId = _options.TransactionalId
                    };
                }
            }
        }
    }

    /// <inheritdoc />
    public ITopicProducer<TKey, TValue> ForTopic(string topic)
    {
        ArgumentNullException.ThrowIfNull(topic);
        if (_disposed)
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));

        return new TopicProducer<TKey, TValue>(this, topic, ownsProducer: false);
    }

    private async Task SenderLoopAsync(CancellationToken cancellationToken)
    {
        // PER-BROKER SENDER ARCHITECTURE: Each broker gets a dedicated BrokerSender with its own
        // channel and single-threaded send loop. This guarantees wire-order for same-partition
        // batches. Partition gates (capacity=1) serialize sends per partition, while the
        // per-broker in-flight semaphore enables pipelining across different partitions.
        //
        // SenderLoopAsync is now a simple router: drain batches, look up leader, enqueue to
        // the appropriate BrokerSender. BrokerSender handles coalescing, partition gate management,
        // retry, and in-flight limiting internally.

        var channelReader = _accumulator.ReadyBatches;

        try
        {
            while (await channelReader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (channelReader.TryRead(out var batch))
                {
                    // Complete delivery task (fire-and-forget semantic: batch is "ready")
                    batch.CompleteDelivery();

                    // NOTE: Inflight tracker registration is deferred to BrokerSender.SendCoalescedAsync
                    // (send time) rather than here (drain time). Registering at drain time caused a
                    // deadlock: queued-but-not-sent batches appeared as predecessors of retry batches
                    // in the inflight list, but could never complete because the retry batch held the
                    // partition gate. Moving registration to send time ensures only batches actually
                    // hitting the wire are tracked.

                    // Release buffer memory as soon as the sender dequeues the batch.
                    _accumulator.ReleaseMemory(batch.DataSize);

                    // Look up leader and route to the appropriate broker sender
                    try
                    {
                        var leader = _metadataManager.Metadata.GetPartitionLeader(
                            batch.TopicPartition.Topic,
                            batch.TopicPartition.Partition);

                        if (leader is null)
                        {
                            // No leader cached — do async lookup with timeout to prevent
                            // hanging the sender loop if metadata refresh is blocked
                            using var metadataCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                            metadataCts.CancelAfter(TimeSpan.FromSeconds(30));
                            try
                            {
                                leader = await _metadataManager.GetPartitionLeaderAsync(
                                    batch.TopicPartition.Topic,
                                    batch.TopicPartition.Partition,
                                    metadataCts.Token).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                            {
                                // Metadata lookup timed out — fall through to fail the batch below
                                leader = null;
                            }
                        }

                        if (leader is null)
                        {
                            // Still no leader — fail the batch
                            CompleteInflightEntry(batch);
                            try { batch.Fail(new KafkaException(ErrorCode.LeaderNotAvailable, "No leader available")); }
                            catch { /* Observe */ }
                            _accumulator.ReturnReadyBatch(batch);
                            _accumulator.OnBatchExitsPipeline();
                            continue;
                        }

                        var brokerSender = GetOrCreateBrokerSender(leader.NodeId);
                        brokerSender.Enqueue(batch);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        CompleteInflightEntry(batch);
                        try { batch.Fail(new OperationCanceledException(cancellationToken)); }
                        catch { /* Observe */ }
                        _accumulator.ReturnReadyBatch(batch);
                        _accumulator.OnBatchExitsPipeline();
                        throw;
                    }
                    catch (Exception ex)
                    {
                        CompleteInflightEntry(batch);
                        try { batch.Fail(ex); }
                        catch { /* Observe */ }
                        _accumulator.ReturnReadyBatch(batch);
                        _accumulator.OnBatchExitsPipeline();
                    }
                }
            }
        }
        finally
        {
            // Sender loop exiting — broker senders will be disposed in DisposeAsync
        }
    }

    /// <summary>
    /// Completes the inflight tracker entry for a batch, if registered.
    /// Signals any successors waiting via WaitForPredecessorAsync and returns entry to pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CompleteInflightEntry(ReadyBatch batch)
    {
        if (batch.InflightEntry is { } entry)
        {
            _inflightTracker?.Complete(entry);
            batch.InflightEntry = null;
        }
    }

    /// <summary>
    /// Creates a partition gate with capacity 1. This serializes sends per partition:
    /// at most one batch per partition is in-flight at a time. Combined with the
    /// IsRetry mechanism in BrokerSender, this prevents OutOfOrderSequenceNumber cascades
    /// where retry batches are overtaken by newer batches during the gate-release → re-enqueue window.
    ///
    /// Cross-partition throughput is unaffected: multiple partitions can still be pipelined
    /// up to MaxInFlightRequestsPerConnection, which is where the real throughput benefit lies.
    /// </summary>
    private static SemaphoreSlim CreatePartitionGate()
    {
        return new SemaphoreSlim(1, 1);
    }

    /// <summary>
    /// Gets or creates a BrokerSender for the given broker ID.
    /// Each broker gets a dedicated sender with its own channel and single-threaded send loop.
    /// </summary>
    private BrokerSender GetOrCreateBrokerSender(int brokerId)
    {
        // Epoch bump recovery is only for idempotent non-transactional producers.
        // Transactional producers manage epochs via InitTransactionsAsync.
        var isIdempotentNonTransactional = _options.EnableIdempotence && _options.TransactionalId is null;

        return _brokerSenders.GetOrAdd(brokerId, id => new BrokerSender(
            id,
            _connectionPool,
            _metadataManager,
            _accumulator,
            _options,
            _compressionCodecs,
            _inflightTracker,
            _statisticsCollector,
            _partitionSendGates,
            CreatePartitionGate,
            () => _produceApiVersion,
            version => Interlocked.CompareExchange(ref _produceApiVersion, version, -1),
            () => _accumulator.IsTransactional,
            EnsurePartitionInTransactionAsync,
            bumpEpoch: isIdempotentNonTransactional ? BumpEpochAsync : null,
            getCurrentEpoch: isIdempotentNonTransactional ? () => _producerEpoch : null,
            RerouteBatchToCurrentLeader,
            _interceptors is not null ? InvokeOnAcknowledgementForBatch : null,
            _logger));
    }

    /// <summary>
    /// Routes a batch to the current leader's BrokerSender.
    /// Used as a callback by BrokerSender when a retry discovers the leader has moved to a different broker.
    /// </summary>
    private void RerouteBatchToCurrentLeader(ReadyBatch batch)
    {
        try
        {
            // During disposal, don't create new BrokerSenders — fail the batch instead.
            // Without this guard, retries that discover leader changes create new senders
            // via GetOrCreateBrokerSender after the disposal loop has already snapshotted
            // _brokerSenders, leaving orphaned send loops that prevent process exit.
            if (_disposed)
            {
                CompleteInflightEntry(batch);
                try { batch.Fail(new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>))); }
                catch { /* Observe */ }
                _accumulator.ReturnReadyBatch(batch);
                _accumulator.OnBatchExitsPipeline();
                return;
            }

            var leader = _metadataManager.Metadata.GetPartitionLeader(
                batch.TopicPartition.Topic, batch.TopicPartition.Partition);

            if (leader is null)
            {
                CompleteInflightEntry(batch);
                try { batch.Fail(new KafkaException(ErrorCode.LeaderNotAvailable, $"No leader available for {batch.TopicPartition.Topic}-{batch.TopicPartition.Partition}")); }
                catch { /* Observe */ }
                _accumulator.ReturnReadyBatch(batch);
                _accumulator.OnBatchExitsPipeline();
                return;
            }

            GetOrCreateBrokerSender(leader.NodeId).Enqueue(batch);
        }
        catch (Exception ex)
        {
            CompleteInflightEntry(batch);
            try { batch.Fail(ex); }
            catch { /* Observe */ }
            _accumulator.ReturnReadyBatch(batch);
            _accumulator.OnBatchExitsPipeline();
        }
    }

    /// <summary>
    /// Ensures a partition is registered in the current transaction.
    /// Used as a callback by BrokerSender for transactional producers.
    /// </summary>
    private async ValueTask EnsurePartitionInTransactionAsync(
        TopicPartition topicPartition, CancellationToken cancellationToken)
    {
        bool needsRegistration;
        lock (_partitionsInTransaction)
        {
            needsRegistration = _partitionsInTransaction.Add(topicPartition);
        }

        if (needsRegistration)
        {
            await AddPartitionsToTransactionAsync([topicPartition], cancellationToken)
                .ConfigureAwait(false);
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

    /// <inheritdoc />
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));

        // Fast path: already initialized (volatile read provides acquire semantics)
        if (_initialized)
            return;

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring lock
            if (_initialized)
                return;

            try
            {
                await _metadataManager.InitializeAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Failed to fetch initial metadata within max.block.ms ({_options.MaxBlockMs}ms). " +
                    $"Ensure the Kafka cluster is reachable and the bootstrap servers are correct.");
            }

            if (_options.EnableIdempotence && _options.TransactionalId is null && !_idempotentInitialized)
            {
                await InitIdempotentProducerAsync(cancellationToken).ConfigureAwait(false);
            }

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> if the producer has not been initialized.
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
            "Call InitializeAsync() or use BuildAsync() before producing messages.");
    }

    /// <summary>
    /// Fetches topic metadata synchronously for Send() when the topic is not in the cache.
    /// This is a rare path — only hit on the first produce to a new topic.
    /// Uses TaskCreationOptions.LongRunning to avoid thread pool starvation.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private TopicInfo FetchTopicMetadataSync(string topic)
    {
        using var timeoutCts = new CancellationTokenSource(_options.MaxBlockMs);
        var token = timeoutCts.Token;
        TopicInfo topicInfo;
        try
        {
            // Use LongRunning to get a dedicated thread — avoids thread pool starvation
            // when multiple producers fetch topic metadata simultaneously.
            topicInfo = Task.Factory.StartNew(
                () => _metadataManager.GetTopicMetadataAsync(topic, token).AsTask(),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap().GetAwaiter().GetResult()!;
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(
                $"Failed to fetch metadata for topic '{topic}' within max.block.ms ({_options.MaxBlockMs}ms). " +
                $"Ensure the topic exists and the Kafka cluster is reachable.");
        }

        if (topicInfo is null || topicInfo.PartitionCount == 0)
        {
            throw new InvalidOperationException($"Topic '{topic}' not found or has no partitions");
        }

        UpdateCachedTopicInfo(topic, topicInfo);
        return topicInfo;
    }

    /// <summary>
    /// Initializes the idempotent producer by obtaining a producer ID from any broker.
    /// This enables sequence number assignment for duplicate detection without full transactions.
    /// </summary>
    private async ValueTask InitIdempotentProducerAsync(CancellationToken cancellationToken)
    {
        // Double-check under lock to prevent concurrent initialization
        await _transactionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_idempotentInitialized)
            {
                return;
            }

            var initProducerIdVersion = _metadataManager.GetNegotiatedApiVersion(
                ApiKey.InitProducerId,
                InitProducerIdRequest.LowestSupportedVersion,
                InitProducerIdRequest.HighestSupportedVersion);

            // Retry with backoff for retriable errors (e.g. CoordinatorLoadInProgress during broker startup)
            var retryDelayMs = _options.RetryBackoffMs;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // For non-transactional idempotent producers, send to any broker (no coordinator needed)
                var brokers = _metadataManager.Metadata.GetBrokers();
                if (brokers.Count == 0)
                {
                    throw new InvalidOperationException("No brokers available for idempotent producer initialization");
                }

                var connection = await _connectionPool.GetConnectionAsync(brokers[0].NodeId, cancellationToken)
                    .ConfigureAwait(false);

                var request = new InitProducerIdRequest
                {
                    TransactionalId = null,
                    TransactionTimeoutMs = -1,
                    ProducerId = _producerId,
                    ProducerEpoch = _producerEpoch
                };

                var response = (InitProducerIdResponse)await connection
                    .SendAsync<InitProducerIdRequest, InitProducerIdResponse>(
                        request, initProducerIdVersion, cancellationToken)
                    .ConfigureAwait(false);

                if (response.ErrorCode == ErrorCode.None)
                {
                    _producerId = response.ProducerId;
                    _producerEpoch = response.ProducerEpoch;

                    // Wire the producer ID/epoch into the accumulator for RecordBatch headers
                    _accumulator.ProducerId = _producerId;
                    _accumulator.ProducerEpoch = _producerEpoch;

                    _idempotentInitialized = true;

                    _logger?.LogDebug(
                        "Initialized idempotent producer: ProducerId={ProducerId}, Epoch={Epoch}",
                        _producerId, _producerEpoch);
                    return;
                }

                if (!response.ErrorCode.IsRetriable())
                {
                    throw new KafkaException(response.ErrorCode,
                        $"Failed to initialize idempotent producer: {response.ErrorCode}");
                }

                _logger?.LogDebug(
                    "InitProducerId returned retriable error {ErrorCode}, retrying in {DelayMs}ms",
                    response.ErrorCode, retryDelayMs);

                await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                retryDelayMs = Math.Min(retryDelayMs * 2, _options.RetryBackoffMaxMs);
            }
        }
        finally
        {
            _transactionLock.Release();
        }
    }

    /// <summary>
    /// Bumps the producer epoch by calling InitProducerId with the current PID/epoch.
    /// The broker returns the same PID with an incremented epoch. Resets all partition
    /// sequence numbers to 0 so subsequent batches use the new epoch.
    /// Serialized by _transactionLock. The expectedEpoch parameter prevents redundant bumps
    /// when multiple partitions fail in the same produce response — if another thread already
    /// bumped the epoch, this returns the current state immediately.
    /// </summary>
    internal async ValueTask<(long ProducerId, short ProducerEpoch)> BumpEpochAsync(
        short expectedEpoch, CancellationToken cancellationToken)
    {
        await _transactionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Another thread already bumped — return current state
            if (_producerEpoch != expectedEpoch)
            {
                return (_producerId, _producerEpoch);
            }

            var initProducerIdVersion = _metadataManager.GetNegotiatedApiVersion(
                ApiKey.InitProducerId,
                InitProducerIdRequest.LowestSupportedVersion,
                InitProducerIdRequest.HighestSupportedVersion);

            var retryDelayMs = _options.RetryBackoffMs;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var brokers = _metadataManager.Metadata.GetBrokers();
                if (brokers.Count == 0)
                {
                    throw new InvalidOperationException("No brokers available for epoch bump");
                }

                var connection = await _connectionPool.GetConnectionAsync(brokers[0].NodeId, cancellationToken)
                    .ConfigureAwait(false);

                var request = new InitProducerIdRequest
                {
                    TransactionalId = null,
                    TransactionTimeoutMs = -1,
                    ProducerId = _producerId,
                    ProducerEpoch = _producerEpoch
                };

                var response = (InitProducerIdResponse)await connection
                    .SendAsync<InitProducerIdRequest, InitProducerIdResponse>(
                        request, initProducerIdVersion, cancellationToken)
                    .ConfigureAwait(false);

                if (response.ErrorCode == ErrorCode.None)
                {
                    _producerId = response.ProducerId;
                    _producerEpoch = response.ProducerEpoch;

                    _accumulator.ProducerId = _producerId;
                    _accumulator.ProducerEpoch = _producerEpoch;
                    _accumulator.ResetSequenceNumbers();

                    _logger?.LogDebug(
                        "Bumped producer epoch: ProducerId={ProducerId}, Epoch={Epoch}",
                        _producerId, _producerEpoch);
                    return (_producerId, _producerEpoch);
                }

                if (!response.ErrorCode.IsRetriable())
                {
                    throw new KafkaException(response.ErrorCode,
                        $"Failed to bump producer epoch: {response.ErrorCode}");
                }

                _logger?.LogDebug(
                    "BumpEpoch InitProducerId returned retriable error {ErrorCode}, retrying in {DelayMs}ms",
                    response.ErrorCode, retryDelayMs);

                await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                retryDelayMs = Math.Min(retryDelayMs * 2, _options.RetryBackoffMaxMs);
            }
        }
        finally
        {
            _transactionLock.Release();
        }
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
    /// Converts Headers collection to a pooled Header array with minimal allocations.
    /// Always uses ArrayPool to avoid per-message heap allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IReadOnlyList<Header> ConvertHeaders(Headers headers, out Header[]? pooledArray)
    {
        var count = headers.Count;

        // Always pool to eliminate per-message allocations
        var result = ArrayPool<Header>.Shared.Rent(count);
        pooledArray = result;

        // Use index-based iteration to avoid enumerator boxing allocation
        for (var i = 0; i < count; i++)
        {
            var h = headers[i];
            result[i] = new Header
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
            // 1. Flush accumulator and complete its channel - sender will process remaining batches
            await _accumulator.CloseAsync(shutdownCts.Token).ConfigureAwait(false);

            // 2. Wait for sender to drain remaining batches
            // CRITICAL: Don't cancel _senderCts yet - sender needs to process flushed batches
            // The sender will exit naturally when the ready batches channel completes (done in CloseAsync)
            if (hasTimeout)
                await _senderTask.WaitAsync(shutdownCts.Token).ConfigureAwait(false);
            else
                await _senderTask.ConfigureAwait(false);

            // 3. Now safe to cancel linger loop (it's already exited or will exit soon)
            _senderCts.Cancel();
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
                await _senderTask
                    .WaitAsync(TimeSpan.FromSeconds(5))
                    .ConfigureAwait(false);
            }
            catch
            {
                // Ignore timeout or cancellation - we tried our best to complete gracefully
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

        // Dispose all per-broker senders (drains channels, waits for in-flight responses).
        // Loop until no new senders were created during the iteration — a retry's
        // RerouteBatchToCurrentLeader callback could race with the _disposed guard and
        // create a new sender after we've started iterating.
        int previousCount;
        do
        {
            previousCount = _brokerSenders.Count;
            foreach (var (_, sender) in _brokerSenders)
            {
                try
                {
                    await sender.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to dispose broker sender");
                }
            }
        } while (_brokerSenders.Count > previousCount);
        _brokerSenders.Clear();

        _senderCts.Dispose();
        _lingerTimer.Dispose();
        _transactionLock.Dispose();
        _initLock.Dispose();

        foreach (var gate in _partitionSendGates.Values)
        {
            gate.Dispose();
        }
        _partitionSendGates.Clear();

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
/// Transaction implementation.
/// </summary>
/// <remarks>
/// Transactions are designed for single-threaded sequential use: only one transaction
/// can be active at a time per producer. State transitions use volatile semantics
/// rather than locks because the Kafka transaction API guarantees sequential access
/// (BeginTransaction → Produce/Commit/Abort → BeginTransaction).
/// </remarks>
internal sealed class Transaction<TKey, TValue> : ITransaction<TKey, TValue>
{
    private readonly KafkaProducer<TKey, TValue> _producer;
    private bool _committed;
    private bool _aborted;

    public Transaction(KafkaProducer<TKey, TValue> producer)
    {
        _producer = producer;
    }

    private void ThrowIfProducerDisposed()
    {
        if (_producer._disposed)
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));
    }

    public async ValueTask<RecordMetadata> ProduceAsync(
        ProducerMessage<TKey, TValue> message,
        CancellationToken cancellationToken = default)
    {
        ThrowIfProducerDisposed();

        if (_committed || _aborted)
            throw new InvalidOperationException("Transaction is already completed");

        // Partition registration with AddPartitionsToTxn is handled automatically
        // by BrokerSender before the ProduceRequest is sent to the broker.
        return await _producer.ProduceAsync(message, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfProducerDisposed();

        if (_committed || _aborted)
            throw new InvalidOperationException("Transaction is already completed");

        _producer._transactionState = TransactionState.CommittingTransaction;

        try
        {
            // Flush all pending messages before committing
            await _producer.FlushAsync(cancellationToken).ConfigureAwait(false);

            await _producer.EndTransactionAsync(committed: true, cancellationToken).ConfigureAwait(false);
            _committed = true;
        }
        finally
        {
            lock (_producer._partitionsInTransaction)
            {
                _producer._partitionsInTransaction.Clear();
            }
            _producer._transactionState = TransactionState.Ready;
        }
    }

    public async ValueTask AbortAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfProducerDisposed();

        if (_committed || _aborted)
            throw new InvalidOperationException("Transaction is already completed");

        _producer._transactionState = TransactionState.AbortingTransaction;

        try
        {
            await _producer.EndTransactionAsync(committed: false, cancellationToken).ConfigureAwait(false);
            _aborted = true;
        }
        finally
        {
            lock (_producer._partitionsInTransaction)
            {
                _producer._partitionsInTransaction.Clear();
            }
            _producer._transactionState = TransactionState.Ready;
        }
    }

    public async ValueTask SendOffsetsToTransactionAsync(
        IEnumerable<TopicPartitionOffset> offsets,
        string consumerGroupId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfProducerDisposed();

        if (_committed || _aborted)
            throw new InvalidOperationException("Transaction is already completed");

        await _producer.SendOffsetsToTransactionInternalAsync(offsets, consumerGroupId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_committed && !_aborted && !_producer._disposed
            && _producer._transactionState == TransactionState.InTransaction)
        {
            // Abort on dispose if not completed and transaction is actually in progress
            try
            {
                await AbortAsync().ConfigureAwait(false);
            }
            catch (TransactionException)
            {
                // Best-effort abort during disposal — if the broker rejects it
                // (e.g. InvalidTxnState because no messages were produced),
                // just clean up state and move on.
                lock (_producer._partitionsInTransaction)
                {
                    _producer._partitionsInTransaction.Clear();
                }
                _producer._transactionState = TransactionState.Ready;
            }
        }
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
internal readonly struct HeaderListWrapper : IReadOnlyList<Header>
{
    private readonly Header[] _array;
    private readonly int _count;

    public HeaderListWrapper(Header[] array, int count)
    {
        _array = array;
        _count = count;
    }

    public Header this[int index]
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

    IEnumerator<Header> IEnumerable<Header>.GetEnumerator() => GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator : IEnumerator<Header>
    {
        private readonly Header[] _array;
        private readonly int _count;
        private int _index;

        public Enumerator(Header[] array, int count)
        {
            _array = array;
            _count = count;
            _index = -1;
        }

        public Header Current => _array[_index];

        object System.Collections.IEnumerator.Current => Current;

        public bool MoveNext() => ++_index < _count;

        public void Reset() => _index = -1;

        public void Dispose() { }
    }
}
