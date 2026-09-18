namespace Dekaf.Outbox;

/// <summary>
/// Optional store capability for stable bucket ownership: acquisition is told which buckets
/// the relay acquired last, and a stopping relay hands its leases back instead of letting
/// them expire.
/// </summary>
/// <remarks>
/// <see cref="IOutboxStore.AcquireBucketLeasesAsync"/> carries only a relay id and a bucket
/// count. A store that claims buckets with one conditional write per bucket therefore cannot
/// probe its own leases first, and a relay whose probe order starts inside a peer's holdings
/// is refused by that peer's leases on every <see cref="OutboxRelayOptions.LeaseRenewInterval"/>.
/// Stores that read the whole lease table before writing (such as the EF Core store) do not
/// need the hint, but still benefit from the release.
/// </remarks>
public interface IOutboxLeaseOwnershipStore
{
    /// <summary>
    /// Same contract as <see cref="IOutboxStore.AcquireBucketLeasesAsync"/>, which the relay
    /// no longer calls for a store with this capability.
    /// </summary>
    /// <param name="request">The acquisition parameters.</param>
    /// <param name="previousBuckets">The buckets this relay's most recent successful
    /// acquisition returned, in the order the store returned them; empty on the first round.
    /// A probe-order hint only, never proof of ownership: the relay keeps it across lease
    /// expiry and store failures, so a peer may have taken any of these buckets since. The
    /// store's atomic conditional write remains the authority.</param>
    /// <param name="cancellationToken">Cancels the acquisition.</param>
    /// <remarks>
    /// Probe <paramref name="previousBuckets"/> first, up to the fair share, where the
    /// owner-is-self branch of the lease condition matches. Probe unfamiliar buckets only for
    /// a remaining deficit, preferably in <see cref="OutboxFairShare.Assign"/> order. Relays
    /// that agree on membership then make no failed conditional write in steady state, so a
    /// failure signals a real membership disagreement.
    /// </remarks>
    /// <returns>The buckets this relay currently owns, in ascending order.</returns>
    ValueTask<IReadOnlyList<int>> AcquireBucketLeasesAsync(
        OutboxLeaseRequest request,
        IReadOnlyList<int> previousBuckets,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases every lease this relay holds and removes its liveness record, so peers can
    /// claim its buckets and stop counting it towards fair share on their next acquisition
    /// instead of after <see cref="OutboxLeaseRequest.LeaseDuration"/>.
    /// </summary>
    /// <param name="request">The parameters of this relay's acquisitions.</param>
    /// <param name="previousBuckets">The buckets this relay's most recent successful
    /// acquisition returned; the same hint as for acquisition. Leases outside this list, for
    /// example from an acquisition that failed after claiming, expire normally.</param>
    /// <param name="cancellationToken">The host's shutdown deadline.</param>
    /// <remarks>
    /// <para>The relay calls this at most once, from a graceful stop, and only after its
    /// publish loop has ended and its last publisher call has been observed. No other call
    /// from this relay is in flight or follows. A relay that does not stop before the
    /// shutdown deadline never calls it, because releasing under a running publisher would
    /// invite a peer onto rows that are still being published.</para>
    /// <para>Each release must atomically require the requesting owner, so a lease a peer
    /// has already taken over is left alone. Records the stopped publisher had already
    /// appended to Kafka can still arrive after a peer takes over; that is the documented
    /// at-least-once duplicate window, the same as after a lease expiry. Release is best
    /// effort: the relay logs a failure and the leases expire as they would without this
    /// capability.</para>
    /// </remarks>
    ValueTask ReleaseBucketLeasesAsync(
        OutboxLeaseRequest request,
        IReadOnlyList<int> previousBuckets,
        CancellationToken cancellationToken = default);
}
