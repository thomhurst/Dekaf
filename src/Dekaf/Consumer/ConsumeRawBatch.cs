using System.Collections;
using System.Runtime.CompilerServices;

namespace Dekaf.Consumer
{
    /// <summary>
    /// Represents a raw (undeserialized) record from a batch.
    /// Provides zero-copy <see cref="ReadOnlyMemory{T}"/> access to key/value data
    /// without any deserialization, header copying, interceptor, or tracing overhead.
    /// </summary>
    public readonly struct ConsumeRawRecord
    {
        public long Offset { get; }
        public long TimestampMs { get; }
        public ReadOnlyMemory<byte> Key { get; }
        public ReadOnlyMemory<byte> Value { get; }
        public bool IsKeyNull { get; }
        public bool IsValueNull { get; }

        internal ConsumeRawRecord(
            long offset, long timestampMs,
            ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value,
            bool isKeyNull, bool isValueNull)
        {
            Offset = offset;
            TimestampMs = timestampMs;
            Key = key;
            Value = value;
            IsKeyNull = isKeyNull;
            IsValueNull = isValueNull;
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

        internal ConsumeRawBatch(PendingFetchData pendingFetchData)
        {
            _pendingFetchData = pendingFetchData;
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
        public long Count => _pendingFetchData.MessageCount;

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

            internal Enumerator(ConsumeRawBatch batch)
            {
                _batch = batch;
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

                if (!pending.MoveNext())
                {
                    return false;
                }

                Protocol.Records.Record record = pending.CurrentRecord;

                long offset = pending.CurrentBaseOffset + record.OffsetDelta;
                long timestampMs = pending.CurrentBaseTimestamp + record.TimestampDelta;

                int messageBytes = (record.IsKeyNull ? 0 : record.Key.Length) +
                                   (record.IsValueNull ? 0 : record.Value.Length);

                Current = new ConsumeRawRecord(
                    offset: offset,
                    timestampMs: timestampMs,
                    key: record.IsKeyNull ? ReadOnlyMemory<byte>.Empty : record.Key,
                    value: record.IsValueNull ? ReadOnlyMemory<byte>.Empty : record.Value,
                    isKeyNull: record.IsKeyNull,
                    isValueNull: record.IsValueNull);

                pending.TrackConsumed(offset, messageBytes);

                return true;
            }

            /// <summary>
            /// Reset is not supported.
            /// </summary>
            public readonly void Reset()
            {
                throw new NotSupportedException();
            }

            /// <summary>
            /// No resources to dispose.
            /// </summary>
            public readonly void Dispose()
            {
            }
        }
    }
}
