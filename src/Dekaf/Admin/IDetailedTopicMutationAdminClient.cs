namespace Dekaf.Admin;

/// <summary>Optional capability for per-entity topic and partition mutation outcomes.</summary>
/// <remarks>
/// Every requested entity has a result. Broker success confirms acceptance, not completion of
/// leader election or replica movement. A missing response or ambiguous send is never success
/// and is not automatically replayed. Confirmed controller/quota rejections may be retried;
/// successful and terminal results are retained. TimeoutMs bounds discovery, sends and retries.
/// Cancellation before invocation throws. Cancellation during execution returns the confirmed
/// results, unknown sent outcomes, and any not-attempted entities. Inspect every outcome before
/// retrying. Existing convenience methods retain their existing exception and retry behavior.
/// </remarks>
public interface IDetailedTopicMutationAdminClient
{
    ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> CreateTopicsDetailedAsync(
        IEnumerable<NewTopic> topics, CreateTopicsOptions? options = null, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> DeleteTopicsDetailedAsync(
        IEnumerable<string> topicNames, DeleteTopicsOptions? options = null, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyDictionary<Guid, AdminMutationResult>> DeleteTopicsDetailedAsync(
        IEnumerable<Guid> topicIds, DeleteTopicsOptions? options = null, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> CreatePartitionsDetailedAsync(
        IReadOnlyDictionary<string, int> newPartitionCounts, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> CreatePartitionsDetailedAsync(
        IReadOnlyDictionary<string, NewPartitions> newPartitions, CreatePartitionsOptions? options = null,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyDictionary<TopicPartition, AdminMutationResult>> AlterPartitionReassignmentsDetailedAsync(
        IReadOnlyDictionary<TopicPartition, Optional<NewPartitionReassignment>> reassignments,
        AlterPartitionReassignmentsOptions? options = null, CancellationToken cancellationToken = default);
}

/// <summary>Detailed mutation operations for compatible <see cref="IAdminClient"/> implementations.</summary>
public static class AdminClientDetailedTopicMutationExtensions
{
    public static ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> CreateTopicsDetailedAsync(
        this IAdminClient adminClient, IEnumerable<NewTopic> topics, CreateTopicsOptions? options = null,
        CancellationToken cancellationToken = default) =>
        GetCapability(adminClient).CreateTopicsDetailedAsync(topics, options, cancellationToken);

    public static ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> DeleteTopicsDetailedAsync(
        this IAdminClient adminClient, IEnumerable<string> topicNames, DeleteTopicsOptions? options = null,
        CancellationToken cancellationToken = default) =>
        GetCapability(adminClient).DeleteTopicsDetailedAsync(topicNames, options, cancellationToken);

    public static ValueTask<IReadOnlyDictionary<Guid, AdminMutationResult>> DeleteTopicsDetailedAsync(
        this IAdminClient adminClient, IEnumerable<Guid> topicIds, DeleteTopicsOptions? options = null,
        CancellationToken cancellationToken = default) =>
        GetCapability(adminClient).DeleteTopicsDetailedAsync(topicIds, options, cancellationToken);

    public static ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> CreatePartitionsDetailedAsync(
        this IAdminClient adminClient, IReadOnlyDictionary<string, int> newPartitionCounts,
        CancellationToken cancellationToken = default) =>
        GetCapability(adminClient).CreatePartitionsDetailedAsync(newPartitionCounts, cancellationToken);

    public static ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> CreatePartitionsDetailedAsync(
        this IAdminClient adminClient, IReadOnlyDictionary<string, NewPartitions> newPartitions,
        CreatePartitionsOptions? options = null, CancellationToken cancellationToken = default) =>
        GetCapability(adminClient).CreatePartitionsDetailedAsync(newPartitions, options, cancellationToken);

    public static ValueTask<IReadOnlyDictionary<TopicPartition, AdminMutationResult>> AlterPartitionReassignmentsDetailedAsync(
        this IAdminClient adminClient, IReadOnlyDictionary<TopicPartition, Optional<NewPartitionReassignment>> reassignments,
        AlterPartitionReassignmentsOptions? options = null, CancellationToken cancellationToken = default) =>
        GetCapability(adminClient).AlterPartitionReassignmentsDetailedAsync(reassignments, options, cancellationToken);

    private static IDetailedTopicMutationAdminClient GetCapability(IAdminClient adminClient)
    {
        ArgumentNullException.ThrowIfNull(adminClient);
        return adminClient as IDetailedTopicMutationAdminClient
            ?? throw new NotSupportedException($"Admin client type '{adminClient.GetType().FullName}' does not support detailed topic mutations.");
    }
}
