using Dekaf.Admin;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Integration;

[Category("ShareConsumer")]
[SupportsKafka(420)]
[NotInParallel("ShareConsumerKafka42")]
public sealed class ShareConsumerSubscriptionTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
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
}
