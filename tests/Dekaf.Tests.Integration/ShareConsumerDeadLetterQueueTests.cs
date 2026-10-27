using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Broker-side share-group dead-letter queues (KIP-1191). Kafka 4.4 clusters finalize share.version=2 by default.
/// </summary>
[Category("ShareConsumer")]
[NotInParallel("ShareConsumerKafka42")]
public class ShareConsumerDeadLetterQueueTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [SupportsKafka(440)]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RejectedRecord_IsWrittenToDeadLetterTopic(bool copyRecord)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var (topic, deadLetterTopic, group) = await CreateTopicsAsync(timeout.Token);
        await ProduceSourceRecordAsync(topic, timeout.Token);

        await using var consumer = await BuildConsumerAsync(group, deadLetterTopic, copyRecord, topic, timeout.Token);
        await foreach (var record in consumer.PollAsync(timeout.Token))
        {
            consumer.Acknowledge(record, AcknowledgeType.Reject);
            break;
        }
        await consumer.CommitAsync(timeout.Token);

        await AssertDeadLetterRecordAsync(deadLetterTopic, topic, group, copyRecord, expectedDeliveryCount: 1, timeout.Token);
    }

    [Test]
    [SupportsKafka(440)]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RecordPastDeliveryLimit_IsWrittenToDeadLetterTopic(bool copyRecord)
    {
        const int deliveryLimit = 2;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var (topic, deadLetterTopic, group) = await CreateTopicsAsync(timeout.Token);
        await using (var admin = Kafka.CreateAdminClient().WithBootstrapServers(KafkaContainer.BootstrapServers).Build())
        {
            await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
            {
                [new ConfigResource { Type = ConfigResourceType.Group, Name = group }] =
                    [ConfigAlter.Set("share.delivery.count.limit", deliveryLimit.ToString(System.Globalization.CultureInfo.InvariantCulture))]
            }, cancellationToken: timeout.Token);
        }
        await ProduceSourceRecordAsync(topic, timeout.Token);

        await using var consumer = await BuildConsumerAsync(group, deadLetterTopic, copyRecord, topic, timeout.Token);
        await foreach (var record in consumer.PollAsync(timeout.Token))
        {
            // Each release makes the record available again until the broker archives it at the limit.
            consumer.Acknowledge(record, AcknowledgeType.Release);
            await consumer.CommitAsync(timeout.Token);
            if (record.DeliveryCount >= deliveryLimit)
                break;
        }

        await AssertDeadLetterRecordAsync(deadLetterTopic, topic, group, copyRecord, deliveryLimit, timeout.Token);
    }

    [Test]
    [SupportsKafka(420)]
    public async Task DeadLetterConfig_IsRejectedBeforeKafka44AndAppliedAfter()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var group = $"share-dlq-config-{Guid.NewGuid():N}";
        var builder = Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(group)
            .WithDeadLetterQueue("dlq.config", copyRecord: true);

        if (KafkaContainer.Version < 440)
        {
            // Older brokers do not know the KIP-1191 group configs, so initialization fails before joining.
            var exception = await Assert.That(async () => await builder.BuildAsync(timeout.Token)).Throws<KafkaException>();
            await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.InvalidConfig);
            return;
        }

        await using var consumer = await builder.BuildAsync(timeout.Token);
        await using var admin = Kafka.CreateAdminClient().WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        var resource = new ConfigResource { Type = ConfigResourceType.Group, Name = group };
        var configs = (await admin.DescribeConfigsAsync([resource], cancellationToken: timeout.Token))[resource];
        await Assert.That(configs.Single(config => config.Name == "errors.deadletterqueue.topic.name").Value).IsEqualTo("dlq.config");
        await Assert.That(configs.Single(config => config.Name == "errors.deadletterqueue.copy.record.enable").Value).IsEqualTo("true");
    }

    private async Task<(string Topic, string DeadLetterTopic, string Group)> CreateTopicsAsync(CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var deadLetterTopic = $"dlq.{Guid.NewGuid():N}";
        await using var admin = Kafka.CreateAdminClient().WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        await admin.CreateTopicsAsync(
        [
            new NewTopic
            {
                Name = deadLetterTopic,
                Configs = new Dictionary<string, string> { ["errors.deadletterqueue.group.enable"] = "true" }
            }
        ], cancellationToken: cancellationToken);
        return (topic, deadLetterTopic, $"share-dlq-{Guid.NewGuid():N}");
    }

    private async Task ProduceSourceRecordAsync(string topic, CancellationToken cancellationToken)
    {
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync(cancellationToken);
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value",
            // A spoofed context header must not survive into the dead-letter record.
            Headers = new Headers { { "app", "kept" }, { ShareGroupDeadLetterHeaders.GroupKey, "spoofed" } }
        }, cancellationToken);
    }

    private async Task<IKafkaShareConsumer<string, string>> BuildConsumerAsync(
        string group, string deadLetterTopic, bool copyRecord, string topic, CancellationToken cancellationToken) =>
        await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(group)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithDeadLetterQueue(deadLetterTopic, copyRecord)
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit)
            .WithMaxPollRecords(1)
            .SubscribeTo(topic)
            .BuildAsync(cancellationToken);

    private async Task AssertDeadLetterRecordAsync(
        string deadLetterTopic, string topic, string group, bool copyRecord, int expectedDeliveryCount, CancellationToken cancellationToken)
    {
        await using var reader = await Kafka.CreateConsumer<string?, string?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"dlq-reader-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithKeyDeserializer(Serializers.NullableString)
            .WithValueDeserializer(Serializers.NullableString)
            .BuildAsync(cancellationToken);
        reader.Subscribe(deadLetterTopic);
        var result = await reader.ConsumeOneAsync(TimeSpan.FromSeconds(60), cancellationToken);
        await Assert.That(result).IsNotNull();
        var deadLetter = result!.Value;
        var headers = deadLetter.Headers;

        await Assert.That(ShareGroupDeadLetterHeaders.TryParse(headers, out var context)).IsTrue();
        await Assert.That(context.Topic).IsEqualTo(topic);
        await Assert.That(context.Partition).IsEqualTo(0);
        await Assert.That(context.Offset).IsEqualTo(0L);
        await Assert.That(context.Group).IsEqualTo(group);
        await Assert.That(context.DeliveryCount).IsEqualTo(expectedDeliveryCount);
        await Assert.That(context.Message).IsNotNull();
        // The broker's context header replaces the spoofed original. Kafka 4.4.0 copies only the key and value.
        await Assert.That(headers.Count(header => header.Key == ShareGroupDeadLetterHeaders.GroupKey)).IsEqualTo(1);
        await Assert.That(deadLetter.Value).IsEqualTo(copyRecord ? "value" : null);
        await Assert.That(deadLetter.Key).IsEqualTo(copyRecord ? "key" : null);
    }
}
