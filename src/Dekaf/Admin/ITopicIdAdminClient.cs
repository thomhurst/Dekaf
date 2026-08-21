namespace Dekaf.Admin;

/// <summary>
/// Optional admin-client capability for addressing topics by topic ID.
/// </summary>
/// <remarks>
/// Dekaf's built-in and in-memory admin clients implement this interface. Custom
/// <see cref="IAdminClient"/> implementations can implement it to support the
/// topic-ID overloads in <see cref="AdminClientTopicIdExtensions"/>.
/// </remarks>
public interface ITopicIdAdminClient
{
    /// <summary>
    /// Deletes topics by topic ID.
    /// </summary>
    /// <param name="topicIds">The topic IDs to delete.</param>
    /// <param name="options">Delete options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask DeleteTopicsAsync(
        IEnumerable<Guid> topicIds,
        DeleteTopicsOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Describes topics by topic ID.
    /// </summary>
    /// <param name="topicIds">The topic IDs to describe.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Descriptions keyed by topic ID.</returns>
    ValueTask<IReadOnlyDictionary<Guid, TopicDescription>> DescribeTopicsAsync(
        IEnumerable<Guid> topicIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Topic-ID operations for <see cref="IAdminClient"/>.
/// </summary>
public static class AdminClientTopicIdExtensions
{
    /// <summary>
    /// Deletes topics by topic ID.
    /// </summary>
    /// <param name="adminClient">The admin client.</param>
    /// <param name="topicIds">The topic IDs to delete.</param>
    /// <param name="options">Delete options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="NotSupportedException">
    /// The admin client does not implement <see cref="ITopicIdAdminClient"/>.
    /// </exception>
    public static ValueTask DeleteTopicsAsync(
        this IAdminClient adminClient,
        IEnumerable<Guid> topicIds,
        DeleteTopicsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adminClient);

        return adminClient is ITopicIdAdminClient topicIdAdminClient
            ? topicIdAdminClient.DeleteTopicsAsync(topicIds, options, cancellationToken)
            : throw CreateNotSupportedException(adminClient);
    }

    /// <summary>
    /// Describes topics by topic ID.
    /// </summary>
    /// <param name="adminClient">The admin client.</param>
    /// <param name="topicIds">The topic IDs to describe.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Descriptions keyed by topic ID.</returns>
    /// <exception cref="NotSupportedException">
    /// The admin client does not implement <see cref="ITopicIdAdminClient"/>.
    /// </exception>
    public static ValueTask<IReadOnlyDictionary<Guid, TopicDescription>> DescribeTopicsAsync(
        this IAdminClient adminClient,
        IEnumerable<Guid> topicIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adminClient);

        return adminClient is ITopicIdAdminClient topicIdAdminClient
            ? topicIdAdminClient.DescribeTopicsAsync(topicIds, cancellationToken)
            : throw CreateNotSupportedException(adminClient);
    }

    private static NotSupportedException CreateNotSupportedException(IAdminClient adminClient) => new(
        $"Admin client type '{adminClient.GetType().FullName}' does not support topic-ID operations.");
}
