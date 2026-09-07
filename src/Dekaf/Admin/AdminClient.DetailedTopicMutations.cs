using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Admin;

public sealed partial class AdminClient : IDetailedTopicMutationAdminClient
{
    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> CreateTopicsDetailedAsync(
        IEnumerable<NewTopic> topics, CreateTopicsOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var opts = options ?? new CreateTopicsOptions();
        ArgumentOutOfRangeException.ThrowIfNegative(opts.TimeoutMs);
        var items = SnapshotDetailedTopics(topics);
        return ExecuteControllerMutationAsync<string, CreateTopicData, CreateTopicsRequest, CreateTopicsResponse>(
            items, static item => item.Name,
            new(ApiKey.CreateTopics, CreateTopicsRequest.LowestSupportedVersion, CreateTopicsRequest.HighestSupportedVersion, nameof(CreateTopicsAsync)),
            opts.TimeoutMs,
            (pending, _) => new() { Topics = pending, TimeoutMs = opts.TimeoutMs, ValidateOnly = opts.ValidateOnly },
            static (_, response) => MapMutationResults(response.Topics, static item => item.Name,
                static item => item.ErrorCode, static item => item.ErrorMessage), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> DeleteTopicsDetailedAsync(
        IEnumerable<string> topicNames, DeleteTopicsOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var timeout = options?.TimeoutMs ?? 30000;
        ArgumentOutOfRangeException.ThrowIfNegative(timeout);
        var names = SnapshotMutationKeys(topicNames, nameof(topicNames), static name => ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(topicNames)));
        return ExecuteControllerMutationAsync<string, string, DeleteTopicsRequest, DeleteTopicsResponse>(
            names, static name => name,
            new(ApiKey.DeleteTopics, DeleteTopicsRequest.LowestSupportedVersion, DeleteTopicsRequest.HighestSupportedVersion, nameof(DeleteTopicsAsync)),
            timeout,
            (pending, version) => version >= 6
                ? new() { Topics = pending.Select(static name => new DeleteTopicState { Name = name }).ToArray(), TimeoutMs = timeout }
                : new() { TopicNames = pending, TimeoutMs = timeout },
            static (_, response) => MapMutationResults(response.Responses, static item => item.Name ?? string.Empty,
                static item => item.ErrorCode, static item => item.ErrorMessage), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<Guid, AdminMutationResult>> DeleteTopicsDetailedAsync(
        IEnumerable<Guid> topicIds, DeleteTopicsOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var timeout = options?.TimeoutMs ?? 30000;
        ArgumentOutOfRangeException.ThrowIfNegative(timeout);
        var ids = SnapshotMutationKeys(topicIds, nameof(topicIds), static id =>
        {
            if (id == Guid.Empty) throw new ArgumentException("Topic IDs cannot contain the empty UUID.", nameof(topicIds));
        });
        return ExecuteControllerMutationAsync<Guid, Guid, DeleteTopicsRequest, DeleteTopicsResponse>(
            ids, static id => id,
            new(ApiKey.DeleteTopics, 6, DeleteTopicsRequest.HighestSupportedVersion, nameof(DeleteTopicsAsync)),
            timeout,
            (pending, _) => new() { Topics = pending.Select(static id => new DeleteTopicState { TopicId = id }).ToArray(), TimeoutMs = timeout },
            static (_, response) => MapMutationResults(response.Responses, static item => item.TopicId,
                static item => item.ErrorCode, static item => item.ErrorMessage), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> CreatePartitionsDetailedAsync(
        IReadOnlyDictionary<string, int> newPartitionCounts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(newPartitionCounts);
        cancellationToken.ThrowIfCancellationRequested();
        var topics = new Dictionary<string, NewPartitions>(newPartitionCounts.Count, StringComparer.Ordinal);
        foreach (var pair in newPartitionCounts)
            topics.Add(pair.Key, new() { TotalCount = pair.Value });
        return CreatePartitionsDetailedAsync(topics, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> CreatePartitionsDetailedAsync(
        IReadOnlyDictionary<string, NewPartitions> newPartitions, CreatePartitionsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(newPartitions);
        cancellationToken.ThrowIfCancellationRequested();
        var opts = options ?? new CreatePartitionsOptions();
        ArgumentOutOfRangeException.ThrowIfNegative(opts.TimeoutMs);
        var topics = BuildPartitionExpansionTopics(newPartitions);
        return ExecuteControllerMutationAsync<string, CreatePartitionsTopic, CreatePartitionsRequest, CreatePartitionsResponse>(
            topics, static item => item.Name,
            new(ApiKey.CreatePartitions, CreatePartitionsRequest.LowestSupportedVersion, CreatePartitionsRequest.HighestSupportedVersion, nameof(CreatePartitionsAsync)),
            opts.TimeoutMs,
            (pending, _) => new() { Topics = pending, TimeoutMs = opts.TimeoutMs, ValidateOnly = opts.ValidateOnly },
            static (_, response) => MapMutationResults(response.Results, static item => item.Name,
                static item => item.ErrorCode, static item => item.ErrorMessage), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<TopicPartition, AdminMutationResult>> AlterPartitionReassignmentsDetailedAsync(
        IReadOnlyDictionary<TopicPartition, Optional<NewPartitionReassignment>> reassignments,
        AlterPartitionReassignmentsOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var opts = options ?? new AlterPartitionReassignmentsOptions();
        ArgumentOutOfRangeException.ThrowIfNegative(opts.TimeoutMs);
        var items = SnapshotDetailedReassignments(reassignments);
        return ExecuteControllerMutationAsync<TopicPartition, KeyValuePair<TopicPartition, int[]?>,
            AlterPartitionReassignmentsRequest, AlterPartitionReassignmentsResponse>(
            items, static item => item.Key,
            new(ApiKey.AlterPartitionReassignments, opts.AllowReplicationFactorChange ? (short)0 : (short)1,
                AlterPartitionReassignmentsRequest.HighestSupportedVersion, nameof(AlterPartitionReassignmentsAsync)),
            opts.TimeoutMs,
            (pending, _) => new()
            {
                Topics = BuildDetailedReassignmentRequest(pending),
                TimeoutMs = opts.TimeoutMs,
                AllowReplicationFactorChange = opts.AllowReplicationFactorChange
            },
            static (pending, response) =>
            {
                var results = new Dictionary<TopicPartition, AdminMutationResult>();
                if (response.ErrorCode != ErrorCode.None)
                {
                    foreach (var item in pending)
                        results.Add(item.Key, AdminMutationResult.FromResponse(response.ErrorCode, response.ErrorMessage));
                    return results;
                }
                foreach (var topic in response.Responses)
                    foreach (var partition in topic.Partitions)
                        AddMutationResult(results, new(topic.Name, partition.PartitionIndex),
                            AdminMutationResult.FromResponse(partition.ErrorCode, partition.ErrorMessage));
                return results;
            }, cancellationToken);
    }

    internal static List<TKey> SnapshotMutationKeys<TKey>(IEnumerable<TKey> keys, string parameterName, Action<TKey> validate) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(keys, parameterName);
        var result = new List<TKey>();
        var seen = new HashSet<TKey>();
        foreach (var key in keys)
        {
            validate(key);
            if (!seen.Add(key)) throw new ArgumentException("Mutation entities must be distinct.", parameterName);
            result.Add(key);
        }
        return result;
    }

    internal static List<CreateTopicData> SnapshotDetailedTopics(IEnumerable<NewTopic> topics)
    {
        ArgumentNullException.ThrowIfNull(topics);
        var result = new List<CreateTopicData>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var topic in topics)
        {
            ArgumentNullException.ThrowIfNull(topic);
            ArgumentException.ThrowIfNullOrWhiteSpace(topic.Name);
            if (!names.Add(topic.Name)) throw new ArgumentException("Topic names must be distinct.", nameof(topics));
            if (topic.NumPartitions < -1 || topic.NumPartitions == 0)
                throw new ArgumentOutOfRangeException(nameof(topics), "Partition count must be positive or -1 for broker defaults.");
            if (topic.ReplicationFactor < -1 || topic.ReplicationFactor == 0)
                throw new ArgumentOutOfRangeException(nameof(topics), "Replication factor must be positive or -1 for broker defaults.");
            List<CreateTopicAssignment>? assignments = null;
            if (topic.ReplicaAssignments is { } requested)
            {
                assignments = new(requested.Count);
                foreach (var pair in requested)
                {
                    ArgumentOutOfRangeException.ThrowIfNegative(pair.Key);
                    assignments.Add(new() { PartitionIndex = pair.Key, BrokerIds = CopyMutationReplicas(pair.Value) });
                }
            }
            result.Add(new()
            {
                Name = topic.Name,
                NumPartitions = topic.NumPartitions,
                ReplicationFactor = topic.ReplicationFactor,
                Assignments = assignments,
                Configs = topic.Configs?.Select(static pair => new CreateTopicConfig { Name = pair.Key, Value = pair.Value }).ToArray()
            });
        }
        return result;
    }

    internal static List<KeyValuePair<TopicPartition, int[]?>> SnapshotDetailedReassignments(
        IReadOnlyDictionary<TopicPartition, Optional<NewPartitionReassignment>> reassignments)
    {
        ArgumentNullException.ThrowIfNull(reassignments);
        var result = new List<KeyValuePair<TopicPartition, int[]?>>(reassignments.Count);
        foreach (var pair in reassignments)
        {
            ValidateTopicPartition(pair.Key);
            int[]? replicas = null;
            if (pair.Value.HasValue)
            {
                ArgumentNullException.ThrowIfNull(pair.Value.Value);
                var requested = pair.Value.Value.TargetReplicas;
                ArgumentNullException.ThrowIfNull(requested);
                if (requested.Count != 0) replicas = CopyMutationReplicas(requested);
            }
            result.Add(new(pair.Key, replicas));
        }
        return result;
    }

    private static int[] CopyMutationReplicas(IReadOnlyList<int> replicas)
    {
        ArgumentNullException.ThrowIfNull(replicas);
        if (replicas.Count == 0) throw new ArgumentException("Replica assignments cannot be empty.", nameof(replicas));
        var copy = new int[replicas.Count];
        var seen = new HashSet<int>();
        for (var i = 0; i < copy.Length; i++)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(replicas[i]);
            if (!seen.Add(replicas[i])) throw new ArgumentException("Replica IDs must be distinct.", nameof(replicas));
            copy[i] = replicas[i];
        }
        return copy;
    }

    private static List<AlterPartitionReassignmentsRequestTopic> BuildDetailedReassignmentRequest(
        List<KeyValuePair<TopicPartition, int[]?>> items)
    {
        var topics = new Dictionary<string, List<AlterPartitionReassignmentsRequestPartition>>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (!topics.TryGetValue(item.Key.Topic, out var partitions))
                topics.Add(item.Key.Topic, partitions = new());
            partitions.Add(new() { PartitionIndex = item.Key.Partition, Replicas = item.Value });
        }
        var result = new List<AlterPartitionReassignmentsRequestTopic>(topics.Count);
        foreach (var topic in topics) result.Add(new() { Name = topic.Key, Partitions = topic.Value });
        return result;
    }
}
