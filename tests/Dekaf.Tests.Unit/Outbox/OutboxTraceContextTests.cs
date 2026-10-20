using System.Diagnostics;
using Dekaf.Diagnostics;
using Dekaf.Outbox;
using Dekaf.Producer;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Outbox;

[NotInParallel("ActivityListener")]
public sealed class OutboxTraceContextTests
{
    private const string HeaderName = "x-outbox-message-id";
    private const string StoredTraceparent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";

    private static RecordMetadata Metadata { get; } = new()
    {
        Topic = "topic",
        Partition = 0,
        Offset = 1,
        Timestamp = DateTimeOffset.UnixEpoch
    };

    [Test]
    public async Task Create_WithCurrentActivity_StoresTraceContextHeaders()
    {
        using var activity = new Activity("enqueue").SetIdFormat(ActivityIdFormat.W3C);
        activity.TraceStateString = "vendor=value";
        activity.Start();

        var message = OutboxMessage.Create("topic", "key", "value", Serializers.String, Serializers.String);

        var headers = OutboxHeaderCodec.Decode(message.Headers)!;
        await Assert.That(headers.GetFirstAsString("traceparent")).IsEqualTo(activity.Id);
        await Assert.That(headers.GetFirstAsString("tracestate")).IsEqualTo("vendor=value");
    }

    [Test]
    public async Task Create_WithoutCurrentActivity_StoresNoHeaders()
    {
        // TUnit runs each test under its own Activity.
        Activity.Current = null;

        var message = OutboxMessage.Create("topic", "key", "value", Serializers.String, Serializers.String);

        await Assert.That(message.Headers).IsNull();
    }

    [Test]
    public async Task Create_WithCallerTraceparent_KeepsCallerContextAndHeaders()
    {
        using var activity = new Activity("enqueue").SetIdFormat(ActivityIdFormat.W3C).Start();
        var callerHeaders = new Headers().Add("traceparent", StoredTraceparent);

        var message = OutboxMessage.Create(
            "topic", "key", "value", Serializers.String, Serializers.String, callerHeaders);

        var headers = OutboxHeaderCodec.Decode(message.Headers)!;
        await Assert.That(headers.Count).IsEqualTo(1);
        await Assert.That(headers.GetFirstAsString("traceparent")).IsEqualTo(StoredTraceparent);
    }

    [Test]
    public async Task Create_WithCurrentActivity_DoesNotModifyCallerHeaders()
    {
        using var activity = new Activity("enqueue").SetIdFormat(ActivityIdFormat.W3C).Start();
        var callerHeaders = new Headers().Add("tenant", "a");

        var message = OutboxMessage.Create(
            "topic", "key", "value", Serializers.String, Serializers.String, callerHeaders);

        await Assert.That(callerHeaders.Count).IsEqualTo(1);
        var headers = OutboxHeaderCodec.Decode(message.Headers)!;
        await Assert.That(headers.GetFirstAsString("tenant")).IsEqualTo("a");
        await Assert.That(headers.GetFirstAsString("traceparent")).IsEqualTo(activity.Id);
    }

    [Test]
    public async Task Publish_WithListener_ParentsRelaySpanToStoredContextAndDropsStoredHeaders()
    {
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stopped.Add
        };
        ActivitySource.AddActivityListener(listener);

        Activity? currentDuringProduce = null;
        Headers? producedHeaders = null;
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                currentDuringProduce = Activity.Current;
                producedHeaders = callInfo.Arg<ProducerMessage<byte[]?, byte[]?>>()!.Headers;
                return new ValueTask<RecordMetadata>(Metadata);
            });
        await using var publisher = new DekafOutboxPublisher(producer, ownsProducer: false);

        var row = Row(new Headers()
            .Add("traceparent", StoredTraceparent)
            .Add("tracestate", "vendor=value")
            .Add("tenant", "a"));
        var callerActivity = Activity.Current;

        var result = await publisher.PublishAsync([row], HeaderName);

        await Assert.That(result.AckedCount).IsEqualTo(1);
        await Assert.That(Activity.Current).IsEqualTo(callerActivity);
        await Assert.That(currentDuringProduce).IsNotNull();
        var relay = currentDuringProduce!;
        await Assert.That(relay.OperationName).IsEqualTo("outbox relay topic");
        await Assert.That(relay.TraceId.ToHexString()).IsEqualTo("0af7651916cd43dd8448eb211c80319c");
        await Assert.That(relay.ParentSpanId.ToHexString()).IsEqualTo("b7ad6b7169203331");
        await Assert.That(relay.TraceStateString).IsEqualTo("vendor=value");
        await Assert.That(relay.GetTagItem("messaging.message.id")).IsEqualTo(row.MessageId.ToString("D"));
        await Assert.That(relay.Status).IsEqualTo(ActivityStatusCode.Ok);
        await Assert.That(stopped).Contains(relay);

        // The producer injects its send span; the stored context must not be published twice.
        await Assert.That(producedHeaders!.GetFirst("traceparent")).IsNull();
        await Assert.That(producedHeaders.GetFirst("tracestate")).IsNull();
        await Assert.That(producedHeaders.GetFirstAsString("tenant")).IsEqualTo("a");
    }

    [Test]
    public async Task Publish_WithListener_FailedProduce_RecordsErrorOnRelaySpan()
    {
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stopped.Add
        };
        ActivitySource.AddActivityListener(listener);

        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException<RecordMetadata>(new InvalidOperationException("rejected")));
        await using var publisher = new DekafOutboxPublisher(producer, ownsProducer: false);

        var result = await publisher.PublishAsync(
            [Row(new Headers().Add("traceparent", StoredTraceparent))], HeaderName);

        await Assert.That(result.AckedCount).IsEqualTo(0);
        var relay = stopped.Single(activity => activity.OperationName == "outbox relay topic");
        await Assert.That(relay.Status).IsEqualTo(ActivityStatusCode.Error);
        await Assert.That(relay.GetTagItem("error.type")).IsEqualTo(typeof(InvalidOperationException).FullName);
    }

    [Test]
    public async Task Publish_WithListener_InvalidStoredTraceparent_PublishesHeadersUnchanged()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        Activity? currentDuringProduce = null;
        Headers? producedHeaders = null;
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                currentDuringProduce = Activity.Current;
                producedHeaders = callInfo.Arg<ProducerMessage<byte[]?, byte[]?>>()!.Headers;
                return new ValueTask<RecordMetadata>(Metadata);
            });
        await using var publisher = new DekafOutboxPublisher(producer, ownsProducer: false);
        Activity.Current = null;

        var result = await publisher.PublishAsync([Row(new Headers().Add("traceparent", "garbage"))], HeaderName);

        await Assert.That(result.AckedCount).IsEqualTo(1);
        await Assert.That(currentDuringProduce).IsNull();
        await Assert.That(producedHeaders!.GetFirstAsString("traceparent")).IsEqualTo("garbage");
    }

    private static OutboxMessage Row(Headers headers) => new()
    {
        Id = 1,
        MessageId = Guid.NewGuid(),
        Bucket = 0,
        Topic = "topic",
        Key = [1],
        Value = [2],
        Headers = OutboxHeaderCodec.Encode(headers),
        CreatedAtUtc = new DateTimeOffset(2026, 7, 22, 0, 0, 0, TimeSpan.Zero)
    };
}
