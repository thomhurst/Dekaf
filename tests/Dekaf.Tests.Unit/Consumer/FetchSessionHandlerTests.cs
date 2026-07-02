using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class FetchSessionHandlerTests
{
    [Test]
    public async Task Build_FirstRequest_UsesInitialEpochAndFullPartitionSet()
    {
        var handler = new FetchSessionHandler();
        var topics = Topics(("topic-a", 0, 100, 1024));

        var build = handler.Build(topics, clusterMetadata: null);

        await Assert.That(build.SessionId).IsEqualTo(0);
        await Assert.That(build.SessionEpoch).IsEqualTo(0);
        await Assert.That(build.IsFull).IsTrue();
        await Assert.That(build.Topics[0].Partitions).Count().IsEqualTo(1);
        await Assert.That(build.ForgottenTopicsData).IsNull();
    }

    [Test]
    public async Task Build_AfterSuccessfulInitialResponse_SendsOnlyChangedPartitions()
    {
        var handler = new FetchSessionHandler();
        var initial = Topics(("topic-a", 0, 100, 1024), ("topic-a", 1, 200, 1024));

        handler.Build(initial, clusterMetadata: null);
        handler.HandleResponse(Response(sessionId: 42));

        var unchanged = handler.Build(initial, clusterMetadata: null);
        await Assert.That(unchanged.SessionId).IsEqualTo(42);
        await Assert.That(unchanged.SessionEpoch).IsEqualTo(1);
        await Assert.That(unchanged.IsFull).IsFalse();
        await Assert.That(unchanged.Topics).IsEmpty();

        var changed = handler.Build(Topics(("topic-a", 0, 101, 1024), ("topic-a", 1, 200, 1024)), clusterMetadata: null);
        await Assert.That(changed.Topics).Count().IsEqualTo(1);
        await Assert.That(changed.Topics[0].Partitions).Count().IsEqualTo(1);
        await Assert.That(changed.Topics[0].Partitions[0].Partition).IsEqualTo(0);
        await Assert.That(changed.Topics[0].Partitions[0].FetchOffset).IsEqualTo(101);
    }

    [Test]
    public async Task Build_IncrementalDetectsForgottenPartitions()
    {
        var topicId = Guid.NewGuid();
        var metadata = Metadata("topic-a", topicId);
        var handler = new FetchSessionHandler();

        handler.Build(Topics(("topic-a", 0, 100, 1024), ("topic-a", 1, 200, 1024)), metadata);
        handler.HandleResponse(Response(sessionId: 42));

        var build = handler.Build(Topics(("topic-a", 1, 200, 1024)), metadata);

        await Assert.That(build.ForgottenTopicsData).IsNotNull();
        await Assert.That(build.ForgottenTopicsData![0].Topic).IsEqualTo("topic-a");
        await Assert.That(build.ForgottenTopicsData[0].TopicId).IsEqualTo(topicId);
        await Assert.That(build.ForgottenTopicsData[0].Partitions).IsEquivalentTo([0]);
    }

    [Test]
    public async Task Build_NoDesiredPartitions_ClosesActiveSession()
    {
        var handler = new FetchSessionHandler();

        handler.Build(Topics(("topic-a", 0, 100, 1024)), clusterMetadata: null);
        handler.HandleResponse(Response(sessionId: 42));

        var build = handler.Build([], clusterMetadata: null);

        await Assert.That(build.SessionId).IsEqualTo(42);
        await Assert.That(build.SessionEpoch).IsEqualTo(-1);
        await Assert.That(build.Topics).IsEmpty();
        await Assert.That(handler.HasActiveSession).IsFalse();
    }

    [Test]
    public async Task HandleResponse_FetchSessionIdNotFound_ResetsToInitial()
    {
        var handler = new FetchSessionHandler();
        var topics = Topics(("topic-a", 0, 100, 1024));

        handler.Build(topics, clusterMetadata: null);
        handler.HandleResponse(Response(sessionId: 42));
        handler.HandleResponse(Response(sessionId: 0, ErrorCode.FetchSessionIdNotFound));

        var build = handler.Build(topics, clusterMetadata: null);

        await Assert.That(build.SessionId).IsEqualTo(0);
        await Assert.That(build.SessionEpoch).IsEqualTo(0);
        await Assert.That(build.IsFull).IsTrue();
    }

    [Test]
    public async Task HandleResponse_InvalidEpoch_ClosesExistingAndAttemptsNewSession()
    {
        var handler = new FetchSessionHandler();
        var topics = Topics(("topic-a", 0, 100, 1024));

        handler.Build(topics, clusterMetadata: null);
        handler.HandleResponse(Response(sessionId: 42));
        handler.HandleResponse(Response(sessionId: 42, ErrorCode.InvalidFetchSessionEpoch));

        var build = handler.Build(topics, clusterMetadata: null);

        await Assert.That(build.SessionId).IsEqualTo(42);
        await Assert.That(build.SessionEpoch).IsEqualTo(0);
        await Assert.That(build.IsFull).IsTrue();
    }

    private static FetchResponse Response(int sessionId, ErrorCode errorCode = ErrorCode.None) => new()
    {
        SessionId = sessionId,
        ErrorCode = errorCode,
        Responses = []
    };

    private static List<FetchRequestTopic> Topics(params (string Topic, int Partition, long Offset, int MaxBytes)[] partitions)
    {
        var grouped = new Dictionary<string, List<FetchRequestPartition>>();
        foreach (var (topic, partition, offset, maxBytes) in partitions)
        {
            if (!grouped.TryGetValue(topic, out var list))
            {
                list = [];
                grouped[topic] = list;
            }

            list.Add(new FetchRequestPartition
            {
                Partition = partition,
                FetchOffset = offset,
                PartitionMaxBytes = maxBytes
            });
        }

        var result = new List<FetchRequestTopic>(grouped.Count);
        foreach (var (topic, topicPartitions) in grouped)
        {
            result.Add(new FetchRequestTopic
            {
                Topic = topic,
                Partitions = topicPartitions
            });
        }

        return result;
    }

    private static ClusterMetadata Metadata(string topic, Guid topicId)
    {
        var metadata = new ClusterMetadata();
        metadata.Update(new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }],
            Topics =
            [
                new TopicMetadata
                {
                    Name = topic,
                    TopicId = topicId,
                    ErrorCode = ErrorCode.None,
                    Partitions = [new PartitionMetadata { ErrorCode = ErrorCode.None, PartitionIndex = 0, LeaderId = 1, ReplicaNodes = [1], IsrNodes = [1] }]
                }
            ]
        });
        return metadata;
    }
}
