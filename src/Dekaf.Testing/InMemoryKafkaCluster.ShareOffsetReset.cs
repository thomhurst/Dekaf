using Dekaf.Consumer;
using Dekaf.ShareConsumer;

namespace Dekaf.Testing;

public sealed partial class InMemoryKafkaCluster
{
    private readonly Dictionary<string, InMemoryShareConsumerOptions> _shareOffsetResetOptions = new(StringComparer.Ordinal);

    internal static void ValidateShareOffsetReset(InMemoryShareConsumerOptions options) =>
        ShareAutoOffsetResetStrategy.GetConfigValue(new ShareConsumerOptions
        {
            BootstrapServers = [],
            GroupId = options.GroupId,
            AutoOffsetReset = options.AutoOffsetReset,
            AutoOffsetResetDuration = options.AutoOffsetResetDuration
        });

    internal void ConfigureShareOffsetReset(InMemoryShareConsumerOptions options)
    {
        if (options.AutoOffsetReset is null)
            return;

        lock (_gate)
            _shareOffsetResetOptions[options.GroupId] = options;
    }

    // Called under _gate only after validating the member registration. Even an empty first fetch
    // establishes the delivery window, so subsequent polls and members must reuse this offset.
    private long GetOrInitializeShareOffsetUnderLock(string groupId, TopicPartition partition)
    {
        if (_shareGroupOffsets.TryGetValue(groupId, out var offsets) &&
            offsets.TryGetValue(partition, out var existing))
            return existing.Offset;

        var isolationLevel = GetShareIsolationUnderLock(groupId);
        var watermarks = GetWatermarks(partition, isolationLevel);
        _shareOffsetResetOptions.TryGetValue(groupId, out var options);
        var offset = watermarks.High;
        switch (options?.AutoOffsetReset)
        {
            case AutoOffsetReset.Earliest:
                offset = watermarks.Low;
                break;
            case AutoOffsetReset.ByDuration:
                var timestamp = AutoOffsetResetStrategy.GetByDurationTimestamp(
                    options.AutoOffsetResetDuration, DateTimeOffset.UtcNow);
                var timestampOffset = GetOffsetForTimestamp(partition, timestamp, isolationLevel);
                if (timestampOffset >= 0)
                    offset = timestampOffset;
                break;
        }

        if (offsets is null)
            _shareGroupOffsets[groupId] = offsets = [];
        offsets[partition] = new TopicPartitionOffset(partition.Topic, partition.Partition, offset);
        return offset;
    }
}
