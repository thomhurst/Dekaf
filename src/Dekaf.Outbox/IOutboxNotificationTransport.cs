namespace Dekaf.Outbox;

/// <summary>
/// Optional cross-process transport for advisory post-commit bucket hints. Configure a
/// separate transport channel for each shared outbox table, with identical bucket counts.
/// Polling remains authoritative; hints may be duplicated, reordered or lost.
/// </summary>
public interface IOutboxNotificationTransport
{
    /// <summary>
    /// Broadcasts a coalesced set of committed bucket IDs to all subscribing relays.
    /// A single value of -1 means the bucket is unknown and requests discovery.
    /// Consume the memory before completion; do not retain it. Honor cancellation.
    /// Called by one background sender, never on the application's commit thread.
    /// </summary>
    ValueTask PublishAsync(ReadOnlyMemory<int> buckets, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes for the lifetime of the supplied token. Invoke the callback for each
    /// received bucket (or -1 for unknown), including remotely owned buckets. Stop all
    /// callbacks before this task completes and honor cancellation. The relay retries
    /// failed subscriptions with ErrorBackoff. May run concurrently with PublishAsync.
    /// Implementations own transport reconnection and must not use competing consumers:
    /// every relay needs the broadcast so the current bucket owner receives the hint.
    /// </summary>
    Task ListenAsync(Action<int> notifyCommitted, CancellationToken cancellationToken = default);
}
