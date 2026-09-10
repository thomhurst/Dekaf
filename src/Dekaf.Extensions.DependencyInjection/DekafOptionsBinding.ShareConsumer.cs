using Dekaf.ShareConsumer;

namespace Dekaf.Extensions.DependencyInjection;

internal static partial class DekafOptionsBinding
{
    internal static void ApplyShareConsumer<TKey, TValue>(ShareConsumerOptions options, ShareConsumerBuilder<TKey, TValue> builder)
    {
        builder.WithBootstrapServers(options.BootstrapServers.ToArray());
        builder.WithGroupId(options.GroupId);
        if (options.ClientId is not null) builder.WithClientId(options.ClientId);
        if (options.RackId is not null) builder.WithRackId(options.RackId);
        builder.WithFetchMinBytes(options.FetchMinBytes);
        builder.WithFetchMaxBytes(options.FetchMaxBytes);
        builder.WithMaxPartitionFetchBytes(options.MaxPartitionFetchBytes);
        builder.WithFetchMaxWaitMs(options.FetchMaxWaitMs);
        builder.WithMaxPollRecords(options.MaxPollRecords);
        builder.WithAcknowledgementMode(options.AcknowledgementMode);
        builder.WithShareAcquireMode(options.ShareAcquireMode);
        builder.WithSessionTimeoutMs(options.SessionTimeoutMs);
        builder.WithHeartbeatIntervalMs(options.HeartbeatIntervalMs);
        builder.WithRequestTimeoutMs(options.RequestTimeoutMs);
        builder.WithSocketSendBufferBytes(options.SocketSendBufferBytes);
        builder.WithSocketReceiveBufferBytes(options.SocketReceiveBufferBytes);
        builder.WithConnectionsPerBroker(options.ConnectionsPerBroker);
        builder.WithClientDnsLookup(options.ClientDnsLookup);
        builder.WithSaslScramMaxIterations(options.SaslScramMaxIterations);
        builder.WithRetryBackoff(TimeSpan.FromMilliseconds(options.RetryBackoffMs));
        builder.WithRetryBackoffMax(TimeSpan.FromMilliseconds(options.RetryBackoffMaxMs));
        builder.WithReconnectBackoff(TimeSpan.FromMilliseconds(options.ReconnectBackoffMs));
        builder.WithReconnectBackoffMax(TimeSpan.FromMilliseconds(options.ReconnectBackoffMaxMs));
        builder.WithConnectionsMaxIdle(ToTimeout(options.ConnectionsMaxIdleMs));
        builder.WithConnectionTimeout(options.ConnectionTimeout);
        builder.WithConnectionTimeoutMax(options.ConnectionTimeoutMax);
        builder.WithTcpKeepAlive(options.TcpKeepAliveTime, options.TcpKeepAliveInterval, options.TcpKeepAliveRetryCount);
        builder.WithTcpKeepAlive(options.EnableTcpKeepAlive);
        builder.WithMetadataClusterCheck(options.MetadataClusterCheckEnabled);
        builder.WithBootstrapResolveTimeout(TimeSpan.FromMilliseconds(options.BootstrapResolveTimeoutMs));
        ApplyTls(options.UseTls, options.TlsConfig, () => builder.WithTls(), tls => builder.WithTlsConfig(tls));
        ApplyRemoteCertificateValidationCallback(options.RemoteCertificateValidationCallback,
            builder.WithRemoteCertificateValidationCallback);
        ApplySasl(options.SaslMechanism, options.SaslUsername, options.SaslPassword, options.SaslScramTokenAuth,
            options.GssapiConfig, options.OAuthBearerConfig, options.OAuthBearerTokenProvider,
            options.AwsMskIamConfig, options.SaslCredentialProvider, builder.WithSaslOptions, builder.WithOAuthBearerTokenProvider);
        if (options.AcknowledgementCommitCallback is not null)
            builder.WithAcknowledgementCommitCallback(options.AcknowledgementCommitCallback);
        if (options.RetryPolicy is not null) builder.WithRetryPolicy(options.RetryPolicy);
        foreach (var metric in options.ApplicationMetrics) builder.RegisterMetricForSubscription(metric);
    }
}
