using Dekaf.Admin;
using Dekaf.Protocol;

namespace Dekaf.Testing;

public sealed partial class InMemoryAdminClient
{
    public ValueTask<IReadOnlyDictionary<string, StreamsGroupOffsetsResult>> ListStreamsGroupOffsetsAsync(
        IReadOnlyDictionary<string, ListStreamsGroupOffsetsSpec> groupSpecs,
        ListStreamsGroupOffsetsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupSpecs);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative((options ?? new ListStreamsGroupOffsetsOptions()).TimeoutMs);

        var results = new Dictionary<string, StreamsGroupOffsetsResult>(groupSpecs.Count, StringComparer.Ordinal);
        foreach (var (groupId, spec) in groupSpecs)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
            ArgumentNullException.ThrowIfNull(spec);

            var storedOffsets = _cluster.GetGroupOffsetDetails(groupId);
            var partitions = spec.TopicPartitions?.ToArray() ?? storedOffsets.Keys.ToArray();
            var uniquePartitions = new HashSet<TopicPartition>();
            var offsets = new Dictionary<TopicPartition, StreamsGroupOffsetDescription>(partitions.Length);
            foreach (var partition in partitions)
            {
                ValidateTopicPartition(partition);
                if (!uniquePartitions.Add(partition))
                    throw new ArgumentException($"Partition '{partition.Topic}-{partition.Partition}' is duplicated.", nameof(groupSpecs));

                var hasOffset = storedOffsets.TryGetValue(partition, out var storedOffset);
                offsets[partition] = new StreamsGroupOffsetDescription
                {
                    TopicPartition = partition,
                    Offset = hasOffset ? storedOffset.Offset : -1,
                    LeaderEpoch = hasOffset ? storedOffset.LeaderEpoch : -1,
                    Metadata = hasOffset ? storedOffset.Metadata : null,
                    ErrorCode = ErrorCode.None
                };
            }

            results.Add(groupId, new StreamsGroupOffsetsResult
            {
                GroupId = groupId,
                ErrorCode = ErrorCode.None,
                Offsets = offsets
            });
        }

        return new ValueTask<IReadOnlyDictionary<string, StreamsGroupOffsetsResult>>(results);
    }

    public ValueTask<IReadOnlyDictionary<TopicPartition, StreamsGroupOffsetOperationResult>> AlterStreamsGroupOffsetsAsync(
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
            return new ValueTask<IReadOnlyDictionary<TopicPartition, StreamsGroupOffsetOperationResult>>(results);

        _cluster.CommitOffsets(groupId, offsetList);
        return new ValueTask<IReadOnlyDictionary<TopicPartition, StreamsGroupOffsetOperationResult>>(results);
    }

    public ValueTask<IReadOnlyDictionary<TopicPartition, StreamsGroupOffsetOperationResult>> DeleteStreamsGroupOffsetsAsync(
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

        if (!_cluster.DeleteGroupOffsets(groupId, partitionList))
        {
            foreach (var partition in partitionList)
            {
                results[partition] = new StreamsGroupOffsetOperationResult
                {
                    TopicPartition = partition,
                    ErrorCode = ErrorCode.GroupIdNotFound
                };
            }
        }

        return new ValueTask<IReadOnlyDictionary<TopicPartition, StreamsGroupOffsetOperationResult>>(results);
    }

    public ValueTask<IReadOnlyDictionary<string, DeleteStreamsGroupResult>> DeleteStreamsGroupsAsync(
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

        foreach (var groupId in groupIdList)
        {
            var errorCode = _cluster.DeleteStreamsGroup(groupId);
            if (errorCode != ErrorCode.None)
            {
                results[groupId] = new DeleteStreamsGroupResult
                {
                    GroupId = groupId,
                    ErrorCode = errorCode
                };
            }
        }

        return new ValueTask<IReadOnlyDictionary<string, DeleteStreamsGroupResult>>(results);
    }

    private static StreamsGroupOffsetOperationResult Success(TopicPartition partition) => new()
    {
        TopicPartition = partition,
        ErrorCode = ErrorCode.None
    };
}
