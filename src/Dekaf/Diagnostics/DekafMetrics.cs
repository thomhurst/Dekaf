using System.Diagnostics.Metrics;

namespace Dekaf.Diagnostics;

/// <summary>
/// Static metric instruments for Dekaf. All instruments are no-ops (~3ns) when
/// no <see cref="MeterListener"/> or OTel SDK is attached.
/// </summary>
internal static class DekafMetrics
{
    // Producer metrics
    internal static readonly Counter<long> MessagesSent =
        DekafDiagnostics.Meter.CreateCounter<long>(
            "messaging.publish.messages",
            unit: "{message}",
            description: "Number of messages published to Kafka.");

    internal static readonly Counter<long> BytesSent =
        DekafDiagnostics.Meter.CreateCounter<long>(
            "messaging.publish.bytes",
            unit: "By",
            description: "Total bytes published to Kafka.");

    internal static readonly Histogram<double> ProduceDuration =
        DekafDiagnostics.Meter.CreateHistogram<double>(
            "messaging.publish.duration",
            unit: "s",
            description: "Duration of produce operations.");

    internal static readonly Counter<long> ProduceErrors =
        DekafDiagnostics.Meter.CreateCounter<long>(
            "messaging.publish.errors",
            unit: "{error}",
            description: "Number of produce errors.");

    internal static readonly Counter<long> Retries =
        DekafDiagnostics.Meter.CreateCounter<long>(
            "messaging.publish.retries",
            unit: "{retry}",
            description: "Number of produce retries.");

    // Consumer metrics
    internal static readonly Counter<long> MessagesReceived =
        DekafDiagnostics.Meter.CreateCounter<long>(
            "messaging.receive.messages",
            unit: "{message}",
            description: "Number of messages received from Kafka.");

    internal static readonly Counter<long> BytesReceived =
        DekafDiagnostics.Meter.CreateCounter<long>(
            "messaging.receive.bytes",
            unit: "By",
            description: "Total bytes received from Kafka.");
}
