using System.Runtime.CompilerServices;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;

namespace Dekaf.Testing;

public sealed partial class InMemoryConsumer<TKey, TValue>
{
    private readonly record struct BatchSelection(
        TopicPartition Partition,
        long Position,
        long RepositionSequence,
        List<InMemoryRecord> Records);

    // A batch stays current only while its partition keeps the position it was selected at and
    // is not seeked, revoked or reassigned after selection, even back to the same offset. Each
    // reposition takes the next value of one increasing sequence; a partition with no entry
    // reads the last full reset.
    private readonly Dictionary<TopicPartition, long> _repositionVersions = [];

    private readonly record struct UnprovenBatch(BatchSelection Selection, object? Batch, long NextOffset);

    // The batch being enumerated, or the enumerated progress of the last batch of a stream the
    // caller broke out of. Only an explicit CommitAsync vouches for it, as in the real consumer;
    // auto-commit and close never do.
    private UnprovenBatch? _unprovenBatch;
    private long _repositionSequence;
    private long _repositionResetVersion;

    public async IAsyncEnumerable<ConsumeBatch<TKey, TValue>> ConsumeBatchAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_requiresAsyncDeserialization)
        {
            throw new NotSupportedException(
                "ConsumeBatchAsync does not support asynchronous deserializers because batch records " +
                "are deserialized during synchronous iteration. Use ConsumeAsync or ConsumeOneAsync, " +
                "or configure synchronous IDeserializer implementations.");
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            var selection = await WaitForBatchAsync(cancellationToken).ConfigureAwait(false);
            var pending = CreatePendingFetch(selection);
            ConsumeBatch<TKey, TValue>? batch = null;
            try
            {
                batch = new ConsumeBatch<TKey, TValue>(
                    pending,
                    _keyDeserializer,
                    _valueDeserializer,
                    CreateBatchIterationGuard(selection),
                    CreateBatchOffsetStore());
                SetUnprovenBatch(selection, batch);
                yield return batch;
                // Requesting the next batch proves only the records enumerated from this one. A batch
                // that was not enumerated has no checkpoint, so its records are redelivered.
                ClearUnprovenBatch(batch);
                if (batch.TryGetNextOffset(out var checkpoint))
                    CompleteBatch(selection, checkpoint.Offset);
            }
            finally
            {
                RetainUnprovenBatch(batch);
                pending.EndCheckpointWindow(batch);
                pending.Dispose();
            }
        }
    }

    public async IAsyncEnumerable<ConsumeRawBatch> ConsumeRawBatchAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        while (!cancellationToken.IsCancellationRequested)
        {
            var selection = await WaitForBatchAsync(cancellationToken).ConfigureAwait(false);
            var pending = CreatePendingFetch(selection);
            ConsumeRawBatch? batch = null;
            try
            {
                batch = new ConsumeRawBatch(
                    pending,
                    CreateBatchIterationGuard(selection),
                    CreateBatchOffsetStore());
                SetUnprovenBatch(selection, batch);
                yield return batch;
                ClearUnprovenBatch(batch);
                if (batch.TryGetNextOffset(out var checkpoint))
                    CompleteBatch(selection, checkpoint.Offset);
            }
            finally
            {
                RetainUnprovenBatch(batch);
                pending.EndCheckpointWindow(batch);
                pending.Dispose();
            }
        }
    }

    private async ValueTask<BatchSelection> WaitForBatchAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (TrySelectBatch(out var selection))
            {
                if (!HasPotentialConsumerFault())
                    return selection;

                await ApplyBatchFaultAsync(KafkaFaultOperation.Fetch, selection.Partition, cancellationToken)
                    .ConfigureAwait(false);
                await ApplyBatchFaultAsync(KafkaFaultOperation.Consume, selection.Partition, cancellationToken)
                    .ConfigureAwait(false);
                if (IsBatchSelectionCurrent(selection))
                    return selection;

                continue;
            }

            await _cluster.WaitForRecordsAsync(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask ApplyBatchFaultAsync(
        KafkaFaultOperation operation,
        TopicPartition partition,
        CancellationToken cancellationToken)
    {
        var scope = new KafkaFaultScope(operation, partition.Topic, partition.Partition, _groupId);
        if (!HasMatchingFault(scope))
            return;

        await _cluster.FaultPlan.ApplyAsync(scope, cancellationToken).ConfigureAwait(false);
        ThrowIfDisposed();
    }

    /// <summary>
    /// Selects the first unpaused assigned partition with a readable record, like
    /// <see cref="ConsumeOneAsync"/>, and collects every record readable from its position.
    /// </summary>
    private bool TrySelectBatch(out BatchSelection selection)
    {
        // Read before selecting, so a reposition that races the selection invalidates the batch.
        long repositionSequence;
        lock (_gate)
            repositionSequence = _repositionSequence;

        var previousRecordProven = false;
        if (!TrySelectRecordWithVersion(
                ref previousRecordProven,
                capturedProof: null,
                out var partition,
                out var first,
                out var position,
                out _))
        {
            selection = default;
            return false;
        }

        var records = new List<InMemoryRecord> { first };
        _cluster.ReadRecordsFrom(partition, first.Offset + 1, _options.IsolationLevel, _borrowStoredRecords, records);

        selection = new BatchSelection(partition, position, repositionSequence, records);
        return true;
    }

    private static PendingFetchData CreatePendingFetch(BatchSelection selection)
    {
        var records = selection.Records;
        var first = records[0];
        var maxTimestamp = first.TimestampMs;
        var kafkaRecords = new Record[records.Count];
        for (var index = 0; index < records.Count; index++)
        {
            var record = records[index];
            var headers = record.Headers.Count == 0 ? null : record.Headers.ToArray();
            maxTimestamp = Math.Max(maxTimestamp, record.TimestampMs);
            kafkaRecords[index] = new Record
            {
                OffsetDelta = checked((int)(record.Offset - first.Offset)),
                TimestampDelta = record.TimestampMs - first.TimestampMs,
                Key = record.Key,
                Value = record.Value,
                IsKeyNull = record.IsKeyNull,
                IsValueNull = record.IsValueNull,
                Headers = headers,
                HeaderCount = headers?.Length ?? 0
            };
        }

        var recordBatch = RecordBatch.RentFromPool();
        recordBatch.BaseOffset = first.Offset;
        recordBatch.BaseTimestamp = first.TimestampMs;
        recordBatch.MaxTimestamp = maxTimestamp;
        recordBatch.LastOffsetDelta = kafkaRecords[^1].OffsetDelta;
        recordBatch.Attributes = RecordBatchAttributes.None;
        recordBatch.Records = kafkaRecords;

        var pending = PendingFetchData.Create(selection.Partition.Topic, selection.Partition.Partition, [recordBatch]);
        pending.EagerParseAll();
        return pending;
    }

    // Stops iteration once the partition is revoked or repositioned, and holds the rest of
    // the batch once it is paused, as the real consumer does.
    private BatchIterationGuard CreateBatchIterationGuard(BatchSelection selection) =>
        new(epoch: null, capturedVersion: 0, partition =>
        {
            lock (_gate)
            {
                if (_disposed || !IsBatchSelectionCurrentUnderLock(selection))
                    return BatchIterationStatus.Stopped;

                return _paused.Contains(partition)
                    ? BatchIterationStatus.Paused
                    : BatchIterationStatus.Continue;
            }
        });

    private Action<TopicPartition, long, int>? CreateBatchOffsetStore()
    {
        if (!_options.EnableAutoOffsetStore || _options.OffsetStoreTiming != OffsetStoreTiming.OnDelivery)
            return null;

        return (partition, nextOffset, _) =>
        {
            lock (_gate)
            {
                if (!_disposed && GetCurrentAssignmentUnderLock().Contains(partition))
                    StoreOffsetUnderLock(partition, nextOffset);
            }
        };
    }

    /// <summary>
    /// Advances a proven batch's partition to the next unprocessed offset.
    /// </summary>
    private void CompleteBatch(BatchSelection selection, long nextOffset)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            if (AdvanceBatchUnderLock(selection, nextOffset) &&
                _options.OffsetCommitMode == OffsetCommitMode.Auto)
            {
                CommitStoredOffsets();
            }
        }
    }

    private bool AdvanceBatchUnderLock(BatchSelection selection, long nextOffset)
    {
        if (!IsBatchSelectionCurrentUnderLock(selection) ||
            _pendingAutoCommitAdvancements is not null &&
            _pendingAutoCommitAdvancements.ContainsKey(selection.Partition))
        {
            return false;
        }

        _positions[selection.Partition] = nextOffset;
        if (_options.EnableAutoOffsetStore)
            StoreOffsetUnderLock(selection.Partition, nextOffset);
        return true;
    }

    private void SetUnprovenBatch(BatchSelection selection, object batch)
    {
        lock (_gate)
            _unprovenBatch = new UnprovenBatch(selection, batch, NextOffset: -1);
    }

    private void ClearUnprovenBatch(object batch)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_unprovenBatch?.Batch, batch))
                _unprovenBatch = null;
        }
    }

    /// <summary>
    /// Keeps the enumerated progress of a batch whose stream ended without requesting the next
    /// batch, for a later explicit commit. Runs before its checkpoint window closes.
    /// </summary>
    private void RetainUnprovenBatch(object? batch)
    {
        if (batch is null)
            return;

        lock (_gate)
        {
            if (_unprovenBatch is not { } unproven || !ReferenceEquals(unproven.Batch, batch))
                return;

            _unprovenBatch = TryGetBatchCheckpoint(batch, out var nextOffset)
                ? unproven with { Batch = null, NextOffset = nextOffset }
                : null;
        }
    }

    /// <summary>
    /// Explicit commit staging: the caller vouches for every record enumerated from the current
    /// batch, or from the last batch of a stream it broke out of.
    /// </summary>
    private void StageUnprovenBatchUnderLock()
    {
        if (_unprovenBatch is not { } unproven)
            return;

        if (unproven.Batch is { } active)
        {
            // Still being enumerated: requesting the next batch advances the position.
            if (_options.EnableAutoOffsetStore &&
                IsBatchSelectionCurrentUnderLock(unproven.Selection) &&
                TryGetBatchCheckpoint(active, out var nextOffset))
            {
                StoreOffsetUnderLock(unproven.Selection.Partition, nextOffset);
            }

            return;
        }

        _unprovenBatch = null;
        _ = AdvanceBatchUnderLock(unproven.Selection, unproven.NextOffset);
    }

    private static bool TryGetBatchCheckpoint(object batch, out long nextOffset)
    {
        TopicPartitionOffset checkpoint;
        var captured = batch switch
        {
            ConsumeBatch<TKey, TValue> typed => typed.TryGetNextOffset(out checkpoint),
            ConsumeRawBatch raw => raw.TryGetNextOffset(out checkpoint),
            _ => throw new InvalidOperationException("Unexpected batch type.")
        };
        nextOffset = captured ? checkpoint.Offset : -1;
        return captured;
    }

    private bool IsBatchSelectionCurrent(BatchSelection selection)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            return IsBatchSelectionCurrentUnderLock(selection);
        }
    }

    private bool IsBatchSelectionCurrentUnderLock(BatchSelection selection) =>
        GetCurrentAssignmentUnderLock().Contains(selection.Partition) &&
        _positions.TryGetValue(selection.Partition, out var position) &&
        position == selection.Position &&
        GetRepositionVersionUnderLock(selection.Partition) <= selection.RepositionSequence;

    private void MarkRepositionedUnderLock(TopicPartition partition) =>
        _repositionVersions[partition] = ++_repositionSequence;

    private void MarkAllRepositionedUnderLock()
    {
        _repositionVersions.Clear();
        _repositionResetVersion = ++_repositionSequence;
    }

    private long GetRepositionVersionUnderLock(TopicPartition partition) =>
        _repositionVersions.TryGetValue(partition, out var version) ? version : _repositionResetVersion;
}
