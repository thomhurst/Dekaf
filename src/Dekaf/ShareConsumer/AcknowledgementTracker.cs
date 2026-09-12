using System.Runtime.CompilerServices;

#if NETSTANDARD2_0
using TopicPartitionSet = System.Collections.Generic.IReadOnlyCollection<Dekaf.TopicPartition>;
#else
using TopicPartitionSet = System.Collections.Generic.IReadOnlySet<Dekaf.TopicPartition>;
#endif

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
    // Provisional delivery state, never sent on the wire. Requeued wire outcomes
    // retain their actual type, so close cannot undo an already submitted Accept.
    private const AcknowledgeType ImplicitDelivery = (AcknowledgeType)byte.MaxValue;

    private Dictionary<TopicPartition, PartitionAcknowledgements> _pendingAcks = new();
    private PartitionAcknowledgements? _freePartitions;
    private int _peakPendingPartitions;
    private PollBatchPool? _pollBatchPool;

    private sealed class PollBatchPool(int capacity)
    {
        internal readonly Dictionary<TopicPartition, List<AcknowledgementBatchData>> Batches = new(capacity);
        internal readonly List<List<AcknowledgementBatchData>> Spare = [];
        internal bool InUse;
    }

    // Only the callback-free classic fetch path borrows these containers. Request
    // models copy batch descriptors and keep their immutable disposition arrays.
    internal Dictionary<TopicPartition, List<AcknowledgementBatchData>> FlushForPoll() => FlushCore(false, reuseForPoll: true);

    internal void ReturnPollBatches(Dictionary<TopicPartition, List<AcknowledgementBatchData>>? batches)
    {
        var pool = _pollBatchPool;
        if (pool is not { InUse: true } || !ReferenceEquals(batches, pool.Batches))
            return;
        foreach (var list in batches!.Values)
        {
            list.Clear();
            pool.Spare.Add(list);
        }
        batches.Clear();
        pool.InUse = false;
    }

    /// <summary>
    /// Tracks delivered records awaiting implicit acceptance by the next poll or commit.
    /// </summary>
    internal void TrackDeliveredRecords(TopicPartition tp, long firstOffset, long lastOffset)
    {
        GetOrAddPartition(tp).TrackRange(firstOffset, lastOffset, ImplicitDelivery);
    }

    // Subscription changes can relinquish whole acquired ranges without allocating one
    // explicit acknowledgement per undisclosed record. Explicit caller outcomes take priority.
    internal void ReleaseUndeliveredRecords(TopicPartition tp, long firstOffset, long lastOffset)
    {
        GetOrAddPartition(tp).TrackRange(firstOffset, lastOffset, AcknowledgeType.Release);
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

            partitionAcks = RentPartition();
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

    // Called after discarded acquisitions or assignment changes, not on ordinary poll windows.
    internal bool HasPendingOutsideAssignment(TopicPartitionSet assignment)
    {
        foreach (var partition in _pendingAcks)
            if (!assignment.Contains(partition.Key))
                return true;
        return false;
    }

    /// <summary>
    /// Flushes all pending acknowledgements, building wire-format batches.
    /// Returned batches own their arrays independently of the reusable tracking state.
    /// </summary>
    /// <returns>Per-TopicPartition acknowledgement batches for the wire format.</returns>
    // Keep the owned path independently inlineable so short-lived trackers can remain stack-allocated.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Dictionary<TopicPartition, List<AcknowledgementBatchData>> Flush(bool releaseImplicit = false)
    {
        var pending = _pendingAcks;
        var count = pending.Count;
        var reusePending = count >= _peakPendingPartitions / 4;
        if (!reusePending)
        {
            // A smaller assignment must not keep clearing the old peak dictionary
            // capacity on every poll. Drop idle scratch at the same boundary.
            _pendingAcks = new Dictionary<TopicPartition, PartitionAcknowledgements>();
            _freePartitions = null;
            _peakPendingPartitions = count;
            if (_pollBatchPool is not { InUse: true })
                _pollBatchPool = null;
        }
        else if (count > _peakPendingPartitions)
        {
            _peakPendingPartitions = count;
        }

        try
        {
            var result = new Dictionary<TopicPartition, List<AcknowledgementBatchData>>(count);
            foreach (var (tp, partitionAcks) in pending)
            {
                var batches = partitionAcks.BuildBatches(releaseImplicit ? AcknowledgeType.Release : AcknowledgeType.Accept);
                if (batches.Count > 0)
                    result[tp] = batches;

                partitionAcks.Reset();
                partitionAcks.Next = _freePartitions;
                _freePartitions = partitionAcks;
            }
            return result;
        }
        finally
        {
            // Flush is synchronous and single-threaded. Detach even when materialization
            // throws, preserving the previous swap's consumption of the pending state.
            if (reusePending)
                pending.Clear();
        }
    }

    private Dictionary<TopicPartition, List<AcknowledgementBatchData>> FlushCore(bool releaseImplicit, bool reuseForPoll)
    {
        var pending = _pendingAcks;
        var count = pending.Count;
        var reusePending = count >= _peakPendingPartitions / 4;
        if (!reusePending)
        {
            // A smaller assignment must not keep clearing the old peak dictionary
            // capacity on every poll. Drop idle scratch at the same boundary.
            _pendingAcks = new Dictionary<TopicPartition, PartitionAcknowledgements>();
            _freePartitions = null;
            _peakPendingPartitions = count;
            if (_pollBatchPool is not { InUse: true })
                _pollBatchPool = null;
        }
        else if (count > _peakPendingPartitions)
        {
            _peakPendingPartitions = count;
        }

        try
        {
            reuseForPoll &= _pollBatchPool is not { InUse: true };
            PollBatchPool? pool = null;
            if (reuseForPoll)
            {
                pool = _pollBatchPool ??= new(count);
                pool.InUse = true;
            }
            var result = pool?.Batches ?? new Dictionary<TopicPartition, List<AcknowledgementBatchData>>(count);
            foreach (var (tp, partitionAcks) in pending)
            {
                List<AcknowledgementBatchData>? scratch = null;
                if (pool is not null && pool.Spare.Count != 0)
                {
                    var last = pool.Spare.Count - 1;
                    scratch = pool.Spare[last];
                    pool.Spare.RemoveAt(last);
                }
                var batches = partitionAcks.BuildBatches(releaseImplicit ? AcknowledgeType.Release : AcknowledgeType.Accept, scratch);
                if (batches.Count > 0)
                    result[tp] = batches;

                partitionAcks.Reset();
                partitionAcks.Next = _freePartitions;
                _freePartitions = partitionAcks;
            }
            return result;
        }
        catch
        {
            if (reuseForPoll)
                ReturnPollBatches(_pollBatchPool?.Batches);
            throw;
        }
        finally
        {
            // Flush is synchronous and single-threaded. Detach even when materialization
            // throws, preserving the previous swap's consumption of the pending state.
            if (reusePending)
                pending.Clear();
        }
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

                for (var i = 0; i < batch.OffsetCount; i++)
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

                    var type = (AcknowledgeType)batch.GetAcknowledgeType(i);
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
#if NET6_0_OR_GREATER
        ref var partitionAcks = ref System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(_pendingAcks, tp, out _);
        return partitionAcks ??= RentPartition();
#else
        if (_pendingAcks.TryGetValue(tp, out var partitionAcks))
            return partitionAcks;

        partitionAcks = RentPartition();
        _pendingAcks[tp] = partitionAcks;
        return partitionAcks;
#endif
    }

    private PartitionAcknowledgements RentPartition()
    {
        var partition = _freePartitions;
        if (partition is null)
            return new PartitionAcknowledgements();
        _freePartitions = partition.Next;
        partition.Next = null;
        return partition;
    }

    private sealed class PartitionAcknowledgements
    {
        private readonly List<AckRange> _ranges = [];
        private Dictionary<long, AcknowledgeType>? _explicitAcks;
        internal PartitionAcknowledgements? Next;

        internal void Reset()
        {
            _ranges.Clear();
            // Explicit dictionaries can be large and need not follow a pooled state to
            // another partition. Retain only the range scratch storage between flushes.
            _explicitAcks = null;
        }

        internal void TrackRange(long firstOffset, long lastOffset, AcknowledgeType type)
        {
            if (lastOffset < firstOffset)
                return;

            var incoming = new AckRange(firstOffset, lastOffset, type);

            if (_ranges.Count == 0)
            {
                _ranges.Add(incoming);
                return;
            }

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

        internal List<AcknowledgementBatchData> BuildBatches(AcknowledgeType implicitDisposition, List<AcknowledgementBatchData>? scratch = null)
        {
            var batches = scratch ?? new List<AcknowledgementBatchData>(_ranges.Count);

            foreach (var range in _ranges)
                batches.Add(BuildRangeBatch(range, implicitDisposition));

            if (_explicitAcks is not null)
                AddStandaloneExplicitBatches(batches);

            if (batches.Count <= 1)
                return batches;

            batches.Sort(static (left, right) => left.FirstOffset.CompareTo(right.FirstOffset));
            return MergeConsecutiveBatches(batches);
        }

        private AcknowledgementBatchData BuildRangeBatch(AckRange range, AcknowledgeType implicitDisposition)
        {
            var length = checked((int)(range.LastOffset - range.FirstOffset + 1));
            // Kafka accepts one disposition for a whole range. Undisclosed acquisitions
            // need no per-offset storage; explicit outcomes still use the mixed path.
            if (range.AcknowledgeType == AcknowledgeType.Release && _explicitAcks is null)
                return new(range.FirstOffset, range.LastOffset, AcknowledgementBatchData.ReleaseTypes);
            var acknowledgeTypes = new byte[length];
            Array.Fill(acknowledgeTypes, (byte)(range.AcknowledgeType == ImplicitDelivery
                ? implicitDisposition
                : range.AcknowledgeType));

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
            var writeIndex = 0;
            var current = batches[0];

            for (var i = 1; i < batches.Count; i++)
            {
                var next = batches[i];
                if (current.LastOffset + 1 != next.FirstOffset)
                {
                    batches[writeIndex++] = current;
                    current = next;
                    continue;
                }

                var combinedTypes = new byte[checked(current.OffsetCount + next.OffsetCount)];
                current.CopyTypesTo(combinedTypes.AsSpan(0, current.OffsetCount));
                next.CopyTypesTo(combinedTypes.AsSpan(current.OffsetCount));
                current = new AcknowledgementBatchData(current.FirstOffset, next.LastOffset, combinedTypes);
            }

            batches[writeIndex++] = current;
            batches.RemoveRange(writeIndex, batches.Count - writeIndex);
            return batches;
        }
    }

    private readonly record struct AckRange(long FirstOffset, long LastOffset, AcknowledgeType AcknowledgeType);
}

/// <summary>
/// Wire-format acknowledgement batch data ready for serialization.
/// </summary>
internal readonly record struct AcknowledgementBatchData(long FirstOffset, long LastOffset, byte[] AcknowledgeTypes)
{
    internal static readonly byte[] ReleaseTypes = [(byte)AcknowledgeType.Release];
    private static readonly IReadOnlyList<byte> ReadOnlyReleaseTypes = System.Array.AsReadOnly(ReleaseTypes);
    internal IReadOnlyList<byte> PublicAcknowledgeTypes => ReferenceEquals(AcknowledgeTypes, ReleaseTypes)
        ? ReadOnlyReleaseTypes : AcknowledgeTypes;
    internal int OffsetCount => AcknowledgeTypes.Length == 1
        ? checked((int)(LastOffset - FirstOffset + 1)) : AcknowledgeTypes.Length;
    internal byte GetAcknowledgeType(int index) => AcknowledgeTypes.Length == 1 ? AcknowledgeTypes[0] : AcknowledgeTypes[index];

    internal void CopyTypesTo(Span<byte> destination)
    {
        if (AcknowledgeTypes.Length == 1)
            destination.Fill(AcknowledgeTypes[0]);
        else
            AcknowledgeTypes.AsSpan().CopyTo(destination);
    }
}
