using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Dekaf.Compression;
using Dekaf.Consumer;
using Dekaf.Diagnostics;
using Dekaf.Errors;
using Dekaf.Internal;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Retry;
using Dekaf.Serialization;
using Dekaf.Telemetry;
using Microsoft.Extensions.Logging;

namespace Dekaf.Producer;

/// <summary>
/// Kafka producer implementation.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public sealed partial class KafkaProducer<TKey, TValue> : IKafkaProducer<TKey, TValue>, IProducerDiagnostics, IProducerFastPath<TKey, TValue>, IBudgetedInstance
{
    internal ValueTask CloseConnectionsForTestingAsync() => _connectionPool.CloseAllAsync();

    /// <summary>
    /// Sentinel return value from <see cref="TryProduceSyncCore"/> indicating that the
    /// buffer is full and the caller should fall back to the async path (ReserveMemoryAsync).
    /// Replaces the previous exception-based control flow to avoid ~5-10μs throw/catch
    /// overhead per message under sustained backpressure.
    /// </summary>
    private enum SyncProduceResult : byte
    {
        Success,
        BufferFull,
    }

    private readonly ProducerOptions _options;
    private readonly ISerializer<TKey> _keySerializer;
    private readonly ISerializer<TValue> _valueSerializer;
    // Non-null when a serializer needs a one-time async setup (e.g. a Schema Registry fetch) before the
    // synchronous Serialize can run without blocking. Null for the common case (built-in serializers).
    private readonly IAsyncSerializerPreparer<TKey>? _keyPreparer;
    private readonly IAsyncSerializerPreparer<TValue>? _valuePreparer;
    // Non-null when the user configured an IAsyncSerializer for that component (issue #2309:
    // serializers that perform per-message I/O, e.g. envelope encryption with short-lived keys).
    // When either is set, every produce entry point routes to the asynchronous serialization path
    // before the synchronous fast path is reached; the corresponding sync slot holds a throwing
    // AsyncOnlySerializerPlaceholder. Null for the common case, costing the fast path one
    // readonly-bool branch (same shape as the _keyPreparer check above).
    private readonly IAsyncSerializer<TKey>? _asyncKeySerializer;
    private readonly IAsyncSerializer<TValue>? _asyncValueSerializer;
    private readonly bool _hasAsyncSerializers;
    private readonly IPartitioner _partitioner;
    private readonly bool _usesCustomPartitioner;
    private readonly IUniformStickyPartitioner? _uniformStickyPartitioner;
    private readonly ConnectionPool _connectionPool;
    private readonly MetadataManager _metadataManager;
    private readonly IDisposable _brokerCountDiscoveredRegistration;
    private readonly ClientTelemetryMetricCollector _telemetryMetricCollector;
    private readonly ClientTelemetryManager _telemetryManager;
    private readonly RecordAccumulator _accumulator;
    internal RecordAccumulator RecordAccumulator => _accumulator;
    private readonly CompressionCodecRegistry _compressionCodecs;
    private readonly ILogger _logger;
    private readonly IDekafMemoryBudget _memoryBudget;
    private readonly bool _ownsInfrastructure;

    private readonly CancellationTokenSource _senderCts;
    private readonly Task _senderTask;
    private readonly Task _lingerTask;
    private readonly ConcurrentDictionary<Task, byte> _partitionEnrollmentTasks = new();

    // Per-broker sender threads: each broker gets a dedicated BrokerSender with its own
    // channel and send loop. The per-broker in-flight semaphore enables pipelining across
    // different partitions. Ordering relies on broker idempotent producer support.
    private readonly ConcurrentDictionary<int, BrokerSender> _brokerSenders = new();

    // Bridges internal controller state (buffer memory, broker budgets, connection
    // scaling) to the "Dekaf" meter's observable gauges. Pull-based: costs nothing
    // unless a metrics listener polls.
    private readonly ProducerStateSource _stateSource;


    // Explicit initialization: users must call InitializeAsync() or use BuildAsync() before producing.
    private volatile bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private volatile int _produceApiVersion = -1;
    private int _disposed;

    internal bool IsDisposed => Volatile.Read(ref _disposed) != 0;

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
    internal PreparedTransactionState _preparedTransactionState = PreparedTransactionState.Empty;
    // The error code that drove the transaction into AbortableError/FatalError, surfaced by
    // the fail-fast produce guard for context. ErrorCode is short-backed, so volatile is valid.
    internal volatile ErrorCode _lastTransactionError = ErrorCode.None;
    private readonly SemaphoreSlim _transactionLock = new(1, 1);
    private readonly System.Threading.Lock _epochBumpLock = new();
    internal readonly System.Threading.Lock _partitionsInTransactionLock = new();
    internal readonly HashSet<TopicPartition> _partitionsInTransaction = [];
    private readonly HashSet<TopicPartition> _pendingTransactionPartitions = [];
    private readonly HashSet<TopicPartition> _transactionPartitionsBeingEnrolled = [];
    private readonly HashSet<Action<Exception?>> _partitionEnrollmentWaiters = [];
    private readonly Dictionary<TopicPartition, Exception> _partitionEnrollmentErrors = [];
    private bool _partitionEnrollmentActive;
    private long _partitionEnrollmentGeneration;
    private readonly Func<IReadOnlyList<TopicPartition>, CancellationToken, ValueTask> _addPartitionsToTransaction;
    internal volatile bool _currentTransactionUsesTV2;
    private short _currentTransactionFeatureVersion;

    // In-flight batch tracker for coordinated retry with multiple in-flight batches per partition.
    // Always initialized but only actively used for idempotent producers. Non-idempotent producers
    // skip sequence assignment and inflight tracking in BrokerSender.SendCoalescedAsync, so the
    // tracker exists but Register/Complete are never called.
    private readonly PartitionInflightTracker _inflightTracker;

    // Pool for PooledValueTaskSource to avoid per-message allocations
    // Unlike TaskCompletionSource, these can be reset and reused
    private readonly ValueTaskSourcePool<RecordMetadata> _valueTaskSourcePool;

    // Interceptors - stored as typed array for zero-allocation iteration
    private readonly IProducerInterceptor<TKey, TValue>[]? _interceptors;

    // Application-level retry policy (null = no retries, zero overhead on fast path)
    private readonly IRetryPolicy? _retryPolicy;

    private readonly ConcurrentDictionary<string, TagList> _metricTagsCache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _activityNameCache = new(StringComparer.Ordinal);

    // Single thread-local cache consolidating all per-thread producer state.
    // Reduces 9 separate [ThreadStatic] lookups (each hitting GetGCThreadStaticsByIndexSlow)
    // to a single lookup per method entry point.
    [ThreadStatic]
    private static ProducerThreadCache? t_cache;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ProducerThreadCache GetOrCreateCache() => t_cache ??= new ProducerThreadCache();

    /// <summary>
    /// Holds all per-thread cached state for the producer hot path.
    /// Consolidating into a single class reduces thread-static lookup overhead from 9 to 1.
    /// </summary>
    private sealed class ProducerThreadCache
    {
        // Reusable SerializationContext to avoid per-message allocations.
        // Since SerializationContext contains reference types (Topic, Headers), copying it
        // involves copying those references. Using ThreadStatic avoids repeated struct creation.
        // Default struct initialization (all fields = null/default) is safe.
        // The struct is updated via property setters before each use.
        public SerializationContext SerializationContext;

        // Cached timestamp for fire-and-forget produces.
        // DateTimeOffset.UtcNow is expensive (~15-30ns). By caching the timestamp and refreshing
        // it approximately every millisecond, we can reduce overhead in high-throughput scenarios.
        // For fire-and-forget, precise per-message timestamps aren't critical since:
        // 1. Records store timestamp deltas from batch base timestamp
        // 2. Batches are sent within LingerMs (typically 0-5ms)
        public long CachedTimestampMs;
        public long CachedTimestampTicks;

        // Cached topic metadata for fire-and-forget produces.
        // Avoids MetadataManager dictionary lookups for consecutive messages to the same topic.
        // Cache validity is time-bounded (~1 second) to pick up background metadata refreshes.
        public string? CachedTopicName;
        public TopicInfo? CachedTopicInfo;
        public long CachedTopicValidUntilTicks;
        public MetadataManager? CachedMetadataManager;

        // Reusable serialization buffers to avoid PooledBufferWriter creation per message.
        // These buffers grow as needed and are reused across messages on the same thread.
        // After serialization, data is copied to a right-sized pooled buffer for the batch.
        // This trades a small copy for eliminating buffer allocation overhead.
        public byte[]? KeySerializationBuffer;
        public byte[]? ValueSerializationBuffer;
    }

    private const string TransactionVersionFeature = "transaction.version";

    // Default sizes match typical key/value sizes to avoid growth in common cases
    private const int DefaultKeyBufferSize = 512;
    private const int DefaultValueBufferSize = 2048;

    public KafkaProducer(
        ProducerOptions options,
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerializer,
        ILoggerFactory? loggerFactory = null,
        MetadataOptions? metadataOptions = null,
        IAsyncSerializer<TKey>? asyncKeySerializer = null,
        IAsyncSerializer<TValue>? asyncValueSerializer = null)
        : this(
            options,
            keySerializer,
            valueSerializer,
            CreateInfrastructure(options, loggerFactory, metadataOptions),
            loggerFactory,
            ownsInfrastructure: true,
            DekafMemoryBudget.Global,
            asyncKeySerializer: asyncKeySerializer,
            asyncValueSerializer: asyncValueSerializer)
    {
    }

    internal KafkaProducer(
        ProducerOptions options,
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerializer,
        ConnectionPool connectionPool,
        MetadataManager metadataManager,
        IDekafMemoryBudget memoryBudget,
        ILoggerFactory? loggerFactory = null,
        Func<IReadOnlyList<TopicPartition>, CancellationToken, ValueTask>? addPartitionsToTransaction = null,
        IAsyncSerializer<TKey>? asyncKeySerializer = null,
        IAsyncSerializer<TValue>? asyncValueSerializer = null)
        : this(
            options,
            keySerializer,
            valueSerializer,
            (connectionPool, metadataManager, new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Producer)),
            loggerFactory,
            ownsInfrastructure: false,
            memoryBudget,
            addPartitionsToTransaction,
            asyncKeySerializer,
            asyncValueSerializer)
    {
    }

    private static (ConnectionPool Pool, MetadataManager Metadata, ClientTelemetryMetricCollector TelemetryMetricCollector) CreateInfrastructure(
        ProducerOptions options,
        ILoggerFactory? loggerFactory,
        MetadataOptions? metadataOptions)
    {
        var reconnectBackoffMaxMs = ReconnectBackoffValidation.ResolveMaximumMilliseconds(
            options.ReconnectBackoffMs,
            options.ReconnectBackoffMaxMs,
            options.IsReconnectBackoffMsConfigured,
            options.IsReconnectBackoffMaxMsConfigured);
        var sharedPoolSizes = PoolSizing.ForSharedPools(
            brokerCount: options.BootstrapServers.Count,
            connectionsPerBroker: options.ConnectionsPerBroker,
            maxInFlightRequestsPerConnection: options.MaxInFlightRequestsPerConnection,
            batchSize: options.BatchSize,
            maxConnectionsPerBroker: options.MaxConnectionsPerBroker);
        var telemetryMetricCollector = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Producer);
        var connectionPool = new ConnectionPool(
            options.ClientId,
            new ConnectionOptions
            {
                UseTls = options.UseTls,
                TlsConfig = options.TlsConfig,
                RemoteCertificateValidationCallback = options.RemoteCertificateValidationCallback,
                ConnectionTimeout = options.ConnectionTimeout,
                EnableTcpKeepAlive = options.EnableTcpKeepAlive,
                TcpKeepAliveTime = options.TcpKeepAliveTime,
                TcpKeepAliveInterval = options.TcpKeepAliveInterval,
                TcpKeepAliveRetryCount = options.TcpKeepAliveRetryCount,
                RequestTimeout = TimeSpan.FromMilliseconds(options.RequestTimeoutMs),
                ReconnectBackoff = TimeSpan.FromMilliseconds(options.ReconnectBackoffMs),
                ReconnectBackoffMax = TimeSpan.FromMilliseconds(reconnectBackoffMaxMs),
                ConnectionsMaxIdleMs = options.ConnectionsMaxIdleMs,
                SaslMechanism = options.SaslMechanism,
                SaslUsername = options.SaslUsername,
                SaslPassword = options.SaslPassword,
                SaslCredentialProvider = options.SaslCredentialProvider,
                SaslScramTokenAuth = options.SaslScramTokenAuth,
                GssapiConfig = options.GssapiConfig,
                OAuthBearerConfig = options.OAuthBearerConfig,
                OAuthBearerTokenProvider = options.OAuthBearerTokenProvider,
                AwsMskIamConfig = options.AwsMskIamConfig,
                SendBufferSize = options.SocketSendBufferBytes,
                ReceiveBufferSize = options.SocketReceiveBufferBytes,
                MaxInFlightRequestsPerConnection = options.MaxInFlightRequestsPerConnection,
                ClientDnsLookup = options.ClientDnsLookup,
                DnsResolver = options.DnsResolver
            },
            loggerFactory,
            options.ConnectionsPerBroker,
            ResponseBufferPool.Default,
            pipeMemoryBucketCapacity: sharedPoolSizes.PipeMemoryArraysPerBucket,
            telemetryMetricCollector: telemetryMetricCollector);

        metadataOptions ??= new MetadataOptions
        {
            MetadataRecoveryStrategy = options.MetadataRecoveryStrategy,
            MetadataClusterCheckEnabled = options.MetadataClusterCheckEnabled,
            RetryBackoffMs = options.RetryBackoffMs,
            RetryBackoffMaxMs = options.RetryBackoffMaxMs,
            InitTimeoutMs = options.MaxBlockMs
        };
        var metadataManager = new MetadataManager(
            connectionPool,
            options.BootstrapServers,
            options: metadataOptions,
            logger: loggerFactory?.CreateLogger<MetadataManager>());

        return (connectionPool, metadataManager, telemetryMetricCollector);
    }

    private KafkaProducer(
        ProducerOptions options,
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerializer,
        (ConnectionPool Pool, MetadataManager Metadata, ClientTelemetryMetricCollector TelemetryMetricCollector) infrastructure,
        ILoggerFactory? loggerFactory,
        bool ownsInfrastructure,
        IDekafMemoryBudget memoryBudget,
        Func<IReadOnlyList<TopicPartition>, CancellationToken, ValueTask>? addPartitionsToTransaction = null,
        IAsyncSerializer<TKey>? asyncKeySerializer = null,
        IAsyncSerializer<TValue>? asyncValueSerializer = null)
    {
        ExponentialRetryBackoff.Validate(options.RetryBackoffMs, options.RetryBackoffMaxMs);
        _options = options;
        _addPartitionsToTransaction = addPartitionsToTransaction ?? AddPartitionsToTransactionAsync;
        // When an async serializer is configured for a component, its sync slot is forced to the
        // throwing placeholder here — by construction, not by builder convention — so a direct
        // constructor caller can never pair an async serializer with a live sync one that would
        // silently serialize on the sync path.
        _keySerializer = asyncKeySerializer is null ? keySerializer : AsyncOnlySerializerPlaceholder<TKey>.Instance;
        _valueSerializer = asyncValueSerializer is null ? valueSerializer : AsyncOnlySerializerPlaceholder<TValue>.Instance;
        _keyPreparer = _keySerializer as IAsyncSerializerPreparer<TKey>;
        _valuePreparer = _valueSerializer as IAsyncSerializerPreparer<TValue>;
        _asyncKeySerializer = asyncKeySerializer;
        _asyncValueSerializer = asyncValueSerializer;
        _hasAsyncSerializers = asyncKeySerializer is not null || asyncValueSerializer is not null;
        _memoryBudget = memoryBudget;
        _ownsInfrastructure = ownsInfrastructure;
        _logger = loggerFactory?.CreateLogger<KafkaProducer<TKey, TValue>>() ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<KafkaProducer<TKey, TValue>>.Instance;

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

        _retryPolicy = options.RetryPolicy;

        var producerPoolSizes = PoolSizing.ForProducer(
            options.BufferMemory,
            options.BatchSize,
            options.MaxInFlightRequestsPerConnection,
            options.MaxConnectionsPerBroker);

        // Scale shared pools based on bootstrap server count (minimum broker estimate).
        // This prevents pool contention when multiple brokers concurrently rent/return arrays.
        // Uses MaxConnectionsPerBroker (not ConnectionsPerBroker) so pools are pre-sized for
        // adaptive scaling peak concurrency, avoiding pool misses when connections ramp up.
        var sharedPoolSizes = PoolSizing.ForSharedPools(
            brokerCount: options.BootstrapServers.Count,
            connectionsPerBroker: options.ConnectionsPerBroker,
            maxInFlightRequestsPerConnection: options.MaxInFlightRequestsPerConnection,
            batchSize: options.BatchSize,
            maxConnectionsPerBroker: options.MaxConnectionsPerBroker);
        ProducerDataPool.RatchetBucketCapacity(sharedPoolSizes.ProducerDataArraysPerBucket);
        ProducerContainerPools.RatchetHeaderBucketCapacity(sharedPoolSizes.HeaderArraysPerBucket);
        DekafPools.RatchetSerializationBucketCapacity(sharedPoolSizes.SerializationArraysPerBucket);
        ProduceResponse.RatchetPoolSize(sharedPoolSizes.ProduceResponsePoolSize);

        var poolSize = options.ValueTaskSourcePoolSize > 0
            ? options.ValueTaskSourcePoolSize
            : producerPoolSizes.ValueTaskSources;
        _valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>(poolSize);

        RecordBatch.RatchetMaxRetainedBufferSize(producerPoolSizes.MaxRetainedBufferSize);

        _usesCustomPartitioner = options.CustomPartitioner is not null;
        _partitioner = options.CustomPartitioner ?? options.Partitioner switch
        {
            PartitionerType.Sticky => new StickyPartitioner(
                options.BatchSize,
                options.EnableAdaptivePartitioning,
                options.PartitionerAvailabilityTimeoutMs,
                options.IgnorePartitionerKeys,
                options.EnableRackAwarePartitioning,
                options.ClientRack),
            PartitionerType.RoundRobin => new RoundRobinPartitioner(),
            PartitionerType.Random => new RandomPartitioner(),
            PartitionerType.Consistent => new ConsistentPartitioner(),
            PartitionerType.ConsistentRandom => new ConsistentRandomPartitioner(),
            PartitionerType.Murmur2 => new Murmur2Partitioner(),
            PartitionerType.Murmur2Random => new Murmur2RandomPartitioner(),
            PartitionerType.Fnv1A => new Fnv1APartitioner(),
            PartitionerType.Fnv1ARandom => new Fnv1ARandomPartitioner(),
            _ => new DefaultPartitioner(
                options.BatchSize,
                options.EnableAdaptivePartitioning,
                options.PartitionerAvailabilityTimeoutMs,
                options.IgnorePartitionerKeys,
                options.EnableRackAwarePartitioning,
                options.ClientRack)
        };
        _uniformStickyPartitioner = _usesCustomPartitioner ? null : _partitioner as IUniformStickyPartitioner;

        _connectionPool = infrastructure.Pool;
        _metadataManager = infrastructure.Metadata;
        _telemetryMetricCollector = infrastructure.TelemetryMetricCollector;
        _telemetryMetricCollector.RegisterMetricsForSubscription(options.ApplicationMetrics);

        _telemetryManager = new ClientTelemetryManager(
            _connectionPool,
            _metadataManager,
            loggerFactory?.CreateLogger<ClientTelemetryManager>(),
            _telemetryMetricCollector);

        // Re-ratchet shared pool sizes after metadata refresh discovers the real broker count.
        // Bootstrap servers are seed nodes — the actual cluster may have more brokers.
        // The ratchet pattern ensures pools only grow, never shrink.
        var connectionsPerBroker = options.ConnectionsPerBroker;
        var maxInFlight = options.MaxInFlightRequestsPerConnection;
        var batchSize = options.BatchSize;
        var maxConns = options.MaxConnectionsPerBroker;
        var connectionPool = _connectionPool;
        _brokerCountDiscoveredRegistration = _metadataManager.AddBrokerCountDiscoveredCallback(brokerCount =>
        {
            var sizes = PoolSizing.ForSharedPools(brokerCount, connectionsPerBroker, maxInFlight, batchSize, maxConns);
            ProducerDataPool.RatchetBucketCapacity(sizes.ProducerDataArraysPerBucket);
            ProducerContainerPools.RatchetHeaderBucketCapacity(sizes.HeaderArraysPerBucket);
            DekafPools.RatchetSerializationBucketCapacity(sizes.SerializationArraysPerBucket);
            ProduceResponse.RatchetPoolSize(sizes.ProduceResponsePoolSize);
            connectionPool.RatchetPipeMemoryBucketCapacity(sizes.PipeMemoryArraysPerBucket);
        });

        _compressionCodecs = CreateCompressionCodecRegistry(options);
        Action<string, int>? batchCompletionCallback = _uniformStickyPartitioner is null
            && _partitioner is IBatchCompletionAwarePartitioner batchCompletionAware
            ? batchCompletionAware.OnBatchComplete
            : null;
        Action<string, int, int, int>? recordAppendedCallback = _uniformStickyPartitioner is not null
            ? _uniformStickyPartitioner.OnRecordAppended
            : null;
        _accumulator = new RecordAccumulator(
            options,
            _compressionCodecs,
            loggerFactory?.CreateLogger<RecordAccumulator>(),
            batchCompletionCallback,
            recordAppendedCallback,
            ResolveLeaderIdForUnackedBudget);
        _uniformStickyPartitioner?.SetPartitionQueueByteProvider(_accumulator.GetPartitionQueueBytes);
        _uniformStickyPartitioner?.SetRackLocalPartitionsProvider(
            _metadataManager.Metadata.GetPartitionsForRack);

        // Inflight tracker enables coordinated retry with multiple in-flight batches per partition.
        // The broker uses sequence numbers to guarantee ordering, so multiple batches can be
        // in-flight simultaneously. The tracker enables coordinated retry on
        // OutOfOrderSequenceNumber instead of blind backoff.
        // Only pre-warm for idempotent producers — non-idempotent producers skip sequence
        // assignment and inflight tracking entirely (no producer ID or sequence numbers).
        var inflightPool = new InflightEntryPool(options.EnableIdempotence
            ? producerPoolSizes.InflightEntries
            : 1); // Minimum valid size; entries never rented for non-idempotent
        if (options.EnableIdempotence)
            inflightPool.PreWarm(Math.Min(options.MaxInFlightRequestsPerConnection * PoolSizing.InflightEntriesPerBatch, inflightPool.MaxPoolSize));
        _inflightTracker = new PartitionInflightTracker(inflightPool);

        GcConfigurationCheck.WarnIfWorkstationGc(_logger);

        _senderCts = new CancellationTokenSource();
        _senderTask = Task.Factory.StartNew(
            () => SenderLoopAsync(_senderCts.Token),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();
        _lingerTask = Task.Factory.StartNew(
            () => LingerLoopAsync(_senderCts.Token),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();
        _accumulator.StartAppendWorkers(_senderCts.Token);

        _stateSource = new ProducerStateSource(options.ClientId, _accumulator, _brokerSenders);
        DekafMetrics.RegisterProducerState(_stateSource);
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
                registry.Register(new GzipCompressionCodec(level));
            }
        }

        return registry;
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
        => ProduceAsync(message, ProduceContinuationMode.Async, cancellationToken);

    internal ValueTask<RecordMetadata> ProduceTransactionAsync(
        ProducerMessage<TKey, TValue> message,
        CancellationToken cancellationToken)
        => ProduceAsync(
            message,
            _options.InlineTransactionCompletions ? ProduceContinuationMode.Inline : ProduceContinuationMode.Async,
            cancellationToken);

    private ValueTask<RecordMetadata> ProduceAsync(
        ProducerMessage<TKey, TValue> message,
        ProduceContinuationMode continuationMode,
        CancellationToken cancellationToken)
    {
        // The retry wrapper's continuation runs retry-policy code (classification, delay
        // computation); ProduceAllAsync's bounded-harvest inline request must not run it on the
        // broker ack thread. The InlineTransactionCompletions opt-in keeps its documented
        // inline behavior.
        if (_retryPolicy is not null)
        {
            if (continuationMode == ProduceContinuationMode.InlineWhenDirect)
                continuationMode = ProduceContinuationMode.Async;
            return ProduceAsyncWithRetry(message, continuationMode, cancellationToken);
        }

        return ProduceAsyncCore(message, continuationMode, cancellationToken);
    }

    private ValueTask<RecordMetadata> ProduceAsyncCore(
        ProducerMessage<TKey, TValue> message,
        ProduceContinuationMode continuationMode,
        CancellationToken cancellationToken)
    {
        ThrowIfProduceCannotStart();

        // Check cancellation upfront before any work
        cancellationToken.ThrowIfCancellationRequested();

        // Apply OnSend interceptors before serialization
        message = ApplyOnSendInterceptors(message);

        var activity = StartPublishActivity(ref message);

        // Async serializers (per-message I/O, issue #2309) cannot run on the synchronous fast
        // path — divert to the dedicated async-serialization path before any sync serialize.
        if (_hasAsyncSerializers)
            return ProduceWithAsyncSerializationAsync(message, activity, continuationMode, cancellationToken);

        // If a serializer needs async setup (e.g. a one-time Schema Registry fetch), do it here on the
        // async path so the synchronous serialize never blocks on it. After the first message for a
        // subject this completes synchronously and falls straight through to the fast path below.
        if (_keyPreparer is not null || _valuePreparer is not null)
        {
            ValueTask prepare;
            try
            {
                prepare = PrepareSerializersAsync(message.Topic, message.Key, message.Value, message.Headers, cancellationToken);
            }
            catch (Exception ex)
            {
                // A preparer that throws synchronously must still stop and tag the started span,
                // matching the invariant AwaitWithActivity enforces on every other completion path.
                RecordActivityFault(activity, ex);
                throw;
            }

            if (!prepare.IsCompletedSuccessfully)
            {
                return AwaitPrepareThenProduce(
                    prepare,
                    message,
                    activity,
                    continuationMode,
                    cancellationToken);
            }
        }

        return ProduceAfterPrepare(message, activity, continuationMode, cancellationToken);
    }

    // Stops and error-tags a started span when async serializer preparation fails before the
    // message is appended. ProduceAfterPrepare owns the activity on all post-preparation paths,
    // so this only runs when preparation itself throws or faults. No-op when tracing is disabled.
    private static void RecordActivityFault(Activity? activity, Exception ex)
    {
        if (activity is null)
        {
            return;
        }

        Diagnostics.DekafDiagnostics.RecordException(activity, ex);
        activity.Dispose();
    }

    private ValueTask<RecordMetadata> ProduceAfterPrepare(
        ProducerMessage<TKey, TValue> message,
        Activity? activity,
        ProduceContinuationMode continuationMode,
        CancellationToken cancellationToken)
    {
        // AwaitWithActivity/AwaitWithMetrics interpose an async state machine whose continuation
        // (metric emission, activity export/dispose) runs on the completing thread; ProduceAllAsync's
        // inline request is only safe for the bare and cancellation awaits, whose continuations are
        // bounded. Hoisted so the mode and the wrapper choice below can never disagree if a metrics
        // listener starts mid-call.
        var metricsEnabled = ProducerMetricsEnabled();
        if (continuationMode == ProduceContinuationMode.InlineWhenDirect && (activity is not null || metricsEnabled))
            continuationMode = ProduceContinuationMode.Async;
        var runContinuationsAsynchronously = continuationMode == ProduceContinuationMode.Async;

        // Fast path: Try synchronous produce if metadata is initialized and cached.
        // This bypasses channel overhead for 99%+ of calls after warmup.
        if (TryProduceSyncForAsync(message, runContinuationsAsynchronously, out var completion))
        {
            // POST-QUEUE: Message appended to batch, committed to being sent
            // Message WILL be delivered, but caller can stop waiting via cancellation token.
            if (activity is not null)
            {
                return AwaitWithActivity(completion!, activity, message.Topic, cancellationToken);
            }
            if (metricsEnabled)
            {
                return AwaitWithMetrics(completion!, message.Topic, cancellationToken);
            }
            if (cancellationToken.CanBeCanceled)
            {
                return AwaitWithCancellation(completion!, cancellationToken);
            }
            return completion!.Task;
        }

        // Slow path: Fall back to channel-based async processing.
        // This handles first-time metadata initialization or cache misses.
        return ProduceAsyncSlow(message, activity, continuationMode, cancellationToken);
    }

    private async ValueTask<RecordMetadata> AwaitPrepareThenProduce(
        ValueTask prepare,
        ProducerMessage<TKey, TValue> message,
        Activity? activity,
        ProduceContinuationMode continuationMode,
        CancellationToken cancellationToken)
    {
        try
        {
            await prepare.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Preparation faulted (e.g. Schema Registry unreachable or cancelled mid-fetch) before the
            // message was appended. Stop and error-tag the span here; ProduceAfterPrepare never runs.
            RecordActivityFault(activity, ex);
            throw;
        }

        return await ProduceAfterPrepare(
            message,
            activity,
            continuationMode,
            cancellationToken).ConfigureAwait(false);
    }

    // Ensures any serializer that implements IAsyncSerializerPreparer<T> has fetched its schema (or
    // other async prerequisite) for this topic before the synchronous serialize runs. Returns a
    // synchronously-completed ValueTask in the steady state (already prepared), keeping the fast path sync.
    private ValueTask PrepareSerializersAsync(
        string topic,
        TKey? key,
        TValue? value,
        Headers? headers,
        CancellationToken cancellationToken)
    {
        var keyPrepare = default(ValueTask);
        if (_keyPreparer is not null && key is not null)
        {
            keyPrepare = _keyPreparer.PrepareAsync(
                key,
                new SerializationContext { Topic = topic, Component = SerializationComponent.Key, Headers = headers },
                cancellationToken);
        }

        var valuePrepare = default(ValueTask);
        if (_valuePreparer is not null && value is not null)
        {
            valuePrepare = _valuePreparer.PrepareAsync(
                value,
                new SerializationContext { Topic = topic, Component = SerializationComponent.Value, Headers = headers },
                cancellationToken);
        }

        if (keyPrepare.IsCompletedSuccessfully && valuePrepare.IsCompletedSuccessfully)
        {
            return default;
        }

        return AwaitBothAsync(keyPrepare, valuePrepare);

        static async ValueTask AwaitBothAsync(ValueTask keyPrepare, ValueTask valuePrepare)
        {
            // Await both even when one faults first, so a later fault on the other side is always
            // observed rather than left as an unobserved task exception. This is the cold path
            // (first message per subject); the steady state returns synchronously above.
            await Task.WhenAll(keyPrepare.AsTask(), valuePrepare.AsTask()).ConfigureAwait(false);
        }
    }

    private ValueTask<RecordMetadata> ProduceAsync(
        string topic,
        TKey? key,
        TValue value,
        Headers? headers,
        int? partition,
        DateTimeOffset? timestamp,
        ProduceContinuationMode continuationMode,
        CancellationToken cancellationToken)
    {
        if (_retryPolicy is not null || _interceptors is not null || Diagnostics.DekafDiagnostics.Source.HasListeners())
        {
            return ProduceAsync(new ProducerMessage<TKey, TValue>
            {
                Topic = topic,
                Key = key,
                Value = value,
                Headers = headers,
                Partition = partition,
                Timestamp = timestamp
            }, continuationMode, cancellationToken);
        }

        return ProduceAsyncCore(topic, key, value, headers, partition, timestamp, continuationMode, cancellationToken);
    }

    private ValueTask<RecordMetadata> ProduceAsyncCore(
        string topic,
        TKey? key,
        TValue value,
        Headers? headers,
        int? partition,
        DateTimeOffset? timestamp,
        ProduceContinuationMode continuationMode,
        CancellationToken cancellationToken)
    {
        ThrowIfProduceCannotStart();

        // Check cancellation upfront before any work
        cancellationToken.ThrowIfCancellationRequested();

        // See ProduceAsyncCore(ProducerMessage): async serializers divert before any sync serialize.
        // This overload is only reached without interceptors/retry/tracing, so no activity exists.
        if (_hasAsyncSerializers)
        {
            return ProduceWithAsyncSerializationAsync(
                new ProducerMessage<TKey, TValue>
                {
                    Topic = topic,
                    Key = key,
                    Value = value,
                    Headers = headers,
                    Partition = partition,
                    Timestamp = timestamp
                },
                activity: null,
                continuationMode,
                cancellationToken);
        }

        // See ProduceAsyncCore(ProducerMessage): resolve any async serializer prerequisite before the
        // synchronous serialize so it never blocks. Steady state completes synchronously and falls through.
        if (_keyPreparer is not null || _valuePreparer is not null)
        {
            var prepare = PrepareSerializersAsync(topic, key, value, headers, cancellationToken);
            if (!prepare.IsCompletedSuccessfully)
            {
                return AwaitPrepareThenProduce(prepare, topic, key, value, headers, partition, timestamp, continuationMode, cancellationToken);
            }
        }

        return ProduceAfterPrepare(topic, key, value, headers, partition, timestamp, continuationMode, cancellationToken);
    }

    private ValueTask<RecordMetadata> ProduceAfterPrepare(
        string topic,
        TKey? key,
        TValue value,
        Headers? headers,
        int? partition,
        DateTimeOffset? timestamp,
        ProduceContinuationMode continuationMode,
        CancellationToken cancellationToken)
    {
        // See the message-based ProduceAfterPrepare: the metrics wrapper's continuation must not
        // run inline on the broker ack thread, and the hoisted check keeps mode and wrapper in sync.
        var metricsEnabled = ProducerMetricsEnabled();
        if (continuationMode == ProduceContinuationMode.InlineWhenDirect && metricsEnabled)
            continuationMode = ProduceContinuationMode.Async;
        var runContinuationsAsynchronously = continuationMode == ProduceContinuationMode.Async;

        if (TryProduceSyncForAsync(
            topic,
            key,
            value,
            headers,
            partition,
            timestamp,
            runContinuationsAsynchronously,
            out var completion))
        {
            // POST-QUEUE: Message appended to batch, committed to being sent
            // Message WILL be delivered, but caller can stop waiting via cancellation token.
            if (metricsEnabled)
            {
                return AwaitWithMetrics(completion!, topic, cancellationToken);
            }
            if (cancellationToken.CanBeCanceled)
            {
                return AwaitWithCancellation(completion!, cancellationToken);
            }
            return completion!.Task;
        }

        // Cold path: allocate the full message only when async metadata/backpressure handling needs it.
        return ProduceAsyncSlow(new ProducerMessage<TKey, TValue>
        {
            Topic = topic,
            Key = key,
            Value = value,
            Headers = headers,
            Partition = partition,
            Timestamp = timestamp
        }, activity: null, continuationMode, cancellationToken);
    }

    private async ValueTask<RecordMetadata> AwaitPrepareThenProduce(
        ValueTask prepare,
        string topic,
        TKey? key,
        TValue value,
        Headers? headers,
        int? partition,
        DateTimeOffset? timestamp,
        ProduceContinuationMode continuationMode,
        CancellationToken cancellationToken)
    {
        await prepare.ConfigureAwait(false);
        return await ProduceAfterPrepare(topic, key, value, headers, partition, timestamp, continuationMode, cancellationToken).ConfigureAwait(false);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async ValueTask<RecordMetadata> ProduceAsyncWithRetry(
        ProducerMessage<TKey, TValue> message,
        ProduceContinuationMode continuationMode,
        CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (true)
        {
            try
            {
                return await ProduceAsyncCore(
                    message,
                    continuationMode,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (KafkaException ex) when (ex.IsRetriable)
            {
                attempt++;
                var delay = _retryPolicy!.GetNextDelay(attempt, ex);
                if (delay is null)
                    throw;

                await Task.Delay(delay.Value, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Awaits a completion source with cancellation support.
    /// If cancellation fires, sets the completion source to cancelled state so that
    /// GetResult() is called and the pool item is properly returned.
    /// The message delivery continues in background regardless.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ValueTask<RecordMetadata> AwaitWithCancellation(
        PooledValueTaskSource<RecordMetadata> completion,
        CancellationToken cancellationToken)
    {
        completion.RegisterCancellation(cancellationToken);
        return completion.Task;
    }

    /// <summary>
    /// Awaits a completion source with metrics and optional cancellation support.
    /// </summary>
    private async ValueTask<RecordMetadata> AwaitWithMetrics(
        PooledValueTaskSource<RecordMetadata> completion,
        string topic,
        CancellationToken cancellationToken)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        completion.RegisterCancellation(cancellationToken);

        try
        {
            var metadata = await completion.Task.ConfigureAwait(false);
            EmitProduceSuccessMetrics(topic, metadata, startTimestamp);
            return metadata;
        }
        catch
        {
            Diagnostics.DekafMetrics.ProduceErrors.Add(1, GetMetricTags(topic));
            throw;
        }
    }

    /// <summary>
    /// Awaits a completion source with Activity lifecycle and optional cancellation support.
    /// Records OTel metrics and sets span status/tags on completion.
    /// </summary>
    private async ValueTask<RecordMetadata> AwaitWithActivity(
        PooledValueTaskSource<RecordMetadata> completion,
        Activity activity,
        string topic,
        CancellationToken cancellationToken)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        completion.RegisterCancellation(cancellationToken);

        try
        {
            var metadata = await completion.Task.ConfigureAwait(false);

            // Success: record metrics and set activity tags
            activity.SetTag(Diagnostics.DekafDiagnostics.MessagingDestinationPartitionId, metadata.Partition);
            activity.SetTag(Diagnostics.DekafDiagnostics.MessagingMessageOffset, metadata.Offset);
            activity.SetTag(Diagnostics.DekafDiagnostics.MessagingMessageBodySize, metadata.KeySize + metadata.ValueSize);
            activity.SetStatus(ActivityStatusCode.Ok);

            EmitProduceSuccessMetrics(topic, metadata, startTimestamp);

            return metadata;
        }
        catch (Exception ex)
        {
            Diagnostics.DekafDiagnostics.RecordException(activity, ex);
            Diagnostics.DekafMetrics.ProduceErrors.Add(1, GetMetricTags(topic));

            throw;
        }
        finally
        {
            activity.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ProducerMetricsEnabled()
        => Diagnostics.DekafMetrics.MessagesSent.Enabled
           || Diagnostics.DekafMetrics.BytesSent.Enabled
           || Diagnostics.DekafMetrics.OperationDuration.Enabled
           || Diagnostics.DekafMetrics.ProduceErrors.Enabled;

    private void EmitProduceSuccessMetrics(string topic, RecordMetadata metadata, long startTimestamp)
    {
        var tagList = GetMetricTags(topic);
        Diagnostics.DekafMetrics.MessagesSent.Add(1, tagList);
        Diagnostics.DekafMetrics.BytesSent.Add(metadata.KeySize + metadata.ValueSize, tagList);
        Diagnostics.DekafMetrics.OperationDuration.Record(
            Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, tagList);
    }

    private TagList GetMetricTags(string topic)
        => _metricTagsCache.GetOrAdd(topic, static t => new TagList
        {
            { Diagnostics.DekafDiagnostics.MessagingDestinationName, t }
        });

    /// <summary>
    /// Attempts synchronous produce for awaited ProduceAsync when metadata is cached.
    /// Returns true if successful with the completion source to await.
    /// Throws exceptions for awaited callers (unlike fire-and-forget which captures them).
    /// Uses thread-local metadata cache for maximum performance on hot path.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryProduceSyncForAsync(
        ProducerMessage<TKey, TValue> message,
        bool runContinuationsAsynchronously,
        out PooledValueTaskSource<RecordMetadata>? completion)
        => TryProduceSyncForAsync(
            message.Topic,
            message.Key,
            message.Value,
            message.Headers,
            message.Partition,
            message.Timestamp,
            runContinuationsAsynchronously,
            out completion);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryProduceSyncForAsync(
        string topic,
        TKey? key,
        TValue value,
        Headers? headers,
        int? partition,
        DateTimeOffset? timestamp,
        bool runContinuationsAsynchronously,
        out PooledValueTaskSource<RecordMetadata>? completion)
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
        if (!TryGetCachedTopicInfo(topic, out topicInfo))
        {
            // Cache miss - try MetadataManager
            if (!_metadataManager.TryGetCachedTopicMetadata(topic, out topicInfo) || topicInfo is null)
            {
                return false; // Cache miss, need async refresh
            }

            // Update thread-local cache for next call
            UpdateCachedTopicInfo(topic, topicInfo);
        }

        if (topicInfo!.PartitionCount == 0)
        {
            return false; // Invalid topic state, let async path handle error
        }

        // All checks passed - we can proceed synchronously
        completion = RentCompletion(runContinuationsAsynchronously);
        var result = SyncProduceResult.Success;
        try
        {
            result = TryProduceSyncCore(topic, key, value, headers, partition, timestamp, topicInfo, completion);
        }
        catch (Exception ex)
        {
            // If TryProduceSyncCore throws before setting result/exception on completion,
            // the rented completion would be leaked (never awaited = never returned to pool).
            // Set the exception on the completion so the caller can await it and it gets
            // properly returned to the pool.
            // Note: TryProduceSyncCore may have already called TrySetException, so use Try variant.
            // Don't re-throw here - let the caller await the ValueTask and get the exception.
            completion.TrySetException(ex);
        }

        if (result == SyncProduceResult.BufferFull)
        {
            // Buffer is full — fall back to async path which uses ReserveMemoryAsync
            // (yields instead of blocking a thread, preventing thread pool starvation).
            // TryProduceSyncCore already cleaned up and returned the completion source.
            completion = null;
            return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async ValueTask<RecordMetadata> ProduceAsyncSlow(
        ProducerMessage<TKey, TValue> message,
        Activity? activity,
        ProduceContinuationMode continuationMode,
        CancellationToken cancellationToken)
    {
        // See ProduceAfterPrepare: instrumented awaits interpose a state machine that must not
        // run inline on the broker ack thread. Callers may have gated already, but the slow path
        // re-checks because a metrics listener can start between their check and this one.
        var metricsEnabled = ProducerMetricsEnabled();
        if (continuationMode == ProduceContinuationMode.InlineWhenDirect && (activity is not null || metricsEnabled))
            continuationMode = ProduceContinuationMode.Async;
        var runContinuationsAsynchronously = continuationMode == ProduceContinuationMode.Async;

        // Retry fast path - metadata should already be initialized via InitializeAsync()
        if (TryProduceSyncForAsync(message, runContinuationsAsynchronously, out var fastCompletion))
        {
            return await AwaitProduceCompletionAsync(fastCompletion!, activity, metricsEnabled, message.Topic, cancellationToken).ConfigureAwait(false);
        }

        // Topic cache miss — fetch topic metadata inline and produce
        var completion = RentCompletion(runContinuationsAsynchronously);
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

        return await AwaitProduceCompletionAsync(completion, activity, metricsEnabled, message.Topic, cancellationToken).ConfigureAwait(false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PooledValueTaskSource<RecordMetadata> RentCompletion(bool runContinuationsAsynchronously)
    {
        var completion = _valueTaskSourcePool.Rent();
        if (!runContinuationsAsynchronously)
            completion.SetRunContinuationsAsynchronously(false);
        return completion;
    }

    /// <inheritdoc />
    public ValueTask FireAsync(ProducerMessage<TKey, TValue> message)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));

        ThrowIfNotInitialized();

        // Apply OnSend interceptors before serialization
        message = ApplyOnSendInterceptors(message);

        var activity = StartPublishActivity(ref message);

        // Async serializers cannot run on the synchronous append path — serialize on the async
        // path, then enqueue. Delivery errors are observed and logged (fire-and-forget contract).
        if (_hasAsyncSerializers)
            return FireWithAsyncSerializationAsync(message, activity, deliveryHandler: null);

        // Fast path: try thread-local cached topic metadata first
        var inThreadLocalCache = TryGetCachedTopicInfo(message.Topic, out var topicInfo);
        if (inThreadLocalCache ||
            (_metadataManager.TryGetCachedTopicMetadata(message.Topic, out topicInfo) && topicInfo is not null && topicInfo.PartitionCount > 0))
        {
            if (!inThreadLocalCache)
                UpdateCachedTopicInfo(message.Topic, topicInfo!);

            try
            {
                var appendResult = SerializeAndAppendFromSpansAsync(message.Topic, message.Key, message.Value, message.Headers, message.Partition, message.Timestamp, topicInfo!, null);

                if (appendResult.IsCompleted)
                {
                    // Hot path complete — fully synchronous, zero allocation
                    if (!appendResult.Result)
                        throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));
                    activity?.SetStatus(ActivityStatusCode.Ok);
                    activity?.Dispose();
                    return default;
                }

                // Buffer was full — appendResult is a real async ValueTask
                return FinishProduceAsync(appendResult, activity, message.Topic);
            }
            catch (KafkaTimeoutException ex)
            {
                // BufferMemory backpressure timeout must propagate
                if (activity is not null) Diagnostics.DekafDiagnostics.RecordException(activity, ex);
                activity?.Dispose();
                throw;
            }
            catch (Exception ex)
            {
                // Fire-and-forget: swallow exception but log
                LogFireAndForgetProduceFailed(ex, message.Topic);
                activity?.Dispose();
                return default;
            }
        }

        // Metadata miss — full async path
        return FireAsyncSlow(message, activity);
    }

    /// <inheritdoc />
    public ValueTask FireAsync(string topic, TKey? key, TValue value)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));

        ThrowIfNotInitialized();

        // When interceptors are configured, fall back to ProducerMessage overload
        // so interceptors can inspect/modify the message before serialization.
        if (_interceptors is not null)
            return FireAsync(new ProducerMessage<TKey, TValue> { Topic = topic, Key = key, Value = value });

        // Async serializers divert before the synchronous append path (see FireAsync(message)).
        if (_hasAsyncSerializers)
        {
            return FireWithAsyncSerializationAsync(
                new ProducerMessage<TKey, TValue> { Topic = topic, Key = key, Value = value },
                activity: null,
                deliveryHandler: null);
        }

        // Fast path: no ProducerMessage allocation, no interceptors, no activity tracing
        var inThreadLocalCache = TryGetCachedTopicInfo(topic, out var topicInfo);
        if (inThreadLocalCache ||
            (_metadataManager.TryGetCachedTopicMetadata(topic, out topicInfo) && topicInfo is not null && topicInfo.PartitionCount > 0))
        {
            if (!inThreadLocalCache)
                UpdateCachedTopicInfo(topic, topicInfo!);

            try
            {
                var appendResult = SerializeAndAppendFromSpansAsync(topic, key, value, null, null, null, topicInfo!, null);

                if (appendResult.IsCompleted)
                {
                    if (!appendResult.Result)
                        throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));
                    return default;
                }

                return FinishProduceAsync(appendResult, activity: null, topic);
            }
            catch (KafkaTimeoutException) { throw; }
            catch (Exception ex)
            {
                LogFireAndForgetProduceFailed(ex, topic);
                return default;
            }
        }

        // Metadata miss — allocate ProducerMessage only on cold path
        return FireAsyncSlow(
            new ProducerMessage<TKey, TValue> { Topic = topic, Key = key, Value = value }, activity: null);
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
        if (Volatile.Read(ref _disposed) != 0)
            return false;

        // Check if cache is for this metadata manager, topic, and still valid
        // Use signed comparison to handle TickCount64 wraparound (every ~292 million years)
        var cache = GetOrCreateCache();
        var currentTicks = Dekaf.MonotonicClock.GetMilliseconds();
        if (cache.CachedMetadataManager == _metadataManager &&
            cache.CachedTopicName == topic &&
            cache.CachedTopicInfo is not null &&
            (cache.CachedTopicValidUntilTicks - currentTicks) > 0)
        {
            topicInfo = cache.CachedTopicInfo;
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
        var cache = GetOrCreateCache();
        cache.CachedMetadataManager = _metadataManager;
        cache.CachedTopicName = topic;
        cache.CachedTopicInfo = topicInfo;
        // Cache valid for ~1 second (1000 ticks) - enough for high-throughput bursts
        // while still detecting stale metadata reasonably quickly
        cache.CachedTopicValidUntilTicks = Dekaf.MonotonicClock.GetMilliseconds() + 1000;
    }

    /// <summary>
    /// Core synchronous produce logic for the ProduceAsync fast path (called from TryProduceSyncForAsync).
    /// Handles serialization, partitioning, and accumulator append with proper resource cleanup.
    /// Returns <see cref="SyncProduceResult.BufferFull"/> when the buffer is full instead of
    /// throwing an exception, avoiding ~5-10μs throw/catch overhead per message under backpressure.
    /// </summary>
    private SyncProduceResult TryProduceSyncCore(
        ProducerMessage<TKey, TValue> message,
        TopicInfo topicInfo,
        PooledValueTaskSource<RecordMetadata> completion)
        => TryProduceSyncCore(
            message.Topic,
            message.Key,
            message.Value,
            message.Headers,
            message.Partition,
            message.Timestamp,
            topicInfo,
            completion);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetUniformStickyPartitionCount(
        int? explicitPartition,
        ReadOnlySpan<byte> key,
        bool keyIsNull,
        int partitionCount)
    {
        if (explicitPartition is not null)
            return 0;

        var uniformStickyPartitioner = _uniformStickyPartitioner;
        if (uniformStickyPartitioner is not null)
            return uniformStickyPartitioner.UsesStickyPartition(key, keyIsNull) ? partitionCount : 0;

        return keyIsNull && _partitioner is IBatchCompletionAwarePartitioner ? partitionCount : 0;
    }

    private SyncProduceResult TryProduceSyncCore(
        string topic,
        TKey? key,
        TValue value,
        Headers? headers,
        int? partition,
        DateTimeOffset? timestamp,
        TopicInfo topicInfo,
        PooledValueTaskSource<RecordMetadata> completion)
    {
        Header[]? pooledHeaderArray = null;
        var customPartitionerKey = PooledMemory.Null;
        var customPartitionerValue = PooledMemory.Null;

        try
        {
            var cache = GetOrCreateCache();
            var keyIsNull = key is null;
            var keySpan = ReadOnlySpan<byte>.Empty;
            if (!keyIsNull)
            {
                var keyWriter = new ReusableBufferWriter(ref cache.KeySerializationBuffer, DefaultKeyBufferSize);
                cache.SerializationContext.Topic = topic;
                cache.SerializationContext.Component = SerializationComponent.Key;
                cache.SerializationContext.Headers = headers;
                _keySerializer.Serialize(key!, ref keyWriter, cache.SerializationContext);
                keySpan = keyWriter.WrittenSpan;
                keyWriter.UpdateBufferRef(ref cache.KeySerializationBuffer);
            }

            var valueIsNull = value is null;
            var valueSpan = ReadOnlySpan<byte>.Empty;
            if (!valueIsNull)
            {
                var valueWriter = new ReusableBufferWriter(ref cache.ValueSerializationBuffer, DefaultValueBufferSize);
                cache.SerializationContext.Topic = topic;
                cache.SerializationContext.Component = SerializationComponent.Value;
                cache.SerializationContext.Headers = headers;
                _valueSerializer.Serialize(value!, ref valueWriter, cache.SerializationContext);
                valueSpan = valueWriter.WrittenSpan;
                valueWriter.UpdateBufferRef(ref cache.ValueSerializationBuffer);
            }

            // Determine partition
            int resolvedPartition;
            if (partition is { } explicitPartition)
            {
                resolvedPartition = explicitPartition;
            }
            else
            {
                if (_usesCustomPartitioner)
                {
                    // User partitioners can run arbitrary code, including reentrant ProduceAsync.
                    // Keep serialized bytes independent of thread-local buffers until append copies them.
                    if (!keyIsNull && keySpan.Length > 0)
                    {
                        customPartitionerKey = CopySpanToPooledMemory(keySpan);
                        keySpan = customPartitionerKey.Span;
                    }

                    if (!valueIsNull && valueSpan.Length > 0)
                    {
                        customPartitionerValue = CopySpanToPooledMemory(valueSpan);
                        valueSpan = customPartitionerValue.Span;
                    }
                }

                resolvedPartition = _partitioner.Partition(topic, keySpan, keyIsNull, topicInfo.PartitionCount);
            }

            var batchCompletionPartitionCount = GetUniformStickyPartitionCount(
                partition, keySpan, keyIsNull, topicInfo.PartitionCount);

            // Get timestamp - use fast cached timestamp when no override provided
            var timestampMs = timestamp?.ToUnixTimeMilliseconds() ?? GetFastTimestampMs();

            // Convert headers
            var headerCount = 0;
            if (headers is not null && headers.Count > 0)
            {
                RentAndFillHeaders(headers, out pooledHeaderArray, out headerCount);
            }

            // Append to accumulator synchronously (non-blocking memory reservation).
            // Returns false when buffer is full OR accumulator is disposed.
            if (!_accumulator.TryAppendFromSpansWithCompletion(
                topic,
                resolvedPartition,
                timestampMs,
                keySpan,
                keyIsNull,
                valueSpan,
                valueIsNull,
                pooledHeaderArray,
                headerCount,
                completion,
                batchCompletionPartitionCount))
            {
                // Clean up headers — the async slow path will re-serialize.
                RecordAccumulator.ReturnPooledHeaders(pooledHeaderArray);
                completion.SetRunContinuationsAsynchronously(true);
                _valueTaskSourcePool.Return(completion);
                customPartitionerKey.Return();
                customPartitionerValue.Return();
                return SyncProduceResult.BufferFull;
            }

            customPartitionerKey.Return();
            customPartitionerValue.Return();
            return SyncProduceResult.Success;
        }
        catch (Exception ex)
        {
            RecordAccumulator.ReturnPooledHeaders(pooledHeaderArray);
            customPartitionerKey.Return();
            customPartitionerValue.Return();
            if (ex is not ObjectDisposedException)
                completion.TrySetException(ex);
            throw;
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
                LogInterceptorOnSendFailed(ex, interceptor.GetType().Name);
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
                LogInterceptorOnAcknowledgementFailed(ex, interceptor.GetType().Name);
            }
        }
    }

    /// <summary>
    /// Starts a publish activity for tracing, guarded by HasListeners() to avoid
    /// string interpolation allocation when no diagnostic listener is attached.
    /// Sets standard OTel messaging tags and injects trace context into headers.
    /// </summary>
    private Activity? StartPublishActivity(ref ProducerMessage<TKey, TValue> message)
    {
        if (!Diagnostics.DekafDiagnostics.Source.HasListeners())
            return null;

        var activity = Diagnostics.DekafDiagnostics.Source.StartActivity(
            GetPublishActivityName(message.Topic), ActivityKind.Producer);
        if (activity is not null)
        {
            activity.SetTag(Diagnostics.DekafDiagnostics.MessagingSystem, Diagnostics.DekafDiagnostics.MessagingSystemValue);
            activity.SetTag(Diagnostics.DekafDiagnostics.MessagingDestinationName, message.Topic);
            activity.SetTag(Diagnostics.DekafDiagnostics.MessagingOperationType, "publish");
            if (_options.ClientId is not null)
                activity.SetTag(Diagnostics.DekafDiagnostics.MessagingClientId, _options.ClientId);
            if (message.Key is string stringKey)
                activity.SetTag(Diagnostics.DekafDiagnostics.MessagingMessageKey, stringKey);
            else if (message.Key is not null and not byte[])
                activity.SetTag(Diagnostics.DekafDiagnostics.MessagingMessageKey, message.Key.ToString());
            message = message with { Headers = Diagnostics.TraceContextPropagator.InjectTraceContext(message.Headers, activity) };
        }

        return activity;
    }

    private string GetPublishActivityName(string topic)
        => _activityNameCache.GetOrAdd(topic, static t => $"{t} publish");

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
    public ValueTask FireAsync(ProducerMessage<TKey, TValue> message, Action<RecordMetadata, Exception?> deliveryHandler)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));

        ThrowIfNotInitialized();

        ArgumentNullException.ThrowIfNull(deliveryHandler);

        // Apply OnSend interceptors before serialization
        message = ApplyOnSendInterceptors(message);

        // Async serializers divert before the synchronous append path; the delivery handler is
        // invoked from a background observer when the batch completes (see FireAsync(message)).
        if (_hasAsyncSerializers)
            return FireWithAsyncSerializationAsync(message, activity: null, deliveryHandler);

        // Fast path: try thread-local cached topic metadata first
        var inThreadLocalCache = TryGetCachedTopicInfo(message.Topic, out var topicInfo);
        if (inThreadLocalCache ||
            (_metadataManager.TryGetCachedTopicMetadata(message.Topic, out topicInfo) && topicInfo is not null && topicInfo.PartitionCount > 0))
        {
            if (!inThreadLocalCache)
                UpdateCachedTopicInfo(message.Topic, topicInfo!);

            try
            {
                var appendResult = SerializeAndAppendFromSpansAsync(message.Topic, message.Key, message.Value, message.Headers, message.Partition, message.Timestamp, topicInfo!, deliveryHandler);

                if (appendResult.IsCompleted)
                {
                    // Hot path complete — fully synchronous, zero allocation
                    if (!appendResult.Result)
                        throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));
                    return default;
                }

                // Buffer was full — appendResult is a real async ValueTask
                // Use callback-aware finish so exceptions route to deliveryHandler, not thrown
                return FinishProduceAsyncWithCallback(appendResult, deliveryHandler);
            }
            catch (Exception ex)
            {
                // Deliver exception to callback - don't throw for fire-and-forget style
                try { deliveryHandler(default, ex); } catch (Exception cbEx) { LogBatchCleanupStepFailed(cbEx); }
                return default;
            }
        }

        // Metadata miss — full async path
        return ProduceAsyncWithCallbackSlow(message, deliveryHandler);
    }

    private async ValueTask ProduceInternalAsync(
        ProducerMessage<TKey, TValue> message,
        PooledValueTaskSource<RecordMetadata> completion,
        CancellationToken cancellationToken)
    {
        // Fast path: thread-local topic cache (three reference compares), then the metadata
        // manager's cache; async fetch only on a true miss. The thread-local hit matters for
        // the async-serialization path (issue #2309), which routes every message through here.
        TopicInfo? topicInfo;
        if (!TryGetCachedTopicInfo(message.Topic, out topicInfo) &&
            !_metadataManager.TryGetCachedTopicMetadata(message.Topic, out topicInfo))
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
                throw new ProduceException(
                    $"Failed to fetch metadata for topic '{message.Topic}' within max.block.ms ({_options.MaxBlockMs}ms). " +
                    $"Ensure the topic exists and the Kafka cluster is reachable.")
                { Topic = message.Topic };
            }
        }

        if (topicInfo is null)
        {
            throw new ProduceException($"Topic '{message.Topic}' not found") { Topic = message.Topic };
        }

        if (topicInfo.PartitionCount == 0)
        {
            throw NoUsablePartitionsException(message.Topic, topicInfo);
        }

        UpdateCachedTopicInfo(message.Topic, topicInfo);

        // Serialize key and value to pooled memory (returned to pool when batch completes).
        // Async serializers (issue #2309) are awaited per component; a mixed configuration uses
        // the thread-local sync path for the other component. Key then value sequentially — a
        // both-async configuration pays two serde round trips per message (accepted; the common
        // mixed configuration is unaffected).
        var keyIsNull = message.Key is null;
        var valueIsNull = message.Value is null;
        var key = PooledMemory.Null;
        var value = PooledMemory.Null;
        try
        {
            if (!keyIsNull)
            {
                key = _asyncKeySerializer is not null
                    ? await SerializeToPooledAsync(
                        _asyncKeySerializer, message.Key!, message.Topic,
                        SerializationComponent.Key, message.Headers, cancellationToken).ConfigureAwait(false)
                    : SerializeKeyToPooled(message.Key!, message.Topic, message.Headers);
            }

            if (!valueIsNull)
            {
                value = _asyncValueSerializer is not null
                    ? await SerializeToPooledAsync(
                        _asyncValueSerializer, message.Value!, message.Topic,
                        SerializationComponent.Value, message.Headers, cancellationToken).ConfigureAwait(false)
                    : SerializeValueToPooled(message.Value!, message.Topic, message.Headers);
            }

            // Determine partition
            var partition = message.Partition
                ?? _partitioner.Partition(message.Topic, key.Span, keyIsNull, topicInfo.PartitionCount);
            var batchCompletionPartitionCount = GetUniformStickyPartitionCount(
                message.Partition, key.Span, keyIsNull, topicInfo.PartitionCount);

            // Get timestamp
            var timestamp = message.Timestamp ?? DateTimeOffset.UtcNow;
            var timestampMs = timestamp.ToUnixTimeMilliseconds();

            // Convert headers with minimal allocations
            Header[]? pooledHeaderArray = null;
            var headerCount = 0;
            if (message.Headers is not null && message.Headers.Count > 0)
            {
                RentAndFillHeaders(message.Headers, out pooledHeaderArray, out headerCount);
            }

            // Enqueue to per-partition-affine worker instead of calling AppendAsync inline.
            // The worker will call AppendAsync and set the completion source on success/failure.
            _accumulator.EnqueueAppend(
                message.Topic,
                partition,
                timestampMs,
                key,
                value,
                pooledHeaderArray,
                headerCount,
                completion,
                cancellationToken,
                batchCompletionPartitionCount);
        }
        catch
        {
            // EnqueueAppend transfers ownership of the pooled arrays; any failure before that
            // must return them here.
            key.Return();
            value.Return();
            throw;
        }
    }

    public ValueTask<RecordMetadata> ProduceAsync(
        string topic,
        TKey? key,
        TValue value,
        CancellationToken cancellationToken = default)
    {
        return ProduceAsync(topic, key, value, headers: null, partition: null, timestamp: null, ProduceContinuationMode.Async, cancellationToken);
    }

    ValueTask<RecordMetadata> IProducerFastPath<TKey, TValue>.ProduceAsync(
        string topic,
        TKey? key,
        TValue value,
        Headers? headers,
        int? partition,
        DateTimeOffset? timestamp,
        CancellationToken cancellationToken)
        => ProduceAsync(topic, key, value, headers, partition, timestamp, ProduceContinuationMode.Async, cancellationToken);

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

        var completion = ProduceAllCompletion.Rent(messageList.Count);
        for (var i = 0; i < messageList.Count; i++)
        {
            try
            {
                // InlineWhenDirect: safe because the sole direct continuation is the bounded
                // harvest; instrumented/retry paths upgrade to Async — see ProduceAllCompletion remarks.
                completion.Register(i, ProduceAsync(messageList[i], ProduceContinuationMode.InlineWhenDirect, cancellationToken));
            }
            catch (Exception ex)
            {
                // Synchronous produce failure (disposed, cancelled, interceptor throw) stops
                // registration; already-registered operations still complete before the
                // aggregate faults, so no pooled completion is abandoned.
                completion.RecordFailure(i, ex);
                completion.AbortRegistration(messageList.Count - i);
                break;
            }
        }

        return await completion.WaitAsync().ConfigureAwait(false);
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

        var completion = ProduceAllCompletion.Rent(messageList.Count);
        for (var i = 0; i < messageList.Count; i++)
        {
            var (key, value) = messageList[i];
            try
            {
                // InlineWhenDirect: safe because the sole direct continuation is the bounded
                // harvest; instrumented/retry paths upgrade to Async — see ProduceAllCompletion remarks.
                completion.Register(i, ProduceAsync(topic, key, value, headers: null, partition: null, timestamp: null, ProduceContinuationMode.InlineWhenDirect, cancellationToken));
            }
            catch (Exception ex)
            {
                // See the message-list overload: stop registration, let in-flight operations
                // finish, then surface the failure from the aggregate await.
                completion.RecordFailure(i, ex);
                completion.AbortRegistration(messageList.Count - i);
                break;
            }
        }

        return await completion.WaitAsync().ConfigureAwait(false);
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        if (ProducerCallbackContext.IsInDeliveryCallback)
        {
            throw new InvalidOperationException(
                "FlushAsync cannot be called from a delivery callback because it can deadlock the producer sender thread. Move the flush call outside the callback.");
        }

        ThrowIfNotInitialized();

        // No channel to drain — all produce paths append directly to the accumulator.
        return _accumulator.FlushAsync(cancellationToken);
    }

    public ValueTask PurgeAsync(PurgeOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));

        ThrowIfNotInitialized();

        if ((options & PurgeOptions.All) == PurgeOptions.None)
            return default;

        ThrowIfPurgeCannotRun();

        var exception = new ProduceException(
            ProduceErrorKind.Purged,
            "Produce operation was purged before delivery completed.");

        _accumulator.Purge(options, exception, CompleteInflightEntry);
        return default;
    }

    private void ThrowIfPurgeCannotRun()
    {
        if (_options.TransactionalId is null)
            return;

        var transactionState = _transactionState;
        if (transactionState is TransactionState.InTransaction
            or TransactionState.PreparedTransaction
            or TransactionState.CommittingTransaction
            or TransactionState.AbortingTransaction
            or TransactionState.AbortableError)
        {
            throw new InvalidOperationException(
                "PurgeAsync cannot be called while a transaction is active. Commit or abort the transaction before purging buffered records.");
        }
    }

    /// <inheritdoc />
    public void RegisterMetricForSubscription(ApplicationTelemetryMetric metric)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));

        _telemetryMetricCollector.RegisterMetricForSubscription(metric);
    }

    /// <inheritdoc />
    public void UnregisterMetricFromSubscription(string name)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));

        _telemetryMetricCollector.UnregisterMetricFromSubscription(name);
    }

    public ITransaction<TKey, TValue> BeginTransaction()
    {
        if (string.IsNullOrEmpty(_options.TransactionalId))
        {
            throw new InvalidOperationException("Producer is not transactional. Set TransactionalId in options.");
        }

        ThrowIfFatalTransactionError("Cannot begin transaction");

        if (_transactionState == TransactionState.Uninitialized)
        {
            throw new InvalidOperationException(
                "Transactions not initialized. Call InitTransactionsAsync() before BeginTransaction().");
        }

        if (_transactionState == TransactionState.AbortableError)
        {
            throw new InvalidOperationException(
                "The current transaction has an abortable error. Call AbortAsync() before beginning a new transaction.");
        }

        if (_transactionState == TransactionState.InTransaction)
        {
            throw new InvalidOperationException(
                "A transaction is already in progress. Commit or abort it before starting a new one.");
        }

        if (_transactionState == TransactionState.PreparedTransaction)
        {
            throw new InvalidOperationException(
                "A prepared transaction is waiting for completion. Complete, commit, or abort it before starting a new one.");
        }

        if (_transactionState != TransactionState.Ready)
        {
            throw new InvalidOperationException(
                $"Cannot begin transaction in state: {_transactionState}");
        }

        RefreshTransactionFeaturesAtBoundary();

        _transactionState = TransactionState.InTransaction;
        _preparedTransactionState = PreparedTransactionState.Empty;
        _lastTransactionError = ErrorCode.None;
        NotifyPartitionEnrollmentResetWaiters(ResetPartitionEnrollmentState());

        return new Transaction<TKey, TValue>(this);
    }

    public ValueTask InitTransactionsAsync(CancellationToken cancellationToken = default)
        => InitTransactionsAsync(keepPreparedTransaction: false, cancellationToken);

    public async ValueTask InitTransactionsAsync(
        bool keepPreparedTransaction,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_options.TransactionalId))
        {
            throw new InvalidOperationException(
                "Cannot initialize transactions: TransactionalId is not set in producer options.");
        }

        ThrowIfNotInitialized();
        ThrowIfFatalTransactionError("Cannot initialize transactions");
        await SemaphoreHelper.AcquireOrThrowDisposedAsync(_transactionLock, nameof(KafkaProducer<TKey, TValue>), cancellationToken).ConfigureAwait(false);
        try
        {

            // Step 1: Find the transaction coordinator
            await FindTransactionCoordinatorAsync(cancellationToken).ConfigureAwait(false);

            // Step 2: Initialize the producer ID via the coordinator
            await ReinitializeProducerIdAsync(cancellationToken, keepPreparedTransaction).ConfigureAwait(false);

            _transactionState = _preparedTransactionState.HasTransaction
                ? TransactionState.PreparedTransaction
                : TransactionState.Ready;
        }
        finally
        {
            SemaphoreHelper.ReleaseSafely(_transactionLock);
        }
    }

    public async ValueTask CompletePreparedTransactionAsync(
        PreparedTransactionState preparedState,
        bool committed,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_options.TransactionalId))
        {
            throw new InvalidOperationException(
                "Cannot complete a prepared transaction: TransactionalId is not set in producer options.");
        }

        if (!preparedState.HasTransaction)
            throw new ArgumentException("Prepared transaction state must identify a transaction.", nameof(preparedState));

        ThrowIfNotInitialized();
        ThrowIfFatalTransactionError("Cannot complete prepared transaction");

        if (_transactionState != TransactionState.PreparedTransaction)
        {
            throw new TransactionException(ErrorCode.InvalidTxnState,
                $"Cannot complete prepared transaction in state: {_transactionState}")
            {
                TransactionalId = _options.TransactionalId
            };
        }

        var transactionToComplete = _preparedTransactionState;
        if (preparedState != transactionToComplete)
        {
            throw new TransactionException(ErrorCode.InvalidTxnState,
                "Prepared transaction state does not match the pending prepared transaction.")
            {
                TransactionalId = _options.TransactionalId
            };
        }

        _transactionState = committed
            ? TransactionState.CommittingTransaction
            : TransactionState.AbortingTransaction;

        try
        {
            var applyResponseProducerState =
                transactionToComplete.ProducerId == _producerId
                && transactionToComplete.ProducerEpoch == _producerEpoch;

            await EndTransactionAsync(
                    committed,
                    transactionToComplete.ProducerId,
                    transactionToComplete.ProducerEpoch,
                    applyResponseProducerState,
                    afterRequestWrittenAsync: null,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!committed && !_currentTransactionUsesTV2)
            {
                await ReinitializeProducerIdAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            FinalizeCompletedTransactionState();
        }
    }

    private void ThrowIfTwoPhaseCommitUnsupported(bool keepPreparedTransaction)
    {
        if (!_options.EnableTwoPhaseCommit && !keepPreparedTransaction)
            return;

        if (_currentTransactionFeatureVersion < 3)
        {
            throw new BrokerVersionException(
                "Broker does not support KIP-939 two-phase commit (transaction.version >= 3 required).");
        }
    }

    internal PreparedTransactionState PrepareCurrentTransaction()
    {
        ThrowIfFatalTransactionError("Cannot prepare transaction");

        if (!_options.EnableTwoPhaseCommit)
        {
            throw new TransactionException(ErrorCode.InvalidTxnState,
                "Cannot prepare a transaction when two-phase commit is not enabled.")
            {
                TransactionalId = _options.TransactionalId
            };
        }

        ThrowIfTwoPhaseCommitUnsupported(keepPreparedTransaction: false);

        if (_transactionState != TransactionState.InTransaction)
        {
            throw new TransactionException(ErrorCode.InvalidTxnState,
                $"Cannot prepare transaction in state: {_transactionState}")
            {
                TransactionalId = _options.TransactionalId
            };
        }

        var preparedState = new PreparedTransactionState(_producerId, _producerEpoch);
        if (!preparedState.HasTransaction)
        {
            throw new TransactionException(ErrorCode.InvalidTxnState,
                "Cannot prepare transaction before InitTransactionsAsync has assigned a producer ID.")
            {
                TransactionalId = _options.TransactionalId
            };
        }

        _preparedTransactionState = preparedState;
        _transactionState = TransactionState.PreparedTransaction;
        return preparedState;
    }

    internal void ThrowIfInPreparedTransaction()
    {
        if (_transactionState == TransactionState.PreparedTransaction)
        {
            throw new InvalidOperationException(
                "Cannot perform this operation while the transaction is prepared. Only commit, abort, dispose, or complete are permitted.");
        }
    }

    internal void FinalizeCompletedTransactionState(bool preserveAbortableError = true)
    {
        NotifyPartitionEnrollmentResetWaiters(ResetPartitionEnrollmentState());

        _preparedTransactionState = PreparedTransactionState.Empty;

        var preserveError = _transactionState == TransactionState.FatalError
            || preserveAbortableError && _transactionState == TransactionState.AbortableError;
        if (!preserveError)
        {
            _transactionState = TransactionState.Ready;
        }
    }

    /// <summary>
    /// Re-initializes the producer ID/epoch via the transaction coordinator.
    /// Called after abort to get the bumped epoch (KIP-360), and during initial setup.
    /// </summary>
    /// <summary>
    /// Builds the transaction exception whose type reflects the KIP-1050 classification, and
    /// records the error code for the fail-fast produce guard. Does not change transaction state.
    /// </summary>
    private TransactionException CreateTransactionException(
        ErrorCode errorCode, TransactionErrorClassification classification, string message)
    {
        _lastTransactionError = errorCode;
        return classification switch
        {
            TransactionErrorClassification.Fatal =>
                new FatalTransactionException(errorCode, message) { TransactionalId = _options.TransactionalId },
            TransactionErrorClassification.Abortable =>
                new AbortableTransactionException(errorCode, message) { TransactionalId = _options.TransactionalId },
            _ =>
                new TransactionException(errorCode, message) { TransactionalId = _options.TransactionalId },
        };
    }

    /// <summary>
    /// Handles a non-<see cref="ErrorCode.None"/> transactional error. Returns normally when the error
    /// is retriable (the caller should back off and retry). For fatal errors it transitions to
    /// <see cref="TransactionState.FatalError"/> and for abortable errors to
    /// <see cref="TransactionState.AbortableError"/>, then throws the matching typed exception.
    /// </summary>
    private void ThrowIfNonRetriableTransactionError(ErrorCode errorCode, string operation, bool tv2)
    {
        var classification = TransactionErrorClassifier.Classify(errorCode, tv2);
        if (classification == TransactionErrorClassification.Retriable)
        {
            return;
        }

        _transactionState = classification == TransactionErrorClassification.Fatal
            ? TransactionState.FatalError
            : TransactionState.AbortableError;

        throw CreateTransactionException(errorCode, classification, $"{operation} failed: {errorCode}");
    }

    private async ValueTask<TResponse> SendWithConnectionLeaseAsync<TRequest, TResponse>(
        int brokerId,
        TRequest request,
        CancellationToken cancellationToken,
        short minimumRequiredVersion = short.MinValue,
        bool captureTransactionFeatures = false,
        bool requireTransactionFeatureMatch = false,
        bool keepPreparedTransaction = false)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        using var connectionLease = await _connectionPool.LeaseConnectionAsync(
            brokerId,
            cancellationToken).ConfigureAwait(false);
        var connection = connectionLease.Connection;
        if (captureTransactionFeatures)
            CaptureTransactionFeatures(keepPreparedTransaction);
        else if (requireTransactionFeatureMatch)
            EnsureTransactionFeatureMatch();

        var apiVersion = _metadataManager.GetNegotiatedApiVersion(
            connection,
            KafkaMessageMetadata<TRequest, TResponse>.ApiKey,
            KafkaMessageMetadata<TRequest, TResponse>.LowestSupportedVersion,
            KafkaMessageMetadata<TRequest, TResponse>.HighestSupportedVersion);
        if (apiVersion < minimumRequiredVersion)
        {
            throw new BrokerVersionException(
                $"Broker {brokerId} does not support {KafkaMessageMetadata<TRequest, TResponse>.ApiKey} " +
                $"v{minimumRequiredVersion} required by this operation; negotiated v{apiVersion}.");
        }

        return await connection.SendAsync<TRequest, TResponse>(
            request,
            apiVersion,
            cancellationToken).ConfigureAwait(false);
    }

    private void CaptureTransactionFeatures(bool keepPreparedTransaction)
    {
        var status = _metadataManager.GetFinalizedFeatureStatus(
            TransactionVersionFeature,
            out var featureVersion);
        if (status != FinalizedFeatureStatus.Present)
            featureVersion = 0;

        _currentTransactionFeatureVersion = featureVersion;
        _currentTransactionUsesTV2 = featureVersion >= 2;
        ThrowIfTwoPhaseCommitUnsupported(keepPreparedTransaction);
    }

    private void RefreshTransactionFeaturesAtBoundary()
    {
        var status = _metadataManager.GetFinalizedFeatureStatus(
            TransactionVersionFeature,
            out var featureVersion);
        if (status == FinalizedFeatureStatus.Unavailable)
            return;

        if (status == FinalizedFeatureStatus.Absent)
            featureVersion = 0;

        _currentTransactionFeatureVersion = featureVersion;
        _currentTransactionUsesTV2 = featureVersion >= 2;
        ThrowIfTwoPhaseCommitUnsupported(keepPreparedTransaction: false);
    }

    private void EnsureTransactionFeatureMatch()
    {
        var status = _metadataManager.GetFinalizedFeatureStatus(
            TransactionVersionFeature,
            out var featureVersion);
        if (status == FinalizedFeatureStatus.Unavailable)
            return;

        if (status == FinalizedFeatureStatus.Absent)
            featureVersion = 0;

        if (featureVersion != _currentTransactionFeatureVersion)
        {
            _transactionState = TransactionState.FatalError;
            throw CreateTransactionException(
                ErrorCode.UnsupportedVersion,
                TransactionErrorClassification.Fatal,
                "The coordinator transaction.version changed after producer initialization. " +
                "Close the producer and initialize a new instance before beginning another transaction.");
        }
    }

    private int CalculateRequestRetryBackoff(int zeroBasedAttempt)
        => ExponentialRetryBackoff.CalculateDelayMilliseconds(
            _options.RetryBackoffMs,
            _options.RetryBackoffMaxMs,
            zeroBasedAttempt + 1);

    internal async ValueTask ReinitializeProducerIdAsync(
        CancellationToken cancellationToken,
        bool keepPreparedTransaction = false)
    {
        const int maxRetries = 10;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            var request = new InitProducerIdRequest
            {
                TransactionalId = _options.TransactionalId,
                TransactionTimeoutMs = _options.TransactionTimeoutMs,
                ProducerId = _producerId,
                ProducerEpoch = _producerEpoch,
                EnableTwoPhaseCommit = _options.EnableTwoPhaseCommit,
                KeepPreparedTransaction = keepPreparedTransaction
            };

            var response = await SendWithConnectionLeaseAsync<InitProducerIdRequest, InitProducerIdResponse>(
                    _transactionCoordinatorId,
                    request,
                    cancellationToken,
                    minimumRequiredVersion: (_options.EnableTwoPhaseCommit || keepPreparedTransaction)
                        ? (short)6
                        : short.MinValue,
                    captureTransactionFeatures: true,
                    keepPreparedTransaction: keepPreparedTransaction)
                .ConfigureAwait(false);

            if (response.ErrorCode != ErrorCode.None)
            {
                var classification = TransactionErrorClassifier.Classify(response.ErrorCode, _currentTransactionUsesTV2);

                if (classification == TransactionErrorClassification.Retriable)
                {
                    if (attempt == maxRetries - 1)
                        break;

                    var retryDelayMs = CalculateRequestRetryBackoff(attempt);
                    if (response.ErrorCode == ErrorCode.NotCoordinator)
                    {
                        LogInitProducerIdNotCoordinator(attempt + 1, maxRetries);

                        await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                        await FindTransactionCoordinatorAsync(cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    LogInitProducerIdRetriableError(response.ErrorCode, attempt + 1, maxRetries, retryDelayMs);

                    await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                // InitProducerId runs before a transaction is active, so abortable errors do not
                // transition to AbortableError; only fatal errors mark the producer unusable.
                if (classification == TransactionErrorClassification.Fatal)
                {
                    _transactionState = TransactionState.FatalError;
                }

                throw CreateTransactionException(response.ErrorCode, classification,
                    $"InitProducerId failed: {response.ErrorCode}");
            }

            _producerId = response.ProducerId;
            _producerEpoch = response.ProducerEpoch;

            _accumulator.ProducerId = _producerId;
            _accumulator.ProducerEpoch = _producerEpoch;
            _accumulator.IsTransactional = true;

            _accumulator.ResetSequenceNumbers();
            _preparedTransactionState = response.OngoingTransactionProducerId >= 0
                && response.OngoingTransactionProducerEpoch >= 0
                    ? new PreparedTransactionState(
                        response.OngoingTransactionProducerId,
                        response.OngoingTransactionProducerEpoch)
                    : PreparedTransactionState.Empty;

            LogTransactionsInitialized(_producerId, _producerEpoch);
            return;
        }

        throw new TransactionException(ErrorCode.CoordinatorLoadInProgress,
            $"InitProducerId failed after {maxRetries} retries")
        {
            TransactionalId = _options.TransactionalId
        };
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

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            var response = await SendWithConnectionLeaseAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                brokers[0].NodeId,
                request,
                cancellationToken).ConfigureAwait(false);

            if (response.Coordinators.Count == 0)
            {
                throw new TransactionException(ErrorCode.CoordinatorNotAvailable,
                    "FindCoordinator returned an empty Coordinators array")
                {
                    TransactionalId = _options.TransactionalId
                };
            }

            var coordinator = response.Coordinators[0];
            var errorCode = coordinator.ErrorCode;

            if (errorCode is ErrorCode.CoordinatorNotAvailable or ErrorCode.NotCoordinator)
            {
                if (attempt == maxRetries - 1)
                    break;

                var retryDelayMs = CalculateRequestRetryBackoff(attempt);
                LogTransactionCoordinatorNotAvailable(errorCode, attempt + 1, maxRetries, retryDelayMs);

                await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
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

            _transactionCoordinatorId = coordinator.NodeId;
            _connectionPool.RegisterBroker(coordinator.NodeId, coordinator.Host, coordinator.Port);

            LogTransactionCoordinatorFound(_transactionCoordinatorId, _options.TransactionalId);
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

        var request = new AddPartitionsToTxnRequest
        {
            TransactionalId = _options.TransactionalId!,
            ProducerId = _producerId,
            ProducerEpoch = _producerEpoch,
            Topics = topics
        };

        const int maxRetries = 5;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            var response = await SendWithConnectionLeaseAsync<AddPartitionsToTxnRequest, AddPartitionsToTxnResponse>(
                    _transactionCoordinatorId,
                    request,
                    cancellationToken,
                    requireTransactionFeatureMatch: true)
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
                // Runs on the BrokerSender background path: transitioning to AbortableError/FatalError
                // here is what makes subsequent ProduceAsync calls fail fast. This error is not in the
                // classifier's retriable set, so the call always throws (never returns).
                ThrowIfNonRetriableTransactionError(
                    firstNonRetriableError.Value,
                    $"AddPartitionsToTxn for {errorContext}",
                    _currentTransactionUsesTV2);
            }

            if (!hasRetriableError)
            {
                return; // Success
            }

            if (attempt == maxRetries - 1)
                break;

            var retryDelayMs = CalculateRequestRetryBackoff(attempt);
            LogAddPartitionsToTxnRetriableError(attempt + 1, maxRetries, retryDelayMs);

            await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
        }

        throw new TransactionException(ErrorCode.ConcurrentTransactions,
            $"AddPartitionsToTxn failed after {maxRetries} retries")
        {
            TransactionalId = _options.TransactionalId
        };
    }

    internal ValueTask EndTransactionAsync(bool committed, CancellationToken cancellationToken)
        => EndTransactionAsync(
            committed,
            _producerId,
            _producerEpoch,
            applyResponseProducerState: true,
            afterRequestWrittenAsync: null,
            cancellationToken);

    internal ValueTask EndTransactionAsync(
        bool committed,
        Func<ValueTask>? afterRequestWrittenAsync,
        CancellationToken cancellationToken)
        => EndTransactionAsync(
            committed,
            _producerId,
            _producerEpoch,
            applyResponseProducerState: true,
            afterRequestWrittenAsync,
            cancellationToken);

    private async ValueTask EndTransactionAsync(
        bool committed,
        long producerId,
        short producerEpoch,
        bool applyResponseProducerState,
        Func<ValueTask>? afterRequestWrittenAsync,
        CancellationToken cancellationToken)
    {
        const int maxRetries = 5;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            var request = new EndTxnRequest
            {
                TransactionalId = _options.TransactionalId!,
                ProducerId = producerId,
                ProducerEpoch = producerEpoch,
                Committed = committed
            };

            EndTxnResponse response;
            using (var connectionLease = await _connectionPool.LeaseConnectionAsync(
                       _transactionCoordinatorId,
                       cancellationToken).ConfigureAwait(false))
            {
                var connection = connectionLease.Connection;
                EnsureTransactionFeatureMatch();
                var apiVersion = _metadataManager.GetNegotiatedApiVersion(
                    connection,
                    ApiKey.EndTxn,
                    EndTxnRequest.LowestSupportedVersion,
                    EndTxnRequest.HighestSupportedVersion);
                if (afterRequestWrittenAsync is null)
                {
                    response = await connection
                        .SendAsync<EndTxnRequest, EndTxnResponse>(
                            request, apiVersion, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    if (connection is not IKafkaPipelinedWriteCompletionConnection writeCompletionConnection)
                    {
                        throw new InvalidOperationException(
                            "The transaction coordinator connection cannot report request write completion.");
                    }

                    var responseTask = await writeCompletionConnection
                        .SendPipelinedAfterWriteAsync<EndTxnRequest, EndTxnResponse>(
                            request, apiVersion, cancellationToken)
                        .ConfigureAwait(false);
                    var responseConsumptionStarted = false;
                    try
                    {
                        var requestWrittenCallback = afterRequestWrittenAsync;
                        afterRequestWrittenAsync = null;
                        await requestWrittenCallback().ConfigureAwait(false);

                        var responseValueTask = responseTask.AsValueTask();
                        responseConsumptionStarted = true;
                        response = await responseValueTask.ConfigureAwait(false);
                    }
                    finally
                    {
                        if (!responseConsumptionStarted)
                            responseTask.Abandon();
                    }
                }
            }

            if (response.ErrorCode == ErrorCode.None)
            {
                // TV2 (v5+): broker returns bumped ProducerId/Epoch in EndTxn response.
                // Apply them so the next transaction uses the new identity without
                // a separate InitProducerId round-trip.
                // Safe without _epochBumpLock: EndTxn is called only after FlushAsync
                // drains all in-flight batches, so no BrokerSender is active.
                if (applyResponseProducerState && _currentTransactionUsesTV2 && response.ProducerId >= 0)
                {
                    _producerId = response.ProducerId;
                    _producerEpoch = response.ProducerEpoch;
                    _accumulator.ProducerId = _producerId;
                    _accumulator.ProducerEpoch = _producerEpoch;
                    _accumulator.ResetSequenceNumbers();
                }

                return;
            }

            // Fatal or abortable errors transition state and throw the matching typed exception;
            // this returns only for retriable errors, which are handled with backoff below.
            ThrowIfNonRetriableTransactionError(response.ErrorCode,
                $"EndTxn ({(committed ? "commit" : "abort")})", _currentTransactionUsesTV2);

            if (attempt == maxRetries - 1)
                break;

            var retryDelayMs = CalculateRequestRetryBackoff(attempt);
            if (response.ErrorCode == ErrorCode.NotCoordinator)
            {
                LogEndTxnNotCoordinator(attempt + 1, maxRetries);
                await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                await FindTransactionCoordinatorAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            LogEndTxnRetriableError(response.ErrorCode, attempt + 1, maxRetries, retryDelayMs);
            await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
            continue;
        }

        throw new TransactionException(ErrorCode.CoordinatorLoadInProgress,
            $"EndTxn ({(committed ? "commit" : "abort")}) failed after {maxRetries} retries")
        {
            TransactionalId = _options.TransactionalId
        };
    }

    internal ValueTask SendOffsetsToTransactionInternalAsync(
        IEnumerable<TopicPartitionOffset> offsets,
        string consumerGroupId,
        CancellationToken cancellationToken)
        => SendOffsetsToTransactionInternalAsync(
            offsets,
            consumerGroupId,
            generationIdOrMemberEpoch: -1,
            memberId: string.Empty,
            groupInstanceId: null,
            cancellationToken);

    internal ValueTask SendOffsetsToTransactionInternalAsync(
        IEnumerable<TopicPartitionOffset> offsets,
        ConsumerGroupMetadata consumerGroupMetadata,
        CancellationToken cancellationToken)
        => SendOffsetsToTransactionInternalAsync(
            offsets,
            consumerGroupMetadata.GroupId,
            consumerGroupMetadata.GenerationId,
            consumerGroupMetadata.MemberId,
            consumerGroupMetadata.GroupInstanceId,
            cancellationToken);

    private async ValueTask SendOffsetsToTransactionInternalAsync(
        IEnumerable<TopicPartitionOffset> offsets,
        string consumerGroupId,
        int generationIdOrMemberEpoch,
        string memberId,
        string? groupInstanceId,
        CancellationToken cancellationToken)
    {
        // Each path is retried as a unit. TV1 uses AddOffsetsToTxn -> FindCoordinator ->
        // TxnOffsetCommit v4. TV2 discovers the group coordinator first, then uses v5 or v6
        // to enroll offsets implicitly when that exact connection supports it. V6 uses
        // request-local topic IDs; a connection capped at v4 retains explicit enrollment.
        const int maxRetries = 5;
        var tv2 = _currentTransactionUsesTV2;

        // Materialize once so a lost response can retry safely even when the caller supplied
        // a single-use enumerable.
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
                CommittedOffset = offset.Offset,
                CommittedLeaderEpoch = offset.LeaderEpoch,
                CommittedMetadata = offset.Metadata
            });
        }

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            // TV1 always performs explicit offset enrollment before coordinator discovery.
            if (!tv2)
            {
                var addOffsetsError = await SendAddOffsetsToTransactionAsync(
                        consumerGroupId,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (addOffsetsError != ErrorCode.None)
                {
                    if (await PrepareAddOffsetsRetryAsync(
                            addOffsetsError,
                            attempt,
                            maxRetries,
                            tv2,
                            cancellationToken)
                        .ConfigureAwait(false))
                    {
                        continue;
                    }

                    break;
                }
            }

            // Find the group coordinator. This remains uncached so NotCoordinator retries
            // refresh only the affected group coordinator information.
            var brokers = _metadataManager.Metadata.GetBrokers();
            var findCoordRequest = new FindCoordinatorRequest
            {
                Key = consumerGroupId,
                KeyType = CoordinatorType.Group
            };

            var findCoordResponse = await SendWithConnectionLeaseAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                    brokers[0].NodeId,
                    findCoordRequest,
                    cancellationToken)
                .ConfigureAwait(false);

            if (findCoordResponse.Coordinators.Count == 0)
            {
                // Treat an empty coordinator set as transiently unavailable and retry.
                if (attempt == maxRetries - 1)
                    break;
                var retryDelayMs = CalculateRequestRetryBackoff(attempt);
                await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var coord = findCoordResponse.Coordinators[0];
            _connectionPool.RegisterBroker(coord.NodeId, coord.Host, coord.Port);

            if (coord.ErrorCode != ErrorCode.None)
            {
                ThrowIfNonRetriableTransactionError(coord.ErrorCode,
                    $"FindCoordinator for consumer group '{consumerGroupId}'", tv2);
                if (attempt == maxRetries - 1)
                    break;
                var retryDelayMs = CalculateRequestRetryBackoff(attempt);
                await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                continue;
            }

            using var coordinatorLease = await _connectionPool.LeaseConnectionAsync(
                coord.NodeId,
                cancellationToken).ConfigureAwait(false);
            EnsureTransactionFeatureMatch();
            var coordinatorConnection = coordinatorLease.Connection;
            var highestAllowedVersion = tv2
                ? TxnOffsetCommitRequest.HighestSupportedVersion
                : (short)4;
            var txnOffsetCommitVersion = _metadataManager.GetNegotiatedApiVersion(
                coordinatorConnection,
                ApiKey.TxnOffsetCommit,
                TxnOffsetCommitRequest.LowestSupportedVersion,
                highestAllowedVersion);

            if (tv2 && txnOffsetCommitVersion < 5)
            {
                var addOffsetsError = await SendAddOffsetsToTransactionAsync(
                        consumerGroupId,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (addOffsetsError != ErrorCode.None)
                {
                    if (await PrepareAddOffsetsRetryAsync(
                            addOffsetsError,
                            attempt,
                            maxRetries,
                            tv2,
                            cancellationToken)
                        .ConfigureAwait(false))
                    {
                        continue;
                    }

                    break;
                }
            }

            OffsetTopicIdRequestMap? topicIdMap = null;
            List<TxnOffsetCommitRequestTopic> txnTopics;
            if (txnOffsetCommitVersion >= TxnOffsetCommitRequest.TopicIdVersion)
            {
                (txnTopics, topicIdMap) = await BuildTxnOffsetCommitTopicsAsync(
                        topicOffsets,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (topicIdMap is null)
                    txnOffsetCommitVersion = TxnOffsetCommitRequest.TopicIdVersion - 1;
            }
            else
            {
                txnTopics = BuildTxnOffsetCommitTopics(topicOffsets, topicIdMap: null);
            }

            var txnOffsetCommitRequest = new TxnOffsetCommitRequest
            {
                TransactionalId = _options.TransactionalId!,
                GroupId = consumerGroupId,
                ProducerId = _producerId,
                ProducerEpoch = _producerEpoch,
                GenerationIdOrMemberEpoch = generationIdOrMemberEpoch,
                MemberId = memberId,
                GroupInstanceId = groupInstanceId,
                Topics = txnTopics
            };

            TxnOffsetCommitResponse txnOffsetCommitResponse;
            try
            {
                txnOffsetCommitResponse = await coordinatorConnection
                    .SendAsync<TxnOffsetCommitRequest, TxnOffsetCommitResponse>(
                        txnOffsetCommitRequest,
                        txnOffsetCommitVersion,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (
                attempt < maxRetries - 1
                && !cancellationToken.IsCancellationRequested
                && RetryHelper.IsRetriableRequestFailure(ex))
            {
                await Task.Delay(CalculateRequestRetryBackoff(attempt), cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            ErrorCode? commitError = null;
            string? commitContext = null;
            try
            {
                var responseSnapshot = topicIdMap?.CaptureResponseSnapshot();
                foreach (var topicResult in txnOffsetCommitResponse.Topics)
                {
                    var topicName = topicIdMap is null
                        ? topicResult.Name
                        : topicIdMap.MatchResponseTopic(
                            topicResult.TopicId,
                            responseSnapshot!,
                            "TxnOffsetCommit",
                            responseMismatchIsRetriable: true);

                    foreach (var partitionResult in topicResult.Partitions)
                    {
                        if (partitionResult.ErrorCode != ErrorCode.None)
                        {
                            commitError = partitionResult.ErrorCode;
                            commitContext = $"TxnOffsetCommit for {topicName}-{partitionResult.PartitionIndex}";
                            break;
                        }
                    }

                    if (commitError.HasValue)
                        break;
                }
            }
            catch (Exception ex) when (
                attempt < maxRetries - 1
                && !cancellationToken.IsCancellationRequested
                && RetryHelper.IsRetriableRequestFailure(ex))
            {
                await _metadataManager.RefreshMetadataAsync(
                        topicOffsets.Keys,
                        forceRefresh: true,
                        cancellationToken)
                    .ConfigureAwait(false);
                await Task.Delay(CalculateRequestRetryBackoff(attempt), cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            if (commitError.HasValue)
            {
                ThrowIfNonRetriableTransactionError(commitError.Value, commitContext!, tv2);
                if (attempt == maxRetries - 1)
                    break;
                if (commitError.Value.RequiresMetadataRefresh())
                {
                    await _metadataManager.RefreshMetadataAsync(
                            topicOffsets.Keys,
                            forceRefresh: true,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                var retryDelayMs = CalculateRequestRetryBackoff(attempt);
                await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                continue;
            }

            return;
        }

        throw new TransactionException(ErrorCode.CoordinatorLoadInProgress,
            $"SendOffsetsToTransaction failed after {maxRetries} retries")
        {
            TransactionalId = _options.TransactionalId
        };
    }

    private async ValueTask<(List<TxnOffsetCommitRequestTopic> Topics, OffsetTopicIdRequestMap? TopicIdMap)>
        BuildTxnOffsetCommitTopicsAsync(
            Dictionary<string, List<TxnOffsetCommitRequestPartition>> topicOffsets,
            CancellationToken cancellationToken)
    {
        var requiresRefresh = topicOffsets.Keys.Any(topicName =>
        {
            var topic = _metadataManager.Metadata.GetTopic(topicName);
            return topic is null || topic.TopicId == Guid.Empty;
        });

        if (requiresRefresh)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.MaxBlockMs);
            try
            {
                await _metadataManager.RefreshMetadataAsync(
                        topicOffsets.Keys,
                        forceRefresh: true,
                        timeoutCts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return (BuildTxnOffsetCommitTopics(topicOffsets, topicIdMap: null), null);
            }
            catch (Exception ex) when (
                !cancellationToken.IsCancellationRequested
                && RetryHelper.IsRetriableRequestFailure(ex))
            {
                return (BuildTxnOffsetCommitTopics(topicOffsets, topicIdMap: null), null);
            }
        }

        var topicIdMap = new OffsetTopicIdRequestMap(_metadataManager.Metadata, topicOffsets.Count);
        try
        {
            return (BuildTxnOffsetCommitTopics(topicOffsets, topicIdMap), topicIdMap);
        }
        catch (KafkaException exception) when (exception.ErrorCode == ErrorCode.UnknownTopicId)
        {
            return (BuildTxnOffsetCommitTopics(topicOffsets, topicIdMap: null), null);
        }
    }

    private static List<TxnOffsetCommitRequestTopic> BuildTxnOffsetCommitTopics(
        Dictionary<string, List<TxnOffsetCommitRequestPartition>> topicOffsets,
        OffsetTopicIdRequestMap? topicIdMap)
    {
        var topics = new List<TxnOffsetCommitRequestTopic>(topicOffsets.Count);
        foreach (var (topicName, partitions) in topicOffsets)
        {
            topics.Add(new TxnOffsetCommitRequestTopic
            {
                Name = topicName,
                TopicId = topicIdMap?.AddTopic(topicName, "TxnOffsetCommit") ?? Guid.Empty,
                Partitions = partitions
            });
        }

        return topics;
    }

    private async ValueTask<ErrorCode> SendAddOffsetsToTransactionAsync(
        string consumerGroupId,
        CancellationToken cancellationToken)
    {
        var request = new AddOffsetsToTxnRequest
        {
            TransactionalId = _options.TransactionalId!,
            ProducerId = _producerId,
            ProducerEpoch = _producerEpoch,
            GroupId = consumerGroupId
        };

        var response = await SendWithConnectionLeaseAsync<AddOffsetsToTxnRequest, AddOffsetsToTxnResponse>(
                _transactionCoordinatorId,
                request,
                cancellationToken,
                requireTransactionFeatureMatch: true)
            .ConfigureAwait(false);
        return response.ErrorCode;
    }

    private async ValueTask<bool> PrepareAddOffsetsRetryAsync(
        ErrorCode errorCode,
        int attempt,
        int maxRetries,
        bool tv2,
        CancellationToken cancellationToken)
    {
        ThrowIfNonRetriableTransactionError(errorCode, "AddOffsetsToTxn", tv2);
        if (attempt == maxRetries - 1)
            return false;

        if (errorCode == ErrorCode.NotCoordinator)
            await FindTransactionCoordinatorAsync(cancellationToken).ConfigureAwait(false);

        await Task.Delay(CalculateRequestRetryBackoff(attempt), cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public ITopicProducer<TKey, TValue> ForTopic(string topic)
    {
        ArgumentNullException.ThrowIfNull(topic);
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));

        return new TopicProducer<TKey, TValue>(this, topic, ownsProducer: false);
    }

    ProducerDeliveryDiagnosticsSnapshot IProducerDiagnostics.GetDeliveryDiagnosticsSnapshot()
    {
        var snapshot = _accumulator.GetDeliveryDiagnosticsSnapshot();
        if (snapshot.DiagnosticsEnabled)
            snapshot.ConnectionReapEvents.AddRange(
                ((IConnectionPoolDiagnostics)_connectionPool).GetConnectionReapDiagnosticsSnapshot());

        return snapshot;
    }

    void IProducerDiagnostics.ResetProduceRequestDiagnostics() =>
        _accumulator.ResetProduceRequestDiagnostics();

    int IProducerDiagnostics.MaxObservedBrokerThrottleTimeMs =>
        _connectionPool is IConnectionPoolDiagnostics diagnostics
            ? diagnostics.GetMaxObservedBrokerThrottleTimeMs()
            : _telemetryMetricCollector.MaxObservedBrokerThrottleTimeMs;

    private async Task SenderLoopAsync(CancellationToken cancellationToken)
    {
        // PER-BROKER SENDER ARCHITECTURE: Each broker gets a dedicated BrokerSender with its own
        // channel and single-threaded send loop. This guarantees wire-order for same-partition
        // requests. Retry-safe record ordering requires idempotence (sequence numbers + epoch);
        // non-idempotent pipelining accepts Kafka's documented retry-reordering trade-off.
        //
        // Ready → Drain → Distribute loop:
        // 1. Ready() checks which brokers have sendable data (sealed batches in partition deques)
        // 2. Drain() pulls one batch per partition for each ready broker
        // 3. Distribute routes pre-drained batch lists to BrokerSenders
        // 4. WaitForWakeupAsync() sleeps until a new batch is sealed or a response arrives

        try
        {
            // Register shutdown token once — persists for the lifetime of the sender loop.
            // The wakeup signal uses this for zero-allocation cancellation detection.
            _accumulator.RegisterWakeupShutdownToken(cancellationToken);

            // Allocate collections once and reuse across iterations to avoid per-iteration allocations
            var readyNodes = new HashSet<int>();
            var drainResult = new Dictionary<int, List<ReadyBatch>>();
            var batchListPool = new Stack<List<ReadyBatch>>();

            while (!cancellationToken.IsCancellationRequested)
            {
                // 1. Check which brokers have sendable data
                readyNodes.Clear();
                var (nextCheckDelayMs, unknownLeadersExist) = _accumulator.Ready(
                    _metadataManager, readyNodes);

                if (readyNodes.Count > 0)
                {
                    try
                    {
                        // 2. Drain one batch per partition for each ready broker
                        _accumulator.Drain(
                            _metadataManager,
                            readyNodes,
                            _options.MaxRequestSize > 0 ? _options.MaxRequestSize : 1048576,
                            drainResult,
                            batchListPool);

                        // 3. Distribute pre-drained batch lists to broker senders.
                        // Use bulk enqueue so all batches for a broker are written to the
                        // event channel in a tight loop without yielding. This ensures the
                        // BrokerSender's send loop sees all batches when it coalesces,
                        // reducing per-request overhead in multi-broker setups.
                        foreach (var (brokerId, batchList) in drainResult)
                        {
                            if (batchList.Count == 0)
                                continue;

                            // Complete delivery task for each batch
                            for (var i = 0; i < batchList.Count; i++)
                            {
                                batchList[i].CompleteDelivery();
                                if (_options.EnableDeliveryDiagnostics)
                                    batchList[i].AppendDiag('Q');
                            }

                            try
                            {
                                var brokerSender = GetOrCreateBrokerSender(brokerId);
                                brokerSender.EnqueueBulk(batchList);
                            }
                            catch (Exception ex)
                            {
                                // Fail all batches in this batch list.
                                // Note: if EnqueueBulk hit a completed channel, it already failed
                                // remaining batches via FailEnqueuedBatch — but batch.Fail() is
                                // idempotent (guarded by Interlocked.Exchange), so double-fail is safe.
                                for (var i = 0; i < batchList.Count; i++)
                                    FailAndCleanupBatch(batchList[i], ex);
                            }
                        }
                    }
                    finally
                    {
                        // Return batch lists to pool for reuse (including on cancellation paths)
                        foreach (var (_, batchList) in drainResult)
                        {
                            batchList.Clear();
                            batchListPool.Push(batchList);
                        }

                        drainResult.Clear();
                    }
                }

                // 4. If batches exist for partitions with unknown leaders, trigger metadata refresh.
                // This handles partition expansion: producer cached 2-partition metadata but
                // topic now has 4 partitions. Without refresh, those batches sit in deque forever.
                // Matches Java's RecordAccumulator.ready() unknownLeadersExist behavior.
                if (unknownLeadersExist)
                {
                    try
                    {
                        await _metadataManager.RefreshMetadataAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception) when (!cancellationToken.IsCancellationRequested)
                    {
                        // Metadata refresh failed — will retry on next iteration
                    }
                }

                // 5. Exit after close when all work is done.
                // Closed is set at the start of CloseAsync, but FlushAsync (called within
                // CloseAsync) waits for _inFlightBatchCount == 0. We must keep the sender
                // alive until FlushAsync completes by checking in-flight batches too.
                if (_accumulator.Closed && readyNodes.Count == 0 && !_accumulator.HasPendingWork())
                    break;

                // 6. Wait for wakeup signal (new batch sealed, response complete, or timeout)
                try
                {
                    await _accumulator.WaitForWakeupAsync(
                        readyNodes.Count > 0 ? 0 : nextCheckDelayMs).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            LogSenderLoopFailed(ex);
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
            _inflightTracker.Complete(entry);
            batch.InflightEntry = null;
        }
    }

    /// <summary>
    /// Fails a batch and cleans up all associated resources (inflight entry, memory, pool).
    /// Every operation is wrapped in try/catch to guarantee cleanup completes even if
    /// earlier steps throw — preventing orphaned completion sources that cause producer hangs.
    /// Unlike BrokerSender.FailAndCleanupBatch, this handles pre-send failures (e.g., metadata
    /// lookup timeout) where the batch never reached a BrokerSender — so no statistics recording
    /// or acknowledgement callback is needed (those are BrokerSender responsibilities).
    /// </summary>
    private void FailAndCleanupBatch(ReadyBatch batch, Exception ex)
    {
        FailBatch(batch, ex, sendCompletionClaimed: false);
        try { _accumulator.ReturnReadyBatch(batch); }
        catch (Exception returnEx) { LogBatchCleanupStepFailed(returnEx); }
    }

    private void FailAndCleanupBatch(ReadyBatch batch, int expectedGeneration, Exception ex)
    {
        if (!batch.TryAcquireResourcePin(expectedGeneration))
            return;

        var sendCompletionClaimed = false;
        try
        {
            sendCompletionClaimed = batch.TryClaimSendCompletion(expectedGeneration);
        }
        finally
        {
            batch.ReleaseResourcePin();
        }

        if (!sendCompletionClaimed)
            return;

        try
        {
            FailBatch(batch, ex, sendCompletionClaimed: true);
        }
        finally
        {
            try { _accumulator.CompleteTerminalBookkeepingAndReturnReadyBatch(batch); }
            catch (Exception returnEx) { LogBatchCleanupStepFailed(returnEx); }
        }
    }

    private void FailBatch(ReadyBatch batch, Exception ex, bool sendCompletionClaimed)
    {
        try { CompleteInflightEntry(batch); }
        catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx); }
        try
        {
            if (sendCompletionClaimed)
                batch.FailAfterSendCompletionClaimed(ex);
            else
                batch.Fail(ex);
        }
        catch (Exception failEx) { LogBatchCleanupStepFailed(failEx); }
        try
        {
            _accumulator.ReleaseBatchMemory(batch);
        }
        catch (Exception memEx) { LogBatchCleanupStepFailed(memEx); }
        // Remove from tracking BEFORE returning to pool. ReturnReadyBatch is idempotent
        // (atomic _returnedToPool flag), so this is safe even if another path races.
        try { _accumulator.OnBatchExitsPipeline(batch); }
        catch (Exception exitEx) { LogBatchCleanupStepFailed(exitEx); }
    }

    /// <summary>
    /// Gets or creates a BrokerSender for the given broker ID.
    /// Each broker gets a dedicated sender with its own channel and single-threaded send loop.
    /// If the existing BrokerSender's send loop has exited, replaces it with a fresh one.
    /// </summary>
    private BrokerSender GetOrCreateBrokerSender(int brokerId)
    {
        var sender = GetExistingOrCreateBrokerSender(brokerId);

        if (sender.IsAlive)
            return sender;

        // Send loop exited — replace with a fresh BrokerSender.
        // This handles transient connection errors that killed the send loop.
        LogBrokerSenderReplaced(brokerId);
        var replacement = CreateBrokerSender(brokerId);
        if (_brokerSenders.TryUpdate(brokerId, replacement, sender))
        {
            // Dispose old sender asynchronously (its finally block already cleaned up).
            _ = sender.DisposeAsync().AsTask().ContinueWith(static (t, _) =>
            {
                // Observe any disposal exceptions to prevent UnobservedTaskException
                _ = t.Exception;
            }, null, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return replacement;
        }

        // Another thread replaced it concurrently — dispose ours, use theirs
        _ = replacement.DisposeAsync();
        return GetExistingOrCreateBrokerSender(brokerId);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BrokerSender GetExistingOrCreateBrokerSender(int brokerId)
    {
        if (_brokerSenders.TryGetValue(brokerId, out var sender))
        {
            return sender;
        }

        return _brokerSenders.GetOrAdd(
            brokerId,
            static (id, producer) => producer.CreateBrokerSender(id),
            this);
    }

    private BrokerSender CreateBrokerSender(int brokerId)
    {
        // Epoch bump recovery is only for non-transactional idempotent producers.
        // Transactional producers manage epochs via InitTransactionsAsync.
        // Non-idempotent producers don't have producer IDs or epochs at all.
        var useEpochRecovery = _options.TransactionalId is null && _options.EnableIdempotence;

        return new BrokerSender(
            brokerId,
            _connectionPool,
            _metadataManager,
            _accumulator,
            _options,
            _compressionCodecs,
            _inflightTracker,
            () => _produceApiVersion,
            version => Interlocked.CompareExchange(ref _produceApiVersion, version, -1),
            () => _accumulator.IsTransactional,
            TryEnsurePartitionsInTransaction,
            bumpEpoch: useEpochRecovery ? BumpEpochLocally : null,
            getCurrentEpoch: useEpochRecovery ? () => _producerEpoch : null,
            RerouteBatchToCurrentLeader,
            _interceptors is not null ? InvokeOnAcknowledgementForBatch : null,
            _logger,
            canPhysicallyShrinkConnections: _ownsInfrastructure,
            onBrokerThrottle: _options.EnableDeliveryDiagnostics
                ? _telemetryMetricCollector.RecordBrokerThrottle
                : null,
            unackedBudget: _accumulator.GetBrokerUnackedBudget(brokerId),
            usesTransactionV2: () => _currentTransactionUsesTV2);
    }

    /// <summary>
    /// Resolves the current leader broker id for the unacked-byte admission budget.
    /// Returns -1 when the leader is not cached yet (the budget then skips the batch).
    /// </summary>
    private int ResolveLeaderIdForUnackedBudget(string topic, int partition)
        => _metadataManager.TryGetCachedPartitionLeader(topic, partition)?.NodeId ?? -1;

    /// <summary>
    /// Routes a batch to the current leader's BrokerSender.
    /// Used as a callback by BrokerSender when a retry discovers the leader has moved to a different broker.
    /// </summary>
    private void RerouteBatchToCurrentLeader(ReadyBatch batch, int expectedGeneration)
    {
        if (!batch.TryAcquireResourcePin(expectedGeneration))
            return;

        TopicPartition topicPartition;
        try
        {
            if (!batch.IsCurrentIncarnation(expectedGeneration))
                return;
            topicPartition = batch.TopicPartition;
        }
        finally
        {
            batch.ReleaseResourcePin();
        }

        try
        {
            if (!batch.IsCurrentIncarnation(expectedGeneration))
                return;

            // During disposal, don't create new BrokerSenders — fail the batch instead.
            // Without this guard, retries that discover leader changes create new senders
            // via GetOrCreateBrokerSender after the disposal loop has already snapshotted
            // _brokerSenders, leaving orphaned send loops that prevent process exit.
            if (Volatile.Read(ref _disposed) != 0)
            {
                LogRerouteBlockedByDisposal(topicPartition.Topic, topicPartition.Partition);
                FailAndCleanupBatch(batch, expectedGeneration,
                    new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>)));
                return;
            }

            var leader = _metadataManager.Metadata.GetPartitionLeader(
                topicPartition.Topic, topicPartition.Partition);

            if (leader is null)
            {
                FailAndCleanupBatch(batch, expectedGeneration,
                    new KafkaException(ErrorCode.LeaderNotAvailable,
                        $"No leader available for {topicPartition.Topic}-{topicPartition.Partition}"));
                return;
            }

            LogReroutedBatch(topicPartition.Topic, topicPartition.Partition, leader.NodeId);
            _accumulator.ReattributeUnackedBudget(batch, leader.NodeId);
            GetOrCreateBrokerSender(leader.NodeId).Enqueue(batch, expectedGeneration);
        }
        catch (Exception ex)
        {
            FailAndCleanupBatch(batch, expectedGeneration, ex);
        }
    }

    /// <summary>
    /// Queues every new partition in a coalesced broker pass for one background TV1
    /// AddPartitionsToTxn request. Returns false while any requested partition is pending.
    /// </summary>
    internal TransactionPartitionEnrollmentResult TryEnsurePartitionsInTransaction(
        ReadyBatch[] batches,
        int count,
        Action<Exception?> enrollmentCompleted,
        HashSet<TopicPartition> enrollmentPendingPartitions,
        HashSet<TopicPartition> enrollmentFailedPartitions)
    {
        var startEnrollment = false;
        long enrollmentGeneration = 0;

        lock (_partitionsInTransactionLock)
        {
            var usesImplicitEnrollment = _currentTransactionUsesTV2;
            if (usesImplicitEnrollment)
            {
                for (var i = 0; i < count; i++)
                    _partitionsInTransaction.Add(batches[i].TopicPartition);
                return TransactionPartitionEnrollmentResult.Enrolled;
            }

            Exception? enrollmentError = null;
            for (var i = 0; i < count; i++)
            {
                var topicPartition = batches[i].TopicPartition;
                if (!_partitionEnrollmentErrors.TryGetValue(topicPartition, out var partitionError))
                    continue;

                enrollmentFailedPartitions.Add(topicPartition);
                enrollmentError ??= partitionError;
            }

            if (enrollmentError is not null)
                return TransactionPartitionEnrollmentResult.Failed(enrollmentError);

            var allEnrolled = true;
            for (var i = 0; i < count; i++)
            {
                var topicPartition = batches[i].TopicPartition;
                if (_partitionsInTransaction.Contains(topicPartition))
                    continue;

                allEnrolled = false;
                enrollmentPendingPartitions.Add(topicPartition);
                if (!_transactionPartitionsBeingEnrolled.Contains(topicPartition))
                    _pendingTransactionPartitions.Add(topicPartition);
            }

            if (allEnrolled)
                return TransactionPartitionEnrollmentResult.Enrolled;

            _partitionEnrollmentWaiters.Add(enrollmentCompleted);
            if (!_partitionEnrollmentActive)
            {
                _partitionEnrollmentActive = true;
                startEnrollment = true;
                enrollmentGeneration = _partitionEnrollmentGeneration;
            }
        }

        if (startEnrollment)
            TrackPartitionEnrollmentTask(EnrollPendingTransactionPartitionsAsync(enrollmentGeneration));

        return TransactionPartitionEnrollmentResult.Pending;
    }

    private async Task EnrollPendingTransactionPartitionsAsync(long enrollmentGeneration)
    {
        while (true)
        {
            TopicPartition[] partitions;
            lock (_partitionsInTransactionLock)
            {
                if (enrollmentGeneration != _partitionEnrollmentGeneration)
                    return;

                if (_pendingTransactionPartitions.Count == 0)
                {
                    _partitionEnrollmentActive = false;
                    return;
                }

                partitions = [.. _pendingTransactionPartitions];
                _pendingTransactionPartitions.Clear();
                foreach (var partition in partitions)
                    _transactionPartitionsBeingEnrolled.Add(partition);
            }

            try
            {
                await EnrollTransactionPartitionsWithRetryAsync(partitions, _senderCts.Token)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Action<Exception?>[] waiters;
                lock (_partitionsInTransactionLock)
                {
                    if (enrollmentGeneration != _partitionEnrollmentGeneration)
                        return;

                    foreach (var partition in partitions)
                    {
                        _transactionPartitionsBeingEnrolled.Remove(partition);
                        _partitionEnrollmentErrors[partition] = exception;
                    }
                    waiters = [.. _partitionEnrollmentWaiters];
                    _partitionEnrollmentWaiters.Clear();
                }

                NotifyPartitionEnrollmentWaiters(waiters, null);
                continue;
            }

            Action<Exception?>[] completedWaiters;
            lock (_partitionsInTransactionLock)
            {
                if (enrollmentGeneration != _partitionEnrollmentGeneration)
                    return;

                foreach (var partition in partitions)
                {
                    _transactionPartitionsBeingEnrolled.Remove(partition);
                    _partitionsInTransaction.Add(partition);
                }

                completedWaiters = [.. _partitionEnrollmentWaiters];
                _partitionEnrollmentWaiters.Clear();
            }

            NotifyPartitionEnrollmentWaiters(completedWaiters, null);
        }
    }

    private static void NotifyPartitionEnrollmentWaiters(
        Action<Exception?>[] waiters,
        Exception? error)
    {
        foreach (var waiter in waiters)
            waiter(error);
    }

    private static void NotifyPartitionEnrollmentResetWaiters(Action<Exception?>[] waiters)
    {
        if (waiters.Length == 0)
            return;

        NotifyPartitionEnrollmentWaiters(
            waiters,
            new TransactionException("Transaction partition enrollment was reset before completion."));
    }

    private async ValueTask EnrollTransactionPartitionsWithRetryAsync(
        IReadOnlyList<TopicPartition> partitions,
        CancellationToken cancellationToken)
    {
        const int maxRetries = 5;
        var retryDeadline = Stopwatch.GetTimestamp() + _options.DeliveryTimeoutTicks;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                await _addPartitionsToTransaction(partitions, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception exception) when (attempt < maxRetries - 1
                && IsRetriablePartitionEnrollmentException(exception)
                && Stopwatch.GetTimestamp() < retryDeadline)
            {
                var retryDelayMs = CalculateRequestRetryBackoff(attempt);
                LogAddPartitionsToTxnTransportRetry(attempt + 1, retryDelayMs);
                var remainingTicks = retryDeadline - Stopwatch.GetTimestamp();
                var remainingMs = Math.Max(1, remainingTicks * 1000d / Stopwatch.Frequency);
                await Task.Delay((int)Math.Min(retryDelayMs, remainingMs), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static bool IsRetriablePartitionEnrollmentException(Exception exception) =>
        exception is IOException or System.Net.Sockets.SocketException or TimeoutException
        || exception is KafkaException { IsRetriable: true };

    private Action<Exception?>[] ResetPartitionEnrollmentState()
    {
        lock (_partitionsInTransactionLock)
        {
            _partitionEnrollmentGeneration++;
            _partitionsInTransaction.Clear();
            _pendingTransactionPartitions.Clear();
            _transactionPartitionsBeingEnrolled.Clear();
            Action<Exception?>[] waiters = [.. _partitionEnrollmentWaiters];
            _partitionEnrollmentWaiters.Clear();
            _partitionEnrollmentErrors.Clear();
            _partitionEnrollmentActive = false;
            return waiters;
        }
    }

    private void TrackPartitionEnrollmentTask(Task task)
    {
        _partitionEnrollmentTasks.TryAdd(task, 0);
        if (task.IsCompleted)
        {
            _partitionEnrollmentTasks.TryRemove(task, out _);
            return;
        }

        _ = task.ContinueWith(static (completedTask, state) =>
        {
            var tasks = (ConcurrentDictionary<Task, byte>)state!;
            tasks.TryRemove(completedTask, out _);
        }, _partitionEnrollmentTasks,
        CancellationToken.None,
        TaskContinuationOptions.ExecuteSynchronously,
        TaskScheduler.Default);
    }

    private async Task LingerLoopAsync(CancellationToken cancellationToken)
    {
        // Orphan sweep interval: check for expired in-flight batches every ~5 seconds.
        // This catches batches whose references were lost from BrokerSender data structures
        // (root cause under investigation) — without this sweep, ProduceAsync hangs indefinitely.
        var orphanSweepIntervalTicks = (long)(5.0 * Stopwatch.Frequency);
        var lastOrphanSweepTicks = Stopwatch.GetTimestamp();

        _accumulator.RegisterLingerWakeupShutdownToken(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var now = Stopwatch.GetTimestamp();
                var ticksUntilOrphanSweep = orphanSweepIntervalTicks - (now - lastOrphanSweepTicks);
                var orphanWaitMs = ticksUntilOrphanSweep <= 0
                    ? 0
                    : Math.Max(1, (int)Math.Ceiling(ticksUntilOrphanSweep * 1000.0 / Stopwatch.Frequency));

                if (_accumulator.HasPendingLingerBatches)
                {
                    // Deadline wait: sleep until the earliest pending batch can reach its
                    // linger deadline instead of polling a fixed 1 ms tick (~1,000 threadpool
                    // wakes/s under any active workload). A batch with an earlier deadline or
                    // a first awaited produce signals the wakeup so the wait re-arms.
                    var lingerWaitMs = _accumulator.GetMillisUntilEarliestLingerDeadline(orphanWaitMs);
                    BeforeActiveLingerWaitForTest?.Invoke();
                    await _accumulator.WaitForLingerWakeupAsync(lingerWaitMs).ConfigureAwait(false);
                }
                else
                {
                    await _accumulator.WaitForLingerWakeupAsync(orphanWaitMs).ConfigureAwait(false);
                }

                await _accumulator.ExpireLingerAsync(cancellationToken).ConfigureAwait(false);

                // Periodic orphan sweep: fail in-flight batches that exceeded delivery timeout.
                now = Stopwatch.GetTimestamp();
                if (now - lastOrphanSweepTicks >= orphanSweepIntervalTicks)
                {
                    lastOrphanSweepTicks = now;
                    _accumulator.SweepExpiredInFlightBatches();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogLingerLoopError(ex);
            }
        }
    }

    internal Action? BeforeActiveLingerWaitForTest;

    /// <inheritdoc />
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));

        // Fast path: already initialized (volatile read provides acquire semantics)
        if (_initialized)
            return;

        await SemaphoreHelper.AcquireOrThrowDisposedAsync(_initLock, nameof(KafkaProducer<TKey, TValue>), cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring lock
            if (_initialized)
                return;

            // The metadata manager time-bounds initialization itself (InitTimeoutMs, mapped
            // from MaxBlockMs by the builder) and throws KafkaTimeoutException on expiry.
            await _metadataManager.InitializeAsync(cancellationToken).ConfigureAwait(false);

            if (_options.EnableIdempotence && _options.TransactionalId is null && !_idempotentInitialized)
            {
                await InitIdempotentProducerAsync(cancellationToken).ConfigureAwait(false);
            }

            await _telemetryManager.StartAsync(cancellationToken).ConfigureAwait(false);

            _initialized = true;
        }
        finally
        {
            SemaphoreHelper.ReleaseSafely(_initLock);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfProduceCannotStart()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));

        ThrowIfNotInitialized();

        // KIP-1050 fail-fast: once a transaction is broken, reject new produces so the caller must
        // abort (abortable) or close the producer (fatal) before continuing. Single volatile read
        // on the hot path; only transactional producers ever reach the error states.
        if (_options.TransactionalId is not null)
        {
            ThrowIfTransactionCannotProduce();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowIfTransactionCannotProduce()
    {
        var txnState = _transactionState;
        if (txnState == TransactionState.AbortableError)
        {
            throw new AbortableTransactionException(_lastTransactionError,
                "Cannot produce: the current transaction has an abortable error and must be aborted.")
            {
                TransactionalId = _options.TransactionalId
            };
        }

        if (txnState == TransactionState.FatalError)
        {
            ThrowFatalTransactionError("Cannot produce");
        }

        if (txnState == TransactionState.PreparedTransaction)
        {
            throw new TransactionException(ErrorCode.InvalidTxnState,
                "Cannot produce: the current transaction is prepared. Only commit, abort, or complete are permitted.")
            {
                TransactionalId = _options.TransactionalId
            };
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ThrowIfFatalTransactionError(string operation)
    {
        if (_transactionState == TransactionState.FatalError)
            ThrowFatalTransactionError(operation);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowFatalTransactionError(string operation)
    {
        throw new FatalTransactionException(_lastTransactionError,
            $"{operation}: the producer is in a fatal error state and must be closed.")
        {
            TransactionalId = _options.TransactionalId
        };
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowNotInitialized()
    {
        throw new InvalidOperationException(
            "Call InitializeAsync() or use BuildAsync() before producing messages.");
    }

    /// <summary>
    /// Async slow path for fire-and-forget produce when topic metadata is not cached.
    /// Fetches metadata asynchronously, serializes, and appends to the accumulator.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private async ValueTask FireAsyncSlow(
        ProducerMessage<TKey, TValue> message,
        Activity? activity)
    {
        try
        {
            using var timeoutCts = new CancellationTokenSource(_options.MaxBlockMs);

            TopicInfo? topicInfo;
            try
            {
                topicInfo = await _metadataManager.GetTopicMetadataAsync(message.Topic, timeoutCts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw new KafkaTimeoutException(
                    $"Failed to fetch metadata for topic '{message.Topic}' within max.block.ms ({_options.MaxBlockMs}ms).");
            }

            if (topicInfo is null || topicInfo.PartitionCount == 0)
            {
                var ex = NoUsablePartitionsException(message.Topic, topicInfo);
                LogFireAndForgetMetadataFetchFailed(ex, message.Topic);
                return;
            }

            UpdateCachedTopicInfo(message.Topic, topicInfo);

            var appendResult = await SerializeAndAppendFromSpansAsync(message.Topic, message.Key, message.Value, message.Headers, message.Partition, message.Timestamp, topicInfo, null).ConfigureAwait(false);

            if (!appendResult)
                throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (KafkaTimeoutException ex)
        {
            if (activity is not null) Diagnostics.DekafDiagnostics.RecordException(activity, ex);
            throw;
        }
        catch (Exception ex)
        {
            LogFireAndForgetProduceFailed(ex, message.Topic);
        }
        finally
        {
            activity?.Dispose();
        }
    }

    /// <summary>
    /// Builds the exception to throw when a topic has no usable partition metadata. When the cause
    /// is an authorization error, returns an <see cref="AuthorizationException"/> (via
    /// <see cref="KafkaException.FromErrorCode"/>) so callers can catch it specifically; otherwise a
    /// generic <see cref="ProduceException"/>.
    /// </summary>
    private static Exception NoUsablePartitionsException(string topic, TopicInfo? topicInfo)
    {
        if (topicInfo is not null &&
            topicInfo.ErrorCode is ErrorCode.TopicAuthorizationFailed or ErrorCode.ClusterAuthorizationFailed)
        {
            return KafkaException.FromErrorCode(topicInfo.ErrorCode,
                $"Cannot produce to topic '{topic}': {topicInfo.ErrorCode}");
        }

        return new ProduceException($"Topic '{topic}' not found or has no partitions") { Topic = topic };
    }

    /// <summary>
    /// Async slow path for fire-and-forget produce with delivery callback when metadata is not cached.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private async ValueTask ProduceAsyncWithCallbackSlow(
        ProducerMessage<TKey, TValue> message,
        Action<RecordMetadata, Exception?> deliveryHandler)
    {
        try
        {
            using var timeoutCts = new CancellationTokenSource(_options.MaxBlockMs);

            TopicInfo? topicInfo;
            try
            {
                topicInfo = await _metadataManager.GetTopicMetadataAsync(message.Topic, timeoutCts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw new KafkaTimeoutException(
                    $"Failed to fetch metadata for topic '{message.Topic}' within max.block.ms ({_options.MaxBlockMs}ms).");
            }

            if (topicInfo is null || topicInfo.PartitionCount == 0)
            {
                throw NoUsablePartitionsException(message.Topic, topicInfo);
            }

            UpdateCachedTopicInfo(message.Topic, topicInfo);

            var appendResult = await SerializeAndAppendFromSpansAsync(message.Topic, message.Key, message.Value, message.Headers, message.Partition, message.Timestamp, topicInfo, deliveryHandler).ConfigureAwait(false);

            if (!appendResult)
                throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));
        }
        catch (Exception ex)
        {
            // Deliver exception to callback
            try { deliveryHandler(default, ex); } catch (Exception cbEx) { LogBatchCleanupStepFailed(cbEx); }
        }
    }

    /// <summary>
    /// Serializes key/value to thread-local buffers and appends to the accumulator.
    /// Non-async to allow ReadOnlySpan usage — returns ValueTask for hot/cold path split.
    /// </summary>
    private ValueTask<bool> SerializeAndAppendFromSpansAsync(
        string topic, TKey? key, TValue value, Headers? headers, int? partition, DateTimeOffset? timestamp,
        TopicInfo topicInfo,
        Action<RecordMetadata, Exception?>? callback)
    {
        var cache = GetOrCreateCache();
        var keyIsNull = key is null;
        int keyLength = 0;

        if (!keyIsNull)
        {
            var keyWriter = new ReusableBufferWriter(ref cache.KeySerializationBuffer, DefaultKeyBufferSize);
            cache.SerializationContext.Topic = topic;
            cache.SerializationContext.Component = SerializationComponent.Key;
            cache.SerializationContext.Headers = headers;
            _keySerializer.Serialize(key!, ref keyWriter, cache.SerializationContext);
            keyWriter.UpdateBufferRef(ref cache.KeySerializationBuffer);
            keyLength = keyWriter.WrittenCount;
        }

        var valueIsNull = value is null;
        int valueLength = 0;

        if (!valueIsNull)
        {
            var valueWriter = new ReusableBufferWriter(ref cache.ValueSerializationBuffer, DefaultValueBufferSize);
            cache.SerializationContext.Topic = topic;
            cache.SerializationContext.Component = SerializationComponent.Value;
            cache.SerializationContext.Headers = headers;
            _valueSerializer.Serialize(value!, ref valueWriter, cache.SerializationContext);
            valueWriter.UpdateBufferRef(ref cache.ValueSerializationBuffer);
            valueLength = valueWriter.WrittenCount;
        }

        var keySpan = keyIsNull ? ReadOnlySpan<byte>.Empty : cache.KeySerializationBuffer.AsSpan(0, keyLength);
        var resolvedPartition = partition
            ?? _partitioner.Partition(topic, keySpan, keyIsNull, topicInfo.PartitionCount);
        var batchCompletionPartitionCount = GetUniformStickyPartitionCount(
            partition, keySpan, keyIsNull, topicInfo.PartitionCount);

        var timestampMs = timestamp?.ToUnixTimeMilliseconds() ?? GetFastTimestampMs();

        Header[]? pooledHeaderArray = null;
        var headerCount = 0;
        if (headers is not null && headers.Count > 0)
        {
            RentAndFillHeaders(headers, out pooledHeaderArray, out headerCount);
        }

        // CancellationToken.None is intentional: fire-and-forget callers have no per-call token.
        // Backpressure is bounded by MaxBlockMs inside ReserveMemoryAsync, which enforces its
        // own deadline independently of the cancellation token.
        return _accumulator.AppendFromSpansAsync(
            topic, resolvedPartition, timestampMs,
            keySpan, keyIsNull,
            valueIsNull ? ReadOnlySpan<byte>.Empty : cache.ValueSerializationBuffer.AsSpan(0, valueLength),
            valueIsNull,
            pooledHeaderArray, headerCount, callback, CancellationToken.None, batchCompletionPartitionCount);
    }

    /// <summary>
    /// Awaits an in-flight append and disposes the activity on completion.
    /// </summary>
    private async ValueTask FinishProduceAsync(ValueTask<bool> appendResult, Activity? activity, string topic)
    {
        try
        {
            var result = await appendResult.ConfigureAwait(false);
            if (!result)
                throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (KafkaTimeoutException ex)
        {
            if (activity is not null) Diagnostics.DekafDiagnostics.RecordException(activity, ex);
            throw;
        }
        catch (Exception ex) when (ex is not ObjectDisposedException)
        {
            LogFireAndForgetProduceFailed(ex, topic);
        }
        finally
        {
            activity?.Dispose();
        }
    }

    /// <summary>
    /// Cold-path finish for the callback fire-and-forget overload.
    /// All exceptions are delivered to the callback rather than thrown.
    /// </summary>
    private async ValueTask FinishProduceAsyncWithCallback(
        ValueTask<bool> appendResult,
        Action<RecordMetadata, Exception?> deliveryHandler)
    {
        try
        {
            var result = await appendResult.ConfigureAwait(false);
            if (!result)
                throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));
        }
        catch (Exception ex)
        {
            try { deliveryHandler(default, ex); }
            catch (Exception cbEx) { LogBatchCleanupStepFailed(cbEx); }
        }
    }

    /// <summary>
    /// Initializes the idempotent producer by obtaining a producer ID from any broker.
    /// This enables sequence number assignment for duplicate detection without full transactions.
    /// </summary>
    private async ValueTask InitIdempotentProducerAsync(CancellationToken cancellationToken)
    {
        // Double-check under lock to prevent concurrent initialization
        await SemaphoreHelper.AcquireOrThrowDisposedAsync(_transactionLock, nameof(KafkaProducer<TKey, TValue>), cancellationToken).ConfigureAwait(false);
        try
        {
            if (_idempotentInitialized)
            {
                return;
            }

            // Retry with backoff for retriable errors (e.g. CoordinatorLoadInProgress during broker startup)
            var consecutiveFailures = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // For non-transactional idempotent producers, send to any broker (no coordinator needed)
                var brokers = _metadataManager.Metadata.GetBrokers();
                if (brokers.Count == 0)
                {
                    throw new InvalidOperationException("No brokers available for idempotent producer initialization");
                }

                var request = new InitProducerIdRequest
                {
                    TransactionalId = null,
                    TransactionTimeoutMs = -1,
                    ProducerId = _producerId,
                    ProducerEpoch = _producerEpoch
                };

                var response = await SendWithConnectionLeaseAsync<InitProducerIdRequest, InitProducerIdResponse>(
                        brokers[0].NodeId,
                        request,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (response.ErrorCode == ErrorCode.None)
                {
                    _producerId = response.ProducerId;
                    _producerEpoch = response.ProducerEpoch;

                    // Wire the producer ID/epoch into the accumulator for RecordBatch headers
                    _accumulator.ProducerId = _producerId;
                    _accumulator.ProducerEpoch = _producerEpoch;

                    _idempotentInitialized = true;

                    LogIdempotentProducerInitialized(_producerId, _producerEpoch);
                    return;
                }

                if (!response.ErrorCode.IsRetriable())
                {
                    throw KafkaException.FromErrorCode(response.ErrorCode,
                        $"Failed to initialize idempotent producer: {response.ErrorCode}");
                }

                var retryDelayMs = ExponentialRetryBackoff.CalculateDelayMilliseconds(
                    _options.RetryBackoffMs,
                    _options.RetryBackoffMaxMs,
                    ++consecutiveFailures);
                LogInitProducerIdRetriable(response.ErrorCode, retryDelayMs);

                await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            SemaphoreHelper.ReleaseSafely(_transactionLock);
        }
    }

    /// <summary>
    /// Client-side epoch bump for idempotent (non-transactional) producers (Java-style, KIP-360).
    /// Increments the epoch locally without sending InitProducerIdRequest. The broker accepts
    /// epoch+1 with sequence=0 as a valid fresh start for the affected partition.
    /// Only resets sequences for partitions that triggered the error (OOSN, InvalidProducerEpoch).
    /// Unaffected partitions keep their sequence counters — the broker carries forward
    /// per-partition sequence state across epoch bumps.
    /// </summary>
    internal (long ProducerId, short ProducerEpoch) BumpEpochLocally(
        short expectedEpoch, IReadOnlyCollection<TopicPartition> partitionsToReset)
    {
        // Multiple BrokerSenders can call this concurrently when different brokers
        // return OutOfOrderSequenceNumber simultaneously. Use lock to make the
        // check-and-increment atomic and prevent double epoch bumps.
        lock (_epochBumpLock)
        {
            // Already bumped by another BrokerSender — return current state
            if (_producerEpoch != expectedEpoch)
            {
                LogEpochAlreadyBumped(expectedEpoch, _producerEpoch);
                return (_producerId, _producerEpoch);
            }

            if (_producerEpoch == short.MaxValue)
            {
                throw new KafkaException(ErrorCode.UnknownServerError,
                    "Producer epoch overflow — requires producer restart");
            }

            _producerEpoch = (short)(_producerEpoch + 1);
            _accumulator.ProducerEpoch = _producerEpoch;

            // Per-partition reset: only affected partitions restart at seq=0.
            // Unaffected partitions continue with current sequences under new epoch.
            _accumulator.ResetSequencesForPartitions(partitionsToReset);

            LogProducerEpochBumped(_producerId, _producerEpoch);
            return (_producerId, _producerEpoch);
        } // lock (_epochBumpLock)
    }

    /// <summary>
    /// Server-side epoch bump via InitProducerIdRequest. Used for transactional producers
    /// and as fallback when client-side bump is not possible (e.g., epoch overflow).
    /// The broker returns the same PID with an incremented epoch. Resets all partition
    /// sequence numbers to 0 so subsequent batches use the new epoch.
    /// Serialized by _transactionLock. The expectedEpoch parameter prevents redundant bumps.
    /// </summary>
    internal async ValueTask<(long ProducerId, short ProducerEpoch)> BumpEpochAsync(
        short expectedEpoch, CancellationToken cancellationToken)
    {
        await SemaphoreHelper.AcquireOrThrowDisposedAsync(_transactionLock, nameof(KafkaProducer<TKey, TValue>), cancellationToken).ConfigureAwait(false);
        try
        {
            // Another thread already bumped — return current state
            if (_producerEpoch != expectedEpoch)
            {
                LogEpochAlreadyBumped(expectedEpoch, _producerEpoch);
                return (_producerId, _producerEpoch);
            }

            var consecutiveFailures = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var brokers = _metadataManager.Metadata.GetBrokers();
                if (brokers.Count == 0)
                {
                    throw new InvalidOperationException("No brokers available for epoch bump");
                }

                var request = new InitProducerIdRequest
                {
                    TransactionalId = null,
                    TransactionTimeoutMs = -1,
                    ProducerId = _producerId,
                    ProducerEpoch = _producerEpoch
                };

                var response = await SendWithConnectionLeaseAsync<InitProducerIdRequest, InitProducerIdResponse>(
                        brokers[0].NodeId,
                        request,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (response.ErrorCode == ErrorCode.None)
                {
                    _producerId = response.ProducerId;
                    _producerEpoch = response.ProducerEpoch;

                    _accumulator.ProducerId = _producerId;
                    _accumulator.ProducerEpoch = _producerEpoch;
                    _accumulator.ResetSequenceNumbers();

                    LogProducerEpochBumped(_producerId, _producerEpoch);
                    return (_producerId, _producerEpoch);
                }

                if (!response.ErrorCode.IsRetriable())
                {
                    throw new KafkaException(response.ErrorCode,
                        $"Failed to bump producer epoch: {response.ErrorCode}");
                }

                var retryDelayMs = ExponentialRetryBackoff.CalculateDelayMilliseconds(
                    _options.RetryBackoffMs,
                    _options.RetryBackoffMaxMs,
                    ++consecutiveFailures);
                LogBumpEpochRetriable(response.ErrorCode, retryDelayMs);

                await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            SemaphoreHelper.ReleaseSafely(_transactionLock);
        }
    }

    /// <summary>
    /// Serializes the key using a thread-local reusable buffer.
    /// Avoids PooledBufferWriter creation per message by reusing the same buffer.
    /// Data is copied to a right-sized pooled buffer for the batch.
    /// </summary>
    private PooledMemory SerializeKeyToPooled(TKey key, string topic, Headers? headers)
    {
        var cache = GetOrCreateCache();

        // Use thread-local buffer to avoid per-message allocation
        var writer = new ReusableBufferWriter(ref cache.KeySerializationBuffer, DefaultKeyBufferSize);

        // Reuse thread-local context by updating fields (zero-allocation)
        cache.SerializationContext.Topic = topic;
        cache.SerializationContext.Component = SerializationComponent.Key;
        cache.SerializationContext.Headers = headers;
        _keySerializer.Serialize(key, ref writer, cache.SerializationContext);

        // Update buffer ref in case it grew during serialization
        writer.UpdateBufferRef(ref cache.KeySerializationBuffer);

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
        var cache = GetOrCreateCache();

        // Use thread-local buffer to avoid per-message allocation
        var writer = new ReusableBufferWriter(ref cache.ValueSerializationBuffer, DefaultValueBufferSize);

        // Reuse thread-local context by updating fields (zero-allocation)
        cache.SerializationContext.Topic = topic;
        cache.SerializationContext.Component = SerializationComponent.Value;
        cache.SerializationContext.Headers = headers;
        _valueSerializer.Serialize(value, ref writer, cache.SerializationContext);

        // Update buffer ref in case it grew during serialization
        writer.UpdateBufferRef(ref cache.ValueSerializationBuffer);

        // Copy to right-sized pooled buffer for batch storage
        return writer.ToPooledMemory();
    }

    /// <summary>
    /// Produce path used when an <see cref="IAsyncSerializer{T}"/> is configured (issue #2309).
    /// Reuses <see cref="ProduceInternalAsync"/> (which awaits async serializers per component)
    /// and ProduceAsyncSlow's completion/instrumentation handling.
    /// </summary>
    private async ValueTask<RecordMetadata> ProduceWithAsyncSerializationAsync(
        ProducerMessage<TKey, TValue> message,
        Activity? activity,
        ProduceContinuationMode continuationMode,
        CancellationToken cancellationToken)
    {
        // See ProduceAfterPrepare: instrumented awaits interpose a state machine that must not
        // run inline on the broker ack thread.
        var metricsEnabled = ProducerMetricsEnabled();
        if (continuationMode == ProduceContinuationMode.InlineWhenDirect && (activity is not null || metricsEnabled))
            continuationMode = ProduceContinuationMode.Async;
        var runContinuationsAsynchronously = continuationMode == ProduceContinuationMode.Async;

        var completion = RentCompletion(runContinuationsAsynchronously);
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

        return await AwaitProduceCompletionAsync(completion, activity, metricsEnabled, message.Topic, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Shared cold-path tail for awaiting a produce completion with the correct instrumentation
    /// wrapper. Branch order is load-bearing: activity, then metrics, then bare cancellation.
    /// </summary>
    private ValueTask<RecordMetadata> AwaitProduceCompletionAsync(
        PooledValueTaskSource<RecordMetadata> completion,
        Activity? activity,
        bool metricsEnabled,
        string topic,
        CancellationToken cancellationToken)
    {
        if (activity is not null)
        {
            return AwaitWithActivity(completion, activity, topic, cancellationToken);
        }
        if (metricsEnabled)
        {
            return AwaitWithMetrics(completion, topic, cancellationToken);
        }
        if (cancellationToken.CanBeCanceled)
        {
            return AwaitWithCancellation(completion, cancellationToken);
        }
        return completion.Task;
    }

    /// <summary>
    /// Runs an async serializer against a pooled, heap-safe buffer writer and detaches the result
    /// as <see cref="PooledMemory"/> (returned to the pool when the batch completes).
    /// </summary>
    private static async ValueTask<PooledMemory> SerializeToPooledAsync<T>(
        IAsyncSerializer<T> serializer,
        T value,
        string topic,
        SerializationComponent component,
        Headers? headers,
        CancellationToken cancellationToken)
    {
        var writer = AsyncSerializationBufferWriter.Rent();
        try
        {
            var context = new SerializationContext
            {
                Topic = topic,
                Component = component,
                Headers = headers
            };
            await serializer.SerializeAsync(value, writer, context, cancellationToken).ConfigureAwait(false);
            return writer.DetachWrittenMemory();
        }
        finally
        {
            AsyncSerializationBufferWriter.Return(writer);
        }
    }

    /// <summary>
    /// Fire-and-forget path with async serializers. Serializes on the async path, then appends
    /// through the accumulator's span/callback machinery — the same delivery-callback mechanism
    /// the synchronous fire-and-forget path uses, so handler dispatch, backpressure, and
    /// delivery-error accounting stay unified. The returned ValueTask completes when the message
    /// is appended (parity with FireAsync's append semantics).
    /// </summary>
    private async ValueTask FireWithAsyncSerializationAsync(
        ProducerMessage<TKey, TValue> message,
        Activity? activity,
        Action<RecordMetadata, Exception?>? deliveryHandler)
    {
        try
        {
            // Metadata: thread-local cache, then manager cache, then bounded fetch
            // (mirrors FireAsyncSlow).
            TopicInfo? topicInfo;
            if (!TryGetCachedTopicInfo(message.Topic, out topicInfo) &&
                !_metadataManager.TryGetCachedTopicMetadata(message.Topic, out topicInfo))
            {
                using var timeoutCts = new CancellationTokenSource(_options.MaxBlockMs);
                try
                {
                    topicInfo = await _metadataManager.GetTopicMetadataAsync(message.Topic, timeoutCts.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw new KafkaTimeoutException(
                        $"Failed to fetch metadata for topic '{message.Topic}' within max.block.ms ({_options.MaxBlockMs}ms).");
                }
            }

            if (topicInfo is null || topicInfo.PartitionCount == 0)
            {
                var metadataException = NoUsablePartitionsException(message.Topic, topicInfo);
                if (deliveryHandler is null)
                {
                    // Parity with FireAsyncSlow: log and drop rather than throw.
                    LogFireAndForgetMetadataFetchFailed(metadataException, message.Topic);
                    return;
                }

                throw metadataException;
            }

            UpdateCachedTopicInfo(message.Topic, topicInfo);

            var keyIsNull = message.Key is null;
            var valueIsNull = message.Value is null;
            var key = PooledMemory.Null;
            var value = PooledMemory.Null;
            try
            {
                if (!keyIsNull)
                {
                    key = _asyncKeySerializer is not null
                        ? await SerializeToPooledAsync(
                            _asyncKeySerializer, message.Key!, message.Topic,
                            SerializationComponent.Key, message.Headers, CancellationToken.None).ConfigureAwait(false)
                        : SerializeKeyToPooled(message.Key!, message.Topic, message.Headers);
                }

                if (!valueIsNull)
                {
                    value = _asyncValueSerializer is not null
                        ? await SerializeToPooledAsync(
                            _asyncValueSerializer, message.Value!, message.Topic,
                            SerializationComponent.Value, message.Headers, CancellationToken.None).ConfigureAwait(false)
                        : SerializeValueToPooled(message.Value!, message.Topic, message.Headers);
                }

                var appendResult = await AppendSerializedToAccumulatorAsync(
                    message.Topic, key, keyIsNull, value, valueIsNull,
                    message.Headers, message.Partition, message.Timestamp,
                    topicInfo, deliveryHandler).ConfigureAwait(false);

                if (!appendResult)
                    throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));
            }
            finally
            {
                // AppendFromSpansAsync copies the record data before any await, so the pooled
                // serialization arrays are no longer referenced once the append completes.
                key.Return();
                value.Return();
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (KafkaTimeoutException ex) when (deliveryHandler is null)
        {
            // Metadata/BufferMemory backpressure timeout must propagate (parity with FireAsync).
            if (activity is not null) Diagnostics.DekafDiagnostics.RecordException(activity, ex);
            throw;
        }
        catch (Exception ex)
        {
            if (activity is not null) Diagnostics.DekafDiagnostics.RecordException(activity, ex);
            if (deliveryHandler is not null)
            {
                // Pre-append failure: deliver to the callback (parity with ProduceAsyncWithCallbackSlow).
                try { deliveryHandler(default, ex); } catch (Exception cbEx) { LogBatchCleanupStepFailed(cbEx); }
            }
            else
            {
                LogFireAndForgetProduceFailed(ex, message.Topic);
            }
        }
        finally
        {
            activity?.Dispose();
        }
    }

    /// <summary>
    /// Appends already-serialized key/value bytes to the accumulator with an optional delivery
    /// callback. Non-async so the spans never enter an async frame; AppendFromSpansAsync copies
    /// the data before any await, so the caller may return the pooled arrays once the returned
    /// ValueTask completes.
    /// </summary>
    private ValueTask<bool> AppendSerializedToAccumulatorAsync(
        string topic,
        in PooledMemory key,
        bool keyIsNull,
        in PooledMemory value,
        bool valueIsNull,
        Headers? headers,
        int? explicitPartition,
        DateTimeOffset? timestamp,
        TopicInfo topicInfo,
        Action<RecordMetadata, Exception?>? callback)
    {
        var keySpan = key.Span;
        var partition = explicitPartition
            ?? _partitioner.Partition(topic, keySpan, keyIsNull, topicInfo.PartitionCount);
        var batchCompletionPartitionCount = GetUniformStickyPartitionCount(
            explicitPartition, keySpan, keyIsNull, topicInfo.PartitionCount);
        var timestampMs = timestamp?.ToUnixTimeMilliseconds() ?? GetFastTimestampMs();

        Header[]? pooledHeaderArray = null;
        var headerCount = 0;
        if (headers is not null && headers.Count > 0)
        {
            RentAndFillHeaders(headers, out pooledHeaderArray, out headerCount);
        }

        // CancellationToken.None is intentional: fire-and-forget callers have no per-call token.
        // Backpressure is bounded by MaxBlockMs inside ReserveMemoryAsync.
        return _accumulator.AppendFromSpansAsync(
            topic, partition, timestampMs,
            keySpan, keyIsNull,
            value.Span, valueIsNull,
            pooledHeaderArray, headerCount, callback, CancellationToken.None, batchCompletionPartitionCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PooledMemory CopySpanToPooledMemory(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
            return new PooledMemory(null, 0, isNull: false);

        var pooledArray = ProducerDataPool.BytePool.Rent(data.Length);
        data.CopyTo(pooledArray);
        return new PooledMemory(pooledArray, data.Length);
    }

    /// <summary>
    /// Gets a fast cached timestamp in milliseconds for fire-and-forget operations.
    /// Refreshes the cache approximately every millisecond to balance accuracy and performance.
    /// This is ~10x faster than DateTimeOffset.UtcNow for high-throughput scenarios.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long GetFastTimestampMs()
    {
        var cache = GetOrCreateCache();

        // Use a cheap monotonic counter to determine if we need to refresh.
        // TickCount64 increments every ~15.6ms on Windows, ~1ms on Linux, but checking
        // the difference is still much cheaper than calling DateTimeOffset.UtcNow.
        var currentTicks = Dekaf.MonotonicClock.GetMilliseconds();

        // Refresh if more than ~1ms has passed (or on first call when CachedTimestampTicks is 0)
        if (currentTicks - cache.CachedTimestampTicks > 1 || cache.CachedTimestampTicks == 0)
        {
            cache.CachedTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            cache.CachedTimestampTicks = currentTicks;
        }

        return cache.CachedTimestampMs;
    }

    /// <summary>
    /// Rents a Header array from the pool and fills it from the Headers collection.
    /// Always uses ArrayPool to avoid per-message heap allocations.
    /// Returns the raw array and count to avoid boxing a wrapper struct to IReadOnlyList.
    /// </summary>
    /// <remarks>
    /// Assumes headers is non-null and non-empty; callers must guard before invoking.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RentAndFillHeaders(Headers headers, out Header[] pooledArray, out int headerCount)
    {
        var count = headers.Count;
        headerCount = count;

        // Use dedicated pool to prevent TLS accumulation from cross-thread return
        // (rented on producer thread, returned on BrokerSender thread in ReadyBatch.Cleanup)
        var result = ProducerContainerPools.Headers.Rent(count);
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
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        DekafMetrics.UnregisterProducerState(_stateSource);

        if (_options.IsAutoTuned)
            _memoryBudget.UnregisterProducer(this);
        else
            _memoryBudget.ReleaseExplicit(_options.BufferMemory);

        var disposeStart = Stopwatch.GetTimestamp();
        LogProducerDisposing();
        _brokerCountDiscoveredRegistration.Dispose();

        // Time budget allocation within CloseTimeoutMs:
        //   40% → graceful flush (accumulator close + sender drain)
        //   ~48% → BrokerSender disposal (remaining minus cleanup reservation)
        //   ~12% → post-disposal cleanup (statistics, accumulator, pool, network)
        //
        // Previously the split was 50/50 and post-disposal cleanup had NO time budget,
        // so internal timeouts in the accumulator (10s) and connection pool (30s) could
        // cause DisposeAsync to far exceed CloseTimeoutMs.
        var hasTimeout = _options.CloseTimeoutMs > 0;
        var totalDeadline = hasTimeout
            ? DateTimeOffset.UtcNow.AddMilliseconds(_options.CloseTimeoutMs)
            : DateTimeOffset.MaxValue;
        var gracefulMs = hasTimeout ? _options.CloseTimeoutMs * 2 / 5 : 0;

        // Local helper: milliseconds remaining until totalDeadline, floored at zero.
        int RemainingMs() => Math.Max(0, (int)(totalDeadline - DateTimeOffset.UtcNow).TotalMilliseconds);
        using var shutdownCts = hasTimeout
            ? new CancellationTokenSource(TimeSpan.FromMilliseconds(gracefulMs))
            : new CancellationTokenSource();
        var gracefulShutdown = true;

        try
        {
            // 1. Flush accumulator and complete its channel - sender will process remaining batches
            await _accumulator.CloseAsync(shutdownCts.Token).ConfigureAwait(false);

            // 2. Wait for sender to drain remaining batches
            // CRITICAL: Don't cancel _senderCts yet - sender needs to process flushed batches
            // The sender exits naturally when Closed is set and all deques are drained
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
            var gracefulElapsed = Stopwatch.GetElapsedTime(disposeStart);
            var gracefulElapsedMs = gracefulElapsed.TotalMilliseconds;
            LogGracefulShutdownTimedOut(gracefulMs, gracefulElapsedMs);
        }
        catch
        {
            gracefulShutdown = false;
        }

        if (!gracefulShutdown)
        {
            // Forceful shutdown: cancel everything and fail pending batches.
            // Cancel BrokerSender loops FIRST so they start exiting immediately
            // while we wait for the main sender task — otherwise the sender task
            // can exit quickly but BrokerSender disposal still takes 5s each.
            //
            // Note: senders created by RerouteBatchToCurrentLeader between this
            // iteration and the disposal loop below won't have RequestCancellation
            // called. This is benign — they proceed directly to DisposeAsync which
            // performs the same cancellation internally. The do/while disposal loop
            // below will also discover senders added during earlier iterations,
            // since it re-checks _brokerSenders.Count after each round.
            foreach (var (_, sender) in _brokerSenders)
                sender.RequestCancellation();

            _senderCts.Cancel();

            // Force-fail all in-flight batches IMMEDIATELY after cancelling senders.
            // This unblocks any FlushAsync waiter (which polls _inFlightBatchCount)
            // and lets the sender loop's HasPendingWork() return false promptly,
            // so _senderTask exits without waiting for broker responses that will
            // never arrive. Previously this was done only after BrokerSender disposal,
            // meaning the sender task could hang for 5s waiting for in-flight work
            // that was already doomed.
            //
            // returnToPool: false — BrokerSender send loops are still running and hold
            // references to these batches (in carry-over, pending responses, etc.).
            // Returning to pool calls Reset() which nulls TopicPartition/RecordBatch,
            // causing NullReferenceException in the send loop. Pool return is deferred
            // to the second sweep at line ~2983 after BrokerSenders are disposed.
            _accumulator.ForceFailAllInFlightBatches(returnToPool: false);

            // Derive sender wait timeout from remaining time budget rather than hardcoding,
            // so the total dispose time never exceeds CloseTimeoutMs.
            var senderWaitMs = Math.Max(500, RemainingMs() / 3);

            try
            {
                await _senderTask
                    .WaitAsync(TimeSpan.FromMilliseconds(senderWaitMs))
                    .ConfigureAwait(false);
            }
            catch
            {
                // Ignore timeout or cancellation - we tried our best to complete gracefully
            }
        }

        // Wait for linger task to exit (it should be quick after cancellation).
        // Use a small slice of the remaining budget (capped at 1s).
        {
            var lingerWaitMs = Math.Min(1000, Math.Max(250, RemainingMs() / 4));
            try
            {
                await _lingerTask.WaitAsync(TimeSpan.FromMilliseconds(lingerWaitMs)).ConfigureAwait(false);
            }
            catch
            {
                // Ignore
            }
        }

        // Dispose all per-broker senders in parallel to avoid O(N * timeout) sequential waits.
        // Each BrokerSender.DisposeAsync waits up to 5s for its send loop — with multiple brokers,
        // sequential disposal can exceed the caller's overall dispose budget.
        // Loop until no new senders were created during the iteration — a retry's
        // RerouteBatchToCurrentLeader callback could race with the _disposed guard and
        // create a new sender after we've started iterating.
        LogDisposingBrokerSenders(_brokerSenders.Count);
        int previousCount;
        do
        {
            previousCount = _brokerSenders.Count;
            var disposeTasks = new List<Task>(_brokerSenders.Count);
            foreach (var (_, sender) in _brokerSenders)
            {
                disposeTasks.Add(DisposeOneSenderAsync(sender));
            }

            // Derive broker disposal timeout from remaining time budget, reserving 20%
            // (min 2s) for post-disposal resource cleanup (accumulator, connection pool).
            var remaining = RemainingMs();
            var reservedForCleanup = Math.Max(2000, remaining / 5);
            var brokerDisposeMs = Math.Max(500, remaining - reservedForCleanup);

            try
            {
                // Wall-clock safety net derived from the remaining time budget.
                // Each BrokerSender.DisposeAsync has its own internal per-sender timeout,
                // so under normal parallel disposal the aggregate completes quickly.
                // This outer deadline only fires if something beyond the per-sender
                // timeout hangs (e.g., CancelAsync blocking on a slow callback).
                var disposeSendersTask = Task.WhenAll(disposeTasks);
                if (_ownsInfrastructure)
                    await disposeSendersTask.ConfigureAwait(false);
                else
                    await disposeSendersTask
                        .WaitAsync(TimeSpan.FromMilliseconds(brokerDisposeMs))
                        .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // Per-sender exceptions are already swallowed and logged inside
                // DisposeOneSenderAsync, so this catch only fires when the aggregate
                // WaitAsync deadline expires. Senders may still be disposing in the
                // background — _brokerSenders.Clear() below drops our references,
                // accepting that their cleanup will finish asynchronously.
                LogBrokerSenderParallelDisposeTimedOut(_brokerSenders.Count);
            }
        } while (_brokerSenders.Count > previousCount);
        _brokerSenders.Clear();

        // Final orphan sweep: fail any batches still tracked as in-flight after all
        // BrokerSenders have been disposed. The earlier ForceFailAllInFlightBatches call
        // (in the forceful path) handles the common case; this second sweep catches batches
        // that entered the pipeline between the first sweep and BrokerSender disposal.
        _accumulator.ForceFailAllInFlightBatches();

        var partitionEnrollmentTasks = _partitionEnrollmentTasks.Keys.ToArray();
        if (partitionEnrollmentTasks.Length > 0)
        {
            try
            {
                await Task.WhenAll(partitionEnrollmentTasks).ConfigureAwait(false);
            }
            catch
            {
                // Enrollment failures are surfaced to their parked batches.
            }
        }

        _senderCts.Dispose();
        _transactionLock.Dispose();
        _initLock.Dispose();

        // Post-disposal cleanup: dispose remaining resources with bounded timeouts.
        // Previously these disposals had no time budget, so their internal timeouts
        // (e.g., accumulator's 10s worker+batch wait, connection pool's 30s connection
        // timeout) could cause DisposeAsync to far exceed CloseTimeoutMs. Now each step
        // is capped by the remaining time budget so the total dispose time stays bounded.

        // Dispose accumulator — fails any remaining batches if graceful shutdown failed.
        // Has internal 5s+5s timeouts for workers and in-flight batches, but at this point
        // BrokerSenders are already disposed so it should complete quickly.
        // Reserve half the remaining budget for subsequent disposals (pool, network).
        await DisposeWithBudgetAsync(
            _accumulator.DisposeAsync(), Math.Max(500, RemainingMs() / 2), "accumulator");

        // Stop the inflight tracker's pruning timer to prevent callbacks after disposal.
        _inflightTracker.Dispose();

        // Dispose ValueTaskSource pool — prevents resource leaks (usually fast).
        await DisposeWithBudgetAsync(
            _valueTaskSourcePool.DisposeAsync(), Math.Max(200, RemainingMs() / 10), "valueTaskSourcePool");

        var telemetryStopMs = Math.Max(200, Math.Min(5000, RemainingMs()));
        await DisposeWithBudgetAsync(
            _telemetryManager.StopAsync(TimeSpan.FromMilliseconds(telemetryStopMs)),
            telemetryStopMs,
            "telemetry");

        await DisposeWithBudgetAsync(
            _telemetryManager.DisposeAsync(), Math.Max(100, RemainingMs() / 10), "telemetryManager");

        if (_ownsInfrastructure)
        {
            // Dispose metadata manager and connection pool in parallel.
            // Connection pool disposal waits up to ConnectionTimeout (30s default) per connection,
            // which can single-handedly exceed CloseTimeoutMs. Cap with remaining budget.
            await DisposeWithBudgetAsync(
                new ValueTask(Task.WhenAll(
                    _metadataManager.DisposeAsync().AsTask(),
                    _connectionPool.DisposeAsync().AsTask())),
                Math.Max(500, RemainingMs()),
                "network");
        }

        var disposeElapsedMs = Stopwatch.GetElapsedTime(disposeStart).TotalMilliseconds;
        LogProducerDisposed(disposeElapsedMs, gracefulShutdown);
    }

    /// <summary>
    /// Disposes a single BrokerSender, swallowing and logging all exceptions by design.
    /// Individual sender failures must not prevent disposal of remaining senders or the
    /// producer itself — the caller runs these in parallel via Task.WhenAll.
    /// </summary>
    private async Task DisposeOneSenderAsync(BrokerSender sender)
    {
        try
        {
            await sender.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogDisposeBrokerSenderFailed(ex);
        }
    }

    /// <summary>
    /// Awaits a disposal ValueTask with a wall-clock timeout, swallowing all exceptions.
    /// Used during producer shutdown to prevent any single resource disposal from exceeding
    /// the remaining time budget.
    /// </summary>
    private async ValueTask DisposeWithBudgetAsync(ValueTask disposeTask, int budgetMs, string step)
    {
        try
        {
            await disposeTask.AsTask()
                .WaitAsync(TimeSpan.FromMilliseconds(budgetMs))
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            LogPostDisposalStepTimedOut(step);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TimeoutException)
        {
            LogDisposalStepFailed(ex, step);
        }
    }


    #region Logging

    [LoggerMessage(Level = LogLevel.Warning, Message = "Fire-and-forget produce failed for topic {Topic} (topic metadata fetch)")]
    private partial void LogFireAndForgetMetadataFetchFailed(Exception ex, string topic);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Fire-and-forget produce failed for topic {Topic}")]
    private partial void LogFireAndForgetProduceFailed(Exception ex, string topic);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Producer interceptor {Interceptor} OnSend threw an exception")]
    private partial void LogInterceptorOnSendFailed(Exception ex, string interceptor);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Producer interceptor {Interceptor} OnAcknowledgement threw an exception")]
    private partial void LogInterceptorOnAcknowledgementFailed(Exception ex, string interceptor);

    [LoggerMessage(Level = LogLevel.Debug, Message = "InitProducerId retriable error ({ErrorCode}, attempt {Attempt}/{MaxRetries}), retrying in {Delay}ms")]
    private partial void LogInitProducerIdRetriableError(ErrorCode errorCode, int attempt, int maxRetries, int delay);

    [LoggerMessage(Level = LogLevel.Debug, Message = "InitProducerId got NotCoordinator (attempt {Attempt}/{MaxRetries}), re-discovering coordinator")]
    private partial void LogInitProducerIdNotCoordinator(int attempt, int maxRetries);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Initialized transactions: ProducerId={ProducerId}, Epoch={Epoch}")]
    private partial void LogTransactionsInitialized(long producerId, short epoch);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Transaction coordinator not available ({ErrorCode}, attempt {Attempt}/{MaxRetries}), retrying in {Delay}ms")]
    private partial void LogTransactionCoordinatorNotAvailable(ErrorCode errorCode, int attempt, int maxRetries, int delay);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found transaction coordinator {NodeId} for {TransactionalId}")]
    private partial void LogTransactionCoordinatorFound(int nodeId, string? transactionalId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "AddPartitionsToTxn retriable error (attempt {Attempt}/{MaxRetries}), retrying in {Delay}ms")]
    private partial void LogAddPartitionsToTxnRetriableError(int attempt, int maxRetries, int delay);

    [LoggerMessage(Level = LogLevel.Debug, Message = "AddPartitionsToTxn transport failure (attempt {Attempt}), retrying in {Delay}ms")]
    private partial void LogAddPartitionsToTxnTransportRetry(int attempt, int delay);

    [LoggerMessage(Level = LogLevel.Debug, Message = "EndTxn retriable error ({ErrorCode}, attempt {Attempt}/{MaxRetries}), retrying in {Delay}ms")]
    private partial void LogEndTxnRetriableError(ErrorCode errorCode, int attempt, int maxRetries, int delay);

    [LoggerMessage(Level = LogLevel.Debug, Message = "EndTxn got NotCoordinator (attempt {Attempt}/{MaxRetries}), re-discovering coordinator")]
    private partial void LogEndTxnNotCoordinator(int attempt, int maxRetries);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error in linger loop")]
    private partial void LogLingerLoopError(Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Initialized idempotent producer: ProducerId={ProducerId}, Epoch={Epoch}")]
    private partial void LogIdempotentProducerInitialized(long producerId, short epoch);

    [LoggerMessage(Level = LogLevel.Debug, Message = "InitProducerId returned retriable error {ErrorCode}, retrying in {DelayMs}ms")]
    private partial void LogInitProducerIdRetriable(ErrorCode errorCode, int delayMs);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Bumped producer epoch: ProducerId={ProducerId}, Epoch={Epoch}")]
    private partial void LogProducerEpochBumped(long producerId, short epoch);

    [LoggerMessage(Level = LogLevel.Debug, Message = "BumpEpoch InitProducerId returned retriable error {ErrorCode}, retrying in {DelayMs}ms")]
    private partial void LogBumpEpochRetriable(ErrorCode errorCode, int delayMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Graceful shutdown timed out after {ElapsedMs:F0}ms (budget={Timeout}ms), forcing disposal")]
    private partial void LogGracefulShutdownTimedOut(int timeout, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to dispose broker sender")]
    private partial void LogDisposeBrokerSenderFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Parallel BrokerSender disposal timed out for {Count} senders")]
    private partial void LogBrokerSenderParallelDisposeTimedOut(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Post-disposal {Step} cleanup timed out, proceeding with remaining cleanup")]
    private partial void LogPostDisposalStepTimedOut(string step);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BrokerSender for broker {BrokerId} send loop exited — replacing with fresh sender")]
    private partial void LogBrokerSenderReplaced(int brokerId);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Batch routed: {Topic}-{Partition} -> broker {BrokerId}")]
    private partial void LogBatchRouted(string topic, int partition, int brokerId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "No leader cached for {Topic}-{Partition}, triggering metadata lookup")]
    private partial void LogNoLeaderCached(string topic, int partition);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Leader still unavailable for {Topic}-{Partition} after metadata lookup")]
    private partial void LogLeaderStillUnavailable(string topic, int partition);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Rerouted batch {Topic}-{Partition} to broker {BrokerId}")]
    private partial void LogReroutedBatch(string topic, int partition, int brokerId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Reroute blocked by disposal for {Topic}-{Partition}")]
    private partial void LogRerouteBlockedByDisposal(string topic, int partition);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Epoch already bumped by another thread: expected={ExpectedEpoch}, current={CurrentEpoch}")]
    private partial void LogEpochAlreadyBumped(short expectedEpoch, short currentEpoch);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Producer disposing: beginning graceful shutdown")]
    private partial void LogProducerDisposing();

    [LoggerMessage(Level = LogLevel.Information, Message = "Producer disposed in {ElapsedMs:F0}ms (graceful={Graceful})")]
    private partial void LogProducerDisposed(double elapsedMs, bool graceful);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Disposing {Count} broker senders")]
    private partial void LogDisposingBrokerSenders(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Non-fatal exception during batch cleanup step (suppressed)")]
    private partial void LogBatchCleanupStepFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Non-fatal exception during {Step} disposal (suppressed)")]
    private partial void LogDisposalStepFailed(Exception exception, string step);

    [LoggerMessage(Level = LogLevel.Error, Message = "Sender loop failed with unexpected exception")]
    private partial void LogSenderLoopFailed(Exception ex);

    #endregion

    void IBudgetedInstance.OnBudgetChanged(ulong newLimit) => _accumulator.SetMaxBufferMemory(newLimit);
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
        if (_producer.IsDisposed)
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>));
    }

    public ValueTask<RecordMetadata> ProduceAsync(
        ProducerMessage<TKey, TValue> message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ThrowIfProducerDisposed();
            _producer.ThrowIfFatalTransactionError("Cannot produce");

            if (_committed || _aborted)
                throw new InvalidOperationException("Transaction is already completed");

            _producer.ThrowIfInPreparedTransaction();

            // Partition registration with AddPartitionsToTxn is handled automatically
            // by BrokerSender before the ProduceRequest is sent to the broker.
            return _producer.ProduceTransactionAsync(message, cancellationToken);
        }
        catch (OperationCanceledException exception)
        {
            var canceledToken = exception.CancellationToken.IsCancellationRequested
                ? exception.CancellationToken
                : cancellationToken.IsCancellationRequested
                    ? cancellationToken
                    : new CancellationToken(canceled: true);
            return new ValueTask<RecordMetadata>(Task.FromCanceled<RecordMetadata>(canceledToken));
        }
        catch (Exception exception)
        {
            // Preserve async-method exception timing: validation failures fault the returned
            // ValueTask instead of escaping synchronously from ProduceAsync.
            return new ValueTask<RecordMetadata>(Task.FromException<RecordMetadata>(exception));
        }
    }

    public ValueTask CommitAsync(CancellationToken cancellationToken = default) =>
        CommitCoreAsync(afterRequestWrittenAsync: null, cancellationToken);

    internal ValueTask CommitAfterRequestWrittenAsync(
        Func<ValueTask> afterRequestWrittenAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(afterRequestWrittenAsync);
        return CommitCoreAsync(afterRequestWrittenAsync, cancellationToken);
    }

    private async ValueTask CommitCoreAsync(
        Func<ValueTask>? afterRequestWrittenAsync,
        CancellationToken cancellationToken)
    {
        ThrowIfProducerDisposed();
        _producer.ThrowIfFatalTransactionError("Cannot commit transaction");

        if (_committed || _aborted)
            throw new InvalidOperationException("Transaction is already completed");

        _producer._transactionState = TransactionState.CommittingTransaction;

        try
        {
            // Flush all pending messages before committing
            await _producer.FlushAsync(cancellationToken).ConfigureAwait(false);

            await _producer.EndTransactionAsync(
                    committed: true,
                    afterRequestWrittenAsync,
                    cancellationToken)
                .ConfigureAwait(false);
            _committed = true;
        }
        finally
        {
            FinalizeTransactionState();
        }
    }

    public async ValueTask<PreparedTransactionState> PrepareAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfProducerDisposed();
        _producer.ThrowIfFatalTransactionError("Cannot prepare transaction");

        if (_committed || _aborted)
            throw new InvalidOperationException("Transaction is already completed");

        await _producer.FlushAsync(cancellationToken).ConfigureAwait(false);
        return _producer.PrepareCurrentTransaction();
    }

    /// <summary>
    /// Clears the transaction's partition set and returns the producer to
    /// <see cref="TransactionState.Ready"/> for the next transaction — unless ending the transaction
    /// left it in an error state, which the fail-fast produce guard relies on being preserved.
    /// </summary>
    private void FinalizeTransactionState()
    {
        _producer.FinalizeCompletedTransactionState();
    }

    public async ValueTask AbortAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfProducerDisposed();
        _producer.ThrowIfFatalTransactionError("Cannot abort transaction");

        if (_committed || _aborted)
            throw new InvalidOperationException("Transaction is already completed");

        _producer._transactionState = TransactionState.AbortingTransaction;

        try
        {
            await _producer.EndTransactionAsync(committed: false, cancellationToken).ConfigureAwait(false);

            // TV1: broker doesn't return bumped epoch in EndTxn, so we must call
            // InitProducerId to get it (KIP-360).
            // TV2: EndTxn v5 response already contained the bumped epoch — skip.
            if (!_producer._currentTransactionUsesTV2)
            {
                await _producer.ReinitializeProducerIdAsync(cancellationToken).ConfigureAwait(false);
            }

            _aborted = true;
        }
        finally
        {
            FinalizeTransactionState();
        }
    }

    public async ValueTask SendOffsetsToTransactionAsync(
        IEnumerable<TopicPartitionOffset> offsets,
        string consumerGroupId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfProducerDisposed();
        _producer.ThrowIfFatalTransactionError("Cannot send offsets to transaction");

        if (_committed || _aborted)
            throw new InvalidOperationException("Transaction is already completed");

        _producer.ThrowIfInPreparedTransaction();

        await _producer.SendOffsetsToTransactionInternalAsync(offsets, consumerGroupId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask SendOffsetsToTransactionAsync(
        IEnumerable<TopicPartitionOffset> offsets,
        ConsumerGroupMetadata consumerGroupMetadata,
        CancellationToken cancellationToken = default)
    {
        ThrowIfProducerDisposed();
        _producer.ThrowIfFatalTransactionError("Cannot send offsets to transaction");

        if (_committed || _aborted)
            throw new InvalidOperationException("Transaction is already completed");

        _producer.ThrowIfInPreparedTransaction();
        ArgumentNullException.ThrowIfNull(consumerGroupMetadata);

        await _producer.SendOffsetsToTransactionInternalAsync(
                offsets,
                consumerGroupMetadata,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_committed && !_aborted && !_producer.IsDisposed
            && _producer._transactionState is TransactionState.InTransaction or TransactionState.AbortableError)
        {
            // Abort on dispose if not completed and a transaction is in progress or abortable
            try
            {
                await AbortAsync().ConfigureAwait(false);
            }
            catch (TransactionException)
            {
                // Best-effort abort during disposal — if the broker rejects it
                // (e.g. InvalidTxnState because no messages were produced),
                // just clean up state and move on. Fatal responses remain sticky, while
                // abortable responses return to Ready because this transaction is disposed.
                _producer.FinalizeCompletedTransactionState(preserveAbortableError: false);
            }
        }
    }
}

/// <summary>
/// A buffer writer that writes to a provided reusable buffer.
/// Used with thread-local buffers to avoid per-message ArrayPool rentals.
/// After serialization, ToPooledMemory() copies data to a right-sized pooled buffer.
/// </summary>
#if NETSTANDARD2_0
internal struct ReusableBufferWriter : IBufferWriter<byte>
#else
internal ref struct ReusableBufferWriter : IBufferWriter<byte>
#endif
{
    private byte[] _buffer;
    private int _written;

    public ReusableBufferWriter(ref byte[]? buffer, int initialCapacity)
    {
        _buffer = buffer ??= new byte[initialCapacity];
        _written = 0;
    }

    public readonly int WrittenCount => _written;

    public readonly ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _written);

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

        // Rent exact-size buffer from ProducerDataPool to prevent cross-thread
        // TLS accumulation in ArrayPool<byte>.Shared (see ProducerDataPool docs).
        var pooledArray = ProducerDataPool.BytePool.Rent(_written);
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
        _buffer = ProducerDataPool.BytePool.Rent(initialCapacity);
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
            // clearArray: false for performance — avoids unnecessary zero-fill overhead.
            // No security requirement: buffer holds serialized Kafka protocol bytes, not credentials.
            // Consistent with Grow() which also uses clearArray: false.
            ProducerDataPool.BytePool.Return(_buffer, clearArray: false);
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

        var newBuffer = ProducerDataPool.BytePool.Rent(newSize);

        // Copy existing data
        _buffer.AsSpan(0, _written).CopyTo(newBuffer);

        // Return old buffer - use clearArray: false for performance (avoids zero-fill overhead).
        // No security requirement: the buffer holds serialized protocol bytes, not credentials.
        var oldBuffer = _buffer;
        _buffer = newBuffer;
        ProducerDataPool.BytePool.Return(oldBuffer, clearArray: false);
    }
}

