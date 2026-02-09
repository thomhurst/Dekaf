namespace Dekaf;

/// <summary>
/// Marker interface for Kafka clients that require async initialization.
/// Used by the DI integration to auto-initialize all registered clients at host startup.
/// </summary>
public interface IInitializableKafkaClient
{
    /// <summary>
    /// Initializes the client by fetching cluster metadata and setting up required state.
    /// This method is idempotent and thread-safe.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask InitializeAsync(CancellationToken cancellationToken = default);
}
