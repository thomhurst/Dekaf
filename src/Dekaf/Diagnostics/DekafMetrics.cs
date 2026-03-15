using System.Collections.Concurrent;
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

    // Consumer lag — single static gauge with shared callback registry.
    // Each KafkaConsumer instance registers/unregisters its callback, avoiding
    // duplicate instrument registration and ensuring disposed consumers stop reporting.

    private static readonly ConcurrentDictionary<Func<IEnumerable<Measurement<long>>>, byte> ConsumerLagCallbacks = new();

    internal static readonly ObservableGauge<long> ConsumerLagGauge =
        DekafDiagnostics.Meter.CreateObservableGauge(
            "messaging.consumer.lag",
            observeValues: ObserveAllConsumerLag,
            unit: "{message}",
            description: "Difference between high watermark and consumed position per partition.");

    /// <summary>
    /// Registers a consumer lag observation callback. Called from <c>KafkaConsumer</c> constructor.
    /// </summary>
    internal static void RegisterConsumerLagCallback(Func<IEnumerable<Measurement<long>>> callback)
    {
        ConsumerLagCallbacks.TryAdd(callback, 0);
    }

    /// <summary>
    /// Unregisters a consumer lag observation callback. Called from <c>KafkaConsumer.DisposeAsync</c>.
    /// </summary>
    internal static void UnregisterConsumerLagCallback(Func<IEnumerable<Measurement<long>>> callback)
    {
        ConsumerLagCallbacks.TryRemove(callback, out _);
    }

    private static IEnumerable<Measurement<long>> ObserveAllConsumerLag()
    {
        foreach (var (callback, _) in ConsumerLagCallbacks)
        {
            IEnumerable<Measurement<long>> measurements;
            try
            {
                measurements = callback();
            }
            catch
            {
                // Skip callbacks that throw (e.g., consumer mid-disposal)
                continue;
            }

            foreach (var measurement in measurements)
            {
                yield return measurement;
            }
        }
    }
}
