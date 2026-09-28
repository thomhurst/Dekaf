using Dekaf.Consumer.DeadLetter;

namespace Dekaf.Aspire;

/// <summary>Settings for a Dekaf share consumer (KIP-932) registered through Aspire.</summary>
public sealed class DekafShareConsumerSettings : IDekafAspireSettings
{
    /// <summary>Gets or sets the comma-separated bootstrap servers.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Gets or sets whether the share consumer health check is disabled. The default is <see langword="false"/>.</summary>
    public bool DisableHealthChecks { get; set; }

    /// <summary>Gets or sets whether this registration skips subscribing OpenTelemetry to Dekaf metrics. The default is <see langword="false"/>.</summary>
    public bool DisableMetrics { get; set; }

    /// <summary>Gets or sets whether this registration skips subscribing OpenTelemetry to Dekaf traces. The default is <see langword="false"/>.</summary>
    public bool DisableTracing { get; set; }

    /// <summary>Gets or sets optional dead-letter queue configuration for a hosted share consumer service.</summary>
    public Action<DeadLetterQueueBuilder>? ConfigureDeadLetterQueue { get; set; }
}
