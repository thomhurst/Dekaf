using Dekaf.Admin;
using Dekaf.Protocol.Messages;

namespace Dekaf.Testing;

public sealed partial class InMemoryAdminClient : IClusterDiscoveryAdminClient
{
    /// <inheritdoc />
    async ValueTask<ClusterDescriptionSnapshot> IClusterDiscoveryAdminClient.DescribeClusterAsync(
        DescribeClusterOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);

        // The in-memory cluster has one broker and does not simulate broker fencing.
        return new ClusterDescriptionSnapshot
        {
            ClusterId = _cluster.Options.ClusterId,
            ControllerId = 0,
            EndpointType = DescribeClusterEndpointType.Broker,
            Nodes =
            [
                new ClusterNodeDescription
                {
                    NodeId = 0,
                    Host = "in-memory",
                    Port = 0,
                    IsFenced = false
                }
            ]
        };
    }
}
