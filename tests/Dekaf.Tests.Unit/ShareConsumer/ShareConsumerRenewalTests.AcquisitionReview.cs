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
    [Arguments(false, false, false)]
    [Arguments(true, false, false)]
    [Arguments(false, true, false)]
    [Arguments(true, true, false)]
    [Arguments(false, true, true)]
    [Arguments(true, true, true)]
    public async Task Poll_ParseFailure_ReleasesAcquisitionsAfterAssignmentChange(
        bool prepared, bool revoke, bool failFirstCommit)
    {
        var nextPartition = revoke ? 1 : 0;
        var attempts = 0;
        var failReleaseCommit = false;
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponses = new([CreateFetchResponse(0, 100, 3), CreateFetchResponse(nextPartition, 200)]),
            ShareAcknowledgeHandler = (_, _) => new(CreateAcknowledgeResponse((0,
                ++attempts == 1 && failReleaseCommit ? ErrorCode.TopicAuthorizationFailed : ErrorCode.None)))
        };
        WindowCountingDeserializer deserializer = prepared
            ? new PreparedWindowCountingDeserializer(2) : new WindowCountingDeserializer(2);
        await using var fixture = CreateFixture(connection, ShareAcknowledgementMode.Implicit,
            maxPollRecords: 1, valueDeserializer: deserializer);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using (var first = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator())
        {
            await Assert.That(await first.MoveNextAsync()).IsTrue();
            await Assert.That(first.Current.Offset).IsEqualTo(100);
        }
        await using (var failedParse = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator())
            await Assert.That(async () => await failedParse.MoveNextAsync()).Throws<FormatException>();

        attempts = 0;
        connection.ShareAcknowledgeRequests.Clear();
        failReleaseCommit = failFirstCommit;
        PrepareForPoll(fixture.Consumer, new TopicPartition("topic", nextPartition));
        if (failFirstCommit)
        {
            await using var failedCommit = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
            await Assert.That(async () => await failedCommit.MoveNextAsync()).Throws<KafkaException>();
            await Assert.That(connection.ShareFetchRequests).HasSingleItem();
        }

        await using var next = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
        await Assert.That(await next.MoveNextAsync()).IsTrue();
        await Assert.That(next.Current.Offset).IsEqualTo(200);
        await Assert.That(attempts).IsEqualTo(revoke ? (failFirstCommit ? 2 : 1) : 0);
        var released = revoke
            ? connection.ShareAcknowledgeRequests[^1].Topics[0].Partitions[0].AcknowledgementBatches[0]
            : null;
        var inline = revoke ? null
            : connection.ShareFetchRequests[^1].Topics[0].Partitions[0].AcknowledgementBatches?[0]
                ?? throw new InvalidOperationException("Expected inline acknowledgements for the retained partition.");
        // The first window committed offset 100 before parsing the remaining buffer.
        await Assert.That(released?.FirstOffset ?? inline!.FirstOffset).IsEqualTo(101);
        await Assert.That(released?.LastOffset ?? inline!.LastOffset).IsEqualTo(102);
        await Assert.That(released?.AcknowledgeTypes ?? inline!.AcknowledgeTypes)
            .IsEquivalentTo([(byte)AcknowledgeType.Release, (byte)AcknowledgeType.Release]);
    }

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

    [Test]
    [Arguments(1, false)]
    [Arguments(2, false)]
    [Arguments(1, true)]
    [Arguments(2, true)]
    public async Task Poll_Resubscribe_CommitsPreviousAcquisitionsBeforeNewFetch(
        int maxPollRecords, bool failFirstCommit)
    {
        var attempts = 0;
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponses = new([CreateFetchResponse(0, 100, 3), CreateFetchResponse(1, 200)]),
            ShareAcknowledgeHandler = (_, _) => new(CreateAcknowledgeResponse((0,
                ++attempts == 1 && failFirstCommit ? ErrorCode.TopicAuthorizationFailed : ErrorCode.None)))
        };
        await using var fixture = CreateFixture(connection, ShareAcknowledgementMode.Implicit,
            maxPollRecords: maxPollRecords, valueDeserializer: Serializers.String);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using (var first = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator())
        {
            await Assert.That(await first.MoveNextAsync()).IsTrue();
            await Assert.That(first.Current.Offset).IsEqualTo(100);
        }

        // Subscribe discards the old windows before the replacement assignment arrives.
        // Inline acknowledgements in its next ShareFetch cannot include partition zero.
        fixture.Consumer.Subscribe("topic", "additional-topic");
        PrepareForPoll(fixture.Consumer, new TopicPartition("topic", 1));
        if (failFirstCommit)
        {
            await using var failed = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
            await Assert.That(async () => await failed.MoveNextAsync()).Throws<KafkaException>();
            await Assert.That(connection.ShareFetchRequests).HasSingleItem();
        }

        await using var next = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
        await Assert.That(await next.MoveNextAsync()).IsTrue();
        await Assert.That(next.Current.Offset).IsEqualTo(200);
        await Assert.That(attempts).IsEqualTo(failFirstCommit ? 2 : 1);
        await Assert.That(connection.ShareFetchRequests.Count).IsEqualTo(2);
        var released = connection.ShareAcknowledgeRequests[^1].Topics[0].Partitions[0];
        await Assert.That(released.PartitionIndex).IsEqualTo(0);
        await Assert.That(released.AcknowledgementBatches[0].FirstOffset).IsEqualTo(100);
        await Assert.That(released.AcknowledgementBatches[0].LastOffset).IsEqualTo(102);
        await Assert.That(released.AcknowledgementBatches[0].AcknowledgeTypes)
            .IsEquivalentTo([(byte)AcknowledgeType.Accept, (byte)AcknowledgeType.Release, (byte)AcknowledgeType.Release]);
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    public async Task Poll_Resubscribe_RetainedAssignmentKeepsAcknowledgementsInline(int maxPollRecords)
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponses = new([CreateFetchResponse(0, 100, 3), CreateFetchResponse(0, 200)])
        };
        await using var fixture = CreateFixture(connection, ShareAcknowledgementMode.Implicit,
            maxPollRecords: maxPollRecords, valueDeserializer: Serializers.String);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using (var first = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator())
            await Assert.That(await first.MoveNextAsync()).IsTrue();

        fixture.Consumer.Subscribe("topic", "additional-topic");
        await using var next = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
        await Assert.That(await next.MoveNextAsync()).IsTrue();
        await Assert.That(next.Current.Offset).IsEqualTo(200);
        await Assert.That(connection.ShareAcknowledgeRequests).IsEmpty();
        var inline = connection.ShareFetchRequests[1].Topics[0].Partitions[0].AcknowledgementBatches
            ?? throw new InvalidOperationException("Expected inline acknowledgements for the retained partition.");
        await Assert.That(inline).HasSingleItem();
        await Assert.That(inline[0].AcknowledgeTypes)
            .IsEquivalentTo([(byte)AcknowledgeType.Accept, (byte)AcknowledgeType.Release, (byte)AcknowledgeType.Release]);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Poll_Resubscribe_RetriesFailedInlineReleaseAfterAssignmentChange(bool transportFailure)
    {
        var failedFetch = new TaskCompletionSource<ShareFetchResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var fetches = 0;
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchHandler = (request, _) => ++fetches switch
            {
                1 => new(CreateFetchResponse(0, 100, 3)),
                2 => new(failedFetch.Task),
                _ when request.Topics[0].Partitions[0].PartitionIndex == 0 =>
                    ValueTask.FromException<ShareFetchResponse>(new IOException("inline release retry failed")),
                _ => new(CreateFetchResponse(1, 200))
            },
            ShareAcknowledgeResponse = CreateAcknowledgeResponse((0, ErrorCode.None))
        };
        await using var fixture = CreateFixture(connection, ShareAcknowledgementMode.Implicit,
            maxPollRecords: 1, valueDeserializer: Serializers.String);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using (var first = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator())
            await Assert.That(await first.MoveNextAsync()).IsTrue();

        fixture.Consumer.Subscribe("topic", "additional-topic");
        await using var next = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
        var pending = next.MoveNextAsync().AsTask();
        await Assert.That(pending.IsCompleted).IsFalse();
        await Assert.That(connection.ShareFetchRequests[1].Topics[0].Partitions[0].AcknowledgementBatches)
            .IsNotNull();
        // The old assignment still allowed inline release when the request started.
        // Its failure arrives only after partition zero has been revoked.
        PrepareForPoll(fixture.Consumer, new TopicPartition("topic", 1));
        if (transportFailure)
        {
            failedFetch.SetException(new IOException("inline release failed"));
        }
        else
        {
            failedFetch.SetResult(new ShareFetchResponse
            {
                ErrorCode = ErrorCode.TopicAuthorizationFailed, Responses = [], NodeEndpoints = []
            });
        }
        await Assert.That(await pending).IsTrue();
        await Assert.That(next.Current.Offset).IsEqualTo(200);

        await Assert.That(connection.ShareAcknowledgeRequests).HasSingleItem();
        var released = connection.ShareAcknowledgeRequests[0].Topics[0].Partitions[0];
        await Assert.That(released.PartitionIndex).IsEqualTo(0);
        await Assert.That(released.AcknowledgementBatches[0].FirstOffset).IsEqualTo(100);
        await Assert.That(released.AcknowledgementBatches[0].LastOffset).IsEqualTo(102);
        await Assert.That(released.AcknowledgementBatches[0].AcknowledgeTypes)
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
