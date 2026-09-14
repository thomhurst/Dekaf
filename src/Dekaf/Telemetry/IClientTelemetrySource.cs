namespace Dekaf.Telemetry;

/// <summary>Identifies the logical client for requests sent through a shared connection.</summary>
internal interface IClientTelemetrySource
{
    ClientTelemetryMetricCollector? TelemetryMetricCollector { get; }
}
