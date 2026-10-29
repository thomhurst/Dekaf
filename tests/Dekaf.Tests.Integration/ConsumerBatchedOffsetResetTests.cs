using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

[Category("Consumer")]
public sealed class ConsumerBatchedOffsetResetTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ManualAssignment_MissingTopicsAreAutoCreatedBeforeOffsetReset()
    {
        var firstTopic = $"batch-auto-create-{Guid.NewGuid():N}";
        var secondTopic = $"batch-auto-create-{Guid.NewGuid():N}";
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAllowAutoCreateTopics()
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellation.Token);
        consumer.Assign(new TopicPartition(firstTopic, 0), new TopicPartition(secondTopic, 0));

        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), cancellation.Token);

        await Assert.That(result).IsNull();
        await using var admin = KafkaContainer.CreateAdminClient();
        var topics = await admin.ListTopicsAsync(cancellationToken: cancellation.Token);
        await Assert.That(topics.Select(topic => topic.Name)).Contains(firstTopic);
        await Assert.That(topics.Select(topic => topic.Name)).Contains(secondTopic);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task AssignmentAndSeek_ResolveEveryPartition(bool manualAssignment, bool byDuration)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var partitions = new[]
        {
            new TopicPartition(topic, 0),
            new TopicPartition(topic, 1),
            new TopicPartition(topic, 2)
        };
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellation.Token);
        foreach (var partition in partitions)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Partition = partition.Partition,
                Value = $"partition-{partition.Partition}"
            }, cancellation.Token);
        }

        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"batch-reset-{Guid.NewGuid():N}")
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory());
        if (byDuration)
            builder.WithAutoOffsetResetByDuration(TimeSpan.FromHours(1));
        else
            builder.WithAutoOffsetReset(AutoOffsetReset.Earliest);
        await using var consumer = await builder.BuildAsync(cancellation.Token);
        if (manualAssignment)
            consumer.Assign(partitions);
        else
            consumer.Subscribe(topic);

        await AssertEveryPartitionAsync();
        consumer.SeekToBeginning(partitions);
        await AssertEveryPartitionAsync();

        async Task AssertEveryPartitionAsync()
        {
            var received = new HashSet<int>();
            for (var i = 0; i < partitions.Length; i++)
            {
                var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cancellation.Token);
                await Assert.That(result).IsNotNull();
                await Assert.That(result!.Value.Offset).IsEqualTo(0L);
                await Assert.That(result.Value.Value).IsEqualTo($"partition-{result.Value.Partition}");
                await Assert.That(received.Add(result.Value.Partition)).IsTrue();
            }
            await Assert.That(received.Count).IsEqualTo(partitions.Length);
        }
    }
}
