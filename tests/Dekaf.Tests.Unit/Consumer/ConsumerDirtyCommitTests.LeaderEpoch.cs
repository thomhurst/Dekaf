using Dekaf.Consumer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Consumer;

public sealed partial class ConsumerDirtyCommitTests
{
    [Test]
    [Arguments("CommitAsync", 1, 2)]
    [Arguments("CommitAsync", -1, 2)]
    [Arguments("CommitAsync", 1, -1)]
    [Arguments("CommitAsync", 1, 1)]
    [Arguments("AutoCommit", 1, 2)]
    [Arguments("AutoCommit", -1, 2)]
    [Arguments("AutoCommit", 1, -1)]
    [Arguments("AutoCommit", 1, 1)]
    [Arguments("Explicit", 1, 2)]
    [Arguments("Explicit", -1, 2)]
    [Arguments("Explicit", 1, -1)]
    [Arguments("Explicit", 1, 1)]
    public async Task CommitCompletion_SameOffsetStoredWhileInFlight_ClearsOnlyMatchingLeaderEpoch(
        string commitKind, int capturedEpoch, int storedEpoch)
    {
        var requests = new List<OffsetCommitRequest>();
        var commitStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completeCommit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commitAttempt = 0;
        await using var consumer = CreateConsumer(
            requests,
            ErrorCode.None,
            commitKind == "AutoCommit" ? OffsetCommitMode.Auto : OffsetCommitMode.Manual,
            enableAutoOffsetStore: false,
            onOffsetCommitAsync: async token =>
            {
                if (Interlocked.Increment(ref commitAttempt) != 1)
                    return;

                commitStarted.SetResult();
                await completeCommit.Task.WaitAsync(token);
            });
        consumer.Subscribe("topic-a");
        PublishInitializedAssignment(consumer, OwnedP0);
        var captured = new TopicPartitionOffset("topic-a", 0, 5, capturedEpoch);
        var stored = new TopicPartitionOffset("topic-a", 0, 5, storedEpoch);
        consumer.StoreOffset(captured);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var commit = commitKind == "Explicit"
            ? consumer.CommitAsync([captured], timeout.Token).AsTask()
            : CommitByAsync(consumer, commitKind);
        try
        {
            await commitStarted.Task.WaitAsync(timeout.Token);
            // The request has captured the old checkpoint, but its response is still pending.
            consumer.StoreOffset(stored);
        }
        finally
        {
            completeCommit.TrySetResult();
            await commit.WaitAsync(timeout.Token);
        }

        var changed = capturedEpoch != storedEpoch;
        await Assert.That(GetDirtyStoredOffsets(consumer).ContainsKey(OwnedP0)).IsEqualTo(changed);
        await Assert.That(GetCommittedOffsets(requests[0])).IsEquivalentTo([captured]);

        await consumer.CommitAsync(timeout.Token);
        await consumer.CommitAsync(timeout.Token);

        await Assert.That(requests.Count).IsEqualTo(changed ? 2 : 1);
        if (changed)
            await Assert.That(GetCommittedOffsets(requests[1])).IsEquivalentTo([stored]);
        await Assert.That(GetDirtyStoredOffsets(consumer)).IsEmpty();
    }
}
