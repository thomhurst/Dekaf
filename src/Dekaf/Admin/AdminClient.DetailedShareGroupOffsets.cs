using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Admin;

public sealed partial class AdminClient : IDetailedShareGroupOffsetAdminClient
{
    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<TopicPartition, AdminMutationResult>> AlterShareGroupOffsetsDetailedAsync(
        string groupId, IEnumerable<ShareGroupOffsetAlteration> offsets,
        ShareGroupOffsetMutationOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        cancellationToken.ThrowIfCancellationRequested();
        var timeout = options?.TimeoutMs ?? 30000;
        ArgumentOutOfRangeException.ThrowIfNegative(timeout);
        var items = SnapshotShareGroupOffsetAlterations(offsets);
        return ExecuteDetailedMutationAsync<TopicPartition, ShareGroupOffsetAlteration, AlterShareGroupOffsetsRequest, AlterShareGroupOffsetsResponse>(
            items, static item => item.TopicPartition,
            new(ApiKey.AlterShareGroupOffsets, AlterShareGroupOffsetsRequest.LowestSupportedVersion,
                AlterShareGroupOffsetsRequest.HighestSupportedVersion, nameof(AlterShareGroupOffsetsAsync), groupId), timeout,
            (pending, _) => BuildDetailedShareOffsetAlteration(groupId, pending),
            static (pending, response) =>
            {
                if (response.ErrorCode != ErrorCode.None)
                    return MapGroupMutationError(pending, static item => item.TopicPartition, response.ErrorCode, response.ErrorMessage);
                var results = new Dictionary<TopicPartition, AdminMutationResult>(pending.Count);
                foreach (var topic in response.Responses)
                    foreach (var partition in topic.Partitions)
                        AddMutationResult(results, new(topic.TopicName, partition.PartitionIndex),
                            AdminMutationResult.FromResponse(partition.ErrorCode, partition.ErrorMessage));
                return results;
            }, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> DeleteShareGroupOffsetsDetailedAsync(
        string groupId, IEnumerable<string> topics,
        ShareGroupOffsetMutationOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        cancellationToken.ThrowIfCancellationRequested();
        var timeout = options?.TimeoutMs ?? 30000;
        ArgumentOutOfRangeException.ThrowIfNegative(timeout);
        var items = SnapshotMutationKeys(topics, nameof(topics), static topic => ArgumentException.ThrowIfNullOrWhiteSpace(topic, nameof(topics)));
        return ExecuteDetailedMutationAsync<string, string, DeleteShareGroupOffsetsRequest, DeleteShareGroupOffsetsResponse>(
            items, static item => item,
            new(ApiKey.DeleteShareGroupOffsets, DeleteShareGroupOffsetsRequest.LowestSupportedVersion,
                DeleteShareGroupOffsetsRequest.HighestSupportedVersion, nameof(DeleteShareGroupOffsetsAsync), groupId), timeout,
            (pending, _) => new() { GroupId = groupId, Topics = pending.Select(static topic => new DeleteShareGroupOffsetsRequestTopic { TopicName = topic }).ToArray() },
            static (pending, response) => response.ErrorCode != ErrorCode.None
                ? MapGroupMutationError(pending, static topic => topic, response.ErrorCode, response.ErrorMessage)
                : MapMutationResults(response.Responses, static topic => topic.TopicName,
                    static topic => topic.ErrorCode, static topic => topic.ErrorMessage), cancellationToken);
    }

    internal static List<ShareGroupOffsetAlteration> SnapshotShareGroupOffsetAlterations(IEnumerable<ShareGroupOffsetAlteration> offsets)
    {
        ArgumentNullException.ThrowIfNull(offsets);
        var items = new List<ShareGroupOffsetAlteration>();
        var keys = new HashSet<TopicPartition>();
        foreach (var item in offsets)
        {
            ArgumentNullException.ThrowIfNull(item);
            ValidateTopicPartition(item.TopicPartition);
            if (!keys.Add(item.TopicPartition)) throw new ArgumentException("Topic partitions must be distinct.", nameof(offsets));
            items.Add(item);
        }
        return items;
    }

    private static AlterShareGroupOffsetsRequest BuildDetailedShareOffsetAlteration(string groupId, List<ShareGroupOffsetAlteration> items)
    {
        var topics = new Dictionary<string, List<AlterShareGroupOffsetsRequestPartition>>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (!topics.TryGetValue(item.TopicPartition.Topic, out var partitions))
                topics.Add(item.TopicPartition.Topic, partitions = new());
            partitions.Add(new() { PartitionIndex = item.TopicPartition.Partition, StartOffset = item.StartOffset });
        }
        var requests = new List<AlterShareGroupOffsetsRequestTopic>(topics.Count);
        foreach (var topic in topics) requests.Add(new() { TopicName = topic.Key, Partitions = topic.Value });
        return new() { GroupId = groupId, Topics = requests };
    }

    private static Dictionary<TKey, AdminMutationResult> MapGroupMutationError<TItem, TKey>(List<TItem> items,
        Func<TItem, TKey> key, ErrorCode error, string? message) where TKey : notnull
    {
        var results = new Dictionary<TKey, AdminMutationResult>(items.Count);
        foreach (var item in items) results.Add(key(item), AdminMutationResult.FromResponse(error, message));
        return results;
    }
}
