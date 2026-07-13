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
        DekafMetrics.InlineContinuationExceptions.Add(0);
        DekafMetrics.CompletionSourceFaults.Add(0);
        DekafMetrics.MessagesReceived.Add(0);
        DekafMetrics.BytesReceived.Add(0);
        DekafMetrics.RebalanceDuration.Record(0);
        DekafMetrics.FetchDuration.Record(0);

        await Assert.That(instrumentNames).Contains("messaging.client.sent.messages");
        await Assert.That(instrumentNames).Contains("messaging.client.sent.bytes");
        await Assert.That(instrumentNames).Contains("messaging.client.operation.duration");
        await Assert.That(instrumentNames).Contains("messaging.client.sent.errors");
        await Assert.That(instrumentNames).Contains("messaging.client.sent.retries");
        await Assert.That(instrumentNames).Contains("dekaf.producer.inline_continuation.exceptions");
        await Assert.That(instrumentNames).Contains("dekaf.producer.completion_source.faults");
        await Assert.That(instrumentNames).Contains("messaging.client.consumed.messages");
        await Assert.That(instrumentNames).Contains("messaging.client.consumed.bytes");
        await Assert.That(instrumentNames).Contains("messaging.consumer.rebalance.duration");
        await Assert.That(instrumentNames).Contains("messaging.consumer.fetch.duration");
        await Assert.That(instrumentNames).Contains("messaging.consumer.lag");
    }
}
