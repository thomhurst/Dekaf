using System.Diagnostics;
using Dekaf.Admin;
using Dekaf.Producer;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for the share consumer (KIP-932).
/// Requires Kafka 4.2+ with group.share.enable=true.
///
/// Share groups use a Share Partition Start Offset (SPSO) that is set when the
/// share coordinator first initializes a share-partition. Records must be produced
/// after the consumer has joined the group and issued an initial ShareFetch for
/// them to be within the acquisition window.
/// </summary>
[Category("ShareConsumer")]
[SupportsKafka(420)]
[NotInParallel("ShareConsumerKafka42")]
public class ShareConsumerTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ShareConsumer_SingleConsumer_ReceivesAllMessages()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var groupId = $"share-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);
        await ShareConsumerTestHelper.PrimeShareConsumerAsync(consumer);

        await ShareConsumerTestHelper.ProduceAsync(producer, topic, count: 5);

        var messages = new List<ShareConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            await foreach (var msg in consumer.PollAsync(cts.Token))
            {
                messages.Add(msg);
                if (messages.Count >= 5) break;
            }
        }
        catch (OperationCanceledException) { }

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
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"share-group-{Guid.NewGuid():N}";

        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);
        await Assert.That(consumer.Subscription.Count).IsEqualTo(1);
        await Assert.That(consumer.Subscription.Contains(topic)).IsTrue();

        consumer.Unsubscribe();
        await Assert.That(consumer.Subscription.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ShareConsumer_DeliveryCount_IsAtLeastOne()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"share-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);
        await ShareConsumerTestHelper.PrimeShareConsumerAsync(consumer);

        await ShareConsumerTestHelper.ProduceAsync(producer, topic, count: 1);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        ShareConsumeResult<string, string>? result = null;

        try
        {
            await foreach (var msg in consumer.PollAsync(cts.Token))
            {
                result = msg;
                break;
            }
        }
        catch (OperationCanceledException) { }

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.DeliveryCount).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task ShareConsumer_CommitAsync_FlushesAcknowledgements()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"share-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);
        await ShareConsumerTestHelper.PrimeShareConsumerAsync(consumer);

        await ShareConsumerTestHelper.ProduceAsync(producer, topic, count: 1);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            await foreach (var msg in consumer.PollAsync(cts.Token))
            {
                consumer.Acknowledge(msg, AcknowledgeType.Accept);
                break;
            }
        }
        catch (OperationCanceledException) { }

        // Should not throw
        await consumer.CommitAsync(CancellationToken.None);
    }

    [Test]
    public async Task ShareConsumer_ExplicitAcknowledgement_DoesNotAutoAcceptPolledRecord()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var groupId = $"share-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await using var firstConsumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit)
            .WithMaxPollRecords(1)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        firstConsumer.Subscribe(topic);
        await ShareConsumerTestHelper.PrimeShareConsumerAsync(firstConsumer);

        await ShareConsumerTestHelper.ProduceAsync(producer, topic, count: 1);
        var first = await ConsumeOneAsync(firstConsumer);

        await Assert.That(first.Value).IsEqualTo("value-0");

        await firstConsumer.CommitAsync(CancellationToken.None);
        firstConsumer.Acknowledge(first, AcknowledgeType.Release);
        await firstConsumer.CommitAsync(CancellationToken.None);
        await firstConsumer.CloseAsync(CancellationToken.None);

        await using var secondConsumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit)
            .WithMaxPollRecords(1)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        secondConsumer.Subscribe(topic);

        var redelivered = await ConsumeOneAsync(secondConsumer);

        await Assert.That(redelivered.Value).IsEqualTo(first.Value);
        await Assert.That(redelivered.Offset).IsEqualTo(first.Offset);
        await Assert.That(redelivered.DeliveryCount).IsGreaterThan(first.DeliveryCount);
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
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"share-group-{Guid.NewGuid():N}";

        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);
        await ShareConsumerTestHelper.PrimeShareConsumerAsync(consumer);

        // After polling, MemberId should be set
        await Assert.That(consumer.MemberId).IsNotNull();
    }

    private static async Task<ShareConsumeResult<string, string>> ConsumeOneAsync(
        IKafkaShareConsumer<string, string> consumer)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            await foreach (var msg in consumer.PollAsync(cts.Token))
            {
                return msg;
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }

        throw new InvalidOperationException("Share consumer completed without returning a record.");
    }
}

/// <summary>
/// Integration tests for share group admin operations (KIP-932).
/// Requires Kafka 4.2+ with group.share.enable=true.
/// </summary>
[Category("ShareConsumerAdmin")]
[SupportsKafka(420)]
[NotInParallel("ShareConsumerKafka42")]
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

        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);
        await ShareConsumerTestHelper.PrimeShareConsumerAsync(consumer);

        await ShareConsumerTestHelper.ProduceAsync(producer, topic, count: 1);

        // Poll to ensure group is active and has received a message
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            await foreach (var msg in consumer.PollAsync(cts.Token))
            {
                break;
            }
        }
        catch (OperationCanceledException) { }

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

        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);
        await ShareConsumerTestHelper.PrimeShareConsumerAsync(consumer);

        await ShareConsumerTestHelper.ProduceAsync(producer, topic, count: 1);

        // Poll to ensure group is active
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            await foreach (var msg in consumer.PollAsync(cts.Token))
            {
                break;
            }
        }
        catch (OperationCanceledException) { }

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

        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);
        await ShareConsumerTestHelper.PrimeShareConsumerAsync(consumer);

        await ShareConsumerTestHelper.ProduceAsync(producer, topic, count: 1);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            await foreach (var msg in consumer.PollAsync(cts.Token))
            {
                consumer.Acknowledge(msg, AcknowledgeType.Accept);
                break;
            }
        }
        catch (OperationCanceledException) { }

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

/// <summary>
/// Shared helper for share consumer integration tests.
/// Primes the share consumer before producing records so the broker initializes
/// the Share Partition Start Offset (SPSO) before test messages are written.
/// </summary>
internal static class ShareConsumerTestHelper
{
    internal static async Task PrimeShareConsumerAsync(
        IKafkaShareConsumer<string, string> consumer)
    {
        using var pollCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var pollTask = PollUntilCanceledAsync(consumer, pollCts.Token);

        try
        {
            await WaitForShareAssignmentAsync(consumer, TimeSpan.FromSeconds(15));
            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }
        finally
        {
            await pollCts.CancelAsync();
        }

        await pollTask;
    }

    internal static async Task ProduceAsync(
        IKafkaProducer<string, string> producer, string topic, int count)
    {
        for (int i = 0; i < count; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        await producer.FlushAsync();
    }

    private static async Task PollUntilCanceledAsync(
        IKafkaShareConsumer<string, string> consumer, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var _ in consumer.PollAsync(cancellationToken))
            {
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static async Task WaitForShareAssignmentAsync(
        IKafkaShareConsumer<string, string> consumer, TimeSpan timeout)
    {
        var startedAt = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(startedAt) < timeout)
        {
            if (consumer.MemberId is not null && consumer.Assignment.Count > 0)
                return;

            await Task.Delay(100);
        }

        throw new TimeoutException("Share consumer did not receive a partition assignment.");
    }
}
