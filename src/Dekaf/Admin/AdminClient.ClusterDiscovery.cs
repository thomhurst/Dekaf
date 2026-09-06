using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Admin;

public sealed partial class AdminClient : IClusterDiscoveryAdminClient
{
    async ValueTask<ClusterDescriptionSnapshot> IClusterDiscoveryAdminClient.DescribeClusterAsync(
        DescribeClusterOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        var endpointType = _controllerMetadataManager is null
            ? DescribeClusterEndpointType.Broker
            : DescribeClusterEndpointType.Controller;
        if (options.IncludeFencedBrokers && endpointType == DescribeClusterEndpointType.Controller)
        {
            throw new NotSupportedException(
                "Fenced broker discovery requires broker bootstrap endpoints; controller endpoints describe controllers.");
        }

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return await WithRetryAsync(async () =>
        {
            using var lease = endpointType == DescribeClusterEndpointType.Broker
                ? await LeaseAnyBrokerConnectionAsync(cancellationToken).ConfigureAwait(false)
                : await LeaseControllerAsync(ApiKey.DescribeCluster, cancellationToken).ConfigureAwait(false);
            var connection = lease.Connection;
            var minimumVersion = endpointType == DescribeClusterEndpointType.Controller ? (short)1 : (short)0;
            if (options.IncludeFencedBrokers)
                minimumVersion = 2;
            var version = _metadataManager.GetNegotiatedApiVersion(
                connection, ApiKey.DescribeCluster, minimumVersion, DescribeClusterRequest.HighestSupportedVersion);
            var response = await connection.SendAsync<DescribeClusterRequest, DescribeClusterResponse>(
                new DescribeClusterRequest
                {
                    EndpointType = endpointType,
                    IncludeFencedBrokers = options.IncludeFencedBrokers
                }, version, cancellationToken).ConfigureAwait(false);
            if (response.ErrorCode != ErrorCode.None)
                throw KafkaException.FromErrorCode(response.ErrorCode, response.ErrorMessage ?? "DescribeCluster failed.");
            if (response.EndpointType != endpointType)
                throw KafkaException.FromErrorCode(ErrorCode.MismatchedEndpointType,
                    $"DescribeCluster returned {response.EndpointType} nodes for a {endpointType} endpoint.");

            var nodes = new ClusterNodeDescription[response.Nodes.Count];
            for (var i = 0; i < nodes.Length; i++)
            {
                var node = response.Nodes[i];
                nodes[i] = new ClusterNodeDescription
                {
                    NodeId = node.NodeId,
                    Host = node.Host,
                    Port = node.Port,
                    Rack = node.Rack,
                    IsFenced = endpointType == DescribeClusterEndpointType.Broker && version >= 2
                        ? node.IsFenced : null
                };
            }

            return new ClusterDescriptionSnapshot
            {
                ClusterId = response.ClusterId,
                ControllerId = response.ControllerId,
                EndpointType = endpointType,
                Nodes = nodes
            };
        }, cancellationToken).ConfigureAwait(false);
    }
}
