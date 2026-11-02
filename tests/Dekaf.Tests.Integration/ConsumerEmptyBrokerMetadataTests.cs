using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

[Category("ConsumerGroup")]
public sealed class ConsumerEmptyBrokerMetadataTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [Timeout(120_000)]
    public async Task Consumer_EmptyBrokerMetadata_RefreshesBootstrapAndDelivers(CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync(cancellationToken);
        await producer.ProduceAsync(topic, "key", "after-empty-metadata", cancellationToken);

        var clientId = $"empty-metadata-{Guid.NewGuid():N}";
        await using var pool = new ConnectionPool(clientId, new ConnectionOptions());
        await using var metadata = new MetadataManager(pool, [KafkaContainer.BootstrapServers]);
        await using var consumer = new KafkaConsumer<string, string>(new ConsumerOptions
        {
            BootstrapServers = [KafkaContainer.BootstrapServers],
            ClientId = clientId,
            GroupId = clientId,
            OffsetCommitMode = OffsetCommitMode.Manual,
            AutoOffsetReset = AutoOffsetReset.Earliest
        }, Serializers.String, Serializers.String, pool, metadata);
        await consumer.InitializeAsync(cancellationToken);

        // Apply the successful zero-broker response observed during graceful shutdown.
        // Discovery must repair this cache through the real bootstrap and join protocols.
        metadata.Metadata.Update(new MetadataResponse { Brokers = [], Topics = [] });
        consumer.Subscribe(topic);
        var message = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cancellationToken);

        await Assert.That(message).IsNotNull();
        await Assert.That(message!.Value.Value).IsEqualTo("after-empty-metadata");
        await consumer.CommitAsync([new TopicPartitionOffset(topic, 0, message.Value.Offset + 1)], cancellationToken);
    }
}
