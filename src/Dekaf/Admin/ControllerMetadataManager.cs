using System.Runtime.ExceptionServices;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Admin;

/// <summary>
/// Discovers KRaft controller endpoints without registering them as brokers.
/// </summary>
internal sealed class ControllerMetadataManager : IDisposable
{
    private readonly IConnectionPool _connectionPool;
    private readonly MetadataManager _versionManager;
    private readonly IReadOnlyList<ControllerEndpoint> _bootstrapEndpoints;
    private readonly TimeSpan _refreshInterval;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private ControllerMetadataSnapshot _snapshot = ControllerMetadataSnapshot.Empty;
    private int _disposed;

    internal ControllerMetadataManager(
        IConnectionPool connectionPool,
        MetadataManager versionManager,
        IReadOnlyList<string> bootstrapControllers,
        TimeSpan refreshInterval)
    {
        if (refreshInterval < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(refreshInterval), "Controller metadata refresh interval must not be negative.");

        _connectionPool = connectionPool;
        _versionManager = versionManager;
        _bootstrapEndpoints = ParseEndpoints(bootstrapControllers);
        _refreshInterval = refreshInterval;
    }

    internal ControllerMetadataSnapshot Snapshot => Volatile.Read(ref _snapshot);

    internal ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        return IsRefreshDue(Snapshot)
            ? RefreshAsync(force: false, cancellationToken)
            : ValueTask.CompletedTask;
    }

    internal ValueTask RefreshAsync(CancellationToken cancellationToken) =>
        RefreshAsync(force: true, cancellationToken);

    private async ValueTask RefreshAsync(bool force, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = Snapshot;
            if (!force && !IsRefreshDue(snapshot))
                return;

            Exception? lastException = null;
            var endpoints = BuildRefreshEndpoints(snapshot, _bootstrapEndpoints);
            foreach (var endpoint in endpoints)
            {
                try
                {
                    using var lease = await _connectionPool.LeaseConnectionAsync(
                        endpoint.Host,
                        endpoint.Port,
                        cancellationToken).ConfigureAwait(false);
                    var connection = lease.Connection;
                    var version = _versionManager.GetNegotiatedApiVersion(
                        connection,
                        ApiKey.DescribeCluster,
                        1,
                        DescribeClusterRequest.HighestSupportedVersion);
                    var response = await connection.SendAsync<DescribeClusterRequest, DescribeClusterResponse>(
                        new DescribeClusterRequest { EndpointType = DescribeClusterEndpointType.Controller },
                        version,
                        cancellationToken).ConfigureAwait(false);

                    if (response.ErrorCode != ErrorCode.None)
                    {
                        throw KafkaException.FromErrorCode(
                            response.ErrorCode,
                            response.ErrorMessage ?? $"Controller discovery failed: {response.ErrorCode}");
                    }

                    if (response.EndpointType != DescribeClusterEndpointType.Controller)
                    {
                        throw new KafkaException(
                            ErrorCode.MismatchedEndpointType,
                            $"Controller bootstrap endpoint {endpoint.Host}:{endpoint.Port} returned endpoint type {response.EndpointType}.");
                    }

                    if (response.ControllerId < 0)
                    {
                        throw new KafkaException(
                            ErrorCode.UnknownControllerId,
                            "Controller discovery did not return an active controller ID.");
                    }

                    var controllers = new Dictionary<int, ControllerEndpoint>(response.Nodes.Count);
                    foreach (var node in response.Nodes)
                        controllers[node.NodeId] = new ControllerEndpoint(node.NodeId, node.Host, node.Port, node.Rack);

                    if (!controllers.ContainsKey(response.ControllerId))
                    {
                        throw new KafkaException(
                            ErrorCode.UnknownControllerId,
                            $"Controller discovery omitted active controller {response.ControllerId}.");
                    }

                    Volatile.Write(
                        ref _snapshot,
                        new ControllerMetadataSnapshot(
                            response.ClusterId,
                            response.ControllerId,
                            controllers,
                            DateTimeOffset.UtcNow));
                    return;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (KafkaException ex) when (!ex.IsRetriable)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                }
            }

            if (lastException is not null)
                ExceptionDispatchInfo.Capture(lastException).Throw();

            throw new InvalidOperationException("Unable to discover an active KRaft controller.");
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    internal async ValueTask<KafkaConnectionLease> LeaseActiveControllerAsync(
        ApiKey apiKey,
        CancellationToken cancellationToken)
    {
        EnsureSupported(apiKey);
        var snapshot = Snapshot;
        if (!snapshot.Controllers.TryGetValue(snapshot.ActiveControllerId, out var endpoint))
            throw new KafkaException(ErrorCode.UnknownControllerId, "No active controller endpoint is available.");

        return await _connectionPool.LeaseConnectionAsync(endpoint.Host, endpoint.Port, cancellationToken)
            .ConfigureAwait(false);
    }

    internal async ValueTask<KafkaConnectionLease> LeaseControllerAsync(
        int controllerId,
        ApiKey apiKey,
        CancellationToken cancellationToken)
    {
        EnsureSupported(apiKey);
        if (!Snapshot.Controllers.TryGetValue(controllerId, out var endpoint))
            throw new KafkaException(ErrorCode.UnknownControllerId, $"Unknown controller ID: {controllerId}.");

        return await _connectionPool.LeaseConnectionAsync(endpoint.Host, endpoint.Port, cancellationToken)
            .ConfigureAwait(false);
    }

    internal static void EnsureSupported(ApiKey apiKey)
    {
        if (apiKey is ApiKey.AlterConfigs
            or ApiKey.CreateAcls
            or ApiKey.DeleteAcls
            or ApiKey.DescribeCluster
            or ApiKey.DescribeAcls
            or ApiKey.DescribeConfigs
            or ApiKey.DescribeClientQuotas
            or ApiKey.AlterClientQuotas
            or ApiKey.IncrementalAlterConfigs
            or ApiKey.DescribeDelegationToken
            or ApiKey.ElectLeaders
            or ApiKey.AlterPartitionReassignments
            or ApiKey.ListPartitionReassignments
            or ApiKey.DescribeUserScramCredentials
            or ApiKey.ApiVersions
            or ApiKey.UpdateFeatures
            or ApiKey.DescribeQuorum
            or ApiKey.AddRaftVoter
            or ApiKey.RemoveRaftVoter
            or ApiKey.UnregisterBroker)
        {
            return;
        }

        throw new KafkaException(
            ErrorCode.UnsupportedEndpointType,
            $"API {apiKey} is not supported when using controller bootstrap endpoints.");
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            _refreshLock.Dispose();
    }

    private static IReadOnlyList<ControllerEndpoint> ParseEndpoints(IReadOnlyList<string> values)
    {
        var endpoints = new List<ControllerEndpoint>(values.Count);
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw InvalidEndpoint(value);

            var colon = value.LastIndexOf(':');
            if (colon <= 0 || colon == value.Length - 1)
                throw InvalidEndpoint(value);

#if NETSTANDARD2_0
            if (!int.TryParse(value.Substring(colon + 1), out var port))
#else
            if (!int.TryParse(value.AsSpan(colon + 1), out var port))
#endif
                throw InvalidEndpoint(value);
            if (port is < 1 or > ushort.MaxValue)
                throw InvalidEndpoint(value);

            var host = value[..colon];
            if (host.Length > 1 && host[0] == '[' && host[^1] == ']')
                host = host[1..^1];
            if (string.IsNullOrWhiteSpace(host))
                throw InvalidEndpoint(value);

            endpoints.Add(new ControllerEndpoint(-1, host, port, null));
        }

        return endpoints;
    }

    private bool IsRefreshDue(ControllerMetadataSnapshot snapshot) =>
        snapshot.LastRefreshed == default
        || DateTimeOffset.UtcNow - snapshot.LastRefreshed >= _refreshInterval;

    private static ArgumentException InvalidEndpoint(string? value) =>
        new($"Invalid controller bootstrap endpoint '{value}'. Expected host:port.");

    private static IReadOnlyList<ControllerEndpoint> BuildRefreshEndpoints(
        ControllerMetadataSnapshot snapshot,
        IReadOnlyList<ControllerEndpoint> bootstrapEndpoints)
    {
        var result = new List<ControllerEndpoint>(snapshot.Controllers.Count + bootstrapEndpoints.Count);
        var seen = new HashSet<(string Host, int Port)>();
        if (snapshot.Controllers.TryGetValue(snapshot.ActiveControllerId, out var active))
        {
            result.Add(active);
            seen.Add((active.Host, active.Port));
        }

        foreach (var endpoint in snapshot.Controllers.Values)
        {
            if (seen.Add((endpoint.Host, endpoint.Port)))
                result.Add(endpoint);
        }

        foreach (var endpoint in bootstrapEndpoints)
        {
            if (seen.Add((endpoint.Host, endpoint.Port)))
                result.Add(endpoint);
        }

        return result;
    }
}

internal sealed record ControllerMetadataSnapshot(
    string? ClusterId,
    int ActiveControllerId,
    IReadOnlyDictionary<int, ControllerEndpoint> Controllers,
    DateTimeOffset LastRefreshed)
{
    internal static ControllerMetadataSnapshot Empty { get; } = new(null, -1, new Dictionary<int, ControllerEndpoint>(), default);
}

internal sealed record ControllerEndpoint(int NodeId, string Host, int Port, string? Rack);
