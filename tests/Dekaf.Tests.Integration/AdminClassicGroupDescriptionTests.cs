using Dekaf.Admin;
using Dekaf.Protocol;
using ConfluentKafka = Confluent.Kafka;

namespace Dekaf.Tests.Integration;

[Category("ConsumerGroup")]
public sealed class AdminClassicGroupDescriptionTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ClassicConsumer_PreservesMemberAssignmentAndEmptyStateAfterLeave()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var group = $"classic-description-{Guid.NewGuid():N}";
        await using var client = Kafka.Connect(KafkaContainer.BootstrapServers);
        await using var admin = client.CreateAdminClient().Build();
        await using var producer = await client.CreateProducer<string, string>().BuildAsync();
        await producer.ProduceAsync(topic, "key", "value");
        using var consumer = new ConfluentKafka.ConsumerBuilder<string, string>(new ConfluentKafka.ConsumerConfig
        {
            BootstrapServers = KafkaContainer.BootstrapServers, GroupId = group,
            GroupProtocol = ConfluentKafka.GroupProtocol.Classic,
            PartitionAssignmentStrategy = ConfluentKafka.PartitionAssignmentStrategy.Range,
            AutoOffsetReset = ConfluentKafka.AutoOffsetReset.Earliest, EnableAutoCommit = false
        }).Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        consumer.Subscribe(topic);
        var consumed = consumer.Consume(timeout.Token);
        consumer.Commit(consumed);
        var results = await admin.DescribeClassicGroupsAsync([group, group + "-missing"],
            new() { IncludeAuthorizedOperations = true }, timeout.Token);
        await Assert.That(results[group].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(results[group + "-missing"].ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
        await Assert.That(results[group + "-missing"].Description).IsNull();
        var description = results[group].Description!;
        await Assert.That(description.ProtocolType).IsEqualTo("consumer");
        await Assert.That(description.ProtocolData).IsEqualTo("range");
        await Assert.That(description.State).IsEqualTo("Stable");
        var member = description.Members.Single();
        await Assert.That(member.MemberId).IsEqualTo(consumer.MemberId);
        await Assert.That(member.Assignment!.Any(p => p.Topic == topic && p.Partition == consumed.Partition.Value)).IsTrue();
        await Assert.That(member.AssignmentData.IsEmpty).IsFalse();
        await Assert.That(member.Metadata.IsEmpty).IsFalse();
        // The existing consumer-description surface still uses its KIP-848/classic fallback.
        var legacy = await admin.DescribeConsumerGroupsAsync([group], timeout.Token);
        await Assert.That(legacy[group].Members.Single().Assignment!).IsEquivalentTo(member.Assignment!);
        consumer.Close();
        var empty = await admin.DescribeClassicGroupsAsync([group], cancellationToken: timeout.Token);
        await Assert.That(empty[group].Description!.State).IsEqualTo("Empty");
        await Assert.That(empty[group].Description!.Members).IsEmpty();
    }
}
