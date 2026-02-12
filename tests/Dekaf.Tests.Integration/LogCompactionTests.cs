using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for log compaction and tombstone behavior.
/// </summary>
[Category("Messaging")]
public sealed class LogCompactionTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task CompactedTopic_CreateWithCleanupPolicy_ConfigVerified()
    {
        var topicName = $"compacted-topic-{Guid.NewGuid():N}";

        await using var adminClient = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        await adminClient.CreateTopicsAsync([
            new NewTopic
            {
                Name = topicName,
                NumPartitions = 1,
                ReplicationFactor = 1,
                Configs = new Dictionary<string, string>
                {
                    ["cleanup.policy"] = "compact"
                }
            }
        ]);

        // Wait for topic creation
        await Task.Delay(2000).ConfigureAwait(false);

        // Verify the topic config
        var configs = await adminClient.DescribeConfigsAsync([
            ConfigResource.Topic(topicName)
        ]);

        var topicConfig = configs[ConfigResource.Topic(topicName)];
        var cleanupPolicy = topicConfig.First(c => c.Name == "cleanup.policy");
        await Assert.That(cleanupPolicy.Value).IsEqualTo("compact");
    }

    [Test]
    public async Task DuplicateKeys_ProduceSameKeyTwice_BothMessagesConsumedFromBeginning()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce two messages with the same key
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "duplicate-key",
            Value = "first-value"
        });

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "duplicate-key",
            Value = "second-value"
        });

        // Consume from beginning - both messages should be visible before compaction
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"duplicate-test-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 2) break;
        }

        await Assert.That(messages).Count().IsEqualTo(2);
        await Assert.That(messages[0].Value).IsEqualTo("first-value");
        await Assert.That(messages[1].Value).IsEqualTo("second-value");

        // The latest value for the key
        var latestValue = messages.Last(m => m.Key == "duplicate-key").Value;
        await Assert.That(latestValue).IsEqualTo("second-value");
    }
}
