namespace Dekaf.Extensions.Hosting;

/// <summary>
/// Configuration options for <see cref="KafkaConsumerService{TKey, TValue}"/> shutdown behavior.
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
}
