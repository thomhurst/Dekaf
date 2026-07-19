using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using Dekaf.Errors;
using Dekaf.Internal;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Retry;
using Microsoft.Extensions.Logging;

namespace Dekaf.Metadata;

/// <summary>
/// Manages cluster metadata with automatic refresh.
/// </summary>
public sealed partial class MetadataManager : IAsyncDisposable
{
    private readonly IConnectionPool _connectionPool;
    private IConnectionPool? _additionalBrokerRegistrationTarget;
    private readonly MetadataOptions _options;
    private readonly ILogger _logger;
    private readonly ClusterMetadata _metadata = new();
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly List<(string Host, int Port)> _bootstrapEndpoints;
    private List<(int BrokerId, string Host, int Port)>? _cachedEndpoints;
    private int _cachedBrokerHash;
    private readonly object _endpointCacheLock = new();

    private volatile short _metadataApiVersion = -1;
    private readonly ConcurrentDictionary<ApiKey, (short MinVersion, short MaxVersion)> _brokerApiVersions = new();
    private readonly ConcurrentDictionary<(ApiKey, short, short), short> _negotiatedVersionCache = new();
    private readonly object _finalizedFeatureLock = new();
    private FinalizedFeatureSnapshot? _finalizedFeatures;
    private string? _finalizedFeatureClusterId;
    private string? _trustedMetadataClusterId;
    private readonly SemaphoreSlim _initializeLock = new(1, 1);
    private volatile bool _initialized;
    private volatile bool _hasSuccessfulRefresh;
    private int _initializationAttempt;
    private int _disposed;
    private readonly CancellationTokenSource _disposalCts = new();
    private readonly object _backgroundRefreshGate = new();
    private CancellationTokenSource? _backgroundRefreshCts;
    private Task? _backgroundRefreshTask;

    private readonly ConcurrentDictionary<string, Lazy<Task<TopicInfo?>>> _pendingTopicFetches = new();

    private const int BootstrapWarningAttempt = 6;

    // Rebootstrap recovery state
    private readonly List<string> _originalBootstrapHostnames;
    private long _allBrokersUnavailableSince;

    /// <summary>
    /// Optional callbacks invoked after each successful metadata refresh with the discovered broker count.
    /// Used by producers to re-ratchet shared pool sizes once the real cluster size is known.
    /// </summary>
    private readonly object _brokerCountDiscoveredLock = new();
    private Action<int>? _brokerCountDiscoveredCallbacks;

    internal IDisposable AddBrokerCountDiscoveredCallback(Action<int> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        lock (_brokerCountDiscoveredLock)
        {
            _brokerCountDiscoveredCallbacks += callback;
        }

        return new BrokerCountDiscoveredCallbackRegistration(this, callback);
    }

    internal int BrokerCountDiscoveredCallbackCount
    {
        get
        {
            lock (_brokerCountDiscoveredLock)
            {
                return _brokerCountDiscoveredCallbacks?.GetInvocationList().Length ?? 0;
            }
        }
    }

    internal void NotifyBrokerCountDiscovered(int brokerCount)
    {
        Action<int>? callbacks;
        lock (_brokerCountDiscoveredLock)
        {
            callbacks = _brokerCountDiscoveredCallbacks;
        }

        callbacks?.Invoke(brokerCount);
    }

    private sealed class BrokerCountDiscoveredCallbackRegistration(
        MetadataManager metadataManager,
        Action<int> callback) : IDisposable
    {
        private Action<int>? _callback = callback;

        public void Dispose()
        {
            lock (metadataManager._brokerCountDiscoveredLock)
            {
                if (_callback is null)
                    return;

                metadataManager._brokerCountDiscoveredCallbacks -= _callback;
                _callback = null;
            }
        }
    }

    public MetadataManager(
        IConnectionPool connectionPool,
        IEnumerable<string> bootstrapServers,
        MetadataOptions? options = null,
        ILogger<MetadataManager>? logger = null)
    {
        _connectionPool = connectionPool;
        _options = options ?? new MetadataOptions();
        ConfigureMetadataClusterCheck(_connectionPool);
        ExponentialRetryBackoff.Validate(_options.RetryBackoffMs, _options.RetryBackoffMaxMs);
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MetadataManager>.Instance;
        if (connectionPool is IConnectionCapabilityObserverPool observerPool)
            observerPool.SetConnectionCapabilityObserver(ObserveConnectionCapabilities);

        // Pre-parse bootstrap servers to avoid allocation in hot path
        _bootstrapEndpoints = new List<(string Host, int Port)>();
        _originalBootstrapHostnames = new List<string>();
        foreach (var server in bootstrapServers)
        {
            var colonIndex = server.IndexOf(':');
            if (colonIndex > 0 && colonIndex < server.Length - 1)
            {
                var host = server.Substring(0, colonIndex);
#if NETSTANDARD2_0
                if (int.TryParse(server.Substring(colonIndex + 1), out var port))
#else
                if (int.TryParse(server.AsSpan(colonIndex + 1), out var port))
#endif
                {
                    _bootstrapEndpoints.Add((host, port));
                    _originalBootstrapHostnames.Add(server);
                }
            }
        }
    }

    internal void SetAdditionalBrokerRegistrationTarget(IConnectionPool connectionPool)
    {
        ArgumentNullException.ThrowIfNull(connectionPool);
        if (Interlocked.CompareExchange(
                ref _additionalBrokerRegistrationTarget,
                connectionPool,
                null) is not null)
        {
            throw new InvalidOperationException("An additional broker registration target is already configured");
        }

        if (connectionPool is IConnectionCapabilityObserverPool observerPool)
            observerPool.SetConnectionCapabilityObserver(ObserveConnectionCapabilities);

        ConfigureMetadataClusterCheck(connectionPool);
        UpdateMetadataClusterId(connectionPool, Volatile.Read(ref _trustedMetadataClusterId));
    }

    private void ConfigureMetadataClusterCheck(IConnectionPool connectionPool)
    {
        if (connectionPool is IMetadataClusterIdentityPool identityPool)
        {
            identityPool.ConfigureMetadataClusterCheck(
                _options.MetadataClusterCheckEnabled
                && _options.MetadataRecoveryStrategy == MetadataRecoveryStrategy.Rebootstrap);
        }
    }

    private static void UpdateMetadataClusterId(IConnectionPool connectionPool, string? clusterId)
    {
        if (connectionPool is IMetadataClusterIdentityPool identityPool)
            identityPool.UpdateMetadataClusterId(clusterId);
    }

    private void UpdateMetadataClusterId(string? clusterId)
    {
        Volatile.Write(ref _trustedMetadataClusterId, clusterId);
        UpdateMetadataClusterId(_connectionPool, clusterId);
        var additionalPool = Volatile.Read(ref _additionalBrokerRegistrationTarget);
        if (additionalPool is not null)
            UpdateMetadataClusterId(additionalPool, clusterId);
    }

    private void BeginMetadataRebootstrap()
    {
        Volatile.Write(ref _trustedMetadataClusterId, null);
        if (_connectionPool is IMetadataClusterIdentityPool identityPool)
            identityPool.BeginMetadataRebootstrap();

        if (Volatile.Read(ref _additionalBrokerRegistrationTarget) is IMetadataClusterIdentityPool additionalIdentityPool)
            additionalIdentityPool.BeginMetadataRebootstrap();
    }

    private bool TryConsumeMetadataRebootstrapRequest()
    {
        var requested = _connectionPool is IMetadataClusterIdentityPool identityPool
                        && identityPool.TryConsumeMetadataRebootstrapRequest();
        var additionalPool = Volatile.Read(ref _additionalBrokerRegistrationTarget);
        if (additionalPool is IMetadataClusterIdentityPool additionalIdentityPool)
            requested |= additionalIdentityPool.TryConsumeMetadataRebootstrapRequest();

        return requested;
    }

    /// <summary>
    /// Gets the current cluster metadata.
    /// </summary>
    public ClusterMetadata Metadata => _metadata;

    /// <summary>
    /// Returns true if the broker reported support for the given API key during version negotiation.
    /// </summary>
    public bool HasApiKey(ApiKey apiKey) => _brokerApiVersions.ContainsKey(apiKey);

    internal bool HasApiKey(IKafkaConnection connection, ApiKey apiKey) =>
        connection is IKafkaCapabilityProvider provider
            ? provider.Capabilities.HasApi(apiKey)
            : HasApiKey(apiKey);

    internal bool SupportsApiVersion(ApiKey apiKey, short version) =>
        _brokerApiVersions.TryGetValue(apiKey, out var versions) &&
        versions.MinVersion <= version &&
        versions.MaxVersion >= version;

    internal bool SupportsApiVersion(IKafkaConnection connection, ApiKey apiKey, short version) =>
        connection is IKafkaCapabilityProvider provider
            ? provider.Capabilities.SupportsVersion(apiKey, version)
            : SupportsApiVersion(apiKey, version);

    /// <summary>
    /// Seeds a broker API version entry. Internal — used by unit tests to bypass negotiation.
    /// </summary>
    internal void SetApiVersion(ApiKey apiKey, short minVersion, short maxVersion)
    {
        _brokerApiVersions[apiKey] = (minVersion, maxVersion);
        _negotiatedVersionCache.Clear();
    }

    /// <summary>
    /// Attempts to get the highest API version supported by both the broker and client.
    /// Negotiated versions are cached for performance.
    /// </summary>
    public bool TryGetNegotiatedApiVersion(
        ApiKey apiKey,
        short ourMinVersion,
        short ourMaxVersion,
        out short negotiatedVersion)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ourMinVersion, ourMaxVersion);

        var cacheKey = (apiKey, ourMinVersion, ourMaxVersion);

        // Check cache first (fast path)
        if (_negotiatedVersionCache.TryGetValue(cacheKey, out var cached))
        {
            negotiatedVersion = cached;
            return true;
        }

        if (!_brokerApiVersions.TryGetValue(apiKey, out var brokerVersions))
        {
            negotiatedVersion = default;
            return false;
        }

        if (!ApiVersionNegotiator.TryNegotiate(
                brokerVersions.MinVersion,
                brokerVersions.MaxVersion,
                ourMinVersion,
                ourMaxVersion,
                out var highestUsableVersion))
        {
            negotiatedVersion = default;
            return false;
        }

        // Benign race: concurrent callers calculate the same intersection.
        _negotiatedVersionCache.TryAdd(cacheKey, highestUsableVersion);
        negotiatedVersion = highestUsableVersion;
        return true;
    }

    internal short GetNegotiatedApiVersion(
        IKafkaConnection connection,
        ApiKey apiKey,
        short ourMinVersion,
        short ourMaxVersion) =>
        connection is IKafkaCapabilityProvider provider
            ? provider.Capabilities.NegotiateVersion(apiKey, ourMinVersion, ourMaxVersion)
            : GetNegotiatedApiVersion(apiKey, ourMinVersion, ourMaxVersion);

    internal bool TryGetNegotiatedApiVersion(
        IKafkaConnection connection,
        ApiKey apiKey,
        short ourMinVersion,
        short ourMaxVersion,
        out short negotiatedVersion) =>
        connection is IKafkaCapabilityProvider provider
            ? provider.Capabilities.TryNegotiateVersion(
                apiKey,
                ourMinVersion,
                ourMaxVersion,
                out negotiatedVersion)
            : TryGetNegotiatedApiVersion(
                apiKey,
                ourMinVersion,
                ourMaxVersion,
                out negotiatedVersion);

    /// <summary>
    /// Gets the highest API version supported by both the broker and client.
    /// </summary>
    /// <exception cref="BrokerVersionException">
    /// The broker omitted the API key or its supported range does not overlap the client range.
    /// </exception>
    public short GetNegotiatedApiVersion(ApiKey apiKey, short ourMinVersion, short ourMaxVersion)
    {
        // Keep this cache-hit path direct. Delegating to TryGetNegotiatedApiVersion measurably
        // regresses request-path throughput even though both methods use the same negotiation rules.
        var cacheKey = (apiKey, ourMinVersion, ourMaxVersion);
        if (_negotiatedVersionCache.TryGetValue(cacheKey, out var negotiatedVersion))
        {
            return negotiatedVersion;
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(ourMinVersion, ourMaxVersion);

        if (!_brokerApiVersions.TryGetValue(apiKey, out var brokerVersions))
        {
            ApiVersionNegotiator.ThrowApiAbsent(apiKey, ourMinVersion, ourMaxVersion);
        }

        if (!ApiVersionNegotiator.TryNegotiate(
                brokerVersions.MinVersion,
                brokerVersions.MaxVersion,
                ourMinVersion,
                ourMaxVersion,
                out var highestUsableVersion))
        {
            ApiVersionNegotiator.ThrowDisjointRange(
                apiKey,
                brokerVersions.MinVersion,
                brokerVersions.MaxVersion,
                ourMinVersion,
                ourMaxVersion);
        }

        _negotiatedVersionCache.TryAdd(cacheKey, highestUsableVersion);
        return highestUsableVersion;
    }

    /// <summary>
    /// Returns whether finalized cluster state is unavailable, the feature is absent, or it is present.
    /// </summary>
    internal FinalizedFeatureStatus GetFinalizedFeatureStatus(
        string featureName,
        out short maxVersionLevel)
    {
        var snapshot = Volatile.Read(ref _finalizedFeatures);
        if (snapshot is null)
        {
            maxVersionLevel = default;
            return FinalizedFeatureStatus.Unavailable;
        }

        return snapshot.GetFeatureStatus(featureName, out maxVersionLevel);
    }

    internal bool TryGetFinalizedFeatureVersion(string featureName, out short maxVersionLevel) =>
        GetFinalizedFeatureStatus(featureName, out maxVersionLevel) == FinalizedFeatureStatus.Present;

    private void ObserveConnectionCapabilities(KafkaConnectionCapabilities capabilities)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        var candidate = capabilities.FinalizedFeatureSnapshot;
        if (candidate is null)
            return;

        lock (_finalizedFeatureLock)
        {
            if (_finalizedFeatureClusterId is null)
                return;

            PublishFinalizedFeatures(candidate);
        }
    }

    private void ObserveClusterCapabilities(string? clusterId, IKafkaConnection connection)
    {
        if (connection is not IKafkaCapabilityProvider provider)
            return;

        ObserveClusterCapabilities(clusterId, provider.Capabilities);
    }

    internal void ObserveClusterCapabilities(
        string? clusterId,
        KafkaConnectionCapabilities capabilities)
    {
        lock (_finalizedFeatureLock)
        {
            if (!string.Equals(_finalizedFeatureClusterId, clusterId, StringComparison.Ordinal))
            {
                _finalizedFeatureClusterId = clusterId;
                Volatile.Write(ref _finalizedFeatures, null);
            }

            if (clusterId is null)
                return;

            var candidate = capabilities.FinalizedFeatureSnapshot;
            if (candidate is not null)
                PublishFinalizedFeatures(candidate);
        }
    }

    private void PublishFinalizedFeatures(FinalizedFeatureSnapshot candidate)
    {
        var current = Volatile.Read(ref _finalizedFeatures);
        if (current is not null)
        {
            if (candidate.Epoch < current.Epoch)
                return;

            if (candidate.Epoch == current.Epoch)
            {
                if (current.HasSameContent(candidate))
                    return;

                LogConflictingFinalizedFeatures(candidate.Epoch);
                throw new KafkaException(
                    ErrorCode.FeatureUpdateFailed,
                    $"Brokers returned conflicting finalized feature state for epoch {candidate.Epoch}");
            }
        }

        Volatile.Write(ref _finalizedFeatures, candidate);
    }

    private void ResetFinalizedFeaturesForRebootstrap()
    {
        lock (_finalizedFeatureLock)
        {
            _finalizedFeatureClusterId = null;
            Volatile.Write(ref _finalizedFeatures, null);
        }
    }

    /// <summary>
    /// Initializes the metadata manager by fetching initial metadata.
    /// Retries with exponential backoff matching Java client's reconnect.backoff behavior.
    /// </summary>
    /// <summary>
    /// Errors that no other broker (or retry) can resolve: the same broker-version mismatch,
    /// credentials, or authorization decision applies everywhere, so fail fast and surface the
    /// real cause instead of masking it as a generic metadata-refresh failure.
    /// </summary>
    private bool IsFatalMetadataError(Exception ex)
    {
        if (ex is BrokerVersionException or AuthenticationException or AuthorizationException)
            return true;

        // KafkaConnection instances can be disposed independently during pool churn. Only the
        // manager's own disposal is fatal; recycled connections should fall through to retry.
        return ex is ObjectDisposedException && Volatile.Read(ref _disposed) != 0;
    }

    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(MetadataManager));

        if (_initialized)
            return;

        await SemaphoreHelper.AcquireOrThrowDisposedAsync(_initializeLock, nameof(MetadataManager), cancellationToken)
            .ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(MetadataManager));

            if (_initialized)
                return;

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposalCts.Token);
            var initializationToken = linkedCts.Token;
            var startedAt = Stopwatch.GetTimestamp();

            try
            {
                for (var attempt = 0; ; attempt++)
                {
                    Volatile.Write(ref _initializationAttempt, attempt + 1);
                    try
                    {
                        await RefreshMetadataAsync(initializationToken).ConfigureAwait(false);
                        break;
                    }
                    catch (Exception ex) when (!IsFatalMetadataError(ex) && !initializationToken.IsCancellationRequested)
                    {
                        if (attempt >= _options.MaxInitRetries)
                        {
                            LogMetadataInitializationAbandoned(ex, attempt + 1);
                            throw;
                        }

                        var elapsed = Stopwatch.GetElapsedTime(startedAt);
                        var remainingMs = _options.InitTimeoutMs - elapsed.TotalMilliseconds;
                        if (remainingMs <= 0)
                        {
                            LogMetadataInitializationAbandoned(ex, attempt + 1);
                            throw new KafkaTimeoutException(
                                TimeoutKind.Metadata,
                                elapsed,
                                TimeSpan.FromMilliseconds(_options.InitTimeoutMs),
                                $"Failed to fetch initial metadata within {_options.InitTimeoutMs}ms. " +
                                "Ensure the Kafka cluster is reachable and the bootstrap servers are correct.",
                                ex);
                        }

                        var backoffMs = ExponentialRetryBackoff.CalculateDelayMilliseconds(
                            _options.RetryBackoffMs,
                            _options.RetryBackoffMaxMs,
                            attempt + 1);
                        LogMetadataInitializationFailed(ex, attempt + 1, backoffMs);
                        await Task.Delay((int)Math.Min(backoffMs, remainingMs), initializationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (_disposalCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new ObjectDisposedException(nameof(MetadataManager));
            }
            finally
            {
                Volatile.Write(ref _initializationAttempt, 0);
            }

            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(MetadataManager));

            if (_options.EnableBackgroundRefresh)
            {
                StartBackgroundRefresh();
            }

            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(MetadataManager));

            _initialized = true;
        }
        finally
        {
            SemaphoreHelper.ReleaseSafely(_initializeLock);
        }
    }

    /// <summary>
    /// Attempts to get topic metadata from cache synchronously.
    /// Returns true if valid cached metadata exists, false if topic is unknown.
    /// This is the fast path - no async overhead, no allocations.
    /// </summary>
    /// <remarks>
    /// Does NOT check metadata staleness. Background refresh keeps metadata fresh,
    /// and the producer should use cached metadata optimistically. This avoids
    /// falling back to the slow path (with allocations) just because an arbitrary
    /// time threshold was exceeded. If metadata is truly stale (e.g., leader changed),
    /// the actual send will fail and trigger an on-demand refresh.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetCachedTopicMetadata(string topicName, out TopicInfo? topic)
    {
        topic = _metadata.GetTopic(topicName);
        return topic is not null
            && topic.PartitionCount > 0
            && topic.ErrorCode == ErrorCode.None;
    }

    /// <summary>
    /// Gets topic metadata, fetching if necessary.
    /// Retries if the topic is being created (has no partitions or transient error).
    /// </summary>
    public async ValueTask<TopicInfo?> GetTopicMetadataAsync(string topicName, CancellationToken cancellationToken = default)
    {
        // Fast path: check cache synchronously first
        if (TryGetCachedTopicMetadata(topicName, out var topic))
        {
            return topic;
        }

        // Slow path: need to refresh metadata
        return await GetTopicMetadataSlowAsync(topicName, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Synchronously blocks until topic metadata is available, coalescing concurrent requests
    /// for the same topic into a single metadata fetch. This prevents the thundering herd problem
    /// when Send() (fire-and-forget) encounters a topic not yet in the metadata cache.
    /// </summary>
    /// <remarks>
    /// Analogous to Java client's waitOnMetadata. Uses Lazy&lt;Task&gt; via GetOrAdd to ensure
    /// only one metadata fetch is in-flight per topic, regardless of how many threads call this
    /// concurrently. The Lazy wrapper ensures the Task factory runs exactly once even under
    /// concurrent GetOrAdd calls (ConcurrentDictionary may invoke the factory speculatively,
    /// but Lazy defers execution until .Value is accessed).
    /// </remarks>
    internal TopicInfo? WaitForTopicMetadataSync(string topicName, int timeoutMs)
    {
        if (TryGetCachedTopicMetadata(topicName, out var topic))
            return topic;

        // Coalesce: all callers for the same topic share one fetch task.
        // The inner task uses _disposalCts so it is cancelled promptly on disposal,
        // regardless of the caller's timeout. This is intentional — the underlying
        // network operation should continue running even if one waiter times out,
        // so that other waiters (or retry callers) can benefit from the result.
        var sharedTask = _pendingTopicFetches.GetOrAdd(topicName,
            static (t, self) => new Lazy<Task<TopicInfo?>>(
                () => self.GetTopicMetadataSlowAsync(t, self._disposalCts.Token).AsTask()),
            this);

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_disposalCts.Token);
            timeoutCts.CancelAfter(timeoutMs);
            return sharedTask.Value.WaitAsync(timeoutCts.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (_disposalCts.IsCancellationRequested)
        {
            throw new ObjectDisposedException(nameof(MetadataManager));
        }
        catch (OperationCanceledException)
        {
            throw new KafkaTimeoutException(
                TimeoutKind.Metadata,
                TimeSpan.FromMilliseconds(timeoutMs),
                TimeSpan.FromMilliseconds(timeoutMs),
                $"Failed to fetch metadata for topic '{topicName}' within max.block.ms ({timeoutMs}ms). " +
                $"Ensure the topic exists and the Kafka cluster is reachable.");
        }
        finally
        {
            if (sharedTask.IsValueCreated && sharedTask.Value.IsCompleted)
            {
                _pendingTopicFetches.TryRemove(topicName, out _);
            }
            else if (sharedTask.IsValueCreated)
            {
                // We timed out but the inner task is still running.
                // Remove unconditionally on completion so callers always get a fresh
                // fetch — a successful-but-late result would otherwise leave a stale
                // entry permanently in the dictionary.
                sharedTask.Value.ContinueWith(static (t, state) =>
                {
                    var (dict, key) = ((ConcurrentDictionary<string, Lazy<Task<TopicInfo?>>>, string))state!;
                    dict.TryRemove(key, out _);
                }, (_pendingTopicFetches, topicName),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            }
        }
    }

    /// <summary>
    /// Slow path for topic metadata retrieval when cache miss occurs.
    /// Separated from GetTopicMetadataAsync to keep the fast path inlined.
    /// </summary>
    /// <remarks>
    /// Retries with exponential backoff until the cancellation token fires (typically
    /// bounded by the caller's max.block.ms timeout). This matches the Java client's
    /// waitOnMetadata behavior: loop until metadata is available or timeout expires,
    /// rather than giving up after a fixed number of attempts.
    /// </remarks>
    private async ValueTask<TopicInfo?> GetTopicMetadataSlowAsync(string topicName, CancellationToken cancellationToken)
    {
        TopicInfo? topic = null;
        var failureCount = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            // Refresh metadata for this topic
            await RefreshMetadataAsync([topicName], cancellationToken: cancellationToken).ConfigureAwait(false);
            topic = _metadata.GetTopic(topicName);

            if (topic is not null && topic.PartitionCount > 0 && topic.ErrorCode == ErrorCode.None)
            {
                return topic;
            }

            // Retry for transient states: topic not yet in response (null), being created,
            // or leader election in progress. These are all expected during topic creation.
            if (topic is null
                || topic.ErrorCode is ErrorCode.LeaderNotAvailable or ErrorCode.UnknownTopicOrPartition)
            {
                failureCount++;
                var retryDelayMs = ExponentialRetryBackoff.CalculateDelayMilliseconds(
                    _options.RetryBackoffMs,
                    _options.RetryBackoffMaxMs,
                    failureCount);
                await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                continue;
            }

            // Non-transient error — no point retrying
            break;
        }

        return topic;
    }

    /// <summary>
    /// Gets the cached leader for a partition without triggering a metadata refresh.
    /// Thread-safe and allocation-free — reads from an immutable snapshot.
    /// Returns null if the partition leader is unknown (metadata not yet fetched or stale).
    /// </summary>
    public BrokerNode? TryGetCachedPartitionLeader(string topicName, int partition)
        => _metadata.GetPartitionLeader(topicName, partition);

    /// <summary>
    /// Applies inline leader information from Produce/Fetch responses when it advances the cached epoch.
    /// </summary>
    internal bool TryUpdatePartitionLeader(
        string topicName,
        int partition,
        int leaderId,
        int leaderEpoch,
        NodeEndpoint? leaderEndpoint = null)
    {
        BrokerNode? broker = null;
        if (leaderEndpoint is not null)
        {
            broker = new BrokerNode
            {
                NodeId = leaderEndpoint.NodeId,
                Host = leaderEndpoint.Host,
                Port = leaderEndpoint.Port,
                Rack = leaderEndpoint.Rack
            };
        }

        return _metadata.TryUpdatePartitionLeader(topicName, partition, leaderId, leaderEpoch, broker);
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
        if (leader is not null)
        {
            LogMetadataCacheHit(topicName, partition);
            return leader;
        }

        LogMetadataCacheMiss(topicName, partition);
        // Force refresh: the topic may be cached but with LeaderId=-1 for this partition
        // (leader election in progress). Without forceRefresh, AllTopicsCached returns true
        // and the stale metadata persists — the partition is permanently unreachable.
        await RefreshMetadataAsync([topicName], forceRefresh: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        return _metadata.GetPartitionLeader(topicName, partition);
    }

    /// <summary>
    /// Returns all TopicPartitions currently led by the given broker node.
    /// Uses the pre-built reverse index in the metadata snapshot for O(1) lookup
    /// instead of scanning all topics × partitions.
    /// </summary>
    /// <remarks>
    /// Thin pass-through to <see cref="ClusterMetadata.GetPartitionsForBroker"/>.
    /// Kept as a facade so callers (RecordAccumulator) depend on MetadataManager
    /// rather than reaching into ClusterMetadata directly.
    /// </remarks>
    internal IReadOnlyList<TopicPartition> GetPartitionsForNode(int nodeId)
    {
        return _metadata.GetPartitionsForBroker(nodeId);
    }

    /// <summary>
    /// Forces a metadata refresh.
    /// </summary>
    public async ValueTask RefreshMetadataAsync(CancellationToken cancellationToken = default)
    {
        await RefreshMetadataAsync(topics: null, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Refreshes metadata for specific topics.
    /// </summary>
    public async ValueTask RefreshMetadataAsync(IEnumerable<string>? topics, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(MetadataManager));

        LogMetadataRefreshRequested();
        await SemaphoreHelper.AcquireOrThrowDisposedAsync(_refreshLock, nameof(MetadataManager), cancellationToken)
            .ConfigureAwait(false);
        try
        {
            // Re-check cache after acquiring lock — another thread may have refreshed
            // the metadata we need while we were waiting for the lock, avoiding a
            // redundant network request. When topics is null, this is an explicit
            // full-cluster refresh (background/init) that should always hit the network.
            if (!forceRefresh && topics is not null && AllTopicsCached(topics))
            {
                LogMetadataRefreshSkippedCacheHit();
                return;
            }

            await RefreshMetadataInternalAsync(topics, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            SemaphoreHelper.ReleaseSafely(_refreshLock);
        }
    }

    /// <summary>
    /// Returns true if all requested topics already have valid cached metadata
    /// (non-null, with partitions, no error code).
    /// </summary>
    private bool AllTopicsCached(IEnumerable<string> topics)
    {
        foreach (var topicName in topics)
        {
            if (!TryGetCachedTopicMetadata(topicName, out _))
            {
                return false;
            }
        }

        return true;
    }

    private async ValueTask RefreshMetadataInternalAsync(IEnumerable<string>? topics, CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        if (_options.MetadataRecoveryStrategy == MetadataRecoveryStrategy.Rebootstrap
            && TryConsumeMetadataRebootstrapRequest())
        {
            if (await ExecuteRebootstrapAsync(topics, cancellationToken).ConfigureAwait(false))
            {
                _hasSuccessfulRefresh = true;
                return;
            }
        }

        // Try each bootstrap server or known broker
        var endpoints = GetEndpointsToTry();

        foreach (var (brokerId, host, port) in endpoints)
        {
            try
            {
                using var connectionLease = brokerId >= 0
                    ? await _connectionPool.LeaseConnectionAsync(brokerId, cancellationToken).ConfigureAwait(false)
                    : await _connectionPool.LeaseConnectionAsync(host, port, cancellationToken).ConfigureAwait(false);
                var connection = connectionLease.Connection;

                // Production connections negotiate before becoming ready. Keep explicit
                // negotiation only for injected test connections without a capability snapshot.
                KafkaConnectionCapabilities? negotiatedCapabilities = null;
                if (connection is not IKafkaCapabilityProvider && _metadataApiVersion < 0)
                {
                    negotiatedCapabilities = await NegotiateApiVersionsAsync(
                        connection,
                        cancellationToken).ConfigureAwait(false);
                }

                var metadataApiVersion = connection is IKafkaCapabilityProvider
                    ? GetNegotiatedApiVersion(
                        connection,
                        ApiKey.Metadata,
                        MetadataRequest.LowestSupportedVersion,
                        MetadataRequest.HighestSupportedVersion)
                    : _metadataApiVersion;

                // Build metadata request
                var request = topics is null
                    ? MetadataRequest.ForAllTopics()
                    : MetadataRequest.ForTopics(topics);

                var response = await connection.SendAsync<MetadataRequest, MetadataResponse>(
                    request,
                    metadataApiVersion,
                    cancellationToken).ConfigureAwait(false);

                UpdateVersionlessCapabilities(connection, metadataApiVersion);

                // KIP-1102: If the broker signals rebootstrap, prefer fresh topology
                // from re-resolved DNS over the current response's potentially-stale data.
                if (response.ErrorCode == ErrorCode.RebootstrapRequired
                    && _options.MetadataRecoveryStrategy == MetadataRecoveryStrategy.Rebootstrap)
                {
                    var rebootstrapped = await TryRebootstrapImmediateAsync(topics, cancellationToken).ConfigureAwait(false);
                    if (rebootstrapped)
                    {
                        ResetAllBrokersUnavailableTimestamp();
                        _hasSuccessfulRefresh = true;
                        return;
                    }
                    // Rebootstrap failed — fall through and apply the original response as best-effort fallback
                }

                if (negotiatedCapabilities is not null)
                    ObserveClusterCapabilities(response.ClusterId, negotiatedCapabilities);
                else
                    ObserveClusterCapabilities(response.ClusterId, connection);

                // Topic-specific requests merge into the existing snapshot to preserve
                // metadata for other topics. Full-cluster requests replace the snapshot.
                // This matches the Java client's incremental metadata update behavior.
                _metadata.Update(response, mergeTopics: topics is not null);
                if (response.ErrorCode != ErrorCode.RebootstrapRequired)
                    UpdateMetadataClusterId(response.ClusterId);

                // Register brokers with connection pool
                foreach (var broker in response.Brokers)
                {
                    RegisterBroker(broker.NodeId, broker.Host, broker.Port);
                }

                LogMetadataRefreshed(response.Brokers.Count, response.Topics.Count);

                NotifyBrokerCountDiscovered(response.Brokers.Count);

                // Success - reset the rebootstrap timer
                ResetAllBrokersUnavailableTimestamp();
                _hasSuccessfulRefresh = true;

                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (IsFatalMetadataError(ex))
            {
                throw; // No other broker will resolve this — surface the real cause.
            }
            catch (Exception ex)
            {
                LogMetadataRefreshFailed(ex, host, port);
                lastException = ex;
            }
        }

        // All known endpoints failed - try rebootstrap if configured
        if (_options.MetadataRecoveryStrategy == MetadataRecoveryStrategy.Rebootstrap)
        {
            var rebootstrapped = await TryRebootstrapAsync(topics, cancellationToken).ConfigureAwait(false);
            if (rebootstrapped)
            {
                _hasSuccessfulRefresh = true;
                return;
            }
        }

        throw new InvalidOperationException("Failed to refresh metadata from any broker", lastException);
    }

    /// <summary>
    /// Timer-gated rebootstrap: attempts recovery by re-resolving bootstrap server DNS to discover new broker IPs.
    /// Only triggers after the configured delay has elapsed since all brokers became unavailable.
    /// </summary>
    internal async ValueTask<bool> TryRebootstrapAsync(IEnumerable<string>? topics, CancellationToken cancellationToken)
    {
        var now = Dekaf.MonotonicClock.GetMilliseconds();

        // Atomically set the timestamp only if it hasn't been set yet (compare-and-set from 0)
        if (Interlocked.CompareExchange(ref _allBrokersUnavailableSince, now, 0) == 0)
        {
            // First time all brokers are unavailable - we just recorded the timestamp
            LogAllBrokersUnavailable(_options.MetadataRecoveryRebootstrapTriggerMs);
            return false;
        }

        var elapsedMs = now - Interlocked.Read(ref _allBrokersUnavailableSince);
        if (elapsedMs < _options.MetadataRecoveryRebootstrapTriggerMs)
        {
            LogRebootstrapNotYetTriggered(elapsedMs, _options.MetadataRecoveryRebootstrapTriggerMs);
            return false;
        }

        LogRebootstrapTriggered(elapsedMs);
        return await ExecuteRebootstrapAsync(topics, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Broker-directed immediate rebootstrap (KIP-1102): skips the timer delay and re-resolves DNS immediately.
    /// Called when a broker returns <see cref="ErrorCode.RebootstrapRequired"/> in a MetadataResponse.
    /// </summary>
    internal async ValueTask<bool> TryRebootstrapImmediateAsync(IEnumerable<string>? topics, CancellationToken cancellationToken)
    {
        LogBrokerInitiatedRebootstrap();
        return await ExecuteRebootstrapAsync(topics, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Shared rebootstrap execution: re-resolves bootstrap DNS and attempts metadata fetch from new endpoints.
    /// Intentionally does not re-check the response for <see cref="ErrorCode.RebootstrapRequired"/>
    /// to prevent infinite recursion when the new broker also signals rebootstrap.
    /// </summary>
    private async ValueTask<bool> ExecuteRebootstrapAsync(IEnumerable<string>? topics, CancellationToken cancellationToken)
    {
        // Re-resolve DNS for each original bootstrap server
        var newEndpoints = await ResolveBootstrapEndpointsAsync(cancellationToken).ConfigureAwait(false);

        if (newEndpoints.Count == 0)
        {
            LogRebootstrapDnsNoEndpoints();
            return false;
        }

        // Try the newly resolved endpoints
        Exception? lastException = null;
        foreach (var (host, port) in newEndpoints)
        {
            try
            {
                using var connectionLease = await _connectionPool.LeaseConnectionAsync(host, port, cancellationToken)
                    .ConfigureAwait(false);
                var connection = connectionLease.Connection;

                KafkaConnectionCapabilities? negotiatedCapabilities = null;
                if (connection is not IKafkaCapabilityProvider)
                {
                    negotiatedCapabilities = await NegotiateApiVersionsAsync(
                        connection,
                        cancellationToken).ConfigureAwait(false);
                }

                var metadataApiVersion = connection is IKafkaCapabilityProvider
                    ? GetNegotiatedApiVersion(
                        connection,
                        ApiKey.Metadata,
                        MetadataRequest.LowestSupportedVersion,
                        MetadataRequest.HighestSupportedVersion)
                    : _metadataApiVersion;

                var request = topics is null
                    ? MetadataRequest.ForAllTopics()
                    : MetadataRequest.ForTopics(topics);

                var response = await connection.SendAsync<MetadataRequest, MetadataResponse>(
                    request,
                    metadataApiVersion,
                    cancellationToken).ConfigureAwait(false);

                if (response.ErrorCode != ErrorCode.RebootstrapRequired)
                {
                    ResetFinalizedFeaturesForRebootstrap();
                    BeginMetadataRebootstrap();
                }

                UpdateVersionlessCapabilities(
                    connection,
                    metadataApiVersion,
                    replaceExisting: true);

                if (negotiatedCapabilities is not null)
                    ObserveClusterCapabilities(response.ClusterId, negotiatedCapabilities);
                else
                    ObserveClusterCapabilities(response.ClusterId, connection);

                _metadata.Update(response, mergeTopics: topics is not null);
                if (response.ErrorCode != ErrorCode.RebootstrapRequired)
                    UpdateMetadataClusterId(response.ClusterId);

                // Surface the condition where the new broker also wants a rebootstrap —
                // we stop here to prevent an infinite loop, but log it for operators.
                if (response.ErrorCode == ErrorCode.RebootstrapRequired)
                {
                    LogRebootstrapChainSuppressed();
                }

                foreach (var broker in response.Brokers)
                {
                    RegisterBroker(broker.NodeId, broker.Host, broker.Port);
                }

                LogRebootstrapSuccessful(response.Brokers.Count, host, port);

                NotifyBrokerCountDiscovered(response.Brokers.Count);

                // Success - reset the rebootstrap timer
                ResetAllBrokersUnavailableTimestamp();

                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (IsFatalMetadataError(ex))
            {
                throw; // No other broker will resolve this — surface the real cause.
            }
            catch (Exception ex)
            {
                LogRebootstrapEndpointFailed(ex, host, port);
                lastException = ex;
            }
        }

        LogRebootstrapFailed(lastException);
        return false;
    }

    private void RegisterBroker(int brokerId, string host, int port)
    {
        _connectionPool.RegisterBroker(brokerId, host, port);
        Volatile.Read(ref _additionalBrokerRegistrationTarget)?.RegisterBroker(brokerId, host, port);
    }

    /// <summary>
    /// Re-resolves DNS for the original bootstrap servers to discover new broker IPs.
    /// </summary>
    internal async ValueTask<List<(string Host, int Port)>> ResolveBootstrapEndpointsAsync(CancellationToken cancellationToken)
    {
        var seen = new HashSet<(string Host, int Port)>();
        var resolved = new List<(string Host, int Port)>();

        foreach (var (host, port) in _bootstrapEndpoints)
        {
            try
            {
                // Apply a per-host timeout to prevent DNS hangs from blocking rebootstrap
                using var dnsCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                dnsCts.CancelAfter(TimeSpan.FromSeconds(5));
                var endpoints = await _options.DnsResolver
                    .ResolveAsync(host, port, _options.ClientDnsLookup, dnsCts.Token)
                    .ConfigureAwait(false);
                if (_options.ClientDnsLookup == ClientDnsLookup.ResolveCanonicalBootstrapServersOnly)
                {
                    if (endpoints.Count > 0)
                    {
                        var canonicalEndpoint = (endpoints[0].TargetHost, port);
                        if (seen.Add(canonicalEndpoint))
                            resolved.Add(canonicalEndpoint);
                    }
                }
                else
                {
                    foreach (var endpoint in endpoints)
                    {
                        var resolvedEndpoint = (endpoint.Address.ToString(), port);
                        if (seen.Add(resolvedEndpoint))
                            resolved.Add(resolvedEndpoint);
                    }
                }

                // Also add the original hostname endpoint (it may resolve differently now)
                var hostnameEndpoint = (host, port);
                if (seen.Add(hostnameEndpoint))
                {
                    resolved.Add(hostnameEndpoint);
                }

                LogRebootstrapDnsResolved(host, port, endpoints.Count);
            }
            catch (Exception ex)
            {
                LogRebootstrapDnsResolutionFailed(ex, host, port);
                // Still add the original hostname as a fallback
                var fallbackEndpoint = (host, port);
                if (seen.Add(fallbackEndpoint))
                {
                    resolved.Add(fallbackEndpoint);
                }
            }
        }

        return resolved;
    }

    private void ResetAllBrokersUnavailableTimestamp()
    {
        Interlocked.Exchange(ref _allBrokersUnavailableSince, 0);
    }

    private async ValueTask<KafkaConnectionCapabilities> NegotiateApiVersionsAsync(
        IKafkaConnection connection,
        CancellationToken cancellationToken)
    {
        const short versionlessConnectionApiVersionsVersion = 3;

        // Production connections negotiate v4 with fallback during physical connection setup.
        // Injected connections have no capability snapshot or physical-connection retry path,
        // so preserve the broadly-compatible v3 probe used before v4 support was added.
        var request = new ApiVersionsRequest
        {
            ClientSoftwareName = "dekaf",
            ClientSoftwareVersion = typeof(MetadataManager).Assembly.GetName().Version?.ToString() ?? "0.0.0"
        };

        var response = await connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
            request,
            versionlessConnectionApiVersionsVersion,
            cancellationToken).ConfigureAwait(false);

        if (response.ErrorCode != ErrorCode.None)
        {
            throw new InvalidOperationException($"ApiVersions failed: {response.ErrorCode}");
        }

        // IMPORTANT: Build new values locally first, then update shared state atomically.
        // This prevents a race condition where GetNegotiatedApiVersion could read
        // partially-cleared state during concurrent refreshes.

        // Build local map of API versions first
        short newMetadataVersion = MetadataRequest.LowestSupportedVersion;
        var newApiVersions = new Dictionary<ApiKey, (short MinVersion, short MaxVersion)>();

        foreach (var apiKey in response.ApiKeys)
        {
            newApiVersions[apiKey.ApiKey] = (apiKey.MinVersion, apiKey.MaxVersion);

            if (apiKey.ApiKey == ApiKey.Metadata)
            {
                newMetadataVersion = Math.Min(apiKey.MaxVersion, MetadataRequest.HighestSupportedVersion);
                if (newMetadataVersion < MetadataRequest.LowestSupportedVersion)
                {
                    newMetadataVersion = MetadataRequest.LowestSupportedVersion;
                }
            }
        }

        // Now update shared state: first add new values, then clear cache, then set version
        // The order matters: GetNegotiatedApiVersion checks cache first, then _brokerApiVersions.
        // By updating _brokerApiVersions before clearing cache, we ensure any cache miss
        // will find valid data in _brokerApiVersions.
        foreach (var kvp in newApiVersions)
        {
            _brokerApiVersions[kvp.Key] = kvp.Value;
        }

        // Clear negotiated cache since broker versions may have changed
        _negotiatedVersionCache.Clear();

        // Set metadata version last (acts as a signal that negotiation is complete)
        _metadataApiVersion = newMetadataVersion;

        LogNegotiatedApiVersion(_metadataApiVersion);
        return KafkaConnectionCapabilities.Create(response);
    }

    private void UpdateVersionlessCapabilities(
        IKafkaConnection connection,
        short metadataApiVersion,
        bool replaceExisting = false)
    {
        if ((!replaceExisting && _metadataApiVersion >= 0)
            || connection is not IKafkaCapabilityProvider provider)
            return;

        var capabilities = provider.Capabilities;
        if (replaceExisting)
            _brokerApiVersions.Clear();

        for (var key = 0; key < capabilities.ApiRangeCount; key++)
        {
            var apiKey = (ApiKey)key;
            if (capabilities.TryGetApiRange(apiKey, out var minVersion, out var maxVersion))
                _brokerApiVersions[apiKey] = (minVersion, maxVersion);
        }

        _negotiatedVersionCache.Clear();
        _metadataApiVersion = metadataApiVersion;
    }

    internal IReadOnlyList<(int BrokerId, string Host, int Port)> GetEndpointsToTry()
    {
        // Thread-safe cache check - avoid rebuilding if brokers haven't changed
        lock (_endpointCacheLock)
        {
            // Get current brokers inside lock to prevent race with _metadata.Update()
            var currentBrokers = _metadata.GetBrokers();
            // Compute hash of broker data (NodeId, Host, Port) to detect any changes
            // This detects count changes, membership changes, AND host/port changes
            var hash = new HashCode();
            foreach (var broker in currentBrokers)
            {
                hash.Add(broker.NodeId);
                hash.Add(broker.Host);
                hash.Add(broker.Port);
            }
            var currentBrokerHash = hash.ToHashCode();

            // Cache is valid if broker hash hasn't changed
            if (_cachedEndpoints is not null && _cachedBrokerHash == currentBrokerHash)
            {
                // Return cached list directly - callers only iterate, no modification
                return _cachedEndpoints;
            }

            // Build new endpoint list (allocation only when metadata changes)
            var endpoints = new List<(int BrokerId, string Host, int Port)>(
                currentBrokers.Count + _bootstrapEndpoints.Count);

            // First try known brokers, tracking seen endpoints for O(1) dedup
            var seen = new HashSet<(string Host, int Port)>();

            foreach (var broker in currentBrokers)
            {
                var ep = (broker.Host, broker.Port);
                seen.Add(ep);
                endpoints.Add((broker.NodeId, broker.Host, broker.Port));
            }

            // Once KIP-1242 has learned a trusted cluster identity, anonymous bootstrap
            // endpoints cannot be a fallback for normal refreshes. Explicit rebootstrap
            // uses freshly resolved bootstrap endpoints through ExecuteRebootstrapAsync.
            var hasEnforcedClusterIdentity = _options.MetadataClusterCheckEnabled
                && _options.MetadataRecoveryStrategy == MetadataRecoveryStrategy.Rebootstrap
                && Volatile.Read(ref _trustedMetadataClusterId) is not null;
            if (!hasEnforcedClusterIdentity)
            {
                foreach (var endpoint in _bootstrapEndpoints)
                {
                    if (seen.Add(endpoint))
                        endpoints.Add((-1, endpoint.Host, endpoint.Port));
                }
            }

            // Update cache with new hash and endpoints
            _cachedBrokerHash = currentBrokerHash;
            _cachedEndpoints = endpoints;

            // Return cached list directly - callers only iterate, no modification
            return endpoints;
        }
    }

    private void StartBackgroundRefresh()
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        lock (_backgroundRefreshGate)
        {
            if (Volatile.Read(ref _disposed) != 0)
                return;

            if (_backgroundRefreshTask is { IsCompleted: false })
                return;

            _backgroundRefreshCts?.Dispose();
            _backgroundRefreshCts = new CancellationTokenSource();
            _backgroundRefreshTask = BackgroundRefreshLoopAsync(_backgroundRefreshCts.Token);
        }
    }

    private async Task BackgroundRefreshLoopAsync(CancellationToken cancellationToken)
    {
        var consecutiveFailures = 0;
        LogBackgroundRefreshStarted((int)_options.MetadataRefreshInterval.TotalMilliseconds);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.MetadataRefreshInterval, cancellationToken).ConfigureAwait(false);
                await RefreshMetadataAsync(cancellationToken).ConfigureAwait(false);
                consecutiveFailures = 0; // Reset on success
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (Volatile.Read(ref _disposed) != 0)
            {
                break;
            }
            catch (Exception ex) when (IsFatalMetadataError(ex))
            {
                // Credentials or authorization changed mid-session: retrying forever would mask the
                // failure. Stop the background loop and require a later InitializeAsync call to
                // re-establish refresh after the caller handles the fatal error.
                _initialized = false;
                LogBackgroundMetadataRefreshFatal(ex);
                break;
            }
            catch (Exception ex)
            {
                consecutiveFailures++;
                LogBackgroundMetadataRefreshFailed(ex, consecutiveFailures);

                // Background refresh has a deliberately slower policy than individual request
                // retries: preserve the existing 5-second step and 60-second outage cap.
                var backoffMs = (int)Math.Min(consecutiveFailures * 5_000L, 60_000L);
                try
                {
                    await Task.Delay(backoffMs, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _disposalCts.Cancel();
        _backgroundRefreshCts?.Cancel();

        await WaitForInitializationToDrainAsync().ConfigureAwait(false);
        // An InitializeAsync racing disposal can create the background CTS while we drain it.
        _backgroundRefreshCts?.Cancel();

        if (_backgroundRefreshTask is not null)
        {
            try
            {
                await _backgroundRefreshTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch
            {
                // Ignore errors during shutdown
            }
        }

        _backgroundRefreshCts?.Dispose();
        _disposalCts.Dispose();
        _initializeLock.Dispose();
        _refreshLock.Dispose();
    }

    private async ValueTask WaitForInitializationToDrainAsync()
    {
        try
        {
            await _initializeLock.WaitAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        SemaphoreHelper.ReleaseSafely(_initializeLock);
    }

    #region Logging

    private bool ShouldWarnAboutMetadataFailure()
        => _hasSuccessfulRefresh || Volatile.Read(ref _initializationAttempt) >= BootstrapWarningAttempt;

    private void LogMetadataInitializationFailed(Exception ex, int attempt, int backoffMs)
    {
        if (ShouldWarnAboutMetadataFailure())
            LogMetadataInitializationFailedWarning(ex, attempt, backoffMs);
        else
            LogMetadataInitializationFailedDebug(ex, attempt, backoffMs);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Metadata initialization attempt {Attempt} failed, retrying in {BackoffMs}ms")]
    private partial void LogMetadataInitializationFailedDebug(Exception ex, int attempt, int backoffMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Metadata initialization attempt {Attempt} failed, retrying in {BackoffMs}ms")]
    private partial void LogMetadataInitializationFailedWarning(Exception ex, int attempt, int backoffMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Metadata initialization failed after {AttemptCount} attempts")]
    private partial void LogMetadataInitializationAbandoned(Exception ex, int attemptCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Refreshed metadata: {BrokerCount} brokers, {TopicCount} topics")]
    private partial void LogMetadataRefreshed(int brokerCount, int topicCount);

    private void LogMetadataRefreshFailed(Exception ex, string host, int port)
    {
        if (ShouldWarnAboutMetadataFailure())
            LogMetadataRefreshFailedWarning(ex, host, port);
        else
            LogMetadataRefreshFailedDebug(ex, host, port);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to refresh metadata from {Host}:{Port}")]
    private partial void LogMetadataRefreshFailedDebug(Exception ex, string host, int port);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to refresh metadata from {Host}:{Port}")]
    private partial void LogMetadataRefreshFailedWarning(Exception ex, string host, int port);

    private void LogAllBrokersUnavailable(int triggerMs)
    {
        // This signal is emitted once per outage episode, so an initialization-attempt
        // threshold cannot escalate it later. A terminal initialization warning covers a
        // persistent bootstrap outage; after successful operation, a new outage is warning.
        if (_hasSuccessfulRefresh)
            LogAllBrokersUnavailableWarning(triggerMs);
        else
            LogAllBrokersUnavailableDebug(triggerMs);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "All known brokers are unavailable. Rebootstrap will trigger after {TriggerMs}ms")]
    private partial void LogAllBrokersUnavailableDebug(int triggerMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "All known brokers are unavailable. Rebootstrap will trigger after {TriggerMs}ms")]
    private partial void LogAllBrokersUnavailableWarning(int triggerMs);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Rebootstrap not yet triggered. Elapsed: {ElapsedMs}ms, Trigger: {TriggerMs}ms")]
    private partial void LogRebootstrapNotYetTriggered(long elapsedMs, int triggerMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Triggering rebootstrap: re-resolving bootstrap server DNS after {ElapsedMs}ms of broker unavailability")]
    private partial void LogRebootstrapTriggered(long elapsedMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Broker signaled REBOOTSTRAP_REQUIRED (KIP-1102): triggering immediate rebootstrap")]
    private partial void LogBrokerInitiatedRebootstrap();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rebootstrap: new broker also returned REBOOTSTRAP_REQUIRED — suppressing chained rebootstrap to prevent infinite loop")]
    private partial void LogRebootstrapChainSuppressed();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rebootstrap DNS resolution returned no endpoints")]
    private partial void LogRebootstrapDnsNoEndpoints();

    [LoggerMessage(Level = LogLevel.Information, Message = "Rebootstrap successful: discovered {BrokerCount} brokers via {Host}:{Port}")]
    private partial void LogRebootstrapSuccessful(int brokerCount, string host, int port);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rebootstrap: failed to connect to resolved endpoint {Host}:{Port}")]
    private partial void LogRebootstrapEndpointFailed(Exception ex, string host, int port);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rebootstrap failed: could not connect to any resolved endpoint")]
    private partial void LogRebootstrapFailed(Exception? ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Rebootstrap DNS resolution for {Host}:{Port} returned {Count} addresses")]
    private partial void LogRebootstrapDnsResolved(string host, int port, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rebootstrap DNS resolution failed for {Host}:{Port}")]
    private partial void LogRebootstrapDnsResolutionFailed(Exception ex, string host, int port);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Negotiated Metadata API version: {Version}")]
    private partial void LogNegotiatedApiVersion(short version);

    [LoggerMessage(Level = LogLevel.Error, Message = "Brokers returned conflicting finalized feature state for epoch {Epoch}")]
    private partial void LogConflictingFinalizedFeatures(long epoch);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Background metadata refresh failed (attempt {Attempt}), continuing with existing metadata")]
    private partial void LogBackgroundMetadataRefreshFailed(Exception exception, int attempt);

    [LoggerMessage(Level = LogLevel.Error, Message = "Background metadata refresh hit a fatal authentication/authorization error; stopping background refresh (foreground operations will surface it)")]
    private partial void LogBackgroundMetadataRefreshFatal(Exception exception);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Metadata cache hit for {Topic}-{Partition}")]
    private partial void LogMetadataCacheHit(string topic, int partition);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Metadata cache miss for {Topic}-{Partition}, triggering refresh")]
    private partial void LogMetadataCacheMiss(string topic, int partition);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Metadata refresh requested")]
    private partial void LogMetadataRefreshRequested();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Metadata refresh skipped — all requested topics already cached")]
    private partial void LogMetadataRefreshSkippedCacheHit();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Background metadata refresh loop started with interval {IntervalMs}ms")]
    private partial void LogBackgroundRefreshStarted(int intervalMs);

    #endregion
}

/// <summary>
/// Options for metadata management.
/// </summary>
public sealed class MetadataOptions
{
    /// <summary>
    /// Interval for background metadata refresh.
    /// Metadata is never considered stale - it refreshes periodically and swaps in silently.
    /// Default matches Confluent's metadata.max.age.ms (15 minutes).
    /// </summary>
    public TimeSpan MetadataRefreshInterval { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Whether to enable background metadata refresh.
    /// </summary>
    public bool EnableBackgroundRefresh { get; init; } = true;

    /// <summary>
    /// Whether to allow auto-creation of topics.
    /// </summary>
    public bool AllowAutoTopicCreation { get; init; } = true;

    /// <summary>
    /// Strategy for recovering cluster metadata when all known brokers become unavailable.
    /// Default is <see cref="MetadataRecoveryStrategy.Rebootstrap"/>.
    /// </summary>
    public MetadataRecoveryStrategy MetadataRecoveryStrategy { get; init; } = MetadataRecoveryStrategy.Rebootstrap;

    /// <summary>
    /// Whether new broker connections include the expected cluster and node identity (KIP-1242).
    /// Disabled when <see cref="MetadataRecoveryStrategy"/> is <see cref="MetadataRecoveryStrategy.None"/>.
    /// </summary>
    public bool MetadataClusterCheckEnabled { get; init; } = true;

    /// <summary>
    /// How long in milliseconds to wait before triggering a rebootstrap when all known brokers
    /// are unavailable. Only applies when <see cref="MetadataRecoveryStrategy"/> is
    /// <see cref="MetadataRecoveryStrategy.Rebootstrap"/>.
    /// Default is 300000 (5 minutes).
    /// </summary>
    public int MetadataRecoveryRebootstrapTriggerMs { get; init; } = 300000;

    /// <summary>
    /// Controls how bootstrap and broker hostnames are resolved before connecting.
    /// </summary>
    public ClientDnsLookup ClientDnsLookup { get; init; } = ClientDnsLookup.UseAllDnsIps;

    /// <summary>
    /// DNS resolver used when re-resolving bootstrap endpoints. Internal for deterministic tests.
    /// </summary>
    internal ClientDnsEndpointResolver DnsResolver { get; init; } = ClientDnsEndpointResolver.Default;

    /// <summary>
    /// Initial retry backoff in milliseconds for metadata initialization.
    /// Matches Java client's retry.backoff.ms. Default is 100ms.
    /// </summary>
    public int RetryBackoffMs { get; init; } = 100;

    /// <summary>
    /// Maximum retry backoff in milliseconds for metadata initialization.
    /// Matches Java client's retry.backoff.max.ms. Default is 1000ms.
    /// </summary>
    public int RetryBackoffMaxMs { get; init; } = 1000;

    /// <summary>
    /// Total time budget in milliseconds for the initial metadata fetch. Initialization
    /// keeps retrying retriable failures with exponential backoff until this deadline,
    /// then throws <see cref="Errors.KafkaTimeoutException"/>. Matches the Java client,
    /// where the first send blocks up to max.block.ms (60s) while metadata is fetched.
    /// Default is 60000 (60 seconds). The producer maps its MaxBlockMs setting here.
    /// </summary>
    public int InitTimeoutMs { get; init; } = 60000;

    /// <summary>
    /// Optional cap on the number of retries for the initial metadata fetch. By default
    /// initialization is bounded by <see cref="InitTimeoutMs"/> alone; set this to make
    /// it give up after a fixed number of attempts instead, rethrowing the last failure.
    /// </summary>
    public int MaxInitRetries { get; init; } = int.MaxValue;
}
