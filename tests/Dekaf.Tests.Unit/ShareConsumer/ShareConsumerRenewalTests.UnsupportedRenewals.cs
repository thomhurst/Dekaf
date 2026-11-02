using System.Reflection;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

// A leader before ShareFetch/ShareAcknowledge v2 cannot renew. The request leaves out the
// partitions whose acknowledgements include Renew, and the poll, commit or close that awaits it
// drops the Renew and sends the partition's other dispositions later (#4032).
public sealed partial class ShareConsumerRenewalTests
{
    [Test]
    public async Task Poll_RenewalOnV1BrokerWithUnresolvedLeader_DropsRenewalOnce()
    {
        using var cancellation = new CancellationTokenSource();
        var connection = new CapturingConnection(ApiKey.ShareFetch, 1)
        {
            OnSend = cancellation.Cancel
        };
        await using var fixture = CreateFixture(connection, acknowledgementCommitCallback: static _ => { });
        // Assign the leaderless partition too: an acknowledgement outside the assignment is
        // drained by a commit before the fetch (#4045), not carried inline.
        PrepareForPoll(fixture.Consumer, new TopicPartition("topic", 0), new TopicPartition("missing-topic", 0));
        fixture.Consumer.Subscribe("topic");
        EstablishSessions(fixture.Consumer, 1);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 39), AcknowledgeType.Accept);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 40), AcknowledgeType.Renew);
        // No leader, so this partition's acknowledgement is unsent and requeued.
        fixture.Consumer.Acknowledge(CreateRecord(topic: "missing-topic", offset: 7), AcknowledgeType.Accept);

        await using var poll = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
        await poll.MoveNextAsync();

        await Assert.That(AcknowledgedPartitions(connection.ShareFetchRequest!)).IsEmpty();
        // The excluded partition is not requeued as unsent, so its Renew is dropped.
        var pending = FlushPendingAcknowledgements(fixture.Consumer);
        await Assert.That(pending.Keys).IsEquivalentTo(
            [new TopicPartition("topic", 0), new TopicPartition("missing-topic", 0)]);
        var batch = pending[new TopicPartition("topic", 0)].Single();
        await Assert.That(batch.FirstOffset).IsEqualTo(39);
        await Assert.That(batch.LastOffset).IsEqualTo(39);
        await Assert.That(batch.AcknowledgeTypes).IsEquivalentTo(new[] { (byte)AcknowledgeType.Accept });
    }

    [Test]
    public async Task Close_RenewalOnV1Broker_SendsOtherDispositionBeforeClosingSessions()
    {
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 1);
        await using var fixture = CreateFixture(connection);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 39), AcknowledgeType.Accept);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 40), AcknowledgeType.Renew);

        await fixture.Consumer.CloseAsync();

        var request = connection.ShareAcknowledgeRequests.Single();
        await Assert.That(request.IsRenewAck).IsFalse();
        var partition = request.Topics.Single().Partitions.Single();
        await Assert.That(partition.PartitionIndex).IsEqualTo(0);
        var batch = partition.AcknowledgementBatches.Single();
        await Assert.That(batch.FirstOffset).IsEqualTo(39);
        await Assert.That(batch.LastOffset).IsEqualTo(39);
        await Assert.That(batch.AcknowledgeTypes).IsEquivalentTo(new[] { (byte)AcknowledgeType.Accept });
        await Assert.That(HasPendingAcknowledgements(fixture.Consumer)).IsFalse();
    }

    [Test]
    public async Task Unsubscribe_RenewalOnV1Broker_ReleasesItInTheClosingRequest()
    {
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 1);
        await using var fixture = CreateFixture(connection);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 39), AcknowledgeType.Accept);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 40), AcknowledgeType.Renew);

        fixture.Consumer.Unsubscribe();
        // Disposal awaits the tracked best-effort release task.
        await fixture.Consumer.DisposeAsync();

        // Closing the session releases the record anyway, so the Renew goes out as Release and
        // the partition's other outcome is not left out of the request that closes the session.
        var request = connection.ShareAcknowledgeRequests.Single();
        await Assert.That(request.IsRenewAck).IsFalse();
        var partition = request.Topics.Single().Partitions.Single();
        await Assert.That(partition.PartitionIndex).IsEqualTo(0);
        var batch = partition.AcknowledgementBatches.Single();
        await Assert.That(batch.FirstOffset).IsEqualTo(39);
        await Assert.That(batch.LastOffset).IsEqualTo(40);
        await Assert.That(batch.AcknowledgeTypes).IsEquivalentTo(
            new[] { (byte)AcknowledgeType.Accept, (byte)AcknowledgeType.Release });
        await Assert.That(HasPendingAcknowledgements(fixture.Consumer)).IsFalse();
    }

    [Test]
    public async Task Unsubscribe_AbandonedFetchExcludingRenewal_DoesNotSettleIntoLaterPolls()
    {
        var leaseEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new CapturingConnection(ApiKey.ShareFetch, 1, includeShareAcknowledge: true);
        var leases = 0;
        await using var fixture = CreateFixture(connection, leaseHandler: async _ =>
        {
            // Only the poll's request waits; it ignores the poll's cancellation like a pending
            // connect, so it excludes the Renew after the abandon.
            if (Interlocked.Increment(ref leases) == 1)
            {
                leaseEntered.TrySetResult();
                await releaseLease.Task;
            }
            return connection;
        });
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        EstablishSessions(fixture.Consumer, 1);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 39), AcknowledgeType.Accept);
        fixture.Consumer.Acknowledge(CreateRecord(partition: 0, offset: 40), AcknowledgeType.Renew);

        using (var cancellation = new CancellationTokenSource())
        {
            await using var cancelled = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
            var pending = cancelled.MoveNextAsync().AsTask();
            await leaseEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await cancellation.CancelAsync();
            await Assert.That(async () => await pending.WaitAsync(TimeSpan.FromSeconds(10)))
                .Throws<OperationCanceledException>();
        }

        // The abandon requeues and releases every acknowledgement of the detached fetch.
        fixture.Consumer.Unsubscribe();
        releaseLease.TrySetResult();
        var queue = typeof(KafkaShareConsumer<string, string>)
            .GetField("_unsupportedRenewals", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (queue.GetValue(fixture.Consumer) is not System.Collections.ICollection { Count: > 0 })
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The abandoned request never excluded its Renew.");
            await Task.Delay(10);
        }

        // A later poll or commit drops what the abandoned request excluded instead of requeueing it.
        var taken = typeof(KafkaShareConsumer<string, string>)
            .GetMethod("TakeUnsupportedRenewals", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(fixture.Consumer, null);
        await Assert.That(taken).IsNull();
        await Assert.That(HasPendingAcknowledgements(fixture.Consumer)).IsFalse();
    }
}
