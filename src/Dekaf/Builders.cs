using System.Security.Cryptography.X509Certificates;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Producer;
using Dekaf.Security;
using Dekaf.Security.Sasl;
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
    private bool _enableIdempotence;
    private string? _transactionalId;
    private Protocol.Records.CompressionType _compressionType = Protocol.Records.CompressionType.None;
    private int? _compressionLevel;
    private PartitionerType _partitionerType = PartitionerType.Default;
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
    private List<IProducerInterceptor<TKey, TValue>>? _interceptors;
    private TimeSpan? _metadataMaxAge;

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

    public ProducerBuilder<TKey, TValue> EnableIdempotence()
    {
        _enableIdempotence = true;
        return this;
    }

    public ProducerBuilder<TKey, TValue> WithTransactionalId(string transactionalId)
    {
        _transactionalId = transactionalId;
        _enableIdempotence = true;
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
    /// <item><description>EnableIdempotence: true (exactly-once semantics)</description></item>
    /// </list>
    /// <para>These settings can be overridden by calling other builder methods after this one.</para>
    /// </remarks>
    /// <returns>The builder instance for method chaining.</returns>
    public ProducerBuilder<TKey, TValue> ForReliability()
    {
        _acks = Acks.All;
        _enableIdempotence = true;
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

    public IKafkaProducer<TKey, TValue> Build()
    {
        if (_bootstrapServers.Count == 0)
            throw new InvalidOperationException("Bootstrap servers must be specified. Call WithBootstrapServers() before Build().");

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
            EnableIdempotence = _enableIdempotence,
            TransactionalId = _transactionalId,
            CompressionType = _compressionType,
            CompressionLevel = _compressionLevel,
            Partitioner = _partitionerType,
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
            Interceptors = _interceptors?.Count > 0 ? _interceptors.ToArray() : null
        };

        var metadataOptions = _metadataMaxAge.HasValue
            ? new Metadata.MetadataOptions { MetadataRefreshInterval = _metadataMaxAge.Value }
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
    private int _fetchMaxWaitMs = 500;
    private int _maxPollRecords = 500;
    private int _sessionTimeoutMs = 45000;
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

    public ConsumerBuilder<TKey, TValue> WithMaxPollRecords(int maxPollRecords)
    {
        _maxPollRecords = maxPollRecords;
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
            FetchMaxWaitMs = _fetchMaxWaitMs,
            MaxPollRecords = _maxPollRecords,
            SessionTimeoutMs = _sessionTimeoutMs,
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
            MetadataRecoveryStrategy = _metadataRecoveryStrategy,
            MetadataRecoveryRebootstrapTriggerMs = _metadataRecoveryRebootstrapTriggerMs,
            Interceptors = _interceptors?.Count > 0 ? _interceptors.ToArray() : null
        };

        var metadataOptions = _metadataMaxAge.HasValue
            ? new Metadata.MetadataOptions { MetadataRefreshInterval = _metadataMaxAge.Value }
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
