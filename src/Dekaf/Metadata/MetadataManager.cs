using System.Collections.Concurrent;
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
    private readonly List<(string Host, int Port)> _bootstrapEndpoints;
    private List<(string Host, int Port)>? _cachedEndpoints;
    private int _cachedBrokerCount = -1;
    private readonly object _endpointCacheLock = new();

    private volatile short _metadataApiVersion = -1;
    private readonly ConcurrentDictionary<ApiKey, (short MinVersion, short MaxVersion)> _brokerApiVersions = new();
    private readonly ConcurrentDictionary<(ApiKey, short, short), short> _negotiatedVersionCache = new();
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
        _options = options ?? new MetadataOptions();
        _logger = logger;

        // Pre-parse bootstrap servers to avoid allocation in hot path
        _bootstrapEndpoints = new List<(string Host, int Port)>();
        foreach (var server in bootstrapServers)
        {
            var colonIndex = server.IndexOf(':');
            if (colonIndex > 0 && colonIndex < server.Length - 1)
            {
                var host = server.Substring(0, colonIndex);
                var portStr = server.Substring(colonIndex + 1);
                if (int.TryParse(portStr, out var port))
                {
                    _bootstrapEndpoints.Add((host, port));
                }
            }
        }
    }

    /// <summary>
    /// Gets the current cluster metadata.
    /// </summary>
    public ClusterMetadata Metadata => _metadata;

    /// <summary>
    /// Gets the negotiated API version for the specified API key.
    /// Returns the minimum of the broker's max version and our supported version.
    /// Negotiated versions are cached for performance.
    /// </summary>
    public short GetNegotiatedApiVersion(ApiKey apiKey, short ourMinVersion, short ourMaxVersion)
    {
        var cacheKey = (apiKey, ourMinVersion, ourMaxVersion);

        // Check cache first (fast path)
        if (_negotiatedVersionCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        // Calculate and cache
        short negotiated;
        if (_brokerApiVersions.TryGetValue(apiKey, out var brokerVersions))
        {
            // Use the minimum of our max and broker's max
            negotiated = Math.Min(ourMaxVersion, brokerVersions.MaxVersion);
            // But not below our minimum or broker's minimum
            negotiated = Math.Max(negotiated, ourMinVersion);
            negotiated = Math.Max(negotiated, brokerVersions.MinVersion);
        }
        else
        {
            // Fall back to our minimum version if we don't have broker info yet
            negotiated = ourMinVersion;
        }

        // Cache it (benign race - same value computed)
        _negotiatedVersionCache.TryAdd(cacheKey, negotiated);
        return negotiated;
    }

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
    /// Retries if the topic is being created (has no partitions or transient error).
    /// </summary>
    public async ValueTask<TopicInfo?> GetTopicMetadataAsync(string topicName, CancellationToken cancellationToken = default)
    {
        var topic = _metadata.GetTopic(topicName);
        if (topic is not null && topic.PartitionCount > 0 && topic.ErrorCode == ErrorCode.None && !_metadata.IsStale(_options.MetadataMaxAge))
        {
            return topic;
        }

        // Retry logic for topics being created
        const int maxRetries = 3;
        const int retryDelayMs = 500;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            // Refresh metadata for this topic
            await RefreshMetadataAsync([topicName], cancellationToken).ConfigureAwait(false);
            topic = _metadata.GetTopic(topicName);

            if (topic is not null && topic.PartitionCount > 0 && topic.ErrorCode == ErrorCode.None)
            {
                return topic;
            }

            // Check for transient errors that indicate topic is being created
            if (topic?.ErrorCode is ErrorCode.LeaderNotAvailable or ErrorCode.UnknownTopicOrPartition)
            {
                await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                continue;
            }

            // Non-transient error or topic found with partitions
            break;
        }

        return topic;
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

        // Clear old versions and negotiated cache
        _brokerApiVersions.Clear();
        _negotiatedVersionCache.Clear();

        // Store all API versions and find metadata API in one pass (zero-allocation)
        ApiVersion? metadataApi = null;
        foreach (var apiKey in response.ApiKeys)
        {
            _brokerApiVersions[apiKey.ApiKey] = (apiKey.MinVersion, apiKey.MaxVersion);

            if (apiKey.ApiKey == ApiKey.Metadata)
            {
                metadataApi = apiKey;
            }
        }

        if (metadataApi is not null)
        {
            _metadataApiVersion = Math.Min(metadataApi.Value.MaxVersion, MetadataRequest.HighestSupportedVersion);
            if (_metadataApiVersion < MetadataRequest.LowestSupportedVersion)
            {
                _metadataApiVersion = MetadataRequest.LowestSupportedVersion;
            }
        }
        else
        {
            _metadataApiVersion = MetadataRequest.LowestSupportedVersion;
        }

        _logger?.LogDebug("Negotiated Metadata API version: {Version}", _metadataApiVersion);
    }

    private List<(string Host, int Port)> GetEndpointsToTry()
    {
        // Get current brokers - this allocates, but we need it to detect changes
        var currentBrokers = _metadata.GetBrokers();
        var currentBrokerCount = currentBrokers.Count;

        // Thread-safe cache check - avoid rebuilding if brokers haven't changed
        lock (_endpointCacheLock)
        {
            // Cache is valid if broker count hasn't changed
            if (_cachedEndpoints is not null && _cachedBrokerCount == currentBrokerCount)
            {
                // Return defensive copy to prevent caller modification
                return new List<(string Host, int Port)>(_cachedEndpoints);
            }

            // Build new endpoint list (allocation only when metadata changes)
            var endpoints = new List<(string Host, int Port)>(
                currentBrokerCount + _bootstrapEndpoints.Count);

            // First try known brokers
            foreach (var broker in currentBrokers)
            {
                endpoints.Add((broker.Host, broker.Port));
            }

            // Then bootstrap servers
            foreach (var endpoint in _bootstrapEndpoints)
            {
                endpoints.Add(endpoint);
            }

            // Update cache
            _cachedEndpoints = endpoints;
            _cachedBrokerCount = currentBrokerCount;

            // Return defensive copy
            return new List<(string Host, int Port)>(endpoints);
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
