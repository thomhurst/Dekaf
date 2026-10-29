using System.Buffers;
using Amazon;
using Amazon.KeyManagementService;

namespace Dekaf.SchemaRegistry.Kms.Aws;

public sealed partial class AwsKmsProvider
{
    private static readonly SearchValues<char> RegionCharacters = SearchValues.Create("abcdefghijklmnopqrstuvwxyz0123456789-");

    private ClientLease GetClient(string keyId)
    {
        if (_regionalClients is null || !Arn.TryParse(keyId, out var arn) || arn.Service != "kms")
            return new ClientLease(_client);

        var region = arn.Region;
        if (string.IsNullOrEmpty(region) || region.Length > 64 ||
            region.AsSpan().ContainsAnyExcept(RegionCharacters))
        {
            throw new SchemaRegistryKmsException("AWS KMS key ARN has an invalid region.");
        }

        return _regionalClients.Rent(region, _client);
    }

    private readonly struct ClientLease(
        IAmazonKeyManagementService client,
        RegionalClientCache? temporaryOwner = null) : IDisposable
    {
        public IAmazonKeyManagementService Client { get; } = client;
        public void Dispose() => temporaryOwner?.Return(Client);
    }

    private sealed class RegionalClientCache(AmazonKeyManagementServiceConfig config) : IDisposable
    {
        private const int Capacity = 16;
        private readonly object _gate = new();
        private readonly Dictionary<string, IAmazonKeyManagementService> _clients = new(StringComparer.Ordinal);
        private readonly HashSet<IAmazonKeyManagementService> _temporaryClients = [];
        private bool _disposed;

        public ClientLease Rent(string region, IAmazonKeyManagementService defaultClient)
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (region == config.RegionEndpoint?.SystemName)
                    return new ClientLease(defaultClient);
                if (_clients.TryGetValue(region, out var existing))
                    return new ClientLease(existing);

                var client = new AmazonKeyManagementServiceClient(CopyConfiguration(config, RegionEndpoint.GetBySystemName(region)));
                if (_clients.Count < Capacity)
                {
                    _clients.Add(region, client);
                    return new ClientLease(client);
                }

                // Overflow regions still work; only their in-flight clients are retained.
                _temporaryClients.Add(client);
                return new ClientLease(client, this);
            }
        }

        public void Return(IAmazonKeyManagementService client)
        {
            lock (_gate)
            {
                if (!_temporaryClients.Remove(client))
                    return;
            }
            client.Dispose();
        }

        public void Dispose()
        {
            List<IAmazonKeyManagementService> clients;
            lock (_gate)
            {
                if (_disposed)
                    return;
                _disposed = true;
                clients = new List<IAmazonKeyManagementService>(_clients.Values);
                clients.AddRange(_temporaryClients);
                _clients.Clear();
                _temporaryClients.Clear();
            }
            foreach (var client in clients)
                client.Dispose();
        }
    }

    // The SDK has no public configuration clone API. Copy effective settings without
    // reflection so credentials, transports and retry policy also survive NativeAOT.
    private static AmazonKeyManagementServiceConfig CopyConfiguration(
        AmazonKeyManagementServiceConfig source, RegionEndpoint region)
    {
        var copy = new AmazonKeyManagementServiceConfig
        {
            AWSTokenProvider = source.AWSTokenProvider,
            AccountIdEndpointMode = source.AccountIdEndpointMode,
            AllowAutoRedirect = source.AllowAutoRedirect,
            AuthSchemePreference = source.AuthSchemePreference,
            AuthenticationRegion = region.SystemName,
            AuthenticationServiceName = source.AuthenticationServiceName,
            BufferSize = source.BufferSize,
            CacheHttpClient = source.CacheHttpClient,
            ClientAppId = source.ClientAppId,
            DefaultAWSCredentials = source.DefaultAWSCredentials,
            DefaultConfigurationMode = source.DefaultConfigurationMode,
            DisableClockSkewCorrection = source.DisableClockSkewCorrection,
            DisableHostPrefixInjection = source.DisableHostPrefixInjection,
            DisableLogging = source.DisableLogging,
            DisableRequestCompression = source.DisableRequestCompression,
            EndpointDiscoveryCacheLimit = source.EndpointDiscoveryCacheLimit,
            EndpointDiscoveryEnabled = source.EndpointDiscoveryEnabled,
            EndpointProvider = source.EndpointProvider,
            FastFailRequests = source.FastFailRequests,
            HttpClientCacheSize = source.HttpClientCacheSize,
            HttpClientFactory = source.HttpClientFactory,
            IdentityResolverConfiguration = source.IdentityResolverConfiguration,
            IgnoreConfiguredEndpointUrls = source.IgnoreConfiguredEndpointUrls,
            JsonMaxDepth = source.JsonMaxDepth,
            LogMetrics = source.LogMetrics,
            LogResponse = source.LogResponse,
            MaxConnectionsPerServer = source.MaxConnectionsPerServer,
            MaxStaleConnectionRetries = source.MaxStaleConnectionRetries,
            Profile = source.Profile,
            ProgressUpdateInterval = source.ProgressUpdateInterval,
            ProxyCredentials = source.ProxyCredentials,
            ProxyHost = source.ProxyHost,
            ProxyPort = source.ProxyPort,
            RegionEndpoint = region,
            RequestChecksumCalculation = source.RequestChecksumCalculation,
            RequestMinCompressionSizeBytes = source.RequestMinCompressionSizeBytes,
            ResignRetries = source.ResignRetries,
            ResponseChecksumValidation = source.ResponseChecksumValidation,
            RetryMode = source.RetryMode,
            SigV4aSigningRegionSet = source.SigV4aSigningRegionSet,
            SignatureMethod = source.SignatureMethod,
            TelemetryProvider = source.TelemetryProvider,
            ThrottleRetries = source.ThrottleRetries,
            UseAlternateUserAgentHeader = source.UseAlternateUserAgentHeader,
            UseDualstackEndpoint = source.UseDualstackEndpoint,
            UseFIPSEndpoint = source.UseFIPSEndpoint,
            UseHttp = source.UseHttp
        };
        if (source.IsMaxErrorRetrySet)
            copy.MaxErrorRetry = source.MaxErrorRetry;
        if (source.ConnectTimeout.HasValue)
            copy.ConnectTimeout = source.ConnectTimeout;
        if (source.Timeout.HasValue)
            copy.Timeout = source.Timeout;
        if (source.GetWebProxy() is { } proxy)
            copy.SetWebProxy(proxy);
        return copy;
    }
}
