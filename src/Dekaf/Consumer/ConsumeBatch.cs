using System.Collections;
using System.Runtime.CompilerServices;
using Dekaf.Errors;
using Dekaf.Serialization;

namespace Dekaf.Consumer
{
    internal sealed class BatchIterationEpoch
    {
        // Seqlock: even versions are stable; odd versions mean assignment/revocation state
        // is being published and must not be adopted by a batch iterator.
        private readonly object _publicationLock = new();
        public int Version;
        // One-read hot-path marker. Cleared only after ConsumeOne applies every change from
        // a stable epoch, so validating one partition cannot hide a change for another.
        public int ConsumeOneDeliveryChangesPending;
        // Set only when a batch iterator stops before reading because its partition paused.
        // Batch completion probes once to distinguish a remaining record from exhaustion.
        public int BatchExhaustionProbePending;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invalidate()
        {
            lock (_publicationLock)
            {
                Volatile.Write(ref ConsumeOneDeliveryChangesPending, 1);
                Interlocked.Add(ref Version, 2);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginPublication()
        {
            Monitor.Enter(_publicationLock);
            Volatile.Write(ref ConsumeOneDeliveryChangesPending, 1);
            // Snapshot delivery can reserve the odd epoch without entering this monitor.
            // Wait until that tiny position-publication section completes, then claim the
            // epoch with CAS so publication cannot overlap it.
            var spin = new SpinWait();
            while (true)
            {
                var version = Volatile.Read(ref Version);
                if ((version & 1) == 0
                    && Interlocked.CompareExchange(ref Version, version + 1, version) == version)
                {
                    return;
                }

                spin.SpinOnce();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EndPublication()
        {
            Interlocked.Increment(ref Version);
            Monitor.Exit(_publicationLock);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryBeginSnapshotDelivery(int expectedVersion) =>
            (expectedVersion & 1) == 0
            && Interlocked.CompareExchange(
                ref Version,
                expectedVersion + 1,
                expectedVersion) == expectedVersion;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int EndSnapshotDelivery(int deliveryVersion)
        {
            var stableVersion = deliveryVersion + 2;
            Volatile.Write(ref Version, stableVersion);
            return stableVersion;
        }

        public int CaptureStableVersion()
        {
            var spin = new SpinWait();
            while (true)
            {
                var version = Volatile.Read(ref Version);
                if ((version & 1) == 0)
                    return version;

                spin.SpinOnce();
            }
        }

        public bool TryAcknowledgeConsumeOneDeliveryChanges(int expectedVersion)
        {
            lock (_publicationLock)
            {
                if (Version != expectedVersion)
                    return false;

                Volatile.Write(ref ConsumeOneDeliveryChangesPending, 0);
                return true;
            }
        }
    }

    internal enum BatchIterationStatus : byte
    {
        Continue,
        Paused,
        Stopped
    }

    internal delegate BatchIterationStatus BatchIterationContinuation(TopicPartition partition);

    internal readonly struct BatchIterationGuard(
        BatchIterationEpoch? epoch,
        int capturedVersion,
        BatchIterationContinuation? getStatus = null)
    {
        public int CapturedVersion => capturedVersion;

        public bool CanStart(TopicPartition partition, ref int observedVersion)
        {
            if (epoch is null)
                return getStatus is null || getStatus(partition) == BatchIterationStatus.Continue;

            if (getStatus is null)
            {
                var currentVersion = Volatile.Read(ref epoch.Version);
                return (currentVersion & 1) == 0 && currentVersion == observedVersion;
            }

            var spin = new SpinWait();
            while (true)
            {
                var currentVersion = Volatile.Read(ref epoch.Version);
                if ((currentVersion & 1) != 0)
                {
                    spin.SpinOnce();
                    continue;
                }

                var status = getStatus(partition);
                if (status != BatchIterationStatus.Continue)
                {
                    if (status == BatchIterationStatus.Paused)
                        Volatile.Write(ref epoch.BatchExhaustionProbePending, 1);
                    return false;
                }

                if (Volatile.Read(ref epoch.Version) == currentVersion)
                {
                    observedVersion = currentVersion;
                    return true;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCurrent(TopicPartition partition, ref int observedVersion)
        {
            if (epoch is null)
                return getStatus is null || getStatus(partition) == BatchIterationStatus.Continue;

            var spin = new SpinWait();
            while (true)
            {
                var currentVersion = Volatile.Read(ref epoch.Version);
                if ((currentVersion & 1) != 0)
                {
                    spin.SpinOnce();
                    continue;
                }

                if (currentVersion == observedVersion)
                    return true;

                var status = getStatus?.Invoke(partition) ?? BatchIterationStatus.Stopped;
                if (status != BatchIterationStatus.Continue)
                {
                    if (status == BatchIterationStatus.Paused)
                        Volatile.Write(ref epoch.BatchExhaustionProbePending, 1);
                    return false;
                }

                if (Volatile.Read(ref epoch.Version) == currentVersion)
                {
                    observedVersion = currentVersion;
                    return true;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BatchIterationStatus GetStatusAfterRead(TopicPartition partition, ref int observedVersion)
        {
            if (epoch is null)
                return getStatus?.Invoke(partition) ?? BatchIterationStatus.Continue;

            var spin = new SpinWait();
            while (true)
            {
                var currentVersion = Volatile.Read(ref epoch.Version);
                if ((currentVersion & 1) != 0)
                {
                    spin.SpinOnce();
                    continue;
                }

                if (currentVersion == observedVersion)
                    return BatchIterationStatus.Continue;

                var status = getStatus?.Invoke(partition) ?? BatchIterationStatus.Stopped;
                if (Volatile.Read(ref epoch.Version) == currentVersion)
                {
                    if (status == BatchIterationStatus.Continue)
                        observedVersion = currentVersion;
                    return status;
                }
            }
        }
    }

    /// <summary>
    /// Represents a batch of consume results from a single partition fetch response.
    /// Records within the batch are iterated synchronously, eliminating async state machine
    /// overhead per message. Typically contains ~1000 messages per batch.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    public sealed class ConsumeBatch<TKey, TValue> : IEnumerable<ConsumeResult<TKey, TValue>>
    {
        private const int MaxRejectedRecordsPerBatch = 64;

        private readonly PendingFetchData _pendingFetchData;
        private readonly IDeserializer<TKey>? _keyDeserializer;
        private readonly IDeserializer<TValue>? _valueDeserializer;
        private readonly bool _hasRecordHeaderDeserializers;
        private readonly RecordHeaderRoutingPlan? _recordHeaderRoutingPlan;
        private readonly BatchIterationGuard _iterationGuard;
        private readonly Action<TopicPartition, long, int>? _storeOffsetOnDelivery;
        private readonly Action<PendingFetchData, long>? _rewindAfterDeliveryFailure;
        private readonly IConsumerRecordFilter? _recordFilter;
        private readonly Func<bool>? _tryRecordPollFast;
        private readonly int _maxRecords;
        private long _count;

        internal ConsumeBatch(PendingFetchData pendingFetchData,
            IDeserializer<TKey>? keyDeserializer,
            IDeserializer<TValue>? valueDeserializer,
            BatchIterationGuard iterationGuard = default,
            Action<TopicPartition, long, int>? storeOffsetOnDelivery = null,
            int maxRecords = int.MaxValue,
            Action<PendingFetchData, long>? rewindAfterDeliveryFailure = null,
            IConsumerRecordFilter? recordFilter = null,
            RecordHeaderRoutingPlan? recordHeaderRoutingPlan = null,
            Func<bool>? tryRecordPollFast = null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maxRecords, 1);
            _pendingFetchData = pendingFetchData;
            _keyDeserializer = keyDeserializer;
            _valueDeserializer = valueDeserializer;
            _iterationGuard = iterationGuard;
            _storeOffsetOnDelivery = storeOffsetOnDelivery;
            _maxRecords = maxRecords;
            _rewindAfterDeliveryFailure = rewindAfterDeliveryFailure;
            _recordFilter = recordFilter;
            _tryRecordPollFast = tryRecordPollFast;
            _recordHeaderRoutingPlan = recordHeaderRoutingPlan
                                       ?? RecordHeaderRoutingPlan.Create(
                                           keyDeserializer,
                                           valueDeserializer);
            _hasRecordHeaderDeserializers = _recordHeaderRoutingPlan is not null;
            if (_recordHeaderRoutingPlan is not null)
                pendingFetchData.ConfigureHeaderRouting(_recordHeaderRoutingPlan);
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowAfterDeserializationFailure(PendingFetchData pending, long offset, Exception exception)
        {
            if (exception is not OperationCanceledException)
            {
                ref readonly var record = ref pending.CurrentRecord;
                exception = ConsumeResult<TKey, TValue>.CreateDeserializationException(
                    ConsumeResult<TKey, TValue>.LastDeserializationOrigin,
                    pending.Topic,
                    pending.PartitionIndex,
                    offset,
                    pending.CurrentBaseTimestamp + record.TimestampDelta,
                    pending.CurrentTimestampType,
                    record.Key,
                    record.IsKeyNull,
                    record.Value,
                    record.IsValueNull,
                    headers: null,
                    record.Headers,
                    record.HeaderCount,
                    exception);
            }

            _rewindAfterDeliveryFailure?.Invoke(pending, offset);
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
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
            private bool _canContinue;
            private int _observedVersion;
            private int _recordsExamined;
            private int _rejectedRecordsExamined;

            internal Enumerator(ConsumeBatch<TKey, TValue> batch)
            {
                _batch = batch;
                _observedVersion = batch._iterationGuard.CapturedVersion;
                _canContinue = batch._iterationGuard.CanStart(
                    batch._pendingFetchData.TopicPartition,
                    ref _observedVersion);
                _recordsExamined = 0;
                _rejectedRecordsExamined = 0;
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

                if (!_canContinue)
                    return false;

                return _batch._recordFilter is null && !_batch._hasRecordHeaderDeserializers
                    ? MoveNextFast(pending)
                    : MoveNextFilteredOrRouted(pending);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool MoveNextFast(PendingFetchData pending)
            {
                if (!_batch._iterationGuard.IsCurrent(pending.TopicPartition, ref _observedVersion))
                {
                    _canContinue = false;
                    return false;
                }

                if (_recordsExamined >= _batch._maxRecords)
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

                // `record` references pooled batch storage; every read below happens in
                // constructor-argument evaluation, before the ctor body runs user
                // deserializers. Do not touch `record` after the constructor call.
                ref readonly Protocol.Records.Record record = ref pending.CurrentRecord;

                long offset = pending.CurrentBaseOffset + record.OffsetDelta;
                long timestampMs = pending.CurrentBaseTimestamp + record.TimestampDelta;

                TimestampType timestampType = pending.CurrentTimestampType;

                int messageBytes = (record.IsKeyNull ? 0 : record.Key.Length) +
                                   (record.IsValueNull ? 0 : record.Value.Length);

                try
                {
                    Current = new ConsumeResult<TKey, TValue>(
                        topic: pending.Topic,
                        partition: pending.PartitionIndex,
                        offset: offset,
                        keyData: record.Key,
                        isKeyNull: record.IsKeyNull,
                        valueData: record.Value,
                        isValueNull: record.IsValueNull,
                        pooledHeaders: record.Headers,
                        pooledHeaderCount: record.HeaderCount,
                        headerOwner: pending,
                        timestampMs: timestampMs,
                        timestampType: timestampType,
                        leaderEpoch: pending.CurrentPartitionLeaderEpoch >= 0 ? pending.CurrentPartitionLeaderEpoch : null,
                        keyDeserializer: _batch._keyDeserializer,
                        valueDeserializer: _batch._valueDeserializer);
                }
                catch (Exception ex)
                {
                    _canContinue = false;
                    _batch.ThrowAfterDeserializationFailure(pending, offset, ex);
                    return false;
                }

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
                _recordsExamined++;
                _batch._count++;

                return true;
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            private bool MoveNextFilteredOrRouted(PendingFetchData pending)
            {
                while (true)
                {
                    if (!_batch._iterationGuard.IsCurrent(pending.TopicPartition, ref _observedVersion))
                    {
                        _canContinue = false;
                        return false;
                    }

                    if (_recordsExamined >= _batch._maxRecords)
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

                    ref readonly Protocol.Records.Record record = ref pending.CurrentRecord;
                    var offset = pending.CurrentBaseOffset + record.OffsetDelta;
                    var timestampMs = pending.CurrentBaseTimestamp + record.TimestampDelta;
                    var timestampType = pending.CurrentTimestampType;
                    var leaderEpoch = pending.CurrentPartitionLeaderEpoch >= 0
                        ? (int?)pending.CurrentPartitionLeaderEpoch
                        : null;
                    var keyData = record.Key;
                    var valueData = record.Value;
                    var isKeyNull = record.IsKeyNull;
                    var isValueNull = record.IsValueNull;
                    var pooledHeaders = record.Headers;
                    var pooledHeaderCount = record.HeaderCount;
                    var headerRouting = record.CreateHeaderRoutingLookup(
                        _batch._recordHeaderRoutingPlan);
                    var messageBytes = (isKeyNull ? 0 : keyData.Length) +
                                       (isValueNull ? 0 : valueData.Length);

                    if (_batch._recordFilter is { } filter)
                    {
                        bool shouldDeserialize;
                        using var retention = pending.RetainForIteration();
                        try
                        {
                            var context = new ConsumerRecordFilterContext(
                                pending.Topic,
                                pending.PartitionIndex,
                                offset,
                                timestampMs,
                                timestampType,
                                leaderEpoch,
                                keyData,
                                isKeyNull,
                                valueData,
                                isValueNull,
                                pooledHeaders.AsSpan(0, pooledHeaderCount));
                            shouldDeserialize = filter.ShouldDeserialize(in context);
                        }
                        // lgtm[cs/catch-of-all-exceptions] Arbitrary user filter failures must rewind.
                        catch
                        {
                            _canContinue = false;
                            _batch._rewindAfterDeliveryFailure?.Invoke(pending, offset);
                            throw;
                        }

                        var filterIterationStatus = _batch._iterationGuard.GetStatusAfterRead(
                            pending.TopicPartition,
                            ref _observedVersion);
                        if (filterIterationStatus != BatchIterationStatus.Continue)
                        {
                            _canContinue = false;
                            if (filterIterationStatus == BatchIterationStatus.Paused)
                                pending.BufferCurrentForRedelivery();
                            return false;
                        }

                        if (!shouldDeserialize)
                        {
                            if (!CompleteRecord(pending, offset, messageBytes, proveProcessed: true))
                                return false;

                            if (++_rejectedRecordsExamined >= MaxRejectedRecordsPerBatch)
                            {
                                _rejectedRecordsExamined = 0;
                                // Refresh inline when the coordinator can prove the current poll
                                // generation is healthy. Expired/slow-path states end the batch so
                                // the outer async iterator can run RecordPollAsync.
                                if (_batch._tryRecordPollFast is null
                                    || !_batch._tryRecordPollFast())
                                {
                                    _canContinue = false;
                                    return false;
                                }
                            }

                            continue;
                        }
                    }

                    try
                    {
                        Current = _batch._hasRecordHeaderDeserializers
                            ? ConsumeResult<TKey, TValue>.CreateWithHeaderRouting(
                                pending.Topic,
                                pending.PartitionIndex,
                                offset,
                                keyData,
                                isKeyNull,
                                valueData,
                                isValueNull,
                                pooledHeaders,
                                pooledHeaderCount,
                                in headerRouting,
                                pending,
                                timestampMs,
                                timestampType,
                                leaderEpoch,
                                _batch._keyDeserializer,
                                _batch._valueDeserializer)
                            : new ConsumeResult<TKey, TValue>(
                                topic: pending.Topic,
                                partition: pending.PartitionIndex,
                                offset: offset,
                                keyData: keyData,
                                isKeyNull: isKeyNull,
                                valueData: valueData,
                                isValueNull: isValueNull,
                                pooledHeaders: pooledHeaders,
                                pooledHeaderCount: pooledHeaderCount,
                                headerOwner: pending,
                                timestampMs: timestampMs,
                                timestampType: timestampType,
                                leaderEpoch: leaderEpoch,
                                keyDeserializer: _batch._keyDeserializer,
                                valueDeserializer: _batch._valueDeserializer);
                    }
                    catch (Exception ex)
                    {
                        _canContinue = false;
                        _batch.ThrowAfterDeserializationFailure(pending, offset, ex);
                        return false;
                    }

                    if (!CompleteRecord(pending, offset, messageBytes, proveProcessed: false))
                        return false;

                    _batch._count++;
                    return true;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool CompleteRecord(
                PendingFetchData pending,
                long offset,
                int messageBytes,
                bool proveProcessed)
            {
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
                if (proveProcessed)
                    pending.MarkYieldedProcessed();
                _batch._storeOffsetOnDelivery?.Invoke(
                    pending.TopicPartition,
                    offset + 1,
                    pending.LastYieldedLeaderEpoch);
                _recordsExamined++;
                return true;
            }

            public readonly void Reset() => throw new NotSupportedException();

            public readonly void Dispose() { }
        }
    }
}
