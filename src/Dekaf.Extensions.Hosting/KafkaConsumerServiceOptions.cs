namespace Dekaf.Extensions.Hosting;

/// <summary>
/// Configuration options for <see cref="KafkaConsumerService{TKey, TValue}"/> restart and shutdown behavior.
/// </summary>
public sealed class KafkaConsumerServiceOptions
{
    /// <summary>
    /// Maximum time to wait for in-flight work to complete during shutdown.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan ShutdownTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to continue processing remaining buffered messages before stopping.
    /// When true, the service will process messages that have already been fetched
    /// until the buffer is empty or <see cref="ShutdownTimeout"/> is reached.
    /// Default: true.
    /// </summary>
    public bool DrainOnShutdown { get; init; } = true;

    /// <summary>
    /// Delay before the first restart after consumer initialization, subscription, or polling fails.
    /// Consecutive failures double the delay up to <see cref="MaxPollRetryBackoff"/>; a delivered
    /// record resets it. Shutdown cancels the delay. Must be at least 1 millisecond. Default: 1 second.
    /// </summary>
    /// <remarks>
    /// Message processing failures are not retried by this policy: they follow the configured
    /// retry policy, retry topics, dead-letter routing, and failure disposition. Disposed consumers,
    /// argument errors, and deserialization errors also fault the service.
    /// </remarks>
    public TimeSpan PollRetryBackoff { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum delay between consecutive restarts governed by <see cref="PollRetryBackoff"/>.
    /// Must be at least <see cref="PollRetryBackoff"/>. Default: 30 seconds.
    /// </summary>
    public TimeSpan MaxPollRetryBackoff { get; init; } = TimeSpan.FromSeconds(30);
}
