using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for log compaction and tombstone behavior.
/// </summary>
[Category("Messaging")]
[ParallelLimiter<MessagingTestLimit>]
public sealed class LogCompactionTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task CompactedTopic_CreateWithCleanupPolicy_ConfigVerified()
    {
        var topicName = $"compacted-topic-{Guid.NewGuid():N}";

        await using var adminClient = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
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

        // Wait for topic to appear in metadata
        await KafkaContainer.WaitForTopicReadyAsync(topicName).ConfigureAwait(false);

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
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        // Warm up to ensure broker has initialized partition state
        await producer.FireAsync(new ProducerMessage<string, string>
            { Topic = topic, Key = "warmup", Value = "warmup" });

        // Produce two messages with the same key
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "duplicate-key",
            Value = "first-value"
        }, CancellationToken.None);

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "duplicate-key",
            Value = "second-value"
        }, CancellationToken.None);

        // Consume from beginning - both messages should be visible before compaction
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"duplicate-test-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count(m => m.Key != "warmup") >= 2) break;
        }

        var actual = messages.Where(m => m.Key != "warmup").ToList();
        await Assert.That(actual).Count().IsEqualTo(2);
        await Assert.That(actual[0].Value).IsEqualTo("first-value");
        await Assert.That(actual[1].Value).IsEqualTo("second-value");

        // The latest value for the key
        var latestValue = actual.Last(m => m.Key == "duplicate-key").Value;
        await Assert.That(latestValue).IsEqualTo("second-value");
    }
}
