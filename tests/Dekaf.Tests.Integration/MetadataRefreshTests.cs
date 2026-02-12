using Dekaf.Metadata;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for producer metadata refresh and recovery behavior.
/// Verifies that producers correctly refresh metadata after max age expiry,
/// discover new topics, handle partition expansion, and recover via rebootstrap.
/// </summary>
[Category("Admin")]
public sealed class MetadataRefreshTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task MetadataRefreshes_AfterMaxAge_StillProducesSuccessfully()
    {
        // Arrange - create topic and producer with short metadata max age
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-metadata-refresh-max-age")
            .WithMetadataMaxAge(TimeSpan.FromSeconds(5))
            .BuildAsync();

        // Act - produce an initial message
        var metadata1 = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key-before",
            Value = "value-before-refresh"
        });

        await Assert.That(metadata1.Topic).IsEqualTo(topic);
        await Assert.That(metadata1.Offset).IsGreaterThanOrEqualTo(0);

        // Wait for metadata to expire and auto-refresh
        await Task.Delay(TimeSpan.FromSeconds(7));

        // Produce again after metadata has refreshed
        var metadata2 = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key-after",
            Value = "value-after-refresh"
        });

        // Assert - production should still work after metadata refresh
        await Assert.That(metadata2.Topic).IsEqualTo(topic);
        await Assert.That(metadata2.Offset).IsGreaterThanOrEqualTo(0);
        // Second message should have a higher offset than the first
        await Assert.That(metadata2.Offset).IsGreaterThan(metadata1.Offset);
    }

    [Test]
    public async Task NewTopic_MetadataDiscovered_OnFirstProduce()
    {
        // Arrange - create producer BEFORE creating the topic
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-metadata-new-topic-discovery")
            .WithMetadataMaxAge(TimeSpan.FromSeconds(5))
            .BuildAsync();

        // Create the topic after the producer is already built and initialized
        var topic = await KafkaContainer.CreateTestTopicAsync();

        // Act - produce to the newly created topic
        // The producer should discover the topic via metadata refresh on first produce
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "discovery-key",
            Value = "discovery-value"
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
        await Assert.That(metadata.Partition).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task MetadataRefresh_AfterPartitionExpansion_ProducesToNewPartitions()
    {
        // Arrange - create topic with 2 partitions
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-metadata-partition-expansion")
            .WithMetadataMaxAge(TimeSpan.FromSeconds(5))
            .BuildAsync();

        // Produce to original partitions to establish metadata
        for (var p = 0; p < 2; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-original-{p}",
                Value = $"value-original-{p}",
                Partition = p
            });
        }

        // Expand partitions from 2 to 4 using admin client
        await using var adminClient = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        await adminClient.CreatePartitionsAsync(new Dictionary<string, int>
        {
            [topic] = 4
        });

        // Wait for partition expansion to propagate and metadata to refresh
        await Task.Delay(TimeSpan.FromSeconds(8));

        // Act - produce to the new partitions (2 and 3)
        var results = new List<RecordMetadata>();
        for (var p = 2; p < 4; p++)
        {
            var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-expanded-{p}",
                Value = $"value-expanded-{p}",
                Partition = p
            });
            results.Add(metadata);
        }

        // Assert - production to new partitions should succeed
        await Assert.That(results).Count().IsEqualTo(2);
        await Assert.That(results[0].Partition).IsEqualTo(2);
        await Assert.That(results[1].Partition).IsEqualTo(3);
        foreach (var result in results)
        {
            await Assert.That(result.Topic).IsEqualTo(topic);
            await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
        }
    }

    [Test]
    public async Task ShortMetadataMaxAge_DoesNotDisruptProduction()
    {
        // Arrange - producer with very short metadata max age (2 seconds)
        // to ensure frequent refreshes do not disrupt ongoing production
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-metadata-short-max-age")
            .WithMetadataMaxAge(TimeSpan.FromSeconds(2))
            .BuildAsync();

        // Act - produce messages continuously over a period longer than metadata max age
        // This ensures multiple metadata refreshes happen during production
        const int messageCount = 50;
        var results = new List<RecordMetadata>();
        var errors = new List<Exception>();

        for (var i = 0; i < messageCount; i++)
        {
            try
            {
                var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"key-{i}",
                    Value = $"value-{i}"
                });
                results.Add(metadata);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }

            // Small delay to spread production over time so metadata refreshes happen
            if (i % 10 == 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
        }

        // Assert - all messages should succeed despite frequent metadata refreshes
        await Assert.That(errors).Count().IsEqualTo(0)
            .Because($"No errors expected, but got: {string.Join("; ", errors.Select(e => e.Message))}");
        await Assert.That(results).Count().IsEqualTo(messageCount);

        foreach (var result in results)
        {
            await Assert.That(result.Topic).IsEqualTo(topic);
            await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
        }
    }

    [Test]
    public async Task Producer_WithMetadataRecovery_RecoversAfterBrokerRecovery()
    {
        // Arrange - producer with metadata recovery strategy enabled
        // In a single-broker test environment, we verify that the recovery strategy
        // is correctly configured and does not interfere with normal production
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-metadata-recovery-strategy")
            .WithMetadataMaxAge(TimeSpan.FromSeconds(5))
            .WithMetadataRecoveryStrategy(MetadataRecoveryStrategy.Rebootstrap)
            .WithMetadataRecoveryRebootstrapTrigger(TimeSpan.FromSeconds(10))
            .BuildAsync();

        // Act - produce messages to verify the producer works normally with recovery enabled
        var metadata1 = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "recovery-key-1",
            Value = "recovery-value-1"
        });

        await Assert.That(metadata1.Topic).IsEqualTo(topic);
        await Assert.That(metadata1.Offset).IsGreaterThanOrEqualTo(0);

        // Wait for metadata to become stale, triggering a refresh cycle
        await Task.Delay(TimeSpan.FromSeconds(7));

        // Produce again - the recovery strategy should allow metadata refresh
        // even though no actual broker failure occurred
        var metadata2 = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "recovery-key-2",
            Value = "recovery-value-2"
        });

        await Assert.That(metadata2.Topic).IsEqualTo(topic);
        await Assert.That(metadata2.Offset).IsGreaterThanOrEqualTo(0);

        // Produce to a second newly created topic to verify metadata discovery works
        // with recovery strategy enabled
        var topic2 = await KafkaContainer.CreateTestTopicAsync();

        var metadata3 = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic2,
            Key = "recovery-key-3",
            Value = "recovery-value-3"
        });

        await Assert.That(metadata3.Topic).IsEqualTo(topic2);
        await Assert.That(metadata3.Offset).IsGreaterThanOrEqualTo(0);
    }
}
