namespace Dekaf.Consumer;

/// <summary>
/// Controls a running partitioned consumer: seeks a partition to replay or skip records while
/// the other partitions keep processing.
/// </summary>
/// <remarks>
/// Pass the control in <see cref="PartitionedProcessingOptions.Control"/>. It attaches to one
/// running <c>RunPartitionedAsync</c> or <c>RunPartitionedBatchesAsync</c> call at a time and
/// detaches when that call returns, so it can be reused by a later run.
/// </remarks>
public sealed class PartitionedProcessingControl
{
    private IPartitionedProcessingSeekTarget? _target;

    /// <summary>
    /// Moves an assigned partition to <paramref name="offset"/> and restarts its processor there.
    /// </summary>
    /// <remarks>
    /// The runtime stops dispatching the partition's buffered records and stops its in-flight
    /// handlers under <see cref="PartitionedProcessingOptions.StopPolicy"/> and
    /// <see cref="PartitionedProcessingOptions.StopTimeout"/>. Records already buffered for the
    /// partition are discarded, not processed. With a runtime-managed
    /// <see cref="PartitionedProcessingOptions.CommitPolicy"/>, the runtime then commits the lower
    /// of the partition's completed offset and <paramref name="offset"/>, so a restart replays from
    /// the seek position. Completion tracking for the old position is discarded. The partition's
    /// processor restarts at <paramref name="offset"/>, including a partition that
    /// <see cref="PartitionWorkerErrorPolicy.StopPartition"/> stopped.
    /// <para>
    /// The other partitions' processors keep processing their buffered records, but the runtime
    /// routes no new batches until the seek completes. Cancelling
    /// <paramref name="cancellationToken"/> before the runtime starts the seek prevents it; later
    /// cancellation stops only the caller's wait.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The offset is negative.</exception>
    /// <exception cref="InvalidOperationException">
    /// No partitioned run is active for this control, the run stopped before the seek completed,
    /// or the partition is not assigned to the consumer.
    /// </exception>
    public ValueTask SeekAsync(TopicPartitionOffset offset, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset.Offset, nameof(offset));
        return GetTarget().SeekAsync(offset, cancellationToken);
    }

    /// <summary>
    /// Moves an assigned partition to the first record whose timestamp is at or after
    /// <paramref name="timestamp"/> and restarts its processor there.
    /// </summary>
    /// <remarks>
    /// The offset is resolved with the consumer's offsets-for-times lookup before the partition
    /// stops. <see cref="TopicPartitionTimestamp.Earliest"/> and <see cref="TopicPartitionTimestamp.Latest"/>
    /// seek to the start or end of the partition. When no record is at or after the timestamp, the
    /// partition seeks to its end. The seek then behaves like
    /// <see cref="SeekAsync(TopicPartitionOffset, CancellationToken)"/>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// No partitioned run is active for this control, the run stopped before the seek completed,
    /// or the partition is not assigned to the consumer.
    /// </exception>
    public ValueTask SeekAsync(TopicPartitionTimestamp timestamp, CancellationToken cancellationToken = default)
        => GetTarget().SeekAsync(timestamp, cancellationToken);

    internal void Attach(IPartitionedProcessingSeekTarget target)
    {
        if (Interlocked.CompareExchange(ref _target, target, null) is not null)
        {
            throw new InvalidOperationException(
                $"This {nameof(PartitionedProcessingControl)} is already attached to a running partitioned consumer. " +
                "Use a separate control for each concurrent run.");
        }
    }

    internal void Detach(IPartitionedProcessingSeekTarget target)
        => Interlocked.CompareExchange(ref _target, null, target);

    private IPartitionedProcessingSeekTarget GetTarget()
        => Volatile.Read(ref _target)
            ?? throw new InvalidOperationException(
                $"No partitioned consumer run is active for this {nameof(PartitionedProcessingControl)}.");
}

internal interface IPartitionedProcessingSeekTarget
{
    ValueTask SeekAsync(TopicPartitionOffset offset, CancellationToken cancellationToken);

    ValueTask SeekAsync(TopicPartitionTimestamp timestamp, CancellationToken cancellationToken);
}
