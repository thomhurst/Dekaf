using System.Runtime.CompilerServices;

namespace Dekaf.Consumer;

public sealed partial class KafkaConsumer<TKey, TValue>
{
    // Value-type modes let the JIT remove proof selection from the record loop.
    private readonly struct RecordProofMode;
    private readonly struct BufferedBatchProofMode;

    private BufferedBatchProof? _bufferedBatchProof;

    internal sealed class ConsumeStream(KafkaConsumer<TKey, TValue> consumer, CancellationToken sourceToken)
        : IAsyncEnumerable<ConsumeResult<TKey, TValue>>
    {
        public IAsyncEnumerator<ConsumeResult<TKey, TValue>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            lock (consumer._coordinatorRevokedPartitionsPendingFetchClearLock)
            {
                if (consumer._bufferedBatchProof is { Active: true })
                    throw new InvalidOperationException("Cannot consume records while a buffered batch stream is active.");
                consumer._bufferedBatchProof?.PreserveForExplicitCommit();
            }
            return consumer.SelectConsumeCore<RecordProofMode>(sourceToken).GetAsyncEnumerator(cancellationToken);
        }

        internal IAsyncEnumerable<IReadOnlyList<ConsumeResult<TKey, TValue>>> Batch(int batchSize) =>
            consumer.ConsumeBufferedBatchesAsync(batchSize, sourceToken);
    }

    private async IAsyncEnumerable<IReadOnlyList<ConsumeResult<TKey, TValue>>> ConsumeBufferedBatchesAsync(
        int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        BufferedBatchProof proof;
        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
        {
            if (_bufferedBatchProof is { Active: true })
                throw new InvalidOperationException("Only one buffered batch stream can be active on a consumer.");
            proof = _bufferedBatchProof ?? new BufferedBatchProof();
            proof.PreserveForExplicitCommit();
            proof.Active = true;
            _bufferedBatchProof = proof;
        }

        try
        {
            var batch = new List<ConsumeResult<TKey, TValue>>(batchSize);
            await foreach (var record in SelectConsumeCore<BufferedBatchProofMode>(cancellationToken).ConfigureAwait(false))
            {
                batch.Add(record);
                if (batch.Count < batchSize)
                    continue;

                yield return batch;
                ProveBufferedBatch();
                batch = new List<ConsumeResult<TKey, TValue>>(batchSize);
            }

            if (batch.Count > 0)
            {
                yield return batch;
                ProveBufferedBatch();
            }
        }
        finally
        {
            // Retain the last window for an explicit CommitAsync after a break. Close
            // and auto-commit never vouch for these unprocessed offsets.
            lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
                proof.Active = false;
        }
    }

    private bool BeginBufferedBatchFetch(PendingFetchData pending)
    {
        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
        {
            if (_bufferedBatchProof is { Active: true } proof)
            {
                proof.Pending = pending;
                proof.HeaderGeneration = pending.HeaderGeneration;
                return proof.Positions.ContainsKey(pending.TopicPartition)
                    || HasUnprovenCommitPosition(proof, pending.TopicPartition);
            }
            return false;
        }
    }

    // Called at fetch boundaries and explicit batch boundaries, never per record.
    private bool CaptureBufferedBatchPosition(PendingFetchData pending)
    {
        if (_bufferedBatchProof is not { Active: true } proof || EagerOffsetStore)
            return false;

        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
        {
            // A rejected-only prefix has no application work to prove. Do not defer
            // it unless an earlier fetch or stream left this partition in doubt.
            if (pending.ProvenOffset == pending.LastYieldedOffset
                && !proof.Positions.ContainsKey(pending.TopicPartition)
                && !HasUnprovenCommitPosition(proof, pending.TopicPartition))
                return false;
            CaptureBufferedBatchPositionLocked(proof, pending);
        }
        return true;
    }

    private void CaptureBufferedBatchPositionLocked(BufferedBatchProof proof, PendingFetchData pending)
    {
        if (ReferenceEquals(proof.Pending, pending)
            && pending.TryGetFetchGeneration(proof.HeaderGeneration, out _, out var ownership)
            && !HasPendingFetchClear(pending.TopicPartition)
            && TryGetConsumedPosition(pending, out var partition, out var nextOffset, out var leaderEpoch))
        {
            proof.Positions[partition] = new BufferedBatchPosition(
                nextOffset, leaderEpoch, ownership, pending, proof.HeaderGeneration);
        }
    }

    private void ProveBufferedBatch(bool includePreviousStreams = false)
    {
        lock (_coordinatorRevokedPartitionsPendingFetchClearLock)
        {
            if (_bufferedBatchProof is not { } proof)
                return;

            // The core is suspended at the batch boundary. Released fetches have
            // already contributed snapshots; capture the remaining live fetch now.
            if (proof.Active && proof.Pending is { } pending)
                CaptureBufferedBatchPositionLocked(proof, pending);

            if (_options.EnableAutoOffsetStore)
            {
                if (includePreviousStreams)
                    StoreBufferedBatchPositions(proof.CommitPositions, preserveNewerOffsets: true);
                StoreBufferedBatchPositions(proof.Positions);
            }
            proof.Positions.Clear();
            if (includePreviousStreams)
                proof.CommitPositions.Clear();
        }
    }

    private bool HasUnprovenCommitPosition(BufferedBatchProof proof, TopicPartition partition)
    {
        if (!proof.CommitPositions.TryGetValue(partition, out var position))
            return false;
        if (!IsBufferedBatchPositionStored(partition, position))
            return true;
        proof.CommitPositions.Remove(partition);
        return false;
    }

    private bool IsBufferedBatchPositionStored(TopicPartition partition, BufferedBatchPosition position)
    {
        if (!_storedOffsetSlots.TryGetValue(partition, out var slot))
            return false;
        slot.TryReadDirty(out var storedOwnership, out var storedOffset, out _);
        var ownership = IsGroupManagedSubscription() ? position.Ownership : 0;
        return storedOwnership == ownership && storedOffset >= position.NextOffset;
    }

    private void StoreBufferedBatchPositions(Dictionary<TopicPartition, BufferedBatchPosition> positions,
        bool preserveNewerOffsets = false)
    {
        foreach (var (partition, position) in positions)
        {
            if (HasPendingFetchClear(partition) || !IsCurrentlyAssigned(partition))
                continue;
            // Another stream may already have proven a later offset. An explicit
            // commit must not replace that progress with an abandoned checkpoint.
            if (preserveNewerOffsets && IsBufferedBatchPositionStored(partition, position))
                continue;
            StoreOffsetCore(partition, IsGroupManagedSubscription() ? position.Ownership : 0,
                position.NextOffset, position.LeaderEpoch);
            if (position.Pending.TryGetFetchGeneration(position.HeaderGeneration, out _))
                position.Pending.MarkYieldedProcessed();
        }
    }

    private readonly record struct BufferedBatchPosition(
        long NextOffset, int LeaderEpoch, long Ownership, PendingFetchData Pending, int HeaderGeneration);

    private sealed class BufferedBatchProof
    {
        public bool Active = true;
        public PendingFetchData? Pending;
        public int HeaderGeneration;
        public Dictionary<TopicPartition, BufferedBatchPosition> Positions { get; } = new();
        public Dictionary<TopicPartition, BufferedBatchPosition> CommitPositions { get; } = new();

        public void PreserveForExplicitCommit()
        {
            foreach (var (partition, position) in Positions)
                CommitPositions[partition] = position;
            Positions.Clear();
            Pending = null;
        }

        public void Invalidate(TopicPartition partition)
        {
            Positions.Remove(partition);
            CommitPositions.Remove(partition);
            if (Pending is { } pending && pending.TopicPartition.Equals(partition))
                Pending = null;
        }

        public void InvalidateAll()
        {
            Positions.Clear();
            CommitPositions.Clear();
            Pending = null;
        }
    }
}
