namespace Dekaf.Extensions.HealthChecks;

/// <summary>
/// Options for the Dekaf broker connectivity health check.
/// </summary>
public sealed class DekafBrokerHealthCheckOptions
{
    /// <summary>
    /// The timeout for the broker connectivity check.
    /// Default is 5 seconds.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
}
