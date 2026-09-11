using System.Diagnostics;
using Dekaf.Admin;
using Dekaf.Producer;
using Dekaf.Serialization;
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
    [Arguments(32)]
    [Arguments(128 * 1024)]
    public async Task ShareConsumer_BorrowedPayloadsAndHeaders_SurviveParsingOtherBatches(int payloadSize)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync();
        await using var consumer = await Kafka.CreateShareConsumer<string, ReadOnlyMemory<byte>>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId($"share-borrow-{Guid.NewGuid():N}")
            .WithValueDeserializer(Serializers.RawBytes)
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit).WithMaxPollRecords(2)
            .BuildAsync();
        consumer.Subscribe(topic);
        await ShareConsumerTestHelper.PrimeShareConsumerAsync(consumer);

        var values = new[] { new string('a', payloadSize), new string('b', payloadSize) };
        for (var index = 0; index < values.Length; index++)
        {
            // Await delivery so each record is a separate broker batch.
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic, Partition = 0, Key = index.ToString(), Value = values[index],
                Headers = Headers.Create("test", values[index])
            });
        }

        var received = new HashSet<int>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var record in consumer.PollAsync(timeout.Token))
        {
            var index = int.Parse(record.Key!);
            await Assert.That(received.Add(index)).IsTrue();
            await Assert.That(System.Text.Encoding.UTF8.GetString(record.Value.Span)).IsEqualTo(values[index]);
            var header = record.Headers.Single(static header => header.Key == "test");
            await Assert.That(System.Text.Encoding.UTF8.GetString(header.Value.Span)).IsEqualTo(values[index]);
            consumer.Acknowledge(record);
            await consumer.CommitAsync(timeout.Token);
            await Assert.That(System.Text.Encoding.UTF8.GetString(record.Value.Span)).IsEqualTo(values[index]);
            if (received.Count == values.Length)
                break;
        }
        await Assert.That(received.Count).IsEqualTo(values.Length);
    }

    [Test]
    [Arguments(1)]
    [Arguments(4)]
    public async Task ShareConsumer_NativeResponsePayload_RemainsReadable(int messageCount)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var groupId = $"share-native-{Guid.NewGuid():N}";
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync();
        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(groupId)
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit).WithMaxPollRecords(messageCount)
            .BuildAsync();
        consumer.Subscribe(topic);
        await ShareConsumerTestHelper.PrimeShareConsumerAsync(consumer);

        // Each record exceeds the 85,000-byte native response threshold by itself.
        var values = new string[messageCount];
        for (var index = 0; index < messageCount; index++)
        {
            values[index] = new string((char)('a' + index), 128 * 1024);
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic, Partition = 0, Key = index.ToString(), Value = values[index]
            });
        }
        await producer.FlushAsync();

        var received = new HashSet<int>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var record in consumer.PollAsync(timeout.Token))
        {
            var index = int.Parse(record.Key!);
            await Assert.That(received.Add(index)).IsTrue();
            await Assert.That(record.Value).IsEqualTo(values[index]);
            consumer.Acknowledge(record);
            await consumer.CommitAsync(timeout.Token);
            if (received.Count == messageCount) break;
        }
        await Assert.That(received.Count).IsEqualTo(messageCount);
        await consumer.CloseAsync(timeout.Token);
    }

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
    public async Task ShareConsumer_CommitAsync_ReportsBrokerAcknowledgementOutcome()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"share-group-{Guid.NewGuid():N}";
        ShareAcknowledgementCommitResult[]? outcomes = null;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit)
            .WithAcknowledgementCommitCallback(results => outcomes = results.ToArray())
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);
        await ShareConsumerTestHelper.PrimeShareConsumerAsync(consumer);
        await ShareConsumerTestHelper.ProduceAsync(producer, topic, count: 1);
        var message = await ConsumeOneAsync(consumer);
        consumer.Acknowledge(message, AcknowledgeType.Accept);

        await consumer.CommitAsync(CancellationToken.None);

        await Assert.That(outcomes).HasSingleItem();
        await Assert.That(outcomes![0].TopicPartition)
            .IsEqualTo(new TopicPartition(topic, message.Partition));
        var acknowledgedOffsets = new long[outcomes[0].Offsets.Length];
        outcomes[0].Offsets.CopyTo(acknowledgedOffsets);
        await Assert.That(acknowledgedOffsets).IsEquivalentTo([message.Offset]);
        await Assert.That(outcomes[0].Succeeded).IsTrue();
    }

    [Test]
    public async Task ShareConsumer_SparseCommitOffsetsRemainValidAfterAnotherCommit()
    {
        const int messageCount = 64;
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        ShareAcknowledgementCommitResult[]? outcomes = null;
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();
        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"share-sparse-offsets-{Guid.NewGuid():N}")
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit)
            .WithMaxPollRecords(messageCount)
            .WithAcknowledgementCommitCallback(results => outcomes = results.ToArray())
            .BuildAsync();
        consumer.Subscribe(topic);
        await ShareConsumerTestHelper.PrimeShareConsumerAsync(consumer);
        await ShareConsumerTestHelper.ProduceAsync(producer, topic, messageCount);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var received = new List<ShareConsumeResult<string, string>>(messageCount);
        await foreach (var record in consumer.PollAsync(timeout.Token))
        {
            received.Add(record);
            if (received.Count == messageCount)
                break;
        }
        await Assert.That(received.Count).IsEqualTo(messageCount);
        received.Sort(static (left, right) => left.Offset.CompareTo(right.Offset));

        // Alternating dispositions create real gaps inside the broker acknowledgement vector.
        for (var index = 0; index < messageCount; index += 2)
            consumer.Acknowledge(received[index], AcknowledgeType.Accept);
        await consumer.CommitAsync(timeout.Token);
        await Assert.That(outcomes).HasSingleItem();
        var firstOutcome = outcomes![0];
        var firstOffsets = firstOutcome.Offsets;
        await Assert.That(firstOutcome.Succeeded).IsTrue();
        await Assert.That(firstOffsets.Length).IsEqualTo(messageCount / 2);

        outcomes = null;
        for (var index = 1; index < messageCount; index += 2)
            consumer.Acknowledge(received[index], AcknowledgeType.Accept);
        await consumer.CommitAsync(timeout.Token);
        await Assert.That(outcomes).HasSingleItem();
        var secondOutcome = outcomes![0];
        await Assert.That(secondOutcome.Succeeded).IsTrue();
        await Assert.That(secondOutcome.Offsets.Length).IsEqualTo(messageCount / 2);

        // Both retained struct copies and property access must survive return of the callback array.
        var copied = new long[firstOffsets.Length];
        firstOffsets.CopyTo(copied);
        for (var index = 0; index < copied.Length; index++)
        {
            await Assert.That(copied[index]).IsEqualTo(received[index * 2].Offset);
            await Assert.That(firstOutcome.Offsets[index]).IsEqualTo(copied[index]);
            await Assert.That(secondOutcome.Offsets[index]).IsEqualTo(received[index * 2 + 1].Offset);
        }
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
    public async Task ShareConsumer_Renewal_RetainsRecordAcrossPolls()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var groupId = $"share-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit)
            .WithMaxPollRecords(1)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);
        await ShareConsumerTestHelper.PrimeShareConsumerAsync(consumer);
        await ShareConsumerTestHelper.ProduceAsync(producer, topic, count: 1);
        var first = await ConsumeOneAsync(consumer);

        consumer.Acknowledge(first, AcknowledgeType.Renew);
        await consumer.CommitAsync();
        await ShareConsumerTestHelper.ProduceAsync(producer, topic, count: 1);
        var fetched = await ConsumeOneAsync(consumer);
        var renewed = await ConsumeOneAsync(consumer);

        await Assert.That(consumer.AcquisitionLockTimeoutMs).IsNotNull();
        await Assert.That(consumer.AcquisitionLockTimeoutMs!.Value).IsGreaterThan(0);
        await Assert.That(fetched.Offset).IsEqualTo(first.Offset + 1);
        await Assert.That(renewed.Offset).IsEqualTo(first.Offset);
        await Assert.That(renewed.Value).IsEqualTo(first.Value);

        consumer.Acknowledge(fetched, AcknowledgeType.Accept);
        consumer.Acknowledge(renewed, AcknowledgeType.Accept);
        await consumer.CommitAsync();
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
    public async Task ShareConsumer_HeaderRouterUsesRecordHeaders()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"share-group-{Guid.NewGuid():N}";
        var router = new HeaderRoutingDeserializer<string>(
            "event-type",
            new LabelDeserializer("fallback"),
            new HeaderDeserializerRoute<string>(
                "created"u8.ToArray(),
                new LabelDeserializer("created")));

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithValueDeserializer(new RecordHeaderDeserializerDecorator<string>(router))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);
        await ShareConsumerTestHelper.PrimeShareConsumerAsync(consumer);
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "payload",
            Headers = Headers.Create("event-type", "created")
        });
        await producer.FlushAsync();

        var result = await ConsumeOneAsync(consumer);

        await Assert.That(result.Value).IsEqualTo("created:payload");
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

    private sealed class LabelDeserializer(string label) : IDeserializer<string>
    {
        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
            $"{label}:{Serializers.String.Deserialize(data, context)}";
    }

    private sealed class RecordHeaderDeserializerDecorator<T>(IDeserializer<T> inner) :
        IDeserializer<T>,
        IRecordHeaderDeserializer
    {
        bool IRecordHeaderDeserializer.ConsumesRecordHeaders => true;

        public T Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
            inner.Deserialize(data, context);
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
    [SupportsKafka(430)]
    public async Task DeleteShareGroups_DeletesShareGroupWithoutAffectingConsumerGroup()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var shareGroupId = $"share-delete-{Guid.NewGuid():N}";
        var consumerGroupId = $"consumer-preserve-{Guid.NewGuid():N}";
        var partition = new TopicPartition(topic, 0);

        await using (var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(shareGroupId)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync())
        {
            consumer.Subscribe(topic);
            await ShareConsumerTestHelper.PrimeShareConsumerAsync(consumer);
        }

        await using var admin = KafkaContainer.CreateAdminClient();
        await admin.AlterConsumerGroupOffsetsAsync(
            consumerGroupId,
            [new TopicPartitionOffset(topic, 0, 0)]);

        var deletionResults = await WaitForConditionAsync(
            () => admin.DeleteShareGroupsAsync([shareGroupId]).AsTask(),
            results => results[shareGroupId].ErrorCode == Protocol.ErrorCode.None,
            maxRetries: 12,
            initialDelayMs: 250,
            description: "share group to become empty and delete");

        await Assert.That(deletionResults[shareGroupId].ErrorCode).IsEqualTo(Protocol.ErrorCode.None);

        var shareGroups = await WaitForConditionAsync(
            () => admin.ListShareGroupsAsync().AsTask(),
            groups => groups.All(group => group.GroupId != shareGroupId),
            maxRetries: 12,
            initialDelayMs: 250,
            description: "deleted share group to disappear");
        await Assert.That(shareGroups.Any(group => group.GroupId == shareGroupId)).IsFalse();

        var consumerOffsets = await admin.ListConsumerGroupOffsetsAsync(consumerGroupId);
        await Assert.That(consumerOffsets).ContainsKey(partition);
        await Assert.That(consumerOffsets[partition]).IsEqualTo(0);
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
    internal static async Task PrimeShareConsumerAsync<TKey, TValue>(
        IKafkaShareConsumer<TKey, TValue> consumer)
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

    private static async Task PollUntilCanceledAsync<TKey, TValue>(
        IKafkaShareConsumer<TKey, TValue> consumer, CancellationToken cancellationToken)
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

    private static async Task WaitForShareAssignmentAsync<TKey, TValue>(
        IKafkaShareConsumer<TKey, TValue> consumer, TimeSpan timeout)
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
