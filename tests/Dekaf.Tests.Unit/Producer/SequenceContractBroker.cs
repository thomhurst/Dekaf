using Dekaf.Producer;
using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Broker double that enforces the idempotent-producer sequence contract per partition and
/// producer ID, the way <c>ProducerStateManager</c> / <c>ProducerAppendInfo.checkSequence</c> do:
/// <list type="bullet">
/// <item>an epoch older than the partition's entry is fenced (<c>InvalidProducerEpoch</c>);</item>
/// <item>the first batch of a new producer ID or epoch must carry sequence 0;</item>
/// <item>within an epoch, sequences are contiguous and wrap to 0 after <see cref="int.MaxValue"/>;</item>
/// <item>a batch matching one of the last five appended batches is a duplicate: acknowledged
/// with its original offset and not appended again; an older one is answered
/// <c>DuplicateSequenceNumber</c>; anything else is <c>OutOfOrderSequenceNumber</c>.</item>
/// </list>
/// It keeps an append log of record ordinals so a test can assert that no record was appended
/// twice and that every partition's records were appended in order. Thread-safe.
/// </summary>
internal sealed class SequenceContractBroker
{
    private const int DuplicateCacheSize = 5;

    internal readonly record struct AppendedBatch(
        long ProducerId,
        short ProducerEpoch,
        int BaseSequence,
        int RecordCount,
        long BaseOffset,
        long FirstOrdinal);

    private sealed class ProducerEntry
    {
        public short Epoch = -1;
        public int LastSequence = -1;
        public readonly Queue<AppendedBatch> Recent = new(DuplicateCacheSize);
    }

    private sealed class PartitionLog
    {
        public readonly Dictionary<long, ProducerEntry> Producers = [];
        public readonly List<AppendedBatch> Appended = [];
        public long NextOffset;
    }

    // object, not System.Threading.Lock: this project also targets net8.0.
    private readonly object _lock = new();
    private readonly Dictionary<TopicPartition, PartitionLog> _partitions = [];
    private int _deduplicatedBatches;

    /// <summary>Batches acknowledged from the duplicate cache instead of being appended again.</summary>
    public int DeduplicatedBatches => Volatile.Read(ref _deduplicatedBatches);

    /// <summary>
    /// Records that <paramref name="producerId"/> already appended up to
    /// <paramref name="lastSequence"/> under <paramref name="epoch"/>, so a test can start next to
    /// the sequence wrap without producing two billion records.
    /// </summary>
    public void Seed(TopicPartition topicPartition, long producerId, short epoch, int lastSequence)
    {
        lock (_lock)
        {
            var entry = GetEntry(GetPartition(topicPartition), producerId);
            entry.Epoch = epoch;
            entry.LastSequence = lastSequence;
            entry.Recent.Clear();
        }
    }

    /// <summary>
    /// Applies one record batch. <paramref name="firstOrdinal"/> identifies the batch's first
    /// record in the test's own per-partition numbering.
    /// </summary>
    public (ErrorCode ErrorCode, long BaseOffset) Append(
        TopicPartition topicPartition,
        long producerId,
        short epoch,
        int baseSequence,
        int recordCount,
        long firstOrdinal)
    {
        lock (_lock)
        {
            var partition = GetPartition(topicPartition);
            var entry = GetEntry(partition, producerId);

            if (epoch < entry.Epoch)
                return (ErrorCode.InvalidProducerEpoch, -1);

            if (epoch > entry.Epoch)
            {
                if (baseSequence != 0)
                    return (ErrorCode.OutOfOrderSequenceNumber, -1);

                entry.Epoch = epoch;
                entry.Recent.Clear();
                return (ErrorCode.None, AppendLocked(partition, entry, producerId, epoch, baseSequence, recordCount, firstOrdinal));
            }

            var lastSequence = LastSequenceOf(baseSequence, recordCount);
            foreach (var recent in entry.Recent)
            {
                if (recent.BaseSequence == baseSequence
                    && LastSequenceOf(recent.BaseSequence, recent.RecordCount) == lastSequence)
                {
                    _deduplicatedBatches++;
                    return (ErrorCode.None, recent.BaseOffset);
                }
            }

            if (baseSequence == NextSequenceAfter(entry.LastSequence))
                return (ErrorCode.None, AppendLocked(partition, entry, producerId, epoch, baseSequence, recordCount, firstOrdinal));

            // Entirely at or before the last appended sequence, but no longer cached.
            return IsAtOrBefore(lastSequence, entry.LastSequence)
                ? (ErrorCode.DuplicateSequenceNumber, -1)
                : (ErrorCode.OutOfOrderSequenceNumber, -1);
        }
    }

    /// <summary>
    /// True when the log already holds a batch with this stamp. A broker never answers such a
    /// batch with a sequence error, so a fault plan must not inject one for it.
    /// </summary>
    public bool Holds(TopicPartition topicPartition, long producerId, short epoch, int baseSequence, int recordCount)
    {
        lock (_lock)
        {
            if (!_partitions.TryGetValue(topicPartition, out var partition))
                return false;

            foreach (var appended in partition.Appended)
            {
                if (appended.ProducerId == producerId
                    && appended.ProducerEpoch == epoch
                    && appended.BaseSequence == baseSequence
                    && appended.RecordCount == recordCount)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Snapshot of what <paramref name="topicPartition"/>'s log holds, in append order.</summary>
    public AppendedBatch[] GetLog(TopicPartition topicPartition)
    {
        lock (_lock)
            return _partitions.TryGetValue(topicPartition, out var partition) ? [.. partition.Appended] : [];
    }

    private static long AppendLocked(
        PartitionLog partition,
        ProducerEntry entry,
        long producerId,
        short epoch,
        int baseSequence,
        int recordCount,
        long firstOrdinal)
    {
        var baseOffset = partition.NextOffset;
        partition.NextOffset += recordCount;
        var appended = new AppendedBatch(producerId, epoch, baseSequence, recordCount, baseOffset, firstOrdinal);
        partition.Appended.Add(appended);
        entry.LastSequence = LastSequenceOf(baseSequence, recordCount);
        if (entry.Recent.Count == DuplicateCacheSize)
            entry.Recent.Dequeue();
        entry.Recent.Enqueue(appended);
        return baseOffset;
    }

    private PartitionLog GetPartition(TopicPartition topicPartition)
    {
        if (!_partitions.TryGetValue(topicPartition, out var partition))
            _partitions[topicPartition] = partition = new PartitionLog();
        return partition;
    }

    private static ProducerEntry GetEntry(PartitionLog partition, long producerId)
    {
        if (!partition.Producers.TryGetValue(producerId, out var entry))
            partition.Producers[producerId] = entry = new ProducerEntry();
        return entry;
    }

    // Java DefaultRecordBatch.incrementSequence: the sequence space is [0, int.MaxValue].
    private static int LastSequenceOf(int baseSequence, int recordCount)
        => unchecked(baseSequence + recordCount - 1) & int.MaxValue;

    private static int NextSequenceAfter(int lastSequence)
        => lastSequence < 0 ? 0 : unchecked(lastSequence + 1) & int.MaxValue;

    private static bool IsAtOrBefore(int sequence, int other)
        => (unchecked(other - sequence) & int.MaxValue) < (1 << 30);
}
