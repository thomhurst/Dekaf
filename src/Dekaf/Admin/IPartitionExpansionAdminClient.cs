namespace Dekaf.Admin;

/// <summary>
/// Optional admin capability for validating partition expansion and assigning new replicas.
/// </summary>
public interface IPartitionExpansionAdminClient
{
    /// <summary>
    /// Increases topics to their final partition counts, or validates the requested expansion.
    /// </summary>
    ValueTask CreatePartitionsAsync(
        IReadOnlyDictionary<string, NewPartitions> newPartitions,
        CreatePartitionsOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Partition expansion operations for <see cref="IAdminClient"/>.
/// </summary>
public static class AdminClientPartitionExpansionExtensions
{
    /// <summary>
    /// Increases topics to their final partition counts. Replica assignments describe only
    /// additional partitions, in partition order; existing assignments remain unchanged.
    /// </summary>
    /// <exception cref="NotSupportedException">The client does not implement the optional capability.</exception>
    public static ValueTask CreatePartitionsAsync(
        this IAdminClient adminClient,
        IReadOnlyDictionary<string, NewPartitions> newPartitions,
        CreatePartitionsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adminClient);
        return adminClient is IPartitionExpansionAdminClient expansionClient
            ? expansionClient.CreatePartitionsAsync(newPartitions, options, cancellationToken)
            : throw new NotSupportedException($"Admin client type '{adminClient.GetType().FullName}' does not support typed partition expansion.");
    }
}

/// <summary>
/// Describes the final partition count and assignments of newly added partitions.
/// </summary>
public sealed class NewPartitions
{
    /// <summary>The total partition count after expansion, not the number to add.</summary>
    public required int TotalCount { get; init; }

    /// <summary>
    /// Replica broker IDs for each additional partition, or null for controller assignment.
    /// The outer count must equal TotalCount minus the current partition count; each inner
    /// list must match the topic replication factor. The first broker is the preferred replica.
    /// The controller validates these constraints against current cluster state.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<int>>? ReplicaAssignments { get; init; }
}

/// <summary>Options for partition expansion.</summary>
public sealed class CreatePartitionsOptions
{
    /// <summary>Broker operation timeout in milliseconds.</summary>
    public int TimeoutMs { get; init; } = 30000;

    /// <summary>Validate the expansion without changing partitions or replica assignments.</summary>
    public bool ValidateOnly { get; init; }
}
