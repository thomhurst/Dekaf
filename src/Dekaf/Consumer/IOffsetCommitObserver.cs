namespace Dekaf.Consumer;

/// <summary>
/// Observes offset commits that the client sends on the application's behalf, for example to
/// record a span or a metric around each commit request.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="OnCommitStarting"/> runs before the commit request and
/// <see cref="OnCommitCompleted"/> runs once after it, with the exception when it failed.
/// Commits can overlap, so keep per-commit state in the value <see cref="OnCommitStarting"/>
/// returns; the client passes it to the matching <see cref="OnCommitCompleted"/> call.
/// </para>
/// <para>
/// Callbacks run inline on the commit path and must return quickly. The observer cannot change
/// commit decisions: an exception it throws is logged and the commit proceeds as before.
/// </para>
/// </remarks>
public interface IOffsetCommitObserver
{
    /// <summary>
    /// Called before the commit request is sent.
    /// </summary>
    /// <param name="observation">The offsets being committed and why.</param>
    /// <returns>State for this commit, passed to <see cref="OnCommitCompleted"/>, or <see langword="null"/>.</returns>
    object? OnCommitStarting(OffsetCommitObservation observation);

    /// <summary>
    /// Called once after the commit request completes or fails.
    /// </summary>
    /// <param name="observation">The same observation passed to <see cref="OnCommitStarting"/>.</param>
    /// <param name="state">The value <see cref="OnCommitStarting"/> returned for this commit.</param>
    /// <param name="exception">The failure, or <see langword="null"/> when the commit succeeded.
    /// A cancelled commit reports an <see cref="OperationCanceledException"/>.</param>
    void OnCommitCompleted(OffsetCommitObservation observation, object? state, Exception? exception);
}

/// <summary>
/// Describes one observed offset commit.
/// </summary>
/// <param name="reason">Why the commit was sent.</param>
/// <param name="offsets">The offsets the commit request carries.</param>
public readonly struct OffsetCommitObservation(OffsetCommitReason reason, IReadOnlyList<TopicPartitionOffset> offsets)
{
    /// <summary>
    /// Gets why the commit was sent.
    /// </summary>
    public OffsetCommitReason Reason { get; } = reason;

    /// <summary>
    /// Gets the offsets the commit request carries: the next offset to consume for each partition.
    /// </summary>
    public IReadOnlyList<TopicPartitionOffset> Offsets { get; } = offsets;
}

/// <summary>
/// Why an observed offset commit was sent.
/// </summary>
public enum OffsetCommitReason
{
    /// <summary>
    /// A periodic commit of completed offsets.
    /// </summary>
    Periodic,

    /// <summary>
    /// A commit of completed offsets for partitions the group coordinator revoked.
    /// </summary>
    Revoke,

    /// <summary>
    /// A commit of completed offsets for a partition that stopped, for example during shutdown.
    /// </summary>
    Stop,

    /// <summary>
    /// A commit of the restart position for a partition being sought to a new offset.
    /// </summary>
    Seek
}
