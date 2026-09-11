namespace Dekaf.Outbox;

/// <summary>
/// Optional bucket-aware notifications for one local relay. Unknown-bucket notifications
/// still use <see cref="IOutboxNotifier.NotifyCommitted"/> and wake unconditionally.
/// </summary>
public interface IOutboxBucketNotifier : IOutboxNotifier
{
    /// <summary>
    /// Wakes the relay only if the committed buckets overlap its current ownership.
    /// Called after commit; implementations must consume the set synchronously without
    /// retaining it, throwing, or waiting for publication. Polling remains the fallback.
    /// </summary>
    void NotifyCommitted(IReadOnlySet<int> buckets);

    /// <summary>Notifies a single committed bucket with the same ownership filtering.</summary>
    void NotifyCommitted(int bucket);

    /// <summary>
    /// Updates the ownership hint after acquisition or loss. Implementations must copy
    /// the supplied collection and safely publish the snapshot to concurrent committers.
    /// Before the first snapshot, notifications must be treated as relevant.
    /// </summary>
    void SetOwnedBuckets(IReadOnlyList<int> buckets);
}
