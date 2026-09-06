namespace Dekaf.Admin;

/// <summary>Optional capability for querying the features supported by a selected node.</summary>
public interface INodeFeatureAdminClient
{
    /// <summary>Queries node-supported ranges and cluster-finalized feature metadata.</summary>
    ValueTask<FeatureMetadata> DescribeFeaturesAsync(
        DescribeFeaturesOptions options,
        CancellationToken cancellationToken = default);
}

/// <summary>Controls feature discovery within the configured broker or controller endpoint mode.</summary>
public sealed class DescribeFeaturesOptions
{
    /// <summary>The broker or controller ID to query. Null selects an arbitrary broker or the active controller.</summary>
    public int? NodeId { get; init; }

    /// <summary>Total operation timeout in milliseconds, including discovery and retries. Null uses the client's request timeout.</summary>
    public int? TimeoutMs { get; init; }
}

/// <summary>Node-specific feature discovery for admin clients.</summary>
public static class AdminClientNodeFeatureExtensions
{
    /// <summary>
    /// Queries supported features on the selected node. Finalized levels and epoch describe the cluster.
    /// Explicit node IDs never fall back to another node. IDs must belong to the configured endpoint mode.
    /// </summary>
    /// <exception cref="NotSupportedException">The client does not implement <see cref="INodeFeatureAdminClient"/>.</exception>
    public static ValueTask<FeatureMetadata> DescribeFeaturesAsync(
        this IAdminClient adminClient,
        DescribeFeaturesOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adminClient);
        ArgumentNullException.ThrowIfNull(options);
        return adminClient is INodeFeatureAdminClient nodeFeatureAdminClient
            ? nodeFeatureAdminClient.DescribeFeaturesAsync(options, cancellationToken)
            : throw new NotSupportedException(
                $"Admin client type '{adminClient.GetType().FullName}' does not support node-specific feature queries.");
    }
}
