using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed partial class ShareConsumerRenewalTests
{
    [Test]
    [Arguments(1)]
    [Arguments(2)]
    public async Task Poll_SameSubscription_RetainsBufferedAcquisitions(int maxPollRecords)
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponse = CreateFetchResponse(0, 100, 3),
            ShareAcknowledgeResponse = CreateAcknowledgeResponse((0, ErrorCode.None))
        };
        await using var fixture = CreateFixture(connection, ShareAcknowledgementMode.Implicit,
            maxPollRecords: maxPollRecords, valueDeserializer: Serializers.String);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using (var first = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator())
            await Assert.That(await first.MoveNextAsync()).IsTrue();

        fixture.Consumer.Subscribe("topic");
        await using var next = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
        await Assert.That(await next.MoveNextAsync()).IsTrue();
        await Assert.That(next.Current.Offset).IsEqualTo(101);
        await Assert.That(connection.ShareFetchRequests).HasSingleItem();
        await Assert.That(connection.ShareAcknowledgeRequests).HasSingleItem();
    }

    [Test]
    [Arguments(false, 1)]
    [Arguments(true, 1)]
    [Arguments(false, 2)]
    [Arguments(true, 2)]
    public async Task Poll_BufferedAcknowledgements_RefreshMissingLeader(bool implicitAcknowledgement, int maxPollRecords)
    {
        var refreshes = 0;
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponse = CreateFetchResponse(0, 100, 3),
            ShareAcknowledgeResponse = CreateAcknowledgeResponse((0, ErrorCode.None)),
            MetadataHandler = _ =>
            {
                refreshes++;
                return new(BufferedLeaderMetadata(1));
            }
        };
        await using var fixture = CreateFixture(connection,
            implicitAcknowledgement ? ShareAcknowledgementMode.Implicit : ShareAcknowledgementMode.Explicit,
            maxPollRecords: maxPollRecords, valueDeserializer: Serializers.String);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using (var first = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator())
        {
            await Assert.That(await first.MoveNextAsync()).IsTrue();
            await Assert.That(first.Current.Offset).IsEqualTo(100);
            if (!implicitAcknowledgement) fixture.Consumer.Acknowledge(first.Current);
        }
        fixture.MetadataManager.Metadata.Update(BufferedLeaderMetadata(-1));

        await using var next = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
        await Assert.That(await next.MoveNextAsync()).IsTrue();
        await Assert.That(next.Current.Offset).IsEqualTo(101);
        await Assert.That(refreshes).IsEqualTo(1);
        await Assert.That(connection.ShareFetchRequests).HasSingleItem();
        await Assert.That(connection.ShareAcknowledgeRequests).HasSingleItem();
        var acknowledged = connection.ShareAcknowledgeRequests[0].Topics[0].Partitions[0].AcknowledgementBatches[0];
        await Assert.That(acknowledged.FirstOffset).IsEqualTo(100);
        await Assert.That(acknowledged.LastOffset).IsEqualTo(100);
        await Assert.That(acknowledged.AcknowledgeTypes).IsEquivalentTo([(byte)AcknowledgeType.Accept]);
    }

    [Test]
    public async Task Poll_BufferedAcknowledgements_CancellationDuringRefreshPreservesOutcome()
    {
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var metadata = new TaskCompletionSource<MetadataResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponse = CreateFetchResponse(0, 100, 3),
            MetadataHandler = token =>
            {
                requested.TrySetResult();
                return new(metadata.Task.WaitAsync(token));
            }
        };
        await using var fixture = CreateFixture(connection, ShareAcknowledgementMode.Implicit,
            maxPollRecords: 1, valueDeserializer: Serializers.String);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using (var first = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator())
            await Assert.That(await first.MoveNextAsync()).IsTrue();
        fixture.MetadataManager.Metadata.Update(BufferedLeaderMetadata(-1));
        await using var next = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
        var pending = next.MoveNextAsync().AsTask();
        try
        {
            await requested.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await stop.CancelAsync();
            await Assert.That(async () => await pending).Throws<OperationCanceledException>();
        }
        finally
        {
            await stop.CancelAsync();
            metadata.TrySetCanceled();
            try { await pending; } catch { }
        }
        await Assert.That(connection.ShareAcknowledgeRequests).IsEmpty();
        var outcomes = FlushPendingAcknowledgements(fixture.Consumer);
        await Assert.That(outcomes[new("topic", 0)][0].FirstOffset).IsEqualTo(100);
        await Assert.That(outcomes[new("topic", 0)][0].AcknowledgeTypes)
            .IsEquivalentTo([(byte)AcknowledgeType.Accept]);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Commit_MissingLeaderRecovery_ObservesOtherRequests(bool cancelRefresh)
    {
        var firstReply = new TaskCompletionSource<ShareAcknowledgeResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var metadata = new TaskCompletionSource<MetadataResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sends = 0;
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2)
        {
            ShareAcknowledgeHandler = (_, _) => ++sends == 1
                ? new(firstReply.Task) : new(CreateAcknowledgeResponse((0, ErrorCode.None))),
            MetadataHandler = token =>
            {
                refreshing.TrySetResult();
                return new(metadata.Task.WaitAsync(token));
            }
        };
        var outcomes = new List<ShareAcknowledgementCommitResult>();
        await using var fixture = CreateFixture(connection,
            acknowledgementCommitCallback: results => outcomes.AddRange(results.ToArray()));
        PrepareForPoll(fixture.Consumer, new("topic", 0), new("topic", 1));
        fixture.MetadataManager.Metadata.Update(BufferedLeaderMetadata(-1));
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 100));
        fixture.Consumer.Acknowledge(CreateRecord(partition: 1, offset: 200));
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var pending = fixture.Consumer.CommitAsync(stop.Token).AsTask();
        try
        {
            await Assert.That(sends).IsEqualTo(1);
            await Assert.That(refreshing.Task.IsCompleted).IsFalse();
            firstReply.SetResult(CreateAcknowledgeResponse((1, ErrorCode.None)));
            await refreshing.Task.WaitAsync(stop.Token);
            if (cancelRefresh)
            {
                await stop.CancelAsync();
                await Assert.That(async () => await pending).Throws<OperationCanceledException>();
                await Assert.That(outcomes).HasSingleItem();
                await Assert.That(outcomes[0].Succeeded).IsTrue();
                var requeued = FlushPendingAcknowledgements(fixture.Consumer);
                await Assert.That(requeued.Keys).IsEquivalentTo([new TopicPartition("topic", 0)]);
                await Assert.That(requeued[new("topic", 0)][0].FirstOffset).IsEqualTo(100);
            }
            else
            {
                metadata.SetResult(BufferedLeaderMetadata(1));
                await pending;
                await Assert.That(sends).IsEqualTo(2);
                await Assert.That(outcomes.Count).IsEqualTo(2);
                await Assert.That(outcomes.All(static outcome => outcome.Succeeded)).IsTrue();
                await Assert.That(HasPendingAcknowledgements(fixture.Consumer)).IsFalse();
            }
        }
        finally
        {
            await stop.CancelAsync();
            firstReply.TrySetCanceled();
            metadata.TrySetCanceled();
            try { await pending; } catch { }
        }
    }

    private static MetadataResponse BufferedLeaderMetadata(int leader) => new()
    {
        Brokers = [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }],
        Topics = [new TopicMetadata
        {
            ErrorCode = ErrorCode.None, Name = "topic", TopicId = TopicId,
            Partitions =
            [
                new PartitionMetadata
                {
                    ErrorCode = ErrorCode.None, PartitionIndex = 0, LeaderId = leader,
                    ReplicaNodes = [1], IsrNodes = [1]
                },
                new PartitionMetadata
                {
                    ErrorCode = ErrorCode.None, PartitionIndex = 1, LeaderId = 1,
                    ReplicaNodes = [1], IsrNodes = [1]
                }
            ]
        }]
    };
}
