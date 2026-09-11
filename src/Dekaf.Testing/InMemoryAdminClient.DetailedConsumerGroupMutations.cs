using Dekaf.Admin;

namespace Dekaf.Testing;

public sealed partial class InMemoryAdminClient : IDetailedConsumerGroupMutationAdminClient
{
    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> DeleteConsumerGroupsDetailedAsync(
        IEnumerable<string> groupIds, ConsumerGroupMutationOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var groups = AdminClient.SnapshotMutationKeys(groupIds, nameof(groupIds), static group => ArgumentException.ThrowIfNullOrWhiteSpace(group, nameof(groupIds)));
        return ExecuteInMemoryMutationAsync(groups, static group => group, static _ => (null, null),
            _cluster.DeleteGroup, options?.TimeoutMs ?? 30000, cancellationToken, getGroupId: static group => group);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<TopicPartition, AdminMutationResult>> AlterConsumerGroupOffsetsDetailedAsync(
        string groupId, IEnumerable<TopicPartitionOffset> offsets, ConsumerGroupMutationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        cancellationToken.ThrowIfCancellationRequested();
        var items = AdminClient.SnapshotDetailedConsumerOffsets(offsets);
        return ExecuteInMemoryMutationAsync(items, static item => new TopicPartition(item.Topic, item.Partition),
            static item => (item.Topic, (int?)item.Partition), item => _cluster.AlterConsumerGroupOffsetDetailed(groupId, item),
            options?.TimeoutMs ?? 30000, cancellationToken, getGroupId: _ => groupId);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<TopicPartition, AdminMutationResult>> DeleteConsumerGroupOffsetsDetailedAsync(
        string groupId, IEnumerable<TopicPartition> partitions, DeleteConsumerGroupOffsetsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        cancellationToken.ThrowIfCancellationRequested();
        var items = AdminClient.SnapshotMutationKeys(partitions, nameof(partitions), AdminClient.ValidateDetailedConsumerPartition);
        return ExecuteInMemoryMutationAsync(items, static item => item, static item => (item.Topic, (int?)item.Partition),
            item => _cluster.DeleteConsumerGroupOffsetDetailed(groupId, item),
            options?.TimeoutMs ?? 30000, cancellationToken, getGroupId: _ => groupId);
    }
}
