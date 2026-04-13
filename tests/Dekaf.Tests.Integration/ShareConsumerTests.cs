using Dekaf.Admin;
using Dekaf.Producer;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for the share consumer (KIP-932).
/// Requires Kafka 4.2+ with group.share.enable=true.
/// </summary>
[Category("ShareConsumer")]
[SupportsKafka(420)]
public class ShareConsumerTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ShareConsumer_SingleConsumer_ReceivesAllMessages()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var groupId = $"share-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        for (int i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        await producer.FlushAsync();

        // Act
        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<ShareConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.PollAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 5) break;
        }

        // Assert
        await Assert.That(messages.Count).IsEqualTo(5);

        var values = messages.Select(m => m.Value).OrderBy(v => v).ToList();
        for (int i = 0; i < 5; i++)
        {
            await Assert.That(values[i]).IsEqualTo($"value-{i}");
        }
    }

    [Test]
    public async Task ShareConsumer_Subscribe_Unsubscribe_Works()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"share-group-{Guid.NewGuid():N}";

        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        // Act
        consumer.Subscribe(topic);
        await Assert.That(consumer.Subscription.Count).IsEqualTo(1);
        await Assert.That(consumer.Subscription.Contains(topic)).IsTrue();

        consumer.Unsubscribe();
        await Assert.That(consumer.Subscription.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ShareConsumer_DeliveryCount_IsAtLeastOne()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"share-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value"
        });

        await producer.FlushAsync();

        // Act
        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        ShareConsumeResult<string, string>? result = null;

        await foreach (var msg in consumer.PollAsync(cts.Token))
        {
            result = msg;
            break;
        }

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.DeliveryCount).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task ShareConsumer_CommitAsync_FlushesAcknowledgements()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"share-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value"
        });

        await producer.FlushAsync();

        // Act
        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.PollAsync(cts.Token))
        {
            consumer.Acknowledge(msg, AcknowledgeType.Accept);
            break;
        }

        // Should not throw
        await consumer.CommitAsync(CancellationToken.None);
    }

    [Test]
    public async Task ShareConsumer_Builder_ConfiguresCorrectly()
    {
        var groupId = $"share-group-{Guid.NewGuid():N}";

        await using var consumer = Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithClientId("test-share-consumer")
            .WithFetchMinBytes(1)
            .WithFetchMaxBytes(1048576)
            .WithFetchMaxWaitMs(100)
            .WithMaxPollRecords(100)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .Build();

        await Assert.That(consumer).IsNotNull();
        await Assert.That(consumer.Subscription.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ShareConsumer_MemberId_IsSetAfterJoining()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"share-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value"
        });

        await producer.FlushAsync();

        // Act
        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.PollAsync(cts.Token))
        {
            break;
        }

        // Assert — after polling, MemberId should be set
        await Assert.That(consumer.MemberId).IsNotNull();
    }
}

/// <summary>
/// Integration tests for share group admin operations (KIP-932).
/// Requires Kafka 4.2+ with group.share.enable=true.
/// </summary>
[Category("ShareConsumerAdmin")]
[SupportsKafka(420)]
public class ShareConsumerAdminTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task DescribeShareGroups_ReturnsGroupInfo()
    {
        // Arrange — create a share consumer so a group exists
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"share-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value"
        });

        await producer.FlushAsync();

        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);

        // Poll to ensure group is active
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var msg in consumer.PollAsync(cts.Token))
        {
            break;
        }

        // Act
        await using var adminClient = KafkaContainer.CreateAdminClient();
        var descriptions = await adminClient.DescribeShareGroupsAsync([groupId]);

        // Assert
        await Assert.That(descriptions.ContainsKey(groupId)).IsTrue();
        var desc = descriptions[groupId];
        await Assert.That(desc.GroupId).IsEqualTo(groupId);
        await Assert.That(desc.GroupState).IsNotNull();
        await Assert.That(desc.Members.Count).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task ListShareGroups_ReturnsShareGroupType()
    {
        // Arrange — create a share consumer so a group exists
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"share-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value"
        });

        await producer.FlushAsync();

        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);

        // Poll to ensure group is active
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var msg in consumer.PollAsync(cts.Token))
        {
            break;
        }

        // Act
        await using var adminClient = KafkaContainer.CreateAdminClient();
        var groups = await adminClient.ListShareGroupsAsync();

        // Assert — our group should be in the list
        var ourGroup = groups.FirstOrDefault(g => g.GroupId == groupId);
        await Assert.That(ourGroup).IsNotNull();
    }

    [Test]
    public async Task DescribeShareGroupOffsets_ReturnsStartOffsets()
    {
        // Arrange — create a share consumer and consume a record to establish offsets
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"share-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value"
        });

        await producer.FlushAsync();

        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var msg in consumer.PollAsync(cts.Token))
        {
            consumer.Acknowledge(msg, AcknowledgeType.Accept);
            break;
        }

        await consumer.CommitAsync();

        // Act
        await using var adminClient = KafkaContainer.CreateAdminClient();
        var offsets = await adminClient.DescribeShareGroupOffsetsAsync(
            groupId,
            [new TopicPartition(topic, 0)]);

        // Assert
        await Assert.That(offsets.Count).IsGreaterThanOrEqualTo(1);
        var offset = offsets.First(o => o.TopicPartition.Topic == topic);
        await Assert.That(offset.StartOffset).IsGreaterThanOrEqualTo(0);
    }
}
