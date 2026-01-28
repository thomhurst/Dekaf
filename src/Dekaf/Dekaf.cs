using System.Security.Cryptography.X509Certificates;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Security;
using Dekaf.Security.Sasl;
using Dekaf.Serialization;

namespace Dekaf;

/// <summary>
/// Main entry point for creating Kafka clients.
/// </summary>
public static class Dekaf
{
    /// <summary>
    /// Creates a producer builder.
    /// </summary>
    public static ProducerBuilder<TKey, TValue> CreateProducer<TKey, TValue>()
    {
        return new ProducerBuilder<TKey, TValue>();
    }

    /// <summary>
    /// Creates a producer with the specified bootstrap servers.
    /// </summary>
    /// <param name="bootstrapServers">Comma-separated list of bootstrap servers.</param>
    public static IKafkaProducer<TKey, TValue> CreateProducer<TKey, TValue>(string bootstrapServers)
    {
        return new ProducerBuilder<TKey, TValue>()
            .WithBootstrapServers(bootstrapServers)
            .Build();
    }

    /// <summary>
    /// Creates a topic-specific producer with the specified bootstrap servers and topic.
    /// </summary>
    /// <param name="bootstrapServers">Comma-separated list of bootstrap servers.</param>
    /// <param name="topic">The topic to bind the producer to.</param>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <returns>A producer bound to the specified topic.</returns>
    public static ITopicProducer<TKey, TValue> CreateTopicProducer<TKey, TValue>(string bootstrapServers, string topic)
    {
        return new ProducerBuilder<TKey, TValue>()
            .WithBootstrapServers(bootstrapServers)
            .BuildForTopic(topic);
    }

    /// <summary>
    /// Creates a consumer builder.
    /// </summary>
    public static ConsumerBuilder<TKey, TValue> CreateConsumer<TKey, TValue>()
    {
        return new ConsumerBuilder<TKey, TValue>();
    }

    /// <summary>
    /// Creates a consumer with the specified bootstrap servers and group ID.
    /// </summary>
    /// <param name="bootstrapServers">Comma-separated list of bootstrap servers.</param>
    /// <param name="groupId">The consumer group ID.</param>
    public static IKafkaConsumer<TKey, TValue> CreateConsumer<TKey, TValue>(string bootstrapServers, string groupId)
    {
        return new ConsumerBuilder<TKey, TValue>()
            .WithBootstrapServers(bootstrapServers)
            .WithGroupId(groupId)
            .Build();
    }
}

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
    private int _batchSize = 16384;
    private bool _enableIdempotence;
    private string? _transactionalId;
    private Protocol.Records.CompressionType _compressionType = Protocol.Records.CompressionType.None;
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

    public ProducerBuilder<TKey, TValue> WithLingerMs(int lingerMs)
    {
        _lingerMs = lingerMs;
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

    public ProducerBuilder<TKey, TValue> UseZstdCompression()
    {
        _compressionType = Protocol.Records.CompressionType.Zstd;
        return this;
    }

    public ProducerBuilder<TKey, TValue> UseLz4Compression()
    {
        _compressionType = Protocol.Records.CompressionType.Lz4;
        return this;
    }

    public ProducerBuilder<TKey, TValue> UseGzipCompression()
    {
        _compressionType = Protocol.Records.CompressionType.Gzip;
        return this;
    }

    public ProducerBuilder<TKey, TValue> UseSnappyCompression()
    {
        _compressionType = Protocol.Records.CompressionType.Snappy;
        return this;
    }

    public ProducerBuilder<TKey, TValue> UseCompression(Protocol.Records.CompressionType compressionType)
    {
        _compressionType = compressionType;
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
    /// Configures the producer for high throughput scenarios.
    /// </summary>
    /// <remarks>
    /// <para>Settings applied:</para>
    /// <list type="bullet">
    /// <item><description>Acks: Leader only (faster acknowledgment)</description></item>
    /// <item><description>LingerMs: 5ms (allows batching)</description></item>
    /// <item><description>BatchSize: 64KB (larger batches)</description></item>
    /// <item><description>Compression: LZ4 (fast compression)</description></item>
    /// </list>
    /// <para>These settings can be overridden by calling other builder methods after this one.</para>
    /// </remarks>
    /// <returns>The builder instance for method chaining.</returns>
    public ProducerBuilder<TKey, TValue> ForHighThroughput()
    {
        _acks = Acks.Leader;
        _lingerMs = 5;
        _batchSize = 65536;
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
    /// <item><description>BatchSize: 16KB (default, smaller batches)</description></item>
    /// </list>
    /// <para>These settings can be overridden by calling other builder methods after this one.</para>
    /// </remarks>
    /// <returns>The builder instance for method chaining.</returns>
    public ProducerBuilder<TKey, TValue> ForLowLatency()
    {
        _acks = Acks.Leader;
        _lingerMs = 0;
        _batchSize = 16384;
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
            EnableIdempotence = _enableIdempotence,
            TransactionalId = _transactionalId,
            CompressionType = _compressionType,
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
            StatisticsHandler = _statisticsHandler
        };

        return new KafkaProducer<TKey, TValue>(options, keySerializer, valueSerializer, _loggerFactory);
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
    private OffsetCommitMode _offsetCommitMode = OffsetCommitMode.Auto;
    private int _autoCommitIntervalMs = 5000;
    private AutoOffsetReset _autoOffsetReset = AutoOffsetReset.Latest;
    private int _fetchMinBytes = 1;
    private int _fetchMaxWaitMs = 500;
    private int _maxPollRecords = 500;
    private int _sessionTimeoutMs = 45000;
    private bool _useTls;
    private TlsConfig? _tlsConfig;
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
    private readonly List<string> _topicsToSubscribe = [];

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

    public ConsumerBuilder<TKey, TValue> WithAutoCommitInterval(int intervalMs)
    {
        _autoCommitIntervalMs = intervalMs;
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

    public ConsumerBuilder<TKey, TValue> WithSessionTimeout(int timeoutMs)
    {
        _sessionTimeoutMs = timeoutMs;
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

    public IKafkaConsumer<TKey, TValue> Build()
    {
        if (_bootstrapServers.Count == 0)
            throw new InvalidOperationException("Bootstrap servers must be specified. Call WithBootstrapServers() before Build().");

        var keyDeserializer = _keyDeserializer ?? GetDefaultDeserializer<TKey>();
        var valueDeserializer = _valueDeserializer ?? GetDefaultDeserializer<TValue>();

        var options = new ConsumerOptions
        {
            BootstrapServers = _bootstrapServers,
            ClientId = _clientId,
            GroupId = _groupId,
            GroupInstanceId = _groupInstanceId,
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
            StatisticsHandler = _statisticsHandler
        };

        var consumer = new KafkaConsumer<TKey, TValue>(options, keyDeserializer, valueDeserializer, _loggerFactory);

        if (_topicsToSubscribe.Count > 0)
        {
            consumer.Subscribe(_topicsToSubscribe.ToArray());
        }

        return consumer;
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
