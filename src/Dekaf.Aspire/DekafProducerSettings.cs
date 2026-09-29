using Dekaf.Extensions.HealthChecks;

namespace Dekaf.Aspire;

/// <summary>Settings for a Dekaf producer registered through Aspire.</summary>
public sealed class DekafProducerSettings : IDekafAspireSettings
{
    /// <summary>Gets or sets the comma-separated bootstrap servers.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Gets or sets whether the producer health check is disabled. The default is <see langword="false"/>.</summary>
    public bool DisableHealthChecks { get; set; }

    /// <summary>Gets or sets the producer health check options.</summary>
    public DekafProducerHealthCheckOptions HealthCheck { get; set; } = new();

    /// <summary>Gets or sets whether this registration skips subscribing OpenTelemetry to Dekaf metrics. The default is <see langword="false"/>.</summary>
    public bool DisableMetrics { get; set; }

    /// <summary>Gets or sets whether this registration skips subscribing OpenTelemetry to Dekaf traces. The default is <see langword="false"/>.</summary>
    public bool DisableTracing { get; set; }
}
