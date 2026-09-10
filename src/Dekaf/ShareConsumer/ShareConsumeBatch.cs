using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;

namespace Dekaf.ShareConsumer;

/// <summary>A header whose UTF-8 key and value borrow the enclosing batch's storage.</summary>
/// <remarks>Read or copy both fields before disposing the batch or advancing its polling iterator.</remarks>
public readonly record struct ShareBatchHeader(
    ReadOnlyMemory<byte> KeyUtf8, ReadOnlyMemory<byte> Value, bool IsValueNull);

/// <summary>A synchronous, allocation-free view of a batch record's headers.</summary>
/// <remarks>The view, its enumerators, and their memory slices share the enclosing batch's lifetime.</remarks>
public readonly struct ShareBatchHeaders
{
    private readonly ReadOnlyMemory<byte> _bytes;
    /// <summary>Gets the number of headers, including duplicate keys.</summary>
    public int Count { get; }

    internal ShareBatchHeaders(ReadOnlyMemory<byte> bytes, int count)
    {
        _bytes = bytes;
        Count = count;
    }

    /// <summary>Returns a struct enumerator over headers in wire order.</summary>
    public Enumerator GetEnumerator() => new(_bytes, Count);

    /// <summary>Enumerates borrowed header fields without decoding UTF-8 keys.</summary>
    public struct Enumerator
    {
        private readonly ReadOnlyMemory<byte> _bytes;
        private int _position;
        private int _remaining;
        internal Enumerator(ReadOnlyMemory<byte> bytes, int count)
        {
            _bytes = bytes;
            _remaining = count;
        }
        /// <summary>Gets the current borrowed header.</summary>
        public ShareBatchHeader Current { get; private set; }

        /// <summary>Advances to the next header.</summary>
        public bool MoveNext()
        {
            if (_remaining == 0)
                return false;
            var reader = new KafkaProtocolReader(_bytes[_position..]);
            var key = reader.ReadMemorySlice(reader.ReadVarInt());
            var valueLength = reader.ReadVarInt();
            var value = valueLength < 0 ? ReadOnlyMemory<byte>.Empty : reader.ReadMemorySlice(valueLength);
            Current = new ShareBatchHeader(key, value, valueLength < 0);
            _position += checked((int)reader.Consumed);
            _remaining--;
            return true;
        }
    }
}

/// <summary>A value-type view of an acquired record owned by a share batch.</summary>
/// <remarks>
/// The view is valid until its batch is disposed, its polling iterator advances or is disposed,
/// or the consumer closes or unsubscribes. Copy data needed after that boundary. Deserializers
/// may return objects that borrow key/value memory, so their results share this lifetime unless
/// the deserializer explicitly returns independent storage.
/// </remarks>
public readonly struct ShareBatchRecord<TKey, TValue>
{
    private readonly ShareConsumeBatch<TKey, TValue> batch;
    private readonly int index;
    internal ShareBatchRecord(ShareConsumeBatch<TKey, TValue> batch, int index)
    {
        this.batch = batch;
        this.index = index;
    }
    internal ShareConsumeBatch<TKey, TValue> Batch => batch;
    internal int Index => index;
    /// <summary>Gets the topic name.</summary>
    public string Topic => batch.TopicPartition.Topic;
    /// <summary>Gets the partition number.</summary>
    public int Partition => batch.TopicPartition.Partition;
    /// <summary>Gets the absolute Kafka offset.</summary>
    public long Offset => batch.BaseOffset + batch.GetEntry(index).Raw.OffsetDelta;
    /// <summary>Gets the record timestamp in milliseconds since the Unix epoch.</summary>
    public long TimestampMs => batch.BaseTimestamp + batch.GetEntry(index).Raw.TimestampDelta;
    /// <summary>Gets the broker-reported acquisition delivery count.</summary>
    public int DeliveryCount => batch.GetEntry(index).DeliveryCount;
    /// <summary>Gets the deserialized key.</summary>
    public TKey? Key => batch.GetEntry(index).Key;
    /// <summary>Gets the deserialized value.</summary>
    public TValue Value => batch.GetEntry(index).Value;
    /// <summary>Gets whether the Kafka key is null.</summary>
    public bool IsKeyNull => batch.GetEntry(index).Raw.IsKeyNull;
    /// <summary>Gets whether the Kafka value is null.</summary>
    public bool IsValueNull => batch.GetEntry(index).Raw.IsValueNull;
    /// <summary>Gets the borrowed serialized key bytes; use IsKeyNull to distinguish null from empty.</summary>
    public ReadOnlyMemory<byte> KeyBytes => batch.GetEntry(index).Raw.Key;
    /// <summary>Gets the borrowed serialized value bytes; use IsValueNull to distinguish null from empty.</summary>
    public ReadOnlyMemory<byte> ValueBytes => batch.GetEntry(index).Raw.Value;
    /// <summary>Gets the borrowed headers in wire order without allocating strings.</summary>
    public ShareBatchHeaders Headers
    {
        get
        {
            ref readonly var entry = ref batch.GetEntry(index);
            return new ShareBatchHeaders(entry.Raw.HeaderBytes, entry.Raw.HeaderCount);
        }
    }
}

/// <summary>A borrowed batch of acquired share records with indexed acknowledgement state.</summary>
/// <remarks>
/// Enumerate synchronously with foreach. Enumeration is forward-only across all enumerators;
/// only records actually enumerated count as delivered. Acknowledge records before the lease
/// ends. Advancing or disposing the outer polling iterator, closing the consumer, or calling
/// Unsubscribe disposes this lease. Dispose releases local storage ownership; it does not accept
/// records or send acknowledgements. In implicit mode the next poll or CommitAsync accepts
/// delivered records, while CloseAsync releases implicit dispositions that were never submitted.
/// This type is not thread-safe; synchronize its use with all consumer operations.
/// </remarks>
public sealed class ShareConsumeBatch<TKey, TValue> : IDisposable
{
    private readonly ShareBatchStorage<TKey, TValue> _storage;
    private readonly int[]? _replayIndices;
    private readonly int _count;
    // Keep disposal in the cursor's sign bit. The saved word offsets the storage's
    // tracker index without increasing the allocation of an acquired batch/lease pair.
    private int _nextIndex;
    private bool IsDisposed => _nextIndex < 0;

    internal ShareConsumeBatch(ShareBatchStorage<TKey, TValue> storage)
    {
        _storage = storage;
        _count = storage.Count;
    }

    internal ShareConsumeBatch(ShareBatchStorage<TKey, TValue> storage, int[] replayIndices, int count)
    {
        _storage = storage;
        _replayIndices = replayIndices;
        _count = count;
        storage.OpenLease();
    }
    /// <summary>Gets the topic and partition shared by all records in this batch.</summary>
    public TopicPartition TopicPartition => _storage.TopicPartition;
    internal long BaseOffset => _storage.BaseOffset;
    internal long BaseTimestamp => _storage.BaseTimestamp;
    /// <summary>Gets the number of available records in this lease.</summary>
    public int Count => _count;
    /// <summary>Gets the number of records enumerated from this lease.</summary>
    public int DeliveredCount => _nextIndex & int.MaxValue;
    internal ShareBatchStorage<TKey, TValue> Storage => _storage;
    /// <summary>Returns a struct enumerator that resumes at the next undelivered record.</summary>
    public Enumerator GetEnumerator() => new(this);

    internal ref readonly ShareBatchEntry<TKey, TValue> GetEntry(int index)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return ref _storage.Entries[index];
    }

    /// <summary>Sets a delivered record's disposition without allocating per-record tracking state.</summary>
    /// <remarks>
    /// Renew requires explicit acknowledgement mode and broker ShareFetch/ShareAcknowledge v2.
    /// A successful renewal retains the payload for replay through a new batch lease. Terminal
    /// acknowledgement success, a new acquisition of the same offset, or lost assignment ends
    /// the old acquisition. A newer disposition takes precedence when an older request fails.
    /// </remarks>
    public void Acknowledge(ShareBatchRecord<TKey, TValue> record, AcknowledgeType type = AcknowledgeType.Accept)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (!ReferenceEquals(record.Batch, this))
            throw new ArgumentException("The record does not belong to this batch.", nameof(record));
        _storage.Acknowledge(record.Index, type);
    }

    /// <summary>Ends this lease and invalidates its record views without sending acknowledgements.</summary>
    public void Dispose()
    {
        if (IsDisposed)
            return;
        _nextIndex |= int.MinValue;
        if (_replayIndices is not null)
            ArrayPool<int>.Shared.Return(_replayIndices);
        _storage.CloseLease(isReplay: _replayIndices is not null);
    }

    /// <summary>Enumerates records synchronously and tracks delivery once per acquired record.</summary>
    public struct Enumerator
    {
        private readonly ShareConsumeBatch<TKey, TValue> batch;
        internal Enumerator(ShareConsumeBatch<TKey, TValue> batch) => this.batch = batch;
        /// <summary>Gets the current borrowed record view.</summary>
        public ShareBatchRecord<TKey, TValue> Current { get; private set; }

        /// <summary>Advances to the next record and marks it delivered.</summary>
        public bool MoveNext()
        {
            ObjectDisposedException.ThrowIf(batch.IsDisposed, batch);
            if (batch._nextIndex == batch.Count)
                return false;
            var position = batch._nextIndex++;
            var index = batch._replayIndices is null ? position : batch._replayIndices[position];
            batch._storage.Deliver(index);
            if (batch._replayIndices is not null)
                batch._storage.Tracker?.RecordReplay();
            Current = new ShareBatchRecord<TKey, TValue>(batch, index);
            return true;
        }
    }
}

internal struct ShareBatchEntry<TKey, TValue>
{
    internal ShareBatchRecordData Raw;
    internal TKey? Key;
    internal TValue Value;
    internal int DeliveryCount;
    internal byte PendingAcknowledgement;
    internal bool Delivered;
    internal bool Renewed;
    // The existing wire array is unique to one flush. Reuse it as submission
    // identity instead of retaining only a disposition counter for this offset.
    internal byte[]? SubmittedAcknowledgements;
    internal byte SubmittedAcknowledgement;
    internal bool Settled;
    internal bool Superseded;
}

internal sealed class ShareBatchStorage<TKey, TValue>
{
    internal const byte ImplicitDelivery = byte.MaxValue;
    private readonly RecordBatch _source;
    // The sign bit records explicit mode; the remaining 31 bits hold the replay
    // cursor. Sharing this existing word avoids adding 8 B to each storage object.
    private int _modeAndReplayStart;
    private int _references = 1;
    internal readonly ShareBatchEntry<TKey, TValue>[] Entries;

    internal ShareBatchStorage(TopicPartition partition, RecordBatch source, int capacity, ShareAcknowledgementMode mode)
    {
        TopicPartition = partition;
        _source = source;
        _modeAndReplayStart = mode == ShareAcknowledgementMode.Explicit ? int.MinValue : 0;
        Entries = ArrayPool<ShareBatchEntry<TKey, TValue>>.Shared.Rent(Math.Max(1, capacity));
    }

    internal TopicPartition TopicPartition { get; }
    internal long BaseOffset => _source.BaseOffset;
    internal long BaseTimestamp => _source.BaseTimestamp;
    internal int Count { get; set; }
    internal int PendingCount { get; private set; }
    internal int OpenLeases { get; private set; } = 1;
    internal int TrackedCount { get; set; }
    internal int ReplayStart
    {
        get => _modeAndReplayStart & int.MaxValue;
        set => _modeAndReplayStart = (_modeAndReplayStart & int.MinValue) | value;
    }
    internal int TrackerIndex { get; set; }
    internal ShareBatchAcknowledgements<TKey, TValue>.PartitionIndex? PartitionIndex { get; set; }
    internal ShareBatchAcknowledgements<TKey, TValue>? Tracker => PartitionIndex?.Owner;

    internal void CloseLease(bool isReplay)
    {
        OpenLeases--;
        // Replay entries remain renewed, pending or submitted until acknowledgement
        // completion removes them. Only the original lease can leave unread or
        // unacknowledged entries.
        if (!isReplay && TrackedCount != 0 && PendingCount != TrackedCount)
            PartitionIndex?.PruneClosedLease(this);
        Release();
    }

    internal void OpenLease()
    {
        Retain();
        OpenLeases++;
    }

    internal void Retain() => Interlocked.Increment(ref _references);

    internal void Release()
    {
        if (Interlocked.Decrement(ref _references) != 0)
            return;
        ArrayPool<ShareBatchEntry<TKey, TValue>>.Shared.Return(Entries, clearArray: true);
        _source.DisposeAndReturnUnownedConsumerBatch();
    }

    // Consumer operations are serialized. Notify the tracker only when this batch
    // crosses zero; acknowledgement entries keep their existing allocation-free layout.
    private void AddPending(int index)
    {
        PartitionIndex?.AddPending(BaseOffset + Entries[index].Raw.OffsetDelta);
        if (PendingCount++ == 0)
            Tracker?.AddPendingStorage();
    }

    private void RemovePending()
    {
        PartitionIndex?.RemovePending();
        if (--PendingCount == 0)
            Tracker?.RemovePendingStorage();
    }

    internal void Deliver(int index)
    {
        ref var entry = ref Entries[index];
        if (entry.Delivered)
            return;
        entry.Delivered = true;
        if (_modeAndReplayStart >= 0 && entry.PendingAcknowledgement == 0)
        {
            entry.PendingAcknowledgement = ImplicitDelivery;
            AddPending(index);
        }
    }

    internal void Acknowledge(int index, AcknowledgeType type)
    {
        if (type is < AcknowledgeType.Accept or > AcknowledgeType.Renew)
            throw new ArgumentOutOfRangeException(nameof(type));
        if (type == AcknowledgeType.Renew && _modeAndReplayStart >= 0)
            throw new InvalidOperationException("Renew acknowledgements require explicit acknowledgement mode.");
        ref var entry = ref Entries[index];
        Tracker?.ThrowIfDisposed();
        if (entry.Settled || entry.Superseded)
            throw new InvalidOperationException("The record acquisition is no longer active.");
        if (!entry.Delivered)
            throw new InvalidOperationException("The record has not been delivered.");
        if (entry.PendingAcknowledgement == 0)
            AddPending(index);
        entry.PendingAcknowledgement = (byte)type;
        entry.Renewed = false;
        if (type == AcknowledgeType.Renew)
            Tracker?.RecordRenewal(TopicPartition, BaseOffset + entry.Raw.OffsetDelta);
    }

    internal void Submit(int index, byte type, byte[] acknowledgements)
    {
        ref var entry = ref Entries[index];
        entry.SubmittedAcknowledgements = acknowledgements;
        entry.SubmittedAcknowledgement = type;
        entry.PendingAcknowledgement = 0;
        RemovePending();
    }

    // Unsubscribe releases every tracked acquisition, including records the caller
    // has not enumerated. This scan runs once when leaving the subscription.
    internal void ReleaseActiveAcquisitions()
    {
        for (var index = 0; index < Count; index++)
        {
            ref var entry = ref Entries[index];
            if (entry.Settled || entry.Superseded)
                continue;
            if (entry.PendingAcknowledgement == 0)
                AddPending(index);
            entry.PendingAcknowledgement = (byte)AcknowledgeType.Release;
            entry.Renewed = false;
        }
    }

    internal void Complete(int index, byte type, byte[] acknowledgements, bool successful)
    {
        ref var entry = ref Entries[index];
        if (!ReferenceEquals(entry.SubmittedAcknowledgements, acknowledgements)
            || entry.SubmittedAcknowledgement != type || entry.Superseded)
            return;
        entry.SubmittedAcknowledgement = 0;
        entry.SubmittedAcknowledgements = null;
        if (entry.PendingAcknowledgement != 0)
            return;
        if (!successful)
        {
            entry.PendingAcknowledgement = type;
            AddPending(index);
            return;
        }
        entry.Renewed = type == (byte)AcknowledgeType.Renew;
        entry.Settled = !entry.Renewed;
        if (entry.Renewed && index < ReplayStart)
            ReplayStart = index;
    }

    internal void Supersede(int index)
    {
        ref var entry = ref Entries[index];
        entry.Superseded = true;
        entry.Renewed = false;
        if (entry.PendingAcknowledgement != 0)
        {
            entry.PendingAcknowledgement = 0;
            RemovePending();
        }
        entry.SubmittedAcknowledgement = 0;
        entry.SubmittedAcknowledgements = null;
    }
}
