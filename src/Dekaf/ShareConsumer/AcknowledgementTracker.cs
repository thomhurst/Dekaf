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
    private Dictionary<TopicPartition, PartitionAcknowledgements> _pendingAcks = new();

    /// <summary>
    /// Tracks records delivered by a poll. All records default to Accept.
    /// </summary>
    internal void TrackDeliveredRecords(TopicPartition tp, long firstOffset, long lastOffset)
    {
        GetOrAddPartition(tp).TrackRange(firstOffset, lastOffset, AcknowledgeType.Accept);
    }

    /// <summary>
    /// Sets an explicit acknowledgement type for a specific record.
    /// Throws if the record was not delivered by the current poll unless requireTracked is false.
    /// </summary>
    internal void Acknowledge(TopicPartition tp, long offset, AcknowledgeType type, bool requireTracked = true)
    {
        if (!_pendingAcks.TryGetValue(tp, out var partitionAcks))
        {
            if (requireTracked)
            {
                throw new InvalidOperationException(
                    $"Cannot acknowledge offset {offset} for {tp} — record was not delivered by the current poll.");
            }

            partitionAcks = new PartitionAcknowledgements();
            _pendingAcks[tp] = partitionAcks;
        }
        else if (requireTracked && !partitionAcks.ContainsOffset(offset))
        {
            throw new InvalidOperationException(
                $"Cannot acknowledge offset {offset} for {tp} — record was not delivered by the current poll.");
        }

        partitionAcks.SetExplicit(offset, type);
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
        _pendingAcks = new Dictionary<TopicPartition, PartitionAcknowledgements>();

        Dictionary<TopicPartition, List<AcknowledgementBatchData>> result = new();

        foreach (var (tp, partitionAcks) in old)
        {
            var batches = partitionAcks.BuildBatches();
            if (batches.Count > 0)
            {
                result[tp] = batches;
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
            var partitionAcks = GetOrAddPartition(tp);

            foreach (var batch in batches)
            {
                long? runStart = null;
                long runEnd = 0;
                var runType = AcknowledgeType.Accept;

                for (var i = 0; i < batch.AcknowledgeTypes.Length; i++)
                {
                    var offset = batch.FirstOffset + i;
                    // Preserve any acknowledgement tracked after the flush. A newer
                    // Acknowledge() call takes priority over stale re-queued acks
                    // from a failed CommitAsync.
                    if (partitionAcks.ContainsOffset(offset))
                    {
                        if (runStart is not null)
                        {
                            partitionAcks.TrackRange(runStart.Value, runEnd, runType);
                            runStart = null;
                        }

                        continue;
                    }

                    var type = (AcknowledgeType)batch.AcknowledgeTypes[i];
                    if (runStart is not null && type == runType && offset == runEnd + 1)
                    {
                        runEnd = offset;
                        continue;
                    }

                    if (runStart is not null)
                        partitionAcks.TrackRange(runStart.Value, runEnd, runType);

                    runStart = offset;
                    runEnd = offset;
                    runType = type;
                }

                if (runStart is not null)
                    partitionAcks.TrackRange(runStart.Value, runEnd, runType);
            }
        }
    }

    private PartitionAcknowledgements GetOrAddPartition(TopicPartition tp)
    {
        if (_pendingAcks.TryGetValue(tp, out var partitionAcks))
            return partitionAcks;

        partitionAcks = new PartitionAcknowledgements();
        _pendingAcks[tp] = partitionAcks;
        return partitionAcks;
    }

    private sealed class PartitionAcknowledgements
    {
        private readonly List<AckRange> _ranges = [];
        private Dictionary<long, AcknowledgeType>? _explicitAcks;

        internal void TrackRange(long firstOffset, long lastOffset, AcknowledgeType type)
        {
            if (lastOffset < firstOffset)
                return;

            var incoming = new AckRange(firstOffset, lastOffset, type);

            if (_ranges.Count > 0)
            {
                var last = _ranges[^1];
                if (last.AcknowledgeType == type && TouchesOrOverlaps(last, incoming))
                {
                    _ranges[^1] = Merge(last, incoming);
                    return;
                }

                if (firstOffset > last.LastOffset)
                {
                    _ranges.Add(incoming);
                    return;
                }
            }

            InsertRange(incoming);
        }

        private void InsertRange(AckRange incoming)
        {
            var index = 0;
            while (index < _ranges.Count)
            {
                var current = _ranges[index];
                if (current.AcknowledgeType == incoming.AcknowledgeType && TouchesOrOverlaps(current, incoming))
                {
                    incoming = Merge(current, incoming);
                    _ranges.RemoveAt(index);
                    continue;
                }

                if (Overlaps(current, incoming))
                {
                    _ranges.RemoveAt(index);

                    if (current.FirstOffset < incoming.FirstOffset)
                    {
                        _ranges.Insert(
                            index,
                            current with { LastOffset = incoming.FirstOffset - 1 });
                        index++;
                    }

                    if (current.LastOffset > incoming.LastOffset)
                    {
                        _ranges.Insert(
                            index,
                            current with { FirstOffset = incoming.LastOffset + 1 });
                        index++;
                    }

                    continue;
                }

                if (incoming.LastOffset < current.FirstOffset)
                    break;

                index++;
            }

            index = 0;
            while (index < _ranges.Count && _ranges[index].FirstOffset < incoming.FirstOffset)
                index++;

            _ranges.Insert(index, incoming);
        }

        private static AckRange Merge(AckRange left, AckRange right)
        {
            return new AckRange(
                Math.Min(left.FirstOffset, right.FirstOffset),
                Math.Max(left.LastOffset, right.LastOffset),
                left.AcknowledgeType);
        }

        private static bool TouchesOrOverlaps(AckRange left, AckRange right)
        {
            return !EndsBeforeWithGap(left.LastOffset, right.FirstOffset) &&
                   !EndsBeforeWithGap(right.LastOffset, left.FirstOffset);
        }

        private static bool Overlaps(AckRange left, AckRange right)
        {
            return left.FirstOffset <= right.LastOffset &&
                   right.FirstOffset <= left.LastOffset;
        }

        private static bool EndsBeforeWithGap(long lastOffset, long firstOffset)
        {
            return lastOffset < firstOffset && lastOffset + 1 < firstOffset;
        }

        internal bool ContainsOffset(long offset)
        {
            if (_explicitAcks is not null && _explicitAcks.ContainsKey(offset))
                return true;

            return ContainsOffsetInRanges(offset);
        }

        internal void SetExplicit(long offset, AcknowledgeType type)
        {
            _explicitAcks ??= new Dictionary<long, AcknowledgeType>();
            _explicitAcks[offset] = type;
        }

        internal List<AcknowledgementBatchData> BuildBatches()
        {
            List<AcknowledgementBatchData> batches = [];

            foreach (var range in _ranges)
                batches.Add(BuildRangeBatch(range));

            if (_explicitAcks is not null)
                AddStandaloneExplicitBatches(batches);

            if (batches.Count <= 1)
                return batches;

            batches.Sort(static (left, right) => left.FirstOffset.CompareTo(right.FirstOffset));
            return MergeConsecutiveBatches(batches);
        }

        private AcknowledgementBatchData BuildRangeBatch(AckRange range)
        {
            var length = checked((int)(range.LastOffset - range.FirstOffset + 1));
            var acknowledgeTypes = new byte[length];
            Array.Fill(acknowledgeTypes, (byte)range.AcknowledgeType);

            if (_explicitAcks is not null)
            {
                foreach (var (offset, type) in _explicitAcks)
                {
                    if (offset >= range.FirstOffset && offset <= range.LastOffset)
                        acknowledgeTypes[offset - range.FirstOffset] = (byte)type;
                }
            }

            return new AcknowledgementBatchData(range.FirstOffset, range.LastOffset, acknowledgeTypes);
        }

        private void AddStandaloneExplicitBatches(List<AcknowledgementBatchData> batches)
        {
            if (_explicitAcks is null)
                return;

            List<KeyValuePair<long, AcknowledgeType>>? standaloneAcks = null;
            foreach (var kvp in _explicitAcks)
            {
                if (ContainsOffsetInRanges(kvp.Key))
                    continue;

                standaloneAcks ??= [];
                standaloneAcks.Add(kvp);
            }

            if (standaloneAcks is null)
                return;

            standaloneAcks.Sort(static (left, right) => left.Key.CompareTo(right.Key));

            var runStart = standaloneAcks[0].Key;
            var previousOffset = runStart;
            List<byte> runTypes = [(byte)standaloneAcks[0].Value];

            for (var i = 1; i < standaloneAcks.Count; i++)
            {
                var (offset, type) = standaloneAcks[i];
                if (offset == previousOffset + 1)
                {
                    runTypes.Add((byte)type);
                }
                else
                {
                    batches.Add(new AcknowledgementBatchData(runStart, previousOffset, runTypes.ToArray()));
                    runStart = offset;
                    runTypes = [(byte)type];
                }

                previousOffset = offset;
            }

            batches.Add(new AcknowledgementBatchData(runStart, previousOffset, runTypes.ToArray()));
        }

        private bool ContainsOffsetInRanges(long offset)
        {
            foreach (var range in _ranges)
            {
                if (offset >= range.FirstOffset && offset <= range.LastOffset)
                    return true;
            }

            return false;
        }

        private static List<AcknowledgementBatchData> MergeConsecutiveBatches(List<AcknowledgementBatchData> batches)
        {
            List<AcknowledgementBatchData> merged = [];
            var current = batches[0];

            for (var i = 1; i < batches.Count; i++)
            {
                var next = batches[i];
                if (current.LastOffset + 1 != next.FirstOffset)
                {
                    merged.Add(current);
                    current = next;
                    continue;
                }

                var combinedTypes = new byte[current.AcknowledgeTypes.Length + next.AcknowledgeTypes.Length];
                Array.Copy(current.AcknowledgeTypes, combinedTypes, current.AcknowledgeTypes.Length);
                Array.Copy(next.AcknowledgeTypes, 0, combinedTypes, current.AcknowledgeTypes.Length, next.AcknowledgeTypes.Length);
                current = new AcknowledgementBatchData(current.FirstOffset, next.LastOffset, combinedTypes);
            }

            merged.Add(current);
            return merged;
        }
    }

    private readonly record struct AckRange(long FirstOffset, long LastOffset, AcknowledgeType AcknowledgeType);
}

/// <summary>
/// Wire-format acknowledgement batch data ready for serialization.
/// </summary>
internal readonly record struct AcknowledgementBatchData(long FirstOffset, long LastOffset, byte[] AcknowledgeTypes);
