using System.Reflection;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed partial class ShareConsumerRenewalTests
{
    [Test]
    [Arguments(false, false, "unsubscribe")]
    [Arguments(true, false, "unsubscribe")]
    [Arguments(false, true, "unsubscribe")]
    [Arguments(true, true, "unsubscribe")]
    [Arguments(false, false, "close")]
    [Arguments(true, false, "close")]
    [Arguments(false, true, "close")]
    [Arguments(true, true, "close")]
    [Arguments(false, false, "dispose")]
    [Arguments(true, false, "dispose")]
    [Arguments(false, true, "dispose")]
    [Arguments(true, true, "dispose")]
    [Arguments(false, false, "reassign")]
    [Arguments(true, false, "reassign")]
    [Arguments(false, true, "reassign")]
    [Arguments(true, true, "reassign")]
    public async Task MissingLeader_StateChangeInterruptsRecovery(bool batch, bool delayedMetadata, string action)
    {
        var missing = new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }],
            Topics = [new TopicMetadata
            {
                ErrorCode = ErrorCode.None, Name = "topic", TopicId = TopicId,
                Partitions =
                [
                    new PartitionMetadata
                    {
                        ErrorCode = ErrorCode.None, PartitionIndex = 0, LeaderId = -1,
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
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<MetadataResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new CapturingConnection(ApiKey.ShareFetch, maximumVersion: 2)
        {
            MetadataResponse = missing,
            ShareFetchResponse = CreateFetchResponse(partition: 1, offset: 43),
            OnSend = () => requested.TrySetResult(),
            MetadataHandler = delayedMetadata ? async token =>
            {
                try { return await release.Task.WaitAsync(token); }
                catch (OperationCanceledException)
                {
                    stopped.TrySetResult();
                    throw;
                }
            } : null
        };
        await using var fixture = CreateFixture(connection, retryBackoffMs: 60_000);
        fixture.MetadataManager.Metadata.Update(missing);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var records = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
        await using var batches = fixture.Consumer.PollBatchesAsync(cancellation.Token).GetAsyncEnumerator();
        var pending = (batch ? batches.MoveNextAsync() : records.MoveNextAsync()).AsTask();
        try
        {
            await requested.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await Assert.That(pending.IsCompleted).IsFalse();
            switch (action)
            {
                case "unsubscribe": fixture.Consumer.Unsubscribe(); break;
                case "close": await fixture.Consumer.CloseAsync(); break;
                case "dispose": await fixture.Consumer.DisposeAsync(); break;
                case "reassign":
                    typeof(ShareConsumerCoordinator)
                        .GetMethod("ProcessShareGroupAssignment", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(IdleCoordinator(fixture.Consumer), [new ShareGroupHeartbeatAssignment
                        {
                            TopicPartitions = [new ShareGroupHeartbeatTopicPartitions { TopicId = TopicId, Partitions = [1] }]
                        }]);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(action));
            }
            await Assert.That(await pending.WaitAsync(TimeSpan.FromSeconds(3))).IsEqualTo(action == "reassign");
            if (delayedMetadata)
                await Assert.That(stopped.Task.IsCompletedSuccessfully).IsTrue();
            if (action == "reassign")
            {
                if (batch) await Assert.That(batches.Current.Count).IsEqualTo(1);
                else await Assert.That(records.Current.Offset).IsEqualTo(43L);
            }
        }
        finally
        {
            await cancellation.CancelAsync();
            release.TrySetResult(missing);
            try { await pending; }
            catch (OperationCanceledException) { }
        }
    }
}
