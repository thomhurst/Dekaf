using System.Buffers;
using System.Runtime.CompilerServices;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.ShareConsumer;

internal sealed partial class KafkaShareConsumer<TKey, TValue>
{
    private byte _consumptionMode;
    private ShareBatchAcknowledgements<TKey, TValue>? _batchAcknowledgements;
    private ShareConsumeBatch<TKey, TValue>? _activeShareBatch;

    private bool HasPendingAcknowledgements => _batchAcknowledgements?.HasPending ?? _ackTracker.HasPending;

    private Dictionary<TopicPartition, List<AcknowledgementBatchData>> FlushAcknowledgements(bool releaseImplicit = false)
        => _batchAcknowledgements?.Flush(releaseImplicit) ?? _ackTracker.Flush(releaseImplicit);

    private void SelectConsumptionMode(bool batch)
    {
        var mode = (byte)(batch ? 2 : 1);
        if (_consumptionMode != 0 && _consumptionMode != mode)
            throw new InvalidOperationException("Use either PollAsync or PollBatchesAsync for a consumer instance.");
        _consumptionMode = mode;
        if (batch)
            _batchAcknowledgements ??= new ShareBatchAcknowledgements<TKey, TValue>(OnBatchRenewal, OnBatchReplay);
    }

    private void OnBatchRenewal(TopicPartition partition, long offset)
    {
        Interlocked.Increment(ref _renewalRequestCount);
        LogRenewalRequested(partition.Topic, partition.Partition, offset);
    }

    private void OnBatchReplay() => Interlocked.Increment(ref _renewedRecordReplayCount);

    public async IAsyncEnumerable<ShareConsumeBatch<TKey, TValue>> PollBatchesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();
        SelectConsumptionMode(batch: true);
        await WaitForPendingReleaseAsync(cancellationToken).ConfigureAwait(false);
        var acknowledgements = _batchAcknowledgements!;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (_subscriptionSnapshot.Count == 0)
                yield break;
            // Ensure we're part of the share group
            await _coordinator.EnsureActiveGroupAsync(_subscriptionSnapshot, cancellationToken)
                .ConfigureAwait(false);
            _assignmentSnapshot = _coordinator.Assignment;

            var assignment = _assignmentSnapshot;
            acknowledgements.RemoveOutsideAssignment(assignment);
            if (assignment.Count == 0)
            {
                // No partitions assigned (e.g. rebalance removed them while state is Stable).
                // Delay to avoid a spin-loop — reuse FetchMaxWaitMs as the broker's natural
                // back-pressure is absent when no fetch request is issued.
                await Task.Delay(_options.FetchMaxWaitMs, cancellationToken).ConfigureAwait(false);
                continue;
            }

            // Replay successful renewals before requesting more records. Fresh traffic
            // cannot consume their poll budget or indefinitely retain their payloads.
            var replays = acknowledgements.GetReplays(_options.MaxPollRecords);
            if (replays is not null)
            {
                try
                {
                    // An unread renewal can remain eligible indefinitely. Send earlier
                    // replay dispositions before yielding another lease from that set.
                    if (acknowledgements.HasPending)
                        await CommitAsync(cancellationToken).ConfigureAwait(false);
                    foreach (var replay in replays)
                    {
                        try
                        {
                            _activeShareBatch = replay;
                            yield return replay;
                        }
                        finally
                        {
                            replay.Dispose();
                            _activeShareBatch = null;
                        }
                        if (Volatile.Read(ref _closed) != 0 || _subscriptionSnapshot.Count == 0)
                            yield break;
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
                finally
                {
                    foreach (var replay in replays)
                        replay.Dispose();
                }
                continue;
            }

            var fetchTasks = StartPollFetch(assignment, cancellationToken,
                out var pendingAcks, out var sentAcknowledgementPartitionCount);
            // Keep response frames valid while parsing producer batches. Parsed
            // RecordBatch storage has its own lifetime, including renewal replay.
            using var responseScope = new ShareFetchResponseScope(fetchTasks);
            ShareFetchBrokerResult[] fetchResults;
            try
            {
                fetchResults = await Task.WhenAll(fetchTasks).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // A broker task should normally return its failure in ShareFetchBrokerResult.
                // Preserve every drained acknowledgement if an unexpected fault escapes.
                RequeueAcknowledgements(pendingAcks);
                InvokeAcknowledgementCommitCallback(pendingAcks, ex);
                throw;
            }

            CompletePollFetch(fetchResults, pendingAcks, sentAcknowledgementPartitionCount);

            foreach (var result in fetchResults)
            {
                var response = result.Response;
                if (response is null || response.ErrorCode != ErrorCode.None)
                    continue;
                foreach (var topic in response.Responses)
                {
                    var topicInfo = _metadataManager.Metadata.GetTopic(topic.TopicId);
                    if (topicInfo is null)
                        continue;
                    foreach (var partition in topic.Partitions)
                    {
                        if (partition.ErrorCode != ErrorCode.None)
                        {
                            LogPartitionFetchError(topicInfo.Name, partition.PartitionIndex, partition.ErrorCode);
                            continue;
                        }
                        if (partition.AcquiredRecords.Count == 0)
                            continue;
                        var byteOffset = 0;
                        var acquiredIndex = 0;
                        var previousBatchOffset = long.MinValue;
                        // Batch-optimized acquisition and independent broker responses may
                        // exceed the request budget. Drain every acquired batch while its
                        // response frame is owned, before starting another fetch.
                        while (TryReadBatch(partition.RecordBytes, ref byteOffset, out var source))
                        {
                            // Ordered batches share a cursor instead of revisiting every
                            // preceding acquisition range. Restart for an out-of-order batch.
                            if (source!.BaseOffset < previousBatchOffset)
                                acquiredIndex = 0;
                            previousBatchOffset = source.BaseOffset;
                            while (acquiredIndex < partition.AcquiredRecords.Count
                                && partition.AcquiredRecords[acquiredIndex].LastOffset < source.BaseOffset)
                                acquiredIndex++;
                            var batch = await ParseRecordBatchAsync(
                                new TopicPartition(topicInfo.Name, partition.PartitionIndex),
                                source!, partition.AcquiredRecords, source!.UnparsedLazyRecordCount,
                                acquiredIndex, cancellationToken).ConfigureAwait(false);
                            try
                            {
                                if (batch.Count == 0)
                                    continue;
                                acknowledgements.Register(batch);
                                _activeShareBatch = batch;
                                yield return batch;
                            }
                            finally
                            {
                                batch.Dispose();
                                _activeShareBatch = null;
                            }
                            if (Volatile.Read(ref _closed) != 0 || _subscriptionSnapshot.Count == 0)
                                yield break;
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                    }
                }
            }
        }
    }

    private bool TryReadBatch(ReadOnlyMemory<byte> bytes, ref int byteOffset, out RecordBatch? batch)
    {
        batch = null;
        if (byteOffset >= bytes.Length)
            return false;
        var reader = new KafkaProtocolReader(bytes[byteOffset..]);
        try
        {
            // No ResponseParsingContext is active here: RecordBatch owns a copy
            // (or decompressed buffer), so replay does not borrow the response frame.
            batch = RecordBatch.Read(ref reader, _compressionCodecs);
            byteOffset += checked((int)reader.Consumed);
            return true;
        }
        catch (InsufficientDataException)
        {
            return false;
        }
    }

    internal ValueTask<ShareConsumeBatch<TKey, TValue>> ParseRecordBatchAsync(
        TopicPartition partition,
        RecordBatch source,
        IReadOnlyList<ShareFetchAcquiredRecords> acquiredRecords,
        int maxRecords,
        CancellationToken cancellationToken)
        => ParseRecordBatchAsync(partition, source, acquiredRecords, maxRecords, 0, cancellationToken);

#if NET
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
#endif
    private async ValueTask<ShareConsumeBatch<TKey, TValue>> ParseRecordBatchAsync(
        TopicPartition partition,
        RecordBatch source,
        IReadOnlyList<ShareFetchAcquiredRecords> acquiredRecords,
        int maxRecords,
        int acquiredIndex,
        CancellationToken cancellationToken)
    {
        ShareBatchStorage<TKey, TValue>? storage = null;
        var state = new BorrowedParserState { PreviousOffsetDelta = -1, AcquiredIndex = acquiredIndex };
        try
        {
            var data = source.GetUnparsedRecordData();
            var capacity = Math.Min(Math.Min(Math.Max(0, maxRecords), source.UnparsedLazyRecordCount),
                data.Length / Record.MinimumEncodedSize);
            storage = new ShareBatchStorage<TKey, TValue>(partition, source, capacity, _options.AcknowledgementMode);
            while (ParseBorrowedRecords(partition, source, storage, acquiredRecords, capacity,
                cancellationToken, ref state, out var pending))
            {
                var keyWasPending = pending.Context.Component == SerializationComponent.Key;
                if (keyWasPending)
                {
                    var keyPreparer = _keyDeserializerPreparer!;
                    var attempts = 0;
                    do
                    {
                        if (attempts++ == MaxDeserializerPreparationAttempts)
                            throw new InvalidOperationException("Deserializer remained unprepared after PrepareAsync completed.");
                        await PrepareDeserializerAsync(keyPreparer, pending.Raw.Key, pending.Context, pending.Routing, cancellationToken)
                            .ConfigureAwait(false);
                        cancellationToken.ThrowIfCancellationRequested();
                    } while (!TryDeserializePrepared(keyPreparer, pending.Raw.Key, pending.Context, in pending.Routing, out pending.Key));
                }

                pending.Context.Component = SerializationComponent.Value;
                pending.Context.KeyData = SerializationContext.NormalizeKeyData(pending.Raw.Key, pending.Raw.IsKeyNull);
                pending.Context.IsNull = pending.Raw.IsValueNull;
                pending.Context.Headers = pending.Routing.ValueRequiresMaterializedHeaders ? _recordHeaderDeserializationHeaders : null;
                TValue value = default!;
                if (!pending.Raw.IsValueNull)
                {
                    if (_valueDeserializerPreparer is { } valuePreparer)
                    {
                        var prepared = keyWasPending
                            && TryDeserializePrepared(valuePreparer, pending.Raw.Value, pending.Context, in pending.Routing, out value);
                        var attempts = 0;
                        while (!prepared)
                        {
                            if (attempts++ == MaxDeserializerPreparationAttempts)
                                throw new InvalidOperationException("Deserializer remained unprepared after PrepareAsync completed.");
                            await PrepareDeserializerAsync(valuePreparer, pending.Raw.Value, pending.Context, pending.Routing, cancellationToken)
                                .ConfigureAwait(false);
                            cancellationToken.ThrowIfCancellationRequested();
                            prepared = TryDeserializePrepared(valuePreparer, pending.Raw.Value, pending.Context, in pending.Routing, out value);
                        }
                    }
                    else
                    {
                        value = RecordHeaderDeserializer.Deserialize(_valueDeserializer, pending.Raw.Value, pending.Context, in pending.Routing);
                    }
                }
                storage.Entries[storage.Count++] = new ShareBatchEntry<TKey, TValue>
                {
                    Raw = pending.Raw, Key = pending.Key, Value = value, DeliveryCount = pending.DeliveryCount
                };
            }
            return new ShareConsumeBatch<TKey, TValue>(storage);
        }
        catch
        {
            if (storage is null)
                source.DisposeAndReturnUnownedConsumerBatch();
            else
                storage.Release();
            throw;
        }
        finally
        {
            if (state.Headers is not null)
                ArrayPool<Header>.Shared.Return(state.Headers, clearArray: true);
        }
    }

    // Parse prepared records synchronously. Only a cold preparer carries a record
    // across an await; normal records do not spill through an async state machine.
    private bool ParseBorrowedRecords(
        TopicPartition partition, RecordBatch source, ShareBatchStorage<TKey, TValue> storage,
        IReadOnlyList<ShareFetchAcquiredRecords> acquiredRecords, int capacity,
        CancellationToken cancellationToken, ref BorrowedParserState state,
        out BorrowedRecordPreparation pending)
    {
        var data = source.GetUnparsedRecordData();
        while (state.ByteOffset < data.Length && storage.Count < capacity
            && state.ParsedCount < source.UnparsedLazyRecordCount)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ShareBatchRecordData raw;
            try
            {
                raw = ReadBorrowedRecord(data, ref state.ByteOffset);
            }
            catch (Exception exception) when (RecordBatch.IsTruncatedRecordTail(
                exception, data[state.ByteOffset..], state.ParsedCount, source.UnparsedLazyRecordCount))
            {
                break;
            }
            if (raw.OffsetDelta <= state.PreviousOffsetDelta
                || source.BaseOffset > long.MaxValue - raw.OffsetDelta)
                throw new MalformedProtocolDataException("Share batch offsets must be increasing and representable.");
            if ((raw.TimestampDelta > 0 && source.BaseTimestamp > long.MaxValue - raw.TimestampDelta)
                || (raw.TimestampDelta < 0 && source.BaseTimestamp < long.MinValue - raw.TimestampDelta))
                throw new MalformedProtocolDataException("Share record timestamp cannot be represented.");
            state.PreviousOffsetDelta = raw.OffsetDelta;
            state.ParsedCount++;
            var deliveryCount = FindDeliveryCount(acquiredRecords, source.BaseOffset + raw.OffsetDelta, ref state.AcquiredIndex);
            if (deliveryCount < 0)
                continue;

            var routing = PrepareBatchHeaderRouting(raw, ref state.Headers);
            if (_recordHeaderDeserializationHeaders is { } materializedHeaders)
                routing.CopyTo(materializedHeaders);
            var context = new SerializationContext
            {
                Topic = partition.Topic,
                Component = SerializationComponent.Key,
                IsNull = raw.IsKeyNull,
                Headers = routing.KeyRequiresMaterializedHeaders ? _recordHeaderDeserializationHeaders : null
            };
            TKey? key = default;
            if (!raw.IsKeyNull)
            {
                if (_keyDeserializerPreparer is { } keyPreparer)
                {
                    if (!TryDeserializePrepared(keyPreparer, raw.Key, context, in routing, out key))
                    {
                        pending = new(raw, routing, context, default, deliveryCount);
                        return true;
                    }
                }
                else
                {
                    key = RecordHeaderDeserializer.Deserialize(_keyDeserializer, raw.Key, context, in routing);
                }
            }

            context.Component = SerializationComponent.Value;
            context.KeyData = SerializationContext.NormalizeKeyData(raw.Key, raw.IsKeyNull);
            context.IsNull = raw.IsValueNull;
            context.Headers = routing.ValueRequiresMaterializedHeaders ? _recordHeaderDeserializationHeaders : null;
            TValue value = default!;
            if (!raw.IsValueNull)
            {
                if (_valueDeserializerPreparer is { } valuePreparer)
                {
                    if (!TryDeserializePrepared(valuePreparer, raw.Value, context, in routing, out value))
                    {
                        pending = new(raw, routing, context, key, deliveryCount);
                        return true;
                    }
                }
                else
                {
                    value = RecordHeaderDeserializer.Deserialize(_valueDeserializer, raw.Value, context, in routing);
                }
            }
            storage.Entries[storage.Count++] = new ShareBatchEntry<TKey, TValue>
            {
                Raw = raw, Key = key, Value = value, DeliveryCount = deliveryCount
            };
        }
        pending = default;
        return false;
    }

    private struct BorrowedParserState
    {
        internal int ByteOffset;
        internal int ParsedCount;
        internal int AcquiredIndex;
        internal int PreviousOffsetDelta;
        internal Header[]? Headers;
    }

    private struct BorrowedRecordPreparation(
        ShareBatchRecordData raw, RecordHeaderRoutingLookup routing, SerializationContext context,
        TKey? key, int deliveryCount)
    {
        internal readonly ShareBatchRecordData Raw = raw;
        internal readonly RecordHeaderRoutingLookup Routing = routing;
        internal SerializationContext Context = context;
        internal TKey? Key = key;
        internal readonly int DeliveryCount = deliveryCount;
    }

    private static ShareBatchRecordData ReadBorrowedRecord(ReadOnlyMemory<byte> data, ref int byteOffset)
    {
        var reader = new KafkaProtocolReader(data[byteOffset..]);
        var record = ShareBatchRecordReader.Read(ref reader);
        byteOffset += checked((int)reader.Consumed);
        return record;
    }

    private RecordHeaderRoutingLookup PrepareBatchHeaderRouting(
        ShareBatchRecordData raw, ref Header[]? headers)
    {
        if (_recordHeaderRoutingPlan is null)
            return default;
        if (raw.HeaderCount == 0)
            return new RecordHeaderRoutingLookup(_recordHeaderRoutingPlan, null, 0, 0, 0,
                RecordHeaderRoutingPlan.FullyIndexedWithoutTail);

        var tailCapacity = _recordHeaderRoutingPlan.Count > 2
            ? _recordHeaderRoutingPlan.GetRoutingTailCapacity(raw.HeaderCount)
            : 0;
        var requiredCapacity = checked(raw.HeaderCount + tailCapacity);
        if (headers is null || headers.Length < requiredCapacity)
        {
            var larger = ArrayPool<Header>.Shared.Rent(requiredCapacity);
            if (headers is not null)
                ArrayPool<Header>.Shared.Return(headers, clearArray: true);
            headers = larger;
        }

        var reader = new KafkaProtocolReader(raw.HeaderBytes);
        for (var index = 0; index < raw.HeaderCount; index++)
            headers[index] = HeaderProtocol.Read(ref reader, raw.HeaderBytes.Length);
        var record = new Record { Headers = headers, HeaderCount = raw.HeaderCount }
            .IndexPooledHeaders(_recordHeaderRoutingPlan);
        return record.CreateHeaderRoutingLookup(_recordHeaderRoutingPlan);
    }
}
