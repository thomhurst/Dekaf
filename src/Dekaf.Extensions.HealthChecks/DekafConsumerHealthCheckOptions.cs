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

    /// <summary>
    /// The timeout for watermark offset queries.
    /// Default is 5 seconds.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Validates that the options are consistent.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when <see cref="DegradedThreshold"/> is greater than or equal to <see cref="UnhealthyThreshold"/>.
    /// </exception>
    public void Validate()
    {
        if (DegradedThreshold >= UnhealthyThreshold)
        {
            throw new ArgumentException(
                $"DegradedThreshold ({DegradedThreshold}) must be less than UnhealthyThreshold ({UnhealthyThreshold}).");
        }
    }
}
