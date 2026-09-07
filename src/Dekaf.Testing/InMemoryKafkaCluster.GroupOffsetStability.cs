using System.Diagnostics.CodeAnalysis;
using Dekaf.Consumer;

namespace Dekaf.Testing;

internal sealed class InMemoryPendingGroupOffsets
{
    public List<TopicPartitionOffset> Offsets { get; } = [];
    public List<ConsumerGroupMetadata> MetadataSnapshots { get; } = [];
}

public sealed partial class InMemoryKafkaCluster
{
    internal void StageConsumerGroupOffsets(
        InMemoryTransactionMarker transactionMarker,
        Dictionary<string, InMemoryPendingGroupOffsets> pendingOffsets,
        string groupId,
        TopicPartitionOffset[] offsets,
        ConsumerGroupMetadata? metadata)
    {
        lock (_gate)
        {
            if (!pendingOffsets.TryGetValue(groupId, out var pending))
            {
                pending = new InMemoryPendingGroupOffsets();
                pendingOffsets.Add(groupId, pending);
            }
            if (metadata is not null)
                pending.MetadataSnapshots.Add(metadata);
            pending.Offsets.AddRange(offsets);
            if (offsets.Length != 0)
            {
                _pendingConsumerGroupOffsets ??= [];
                _pendingConsumerGroupOffsets.TryAdd(transactionMarker, pendingOffsets);
            }
        }
    }

    internal bool TryGetStableGroupOffsetDetails(
        string groupId,
        TopicPartition[]? selectedPartitions,
        [NotNullWhen(true)] out Dictionary<TopicPartition, TopicPartitionOffset>? offsets,
        [NotNullWhen(false)] out Task? changed)
    {
        lock (_gate)
        {
            if (HasPendingConsumerGroupOffsetsUnderLock(groupId, selectedPartitions))
            {
                offsets = null;
                changed = (_groupOffsetsChanged ??= new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
                return false;
            }

            // Snapshot and stability check share the same lock, so a new stage cannot slip
            // between the check and the returned checkpoint.
            offsets = _consumerGroupOffsets.TryGetValue(groupId, out var stored)
                ? new Dictionary<TopicPartition, TopicPartitionOffset>(stored)
                : new Dictionary<TopicPartition, TopicPartitionOffset>();
            changed = null;
            return true;
        }
    }

    private bool HasPendingConsumerGroupOffsetsUnderLock(string groupId, TopicPartition[]? selectedPartitions)
    {
        if (_pendingConsumerGroupOffsets is null || selectedPartitions is { Length: 0 })
            return false;

        // Only administrative stability queries scan pending offsets. Staging reuses the
        // transaction's existing state; completion removes its registration in O(1).
        foreach (var groups in _pendingConsumerGroupOffsets.Values)
        {
            if (!groups.TryGetValue(groupId, out var pending))
                continue;
            if (selectedPartitions is null)
            {
                if (pending.Offsets.Count != 0)
                    return true;
                continue;
            }
            for (var i = 0; i < pending.Offsets.Count; i++)
            {
                var offset = pending.Offsets[i];
                for (var j = 0; j < selectedPartitions.Length; j++)
                {
                    var selected = selectedPartitions[j];
                    if (selected.Partition == offset.Partition && selected.Topic == offset.Topic)
                        return true;
                }
            }
        }
        return false;
    }
}
