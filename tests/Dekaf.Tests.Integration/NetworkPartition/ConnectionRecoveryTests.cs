using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.NetworkPartition;

/// <summary>
/// Tests verifying client reconnection and metadata recovery after network interruptions.
/// </summary>
[Category("NetworkPartition")]
[ClassDataSource<NetworkPartitionKafkaContainer>(Shared = SharedType.PerClass)]
public class ConnectionRecoveryTests(NetworkPartitionKafkaContainer kafka)
{
    [Test]
    public async Task Client_ReconnectsAndProduces_AfterBriefNetworkInterruption()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-reconnect-producer")
            .WithAcks(Acks.All)
            .WithDeliveryTimeout(TimeSpan.FromSeconds(30))
            .WithRequestTimeout(TimeSpan.FromSeconds(5))
            .BuildAsync();

        // Produce before partition
        var preResult = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "pre-key",
            Value = "pre-value"
        });
        await Assert.That(preResult.Offset).IsGreaterThanOrEqualTo(0);

        // Act: brief network interruption (3 seconds)
        await kafka.PauseAsync();

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3));
            await kafka.UnpauseAsync();

            // Wait for connection recovery
            await Task.Delay(TimeSpan.FromSeconds(2));

            // Produce again - should succeed after reconnection
            var postResult = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "post-key",
                Value = "post-value"
            });
            await Assert.That(postResult.Topic).IsEqualTo(topic);
            await Assert.That(postResult.Offset).IsGreaterThanOrEqualTo(0);
        }
        finally
        {
            try { await kafka.UnpauseAsync(); } catch { /* may already be unpaused */ }
        }

        // Verify data integrity: consume both messages
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-reconnect-consumer")
            .WithGroupId($"reconnect-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .SubscribeTo(topic)
            .BuildAsync();

        var consumed = new List<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            consumed.Add(msg.Value!);
            if (consumed.Count >= 2)
                break;
        }

        await Assert.That(consumed).Contains("pre-value");
        await Assert.That(consumed).Contains("post-value");
    }

    [Test]
    public async Task MetadataRefresh_DiscoversNewTopic_AfterNetworkRecovery()
    {
        // Arrange: produce to topic1 to establish connection and cache metadata
        var topic1 = await kafka.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-metadata-refresh-producer")
            .WithAcks(Acks.All)
            .WithDeliveryTimeout(TimeSpan.FromSeconds(30))
            .WithRequestTimeout(TimeSpan.FromSeconds(5))
            .WithMetadataMaxAge(TimeSpan.FromSeconds(3))
            .WithMaxBlock(TimeSpan.FromSeconds(15))
            .BuildAsync();

        var result1 = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic1,
            Key = "t1-key",
            Value = "t1-value"
        });
        await Assert.That(result1.Offset).IsGreaterThanOrEqualTo(0);

        // Act: network interruption
        await kafka.PauseAsync();

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            await kafka.UnpauseAsync();

            // Wait for metadata max age to expire (3s) and recovery
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Create a new topic after recovery
            var topic2 = await kafka.CreateTestTopicAsync();

            // Assert: producer should discover and produce to the new topic
            var result2 = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic2,
                Key = "t2-key",
                Value = "t2-value"
            });

            await Assert.That(result2.Topic).IsEqualTo(topic2);
            await Assert.That(result2.Offset).IsGreaterThanOrEqualTo(0);
        }
        finally
        {
            try { await kafka.UnpauseAsync(); } catch { /* may already be unpaused */ }
        }
    }
}
