using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Outbox;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

[Category("MessagingPatterns")]
public sealed class OutboxOrderingIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    private const string MessageIdHeader = OutboxRelayOptions.DefaultMessageIdHeaderName;

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    [Timeout(90_000)]
    public async Task PartialFailure_Retry_CanReorderEvenAfterMessageIdDeduplication(bool localFailure, CancellationToken cancellationToken)
    {
        var topic = $"outbox-order-{Guid.NewGuid():N}";
        await using var admin = KafkaContainer.CreateAdminClient();
        await admin.CreateTopicsAsync([new NewTopic
        {
            Name = topic,
            NumPartitions = 1,
            ReplicationFactor = 1,
            Configs = new Dictionary<string, string> { ["max.message.bytes"] = localFailure ? "65536" : "512" }
        }], cancellationToken: cancellationToken);

        var producer = OutboxServiceCollectionExtensions.CreateRelayProducerBuilder(
            builder => builder.WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithBatchSize(256)
                .WithMaxRequestSize(localFailure ? 1024 : 65536)
                .WithLinger(TimeSpan.FromMilliseconds(10)),
            GlobalTestSetup.GetLoggerFactory()).Build();
        await using var publisher = new DekafOutboxPublisher(producer);
        await publisher.InitializeAsync(cancellationToken);
        _ = await producer.GetPartitionsForAsync(topic, cancellationToken);

        OutboxMessage[] rows = [Row(topic, 1, 4096), Row(topic, 2, 1), Row(topic, 3, 1)];
        var firstAttempt = await publisher.PublishAsync(rows, MessageIdHeader, cancellationToken);
        await Assert.That(firstAttempt.AckedCount).IsEqualTo(0);
        await Assert.That(firstAttempt.FirstError).IsAssignableTo<KafkaException>();
        await Assert.That(((KafkaException)firstAttempt.FirstError!).ErrorCode).IsEqualTo(ErrorCode.MessageTooLarge);
        var beforeRetry = await EndOffsetAsync(admin, topic, cancellationToken);

        // Repair the invalid stored payload, preserving the stable deduplication identity.
        // No change to broker configuration and no delay-dependent config propagation.
        rows[0] = Row(topic, 1, 1, rows[0].MessageId);
        var retry = await publisher.PublishAsync(rows, MessageIdHeader, cancellationToken);
        await Assert.That(retry.FirstError).IsNull();
        await Assert.That(retry.AckedCount).IsEqualTo(3);
        var endOffset = await EndOffsetAsync(admin, topic, cancellationToken);

        await using var consumer = await Kafka.CreateConsumer<byte[], byte[]>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"outbox-order-reader-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync(cancellationToken);
        consumer.Assign(new TopicPartition(topic, 0));
        var observed = new List<int>();
        var deduplicated = new List<int>();
        var seen = new HashSet<Guid>();
        await foreach (var record in consumer.ConsumeAsync(cancellationToken))
        {
            var messageId = Guid.Parse(record.Headers.Single(header => header.Key == MessageIdHeader).GetValueAsString()!);
            var rowId = Array.FindIndex(rows, row => row.MessageId == messageId) + 1;
            await Assert.That(rowId).IsGreaterThan(0);
            await Assert.That(record.Partition).IsEqualTo(0);
            await Assert.That(record.Key.SequenceEqual(new byte[] { 1 })).IsTrue();
            observed.Add(rowId);
            if (seen.Add(messageId))
                deduplicated.Add(rowId);
            if (record.Offset + 1 == endOffset)
                break;
        }

        // This is the documented batched publisher contract: prefix accounting preserves
        // rows for retry, but cannot retract later records already accepted by Kafka.
        await Assert.That(beforeRetry).IsEqualTo(2L);
        await Assert.That(endOffset).IsEqualTo(5L);
        await Assert.That(string.Join(',', observed)).IsEqualTo("2,3,1,2,3");
        await Assert.That(string.Join(',', deduplicated)).IsEqualTo("2,3,1");
    }

    private static OutboxMessage Row(string topic, int id, int valueLength, Guid? messageId = null) => new()
    {
        Id = id,
        MessageId = messageId ?? Guid.NewGuid(),
        Bucket = 0,
        Topic = topic,
        Key = [1],
        Value = new byte[valueLength],
        Partition = 0,
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    private static async Task<long> EndOffsetAsync(IAdminClient admin, string topic, CancellationToken cancellationToken)
    {
        var partition = new TopicPartition(topic, 0);
        var result = await admin.ListOffsetsAsync([
            new TopicPartitionOffsetSpec { TopicPartition = partition, Spec = OffsetSpec.Latest }
        ], cancellationToken: cancellationToken);
        return result[partition].Offset;
    }
}
