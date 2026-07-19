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

    public void Configure(bool enabled)
    {
        lock (_gate)
        {
            _enabled = enabled;
            if (enabled)
                return;

            _clusterId = null;
            _rebootstrapRequested = false;
        }
    }

    public void UpdateClusterId(string? clusterId)
    {
        lock (_gate)
        {
            if (!_enabled)
                return;

            _clusterId = clusterId;
            _rebootstrapRequested = false;
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

    public void BeginRebootstrap()
    {
        lock (_gate)
        {
            _clusterId = null;
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
