using System.Diagnostics;
using Dekaf.Diagnostics;

namespace Dekaf.Tests.Unit.Diagnostics;

[NotInParallel("ActivityListener")]
public sealed class DekafDiagnosticsTests
{
    [Test]
    public async Task StartActivity_WithListener_ReturnsActivity()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = DekafDiagnostics.Source.StartActivity("send test", ActivityKind.Producer);
        await Assert.That(activity).IsNotNull();
        await Assert.That(activity!.OperationName).IsEqualTo("send test");
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

        using var activity = DekafDiagnostics.Source.StartActivity("send test-topic", ActivityKind.Producer);
        await Assert.That(activity).IsNotNull();

        activity!.SetTag(DekafDiagnostics.MessagingSystem, DekafDiagnostics.MessagingSystemValue);
        activity.SetTag(DekafDiagnostics.MessagingDestinationName, "test-topic");
        activity.SetTag(DekafDiagnostics.MessagingOperationName, DekafDiagnostics.OperationNameSend);
        activity.SetTag(DekafDiagnostics.MessagingOperationType, DekafDiagnostics.OperationTypeSend);

        var tags = activity.Tags.ToDictionary(t => t.Key, t => t.Value);
        await Assert.That(tags["messaging.system"]).IsEqualTo("kafka");
        await Assert.That(tags["messaging.destination.name"]).IsEqualTo("test-topic");
        await Assert.That(tags["messaging.operation.name"]).IsEqualTo("send");
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
    public async Task ProducerSpan_HasCorrectNamingConvention()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // OTel semantic convention: "{operation name} {destination}" for producer spans
        using var activity = DekafDiagnostics.Source.StartActivity("send orders", ActivityKind.Producer);
        await Assert.That(activity).IsNotNull();
        await Assert.That(activity!.OperationName).IsEqualTo("send orders");
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

        // OTel semantic convention: "{operation name} {destination}" for consumer spans
        using var activity = DekafDiagnostics.Source.StartActivity("poll orders", ActivityKind.Client);
        await Assert.That(activity).IsNotNull();
        await Assert.That(activity!.OperationName).IsEqualTo("poll orders");
        await Assert.That(activity.Kind).IsEqualTo(ActivityKind.Client);
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

        using var activity = DekafDiagnostics.Source.StartActivity("send my-topic", ActivityKind.Producer);
        await Assert.That(activity).IsNotNull();

        // Simulate what the producer does (using string values for tag retrieval compatibility)
        activity!.SetTag(DekafDiagnostics.MessagingSystem, DekafDiagnostics.MessagingSystemValue);
        activity.SetTag(DekafDiagnostics.MessagingDestinationName, "my-topic");
        activity.SetTag(DekafDiagnostics.MessagingOperationName, DekafDiagnostics.OperationNameSend);
        activity.SetTag(DekafDiagnostics.MessagingOperationType, DekafDiagnostics.OperationTypeSend);
        activity.SetTag(DekafDiagnostics.MessagingClientId, "my-producer");
        activity.SetTag(DekafDiagnostics.MessagingMessageKey, "order-123");
        activity.SetTag(DekafDiagnostics.MessagingDestinationPartitionId, "2");
        activity.SetTag(DekafDiagnostics.MessagingKafkaOffset, "42");
        activity.SetTag(DekafDiagnostics.MessagingMessageBodySize, 1024);

        var tags = activity.Tags.ToDictionary(t => t.Key, t => t.Value);
        await Assert.That(tags["messaging.system"]).IsEqualTo("kafka");
        await Assert.That(tags["messaging.destination.name"]).IsEqualTo("my-topic");
        await Assert.That(tags["messaging.operation.name"]).IsEqualTo("send");
        await Assert.That(tags["messaging.operation.type"]).IsEqualTo("send");
        await Assert.That(tags["messaging.client.id"]).IsEqualTo("my-producer");
        await Assert.That(tags["messaging.kafka.message.key"]).IsEqualTo("order-123");
        await Assert.That(tags["messaging.destination.partition.id"]).IsEqualTo("2");
        await Assert.That(tags["messaging.kafka.offset"]).IsEqualTo("42");
        await Assert.That(activity.GetTagItem("messaging.message.body.size")).IsEqualTo(1024);
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

        using var activity = DekafDiagnostics.Source.StartActivity("poll my-topic", ActivityKind.Client);
        await Assert.That(activity).IsNotNull();

        // Simulate what the consumer does (using string values for tag retrieval compatibility)
        activity!.SetTag(DekafDiagnostics.MessagingSystem, DekafDiagnostics.MessagingSystemValue);
        activity.SetTag(DekafDiagnostics.MessagingDestinationName, "my-topic");
        activity.SetTag(DekafDiagnostics.MessagingOperationName, DekafDiagnostics.OperationNamePoll);
        activity.SetTag(DekafDiagnostics.MessagingOperationType, DekafDiagnostics.OperationTypeReceive);
        activity.SetTag(DekafDiagnostics.MessagingClientId, "my-consumer");
        activity.SetTag(DekafDiagnostics.MessagingDestinationPartitionId, "0");
        activity.SetTag(DekafDiagnostics.MessagingKafkaOffset, "100");
        activity.SetTag(DekafDiagnostics.MessagingMessageBodySize, 512);
        activity.SetTag(DekafDiagnostics.MessagingConsumerGroupName, "my-group");

        var tags = activity.Tags.ToDictionary(t => t.Key, t => t.Value);
        await Assert.That(tags["messaging.system"]).IsEqualTo("kafka");
        await Assert.That(tags["messaging.destination.name"]).IsEqualTo("my-topic");
        await Assert.That(tags["messaging.operation.name"]).IsEqualTo("poll");
        await Assert.That(tags["messaging.operation.type"]).IsEqualTo("receive");
        await Assert.That(tags["messaging.client.id"]).IsEqualTo("my-consumer");
        await Assert.That(tags["messaging.destination.partition.id"]).IsEqualTo("0");
        await Assert.That(tags["messaging.kafka.offset"]).IsEqualTo("100");
        await Assert.That(activity.GetTagItem("messaging.message.body.size")).IsEqualTo(512);
        await Assert.That(tags["messaging.consumer.group.name"]).IsEqualTo("my-group");
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
            using var producerActivity = DekafDiagnostics.Source.StartActivity("send orders", ActivityKind.Producer);
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
            "poll orders",
            ActivityKind.Client,
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
            "poll orders",
            ActivityKind.Client,
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
    public async Task MessagingKafkaOffset_ConstantHasCorrectValue()
    {
        // Current semconv name; "messaging.kafka.message.offset" is the deprecated form.
        string value = DekafDiagnostics.MessagingKafkaOffset;
        await Assert.That(value).IsEqualTo("messaging.kafka.offset");
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

        using var activity = DekafDiagnostics.Source.StartActivity("send my-topic", ActivityKind.Producer);
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

        using var activity = DekafDiagnostics.Source.StartActivity("send my-topic", ActivityKind.Producer);
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

    [Test]
    public async Task RecordException_SetsStatusErrorTypeAndAddsEvent()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = DekafDiagnostics.Source.StartActivity("send test", ActivityKind.Producer);
        await Assert.That(activity).IsNotNull();

        var exception = new InvalidOperationException("test error");
        DekafDiagnostics.RecordException(activity!, exception);

        await Assert.That(activity!.Status).IsEqualTo(ActivityStatusCode.Error);
        await Assert.That(activity.StatusDescription).IsEqualTo("test error");

        // error.type is conditionally required on failed messaging spans
        await Assert.That(activity.GetTagItem("error.type"))
            .IsEqualTo(typeof(InvalidOperationException).FullName);

        var events = activity.Events.ToList();
        await Assert.That(events).Count().IsEqualTo(1);
        await Assert.That(events[0].Name).IsEqualTo("exception");

        var eventTags = events[0].Tags.ToDictionary(t => t.Key, t => t.Value);
        await Assert.That(eventTags["exception.type"]).IsEqualTo(typeof(InvalidOperationException).FullName);
        await Assert.That(eventTags["exception.message"]).IsEqualTo("test error");
        await Assert.That((string?)eventTags["exception.stacktrace"]).IsNotNull();
    }
}
