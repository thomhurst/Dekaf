namespace Dekaf;

using System.Net.Security;
using Admin;
using Dekaf.Consumer;
using Dekaf.Internal;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Retry;
using Dekaf.Security;
using Dekaf.Security.Sasl;
using Microsoft.Extensions.Logging;

/// <summary>
/// Root client that owns shared Kafka infrastructure for related producers, consumers, and admin clients.
/// </summary>
public sealed class KafkaClient : IAsyncDisposable
{
    private readonly KafkaClientInfrastructure _infrastructure;
    private int _disposed;

    internal KafkaClient(KafkaClientInfrastructure infrastructure)
    {
        _infrastructure = infrastructure;
    }

    public ProducerBuilder<TKey, TValue> CreateProducer<TKey, TValue>()
    {
        ThrowIfDisposed();
        return new ProducerBuilder<TKey, TValue>(_infrastructure);
    }

    public ConsumerBuilder<TKey, TValue> CreateConsumer<TKey, TValue>()
    {
        ThrowIfDisposed();
        return new ConsumerBuilder<TKey, TValue>(_infrastructure);
    }

    public ConsumerBuilder<TKey, TValue> CreateConsumer<TKey, TValue>(string groupId)
    {
        return CreateConsumer<TKey, TValue>().WithGroupId(groupId);
    }

    public ShareConsumerBuilder<TKey, TValue> CreateShareConsumer<TKey, TValue>()
    {
        ThrowIfDisposed();
        return new ShareConsumerBuilder<TKey, TValue>(_infrastructure);
    }

    public ShareConsumerBuilder<TKey, TValue> CreateShareConsumer<TKey, TValue>(string groupId)
    {
        return CreateShareConsumer<TKey, TValue>().WithGroupId(groupId);
    }

    public AdminClientBuilder CreateAdminClient()
    {
        ThrowIfDisposed();
        return new AdminClientBuilder(_infrastructure);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        await _infrastructure.DisposeAsync().ConfigureAwait(false);
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(KafkaClient));
    }
}
/// <summary>
/// Builder for <see cref="KafkaClient"/>.
/// </summary>
public sealed class KafkaClientBuilder
{
    private IReadOnlyList<string> _bootstrapServers = [];
    private string? _clientId;
    private int _requestTimeoutMs = 30000;
    private int _retryBackoffMs = 100;
    private int _retryBackoffMaxMs = 1000;
    private int _reconnectBackoffMs = 50;
    private int _reconnectBackoffMaxMs = 1000;
    private bool _reconnectBackoffConfigured;
    private bool _reconnectBackoffMaxConfigured;
    private bool _useTls;
    private TlsConfig? _tlsConfig;
    private SaslMechanism _saslMechanism = SaslMechanism.None;
    private string? _saslUsername;
    private string? _saslPassword;
    private Func<CancellationToken, ValueTask<SaslCredentials>>? _saslCredentialProvider;
    private bool _saslScramTokenAuth;
    private GssapiConfig? _gssapiConfig;
    private OAuthBearerConfig? _oauthConfig;
    private Func<CancellationToken, ValueTask<OAuthBearerToken>>? _oauthTokenProvider;
    private AwsMskIamConfig? _awsMskIamConfig;
    private int _socketSendBufferBytes;
    private int _socketReceiveBufferBytes;
    private int _connectionsPerBroker = 1;
    private int _maxInFlightRequestsPerConnection = 5;
    private int _maxConnectionsPerBroker = ConsumerOptions.DefaultMaxConnectionsPerBroker;
    private int _producerMaxConnectionsPerBroker = ProducerOptions.DefaultMaxConnectionsPerBroker;
    private bool _isMaxConnectionsPerBrokerConfigured;
    private int _connectionsMaxIdleMs = ConnectionOptions.DefaultConnectionsMaxIdleMs;
    private TimeSpan _connectionTimeout = ConnectionOptions.DefaultConnectionTimeout;
    private bool _enableTcpKeepAlive = ConnectionOptions.DefaultEnableTcpKeepAlive;
    private TimeSpan _tcpKeepAliveTime = ConnectionOptions.DefaultTcpKeepAliveTime;
    private TimeSpan _tcpKeepAliveInterval = ConnectionOptions.DefaultTcpKeepAliveInterval;
    private int _tcpKeepAliveRetryCount = ConnectionOptions.DefaultTcpKeepAliveRetryCount;
    private RemoteCertificateValidationCallback? _remoteCertificateValidationCallback;
    private TimeSpan? _metadataMaxAge;
    private MetadataRecoveryStrategy _metadataRecoveryStrategy = MetadataRecoveryStrategy.Rebootstrap;
    private bool _metadataClusterCheckEnabled = true;
    private int _metadataRecoveryRebootstrapTriggerMs = 300000;
    private ClientDnsLookup _clientDnsLookup = ClientDnsLookup.UseAllDnsIps;
    private ulong? _memoryBudgetBytes;
    private ILoggerFactory? _loggerFactory;

    public KafkaClientBuilder WithBootstrapServers(string servers)
    {
        _bootstrapServers = servers.Split(',').Select(s => s.Trim()).ToArray();
        return this;
    }

    public KafkaClientBuilder WithBootstrapServers(params string[] servers)
    {
        _bootstrapServers = [.. servers];
        return this;
    }

    public KafkaClientBuilder WithClientId(string clientId)
    {
        _clientId = clientId;
        return this;
    }

    public KafkaClientBuilder WithRequestTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Request timeout must be positive");
        ArgumentOutOfRangeException.ThrowIfGreaterThan(timeout.TotalMilliseconds, int.MaxValue, nameof(timeout));

        _requestTimeoutMs = (int)timeout.TotalMilliseconds;
        return this;
    }

    /// <summary>
    /// Sets the initial delay for retrying failed broker requests for clients sharing this connection.
    /// Equivalent to Kafka's <c>retry.backoff.ms</c>.
    /// </summary>
    public KafkaClientBuilder WithRetryBackoff(TimeSpan backoff)
    {
        if (backoff < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(backoff), "Retry backoff cannot be negative");
        ArgumentOutOfRangeException.ThrowIfGreaterThan(backoff.TotalMilliseconds, int.MaxValue, nameof(backoff));
        _retryBackoffMs = (int)backoff.TotalMilliseconds;
        return this;
    }

    /// <summary>
    /// Sets the maximum delay for retrying repeatedly failed broker requests for shared clients.
    /// Equivalent to Kafka's <c>retry.backoff.max.ms</c>.
    /// </summary>
    public KafkaClientBuilder WithRetryBackoffMax(TimeSpan backoff)
    {
        if (backoff < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(backoff), "Maximum retry backoff cannot be negative");
        ArgumentOutOfRangeException.ThrowIfGreaterThan(backoff.TotalMilliseconds, int.MaxValue, nameof(backoff));
        _retryBackoffMaxMs = (int)backoff.TotalMilliseconds;
        return this;
    }

    /// <summary>
    /// Sets the initial delay before reconnecting to a broker after a connection failure.
    /// Equivalent to Kafka's <c>reconnect.backoff.ms</c>. Set to zero to disable the delay.
    /// When the maximum is not configured, it uses this value and disables exponential growth.
    /// </summary>
    /// <param name="backoff">The reconnect backoff. Cannot be negative.</param>
    public KafkaClientBuilder WithReconnectBackoff(TimeSpan backoff)
    {
        _reconnectBackoffMs = ReconnectBackoffValidation.ToMilliseconds(
            backoff,
            nameof(backoff),
            "Reconnect backoff cannot be negative");
        _reconnectBackoffConfigured = true;
        return this;
    }

    /// <summary>
    /// Sets the maximum delay before reconnecting to a broker after repeated connection failures.
    /// Equivalent to Kafka's <c>reconnect.backoff.max.ms</c>.
    /// </summary>
    /// <param name="backoff">The maximum reconnect backoff. Cannot be negative.</param>
    public KafkaClientBuilder WithReconnectBackoffMax(TimeSpan backoff)
    {
        _reconnectBackoffMaxMs = ReconnectBackoffValidation.ToMilliseconds(
            backoff,
            nameof(backoff),
            "Maximum reconnect backoff cannot be negative");
        _reconnectBackoffMaxConfigured = true;
        return this;
    }

    public KafkaClientBuilder UseTls()
    {
        _useTls = true;
        return this;
    }

    public KafkaClientBuilder UseTls(TlsConfig config)
    {
        _useTls = true;
        _tlsConfig = config;
        return this;
    }

    /// <summary>
    /// Sets the maximum time allowed for socket connection setup, including TLS/SASL handshakes.
    /// Equivalent to Kafka's <c>socket.connection.setup.timeout.ms</c>.
    /// </summary>
    /// <param name="timeout">The connection setup timeout. Must be positive.</param>
    public KafkaClientBuilder WithConnectionTimeout(TimeSpan timeout)
    {
        _connectionTimeout = ConnectionOptionValidation.ValidatePositiveTimeout(
            timeout,
            nameof(timeout),
            "Connection timeout must be positive");
        return this;
    }

    /// <summary>
    /// Enables or disables TCP keepalive on broker sockets.
    /// Equivalent to Kafka's <c>socket.keepalive.enable</c>.
    /// </summary>
    public KafkaClientBuilder WithTcpKeepAlive(bool enabled = true)
    {
        _enableTcpKeepAlive = enabled;
        return this;
    }

    /// <summary>
    /// Configures TCP keepalive probe timing on broker sockets and enables TCP keepalive.
    /// Unsupported platforms ignore individual probe options.
    /// </summary>
    public KafkaClientBuilder WithTcpKeepAlive(
        TimeSpan time,
        TimeSpan interval,
        int retryCount = ConnectionOptions.DefaultTcpKeepAliveRetryCount)
    {
        ConnectionOptionValidation.ValidateTcpKeepAlive(time, interval, retryCount);
        _enableTcpKeepAlive = true;
        _tcpKeepAliveTime = time;
        _tcpKeepAliveInterval = interval;
        _tcpKeepAliveRetryCount = retryCount;
        return this;
    }

    /// <summary>
    /// Sets a custom TLS certificate validation callback and enables TLS.
    /// </summary>
    public KafkaClientBuilder WithRemoteCertificateValidationCallback(RemoteCertificateValidationCallback callback)
    {
        _useTls = true;
        _remoteCertificateValidationCallback = callback ?? throw new ArgumentNullException(nameof(callback));
        return this;
    }

    public KafkaClientBuilder WithSaslPlain(string username, string password)
    {
        _saslMechanism = SaslMechanism.Plain;
        _saslUsername = username;
        _saslPassword = password;
        _saslCredentialProvider = null;
        _saslScramTokenAuth = false;
        return this;
    }

    public KafkaClientBuilder WithSaslPlain(
        Func<CancellationToken, ValueTask<SaslCredentials>> credentialProvider)
    {
        _saslMechanism = SaslMechanism.Plain;
        _saslCredentialProvider = credentialProvider ?? throw new ArgumentNullException(nameof(credentialProvider));
        _saslUsername = null;
        _saslPassword = null;
        _saslScramTokenAuth = false;
        return this;
    }

    public KafkaClientBuilder WithSaslScramSha256(string username, string password)
    {
        _saslMechanism = SaslMechanism.ScramSha256;
        _saslUsername = username;
        _saslPassword = password;
        _saslCredentialProvider = null;
        _saslScramTokenAuth = false;
        return this;
    }

    public KafkaClientBuilder WithSaslScramSha256(
        Func<CancellationToken, ValueTask<SaslCredentials>> credentialProvider)
    {
        _saslMechanism = SaslMechanism.ScramSha256;
        _saslCredentialProvider = credentialProvider ?? throw new ArgumentNullException(nameof(credentialProvider));
        _saslUsername = null;
        _saslPassword = null;
        _saslScramTokenAuth = false;
        return this;
    }

    public KafkaClientBuilder WithSaslScramSha512(string username, string password)
    {
        _saslMechanism = SaslMechanism.ScramSha512;
        _saslUsername = username;
        _saslPassword = password;
        _saslCredentialProvider = null;
        _saslScramTokenAuth = false;
        return this;
    }

    public KafkaClientBuilder WithSaslScramSha512(
        Func<CancellationToken, ValueTask<SaslCredentials>> credentialProvider)
    {
        _saslMechanism = SaslMechanism.ScramSha512;
        _saslCredentialProvider = credentialProvider ?? throw new ArgumentNullException(nameof(credentialProvider));
        _saslUsername = null;
        _saslPassword = null;
        _saslScramTokenAuth = false;
        return this;
    }

    public KafkaClientBuilder WithSaslScramSha256DelegationToken(string tokenId, string tokenHmac)
    {
        _saslMechanism = SaslMechanism.ScramSha256;
        _saslUsername = tokenId;
        _saslPassword = tokenHmac;
        _saslCredentialProvider = null;
        _saslScramTokenAuth = true;
        return this;
    }

    public KafkaClientBuilder WithSaslScramSha512DelegationToken(string tokenId, string tokenHmac)
    {
        _saslMechanism = SaslMechanism.ScramSha512;
        _saslUsername = tokenId;
        _saslPassword = tokenHmac;
        _saslCredentialProvider = null;
        _saslScramTokenAuth = true;
        return this;
    }

    public KafkaClientBuilder WithGssapi(GssapiConfig config)
    {
        _saslMechanism = SaslMechanism.Gssapi;
        _gssapiConfig = config ?? throw new ArgumentNullException(nameof(config));
        _saslScramTokenAuth = false;
        return this;
    }

    public KafkaClientBuilder WithOAuthBearer(OAuthBearerConfig config)
    {
        _saslMechanism = SaslMechanism.OAuthBearer;
        _oauthConfig = config ?? throw new ArgumentNullException(nameof(config));
        _oauthTokenProvider = null;
        _saslScramTokenAuth = false;
        return this;
    }

    public KafkaClientBuilder WithOAuthBearerJwtBearer(OAuthBearerJwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var oauthConfig = options.ToOAuthBearerConfig();
        _saslMechanism = SaslMechanism.OAuthBearer;
        _oauthConfig = oauthConfig;
        _oauthTokenProvider = null;
        _saslScramTokenAuth = false;
        return this;
    }

    public KafkaClientBuilder WithOAuthBearerJwtBearer(Action<OAuthBearerJwtBearerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new OAuthBearerJwtBearerOptions();
        configure(options);
        return WithOAuthBearerJwtBearer(options);
    }

    public KafkaClientBuilder WithOAuthBearerClientAssertion(OAuthBearerClientAssertionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var oauthConfig = options.ToOAuthBearerConfig();
        _saslMechanism = SaslMechanism.OAuthBearer;
        _oauthConfig = oauthConfig;
        _oauthTokenProvider = null;
        _saslScramTokenAuth = false;
        return this;
    }

    public KafkaClientBuilder WithOAuthBearerClientAssertion(Action<OAuthBearerClientAssertionOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new OAuthBearerClientAssertionOptions();
        configure(options);
        return WithOAuthBearerClientAssertion(options);
    }

    public KafkaClientBuilder WithOAuthBearerAzureImds(OAuthBearerAzureImdsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _saslMechanism = SaslMechanism.OAuthBearer;
        _oauthConfig = options.ToOAuthBearerConfig();
        _oauthTokenProvider = null;
        _saslScramTokenAuth = false;
        return this;
    }

    public KafkaClientBuilder WithOAuthBearerAzureImds(Action<OAuthBearerAzureImdsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new OAuthBearerAzureImdsOptions { Resource = string.Empty };
        configure(options);
        return WithOAuthBearerAzureImds(options);
    }

    public KafkaClientBuilder WithOAuthBearer(Func<CancellationToken, ValueTask<OAuthBearerToken>> tokenProvider)
    {
        _saslMechanism = SaslMechanism.OAuthBearer;
        _oauthTokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        _oauthConfig = null;
        _saslScramTokenAuth = false;
        return this;
    }

    /// <summary>
    /// Configures Amazon MSK IAM authentication using the AWS_MSK_IAM SASL mechanism.
    /// </summary>
    /// <param name="config">Optional AWS_MSK_IAM configuration. Defaults to the AWS credential chain and broker-derived region.</param>
    public KafkaClientBuilder WithAwsMskIam(AwsMskIamConfig? config = null)
    {
        _saslMechanism = SaslMechanism.AwsMskIam;
        _awsMskIamConfig = config ?? new AwsMskIamConfig();
        _saslScramTokenAuth = false;
        return this;
    }

    public KafkaClientBuilder WithSocketSendBufferBytes(int bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        _socketSendBufferBytes = bytes;
        return this;
    }

    public KafkaClientBuilder WithSocketReceiveBufferBytes(int bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        _socketReceiveBufferBytes = bytes;
        return this;
    }

    public KafkaClientBuilder WithConnectionsPerBroker(int connectionsPerBroker)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(connectionsPerBroker, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(connectionsPerBroker, 32);
        _connectionsPerBroker = connectionsPerBroker;
        if (!_isMaxConnectionsPerBrokerConfigured)
        {
            _maxConnectionsPerBroker = Math.Max(_maxConnectionsPerBroker, connectionsPerBroker);
            _producerMaxConnectionsPerBroker = Math.Max(_producerMaxConnectionsPerBroker, connectionsPerBroker);
        }
        return this;
    }

    public KafkaClientBuilder WithMaxInFlightRequestsPerConnection(int maxInFlightRequestsPerConnection)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxInFlightRequestsPerConnection, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxInFlightRequestsPerConnection, 1000000);
        _maxInFlightRequestsPerConnection = maxInFlightRequestsPerConnection;
        return this;
    }

    public KafkaClientBuilder WithMaxConnectionsPerBroker(int maxConnectionsPerBroker)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConnectionsPerBroker, 1);
        _maxConnectionsPerBroker = maxConnectionsPerBroker;
        _producerMaxConnectionsPerBroker = maxConnectionsPerBroker;
        _isMaxConnectionsPerBrokerConfigured = true;
        return this;
    }

    /// <summary>
    /// Sets the maximum idle time before unused broker connections are closed.
    /// Equivalent to Kafka's <c>connections.max.idle.ms</c>. Use <see cref="Timeout.InfiniteTimeSpan"/> to disable.
    /// </summary>
    /// <param name="idle">The maximum idle time. Must be non-negative, or <see cref="Timeout.InfiniteTimeSpan"/>.</param>
    public KafkaClientBuilder WithConnectionsMaxIdle(TimeSpan idle)
    {
        _connectionsMaxIdleMs = ConnectionOptions.ToConnectionsMaxIdleMs(idle, nameof(idle));
        return this;
    }

    public KafkaClientBuilder WithMetadataMaxAge(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), "Metadata max age must be positive");

        _metadataMaxAge = interval;
        return this;
    }

    public KafkaClientBuilder WithMetadataRecoveryStrategy(MetadataRecoveryStrategy strategy)
    {
        _metadataRecoveryStrategy = strategy;
        return this;
    }

    /// <summary>
    /// Controls KIP-1242 cluster and broker identity checks on new connections.
    /// </summary>
    public KafkaClientBuilder WithMetadataClusterCheck(bool enabled = true)
    {
        _metadataClusterCheckEnabled = enabled;
        return this;
    }

    public KafkaClientBuilder WithMetadataRecoveryRebootstrapTrigger(TimeSpan trigger)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(trigger.TotalMilliseconds, int.MaxValue, nameof(trigger));
        _metadataRecoveryRebootstrapTriggerMs = (int)trigger.TotalMilliseconds;
        return this;
    }

    /// <summary>
    /// Sets how broker DNS names are resolved before connecting.
    /// </summary>
    public KafkaClientBuilder WithClientDnsLookup(ClientDnsLookup lookup)
    {
        _clientDnsLookup = lookup;
        return this;
    }

    public KafkaClientBuilder WithMemoryBudget(ulong bytes)
    {
        ArgumentOutOfRangeException.ThrowIfZero(bytes);
        _memoryBudgetBytes = bytes;
        return this;
    }

    public KafkaClientBuilder WithLoggerFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        return this;
    }

    public KafkaClient Build()
    {
        if (_bootstrapServers.Count == 0)
            throw new InvalidOperationException("Bootstrap servers must be specified. Call WithBootstrapServers() before Build().");
        if (_maxConnectionsPerBroker < _connectionsPerBroker)
            throw new InvalidOperationException(
                $"MaxConnectionsPerBroker ({_maxConnectionsPerBroker}) must be >= ConnectionsPerBroker ({_connectionsPerBroker}).");
        var reconnectBackoffMaxMs = ReconnectBackoffValidation.ResolveMaximumMilliseconds(
            _reconnectBackoffMs,
            _reconnectBackoffMaxMs,
            _reconnectBackoffConfigured,
            _reconnectBackoffMaxConfigured);
        ExponentialRetryBackoff.Validate(_retryBackoffMs, _retryBackoffMaxMs);

        GssapiConfig.ValidateForBuild(_saslMechanism, _gssapiConfig);

        var options = new KafkaClientOptions
        {
            BootstrapServers = _bootstrapServers,
            ClientId = _clientId,
            RequestTimeoutMs = _requestTimeoutMs,
            RetryBackoffMs = _retryBackoffMs,
            RetryBackoffMaxMs = _retryBackoffMaxMs,
            ReconnectBackoffMs = _reconnectBackoffMs,
            ReconnectBackoffMaxMs = reconnectBackoffMaxMs,
            UseTls = _useTls,
            TlsConfig = _tlsConfig,
            RemoteCertificateValidationCallback = _remoteCertificateValidationCallback,
            ConnectionTimeout = _connectionTimeout,
            EnableTcpKeepAlive = _enableTcpKeepAlive,
            TcpKeepAliveTime = _tcpKeepAliveTime,
            TcpKeepAliveInterval = _tcpKeepAliveInterval,
            TcpKeepAliveRetryCount = _tcpKeepAliveRetryCount,
            SaslMechanism = _saslMechanism,
            SaslUsername = _saslUsername,
            SaslPassword = _saslPassword,
            SaslCredentialProvider = _saslCredentialProvider,
            SaslScramTokenAuth = _saslScramTokenAuth,
            GssapiConfig = _gssapiConfig,
            OAuthBearerConfig = _oauthConfig,
            OAuthBearerTokenProvider = _oauthTokenProvider,
            AwsMskIamConfig = _awsMskIamConfig,
            SocketSendBufferBytes = _socketSendBufferBytes,
            SocketReceiveBufferBytes = _socketReceiveBufferBytes,
            ConnectionsPerBroker = _connectionsPerBroker,
            MaxInFlightRequestsPerConnection = _maxInFlightRequestsPerConnection,
            MaxConnectionsPerBroker = _maxConnectionsPerBroker,
            ProducerMaxConnectionsPerBroker = _producerMaxConnectionsPerBroker,
            ConnectionsMaxIdleMs = _connectionsMaxIdleMs,
            MetadataMaxAge = _metadataMaxAge,
            MetadataRecoveryStrategy = _metadataRecoveryStrategy,
            MetadataClusterCheckEnabled = _metadataClusterCheckEnabled,
            MetadataRecoveryRebootstrapTriggerMs = _metadataRecoveryRebootstrapTriggerMs,
            ClientDnsLookup = _clientDnsLookup,
            MemoryBudgetBytes = _memoryBudgetBytes,
            LoggerFactory = _loggerFactory
        };

        return new KafkaClient(KafkaClientInfrastructure.Create(options));
    }
}
internal sealed class KafkaClientOptions
{
    public required IReadOnlyList<string> BootstrapServers { get; init; }
    public string? ClientId { get; init; }
    public int RequestTimeoutMs { get; init; }
    public int RetryBackoffMs { get; init; }
    public int RetryBackoffMaxMs { get; init; }
    public int ReconnectBackoffMs { get; init; }
    public int ReconnectBackoffMaxMs { get; init; }
    public bool UseTls { get; init; }
    public TlsConfig? TlsConfig { get; init; }
    public RemoteCertificateValidationCallback? RemoteCertificateValidationCallback { get; init; }
    public TimeSpan ConnectionTimeout { get; init; } = ConnectionOptions.DefaultConnectionTimeout;
    public bool EnableTcpKeepAlive { get; init; } = ConnectionOptions.DefaultEnableTcpKeepAlive;
    public TimeSpan TcpKeepAliveTime { get; init; } = ConnectionOptions.DefaultTcpKeepAliveTime;
    public TimeSpan TcpKeepAliveInterval { get; init; } = ConnectionOptions.DefaultTcpKeepAliveInterval;
    public int TcpKeepAliveRetryCount { get; init; } = ConnectionOptions.DefaultTcpKeepAliveRetryCount;
    public SaslMechanism SaslMechanism { get; init; }
    public string? SaslUsername { get; init; }
    public string? SaslPassword { get; init; }
    public Func<CancellationToken, ValueTask<SaslCredentials>>? SaslCredentialProvider { get; init; }
    public bool SaslScramTokenAuth { get; init; }
    public GssapiConfig? GssapiConfig { get; init; }
    public OAuthBearerConfig? OAuthBearerConfig { get; init; }
    public Func<CancellationToken, ValueTask<OAuthBearerToken>>? OAuthBearerTokenProvider { get; init; }
    public AwsMskIamConfig? AwsMskIamConfig { get; init; }
    public int SocketSendBufferBytes { get; init; }
    public int SocketReceiveBufferBytes { get; init; }
    public int ConnectionsPerBroker { get; init; }
    public int MaxInFlightRequestsPerConnection { get; init; }
    public int MaxConnectionsPerBroker { get; init; }
    public int ProducerMaxConnectionsPerBroker { get; init; }
    public int ConnectionsMaxIdleMs { get; init; }
    public TimeSpan? MetadataMaxAge { get; init; }
    public MetadataRecoveryStrategy MetadataRecoveryStrategy { get; init; }
    public bool MetadataClusterCheckEnabled { get; init; } = true;
    public int MetadataRecoveryRebootstrapTriggerMs { get; init; }
    public ClientDnsLookup ClientDnsLookup { get; init; }
    public ulong? MemoryBudgetBytes { get; init; }
    public ILoggerFactory? LoggerFactory { get; init; }
}

internal sealed class KafkaClientInfrastructure : IAsyncDisposable
{
    private const int SharedResponseBufferFetchMaxBytes = 200 * 1024 * 1024;

    private int _disposed;

    private KafkaClientInfrastructure(
        IReadOnlyList<string> bootstrapServers,
        ConnectionPool connectionPool,
        ConnectionPool consumerConnectionPool,
        MetadataManager metadataManager,
        IDekafMemoryBudget memoryBudget,
        ILoggerFactory? loggerFactory,
        int connectionsPerBroker,
        int maxConnectionsPerBroker,
        int producerMaxConnectionsPerBroker,
        int retryBackoffMs,
        int retryBackoffMaxMs,
        SaslMechanism saslMechanism,
        bool usesDynamicSaslCredentials)
    {
        BootstrapServers = bootstrapServers;
        ConnectionPool = connectionPool;
        ConsumerConnectionPool = consumerConnectionPool;
        MetadataManager = metadataManager;
        MemoryBudget = memoryBudget;
        LoggerFactory = loggerFactory;
        ConnectionsPerBroker = connectionsPerBroker;
        MaxConnectionsPerBroker = maxConnectionsPerBroker;
        ProducerMaxConnectionsPerBroker = producerMaxConnectionsPerBroker;
        RetryBackoffMs = retryBackoffMs;
        RetryBackoffMaxMs = retryBackoffMaxMs;
        SaslMechanism = saslMechanism;
        UsesDynamicSaslCredentials = usesDynamicSaslCredentials;
    }

    public IReadOnlyList<string> BootstrapServers { get; }
    public ConnectionPool ConnectionPool { get; }
    public ConnectionPool ConsumerConnectionPool { get; }
    public MetadataManager MetadataManager { get; }
    public IDekafMemoryBudget MemoryBudget { get; }
    public ILoggerFactory? LoggerFactory { get; }
    public int ConnectionsPerBroker { get; }
    public int MaxConnectionsPerBroker { get; }
    public int ProducerMaxConnectionsPerBroker { get; }
    public int RetryBackoffMs { get; }
    public int RetryBackoffMaxMs { get; }
    public SaslMechanism SaslMechanism { get; }
    public bool UsesDynamicSaslCredentials { get; }

    public static KafkaClientInfrastructure Create(KafkaClientOptions options)
    {
        var poolSizes = PoolSizing.ForSharedPools(
            brokerCount: options.BootstrapServers.Count,
            connectionsPerBroker: options.ConnectionsPerBroker,
            maxInFlightRequestsPerConnection: options.MaxInFlightRequestsPerConnection,
            batchSize: 1048576,
            maxConnectionsPerBroker: options.MaxConnectionsPerBroker);

        var connectionOptions = CreateConnectionOptions(options);
        var connectionPool = new ConnectionPool(
            options.ClientId,
            connectionOptions,
            options.LoggerFactory,
            options.ConnectionsPerBroker,
            ResponseBufferPool.Create(SharedResponseBufferFetchMaxBytes),
            pipeMemoryBucketCapacity: poolSizes.PipeMemoryArraysPerBucket);
        var consumerConnectionPool = new ConnectionPool(
            options.ClientId,
            connectionOptions,
            options.LoggerFactory,
            options.ConnectionsPerBroker,
            ResponseBufferPool.Create(SharedResponseBufferFetchMaxBytes),
            pipeMemoryBucketCapacity: poolSizes.PipeMemoryArraysPerBucket,
            responseMemoryAdmissionsEnabled: true);

        var metadataOptions = new MetadataOptions
        {
            MetadataRefreshInterval = options.MetadataMaxAge ?? TimeSpan.FromMinutes(15),
            MetadataRecoveryStrategy = options.MetadataRecoveryStrategy,
            MetadataClusterCheckEnabled = options.MetadataClusterCheckEnabled,
            MetadataRecoveryRebootstrapTriggerMs = options.MetadataRecoveryRebootstrapTriggerMs,
            ClientDnsLookup = options.ClientDnsLookup,
            RetryBackoffMs = options.RetryBackoffMs,
            RetryBackoffMaxMs = options.RetryBackoffMaxMs
        };

        var metadataManager = new MetadataManager(
            connectionPool,
            options.BootstrapServers,
            options: metadataOptions,
            logger: options.LoggerFactory?.CreateLogger<MetadataManager>());
        metadataManager.SetAdditionalBrokerRegistrationTarget(consumerConnectionPool);

        return new KafkaClientInfrastructure(
            options.BootstrapServers,
            connectionPool,
            consumerConnectionPool,
            metadataManager,
            new ClientMemoryBudget(options.MemoryBudgetBytes),
            options.LoggerFactory,
            options.ConnectionsPerBroker,
            options.MaxConnectionsPerBroker,
            options.ProducerMaxConnectionsPerBroker,
            options.RetryBackoffMs,
            options.RetryBackoffMaxMs,
            options.SaslMechanism,
            options.SaslCredentialProvider is not null);
    }

    private static ConnectionOptions CreateConnectionOptions(KafkaClientOptions options) => new()
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
        ReconnectBackoffMax = TimeSpan.FromMilliseconds(options.ReconnectBackoffMaxMs),
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
        ClientDnsLookup = options.ClientDnsLookup
    };

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        await MetadataManager.DisposeAsync().ConfigureAwait(false);
        await ConsumerConnectionPool.DisposeAsync().ConfigureAwait(false);
        await ConnectionPool.DisposeAsync().ConfigureAwait(false);
    }
}
