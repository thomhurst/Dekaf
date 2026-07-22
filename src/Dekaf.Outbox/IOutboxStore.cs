namespace Dekaf.Outbox;

/// <summary>
/// Storage contract for the outbox relay. Implementations own the pending-message storage,
/// the bucket leases, and the relay heartbeat mechanism that drives fair bucket distribution.
/// </summary>
/// <remarks>
/// <para><b>Concurrency model:</b> the store never locks individual rows. Bucket leases are
/// the only concurrency control - a relay may read and publish a bucket's rows only while it
/// holds that bucket's lease, so each bucket has a single writer and per-key ordering is
/// preserved. A relay that pauses past its lease expiry may double-publish rows another relay
/// picked up; that is the documented at-least-once duplicate window, never message loss.</para>
/// <para><b>Storage-agnostic by design:</b> nothing in this contract requires a relational
/// database. Leases need only an atomic conditional write (SQL guarded UPDATE, MongoDB
/// findAndModify, DynamoDB conditional put, Redis SET NX). Message identity is the store's
/// own business: the relay passes the same <see cref="OutboxMessage"/> instances returned by
/// <see cref="GetNextBatchAsync"/> back to <see cref="MarkPublishedAsync"/>, so a store may
/// key on <see cref="OutboxMessage.Id"/>, on <see cref="OutboxMessage.MessageId"/>, or on a
/// native identifier carried by an <see cref="OutboxMessage"/> subclass. Ordering is a
/// behavioral requirement (per bucket, rows come back in enqueue order), not a schema
/// requirement - an auto-increment column, a time-ordered document id, or a sequence all
/// satisfy it.</para>
/// <para><b>Fatal misconfiguration:</b> throwing <see cref="OutboxMisconfigurationException"/>
/// from any method is the sanctioned way to signal a condition that can never self-heal
/// (e.g. rows in buckets the relay can never claim). The relay treats it as fatal - it
/// faults instead of retrying - while every other exception gets error backoff and retry.</para>
/// </remarks>
public interface IOutboxStore
{
    /// <summary>
    /// Renews this relay's existing bucket leases and acquires unowned or expired buckets up
    /// to a fair share of the total, based on how many relays are currently active. Also
    /// records this relay's liveness so peers can compute their own fair share.
    /// </summary>
    /// <remarks>
    /// Lease expiry comparisons typically use timestamps written by the relay hosts, so
    /// host clocks must stay synchronized to within the renewal slack
    /// (<c>LeaseDuration - LeaseRenewInterval</c>); larger skew degrades to the documented
    /// duplicates-never-loss takeover window, exactly as a stalled relay would.
    /// </remarks>
    /// <returns>The buckets this relay currently owns, in ascending order.</returns>
    ValueTask<IReadOnlyList<int>> AcquireBucketLeasesAsync(
        OutboxLeaseRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns which of the given buckets currently have unpublished rows, so an idle relay
    /// costs one probe per cycle instead of one query per owned bucket.
    /// </summary>
    ValueTask<IReadOnlyList<int>> GetBucketsWithPendingAsync(
        IReadOnlyList<int> buckets,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the oldest unpublished rows for a bucket, ordered by ascending
    /// <see cref="OutboxMessage.Id"/>. Only call for a bucket whose lease this relay holds.
    /// </summary>
    ValueTask<IReadOnlyList<OutboxMessage>> GetNextBatchAsync(
        int bucket,
        int maxCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes messages that were acknowledged by the broker so they are never republished.
    /// </summary>
    /// <remarks>
    /// <paramref name="publishedMessages"/> is always a contiguous prefix of the list the
    /// preceding <see cref="GetNextBatchAsync"/> call returned - the same instances, in the
    /// same order - so implementations can identify rows by <see cref="OutboxMessage.Id"/>,
    /// by <see cref="OutboxMessage.MessageId"/>, or by downcasting to their own
    /// <see cref="OutboxMessage"/> subclass carrying a native storage identifier.
    /// </remarks>
    ValueTask MarkPublishedAsync(
        int bucket,
        IReadOnlyList<OutboxMessage> publishedMessages,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Parameters for a lease acquisition round.
/// </summary>
public sealed class OutboxLeaseRequest
{
    /// <summary>
    /// Unique identifier of the requesting relay instance.
    /// </summary>
    public required string RelayId { get; init; }

    /// <summary>
    /// Total bucket count. Must be identical across every relay and every enqueuing writer
    /// sharing the outbox table.
    /// </summary>
    public required int BucketCount { get; init; }

    /// <summary>
    /// How long acquired leases remain valid without renewal.
    /// </summary>
    public required TimeSpan LeaseDuration { get; init; }
}
