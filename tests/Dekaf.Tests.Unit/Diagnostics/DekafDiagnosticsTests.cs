using System.Diagnostics;
using System.Diagnostics.Metrics;
using Dekaf.Diagnostics;

namespace Dekaf.Tests.Unit.Diagnostics;

[NotInParallel("ActivityListener")]
public sealed class DekafDiagnosticsTests
{
    [Test]
    public async Task StartActivity_NoListener_ReturnsNull()
    {
        // No ActivityListener registered — should return null
        var activity = DekafDiagnostics.Source.StartActivity("test", ActivityKind.Producer);
        await Assert.That(activity).IsNull();
    }

    [Test]
    public async Task StartActivity_WithListener_ReturnsActivity()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = DekafDiagnostics.Source.StartActivity("test send", ActivityKind.Producer);
        await Assert.That(activity).IsNotNull();
        await Assert.That(activity!.OperationName).IsEqualTo("test send");
        await Assert.That(activity.Kind).IsEqualTo(ActivityKind.Producer);
    }

    [Test]
    public async Task Activity_SemanticConventionTags_AreSet()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = DekafDiagnostics.Source.StartActivity("test-topic send", ActivityKind.Producer);
        await Assert.That(activity).IsNotNull();

        activity!.SetTag(DekafDiagnostics.MessagingSystem, DekafDiagnostics.MessagingSystemValue);
        activity.SetTag(DekafDiagnostics.MessagingDestinationName, "test-topic");
        activity.SetTag(DekafDiagnostics.MessagingOperationType, "send");

        var tags = activity.Tags.ToDictionary(t => t.Key, t => t.Value);
        await Assert.That(tags["messaging.system"]).IsEqualTo("kafka");
        await Assert.That(tags["messaging.destination.name"]).IsEqualTo("test-topic");
        await Assert.That(tags["messaging.operation.type"]).IsEqualTo("send");
    }

    [Test]
    public async Task ActivitySourceName_IsCorrect()
    {
        var name = DekafDiagnostics.ActivitySourceName;
        await Assert.That(name).IsEqualTo("Dekaf");
    }

    [Test]
    public async Task MeterName_IsCorrect()
    {
        var name = DekafDiagnostics.MeterName;
        await Assert.That(name).IsEqualTo("Dekaf");
    }

    [Test]
    public async Task Meter_HasCorrectName()
    {
        var meter = DekafDiagnostics.Meter;
        await Assert.That(meter.Name).IsEqualTo("Dekaf");
    }

    [Test]
    public async Task MetricInstruments_HaveCorrectNames()
    {
        var instrumentNames = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == DekafDiagnostics.MeterName)
            {
                instrumentNames.Add(instrument.Name);
            }
        };
        listener.Start();

        // Force instrument creation by touching them
        DekafMetrics.MessagesSent.Add(0);
        DekafMetrics.BytesSent.Add(0);
        DekafMetrics.ProduceDuration.Record(0);
        DekafMetrics.ProduceErrors.Add(0);
        DekafMetrics.Retries.Add(0);
        DekafMetrics.MessagesReceived.Add(0);
        DekafMetrics.BytesReceived.Add(0);

        await Assert.That(instrumentNames).Contains("messaging.publish.messages");
        await Assert.That(instrumentNames).Contains("messaging.publish.bytes");
        await Assert.That(instrumentNames).Contains("messaging.publish.duration");
        await Assert.That(instrumentNames).Contains("messaging.publish.errors");
        await Assert.That(instrumentNames).Contains("messaging.publish.retries");
        await Assert.That(instrumentNames).Contains("messaging.receive.messages");
        await Assert.That(instrumentNames).Contains("messaging.receive.bytes");
    }
}
