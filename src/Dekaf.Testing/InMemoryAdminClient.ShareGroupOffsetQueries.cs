using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;

namespace Dekaf.Testing;

public sealed partial class InMemoryAdminClient : IShareGroupOffsetQueryAdminClient
{
    public ValueTask<IReadOnlyDictionary<string, ShareGroupOffsetsResult>> ListShareGroupOffsetsAsync(
        IReadOnlyDictionary<string, ListShareGroupOffsetsSpec> groupSpecs,
        ListShareGroupOffsetsOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupSpecs);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        var requests = ValidateGroupOffsetQueries(groupSpecs, static spec => spec.TopicPartitions);
        var opts = options ?? new ListShareGroupOffsetsOptions();
        ArgumentOutOfRangeException.ThrowIfNegative(opts.TimeoutMs);
        AdminClient.ThrowIfShareOffsetQueryDeadlineExpired(opts.TimeoutMs);
        return ExecuteWithTimeoutAsync<IReadOnlyDictionary<string, ShareGroupOffsetsResult>>(async token =>
        {
            var results = new Dictionary<string, ShareGroupOffsetsResult>(requests.Length, StringComparer.Ordinal);
            foreach (var (groupId, selected) in requests)
            {
                if (selected is { Length: 0 })
                {
                    results.Add(groupId, AdminClient.ShareGroupOffsetsError(groupId, ErrorCode.None));
                    continue;
                }
                try
                {
                    await ApplyAdminFaultAsync(token, groupId: groupId).ConfigureAwait(false);
                    var stored = _cluster.GetShareGroupOffsetSnapshot(groupId);
                    var targets = selected ?? stored.Keys.ToArray();
                    var offsets = new Dictionary<TopicPartition, ShareGroupOffsetDescription>(targets.Length);
                    foreach (var partition in targets)
                    {
                        ErrorCode error;
                        string? message = null;
                        try
                        {
                            await ApplyAdminFaultAsync(token, partition.Topic, partition.Partition, groupId).ConfigureAwait(false);
                            error = _cluster.GetTopicPartitionError(partition);
                            if (error == ErrorCode.UnknownTopicId)
                                error = ErrorCode.UnknownTopicOrPartition;
                        }
                        catch (KafkaException exception) when (!token.IsCancellationRequested)
                        {
                            error = exception.ErrorCode ?? ErrorCode.UnknownServerError;
                            message = exception.Message;
                        }
                        var offset = stored.TryGetValue(partition, out var checkpoint) ? checkpoint : (TopicPartitionOffset?)null;
                        offsets.Add(partition, new()
                        {
                            TopicPartition = partition, ErrorCode = error, ErrorMessage = message,
                            StartOffset = error == ErrorCode.None ? offset?.Offset ?? -1 : -1,
                            LeaderEpoch = error == ErrorCode.None ? offset?.LeaderEpoch ?? -1 : -1,
                            Lag = error == ErrorCode.None && offset is not null
                                ? Math.Max(0, _cluster.GetWatermarks(partition).High - offset.Value.Offset) : -1
                        });
                    }
                    results.Add(groupId, new() { GroupId = groupId, Offsets = offsets });
                }
                catch (KafkaException exception) when (!token.IsCancellationRequested)
                {
                    results.Add(groupId, AdminClient.ShareGroupOffsetsError(groupId,
                        exception.ErrorCode ?? ErrorCode.UnknownServerError, exception.Message));
                }
            }
            token.ThrowIfCancellationRequested();
            return results;
        }, opts.TimeoutMs, nameof(ListShareGroupOffsetsAsync), cancellationToken);
    }
}
