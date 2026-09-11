using System.Buffers;
#if NETSTANDARD2_0
using TopicPartitionSet = System.Collections.Generic.IReadOnlyCollection<Dekaf.TopicPartition>;
#else
using TopicPartitionSet = System.Collections.Generic.IReadOnlySet<Dekaf.TopicPartition>;
#endif

namespace Dekaf.ShareConsumer;

/// <summary>
/// Keeps acknowledgement ownership in pooled batch indexes. Caller acknowledgement writes
/// are indexed; merging, retry lookup, and reclamation happen at batch/network boundaries.
/// </summary>
internal sealed class ShareBatchAcknowledgements<TKey, TValue> : IDisposable
{
    private readonly Dictionary<TopicPartition, PartitionIndex> _partitions = new();
    private readonly List<ShareBatchStorage<TKey, TValue>> _storages = new();
    private readonly Action<TopicPartition, long>? _onRenewal;
    private readonly Action? _onReplay;
    private int _pendingStorageCount;
    private bool _disposed;

    internal ShareBatchAcknowledgements(Action<TopicPartition, long>? onRenewal = null, Action? onReplay = null)
    {
        _onRenewal = onRenewal;
        _onReplay = onReplay;
    }
    internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    internal void RecordRenewal(TopicPartition partition, long offset) => _onRenewal?.Invoke(partition, offset);
    internal void RecordReplay() => _onReplay?.Invoke();
    internal int RetainedBatchCount => _storages.Count;

    internal bool HasPending => _pendingStorageCount != 0;

    internal void AddPendingStorage() => _pendingStorageCount++;
    internal void RemovePendingStorage() => _pendingStorageCount--;

    internal void Register(ShareConsumeBatch<TKey, TValue> batch)
    {
        ThrowIfDisposed();
        var storage = batch.Storage;
        if (storage.Tracker is not null)
            throw new InvalidOperationException("The batch is already tracked.");
        if (storage.Count == 0)
            return;
        if (!_partitions.TryGetValue(storage.TopicPartition, out var partition))
        {
            partition = PartitionIndex.Rent(this);
            _partitions.Add(storage.TopicPartition, partition);
        }
        storage.Retain();
        storage.PartitionIndex = partition;
        storage.TrackerIndex = _storages.Count;
        if (storage.PendingCount != 0)
            AddPendingStorage();
        _storages.Add(storage);
        partition.Add(storage);
    }

    internal Dictionary<TopicPartition, List<AcknowledgementBatchData>> Flush(bool releaseImplicit = false)
    {
        ThrowIfDisposed();
        var result = new Dictionary<TopicPartition, List<AcknowledgementBatchData>>();
        foreach (var (partition, index) in _partitions)
        {
            var batches = index.Flush(releaseImplicit);
            if (batches is not null)
                result.Add(partition, batches);
        }
        return result;
    }

    internal void ApplySuccessfulAcknowledgements(Dictionary<TopicPartition, List<AcknowledgementBatchData>>? acknowledgements)
        => Complete(acknowledgements, successful: true);

    internal void RequeueAcknowledgements(Dictionary<TopicPartition, List<AcknowledgementBatchData>>? acknowledgements)
        => Complete(acknowledgements, successful: false);

    private void Complete(Dictionary<TopicPartition, List<AcknowledgementBatchData>>? acknowledgements, bool successful)
    {
        if (acknowledgements is null)
            return;
        foreach (var (partition, batches) in acknowledgements)
        {
            if (!_partitions.TryGetValue(partition, out var index))
                continue;
            foreach (var batch in batches)
                index.Complete(batch, successful);
        }
    }

    // Keep the response boundary available to benchmark fixtures compiled against both
    // revisions. Lease and acknowledgement cleanup no longer needs a deferred sweep.
    internal DeliveryScope BeginDelivery()
    {
        ThrowIfDisposed();
        return default;
    }

    internal readonly struct DeliveryScope : IDisposable
    {
        public void Dispose() { }
    }

    internal void Prune()
    {
        foreach (var partition in _partitions.Values)
            partition.Prune();
    }

    internal List<ShareConsumeBatch<TKey, TValue>>? GetReplays(int maxRecords)
    {
        ThrowIfDisposed();
        List<ShareConsumeBatch<TKey, TValue>>? batches = null;
        foreach (var storage in _storages)
        {
            if (maxRecords <= 0)
                break;
            int[]? indices = null;
            var count = 0;
            for (var index = storage.ReplayStart; index < storage.Count && count < maxRecords; index++)
            {
                ref readonly var entry = ref storage.Entries[index];
                if (!entry.Renewed || entry.Superseded)
                {
                    if (indices is null)
                        storage.ReplayStart = index + 1;
                    continue;
                }
                // Keep the first eligible record until its disposition changes. An
                // unread or unacknowledged replay must remain available next poll.
                indices ??= ArrayPool<int>.Shared.Rent(Math.Min(storage.Count, maxRecords));
                indices[count++] = index;
            }
            if (indices is null)
                continue;
            (batches ??= []).Add(new ShareConsumeBatch<TKey, TValue>(storage, indices, count));
            maxRecords -= count;
        }
        return batches;
    }

    private void ReleaseCompletedStorage(ShareBatchStorage<TKey, TValue> storage)
    {
        var index = storage.TrackerIndex;
        var moved = _storages[^1];
        _storages[index] = moved;
        moved.TrackerIndex = index;
        _storages.RemoveAt(_storages.Count - 1);
        if (storage.PendingCount != 0)
        {
            RemovePendingStorage();
            storage.PartitionIndex!.RemovePending(storage.PendingCount);
        }
        storage.PartitionIndex = null;
        storage.Release();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Clear();
    }

    internal void RemoveOutsideAssignment(TopicPartitionSet assignment)
    {
        List<TopicPartition>? removed = null;
        foreach (var topicPartition in _partitions.Keys)
        {
            if (assignment.Contains(topicPartition))
                continue;
            (removed ??= []).Add(topicPartition);
        }
        if (removed is null)
            return;
        for (var storageIndex = _storages.Count - 1; storageIndex >= 0; storageIndex--)
        {
            var storage = _storages[storageIndex];
            if (assignment.Contains(storage.TopicPartition))
                continue;
            for (var index = 0; index < storage.Count; index++)
                storage.Supersede(index);
            storage.TrackedCount = 0;
            ReleaseCompletedStorage(storage);
        }
        foreach (var topicPartition in removed)
        {
            _partitions[topicPartition].Dispose();
            _partitions.Remove(topicPartition);
        }
    }

    internal void Clear()
    {
        foreach (var storage in _storages)
        {
            for (var index = 0; index < storage.Count; index++)
                storage.Supersede(index);
            storage.TrackedCount = 0;
            storage.PartitionIndex = null;
            storage.Release();
        }
        _storages.Clear();
        foreach (var partition in _partitions.Values)
            partition.Dispose();
        _partitions.Clear();
    }

    internal void ReleaseActiveAcquisitions()
    {
        foreach (var storage in _storages)
            storage.ReleaseActiveAcquisitions();
    }

    private readonly record struct IndexedRecord(long Offset, ShareBatchStorage<TKey, TValue>? Storage, int Index);

    internal sealed class PartitionIndex : IDisposable
    {
        // Index bookkeeping is per assigned partition. Reuse the cleared object across
        // consumer lifetimes; the bounded pool retains neither payloads nor consumer owners.
        private static readonly Reservoir.ObjectPool<PartitionIndex, IndexPolicy> s_pool = new(new(), 128);
        internal ShareBatchAcknowledgements<TKey, TValue> Owner { get; private set; } = null!;
        private IndexedRecord[] _records = [];
        private int _start;
        private int _count;
        private int _liveCount;
        private int _pendingCount;
        private long _firstPending = long.MaxValue;
        private long _lastPending = long.MinValue;

        internal static PartitionIndex Rent(ShareBatchAcknowledgements<TKey, TValue> owner)
        {
            var index = s_pool.Rent();
            index.Owner = owner;
            return index;
        }

        internal void AddPending(long offset)
        {
            _pendingCount++;
            _firstPending = Math.Min(_firstPending, offset);
            _lastPending = Math.Max(_lastPending, offset);
        }

        internal void RemovePending(int count = 1)
        {
            _pendingCount -= count;
            if (_pendingCount != 0)
                return;
            _firstPending = long.MaxValue;
            _lastPending = long.MinValue;
        }

        internal void Add(ShareBatchStorage<TKey, TValue> storage)
        {
            // Reclaim tombstones only when capacity needs them and at least half the
            // index is dead. Repeated small completions cannot trigger full compaction.
            if (_records.Length < (long)_count + storage.Count && _liveCount <= _count / 2)
                Compact();
            var required = checked(_count + storage.Count);
            if (_records.Length < required)
            {
                var larger = ArrayPool<IndexedRecord>.Shared.Rent(required);
                _records.AsSpan(0, _count).CopyTo(larger);
                if (_records.Length != 0)
                    ArrayPool<IndexedRecord>.Shared.Return(_records, clearArray: true);
                _records = larger;
            }

            if (_count == 0 || (storage.Count != 0
                && _records[_count - 1].Offset < storage.BaseOffset + storage.Entries[0].Raw.OffsetDelta))
            {
                // Normal response batches extend the ordered index. Only overlaps need
                // the merge below; copying the entire prefix for each batch is quadratic.
                for (var index = 0; index < storage.Count; index++)
                    _records[_count++] = new IndexedRecord(
                        storage.BaseOffset + storage.Entries[index].Raw.OffsetDelta, storage, index);
                storage.TrackedCount = storage.Count;
                _liveCount += storage.Count;
                RegisterPending(storage);
                return;
            }

            var existing = _count - 1;
            var incoming = storage.Count - 1;
            var destination = required - 1;
            while (existing >= 0 && incoming >= 0)
            {
                var offset = storage.BaseOffset + storage.Entries[incoming].Raw.OffsetDelta;
                var previous = _records[existing];
                if (previous.Offset > offset)
                {
                    _records[destination--] = previous;
                    existing--;
                    continue;
                }
                if (previous.Offset == offset)
                {
                    if (previous.Storage is { } previousStorage)
                    {
                        previousStorage.Supersede(previous.Index);
                        previousStorage.TrackedCount--;
                        _liveCount--;
                        if (previousStorage.TrackedCount == 0)
                            Owner.ReleaseCompletedStorage(previousStorage);
                    }
                    existing--;
                }
                _records[destination--] = new IndexedRecord(offset, storage, incoming--);
            }
            while (incoming >= 0)
            {
                _records[destination--] = new IndexedRecord(
                    storage.BaseOffset + storage.Entries[incoming].Raw.OffsetDelta, storage, incoming);
                incoming--;
            }
            while (existing >= 0)
                _records[destination--] = _records[existing--];

            _count = required - destination - 1;
            _start = 0;
            _records.AsSpan(destination + 1, _count).CopyTo(_records);
            _records.AsSpan(_count, required - _count).Clear();
            storage.TrackedCount = storage.Count;
            _liveCount += storage.Count;
            RegisterPending(storage);
        }

        private void RegisterPending(ShareBatchStorage<TKey, TValue> storage)
        {
            if (storage.PendingCount == 0)
                return;
            for (var index = 0; index < storage.Count; index++)
                if (storage.Entries[index].PendingAcknowledgement != 0)
                    AddPending(storage.BaseOffset + storage.Entries[index].Raw.OffsetDelta);
        }

        internal List<AcknowledgementBatchData>? Flush(bool releaseImplicit)
        {
            if (_pendingCount == 0)
                return null;
            List<AcknowledgementBatchData>? batches = null;
            var index = LowerBound(_firstPending);
            var lastOffset = _lastPending;
            while (index < _count && _records[index].Offset <= lastOffset)
            {
                while (index < _count && _records[index].Offset <= lastOffset && Pending(index) == 0)
                    index++;
                if (index == _count || _records[index].Offset > lastOffset)
                    break;
                var first = index;
                var lastPending = index;
                while (index + 1 < _count && _records[index].Offset != long.MaxValue
                    && _records[index + 1].Offset <= lastOffset
                    && _records[index + 1].Offset == _records[index].Offset + 1)
                {
                    index++;
                    if (Pending(index) != 0)
                        lastPending = index;
                }
                var types = new byte[lastPending - first + 1];
                for (var item = first; item <= lastPending; item++)
                {
                    var type = Pending(item);
                    if (type == 0)
                        continue;
                    if (type == ShareBatchStorage<TKey, TValue>.ImplicitDelivery)
                        type = (byte)(releaseImplicit ? AcknowledgeType.Release : AcknowledgeType.Accept);
                    types[item - first] = type;
                    var record = _records[item];
                    record.Storage!.Submit(record.Index, type, types);
                }
                (batches ??= []).Add(new AcknowledgementBatchData(
                    _records[first].Offset, _records[lastPending].Offset, types));
                index++;
            }
            return batches;
        }

        internal void Complete(AcknowledgementBatchData batch, bool successful)
        {
            var index = LowerBound(batch.FirstOffset);
            while (index < _count && _records[index].Offset <= batch.LastOffset)
            {
                var position = index++;
                var record = _records[position];
                if (record.Storage is null)
                    continue;
                var typeIndex = record.Offset - batch.FirstOffset;
                if ((ulong)typeIndex >= (ulong)batch.AcknowledgeTypes.Length)
                    continue;
                var type = batch.AcknowledgeTypes[typeIndex];
                if (type != 0)
                {
                    record.Storage.Complete(record.Index, type, batch.AcknowledgeTypes, successful);
                    PruneRecord(position);
                }
            }
            AdvanceStart();
        }

        private int LowerBound(long offset)
        {
            var low = _start;
            var high = _count;
            while (low < high)
            {
                var middle = low + ((high - low) >> 1);
                if (_records[middle].Offset < offset)
                    low = middle + 1;
                else
                    high = middle;
            }
            return low;
        }

        private byte Pending(int index)
        {
            var record = _records[index];
            return record.Storage?.Entries[record.Index].PendingAcknowledgement ?? 0;
        }

        internal void PruneClosedLease(ShareBatchStorage<TKey, TValue> storage)
        {
            if (_liveCount == storage.TrackedCount)
            {
                PruneOnlyStorage(storage);
                return;
            }
            var first = LowerBound(storage.BaseOffset + storage.Entries[0].Raw.OffsetDelta);
            var lastOffset = storage.BaseOffset + storage.Entries[storage.Count - 1].Raw.OffsetDelta;
            // Any other open lease contains only renewal entries, which retain their
            // renewal/pending/submission state. Original unread entries can be removed now.
            for (var index = first; index < _count && _records[index].Offset <= lastOffset; index++)
                if (ReferenceEquals(_records[index].Storage, storage))
                    PruneRecord(index, originalLeaseClosed: true);
            AdvanceStart();
        }

        private void PruneOnlyStorage(ShareBatchStorage<TKey, TValue> storage)
        {
            // A lone original batch needs one compacting pass and bulk clear, rather
            // than a tombstone write and storage-removal check for every unread record.
            var retained = 0;
            for (var index = _start; index < _count; index++)
            {
                var record = _records[index];
                if (record.Storage is null)
                    continue;
                ref readonly var entry = ref storage.Entries[record.Index];
                if (entry.Settled || entry.Superseded || (entry.PendingAcknowledgement == 0
                    && entry.SubmittedAcknowledgement == 0 && !entry.Renewed))
                    continue;
                _records[retained++] = record;
            }
            _records.AsSpan(retained, _count - retained).Clear();
            _start = 0;
            _count = _liveCount = storage.TrackedCount = retained;
            if (retained == 0)
                Owner.ReleaseCompletedStorage(storage);
        }

        private void PruneRecord(int index, bool originalLeaseClosed = false)
        {
            var record = _records[index];
            if (record.Storage is not { } storage)
                return;
            ref readonly var entry = ref storage.Entries[record.Index];
            if (!entry.Settled && !entry.Superseded &&
                ((!originalLeaseClosed && storage.OpenLeases != 0) || entry.PendingAcknowledgement != 0
                    || entry.SubmittedAcknowledgement != 0 || entry.Renewed))
                return;
            _records[index] = new IndexedRecord(record.Offset, null, 0);
            _liveCount--;
            storage.TrackedCount--;
            if (storage.TrackedCount == 0)
                Owner.ReleaseCompletedStorage(storage);
        }

        private void AdvanceStart()
        {
            while (_start < _count && _records[_start].Storage is null)
                _start++;
            if (_start == _count)
                _start = _count = 0;
        }

        internal void Prune()
        {
            for (var index = _start; index < _count; index++)
                PruneRecord(index);
            Compact();
        }

        private void Compact()
        {
            var retained = 0;
            for (var index = _start; index < _count; index++)
                if (_records[index].Storage is not null)
                    _records[retained++] = _records[index];
            _records.AsSpan(retained, _count - retained).Clear();
            _start = 0;
            _count = retained;
        }

        public void Dispose()
        {
            if (Owner is null)
                return;
            if (_records.Length != 0)
                ArrayPool<IndexedRecord>.Shared.Return(_records, clearArray: true);
            _records = [];
            _count = 0;
            _start = 0;
            _liveCount = 0;
            _pendingCount = 0;
            _firstPending = long.MaxValue;
            _lastPending = long.MinValue;
            Owner = null!;
            s_pool.Return(this);
        }

        private readonly struct IndexPolicy : Reservoir.IPooledObjectPolicy<PartitionIndex>, Reservoir.INonThrowingResetPolicy
        {
            public PartitionIndex Create() => new();
            public bool TryReset(PartitionIndex item) => true;
            public void Destroy(PartitionIndex item) { }
        }
    }
}
