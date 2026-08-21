namespace Dekaf.Admin;

/// <summary>
/// Optional admin-client capability for deleting share groups.
/// </summary>
/// <remarks>
/// Dekaf's built-in and in-memory admin clients implement this interface. Custom
/// <see cref="IAdminClient"/> implementations can implement it to support
/// <see cref="AdminClientShareGroupDeletionExtensions.DeleteShareGroupsAsync"/>.
/// </remarks>
public interface IShareGroupDeletionAdminClient
{
    /// <summary>
    /// Deletes share groups and returns the result for every requested group.
    /// </summary>
    /// <param name="groupIds">The share group IDs to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary containing one result per requested group ID.</returns>
    ValueTask<IReadOnlyDictionary<string, DeleteShareGroupResult>> DeleteShareGroupsAsync(
        IEnumerable<string> groupIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Share-group deletion operations for <see cref="IAdminClient"/>.
/// </summary>
public static class AdminClientShareGroupDeletionExtensions
{
    /// <summary>
    /// Deletes share groups and returns the result for every requested group.
    /// </summary>
    /// <param name="adminClient">The admin client.</param>
    /// <param name="groupIds">The share group IDs to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary containing one result per requested group ID.</returns>
    /// <exception cref="NotSupportedException">
    /// The admin client does not implement <see cref="IShareGroupDeletionAdminClient"/>.
    /// </exception>
    public static ValueTask<IReadOnlyDictionary<string, DeleteShareGroupResult>> DeleteShareGroupsAsync(
        this IAdminClient adminClient,
        IEnumerable<string> groupIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adminClient);

        return adminClient is IShareGroupDeletionAdminClient shareGroupDeletionAdminClient
            ? shareGroupDeletionAdminClient.DeleteShareGroupsAsync(groupIds, cancellationToken)
            : throw new NotSupportedException(
                $"Admin client type '{adminClient.GetType().FullName}' does not support share-group deletion.");
    }
}
