using System.Text;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

[Category("EventHubs")]
[ClassDataSource<EventHubsEmulatorContainer>(Shared = SharedType.PerTestSession)]
public sealed class EventHubsEmulatorIntegrationTests(EventHubsEmulatorContainer emulator)
{
    [Test]
    public async Task Producer_AuthenticatesAndProducesKeyedMessage()
    {
        await using var producer = await CreateProducerAsync<string, string>("producer-auth");

        var result = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = EventHubsEmulatorContainer.ProducerTopic,
            Key = "producer-key",
            Value = "producer-value"
        }, CancellationToken.None);

        await Assert.That(result.Topic).IsEqualTo(EventHubsEmulatorContainer.ProducerTopic);
        await Assert.That(result.Partition).IsGreaterThanOrEqualTo(0);
        await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Subscribe_WhenKip848IsUnavailable_ThrowsBrokerVersionException()
    {
        await ProduceAsync(
            EventHubsEmulatorContainer.ConsumerTopic,
            "consumer-key",
            "consumer-value");

        await using var consumer = await CreateConsumerAsync<string, string>(
            "consumer-auth",
            EventHubsEmulatorContainer.ConsumerGroup);
        consumer.Subscribe(EventHubsEmulatorContainer.ConsumerTopic);

        await Assert.ThrowsAsync<BrokerVersionException>(async () =>
            await ConsumeOneAsync(consumer));
    }

    [Test]
    public async Task Consumer_AuthenticatesAssignsAndConsumesFromBeginning()
    {
        await ProduceAsync(
            EventHubsEmulatorContainer.ConsumerTopic,
            "consumer-key",
            "consumer-value");

        await using var consumer = await CreateConsumerAsync<string, string>(
            "consumer-manual-assignment",
            EventHubsEmulatorContainer.ConsumerGroup);
        AssignFromBeginning(consumer, EventHubsEmulatorContainer.ConsumerTopic);

        var result = await ConsumeOneAsync(consumer);

        await Assert.That(result.Topic).IsEqualTo(EventHubsEmulatorContainer.ConsumerTopic);
        await Assert.That(result.Key).IsEqualTo("consumer-key");
        await Assert.That(result.Value).IsEqualTo("consumer-value");
    }

    [Test]
    public async Task RoundTrip_PreservesNullKeyValueAndHeaders()
    {
        var headers = new Headers()
            .Add("content-type", "application/deleted")
            .Add("trace-id", "eventhubs-emulator");

        await using (var producer = await Kafka.CreateProducer<string?, string?>()
            .WithBootstrapServers(emulator.BootstrapServers)
            .WithClientId("eventhubs-null-producer")
            .WithSaslPlain(EventHubsEmulatorContainer.SaslUsername, emulator.SaslPassword)
            .WithIdempotence(false)
            .WithKeySerializer(Serializers.NullableString)
            .WithValueSerializer(Serializers.NullableString)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync())
        {
            await producer.ProduceAsync(new ProducerMessage<string?, string?>
            {
                Topic = EventHubsEmulatorContainer.RoundTripTopic,
                Key = null,
                Value = null,
                Headers = headers
            }, CancellationToken.None);
        }

        await using var consumer = await Kafka.CreateConsumer<string?, string?>()
            .WithBootstrapServers(emulator.BootstrapServers)
            .WithClientId("eventhubs-null-consumer")
            .WithGroupId(EventHubsEmulatorContainer.RoundTripGroup)
            .WithSaslPlain(EventHubsEmulatorContainer.SaslUsername, emulator.SaslPassword)
            .WithKeyDeserializer(Serializers.NullableString)
            .WithValueDeserializer(Serializers.NullableString)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        AssignFromBeginning(consumer, EventHubsEmulatorContainer.RoundTripTopic);

        var result = await ConsumeOneAsync(consumer);

        await Assert.That(result.Key).IsNull();
        await Assert.That(result.Value).IsNull();
        await Assert.That(result.Headers.First(h => h.Key == "content-type").GetValueAsString())
            .IsEqualTo("application/deleted");
        await Assert.That(result.Headers.First(h => h.Key == "trace-id").GetValueAsString())
            .IsEqualTo("eventhubs-emulator");
    }

    [Test]
    public async Task ConsumerGroup_CommitResumesAtNextOffset()
    {
        await ProduceAsync(EventHubsEmulatorContainer.OffsetTopic, "offset-key-1", "offset-value-1", partition: 0);
        await ProduceAsync(EventHubsEmulatorContainer.OffsetTopic, "offset-key-2", "offset-value-2", partition: 0);

        ConsumeResult<string, string> first;
        await using (var consumer = await CreateConsumerAsync<string, string>(
            "offset-consumer-1",
            EventHubsEmulatorContainer.OffsetGroup,
            OffsetCommitMode.Manual))
        {
            consumer.Assign(new TopicPartition(EventHubsEmulatorContainer.OffsetTopic, 0));
            consumer.Seek(new TopicPartitionOffset(EventHubsEmulatorContainer.OffsetTopic, 0, 0));
            first = await ConsumeOneAsync(consumer);
            await consumer.CommitAsync([
                new TopicPartitionOffset(first.Topic, first.Partition, first.Offset + 1)
            ]);
        }

        await using var resumedConsumer = await CreateConsumerAsync<string, string>(
            "offset-consumer-2",
            EventHubsEmulatorContainer.OffsetGroup,
            OffsetCommitMode.Manual);
        var topicPartition = new TopicPartition(EventHubsEmulatorContainer.OffsetTopic, 0);
        resumedConsumer.Assign(topicPartition);
        var committedOffset = await resumedConsumer.GetCommittedOffsetAsync(topicPartition);
        await Assert.That(committedOffset).IsEqualTo(first.Offset + 1);
        resumedConsumer.Seek(new TopicPartitionOffset(
            topicPartition.Topic,
            topicPartition.Partition,
            committedOffset!.Value));

        var resumed = await ConsumeOneAsync(resumedConsumer);

        await Assert.That(resumed.Offset).IsEqualTo(first.Offset + 1);
        await Assert.That(resumed.Key).IsEqualTo("offset-key-2");
        await Assert.That(resumed.Value).IsEqualTo("offset-value-2");
    }

    [Test]
    public async Task BatchedProduction_ConsumesEveryMessageExactlyOnce()
    {
        const int messageCount = 50;
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(emulator.BootstrapServers)
            .WithClientId("eventhubs-batch-producer")
            .WithSaslPlain(EventHubsEmulatorContainer.SaslUsername, emulator.SaslPassword)
            .WithIdempotence(false)
            .WithLinger(TimeSpan.FromMilliseconds(10))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var produces = new Task<RecordMetadata>[messageCount];
        for (var i = 0; i < messageCount; i++)
        {
            produces[i] = producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = EventHubsEmulatorContainer.BatchTopic,
                Key = $"batch-key-{i}",
                Value = $"batch-value-{i}"
            }, CancellationToken.None).AsTask();
        }
        await Task.WhenAll(produces);

        await using var consumer = await CreateConsumerAsync<string, string>(
            "batch-consumer",
            EventHubsEmulatorContainer.BatchGroup);
        AssignFromBeginning(consumer, EventHubsEmulatorContainer.BatchTopic);

        var values = new HashSet<string>(StringComparer.Ordinal);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var result in consumer.ConsumeAsync(timeout.Token))
        {
            await Assert.That(values.Add(result.Value)).IsTrue();
            if (values.Count == messageCount)
                break;
        }

        await Assert.That(values.Count).IsEqualTo(messageCount);
        for (var i = 0; i < messageCount; i++)
            await Assert.That(values).Contains($"batch-value-{i}");
    }

    [Test]
    [Timeout(30_000)]
    public async Task CancellationAndDisposal_CompletePromptly(CancellationToken testCancellation)
    {
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(emulator.BootstrapServers)
            .WithClientId("eventhubs-lifecycle-consumer")
            .WithSaslPlain(EventHubsEmulatorContainer.SaslUsername, emulator.SaslPassword)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(testCancellation);
        consumer.Assign(new TopicPartition(EventHubsEmulatorContainer.LifecycleTopic, 0));
        consumer.Seek(new TopicPartitionOffset(EventHubsEmulatorContainer.LifecycleTopic, 0, 0));

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(testCancellation);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(250));
        await using var records = consumer.ConsumeAsync(cancellation.Token)
            .GetAsyncEnumerator(CancellationToken.None);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await records.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), testCancellation));
    }

    private async Task ProduceAsync(string topic, string key, string value, int? partition = null)
    {
        await using var producer = await CreateProducerAsync<string, string>($"producer-{topic}");
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Partition = partition,
            Key = key,
            Value = value
        }, CancellationToken.None);
    }

    private async ValueTask<IKafkaProducer<TKey, TValue>> CreateProducerAsync<TKey, TValue>(string clientId) =>
        await Kafka.CreateProducer<TKey, TValue>()
            .WithBootstrapServers(emulator.BootstrapServers)
            .WithClientId(clientId)
            .WithSaslPlain(EventHubsEmulatorContainer.SaslUsername, emulator.SaslPassword)
            .WithIdempotence(false)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

    private async ValueTask<IKafkaConsumer<TKey, TValue>> CreateConsumerAsync<TKey, TValue>(
        string clientId,
        string groupId,
        OffsetCommitMode offsetCommitMode = OffsetCommitMode.Auto) =>
        await Kafka.CreateConsumer<TKey, TValue>()
            .WithBootstrapServers(emulator.BootstrapServers)
            .WithClientId(clientId)
            .WithGroupId(groupId)
            .WithSaslPlain(EventHubsEmulatorContainer.SaslUsername, emulator.SaslPassword)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(offsetCommitMode)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

    private static async ValueTask<ConsumeResult<TKey, TValue>> ConsumeOneAsync<TKey, TValue>(
        IKafkaConsumer<TKey, TValue> consumer)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), timeout.Token);
        await Assert.That(result).IsNotNull();
        return result!.Value;
    }

    private static void AssignFromBeginning<TKey, TValue>(
        IKafkaConsumer<TKey, TValue> consumer,
        string topic)
    {
        var partitions = new TopicPartition[EventHubsEmulatorContainer.PartitionCount];
        for (var partition = 0; partition < partitions.Length; partition++)
            partitions[partition] = new TopicPartition(topic, partition);

        consumer.Assign(partitions);
        for (var partition = 0; partition < partitions.Length; partition++)
            consumer.Seek(new TopicPartitionOffset(topic, partition, 0));
    }
}
