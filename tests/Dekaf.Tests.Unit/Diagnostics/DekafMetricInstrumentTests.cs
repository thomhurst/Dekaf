using System.Diagnostics.Metrics;
using Dekaf.Diagnostics;

namespace Dekaf.Tests.Unit.Diagnostics;

[NotInParallel("MeterListener")]
public sealed class DekafMetricInstrumentTests
{
    [Test]
    public async Task MetricInstruments_HaveCorrectNames()
    {
        var instrumentNames = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, _) =>
        {
            if (instrument.Meter.Name == DekafDiagnostics.MeterName)
                instrumentNames.Add(instrument.Name);
        };
        listener.Start();

        // Force instrument creation by touching them. ConsumerLag is an observable
        // gauge, so listening to the meter publishes it without a measurement call.
        DekafMetrics.MessagesSent.Add(0);
        DekafMetrics.BytesSent.Add(0);
        DekafMetrics.OperationDuration.Record(0);
        DekafMetrics.ProduceErrors.Add(0);
        DekafMetrics.Retries.Add(0);
        DekafMetrics.BatchSplits.Add(0);
        DekafMetrics.InlineContinuationExceptions.Add(0);
        DekafMetrics.CompletionSourceFaults.Add(0);
        DekafMetrics.PendingResponseCleanupDeferred.Add(0);
        DekafMetrics.PendingResponseCleanupRecovered.Add(0);
        DekafMetrics.MessagesReceived.Add(0);
        DekafMetrics.BytesReceived.Add(0);
        DekafMetrics.RebalanceDuration.Record(0);
        DekafMetrics.FetchDuration.Record(0);

        await Assert.That(instrumentNames).Contains("messaging.client.sent.messages");
        await Assert.That(instrumentNames).Contains("dekaf.producer.sent.bytes");
        await Assert.That(instrumentNames).Contains("messaging.client.operation.duration");
        await Assert.That(instrumentNames).Contains("dekaf.producer.send.errors");
        await Assert.That(instrumentNames).Contains("dekaf.producer.send.retries");
        await Assert.That(instrumentNames).Contains("dekaf.producer.batch.splits");
        await Assert.That(instrumentNames).Contains("dekaf.producer.inline_continuation.exceptions");
        await Assert.That(instrumentNames).Contains("dekaf.producer.completion_source.faults");
        await Assert.That(instrumentNames).Contains("dekaf.producer.pending_response.cleanup.deferred");
        await Assert.That(instrumentNames).Contains("dekaf.producer.pending_response.cleanup.recovered");
        await Assert.That(instrumentNames).Contains("messaging.client.consumed.messages");
        await Assert.That(instrumentNames).Contains("dekaf.consumer.consumed.bytes");
        await Assert.That(instrumentNames).Contains("dekaf.consumer.rebalance.duration");
        await Assert.That(instrumentNames).Contains("dekaf.consumer.fetch.duration");
        await Assert.That(instrumentNames).Contains("dekaf.consumer.lag");
        await Assert.That(instrumentNames).Contains("dekaf.consumer.fetch_buffer.used_bytes");
        await Assert.That(instrumentNames).Contains("dekaf.consumer.fetch_buffer.free_bytes");
        await Assert.That(instrumentNames).Contains("dekaf.consumer.fetch_buffer.depleted_percent");
        await Assert.That(instrumentNames).Contains("dekaf.consumer.fetch_buffer.depleted_duration");

        // Producer internal-state gauges (observable — published by listening alone)
        await Assert.That(instrumentNames).Contains("dekaf.producer.buffer.used_bytes");
        await Assert.That(instrumentNames).Contains("dekaf.producer.buffer.limit_bytes");
        await Assert.That(instrumentNames).Contains("dekaf.producer.buffer.pressure_events");
        await Assert.That(instrumentNames).Contains("dekaf.producer.broker.budget_bytes");
        await Assert.That(instrumentNames).Contains("dekaf.producer.broker.unacked_bytes");
        await Assert.That(instrumentNames).Contains("dekaf.producer.broker.min_rtt");
        await Assert.That(instrumentNames).Contains("dekaf.producer.broker.max_delivery_rate");
        await Assert.That(instrumentNames).Contains("dekaf.producer.broker.queue_latency_ewma");
        await Assert.That(instrumentNames).Contains("dekaf.producer.broker.latency_budget_scale");
        await Assert.That(instrumentNames).Contains("dekaf.producer.broker.admission_blocks");
        await Assert.That(instrumentNames).Contains("dekaf.producer.broker.capacity_probe.successes");
        await Assert.That(instrumentNames).Contains("dekaf.producer.broker.capacity_probe.failures");
        await Assert.That(instrumentNames).Contains("dekaf.producer.broker.connections");
        await Assert.That(instrumentNames).Contains("dekaf.producer.broker.in_flight_bytes");
        await Assert.That(instrumentNames).Contains("dekaf.producer.broker.in_flight_requests");
    }
}
