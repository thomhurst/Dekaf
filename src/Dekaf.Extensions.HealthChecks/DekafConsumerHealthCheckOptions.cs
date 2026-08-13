using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dekaf.Extensions.HealthChecks;

/// <summary>
/// Options for the Dekaf consumer lag health check.
/// </summary>
public sealed class DekafConsumerHealthCheckOptions
{
    /// <summary>
    /// The status returned for a live, joined consumer that has no assigned partitions.
    /// Default is <see cref="HealthStatus.Healthy"/> because a consumer group may have more
    /// members than partitions.
    /// </summary>
    public HealthStatus NoAssignmentStatus { get; init; } = HealthStatus.Healthy;

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
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <see cref="NoAssignmentStatus"/> is not a valid <see cref="HealthStatus"/>.
    /// </exception>
    public void Validate()
    {
        if (!Enum.IsDefined(NoAssignmentStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(NoAssignmentStatus),
                NoAssignmentStatus,
                "NoAssignmentStatus must be a valid health status.");
        }

        if (DegradedThreshold >= UnhealthyThreshold)
        {
            throw new ArgumentException(
                $"DegradedThreshold ({DegradedThreshold}) must be less than UnhealthyThreshold ({UnhealthyThreshold}).");
        }
    }
}
