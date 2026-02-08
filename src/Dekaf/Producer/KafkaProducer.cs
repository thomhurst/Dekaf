using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
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

    // Pipelining: allow multiple batches to be sent concurrently (up to MaxInFlightRequestsPerConnection)
    // This dramatically improves throughput by overlapping network round-trips for DIFFERENT partitions.
    //
    // Per-partition ordering: a per-partition semaphore ensures only one batch per partition is in-flight
    // at a time. The broker requires strictly ascending sequence numbers per partition for idempotent
    // producers; sending batches out-of-order causes OutOfOrderSequenceNumber rejections.
    //
    // Thread-safety model:
    // - _senderPipelineSemaphore: Acquired in SenderLoopAsync, released in SendBatchWithCleanupAsync.
    //   Bounds fire-and-forget task count to prevent thread pool saturation when buffer fills.
    //   Cost: 2 Interlocked ops per batch (~0.002 per message). Must be separate from
    //   _sendConcurrencySemaphore to avoid head-of-line blocking (see SenderLoopAsync comments).
    // - _sendConcurrencySemaphore: Acquired AFTER per-partition gate in SendBatchWithCleanupAsync.
    //   Limits actual concurrent network sends. Acquired after partition gate so that partition
    //   gate waiters don't consume global send slots (which would starve other partitions).
    // - _partitionSendGates: ConcurrentDictionary is thread-safe; per-partition semaphores ensure ordering
    // - _inFlightSendCount: Modified only via Interlocked operations, read via Volatile.Read
    // - _allSendsCompleted: Signaling coordinated with _inFlightSendCount via _sendCompletionLock
    // - _sendCompletionLock: Protects the atomicity of count transition + event signal pairs
    private readonly SemaphoreSlim _senderPipelineSemaphore;
    private readonly SemaphoreSlim _sendConcurrencySemaphore;
    private readonly ConcurrentDictionary<TopicPartition, SemaphoreSlim> _partitionSendGates = new();
    private long _inFlightSendCount;
    private readonly ManualResetEventSlim _allSendsCompleted = new(true); // Initially signaled (no sends in flight)
    private readonly object _sendCompletionLock = new(); // Prevents race between Reset() and Set()


    // Channel-based worker pool for thread-safe produce operations
    private readonly Channel<ProduceWorkItem<TKey, TValue>> _workChannel;
    private readonly Task[] _workerTasks;
    private readonly int _workerCount;

    // Tracks pending messages in the work channel to prevent ordering issues.
    // When > 0, the fire-and-forget fast path must be disabled to ensure messages
    // appended via the slow path (worker thread) are not overtaken by fast-path messages.
    private int _pendingChannelMessages;

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

    // Thread-local reusable single-element arrays for ProduceRequest construction.
    // Avoids per-batch array allocations. Safe because async methods don't run
    // concurrently on the same thread, and elements are overwritten before each use.
    [ThreadStatic]
    private static ProduceRequestTopicData[]? t_topicDataArray;
    [ThreadStatic]
    private static ProduceRequestPartitionData[]? t_partitionDataArray;
    [ThreadStatic]
    private static RecordBatch[]? t_recordBatchArray;

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

        // Pipeline depth limiter: bounds fire-and-forget task count to prevent thread pool saturation.
        // 4x MaxInFlightRequestsPerConnection provides enough depth for concurrent sends plus
        // partition gate waiters, without allowing hundreds of tasks to pile up.
        var pipelineDepth = options.MaxInFlightRequestsPerConnection * 4;
        _senderPipelineSemaphore = new SemaphoreSlim(pipelineDepth, pipelineDepth);

        // Send concurrency limiter: limits actual concurrent network sends.
        // Acquired AFTER per-partition gate to avoid head-of-line blocking.
        _sendConcurrencySemaphore = new SemaphoreSlim(options.MaxInFlightRequestsPerConnection, options.MaxInFlightRequestsPerConnection);

        _senderCts = new CancellationTokenSource();
        // Use 1ms check interval for low-latency awaited produces.
        // ShouldFlush() is smart: it returns true immediately for batches with completion
        // sources (awaited produces), but waits for full LingerMs for fire-and-forget batches.
        // This provides low latency for ProduceAsync while maintaining efficient batching for Send.
        _lingerTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(1));
        _senderTask = SenderLoopAsync(_senderCts.Token);
        _lingerTask = LingerLoopAsync(_senderCts.Token);

        // Set up worker for channel-based produce operations (slow path).
        // Use a SINGLE worker to preserve message ordering within the channel.
        // The channel is only used during startup (before metadata is cached) and when
        // the fast path is unavailable. After metadata is cached, Send() uses the
        // zero-allocation fast path that bypasses the channel entirely.
        // Multiple workers would break FIFO ordering by processing messages concurrently.
        _workerCount = 1;
        _workChannel = Channel.CreateUnbounded<ProduceWorkItem<TKey, TValue>>(
            new UnboundedChannelOptions
            {
                SingleReader = true,    // Single worker for ordering guarantee
                SingleWriter = false    // Multiple callers write
            });

        // Start single worker task
        _workerTasks = new Task[_workerCount];
        _workerTasks[0] = ProcessWorkAsync(_senderCts.Token);

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
        // BACKPRESSURE: Reserve memory before queueing to prevent unbounded channel growth.
        // This ensures backpressure even for ProduceAsync calls that aren't awaited.
        var estimatedSize = EstimateMessageSizeForBackpressure(message);

        // Use async reservation for the async path to avoid blocking threads
        await _accumulator.ReserveMemoryAsyncForBackpressure(estimatedSize, cancellationToken).ConfigureAwait(false);

        var memoryOwnershipTransferred = false;
        try
        {
            // Rent completion source from pool - it will auto-return when awaited
            var completion = _valueTaskSourcePool.Rent();

            var workItem = new ProduceWorkItem<TKey, TValue>(message, completion, cancellationToken, estimatedSize);

            // PRE-QUEUE: Channel write can be cancelled (throws OperationCanceledException)
            // If cancelled here, completion source never gets used and returns to pool
            await _workChannel.Writer.WriteAsync(workItem, cancellationToken).ConfigureAwait(false);

            // POST-QUEUE: Memory ownership transferred to work item.
            // The worker releases PreReservedBytes immediately when it picks up the item.
            memoryOwnershipTransferred = true;

            // Message WILL be delivered, but caller can stop waiting via cancellation token.
            if (cancellationToken.CanBeCanceled)
            {
                return await AwaitWithCancellation(completion, cancellationToken).ConfigureAwait(false);
            }

            return await completion.Task.ConfigureAwait(false);
        }
        catch
        {
            // Only release if we still own the memory (WriteAsync failed before queueing)
            if (!memoryOwnershipTransferred)
            {
                _accumulator.ReleaseMemory(estimatedSize);
            }
            throw;
        }
    }

    /// <inheritdoc />
    public void Send(ProducerMessage<TKey, TValue> message)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));

        // Apply OnSend interceptors before serialization
        message = ApplyOnSendInterceptors(message);

        // Fast path: Try synchronous fire-and-forget produce if metadata is cached.
        // TryProduceSyncFireAndForget returns false when metadata isn't available, naturally
        // falling through to the channel-based slow path for metadata initialization.
        // We don't gate on _pendingChannelMessages here because doing so with a single worker
        // creates a death spiral: initial channel messages prevent the fast path from activating,
        // forcing ALL subsequent messages through the single-threaded worker, which can never
        // drain the channel fast enough to re-enable the fast path.
        if (TryProduceSyncFireAndForget(message))
        {
            return;
        }

        // Slow path: Fall back to channel-based async processing
        // This handles the case where metadata isn't initialized or cached

        // BACKPRESSURE: Reserve memory before queueing to prevent unbounded channel growth.
        // The worker releases pre-reserved memory immediately when it picks up the item,
        // then AppendAsync reserves memory based on actual serialized size.
        var estimatedSize = EstimateMessageSizeForBackpressure(message);
        _accumulator.ReserveMemorySyncForBackpressure(estimatedSize);

        // Increment BEFORE writing to channel to prevent race with fast-path check.
        // The worker decrements AFTER appending, ensuring all slow-path messages are
        // visible before the fast path can resume.
        Interlocked.Increment(ref _pendingChannelMessages);

        PooledValueTaskSource<RecordMetadata>? completion = null;
        try
        {
            completion = _valueTaskSourcePool.Rent();
            // Fire-and-forget: ensure completion source returns to pool when batch completes
            // Uses zero-allocation callback instead of async Task to avoid GC pressure
            completion.ObserveForFireAndForget();

            var workItem = new ProduceWorkItem<TKey, TValue>(message, completion, CancellationToken.None, estimatedSize);

            if (!_workChannel.Writer.TryWrite(workItem))
            {
                Interlocked.Decrement(ref _pendingChannelMessages);
                completion.TrySetException(new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>)));
                // Don't release here - let the catch block handle it to avoid double release
                throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));
            }

            // Success - memory ownership transferred to work item, don't release
            return;
        }
        catch
        {
            Interlocked.Decrement(ref _pendingChannelMessages);
            _accumulator.ReleaseMemory(estimatedSize);
            throw;
        }
    }

    /// <inheritdoc />
    public void Send(string topic, TKey? key, TValue value)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));

        // When interceptors are present, we must create a ProducerMessage so interceptors can operate
        if (_interceptors is not null)
        {
            Send(new ProducerMessage<TKey, TValue> { Topic = topic, Key = key, Value = value });
            return;
        }

        // Fast path: Try synchronous fire-and-forget produce if metadata is cached.
        // See Send(ProducerMessage) for explanation of why we don't gate on _pendingChannelMessages.
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

    /// <summary>
    /// Estimates message size conservatively for backpressure purposes.
    /// Used when queueing to the work channel before serialization occurs.
    /// The actual size will be determined after serialization in AppendAsync.
    /// </summary>
    private static int EstimateMessageSizeForBackpressure(ProducerMessage<TKey, TValue> message)
    {
        const int recordOverhead = 20;
        const int defaultKeyEstimate = 100;
        const int defaultValueEstimate = 1000;

        // Estimate key size - be more precise for common types
        var keySize = message.Key switch
        {
            null => 0,
            string s => s.Length * 3, // Max UTF8 expansion
            byte[] b => b.Length,
            _ => defaultKeyEstimate
        };

        // Estimate value size - be more precise for common types
        var valueSize = message.Value switch
        {
            null => 0,
            string s => s.Length * 3, // Max UTF8 expansion
            byte[] b => b.Length,
            _ => defaultValueEstimate
        };

        // Conservative header estimate
        var headerSize = 0;
        if (message.Headers is { Count: > 0 })
        {
            headerSize = message.Headers.Count * 50;
        }

        return recordOverhead + keySize + valueSize + headerSize;
    }

    /// <inheritdoc />
    public void Send(ProducerMessage<TKey, TValue> message, Action<RecordMetadata, Exception?> deliveryHandler)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));

        ArgumentNullException.ThrowIfNull(deliveryHandler);

        // Apply OnSend interceptors before serialization
        message = ApplyOnSendInterceptors(message);

        // Fast path: Try synchronous produce if metadata is initialized and cached.
        // See Send(ProducerMessage) for explanation of why we don't gate on _pendingChannelMessages.
        if (TryProduceSyncWithHandler(message, deliveryHandler))
        {
            return;
        }

        // Slow path: Fall back to channel-based async processing

        // BACKPRESSURE: Reserve memory before queueing to prevent unbounded channel growth.
        var estimatedSize = EstimateMessageSizeForBackpressure(message);
        _accumulator.ReserveMemorySyncForBackpressure(estimatedSize);

        // Increment BEFORE writing to channel to prevent race with fast-path check.
        Interlocked.Increment(ref _pendingChannelMessages);

        PooledValueTaskSource<RecordMetadata>? completion = null;
        try
        {
            completion = _valueTaskSourcePool.Rent();
            completion.SetDeliveryHandler(deliveryHandler);
            // Fire-and-forget: ensure completion source returns to pool when batch completes
            // Uses zero-allocation callback instead of async Task to avoid GC pressure
            completion.ObserveForFireAndForget();

            var workItem = new ProduceWorkItem<TKey, TValue>(message, completion, CancellationToken.None, estimatedSize);

            if (!_workChannel.Writer.TryWrite(workItem))
            {
                Interlocked.Decrement(ref _pendingChannelMessages);
                completion.TrySetException(new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>)));
                // Don't release here - let the catch block handle it to avoid double release
                throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));
            }

            // Success - memory ownership transferred to work item, don't release
            return;
        }
        catch
        {
            Interlocked.Decrement(ref _pendingChannelMessages);
            _accumulator.ReleaseMemory(estimatedSize);
            throw;
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

    private async Task ProcessWorkAsync(CancellationToken cancellationToken)
    {
        await foreach (var work in _workChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                // OPTIMIZATION: Release pre-reserved memory IMMEDIATELY when we pick up the work item.
                // This minimizes peak memory usage and maximizes throughput by avoiding double-reservation.
                // The pre-reservation's purpose is backpressure (limiting queue depth), not tracking actual usage.
                // AppendAsync will reserve the actual memory it needs based on serialized size.
                if (work.PreReservedBytes > 0)
                {
                    _accumulator.ReleaseMemory(work.PreReservedBytes);
                }

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
            finally
            {
                // Decrement AFTER appending to ensure the message is visible in the batch
                // before the fast path in Send() can resume. This prevents ordering violations
                // between slow-path (channel) and fast-path (inline) messages.
                Interlocked.Decrement(ref _pendingChannelMessages);
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

        await _transactionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Ensure metadata is initialized
            await EnsureInitializedWithTimeoutAsync(cancellationToken).ConfigureAwait(false);

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
        // PIPELINED ARCHITECTURE: Send multiple batches concurrently (up to MaxInFlightRequestsPerConnection)
        // This overlaps network round-trips for DIFFERENT partitions, dramatically improving throughput.
        // Without pipelining: throughput = 1 / network_latency (e.g., 30 batches/sec at 33ms latency)
        // With pipelining (5 in-flight): throughput = 5 / network_latency (e.g., 150 batches/sec)
        //
        // ORDERING: Within-partition ordering is guaranteed by per-partition semaphores.
        // Only one batch per partition is in-flight at a time, ensuring the broker receives
        // batches with strictly ascending sequence numbers. Cross-partition batches are pipelined
        // freely. If the idempotent producer is enabled, DuplicateSequenceNumber is treated as success
        // (safe retransmit detection).
        //
        // BATCH COALESCING: When multiple batches are ready simultaneously, they are grouped by
        // broker and sent as a single ProduceRequest per broker. This reduces network round-trips
        // proportional to the number of partitions per broker. When only 1 batch is ready, the
        // existing single-batch path runs with zero overhead.
        //
        // SEMAPHORE ORDERING (critical for throughput):
        // 1. _senderPipelineSemaphore: Acquired HERE before dispatch. Bounds fire-and-forget task
        //    count to prevent thread pool saturation when hundreds of batches are ready.
        // 2. Per-partition gate: Acquired FIRST inside SendBatchWithCleanupAsync for ordering.
        //    For coalesced sends, acquired non-blocking in DispatchCoalescedBatches.
        // 3. _sendConcurrencySemaphore: Acquired SECOND inside SendBatchWithCleanupAsync, AFTER
        //    the partition gate. This is critical: if the global send semaphore were acquired before
        //    the partition gate, tasks waiting on their partition gate would consume global slots,
        //    starving other partitions (head-of-line blocking). With partition gate first, only
        //    tasks ready to send consume global slots.

        var channelReader = _accumulator.ReadyBatches;
        var maxDrain = _options.MaxInFlightRequestsPerConnection * 4;
        var drainBuffer = ArrayPool<ReadyBatch>.Shared.Rent(maxDrain);

        try
        {
            while (await channelReader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                // Drain all currently-available batches (up to pipeline depth)
                var drainCount = 0;
                while (drainCount < maxDrain && channelReader.TryRead(out var batch))
                {
                    // Complete delivery task (fire-and-forget semantic: batch is "ready")
                    batch.CompleteDelivery();

                    // Release buffer memory as soon as the sender dequeues the batch.
                    _accumulator.ReleaseMemory(batch.DataSize);

                    // Track in-flight send count for disposal coordination
                    IncrementInFlightSendCount();

                    // Limit pipeline depth to prevent spawning hundreds of concurrent tasks.
                    try
                    {
                        await _senderPipelineSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        DecrementInFlightSendCount();
                        throw;
                    }

                    drainBuffer[drainCount++] = batch;
                }

                if (drainCount == 0)
                    continue;

                if (drainCount == 1)
                {
                    // Single batch: existing path (no coalescing overhead)
                    _ = SendBatchWithCleanupAsync(drainBuffer[0], cancellationToken);
                }
                else
                {
                    // Multiple batches: group by broker and coalesce into fewer requests
                    DispatchCoalescedBatches(drainBuffer, drainCount, cancellationToken);
                }
            }
        }
        finally
        {
            ArrayPool<ReadyBatch>.Shared.Return(drainBuffer, clearArray: true);
        }

        // Channel completed - wait for all in-flight sends to finish before exiting
        await WaitForInFlightSendsAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a batch and handles all cleanup (error handling, semaphore release, pool return).
    /// This method is fire-and-forget from SenderLoopAsync to enable pipelining.
    /// </summary>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask SendBatchWithCleanupAsync(ReadyBatch batch, CancellationToken cancellationToken)
    {
        // All semaphore acquisitions are inside try/finally to guarantee cleanup on cancellation.
        // Without this, cancellation during partition gate or send semaphore waits would leak
        // semaphore slots, in-flight counts, and batch resources.
        var partitionGateAcquired = false;
        var sendSemaphoreAcquired = false;
        var partitionGate = _partitionSendGates.GetOrAdd(batch.TopicPartition, _ => new SemaphoreSlim(1, 1));

        try
        {
            // Acquire per-partition gate FIRST to ensure within-partition ordering.
            // Only one batch per partition can be in-flight at a time to avoid OutOfOrderSequenceNumber.
            // IMPORTANT: This must be acquired BEFORE the global semaphore. If the global semaphore
            // were acquired first, batches waiting on their partition gate would consume global slots,
            // starving other partitions and collapsing throughput (head-of-line blocking).
            await partitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            partitionGateAcquired = true;

            // Then acquire global concurrency slot - limits total concurrent network sends
            // to MaxInFlightRequestsPerConnection. Only counted when partition gate is held,
            // so only actual sends consume slots (not batches waiting for their partition).
            await _sendConcurrencySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            sendSemaphoreAcquired = true;

            await SendBatchAsync(batch, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Failed to send batch to {Topic}-{Partition}",
                batch.TopicPartition.Topic, batch.TopicPartition.Partition);

            // Track batch failure (if not already tracked in SendBatchAsync)
            _statisticsCollector.RecordBatchFailed(
                batch.TopicPartition.Topic,
                batch.TopicPartition.Partition,
                batch.CompletionSourcesCount);

            // CRITICAL: Protect against exceptions from batch.Fail() to prevent unobserved task exceptions
            try
            {
                batch.Fail(ex);
            }
            catch (Exception failEx)
            {
                _logger?.LogError(failEx, "batch.Fail() threw unexpectedly for {Topic}-{Partition}",
                    batch.TopicPartition.Topic, batch.TopicPartition.Partition);
            }

            // Invoke OnAcknowledgement interceptors for failed batch
            InvokeOnAcknowledgementForBatch(batch.TopicPartition, -1, DateTimeOffset.UtcNow, batch.CompletionSourcesCount, ex);
        }
        finally
        {
            // Release only the semaphores that were actually acquired
            if (sendSemaphoreAcquired)
            {
                _sendConcurrencySemaphore.Release();
            }

            if (partitionGateAcquired)
            {
                partitionGate.Release();
            }

            // Return the ReadyBatch to the pool for reuse
            _accumulator.ReturnReadyBatch(batch);

            // Decrement accumulator in-flight counter to unblock FlushAsync
            // CRITICAL: Must be in finally (not SenderLoopAsync) so FlushAsync waits
            // for actual send completion, not just sender loop pickup. Otherwise
            // FlushAsync returns early and callers can overwhelm the buffer with new
            // messages before previous sends have released their memory.
            _accumulator.OnBatchExitsPipeline();

            // Decrement in-flight count and signal if all sends complete
            DecrementInFlightSendCount();

            // Release pipeline depth slot last - allows SenderLoopAsync to dispatch another batch
            _senderPipelineSemaphore.Release();
        }
    }

    /// <summary>
    /// Waits for all in-flight batch sends to complete.
    /// Used during disposal to ensure graceful shutdown.
    /// </summary>
    /// <summary>
    /// Increments the in-flight send count and resets the completion signal if this is the first in-flight send.
    /// Lock ensures atomicity of (increment + Reset) to prevent race with (decrement + Set).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IncrementInFlightSendCount()
    {
        lock (_sendCompletionLock)
        {
            if (Interlocked.Increment(ref _inFlightSendCount) == 1)
            {
                _allSendsCompleted.Reset();
            }
        }
    }

    /// <summary>
    /// Decrements the in-flight send count and signals completion if all sends are done.
    /// Lock ensures atomicity of (decrement + Set) to prevent race with (increment + Reset).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecrementInFlightSendCount()
    {
        lock (_sendCompletionLock)
        {
            if (Interlocked.Decrement(ref _inFlightSendCount) == 0)
            {
                _allSendsCompleted.Set();
            }
        }
    }

    private ValueTask WaitForInFlightSendsAsync(CancellationToken cancellationToken)
    {
        // Fast path: no sends in flight
        if (Volatile.Read(ref _inFlightSendCount) == 0)
        {
            return ValueTask.CompletedTask;
        }

        return WaitForInFlightSendsSlowAsync(cancellationToken);
    }

    private async ValueTask WaitForInFlightSendsSlowAsync(CancellationToken cancellationToken)
    {
        // Poll with short interval - ManualResetEventSlim doesn't have native async wait
        while (Volatile.Read(ref _inFlightSendCount) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check if signaled (no sends in flight)
            if (_allSendsCompleted.IsSet)
            {
                return;
            }

            // Short poll interval for responsive shutdown
            await Task.Delay(5, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Groups drained batches by broker and dispatches them.
    /// For each batch: synchronously looks up the partition leader, tries to non-blocking acquire
    /// the partition gate, and groups by broker ID. Batches that can't be grouped (no leader cached,
    /// or partition gate is busy) fall back to the existing SendBatchWithCleanupAsync path.
    /// </summary>
    private void DispatchCoalescedBatches(
        ReadyBatch[] drainedBatches,
        int drainCount,
        CancellationToken cancellationToken)
    {
        // broker ID → (batches, partition gates)
        Dictionary<int, (List<ReadyBatch> Batches, List<SemaphoreSlim> Gates)>? brokerGroups = null;

        for (var i = 0; i < drainCount; i++)
        {
            var batch = drainedBatches[i];

            // Look up leader synchronously from cached metadata
            var leader = _metadataManager.Metadata.GetPartitionLeader(
                batch.TopicPartition.Topic,
                batch.TopicPartition.Partition);

            if (leader is null)
            {
                // No leader cached — fall back to existing path (it does async metadata refresh + retry)
                _ = SendBatchWithCleanupAsync(batch, cancellationToken);
                continue;
            }

            // Try non-blocking acquire of partition gate
            var partitionGate = _partitionSendGates.GetOrAdd(batch.TopicPartition, _ => new SemaphoreSlim(1, 1));
            if (!partitionGate.Wait(0, CancellationToken.None))
            {
                // Gate is busy (another batch for this partition is in-flight) — fall back
                _ = SendBatchWithCleanupAsync(batch, cancellationToken);
                continue;
            }

            // Gate acquired — add to broker group
            brokerGroups ??= new Dictionary<int, (List<ReadyBatch>, List<SemaphoreSlim>)>();
            if (!brokerGroups.TryGetValue(leader.NodeId, out var group))
            {
                group = ([], []);
                brokerGroups[leader.NodeId] = group;
            }
            group.Batches.Add(batch);
            group.Gates.Add(partitionGate);
        }

        if (brokerGroups is null)
            return;

        // Dispatch each broker group
        foreach (var (brokerId, (batches, gates)) in brokerGroups)
        {
            if (batches.Count == 1)
            {
                // Single batch for this broker — use optimized single-batch path
                _ = SendBatchWithGateAlreadyAcquiredAsync(batches[0], gates[0], cancellationToken);
            }
            else
            {
                // Multiple batches for this broker — coalesce into one request
                _ = SendCoalescedBatchesAsync(batches, brokerId, gates, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Sends a batch where the partition gate has already been acquired by the caller.
    /// Used by the coalesced dispatch path when a broker group has exactly 1 batch.
    /// The caller is responsible for having acquired both the pipeline semaphore slot
    /// and the partition gate; this method releases them in finally.
    /// </summary>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask SendBatchWithGateAlreadyAcquiredAsync(
        ReadyBatch batch,
        SemaphoreSlim partitionGate,
        CancellationToken cancellationToken)
    {
        var sendSemaphoreAcquired = false;

        try
        {
            // Partition gate already held — just acquire global concurrency slot
            await _sendConcurrencySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            sendSemaphoreAcquired = true;

            await SendBatchAsync(batch, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Failed to send batch to {Topic}-{Partition}",
                batch.TopicPartition.Topic, batch.TopicPartition.Partition);

            _statisticsCollector.RecordBatchFailed(
                batch.TopicPartition.Topic,
                batch.TopicPartition.Partition,
                batch.CompletionSourcesCount);

            try
            {
                batch.Fail(ex);
            }
            catch (Exception failEx)
            {
                _logger?.LogError(failEx, "batch.Fail() threw unexpectedly for {Topic}-{Partition}",
                    batch.TopicPartition.Topic, batch.TopicPartition.Partition);
            }

            InvokeOnAcknowledgementForBatch(batch.TopicPartition, -1, DateTimeOffset.UtcNow, batch.CompletionSourcesCount, ex);
        }
        finally
        {
            if (sendSemaphoreAcquired)
            {
                _sendConcurrencySemaphore.Release();
            }

            // Release the pre-acquired partition gate
            partitionGate.Release();

            _accumulator.ReturnReadyBatch(batch);
            _accumulator.OnBatchExitsPipeline();
            DecrementInFlightSendCount();
            _senderPipelineSemaphore.Release();
        }
    }

    /// <summary>
    /// Lifecycle manager for a coalesced send of multiple batches to the same broker.
    /// Acquires one _sendConcurrencySemaphore slot for the group, sends all batches in a single
    /// ProduceRequest, then handles per-partition failures by falling back to individual retry paths.
    /// </summary>
    /// <param name="batches">Batches in this group (all destined for the same broker). Ownership is transferred.</param>
    /// <param name="brokerId">Target broker ID.</param>
    /// <param name="partitionGates">Pre-acquired partition gates, one per batch (same indices as batches).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask SendCoalescedBatchesAsync(
        List<ReadyBatch> batches,
        int brokerId,
        List<SemaphoreSlim> partitionGates,
        CancellationToken cancellationToken)
    {
        // Track which batches have been handed off to retry (they handle their own cleanup)
        HashSet<ReadyBatch>? retriedBatches = null;

        try
        {
            // Acquire ONE global concurrency slot for the entire group
            await _sendConcurrencySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            List<(ReadyBatch Batch, ErrorCode Error)>? failures;
            try
            {
                failures = await TrySendCoalescedCoreAsync(batches, brokerId, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                _sendConcurrencySemaphore.Release();
            }

            // Handle per-partition failures
            if (failures is not null)
            {
                foreach (var (batch, errorCode) in failures)
                {
                    if (errorCode.IsRetriable())
                    {
                        // Hand off to existing single-batch retry path.
                        // That path acquires its own concurrency slot, partition gate, and handles cleanup.
                        // We must release THIS batch's partition gate before handing off, since
                        // SendBatchWithCleanupAsync acquires it again.
                        retriedBatches ??= [];
                        retriedBatches.Add(batch);

                        // Release this batch's partition gate now — the retry path will re-acquire it
                        var gateIndex = batches.IndexOf(batch);
                        partitionGates[gateIndex].Release();

                        // Fire-and-forget into the existing retry path (it handles all cleanup)
                        _ = SendBatchWithCleanupAsync(batch, cancellationToken);
                    }
                    else
                    {
                        // Non-retriable error — fail the batch
                        _statisticsCollector.RecordBatchFailed(
                            batch.TopicPartition.Topic,
                            batch.TopicPartition.Partition,
                            batch.CompletionSourcesCount);

                        try
                        {
                            batch.Fail(new KafkaException(errorCode, $"Produce failed: {errorCode}"));
                        }
                        catch (Exception failEx)
                        {
                            _logger?.LogError(failEx, "batch.Fail() threw unexpectedly for {Topic}-{Partition}",
                                batch.TopicPartition.Topic, batch.TopicPartition.Partition);
                        }

                        InvokeOnAcknowledgementForBatch(batch.TopicPartition, -1, DateTimeOffset.UtcNow, batch.CompletionSourcesCount,
                            new KafkaException(errorCode, $"Produce failed: {errorCode}"));
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Entire coalesced send failed — fail all batches that weren't already retried
            _logger?.LogError(ex, "Coalesced send to broker {BrokerId} failed", brokerId);

            foreach (var batch in batches)
            {
                if (retriedBatches?.Contains(batch) == true)
                    continue;

                _statisticsCollector.RecordBatchFailed(
                    batch.TopicPartition.Topic,
                    batch.TopicPartition.Partition,
                    batch.CompletionSourcesCount);

                try
                {
                    batch.Fail(ex);
                }
                catch (Exception failEx)
                {
                    _logger?.LogError(failEx, "batch.Fail() threw unexpectedly for {Topic}-{Partition}",
                        batch.TopicPartition.Topic, batch.TopicPartition.Partition);
                }

                InvokeOnAcknowledgementForBatch(batch.TopicPartition, -1, DateTimeOffset.UtcNow, batch.CompletionSourcesCount, ex);
            }
        }
        finally
        {
            // Clean up all batches NOT handed off to retry
            for (var i = 0; i < batches.Count; i++)
            {
                var batch = batches[i];
                if (retriedBatches?.Contains(batch) == true)
                    continue;

                // Release partition gate (if not already released for retry)
                partitionGates[i].Release();

                _accumulator.ReturnReadyBatch(batch);
                _accumulator.OnBatchExitsPipeline();
                DecrementInFlightSendCount();
                _senderPipelineSemaphore.Release();
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

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask SendBatchAsync(ReadyBatch batch, CancellationToken cancellationToken)
    {
        // DIAGNOSTIC: Track batch entry
        _logger?.LogDebug("[SendBatch] START {Topic}-{Partition} with {Count} messages",
            batch.TopicPartition.Topic, batch.TopicPartition.Partition, batch.CompletionSourcesCount);

        // CRITICAL: Use try-finally to ensure BufferMemory is ALWAYS released
        // This prevents memory leaks when exceptions occur during batch sending
        try
        {
            // Retry loop for transient errors (leader changes, network issues, etc.)
            // Uses Stopwatch ticks for allocation-free timing
            var deliveryDeadlineTicks = Stopwatch.GetTimestamp() +
                (long)(_options.DeliveryTimeoutMs * (Stopwatch.Frequency / 1000.0));
            var backoffMs = _options.RetryBackoffMs;
            ErrorCode lastErrorCode = ErrorCode.None;

            while (true)
            {
                // Check cancellation at top of loop to avoid unnecessary work
                cancellationToken.ThrowIfCancellationRequested();

                // Check delivery timeout before attempting send
                if (Stopwatch.GetTimestamp() >= deliveryDeadlineTicks)
                {
                    throw new TimeoutException(
                        $"Delivery timeout exceeded for {batch.TopicPartition.Topic}-{batch.TopicPartition.Partition}" +
                        (lastErrorCode != ErrorCode.None ? $" (last error: {lastErrorCode})" : ""));
                }

                // Try to send the batch - returns error code on retriable error, None on success
                var errorCode = await TrySendBatchCoreAsync(batch, cancellationToken).ConfigureAwait(false);

                if (errorCode == ErrorCode.None)
                {
                    // Success - batch completed in TrySendBatchCoreAsync
                    return;
                }

                // Check if error is retriable
                if (!errorCode.IsRetriable())
                {
                    // Non-retriable error - fail immediately
                    _statisticsCollector.RecordBatchFailed(
                        batch.TopicPartition.Topic,
                        batch.TopicPartition.Partition,
                        batch.CompletionSourcesCount);
                    throw new KafkaException(errorCode, $"Produce failed: {errorCode}");
                }

                // Retriable error - refresh metadata and retry
                lastErrorCode = errorCode;
                _statisticsCollector.RecordRetry();

                _logger?.LogDebug(
                    "[SendBatch] Retriable error {ErrorCode} for {Topic}-{Partition}, refreshing metadata and retrying after {BackoffMs}ms",
                    errorCode, batch.TopicPartition.Topic, batch.TopicPartition.Partition, backoffMs);

                // Refresh metadata to get new leader (fire-and-forget, don't block retry loop)
                // With exponential backoff (100ms -> 200ms -> 400ms...), later retries give
                // sufficient time for metadata refresh to complete before the next send attempt
                _ = _metadataManager.RefreshMetadataAsync([batch.TopicPartition.Topic], cancellationToken);

                // Backoff before retry (respects cancellation)
                await Task.Delay(backoffMs, cancellationToken).ConfigureAwait(false);

                // Exponential backoff with cap
                backoffMs = Math.Min(backoffMs * 2, _options.RetryBackoffMaxMs);
            }
        }
        catch (Exception ex)
        {
            // CRITICAL: Fail the batch to complete its DeliveryTask
            // Otherwise FlushAsync will hang waiting for delivery completion
            _logger?.LogError(ex, "[SendBatch] FAIL {Topic}-{Partition} - calling batch.Fail()",
                batch.TopicPartition.Topic, batch.TopicPartition.Partition);
            batch.Fail(ex);
            throw;
        }
        finally
        {
            // NOTE: BufferMemory is released early in SenderLoopAsync when the batch is dequeued,
            // not here. This prevents the buffer from staying "full" while batches wait on partition
            // gates and the global send semaphore. The pipeline depth limiter bounds how many batches
            // can have their memory freed early, so backpressure is still maintained.
        }
    }

    /// <summary>
    /// Core send logic - attempts to send batch once and returns error code.
    /// Returns ErrorCode.None on success, or the error code on failure.
    /// This separation allows the retry loop to avoid exception allocation overhead
    /// for retriable errors (exceptions are only thrown for non-retriable failures).
    /// </summary>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<ErrorCode> TrySendBatchCoreAsync(ReadyBatch batch, CancellationToken cancellationToken)
    {
        var leader = await _metadataManager.GetPartitionLeaderAsync(
            batch.TopicPartition.Topic,
            batch.TopicPartition.Partition,
            cancellationToken).ConfigureAwait(false);

        if (leader is null)
        {
            // No leader found - this is retriable (leader election in progress)
            return ErrorCode.LeaderNotAvailable;
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

        // For transactional producers, ensure partition is registered before sending
        if (_accumulator.IsTransactional)
        {
            bool needsRegistration;
            lock (_partitionsInTransaction)
            {
                needsRegistration = _partitionsInTransaction.Add(batch.TopicPartition);
            }

            if (needsRegistration)
            {
                await AddPartitionsToTransactionAsync([batch.TopicPartition], cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        // Reuse thread-local single-element arrays to avoid per-batch array allocations
        var recordBatchArray = t_recordBatchArray ??= new RecordBatch[1];
        recordBatchArray[0] = batch.RecordBatch;

        var partitionDataArray = t_partitionDataArray ??= new ProduceRequestPartitionData[1];
        partitionDataArray[0] = new ProduceRequestPartitionData
        {
            Index = expectedPartition,
            Records = recordBatchArray,
            Compression = _options.CompressionType,
            CompressionCodecs = _compressionCodecs
        };

        var topicDataArray = t_topicDataArray ??= new ProduceRequestTopicData[1];
        topicDataArray[0] = new ProduceRequestTopicData
        {
            Name = expectedTopic,
            PartitionData = partitionDataArray
        };

        var request = new ProduceRequest
        {
            Acks = (short)_options.Acks,
            TimeoutMs = _options.RequestTimeoutMs,
            TransactionalId = _options.TransactionalId,
            TopicData = topicDataArray
        };

        // Sanity check: verify the request was built correctly
        System.Diagnostics.Debug.Assert(
            request.TopicData[0].Name == expectedTopic,
            $"Request topic mismatch: expected '{expectedTopic}', got '{request.TopicData[0].Name}'");

        var messageCount = batch.CompletionSourcesCount;
        var requestStartTime = Stopwatch.GetTimestamp();

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
            _logger?.LogDebug("[SendBatch] COMPLETE (fire-and-forget) {Topic}-{Partition}",
                batch.TopicPartition.Topic, batch.TopicPartition.Partition);
            var fireAndForgetTimestamp = DateTimeOffset.UtcNow;
            batch.CompleteSend(-1, fireAndForgetTimestamp);

            // Invoke OnAcknowledgement interceptors for each message in the batch
            InvokeOnAcknowledgementForBatch(batch.TopicPartition, -1, fireAndForgetTimestamp, messageCount, exception: null);

            return ErrorCode.None;
        }

        var response = await connection.SendAsync<ProduceRequest, ProduceResponse>(
            request,
            (short)apiVersion,
            cancellationToken).ConfigureAwait(false);

        // Track response received with latency (allocation-free timing)
        var elapsedTicks = Stopwatch.GetTimestamp() - requestStartTime;
        var latencyMs = (long)(elapsedTicks * 1000.0 / Stopwatch.Frequency);
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
            // No response for our partition - treat as retriable network error
            _logger?.LogWarning(
                "[SendBatch] No response for partition {Topic}-{Partition} from broker {Host}:{Port}",
                expectedTopic, expectedPartition, connection.Host, connection.Port);
            return ErrorCode.NetworkException;
        }

        if (partitionResponse.ErrorCode == ErrorCode.DuplicateSequenceNumber)
        {
            // DuplicateSequenceNumber means the batch was already written (a previous attempt
            // succeeded but its response was lost). Treat as success with the broker's offset.
            _logger?.LogDebug("[SendBatch] DuplicateSequenceNumber for {Topic}-{Partition} - batch already written at offset {Offset}",
                batch.TopicPartition.Topic, batch.TopicPartition.Partition, partitionResponse.BaseOffset);
        }
        else if (partitionResponse.ErrorCode != ErrorCode.None)
        {
            // Return error code - caller decides if retriable
            return partitionResponse.ErrorCode;
        }

        // Success - track and complete the batch
        _statisticsCollector.RecordBatchDelivered(
            batch.TopicPartition.Topic,
            batch.TopicPartition.Partition,
            messageCount);

        var timestamp = partitionResponse.LogAppendTimeMs > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(partitionResponse.LogAppendTimeMs)
            : DateTimeOffset.UtcNow;

        _logger?.LogDebug("[SendBatch] COMPLETE (normal) {Topic}-{Partition} at offset {Offset}",
            batch.TopicPartition.Topic, batch.TopicPartition.Partition, partitionResponse.BaseOffset);
        batch.CompleteSend(partitionResponse.BaseOffset, timestamp);

        // Invoke OnAcknowledgement interceptors for each message in the batch
        InvokeOnAcknowledgementForBatch(batch.TopicPartition, partitionResponse.BaseOffset, timestamp, messageCount, exception: null);

        return ErrorCode.None;
    }

    /// <summary>
    /// Core send logic for a coalesced group of batches destined for the same broker.
    /// Builds a multi-topic/partition ProduceRequest, sends it, and processes the response.
    /// Returns a list of (batch, errorCode) pairs for partitions that failed with retriable errors.
    /// Successfully sent batches are completed inline.
    /// </summary>
    private async ValueTask<List<(ReadyBatch Batch, ErrorCode Error)>?> TrySendCoalescedCoreAsync(
        List<ReadyBatch> batches,
        int brokerId,
        CancellationToken cancellationToken)
    {
        var connection = await _connectionPool.GetConnectionAsync(brokerId, cancellationToken)
            .ConfigureAwait(false);

        // Ensure API version is negotiated (thread-safe initialization)
        var apiVersion = _produceApiVersion;
        if (apiVersion < 0)
        {
            apiVersion = _metadataManager.GetNegotiatedApiVersion(
                ApiKey.Produce,
                ProduceRequest.LowestSupportedVersion,
                ProduceRequest.HighestSupportedVersion);
            Interlocked.CompareExchange(ref _produceApiVersion, apiVersion, -1);
            apiVersion = _produceApiVersion;
        }

        // For transactional producers, register all new partitions in one call
        if (_accumulator.IsTransactional)
        {
            List<TopicPartition>? newPartitions = null;
            lock (_partitionsInTransaction)
            {
                foreach (var batch in batches)
                {
                    if (_partitionsInTransaction.Add(batch.TopicPartition))
                    {
                        newPartitions ??= [];
                        newPartitions.Add(batch.TopicPartition);
                    }
                }
            }

            if (newPartitions is not null)
            {
                await AddPartitionsToTransactionAsync(newPartitions, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        // Group batches by topic to build the multi-partition request
        // Use a dictionary keyed by topic name → list of (batch, partitionData)
        var topicGroups = new Dictionary<string, List<(ReadyBatch Batch, int Partition)>>();
        foreach (var batch in batches)
        {
            var topic = batch.TopicPartition.Topic;
            if (!topicGroups.TryGetValue(topic, out var group))
            {
                group = [];
                topicGroups[topic] = group;
            }
            group.Add((batch, batch.TopicPartition.Partition));
        }

        // Build ProduceRequestTopicData[] / ProduceRequestPartitionData[]
        var topicDataArray = new ProduceRequestTopicData[topicGroups.Count];
        var topicIndex = 0;
        foreach (var (topicName, group) in topicGroups)
        {
            var partitionDataArray = new ProduceRequestPartitionData[group.Count];
            for (var i = 0; i < group.Count; i++)
            {
                partitionDataArray[i] = new ProduceRequestPartitionData
                {
                    Index = group[i].Partition,
                    Records = [group[i].Batch.RecordBatch],
                    Compression = _options.CompressionType,
                    CompressionCodecs = _compressionCodecs
                };
            }

            topicDataArray[topicIndex++] = new ProduceRequestTopicData
            {
                Name = topicName,
                PartitionData = partitionDataArray
            };
        }

        var request = new ProduceRequest
        {
            Acks = (short)_options.Acks,
            TimeoutMs = _options.RequestTimeoutMs,
            TransactionalId = _options.TransactionalId,
            TopicData = topicDataArray
        };

        var requestStartTime = Stopwatch.GetTimestamp();
        _statisticsCollector.RecordRequestSent();

        // Handle Acks.None (fire-and-forget) — complete all batches with offset -1
        if (_options.Acks == Acks.None)
        {
            await connection.SendFireAndForgetAsync<ProduceRequest, ProduceResponse>(
                request,
                (short)apiVersion,
                cancellationToken).ConfigureAwait(false);

            var fireAndForgetTimestamp = DateTimeOffset.UtcNow;
            foreach (var batch in batches)
            {
                _statisticsCollector.RecordBatchDelivered(
                    batch.TopicPartition.Topic,
                    batch.TopicPartition.Partition,
                    batch.CompletionSourcesCount);
                batch.CompleteSend(-1, fireAndForgetTimestamp);
                InvokeOnAcknowledgementForBatch(batch.TopicPartition, -1, fireAndForgetTimestamp, batch.CompletionSourcesCount, exception: null);
            }

            return null; // All succeeded
        }

        var response = await connection.SendAsync<ProduceRequest, ProduceResponse>(
            request,
            (short)apiVersion,
            cancellationToken).ConfigureAwait(false);

        var elapsedTicks = Stopwatch.GetTimestamp() - requestStartTime;
        var latencyMs = (long)(elapsedTicks * 1000.0 / Stopwatch.Frequency);
        _statisticsCollector.RecordResponseReceived(latencyMs);

        // Process response: match each batch to its partition response
        List<(ReadyBatch Batch, ErrorCode Error)>? failures = null;

        foreach (var batch in batches)
        {
            var expectedTopic = batch.TopicPartition.Topic;
            var expectedPartition = batch.TopicPartition.Partition;
            var messageCount = batch.CompletionSourcesCount;

            // Find topic response
            ProduceResponsePartitionData? partitionResponse = null;
            foreach (var topicResp in response.Responses)
            {
                if (topicResp.Name == expectedTopic)
                {
                    foreach (var partResp in topicResp.PartitionResponses)
                    {
                        if (partResp.Index == expectedPartition)
                        {
                            partitionResponse = partResp;
                            break;
                        }
                    }
                    break;
                }
            }

            if (partitionResponse is null)
            {
                // No response for this partition — treat as retriable
                _logger?.LogWarning(
                    "[CoalescedSend] No response for partition {Topic}-{Partition}",
                    expectedTopic, expectedPartition);
                failures ??= [];
                failures.Add((batch, ErrorCode.NetworkException));
                continue;
            }

            if (partitionResponse.ErrorCode == ErrorCode.DuplicateSequenceNumber)
            {
                // Already written — treat as success
                _logger?.LogDebug(
                    "[CoalescedSend] DuplicateSequenceNumber for {Topic}-{Partition} at offset {Offset}",
                    expectedTopic, expectedPartition, partitionResponse.BaseOffset);
            }
            else if (partitionResponse.ErrorCode != ErrorCode.None)
            {
                // Error for this partition
                failures ??= [];
                failures.Add((batch, partitionResponse.ErrorCode));
                continue;
            }

            // Success
            _statisticsCollector.RecordBatchDelivered(expectedTopic, expectedPartition, messageCount);

            var timestamp = partitionResponse.LogAppendTimeMs > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(partitionResponse.LogAppendTimeMs)
                : DateTimeOffset.UtcNow;

            batch.CompleteSend(partitionResponse.BaseOffset, timestamp);
            InvokeOnAcknowledgementForBatch(batch.TopicPartition, partitionResponse.BaseOffset, timestamp, messageCount, exception: null);
        }

        return failures;
    }

    /// <summary>
    /// Ensures metadata is initialized. Uses inline check to avoid async state machine
    /// overhead when metadata is already initialized (which is 99%+ of calls).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        // Fast path: metadata already initialized and idempotent producer ready (no allocation)
        if (_metadataManager.Metadata.LastRefreshed != default &&
            (!_options.EnableIdempotence || _options.TransactionalId is not null || _idempotentInitialized))
        {
            return ValueTask.CompletedTask;
        }

        // Slow path: need to initialize metadata and/or idempotent producer
        return EnsureInitializedWithTimeoutAsync(cancellationToken);
    }

    private async ValueTask EnsureInitializedWithTimeoutAsync(CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.MaxBlockMs);

        try
        {
            await _metadataManager.InitializeAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The timeout fired, not the caller's token - throw a descriptive TimeoutException
            throw new TimeoutException(
                $"Failed to fetch initial metadata within max.block.ms ({_options.MaxBlockMs}ms). " +
                $"Ensure the Kafka cluster is reachable and the bootstrap servers are correct.");
        }

        // Initialize idempotent producer after metadata is available (non-transactional only)
        if (_options.EnableIdempotence && _options.TransactionalId is null && !_idempotentInitialized)
        {
            await InitIdempotentProducerAsync(timeoutCts.Token).ConfigureAwait(false);
        }
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

            // 4. Wait for sender to drain remaining batches
            // CRITICAL: Don't cancel _senderCts yet - sender needs to process flushed batches
            // The sender will exit naturally when the ready batches channel completes (done in CloseAsync)
            if (hasTimeout)
                await _senderTask.WaitAsync(shutdownCts.Token).ConfigureAwait(false);
            else
                await _senderTask.ConfigureAwait(false);

            // 5. Now safe to cancel linger loop (it's already exited or will exit soon)
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
                // BUG FIX: Await BOTH workers and sender to ensure in-flight batches complete
                // This prevents DeliveryTasks from hanging if timeout occurred during FlushAsync
                await Task.WhenAll(_workerTasks.Append(_senderTask))
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

        _senderCts.Dispose();
        _lingerTimer.Dispose();
        _senderPipelineSemaphore.Dispose();
        _sendConcurrencySemaphore.Dispose();
        _allSendsCompleted.Dispose();
        _transactionLock.Dispose();

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

    /// <summary>
    /// Memory pre-reserved before queueing to the work channel.
    /// This provides backpressure for fire-and-forget Send() calls.
    /// Must be released after AppendAsync completes (which reserves its own memory based on actual size).
    /// </summary>
    public int PreReservedBytes { get; }

    public ProduceWorkItem(
        ProducerMessage<TKey, TValue> message,
        PooledValueTaskSource<RecordMetadata> completion,
        CancellationToken cancellationToken,
        int preReservedBytes = 0)
    {
        Message = message;
        Completion = completion;
        CancellationToken = cancellationToken;
        PreReservedBytes = preReservedBytes;
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
        // in TrySendBatchCoreAsync before the ProduceRequest is sent to the broker.
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
