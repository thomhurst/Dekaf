using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed partial class ShareConsumerRenewalTests
{
    [Test]
    public async Task Poll_PartitionLeaderMoves_OldBrokerForgetsPartitionOnce()
    {
        Fixture? fixture = null;
        long oldLeaderOffset = 100;
        long newLeaderOffset = 200;
        var oldLeader = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchHandler = (request, _) =>
            {
                // Partition 1 moves to broker 2 after the request that opens the session.
                if (request.ShareSessionEpoch == 0)
                    fixture!.MetadataManager.Metadata.Update(SessionMetadata(partition1Leader: 2));
                return new(CreateFetchResponse(0, oldLeaderOffset++));
            }
        };
        var newLeader = new CapturingConnection(ApiKey.ShareFetch, 2, brokerId: 2)
        {
            ShareFetchHandler = (_, _) => new(CreateFetchResponse(1, newLeaderOffset++))
        };
        await using var created = CreateFixture(oldLeader, ShareAcknowledgementMode.Implicit,
            secondConnection: newLeader, valueDeserializer: Serializers.String);
        fixture = created;
        fixture.MetadataManager.Metadata.Update(SessionMetadata(partition1Leader: 1));
        PrepareForPoll(fixture.Consumer, new TopicPartition("topic", 0), new TopicPartition("topic", 1));
        fixture.Consumer.Subscribe("topic");

        await PollUntilFetchCountAsync(fixture.Consumer, oldLeader, 3);

        var requests = oldLeader.ShareFetchRequests;
        await Assert.That(FetchedPartitions(requests[0])).IsEquivalentTo([0, 1]);
        await Assert.That(ForgottenPartitions(requests[0])).IsEmpty();
        await Assert.That(FetchedPartitions(requests[1])).IsEquivalentTo([0]);
        await Assert.That(requests[1].ForgottenTopicsData!.Single().TopicId).IsEqualTo(TopicId);
        await Assert.That(ForgottenPartitions(requests[1])).IsEquivalentTo([1]);
        await Assert.That(ForgottenPartitions(requests[2])).IsEmpty();
        await Assert.That(newLeader.ShareFetchRequests.All(static request => ForgottenPartitions(request).Length == 0)).IsTrue();
    }

    [Test]
    public async Task Poll_PartitionRevoked_BrokerForgetsPartitionOnce()
    {
        Fixture? fixture = null;
        long offset = 100;
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchHandler = (request, _) =>
            {
                // The group revokes partition 1 after the request that opens the session.
                if (request.ShareSessionEpoch == 0)
                    PrepareForPoll(fixture!.Consumer, new TopicPartition("topic", 0));
                return new(CreateFetchResponse(0, offset++));
            }
        };
        await using var created = CreateFixture(connection, ShareAcknowledgementMode.Implicit,
            valueDeserializer: Serializers.String);
        fixture = created;
        PrepareForPoll(fixture.Consumer, new TopicPartition("topic", 0), new TopicPartition("topic", 1));
        fixture.Consumer.Subscribe("topic");

        await PollUntilFetchCountAsync(fixture.Consumer, connection, 3);

        var requests = connection.ShareFetchRequests;
        await Assert.That(FetchedPartitions(requests[0])).IsEquivalentTo([0, 1]);
        await Assert.That(FetchedPartitions(requests[1])).IsEquivalentTo([0]);
        await Assert.That(ForgottenPartitions(requests[1])).IsEquivalentTo([1]);
        await Assert.That(ForgottenPartitions(requests[2])).IsEmpty();
    }

    [Test]
    public async Task Poll_TopicRecreated_ForgetsOldTopicIdAndFetchesNewOne()
    {
        Fixture? fixture = null;
        Guid? recreatedTopicId = null;
        long offset = 100;
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchHandler = (request, _) =>
            {
                if (request.ShareSessionEpoch == 0)
                {
                    var metadata = RecreatedTopicMetadata();
                    recreatedTopicId = metadata.Topics[0].TopicId;
                    fixture!.MetadataManager.Metadata.Update(metadata);
                    return new(CreateFetchResponse(0, offset++));
                }

                return new(CreateFetchResponse(0, offset++, topicId: recreatedTopicId));
            }
        };
        await using var created = CreateFixture(connection, ShareAcknowledgementMode.Implicit,
            valueDeserializer: Serializers.String);
        fixture = created;
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");

        await PollUntilFetchCountAsync(fixture.Consumer, connection, 3);

        var requests = connection.ShareFetchRequests;
        await Assert.That(requests[1].Topics.Single().TopicId).IsEqualTo(recreatedTopicId!.Value);
        await Assert.That(FetchedPartitions(requests[1])).IsEquivalentTo([0]);
        await Assert.That(requests[1].ForgottenTopicsData!.Single().TopicId).IsEqualTo(TopicId);
        await Assert.That(ForgottenPartitions(requests[1])).IsEquivalentTo([0]);
        await Assert.That(ForgottenPartitions(requests[2])).IsEmpty();
    }

    [Test]
    public async Task Poll_SessionReset_NewSessionForgetsNothing()
    {
        Fixture? fixture = null;
        long offset = 100;
        var requestCount = 0;
        var oldLeader = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchHandler = (_, _) =>
            {
                var requestNumber = ++requestCount;
                if (requestNumber == 1)
                    fixture!.MetadataManager.Metadata.Update(SessionMetadata(partition1Leader: 2));
                // The broker loses the session when the request that forgets partition 1 arrives.
                if (requestNumber == 2)
                {
                    return new(new ShareFetchResponse
                    {
                        ErrorCode = ErrorCode.ShareSessionNotFound,
                        Responses = [],
                        NodeEndpoints = []
                    });
                }

                return new(CreateFetchResponse(0, offset++));
            }
        };
        var newLeader = new CapturingConnection(ApiKey.ShareFetch, 2, brokerId: 2)
        {
            ShareFetchHandler = (_, _) => new(CreateFetchResponse(1, 500))
        };
        await using var created = CreateFixture(oldLeader, ShareAcknowledgementMode.Implicit,
            secondConnection: newLeader, valueDeserializer: Serializers.String);
        fixture = created;
        fixture.MetadataManager.Metadata.Update(SessionMetadata(partition1Leader: 1));
        PrepareForPoll(fixture.Consumer, new TopicPartition("topic", 0), new TopicPartition("topic", 1));
        fixture.Consumer.Subscribe("topic");

        await PollUntilFetchCountAsync(fixture.Consumer, oldLeader, 4);

        var requests = oldLeader.ShareFetchRequests;
        await Assert.That(requests[1].ShareSessionEpoch).IsEqualTo(1);
        await Assert.That(ForgottenPartitions(requests[1])).IsEquivalentTo([1]);
        await Assert.That(requests[2].ShareSessionEpoch).IsEqualTo(0);
        await Assert.That(FetchedPartitions(requests[2])).IsEquivalentTo([0]);
        await Assert.That(ForgottenPartitions(requests[2])).IsEmpty();
        await Assert.That(requests[3].ShareSessionEpoch).IsEqualTo(1);
        await Assert.That(ForgottenPartitions(requests[3])).IsEmpty();
    }

    [Test]
    public async Task Poll_PartitionBackingOff_StaysInShareSession()
    {
        var responses = new Queue<ShareFetchResponse>(
        [
            CreatePartitionErrorResponse(
                (0, ErrorCode.UnknownServerError, LeaderId: -1, LeaderEpoch: -1),
                (1, ErrorCode.None, LeaderId: -1, LeaderEpoch: -1)),
            CreateFetchResponse(1, 200)
        ]);
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchHandler = (_, _) => new(responses.Dequeue()),
            MetadataHandler = _ => new(BufferedLeaderMetadata(1))
        };
        await using var fixture = CreateFixture(connection, ShareAcknowledgementMode.Implicit,
            valueDeserializer: Serializers.String, retryBackoffMs: 60_000);
        PrepareForPoll(fixture.Consumer, new TopicPartition("topic", 0), new TopicPartition("topic", 1));
        fixture.Consumer.Subscribe("topic");

        await PollUntilFetchCountAsync(fixture.Consumer, connection, 2);

        var retried = connection.ShareFetchRequests[1];
        await Assert.That(FetchedPartitions(retried)).IsEquivalentTo([1]);
        await Assert.That(ForgottenPartitions(retried)).IsEmpty();
    }

    private static async Task PollUntilFetchCountAsync(
        KafkaShareConsumer<string, string> consumer,
        CapturingConnection connection,
        int fetchCount)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var poll = consumer.PollAsync(stop.Token).GetAsyncEnumerator();
        while (connection.ShareFetchRequests.Count < fetchCount)
            await Assert.That(await poll.MoveNextAsync()).IsTrue();
    }

    private static int[] FetchedPartitions(ShareFetchRequest request)
        => request.Topics.SelectMany(static topic => topic.Partitions)
            .Select(static partition => partition.PartitionIndex).ToArray();

    private static int[] ForgottenPartitions(ShareFetchRequest request)
        => request.ForgottenTopicsData?.SelectMany(static topic => topic.Partitions).ToArray() ?? [];

    private static MetadataResponse SessionMetadata(int partition1Leader) => new()
    {
        Brokers =
        [
            new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 },
            new BrokerMetadata { NodeId = 2, Host = "localhost", Port = 9093 }
        ],
        Topics =
        [
            new TopicMetadata
            {
                ErrorCode = ErrorCode.None,
                Name = "topic",
                TopicId = TopicId,
                Partitions =
                [
                    new PartitionMetadata
                    {
                        ErrorCode = ErrorCode.None, PartitionIndex = 0, LeaderId = 1,
                        ReplicaNodes = [1], IsrNodes = [1]
                    },
                    new PartitionMetadata
                    {
                        ErrorCode = ErrorCode.None, PartitionIndex = 1, LeaderId = partition1Leader,
                        ReplicaNodes = [partition1Leader], IsrNodes = [partition1Leader]
                    }
                ]
            }
        ]
    };
}
