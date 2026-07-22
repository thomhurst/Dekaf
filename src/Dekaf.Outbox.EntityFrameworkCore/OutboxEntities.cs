namespace Dekaf.Outbox.EntityFrameworkCore;

/// <summary>
/// Lease row granting one relay instance exclusive publishing rights for a bucket.
/// Managed entirely by <see cref="EfCoreOutboxStore{TContext}"/>.
/// </summary>
public sealed class OutboxLease
{
    /// <summary>The bucket this lease covers.</summary>
    public int Bucket { get; set; }

    /// <summary>The relay currently holding the lease, or null when unowned.</summary>
    public string? Owner { get; set; }

    /// <summary>When the lease expires; an expired lease can be claimed by any relay.</summary>
    public DateTimeOffset ExpiresAtUtc { get; set; }
}

/// <summary>
/// Heartbeat row recording a live relay instance. Relays use the count of recent heartbeats
/// to compute their fair share of buckets, releasing excess leases so new instances pick
/// them up. Managed entirely by <see cref="EfCoreOutboxStore{TContext}"/>.
/// </summary>
public sealed class OutboxRelayInstance
{
    /// <summary>The relay instance id.</summary>
    public string RelayId { get; set; } = string.Empty;

    /// <summary>Last heartbeat time.</summary>
    public DateTimeOffset LastSeenUtc { get; set; }
}
