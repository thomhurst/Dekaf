namespace Dekaf.Admin;

/// <summary>Optional capability for detailed share-group offset mutations.</summary>
/// <remarks>
/// Every requested partition or topic receives an outcome, including top-level group errors.
/// Missing responses and ambiguous sends are unknown and are never automatically replayed.
/// Confirmed coordinator rejections may be retried without replaying successful entities.
/// Cancellation before invocation throws; cancellation during execution preserves confirmed
/// outcomes and reports unknown sent requests or not-attempted requests. The total timeout
/// includes discovery and retries. Existing convenience methods retain their exception behavior.
/// </remarks>
public interface IDetailedShareGroupOffsetAdminClient
{
    /// <summary>Alters offsets for an empty share group, returning a result for each requested partition.</summary>
    ValueTask<IReadOnlyDictionary<TopicPartition, AdminMutationResult>> AlterShareGroupOffsetsDetailedAsync(
        string groupId, IEnumerable<ShareGroupOffsetAlteration> offsets,
        ShareGroupOffsetMutationOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes all offsets for each requested topic, returning topic-level broker outcomes.</summary>
    ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> DeleteShareGroupOffsetsDetailedAsync(
        string groupId, IEnumerable<string> topics,
        ShareGroupOffsetMutationOptions? options = null, CancellationToken cancellationToken = default);
}

/// <summary>Options shared by detailed share-group offset mutations.</summary>
public sealed class ShareGroupOffsetMutationOptions
{
    /// <summary>Total operation deadline in milliseconds. Defaults to 30 seconds; zero expires immediately.</summary>
    public int TimeoutMs { get; init; } = 30000;
}

/// <summary>Detailed share-group offset mutations for compatible admin implementations.</summary>
public static class AdminClientDetailedShareGroupOffsetExtensions
{
    public static ValueTask<IReadOnlyDictionary<TopicPartition, AdminMutationResult>> AlterShareGroupOffsetsDetailedAsync(
        this IAdminClient adminClient, string groupId, IEnumerable<ShareGroupOffsetAlteration> offsets,
        ShareGroupOffsetMutationOptions? options = null, CancellationToken cancellationToken = default) =>
        GetCapability(adminClient).AlterShareGroupOffsetsDetailedAsync(groupId, offsets, options, cancellationToken);

    public static ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> DeleteShareGroupOffsetsDetailedAsync(
        this IAdminClient adminClient, string groupId, IEnumerable<string> topics,
        ShareGroupOffsetMutationOptions? options = null, CancellationToken cancellationToken = default) =>
        GetCapability(adminClient).DeleteShareGroupOffsetsDetailedAsync(groupId, topics, options, cancellationToken);

    private static IDetailedShareGroupOffsetAdminClient GetCapability(IAdminClient adminClient)
    {
        ArgumentNullException.ThrowIfNull(adminClient);
        return adminClient as IDetailedShareGroupOffsetAdminClient ?? throw new NotSupportedException(
            $"Admin client type '{adminClient.GetType().FullName}' does not support detailed share-group offset mutations.");
    }
}
