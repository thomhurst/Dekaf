namespace Dekaf.Admin;

/// <summary>Optional capability for per-group and per-partition consumer-group mutation outcomes.</summary>
/// <remarks>
/// Every requested entity receives a result. Confirmed coordinator rejections may be retried;
/// successful and terminal results are retained. Ambiguous sends and omitted results are unknown
/// and are never automatically replayed. TimeoutMs bounds discovery, sends and retries across
/// the entire operation. Pre-cancellation throws; cancellation during execution returns partial
/// outcomes. Existing convenience methods retain their exception and retry behavior.
/// </remarks>
public interface IDetailedConsumerGroupMutationAdminClient
{
    ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> DeleteConsumerGroupsDetailedAsync(
        IEnumerable<string> groupIds, ConsumerGroupMutationOptions? options = null, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyDictionary<TopicPartition, AdminMutationResult>> AlterConsumerGroupOffsetsDetailedAsync(
        string groupId, IEnumerable<TopicPartitionOffset> offsets, ConsumerGroupMutationOptions? options = null,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyDictionary<TopicPartition, AdminMutationResult>> DeleteConsumerGroupOffsetsDetailedAsync(
        string groupId, IEnumerable<TopicPartition> partitions, DeleteConsumerGroupOffsetsOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Deadline for a detailed consumer-group mutation, including discovery and retries.</summary>
public sealed class ConsumerGroupMutationOptions
{
    public int TimeoutMs { get; init; } = 30000;
}

/// <summary>Detailed consumer-group mutations for compatible <see cref="IAdminClient"/> implementations.</summary>
public static class AdminClientDetailedConsumerGroupMutationExtensions
{
    public static ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> DeleteConsumerGroupsDetailedAsync(
        this IAdminClient adminClient, IEnumerable<string> groupIds, ConsumerGroupMutationOptions? options = null,
        CancellationToken cancellationToken = default) => GetCapability(adminClient).DeleteConsumerGroupsDetailedAsync(groupIds, options, cancellationToken);

    public static ValueTask<IReadOnlyDictionary<TopicPartition, AdminMutationResult>> AlterConsumerGroupOffsetsDetailedAsync(
        this IAdminClient adminClient, string groupId, IEnumerable<TopicPartitionOffset> offsets,
        ConsumerGroupMutationOptions? options = null, CancellationToken cancellationToken = default) =>
        GetCapability(adminClient).AlterConsumerGroupOffsetsDetailedAsync(groupId, offsets, options, cancellationToken);

    public static ValueTask<IReadOnlyDictionary<TopicPartition, AdminMutationResult>> DeleteConsumerGroupOffsetsDetailedAsync(
        this IAdminClient adminClient, string groupId, IEnumerable<TopicPartition> partitions,
        DeleteConsumerGroupOffsetsOptions? options = null, CancellationToken cancellationToken = default) =>
        GetCapability(adminClient).DeleteConsumerGroupOffsetsDetailedAsync(groupId, partitions, options, cancellationToken);

    private static IDetailedConsumerGroupMutationAdminClient GetCapability(IAdminClient adminClient)
    {
        ArgumentNullException.ThrowIfNull(adminClient);
        return adminClient as IDetailedConsumerGroupMutationAdminClient
            ?? throw new NotSupportedException($"Admin client type '{adminClient.GetType().FullName}' does not support detailed consumer-group mutations.");
    }
}
