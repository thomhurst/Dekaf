using System.Collections;
using System.Runtime.CompilerServices;
using Dekaf.Serialization;

namespace Dekaf.Consumer
{
    /// <summary>
    /// Represents a raw (undeserialized) record from a batch.
    /// Provides zero-copy <see cref="ReadOnlyMemory{T}"/> access to key/value data
    /// and headers without any deserialization, header copying, interceptor, or tracing overhead.
    /// </summary>
    /// <remarks>
    /// Key, value, header, and header-value memory reference pooled fetch storage. Read or copy
    /// them before advancing the outer <c>ConsumeRawBatchAsync</c> enumerator to another batch.
    /// </remarks>
    public readonly struct ConsumeRawRecord
    {
        private readonly Header[]? _headers;
        private readonly int _headerCount;
        private readonly uint _encodedLeaderEpoch;

        public long Offset { get; }
        public long TimestampMs { get; }
        public TimestampType TimestampType { get; }
        public ReadOnlyMemory<byte> Key { get; }
        public ReadOnlyMemory<byte> Value { get; }
        public bool IsKeyNull { get; }
        public bool IsValueNull { get; }

        /// <summary>
        /// Gets a zero-copy view of the record headers. The view and each header value share
        /// the same pooled-storage lifetime as <see cref="Key"/> and <see cref="Value"/>.
        /// </summary>
        public ReadOnlyMemory<Header> Headers => _headers is null
            ? ReadOnlyMemory<Header>.Empty
            : _headers.AsMemory(0, _headerCount);

        /// <summary>
        /// Gets the partition leader epoch recorded by Kafka, or <see langword="null"/> when unavailable.
        /// </summary>
        public int? LeaderEpoch => _encodedLeaderEpoch == 0
            ? null
            : (int)(_encodedLeaderEpoch - 1);

        internal ConsumeRawRecord(
            long offset, long timestampMs, TimestampType timestampType,
            ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value,
            bool isKeyNull, bool isValueNull,
            Header[]? headers, int headerCount, int leaderEpoch)
        {
            Offset = offset;
            TimestampMs = timestampMs;
            TimestampType = timestampType;
            Key = key;
            Value = value;
            IsKeyNull = isKeyNull;
            IsValueNull = isValueNull;
            _headers = headers;
            _headerCount = headerCount;
            _encodedLeaderEpoch = leaderEpoch >= 0 ? (uint)leaderEpoch + 1 : 0;
        }
    }

    /// <summary>
    /// Represents a batch of raw (undeserialized) records from a single partition fetch response.
    /// Records within the batch are iterated synchronously with absolute minimal overhead:
    /// no deserialization, no header copying, no interceptors, no tracing.
    /// </summary>
    public sealed class ConsumeRawBatch : IEnumerable<ConsumeRawRecord>
    {
        private readonly PendingFetchData _pendingFetchData;
        private readonly BatchIterationGuard _iterationGuard;
        private readonly Action<TopicPartition, long, int>? _storeOffsetOnDelivery;
        private readonly int _maxRecords;
        private long _count;

        internal ConsumeRawBatch(
            PendingFetchData pendingFetchData,
            BatchIterationGuard iterationGuard = default,
            Action<TopicPartition, long, int>? storeOffsetOnDelivery = null,
            int maxRecords = int.MaxValue)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maxRecords, 1);
            _pendingFetchData = pendingFetchData;
            _iterationGuard = iterationGuard;
            _storeOffsetOnDelivery = storeOffsetOnDelivery;
            _maxRecords = maxRecords;
        }

        /// <summary>
        /// The topic this batch was fetched from.
        /// </summary>
        public string Topic => _pendingFetchData.Topic;

        /// <summary>
        /// The partition this batch was fetched from.
        /// </summary>
        public int Partition => _pendingFetchData.PartitionIndex;

        /// <summary>
        /// The topic-partition this batch was fetched from.
        /// </summary>
        public TopicPartition TopicPartition => _pendingFetchData.TopicPartition;

        /// <summary>
        /// Gets the number of messages yielded from this batch after enumeration.
        /// This value is only accurate after the batch has been fully enumerated.
        /// </summary>
        public long Count => _count;

        /// <summary>
        /// Indicates whether this zero-record batch marks the current end offset of its partition.
        /// Emitted only when partition EOF reporting is enabled.
        /// </summary>
        public bool IsPartitionEof => _pendingFetchData.IsPartitionEof;

        /// <summary>
        /// Gets the partition end offset for an EOF batch, or <see langword="null"/> for a data batch.
        /// </summary>
        public long? PartitionEofOffset => _pendingFetchData.PartitionEofOffset;

        /// <summary>
        /// Returns a struct enumerator that avoids boxing allocation.
        /// </summary>
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator<ConsumeRawRecord> IEnumerable<ConsumeRawRecord>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Struct enumerator for zero-allocation foreach over raw batch records.
        /// Each <see cref="MoveNext"/> call advances the underlying <see cref="PendingFetchData"/>
        /// and constructs a <see cref="ConsumeRawRecord"/> with no deserialization.
        /// </summary>
        public struct Enumerator : IEnumerator<ConsumeRawRecord>
        {
            private readonly ConsumeRawBatch _batch;
            private bool _canContinue;
            private int _observedVersion;
            private int _recordsYielded;

            internal Enumerator(ConsumeRawBatch batch)
            {
                _batch = batch;
                _observedVersion = batch._iterationGuard.CapturedVersion;
                _canContinue = batch._iterationGuard.CanStart(
                    batch._pendingFetchData.TopicPartition,
                    ref _observedVersion);
                _recordsYielded = 0;
                Current = default;
            }

            /// <summary>
            /// Gets the current raw record.
            /// </summary>
            public ConsumeRawRecord Current { readonly get; private set; }

            readonly object IEnumerator.Current => Current;

            /// <summary>
            /// Advances to the next record, constructing a <see cref="ConsumeRawRecord"/>
            /// with zero-copy access to key/value bytes. No deserialization is performed.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                PendingFetchData pending = _batch._pendingFetchData;

                if (!_canContinue)
                    return false;

                if (!_batch._iterationGuard.IsCurrent(pending.TopicPartition, ref _observedVersion))
                {
                    _canContinue = false;
                    return false;
                }

                if (_recordsYielded >= _batch._maxRecords)
                {
                    pending.TryBufferNext();
                    _canContinue = false;
                    return false;
                }

                if (!pending.MoveNext())
                {
                    _canContinue = false;
                    return false;
                }

                // `record` references pooled batch storage; all reads complete before
                // control returns to the caller. Do not hold it across MoveNext calls.
                ref readonly Protocol.Records.Record record = ref pending.CurrentRecord;

                long offset = pending.CurrentBaseOffset + record.OffsetDelta;
                long timestampMs = pending.CurrentBaseTimestamp + record.TimestampDelta;

                int messageBytes = (record.IsKeyNull ? 0 : record.Key.Length) +
                                   (record.IsValueNull ? 0 : record.Value.Length);

                Current = new ConsumeRawRecord(
                    offset: offset,
                    timestampMs: timestampMs,
                    timestampType: pending.CurrentTimestampType,
                    key: record.IsKeyNull ? ReadOnlyMemory<byte>.Empty : record.Key,
                    value: record.IsValueNull ? ReadOnlyMemory<byte>.Empty : record.Value,
                    isKeyNull: record.IsKeyNull,
                    isValueNull: record.IsValueNull,
                    headers: record.Headers,
                    headerCount: record.HeaderCount,
                    leaderEpoch: pending.CurrentPartitionLeaderEpoch);

                var iterationStatus = _batch._iterationGuard.GetStatusAfterRead(
                    pending.TopicPartition,
                    ref _observedVersion);
                if (iterationStatus != BatchIterationStatus.Continue)
                {
                    _canContinue = false;
                    if (iterationStatus == BatchIterationStatus.Paused)
                        pending.BufferCurrentForRedelivery();
                    return false;
                }

                pending.TrackConsumed(offset, messageBytes);
                _batch._storeOffsetOnDelivery?.Invoke(
                    pending.TopicPartition,
                    offset + 1,
                    pending.LastYieldedLeaderEpoch);
                _recordsYielded++;
                _batch._count++;

                return true;
            }

            public readonly void Reset() => throw new NotSupportedException();

            public readonly void Dispose() { }
        }
    }
}
