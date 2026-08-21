using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Retry;

namespace Dekaf.Admin;

public sealed partial class AdminClient
{
    public ValueTask<IReadOnlyDictionary<string, StreamsGroupOffsetsResult>> ListStreamsGroupOffsetsAsync(
        IReadOnlyDictionary<string, ListStreamsGroupOffsetsSpec> groupSpecs,
        ListStreamsGroupOffsetsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupSpecs);
        cancellationToken.ThrowIfCancellationRequested();

        var requests = new Dictionary<string, IReadOnlyList<TopicPartition>?>(groupSpecs.Count, StringComparer.Ordinal);
        foreach (var (groupId, spec) in groupSpecs)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
            ArgumentNullException.ThrowIfNull(spec);
            requests.Add(groupId, ValidateDistinctPartitions(spec.TopicPartitions, nameof(groupSpecs)));
        }

        var opts = options ?? new ListStreamsGroupOffsetsOptions();
        ArgumentOutOfRangeException.ThrowIfNegative(opts.TimeoutMs);
        if (requests.Count == 0)
        {
            return new ValueTask<IReadOnlyDictionary<string, StreamsGroupOffsetsResult>>(
                new Dictionary<string, StreamsGroupOffsetsResult>(StringComparer.Ordinal));
        }

        return ExecuteWithTimeoutAsync(
            token => ListStreamsGroupOffsetsCoreAsync(requests, opts.RequireStable, token),
            opts.TimeoutMs,
            nameof(ListStreamsGroupOffsetsAsync),
            cancellationToken);
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

        var offsetList = offsets.ToArray();
        var seen = new HashSet<TopicPartition>();
        foreach (var offset in offsetList)
        {
            var partition = new TopicPartition(offset.Topic, offset.Partition);
            ValidateTopicPartition(partition, nameof(offsets));
            ArgumentOutOfRangeException.ThrowIfNegative(offset.Offset, nameof(offsets));
            if (!seen.Add(partition))
                throw new ArgumentException($"Partition '{partition.Topic}-{partition.Partition}' is duplicated.", nameof(offsets));
        }

        var opts = options ?? new AlterStreamsGroupOffsetsOptions();
        ArgumentOutOfRangeException.ThrowIfNegative(opts.TimeoutMs);
        if (offsetList.Length == 0)
            return EmptyPartitionOperationResults();

        return ExecuteWithTimeoutAsync(
            token => AlterStreamsGroupOffsetsCoreAsync(groupId, offsetList, token),
            opts.TimeoutMs,
            nameof(AlterStreamsGroupOffsetsAsync),
            cancellationToken);
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

        var partitionList = ValidateDistinctPartitions(partitions, nameof(partitions))!;
        var opts = options ?? new DeleteStreamsGroupOffsetsOptions();
        ArgumentOutOfRangeException.ThrowIfNegative(opts.TimeoutMs);
        if (partitionList.Count == 0)
            return EmptyPartitionOperationResults();

        return ExecuteWithTimeoutAsync(
            token => DeleteStreamsGroupOffsetsCoreAsync(groupId, partitionList, token),
            opts.TimeoutMs,
            nameof(DeleteStreamsGroupOffsetsAsync),
            cancellationToken);
    }

    public ValueTask<IReadOnlyDictionary<string, DeleteStreamsGroupResult>> DeleteStreamsGroupsAsync(
        IEnumerable<string> groupIds,
        DeleteStreamsGroupsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupIds);
        cancellationToken.ThrowIfCancellationRequested();

        var groupIdList = groupIds.ToArray();
        var uniqueGroupIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var groupId in groupIdList)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
            if (!uniqueGroupIds.Add(groupId))
                throw new ArgumentException($"Streams group ID '{groupId}' is duplicated.", nameof(groupIds));
        }

        var opts = options ?? new DeleteStreamsGroupsOptions();
        ArgumentOutOfRangeException.ThrowIfNegative(opts.TimeoutMs);
        if (groupIdList.Length == 0)
        {
            return new ValueTask<IReadOnlyDictionary<string, DeleteStreamsGroupResult>>(
                new Dictionary<string, DeleteStreamsGroupResult>(StringComparer.Ordinal));
        }

        return ExecuteWithTimeoutAsync(
            token => DeleteStreamsGroupsCoreAsync(groupIdList, token),
            opts.TimeoutMs,
            nameof(DeleteStreamsGroupsAsync),
            cancellationToken);
    }

    private async ValueTask<IReadOnlyDictionary<string, StreamsGroupOffsetsResult>> ListStreamsGroupOffsetsCoreAsync(
        IReadOnlyDictionary<string, IReadOnlyList<TopicPartition>?> requests,
        bool requireStable,
        CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var results = new Dictionary<string, StreamsGroupOffsetsResult>(requests.Count, StringComparer.Ordinal);
        var retryErrors = new Dictionary<string, Protocol.ErrorCode>(requests.Count, StringComparer.Ordinal);
        var retryResults = new Dictionary<string, StreamsGroupOffsetsResult>(requests.Count, StringComparer.Ordinal);

        try
        {
            return await WithRetryAsync<IReadOnlyDictionary<string, StreamsGroupOffsetsResult>>(async () =>
            {
                retryErrors.Clear();
                retryResults.Clear();
                Exception? retryFailure = null;
                var groupsByCoordinator = new Dictionary<int, List<string>>();
                foreach (var groupId in requests.Keys)
                {
                    if (results.ContainsKey(groupId))
                        continue;

                    int coordinatorId;
                    try
                    {
                        coordinatorId = await FindGroupCoordinatorAsync(groupId, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Errors.GroupException exception) when (
                        exception.ErrorCode is { } errorCode &&
                        !errorCode.IsRetriable() &&
                        !errorCode.RequiresMetadataRefresh())
                    {
                        results[groupId] = GroupOffsetsError(groupId, errorCode);
                        continue;
                    }
                    catch (Exception exception) when (
                        RetryHelper.IsRetriableRequestFailure(exception) &&
                        !cancellationToken.IsCancellationRequested)
                    {
                        retryErrors[groupId] = GetRetryErrorCode(exception);
                        retryFailure ??= exception;
                        continue;
                    }
                    if (!groupsByCoordinator.TryGetValue(coordinatorId, out var coordinatorGroups))
                    {
                        coordinatorGroups = [];
                        groupsByCoordinator[coordinatorId] = coordinatorGroups;
                    }
                    coordinatorGroups.Add(groupId);
                }

                foreach (var (coordinatorId, coordinatorGroups) in groupsByCoordinator)
                {
                    using var connectionLease = await _connectionPool.LeaseConnectionAsync(
                        coordinatorId,
                        cancellationToken).ConfigureAwait(false);
                    var connection = connectionLease.Connection;
                    var highestVersion = coordinatorGroups.Any(groupId => requests[groupId] is null)
                        ? (short)(OffsetFetchRequest.TopicIdVersion - 1)
                        : OffsetFetchRequest.HighestSupportedVersion;
                    var apiVersion = _metadataManager.GetNegotiatedApiVersion(
                        connection,
                        Protocol.ApiKey.OffsetFetch,
                        requireStable
                            ? OffsetFetchRequest.RequireStableVersion
                            : OffsetFetchRequest.LowestSupportedVersion,
                        highestVersion);

                    if (apiVersion < 8)
                    {
                        foreach (var groupId in coordinatorGroups)
                        {
                            var failure = await ListStreamsGroupOffsetsBatchAsync(
                                connection,
                                [groupId],
                                requests,
                                requireStable,
                                apiVersion,
                                results,
                                retryErrors,
                                retryResults,
                                cancellationToken).ConfigureAwait(false);
                            retryFailure ??= failure;
                        }
                    }
                    else
                    {
                        var failure = await ListStreamsGroupOffsetsBatchAsync(
                            connection,
                            coordinatorGroups,
                            requests,
                            requireStable,
                            apiVersion,
                            results,
                            retryErrors,
                            retryResults,
                            cancellationToken).ConfigureAwait(false);
                        retryFailure ??= failure;
                    }
                }

                if (retryFailure is not null)
                    throw retryFailure;

                return OrderGroupResults(requests.Keys, results);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            RetryHelper.IsRetriableRequestFailure(exception) &&
            !cancellationToken.IsCancellationRequested)
        {
            foreach (var (groupId, result) in retryResults)
                results.TryAdd(groupId, result);

            foreach (var (groupId, errorCode) in retryErrors)
                results.TryAdd(groupId, GroupOffsetsError(groupId, errorCode));

            var fallbackError = GetRetryErrorCode(exception);
            foreach (var groupId in requests.Keys)
                results.TryAdd(groupId, GroupOffsetsError(groupId, fallbackError));

            return OrderGroupResults(requests.Keys, results);
        }
    }

    private async ValueTask<Exception?> ListStreamsGroupOffsetsBatchAsync(
        IKafkaConnection connection,
        IReadOnlyList<string> groupIds,
        IReadOnlyDictionary<string, IReadOnlyList<TopicPartition>?> requests,
        bool requireStable,
        short apiVersion,
        Dictionary<string, StreamsGroupOffsetsResult> results,
        Dictionary<string, Protocol.ErrorCode> retryErrors,
        Dictionary<string, StreamsGroupOffsetsResult> retryResults,
        CancellationToken cancellationToken)
    {
        var topicMaps = new Dictionary<string, OffsetTopicIdRequestMap?>(groupIds.Count, StringComparer.Ordinal);
        var requestGroups = new List<OffsetFetchRequestGroup>(groupIds.Count);
        foreach (var groupId in groupIds)
        {
            var topics = BuildOffsetFetchTopics(requests[groupId], apiVersion, nameof(ListStreamsGroupOffsetsAsync), out var topicMap);
            topicMaps[groupId] = topicMap;
            requestGroups.Add(new OffsetFetchRequestGroup
            {
                GroupId = groupId,
                Topics = topics,
                MemberId = null,
                MemberEpoch = -1
            });
        }

        var firstGroup = requestGroups[0];
        var response = await connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
            new OffsetFetchRequest
            {
                GroupId = firstGroup.GroupId,
                Topics = firstGroup.Topics,
                Groups = apiVersion >= 8 ? requestGroups : null,
                RequireStable = requireStable
            },
            apiVersion,
            cancellationToken).ConfigureAwait(false);

        Exception? retryFailure = null;
        if (apiVersion < 8)
        {
            var groupId = groupIds[0];
            retryFailure = CaptureListStreamsGroupResult(
                groupId,
                response.ErrorCode,
                response.Topics ?? [],
                requests[groupId],
                topicMaps[groupId],
                results,
                retryResults);
            if (retryFailure is not null)
                retryErrors[groupId] = GetRetryErrorCode(retryFailure);
            return retryFailure;
        }

        var requestedGroupIds = new HashSet<string>(groupIds, StringComparer.Ordinal);
        var responseGroupIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in response.Groups ?? [])
        {
            if (!requestedGroupIds.Contains(group.GroupId))
                continue;

            responseGroupIds.Add(group.GroupId);
            var failure = CaptureListStreamsGroupResult(
                group.GroupId,
                group.ErrorCode,
                group.Topics,
                requests[group.GroupId],
                topicMaps[group.GroupId],
                results,
                retryResults);
            if (failure is not null)
                retryErrors[group.GroupId] = GetRetryErrorCode(failure);
            retryFailure ??= failure;
        }

        foreach (var groupId in groupIds)
        {
            if (!responseGroupIds.Contains(groupId))
            {
                if (retryFailure is null)
                {
                    results[groupId] = GroupOffsetsError(groupId, Protocol.ErrorCode.UnknownServerError);
                }
                else
                    retryErrors[groupId] = Protocol.ErrorCode.UnknownServerError;
            }
        }

        return retryFailure;
    }

    private Exception? CaptureListStreamsGroupResult(
        string groupId,
        Protocol.ErrorCode groupError,
        IReadOnlyList<OffsetFetchResponseTopic> responseTopics,
        IReadOnlyList<TopicPartition>? requestedPartitions,
        OffsetTopicIdRequestMap? topicMap,
        Dictionary<string, StreamsGroupOffsetsResult> results,
        Dictionary<string, StreamsGroupOffsetsResult> retryResults)
    {
        if (groupError.IsRetriable() || groupError.RequiresMetadataRefresh())
        {
            return new Errors.GroupException(
                groupError,
                $"ListStreamsGroupOffsets failed for group '{groupId}': {groupError}",
                isRetriable: true)
            {
                GroupId = groupId
            };
        }

        if (groupError != Protocol.ErrorCode.None)
        {
            results[groupId] = new StreamsGroupOffsetsResult
            {
                GroupId = groupId,
                ErrorCode = groupError,
                Offsets = new Dictionary<TopicPartition, StreamsGroupOffsetDescription>()
            };
            return null;
        }

        var requested = requestedPartitions?.ToHashSet();
        var offsets = new Dictionary<TopicPartition, StreamsGroupOffsetDescription>();
        var responseSnapshot = topicMap?.CaptureResponseSnapshot();
        Exception? retryFailure = null;
        foreach (var topic in responseTopics)
        {
            string topicName;
            try
            {
                topicName = topicMap is null
                    ? topic.Name
                    : topicMap.MatchResponseTopic(
                        topic.TopicId,
                        responseSnapshot!,
                        nameof(ListStreamsGroupOffsetsAsync),
                        responseMismatchIsRetriable: true);
            }
            catch (KafkaException exception)
            {
                return exception;
            }

            foreach (var partition in topic.Partitions)
            {
                var topicPartition = new TopicPartition(topicName, partition.PartitionIndex);
                if (requested is not null && !requested.Contains(topicPartition))
                    continue;

                offsets[topicPartition] = new StreamsGroupOffsetDescription
                {
                    TopicPartition = topicPartition,
                    Offset = partition.CommittedOffset,
                    LeaderEpoch = partition.CommittedLeaderEpoch,
                    Metadata = partition.Metadata,
                    ErrorCode = partition.ErrorCode
                };
                if (partition.ErrorCode.IsRetriable() || partition.ErrorCode.RequiresMetadataRefresh())
                {
                    retryFailure ??= new Errors.GroupException(
                        partition.ErrorCode,
                        $"ListStreamsGroupOffsets failed for {topicName}-{partition.PartitionIndex}: {partition.ErrorCode}",
                        isRetriable: true)
                    {
                        GroupId = groupId
                    };
                    continue;
                }
            }
        }

        if (requested is not null)
        {
            foreach (var topicPartition in requested)
            {
                if (!offsets.ContainsKey(topicPartition))
                {
                    offsets[topicPartition] = new StreamsGroupOffsetDescription
                    {
                        TopicPartition = topicPartition,
                        ErrorCode = Protocol.ErrorCode.UnknownServerError
                    };
                }
            }
        }

        var result = new StreamsGroupOffsetsResult
        {
            GroupId = groupId,
            ErrorCode = Protocol.ErrorCode.None,
            Offsets = offsets
        };

        if (retryFailure is not null)
        {
            retryResults[groupId] = result;
            return retryFailure;
        }

        results[groupId] = result;
        return null;
    }

    private async ValueTask<IReadOnlyDictionary<TopicPartition, StreamsGroupOffsetOperationResult>> AlterStreamsGroupOffsetsCoreAsync(
        string groupId,
        IReadOnlyList<TopicPartitionOffset> offsets,
        CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var results = new Dictionary<TopicPartition, StreamsGroupOffsetOperationResult>(offsets.Count);
        var requestedPartitions = offsets
            .Select(static offset => new TopicPartition(offset.Topic, offset.Partition))
            .ToArray();
        var retryErrors = new Dictionary<TopicPartition, Protocol.ErrorCode>(offsets.Count);

        try
        {
            return await WithRetryAsync<IReadOnlyDictionary<TopicPartition, StreamsGroupOffsetOperationResult>>(async () =>
            {
                retryErrors.Clear();
                int coordinatorId;
                try
                {
                    coordinatorId = await FindGroupCoordinatorAsync(groupId, cancellationToken).ConfigureAwait(false);
                }
                catch (Errors.GroupException exception) when (
                    exception.ErrorCode is { } errorCode &&
                    !errorCode.IsRetriable() &&
                    !errorCode.RequiresMetadataRefresh())
                {
                    return CompletePartitionResults(requestedPartitions, results, errorCode);
                }
                using var connectionLease = await _connectionPool.LeaseConnectionAsync(coordinatorId, cancellationToken).ConfigureAwait(false);
                var connection = connectionLease.Connection;
                var apiVersion = _metadataManager.GetNegotiatedApiVersion(
                    connection,
                    Protocol.ApiKey.OffsetCommit,
                    OffsetCommitRequest.LowestSupportedVersion,
                    OffsetCommitRequest.HighestSupportedVersion);

                var pendingOffsets = offsets
                    .Where(offset => !results.ContainsKey(new TopicPartition(offset.Topic, offset.Partition)))
                    .ToArray();
                var topics = BuildOffsetCommitTopics(
                    pendingOffsets,
                    apiVersion,
                    nameof(AlterStreamsGroupOffsetsAsync),
                    out var topicMap);
                var response = await connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                    new OffsetCommitRequest
                    {
                        GroupId = groupId,
                        GenerationIdOrMemberEpoch = -1,
                        MemberId = string.Empty,
                        Topics = topics
                    },
                    apiVersion,
                    cancellationToken).ConfigureAwait(false);

                var pending = pendingOffsets
                    .Select(static offset => new TopicPartition(offset.Topic, offset.Partition))
                    .ToHashSet();
                var responseSnapshot = topicMap?.CaptureResponseSnapshot();
                Exception? retryFailure = null;
                foreach (var topic in response.Topics)
                {
                    var topicName = topicMap is null
                        ? topic.Name
                        : topicMap.MatchResponseTopic(
                            topic.TopicId,
                            responseSnapshot!,
                            nameof(AlterStreamsGroupOffsetsAsync),
                            responseMismatchIsRetriable: false);
                    foreach (var partition in topic.Partitions)
                    {
                        var topicPartition = new TopicPartition(topicName, partition.PartitionIndex);
                        if (!pending.Remove(topicPartition))
                            continue;

                        if (partition.ErrorCode.IsRetriable() || partition.ErrorCode.RequiresMetadataRefresh())
                        {
                            retryErrors[topicPartition] = partition.ErrorCode;
                            retryFailure ??= new Errors.GroupException(
                                partition.ErrorCode,
                                $"AlterStreamsGroupOffsets failed for {topicName}-{partition.PartitionIndex}: {partition.ErrorCode}",
                                isRetriable: true)
                            {
                                GroupId = groupId
                            };
                            continue;
                        }

                        results[topicPartition] = PartitionResult(topicPartition, partition.ErrorCode);
                    }
                }

                if (retryFailure is not null)
                {
                    foreach (var topicPartition in pending)
                        retryErrors[topicPartition] = Protocol.ErrorCode.UnknownServerError;
                    throw retryFailure;
                }

                foreach (var topicPartition in pending)
                    results[topicPartition] = PartitionResult(topicPartition, Protocol.ErrorCode.UnknownServerError);

                return OrderPartitionResults(requestedPartitions, results);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            RetryHelper.IsRetriableRequestFailure(exception) &&
            !cancellationToken.IsCancellationRequested)
        {
            foreach (var (topicPartition, errorCode) in retryErrors)
                results.TryAdd(topicPartition, PartitionResult(topicPartition, errorCode));

            return CompletePartitionResults(requestedPartitions, results, GetRetryErrorCode(exception));
        }
    }

    private async ValueTask<IReadOnlyDictionary<TopicPartition, StreamsGroupOffsetOperationResult>> DeleteStreamsGroupOffsetsCoreAsync(
        string groupId,
        IReadOnlyList<TopicPartition> partitions,
        CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var results = new Dictionary<TopicPartition, StreamsGroupOffsetOperationResult>(partitions.Count);
        var retryErrors = new Dictionary<TopicPartition, Protocol.ErrorCode>(partitions.Count);
        var ambiguousPartitions = new HashSet<TopicPartition>();

        try
        {
            return await WithRetryAsync<IReadOnlyDictionary<TopicPartition, StreamsGroupOffsetOperationResult>>(async () =>
            {
                retryErrors.Clear();
                var pending = partitions.Where(partition => !results.ContainsKey(partition)).ToArray();
                int coordinatorId;
                try
                {
                    coordinatorId = await FindGroupCoordinatorAsync(groupId, cancellationToken).ConfigureAwait(false);
                }
                catch (Errors.GroupException exception) when (
                    exception.ErrorCode is { } errorCode &&
                    !errorCode.IsRetriable() &&
                    !errorCode.RequiresMetadataRefresh())
                {
                    return CompletePartitionResults(partitions, results, errorCode);
                }
                using var connectionLease = await _connectionPool.LeaseConnectionAsync(coordinatorId, cancellationToken).ConfigureAwait(false);
                var connection = connectionLease.Connection;
                var apiVersion = _metadataManager.GetNegotiatedApiVersion(
                    connection,
                    Protocol.ApiKey.OffsetDelete,
                    OffsetDeleteRequest.LowestSupportedVersion,
                    OffsetDeleteRequest.HighestSupportedVersion);

                OffsetDeleteResponse response;
                try
                {
                    response = await connection.SendAsync<OffsetDeleteRequest, OffsetDeleteResponse>(
                        new OffsetDeleteRequest
                        {
                            GroupId = groupId,
                            Topics = BuildOffsetDeleteTopics(pending)
                        },
                        apiVersion,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (
                    RetryHelper.IsRetriableRequestFailure(exception) &&
                    !cancellationToken.IsCancellationRequested)
                {
                    ambiguousPartitions.UnionWith(pending);
                    var errorCode = GetRetryErrorCode(exception);
                    foreach (var topicPartition in pending)
                        retryErrors[topicPartition] = errorCode;
                    throw;
                }

                var groupError = response.ErrorCode;
                if (groupError.IsRetriable() || groupError.RequiresMetadataRefresh())
                {
                    if (groupError == Protocol.ErrorCode.RequestTimedOut)
                        ambiguousPartitions.UnionWith(pending);

                    foreach (var topicPartition in pending)
                        retryErrors[topicPartition] = groupError;

                    throw new Errors.GroupException(
                        groupError,
                        $"DeleteStreamsGroupOffsets failed for group '{groupId}': {groupError}",
                        isRetriable: true)
                    {
                        GroupId = groupId
                    };
                }

                Exception? retryFailure = null;
                if (groupError != Protocol.ErrorCode.None)
                {
                    foreach (var topicPartition in pending)
                    {
                        var errorCode = groupError == Protocol.ErrorCode.GroupIdNotFound &&
                                        ambiguousPartitions.Contains(topicPartition)
                            ? Protocol.ErrorCode.None
                            : groupError;
                        results[topicPartition] = PartitionResult(topicPartition, errorCode);
                    }
                }
                else
                {
                    var missing = pending.ToHashSet();
                    foreach (var topic in response.Topics)
                    {
                        foreach (var partition in topic.Partitions)
                        {
                            var topicPartition = new TopicPartition(topic.Name, partition.PartitionIndex);
                            if (!missing.Remove(topicPartition))
                                continue;

                            if (partition.ErrorCode.IsRetriable() || partition.ErrorCode.RequiresMetadataRefresh())
                            {
                                ambiguousPartitions.Add(topicPartition);
                                retryErrors[topicPartition] = partition.ErrorCode;
                                retryFailure ??= new Errors.GroupException(
                                    partition.ErrorCode,
                                    $"DeleteStreamsGroupOffsets failed for {topic.Name}-{partition.PartitionIndex}: {partition.ErrorCode}",
                                    isRetriable: true)
                                {
                                    GroupId = groupId
                                };
                                continue;
                            }

                            results[topicPartition] = PartitionResult(topicPartition, partition.ErrorCode);
                        }
                    }

                    if (retryFailure is not null)
                    {
                        foreach (var topicPartition in missing)
                            retryErrors[topicPartition] = Protocol.ErrorCode.UnknownServerError;
                        throw retryFailure;
                    }

                    foreach (var topicPartition in missing)
                        results[topicPartition] = PartitionResult(topicPartition, Protocol.ErrorCode.UnknownServerError);
                }

                return OrderPartitionResults(partitions, results);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            RetryHelper.IsRetriableRequestFailure(exception) &&
            !cancellationToken.IsCancellationRequested)
        {
            foreach (var (topicPartition, errorCode) in retryErrors)
                results.TryAdd(topicPartition, PartitionResult(topicPartition, errorCode));

            return CompletePartitionResults(partitions, results, GetRetryErrorCode(exception));
        }
    }

    private async ValueTask<IReadOnlyDictionary<string, DeleteStreamsGroupResult>> DeleteStreamsGroupsCoreAsync(
        IReadOnlyList<string> groupIds,
        CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var results = new Dictionary<string, DeleteStreamsGroupResult>(groupIds.Count, StringComparer.Ordinal);
        var ambiguousGroups = new HashSet<string>(StringComparer.Ordinal);
        var retryErrors = new Dictionary<string, Protocol.ErrorCode>(groupIds.Count, StringComparer.Ordinal);

        try
        {
            return await WithRetryAsync<IReadOnlyDictionary<string, DeleteStreamsGroupResult>>(async () =>
            {
                retryErrors.Clear();
                Exception? retryFailure = null;
                var groupsByCoordinator = new Dictionary<int, List<string>>();
                foreach (var groupId in groupIds)
                {
                    if (results.ContainsKey(groupId))
                        continue;

                    int coordinatorId;
                    try
                    {
                        coordinatorId = await FindGroupCoordinatorAsync(
                            groupId,
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (Errors.GroupException exception) when (
                        exception.ErrorCode is { } errorCode &&
                        !errorCode.IsRetriable() &&
                        !errorCode.RequiresMetadataRefresh())
                    {
                        results[groupId] = DeleteGroupResult(groupId, errorCode);
                        continue;
                    }
                    catch (Exception exception) when (
                        RetryHelper.IsRetriableRequestFailure(exception) &&
                        !cancellationToken.IsCancellationRequested)
                    {
                        retryErrors[groupId] = GetRetryErrorCode(exception);
                        retryFailure ??= exception;
                        continue;
                    }

                    if (!groupsByCoordinator.TryGetValue(coordinatorId, out var coordinatorGroups))
                    {
                        coordinatorGroups = [];
                        groupsByCoordinator[coordinatorId] = coordinatorGroups;
                    }
                    coordinatorGroups.Add(groupId);
                }

                foreach (var (coordinatorId, coordinatorGroups) in groupsByCoordinator)
                {
                    using var connectionLease = await _connectionPool.LeaseConnectionAsync(
                        coordinatorId,
                        cancellationToken).ConfigureAwait(false);
                    var connection = connectionLease.Connection;
                    var apiVersion = _metadataManager.GetNegotiatedApiVersion(
                        connection,
                        Protocol.ApiKey.DeleteGroups,
                        DeleteGroupsRequest.LowestSupportedVersion,
                        DeleteGroupsRequest.HighestSupportedVersion);

                    DeleteGroupsResponse response;
                    try
                    {
                        response = await connection.SendAsync<DeleteGroupsRequest, DeleteGroupsResponse>(
                            new DeleteGroupsRequest { GroupsNames = coordinatorGroups },
                            apiVersion,
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception exception) when (
                        RetryHelper.IsRetriableRequestFailure(exception) &&
                        !cancellationToken.IsCancellationRequested)
                    {
                        ambiguousGroups.UnionWith(coordinatorGroups);
                        foreach (var groupId in coordinatorGroups)
                            retryErrors[groupId] = GetRetryErrorCode(exception);
                        retryFailure ??= exception;
                        continue;
                    }

                    var requested = new HashSet<string>(coordinatorGroups, StringComparer.Ordinal);
                    foreach (var groupResult in response.Results)
                    {
                        if (!requested.Contains(groupResult.GroupId))
                            continue;

                        var errorCode = groupResult.ErrorCode;
                        if (errorCode.IsRetriable() || errorCode.RequiresMetadataRefresh())
                        {
                            if (errorCode == Protocol.ErrorCode.RequestTimedOut)
                                ambiguousGroups.Add(groupResult.GroupId);

                            retryErrors[groupResult.GroupId] = errorCode;
                            retryFailure ??= new Errors.GroupException(
                                errorCode,
                                $"DeleteStreamsGroups failed for group '{groupResult.GroupId}': {errorCode}",
                                isRetriable: true)
                            {
                                GroupId = groupResult.GroupId
                            };
                            continue;
                        }

                        if (errorCode == Protocol.ErrorCode.GroupIdNotFound &&
                            ambiguousGroups.Contains(groupResult.GroupId))
                        {
                            errorCode = Protocol.ErrorCode.None;
                        }

                        results[groupResult.GroupId] = DeleteGroupResult(groupResult.GroupId, errorCode);
                    }
                }

                if (retryFailure is not null)
                    throw retryFailure;

                return CompleteDeleteGroupResults(groupIds, results);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            RetryHelper.IsRetriableRequestFailure(exception) &&
            !cancellationToken.IsCancellationRequested)
        {
            foreach (var (groupId, errorCode) in retryErrors)
                results.TryAdd(groupId, DeleteGroupResult(groupId, errorCode));

            return CompleteDeleteGroupResults(groupIds, results);
        }
    }

    private IReadOnlyList<OffsetFetchRequestTopic>? BuildOffsetFetchTopics(
        IReadOnlyList<TopicPartition>? partitions,
        short apiVersion,
        string operation,
        out OffsetTopicIdRequestMap? topicMap)
    {
        topicMap = null;
        if (partitions is null)
            return null;

        var groups = partitions.GroupBy(static partition => partition.Topic).ToArray();
        var requestTopicMap = apiVersion >= OffsetFetchRequest.TopicIdVersion
            ? new OffsetTopicIdRequestMap(_metadataManager.Metadata, groups.Length)
            : null;
        topicMap = requestTopicMap;

        return groups.Select(group => new OffsetFetchRequestTopic
        {
            Name = group.Key,
            TopicId = requestTopicMap?.AddTopic(group.Key, operation) ?? Guid.Empty,
            PartitionIndexes = group.Select(static partition => partition.Partition).ToArray()
        }).ToArray();
    }

    private IReadOnlyList<OffsetCommitRequestTopic> BuildOffsetCommitTopics(
        IReadOnlyList<TopicPartitionOffset> offsets,
        short apiVersion,
        string operation,
        out OffsetTopicIdRequestMap? topicMap)
    {
        var groups = offsets.GroupBy(static offset => offset.Topic).ToArray();
        var requestTopicMap = apiVersion >= OffsetCommitRequest.TopicIdVersion
            ? new OffsetTopicIdRequestMap(_metadataManager.Metadata, groups.Length)
            : null;
        topicMap = requestTopicMap;

        return groups.Select(group => new OffsetCommitRequestTopic
        {
            Name = group.Key,
            TopicId = requestTopicMap?.AddTopic(group.Key, operation) ?? Guid.Empty,
            Partitions = group.Select(static offset => new OffsetCommitRequestPartition
            {
                PartitionIndex = offset.Partition,
                CommittedOffset = offset.Offset,
                CommittedLeaderEpoch = offset.LeaderEpoch,
                CommittedMetadata = offset.Metadata
            }).ToArray()
        }).ToArray();
    }

    private static IReadOnlyList<OffsetDeleteRequestTopic> BuildOffsetDeleteTopics(
        IEnumerable<TopicPartition> partitions) => partitions
        .GroupBy(static partition => partition.Topic)
        .Select(static group => new OffsetDeleteRequestTopic
        {
            Name = group.Key,
            Partitions = group.Select(static partition => new OffsetDeleteRequestPartition
            {
                PartitionIndex = partition.Partition
            }).ToArray()
        })
        .ToArray();

    private static IReadOnlyList<TopicPartition>? ValidateDistinctPartitions(
        IEnumerable<TopicPartition>? partitions,
        string paramName)
    {
        if (partitions is null)
            return null;

        var result = partitions.ToArray();
        var seen = new HashSet<TopicPartition>();
        foreach (var partition in result)
        {
            ValidateTopicPartition(partition, paramName);
            if (!seen.Add(partition))
                throw new ArgumentException($"Partition '{partition.Topic}-{partition.Partition}' is duplicated.", paramName);
        }
        return result;
    }

    private static StreamsGroupOffsetOperationResult PartitionResult(
        TopicPartition topicPartition,
        Protocol.ErrorCode errorCode) => new()
        {
            TopicPartition = topicPartition,
            ErrorCode = errorCode
        };

    private static IReadOnlyDictionary<string, StreamsGroupOffsetsResult> OrderGroupResults(
        IEnumerable<string> groupIds,
        IReadOnlyDictionary<string, StreamsGroupOffsetsResult> results)
    {
        var ordered = new Dictionary<string, StreamsGroupOffsetsResult>(results.Count, StringComparer.Ordinal);
        foreach (var groupId in groupIds)
            ordered[groupId] = results[groupId];
        return ordered;
    }

    private static StreamsGroupOffsetsResult GroupOffsetsError(
        string groupId,
        Protocol.ErrorCode errorCode) => new()
    {
        GroupId = groupId,
        ErrorCode = errorCode,
        Offsets = new Dictionary<TopicPartition, StreamsGroupOffsetDescription>()
    };

    private static IReadOnlyDictionary<string, DeleteStreamsGroupResult> OrderDeleteGroupResults(
        IEnumerable<string> groupIds,
        IReadOnlyDictionary<string, DeleteStreamsGroupResult> results)
    {
        var ordered = new Dictionary<string, DeleteStreamsGroupResult>(results.Count, StringComparer.Ordinal);
        foreach (var groupId in groupIds)
            ordered[groupId] = results[groupId];
        return ordered;
    }

    private static IReadOnlyDictionary<string, DeleteStreamsGroupResult> CompleteDeleteGroupResults(
        IReadOnlyList<string> groupIds,
        Dictionary<string, DeleteStreamsGroupResult> results)
    {
        foreach (var groupId in groupIds)
        {
            results.TryAdd(
                groupId,
                DeleteGroupResult(groupId, Protocol.ErrorCode.UnknownServerError));
        }
        return OrderDeleteGroupResults(groupIds, results);
    }

    private static DeleteStreamsGroupResult DeleteGroupResult(
        string groupId,
        Protocol.ErrorCode errorCode) => new()
    {
        GroupId = groupId,
        ErrorCode = errorCode
    };

    private static Protocol.ErrorCode GetRetryErrorCode(Exception exception) =>
        exception is Errors.KafkaException { ErrorCode: { } errorCode }
            ? errorCode
            : Protocol.ErrorCode.UnknownServerError;

    private static IReadOnlyDictionary<TopicPartition, StreamsGroupOffsetOperationResult> CompletePartitionResults(
        IReadOnlyList<TopicPartition> partitions,
        Dictionary<TopicPartition, StreamsGroupOffsetOperationResult> results,
        Protocol.ErrorCode fallbackError)
    {
        foreach (var topicPartition in partitions)
            results.TryAdd(topicPartition, PartitionResult(topicPartition, fallbackError));

        return OrderPartitionResults(partitions, results);
    }

    private static IReadOnlyDictionary<TopicPartition, StreamsGroupOffsetOperationResult> OrderPartitionResults(
        IEnumerable<TopicPartition> partitions,
        IReadOnlyDictionary<TopicPartition, StreamsGroupOffsetOperationResult> results)
    {
        var ordered = new Dictionary<TopicPartition, StreamsGroupOffsetOperationResult>(results.Count);
        foreach (var partition in partitions)
            ordered[partition] = results[partition];
        return ordered;
    }

    private static ValueTask<IReadOnlyDictionary<TopicPartition, StreamsGroupOffsetOperationResult>> EmptyPartitionOperationResults() =>
        new(new Dictionary<TopicPartition, StreamsGroupOffsetOperationResult>());

    private static async ValueTask<T> ExecuteWithTimeoutAsync<T>(
        Func<CancellationToken, ValueTask<T>> operation,
        int timeoutMs,
        string operationName,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeoutMs);
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
