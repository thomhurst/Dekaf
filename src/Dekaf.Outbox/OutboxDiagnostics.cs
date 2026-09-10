using System.Diagnostics.Metrics;

namespace Dekaf.Outbox;

/// <summary>Diagnostics identity for outbox relay instrumentation.</summary>
public static class OutboxDiagnostics
{
    /// <summary>Register with MeterProviderBuilder.AddMeter to collect relay metrics.</summary>
    public const string MeterName = "Dekaf.Outbox";

    internal static readonly Meter Meter = new(MeterName);
}
