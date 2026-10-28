namespace Dekaf.Networking;

/// <summary>
/// Process-local identity learned from metadata and included in new broker handshakes (KIP-1242).
/// </summary>
internal sealed class MetadataClusterIdentity
{
    private readonly object _gate = new();
    private string? _clusterId;
    private bool _enabled;
    private bool _rebootstrapRequested;
    private Dictionary<int, short>? _apiVersionsMaxVersions;
    private short _lastReportedApiVersionsMaxVersion = -1;

    public void Configure(bool enabled)
    {
        lock (_gate)
        {
            _enabled = enabled;
            if (enabled)
                return;

            _clusterId = null;
            _rebootstrapRequested = false;
            _apiVersionsMaxVersions = null;
            _lastReportedApiVersionsMaxVersion = -1;
        }
    }

    public void UpdateClusterId(string? clusterId)
    {
        lock (_gate)
        {
            if (!_enabled)
                return;

            _clusterId = clusterId;
        }
    }

    public ExpectedBrokerIdentity? GetExpectedBrokerIdentity(int brokerId)
    {
        lock (_gate)
        {
            if (brokerId < 0 || !_enabled || _clusterId is null)
                return null;

            return new ExpectedBrokerIdentity(_clusterId, brokerId);
        }
    }

    /// <summary>
    /// Records the highest ApiVersions version a broker reported, keyed by broker ID when the
    /// connection knows it. The latest report from any connection, bootstrap included, is also
    /// kept as a cluster-wide hint for brokers with no record of their own. A first connection
    /// then starts at a version the cluster supports, which matters for servers that drop the
    /// connection on an unknown ApiVersions version instead of answering UNSUPPORTED_VERSION
    /// (the Azure Event Hubs emulator does).
    /// </summary>
    public void RecordApiVersionsMaxVersion(int brokerId, short maxVersion)
    {
        lock (_gate)
        {
            if (!_enabled)
                return;

            _lastReportedApiVersionsMaxVersion = maxVersion;
            if (brokerId >= 0)
                (_apiVersionsMaxVersions ??= [])[brokerId] = maxVersion;
        }
    }

    public bool TryGetApiVersionsMaxVersion(int brokerId, out short maxVersion)
    {
        lock (_gate)
        {
            if (_apiVersionsMaxVersions is not null
                && _apiVersionsMaxVersions.TryGetValue(brokerId, out maxVersion))
            {
                return true;
            }

            maxVersion = _lastReportedApiVersionsMaxVersion;
            return maxVersion >= 0;
        }
    }

    public bool TryGetClusterApiVersionsMaxVersion(out short maxVersion)
    {
        lock (_gate)
        {
            maxVersion = _lastReportedApiVersionsMaxVersion;
            return maxVersion >= 0;
        }
    }

    /// <summary>
    /// Adopts another pool's cluster-wide hint when this pool has no report of its own. A shared
    /// client's consumer pool learns the cluster ID from the primary pool's metadata but opens
    /// no bootstrap connection, so without the hint its first broker connection would send v5.
    /// </summary>
    public void SeedClusterApiVersionsMaxVersion(short maxVersion)
    {
        lock (_gate)
        {
            if (!_enabled || _lastReportedApiVersionsMaxVersion >= 0)
                return;

            _lastReportedApiVersionsMaxVersion = maxVersion;
        }
    }

    public void RequestRebootstrap()
    {
        lock (_gate)
        {
            if (!_enabled)
                return;

            _rebootstrapRequested = true;
        }
    }

    public bool TryConsumeRebootstrapRequest()
    {
        lock (_gate)
        {
            var requested = _rebootstrapRequested;
            _rebootstrapRequested = false;
            return requested;
        }
    }
}

internal readonly record struct ExpectedBrokerIdentity(string ClusterId, int NodeId);
