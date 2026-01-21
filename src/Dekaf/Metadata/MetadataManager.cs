using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace Dekaf.Metadata;

/// <summary>
/// Manages cluster metadata with automatic refresh.
/// </summary>
public sealed class MetadataManager : IAsyncDisposable
{
    private readonly IConnectionPool _connectionPool;
    private readonly MetadataOptions _options;
    private readonly ILogger<MetadataManager>? _logger;
    private readonly ClusterMetadata _metadata = new();
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly List<string> _bootstrapServers;

    private short _metadataApiVersion = -1;
    private volatile bool _disposed;
    private CancellationTokenSource? _backgroundRefreshCts;
    private Task? _backgroundRefreshTask;

    public MetadataManager(
        IConnectionPool connectionPool,
        IEnumerable<string> bootstrapServers,
        MetadataOptions? options = null,
        ILogger<MetadataManager>? logger = null)
    {
        _connectionPool = connectionPool;
        _bootstrapServers = bootstrapServers.ToList();
        _options = options ?? new MetadataOptions();
        _logger = logger;
    }

    /// <summary>
    /// Gets the current cluster metadata.
    /// </summary>
    public ClusterMetadata Metadata => _metadata;

    /// <summary>
    /// Initializes the metadata manager by fetching initial metadata.
    /// </summary>
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        await RefreshMetadataAsync(cancellationToken).ConfigureAwait(false);

        if (_options.EnableBackgroundRefresh)
        {
            StartBackgroundRefresh();
        }
    }

    /// <summary>
    /// Gets topic metadata, fetching if necessary.
    /// </summary>
    public async ValueTask<TopicInfo?> GetTopicMetadataAsync(string topicName, CancellationToken cancellationToken = default)
    {
        var topic = _metadata.GetTopic(topicName);
        if (topic is not null && !_metadata.IsStale(_options.MetadataMaxAge))
        {
            return topic;
        }

        // Refresh metadata for this topic
        await RefreshMetadataAsync([topicName], cancellationToken).ConfigureAwait(false);
        return _metadata.GetTopic(topicName);
    }

    /// <summary>
    /// Gets the leader for a partition.
    /// </summary>
    public async ValueTask<BrokerNode?> GetPartitionLeaderAsync(
        string topicName,
        int partition,
        CancellationToken cancellationToken = default)
    {
        var leader = _metadata.GetPartitionLeader(topicName, partition);
        if (leader is not null && !_metadata.IsStale(_options.MetadataMaxAge))
        {
            return leader;
        }

        await RefreshMetadataAsync([topicName], cancellationToken).ConfigureAwait(false);
        return _metadata.GetPartitionLeader(topicName, partition);
    }

    /// <summary>
    /// Forces a metadata refresh.
    /// </summary>
    public async ValueTask RefreshMetadataAsync(CancellationToken cancellationToken = default)
    {
        await RefreshMetadataAsync(topics: null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Refreshes metadata for specific topics.
    /// </summary>
    public async ValueTask RefreshMetadataAsync(IEnumerable<string>? topics, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MetadataManager));

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await RefreshMetadataInternalAsync(topics, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async ValueTask RefreshMetadataInternalAsync(IEnumerable<string>? topics, CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        // Try each bootstrap server or known broker
        var endpoints = GetEndpointsToTry();

        foreach (var (host, port) in endpoints)
        {
            try
            {
                var connection = await _connectionPool.GetConnectionAsync(host, port, cancellationToken)
                    .ConfigureAwait(false);

                // Negotiate API version if not already done
                if (_metadataApiVersion < 0)
                {
                    await NegotiateApiVersionsAsync(connection, cancellationToken).ConfigureAwait(false);
                }

                // Build metadata request
                var request = topics is null
                    ? MetadataRequest.ForAllTopics()
                    : MetadataRequest.ForTopics(topics.ToArray());

                var response = await connection.SendAsync<MetadataRequest, MetadataResponse>(
                    request,
                    _metadataApiVersion,
                    cancellationToken).ConfigureAwait(false);

                _metadata.Update(response);

                // Register brokers with connection pool
                foreach (var broker in response.Brokers)
                {
                    _connectionPool.RegisterBroker(broker.NodeId, broker.Host, broker.Port);
                }

                _logger?.LogDebug(
                    "Refreshed metadata: {BrokerCount} brokers, {TopicCount} topics",
                    response.Brokers.Count,
                    response.Topics.Count);

                return;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to refresh metadata from {Host}:{Port}", host, port);
                lastException = ex;
            }
        }

        throw new InvalidOperationException("Failed to refresh metadata from any broker", lastException);
    }

    private async ValueTask NegotiateApiVersionsAsync(IKafkaConnection connection, CancellationToken cancellationToken)
    {
        // Use ApiVersions v0 for bootstrapping - it's the most compatible
        // and doesn't require flexible protocol support
        var request = new ApiVersionsRequest();

        var response = await connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
            request,
            0, // Use v0 for maximum compatibility during bootstrap
            cancellationToken).ConfigureAwait(false);

        if (response.ErrorCode != ErrorCode.None)
        {
            throw new InvalidOperationException($"ApiVersions failed: {response.ErrorCode}");
        }

        // Find metadata API version
        // Use v1 for now to avoid flexible version issues during debugging
        var metadataApi = response.ApiKeys.FirstOrDefault(a => a.ApiKey == ApiKey.Metadata);
        var maxSupported = Math.Min(metadataApi.MaxVersion, MetadataRequest.HighestSupportedVersion);
        // Cap at v8 to avoid flexible versions (v9+) for now
        _metadataApiVersion = maxSupported > 0
            ? Math.Min(maxSupported, (short)8)
            : MetadataRequest.LowestSupportedVersion;
        Console.WriteLine($"[Dekaf] Negotiated Metadata API version: {_metadataApiVersion} (broker supports up to {metadataApi.MaxVersion})");

        _logger?.LogDebug("Negotiated Metadata API version: {Version}", _metadataApiVersion);
    }

    private IEnumerable<(string Host, int Port)> GetEndpointsToTry()
    {
        // First try known brokers
        foreach (var broker in _metadata.GetBrokers())
        {
            yield return (broker.Host, broker.Port);
        }

        // Then bootstrap servers
        foreach (var server in _bootstrapServers)
        {
            var parts = server.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1], out var port))
            {
                yield return (parts[0], port);
            }
        }
    }

    private void StartBackgroundRefresh()
    {
        _backgroundRefreshCts = new CancellationTokenSource();
        _backgroundRefreshTask = BackgroundRefreshLoopAsync(_backgroundRefreshCts.Token);
    }

    private async Task BackgroundRefreshLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.MetadataRefreshInterval, cancellationToken).ConfigureAwait(false);
                await RefreshMetadataAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Background metadata refresh failed");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        _backgroundRefreshCts?.Cancel();

        if (_backgroundRefreshTask is not null)
        {
            try
            {
                await _backgroundRefreshTask.ConfigureAwait(false);
            }
            catch
            {
                // Ignore errors during shutdown
            }
        }

        _backgroundRefreshCts?.Dispose();
        _refreshLock.Dispose();
    }
}

/// <summary>
/// Options for metadata management.
/// </summary>
public sealed class MetadataOptions
{
    /// <summary>
    /// Maximum age of metadata before it's considered stale.
    /// </summary>
    public TimeSpan MetadataMaxAge { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Interval for background metadata refresh.
    /// </summary>
    public TimeSpan MetadataRefreshInterval { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether to enable background metadata refresh.
    /// </summary>
    public bool EnableBackgroundRefresh { get; init; } = true;

    /// <summary>
    /// Whether to allow auto-creation of topics.
    /// </summary>
    public bool AllowAutoTopicCreation { get; init; } = true;
}
