using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryTopicMutationTests
{
    [Test]
    [Arguments(1, false)]
    [Arguments(12, false)]
    [Arguments(1, true)]
    [Arguments(12, true)]
    public async Task CreateTopics_ExistingTopic_ThrowsWithoutChangingTopic(int partitions, bool validateOnly)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using var admin = new InMemoryAdminClient(cluster);
        var originalId = (await admin.DescribeTopicsAsync(["orders"]))["orders"].TopicId;

        async Task Create() => await admin.CreateTopicsAsync(
            [new() { Name = "orders", NumPartitions = partitions }],
            new() { ValidateOnly = validateOnly });

        var exception = await Assert.That(Create).Throws<KafkaException>();
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.TopicAlreadyExists);
        var topic = (await admin.DescribeTopicsAsync(["orders"]))["orders"];
        await Assert.That(topic.TopicId).IsEqualTo(originalId);
        await Assert.That(topic.Partitions.Count).IsEqualTo(1);
    }

    [Test]
    public async Task CreateTopics_AutoCreatedTopic_Throws()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("orders", "key", "value");
        await using var admin = new InMemoryAdminClient(cluster);

        async Task Create() => await admin.CreateTopicsAsync([new() { Name = "orders", NumPartitions = 12 }]);

        var exception = await Assert.That(Create).Throws<KafkaException>();
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.TopicAlreadyExists);
        await Assert.That((await admin.DescribeTopicsAsync(["orders"]))["orders"].Partitions.Count).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CreateTopics_MixedBatch_ProcessesSiblingsBeforeThrowingFirstError(bool validateOnly)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("existing-first");
        cluster.CreateTopic("existing-last");
        await using var admin = new InMemoryAdminClient(cluster);

        async Task Create() => await admin.CreateTopicsAsync(
            [new() { Name = "before" }, new() { Name = "existing-first" }, new() { Name = "after" }, new() { Name = "existing-last" }],
            new() { ValidateOnly = validateOnly });

        var exception = await Assert.That(Create).Throws<KafkaException>();
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.TopicAlreadyExists);
        await Assert.That(exception.Message).Contains("existing-first");
        string[] expected = validateOnly ? ["existing-first", "existing-last"] : ["before", "existing-first", "after", "existing-last"];
        await Assert.That(cluster.ListTopics()).IsEquivalentTo(expected);
    }

    [Test]
    public async Task CreateTopics_ValidateOnlyNewTopic_DoesNotCreateTopic()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);

        await admin.CreateTopicsAsync([new() { Name = "orders", NumPartitions = 12 }], new() { ValidateOnly = true });

        await Assert.That(cluster.ListTopics()).IsEmpty();
    }

    [Test]
    public async Task DeleteTopics_MissingTopic_Throws()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);

        async Task Delete() => await admin.DeleteTopicsAsync(["missing"]);

        var exception = await Assert.That(Delete).Throws<KafkaException>();
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        await Assert.That(exception.Message).Contains("missing");
        await Assert.That(cluster.ListTopics()).IsEmpty();
    }

    [Test]
    public async Task DeleteTopics_MixedBatch_DeletesSiblingsBeforeThrowingFirstError()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("before");
        cluster.CreateTopic("after");
        await using var admin = new InMemoryAdminClient(cluster);

        async Task Delete() => await admin.DeleteTopicsAsync(["before", "missing-first", "after", "missing-last"]);

        var exception = await Assert.That(Delete).Throws<KafkaException>();
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        await Assert.That(exception.Message).Contains("missing-first");
        await Assert.That(cluster.ListTopics()).IsEmpty();
    }
}
