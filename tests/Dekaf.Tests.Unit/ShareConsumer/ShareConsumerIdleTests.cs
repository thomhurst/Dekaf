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

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NoAssignment_CancellationDoesNotCancelLaterPoll(bool batch)
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
        using (var cancelled = new CancellationTokenSource())
        {
            await using var records = fixture.Consumer.PollAsync(cancelled.Token).GetAsyncEnumerator();
            await using var batches = fixture.Consumer.PollBatchesAsync(cancelled.Token).GetAsyncEnumerator();
            var pending = (batch ? batches.MoveNextAsync() : records.MoveNextAsync()).AsTask();
            await Assert.That(pending.IsCompleted).IsFalse();
            await cancelled.CancelAsync();
            await Assert.That(async () => await pending).Throws<OperationCanceledException>();
        }

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var nextRecords = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
        await using var nextBatches = fixture.Consumer.PollBatchesAsync(cancellation.Token).GetAsyncEnumerator();
        var next = (batch ? nextBatches.MoveNextAsync() : nextRecords.MoveNextAsync()).AsTask();
        try
        {
            await Assert.That(next.IsCompleted).IsFalse();
            PublishAssignment(coordinator, assigned: true);
            await Assert.That(await next.WaitAsync(TimeSpan.FromSeconds(3))).IsTrue();
        }
        finally
        {
            await cancellation.CancelAsync();
            try { await next; }
            catch (OperationCanceledException) { }
        }
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task NoAssignment_CloseOrDisposeWakesPendingPoll(bool batch, bool dispose)
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
            if (dispose)
                await fixture.Consumer.DisposeAsync();
            else
                await fixture.Consumer.CloseAsync();
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

    [Test]
    public async Task NoAssignment_UnchangedHeartbeatDoesNotWakeWaiter()
    {
        await using var fixture = CreateFixture(new CapturingConnection(ApiKey.ShareFetch, maximumVersion: 2));
        PrepareForPoll(fixture.Consumer);
        var coordinator = IdleCoordinator(fixture.Consumer);
        PublishAssignment(coordinator, assigned: false);
        var assignment = coordinator.Assignment;
        var pending = coordinator.GetAssignmentChangeTask();
        PublishAssignment(coordinator, assigned: false);
        await Assert.That(pending.IsCompleted).IsFalse();
        await Assert.That(coordinator.Assignment).IsSameReferenceAs(assignment);
        await Assert.That(coordinator.GetAssignmentChangeTask()).IsSameReferenceAs(pending);
        coordinator.RequestRejoin();
        await pending.WaitAsync(TimeSpan.FromSeconds(3));
        await Assert.That(coordinator.State).IsEqualTo(Dekaf.Consumer.CoordinatorState.Unjoined);
    }

    [Test]
    public async Task NoAssignment_UpdateBeforeWaitRegistrationDoesNotLoseWakeup()
    {
        await using var fixture = CreateFixture(new CapturingConnection(ApiKey.ShareFetch, maximumVersion: 2));
        PrepareForPoll(fixture.Consumer);
        var coordinator = IdleCoordinator(fixture.Consumer);
        PublishAssignment(coordinator, assigned: false);
        fixture.Consumer.Subscribe("topic");
        var observed = coordinator.Assignment;
        PublishAssignment(coordinator, assigned: true);
        var wait = (ValueTask)InvokePrivate(fixture.Consumer,
            "WaitForAssignmentChangeAsync", observed, CancellationToken.None)!;
        await Assert.That(wait.IsCompletedSuccessfully).IsTrue();
        await wait;
    }
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task MissingLeader_RefreshesMetadataBeforePollingAgain(bool batch)
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, maximumVersion: 2)
        {
            ShareFetchResponse = CreateFetchResponse(partition: 0, offset: 42)
        };
        await using var fixture = CreateFixture(connection, retryBackoffMs: 60_000);
        fixture.MetadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }],
            Topics = [new TopicMetadata
            {
                ErrorCode = ErrorCode.None, Name = "topic", TopicId = TopicId,
                Partitions = [new PartitionMetadata
                {
                    ErrorCode = ErrorCode.None, PartitionIndex = 0, LeaderId = -1, ReplicaNodes = [1], IsrNodes = [1]
                }]
            }]
        });
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await using var records = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
        await using var batches = fixture.Consumer.PollBatchesAsync(cancellation.Token).GetAsyncEnumerator();
        // The old no-leader path never suspends. A separate worker lets the timeout
        // bound that regression without blocking the test runner's calling thread.
        var pending = Task.Run(async () => batch
            ? await batches.MoveNextAsync()
            : await records.MoveNextAsync());
        try
        {
            await Assert.That(await pending).IsTrue();
            await Assert.That(connection.SendCount).IsGreaterThanOrEqualTo(2);
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
    public async Task MissingLeader_CancellationInterruptsUnavailableMetadataBackoff(bool batch)
    {
        var missing = new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }],
            Topics = [new TopicMetadata
            {
                ErrorCode = ErrorCode.None, Name = "topic", TopicId = TopicId,
                Partitions = [new PartitionMetadata
                {
                    ErrorCode = ErrorCode.None, PartitionIndex = 0, LeaderId = -1,
                    ReplicaNodes = [1], IsrNodes = [1]
                }]
            }]
        };
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new CapturingConnection(ApiKey.ShareFetch, maximumVersion: 2)
        {
            MetadataResponse = missing,
            OnSend = () => requested.TrySetResult()
        };
        await using var fixture = CreateFixture(connection, retryBackoffMs: 60_000);
        fixture.MetadataManager.Metadata.Update(missing);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var records = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
        await using var batches = fixture.Consumer.PollBatchesAsync(cancellation.Token).GetAsyncEnumerator();
        var pending = Task.Run(async () => batch
            ? await batches.MoveNextAsync()
            : await records.MoveNextAsync());
        try
        {
            await requested.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await Assert.That(pending.IsCompleted).IsFalse();
            await cancellation.CancelAsync();
            await Assert.That(async () => await pending.WaitAsync(TimeSpan.FromSeconds(3)))
                .Throws<OperationCanceledException>();
            await Assert.That(connection.SendCount).IsEqualTo(1);
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