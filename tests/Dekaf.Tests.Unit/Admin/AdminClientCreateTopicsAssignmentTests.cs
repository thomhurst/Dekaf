using System.Buffers;
using Dekaf.Admin;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientCreateTopicsAssignmentTests
{
    [Test]
    [Arguments(false, 1, 1)]
    [Arguments(true, 1, 1)]
    [Arguments(false, 8, 3)]
    [Arguments(true, 8, 3)]
    [Arguments(false, -1, -1)]
    [Arguments(true, -1, -1)]
    [Arguments(false, 0, 0)]
    [Arguments(true, 0, 0)]
    public async Task ManualAssignments_OverrideCounts(bool detailed, int partitions, int replicas)
    {
        var topic = new NewTopic
        {
            Name = "assigned",
            NumPartitions = partitions,
            ReplicationFactor = (short)replicas,
            ReplicaAssignments = new Dictionary<int, IReadOnlyList<int>> { [0] = [2, 1], [1] = [1, 3] }
        };

        var sent = await CaptureTopicAsync(topic, detailed);
        var counts = ReadEncodedCounts(sent);

        await Assert.That(counts.Partitions).IsEqualTo(-1);
        await Assert.That(counts.Replicas).IsEqualTo((short)-1);
        await Assert.That(sent.Assignments!.Count).IsEqualTo(2);
        await Assert.That(sent.Assignments[0].PartitionIndex).IsEqualTo(0);
        await Assert.That(sent.Assignments[0].BrokerIds.SequenceEqual([2, 1])).IsTrue();
        await Assert.That(sent.Assignments[1].PartitionIndex).IsEqualTo(1);
        await Assert.That(sent.Assignments[1].BrokerIds.SequenceEqual([1, 3])).IsTrue();
        await Assert.That(topic.NumPartitions).IsEqualTo(partitions);
        await Assert.That(topic.ReplicationFactor).IsEqualTo((short)replicas);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task NoAssignments_PreserveCounts(bool detailed, bool empty)
    {
        var topic = new NewTopic
        {
            Name = "automatic",
            NumPartitions = 4,
            ReplicationFactor = 2,
            ReplicaAssignments = empty ? new Dictionary<int, IReadOnlyList<int>>() : null
        };

        var sent = await CaptureTopicAsync(topic, detailed);
        var counts = ReadEncodedCounts(sent);

        await Assert.That(counts.Partitions).IsEqualTo(4);
        await Assert.That(counts.Replicas).IsEqualTo((short)2);
        await Assert.That(sent.Assignments?.Count ?? 0).IsEqualTo(0);
    }

    private static async Task<CreateTopicData> CaptureTopicAsync(NewTopic topic, bool detailed)
    {
        var (admin, connection) = AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(ApiKey.CreateTopics);
        await using var client = admin;
        CreateTopicData? captured = null;
        connection.SendAsync<CreateTopicsRequest, CreateTopicsResponse>(
                Arg.Any<CreateTopicsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                captured = call.Arg<CreateTopicsRequest>().Topics.Single();
                return ValueTask.FromResult(new CreateTopicsResponse { Topics = [new() { Name = topic.Name }] });
            });
        var options = new CreateTopicsOptions { ValidateOnly = true };
        if (detailed)
        {
            var results = await admin.CreateTopicsDetailedAsync([topic], options);
            await Assert.That(results[topic.Name].IsSuccess).IsTrue();
        }
        else
        {
            await admin.CreateTopicsAsync([topic], options);
        }
        return captured ?? throw new InvalidOperationException("CreateTopics request was not sent.");
    }

    private static (int Partitions, short Replicas) ReadEncodedCounts(CreateTopicData topic)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        topic.Write(ref writer, version: 7);
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        _ = reader.ReadCompactString();
        return (reader.ReadInt32(), reader.ReadInt16());
    }
}
