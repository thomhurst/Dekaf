using Dekaf.Networking;
using Dekaf.Retry;
using Dekaf.Security;
using Dekaf.Security.Sasl;

namespace Dekaf.ShareConsumer;

/// <summary>
/// Configuration options for the Kafka share consumer (KIP-932).
/// Share consumers use queue-semantics with record-level acknowledgement.
/// </summary>
public sealed class ShareConsumerOptions
{
    /// <summary>
    /// Bootstrap servers (host:port,host:port).
    /// </summary>
    public required IReadOnlyList<string> BootstrapServers { get; init; }

    /// <summary>
    /// Client ID.
    /// </summary>
    public string? ClientId { get; init; } = "dekaf-share-consumer";

    /// <summary>
    /// Share group ID. Required for share consumers.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// The rack ID of the consumer, used for rack-aware assignment.
    /// </summary>
    public string? RackId { get; init; }

    /// <summary>
    /// Minimum number of bytes the broker should accumulate before returning a fetch response.
    /// </summary>
    public int FetchMinBytes { get; init; } = 1;

    /// <summary>
    /// Maximum number of bytes the broker will return in a fetch response.
    /// </summary>
    public int FetchMaxBytes { get; init; } = 52428800; // 50 MiB

    /// <summary>
    /// Maximum number of bytes per partition in a fetch response.
    /// </summary>
    public int MaxPartitionFetchBytes { get; init; } = 1048576; // 1 MiB

    /// <summary>
    /// Maximum time in milliseconds the broker will wait for FetchMinBytes to be satisfied.
    /// </summary>
    public int FetchMaxWaitMs { get; init; } = 200;

    /// <summary>
    /// Maximum number of records returned per poll.
    /// </summary>
    public int MaxPollRecords { get; init; } = 500;

    /// <summary>
    /// Session timeout in milliseconds. The coordinator will remove the member if no heartbeat
    /// is received within this interval.
    /// </summary>
    public int SessionTimeoutMs { get; init; } = 45000;

    /// <summary>
    /// Initial heartbeat interval in milliseconds. The broker may adjust this via heartbeat responses.
    /// </summary>
    public int HeartbeatIntervalMs { get; init; } = 3000;

    /// <summary>
    /// Request timeout in milliseconds for protocol requests.
    /// </summary>
    public int RequestTimeoutMs { get; init; } = 30000;

    /// <summary>
    /// Initial delay in milliseconds before reconnecting to a broker after a connection failure.
    /// Equivalent to Kafka's <c>reconnect.backoff.ms</c>. Set to 0 to disable the delay.
    /// </summary>
    public int ReconnectBackoffMs { get; init; } = 50;

    /// <summary>
    /// Maximum delay in milliseconds before reconnecting to a broker after repeated failures.
    /// Equivalent to Kafka's <c>reconnect.backoff.max.ms</c>.
    /// </summary>
    public int ReconnectBackoffMaxMs { get; init; } = 1000;

    /// <summary>
    /// Maximum idle time in milliseconds before unused broker connections are closed.
    /// Default is 9 minutes, below Kafka's default broker-side 10 minute idle timeout.
    /// Set to -1 to disable client-side idle connection reaping.
    /// </summary>
    public int ConnectionsMaxIdleMs { get; init; } = ConnectionOptions.DefaultConnectionsMaxIdleMs;

    /// <summary>
    /// Whether to use TLS for broker connections.
    /// </summary>
    public bool UseTls { get; init; }

    /// <summary>
    /// Custom TLS configuration.
    /// </summary>
    public TlsConfig? TlsConfig { get; init; }

    /// <summary>
    /// SASL authentication mechanism.
    /// </summary>
    public SaslMechanism SaslMechanism { get; init; } = SaslMechanism.None;

    /// <summary>
    /// SASL username for PLAIN/SCRAM authentication.
    /// </summary>
    public string? SaslUsername { get; init; }

    /// <summary>
    /// SASL password for PLAIN/SCRAM authentication.
    /// </summary>
    public string? SaslPassword { get; init; }

    /// <summary>
    /// Whether SCRAM authentication uses Kafka delegation token credentials.
    /// </summary>
    public bool SaslScramTokenAuth { get; init; }

    /// <summary>
    /// GSSAPI (Kerberos) configuration.
    /// </summary>
    public GssapiConfig? GssapiConfig { get; init; }

    /// <summary>
    /// OAuth Bearer configuration.
    /// </summary>
    public OAuthBearerConfig? OAuthBearerConfig { get; init; }

    /// <summary>
    /// Custom OAuth Bearer token provider.
    /// </summary>
    public Func<CancellationToken, ValueTask<OAuthBearerToken>>? OAuthBearerTokenProvider { get; init; }

    /// <summary>
    /// AWS_MSK_IAM configuration.
    /// </summary>
    public AwsMskIamConfig? AwsMskIamConfig { get; init; }

    /// <summary>
    /// TCP socket send buffer size in bytes. 0 uses the system default.
    /// </summary>
    public int SocketSendBufferBytes { get; init; }

    /// <summary>
    /// TCP socket receive buffer size in bytes. 0 uses the system default.
    /// </summary>
    public int SocketReceiveBufferBytes { get; init; }

    /// <summary>
    /// Number of TCP connections per broker.
    /// </summary>
    public int ConnectionsPerBroker { get; init; } = 2;

    /// <summary>
    /// Controls how broker hostnames are resolved before connecting.
    /// </summary>
    public ClientDnsLookup ClientDnsLookup { get; init; } = ClientDnsLookup.UseAllDnsIps;

    /// <summary>
    /// Custom retry policy for transient errors.
    /// </summary>
    public IRetryPolicy? RetryPolicy { get; init; }
}
