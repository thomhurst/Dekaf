using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Dekaf.Consumer;
using Dekaf.Diagnostics;
using Microsoft.Extensions.Logging;
using ConfluentKafka = Confluent.Kafka;

namespace Dekaf.Tests.Integration;

[Category("Consumer")]
[NotInParallel("DekafInstrumentation")]
public sealed class MalformedTraceContextConsumerTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [SkipWhenNativeAot("Confluent.Kafka native delegate binding requires runtime reflection.")]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InvalidTracingHeaders_DoNotInterruptDelivery(bool streaming)
    {
        const string valid = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
        string[] traceparents =
        [
            "00-00000000000000000000000000000000-00f067aa0ba902b7-01",
            "00-4bf92f3577b34da6a3ce929d0e0e4736-0000000000000000-01",
            "00-4BF92F3577B34DA6A3CE929D0E0E4736-00f067aa0ba902b7-01",
            "ff" + valid[2..],
            "zz" + valid[2..],
            valid + "-extra",
            valid
        ];
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var activities = new ConcurrentBag<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                if (activity.GetTagItem(DekafDiagnostics.MessagingDestinationName) as string == topic &&
                    activity.Kind is ActivityKind.Consumer or ActivityKind.Client)
                    activities.Add(activity);
            }
        };
        ActivitySource.AddActivityListener(listener);

        // An independent producer preserves malformed headers instead of injecting Dekaf tracing.
        using var producer = new ConfluentKafka.ProducerBuilder<string, string>(new ConfluentKafka.ProducerConfig
        {
            BootstrapServers = KafkaContainer.BootstrapServers,
            Acks = ConfluentKafka.Acks.All
        }).Build();
        for (var index = 0; index < traceparents.Length; index++)
        {
            await producer.ProduceAsync(topic, new ConfluentKafka.Message<string, string>
            {
                Key = "key",
                Value = $"record-{index}",
                Headers = new ConfluentKafka.Headers
                {
                    { "traceparent", Encoding.UTF8.GetBytes(traceparents[index]) },
                    { "tracestate", "vendor=value"u8.ToArray() }
                }
            });
        }

        using var logs = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"trace-validation-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(loggerFactory)
            .BuildAsync();
        consumer.Assign(new TopicPartition(topic, 0));
        var received = new List<string?>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        if (streaming)
        {
            await foreach (var record in consumer.ConsumeAsync(cancellation.Token))
            {
                received.Add(record.Value);
                if (received.Count == traceparents.Length)
                    break;
            }
        }
        else
        {
            for (var index = 0; index < traceparents.Length; index++)
            {
                var record = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cancellation.Token);
                await Assert.That(record).IsNotNull();
                received.Add(record!.Value.Value);
            }
        }

        await Assert.That(received.Count).IsEqualTo(traceparents.Length);
        for (var index = 0; index < received.Count; index++)
            await Assert.That(received[index]).IsEqualTo($"record-{index}");
        await Assert.That(logs.Entries.Any(entry => entry.Message.StartsWith("Record parsing error", StringComparison.Ordinal))).IsFalse();
        await Assert.That(activities.Count).IsEqualTo(traceparents.Length);
        var linked = activities.SelectMany(static activity => activity.Links).ToArray();
        await Assert.That(linked.Length).IsEqualTo(1);
        await Assert.That(linked[0].Context.TraceId.ToHexString()).IsEqualTo("4bf92f3577b34da6a3ce929d0e0e4736");
        await Assert.That(linked[0].Context.TraceState).IsEqualTo("vendor=value");
    }
}
