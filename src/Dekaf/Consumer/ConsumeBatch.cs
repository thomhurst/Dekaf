using System.Collections;
using System.Runtime.CompilerServices;
using Dekaf.Serialization;

namespace Dekaf.Consumer
{
    /// <summary>
    /// Represents a batch of consume results from a single partition fetch response.
    /// Records within the batch are iterated synchronously, eliminating async state machine
    /// overhead per message. Typically contains ~1000 messages per batch.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    public sealed class ConsumeBatch<TKey, TValue> : IEnumerable<ConsumeResult<TKey, TValue>>
    {
        private readonly PendingFetchData _pendingFetchData;
        private readonly IDeserializer<TKey>? _keyDeserializer;
        private readonly IDeserializer<TValue>? _valueDeserializer;

        internal ConsumeBatch(PendingFetchData pendingFetchData,
            IDeserializer<TKey>? keyDeserializer,
            IDeserializer<TValue>? valueDeserializer)
        {
            _pendingFetchData = pendingFetchData;
            _keyDeserializer = keyDeserializer;
            _valueDeserializer = valueDeserializer;
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

        IEnumerator<ConsumeResult<TKey, TValue>> IEnumerable<ConsumeResult<TKey, TValue>>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Struct enumerator for zero-allocation foreach over batch records.
        /// Each <see cref="MoveNext"/> call advances the underlying <see cref="PendingFetchData"/>
        /// and constructs a <see cref="ConsumeResult{TKey, TValue}"/> with eager deserialization.
        /// </summary>
        public struct Enumerator : IEnumerator<ConsumeResult<TKey, TValue>>
        {
            private readonly ConsumeBatch<TKey, TValue> _batch;

            internal Enumerator(ConsumeBatch<TKey, TValue> batch)
            {
                _batch = batch;
                Current = default!;
            }

            /// <summary>
            /// Gets the current consume result.
            /// </summary>
            public ConsumeResult<TKey, TValue> Current { readonly get; private set; }

            readonly object IEnumerator.Current => Current;

            /// <summary>
            /// Advances to the next record, constructing a <see cref="ConsumeResult{TKey, TValue}"/>
            /// with eager deserialization. This mirrors the per-record logic in
            /// <c>KafkaConsumer.ConsumeAsync</c>.
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

                IReadOnlyList<Header>? headers = null;
                if (record.Headers is not null && record.HeaderCount > 0)
                {
                    headers = record.Headers[..record.HeaderCount];
                }

                TimestampType timestampType = pending.CurrentTimestampType;

                int messageBytes = (record.IsKeyNull ? 0 : record.Key.Length) +
                                   (record.IsValueNull ? 0 : record.Value.Length);

                Current = new ConsumeResult<TKey, TValue>(
                    topic: pending.Topic,
                    partition: pending.PartitionIndex,
                    offset: offset,
                    keyData: record.Key,
                    isKeyNull: record.IsKeyNull,
                    valueData: record.Value,
                    isValueNull: record.IsValueNull,
                    headers: headers,
                    timestampMs: timestampMs,
                    timestampType: timestampType,
                    leaderEpoch: null,
                    keyDeserializer: _batch._keyDeserializer,
                    valueDeserializer: _batch._valueDeserializer);

                pending.TrackConsumed(offset, messageBytes);

                return true;
            }

            public readonly void Reset() => throw new NotSupportedException();

            public readonly void Dispose() { }
        }
    }
}
