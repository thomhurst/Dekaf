using System.Collections.Concurrent;
using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

public sealed partial class PartitionedConsumerRuntimeTests
{
    [Test]
    [Arguments(PartitionedProcessingOrder.Partition)]
    [Arguments(PartitionedProcessingOrder.Key)]
    public async Task StopRequestedToken_RevokedRecordHandlerLeavesWithoutMarkingRecord(PartitionedProcessingOrder ordering)
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);
        consumer.Enqueue(CreateResult(partition, 0, key: "a"), CreateResult(partition, 1, key: "a"));

        var parked = NewCompletionSource();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            async (context, message, cancellationToken) =>
            {
                if (message.Offset != 1)
                    return;

                parked.TrySetResult();
                // Waiting on a dependency outside Kafka; leave only when the lane starts stopping.
                await Task.Delay(Timeout.Infinite, context.StopRequestedToken).ConfigureAwait(false);
            },
            new PartitionedProcessingOptions
            {
                Ordering = ordering,
                CommitPolicy = PartitionCommitPolicy.CommitCompletedOnRevoke,
                StopTimeout = TimeSpan.FromSeconds(30)
            },
            cts.Token).AsTask();

        await parked.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        consumer.SetAssignment();

        // The record still being handled is not committed, and the stop finishes well within StopTimeout.
        await Assert.That(() => consumer.CommitCalls.SelectMany(static offsets => offsets).ToArray())
            .Eventually(offsets => offsets.IsEquivalentTo(new[] { new TopicPartitionOffset("topic-a", 0, 1) }), TimeSpan.FromSeconds(5));
        await Assert.That(runTask.IsCompleted).IsFalse();

        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);
    }

    [Test]
    [Arguments(PartitionedProcessingOrder.Partition)]
    [Arguments(PartitionedProcessingOrder.Key)]
    public async Task StopRequestedToken_SeekReplaysRecordAParkedBatchHandlerLeft(PartitionedProcessingOrder ordering)
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer { RedeliverUnenumeratedBatches = true };
        consumer.SetAssignment(partition);
        consumer.Enqueue(CreateResult(partition, 0, key: "a"), CreateResult(partition, 1, key: "a"));

        var control = new PartitionedProcessingControl();
        var received = new ConcurrentQueue<long>();
        var parked = NewCompletionSource();
        var replayed = NewCompletionSource();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedBatchesAsync(
            async (context, messages, cancellationToken) =>
            {
                foreach (var message in messages)
                {
                    received.Enqueue(message.Offset);
                    if (message.Offset != 1)
                        continue;

                    if (parked.TrySetResult())
                        await Task.Delay(Timeout.Infinite, context.StopRequestedToken).ConfigureAwait(false);
                    else
                        replayed.TrySetResult();
                }
            },
            new PartitionedProcessingOptions
            {
                Ordering = ordering,
                Control = control,
                CommitPolicy = PartitionCommitPolicy.UserManaged,
                StopTimeout = TimeSpan.FromSeconds(30)
            },
            cts.Token).AsTask();

        await parked.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await control.SeekAsync(new TopicPartitionOffset(partition.Topic, partition.Partition, 1), cts.Token)
            .ConfigureAwait(false);
        await replayed.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await Assert.That(runTask.IsCompleted).IsFalse();
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        await Assert.That(received.Count(static offset => offset == 1)).IsEqualTo(2);
    }
}
