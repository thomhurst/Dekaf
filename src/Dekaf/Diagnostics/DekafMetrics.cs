using System.Diagnostics.Metrics;

namespace Dekaf.Diagnostics;

/// <summary>
/// Static metric instruments for Dekaf. All instruments are no-ops (~3ns) when
/// no <see cref="MeterListener"/> or OTel SDK is attached.
/// </summary>
internal static class DekafMetrics
{
    // OTel semantic convention metrics
    internal static readonly Counter<long> MessagesSent =
        DekafDiagnostics.Meter.CreateCounter<long>(
            "messaging.client.sent.messages",
            unit: "{message}",
            description: "Number of messages published to Kafka.");

    internal static readonly Counter<long> BytesSent =
        DekafDiagnostics.Meter.CreateCounter<long>(
            "messaging.client.sent.bytes",
            unit: "By",
            description: "Total bytes published to Kafka.");

    internal static readonly Histogram<double> OperationDuration =
        DekafDiagnostics.Meter.CreateHistogram<double>(
            "messaging.client.operation.duration",
            unit: "s",
            description: "Duration of messaging operations (produce or consume).");

    internal static readonly Counter<long> ProduceErrors =
        DekafDiagnostics.Meter.CreateCounter<long>(
            "messaging.client.sent.errors",
            unit: "{error}",
            description: "Number of produce errors.");

    internal static readonly Counter<long> Retries =
        DekafDiagnostics.Meter.CreateCounter<long>(
            "messaging.client.sent.retries",
            unit: "{retry}",
            description: "Number of produce retries.");

    // Consumer metrics
    internal static readonly Counter<long> MessagesReceived =
        DekafDiagnostics.Meter.CreateCounter<long>(
            "messaging.client.consumed.messages",
            unit: "{message}",
            description: "Number of messages received from Kafka.");

    internal static readonly Counter<long> BytesReceived =
        DekafDiagnostics.Meter.CreateCounter<long>(
            "messaging.client.consumed.bytes",
            unit: "By",
            description: "Total bytes received from Kafka.");

    internal static readonly Histogram<double> RebalanceDuration =
        DekafDiagnostics.Meter.CreateHistogram<double>(
            "messaging.consumer.rebalance.duration",
            unit: "s",
            description: "Duration of consumer group rebalances.");

    internal static readonly Histogram<double> FetchDuration =
        DekafDiagnostics.Meter.CreateHistogram<double>(
            "messaging.consumer.fetch.duration",
            unit: "s",
            description: "Round-trip time of fetch requests to Kafka brokers.");

    /// <summary>
    /// Observable gauge for consumer lag is created per-consumer instance via
    /// <see cref="DekafDiagnostics.Meter"/>.<see cref="Meter.CreateObservableGauge{T}(string, Func{T}, string?, string?)"/>.
    /// See <c>KafkaConsumer</c> constructor for registration.
    /// </summary>
    internal const string ConsumerLagName = "messaging.consumer.lag";
    internal const string ConsumerLagUnit = "{message}";
    internal const string ConsumerLagDescription = "Difference between high watermark and committed offset per partition.";
}
