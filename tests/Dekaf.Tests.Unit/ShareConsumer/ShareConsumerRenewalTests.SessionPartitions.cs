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
    [Arguments(ShareAcknowledgementMode.Implicit)]
    [Arguments(ShareAcknowledgementMode.Explicit)]
    public async Task Poll_FullyReadPartitionRevoked_SendsItsAcknowledgements(ShareAcknowledgementMode mode)
    {
        long offset = 101;
        var requestCount = 0;
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchHandler = (_, _) => new(++requestCount == 1
                ? CreateTwoPartitionFetchResponse()
                : CreateFetchResponse(0, offset++)),
            ShareAcknowledgeHandler = (_, _) => new(CreateAcknowledgeResponse((0, ErrorCode.None), (1, ErrorCode.None)))
        };
        var outcomes = new List<ShareAcknowledgementCommitResult>();
        await using var fixture = CreateFixture(connection, mode,
            acknowledgementCommitCallback: results => outcomes.AddRange(results.ToArray()),
            valueDeserializer: Serializers.String);
        PrepareForPoll(fixture.Consumer, new TopicPartition("topic", 0), new TopicPartition("topic", 1));
        fixture.Consumer.Subscribe("topic");

        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using (var poll = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator())
        {
            // Read every buffered record of both partitions, so nothing of partition 1 stays buffered.
            for (var read = 0; read < 2; read++)
            {
                await Assert.That(await poll.MoveNextAsync()).IsTrue();
                if (mode == ShareAcknowledgementMode.Explicit)
                    fixture.Consumer.Acknowledge(poll.Current, AcknowledgeType.Accept);
            }

            PrepareForPoll(fixture.Consumer, new TopicPartition("topic", 0));
            while (connection.ShareFetchRequests.Count < 3)
            {
                await Assert.That(await poll.MoveNextAsync()).IsTrue();
                if (mode == ShareAcknowledgementMode.Explicit)
                    fixture.Consumer.Acknowledge(poll.Current, AcknowledgeType.Accept);
            }
        }

        await Assert.That(connection.ShareAcknowledgeRequests).HasSingleItem();
        // The outside-assignment drain commits every pending acknowledgement, partition 1 included.
        var released = connection.ShareAcknowledgeRequests[0].Topics.Single().Partitions
            .Single(static partition => partition.PartitionIndex == 1);
        await Assert.That(released.AcknowledgementBatches.Single().FirstOffset).IsEqualTo(200);
        await Assert.That(released.AcknowledgementBatches.Single().AcknowledgeTypes)
            .IsEquivalentTo([(byte)AcknowledgeType.Accept]);
        await Assert.That(connection.ShareFetchRequests.Any(static request =>
            request.Topics.Any(static topic => topic.Partitions.Any(static partition =>
                partition.PartitionIndex == 1 && partition.AcknowledgementBatches is { Count: > 0 })))).IsFalse();
        var partition1 = outcomes.Where(static outcome => outcome.TopicPartition.Partition == 1).ToArray();
        await Assert.That(partition1).HasSingleItem();
        await Assert.That(partition1[0].Exception).IsNull();
    }

    private static ShareFetchResponse CreateTwoPartitionFetchResponse()
    {
        var partition0 = CreateFetchResponse(0, 100);
        var partition1 = CreateFetchResponse(1, 200);
        return new ShareFetchResponse
        {
            ErrorCode = ErrorCode.None,
            Responses =
            [
                new ShareFetchResponseTopic
                {
                    TopicId = TopicId,
                    Partitions = [.. partition0.Responses[0].Partitions, .. partition1.Responses[0].Partitions]
                }
            ],
            NodeEndpoints = []
        };
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

    [Test]
    [Arguments("close")]
    [Arguments("dispose")]
    [Arguments("unsubscribe")]
    public async Task Teardown_AllPartitionLeadersMove_ClosesOldBrokerSession(string operation)
    {
        var oldLeader = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchHandler = (_, _) => new(CreateFetchResponse(0, 100))
        };
        var newLeader = new CapturingConnection(ApiKey.ShareFetch, 2, brokerId: 2,
            includeShareAcknowledge: true)
        {
            ShareFetchHandler = (_, _) => new(CreateFetchResponse(0, 200))
        };
        await using var fixture = CreateFixture(oldLeader, secondConnection: newLeader,
            valueDeserializer: Serializers.String);
        fixture.MetadataManager.Metadata.Update(SessionMetadata(partition1Leader: 1));
        PrepareForPoll(fixture.Consumer, new TopicPartition("topic", 0), new TopicPartition("topic", 1));
        fixture.Consumer.Subscribe("topic");
        await PollUntilFetchCountAsync(fixture.Consumer, oldLeader, 1);

        fixture.MetadataManager.Metadata.Update(SessionMetadata(partition1Leader: 2, partition0Leader: 2));
        await PollUntilFetchCountAsync(fixture.Consumer, newLeader, 1);
        await Assert.That(oldLeader.ShareFetchRequests.Count).IsEqualTo(1);

        await TeardownAsync(fixture.Consumer, operation);

        await AssertSessionClosedAsync(oldLeader, operation);
        await AssertSessionClosedAsync(newLeader, operation);
    }

    [Test]
    [Arguments("close")]
    [Arguments("dispose")]
    [Arguments("unsubscribe")]
    public async Task Teardown_NoAssignment_ClosesResetBrokerSession(string operation)
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchHandler = (_, _) => new(CreateFetchResponse(0, 100))
        };
        await using var fixture = CreateFixture(connection, valueDeserializer: Serializers.String);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        await PollUntilFetchCountAsync(fixture.Consumer, connection, 1);
        // A revocation or topic deletion leaves no current leader to route teardown to.
        typeof(KafkaShareConsumer<string, string>)
            .GetField("_assignmentSnapshot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(fixture.Consumer, new HashSet<TopicPartition>());
        var sessions = (ShareSessionManager)typeof(KafkaShareConsumer<string, string>)
            .GetField("_sessionManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(fixture.Consumer)!;
        sessions.ResetSession(1);

        await TeardownAsync(fixture.Consumer, operation);

        await AssertSessionClosedAsync(connection, operation);
    }

    private static async Task TeardownAsync(KafkaShareConsumer<string, string> consumer, string operation)
    {
        if (operation == "close")
            await consumer.CloseAsync();
        else if (operation == "dispose")
            await consumer.DisposeAsync();
        else
        {
            consumer.Unsubscribe();
            // Unsubscribe queues final acknowledgements. Close waits for that queue.
            await consumer.CloseAsync();
        }
    }

    private static async Task AssertSessionClosedAsync(CapturingConnection connection, string operation)
    {
        if (operation == "unsubscribe")
            await Assert.That(connection.ShareAcknowledgeRequests.Count(static request =>
                request.ShareSessionEpoch == ShareSessionManager.CloseEpoch)).IsEqualTo(1);
        else
            await Assert.That(connection.ShareFetchRequests.Count(static request =>
                request.ShareSessionEpoch == ShareSessionManager.CloseEpoch)).IsEqualTo(1);
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

    private static MetadataResponse SessionMetadata(int partition1Leader, int partition0Leader = 1) => new()
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
                        ErrorCode = ErrorCode.None, PartitionIndex = 0, LeaderId = partition0Leader,
                        ReplicaNodes = [partition0Leader], IsrNodes = [partition0Leader]
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
