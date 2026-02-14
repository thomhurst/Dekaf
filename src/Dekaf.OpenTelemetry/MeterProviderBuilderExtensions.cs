using Dekaf.Diagnostics;
using OpenTelemetry.Metrics;

namespace Dekaf.OpenTelemetry;

/// <summary>
/// Extension methods for registering Dekaf metrics instrumentation with OpenTelemetry.
/// </summary>
public static class MeterProviderBuilderExtensions
{
    /// <summary>
    /// Adds Dekaf producer and consumer metrics instrumentation.
    /// This subscribes to the Dekaf <see cref="System.Diagnostics.Metrics.Meter"/>
    /// to capture message counts, byte counts, latencies, and error rates.
    /// </summary>
    /// <param name="builder">The <see cref="MeterProviderBuilder"/> to configure.</param>
    /// <returns>The builder for chaining.</returns>
    public static MeterProviderBuilder AddDekafInstrumentation(this MeterProviderBuilder builder)
        => builder.AddMeter(DekafDiagnostics.MeterName);
}
