namespace Dekaf.Consumer;

/// <summary>
/// Per-window checkpoint lifetime. Reading a checkpoint reuses existing delivery counters;
/// record delivery does not maintain a second position or allocate checkpoint objects.
/// </summary>
internal static class BatchCheckpointAccess
{
    internal static bool TryGetNextOffset(PendingFetchData pending, BatchIterationGuard guard, object owner,
        out TopicPartitionOffset checkpoint)
    {
        checkpoint = default;
        if (!pending.IsCheckpointWindowActive(owner))
            return false;

        var observedVersion = guard.CapturedVersion;
        if (guard.GetStatusAfterRead(pending.TopicPartition, ref observedVersion) == BatchIterationStatus.Stopped
            || !pending.IsCheckpointWindowActive(owner))
            return false;

        // Snapshot only scalar/value fields. Pool reset may overlap these reads;
        // ownership validation below discards the entire snapshot in that case.
        var partition = pending.TopicPartition;
        var fetchEnd = pending.FetchEndOffsetExclusive;
        var fetchEndLeaderEpoch = pending.FetchEndLeaderEpoch;
        var exhausted = pending.IsExhausted && fetchEnd >= 0;
        if (pending.MessageCount == pending.CheckpointInitialMessageCount
            && (!exhausted || fetchEnd <= pending.CheckpointStartOffset))
            return false;

        var nextOffset = pending.LastYieldedOffset + 1;
        var leaderEpoch = pending.LastYieldedLeaderEpoch;
        if (exhausted && fetchEnd > nextOffset)
        {
            nextOffset = fetchEnd;
            leaderEpoch = fetchEndLeaderEpoch;
        }

        // Keep all snapshot reads before the final lifetime/epoch observations.
        // This fence is per explicit checkpoint capture, never per delivered record.
        Interlocked.MemoryBarrier();
        if (guard.GetStatusAfterRead(partition, ref observedVersion) == BatchIterationStatus.Stopped
            || !pending.IsCheckpointWindowActive(owner))
            return false;

        checkpoint = new TopicPartitionOffset(partition.Topic, partition.Partition, nextOffset, leaderEpoch);
        return true;
    }
}
