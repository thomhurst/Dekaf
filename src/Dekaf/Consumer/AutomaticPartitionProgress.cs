namespace Dekaf.Consumer;

// Automatic handlers have one completion coordinator. Readers may run concurrently.
// The sequence protects the offset/epoch/count triple without an object per publication.
internal sealed class AutomaticPartitionProgress(TopicPartition partition)
{
    private int _sequence;
    private long _offset = -1;
    private int _leaderEpoch = -1;
    private long _completedRecords;
    private long _lastProcessedOffset = -1;
    private long _completedRecordTotal = -1;

    /// <summary>
    /// Every completed record of a finished out-of-order dispatcher, including completions above
    /// the frontier, or -1 when none reported it. Read only after the processor task completes.
    /// </summary>
    internal long CompletedRecordTotal => Volatile.Read(ref _completedRecordTotal);

    internal void SetCompletedRecordTotal(long records) => Volatile.Write(ref _completedRecordTotal, records);

    internal long? LastProcessedOffset
    {
        get
        {
            var offset = Volatile.Read(ref _lastProcessedOffset);
            return offset >= 0 ? offset : null;
        }
    }

    /// <summary>The published commit offset, or -1 before the first publication.</summary>
    internal long CompletedOffset => Volatile.Read(ref _offset);

    internal void MarkProcessed(long offset)
    {
        if (offset > _lastProcessedOffset)
            Volatile.Write(ref _lastProcessedOffset, offset);
    }

    /// <param name="offset">The commit offset.</param>
    /// <param name="leaderEpoch">The leader epoch of the last completed record.</param>
    /// <param name="completedRecords">How many records the lane delivered below <paramref name="offset"/>; all of them completed.</param>
    internal void Publish(long offset, int leaderEpoch, long completedRecords)
    {
        if (offset <= _offset)
            return;
        Interlocked.Increment(ref _sequence);
        Volatile.Write(ref _leaderEpoch, leaderEpoch);
        Volatile.Write(ref _completedRecords, completedRecords);
        Volatile.Write(ref _offset, offset);
        Interlocked.Increment(ref _sequence);
    }

    internal TopicPartitionOffset? GetCommitOffset(out long completedRecords)
    {
        while (true)
        {
            var sequence = Volatile.Read(ref _sequence);
            if ((sequence & 1) != 0)
                continue;

            var offset = Volatile.Read(ref _offset);
            var epoch = Volatile.Read(ref _leaderEpoch);
            completedRecords = Volatile.Read(ref _completedRecords);
            // Prevent the second sequence read from moving ahead of the snapshot reads.
            if (Interlocked.CompareExchange(ref _sequence, 0, 0) != sequence)
                continue;

            return offset >= 0
                ? new TopicPartitionOffset(partition.Topic, partition.Partition, offset, epoch)
                : null;
        }
    }
}
