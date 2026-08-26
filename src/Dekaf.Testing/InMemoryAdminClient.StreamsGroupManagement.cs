using Dekaf.Admin;
using Dekaf.Protocol;

namespace Dekaf.Testing;

public sealed partial class InMemoryAdminClient
{
    public async ValueTask<IReadOnlyDictionary<string, StreamsGroupOffsetsResult>> ListStreamsGroupOffsetsAsync(
        IReadOnlyDictionary<string, ListStreamsGroupOffsetsSpec> groupSpecs,
        ListStreamsGroupOffsetsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupSpecs);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative((options ?? new ListStreamsGroupOffsetsOptions()).TimeoutMs);

        var validatedSpecs = new (string GroupId, TopicPartition[]? Partitions)[groupSpecs.Count];
        var specIndex = 0;
        foreach (var (groupId, spec) in groupSpecs)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
            ArgumentNullException.ThrowIfNull(spec);

            var partitions = spec.TopicPartitions?.ToArray();
            if (partitions is not null)
            {
                var uniquePartitions = new HashSet<TopicPartition>();
                foreach (var partition in partitions)
                {
                    ValidateTopicPartition(partition);
                    if (!uniquePartitions.Add(partition))
                        throw new ArgumentException($"Partition '{partition.Topic}-{partition.Partition}' is duplicated.", nameof(groupSpecs));
                }
            }

            validatedSpecs[specIndex++] = (groupId, partitions);
        }

        var results = new Dictionary<string, StreamsGroupOffsetsResult>(groupSpecs.Count, StringComparer.Ordinal);
        if (validatedSpecs.Length == 0)
            await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);
        foreach (var (groupId, selectedPartitions) in validatedSpecs)
        {
            if (selectedPartitions is { Length: > 0 })
            {
                foreach (var partition in selectedPartitions)
                {
                    await ApplyAdminFaultAsync(
                        cancellationToken,
                        partition.Topic,
                        partition.Partition,
                        groupId).ConfigureAwait(false);
                }
            }
            else
            {
                await ApplyAdminFaultAsync(cancellationToken, groupId: groupId).ConfigureAwait(false);
            }

            var storedOffsets = _cluster.GetGroupOffsetDetails(groupId);
            var partitions = selectedPartitions ?? storedOffsets.Keys.ToArray();
            var offsets = new Dictionary<TopicPartition, StreamsGroupOffsetDescription>(partitions.Length);
            foreach (var partition in partitions)
            {
                var hasOffset = storedOffsets.TryGetValue(partition, out var storedOffset);
                var errorCode = _cluster.ContainsTopicPartition(partition)
                    ? ErrorCode.None
                    : ErrorCode.UnknownTopicOrPartition;
                offsets[partition] = new StreamsGroupOffsetDescription
                {
                    TopicPartition = partition,
                    Offset = hasOffset ? storedOffset.Offset : -1,
                    LeaderEpoch = hasOffset ? storedOffset.LeaderEpoch : -1,
                    Metadata = hasOffset ? storedOffset.Metadata : null,
                    ErrorCode = errorCode
                };
            }

            results.Add(groupId, new StreamsGroupOffsetsResult
            {
                GroupId = groupId,
                ErrorCode = ErrorCode.None,
                Offsets = offsets
            });
        }

        return results;
    }

    public async ValueTask<IReadOnlyDictionary<TopicPartition, StreamsGroupOffsetOperationResult>> AlterStreamsGroupOffsetsAsync(
        string groupId,
        IEnumerable<TopicPartitionOffset> offsets,
        AlterStreamsGroupOffsetsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentNullException.ThrowIfNull(offsets);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative((options ?? new AlterStreamsGroupOffsetsOptions()).TimeoutMs);

        var offsetList = offsets.ToArray();
        var results = new Dictionary<TopicPartition, StreamsGroupOffsetOperationResult>(offsetList.Length);
        foreach (var offset in offsetList)
        {
            var partition = new TopicPartition(offset.Topic, offset.Partition);
            ValidateTopicPartition(partition);
            ArgumentOutOfRangeException.ThrowIfNegative(offset.Offset, nameof(offsets));
            if (!results.TryAdd(partition, Success(partition)))
                throw new ArgumentException($"Partition '{partition.Topic}-{partition.Partition}' is duplicated.", nameof(offsets));
        }

        if (offsetList.Length == 0)
        {
            await ApplyAdminFaultAsync(cancellationToken, groupId: groupId).ConfigureAwait(false);
            return results;
        }

        foreach (var offset in offsetList)
        {
            await ApplyAdminFaultAsync(
                cancellationToken,
                offset.Topic,
                offset.Partition,
                groupId).ConfigureAwait(false);
        }

        var alterResults = _cluster.AlterStreamsGroupOffsets(groupId, offsetList);
        foreach (var offset in offsetList)
        {
            var partition = new TopicPartition(offset.Topic, offset.Partition);
            var errorCode = alterResults[partition];
            if (errorCode != ErrorCode.None)
            {
                results[partition] = new StreamsGroupOffsetOperationResult
                {
                    TopicPartition = partition,
                    ErrorCode = errorCode
                };
            }
        }

        return results;
    }

    public async ValueTask<IReadOnlyDictionary<TopicPartition, StreamsGroupOffsetOperationResult>> DeleteStreamsGroupOffsetsAsync(
        string groupId,
        IEnumerable<TopicPartition> partitions,
        DeleteStreamsGroupOffsetsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentNullException.ThrowIfNull(partitions);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative((options ?? new DeleteStreamsGroupOffsetsOptions()).TimeoutMs);

        var partitionList = partitions.ToArray();
        var results = new Dictionary<TopicPartition, StreamsGroupOffsetOperationResult>(partitionList.Length);
        foreach (var partition in partitionList)
        {
            ValidateTopicPartition(partition);
            if (!results.TryAdd(partition, Success(partition)))
                throw new ArgumentException($"Partition '{partition.Topic}-{partition.Partition}' is duplicated.", nameof(partitions));
        }

        if (partitionList.Length == 0)
            await ApplyAdminFaultAsync(cancellationToken, groupId: groupId).ConfigureAwait(false);
        foreach (var partition in partitionList)
        {
            await ApplyAdminFaultAsync(
                cancellationToken,
                partition.Topic,
                partition.Partition,
                groupId).ConfigureAwait(false);
        }

        var deleteResults = _cluster.DeleteStreamsGroupOffsets(groupId, partitionList);
        foreach (var partition in partitionList)
        {
            var errorCode = deleteResults[partition];
            if (errorCode != ErrorCode.None)
            {
                results[partition] = new StreamsGroupOffsetOperationResult
                {
                    TopicPartition = partition,
                    ErrorCode = errorCode
                };
            }
        }

        return results;
    }

    public async ValueTask<IReadOnlyDictionary<string, DeleteStreamsGroupResult>> DeleteStreamsGroupsAsync(
        IEnumerable<string> groupIds,
        DeleteStreamsGroupsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupIds);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative((options ?? new DeleteStreamsGroupsOptions()).TimeoutMs);

        var groupIdList = groupIds.ToArray();
        var results = new Dictionary<string, DeleteStreamsGroupResult>(groupIdList.Length, StringComparer.Ordinal);
        foreach (var groupId in groupIdList)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
            if (!results.TryAdd(groupId, new DeleteStreamsGroupResult
                {
                    GroupId = groupId,
                    ErrorCode = ErrorCode.None
                }))
            {
                throw new ArgumentException($"Streams group ID '{groupId}' is duplicated.", nameof(groupIds));
            }
        }

        if (groupIdList.Length == 0)
            await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);
        foreach (var groupId in groupIdList)
        {
            await ApplyAdminFaultAsync(cancellationToken, groupId: groupId).ConfigureAwait(false);
            var errorCode = _cluster.DeleteGroup(groupId);
            if (errorCode != ErrorCode.None)
            {
                results[groupId] = new DeleteStreamsGroupResult
                {
                    GroupId = groupId,
                    ErrorCode = errorCode
                };
            }
        }

        return results;
    }

    private static StreamsGroupOffsetOperationResult Success(TopicPartition partition) => new()
    {
        TopicPartition = partition,
        ErrorCode = ErrorCode.None
    };
}
