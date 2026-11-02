using Dekaf.Admin;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Integration;

[Category("ShareConsumer")]
[SupportsKafka(420)]
[NotInParallel("ShareConsumerKafka42")]
public sealed class ShareConsumerEmptyBrokerMetadataTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    // #4039: a graceful broker shutdown can answer Metadata with zero brokers. The share
    // coordinator must repair that cache through bootstrap and join instead of faulting.
    [Test]
    [Timeout(120_000)]
    public async Task ShareConsumer_EmptyBrokerMetadata_RefreshesBootstrapAndDelivers(CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var group = $"share-empty-metadata-{Guid.NewGuid():N}";
        await using (var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).Build())
        {
            await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
            {
                [new ConfigResource { Type = ConfigResourceType.Group, Name = group }] =
                    [ConfigAlter.Set("share.auto.offset.reset", "earliest")]
            }, cancellationToken: cancellationToken);
        }
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync(cancellationToken);
        await producer.ProduceAsync(topic, "key", "after-empty-metadata", cancellationToken);

        await using var pool = new ConnectionPool(group, new ConnectionOptions());
        await using var metadata = new MetadataManager(pool, [KafkaContainer.BootstrapServers]);
        await using var consumer = new KafkaShareConsumer<string, string>(new ShareConsumerOptions
        {
            BootstrapServers = [KafkaContainer.BootstrapServers],
            ClientId = group,
            GroupId = group
        }, Serializers.String, Serializers.String, pool, metadata);
        await consumer.InitializeAsync(cancellationToken);

        // Apply the successful zero-broker response observed during graceful shutdown.
        metadata.Metadata.Update(new MetadataResponse { Brokers = [], Topics = [] });
        consumer.Subscribe(topic);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        string? value = null;
        await foreach (var record in consumer.PollAsync(timeout.Token))
        {
            value = record.Value;
            consumer.Acknowledge(record);
            break;
        }

        await Assert.That(value).IsEqualTo("after-empty-metadata");
        await consumer.CommitAsync(cancellationToken);
    }
}
