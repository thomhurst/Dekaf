using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Retry;

namespace Dekaf.Testing;

public sealed partial class InMemoryAdminClient : IDetailedShareGroupOffsetAdminClient
{
    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<TopicPartition, AdminMutationResult>> AlterShareGroupOffsetsDetailedAsync(
        string groupId, IEnumerable<ShareGroupOffsetAlteration> offsets,
        ShareGroupOffsetMutationOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        cancellationToken.ThrowIfCancellationRequested();
        var items = AdminClient.SnapshotShareGroupOffsetAlterations(offsets);
        return ExecuteInMemoryMutationAsync(items, static item => item.TopicPartition,
            static item => (item.TopicPartition.Topic, (int?)item.TopicPartition.Partition),
            item => _cluster.AlterShareGroupOffsetDetailed(groupId, item), options?.TimeoutMs ?? 30000, cancellationToken, groupId, allowMissingShareGroup: true);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> DeleteShareGroupOffsetsDetailedAsync(
        string groupId, IEnumerable<string> topics,
        ShareGroupOffsetMutationOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        cancellationToken.ThrowIfCancellationRequested();
        var items = AdminClient.SnapshotMutationKeys(topics, nameof(topics), static topic => ArgumentException.ThrowIfNullOrWhiteSpace(topic, nameof(topics)));
        return ExecuteInMemoryMutationAsync(items, static item => item, static item => (item, (int?)null),
            topic => _cluster.DeleteShareGroupOffsetsDetailed(groupId, topic), options?.TimeoutMs ?? 30000, cancellationToken, groupId);
    }

    private async ValueTask<AdminMutationResult> CheckShareGroupMutationAsync(string groupId, bool allowMissing,
        CancellationTokenSource deadline, int timeoutMs, CancellationToken callerToken)
    {
        var token = deadline.Token;
        AdminMutationResult? last = null;
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                await ApplyAdminFaultAsync(token, groupId: groupId).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                return AdminMutationResult.FromResponse(_cluster.GetShareGroupMutationError(groupId, allowMissing), null);
            }
            catch (Exception exception) when (AdminClient.IsDetailedMutationFailure(exception))
            {
                if (exception is OperationCanceledException && last is not null) return last;
                var failure = AdminClient.MutationFailure(exception, deadline, timeoutMs, "Share-group offset mutation", callerToken);
                last = exception is KafkaException { ErrorCode: { } code } brokerFailure
                    ? AdminMutationResult.FromResponse(code, brokerFailure.Message)
                    : AdminMutationResult.Unconfirmed(exception is OperationCanceledException
                        ? AdminMutationOutcome.NotAttempted : AdminMutationOutcome.Unknown,
                        "The simulated group mutation did not receive a definitive response.", failure);
                if (!AdminMutationResult.IsSafeCoordinatorRetry(last) || attempt >= RetryHelper.MaxRetries || token.IsCancellationRequested)
                    return last;
            }
            try { await Task.Delay(1, token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return last; }
        }
    }
}
