using System.Diagnostics;
using Dekaf.Consumer;
using Dekaf.Diagnostics;
using Dekaf.Outbox;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

[Category("MessagingPatterns")]
[NotInParallel]
public sealed class OutboxTracePropagationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    private const string StoredTraceparent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";

    [Test]
    [Arguments(false, false, false, false)]
    [Arguments(true, false, false, false)]
    [Arguments(true, true, false, false)]
    [Arguments(true, false, true, false)]
    [Arguments(true, true, true, false)]
    [Arguments(true, true, true, true)]
    [Timeout(90_000)]
    public async Task Publish_PreservesFallbackUntilSendContextIsInjected(
        bool listen, bool sampleRelay, bool sampleSend, bool removeListener, CancellationToken cancellationToken)
    {
        using var isolation = new ActivityListenerIsolation();
        var topic = $"outbox-propagation-{Guid.NewGuid():N}";
        await KafkaContainer.CreateTopicAsync(topic, partitions: 1);
        var producer = Kafka.CreateProducer<byte[]?, byte[]?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        await using var publisher = new DekafOutboxPublisher(producer);
        await publisher.InitializeAsync(cancellationToken);
        // Finish network initialization before testing the synchronous sampling boundary.
        _ = await producer.GetPartitionsForAsync(topic, cancellationToken);
        Activity? relay = null;
        Activity? send = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => listen && source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                (options.Kind == ActivityKind.Producer ? sampleSend : sampleRelay)
                    ? ActivitySamplingResult.AllDataAndRecorded : ActivitySamplingResult.None
        };
        listener.ActivityStarted = activity =>
        {
            if (activity.OperationName == $"outbox relay {topic}")
            {
                relay = activity;
                // Deterministic listener loss after publisher extraction, before producer sampling.
                if (removeListener)
                    listener.Dispose();
            }
            else if (activity.Kind == ActivityKind.Producer)
                send = activity;
        };
        ActivitySource.AddActivityListener(listener);
        var callerHeaders = new Headers().Add("traceparent", StoredTraceparent)
            .Add("tracestate", "vendor=value").Add("tenant", "a");
        var row = new OutboxMessage
        {
            Id = 1, MessageId = Guid.NewGuid(), Bucket = 0, Topic = topic,
            Key = [1], Value = [2], Headers = OutboxHeaderCodec.Encode(callerHeaders),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        var originalBlob = row.Headers!.ToArray();

        var result = await publisher.PublishAsync([row], OutboxRelayOptions.DefaultMessageIdHeaderName, cancellationToken);

        await Assert.That(result.FirstError).IsNull();
        await Assert.That(result.AckedCount).IsEqualTo(1);
        await Assert.That(row.Headers!.SequenceEqual(originalBlob)).IsTrue();
        await Assert.That(callerHeaders.Count).IsEqualTo(3);
        await Assert.That(callerHeaders.GetFirstAsString("traceparent")).IsEqualTo(StoredTraceparent);
        await using var consumer = await Kafka.CreateConsumer<byte[], byte[]>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"outbox-propagation-reader-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest).BuildAsync(cancellationToken);
        consumer.Assign(new TopicPartition(topic, 0));
        await using var records = consumer.ConsumeAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        await Assert.That(await records.MoveNextAsync()).IsTrue();
        var headers = records.Current.Headers;
        await Assert.That(headers.Count(header => header.Key == "traceparent")).IsEqualTo(1);
        await Assert.That(headers.Count(header => header.Key == "tracestate")).IsEqualTo(1);
        await Assert.That(headers.Single(header => header.Key == "tracestate").GetValueAsString()).IsEqualTo("vendor=value");
        await Assert.That(headers.Single(header => header.Key == "tenant").GetValueAsString()).IsEqualTo("a");
        var traceparent = headers.Single(header => header.Key == "traceparent").GetValueAsString();
        if (listen && sampleSend && !removeListener)
        {
            await Assert.That(send).IsNotNull();
            await Assert.That(traceparent).IsEqualTo(send!.Id);
            await Assert.That(send.TraceId.ToHexString()).IsEqualTo("0af7651916cd43dd8448eb211c80319c");
            await Assert.That(send.ParentSpanId).IsEqualTo(relay?.SpanId ?? ActivitySpanId.CreateFromString("b7ad6b7169203331"));
        }
        else
        {
            await Assert.That(send).IsNull();
            await Assert.That(traceparent).IsEqualTo(StoredTraceparent);
        }
    }
}
