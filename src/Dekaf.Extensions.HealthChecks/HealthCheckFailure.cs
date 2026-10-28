using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dekaf.Extensions.HealthChecks;

/// <summary>
/// Builds failure results that honor the registration's <see cref="HealthCheckRegistration.FailureStatus"/>.
/// </summary>
/// <remarks>
/// The health check framework applies <see cref="HealthCheckRegistration.FailureStatus"/> only when a check
/// throws. Dekaf checks report failures as results, so they read the configured status themselves.
/// </remarks>
internal static class HealthCheckFailure
{
    /// <summary>
    /// Returns the status configured for failures of this registration, or
    /// <see cref="HealthStatus.Unhealthy"/> when the context carries no registration.
    /// </summary>
    public static HealthStatus GetFailureStatus(this HealthCheckContext context) =>
        context.Registration?.FailureStatus ?? HealthStatus.Unhealthy;
}
