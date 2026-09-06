using Dekaf.Protocol.Messages;

namespace Dekaf.Admin;

/// <summary>
/// Optional admin-client capability for live cluster discovery, including fenced brokers.
/// </summary>
public interface IClusterDiscoveryAdminClient
{
    /// <summary>
    /// Queries the configured endpoint using DescribeCluster instead of cached metadata.
    /// Results are an administrative snapshot and are never added to client routing metadata.
    /// </summary>
    ValueTask<ClusterDescriptionSnapshot> DescribeClusterAsync(
        DescribeClusterOptions options,
        CancellationToken cancellationToken = default);
}

/// <summary>Live cluster discovery operations for <see cref="IAdminClient"/>.</summary>
public static class AdminClientClusterDiscoveryExtensions
{
    /// <summary>
    /// Queries live cluster information. The existing no-options overload retains its cached semantics.
    /// </summary>
    /// <exception cref="NotSupportedException">The client does not implement this optional capability.</exception>
    public static ValueTask<ClusterDescriptionSnapshot> DescribeClusterAsync(
        this IAdminClient adminClient,
        DescribeClusterOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adminClient);
        ArgumentNullException.ThrowIfNull(options);
        return adminClient is IClusterDiscoveryAdminClient discovery
            ? discovery.DescribeClusterAsync(options, cancellationToken)
            : throw new NotSupportedException(
                $"Admin client type '{adminClient.GetType().FullName}' does not support live cluster discovery.");
    }
}

/// <summary>Options for live DescribeCluster discovery.</summary>
public sealed class DescribeClusterOptions
{
    /// <summary>
    /// Includes fenced but registered brokers. Requires DescribeCluster v2 on the destination.
    /// Not supported with controller bootstrap endpoints, which describe controller nodes instead.
    /// </summary>
    public bool IncludeFencedBrokers { get; init; }
}

/// <summary>A live administrative cluster snapshot, independent of producer/consumer routing.</summary>
public sealed class ClusterDescriptionSnapshot
{
    public required string ClusterId { get; init; }
    public int ControllerId { get; init; }
    /// <summary>Identifies whether Nodes describes brokers or controllers.</summary>
    public DescribeClusterEndpointType EndpointType { get; init; }
    public required IReadOnlyList<ClusterNodeDescription> Nodes { get; init; }
}

/// <summary>A node returned by live cluster discovery.</summary>
public sealed class ClusterNodeDescription
{
    public int NodeId { get; init; }
    public required string Host { get; init; }
    public int Port { get; init; }
    public string? Rack { get; init; }
    /// <summary>
    /// Broker fencing status, or null for controller nodes and responses older than DescribeCluster v2.
    /// </summary>
    public bool? IsFenced { get; init; }
}
