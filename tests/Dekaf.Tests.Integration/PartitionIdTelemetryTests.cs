using System.Collections.Concurrent;
using System.Diagnostics;
using Dekaf.Consumer;
using Dekaf.Diagnostics;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// The OTel messaging conventions type messaging.destination.partition.id as a string, so
/// backends that type attributes can match Dekaf spans against other Kafka instrumentation.
/// </summary>
[Category("Producer")]
[NotInParallel("DekafInstrumentation")]
public sealed class PartitionIdTelemetryTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task SendAndConsumeSpans_TagPartitionIdAsString()
    {
        const int partition = 2;
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var sendSpans = new ConcurrentBag<Activity>();
        var consumeSpans = new ConcurrentBag<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                if (activity.GetTagItem(DekafDiagnostics.MessagingDestinationName) as string != topic)
                    return;
                if (activity.Kind == ActivityKind.Producer)
                    sendSpans.Add(activity);
                else if (activity.Kind is ActivityKind.Consumer or ActivityKind.Client)
                    consumeSpans.Add(activity);
            }
        };
        ActivitySource.AddActivityListener(listener);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Partition = partition,
            Key = "key",
            Value = "value"
        });
        await Assert.That(metadata.Partition).IsEqualTo(partition);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"partition-id-telemetry-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        consumer.Assign(new TopicPartition(topic, partition));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var record = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cancellation.Token);
        await Assert.That(record).IsNotNull();

        await Assert.That(sendSpans.Count).IsEqualTo(1);
        await Assert.That(consumeSpans.Count).IsEqualTo(1);
        await Assert.That(sendSpans.Single().GetTagItem(DekafDiagnostics.MessagingDestinationPartitionId)).IsEqualTo("2");
        await Assert.That(consumeSpans.Single().GetTagItem(DekafDiagnostics.MessagingDestinationPartitionId)).IsEqualTo("2");
    }
}
