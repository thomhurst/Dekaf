using System.Collections.Concurrent;

namespace Dekaf.Consumer;

internal sealed class StuckFetchPositionTracker
{
    private readonly ConcurrentDictionary<TopicPartition, Observation> _observations = new();
    private readonly int _threshold;

    public StuckFetchPositionTracker(int threshold)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(threshold, 1);
        _threshold = threshold;
    }

    public Errors.ConsumeException? ObserveEmptyParsedFetch(TopicPartition partition, long position)
    {
        var observation = _observations.AddOrUpdate(
            partition,
            static (_, position) => new Observation(position, Count: 1),
            static (_, current, position) => current.Position == position
                ? current with { Count = current.Count + 1 }
                : new Observation(position, Count: 1),
            position);

        if (observation.Count < _threshold)
            return null;

        return new Errors.ConsumeException(
            Protocol.ErrorCode.CorruptMessage,
            $"Partition {partition.Topic}[{partition.Partition}] returned record bytes but no parseable batches " +
            $"at fetch offset {position} for {_threshold} consecutive responses.",
            isRetriable: false);
    }

    public void Reset(TopicPartition partition) => _observations.TryRemove(partition, out _);

    public void Clear() => _observations.Clear();

    private readonly record struct Observation(long Position, int Count);
}
