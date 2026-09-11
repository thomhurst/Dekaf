using System.Reflection;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed partial class ShareConsumerRenewalTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Poll_LaterPartitionFailure_PreservesEarlierDeliveredDisposition(bool prepared)
    {
        var response = TwoPartitionResponse(2, 2);
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponses = new([response])
        };
        WindowCountingDeserializer deserializer = prepared
            ? new PreparedWindowCountingDeserializer(3) : new WindowCountingDeserializer(3);
        await using var fixture = CreateFixture(connection, ShareAcknowledgementMode.Implicit,
            maxPollRecords: 4, valueDeserializer: deserializer);
        PrepareForPoll(fixture.Consumer, new("topic", 0), new("topic", 1));
        fixture.Consumer.Subscribe("topic");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var poll = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
        for (var offset = 100L; offset < 102; offset++)
        {
            await Assert.That(await poll.MoveNextAsync()).IsTrue();
            await Assert.That(poll.Current.Offset).IsEqualTo(offset);
        }
        await Assert.That(async () => await poll.MoveNextAsync()).Throws<FormatException>();
        var outcomes = FlushPendingAcknowledgements(fixture.Consumer);
        await Assert.That(outcomes[new("topic", 0)][0].AcknowledgeTypes)
            .IsEquivalentTo([(byte)AcknowledgeType.Accept, (byte)AcknowledgeType.Accept]);
        await Assert.That(outcomes[new("topic", 1)][0].AcknowledgeTypes)
            .IsEquivalentTo([(byte)AcknowledgeType.Release, (byte)AcknowledgeType.Release]);
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    public async Task Poll_RevokedAcquisition_ReleasesParsedAndUnparsedRemainder(int maxPollRecords)
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponses = new([TwoPartitionResponse(3, 2)]),
            ShareAcknowledgeResponse = CreateAcknowledgeResponse((0, ErrorCode.None))
        };
        await using var fixture = CreateFixture(connection, ShareAcknowledgementMode.Implicit,
            maxPollRecords: maxPollRecords, valueDeserializer: Serializers.String);
        PrepareForPoll(fixture.Consumer, new("topic", 0), new("topic", 1));
        fixture.Consumer.Subscribe("topic");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using (var first = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator())
        {
            await Assert.That(await first.MoveNextAsync()).IsTrue();
            await Assert.That(first.Current.Offset).IsEqualTo(100);
        }

        PrepareForPoll(fixture.Consumer, new TopicPartition("topic", 1));
        await using var next = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
        await Assert.That(await next.MoveNextAsync()).IsTrue();
        await Assert.That(next.Current.Offset).IsEqualTo(200);
        await Assert.That(connection.ShareAcknowledgeRequests).HasSingleItem();
        var released = connection.ShareAcknowledgeRequests[0].Topics[0].Partitions[0];
        await Assert.That(released.PartitionIndex).IsEqualTo(0);
        await Assert.That(released.AcknowledgementBatches).HasSingleItem();
        await Assert.That(released.AcknowledgementBatches[0].FirstOffset).IsEqualTo(100);
        await Assert.That(released.AcknowledgementBatches[0].LastOffset).IsEqualTo(102);
        await Assert.That(released.AcknowledgementBatches[0].AcknowledgeTypes)
            .IsEquivalentTo([(byte)AcknowledgeType.Accept, (byte)AcknowledgeType.Release, (byte)AcknowledgeType.Release]);
        await Assert.That(connection.ShareFetchRequests).HasSingleItem();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Poll_RevokedAcquisition_RetriesReleaseAfterCommitFailure(bool emptyAssignment)
    {
        var acknowledged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponses = new([CreateFetchResponse(0, 100, 3), CreateFetchResponse(1, 200)]),
            ShareAcknowledgeHandler = (_, _) =>
            {
                if (++attempts == 1)
                    return new(CreateAcknowledgeResponse((0, ErrorCode.TopicAuthorizationFailed)));
                acknowledged.TrySetResult();
                return new(CreateAcknowledgeResponse((0, ErrorCode.None)));
            }
        };
        await using var fixture = CreateFixture(connection, ShareAcknowledgementMode.Implicit,
            maxPollRecords: 4, valueDeserializer: Serializers.String);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using (var first = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator())
            await Assert.That(await first.MoveNextAsync()).IsTrue();

        PrepareForPoll(fixture.Consumer, new TopicPartition("topic", 1));
        if (emptyAssignment)
        {
            var coordinator = typeof(KafkaShareConsumer<string, string>)
                .GetField("_coordinator", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Consumer)!;
            typeof(ShareConsumerCoordinator).GetField("_assignedPartitions", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(coordinator, new HashSet<TopicPartition>());
        }
        await using (var failed = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator())
            await Assert.That(async () => await failed.MoveNextAsync()).Throws<KafkaException>();

        await using var retry = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
        var pending = retry.MoveNextAsync().AsTask();
        await acknowledged.Task.WaitAsync(stop.Token);
        if (emptyAssignment)
        {
            stop.Cancel();
            await Assert.That(async () => await pending).Throws<OperationCanceledException>();
        }
        else
        {
            await Assert.That(await pending).IsTrue();
            await Assert.That(retry.Current.Offset).IsEqualTo(200);
        }
        await Assert.That(attempts).IsEqualTo(2);
        var released = connection.ShareAcknowledgeRequests[1].Topics[0].Partitions[0].AcknowledgementBatches[0];
        await Assert.That(released.AcknowledgeTypes)
            .IsEquivalentTo([(byte)AcknowledgeType.Accept, (byte)AcknowledgeType.Release, (byte)AcknowledgeType.Release]);
    }

    private static ShareFetchResponse TwoPartitionResponse(int firstCount, int secondCount) => new()
    {
        ErrorCode = ErrorCode.None,
        NodeEndpoints = [],
        Responses = [new ShareFetchResponseTopic
        {
            TopicId = TopicId,
            Partitions =
            [
                CreateFetchResponse(0, 100, firstCount).Responses[0].Partitions[0],
                CreateFetchResponse(1, 200, secondCount).Responses[0].Partitions[0]
            ]
        }]
    };
}
