using Dekaf.Protocol.Messages;

namespace Dekaf.Admin;

public sealed partial class AdminClient
{
    /// <inheritdoc />
    public async ValueTask CreatePartitionsAsync(
        IReadOnlyDictionary<string, NewPartitions> newPartitions,
        CreatePartitionsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(newPartitions);
        cancellationToken.ThrowIfCancellationRequested();
        var timeoutMs = options?.TimeoutMs ?? 30000;
        ArgumentOutOfRangeException.ThrowIfNegative(timeoutMs);

        // Snapshot caller-owned collections before any await so retries preserve replica order.
        var topics = BuildPartitionExpansionTopics(newPartitions);

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await CreatePartitionsCoreAsync(topics, timeoutMs, options?.ValidateOnly ?? false, cancellationToken).ConfigureAwait(false);
    }

    internal static List<CreatePartitionsTopic> BuildPartitionExpansionTopics(IReadOnlyDictionary<string, NewPartitions> newPartitions)
    {
        var topics = new List<CreatePartitionsTopic>(newPartitions.Count);
        foreach (var pair in newPartitions)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);
            ArgumentNullException.ThrowIfNull(pair.Value);
            ArgumentOutOfRangeException.ThrowIfLessThan(pair.Value.TotalCount, 1);
            topics.Add(new CreatePartitionsTopic
            {
                Name = pair.Key,
                Count = pair.Value.TotalCount,
                Assignments = CopyPartitionAssignments(pair.Value, nameof(newPartitions))
            });
        }

        return topics;
    }

    private static CreatePartitionsAssignment[]? CopyPartitionAssignments(NewPartitions partitions, string parameterName)
    {
        var assignments = partitions.ReplicaAssignments;
        if (assignments is null)
            return null;

        if (assignments.Count == 0 || assignments.Count >= partitions.TotalCount)
            throw new ArgumentException("Assignments must describe only the additional partitions of an existing topic.", parameterName);

        var result = new CreatePartitionsAssignment[assignments.Count];
        var replicationFactor = 0;
        for (var index = 0; index < assignments.Count; index++)
        {
            var replicas = assignments[index];
            if (replicas is null || replicas.Count == 0 || (index > 0 && replicas.Count != replicationFactor))
                throw new ArgumentException("Each assignment must contain the same nonzero number of replicas.", parameterName);

            replicationFactor = replicas.Count;
            var brokerIds = new int[replicas.Count];
            var seen = new HashSet<int>();
            for (var replica = 0; replica < replicas.Count; replica++)
            {
                var brokerId = replicas[replica];
                if (brokerId < 0 || !seen.Add(brokerId))
                    throw new ArgumentException("Replica broker IDs must be nonnegative and unique within a partition.", parameterName);
                brokerIds[replica] = brokerId;
            }
            result[index] = new CreatePartitionsAssignment { BrokerIds = brokerIds };
        }
        return result;
    }

    private static CreatePartitionsTopic GetRequestedPartitionExpansion(IReadOnlyList<CreatePartitionsTopic> topics, string name)
    {
        // Only used while resolving an ambiguous admin mutation, never on a message path.
        for (var index = 0; index < topics.Count; index++)
        {
            if (topics[index].Name == name)
                return topics[index];
        }
        throw new InvalidOperationException($"Unexpected CreatePartitions response topic '{name}'.");
    }

    private static IReadOnlyList<CreatePartitionsTopic> ExcludeConfirmedPartitionExpansions(
        IReadOnlyList<CreatePartitionsTopic> topics,
        IReadOnlyList<CreatePartitionsResponseResult> results,
        string? metadataConfirmedTopic = null)
    {
        // Only allocate on a partial failure, outside the successful admin request path.
        HashSet<string>? confirmed = null;
        if (metadataConfirmedTopic is not null)
            (confirmed = new(StringComparer.Ordinal)).Add(metadataConfirmedTopic);
        foreach (var result in results)
        {
            if (result.ErrorCode == Protocol.ErrorCode.None)
                (confirmed ??= new(StringComparer.Ordinal)).Add(result.Name);
        }
        if (confirmed is null)
            return topics;

        var remaining = new List<CreatePartitionsTopic>(topics.Count);
        foreach (var topic in topics)
        {
            if (!confirmed.Contains(topic.Name))
                remaining.Add(topic);
        }
        return remaining;
    }
}
