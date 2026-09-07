using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Retry;

namespace Dekaf.Admin;

public sealed partial class AdminClient : IShareGroupOffsetQueryAdminClient
{
    public ValueTask<IReadOnlyDictionary<string, ShareGroupOffsetsResult>> ListShareGroupOffsetsAsync(
        IReadOnlyDictionary<string, ListShareGroupOffsetsSpec> groupSpecs,
        ListShareGroupOffsetsOptions? options = null, CancellationToken cancellationToken = default)
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
        var opts = options ?? new ListShareGroupOffsetsOptions();
        ArgumentOutOfRangeException.ThrowIfNegative(opts.TimeoutMs);
        ThrowIfShareOffsetQueryDeadlineExpired(opts.TimeoutMs);
        return ExecuteWithTimeoutAsync(token => ListShareGroupOffsetsCoreAsync(requests, token),
            opts.TimeoutMs, nameof(ListShareGroupOffsetsAsync), cancellationToken);
    }

    private async ValueTask<IReadOnlyDictionary<string, ShareGroupOffsetsResult>> ListShareGroupOffsetsCoreAsync(
        Dictionary<string, IReadOnlyList<TopicPartition>?> requests, CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, ShareGroupOffsetsResult>(requests.Count, StringComparer.Ordinal);
        var requestGroups = new Dictionary<string, DescribeShareGroupOffsetsRequestGroup>(requests.Count, StringComparer.Ordinal);
        foreach (var (groupId, partitions) in requests)
        {
            if (partitions is { Count: 0 })
            {
                results.Add(groupId, ShareGroupOffsetsError(groupId, ErrorCode.None));
                continue;
            }
            IReadOnlyList<DescribeShareGroupOffsetsRequestTopic>? topics = null;
            if (partitions is not null)
            {
                var byTopic = new Dictionary<string, List<int>>(StringComparer.Ordinal);
                foreach (var partition in partitions)
                {
                    if (!byTopic.TryGetValue(partition.Topic, out var indexes))
                        byTopic.Add(partition.Topic, indexes = []);
                    indexes.Add(partition.Partition);
                }
                var topicList = new List<DescribeShareGroupOffsetsRequestTopic>(byTopic.Count);
                foreach (var (topic, indexes) in byTopic)
                    topicList.Add(new() { TopicName = topic, Partitions = indexes });
                topics = topicList;
            }
            requestGroups.Add(groupId, new() { GroupId = groupId, Topics = topics });
        }
        if (requestGroups.Count == 0)
            return results;

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var failures = new Dictionary<string, ShareGroupOffsetsResult>(StringComparer.Ordinal);
        try
        {
            await WithRetryAsync(async () =>
            {
                Exception? retryFailure = null;
                var byCoordinator = new Dictionary<int, List<DescribeShareGroupOffsetsRequestGroup>>();
                foreach (var (groupId, request) in requestGroups)
                {
                    if (results.ContainsKey(groupId))
                        continue;
                    try
                    {
                        var coordinator = await FindGroupCoordinatorAsync(groupId, cancellationToken).ConfigureAwait(false);
                        if (!byCoordinator.TryGetValue(coordinator, out var groups))
                            byCoordinator.Add(coordinator, groups = []);
                        groups.Add(request);
                    }
                    catch (Exception exception) when (IsShareOffsetQueryFailure(exception, cancellationToken))
                    {
                        CaptureFailure(groupId, exception);
                    }
                }
                foreach (var (coordinator, groups) in byCoordinator)
                {
                    try
                    {
                        using var lease = await _connectionPool.LeaseConnectionAsync(coordinator, cancellationToken).ConfigureAwait(false);
                        // Both supported versions batch groups and allow null selection. Version 0 omits lag.
                        var version = _metadataManager.GetNegotiatedApiVersion(lease.Connection, ApiKey.DescribeShareGroupOffsets,
                            DescribeShareGroupOffsetsRequest.LowestSupportedVersion, DescribeShareGroupOffsetsRequest.HighestSupportedVersion);
                        var response = await lease.Connection.SendAsync<DescribeShareGroupOffsetsRequest, DescribeShareGroupOffsetsResponse>(
                            new() { Groups = groups }, version, cancellationToken).ConfigureAwait(false);
                        cancellationToken.ThrowIfCancellationRequested();
                        var responseGroups = new Dictionary<string, DescribeShareGroupOffsetsResponseGroup?>(StringComparer.Ordinal);
                        foreach (var group in response.Groups)
                            if (!responseGroups.TryAdd(group.GroupId, group))
                                responseGroups[group.GroupId] = null;
                        foreach (var request in groups)
                        {
                            if (!responseGroups.TryGetValue(request.GroupId, out var found) || found is null)
                            {
                                results[request.GroupId] = ShareGroupOffsetsError(request.GroupId, ErrorCode.UnknownServerError,
                                    "The broker returned missing or duplicate group outcomes.");
                                continue;
                            }
                            if (found.ErrorCode.IsRetriable())
                            {
                                failures[request.GroupId] = ShareGroupOffsetsError(request.GroupId, found.ErrorCode, found.ErrorMessage);
                                retryFailure ??= new GroupException(found.ErrorCode, found.ErrorMessage ?? "Share-group offset query failed.") { GroupId = found.GroupId };
                            }
                            else
                                results[request.GroupId] = MapShareGroupOffsets(found, requests[request.GroupId], version);
                        }
                    }
                    catch (Exception exception) when (IsShareOffsetQueryFailure(exception, cancellationToken))
                    {
                        foreach (var request in groups)
                            if (!results.ContainsKey(request.GroupId))
                                CaptureFailure(request.GroupId, exception);
                    }
                }
                if (retryFailure is not null)
                    throw retryFailure;

                void CaptureFailure(string groupId, Exception exception)
                {
                    var error = ShareGroupOffsetsError(groupId, ShareOffsetQueryErrorCode(exception), exception.Message);
                    if (RetryHelper.IsRetriableRequestFailure(exception))
                    {
                        failures[groupId] = error;
                        retryFailure ??= exception;
                    }
                    else
                        results[groupId] = error;
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested &&
            (exception is KafkaException || RetryHelper.IsRetriableRequestFailure(exception)))
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception exception) when (IsShareOffsetQueryFailure(exception, cancellationToken))
        {
            foreach (var groupId in requests.Keys)
                if (!results.ContainsKey(groupId))
                    results[groupId] = failures.TryGetValue(groupId, out var failure)
                        ? failure : ShareGroupOffsetsError(groupId, ShareOffsetQueryErrorCode(exception), exception.Message);
        }
        cancellationToken.ThrowIfCancellationRequested();
        var ordered = new Dictionary<string, ShareGroupOffsetsResult>(requests.Count, StringComparer.Ordinal);
        foreach (var groupId in requests.Keys)
            ordered.Add(groupId, results[groupId]);
        return ordered;
    }

    private static bool IsShareOffsetQueryFailure(Exception exception, CancellationToken token) =>
        !token.IsCancellationRequested && (exception is KafkaException || RetryHelper.IsRetriableRequestFailure(exception));

    private static ErrorCode ShareOffsetQueryErrorCode(Exception exception) =>
        exception is BrokerVersionException ? ErrorCode.UnsupportedVersion : GetRetryErrorCode(exception);

    internal static void ThrowIfShareOffsetQueryDeadlineExpired(int timeoutMs)
    {
        if (timeoutMs == 0)
            throw new KafkaTimeoutException(TimeoutKind.Api, TimeSpan.Zero, TimeSpan.Zero,
                "ListShareGroupOffsetsAsync timed out before querying offsets.");
    }

    internal static ShareGroupOffsetsResult ShareGroupOffsetsError(string groupId, ErrorCode errorCode, string? message = null) =>
        new() { GroupId = groupId, ErrorCode = errorCode, ErrorMessage = message, Offsets = new Dictionary<TopicPartition, ShareGroupOffsetDescription>() };

    private static ShareGroupOffsetsResult MapShareGroupOffsets(DescribeShareGroupOffsetsResponseGroup group,
        IReadOnlyList<TopicPartition>? selected, short version)
    {
        if (group.ErrorCode != ErrorCode.None)
            return ShareGroupOffsetsError(group.GroupId, group.ErrorCode, group.ErrorMessage);
        var offsets = new Dictionary<TopicPartition, ShareGroupOffsetDescription>();
        var requested = selected is null ? null : new HashSet<TopicPartition>(selected);
        foreach (var topic in group.Topics)
        {
            foreach (var partition in topic.Partitions)
            {
                var key = new TopicPartition(topic.TopicName, partition.PartitionIndex);
                if (requested is not null && !requested.Contains(key))
                    continue;
                var duplicate = offsets.ContainsKey(key);
                offsets[key] = new ShareGroupOffsetDescription
                {
                    TopicPartition = key, StartOffset = duplicate ? -1 : partition.StartOffset,
                    LeaderEpoch = duplicate ? -1 : partition.LeaderEpoch, Lag = !duplicate && version >= 1 ? partition.Lag : -1,
                    ErrorCode = duplicate ? ErrorCode.UnknownServerError : partition.ErrorCode,
                    ErrorMessage = duplicate ? "The broker returned duplicate partition outcomes." : partition.ErrorMessage
                };
            }
        }
        if (selected is not null)
            foreach (var key in selected)
                if (!offsets.ContainsKey(key))
                    offsets.Add(key, new ShareGroupOffsetDescription
                    {
                        TopicPartition = key, StartOffset = -1, LeaderEpoch = -1, ErrorCode = ErrorCode.UnknownServerError,
                        ErrorMessage = "The broker omitted the requested partition."
                    });
        return new() { GroupId = group.GroupId, ErrorCode = group.ErrorCode, ErrorMessage = group.ErrorMessage, Offsets = offsets };
    }
}
