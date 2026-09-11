using Dekaf.Admin;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Integration;

[Category("Admin")]
[SupportsKafka(420)]
public sealed class AdminDetailedShareGroupOffsetTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ShareOffsets_RetainMixedOutcomesAndDeleteWholeTopics()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var missing = $"missing-{Guid.NewGuid():N}";
        var group = $"detailed-share-{Guid.NewGuid():N}";
        await using IAdminClient admin = KafkaContainer.CreateAdminClient();
        var first = new TopicPartition(topic, 0);
        var second = new TopicPartition(topic, 1);
        var absent = new TopicPartition(missing, 0);
        await WaitForShareTopicMetadataAsync(group, topic);
        var results = await admin.AlterShareGroupOffsetsDetailedAsync(group,
            [new() { TopicPartition = first, StartOffset = 0 },
             new() { TopicPartition = second, StartOffset = 0 },
             new() { TopicPartition = absent, StartOffset = 0 }]);
        foreach (var (partition, result) in results)
            Console.WriteLine($"Share offset result: {partition}; outcome={result.Outcome}; error={result.ErrorCode}; message={result.ErrorMessage}; exception={result.Exception}");
        await Assert.That(results.Count).IsEqualTo(3);
        await Assert.That(results[first].IsSuccess).IsTrue();
        await Assert.That(results[second].IsSuccess).IsTrue();
        await Assert.That(results[absent].ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        var stored = await admin.DescribeShareGroupOffsetsAsync(group, [first, second]);
        await Assert.That(stored.Single(offset => offset.TopicPartition == first).StartOffset).IsEqualTo(0);
        await Assert.That(stored.Single(offset => offset.TopicPartition == second).StartOffset).IsEqualTo(0);
        var deleted = await admin.DeleteShareGroupOffsetsDetailedAsync(group, [topic, missing]);
        await Assert.That(deleted.Count).IsEqualTo(2);
        await Assert.That(deleted[topic].IsSuccess).IsTrue();
        await Assert.That(deleted[missing].ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
    }

    private async Task WaitForShareTopicMetadataAsync(string group, string topic)
    {
        // CreateTopics can complete before the group coordinator observes the new topic.
        // DescribeShareGroupOffsets returns an empty topic ID while that metadata is absent.
        // Wait for the topic and both partition reads; the mutation and its missing-topic
        // assertion run once. This test uses the single-broker fixture.
        var endpoint = BootstrapServerList.Parse(KafkaContainer.BootstrapServers);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var connection = new KafkaConnection(endpoint.Host, endpoint.Port, "share-topic-readiness");
        await connection.ConnectAsync(deadline.Token);
        var request = new DescribeShareGroupOffsetsRequest
        {
            Groups = [new()
            {
                GroupId = group,
                Topics = [new() { TopicName = topic, Partitions = [0, 1] }]
            }]
        };
        await TestWait.WaitForConditionAsync(async () =>
        {
            var response = await connection.SendAsync<DescribeShareGroupOffsetsRequest, DescribeShareGroupOffsetsResponse>(
                request, 0, deadline.Token);
            var observed = response.Groups.Single();
            var metadata = observed.Topics.SingleOrDefault(item => item.TopicName == topic);
            Console.WriteLine($"Share topic readiness: groupError={observed.ErrorCode}; topicId={metadata?.TopicId}; partitionErrors={string.Join(',', metadata?.Partitions.Select(static partition => partition.ErrorCode) ?? [])}");
            return observed.ErrorCode == ErrorCode.None && metadata is { TopicId: var id } && id != Guid.Empty
                && metadata.Partitions.Count == 2
                && metadata.Partitions.All(static partition => partition.ErrorCode == ErrorCode.None);
        }, static ready => ready, description: "share coordinator topic metadata after CreateTopics");
    }

    [Test]
    public async Task DeleteMissingGroup_PreservesGroupErrorForEveryTopic()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        await using var admin = new AdminClientBuilder().WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        var results = await admin.DeleteShareGroupOffsetsDetailedAsync($"missing-{Guid.NewGuid():N}", [topic, "other"]);
        await Assert.That(results.Count).IsEqualTo(2);
        foreach (var result in results.Values)
        {
            await Assert.That(result.Outcome).IsEqualTo(AdminMutationOutcome.Failed);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
        }
    }
}
