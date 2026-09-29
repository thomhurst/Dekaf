using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.Logging;

namespace Dekaf.Tests.Integration;

[Category("ShareConsumer")]
[SupportsKafka(420)]
[NotInParallel("ShareConsumerKafka42")]
public sealed class ShareConsumerSubscriptionTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ReplacementSubscription_ReleasesOverflowWithoutRedeliveringAcceptedRecords()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var additional = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var group = $"share-overflow-{Guid.NewGuid():N}";
        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [new ConfigResource { Type = ConfigResourceType.Group, Name = group }] =
                [ConfigAlter.Set("share.auto.offset.reset", "earliest")]
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await using var producer = await Kafka.CreateProducer<int, byte[]>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLinger(TimeSpan.FromMinutes(1)).BuildAsync();
        for (var index = 0; index < 128; index++)
            await producer.FireAsync(new ProducerMessage<int, byte[]>
            {
                Topic = topic, Partition = index % 2, Key = index, Value = [(byte)index]
            });
        await producer.FlushAsync(timeout.Token);
        await using var consumer = await Kafka.CreateShareConsumer<int, ReadOnlyMemory<byte>>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group)
            .WithValueDeserializer(Serializers.RawBytes).WithMaxPollRecords(2)
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit).BuildAsync();
        consumer.Subscribe(topic);
        var received = new HashSet<int>();
        ShareConsumeResult<int, ReadOnlyMemory<byte>> first;
        await using (var poll = consumer.PollAsync(timeout.Token).GetAsyncEnumerator())
        {
            await Assert.That(await poll.MoveNextAsync()).IsTrue();
            first = poll.Current;
            received.Add(first.Key);
            consumer.Acknowledge(first);
        }
        consumer.Subscribe(topic, additional);
        await foreach (var record in consumer.PollAsync(timeout.Token))
        {
            await Assert.That(received.Add(record.Key)).IsTrue();
            await Assert.That(record.Value.Span[0]).IsEqualTo((byte)record.Key);
            await Assert.That(() => consumer.Acknowledge(first, AcknowledgeType.Renew)).Throws<InvalidOperationException>();
            consumer.Acknowledge(record);
            if (received.Count == 128) break;
        }
        await Assert.That(received.Count).IsEqualTo(128);
        await consumer.CommitAsync(timeout.Token);
        await consumer.CloseAsync(timeout.Token);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task JoinedConsumer_ReplacementTopicBecomesConsumable(bool batch)
    {
        var first = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var second = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var group = $"share-subscription-{Guid.NewGuid():N}";
        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [new ConfigResource { Type = ConfigResourceType.Group, Name = group }] =
                [ConfigAlter.Set("share.auto.offset.reset", "earliest")]
        });
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync();
        await ShareConsumerTestHelper.ProduceAsync(producer, first, count: 1);
        await ShareConsumerTestHelper.ProduceAsync(producer, second, count: 1);
        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group)
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit).BuildAsync();
        consumer.Subscribe(first);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await using var records = consumer.PollAsync(timeout.Token).GetAsyncEnumerator();
        await using var batches = consumer.PollBatchesAsync(timeout.Token).GetAsyncEnumerator();
        foreach (var topic in new[] { first, second })
        {
            consumer.Subscribe(topic);
            if (batch)
            {
                await Assert.That(await batches.MoveNextAsync()).IsTrue();
                await Assert.That(batches.Current.TopicPartition.Topic).IsEqualTo(topic);
                foreach (var record in batches.Current)
                    batches.Current.Acknowledge(record);
            }
            else
            {
                await Assert.That(await records.MoveNextAsync()).IsTrue();
                await Assert.That(records.Current.Topic).IsEqualTo(topic);
                consumer.Acknowledge(records.Current);
            }
            await consumer.CommitAsync(timeout.Token);
        }
        await Assert.That(consumer.Assignment.All(partition => partition.Topic == second)).IsTrue();
        await consumer.CloseAsync(timeout.Token);
    }

    [Test]
    public async Task TopicCreatedAfterJoin_IsAssignedAndConsumed()
    {
        // The assignment names the new topic by ID before this consumer's metadata knows it: the
        // share consumer refreshes metadata at startup and on fetch errors, not for a subscribed
        // topic that does not exist yet, and the coordinator's warning below proves the unresolved
        // path ran.
        var topic = $"share-late-topic-{Guid.NewGuid():N}";
        var group = $"share-late-topic-{Guid.NewGuid():N}";
        using var logs = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Warning);
            builder.AddProvider(logs);
        });
        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [new ConfigResource { Type = ConfigResourceType.Group, Name = group }] =
                [ConfigAlter.Set("share.auto.offset.reset", "earliest")]
        });
        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group)
            .WithLoggerFactory(loggerFactory).SubscribeTo(topic).BuildAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var received = new List<string>();
        var poll = ConsumeAsync();

        // Join before the topic exists so its assignment arrives on a later heartbeat.
        while (!await HasMemberAsync())
            await Task.Delay(100, timeout.Token);

        await KafkaContainer.CreateTopicAsync(topic);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync();
        await ShareConsumerTestHelper.ProduceAsync(producer, topic, count: 5);

        await poll;
        await Assert.That(received.Count).IsEqualTo(5);
        await Assert.That(consumer.Assignment.Contains(new TopicPartition(topic, 0))).IsTrue();
        await Assert.That(logs.Entries.Any(static entry =>
            entry.Message.StartsWith("ShareGroupHeartbeat: unknown topic ID", StringComparison.Ordinal))).IsTrue();

        async Task<bool> HasMemberAsync()
        {
            try
            {
                var groups = await admin.DescribeShareGroupsAsync([group], timeout.Token);
                return groups.TryGetValue(group, out var description) && description.Members.Count > 0;
            }
            catch (GroupException ex) when (ex.ErrorCode == ErrorCode.GroupIdNotFound)
            {
                return false;
            }
        }

        async Task ConsumeAsync()
        {
            await foreach (var record in consumer.PollAsync(timeout.Token))
            {
                received.Add(record.Value);
                if (received.Count == 5)
                    return;
            }
        }
    }
}
