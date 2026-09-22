namespace Dekaf.Networking;

// Client-side routing failures that clear once metadata catches up or a broker finishes
// restarting. They derive from InvalidOperationException and keep the historical messages so
// existing catch sites and callers that match on the base type behave exactly as before; the
// dedicated types only let TransportFailureClassifier tell them apart from a genuine
// operation-invariant InvalidOperationException, which must never be retried.

/// <summary>
/// The connection pool has no route for the requested broker ID, typically because a leader or
/// coordinator was named before the metadata response carrying its endpoint was applied.
/// </summary>
internal sealed class UnknownBrokerException(int brokerId)
    : InvalidOperationException($"Unknown broker ID: {brokerId}")
{
    public int BrokerId { get; } = brokerId;
}

/// <summary>
/// Every connection-setup attempt produced a connection that was already closed, as happens
/// while a broker (or a proxy in front of it) accepts and immediately drops connections.
/// </summary>
internal sealed class ConnectionSetupExhaustedException(int maxRetries)
    : InvalidOperationException($"Failed to create connection after {maxRetries} retries");

/// <summary>
/// Metadata moved a broker to another endpoint while a connection to its previous endpoint was
/// still being set up. The finished connection is discarded instead of published; the next
/// attempt resolves the current endpoint.
/// </summary>
internal sealed class BrokerEndpointChangedException(
    int brokerId,
    string previousHost,
    int previousPort,
    string currentHost,
    int currentPort)
    : InvalidOperationException(
        $"Broker {brokerId} moved from {previousHost}:{previousPort} to {currentHost}:{currentPort} " +
        "while a connection to the previous endpoint was being set up");

/// <summary>
/// A metadata refresh failed against every known endpoint. The inner exception is the last
/// endpoint's failure.
/// </summary>
internal sealed class MetadataRefreshFailedException(Exception? lastFailure)
    : InvalidOperationException("Failed to refresh metadata from any broker", lastFailure);
