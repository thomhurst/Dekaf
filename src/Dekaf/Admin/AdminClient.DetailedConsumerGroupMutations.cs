using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Retry;

namespace Dekaf.Admin;

public sealed partial class AdminClient : IDetailedConsumerGroupMutationAdminClient
{
    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> DeleteConsumerGroupsDetailedAsync(
        IEnumerable<string> groupIds, ConsumerGroupMutationOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var groups = SnapshotMutationKeys(groupIds, nameof(groupIds), static group => ArgumentException.ThrowIfNullOrWhiteSpace(group, nameof(groupIds)));
        var timeoutMs = options?.TimeoutMs ?? 30000;
        ArgumentOutOfRangeException.ThrowIfNegative(timeoutMs);
        return DeleteDetailedGroupsAsync(groups, timeoutMs, cancellationToken);
    }

    private async ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> DeleteDetailedGroupsAsync(
        List<string> groups, int timeoutMs, CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, AdminMutationResult>(groups.Count, StringComparer.Ordinal);
        if (groups.Count == 0) return results;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeoutMs == 0) deadline.Cancel();
        else deadline.CancelAfter(timeoutMs);
        var token = deadline.Token;
        const string operation = "DeleteConsumerGroupsDetailed";
        HashSet<string>? stoppedGroups = null;

        void StopUnsentGroup(string group, Exception failure)
        {
            (stoppedGroups ??= new(StringComparer.Ordinal)).Add(group);
            // Preserve a prior confirmed coordinator rejection if its retry cannot start.
            if (!results.ContainsKey(group))
                results.Add(group, AdminMutationResult.Unconfirmed(AdminMutationOutcome.NotAttempted,
                    "The operation stopped before this mutation was sent.", failure));
        }
        try
        {
            await WithRetryAsync(async () =>
            {
                token.ThrowIfCancellationRequested();
                await EnsureInitializedAsync(token, operation).ConfigureAwait(false);
                var byCoordinator = new Dictionary<int, List<string>>();
                Exception? retryFailure = null;
                foreach (var group in groups)
                {
                    // A later discovery/lease failure must not replay groups completed by
                    // an earlier coordinator. Only unsent work and confirmed rejections remain.
                    if (stoppedGroups?.Contains(group) == true ||
                        (results.TryGetValue(group, out var prior) && !AdminMutationResult.IsSafeCoordinatorRetry(prior)))
                        continue;
                    token.ThrowIfCancellationRequested();
                    int coordinator;
                    try
                    {
                        coordinator = await FindGroupCoordinatorAsync(group, token).ConfigureAwait(false);
                    }
                    catch (Exception exception) when (IsDetailedMutationFailure(exception) || exception is InvalidOperationException or MalformedProtocolDataException)
                    {
                        token.ThrowIfCancellationRequested();
                        if (RetryHelper.IsRetriableRequestFailure(exception))
                            retryFailure ??= exception;
                        else
                            StopUnsentGroup(group, exception);
                        continue;
                    }
                    if (!byCoordinator.TryGetValue(coordinator, out var pending))
                        byCoordinator.Add(coordinator, pending = []);
                    pending.Add(group);
                }

                foreach (var batch in byCoordinator)
                {
                    token.ThrowIfCancellationRequested();
                    KafkaConnectionLease acquiredLease = default;
                    short version;
                    try
                    {
                        acquiredLease = await _connectionPool.LeaseConnectionAsync(batch.Key, token).ConfigureAwait(false);
                        version = _metadataManager.GetNegotiatedApiVersion(acquiredLease.Connection, ApiKey.DeleteGroups,
                            DeleteGroupsRequest.LowestSupportedVersion, DeleteGroupsRequest.HighestSupportedVersion);
                    }
                    catch (Exception exception) when (IsDetailedMutationFailure(exception) || exception is InvalidOperationException or MalformedProtocolDataException)
                    {
                        acquiredLease.Dispose();
                        token.ThrowIfCancellationRequested();
                        if (RetryHelper.IsRetriableRequestFailure(exception))
                            retryFailure ??= exception;
                        else
                            foreach (var group in batch.Value)
                                StopUnsentGroup(group, exception);
                        continue;
                    }
                    using var lease = acquiredLease;
                    var request = new DeleteGroupsRequest { GroupsNames = batch.Value };
                    token.ThrowIfCancellationRequested();
                    DeleteGroupsResponse response;
                    try
                    {
                        response = await lease.Connection.SendAsync<DeleteGroupsRequest, DeleteGroupsResponse>(request, version, token).ConfigureAwait(false);
                    }
                    catch (Exception exception) when (IsDetailedMutationFailure(exception) || exception is InvalidOperationException or MalformedProtocolDataException)
                    {
                        var failure = MutationFailure(exception, deadline, timeoutMs, operation, cancellationToken);
                        foreach (var group in batch.Value)
                            results[group] = AdminMutationResult.Unconfirmed(AdminMutationOutcome.Unknown,
                                "The mutation may have applied; no definitive response was received.", failure);
                        continue;
                    }

                    var received = MapMutationResults(response.Results, static item => item.GroupId,
                        static item => item.ErrorCode, static _ => null);
                    foreach (var group in batch.Value)
                    {
                        var result = received.TryGetValue(group, out var outcome) ? outcome
                            : AdminMutationResult.Unconfirmed(AdminMutationOutcome.Unknown, "The response omitted this requested entity.");
                        results[group] = result;
                        if (AdminMutationResult.IsSafeCoordinatorRetry(result))
                            retryFailure ??= new KafkaException(result.ErrorCode!.Value, "The coordinator rejected the mutation.");
                    }
                }
                if (retryFailure is not null) throw retryFailure;
            }, token).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsDetailedMutationFailure(exception) || exception is InvalidOperationException or MalformedProtocolDataException)
        {
            AddNotAttemptedMutations(groups, static group => group, results,
                MutationFailure(exception, deadline, timeoutMs, operation, cancellationToken));
        }
        return results;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<TopicPartition, AdminMutationResult>> AlterConsumerGroupOffsetsDetailedAsync(
        string groupId, IEnumerable<TopicPartitionOffset> offsets, ConsumerGroupMutationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        cancellationToken.ThrowIfCancellationRequested();
        var items = SnapshotDetailedConsumerOffsets(offsets);
        var timeoutMs = options?.TimeoutMs ?? 30000;
        ArgumentOutOfRangeException.ThrowIfNegative(timeoutMs);
        Dictionary<Guid, (OffsetCommitRequestTopic? Topic, bool Matched)>? identitiesById = null;
        return ExecuteDetailedMutationAsync<TopicPartition, TopicPartitionOffset, OffsetCommitRequest, OffsetCommitResponse>(
            items, static item => new(item.Topic, item.Partition),
            new(ApiKey.OffsetCommit, OffsetCommitRequest.LowestSupportedVersion, OffsetCommitRequest.HighestSupportedVersion,
                "AlterConsumerGroupOffsetsDetailed", groupId), timeoutMs,
            (pending, version, localResults) =>
            {
                var topics = new Dictionary<string, List<OffsetCommitRequestPartition>>(StringComparer.Ordinal);
                foreach (var item in pending)
                {
                    if (!topics.TryGetValue(item.Topic, out var partitions)) topics.Add(item.Topic, partitions = []);
                    partitions.Add(new() { PartitionIndex = item.Partition, CommittedOffset = item.Offset,
                        CommittedLeaderEpoch = item.LeaderEpoch, CommittedMetadata = item.Metadata });
                }
                var requests = new List<OffsetCommitRequestTopic>(topics.Count);
                var snapshot = version >= OffsetCommitRequest.TopicIdVersion ? _metadataManager.Metadata.CaptureSnapshot() : null;
                identitiesById = snapshot is null ? null : new(topics.Count);
                HashSet<string>? excludedTopics = null;
                foreach (var topic in topics)
                {
                    var id = Guid.Empty;
                    if (snapshot is not null)
                    {
                        if (!snapshot.Topics.TryGetValue(topic.Key, out var metadata) || metadata.TopicId == Guid.Empty)
                        {
                            ExcludeTopic(topic.Key, new KafkaException(ErrorCode.UnknownTopicId,
                                $"AlterConsumerGroupOffsetsDetailed requires a topic ID for '{topic.Key}', but metadata has no current mapping"));
                            continue;
                        }
                        id = metadata.TopicId;
                    }
                    var requestTopic = new OffsetCommitRequestTopic { Name = topic.Key, TopicId = id, Partitions = topic.Value };
                    if (identitiesById is not null && !identitiesById.TryAdd(id, (requestTopic, false)))
                    {
                        var previousName = identitiesById[id].Topic?.Name;
                        // Keep the invalid ID registered so a third colliding name cannot reuse it.
                        identitiesById[id] = (null, false);
                        var exception = new KafkaException(ErrorCode.UnknownTopicId,
                            $"AlterConsumerGroupOffsetsDetailed metadata maps more than one requested topic to ID '{id}'");
                        if (previousName is not null) ExcludeTopic(previousName, exception);
                        ExcludeTopic(topic.Key, exception);
                        continue;
                    }
                    requests.Add(requestTopic);
                }
                if (excludedTopics is not null)
                {
                    // A collision invalidates the first topic's already-built request too.
                    var retained = 0;
                    for (var index = 0; index < requests.Count; index++)
                        if (!excludedTopics.Contains(requests[index].Name))
                            requests[retained++] = requests[index];
                    requests.RemoveRange(retained, requests.Count - retained);
                    retained = 0;
                    for (var index = 0; index < pending.Count; index++)
                        if (!excludedTopics.Contains(pending[index].Topic))
                            pending[retained++] = pending[index];
                    pending.RemoveRange(retained, pending.Count - retained);
                }
                return new() { GroupId = groupId, GenerationIdOrMemberEpoch = -1, MemberId = string.Empty, Topics = requests };

                void ExcludeTopic(string name, KafkaException exception)
                {
                    (excludedTopics ??= new(StringComparer.Ordinal)).Add(name);
                    foreach (var partition in topics[name])
                    {
                        var key = new TopicPartition(name, partition.PartitionIndex);
                        if (!localResults.ContainsKey(key))
                            localResults.Add(key, AdminMutationResult.Unconfirmed(AdminMutationOutcome.NotAttempted,
                                "No mutation was sent because the topic ID could not be mapped uniquely.", exception));
                    }
                }
            },
            (_, response) =>
            {
                var results = new Dictionary<TopicPartition, AdminMutationResult>();
                var current = identitiesById is null ? null : _metadataManager.Metadata.CaptureSnapshot();
                foreach (var topic in response.Topics)
                {
                    var name = topic.Name;
                    if (identitiesById is not null)
                    {
                        if (!identitiesById.TryGetValue(topic.TopicId, out var identity) || identity.Topic is not { } requestedTopic)
                            continue;
                        name = requestedTopic.Name;
                        if (identity.Matched)
                        {
                            identitiesById[topic.TopicId] = (null, true);
                            // Duplicate blocks invalidate the whole topic, even with disjoint partitions.
                            // Replacing earlier rejections with Unknown also prevents unsafe retries.
                            foreach (var partition in requestedTopic.Partitions)
                                results[new(name, partition.PartitionIndex)] = AdminMutationResult.Unconfirmed(
                                    AdminMutationOutcome.Unknown, "The response contained duplicate topic identities.");
                            continue;
                        }
                        identitiesById[topic.TopicId] = (requestedTopic, true);
                        if (!current!.Topics.TryGetValue(name, out var metadata) || metadata.TopicId != topic.TopicId ||
                            !current.TopicsById.TryGetValue(topic.TopicId, out var currentById) ||
                            !string.Equals(currentById.Name, name, StringComparison.Ordinal))
                            continue; // The original identity is no longer current; omitted entries remain unknown.
                    }
                    foreach (var partition in topic.Partitions)
                        AddMutationResult(results, new(name, partition.PartitionIndex), AdminMutationResult.FromResponse(partition.ErrorCode, null));
                }
                return results;
            }, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<TopicPartition, AdminMutationResult>> DeleteConsumerGroupOffsetsDetailedAsync(
        string groupId, IEnumerable<TopicPartition> partitions, DeleteConsumerGroupOffsetsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        cancellationToken.ThrowIfCancellationRequested();
        var items = SnapshotMutationKeys(partitions, nameof(partitions), ValidateDetailedConsumerPartition);
        var timeoutMs = options?.TimeoutMs ?? 30000;
        ArgumentOutOfRangeException.ThrowIfNegative(timeoutMs);
        return ExecuteDetailedMutationAsync<TopicPartition, TopicPartition, OffsetDeleteRequest, OffsetDeleteResponse>(
            items, static item => item,
            new(ApiKey.OffsetDelete, OffsetDeleteRequest.LowestSupportedVersion, OffsetDeleteRequest.HighestSupportedVersion,
                "DeleteConsumerGroupOffsetsDetailed", groupId), timeoutMs,
            (pending, _, _) =>
            {
                var topics = new Dictionary<string, List<OffsetDeleteRequestPartition>>(StringComparer.Ordinal);
                foreach (var item in pending)
                {
                    if (!topics.TryGetValue(item.Topic, out var topicPartitions)) topics.Add(item.Topic, topicPartitions = []);
                    topicPartitions.Add(new() { PartitionIndex = item.Partition });
                }
                var requests = new List<OffsetDeleteRequestTopic>(topics.Count);
                foreach (var topic in topics) requests.Add(new() { Name = topic.Key, Partitions = topic.Value });
                return new() { GroupId = groupId, Topics = requests };
            },
            static (pending, response) =>
            {
                var results = new Dictionary<TopicPartition, AdminMutationResult>();
                if (response.ErrorCode != ErrorCode.None)
                {
                    foreach (var item in pending) results.Add(item, AdminMutationResult.FromResponse(response.ErrorCode, null));
                    return results;
                }
                foreach (var topic in response.Topics)
                    foreach (var partition in topic.Partitions)
                        AddMutationResult(results, new(topic.Name, partition.PartitionIndex), AdminMutationResult.FromResponse(partition.ErrorCode, null));
                return results;
            }, cancellationToken);
    }

    internal static List<TopicPartitionOffset> SnapshotDetailedConsumerOffsets(IEnumerable<TopicPartitionOffset> offsets)
    {
        ArgumentNullException.ThrowIfNull(offsets);
        var items = new List<TopicPartitionOffset>();
        var seen = new HashSet<TopicPartition>();
        foreach (var offset in offsets)
        {
            var partition = new TopicPartition(offset.Topic, offset.Partition);
            ValidateDetailedConsumerPartition(partition);
            if (!seen.Add(partition)) throw new ArgumentException("Offsets must contain distinct topic partitions.", nameof(offsets));
            items.Add(offset);
        }
        return items;
    }

    internal static void ValidateDetailedConsumerPartition(TopicPartition partition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partition.Topic);
        ArgumentOutOfRangeException.ThrowIfNegative(partition.Partition);
    }
}
