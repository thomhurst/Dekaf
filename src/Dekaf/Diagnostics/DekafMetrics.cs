using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Dekaf.Producer;

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

    internal static readonly Counter<long> InlineContinuationExceptions =
        DekafDiagnostics.Meter.CreateCounter<long>(
            "dekaf.producer.inline_continuation.exceptions",
            unit: "{exception}",
            description: "Exceptions isolated from inline transactional produce continuations.");

    internal static readonly Counter<long> CompletionSourceFaults =
        DekafDiagnostics.Meter.CreateCounter<long>(
            "dekaf.producer.completion_source.faults",
            unit: "{fault}",
            description: "Producer completion-source faults isolated while completing messages.");

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

    internal static readonly Counter<long> BatchParseErrors =
        DekafDiagnostics.Meter.CreateCounter<long>(
            "messaging.consumer.batch.parse.errors",
            unit: "{error}",
            description: "Number of record batches that failed protocol parsing.");

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

    // Producer internal controller state — same registry pattern as consumer lag.
    // Diagnosing controller pathologies (budget stuck low, delivery-rate ratchet,
    // connection scale-down inversion, buffer backpressure) requires this state as
    // a live time series; throughput curves only show the symptom. Observable
    // instruments are pull-based, so nothing here ever runs on the produce hot path
    // and the closures below only execute when a metrics listener polls.

    private static readonly ConcurrentDictionary<ProducerStateSource, byte> ProducerStateSources = new();

    internal static readonly ObservableGauge<long> ProducerBufferUsedBytes = ProducerGauge(
        "dekaf.producer.buffer.used_bytes", "By",
        "Bytes currently reserved in the producer's BufferMemory.",
        static source => source.BufferUsedBytes());

    internal static readonly ObservableGauge<long> ProducerBufferLimitBytes = ProducerGauge(
        "dekaf.producer.buffer.limit_bytes", "By",
        "Configured producer BufferMemory limit.",
        static source => source.BufferLimitBytes());

    internal static readonly ObservableCounter<long> ProducerBufferPressureEvents =
        DekafDiagnostics.Meter.CreateObservableCounter(
            "dekaf.producer.buffer.pressure_events",
            observeValues: () => ObserveProducers(static source => source.BufferPressureEvents()),
            unit: "{event}",
            description: "Times ProduceAsync entered the buffer-full slow path.");

    // Per-broker budget stats: keep this selector set in sync with
    // RecordAccumulator.CreateBrokerBudgetDiagnostic, which exports the same
    // state into the opt-in delivery-diagnostics snapshot.
    internal static readonly ObservableGauge<long> ProducerBrokerBudgetBytes = BudgetGauge(
        "dekaf.producer.broker.budget_bytes", "By",
        "Published unacked-byte admission budget per broker.",
        static budget => budget.BudgetBytes);

    internal static readonly ObservableGauge<long> ProducerBrokerUnackedBytes = BudgetGauge(
        "dekaf.producer.broker.unacked_bytes", "By",
        "Standing unacked bytes charged against the broker budget.",
        static budget => budget.UnackedBytes);

    internal static readonly ObservableGauge<long> ProducerBrokerMinRtt = BudgetGauge(
        "dekaf.producer.broker.min_rtt", "us",
        "Minimum-RTT window estimate driving the budget horizon.",
        static budget => budget.MinimumRttMicros);

    internal static readonly ObservableGauge<long> ProducerBrokerMaxDeliveryRate = BudgetGauge(
        "dekaf.producer.broker.max_delivery_rate", "By/s",
        "Windowed-max delivery rate estimate per broker.",
        static budget => budget.MaxRateBytesPerSecond);

    internal static readonly ObservableGauge<long> ProducerBrokerQueueLatencyEwma = BudgetGauge(
        "dekaf.producer.broker.queue_latency_ewma", "us",
        "Seal-to-send queue latency EWMA per broker.",
        static budget => budget.DeliveryLatencyEwmaMicros);

    internal static readonly ObservableGauge<double> ProducerBrokerLatencyBudgetScale =
        DekafDiagnostics.Meter.CreateObservableGauge(
            "dekaf.producer.broker.latency_budget_scale",
            observeValues: () => ObserveProducers(static source => source.BudgetValues<double>(static budget => budget.LatencyBudgetScale)),
            unit: "1",
            description: "Latency-governed budget derating factor (1.0 = no derating).");

    internal static readonly ObservableCounter<long> ProducerBrokerAdmissionBlocks = BudgetCounter(
        "dekaf.producer.broker.admission_blocks", "{event}",
        "Times the admission gate blocked a send on the broker budget.",
        static budget => budget.AdmissionBlockEvents);

    internal static readonly ObservableCounter<long> ProducerBrokerCapacityProbeSuccesses = BudgetCounter(
        "dekaf.producer.broker.capacity_probe.successes", "{probe}",
        "Capacity probes that raised the broker budget.",
        static budget => budget.CapacityProbeSuccessCount);

    internal static readonly ObservableCounter<long> ProducerBrokerCapacityProbeFailures = BudgetCounter(
        "dekaf.producer.broker.capacity_probe.failures", "{probe}",
        "Capacity probes that failed and restored the prior budget.",
        static budget => budget.CapacityProbeFailureCount);

    internal static readonly ObservableGauge<long> ProducerBrokerConnections = SenderGauge(
        "dekaf.producer.broker.connections", "{connection}",
        "Current adaptive connection width per broker.",
        static sender => sender.CurrentConnectionCount);

    internal static readonly ObservableGauge<long> ProducerBrokerInFlightBytes = SenderGauge(
        "dekaf.producer.broker.in_flight_bytes", "By",
        "Bytes written but not yet acknowledged per broker.",
        static sender => sender.InFlightBytes);

    internal static readonly ObservableGauge<long> ProducerBrokerInFlightRequests = SenderGauge(
        "dekaf.producer.broker.in_flight_requests", "{request}",
        "Produce requests awaiting a broker response.",
        static sender => sender.InFlightRequestCount);

    /// <summary>
    /// Registers a producer's state source. Called from the <c>KafkaProducer</c> constructor.
    /// </summary>
    internal static void RegisterProducerState(ProducerStateSource source)
    {
        ProducerStateSources.TryAdd(source, 0);
    }

    /// <summary>
    /// Unregisters a producer's state source. Called from <c>KafkaProducer.DisposeAsync</c>.
    /// </summary>
    internal static void UnregisterProducerState(ProducerStateSource source)
    {
        ProducerStateSources.TryRemove(source, out _);
    }

    private static ObservableGauge<long> ProducerGauge(
        string name, string unit, string description, Func<ProducerStateSource, Measurement<long>> selector)
        => DekafDiagnostics.Meter.CreateObservableGauge(
            name, () => ObserveProducers(selector), unit, description);

    private static ObservableGauge<long> BudgetGauge(
        string name, string unit, string description, Func<BrokerUnackedByteBudget, long> selector)
        => DekafDiagnostics.Meter.CreateObservableGauge(
            name, () => ObserveProducers(source => source.BudgetValues(selector)), unit, description);

    private static ObservableCounter<long> BudgetCounter(
        string name, string unit, string description, Func<BrokerUnackedByteBudget, long> selector)
        => DekafDiagnostics.Meter.CreateObservableCounter(
            name, () => ObserveProducers(source => source.BudgetValues(selector)), unit, description);

    private static ObservableGauge<long> SenderGauge(
        string name, string unit, string description, Func<BrokerSender, long> selector)
        => DekafDiagnostics.Meter.CreateObservableGauge(
            name, () => ObserveProducers(source => source.SenderValues(selector)), unit, description);

    // A source that throws (e.g. producer mid-disposal) is skipped rather than
    // aborting the scrape for every other registered producer — the same
    // isolation ObserveAllConsumerLag applies. Selectors are lazy iterators, so
    // each source is materialized inside the guard to also cover enumeration.
    private static IEnumerable<Measurement<T>> ObserveProducers<T>(
        Func<ProducerStateSource, IEnumerable<Measurement<T>>> selector)
        where T : struct
    {
        foreach (var (source, _) in ProducerStateSources)
        {
            Measurement<T>[] measurements;
            try
            {
                measurements = selector(source).ToArray();
            }
            catch
            {
                continue;
            }

            foreach (var measurement in measurements)
            {
                yield return measurement;
            }
        }
    }

    private static IEnumerable<Measurement<long>> ObserveProducers(
        Func<ProducerStateSource, Measurement<long>> selector)
    {
        foreach (var (source, _) in ProducerStateSources)
        {
            Measurement<long> measurement;
            try
            {
                measurement = selector(source);
            }
            catch
            {
                continue;
            }

            yield return measurement;
        }
    }
}
