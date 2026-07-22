namespace Dekaf.Outbox;

/// <summary>
/// Raised when the outbox is misconfigured in a way that can never self-heal - e.g. the
/// store contains rows in buckets outside the relay's <see cref="OutboxRelayOptions.BucketCount"/>
/// (a writer/relay bucket-count mismatch, which would otherwise silently never publish).
/// The relay treats this as fatal: it logs critically and faults instead of retrying, so
/// the default host behavior stops the application rather than stalling quietly.
/// </summary>
public sealed class OutboxMisconfigurationException : Exception
{
    public OutboxMisconfigurationException(string message)
        : base(message)
    {
    }

    public OutboxMisconfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
