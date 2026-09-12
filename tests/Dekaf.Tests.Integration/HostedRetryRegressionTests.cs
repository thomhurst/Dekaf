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

    private sealed class RoutingService(IKafkaConsumer<byte[]?, byte[]?> consumer, string topic, DeadLetterOptions options)
        : KafkaConsumerService<byte[]?, byte[]?>(consumer, NullLogger.Instance, options,
            serviceOptions: new KafkaConsumerServiceOptions { DrainOnShutdown = false })
    {
        protected override IEnumerable<string> Topics => [topic];
        protected override ValueTask ProcessAsync(ConsumeResult<byte[]?, byte[]?> result, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Route the original record bytes.");
    }
}
