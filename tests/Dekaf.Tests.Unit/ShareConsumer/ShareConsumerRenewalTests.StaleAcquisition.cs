using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed partial class ShareConsumerRenewalTests
{
    [Test]
    public async Task Poll_InlineStaleAcquisition_DropsFailedPartitionAndReportsIt()
    {
        using var cancellation = new CancellationTokenSource();
        ShareAcknowledgementCommitResult[]? outcomes = null;
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponse = new ShareFetchResponse
            {
                ErrorCode = ErrorCode.None,
                Responses =
                [
                    new ShareFetchResponseTopic
                    {
                        TopicId = TopicId,
                        Partitions =
                        [
                            new ShareFetchResponsePartition
                            {
                                PartitionIndex = 0,
                                AcknowledgeErrorCode = ErrorCode.TopicAuthorizationFailed,
                                CurrentLeader = new ShareFetchLeaderIdAndEpoch(),
                                AcquiredRecords = []
                            },
                            new ShareFetchResponsePartition
                            {
                                PartitionIndex = 1,
                                AcknowledgeErrorCode = ErrorCode.InvalidRecordState,
                                CurrentLeader = new ShareFetchLeaderIdAndEpoch(),
                                AcquiredRecords = []
                            }
                        ]
                    }
                ],
                NodeEndpoints = []
            },
            OnSend = cancellation.Cancel
        };
        await using var fixture = CreateFixture(connection,
            acknowledgementCommitCallback: results => outcomes = results.ToArray());
        PrepareForPoll(
            fixture.Consumer,
            new TopicPartition("topic", 0),
            new TopicPartition("topic", 1));
        fixture.Consumer.Subscribe("topic");
        EstablishSessions(fixture.Consumer, 1);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 40), AcknowledgeType.Accept);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 1, offset: 41), AcknowledgeType.Accept);

        await using var poll = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
        await poll.MoveNextAsync();

        // The broker no longer holds partition 1's acquisition, so resending can never succeed.
        var pending = FlushPendingAcknowledgements(fixture.Consumer);
        await Assert.That(pending.Keys).IsEquivalentTo([new TopicPartition("topic", 0)]);
        var stale = outcomes!.Single(outcome => outcome.TopicPartition.Partition == 1);
        await Assert.That(((KafkaException)stale.Exception!).ErrorCode).IsEqualTo(ErrorCode.InvalidRecordState);
    }

    [Test]
    public async Task Commit_StaleAcquisition_ReportsWithoutRequeue()
    {
        ShareAcknowledgementCommitResult[]? outcomes = null;
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2)
        {
            ShareAcknowledgeResponse = CreateAcknowledgeResponse(
                (0, ErrorCode.TopicAuthorizationFailed),
                (1, ErrorCode.InvalidRecordState))
        };
        await using var fixture = CreateFixture(
            connection,
            acknowledgementCommitCallback: results => outcomes = results.ToArray());
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 40));
        fixture.Consumer.Acknowledge(CreateRecord(partition: 1, offset: 41));

        await Assert.That(async () => await fixture.Consumer.CommitAsync())
            .Throws<KafkaException>();

        await Assert.That(((KafkaException)outcomes![1].Exception!).ErrorCode)
            .IsEqualTo(ErrorCode.InvalidRecordState);
        var pending = FlushPendingAcknowledgements(fixture.Consumer);
        await Assert.That(pending.Keys).IsEquivalentTo([new TopicPartition("topic", 0)]);
    }

    [Test]
    public async Task AbandonAcquisition_StopsRenewalReplayWithoutAcknowledging()
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2);
        await using var fixture = CreateFixture(connection);
        var assignment = new HashSet<TopicPartition> { new("topic", 0) };
        PrepareForPoll(fixture.Consumer);
        var hosted = (IHostedShareConsumer)fixture.Consumer;
        var record = CreateRecord();
        fixture.Consumer.Acknowledge(record, AcknowledgeType.Renew);
        ApplySuccessfulAcknowledgements(fixture.Consumer, RenewalAcknowledgements());
        FlushPendingAcknowledgements(fixture.Consumer);
        await Assert.That(GetActiveRenewedRecords(fixture.Consumer, assignment)).HasSingleItem();

        hosted.AbandonAcquisition(record.Topic, record.Partition, record.Offset);

        await Assert.That(GetActiveRenewedRecords(fixture.Consumer, assignment)).IsEmpty();
        await Assert.That(HasPendingAcknowledgements(fixture.Consumer)).IsFalse();
    }
}
