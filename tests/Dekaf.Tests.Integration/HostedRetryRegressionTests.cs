using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dekaf.Tests.Integration;

[Category("Messaging")]
public sealed class HostedRetryRegressionTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    [Timeout(90_000)]
    public async Task FailureRouting_PreservesWireBytes(bool retry, CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var target = topic + (retry ? "-retry-5m" : ".DLQ");
        await KafkaContainer.CreateTopicAsync(target, partitions: 1);
        await using var producer = await Kafka.CreateProducer<byte[]?, byte[]?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync(cancellationToken);
        var consumer = await Kafka.CreateConsumer<byte[]?, byte[]?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"routing-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest).BuildAsync(cancellationToken);
        await using var service = new RoutingService(consumer, topic, new DeadLetterOptions
        {
            BootstrapServers = KafkaContainer.BootstrapServers,
            RetryTopics = retry ? new RetryTopicOptions { Delays = [TimeSpan.FromMinutes(5)] } : null
        });
        byte[]?[] payloads = [null, [], [0, 128, 255]];
        await service.StartAsync(cancellationToken);
        try
        {
            foreach (var payload in payloads)
                await producer.ProduceAsync(topic, payload, payload, cancellationToken);

            await using var verifier = await Kafka.CreateConsumer<byte[]?, byte[]?>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithAutoOffsetReset(AutoOffsetReset.Earliest).BuildAsync(cancellationToken);
            verifier.Assign(new TopicPartition(target, 0));
            foreach (var expected in payloads)
            {
                var record = await verifier.ConsumeOneAsync(TimeSpan.FromSeconds(30), cancellationToken);
                await Assert.That(record).IsNotNull();
                if (expected is null)
                {
                    await Assert.That(record!.Value.Key).IsNull();
                    await Assert.That(record.Value.Value).IsNull();
                }
                else
                {
                    await Assert.That(record!.Value.Key).IsNotNull();
                    await Assert.That(record.Value.Value).IsNotNull();
                    await Assert.That(record.Value.Key!.AsSpan().SequenceEqual(expected)).IsTrue();
                    await Assert.That(record.Value.Value!.AsSpan().SequenceEqual(expected)).IsTrue();
                }
            }
        }
        finally
        {
            await service.StopAsync(cancellationToken);
        }
    }

    [Test]
    [Timeout(90_000)]
    public async Task ReplayedRetryHeadersOnSourceTopic_AreIgnored(CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var retryTopic = topic + "-retry-5m";
        await KafkaContainer.CreateTopicAsync(retryTopic, partitions: 1);
        await using var producer = await Kafka.CreateProducer<byte[]?, byte[]?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync(cancellationToken);
        var consumer = await Kafka.CreateConsumer<byte[]?, byte[]?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"replay-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest).BuildAsync(cancellationToken);
        await using var service = new RoutingService(consumer, topic, new DeadLetterOptions
        {
            BootstrapServers = KafkaContainer.BootstrapServers,
            RetryTopics = new RetryTopicOptions { Delays = [TimeSpan.FromMinutes(5)] }
        });
        await service.StartAsync(cancellationToken);
        try
        {
            // A retry copy replayed to its source topic with its headers intact. Before the fix,
            // the future due time paused the source partition for a day, and the forged source
            // topic and failure count chose an unsubscribed destination.
            var dueAt = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeMilliseconds();
            await producer.ProduceAsync(new Dekaf.Producer.ProducerMessage<byte[]?, byte[]?>
            {
                Topic = topic,
                Key = [1],
                Value = [2],
                Headers = new Dekaf.Serialization.Headers()
                    .Add("app", "kept")
                    .Add(RetryTopicHeaders.SourceTopicKey, "unrelated")
                    .Add(RetryTopicHeaders.FailureCountKey, "9")
                    .Add(RetryTopicHeaders.DueTimestampMsKey, dueAt.ToString(System.Globalization.CultureInfo.InvariantCulture))
            }, cancellationToken);

            await using var verifier = await Kafka.CreateConsumer<byte[]?, byte[]?>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithAutoOffsetReset(AutoOffsetReset.Earliest).BuildAsync(cancellationToken);
            verifier.Assign(new TopicPartition(retryTopic, 0));
            var record = await verifier.ConsumeOneAsync(TimeSpan.FromSeconds(30), cancellationToken);

            await Assert.That(record).IsNotNull();
            var headers = record!.Value.Headers;
            await Assert.That(headers.First(header => header.Key == "app").GetValueAsString()).IsEqualTo("kept");
            await Assert.That(RetryTopicHeaders.GetSourceTopic(headers)).IsEqualTo(topic);
            await Assert.That(RetryTopicHeaders.GetFailureCount(headers)).IsEqualTo(1);
            await Assert.That(service.ProcessedCount).IsEqualTo(1);
        }
        finally
        {
            await service.StopAsync(cancellationToken);
        }
    }

    private sealed class RoutingService(IKafkaConsumer<byte[]?, byte[]?> consumer, string topic, DeadLetterOptions options)
        : KafkaConsumerService<byte[]?, byte[]?>(consumer, NullLogger.Instance, options,
            serviceOptions: new KafkaConsumerServiceOptions { DrainOnShutdown = false })
    {
        private int _processedCount;

        public int ProcessedCount => Volatile.Read(ref _processedCount);

        protected override IEnumerable<string> Topics => [topic];
        protected override ValueTask ProcessAsync(ConsumeResult<byte[]?, byte[]?> result, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _processedCount);
            throw new InvalidOperationException("Route the original record bytes.");
        }
    }
}
