using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Security;
using Dekaf.Security.Sasl;
using Microsoft.Extensions.Configuration;

namespace Dekaf.Extensions.DependencyInjection;

[RequiresDynamicCode(DekafConfigurationBinding.RequiresDynamicCodeMessage)]
[RequiresUnreferencedCode(DekafConfigurationBinding.RequiresUnreferencedCodeMessage)]
internal static class ConfluentConfigurationBinding
{
    private const int ConfluentFetchWaitMaxMsDefault = 500;

    private static readonly HashSet<string> s_clientProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "BootstrapServers",
        "ClientDnsLookup",
        "ClientId",
        "ClientRack",
        "ConnectionsMaxIdleMs",
        "EnableSslCertificateVerification",
        "MetadataMaxAgeMs",
        "MetadataRecoveryRebootstrapTriggerMs",
        "MetadataRecoveryStrategy",
        "ReconnectBackoffMaxMs",
        "ReconnectBackoffMs",
        "RetryBackoffMaxMs",
        "RetryBackoffMs",
        "SaslKerberosKeytab",
        "SaslKerberosPrincipal",
        "SaslKerberosServiceName",
        "SaslMechanism",
        "SaslOauthbearerClientId",
        "SaslOauthbearerClientSecret",
        "SaslOauthbearerGrantType",
        "SaslOauthbearerMethod",
        "SaslOauthbearerScope",
        "SaslOauthbearerTokenEndpointUrl",
        "SaslPassword",
        "SaslUsername",
        "SecurityProtocol",
        "SocketConnectionSetupTimeoutMs",
        "SocketKeepaliveEnable",
        "SocketNagleDisable",
        "SocketReceiveBufferBytes",
        "SocketSendBufferBytes",
        "SslCaLocation",
        "SslCertificateLocation",
        "SslEndpointIdentificationAlgorithm",
        "SslKeyLocation",
        "SslKeyPassword",
        "SslKeystoreLocation",
        "SslKeystorePassword"
    };

    private static readonly HashSet<string> s_producerProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "Acks",
        "BatchSize",
        "CompressionLevel",
        "CompressionType",
        "EnableIdempotence",
        "LingerMs",
        "MaxInFlight",
        "MessageMaxBytes",
        "MessageSendMaxRetries",
        "MessageTimeoutMs",
        "Partitioner",
        "QueueBufferingMaxKbytes",
        "RequestTimeoutMs",
        "TransactionTimeoutMs",
        "TransactionalId"
    };

    private static readonly HashSet<string> s_consumerProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "AutoCommitIntervalMs",
        "AutoOffsetReset",
        "CheckCrcs",
        "ConsumeResultFields",
        "EnableAutoCommit",
        "EnableAutoOffsetStore",
        "EnablePartitionEof",
        "FetchMaxBytes",
        "FetchMinBytes",
        "FetchWaitMaxMs",
        "GroupId",
        "GroupInstanceId",
        "GroupProtocol",
        "GroupProtocolType",
        "GroupRemoteAssignor",
        "IsolationLevel",
        "MaxPartitionFetchBytes",
        "MaxPollIntervalMs",
        "PartitionAssignmentStrategy",
        "QueuedMaxMessagesKbytes",
        "QueuedMinMessages"
    };

    public static void ApplyProducer<TKey, TValue>(
        IConfiguration configuration,
        ProducerBuilder<TKey, TValue> builder)
    {
        ValidateProducerProperties(configuration);

        if (TryGetEnum(configuration, "Acks", out Acks acks))
            builder.WithAcks(acks);
        if (TryGetWholeMilliseconds(configuration, "LingerMs", out var linger))
            builder.WithLinger(linger);
        if (TryGet(configuration, "BatchSize", out int batchSize))
            builder.WithBatchSize(batchSize);
        if (TryGet(configuration, "QueueBufferingMaxKbytes", out int bufferMemoryKbytes))
        {
            ArgumentOutOfRangeException.ThrowIfNegative(bufferMemoryKbytes);
            builder.WithBufferMemory(checked((ulong)bufferMemoryKbytes * 1024));
        }
        if (TryGet(configuration, "MaxInFlight", out int maxInFlight))
            builder.WithMaxInFlightRequestsPerConnection(maxInFlight);
        if (TryGet(configuration, "MessageSendMaxRetries", out int retries))
            builder.WithRetries(retries);
        if (TryGet(configuration, "MessageTimeoutMs", out int deliveryTimeoutMs))
        {
            if (deliveryTimeoutMs == 0)
            {
                throw new NotSupportedException(
                    "Confluent property 'MessageTimeoutMs' value '0' means infinite delivery time, " +
                    "which Dekaf cannot represent exactly.");
            }

            builder.WithDeliveryTimeout(TimeSpan.FromMilliseconds(deliveryTimeoutMs));
        }
        if (TryGet(configuration, "RequestTimeoutMs", out int requestTimeoutMs))
            builder.WithRequestTimeout(TimeSpan.FromMilliseconds(requestTimeoutMs));
        if (TryGet(configuration, "MessageMaxBytes", out int maxRequestSize))
            builder.WithMaxRequestSize(maxRequestSize);
        var hasTransactionalId = TryGet(configuration, "TransactionalId", out string transactionalId);
        builder.WithIdempotence(
            TryGet(configuration, "EnableIdempotence", out bool enableIdempotence)
                ? enableIdempotence
                : hasTransactionalId);
        if (hasTransactionalId)
            builder.WithTransactionalId(transactionalId);
        if (TryGet(configuration, "TransactionTimeoutMs", out int transactionTimeoutMs))
            builder.WithTransactionTimeout(TimeSpan.FromMilliseconds(transactionTimeoutMs));
        if (TryGetEnum(configuration, "CompressionType", out ConfluentCompressionType compressionType))
            builder.UseCompression(MapCompressionType(compressionType));
        if (TryGet(configuration, "CompressionLevel", out int compressionLevel) && compressionLevel != -1)
            builder.WithCompressionLevel(compressionLevel);
        if (TryGetEnum(configuration, "Partitioner", out ConfluentPartitioner partitioner))
            builder.WithPartitioner(MapPartitioner(partitioner));

        var clientBuilder = new ProducerClientBuilder<TKey, TValue>(builder);
        ApplySharedClient(configuration, clientBuilder);
        ApplySecurity(configuration, clientBuilder);
    }

    public static void ApplyConsumer<TKey, TValue>(
        IConfiguration configuration,
        ConsumerBuilder<TKey, TValue> builder)
    {
        ValidateConsumerProperties(configuration);

        if (TryGet(configuration, "GroupId", out string groupId))
            builder.WithGroupId(groupId);
        if (TryGet(configuration, "GroupInstanceId", out string groupInstanceId))
            builder.WithGroupInstanceId(groupInstanceId);

        ApplyGroupProtocol(configuration, builder);

        var autoCommitEnabled = !TryGet(configuration, "EnableAutoCommit", out bool enableAutoCommit) ||
                                enableAutoCommit;
        if (TryGet(configuration, "AutoCommitIntervalMs", out int autoCommitIntervalMs))
        {
            ArgumentOutOfRangeException.ThrowIfNegative(autoCommitIntervalMs);
            if (autoCommitIntervalMs == 0)
                autoCommitEnabled = false;
            else
                builder.WithAutoCommitInterval(TimeSpan.FromMilliseconds(autoCommitIntervalMs));
        }

        builder.WithOffsetCommitMode(autoCommitEnabled ? OffsetCommitMode.Auto : OffsetCommitMode.Manual);
        // librdkafka stores offsets when records are delivered, unlike Dekaf's safer default.
        builder.WithOffsetStoreTiming(OffsetStoreTiming.OnDelivery);
        if (TryGet(configuration, "EnableAutoOffsetStore", out bool enableAutoOffsetStore))
            builder.WithAutoOffsetStore(enableAutoOffsetStore);
        if (TryGetEnum(configuration, "AutoOffsetReset", out ConfluentAutoOffsetReset autoOffsetReset))
            builder.WithAutoOffsetReset(MapAutoOffsetReset(autoOffsetReset));
        if (TryGet(configuration, "FetchMinBytes", out int fetchMinBytes))
            builder.WithFetchMinBytes(fetchMinBytes);
        if (TryGet(configuration, "FetchMaxBytes", out int fetchMaxBytes))
            builder.WithFetchMaxBytes(fetchMaxBytes);
        if (TryGet(configuration, "MaxPartitionFetchBytes", out int maxPartitionFetchBytes))
            builder.WithMaxPartitionFetchBytes(maxPartitionFetchBytes);
        builder.WithFetchMaxWait(TimeSpan.FromMilliseconds(
            TryGet(configuration, "FetchWaitMaxMs", out int fetchWaitMaxMs)
                ? fetchWaitMaxMs
                : ConfluentFetchWaitMaxMsDefault));
        if (TryGet(configuration, "MaxPollIntervalMs", out int maxPollIntervalMs))
            builder.WithMaxPollInterval(TimeSpan.FromMilliseconds(maxPollIntervalMs));
        if (TryGet(configuration, "QueuedMinMessages", out int queuedMinMessages))
            builder.WithQueuedMinMessages(queuedMinMessages);
        if (TryGet(configuration, "QueuedMaxMessagesKbytes", out int queuedMaxMessagesKbytes))
            builder.WithQueuedMaxMessagesKbytes(queuedMaxMessagesKbytes);
        if (TryGet(configuration, "EnablePartitionEof", out bool enablePartitionEof))
            builder.WithPartitionEof(enablePartitionEof);
        builder.WithCheckCrcs(TryGet(configuration, "CheckCrcs", out bool checkCrcs) && checkCrcs);
        builder.WithIsolationLevel(
            TryGetEnum(configuration, "IsolationLevel", out IsolationLevel isolationLevel)
                ? isolationLevel
                : IsolationLevel.ReadCommitted);
        ValidateConsumeResultFields(configuration);

        var clientBuilder = new ConsumerClientBuilder<TKey, TValue>(builder);
        ApplySharedClient(configuration, clientBuilder);
        ApplySecurity(configuration, clientBuilder);
    }

    private static void ApplySharedClient<TBuilder>(
        IConfiguration configuration,
        TBuilder builder)
        where TBuilder : struct, IConfluentClientBuilder
    {
        if (TryGet(configuration, "BootstrapServers", out string bootstrapServers))
            builder.WithBootstrapServers(bootstrapServers);
        if (TryGet(configuration, "ClientId", out string clientId))
            builder.WithClientId(clientId);
        if (TryGet(configuration, "ClientRack", out string clientRack))
            builder.WithClientRack(clientRack);
        if (TryGetEnum(configuration, "ClientDnsLookup", out ClientDnsLookup clientDnsLookup))
            builder.WithClientDnsLookup(clientDnsLookup);
        if (TryGet(configuration, "ConnectionsMaxIdleMs", out int connectionsMaxIdleMs))
        {
            builder.WithConnectionsMaxIdle(connectionsMaxIdleMs == 0
                ? Timeout.InfiniteTimeSpan
                : TimeSpan.FromMilliseconds(connectionsMaxIdleMs));
        }
        if (TryGet(configuration, "MetadataMaxAgeMs", out int metadataMaxAgeMs))
            builder.WithMetadataMaxAge(TimeSpan.FromMilliseconds(metadataMaxAgeMs));
        if (TryGetEnum(
                configuration,
                "MetadataRecoveryStrategy",
                out MetadataRecoveryStrategy metadataRecoveryStrategy))
        {
            builder.WithMetadataRecoveryStrategy(metadataRecoveryStrategy);
        }
        if (TryGet(
                configuration,
                "MetadataRecoveryRebootstrapTriggerMs",
                out int metadataRecoveryRebootstrapTriggerMs))
        {
            builder.WithMetadataRecoveryRebootstrapTrigger(
                TimeSpan.FromMilliseconds(metadataRecoveryRebootstrapTriggerMs));
        }
        if (TryGet(configuration, "RetryBackoffMs", out int retryBackoffMs))
            builder.WithRetryBackoff(TimeSpan.FromMilliseconds(retryBackoffMs));
        if (TryGet(configuration, "RetryBackoffMaxMs", out int retryBackoffMaxMs))
            builder.WithRetryBackoffMax(TimeSpan.FromMilliseconds(retryBackoffMaxMs));
        if (TryGet(configuration, "ReconnectBackoffMs", out int reconnectBackoffMs))
            builder.WithReconnectBackoff(TimeSpan.FromMilliseconds(reconnectBackoffMs));
        if (TryGet(configuration, "ReconnectBackoffMaxMs", out int reconnectBackoffMaxMs))
            builder.WithReconnectBackoffMax(TimeSpan.FromMilliseconds(reconnectBackoffMaxMs));
        if (TryGet(configuration, "SocketConnectionSetupTimeoutMs", out int connectionTimeoutMs))
            builder.WithConnectionTimeout(TimeSpan.FromMilliseconds(connectionTimeoutMs));
        if (TryGet(configuration, "SocketKeepaliveEnable", out bool enableTcpKeepAlive))
            builder.WithTcpKeepAlive(enableTcpKeepAlive);
        if (TryGet(configuration, "SocketNagleDisable", out bool disableNagle) && !disableNagle)
        {
            throw new NotSupportedException(
                "Confluent property 'SocketNagleDisable' value 'false' cannot be represented exactly; " +
                "Dekaf always disables Nagle's algorithm.");
        }
        if (TryGet(configuration, "SocketSendBufferBytes", out int socketSendBufferBytes))
            builder.WithSocketSendBufferBytes(socketSendBufferBytes);
        if (TryGet(configuration, "SocketReceiveBufferBytes", out int socketReceiveBufferBytes))
            builder.WithSocketReceiveBufferBytes(socketReceiveBufferBytes);
    }

    private static void ApplySecurity<TBuilder>(
        IConfiguration configuration,
        TBuilder builder)
        where TBuilder : struct, IConfluentClientBuilder
    {
        var protocol = GetSecurityProtocol(configuration);
        var tlsEnabled = protocol is ConfluentSecurityProtocol.Ssl or ConfluentSecurityProtocol.SaslSsl;
        var saslEnabled = protocol is ConfluentSecurityProtocol.SaslPlaintext or ConfluentSecurityProtocol.SaslSsl;

        var hasTlsSettings = HasAny(
            configuration,
            "EnableSslCertificateVerification",
            "SslCaLocation",
            "SslCertificateLocation",
            "SslEndpointIdentificationAlgorithm",
            "SslKeyLocation",
            "SslKeyPassword",
            "SslKeystoreLocation",
            "SslKeystorePassword");
        if (hasTlsSettings && !tlsEnabled)
        {
            throw new InvalidOperationException(
                "Confluent TLS properties require SecurityProtocol to be Ssl or SaslSsl.");
        }

        if (tlsEnabled)
            builder.UseTls(CreateTlsConfig(configuration));

        var hasSaslSettings = HasAny(
            configuration,
            "SaslKerberosKeytab",
            "SaslKerberosPrincipal",
            "SaslKerberosServiceName",
            "SaslMechanism",
            "SaslOauthbearerClientId",
            "SaslOauthbearerClientSecret",
            "SaslOauthbearerGrantType",
            "SaslOauthbearerMethod",
            "SaslOauthbearerScope",
            "SaslOauthbearerTokenEndpointUrl",
            "SaslPassword",
            "SaslUsername");
        if (hasSaslSettings && !saslEnabled)
        {
            throw new InvalidOperationException(
                "Confluent SASL properties require SecurityProtocol to be SaslPlaintext or SaslSsl.");
        }

        if (!saslEnabled)
            return;

        var mechanism = TryGetSaslMechanism(configuration, out var configuredMechanism)
            ? configuredMechanism
            : SaslMechanism.Gssapi;
        TryGet(configuration, "SaslUsername", out string? username);
        TryGet(configuration, "SaslPassword", out string? password);

        GssapiConfig? gssapi = null;
        OAuthBearerConfig? oauth = null;
        switch (mechanism)
        {
            case SaslMechanism.Plain or SaslMechanism.ScramSha256 or SaslMechanism.ScramSha512:
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    throw new InvalidOperationException(
                        $"SaslUsername and SaslPassword are required for {mechanism}.");
                }
                break;
            case SaslMechanism.Gssapi:
                gssapi = new GssapiConfig
                {
                    ServiceName = Get(configuration, "SaslKerberosServiceName") ?? "kafka",
                    Principal = Get(configuration, "SaslKerberosPrincipal"),
                    KeytabPath = Get(configuration, "SaslKerberosKeytab")
                };
                break;
            case SaslMechanism.OAuthBearer:
                oauth = CreateOAuthConfig(configuration);
                break;
            default:
                throw UnsupportedValue("SaslMechanism", mechanism.ToString());
        }

        builder.WithSaslOptions(mechanism, username, password, gssapi, oauth);
    }

    private static TlsConfig CreateTlsConfig(IConfiguration configuration)
    {
        var certificateLocation = Get(configuration, "SslCertificateLocation");
        var keyLocation = Get(configuration, "SslKeyLocation");
        var keystoreLocation = Get(configuration, "SslKeystoreLocation");
        if (certificateLocation is not null && keystoreLocation is not null)
        {
            throw new InvalidOperationException(
                "SslCertificateLocation and SslKeystoreLocation cannot both be configured.");
        }

        return new TlsConfig
        {
            CaCertificatePath = Get(configuration, "SslCaLocation"),
            ClientCertificatePath = certificateLocation ?? keystoreLocation,
            ClientKeyPath = keyLocation,
            ClientKeyPassword = Get(configuration, "SslKeyPassword")
                ?? Get(configuration, "SslKeystorePassword"),
            ValidateServerCertificate = !TryGet(
                configuration,
                "EnableSslCertificateVerification",
                out bool validateCertificate) || validateCertificate,
            ValidateServerCertificateHostName = GetEndpointIdentification(configuration)
        };
    }

    private static OAuthBearerConfig CreateOAuthConfig(IConfiguration configuration)
    {
        var method = Get(configuration, "SaslOauthbearerMethod") ?? "Default";
        if (!method.Equals("Oidc", StringComparison.OrdinalIgnoreCase))
            throw UnsupportedValue("SaslOauthbearerMethod", method);

        var grantType = Get(configuration, "SaslOauthbearerGrantType");
        if (grantType is not null &&
            !grantType.Equals("client_credentials", StringComparison.OrdinalIgnoreCase) &&
            !grantType.Equals("ClientCredentials", StringComparison.OrdinalIgnoreCase))
        {
            throw UnsupportedValue("SaslOauthbearerGrantType", grantType);
        }

        return new OAuthBearerConfig
        {
            TokenEndpointUrl = GetRequired(configuration, "SaslOauthbearerTokenEndpointUrl"),
            ClientId = GetRequired(configuration, "SaslOauthbearerClientId"),
            ClientSecret = Get(configuration, "SaslOauthbearerClientSecret"),
            Scope = Get(configuration, "SaslOauthbearerScope")
        };
    }

    private static ConfluentSecurityProtocol GetSecurityProtocol(IConfiguration configuration)
    {
        var raw = Get(configuration, "SecurityProtocol");
        if (raw is null)
            return ConfluentSecurityProtocol.Plaintext;
        return Enum.TryParse<ConfluentSecurityProtocol>(NormalizeEnumValue(raw), ignoreCase: true, out var protocol) &&
               Enum.IsDefined(protocol)
            ? protocol
            : throw UnsupportedValue("SecurityProtocol", raw);
    }

    private static bool TryGetSaslMechanism(
        IConfiguration configuration,
        out SaslMechanism mechanism)
    {
        var raw = Get(configuration, "SaslMechanism");
        if (raw is null)
        {
            mechanism = default;
            return false;
        }

        if (Enum.TryParse<SaslMechanism>(NormalizeEnumValue(raw), ignoreCase: true, out mechanism) &&
            Enum.IsDefined(mechanism))
            return true;
        throw UnsupportedValue("SaslMechanism", raw);
    }

    private static bool GetEndpointIdentification(IConfiguration configuration)
    {
        var raw = Get(configuration, "SslEndpointIdentificationAlgorithm");
        if (raw is null || raw.Equals("Https", StringComparison.OrdinalIgnoreCase))
            return true;
        if (raw.Equals("None", StringComparison.OrdinalIgnoreCase))
            return false;
        throw UnsupportedValue("SslEndpointIdentificationAlgorithm", raw);
    }

    private static bool TryGetWholeMilliseconds(
        IConfiguration configuration,
        string key,
        out TimeSpan value)
    {
        if (!TryGet(configuration, key, out double milliseconds))
        {
            value = default;
            return false;
        }

        if (milliseconds != Math.Truncate(milliseconds))
        {
            throw new NotSupportedException(
                $"Confluent property '{key}' value '{milliseconds}' cannot be represented exactly by Dekaf; " +
                "use a whole number of milliseconds.");
        }

        value = TimeSpan.FromMilliseconds(milliseconds);
        return true;
    }

    private static bool TryGetEnum<TEnum>(
        IConfiguration configuration,
        string key,
        out TEnum value)
        where TEnum : struct, Enum
    {
        var raw = Get(configuration, key);
        if (raw is null)
        {
            value = default;
            return false;
        }

        if (Enum.TryParse<TEnum>(NormalizeEnumValue(raw), ignoreCase: true, out value) &&
            Enum.IsDefined(value))
        {
            return true;
        }

        throw UnsupportedValue(key, raw);
    }

    private static string NormalizeEnumValue(string value) =>
        value.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);

    private static void ValidateProducerProperties(IConfiguration configuration)
    {
        ValidateProperties(configuration, s_producerProperties, "ProducerConfig");
    }

    private static void ValidateConsumerProperties(IConfiguration configuration)
    {
        ValidateProperties(configuration, s_consumerProperties, "ConsumerConfig");
    }

    private static void ValidateProperties(
        IConfiguration configuration,
        IReadOnlySet<string> roleProperties,
        string configType)
    {
        foreach (var child in configuration.GetChildren())
        {
            if (s_clientProperties.Contains(child.Key) || roleProperties.Contains(child.Key))
                continue;

            throw new NotSupportedException(
                $"Confluent {configType} property '{child.Key}' has no exact Dekaf equivalent. " +
                "Remove it or configure the corresponding Dekaf builder API explicitly.");
        }
    }

    private static void ApplyGroupProtocol<TKey, TValue>(
        IConfiguration configuration,
        ConsumerBuilder<TKey, TValue> builder)
    {
        if (TryGetEnum(configuration, "GroupProtocol", out ConfluentGroupProtocol groupProtocol) &&
            groupProtocol != ConfluentGroupProtocol.Consumer)
        {
            throw UnsupportedValue("GroupProtocol", groupProtocol.ToString());
        }

        var groupProtocolType = Get(configuration, "GroupProtocolType");
        if (groupProtocolType is not null &&
            !groupProtocolType.Equals("consumer", StringComparison.OrdinalIgnoreCase))
        {
            throw UnsupportedValue("GroupProtocolType", groupProtocolType);
        }

        string? assignor = null;
        if (TryGetEnum(
                configuration,
                "PartitionAssignmentStrategy",
                out ConfluentPartitionAssignmentStrategy assignmentStrategy))
        {
            if (assignmentStrategy != ConfluentPartitionAssignmentStrategy.Range)
                throw UnsupportedValue("PartitionAssignmentStrategy", assignmentStrategy.ToString());
            assignor = "range";
        }

        var remoteAssignor = Get(configuration, "GroupRemoteAssignor");
        if (remoteAssignor is not null)
        {
            if (!remoteAssignor.Equals("range", StringComparison.OrdinalIgnoreCase) &&
                !remoteAssignor.Equals("uniform", StringComparison.OrdinalIgnoreCase))
            {
                throw UnsupportedValue("GroupRemoteAssignor", remoteAssignor);
            }

            if (assignor is not null && !remoteAssignor.Equals(assignor, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "PartitionAssignmentStrategy and GroupRemoteAssignor select different assignors.");
            }

            assignor = remoteAssignor.Equals("range", StringComparison.OrdinalIgnoreCase)
                ? "range"
                : "uniform";
        }

        if (assignor is not null)
            builder.WithGroupRemoteAssignor(assignor);
    }

    private static void ValidateConsumeResultFields(IConfiguration configuration)
    {
        var fields = Get(configuration, "ConsumeResultFields");
        if (fields is not null && !fields.Equals("all", StringComparison.OrdinalIgnoreCase))
            throw UnsupportedValue("ConsumeResultFields", fields);
    }

    private static bool HasAny(IConfiguration configuration, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (configuration.GetSection(key).Exists())
                return true;
        }

        return false;
    }

    private static bool TryGet<T>(IConfiguration configuration, string key, out T value)
    {
        var section = configuration.GetSection(key);
        if (!section.Exists())
        {
            value = default!;
            return false;
        }

        var bound = section.Get<T>();
        if (bound is null)
        {
            throw new InvalidOperationException(
                $"Confluent property '{key}' could not be bound to {typeof(T).Name}.");
        }

        value = bound;
        return true;
    }

    private static string? Get(IConfiguration configuration, string key)
    {
        var section = configuration.GetSection(key);
        if (!section.Exists())
            return null;

        return section.Value ?? throw new InvalidOperationException(
            $"Confluent property '{key}' must be a scalar value.");
    }

    private static string GetRequired(IConfiguration configuration, string key)
    {
        var value = Get(configuration, key);
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"{key} is required for Confluent OAUTHBEARER configuration.");
    }

    private static CompressionType MapCompressionType(ConfluentCompressionType compressionType) =>
        compressionType switch
        {
            ConfluentCompressionType.None => CompressionType.None,
            ConfluentCompressionType.Gzip => CompressionType.Gzip,
            ConfluentCompressionType.Snappy => CompressionType.Snappy,
            ConfluentCompressionType.Lz4 => CompressionType.Lz4,
            ConfluentCompressionType.Zstd => CompressionType.Zstd,
            _ => throw new UnreachableException()
        };

    private static PartitionerType MapPartitioner(ConfluentPartitioner partitioner) =>
        partitioner switch
        {
            ConfluentPartitioner.Random => PartitionerType.Random,
            ConfluentPartitioner.Consistent => PartitionerType.Consistent,
            ConfluentPartitioner.ConsistentRandom => PartitionerType.ConsistentRandom,
            ConfluentPartitioner.Murmur2 => PartitionerType.Murmur2,
            ConfluentPartitioner.Murmur2Random => PartitionerType.Murmur2Random,
            ConfluentPartitioner.Fnv1a => PartitionerType.Fnv1A,
            ConfluentPartitioner.Fnv1aRandom => PartitionerType.Fnv1ARandom,
            _ => throw new UnreachableException()
        };

    private static AutoOffsetReset MapAutoOffsetReset(ConfluentAutoOffsetReset autoOffsetReset) =>
        autoOffsetReset switch
        {
            ConfluentAutoOffsetReset.Earliest => AutoOffsetReset.Earliest,
            ConfluentAutoOffsetReset.Latest => AutoOffsetReset.Latest,
            ConfluentAutoOffsetReset.Error => AutoOffsetReset.None,
            _ => throw new UnreachableException()
        };

    private static NotSupportedException UnsupportedValue(string key, string value) =>
        new($"Confluent property '{key}' value '{value}' has no exact Dekaf equivalent.");

    private enum ConfluentSecurityProtocol
    {
        Plaintext,
        Ssl,
        SaslPlaintext,
        SaslSsl
    }

    private enum ConfluentCompressionType
    {
        None,
        Gzip,
        Snappy,
        Lz4,
        Zstd
    }

    private enum ConfluentPartitioner
    {
        Random,
        Consistent,
        ConsistentRandom,
        Murmur2,
        Murmur2Random,
        Fnv1a,
        Fnv1aRandom
    }

    private enum ConfluentAutoOffsetReset
    {
        Earliest,
        Latest,
        Error
    }

    private enum ConfluentGroupProtocol
    {
        Classic,
        Consumer
    }

    private enum ConfluentPartitionAssignmentStrategy
    {
        Range,
        RoundRobin,
        CooperativeSticky
    }

    private interface IConfluentClientBuilder
    {
        void WithBootstrapServers(string servers);
        void WithClientId(string clientId);
        void WithClientRack(string clientRack);
        void WithClientDnsLookup(ClientDnsLookup lookup);
        void WithConnectionsMaxIdle(TimeSpan idle);
        void WithMetadataMaxAge(TimeSpan interval);
        void WithMetadataRecoveryStrategy(MetadataRecoveryStrategy strategy);
        void WithMetadataRecoveryRebootstrapTrigger(TimeSpan trigger);
        void WithRetryBackoff(TimeSpan backoff);
        void WithRetryBackoffMax(TimeSpan backoff);
        void WithReconnectBackoff(TimeSpan backoff);
        void WithReconnectBackoffMax(TimeSpan backoff);
        void WithConnectionTimeout(TimeSpan timeout);
        void WithTcpKeepAlive(bool enabled);
        void WithSocketSendBufferBytes(int bytes);
        void WithSocketReceiveBufferBytes(int bytes);
        void UseTls(TlsConfig config);
        void WithSaslOptions(
            SaslMechanism mechanism,
            string? username,
            string? password,
            GssapiConfig? gssapi,
            OAuthBearerConfig? oauth);
    }

    private readonly struct ProducerClientBuilder<TKey, TValue>(ProducerBuilder<TKey, TValue> builder)
        : IConfluentClientBuilder
    {
        public void WithBootstrapServers(string servers) => builder.WithBootstrapServers(servers);
        public void WithClientId(string clientId) => builder.WithClientId(clientId);
        public void WithClientRack(string clientRack) => builder.WithClientRack(clientRack);
        public void WithClientDnsLookup(ClientDnsLookup lookup) => builder.WithClientDnsLookup(lookup);
        public void WithConnectionsMaxIdle(TimeSpan idle) => builder.WithConnectionsMaxIdle(idle);
        public void WithMetadataMaxAge(TimeSpan interval) => builder.WithMetadataMaxAge(interval);
        public void WithMetadataRecoveryStrategy(MetadataRecoveryStrategy strategy) =>
            builder.WithMetadataRecoveryStrategy(strategy);
        public void WithMetadataRecoveryRebootstrapTrigger(TimeSpan trigger) =>
            builder.WithMetadataRecoveryRebootstrapTrigger(trigger);
        public void WithRetryBackoff(TimeSpan backoff) => builder.WithRetryBackoff(backoff);
        public void WithRetryBackoffMax(TimeSpan backoff) => builder.WithRetryBackoffMax(backoff);
        public void WithReconnectBackoff(TimeSpan backoff) => builder.WithReconnectBackoff(backoff);
        public void WithReconnectBackoffMax(TimeSpan backoff) => builder.WithReconnectBackoffMax(backoff);
        public void WithConnectionTimeout(TimeSpan timeout) => builder.WithConnectionTimeout(timeout);
        public void WithTcpKeepAlive(bool enabled) => builder.WithTcpKeepAlive(enabled);
        public void WithSocketSendBufferBytes(int bytes) => builder.WithSocketSendBufferBytes(bytes);
        public void WithSocketReceiveBufferBytes(int bytes) => builder.WithSocketReceiveBufferBytes(bytes);
        public void UseTls(TlsConfig config) => builder.UseTls(config);
        public void WithSaslOptions(
            SaslMechanism mechanism,
            string? username,
            string? password,
            GssapiConfig? gssapi,
            OAuthBearerConfig? oauth) =>
            builder.WithSaslOptions(mechanism, username, password, gssapi, oauth, awsMskIamConfig: null);
    }

    private readonly struct ConsumerClientBuilder<TKey, TValue>(ConsumerBuilder<TKey, TValue> builder)
        : IConfluentClientBuilder
    {
        public void WithBootstrapServers(string servers) => builder.WithBootstrapServers(servers);
        public void WithClientId(string clientId) => builder.WithClientId(clientId);
        public void WithClientRack(string clientRack) => builder.WithClientRack(clientRack);
        public void WithClientDnsLookup(ClientDnsLookup lookup) => builder.WithClientDnsLookup(lookup);
        public void WithConnectionsMaxIdle(TimeSpan idle) => builder.WithConnectionsMaxIdle(idle);
        public void WithMetadataMaxAge(TimeSpan interval) => builder.WithMetadataMaxAge(interval);
        public void WithMetadataRecoveryStrategy(MetadataRecoveryStrategy strategy) =>
            builder.WithMetadataRecoveryStrategy(strategy);
        public void WithMetadataRecoveryRebootstrapTrigger(TimeSpan trigger) =>
            builder.WithMetadataRecoveryRebootstrapTrigger(trigger);
        public void WithRetryBackoff(TimeSpan backoff) => builder.WithRetryBackoff(backoff);
        public void WithRetryBackoffMax(TimeSpan backoff) => builder.WithRetryBackoffMax(backoff);
        public void WithReconnectBackoff(TimeSpan backoff) => builder.WithReconnectBackoff(backoff);
        public void WithReconnectBackoffMax(TimeSpan backoff) => builder.WithReconnectBackoffMax(backoff);
        public void WithConnectionTimeout(TimeSpan timeout) => builder.WithConnectionTimeout(timeout);
        public void WithTcpKeepAlive(bool enabled) => builder.WithTcpKeepAlive(enabled);
        public void WithSocketSendBufferBytes(int bytes) => builder.WithSocketSendBufferBytes(bytes);
        public void WithSocketReceiveBufferBytes(int bytes) => builder.WithSocketReceiveBufferBytes(bytes);
        public void UseTls(TlsConfig config) => builder.UseTls(config);
        public void WithSaslOptions(
            SaslMechanism mechanism,
            string? username,
            string? password,
            GssapiConfig? gssapi,
            OAuthBearerConfig? oauth) =>
            builder.WithSaslOptions(mechanism, username, password, gssapi, oauth, awsMskIamConfig: null);
    }
}
