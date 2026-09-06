using Dekaf.Admin;
using Dekaf.Errors;
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
        var opts = options ?? new ListStreamsGroupOffsetsOptions();
        ArgumentOutOfRangeException.ThrowIfNegative(opts.TimeoutMs);
        var requests = ValidateGroupOffsetQueries(groupSpecs, static spec => spec.TopicPartitions);
        return await ExecuteWithTimeoutAsync(
            token => ReadGroupOffsetSnapshotsAsync(requests, opts.RequireStable, token),
            opts.TimeoutMs, nameof(ListStreamsGroupOffsetsAsync), cancellationToken).ConfigureAwait(false);
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
        var opts = options ?? new AlterStreamsGroupOffsetsOptions();
        ArgumentOutOfRangeException.ThrowIfNegative(opts.TimeoutMs);

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

        return await ExecuteWithTimeoutAsync(async operationToken =>
        {
            if (offsetList.Length == 0)
            {
                await ApplyAdminFaultAsync(operationToken, groupId: groupId).ConfigureAwait(false);
                return results;
            }

            foreach (var offset in offsetList)
            {
                await ApplyAdminFaultAsync(
                    operationToken,
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
        }, opts.TimeoutMs, nameof(AlterStreamsGroupOffsetsAsync), cancellationToken).ConfigureAwait(false);
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
        var opts = options ?? new DeleteStreamsGroupOffsetsOptions();
        ArgumentOutOfRangeException.ThrowIfNegative(opts.TimeoutMs);

        var partitionList = partitions.ToArray();
        var results = new Dictionary<TopicPartition, StreamsGroupOffsetOperationResult>(partitionList.Length);
        foreach (var partition in partitionList)
        {
            ValidateTopicPartition(partition);
            if (!results.TryAdd(partition, Success(partition)))
                throw new ArgumentException($"Partition '{partition.Topic}-{partition.Partition}' is duplicated.", nameof(partitions));
        }

        return await ExecuteWithTimeoutAsync(async operationToken =>
        {
            if (partitionList.Length == 0)
                await ApplyAdminFaultAsync(operationToken, groupId: groupId).ConfigureAwait(false);
            foreach (var partition in partitionList)
            {
                await ApplyAdminFaultAsync(
                    operationToken,
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
        }, opts.TimeoutMs, nameof(DeleteStreamsGroupOffsetsAsync), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyDictionary<string, DeleteStreamsGroupResult>> DeleteStreamsGroupsAsync(
        IEnumerable<string> groupIds,
        DeleteStreamsGroupsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupIds);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        var opts = options ?? new DeleteStreamsGroupsOptions();
        ArgumentOutOfRangeException.ThrowIfNegative(opts.TimeoutMs);

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

        return await ExecuteWithTimeoutAsync(async operationToken =>
        {
            if (groupIdList.Length == 0)
                await ApplyAdminFaultAsync(operationToken).ConfigureAwait(false);
            foreach (var groupId in groupIdList)
            {
                await ApplyAdminFaultAsync(operationToken, groupId: groupId).ConfigureAwait(false);
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
        }, opts.TimeoutMs, nameof(DeleteStreamsGroupsAsync), cancellationToken).ConfigureAwait(false);
    }

    private static StreamsGroupOffsetOperationResult Success(TopicPartition partition) => new()
    {
        TopicPartition = partition,
        ErrorCode = ErrorCode.None
    };

    private async ValueTask<T> ExecuteWithTimeoutAsync<T>(
        Func<CancellationToken, ValueTask<T>> operation,
        int timeoutMs,
        string operationName,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (ConfigureTimeoutSourceTestHook is { } configureTimeoutSource)
        {
            configureTimeoutSource(timeoutSource);
        }
        else
        {
            timeoutSource.CancelAfter(timeoutMs);
        }

        try
        {
            return await operation(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (
            !cancellationToken.IsCancellationRequested &&
            timeoutSource.IsCancellationRequested)
        {
            var configuredTimeout = TimeSpan.FromMilliseconds(timeoutMs);
            throw new KafkaTimeoutException(
                TimeoutKind.Api,
                configuredTimeout,
                configuredTimeout,
                $"{operationName} timed out after {timeoutMs} ms.",
                exception);
        }
    }
}
