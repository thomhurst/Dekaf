using Dekaf.Diagnostics;
using OpenTelemetry.Trace;

namespace Dekaf.OpenTelemetry;

/// <summary>
/// Extension methods for registering Dekaf tracing instrumentation with OpenTelemetry.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Adds Dekaf producer and consumer tracing instrumentation.
    /// This subscribes to the Dekaf <see cref="System.Diagnostics.ActivitySource"/>
    /// to capture produce and consume spans with W3C trace context propagation.
    /// </summary>
    /// <param name="builder">The <see cref="TracerProviderBuilder"/> to configure.</param>
    /// <returns>The builder for chaining.</returns>
    public static TracerProviderBuilder AddDekafInstrumentation(this TracerProviderBuilder builder)
        => builder.AddSource(DekafDiagnostics.ActivitySourceName);
}
