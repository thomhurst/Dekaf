namespace Dekaf.ShareConsumer;

/// <summary>
/// Tracks per-record acknowledgement state between polls and builds wire-format
/// acknowledgement batches for ShareFetch (inline acks) or ShareAcknowledge requests.
/// <para>
/// Thread-safety: This class is designed for single-threaded access from the consumer's
/// poll loop. <see cref="TrackDeliveredRecords"/>, <see cref="Acknowledge"/>, and
/// <see cref="Flush"/> must all be called from the same thread (the PollAsync caller).
/// </para>
/// </summary>
internal sealed class AcknowledgementTracker
{
    private Dictionary<TopicPartition, SortedList<long, AcknowledgeType>> _pendingAcks = new();

    /// <summary>
    /// Tracks records delivered by a poll. All records default to Accept.
    /// </summary>
    internal void TrackDeliveredRecords(TopicPartition tp, long firstOffset, long lastOffset)
    {
        if (!_pendingAcks.TryGetValue(tp, out var offsets))
        {
            offsets = new SortedList<long, AcknowledgeType>();
            _pendingAcks[tp] = offsets;
        }

        for (long offset = firstOffset; offset <= lastOffset; offset++)
        {
            offsets[offset] = AcknowledgeType.Accept;
        }
    }

    /// <summary>
    /// Sets an explicit acknowledgement type for a specific record.
    /// </summary>
    internal void Acknowledge(TopicPartition tp, long offset, AcknowledgeType type)
    {
        if (_pendingAcks.TryGetValue(tp, out var offsets))
        {
            if (offsets.ContainsKey(offset))
            {
                offsets[offset] = type;
            }
        }
    }

    /// <summary>
    /// Whether there are any pending acknowledgements.
    /// </summary>
    internal bool HasPending => _pendingAcks.Count > 0;

    /// <summary>
    /// Flushes all pending acknowledgements, building wire-format batches.
    /// Atomically swaps the pending dictionary so no acks can be lost.
    /// </summary>
    /// <returns>Per-TopicPartition acknowledgement batches for the wire format.</returns>
    internal Dictionary<TopicPartition, List<AcknowledgementBatchData>> Flush()
    {
        // Atomic swap — any concurrent reads see the new empty dictionary immediately
        var old = _pendingAcks;
        _pendingAcks = new Dictionary<TopicPartition, SortedList<long, AcknowledgeType>>();

        Dictionary<TopicPartition, List<AcknowledgementBatchData>> result = new();

        foreach (var (tp, offsets) in old)
        {
            if (offsets.Count > 0)
            {
                result[tp] = BuildBatches(offsets);
            }
        }

        return result;
    }

    /// <summary>
    /// Builds wire-format acknowledgement batches from a sorted offset map.
    /// Groups consecutive offsets into batches with per-record AcknowledgeTypes arrays.
    /// </summary>
    private static List<AcknowledgementBatchData> BuildBatches(SortedList<long, AcknowledgeType> offsets)
    {
        List<AcknowledgementBatchData> batches = [];

        if (offsets.Count == 0)
        {
            return batches;
        }

        long batchStart = offsets.Keys[0];
        long previousOffset = batchStart;
        List<byte> currentTypes = [(byte)offsets.Values[0]];

        for (int i = 1; i < offsets.Count; i++)
        {
            long offset = offsets.Keys[i];
            AcknowledgeType type = offsets.Values[i];

            if (offset == previousOffset + 1)
            {
                // Consecutive — extend current batch
                currentTypes.Add((byte)type);
            }
            else
            {
                // Gap — close current batch and start new one
                batches.Add(new AcknowledgementBatchData(batchStart, previousOffset, currentTypes.ToArray()));
                batchStart = offset;
                currentTypes = [(byte)type];
            }

            previousOffset = offset;
        }

        // Close final batch
        batches.Add(new AcknowledgementBatchData(batchStart, previousOffset, currentTypes.ToArray()));

        return batches;
    }
}

/// <summary>
/// Wire-format acknowledgement batch data ready for serialization.
/// </summary>
internal readonly record struct AcknowledgementBatchData(long FirstOffset, long LastOffset, byte[] AcknowledgeTypes);
