namespace Dekaf.Outbox;

/// <summary>Optional store capability for whole-outbox backlog observations.</summary>
/// <remarks>
/// Called on a separate bounded sampling cadence, concurrently with relay operations.
/// Implementations must isolate their database/session state and honor cancellation.
/// Return null when unavailable; do not substitute an empty backlog for a failed query.
/// Existing IOutboxStore implementations need not implement this capability.
/// </remarks>
public interface IOutboxMetricsStore
{
    /// <summary>Reads pending count and, when supported, the oldest creation timestamp.</summary>
    ValueTask<OutboxPendingMetrics?> GetPendingMetricsAsync(CancellationToken cancellationToken = default);
}

/// <summary>A sampled whole-store backlog; count zero means empty.</summary>
public sealed class OutboxPendingMetrics
{
    /// <summary>Creates an immutable backlog observation.</summary>
    public OutboxPendingMetrics(long pendingCount, DateTimeOffset? oldestCreatedAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pendingCount);
        PendingCount = pendingCount;
        OldestCreatedAtUtc = oldestCreatedAtUtc;
    }

    /// <summary>Number of pending messages, never negative.</summary>
    public long PendingCount { get; }

    /// <summary>Earliest creation timestamp, or null when empty or unavailable.</summary>
    public DateTimeOffset? OldestCreatedAtUtc { get; }
}
