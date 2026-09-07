using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

[Category("ConsumerGroup")]
public sealed class PartitionedDispatchIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task KeyChurn_PreservesConcurrencyOrderAndFinalCommit(bool batches)
    {
        const int count = 128;
        const int keys = 7;
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        await using var producer = await Kafka.CreateProducer<int, int>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();
        for (var index = 0; index < count; index++)
        {
            await producer.ProduceAsync(new ProducerMessage<int, int>
            {
                Topic = topic, Partition = 0, Key = index % keys, Value = index
            });
        }

        await using var consumer = await Kafka.CreateConsumer<int, int>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"partitioned-churn-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithQueuedMinMessages(1)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        consumer.Subscribe(topic);

        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        var firstStarted = NewSignal();
        var otherKeyProcessed = NewSignal();
        var releaseFirst = NewSignal();
        var allProcessed = NewSignal();
        var seen = new int[count];
        var lastByKey = new long[keys];
        Array.Fill(lastByKey, -1);
        var handled = 0;

        async ValueTask HandleRecord(ConsumeResult<int, int> record, CancellationToken token)
        {
            if (record.Offset == 0)
            {
                firstStarted.TrySetResult();
                await releaseFirst.Task.WaitAsync(token);
            }

            await Assert.That(record.Value).IsEqualTo(checked((int)record.Offset));
            await Assert.That(Interlocked.Exchange(ref seen[record.Value], 1)).IsEqualTo(0);
            var previous = Interlocked.Exchange(ref lastByKey[record.Key], record.Offset);
            await Assert.That(record.Offset).IsGreaterThan(previous);
            if (record.Key != 0)
                otherKeyProcessed.TrySetResult();
            if (Interlocked.Increment(ref handled) == count)
                allProcessed.TrySetResult();
        }

        var options = new PartitionedProcessingOptions
        {
            Ordering = PartitionedProcessingOrder.Key,
            MaxConcurrentHandlersPerPartition = 3,
            MaxBufferedRecordsPerPartition = 8,
            MaxHandlerBatchSize = 4,
            CommitPolicy = PartitionCommitPolicy.CommitCompletedOnRevoke,
            StopPolicy = PartitionStopPolicy.Drain
        };
        var run = batches
            ? consumer.RunPartitionedBatchesAsync(async (_, records, token) =>
            {
                var key = records[0].Key;
                for (var index = 0; index < records.Count; index++)
                {
                    await Assert.That(records[index].Key).IsEqualTo(key);
                    await HandleRecord(records[index], token);
                }
            }, options, stop.Token).AsTask()
            : consumer.RunPartitionedAsync((_, record, token) => HandleRecord(record, token), options, stop.Token).AsTask();

        try
        {
            await firstStarted.Task.WaitAsync(deadline.Token);
            await otherKeyProcessed.Task.WaitAsync(deadline.Token);
            await Assert.That(Volatile.Read(ref seen[0])).IsEqualTo(0);
            releaseFirst.TrySetResult();
            await Task.WhenAny(allProcessed.Task, run).WaitAsync(deadline.Token);
            if (run.IsCompleted)
                await run;
            await allProcessed.Task.WaitAsync(deadline.Token);
        }
        finally
        {
            releaseFirst.TrySetResult();
            await stop.CancelAsync();
            try
            {
                await run.WaitAsync(TimeSpan.FromSeconds(15));
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
            }
        }

        await Assert.That(handled).IsEqualTo(count);
        await Assert.That(seen).IsEquivalentTo(Enumerable.Repeat(1, count));
        await Assert.That(await consumer.GetCommittedOffsetAsync(new TopicPartition(topic, 0), deadline.Token))
            .IsEqualTo(count);
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
