using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Integration tests for metadata refresh and partition discovery.
/// Verifies that producers can discover newly created topics, consumers can discover
/// expanded partitions, and short metadata max age triggers automatic refreshes.
/// </summary>
public sealed class MetadataRecoveryTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task Producer_MetadataRefresh_FindsNewlyCreatedTopic()
    {
        // Arrange - create producer BEFORE creating the topic
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-metadata-refresh")
            .WithMetadataMaxAge(TimeSpan.FromSeconds(5))
            .Build();

        // Create the topic after producer is built
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        // Act - produce to the newly created topic (metadata refresh should discover it)
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value-to-new-topic"
        }).ConfigureAwait(false);

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Consumer_DiscoverNewPartitions_AfterExpansion()
    {
        // Arrange - create topic with 2 partitions
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2).ConfigureAwait(false);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-partition-expand")
            .Build();

        // Produce to original partitions
        for (var p = 0; p < 2; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-p{p}",
                Value = $"value-original-p{p}",
                Partition = p
            }).ConfigureAwait(false);
        }

        // Expand partitions from 2 to 4 using admin client
        await using var adminClient = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        await adminClient.CreatePartitionsAsync(new Dictionary<string, int>
        {
            [topic] = 4 // Expand to 4 partitions
        }).ConfigureAwait(false);

        // Wait for partition expansion to propagate
        await Task.Delay(5000).ConfigureAwait(false);

        // Produce to the new partitions
        for (var p = 2; p < 4; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-p{p}",
                Value = $"value-expanded-p{p}",
                Partition = p
            }).ConfigureAwait(false);
        }

        // Act - consumer should discover all 4 partitions
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-partition-expand")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        var partitionsSeen = new HashSet<int>();
        var consumed = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token).ConfigureAwait(false))
        {
            consumed.Add(msg);
            partitionsSeen.Add(msg.Partition);
            if (consumed.Count >= 4) break;
        }

        // Assert - should see messages from all 4 partitions
        await Assert.That(consumed).Count().IsEqualTo(4);
        await Assert.That(partitionsSeen.Count).IsEqualTo(4);
    }

    [Test]
    public async Task Producer_ShortMetadataMaxAge_RefreshesAutomatically()
    {
        // Arrange - producer with very short metadata max age
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-short-metadata-age")
            .WithMetadataMaxAge(TimeSpan.FromSeconds(5))
            .Build();

        // Create first topic and produce
        var topic1 = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var metadata1 = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic1,
            Key = "key1",
            Value = "value1"
        }).ConfigureAwait(false);

        await Assert.That(metadata1.Topic).IsEqualTo(topic1);

        // Wait for metadata to become stale
        await Task.Delay(6000).ConfigureAwait(false);

        // Create a second topic after metadata has expired
        var topic2 = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        // Act - produce to the second topic (requires metadata refresh)
        var metadata2 = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic2,
            Key = "key2",
            Value = "value2"
        }).ConfigureAwait(false);

        // Assert - should successfully produce to both topics
        await Assert.That(metadata2.Topic).IsEqualTo(topic2);
        await Assert.That(metadata2.Offset).IsGreaterThanOrEqualTo(0);
    }
}
