using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Retry;

namespace Dekaf.Testing;

public sealed partial class InMemoryAdminClient : IDetailedTopicMutationAdminClient
{
    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> CreateTopicsDetailedAsync(
        IEnumerable<NewTopic> topics, CreateTopicsOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var items = AdminClient.SnapshotDetailedTopics(topics);
        return ExecuteInMemoryMutationAsync(items, static item => item.Name, static item => (item.Name, null),
            item => _cluster.CreateTopicDetailed(item, options?.ValidateOnly ?? false), options?.TimeoutMs ?? 30000, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> DeleteTopicsDetailedAsync(
        IEnumerable<string> topicNames, DeleteTopicsOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var names = AdminClient.SnapshotMutationKeys(topicNames, nameof(topicNames), static name => ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(topicNames)));
        return ExecuteInMemoryMutationAsync(names, static name => name, static name => (name, null),
            name => _cluster.DeleteTopic(name) ? ErrorCode.None : ErrorCode.UnknownTopicOrPartition,
            options?.TimeoutMs ?? 30000, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<Guid, AdminMutationResult>> DeleteTopicsDetailedAsync(
        IEnumerable<Guid> topicIds, DeleteTopicsOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ids = AdminClient.SnapshotMutationKeys(topicIds, nameof(topicIds), static id =>
        {
            if (id == Guid.Empty) throw new ArgumentException("Topic IDs cannot contain the empty UUID.", nameof(topicIds));
        });
        return ExecuteInMemoryMutationAsync(ids, static id => id, id => (_cluster.GetTopicName(id), null),
            id => _cluster.DeleteTopic(id) ? ErrorCode.None : ErrorCode.UnknownTopicId,
            options?.TimeoutMs ?? 30000, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> CreatePartitionsDetailedAsync(
        IReadOnlyDictionary<string, int> newPartitionCounts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(newPartitionCounts);
        cancellationToken.ThrowIfCancellationRequested();
        var topics = AdminClient.BuildPartitionCountTopics(newPartitionCounts);
        return CreatePartitionsDetailedAsync(topics, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> CreatePartitionsDetailedAsync(
        IReadOnlyDictionary<string, NewPartitions> newPartitions, CreatePartitionsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(newPartitions);
        cancellationToken.ThrowIfCancellationRequested();
        var topics = AdminClient.BuildPartitionExpansionTopics(newPartitions);
        return ExecuteInMemoryMutationAsync(topics, static topic => topic.Name, static topic => (topic.Name, null),
            topic =>
            {
                _cluster.CreatePartitions(topic, options?.ValidateOnly ?? false);
                return ErrorCode.None;
            }, options?.TimeoutMs ?? 30000, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<TopicPartition, AdminMutationResult>> AlterPartitionReassignmentsDetailedAsync(
        IReadOnlyDictionary<TopicPartition, Optional<NewPartitionReassignment>> reassignments,
        AlterPartitionReassignmentsOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var items = AdminClient.SnapshotDetailedReassignments(reassignments);
        return ExecuteInMemoryMutationAsync(items, static item => item.Key,
            static item => (item.Key.Topic, (int?)item.Key.Partition),
            item => _cluster.AlterPartitionReassignmentDetailed(item.Key, item.Value),
            options?.TimeoutMs ?? 60000, cancellationToken);
    }

    private async ValueTask<IReadOnlyDictionary<TKey, AdminMutationResult>> ExecuteInMemoryMutationAsync<TItem, TKey>(
        List<TItem> items, Func<TItem, TKey> key, Func<TItem, (string? Topic, int? Partition)> scope,
        Func<TItem, ErrorCode> apply, int timeoutMs, CancellationToken cancellationToken,
        string? shareGroupId = null, bool allowMissingShareGroup = false, Func<TItem, string?>? getGroupId = null,
        Func<TItem, CancellationToken, ValueTask>? applyFault = null) where TKey : notnull
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(timeoutMs);
        var results = new Dictionary<TKey, AdminMutationResult>(items.Count);
        if (items.Count == 0) return results;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeoutMs == 0) deadline.Cancel();
        else deadline.CancelAfter(timeoutMs);
        var token = deadline.Token;
        if (shareGroupId is not null)
        {
            var groupResult = await CheckShareGroupMutationAsync(shareGroupId, allowMissingShareGroup, deadline, timeoutMs, cancellationToken).ConfigureAwait(false);
            if (!groupResult.IsSuccess)
            {
                foreach (var item in items) results.Add(key(item), groupResult);
                return results;
            }
        }
        foreach (var item in items)
        {
            var identifier = key(item);
            for (var attempt = 0; ; attempt++)
            {
                AdminMutationResult result;
                try
                {
                    token.ThrowIfCancellationRequested();
                    if (applyFault is not null)
                        await applyFault(item, token).ConfigureAwait(false);
                    else
                    {
                        var target = scope(item);
                        await ApplyAdminFaultAsync(token, target.Topic, target.Partition, getGroupId?.Invoke(item) ?? shareGroupId).ConfigureAwait(false);
                    }
                    token.ThrowIfCancellationRequested();
                    result = AdminMutationResult.FromResponse(apply(item), null);
                }
                catch (OperationCanceledException) when (attempt > 0 && token.IsCancellationRequested)
                {
                    // Cancellation before applying a retry cannot erase the last confirmed rejection.
                    break;
                }
                catch (Exception exception) when (AdminClient.IsDetailedMutationFailure(exception))
                {
                    var failure = AdminClient.MutationFailure(exception, deadline, timeoutMs, "Detailed admin mutation", cancellationToken);
                    if (exception is KafkaException { ErrorCode: { } code } brokerFailure)
                        result = AdminMutationResult.FromResponse(code, brokerFailure.Message);
                    else
                        result = AdminMutationResult.Unconfirmed(exception is OperationCanceledException
                            ? AdminMutationOutcome.NotAttempted : AdminMutationOutcome.Unknown,
                            "The simulated mutation did not receive a definitive response.", failure);
                }
                results[identifier] = result;
                var canRetry = shareGroupId is null && getGroupId is null ? AdminMutationResult.IsSafeControllerRetry(result) : AdminMutationResult.IsSafeCoordinatorRetry(result);
                if (!canRetry || attempt >= RetryHelper.MaxRetries || token.IsCancellationRequested) break;
                try
                {
                    await Task.Delay(1, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    // The last confirmed rejection survives cancellation during retry backoff.
                    break;
                }
            }
        }
        return results;
    }
}
