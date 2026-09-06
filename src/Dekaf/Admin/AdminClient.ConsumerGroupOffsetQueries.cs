using Dekaf.Protocol;

namespace Dekaf.Admin;

public sealed partial class AdminClient : IConsumerGroupOffsetQueryAdminClient
{
    private static Errors.GroupException ConsumerGroupOffsetQueryError(string groupId, ErrorCode errorCode) =>
        new(errorCode, $"ListConsumerGroupOffsets failed for group '{groupId}': {errorCode}") { GroupId = groupId };

    public ValueTask<IReadOnlyDictionary<string, ConsumerGroupOffsetsResult>> ListConsumerGroupOffsetsAsync(
        IReadOnlyDictionary<string, ListConsumerGroupOffsetsSpec> groupSpecs,
        ListConsumerGroupOffsetsOptions? options = null,
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

        var opts = options ?? new ListConsumerGroupOffsetsOptions();
        ArgumentOutOfRangeException.ThrowIfNegative(opts.TimeoutMs);
        if (requests.Count == 0)
            return new(new Dictionary<string, ConsumerGroupOffsetsResult>(StringComparer.Ordinal));

        return ExecuteWithTimeoutAsync(
            token => ListConsumerGroupOffsetDetailsCoreAsync(requests, opts.RequireStable, token),
            opts.TimeoutMs,
            nameof(ListConsumerGroupOffsetsAsync),
            cancellationToken);
    }

    private async ValueTask<IReadOnlyDictionary<string, ConsumerGroupOffsetsResult>> ListConsumerGroupOffsetDetailsCoreAsync(
        Dictionary<string, IReadOnlyList<TopicPartition>?> requests,
        bool requireStable,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, ConsumerGroupOffsetsResult>(requests.Count, StringComparer.Ordinal);
        var pending = requests;
        while (pending.Count != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Both group types use OffsetFetch. Reuse coordinator batching, destination
            // negotiation, topic-ID mapping, and per-group/partition error handling.
            IReadOnlyDictionary<string, StreamsGroupOffsetsResult> fetched;
            try
            {
                fetched = await ListStreamsGroupOffsetsCoreAsync(
                    pending, requireStable, cancellationToken, deferUnstableOffsets: requireStable).ConfigureAwait(false);
            }
            catch (Exception exception) when (cancellationToken.IsCancellationRequested &&
                Retry.RetryHelper.IsRetriableRequestFailure(exception))
            {
                // Cancellation may race the final retriable broker response. Preserve the
                // caller's deadline/cancellation outcome instead of leaking that response.
                throw new OperationCanceledException(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            Dictionary<string, IReadOnlyList<TopicPartition>?>? unstable = null;
            foreach (var (groupId, result) in fetched)
            {
                if (requireStable && HasUnstableOffsets(result))
                {
                    unstable ??= new(StringComparer.Ordinal);
                    unstable.Add(groupId, pending[groupId]);
                }
                else
                    results.Add(groupId, ConsumerGroupOffsetsResult.FromStreamsResult(result));
            }
            if (unstable is null)
                break;

            // A transaction can outlive the normal request retry count. Keep its group
            // pending until the caller's total budget expires; completed groups stay complete.
            await Task.Delay(Math.Max(1, _options.RetryBackoffMs), cancellationToken).ConfigureAwait(false);
            pending = unstable;
        }

        var ordered = new Dictionary<string, ConsumerGroupOffsetsResult>(requests.Count, StringComparer.Ordinal);
        foreach (var groupId in requests.Keys)
            ordered.Add(groupId, results[groupId]);
        return ordered;
    }

    private static bool HasUnstableOffsets(StreamsGroupOffsetsResult result)
    {
        if (result.ErrorCode == ErrorCode.UnstableOffsetCommit)
            return true;
        foreach (var offset in result.Offsets.Values)
        {
            if (offset.ErrorCode == ErrorCode.UnstableOffsetCommit)
                return true;
        }
        return false;
    }
}
