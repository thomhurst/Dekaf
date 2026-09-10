namespace Dekaf.Outbox;

/// <summary>
/// Coalesces committed outbox writes into wake-ups for one local relay.
/// Notifications are advisory: periodic polling remains necessary for recovery and peer owners.
/// </summary>
public interface IOutboxNotifier
{
    /// <summary>
    /// Wakes the relay after the database transaction has committed. Never call before commit.
    /// This method must not throw or wait for publication.
    /// </summary>
    void NotifyCommitted();

    /// <summary>
    /// Waits for a notification or the fallback polling timeout. Only one relay may wait at a time.
    /// A notification arriving before the wait must remain pending until consumed.
    /// </summary>
    ValueTask WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}
