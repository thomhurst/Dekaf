namespace Dekaf.Extensions.HealthChecks;

/// <summary>
/// Options for the Dekaf consumer lag health check.
/// </summary>
public sealed class DekafConsumerHealthCheckOptions
{
    /// <summary>
    /// The maximum acceptable consumer lag (in messages) per partition before the health check
    /// reports <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded"/>.
    /// Default is 1000.
    /// </summary>
    public long DegradedThreshold { get; init; } = 1000;

    /// <summary>
    /// The maximum acceptable consumer lag (in messages) per partition before the health check
    /// reports <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy"/>.
    /// Default is 10000.
    /// </summary>
    public long UnhealthyThreshold { get; init; } = 10000;
}

/// <summary>
/// Options for the Dekaf producer health check.
/// </summary>
public sealed class DekafProducerHealthCheckOptions
{
    /// <summary>
    /// The timeout for the producer connectivity check.
    /// Default is 5 seconds.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// Options for the Dekaf broker connectivity health check.
/// </summary>
public sealed class DekafBrokerHealthCheckOptions
{
    /// <summary>
    /// The bootstrap servers to check connectivity against.
    /// </summary>
    public required string BootstrapServers { get; init; }

    /// <summary>
    /// The timeout for the broker connectivity check.
    /// Default is 5 seconds.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
}
