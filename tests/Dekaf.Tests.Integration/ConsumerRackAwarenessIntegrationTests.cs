using Dekaf.Consumer;
using Dekaf.Producer;
using Microsoft.Extensions.Logging;

namespace Dekaf.Tests.Integration;

[Category("Consumer")]
[NotInParallel("RackAwareKafkaContainer")]
[ClassDataSource<RackAwareKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class ConsumerRackAwarenessIntegrationTests(RackAwareKafkaContainer kafka)
{
    [Test]
    public async Task Consumer_WithClientRack_FetchesFromPreferredReadReplica()
    {
        var topic = await kafka.CreateTopicWithRemoteLeaderAndLocalFollowerAsync().ConfigureAwait(false);

        await ProduceAsync(kafka.BootstrapServers, topic, key: "first", value: "first").ConfigureAwait(false);

        using var logs = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(logs);
        });

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId($"rack-aware-consumer-{Guid.NewGuid():N}")
            .WithClientRack("rack-a")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(loggerFactory)
            .BuildAsync()
            .ConfigureAwait(false);

        consumer.Assign(new TopicPartition(topic, 0));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var first = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);
        await Assert.That(first).IsNotNull();
        await Assert.That(first!.Value.Value).IsEqualTo("first");

        await ProduceAsync(kafka.BootstrapServers, topic, key: "second", value: "second").ConfigureAwait(false);

        var second = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);
        await Assert.That(second).IsNotNull();
        await Assert.That(second!.Value.Value).IsEqualTo("second");

        await WaitForPreferredReadReplicaLogAsync(logs, topic).ConfigureAwait(false);
    }

    private static async Task ProduceAsync(string bootstrapServers, string topic, string key, string value)
    {
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(bootstrapServers)
            .WithClientId($"rack-aware-producer-{Guid.NewGuid():N}")
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync()
            .ConfigureAwait(false);

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Partition = 0,
            Key = key,
            Value = value
        }, CancellationToken.None).ConfigureAwait(false);

        await producer.FlushWithTimeoutAsync().ConfigureAwait(false);
    }

    private static async Task WaitForPreferredReadReplicaLogAsync(
        CapturingLoggerProvider logs,
        string topic)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (logs.Entries.Any(entry =>
                entry.Message.Contains($"Fetching {topic}-0 from preferred read replica 2 instead of leader 1",
                    StringComparison.Ordinal)))
            {
                return;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        throw new InvalidOperationException("Consumer did not fetch from preferred read replica 2.");
    }
}
