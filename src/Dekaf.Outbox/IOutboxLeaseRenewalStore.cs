namespace Dekaf.Outbox;

/// <summary>
/// Optional store capability for keeping leases alive throughout an asynchronous publish.
/// </summary>
public interface IOutboxLeaseRenewalStore
{
    /// <summary>
    /// Renews the supplied, still-valid leases and this relay's heartbeat without acquiring
    /// new buckets or releasing existing buckets for fair-share rebalancing.
    /// </summary>
    /// <remarks>
    /// Never reacquire an expired lease, even if its owner has not changed. Each update must
    /// atomically require the requesting owner and an expiry later than the renewal's start.
    /// The relay also rejects a response received after its previous lease deadline, since
    /// a delayed response cannot prove continuous ownership. Return false if any
    /// supplied bucket could not be renewed. The relay serializes this method with all
    /// other store calls; only the publisher can run concurrently with it.
    /// </remarks>
    ValueTask<bool> RenewBucketLeasesAsync(
        OutboxLeaseRequest request,
        IReadOnlyList<int> buckets,
        CancellationToken cancellationToken = default);
}
