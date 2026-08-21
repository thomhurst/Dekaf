namespace Dekaf.Admin;

/// <summary>
/// Optional admin-client capability for managing streams-group offsets and deletion.
/// </summary>
/// <remarks>
/// Dekaf's built-in and in-memory admin clients implement this interface. Custom
/// <see cref="IAdminClient"/> implementations can implement it without an
/// <see cref="IAdminClient"/> binary-compatibility break.
/// </remarks>
public interface IStreamsGroupManagementAdminClient
{
    ValueTask<IReadOnlyDictionary<string, StreamsGroupOffsetsResult>> ListStreamsGroupOffsetsAsync(
        IReadOnlyDictionary<string, ListStreamsGroupOffsetsSpec> groupSpecs,
        ListStreamsGroupOffsetsOptions? options = null,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyDictionary<TopicPartition, StreamsGroupOffsetOperationResult>> AlterStreamsGroupOffsetsAsync(
        string groupId,
        IEnumerable<TopicPartitionOffset> offsets,
        AlterStreamsGroupOffsetsOptions? options = null,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyDictionary<TopicPartition, StreamsGroupOffsetOperationResult>> DeleteStreamsGroupOffsetsAsync(
        string groupId,
        IEnumerable<TopicPartition> partitions,
        DeleteStreamsGroupOffsetsOptions? options = null,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyDictionary<string, DeleteStreamsGroupResult>> DeleteStreamsGroupsAsync(
        IEnumerable<string> groupIds,
        DeleteStreamsGroupsOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Streams-group management operations for <see cref="IAdminClient"/>.
/// </summary>
public static class AdminClientStreamsGroupManagementExtensions
{
    public static ValueTask<IReadOnlyDictionary<string, StreamsGroupOffsetsResult>> ListStreamsGroupOffsetsAsync(
        this IAdminClient adminClient,
        IReadOnlyDictionary<string, ListStreamsGroupOffsetsSpec> groupSpecs,
        ListStreamsGroupOffsetsOptions? options = null,
        CancellationToken cancellationToken = default) =>
        GetCapability(adminClient).ListStreamsGroupOffsetsAsync(groupSpecs, options, cancellationToken);

    public static ValueTask<IReadOnlyDictionary<TopicPartition, StreamsGroupOffsetOperationResult>> AlterStreamsGroupOffsetsAsync(
        this IAdminClient adminClient,
        string groupId,
        IEnumerable<TopicPartitionOffset> offsets,
        AlterStreamsGroupOffsetsOptions? options = null,
        CancellationToken cancellationToken = default) =>
        GetCapability(adminClient).AlterStreamsGroupOffsetsAsync(groupId, offsets, options, cancellationToken);

    public static ValueTask<IReadOnlyDictionary<TopicPartition, StreamsGroupOffsetOperationResult>> DeleteStreamsGroupOffsetsAsync(
        this IAdminClient adminClient,
        string groupId,
        IEnumerable<TopicPartition> partitions,
        DeleteStreamsGroupOffsetsOptions? options = null,
        CancellationToken cancellationToken = default) =>
        GetCapability(adminClient).DeleteStreamsGroupOffsetsAsync(groupId, partitions, options, cancellationToken);

    public static ValueTask<IReadOnlyDictionary<string, DeleteStreamsGroupResult>> DeleteStreamsGroupsAsync(
        this IAdminClient adminClient,
        IEnumerable<string> groupIds,
        DeleteStreamsGroupsOptions? options = null,
        CancellationToken cancellationToken = default) =>
        GetCapability(adminClient).DeleteStreamsGroupsAsync(groupIds, options, cancellationToken);

    private static IStreamsGroupManagementAdminClient GetCapability(IAdminClient adminClient)
    {
        ArgumentNullException.ThrowIfNull(adminClient);

        return adminClient as IStreamsGroupManagementAdminClient
            ?? throw new NotSupportedException(
                $"Admin client type '{adminClient.GetType().FullName}' does not support streams-group management.");
    }
}
