using System.Security.Cryptography.X509Certificates;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Producer;
using Dekaf.Retry;
using Dekaf.Security;
using Dekaf.Security.Sasl;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;

namespace Dekaf;

/// <summary>
/// Fluent builder for creating producers.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public sealed class ProducerBuilder<TKey, TValue>
{
    private readonly List<string> _bootstrapServers = [];
    private string? _clientId;
    private Acks _acks = Acks.All;
    private int _lingerMs;
    private int _batchSize = 1048576;
    private string? _transactionalId;
    private int? _transactionTimeoutMs;
    private Protocol.Records.CompressionType _compressionType = Protocol.Records.CompressionType.None;
    private int? _compressionLevel;
    private PartitionerType _partitionerType = PartitionerType.Default;
    private IPartitioner? _customPartitioner;
    private bool _useTls;
    private TlsConfig? _tlsConfig;
    private SaslMechanism _saslMechanism = SaslMechanism.None;
    private string? _saslUsername;
    private string? _saslPassword;
    private GssapiConfig? _gssapiConfig;
    private OAuthBearerConfig? _oauthConfig;
    private Func<CancellationToken, ValueTask<OAuthBearerToken>>? _oauthTokenProvider;
    private ISerializer<TKey>? _keySerializer;
    private ISerializer<TValue>? _valueSerializer;
    private Microsoft.Extensions.Logging.ILoggerFactory? _loggerFactory;
    private TimeSpan? _statisticsInterval;
    private Action<Statistics.ProducerStatistics>? _statisticsHandler;
    private ulong? _bufferMemory;
    private int? _maxBlockMs;
    private MetadataRecoveryStrategy _metadataRecoveryStrategy = MetadataRecoveryStrategy.Rebootstrap;
    private int _metadataRecoveryRebootstrapTriggerMs = 300000;
    private bool _enableIdempotence = true;
    private List<IProducerInterceptor<TKey, TValue>>? _interceptors;
    private TimeSpan? _metadataMaxAge;
    private int? _deliveryTimeoutMs;
    private int? _requestTimeoutMs;
    private IRetryPolicy? _retryPolicy;

    public ProducerBuilder<TKey, TValue> WithBootstrapServers(string servers)
    {
        _bootstrapServers.Clear();
        _bootstrapServers.AddRange(servers.Split(',').Select(s => s.Trim()));
        return this;
    }

    public ProducerBuilder<TKey, TValue> WithBootstrapServers(params string[] servers)
    {
        _bootstrapServers.Clear();
        _bootstrapServers.AddRange(servers);
        return this;
    }

    public ProducerBuilder<TKey, TValue> WithClientId(string clientId)
    {
        _clientId = clientId;
        return this;
    }

    public ProducerBuilder<TKey, TValue> WithAcks(Acks acks)
    {
        _acks = acks;
        return this;
    }

    /// <summary>
    /// Sets the linger time for batching messages.
    /// </summary>
    /// <param name="linger">The time to wait before sending a batch.</param>
    public ProducerBuilder<TKey, TValue> WithLinger(TimeSpan linger)
    {
        _lingerMs = (int)linger.TotalMilliseconds;
        return this;
    }

    public ProducerBuilder<TKey, TValue> WithBatchSize(int batchSize)
    {
        _batchSize = batchSize;
        return this;
    }

    /// <summary>
    /// Sets the total bytes of memory the producer can use to buffer records waiting to be sent.
    /// </summary>
    /// <param name="bufferMemory">The buffer memory limit in bytes.</param>
    /// <remarks>
    /// When the buffer is full, <see cref="KafkaProducer{TKey,TValue}.ProduceAsync"/> will block
    /// until space becomes available or the delivery timeout expires.
    /// Default is 256 MB.
    /// </remarks>
    public ProducerBuilder<TKey, TValue> WithBufferMemory(ulong bufferMemory)
    {
        _bufferMemory = bufferMemory;
        return this;
    }

    /// <summary>
    /// Sets the maximum time that produce operations will block when the buffer
    /// is full or metadata is unavailable.
    /// </summary>
    /// <param name="maxBlock">The maximum block time. Must be positive.</param>
    /// <remarks>
    /// <para>
    /// Equivalent to Kafka's <c>max.block.ms</c> configuration.
    /// Default is 60 seconds.
    /// </para>
    /// </remarks>
    public ProducerBuilder<TKey, TValue> WithMaxBlock(TimeSpan maxBlock)
    {
        if (maxBlock <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(maxBlock), "MaxBlock must be positive");

        _maxBlockMs = (int)maxBlock.TotalMilliseconds;
        return this;
    }

    public ProducerBuilder<TKey, TValue> WithTransactionalId(string transactionalId)
    {
        _transactionalId = transactionalId;
        return this;
    }

    /// <summary>
    /// Enables or disables idempotent producer mode.
    /// <para>
    /// Idempotence is enabled by default for safety. When enabled, the producer obtains a
    /// producer ID from the broker and assigns sequence numbers to each batch, allowing the
    /// broker to deduplicate retried messages.
    /// </para>
    /// <para>
    /// Disabling idempotence reduces overhead slightly (no <c>InitProducerId</c> call during
    /// initialization, no sequence number tracking) but allows duplicate messages on retry.
    /// </para>
    /// <para>
    /// Cannot be disabled when <see cref="WithTransactionalId"/> is set, because transactions
    /// require idempotence for correctness.
    /// </para>
    /// </summary>
    /// <param name="enable">Whether to enable idempotence.</param>
    public ProducerBuilder<TKey, TValue> WithIdempotence(bool enable)
    {
        _enableIdempotence = enable;
        return this;
    }

    /// <summary>
    /// Sets the transaction timeout. If a transaction is not committed or aborted
    /// within this duration, the coordinator will proactively abort it.
    /// </summary>
    /// <param name="timeout">The transaction timeout. Must be positive.</param>
    /// <remarks>
    /// <para>
    /// Equivalent to Kafka's <c>transaction.timeout.ms</c> configuration.
    /// Default is 60 seconds. The value must not exceed the broker's
    /// <c>transaction.max.timeout.ms</c> setting (default 15 minutes).
    /// </para>
    /// </remarks>
    public ProducerBuilder<TKey, TValue> WithTransactionTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Transaction timeout must be positive");

        _transactionTimeoutMs = (int)timeout.TotalMilliseconds;
        return this;
    }

    public ProducerBuilder<TKey, TValue> UseGzipCompression()
    {
        _compressionType = Protocol.Records.CompressionType.Gzip;
        return this;
    }

    public ProducerBuilder<TKey, TValue> UseCompression(Protocol.Records.CompressionType compressionType)
    {
        _compressionType = compressionType;
        return this;
    }

    /// <summary>
    /// Sets the compression level for the configured compression codec.
    /// Valid ranges depend on the compression type:
    /// Gzip: 0-9, LZ4: 0-12, Zstd: 1-22. Snappy does not support levels.
    /// When not set, the codec's default level is used.
    /// </summary>
    /// <param name="level">The compression level.</param>
    public ProducerBuilder<TKey, TValue> WithCompressionLevel(int level)
    {
        _compressionLevel = level;
        return this;
    }

    public ProducerBuilder<TKey, TValue> WithPartitioner(PartitionerType partitionerType)
    {
        _partitionerType = partitionerType;
        _customPartitioner = null;
        return this;
    }

    /// <summary>
    /// Sets a custom partitioner implementation.
    /// When set, this takes precedence over <see cref="WithPartitioner(PartitionerType)"/>.
    /// </summary>
    /// <param name="partitioner">The custom partitioner to use.</param>
    public ProducerBuilder<TKey, TValue> WithCustomPartitioner(IPartitioner partitioner)
    {
        _customPartitioner = partitioner ?? throw new ArgumentNullException(nameof(partitioner));
        return this;
    }

    /// <summary>
    /// Enables TLS for secure connections.
    /// </summary>
    public ProducerBuilder<TKey, TValue> UseTls()
    {
        _useTls = true;
        return this;
    }

    /// <summary>
    /// Configures TLS with custom settings.
    /// </summary>
    /// <param name="config">The TLS configuration.</param>
    public ProducerBuilder<TKey, TValue> UseTls(TlsConfig config)
    {
        _useTls = true;
        _tlsConfig = config;
        return this;
    }

    /// <summary>
    /// Configures mutual TLS (mTLS) authentication using certificate files.
    /// </summary>
    /// <param name="caCertPath">Path to the CA certificate file (PEM format).</param>
    /// <param name="clientCertPath">Path to the client certificate file (PEM format).</param>
    /// <param name="clientKeyPath">Path to the client private key file (PEM format).</param>
    /// <param name="keyPassword">Optional password for the private key.</param>
    public ProducerBuilder<TKey, TValue> UseMutualTls(
        string caCertPath,
        string clientCertPath,
        string clientKeyPath,
        string? keyPassword = null)
    {
        _useTls = true;
        _tlsConfig = TlsConfig.CreateMutualTls(caCertPath, clientCertPath, clientKeyPath, keyPassword);
        return this;
    }

    /// <summary>
    /// Configures mutual TLS (mTLS) authentication using in-memory certificates.
    /// </summary>
    /// <param name="clientCertificate">The client certificate with private key.</param>
    /// <param name="caCertificate">Optional CA certificate for server validation.</param>
    public ProducerBuilder<TKey, TValue> UseMutualTls(
        X509Certificate2 clientCertificate,
        X509Certificate2? caCertificate = null)
    {
        _useTls = true;
        _tlsConfig = TlsConfig.CreateMutualTls(clientCertificate, caCertificate);
        return this;
    }

    public ProducerBuilder<TKey, TValue> WithSaslPlain(string username, string password)
    {
        _saslMechanism = SaslMechanism.Plain;
        _saslUsername = username;
        _saslPassword = password;
        return this;
    }

    public ProducerBuilder<TKey, TValue> WithSaslScramSha256(string username, string password)
    {
        _saslMechanism = SaslMechanism.ScramSha256;
        _saslUsername = username;
        _saslPassword = password;
        return this;
    }

    public ProducerBuilder<TKey, TValue> WithSaslScramSha512(string username, string password)
    {
        _saslMechanism = SaslMechanism.ScramSha512;
        _saslUsername = username;
        _saslPassword = password;
        return this;
    }

    /// <summary>
    /// Configures GSSAPI (Kerberos) authentication.
    /// </summary>
    /// <param name="config">The GSSAPI configuration.</param>
    /// <returns>This builder for chaining.</returns>
    public ProducerBuilder<TKey, TValue> WithGssapi(GssapiConfig config)
    {
        _saslMechanism = SaslMechanism.Gssapi;
        _gssapiConfig = config ?? throw new ArgumentNullException(nameof(config));
        return this;
    }

    /// <summary>
    /// Configures OAUTHBEARER authentication using OAuth 2.0 client credentials flow.
    /// </summary>
    /// <param name="config">The OAuth bearer configuration.</param>
    public ProducerBuilder<TKey, TValue> WithOAuthBearer(OAuthBearerConfig config)
    {
        _saslMechanism = SaslMechanism.OAuthBearer;
        _oauthConfig = config ?? throw new ArgumentNullException(nameof(config));
        _oauthTokenProvider = null;
        return this;
    }

    /// <summary>
    /// Configures OAUTHBEARER authentication using a custom token provider.
    /// </summary>
    /// <param name="tokenProvider">A function that provides OAuth tokens on demand.</param>
    public ProducerBuilder<TKey, TValue> WithOAuthBearer(Func<CancellationToken, ValueTask<OAuthBearerToken>> tokenProvider)
    {
        _saslMechanism = SaslMechanism.OAuthBearer;
        _oauthTokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        _oauthConfig = null;
        return this;
    }

    public ProducerBuilder<TKey, TValue> WithKeySerializer(ISerializer<TKey> serializer)
    {
        _keySerializer = serializer;
        return this;
    }

    public ProducerBuilder<TKey, TValue> WithValueSerializer(ISerializer<TValue> serializer)
    {
        _valueSerializer = serializer;
        return this;
    }

    public ProducerBuilder<TKey, TValue> WithLoggerFactory(Microsoft.Extensions.Logging.ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        return this;
    }

    /// <summary>
    /// Sets the interval for emitting statistics events.
    /// </summary>
    /// <param name="interval">The interval between statistics events. Must be positive.</param>
    public ProducerBuilder<TKey, TValue> WithStatisticsInterval(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), "Statistics interval must be positive");

        _statisticsInterval = interval;
        return this;
    }

    /// <summary>
    /// Sets the handler for statistics events.
    /// </summary>
    /// <param name="handler">The handler to invoke when statistics are emitted.</param>
    public ProducerBuilder<TKey, TValue> WithStatisticsHandler(Action<Statistics.ProducerStatistics> handler)
    {
        _statisticsHandler = handler ?? throw new ArgumentNullException(nameof(handler));
        return this;
    }

    /// <summary>
    /// Sets the handler for statistics events.
    /// </summary>
    /// <param name="handler">The handler to invoke when statistics are emitted.</param>
    [Obsolete("Use WithStatisticsHandler instead.")]
    public ProducerBuilder<TKey, TValue> OnStatistics(Action<Statistics.ProducerStatistics> handler)
        => WithStatisticsHandler(handler);

    /// <summary>
    /// Sets the metadata recovery strategy for when all known brokers become unavailable.
    /// </summary>
    /// <param name="strategy">The recovery strategy to use.</param>
    public ProducerBuilder<TKey, TValue> WithMetadataRecoveryStrategy(MetadataRecoveryStrategy strategy)
    {
        _metadataRecoveryStrategy = strategy;
        return this;
    }

    /// <summary>
    /// Sets how long to wait before triggering a rebootstrap when all known
    /// brokers are unavailable.
    /// </summary>
    /// <param name="trigger">The trigger delay. Default is 5 minutes.</param>
    public ProducerBuilder<TKey, TValue> WithMetadataRecoveryRebootstrapTrigger(TimeSpan trigger)
    {
        _metadataRecoveryRebootstrapTriggerMs = (int)trigger.TotalMilliseconds;
        return this;
    }

    /// <summary>
    /// Sets the maximum age of metadata before it is refreshed.
    /// This controls how frequently the client refreshes its view of the cluster topology.
    /// Equivalent to Kafka's <c>metadata.max.age.ms</c> configuration.
    /// Default is 15 minutes.
    /// </summary>
    /// <param name="interval">The maximum age of metadata. Must be positive.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public ProducerBuilder<TKey, TValue> WithMetadataMaxAge(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), "Metadata max age must be positive");

        _metadataMaxAge = interval;
        return this;
    }

    /// <summary>
    /// Configures the producer for high throughput scenarios.
    /// </summary>
    /// <remarks>
    /// <para>Settings applied:</para>
    /// <list type="bullet">
    /// <item><description>Acks: Leader only (faster acknowledgment)</description></item>
    /// <item><description>LingerMs: 5ms (allows batching)</description></item>
    /// <item><description>BatchSize: 2MB (larger batches for maximum throughput)</description></item>
    /// <item><description>Compression: LZ4 (fast compression)</description></item>
    /// </list>
    /// <para>These settings can be overridden by calling other builder methods after this one.</para>
    /// </remarks>
    /// <returns>The builder instance for method chaining.</returns>
    public ProducerBuilder<TKey, TValue> ForHighThroughput()
    {
        _acks = Acks.Leader;
        _lingerMs = 5;
        _batchSize = 2097152;
        _compressionType = Protocol.Records.CompressionType.Lz4;
        return this;
    }

    /// <summary>
    /// Configures the producer for low latency scenarios.
    /// </summary>
    /// <remarks>
    /// <para>Settings applied:</para>
    /// <list type="bullet">
    /// <item><description>Acks: Leader only (faster acknowledgment)</description></item>
    /// <item><description>LingerMs: 0ms (no batching delay)</description></item>
    /// <item><description>BatchSize: 256KB (smaller batches for lower latency)</description></item>
    /// </list>
    /// <para>These settings can be overridden by calling other builder methods after this one.</para>
    /// </remarks>
    /// <returns>The builder instance for method chaining.</returns>
    public ProducerBuilder<TKey, TValue> ForLowLatency()
    {
        _acks = Acks.Leader;
        _lingerMs = 0;
        _batchSize = 262144;
        return this;
    }

    /// <summary>
    /// Configures the producer for maximum reliability.
    /// </summary>
    /// <remarks>
    /// <para>Settings applied:</para>
    /// <list type="bullet">
    /// <item><description>Acks: All (wait for all in-sync replicas)</description></item>
    /// </list>
    /// <para>Idempotence is always enabled. These settings can be overridden by calling other builder methods after this one.</para>
    /// </remarks>
    /// <returns>The builder instance for method chaining.</returns>
    public ProducerBuilder<TKey, TValue> ForReliability()
    {
        _acks = Acks.All;
        return this;
    }

    /// <summary>
    /// Adds a producer interceptor to the pipeline.
    /// Interceptors are called in the order they are added.
    /// </summary>
    /// <param name="interceptor">The interceptor to add.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public ProducerBuilder<TKey, TValue> AddInterceptor(IProducerInterceptor<TKey, TValue> interceptor)
    {
        ArgumentNullException.ThrowIfNull(interceptor);
        _interceptors ??= [];
        _interceptors.Add(interceptor);
        return this;
    }

    /// <summary>
    /// Sets the delivery timeout - the upper bound on the time to report success or failure
    /// after a call to <c>ProduceAsync</c>. This limits the total time a message can spend
    /// being retried. Equivalent to Kafka's <c>delivery.timeout.ms</c>.
    /// Default is 120 seconds.
    /// </summary>
    /// <param name="timeout">The delivery timeout. Must be positive.</param>
    public ProducerBuilder<TKey, TValue> WithDeliveryTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Delivery timeout must be positive");

        _deliveryTimeoutMs = (int)timeout.TotalMilliseconds;
        return this;
    }

    /// <summary>
    /// Sets the request timeout for individual broker requests.
    /// Equivalent to Kafka's <c>request.timeout.ms</c>.
    /// Default is 30 seconds.
    /// </summary>
    /// <param name="timeout">The request timeout. Must be positive.</param>
    public ProducerBuilder<TKey, TValue> WithRequestTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Request timeout must be positive");

        _requestTimeoutMs = (int)timeout.TotalMilliseconds;
        return this;
    }

    /// <summary>
    /// Sets the application-level retry policy for produce operations.
    /// When set, retriable exceptions from <see cref="IKafkaProducer{TKey,TValue}.ProduceAsync"/>
    /// will be retried according to this policy.
    /// </summary>
    /// <param name="retryPolicy">The retry policy to use.</param>
    public ProducerBuilder<TKey, TValue> WithRetryPolicy(IRetryPolicy retryPolicy)
    {
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        return this;
    }

    /// <summary>
    /// Builds and initializes the producer, ready for immediate use.
    /// This is equivalent to calling <see cref="Build"/> followed by <see cref="IKafkaProducer{TKey,TValue}.InitializeAsync"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the initialization.</param>
    /// <returns>An initialized producer ready to produce messages.</returns>
    public async ValueTask<IKafkaProducer<TKey, TValue>> BuildAsync(
        CancellationToken cancellationToken = default)
    {
        var producer = Build();
        try
        {
            await producer.InitializeAsync(cancellationToken).ConfigureAwait(false);
            return producer;
        }
        catch
        {
            await producer.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Builds and initializes a topic-specific producer bound to the specified topic, ready for immediate use.
    /// </summary>
    /// <param name="topic">The topic to bind the producer to.</param>
    /// <param name="cancellationToken">Cancellation token for the initialization.</param>
    /// <returns>An initialized producer bound to the specified topic.</returns>
    public async ValueTask<ITopicProducer<TKey, TValue>> BuildForTopicAsync(
        string topic,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(topic);
        var producer = await BuildAsync(cancellationToken).ConfigureAwait(false);
        return new TopicProducer<TKey, TValue>(producer, topic, ownsProducer: true);
    }

    public IKafkaProducer<TKey, TValue> Build()
    {
        if (_bootstrapServers.Count == 0)
            throw new InvalidOperationException("Bootstrap servers must be specified. Call WithBootstrapServers() before Build().");

        if (_compressionType == Protocol.Records.CompressionType.Brotli)
            throw new NotSupportedException(
                "Brotli compression (CompressionType.Brotli = 5) is not part of the Apache Kafka protocol specification. " +
                "Standard Kafka brokers will reject produce requests using this compression type. " +
                "Brotli is only supported for client-side use cases such as local storage or inter-service communication " +
                "where both producer and consumer use Dekaf with the Dekaf.Compression.Brotli package installed. " +
                "For standard Kafka broker communication, use Gzip, Snappy, Lz4, or Zstd instead.");

        if (_transactionalId is not null && !_enableIdempotence)
            throw new InvalidOperationException("Idempotence cannot be disabled when TransactionalId is set. Transactions require idempotence for correctness.");

        if (_enableIdempotence && _acks == Acks.None)
            throw new InvalidOperationException("Idempotence requires Acks.Leader or Acks.All. Acks.None is incompatible because the broker cannot acknowledge sequence numbers without sending a response.");

        var keySerializer = _keySerializer ?? GetDefaultSerializer<TKey>();
        var valueSerializer = _valueSerializer ?? GetDefaultSerializer<TValue>();

        var options = new ProducerOptions
        {
            BootstrapServers = _bootstrapServers,
            ClientId = _clientId,
            Acks = _acks,
            LingerMs = _lingerMs,
            BatchSize = _batchSize,
            BufferMemory = _bufferMemory ?? 268435456, // 256 MB default
            MaxBlockMs = _maxBlockMs ?? 60000, // 60 seconds default
            DeliveryTimeoutMs = _deliveryTimeoutMs ?? 120000,
            RequestTimeoutMs = _requestTimeoutMs ?? 30000,
            EnableIdempotence = _enableIdempotence,
            TransactionalId = _transactionalId,
            TransactionTimeoutMs = _transactionTimeoutMs ?? 60000,
            CompressionType = _compressionType,
            CompressionLevel = _compressionLevel,
            Partitioner = _partitionerType,
            CustomPartitioner = _customPartitioner,
            UseTls = _useTls,
            TlsConfig = _tlsConfig,
            SaslMechanism = _saslMechanism,
            SaslUsername = _saslUsername,
            SaslPassword = _saslPassword,
            GssapiConfig = _gssapiConfig,
            OAuthBearerConfig = _oauthConfig,
            OAuthBearerTokenProvider = _oauthTokenProvider,
            StatisticsInterval = _statisticsInterval,
            StatisticsHandler = _statisticsHandler,
            MetadataRecoveryStrategy = _metadataRecoveryStrategy,
            MetadataRecoveryRebootstrapTriggerMs = _metadataRecoveryRebootstrapTriggerMs,
            Interceptors = _interceptors?.Count > 0 ? _interceptors.ToArray() : null,
            RetryPolicy = _retryPolicy
        };

        var metadataOptions = _metadataMaxAge.HasValue
            ? new MetadataOptions { MetadataRefreshInterval = _metadataMaxAge.Value }
            : null;

        return new KafkaProducer<TKey, TValue>(options, keySerializer, valueSerializer, _loggerFactory, metadataOptions);
    }

    /// <summary>
    /// Builds a topic-specific producer bound to the specified topic.
    /// </summary>
    /// <remarks>
    /// <para>The returned topic producer owns the underlying producer and will dispose it
    /// when the topic producer is disposed.</para>
    /// </remarks>
    /// <param name="topic">The topic to bind the producer to.</param>
    /// <returns>A producer bound to the specified topic.</returns>
    public ITopicProducer<TKey, TValue> BuildForTopic(string topic)
    {
        ArgumentNullException.ThrowIfNull(topic);
        var producer = Build();
        return new TopicProducer<TKey, TValue>(producer, topic, ownsProducer: true);
    }

    private static ISerializer<T> GetDefaultSerializer<T>()
    {
        if (typeof(T) == typeof(string))
            return (ISerializer<T>)(object)Serializers.String;
        if (typeof(T) == typeof(byte[]))
            return (ISerializer<T>)(object)Serializers.ByteArray;
        if (typeof(T) == typeof(ReadOnlyMemory<byte>))
            return (ISerializer<T>)(object)Serializers.RawBytes;
        if (typeof(T) == typeof(int))
            return (ISerializer<T>)(object)Serializers.Int32;
        if (typeof(T) == typeof(long))
            return (ISerializer<T>)(object)Serializers.Int64;
        if (typeof(T) == typeof(Guid))
            return (ISerializer<T>)(object)Serializers.Guid;
        if (typeof(T) == typeof(Ignore))
            return (ISerializer<T>)(object)Serializers.Ignore;

        throw new InvalidOperationException($"No default serializer for type {typeof(T)}. Please specify a serializer.");
    }
}

/// <summary>
/// Fluent builder for creating consumers.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public sealed class ConsumerBuilder<TKey, TValue>
{
    private readonly List<string> _bootstrapServers = [];
    private string? _clientId;
    private string? _groupId;
    private string? _groupInstanceId;
    private GroupProtocol _groupProtocol = GroupProtocol.Classic;
    private string? _groupRemoteAssignor;
    private OffsetCommitMode _offsetCommitMode = OffsetCommitMode.Auto;
    private int _autoCommitIntervalMs = 5000;
    private AutoOffsetReset _autoOffsetReset = AutoOffsetReset.Latest;
    private int _fetchMinBytes = 1;
    private int _fetchMaxBytes = 52428800;
    private int _maxPartitionFetchBytes = 1048576;
    private int _fetchMaxWaitMs = 500;
    private int _maxPollRecords = 500;
    private int _sessionTimeoutMs = 45000;
    private int? _heartbeatIntervalMs;
    private bool _useTls;
    private TlsConfig? _tlsConfig;
    private List<IConsumerInterceptor<TKey, TValue>>? _interceptors;
    private SaslMechanism _saslMechanism = SaslMechanism.None;
    private string? _saslUsername;
    private string? _saslPassword;
    private GssapiConfig? _gssapiConfig;
    private OAuthBearerConfig? _oauthConfig;
    private Func<CancellationToken, ValueTask<OAuthBearerToken>>? _oauthTokenProvider;
    private IDeserializer<TKey>? _keyDeserializer;
    private IDeserializer<TValue>? _valueDeserializer;
    private IRebalanceListener? _rebalanceListener;
    private Microsoft.Extensions.Logging.ILoggerFactory? _loggerFactory;
    private bool _enablePartitionEof;
    private TimeSpan? _statisticsInterval;
    private Action<Statistics.ConsumerStatistics>? _statisticsHandler;
    private int _queuedMinMessages = 100000;
    private MetadataRecoveryStrategy _metadataRecoveryStrategy = MetadataRecoveryStrategy.Rebootstrap;
    private int _metadataRecoveryRebootstrapTriggerMs = 300000;
    private readonly List<string> _topicsToSubscribe = [];
    private TimeSpan? _metadataMaxAge;
    private IsolationLevel _isolationLevel = IsolationLevel.ReadUncommitted;
    private PartitionAssignmentStrategy _partitionAssignmentStrategy = PartitionAssignmentStrategy.CooperativeSticky;
    private IPartitionAssignmentStrategy? _customPartitionAssignmentStrategy;
    private IRetryPolicy? _retryPolicy;

    public ConsumerBuilder<TKey, TValue> WithBootstrapServers(string servers)
    {
        _bootstrapServers.Clear();
        _bootstrapServers.AddRange(servers.Split(',').Select(s => s.Trim()));
        return this;
    }

    public ConsumerBuilder<TKey, TValue> WithBootstrapServers(params string[] servers)
    {
        _bootstrapServers.Clear();
        _bootstrapServers.AddRange(servers);
        return this;
    }

    public ConsumerBuilder<TKey, TValue> WithClientId(string clientId)
    {
        _clientId = clientId;
        return this;
    }

    public ConsumerBuilder<TKey, TValue> WithGroupId(string groupId)
    {
        _groupId = groupId;
        return this;
    }

    public ConsumerBuilder<TKey, TValue> WithGroupInstanceId(string groupInstanceId)
    {
        _groupInstanceId = groupInstanceId;
        return this;
    }

    /// <summary>
    /// Sets the consumer group protocol to use for group coordination.
    /// </summary>
    /// <param name="groupProtocol">The group protocol to use.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><see cref="GroupProtocol.Classic"/>: Traditional JoinGroup/SyncGroup/Heartbeat protocol (default)</description></item>
    /// <item><description><see cref="GroupProtocol.Consumer"/>: KIP-848 ConsumerGroupHeartbeat protocol (Kafka 4.0+)</description></item>
    /// </list>
    /// </remarks>
    public ConsumerBuilder<TKey, TValue> WithGroupProtocol(GroupProtocol groupProtocol)
    {
        _groupProtocol = groupProtocol;
        return this;
    }

    /// <summary>
    /// Sets the server-side partition assignor for the Consumer group protocol (KIP-848).
    /// Common values are "uniform" and "range".
    /// </summary>
    /// <param name="assignor">The server-side assignor name.</param>
    /// <remarks>
    /// This setting is only applicable when using <see cref="GroupProtocol.Consumer"/>.
    /// When not set, the broker uses its default assignor.
    /// </remarks>
    public ConsumerBuilder<TKey, TValue> WithGroupRemoteAssignor(string assignor)
    {
        _groupRemoteAssignor = assignor ?? throw new ArgumentNullException(nameof(assignor));
        return this;
    }

    /// <summary>
    /// Sets the partition assignment strategy for the classic consumer group protocol.
    /// </summary>
    /// <param name="strategy">The built-in assignment strategy to use.</param>
    public ConsumerBuilder<TKey, TValue> WithPartitionAssignmentStrategy(PartitionAssignmentStrategy strategy)
    {
        _partitionAssignmentStrategy = strategy;
        _customPartitionAssignmentStrategy = null;
        return this;
    }

    /// <summary>
    /// Sets a custom partition assignment strategy implementation.
    /// When set, this takes precedence over the enum-based <see cref="WithPartitionAssignmentStrategy(PartitionAssignmentStrategy)"/>.
    /// </summary>
    /// <param name="strategy">The custom partition assignment strategy to use.</param>
    public ConsumerBuilder<TKey, TValue> WithPartitionAssignmentStrategy(IPartitionAssignmentStrategy strategy)
    {
        _customPartitionAssignmentStrategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        return this;
    }

    /// <summary>
    /// Subscribes the consumer to the specified topic when built.
    /// This is equivalent to calling Subscribe() on the built consumer.
    /// </summary>
    /// <param name="topic">The topic to subscribe to.</param>
    public ConsumerBuilder<TKey, TValue> SubscribeTo(string topic)
    {
        _topicsToSubscribe.Add(topic);
        return this;
    }

    /// <summary>
    /// Subscribes the consumer to the specified topics when built.
    /// This is equivalent to calling Subscribe() on the built consumer.
    /// </summary>
    /// <param name="topics">The topics to subscribe to.</param>
    public ConsumerBuilder<TKey, TValue> SubscribeTo(params string[] topics)
    {
        _topicsToSubscribe.AddRange(topics);
        return this;
    }

    /// <summary>
    /// Sets the interval for automatic offset commits.
    /// </summary>
    /// <param name="interval">The interval between automatic commits.</param>
    public ConsumerBuilder<TKey, TValue> WithAutoCommitInterval(TimeSpan interval)
    {
        _autoCommitIntervalMs = (int)interval.TotalMilliseconds;
        return this;
    }

    /// <summary>
    /// Sets the offset commit mode, controlling how offsets are stored and committed.
    /// </summary>
    /// <param name="mode">The offset commit mode to use.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><see cref="OffsetCommitMode.Auto"/>: Offsets committed automatically in the background (default)</description></item>
    /// <item><description><see cref="OffsetCommitMode.Manual"/>: You must call CommitAsync() explicitly</description></item>
    /// </list>
    /// </remarks>
    public ConsumerBuilder<TKey, TValue> WithOffsetCommitMode(OffsetCommitMode mode)
    {
        _offsetCommitMode = mode;
        return this;
    }

    public ConsumerBuilder<TKey, TValue> WithAutoOffsetReset(AutoOffsetReset autoOffsetReset)
    {
        _autoOffsetReset = autoOffsetReset;
        return this;
    }

    /// <summary>
    /// Sets the isolation level for transactional reads.
    /// </summary>
    /// <param name="isolationLevel">The isolation level to use.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><see cref="IsolationLevel.ReadUncommitted"/>: Read all records including uncommitted transactions (default)</description></item>
    /// <item><description><see cref="IsolationLevel.ReadCommitted"/>: Only read committed records, filtering out aborted transactional messages</description></item>
    /// </list>
    /// </remarks>
    public ConsumerBuilder<TKey, TValue> WithIsolationLevel(IsolationLevel isolationLevel)
    {
        _isolationLevel = isolationLevel;
        return this;
    }

    public ConsumerBuilder<TKey, TValue> WithMaxPollRecords(int maxPollRecords)
    {
        _maxPollRecords = maxPollRecords;
        return this;
    }

    /// <summary>
    /// Sets the minimum amount of data the server should return for a fetch request.
    /// If insufficient data is available, the request will wait up to <see cref="WithFetchMaxWait"/>
    /// before responding. Equivalent to Kafka's <c>fetch.min.bytes</c> configuration.
    /// Default is 1 byte.
    /// </summary>
    /// <param name="minBytes">The minimum number of bytes to return. Must be at least 1.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public ConsumerBuilder<TKey, TValue> WithFetchMinBytes(int minBytes)
    {
        if (minBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(minBytes), "Fetch min bytes must be at least 1");
        _fetchMinBytes = minBytes;
        return this;
    }

    /// <summary>
    /// Sets the maximum amount of data the server should return for a fetch request.
    /// Equivalent to Kafka's <c>fetch.max.bytes</c> configuration.
    /// Default is 52428800 (50 MiB).
    /// </summary>
    /// <param name="maxBytes">The maximum number of bytes to return. Must be at least 1.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public ConsumerBuilder<TKey, TValue> WithFetchMaxBytes(int maxBytes)
    {
        if (maxBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(maxBytes), "Fetch max bytes must be at least 1");
        _fetchMaxBytes = maxBytes;
        return this;
    }

    /// <summary>
    /// Sets the maximum amount of data per-partition the server should return for a fetch request.
    /// Equivalent to Kafka's <c>max.partition.fetch.bytes</c> configuration.
    /// Default is 1048576 (1 MiB).
    /// </summary>
    /// <param name="maxBytes">The maximum number of bytes per partition. Must be at least 1.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public ConsumerBuilder<TKey, TValue> WithMaxPartitionFetchBytes(int maxBytes)
    {
        if (maxBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(maxBytes), "Max partition fetch bytes must be at least 1");
        _maxPartitionFetchBytes = maxBytes;
        return this;
    }

    /// <summary>
    /// Sets the maximum time the server will block before responding to a fetch request
    /// if there isn't sufficient data to satisfy <see cref="WithFetchMinBytes"/>.
    /// Equivalent to Kafka's <c>fetch.max.wait.ms</c> configuration.
    /// Default is 500ms.
    /// </summary>
    /// <param name="maxWait">The maximum wait duration. Must be positive.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public ConsumerBuilder<TKey, TValue> WithFetchMaxWait(TimeSpan maxWait)
    {
        if (maxWait <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(maxWait), "Fetch max wait must be positive");
        _fetchMaxWaitMs = (int)maxWait.TotalMilliseconds;
        return this;
    }

    /// <summary>
    /// Sets the session timeout for consumer group membership.
    /// </summary>
    /// <param name="timeout">The session timeout duration.</param>
    public ConsumerBuilder<TKey, TValue> WithSessionTimeout(TimeSpan timeout)
    {
        _sessionTimeoutMs = (int)timeout.TotalMilliseconds;
        return this;
    }

    /// <summary>
    /// Sets the heartbeat interval for consumer group membership.
    /// The consumer sends heartbeats to the group coordinator at this interval to indicate
    /// it is alive. Must be lower than <see cref="WithSessionTimeout"/>.
    /// Equivalent to Kafka's <c>heartbeat.interval.ms</c>.
    /// Default is 3 seconds.
    /// </summary>
    /// <param name="interval">The heartbeat interval. Must be positive.</param>
    public ConsumerBuilder<TKey, TValue> WithHeartbeatInterval(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), "Heartbeat interval must be positive");

        _heartbeatIntervalMs = (int)interval.TotalMilliseconds;
        return this;
    }

    /// <summary>
    /// Enables TLS for secure connections.
    /// </summary>
    public ConsumerBuilder<TKey, TValue> UseTls()
    {
        _useTls = true;
        return this;
    }

    /// <summary>
    /// Configures TLS with custom settings.
    /// </summary>
    /// <param name="config">The TLS configuration.</param>
    public ConsumerBuilder<TKey, TValue> UseTls(TlsConfig config)
    {
        _useTls = true;
        _tlsConfig = config;
        return this;
    }

    /// <summary>
    /// Configures mutual TLS (mTLS) authentication using certificate files.
    /// </summary>
    /// <param name="caCertPath">Path to the CA certificate file (PEM format).</param>
    /// <param name="clientCertPath">Path to the client certificate file (PEM format).</param>
    /// <param name="clientKeyPath">Path to the client private key file (PEM format).</param>
    /// <param name="keyPassword">Optional password for the private key.</param>
    public ConsumerBuilder<TKey, TValue> UseMutualTls(
        string caCertPath,
        string clientCertPath,
        string clientKeyPath,
        string? keyPassword = null)
    {
        _useTls = true;
        _tlsConfig = TlsConfig.CreateMutualTls(caCertPath, clientCertPath, clientKeyPath, keyPassword);
        return this;
    }

    /// <summary>
    /// Configures mutual TLS (mTLS) authentication using in-memory certificates.
    /// </summary>
    /// <param name="clientCertificate">The client certificate with private key.</param>
    /// <param name="caCertificate">Optional CA certificate for server validation.</param>
    public ConsumerBuilder<TKey, TValue> UseMutualTls(
        X509Certificate2 clientCertificate,
        X509Certificate2? caCertificate = null)
    {
        _useTls = true;
        _tlsConfig = TlsConfig.CreateMutualTls(clientCertificate, caCertificate);
        return this;
    }

    public ConsumerBuilder<TKey, TValue> WithSaslPlain(string username, string password)
    {
        _saslMechanism = SaslMechanism.Plain;
        _saslUsername = username;
        _saslPassword = password;
        return this;
    }

    public ConsumerBuilder<TKey, TValue> WithSaslScramSha256(string username, string password)
    {
        _saslMechanism = SaslMechanism.ScramSha256;
        _saslUsername = username;
        _saslPassword = password;
        return this;
    }

    public ConsumerBuilder<TKey, TValue> WithSaslScramSha512(string username, string password)
    {
        _saslMechanism = SaslMechanism.ScramSha512;
        _saslUsername = username;
        _saslPassword = password;
        return this;
    }

    /// <summary>
    /// Configures GSSAPI (Kerberos) authentication.
    /// </summary>
    /// <param name="config">The GSSAPI configuration.</param>
    /// <returns>This builder for chaining.</returns>
    public ConsumerBuilder<TKey, TValue> WithGssapi(GssapiConfig config)
    {
        _saslMechanism = SaslMechanism.Gssapi;
        _gssapiConfig = config ?? throw new ArgumentNullException(nameof(config));
        return this;
    }

    /// <summary>
    /// Configures OAUTHBEARER authentication using OAuth 2.0 client credentials flow.
    /// </summary>
    /// <param name="config">The OAuth bearer configuration.</param>
    public ConsumerBuilder<TKey, TValue> WithOAuthBearer(OAuthBearerConfig config)
    {
        _saslMechanism = SaslMechanism.OAuthBearer;
        _oauthConfig = config ?? throw new ArgumentNullException(nameof(config));
        _oauthTokenProvider = null;
        return this;
    }

    /// <summary>
    /// Configures OAUTHBEARER authentication using a custom token provider.
    /// </summary>
    /// <param name="tokenProvider">A function that provides OAuth tokens on demand.</param>
    public ConsumerBuilder<TKey, TValue> WithOAuthBearer(Func<CancellationToken, ValueTask<OAuthBearerToken>> tokenProvider)
    {
        _saslMechanism = SaslMechanism.OAuthBearer;
        _oauthTokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        _oauthConfig = null;
        return this;
    }

    public ConsumerBuilder<TKey, TValue> WithKeyDeserializer(IDeserializer<TKey> deserializer)
    {
        _keyDeserializer = deserializer;
        return this;
    }

    public ConsumerBuilder<TKey, TValue> WithValueDeserializer(IDeserializer<TValue> deserializer)
    {
        _valueDeserializer = deserializer;
        return this;
    }

    public ConsumerBuilder<TKey, TValue> WithRebalanceListener(IRebalanceListener listener)
    {
        _rebalanceListener = listener;
        return this;
    }

    public ConsumerBuilder<TKey, TValue> WithLoggerFactory(Microsoft.Extensions.Logging.ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        return this;
    }

    /// <summary>
    /// Enables partition end-of-file (EOF) events.
    /// When enabled, the consumer will emit a special ConsumeResult with IsPartitionEof=true
    /// when it reaches the end of a partition (caught up to the high watermark).
    /// </summary>
    /// <param name="enabled">Whether to enable partition EOF events. Default is true.</param>
    public ConsumerBuilder<TKey, TValue> WithPartitionEof(bool enabled = true)
    {
        _enablePartitionEof = enabled;
        return this;
    }

    /// <summary>
    /// Sets the minimum number of messages to prefetch per partition.
    /// </summary>
    /// <remarks>
    /// <para>When set to 1, prefetching is disabled (fetch on demand).</para>
    /// <para>Higher values improve throughput by prefetching messages in the background.</para>
    /// <para>Default is 100000 (matching Confluent's <c>queued.min.messages</c>).</para>
    /// </remarks>
    /// <param name="count">Minimum messages to prefetch. Set to 1 to disable prefetching.</param>
    public ConsumerBuilder<TKey, TValue> WithQueuedMinMessages(int count)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "Queued min messages must be at least 1");
        _queuedMinMessages = count;
        return this;
    }

    /// <summary>
    /// Sets the interval for emitting statistics events.
    /// </summary>
    /// <param name="interval">The interval between statistics events. Must be positive.</param>
    public ConsumerBuilder<TKey, TValue> WithStatisticsInterval(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), "Statistics interval must be positive");

        _statisticsInterval = interval;
        return this;
    }

    /// <summary>
    /// Sets the handler for statistics events.
    /// </summary>
    /// <param name="handler">The handler to invoke when statistics are emitted.</param>
    public ConsumerBuilder<TKey, TValue> WithStatisticsHandler(Action<Statistics.ConsumerStatistics> handler)
    {
        _statisticsHandler = handler ?? throw new ArgumentNullException(nameof(handler));
        return this;
    }

    /// <summary>
    /// Sets the handler for statistics events.
    /// </summary>
    /// <param name="handler">The handler to invoke when statistics are emitted.</param>
    [Obsolete("Use WithStatisticsHandler instead.")]
    public ConsumerBuilder<TKey, TValue> OnStatistics(Action<Statistics.ConsumerStatistics> handler)
        => WithStatisticsHandler(handler);

    /// <summary>
    /// Sets the metadata recovery strategy for when all known brokers become unavailable.
    /// </summary>
    /// <param name="strategy">The recovery strategy to use.</param>
    public ConsumerBuilder<TKey, TValue> WithMetadataRecoveryStrategy(MetadataRecoveryStrategy strategy)
    {
        _metadataRecoveryStrategy = strategy;
        return this;
    }

    /// <summary>
    /// Sets how long to wait before triggering a rebootstrap when all known
    /// brokers are unavailable.
    /// </summary>
    /// <param name="trigger">The trigger delay. Default is 5 minutes.</param>
    public ConsumerBuilder<TKey, TValue> WithMetadataRecoveryRebootstrapTrigger(TimeSpan trigger)
    {
        _metadataRecoveryRebootstrapTriggerMs = (int)trigger.TotalMilliseconds;
        return this;
    }

    /// <summary>
    /// Sets the maximum age of metadata before it is refreshed.
    /// This controls how frequently the client refreshes its view of the cluster topology.
    /// Equivalent to Kafka's <c>metadata.max.age.ms</c> configuration.
    /// Default is 15 minutes.
    /// </summary>
    /// <param name="interval">The maximum age of metadata. Must be positive.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public ConsumerBuilder<TKey, TValue> WithMetadataMaxAge(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), "Metadata max age must be positive");

        _metadataMaxAge = interval;
        return this;
    }

    /// <summary>
    /// Configures the consumer for high throughput scenarios.
    /// </summary>
    /// <remarks>
    /// <para>Settings applied:</para>
    /// <list type="bullet">
    /// <item><description>MaxPollRecords: 1000 (larger batches)</description></item>
    /// <item><description>FetchMinBytes: 1KB (wait for more data)</description></item>
    /// <item><description>FetchMaxWaitMs: 500ms (allow batching)</description></item>
    /// </list>
    /// <para>These settings can be overridden by calling other builder methods after this one.</para>
    /// </remarks>
    /// <returns>The builder instance for method chaining.</returns>
    public ConsumerBuilder<TKey, TValue> ForHighThroughput()
    {
        _maxPollRecords = 1000;
        _fetchMinBytes = 1024;
        _fetchMaxWaitMs = 500;
        return this;
    }

    /// <summary>
    /// Configures the consumer for low latency scenarios.
    /// </summary>
    /// <remarks>
    /// <para>Settings applied:</para>
    /// <list type="bullet">
    /// <item><description>MaxPollRecords: 100 (smaller batches for faster processing)</description></item>
    /// <item><description>FetchMinBytes: 1 byte (return immediately when data available)</description></item>
    /// <item><description>FetchMaxWaitMs: 100ms (reduce waiting time)</description></item>
    /// </list>
    /// <para>These settings can be overridden by calling other builder methods after this one.</para>
    /// </remarks>
    /// <returns>The builder instance for method chaining.</returns>
    public ConsumerBuilder<TKey, TValue> ForLowLatency()
    {
        _maxPollRecords = 100;
        _fetchMinBytes = 1;
        _fetchMaxWaitMs = 100;
        return this;
    }

    /// <summary>
    /// Adds a consumer interceptor to the pipeline.
    /// Interceptors are called in the order they are added.
    /// </summary>
    /// <param name="interceptor">The interceptor to add.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public ConsumerBuilder<TKey, TValue> AddInterceptor(IConsumerInterceptor<TKey, TValue> interceptor)
    {
        ArgumentNullException.ThrowIfNull(interceptor);
        _interceptors ??= [];
        _interceptors.Add(interceptor);
        return this;
    }

    /// <summary>
    /// Sets the application-level retry policy for message processing in hosted consumer services.
    /// </summary>
    /// <param name="retryPolicy">The retry policy to use.</param>
    public ConsumerBuilder<TKey, TValue> WithRetryPolicy(IRetryPolicy retryPolicy)
    {
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        return this;
    }

    /// <summary>
    /// Builds and initializes the consumer, ready for immediate use.
    /// This is equivalent to calling <see cref="Build"/> followed by <see cref="IKafkaConsumer{TKey,TValue}.InitializeAsync"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the initialization.</param>
    /// <returns>An initialized consumer ready to consume messages.</returns>
    public async ValueTask<IKafkaConsumer<TKey, TValue>> BuildAsync(
        CancellationToken cancellationToken = default)
    {
        var consumer = Build();
        try
        {
            await consumer.InitializeAsync(cancellationToken).ConfigureAwait(false);
            return consumer;
        }
        catch
        {
            await consumer.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public IKafkaConsumer<TKey, TValue> Build()
    {
        if (_bootstrapServers.Count == 0)
            throw new InvalidOperationException("Bootstrap servers must be specified. Call WithBootstrapServers() before Build().");

        var keyDeserializer = _keyDeserializer ?? GetDefaultDeserializer<TKey>();
        var valueDeserializer = _valueDeserializer ?? GetDefaultDeserializer<TValue>();

        ValidateGroupProtocolConfig();

        var options = new ConsumerOptions
        {
            BootstrapServers = _bootstrapServers,
            ClientId = _clientId,
            GroupId = _groupId,
            GroupInstanceId = _groupInstanceId,
            GroupProtocol = _groupProtocol,
            GroupRemoteAssignor = _groupRemoteAssignor,
            OffsetCommitMode = _offsetCommitMode,
            AutoCommitIntervalMs = _autoCommitIntervalMs,
            AutoOffsetReset = _autoOffsetReset,
            FetchMinBytes = _fetchMinBytes,
            FetchMaxBytes = _fetchMaxBytes,
            MaxPartitionFetchBytes = _maxPartitionFetchBytes,
            FetchMaxWaitMs = _fetchMaxWaitMs,
            MaxPollRecords = _maxPollRecords,
            SessionTimeoutMs = _sessionTimeoutMs,
            HeartbeatIntervalMs = _heartbeatIntervalMs ?? 3000,
            PartitionAssignmentStrategy = _partitionAssignmentStrategy,
            CustomPartitionAssignmentStrategy = _customPartitionAssignmentStrategy,
            UseTls = _useTls,
            TlsConfig = _tlsConfig,
            SaslMechanism = _saslMechanism,
            SaslUsername = _saslUsername,
            SaslPassword = _saslPassword,
            GssapiConfig = _gssapiConfig,
            OAuthBearerConfig = _oauthConfig,
            OAuthBearerTokenProvider = _oauthTokenProvider,
            RebalanceListener = _rebalanceListener,
            EnablePartitionEof = _enablePartitionEof,
            StatisticsInterval = _statisticsInterval,
            StatisticsHandler = _statisticsHandler,
            QueuedMinMessages = _queuedMinMessages,
            IsolationLevel = _isolationLevel,
            MetadataRecoveryStrategy = _metadataRecoveryStrategy,
            MetadataRecoveryRebootstrapTriggerMs = _metadataRecoveryRebootstrapTriggerMs,
            Interceptors = _interceptors?.Count > 0 ? _interceptors.ToArray() : null,
            RetryPolicy = _retryPolicy
        };

        var metadataOptions = _metadataMaxAge.HasValue
            ? new MetadataOptions { MetadataRefreshInterval = _metadataMaxAge.Value }
            : null;

        var consumer = new KafkaConsumer<TKey, TValue>(options, keyDeserializer, valueDeserializer, _loggerFactory, metadataOptions);

        if (_topicsToSubscribe.Count > 0)
        {
            consumer.Subscribe(_topicsToSubscribe.ToArray());
        }

        return consumer;
    }

    private void ValidateGroupProtocolConfig()
    {
        if (_groupRemoteAssignor is not null && _groupProtocol != GroupProtocol.Consumer)
        {
            throw new InvalidOperationException(
                "GroupRemoteAssignor can only be set when using GroupProtocol.Consumer. " +
                "Call WithGroupProtocol(GroupProtocol.Consumer) to use server-side assignment.");
        }
    }

    private static IDeserializer<T> GetDefaultDeserializer<T>()
    {
        if (typeof(T) == typeof(string))
            return (IDeserializer<T>)(object)Serializers.String;
        if (typeof(T) == typeof(byte[]))
            return (IDeserializer<T>)(object)Serializers.ByteArray;
        if (typeof(T) == typeof(ReadOnlyMemory<byte>))
            return (IDeserializer<T>)(object)Serializers.RawBytes;
        if (typeof(T) == typeof(int))
            return (IDeserializer<T>)(object)Serializers.Int32;
        if (typeof(T) == typeof(long))
            return (IDeserializer<T>)(object)Serializers.Int64;
        if (typeof(T) == typeof(Guid))
            return (IDeserializer<T>)(object)Serializers.Guid;
        if (typeof(T) == typeof(Ignore))
            return (IDeserializer<T>)(object)Serializers.Ignore;

        throw new InvalidOperationException($"No default deserializer for type {typeof(T)}. Please specify a deserializer.");
    }
}
