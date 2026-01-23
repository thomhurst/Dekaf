using Dekaf.Consumer;
using Dekaf.Producer;
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
    /// Creates a consumer builder.
    /// </summary>
    public static ConsumerBuilder<TKey, TValue> CreateConsumer<TKey, TValue>()
    {
        return new ConsumerBuilder<TKey, TValue>();
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
    private SaslMechanism _saslMechanism = SaslMechanism.None;
    private string? _saslUsername;
    private string? _saslPassword;
    private GssapiConfig? _gssapiConfig;
    private ISerializer<TKey>? _keySerializer;
    private ISerializer<TValue>? _valueSerializer;
    private Microsoft.Extensions.Logging.ILoggerFactory? _loggerFactory;

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

    public ProducerBuilder<TKey, TValue> UseTls()
    {
        _useTls = true;
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

    public IKafkaProducer<TKey, TValue> Build()
    {
        if (_bootstrapServers.Count == 0)
            throw new InvalidOperationException("Bootstrap servers must be specified");

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
            SaslMechanism = _saslMechanism,
            SaslUsername = _saslUsername,
            SaslPassword = _saslPassword,
            GssapiConfig = _gssapiConfig
        };

        return new KafkaProducer<TKey, TValue>(options, keySerializer, valueSerializer, _loggerFactory);
    }

    private static ISerializer<T> GetDefaultSerializer<T>()
    {
        if (typeof(T) == typeof(string))
            return (ISerializer<T>)(object)Serializers.String;
        if (typeof(T) == typeof(byte[]))
            return (ISerializer<T>)(object)Serializers.ByteArray;
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
    private bool _enableAutoCommit = true;
    private int _autoCommitIntervalMs = 5000;
    private AutoOffsetReset _autoOffsetReset = AutoOffsetReset.Latest;
    private int _maxPollRecords = 500;
    private int _sessionTimeoutMs = 45000;
    private bool _useTls;
    private SaslMechanism _saslMechanism = SaslMechanism.None;
    private string? _saslUsername;
    private string? _saslPassword;
    private GssapiConfig? _gssapiConfig;
    private IDeserializer<TKey>? _keyDeserializer;
    private IDeserializer<TValue>? _valueDeserializer;
    private IRebalanceListener? _rebalanceListener;
    private Microsoft.Extensions.Logging.ILoggerFactory? _loggerFactory;

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

    public ConsumerBuilder<TKey, TValue> EnableAutoCommit(bool enable = true)
    {
        _enableAutoCommit = enable;
        return this;
    }

    public ConsumerBuilder<TKey, TValue> DisableAutoCommit()
    {
        _enableAutoCommit = false;
        return this;
    }

    public ConsumerBuilder<TKey, TValue> WithAutoCommitInterval(int intervalMs)
    {
        _autoCommitIntervalMs = intervalMs;
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

    public ConsumerBuilder<TKey, TValue> UseTls()
    {
        _useTls = true;
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

    public IKafkaConsumer<TKey, TValue> Build()
    {
        if (_bootstrapServers.Count == 0)
            throw new InvalidOperationException("Bootstrap servers must be specified");

        var keyDeserializer = _keyDeserializer ?? GetDefaultDeserializer<TKey>();
        var valueDeserializer = _valueDeserializer ?? GetDefaultDeserializer<TValue>();

        var options = new ConsumerOptions
        {
            BootstrapServers = _bootstrapServers,
            ClientId = _clientId,
            GroupId = _groupId,
            GroupInstanceId = _groupInstanceId,
            EnableAutoCommit = _enableAutoCommit,
            AutoCommitIntervalMs = _autoCommitIntervalMs,
            AutoOffsetReset = _autoOffsetReset,
            MaxPollRecords = _maxPollRecords,
            SessionTimeoutMs = _sessionTimeoutMs,
            UseTls = _useTls,
            SaslMechanism = _saslMechanism,
            SaslUsername = _saslUsername,
            SaslPassword = _saslPassword,
            GssapiConfig = _gssapiConfig,
            RebalanceListener = _rebalanceListener
        };

        return new KafkaConsumer<TKey, TValue>(options, keyDeserializer, valueDeserializer, _loggerFactory);
    }

    private static IDeserializer<T> GetDefaultDeserializer<T>()
    {
        if (typeof(T) == typeof(string))
            return (IDeserializer<T>)(object)Serializers.String;
        if (typeof(T) == typeof(byte[]))
            return (IDeserializer<T>)(object)Serializers.ByteArray;
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
