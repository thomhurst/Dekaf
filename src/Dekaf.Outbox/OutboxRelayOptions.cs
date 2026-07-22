namespace Dekaf.Outbox;

/// <summary>
/// Configuration for the outbox relay.
/// </summary>
public sealed class OutboxRelayOptions
{
    /// <summary>
    /// Default ordering bucket count.
    /// </summary>
    public const int DefaultBucketCount = 8;

    /// <summary>
    /// Default header name carrying the outbox message id for consumer-side deduplication.
    /// </summary>
    public const string DefaultMessageIdHeaderName = "x-outbox-message-id";

    /// <summary>
    /// Maximum <see cref="RelayId"/> length. Part of the store contract: stores persist the
    /// relay id in lease and heartbeat records and may size columns to exactly this bound,
    /// so a longer id must fail here at startup rather than on every store write.
    /// </summary>
    public const int MaxRelayIdLength = 128;

    /// <summary>
    /// Number of ordering buckets. Must match the <c>bucketCount</c> used when enqueuing
    /// messages, and must be identical across every relay instance sharing the outbox table.
    /// Changing it requires draining the table first: rows in buckets at or beyond the new
    /// count are never claimed.
    /// </summary>
    public int BucketCount { get; init; } = DefaultBucketCount;

    /// <summary>
    /// Maximum rows fetched and published per bucket per round trip.
    /// </summary>
    public int BatchSize { get; init; } = 500;

    /// <summary>
    /// Delay before polling again when no bucket had work.
    /// </summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Delay before retrying after a store or publish failure.
    /// </summary>
    public TimeSpan ErrorBackoff { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How long a bucket lease remains valid without renewal. A relay that stalls longer
    /// than this loses its buckets to peers; rows it had published but not yet marked may
    /// then be republished (at-least-once duplicates, never loss).
    /// </summary>
    public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How often leases are renewed. Must be comfortably below <see cref="LeaseDuration"/>.
    /// </summary>
    public TimeSpan LeaseRenewInterval { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Header name stamped with <see cref="OutboxMessage.MessageId"/> on every published
    /// record, enabling consumer-side deduplication.
    /// </summary>
    public string MessageIdHeaderName { get; init; } = DefaultMessageIdHeaderName;

    /// <summary>
    /// Unique identifier for this relay instance, at most <see cref="MaxRelayIdLength"/>
    /// characters. Defaults to machine name plus a random suffix, which is correct for
    /// almost all deployments.
    /// </summary>
    public string RelayId { get; init; } = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    /// <summary>
    /// Validates option consistency. Called by the relay at startup.
    /// </summary>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(BucketCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(BatchSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(PollInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ErrorBackoff, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(LeaseDuration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(LeaseRenewInterval, TimeSpan.Zero);
        ArgumentException.ThrowIfNullOrEmpty(MessageIdHeaderName);
        ArgumentException.ThrowIfNullOrEmpty(RelayId);

        if (RelayId.Length > MaxRelayIdLength)
        {
            throw new ArgumentException(
                $"RelayId must be at most {MaxRelayIdLength} characters; stores size their " +
                $"lease and heartbeat columns to that bound. Got {RelayId.Length}.");
        }

        if (LeaseRenewInterval >= LeaseDuration)
        {
            throw new ArgumentException(
                "LeaseRenewInterval must be less than LeaseDuration, otherwise leases expire between renewals.");
        }
    }
}
