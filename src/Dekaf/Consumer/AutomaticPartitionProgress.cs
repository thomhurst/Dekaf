namespace Dekaf.Consumer;

// Automatic handlers have one completion coordinator. Readers may run concurrently.
// The sequence protects the offset/epoch pair without an object per publication.
internal sealed class AutomaticPartitionProgress(TopicPartition partition)
{
    private int _sequence;
    private long _offset = -1;
    private int _leaderEpoch = -1;
    private long _lastProcessedOffset = -1;

    internal long? LastProcessedOffset
    {
        get
        {
            var offset = Volatile.Read(ref _lastProcessedOffset);
            return offset >= 0 ? offset : null;
        }
    }

    internal void MarkProcessed(long offset)
    {
        if (offset > _lastProcessedOffset)
            Volatile.Write(ref _lastProcessedOffset, offset);
    }

    internal void Publish(long offset, int leaderEpoch)
    {
        if (offset <= _offset)
            return;
        Interlocked.Increment(ref _sequence);
        Volatile.Write(ref _leaderEpoch, leaderEpoch);
        Volatile.Write(ref _offset, offset);
        Interlocked.Increment(ref _sequence);
    }

    internal TopicPartitionOffset? GetCommitOffset()
    {
        while (true)
        {
            var sequence = Volatile.Read(ref _sequence);
            if ((sequence & 1) != 0)
                continue;

            var offset = Volatile.Read(ref _offset);
            var epoch = Volatile.Read(ref _leaderEpoch);
            // Prevent the second sequence read from moving ahead of the snapshot reads.
            if (Interlocked.CompareExchange(ref _sequence, 0, 0) != sequence)
                continue;

            return offset >= 0
                ? new TopicPartitionOffset(partition.Topic, partition.Partition, offset, epoch)
                : null;
        }
    }
}
