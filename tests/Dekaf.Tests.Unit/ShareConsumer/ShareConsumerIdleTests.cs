using System.Reflection;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed partial class ShareConsumerRenewalTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NoAssignment_NewAssignmentWakesPollWithoutFetchDelay(bool batch)
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, maximumVersion: 2)
        {
            ShareFetchResponse = CreateFetchResponse(partition: 0, offset: 42)
        };
        await using var fixture = CreateFixture(connection, fetchMaxWaitMs: 60_000);
        PrepareForPoll(fixture.Consumer);
        var coordinator = IdleCoordinator(fixture.Consumer);
        PublishAssignment(coordinator, assigned: false);
        fixture.Consumer.Subscribe("topic");
        using var cancellation = new CancellationTokenSource();
        await using var records = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
        await using var batches = fixture.Consumer.PollBatchesAsync(cancellation.Token).GetAsyncEnumerator();
        var pending = (batch ? batches.MoveNextAsync() : records.MoveNextAsync()).AsTask();
        try
        {
            await Assert.That(pending.IsCompleted).IsFalse();
            await Assert.That(connection.SendCount).IsEqualTo(0);
            PublishAssignment(coordinator, assigned: true);
            await Assert.That(await pending.WaitAsync(TimeSpan.FromSeconds(3))).IsTrue();
            if (batch)
                await Assert.That(batches.Current.Count).IsEqualTo(1);
            else
                await Assert.That(records.Current.Offset).IsEqualTo(42L);
        }
        finally
        {
            await cancellation.CancelAsync();
            try { await pending; }
            catch (OperationCanceledException) { }
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NoAssignment_UnsubscribeWakesPendingPoll(bool batch)
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, maximumVersion: 2);
        await using var fixture = CreateFixture(connection, fetchMaxWaitMs: 60_000);
        PrepareForPoll(fixture.Consumer);
        PublishAssignment(IdleCoordinator(fixture.Consumer), assigned: false);
        fixture.Consumer.Subscribe("topic");
        using var cancellation = new CancellationTokenSource();
        await using var records = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
        await using var batches = fixture.Consumer.PollBatchesAsync(cancellation.Token).GetAsyncEnumerator();
        var pending = (batch ? batches.MoveNextAsync() : records.MoveNextAsync()).AsTask();
        try
        {
            await Assert.That(pending.IsCompleted).IsFalse();
            fixture.Consumer.Unsubscribe();
            await Assert.That(await pending.WaitAsync(TimeSpan.FromSeconds(3))).IsFalse();
            await Assert.That(connection.SendCount).IsEqualTo(0);
        }
        finally
        {
            await cancellation.CancelAsync();
            try { await pending; }
            catch (OperationCanceledException) { }
        }
    }

    private static ShareConsumerCoordinator IdleCoordinator(KafkaShareConsumer<string, string> consumer)
        => (ShareConsumerCoordinator)typeof(KafkaShareConsumer<string, string>)
            .GetField("_coordinator", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(consumer)!;

    private static void PublishAssignment(ShareConsumerCoordinator coordinator, bool assigned)
        => typeof(ShareConsumerCoordinator)
            .GetMethod("ProcessShareGroupAssignment", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(coordinator, [new ShareGroupHeartbeatAssignment
            {
                TopicPartitions = assigned
                    ? [new ShareGroupHeartbeatTopicPartitions { TopicId = TopicId, Partitions = [0] }]
                    : []
            }]);
}