namespace Dekaf.Extensions.HealthChecks;

/// <summary>
/// Options for the Dekaf producer health check.
/// </summary>
public sealed class DekafProducerHealthCheckOptions
{
    /// <summary>
    /// The timeout for waiting for the producer queue to drain.
    /// Default is 5 seconds.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
}
