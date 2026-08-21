namespace Dekaf.Admin;

/// <summary>
/// Optional admin-client capability for querying the log directories of selected replicas.
/// </summary>
/// <remarks>
/// Dekaf's built-in and in-memory admin clients implement this interface. Custom
/// <see cref="IAdminClient"/> implementations can implement it to support
/// <see cref="AdminClientReplicaLogDirExtensions.DescribeReplicaLogDirsAsync"/>.
/// </remarks>
public interface IReplicaLogDirAdminClient
{
    /// <summary>
    /// Describes the current and future log directories for selected replicas.
    /// </summary>
    /// <param name="replicas">The replicas to query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Results for each distinct requested replica.</returns>
    ValueTask<IReadOnlyDictionary<TopicPartitionReplica, DescribeReplicaLogDirResultInfo>> DescribeReplicaLogDirsAsync(
        IEnumerable<TopicPartitionReplica> replicas,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Selected-replica log-directory operations for <see cref="IAdminClient"/>.
/// </summary>
public static class AdminClientReplicaLogDirExtensions
{
    /// <summary>
    /// Describes the current and future log directories for selected replicas.
    /// </summary>
    /// <param name="adminClient">The admin client.</param>
    /// <param name="replicas">The replicas to query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Results for each distinct requested replica.</returns>
    /// <exception cref="NotSupportedException">
    /// The admin client does not implement <see cref="IReplicaLogDirAdminClient"/>.
    /// </exception>
    public static ValueTask<IReadOnlyDictionary<TopicPartitionReplica, DescribeReplicaLogDirResultInfo>> DescribeReplicaLogDirsAsync(
        this IAdminClient adminClient,
        IEnumerable<TopicPartitionReplica> replicas,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adminClient);

        return adminClient is IReplicaLogDirAdminClient replicaLogDirAdminClient
            ? replicaLogDirAdminClient.DescribeReplicaLogDirsAsync(replicas, cancellationToken)
            : throw new NotSupportedException(
                $"Admin client type '{adminClient.GetType().FullName}' does not support selected-replica log-directory queries.");
    }
}
