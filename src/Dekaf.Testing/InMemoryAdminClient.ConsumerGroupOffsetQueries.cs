using Dekaf.Admin;
using Dekaf.Protocol;

namespace Dekaf.Testing;

public sealed partial class InMemoryAdminClient : IConsumerGroupOffsetQueryAdminClient
{
    public async ValueTask<IReadOnlyDictionary<string, ConsumerGroupOffsetsResult>> ListConsumerGroupOffsetsAsync(
        IReadOnlyDictionary<string, ListConsumerGroupOffsetsSpec> groupSpecs,
        ListConsumerGroupOffsetsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupSpecs);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        var requests = ValidateGroupOffsetQueries(groupSpecs, static spec => spec.TopicPartitions);
        var opts = options ?? new ListConsumerGroupOffsetsOptions();
        ArgumentOutOfRangeException.ThrowIfNegative(opts.TimeoutMs);
        if (requests.Length == 0)
            return new Dictionary<string, ConsumerGroupOffsetsResult>(StringComparer.Ordinal);

        return await ExecuteWithTimeoutAsync(async token =>
        {
            var fetched = await ReadGroupOffsetSnapshotsAsync(requests, opts.RequireStable, token).ConfigureAwait(false);
            var results = new Dictionary<string, ConsumerGroupOffsetsResult>(requests.Length, StringComparer.Ordinal);
            foreach (var (groupId, _) in requests)
                results.Add(groupId, ConsumerGroupOffsetsResult.FromStreamsResult(fetched[groupId]));
            token.ThrowIfCancellationRequested();
            return results;
        }, opts.TimeoutMs, nameof(ListConsumerGroupOffsetsAsync), cancellationToken).ConfigureAwait(false);
    }

    private static (string GroupId, TopicPartition[]? Partitions)[] ValidateGroupOffsetQueries<TSpec>(
        IReadOnlyDictionary<string, TSpec> groupSpecs,
        Func<TSpec, IReadOnlyList<TopicPartition>?> selectPartitions)
        where TSpec : class
    {
        var requests = new (string GroupId, TopicPartition[]? Partitions)[groupSpecs.Count];
        var index = 0;
        foreach (var (groupId, spec) in groupSpecs)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
            ArgumentNullException.ThrowIfNull(spec);
            var partitions = selectPartitions(spec)?.ToArray();
            if (partitions is not null)
            {
                var unique = new HashSet<TopicPartition>();
                foreach (var partition in partitions)
                {
                    ValidateTopicPartition(partition);
                    if (!unique.Add(partition))
                        throw new ArgumentException($"Partition '{partition.Topic}-{partition.Partition}' is duplicated.", nameof(groupSpecs));
                }
            }
            requests[index++] = (groupId, partitions);
        }
        return requests;
    }

    private async ValueTask<IReadOnlyDictionary<string, StreamsGroupOffsetsResult>> ReadGroupOffsetSnapshotsAsync(
        (string GroupId, TopicPartition[]? Partitions)[] requests,
        bool requireStable,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, StreamsGroupOffsetsResult>(requests.Length, StringComparer.Ordinal);
        List<(string GroupId, TopicPartition[]? Partitions)>? pending = null;
        Task? changed = null;
        if (requests.Length == 0)
            await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);

        foreach (var (groupId, selectedPartitions) in requests)
        {
            if (selectedPartitions is { Length: > 0 })
            {
                foreach (var partition in selectedPartitions)
                    await ApplyAdminFaultAsync(cancellationToken, partition.Topic, partition.Partition, groupId).ConfigureAwait(false);
            }
            else
                await ApplyAdminFaultAsync(cancellationToken, groupId: groupId).ConfigureAwait(false);

            if (requireStable)
            {
                if (!_cluster.TryGetStableGroupOffsetDetails(groupId, selectedPartitions, out var stableOffsets, out var signal))
                {
                    pending ??= [];
                    pending.Add((groupId, selectedPartitions));
                    changed ??= signal;
                    continue;
                }
                results.Add(groupId, CreateGroupOffsetsResult(groupId, selectedPartitions, stableOffsets));
            }
            else
                results.Add(groupId, CreateGroupOffsetsResult(groupId, selectedPartitions, _cluster.GetGroupOffsetDetails(groupId)));
        }

        if (pending is not null)
        {
            while (pending.Count != 0)
            {
                await changed!.WaitAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                changed = null;
                var remaining = 0;
                for (var i = 0; i < pending.Count; i++)
                {
                    var request = pending[i];
                    if (_cluster.TryGetStableGroupOffsetDetails(request.GroupId, request.Partitions, out var offsets, out var signal))
                        results.Add(request.GroupId, CreateGroupOffsetsResult(request.GroupId, request.Partitions, offsets));
                    else
                    {
                        pending[remaining++] = request;
                        changed ??= signal;
                    }
                }
                pending.RemoveRange(remaining, pending.Count - remaining);
            }

            // Ready groups retain their first snapshot while other groups wait. Restore
            // request order after unstable groups complete in a different order.
            var ordered = new Dictionary<string, StreamsGroupOffsetsResult>(requests.Length, StringComparer.Ordinal);
            foreach (var (groupId, _) in requests)
                ordered.Add(groupId, results[groupId]);
            results = ordered;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return results;
    }

    private StreamsGroupOffsetsResult CreateGroupOffsetsResult(
        string groupId,
        TopicPartition[]? selectedPartitions,
        Dictionary<TopicPartition, TopicPartitionOffset> storedOffsets)
    {
        var offsets = new Dictionary<TopicPartition, StreamsGroupOffsetDescription>(selectedPartitions?.Length ?? storedOffsets.Count);
        if (selectedPartitions is not null)
        {
            foreach (var partition in selectedPartitions)
            {
                var hasOffset = storedOffsets.TryGetValue(partition, out var offset);
                offsets.Add(partition, CreateGroupOffsetDescription(partition, hasOffset, offset));
            }
        }
        else
        {
            foreach (var (partition, offset) in storedOffsets)
                offsets.Add(partition, CreateGroupOffsetDescription(partition, true, offset));
        }
        return new StreamsGroupOffsetsResult { GroupId = groupId, ErrorCode = ErrorCode.None, Offsets = offsets };
    }

    private StreamsGroupOffsetDescription CreateGroupOffsetDescription(TopicPartition partition, bool hasOffset, TopicPartitionOffset offset)
        => new()
        {
            TopicPartition = partition,
            Offset = hasOffset ? offset.Offset : -1,
            LeaderEpoch = hasOffset ? offset.LeaderEpoch : -1,
            Metadata = hasOffset ? offset.Metadata : null,
            ErrorCode = _cluster.GetTopicPartitionError(partition)
        };
}
