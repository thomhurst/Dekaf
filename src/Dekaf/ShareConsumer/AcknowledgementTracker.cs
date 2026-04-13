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
    private Dictionary<TopicPartition, List<(long Offset, AcknowledgeType Type)>> _pendingAcks = new();

    /// <summary>
    /// Tracks records delivered by a poll. All records default to Accept.
    /// KIP-932 guarantees records within a partition arrive in offset order,
    /// so appending is O(1) amortized.
    /// </summary>
    internal void TrackDeliveredRecords(TopicPartition tp, long firstOffset, long lastOffset)
    {
        if (!_pendingAcks.TryGetValue(tp, out var offsets))
        {
            offsets = new List<(long Offset, AcknowledgeType Type)>();
            _pendingAcks[tp] = offsets;
        }

        for (long offset = firstOffset; offset <= lastOffset; offset++)
        {
            offsets.Add((offset, AcknowledgeType.Accept));
        }
    }

    /// <summary>
    /// Sets an explicit acknowledgement type for a specific record.
    /// Throws if the record was not delivered by the current poll.
    /// </summary>
    internal void Acknowledge(TopicPartition tp, long offset, AcknowledgeType type)
    {
        if (!_pendingAcks.TryGetValue(tp, out var offsets))
        {
            throw new InvalidOperationException(
                $"Cannot acknowledge offset {offset} for {tp} — record was not delivered by the current poll.");
        }

        // Linear scan — acceptable since Acknowledge is user-driven, not hot path.
        var idx = offsets.FindLastIndex(e => e.Offset == offset);
        if (idx < 0)
        {
            throw new InvalidOperationException(
                $"Cannot acknowledge offset {offset} for {tp} — record was not delivered by the current poll.");
        }

        offsets[idx] = (offset, type);
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
        // Swap to a fresh dictionary so any new acks after this point go into a fresh
        // bucket — avoids the per-partition TryRemove race of the snapshot-and-remove pattern.
        var old = _pendingAcks;
        _pendingAcks = new Dictionary<TopicPartition, List<(long Offset, AcknowledgeType Type)>>();

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
    /// Re-queues previously flushed acknowledgement data back into the tracker.
    /// Used when CommitAsync partially fails — the failed partitions' acks are
    /// restored so the next commit can retry them.
    /// </summary>
    internal void RequeueAcks(Dictionary<TopicPartition, List<AcknowledgementBatchData>> acks)
    {
        foreach (var (tp, batches) in acks)
        {
            if (!_pendingAcks.TryGetValue(tp, out var offsets))
            {
                offsets = new List<(long Offset, AcknowledgeType Type)>();
                _pendingAcks[tp] = offsets;
            }

            foreach (var batch in batches)
            {
                for (int i = 0; i < batch.AcknowledgeTypes.Length; i++)
                {
                    var offset = batch.FirstOffset + i;
                    // TryAdd: preserve any explicit acknowledgement the user set after the flush.
                    // A newer Acknowledge() call takes priority over re-queued stale acks
                    // from a failed CommitAsync.
                    if (offsets.FindLastIndex(e => e.Offset == offset) < 0)
                    {
                        offsets.Add((offset, (AcknowledgeType)batch.AcknowledgeTypes[i]));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Builds wire-format acknowledgement batches from an offset list.
    /// Groups consecutive offsets into batches with per-record AcknowledgeTypes arrays.
    /// Input is assumed to be in offset order (guaranteed by KIP-932 delivery order).
    /// </summary>
    private static List<AcknowledgementBatchData> BuildBatches(List<(long Offset, AcknowledgeType Type)> offsets)
    {
        List<AcknowledgementBatchData> batches = [];

        if (offsets.Count == 0)
        {
            return batches;
        }

        long batchStart = offsets[0].Offset;
        long previousOffset = batchStart;
        List<byte> currentTypes = [(byte)offsets[0].Type];

        for (int i = 1; i < offsets.Count; i++)
        {
            long offset = offsets[i].Offset;
            AcknowledgeType type = offsets[i].Type;

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
