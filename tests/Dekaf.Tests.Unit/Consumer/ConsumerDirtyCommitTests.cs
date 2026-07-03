using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumerDirtyCommitTests
{
    [Test]
    public async Task CommitAsync_AfterSuccessfulCommit_CommitsOnlyOffsetsChangedSinceLastCommit()
    {
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);

        consumer.Seek(new TopicPartitionOffset("topic-a", 0, 10));
        consumer.Seek(new TopicPartitionOffset("topic-a", 1, 20));

        await consumer.CommitAsync(CancellationToken.None);

        consumer.Seek(new TopicPartitionOffset("topic-a", 1, 21));

        await consumer.CommitAsync(CancellationToken.None);

        await Assert.That(requests.Count).IsEqualTo(2);
        await Assert.That(GetCommittedOffsets(requests[0])).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 0, 10),
            new TopicPartitionOffset("topic-a", 1, 20)
        ]);
        await Assert.That(GetCommittedOffsets(requests[1])).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 1, 21)
        ]);
    }

    [Test]
    public async Task CommitAsync_WhenNoOffsetsChangedAfterSuccessfulCommit_DoesNotSendCommitRequest()
    {
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);

        consumer.Seek(new TopicPartitionOffset("topic-a", 0, 10));

        await consumer.CommitAsync(CancellationToken.None);
        await consumer.CommitAsync(CancellationToken.None);

        await Assert.That(requests.Count).IsEqualTo(1);
    }

    [Test]
    public async Task CommitAsync_WhenCommitFails_PreservesDirtyOffsetsForRetry()
    {
        var requests = new List<OffsetCommitRequest>();
        var responseErrors = new Queue<ErrorCode>([ErrorCode.InvalidCommitOffsetSize, ErrorCode.None]);
        await using var consumer = CreateConsumer(requests, responseErrors);

        consumer.Seek(new TopicPartitionOffset("topic-a", 0, 10));

        await Assert.That(async () => await consumer.CommitAsync(CancellationToken.None))
            .Throws<GroupException>();

        await consumer.CommitAsync(CancellationToken.None);

        await Assert.That(requests.Count).IsEqualTo(2);
        await Assert.That(GetCommittedOffsets(requests[0])).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 0, 10)
        ]);
        await Assert.That(GetCommittedOffsets(requests[1])).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 0, 10)
        ]);
    }

    [Test]
    public async Task CommitAsync_AfterNonDirtyPositionReset_DoesNotCommitStaleDirtyOffset()
    {
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);
        var partition = new TopicPartition("topic-a", 0);

        consumer.Seek(new TopicPartitionOffset("topic-a", 0, 10));
        SetPosition(consumer, partition, 2, dirty: false);

        await Assert.That(consumer.GetPosition(partition)).IsEqualTo(2);

        await consumer.CommitAsync(CancellationToken.None);

        await Assert.That(requests).IsEmpty();
    }

    private static KafkaConsumer<string, string> CreateConsumer(
        List<OffsetCommitRequest> requests,
        ErrorCode responseError)
        => CreateConsumer(requests, new Queue<ErrorCode>([responseError]));

    private static KafkaConsumer<string, string> CreateConsumer(
        List<OffsetCommitRequest> requests,
        Queue<ErrorCode> responseErrors)
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();

        connectionPool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));

        connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<OffsetCommitRequest>();
                requests.Add(CloneRequest(request));

                var error = responseErrors.Count == 0 ? ErrorCode.None : responseErrors.Dequeue();
                return ValueTask.FromResult(CreateResponse(request, error));
            });

        var metadataManager = new MetadataManager(connectionPool, ["localhost:9092"]);
        metadataManager.SetApiVersion(
            ApiKey.OffsetCommit,
            OffsetCommitRequest.LowestSupportedVersion,
            OffsetCommitRequest.HighestSupportedVersion);

        return new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "group-a",
                OffsetCommitMode = OffsetCommitMode.Manual
            },
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager);
    }

    private static void SetPosition(
        KafkaConsumer<string, string> consumer,
        TopicPartition partition,
        long position,
        bool dirty)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "SetPosition",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        method.Invoke(consumer, [partition, position, dirty]);
    }

    private static OffsetCommitRequest CloneRequest(OffsetCommitRequest request)
    {
        return new OffsetCommitRequest
        {
            GroupId = request.GroupId,
            GenerationIdOrMemberEpoch = request.GenerationIdOrMemberEpoch,
            MemberId = request.MemberId,
            GroupInstanceId = request.GroupInstanceId,
            Topics = request.Topics
                .Select(static topic => new OffsetCommitRequestTopic
                {
                    Name = topic.Name,
                    Partitions = topic.Partitions
                        .Select(static partition => new OffsetCommitRequestPartition
                        {
                            PartitionIndex = partition.PartitionIndex,
                            CommittedOffset = partition.CommittedOffset
                        })
                        .ToArray()
                })
                .ToArray()
        };
    }

    private static OffsetCommitResponse CreateResponse(OffsetCommitRequest request, ErrorCode error)
    {
        return new OffsetCommitResponse
        {
            Topics = request.Topics
                .Select(topic => new OffsetCommitResponseTopic
                {
                    Name = topic.Name,
                    Partitions = topic.Partitions
                        .Select(partition => new OffsetCommitResponsePartition
                        {
                            PartitionIndex = partition.PartitionIndex,
                            ErrorCode = error
                        })
                        .ToArray()
                })
                .ToArray()
        };
    }

    private static TopicPartitionOffset[] GetCommittedOffsets(OffsetCommitRequest request)
    {
        return request.Topics
            .SelectMany(static topic => topic.Partitions.Select(partition =>
                new TopicPartitionOffset(topic.Name, partition.PartitionIndex, partition.CommittedOffset)))
            .OrderBy(static offset => offset.Topic, StringComparer.Ordinal)
            .ThenBy(static offset => offset.Partition)
            .ThenBy(static offset => offset.Offset)
            .ToArray();
    }
}
