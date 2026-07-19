using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Metadata;

public sealed class OffsetTopicIdRequestMapTests
{
    [Test]
    public async Task AddTopic_MissingId_ThrowsRetriableUnknownTopicId()
    {
        var metadata = CreateMetadata(Guid.Empty);
        var map = new OffsetTopicIdRequestMap(metadata, 1);

        var exception = await Assert.That(() => map.AddTopic("test-topic", "OffsetFetch"))
            .Throws<KafkaException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.UnknownTopicId);
        await Assert.That(exception.IsRetriable).IsTrue();
        await Assert.That(ErrorCode.UnknownTopicId.RequiresMetadataRefresh()).IsTrue();
    }

    [Test]
    public async Task MatchResponseTopic_UnknownOrDuplicateId_ThrowsUnknownTopicId()
    {
        var topicId = Guid.NewGuid();
        var metadata = CreateMetadata(topicId);
        var map = new OffsetTopicIdRequestMap(metadata, 1);
        map.AddTopic("test-topic", "OffsetCommit");
        var responseSnapshot = map.CaptureResponseSnapshot();

        var unknown = await Assert.That(() =>
                map.MatchResponseTopic(
                    Guid.NewGuid(),
                    responseSnapshot,
                    "OffsetCommit",
                    responseMismatchIsRetriable: false))
            .Throws<KafkaException>();
        await Assert.That(unknown!.ErrorCode).IsEqualTo(ErrorCode.UnknownTopicId);
        await Assert.That(unknown.IsRetriable).IsFalse();

        await Assert.That(map.MatchResponseTopic(
                topicId,
                responseSnapshot,
                "OffsetCommit",
                responseMismatchIsRetriable: false))
            .IsEqualTo("test-topic");

        var duplicate = await Assert.That(() =>
                map.MatchResponseTopic(
                    topicId,
                    responseSnapshot,
                    "OffsetCommit",
                    responseMismatchIsRetriable: false))
            .Throws<KafkaException>();
        await Assert.That(duplicate!.ErrorCode).IsEqualTo(ErrorCode.UnknownTopicId);
        await Assert.That(duplicate.IsRetriable).IsFalse();
    }

    [Test]
    public async Task MatchResponseTopic_TopicRecreatedInFlight_RejectsStaleId()
    {
        var oldTopicId = Guid.NewGuid();
        var metadata = CreateMetadata(oldTopicId);
        var map = new OffsetTopicIdRequestMap(metadata, 1);
        map.AddTopic("test-topic", "OffsetFetch");

        metadata.Update(CreateMetadataResponse(Guid.NewGuid()));

        var exception = await Assert.That(() =>
                map.MatchResponseTopic(
                    oldTopicId,
                    map.CaptureResponseSnapshot(),
                    "OffsetFetch",
                    responseMismatchIsRetriable: true))
            .Throws<KafkaException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.UnknownTopicId);
        await Assert.That(exception.IsRetriable).IsTrue();
    }

    private static ClusterMetadata CreateMetadata(Guid topicId)
    {
        var metadata = new ClusterMetadata();
        metadata.Update(CreateMetadataResponse(topicId));
        return metadata;
    }

    private static MetadataResponse CreateMetadataResponse(Guid topicId) => new()
    {
        Brokers =
        [
            new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }
        ],
        Topics =
        [
            new TopicMetadata
            {
                Name = "test-topic",
                TopicId = topicId,
                ErrorCode = ErrorCode.None,
                Partitions =
                [
                    new PartitionMetadata
                    {
                        PartitionIndex = 0,
                        LeaderId = 1,
                        ErrorCode = ErrorCode.None,
                        ReplicaNodes = [1],
                        IsrNodes = [1]
                    }
                ]
            }
        ]
    };
}
