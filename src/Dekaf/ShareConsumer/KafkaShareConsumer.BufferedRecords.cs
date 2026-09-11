using Dekaf.Metadata;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
#if NETSTANDARD2_0
using TopicPartitionSet = System.Collections.Generic.IReadOnlyCollection<Dekaf.TopicPartition>;
#else
using TopicPartitionSet = System.Collections.Generic.IReadOnlySet<Dekaf.TopicPartition>;
#endif

namespace Dekaf.ShareConsumer;

internal sealed partial class KafkaShareConsumer<TKey, TValue>
{
    private List<BufferedSharePartition>? _bufferedPartitions;
    private List<ShareConsumeResult<TKey, TValue>>? _spareBufferedRecords;
    // These references survive the next poll's release of _polledBatchOwners.
    private List<ShareRecordBatchOwner>? _bufferedBatchOwners;
    private int _bufferedPartitionIndex;
    private int _bufferedRecordIndex;
    private int _bufferedRecordCount;
    private long _recordBatchScopeId;
    private List<Task<ShareFetchBrokerResult>>? _pendingFetches;
    private List<Task<ShareFetchBrokerResult>>? _deferredFetchDisposal;
    private int _pendingBrokerIndex;
    private int _pendingTopicIndex;
    private int _pendingPartitionIndex;
    private TopicPartitionSet? _bufferedAssignment;
    private DeserializerPreparationParserState _pendingParserState;
    private long _pendingPartitionLastParsedOffset = -1;
    private bool _hasBufferedAcquisitionReleases;

    private bool RemoveBufferedRecordsOutsideAssignment(TopicPartitionSet assignment)
    {
        if (ReferenceEquals(_bufferedAssignment, assignment)) return false;
        _bufferedAssignment = assignment;
        // Assignment changes are infrequent. Release undisclosed ranges before the
        // next fetch, whose inline acknowledgements only cover assigned partitions.
        var released = _pendingFetches is not null && ReleasePendingFetchAcquisitions(assignment);
        if (_bufferedPartitions is null) return released;

        var retainedCount = 0;
        for (var index = _bufferedPartitionIndex; index < _bufferedPartitions.Count; index++)
        {
            var partition = _bufferedPartitions[index];
            var firstRecord = index == _bufferedPartitionIndex ? _bufferedRecordIndex : partition.FirstRecord;
            var record = partition.Records[firstRecord];
            if (assignment.Contains(new TopicPartition(record.Topic, record.Partition)))
                _bufferedPartitions[retainedCount++] = partition with { FirstRecord = firstRecord };
            else
            {
                ReleaseBufferedAcquisitions(partition, firstRecord);
                released = true;
                _bufferedRecordCount -= partition.Records.Count - firstRecord;
                ReleaseBufferedOwners(partition);
            }
        }
        _bufferedPartitions.RemoveRange(retainedCount, _bufferedPartitions.Count - retainedCount);
        if (retainedCount == 0)
            _bufferedBatchOwners?.Clear();
        _bufferedPartitionIndex = 0;
        _bufferedRecordIndex = retainedCount == 0 ? 0 : _bufferedPartitions[0].FirstRecord;
        return released;
    }

    private async ValueTask<bool> BufferNextPartitionAsync(TopicPartitionSet assignment, int maxRecords,
        CancellationToken cancellationToken)
    {
        while (_pendingFetches is not null && !cancellationToken.IsCancellationRequested)
        {
            // Bookkeeping awaited every task before transferring these response frames.
            var pendingFetches = _pendingFetches;
            var fetch = pendingFetches[_pendingBrokerIndex].GetAwaiter().GetResult();
            var topic = fetch.Response!.Responses[_pendingTopicIndex];
            var partition = topic.Partitions[_pendingPartitionIndex];
            var topicInfo = _metadataManager.Metadata.GetTopic(topic.TopicId);
            if (topicInfo is null || !assignment.Contains(new TopicPartition(topicInfo.Name, partition.PartitionIndex)))
            {
                AdvancePendingPartition();
                continue;
            }
            if (partition.ErrorCode != ErrorCode.None)
                LogPartitionFetchError(topicInfo.Name, partition.PartitionIndex, partition.ErrorCode);
            else if (!partition.RecordBytes.IsEmpty && partition.AcquiredRecords.Count != 0)
            {
                var firstOwner = _polledBatchOwners.Count;
                var rawBytesBeforeParsing = _rawBuffer?.WrittenCount ?? 0;
                List<ShareConsumeResult<TKey, TValue>> parsed;
                _activeTelemetryFetch = ShareMetrics?.GetFetchSample(fetch.BrokerId);
                ShareMetrics?.BeginPollWait();
                try
                {
                    parsed = _hasDeserializerPreparers
                        ? await ParseBufferedPartitionWithPreparationAsync(topicInfo, partition, maxRecords, cancellationToken)
                            .ConfigureAwait(false)
                        : ParseBufferedPartition(topicInfo, partition, maxRecords);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Retry this undisclosed partition on the next poll; the response still owns its bytes.
                    RestoreRawBuffer(rawBytesBeforeParsing);
                    throw;
                }
                catch
                {
                    RestoreRawBuffer(rawBytesBeforeParsing);
                    ClearBufferedRecords(releaseAcquisitions: true);
                    // Discarded windows cannot detect revocation on the next poll.
                    _hasBufferedAcquisitionReleases |= _ackTracker.HasPending;
                    throw;
                }
                finally
                {
                    ShareMetrics?.EndPollWait();
                }
                if (!ReferenceEquals(pendingFetches, _pendingFetches))
                {
                    // Close or a subscription change can run during a preparation await.
                    // Its response frames remain scoped until this parser has unwound.
                    ReleaseUndisclosedBatchOwners(firstOwner);
                    return false;
                }
                if (parsed.Count != 0)
                {
                    _pendingPartitionLastParsedOffset = parsed[parsed.Count - 1].Offset;
                    BufferPartitionRecords(parsed, firstOwner, fetch.ReceivedTimestamp);
                }

                if (_pendingParserState.CurrentBatch is null &&
                    _pendingParserState.NextBatchByteOffset >= partition.RecordBytes.Length)
                    AdvancePendingPartition();
                if (parsed.Count != 0)
                    return true;
                continue;
            }

            AdvancePendingPartition();
        }
        return false;
    }

    private List<ShareConsumeResult<TKey, TValue>> TakeBufferedRecordList()
    {
        var records = _spareBufferedRecords;
        _spareBufferedRecords = null;
        return records ?? [];
    }

    private void RestoreRawBuffer(int writtenCount)
    {
        if (_rawBuffer is null) return;
        // Failed preparation disclosed no new slices. Keep earlier partitions readable
        // without accumulating another copy of the failed prefix on every cancellation.
        _rawBuffer.ResetWrittenCount();
        _rawBuffer.Advance(writtenCount);
    }

    private List<ShareConsumeResult<TKey, TValue>> ParseBufferedPartition(
        TopicInfo topicInfo, ShareFetchResponsePartition partition, int maxRecords)
    {
        var parsed = TakeBufferedRecordList();
        var firstOwner = _polledBatchOwners.Count;
        var pendingFetches = _pendingFetches!;
        var parserState = TakePendingParserState();
        var checkpoint = CreateParserCheckpoint(in parserState);
        var completed = false;
        try
        {
            ParsePartitionRecordsCore(topicInfo, partition, maxRecords, parsed, ref parserState);
            completed = true;
            return parsed;
        }
        catch
        {
            DiscardParsedWindow(parsed, firstOwner);
            throw;
        }
        finally
        {
            CompleteBufferedParsing(ref parserState, in checkpoint, pendingFetches, completed);
        }
    }

    private async ValueTask<List<ShareConsumeResult<TKey, TValue>>> ParseBufferedPartitionWithPreparationAsync(
        TopicInfo topicInfo, ShareFetchResponsePartition partition, int maxRecords, CancellationToken cancellationToken)
    {
        var parsed = TakeBufferedRecordList();
        var firstUndisclosedOwner = _polledBatchOwners.Count;
        var pendingFetches = _pendingFetches!;
        var parserState = TakePendingParserState();
        var checkpoint = CreateParserCheckpoint(in parserState);
        var completed = false;
        var hasRetainedKey = false;
        TKey? retainedKey = default;
        long? previousPreparationOffset = null;
        var previousPreparationComponent = default(SerializationComponent);
        var preparationAttempts = 0;
        try
        {
            while (true)
            {
                var pendingPreparation = ParsePartitionRecordsWithPreparation(
                    topicInfo,
                    partition,
                    maxRecords,
                    parsed,
                    ref parserState,
                    hasRetainedKey,
                    retainedKey);
                if (pendingPreparation is null)
                    break;

                if (previousPreparationOffset == pendingPreparation.Offset &&
                    previousPreparationComponent == pendingPreparation.Component)
                {
                    if (preparationAttempts >= MaxDeserializerPreparationAttempts)
                    {
                        throw new InvalidOperationException(
                            "Deserializer remained unprepared after PrepareAsync completed.");
                    }
                }
                else
                {
                    previousPreparationOffset = pendingPreparation.Offset;
                    previousPreparationComponent = pendingPreparation.Component;
                    preparationAttempts = 0;
                }

                preparationAttempts++;
                await PrepareDeserializerAsync(pendingPreparation, cancellationToken)
                    .ConfigureAwait(false);
                hasRetainedKey = pendingPreparation.HasRetainedKey;
                retainedKey = pendingPreparation.RetainedKey;
            }
            completed = true;
        }
        catch
        {
            _activeTelemetryFetch?.ResetPending();
            DiscardParsedWindow(parsed, firstUndisclosedOwner);
            throw;
        }
        finally
        {
            CompleteBufferedParsing(ref parserState, in checkpoint, pendingFetches, completed);
        }
        return parsed;
    }

    private DeserializerPreparationParserState TakePendingParserState()
    {
        var parserState = _pendingParserState;
        _pendingParserState = default;
        if (parserState.CurrentOwner is { } owner)
        {
            // The parser owns one reference across windows; this round needs its own
            // reference until the next poll, even if parsing completes in this window.
            owner = owner.RefreshGeneration();
            parserState.CurrentOwner = owner;
            owner.Retain();
            _polledBatchOwners.Add(owner);
        }
        return parserState;
    }

    private static DeserializerPreparationParserState CreateParserCheckpoint(
        in DeserializerPreparationParserState parserState) => new()
    {
        // A cancelled window is undisclosed. Decode its first batch again on retry,
        // skipping records from earlier successful windows without deserializing them.
        NextBatchByteOffset = parserState.CurrentBatch is { } batch
            ? parserState.NextBatchByteOffset - RecordBatch.BatchBodyOffset - batch.BatchLength
            : parserState.NextBatchByteOffset,
        RecordIndex = parserState.RecordIndex,
        AcquiredRecordIndex = parserState.AcquiredRecordIndex
    };

    private void CompleteBufferedParsing(ref DeserializerPreparationParserState parserState,
        in DeserializerPreparationParserState checkpoint, List<Task<ShareFetchBrokerResult>> pendingFetches,
        bool completed)
    {
        // A lifecycle change during PrepareAsync clears the shared cursor. Keep the
        // active parser local until it unwinds, and never restore an abandoned fetch.
        if (ReferenceEquals(pendingFetches, _pendingFetches))
        {
            _pendingParserState = completed ? parserState : checkpoint;
            if (completed)
                parserState = default;
        }
        parserState.DisposeCurrentBatch();
    }

    private void DiscardParsedWindow(List<ShareConsumeResult<TKey, TValue>> parsed, int firstOwner)
    {
        if (_rawRecords is not null)
        {
            foreach (var record in parsed)
                _rawRecords.Remove(new TopicPartitionOffset(record.Topic, record.Partition, record.Offset));
        }
        ReleaseUndisclosedBatchOwners(firstOwner);
    }

    private void ResetPendingParser()
    {
        _pendingParserState.DisposeCurrentBatch();
        _pendingParserState = default;
        _pendingPartitionLastParsedOffset = -1;
    }

    private void AdvancePendingPartition()
    {
        ResetPendingParser();
        _pendingPartitionIndex++;
        AdvancePendingFetchCursor();
    }

    private void AdvancePendingFetchCursor()
    {
        while (_pendingFetches is not null && _pendingBrokerIndex < _pendingFetches.Count)
        {
            var response = _pendingFetches[_pendingBrokerIndex].GetAwaiter().GetResult().Response;
            if (response is not null && response.ErrorCode == ErrorCode.None)
            {
                while (_pendingTopicIndex < response.Responses.Count)
                {
                    if (_pendingPartitionIndex < response.Responses[_pendingTopicIndex].Partitions.Count)
                        return;
                    _pendingTopicIndex++;
                    _pendingPartitionIndex = 0;
                }
            }
            response?.Dispose();
            _pendingBrokerIndex++;
            _pendingTopicIndex = 0;
            _pendingPartitionIndex = 0;
        }
        _pendingFetches = null;
        _pendingBrokerIndex = 0;
    }

    private void RemoveBufferedRenewalDuplicates(int remaining)
    {
        if (_bufferedPartitions is null) return;
        for (var index = _bufferedPartitionIndex; index < _bufferedPartitions.Count && remaining > 0; index++)
        {
            var partition = _bufferedPartitions[index];
            var firstRecord = index == _bufferedPartitionIndex ? _bufferedRecordIndex : partition.FirstRecord;
            for (var recordIndex = firstRecord; recordIndex < partition.Records.Count && remaining > 0; recordIndex++)
            {
                var record = partition.Records[recordIndex];
                RemoveRenewedRecord(record.Topic, record.Partition, record.Offset);
                remaining--;
            }
        }
    }

    private void BufferPartitionRecords(List<ShareConsumeResult<TKey, TValue>> records,
        int firstOwner, long receivedTimestamp)
    {
        var ownerCount = _polledBatchOwners.Count - firstOwner;
        var firstBufferedOwner = _bufferedBatchOwners?.Count ?? 0;
        if (ownerCount > 0)
        {
            var owners = _bufferedBatchOwners ??= [];
            for (var index = 0; index < ownerCount; index++)
            {
                var owner = _polledBatchOwners[firstOwner + index];
                owner.Retain();
                owners.Add(owner);
            }
        }

        (_bufferedPartitions ??= []).Add(new(records, 0, firstBufferedOwner, ownerCount, receivedTimestamp, _recordBatchScopeId));
        _bufferedRecordCount += records.Count;
    }

    private bool TryTakeBufferedRecord(
        ref ShareRecordBatchOwner? pinnedOwner, out ShareConsumeResult<TKey, TValue> result)
    {
        if (_bufferedPartitions is { Count: > 0 })
        {
            var partition = _bufferedPartitions[_bufferedPartitionIndex];
            var recordIndex = Math.Max(_bufferedRecordIndex, partition.FirstRecord);
            result = partition.Records[recordIndex];
            if (partition.CreatedScopeId != _recordBatchScopeId)
            {
                var owner = result.RetainedBatchOwner;
                if (owner is { Generation: 0 } && pinnedOwner is not null && pinnedOwner.SharesStorageWith(owner))
                    owner = pinnedOwner;
                if (owner is not null && !ReferenceEquals(owner, pinnedOwner))
                {
                    // One reference per delivered batch in this round, not per record.
                    owner.Retain();
                    owner = owner.RefreshGeneration();
                    _polledBatchOwners.Add(owner);
                    pinnedOwner = owner;
                }
                if (owner is not null)
                    result.AttachBatchOwner(owner);
            }
            _acquisitionStartedTimestamp = partition.ReceivedTimestamp;
            _bufferedRecordIndex = recordIndex + 1;
            _bufferedRecordCount--;
            if (_bufferedRecordIndex == partition.Records.Count)
                FinishBufferedPartition(partition);
            return true;
        }

        result = default!;
        return false;
    }

    private void FinishBufferedPartition(BufferedSharePartition partition)
    {
        ReleaseBufferedOwners(partition);
        if (_spareBufferedRecords is null)
        {
            // Keep one empty result list per consumer, bounded by the poll budget.
            // Clear references only once the last buffered result has been copied out.
            partition.Records.Clear();
            _spareBufferedRecords = partition.Records;
        }
        _bufferedPartitions![_bufferedPartitionIndex++] = default;
        _bufferedRecordIndex = 0;
        if (_bufferedPartitionIndex == _bufferedPartitions.Count)
        {
            _bufferedPartitions.Clear();
            _bufferedBatchOwners?.Clear();
            _bufferedPartitionIndex = 0;
        }
    }

    private void ClearBufferedRecords(bool releaseAcquisitions = false)
    {
        _spareBufferedRecords = null;
        if (_pendingFetches is not null)
        {
            if (releaseAcquisitions)
                ReleasePendingFetchAcquisitions();
            if (_recordBatchScopes != 0)
            {
                if (_deferredFetchDisposal is null)
                    _deferredFetchDisposal = _pendingFetches;
                else
                    _deferredFetchDisposal.AddRange(_pendingFetches);
            }
            else
            {
                foreach (var task in _pendingFetches)
                    task.GetAwaiter().GetResult().Response?.Dispose();
            }
            _pendingFetches = null;
        }
        ResetPendingParser();
        _pendingBrokerIndex = 0;
        _pendingTopicIndex = 0;
        _pendingPartitionIndex = 0;
        _bufferedAssignment = null;
        if (_bufferedPartitions is null) return;
        for (var index = _bufferedPartitionIndex; index < _bufferedPartitions.Count; index++)
        {
            var partition = _bufferedPartitions[index];
            if (releaseAcquisitions)
            {
                var firstRecord = index == _bufferedPartitionIndex
                    ? Math.Max(_bufferedRecordIndex, partition.FirstRecord) : partition.FirstRecord;
                ReleaseBufferedAcquisitions(partition, firstRecord);
            }
            ReleaseBufferedOwners(partition);
        }
        _bufferedPartitions.Clear();
        _bufferedBatchOwners?.Clear();
        _bufferedPartitionIndex = 0;
        _bufferedRecordIndex = 0;
        _bufferedRecordCount = 0;
    }

    private void DisposeDeferredFetches()
    {
        if (_deferredFetchDisposal is null) return;
        foreach (var task in _deferredFetchDisposal)
            task.GetAwaiter().GetResult().Response?.Dispose();
        _deferredFetchDisposal = null;
    }

    private bool ReleasePendingFetchAcquisitions(TopicPartitionSet? retainedAssignment = null)
    {
        var released = false;
        for (var brokerIndex = _pendingBrokerIndex; brokerIndex < _pendingFetches!.Count; brokerIndex++)
        {
            var response = _pendingFetches[brokerIndex].GetAwaiter().GetResult().Response;
            if (response is null || response.ErrorCode != ErrorCode.None) continue;
            var firstTopic = brokerIndex == _pendingBrokerIndex ? _pendingTopicIndex : 0;
            for (var topicIndex = firstTopic; topicIndex < response.Responses.Count; topicIndex++)
            {
                var topic = response.Responses[topicIndex];
                var topicInfo = _metadataManager.Metadata.GetTopic(topic.TopicId);
                if (topicInfo is null) continue;
                var firstPartition = brokerIndex == _pendingBrokerIndex && topicIndex == _pendingTopicIndex
                    ? _pendingPartitionIndex : 0;
                for (var partitionIndex = firstPartition; partitionIndex < topic.Partitions.Count; partitionIndex++)
                {
                    var partition = topic.Partitions[partitionIndex];
                    if (partition.ErrorCode != ErrorCode.None) continue;
                    var tp = new TopicPartition(topicInfo.Name, partition.PartitionIndex);
                    if (retainedAssignment is not null && retainedAssignment.Contains(tp)) continue;
                    for (var rangeIndex = 0; rangeIndex < partition.AcquiredRecords.Count; rangeIndex++)
                    {
                        var range = partition.AcquiredRecords[rangeIndex];
                        var firstOffset = range.FirstOffset;
                        if (brokerIndex == _pendingBrokerIndex && topicIndex == _pendingTopicIndex &&
                            partitionIndex == _pendingPartitionIndex)
                        {
                            // Parsed-but-undisclosed records are released from the buffered
                            // lists below. Do not release an already disclosed prefix again.
                            if (_pendingPartitionLastParsedOffset >= range.LastOffset)
                                continue;
                            firstOffset = Math.Max(firstOffset, _pendingPartitionLastParsedOffset + 1);
                        }
                        _ackTracker.ReleaseUndeliveredRecords(tp, firstOffset, range.LastOffset);
                        released = true;
                    }
                }
            }
        }
        return released;
    }

    private void ReleaseBufferedAcquisitions(BufferedSharePartition partition, int firstRecord)
    {
        for (var recordIndex = firstRecord; recordIndex < partition.Records.Count; recordIndex++)
        {
            var record = partition.Records[recordIndex];
            _ackTracker.ReleaseUndeliveredRecords(new(record.Topic, record.Partition), record.Offset, record.Offset);
        }
    }

    private void ReleaseBufferedOwners(BufferedSharePartition partition)
    {
        var owners = _bufferedBatchOwners!;
        var lastOwner = partition.FirstOwner + partition.OwnerCount;
        for (var index = partition.FirstOwner; index < lastOwner; index++)
        {
            owners[index].Release();
            owners[index] = null!;
        }
    }

    private readonly record struct BufferedSharePartition(
        List<ShareConsumeResult<TKey, TValue>> Records, int FirstRecord,
        int FirstOwner, int OwnerCount, long ReceivedTimestamp, long CreatedScopeId);
}
