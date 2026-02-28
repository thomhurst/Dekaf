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

        using var activity = DekafDiagnostics.Source.StartActivity("test publish", ActivityKind.Producer);
        await Assert.That(activity).IsNotNull();
        await Assert.That(activity!.OperationName).IsEqualTo("test publish");
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

        using var activity = DekafDiagnostics.Source.StartActivity("test-topic publish", ActivityKind.Producer);
        await Assert.That(activity).IsNotNull();

        activity!.SetTag(DekafDiagnostics.MessagingSystem, DekafDiagnostics.MessagingSystemValue);
        activity.SetTag(DekafDiagnostics.MessagingDestinationName, "test-topic");
        activity.SetTag(DekafDiagnostics.MessagingOperationType, "publish");

        var tags = activity.Tags.ToDictionary(t => t.Key, t => t.Value);
        await Assert.That(tags["messaging.system"]).IsEqualTo("kafka");
        await Assert.That(tags["messaging.destination.name"]).IsEqualTo("test-topic");
        await Assert.That(tags["messaging.operation.type"]).IsEqualTo("publish");
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

    [Test]
    public async Task ProducerSpan_HasCorrectNamingConvention()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // OTel semantic convention: "{topic} publish" for producer spans
        using var activity = DekafDiagnostics.Source.StartActivity("orders publish", ActivityKind.Producer);
        await Assert.That(activity).IsNotNull();
        await Assert.That(activity!.OperationName).IsEqualTo("orders publish");
        await Assert.That(activity.Kind).IsEqualTo(ActivityKind.Producer);
    }

    [Test]
    public async Task ConsumerSpan_HasCorrectNamingConvention()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // OTel semantic convention: "{topic} receive" for consumer spans
        using var activity = DekafDiagnostics.Source.StartActivity("orders receive", ActivityKind.Consumer);
        await Assert.That(activity).IsNotNull();
        await Assert.That(activity!.OperationName).IsEqualTo("orders receive");
        await Assert.That(activity.Kind).IsEqualTo(ActivityKind.Consumer);
    }

    [Test]
    public async Task ProducerSpan_AllSemanticAttributes_AreSet()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = DekafDiagnostics.Source.StartActivity("my-topic publish", ActivityKind.Producer);
        await Assert.That(activity).IsNotNull();

        // Simulate what the producer does (using string values for tag retrieval compatibility)
        activity!.SetTag(DekafDiagnostics.MessagingSystem, DekafDiagnostics.MessagingSystemValue);
        activity.SetTag(DekafDiagnostics.MessagingDestinationName, "my-topic");
        activity.SetTag(DekafDiagnostics.MessagingOperationType, "publish");
        activity.SetTag(DekafDiagnostics.MessagingMessageKey, "order-123");
        activity.SetTag(DekafDiagnostics.MessagingDestinationPartitionId, "2");
        activity.SetTag(DekafDiagnostics.MessagingMessageOffset, "42");

        var tags = activity.Tags.ToDictionary(t => t.Key, t => t.Value);
        await Assert.That(tags["messaging.system"]).IsEqualTo("kafka");
        await Assert.That(tags["messaging.destination.name"]).IsEqualTo("my-topic");
        await Assert.That(tags["messaging.operation.type"]).IsEqualTo("publish");
        await Assert.That(tags["messaging.kafka.message.key"]).IsEqualTo("order-123");
        await Assert.That(tags["messaging.destination.partition.id"]).IsEqualTo("2");
        await Assert.That(tags["messaging.kafka.message.offset"]).IsEqualTo("42");
    }

    [Test]
    public async Task ConsumerSpan_AllSemanticAttributes_AreSet()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = DekafDiagnostics.Source.StartActivity("my-topic receive", ActivityKind.Consumer);
        await Assert.That(activity).IsNotNull();

        // Simulate what the consumer does (using string values for tag retrieval compatibility)
        activity!.SetTag(DekafDiagnostics.MessagingSystem, DekafDiagnostics.MessagingSystemValue);
        activity.SetTag(DekafDiagnostics.MessagingDestinationName, "my-topic");
        activity.SetTag(DekafDiagnostics.MessagingOperationType, "receive");
        activity.SetTag(DekafDiagnostics.MessagingDestinationPartitionId, "0");
        activity.SetTag(DekafDiagnostics.MessagingMessageOffset, "100");
        activity.SetTag(DekafDiagnostics.MessagingConsumerGroupName, "my-group");

        var tags = activity.Tags.ToDictionary(t => t.Key, t => t.Value);
        await Assert.That(tags["messaging.system"]).IsEqualTo("kafka");
        await Assert.That(tags["messaging.destination.name"]).IsEqualTo("my-topic");
        await Assert.That(tags["messaging.operation.type"]).IsEqualTo("receive");
        await Assert.That(tags["messaging.destination.partition.id"]).IsEqualTo("0");
        await Assert.That(tags["messaging.kafka.message.offset"]).IsEqualTo("100");
        await Assert.That(tags["messaging.kafka.consumer.group"]).IsEqualTo("my-group");
    }

    [Test]
    public async Task ConsumerSpan_WithProducerLink_HasSpanLink()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // Create a producer span to get a trace context, then stop it
        // (simulating the producer being a separate process/service)
        ActivityTraceId producerTraceId;
        ActivitySpanId producerSpanId;
        Dekaf.Serialization.Headers headers;
        {
            using var producerActivity = DekafDiagnostics.Source.StartActivity("orders publish", ActivityKind.Producer);
            await Assert.That(producerActivity).IsNotNull();
            producerTraceId = producerActivity!.TraceId;
            producerSpanId = producerActivity.SpanId;

            // Inject trace context into headers (simulating producer behavior)
            var injected = TraceContextPropagator.InjectTraceContext(null, producerActivity);
            await Assert.That(injected).IsNotNull();
            headers = injected!;
        }

        // Clear Activity.Current so consumer span starts its own trace
        Activity.Current = null;

        // Extract trace context (simulating consumer behavior)
        var producerContext = TraceContextPropagator.ExtractTraceContext(headers.ToList());
        await Assert.That(producerContext).IsNotNull();

        // Consumer creates span with link (not parent-child) per OTel conventions
        var links = new[] { new ActivityLink(producerContext!.Value) };
        using var consumerActivity = DekafDiagnostics.Source.StartActivity(
            "orders receive",
            ActivityKind.Consumer,
            parentContext: default(ActivityContext),
            tags: null,
            links: links);
        await Assert.That(consumerActivity).IsNotNull();

        // Verify the span link exists and points to the producer's trace
        await Assert.That(consumerActivity!.Links.Count()).IsEqualTo(1);
        var link = consumerActivity.Links.First();
        await Assert.That(link.Context.TraceId.ToString())
            .IsEqualTo(producerTraceId.ToString());
        await Assert.That(link.Context.SpanId.ToString())
            .IsEqualTo(producerSpanId.ToString());

        // Verify the consumer span has its own trace (not child of producer)
        await Assert.That(consumerActivity.TraceId.ToString())
            .IsNotEqualTo(producerTraceId.ToString());
    }

    [Test]
    public async Task ConsumerSpan_WithoutProducerContext_HasNoLinks()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // Consumer span without extracted producer context (no traceparent header)
        using var consumerActivity = DekafDiagnostics.Source.StartActivity(
            "orders receive",
            ActivityKind.Consumer,
            parentContext: default(ActivityContext),
            tags: null,
            links: null);
        await Assert.That(consumerActivity).IsNotNull();
        await Assert.That(consumerActivity!.Links.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task MessagingMessageKey_ConstantHasCorrectValue()
    {
        string value = DekafDiagnostics.MessagingMessageKey;
        await Assert.That(value).IsEqualTo("messaging.kafka.message.key");
    }

    [Test]
    public async Task ProducerSpan_WithByteArrayKey_DoesNotSetKeyAttribute()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = DekafDiagnostics.Source.StartActivity("my-topic publish", ActivityKind.Producer);
        await Assert.That(activity).IsNotNull();

        // Simulate the producer's key-handling logic with a byte[] key.
        // The producer skips byte[] keys because their ToString() output
        // ("System.Byte[]") is not meaningful.
        object key = new byte[] { 0x01, 0x02, 0x03 };
        if (key is string stringKey)
            activity!.SetTag(DekafDiagnostics.MessagingMessageKey, stringKey);
        else if (key is not null and not byte[])
            activity!.SetTag(DekafDiagnostics.MessagingMessageKey, key.ToString());

        // Verify the key tag is NOT set for byte[] keys
        var keyTag = activity!.GetTagItem(DekafDiagnostics.MessagingMessageKey);
        await Assert.That(keyTag).IsNull();
    }

    [Test]
    public async Task ProducerSpan_WithIntKey_SetsKeyToStringRepresentation()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = DekafDiagnostics.Source.StartActivity("my-topic publish", ActivityKind.Producer);
        await Assert.That(activity).IsNotNull();

        // Simulate the producer's key-handling logic with an int key.
        // Non-string, non-byte[] keys use ToString() which produces a meaningful value.
        object key = 42;
        if (key is string stringKey)
            activity!.SetTag(DekafDiagnostics.MessagingMessageKey, stringKey);
        else if (key is not null and not byte[])
            activity!.SetTag(DekafDiagnostics.MessagingMessageKey, key.ToString());

        // Verify the key tag is set to the string representation of the int
        var tags = activity!.Tags.ToDictionary(t => t.Key, t => t.Value);
        await Assert.That(tags).ContainsKey("messaging.kafka.message.key");
        await Assert.That(tags["messaging.kafka.message.key"]).IsEqualTo("42");
    }
}
