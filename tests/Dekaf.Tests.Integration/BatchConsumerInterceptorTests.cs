using System.Collections.Concurrent;
using System.Text;
using Dekaf.Consumer;

namespace Dekaf.Tests.Integration;

[Category("ConsumerGroup")]
public sealed class BatchConsumerInterceptorTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [Arguments("batch", 1)]
    [Arguments("batch", 1000)]
    [Arguments("partitioned", 1)]
    [Arguments("partitioned", 1000)]
    [Arguments("partitioned-batches", 1)]
    [Arguments("partitioned-batches", 1000)]
    [Arguments("raw", 1)]
    [Arguments("raw", 1000)]
    public async Task Delivery_AppliesOrderedInterceptorsExceptRaw(string delivery, int queuedMinMessages)
    {
        const int count = 4;
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync();
        for (var index = 0; index < count; index++)
            await producer.ProduceAsync(topic, "key", $"value-{index}");

        var calls = new ConcurrentQueue<string>();
        var groupId = $"batch-interceptors-{Guid.NewGuid():N}";
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithQueuedMinMessages(queuedMinMessages)
            .AddInterceptor(new CallbackInterceptor(result =>
            {
                calls.Enqueue($"first:{result.Value}");
                return new ConsumeResult<string, string>("replacement-topic", 99, 99,
                    result.Key, $"changed-{result.Offset}", result.Headers, result.Timestamp.ToUnixTimeMilliseconds(),
                    result.TimestampType, 99);
            }))
            .AddInterceptor(new CallbackInterceptor(result =>
            {
                calls.Enqueue($"throw:{result.Value}");
                throw new InvalidOperationException("Expected interceptor failure");
            }))
            .AddInterceptor(new CallbackInterceptor(result =>
            {
                calls.Enqueue($"last:{result.Value}");
                return result;
            }))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();
        consumer.Subscribe(topic);

        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        var received = new ConcurrentQueue<string>();
        var delivered = new ConcurrentQueue<ConsumeResult<string, string>>();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        ValueTask Handle(ConsumeResult<string, string> record)
        {
            delivered.Enqueue(record);
            received.Enqueue(record.Value);
            if (received.Count == count)
                done.TrySetResult();
            return ValueTask.CompletedTask;
        }

        if (delivery == "raw")
        {
            await foreach (var batch in consumer.ConsumeRawBatchAsync(deadline.Token))
            {
                foreach (var record in batch)
                    received.Enqueue(Encoding.UTF8.GetString(record.Value.Span));
                if (received.Count == count)
                    break;
            }
        }
        else if (delivery == "batch")
        {
            await foreach (var batch in consumer.ConsumeBatchAsync(deadline.Token))
            {
                foreach (var record in batch)
                    await Handle(record);
                if (received.Count == count)
                    break;
            }
        }
        else
        {
            var options = new PartitionedProcessingOptions
            {
                Ordering = PartitionedProcessingOrder.Key,
                MaxConcurrentHandlersPerPartition = 2,
                MaxBufferedRecordsPerPartition = 8,
                MaxHandlerBatchSize = 4,
                CommitPolicy = PartitionCommitPolicy.CommitCompletedOnRevoke,
                StopPolicy = PartitionStopPolicy.Drain
            };
            var run = delivery == "partitioned"
                ? consumer.RunPartitionedAsync((_, record, _) => Handle(record), options, stop.Token).AsTask()
                : consumer.RunPartitionedBatchesAsync(async (_, records, _) =>
                {
                    for (var index = 0; index < records.Count; index++)
                        await Handle(records[index]);
                }, options, stop.Token).AsTask();
            try
            {
                await Task.WhenAny(done.Task, run).WaitAsync(deadline.Token);
                if (run.IsCompleted)
                    await run;
                await done.Task.WaitAsync(deadline.Token);
            }
            finally
            {
                await stop.CancelAsync();
                try { await run.WaitAsync(TimeSpan.FromSeconds(15)); }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            }
        }

        await Assert.That(received.ToArray()).IsEquivalentTo(
            Enumerable.Range(0, count).Select(index => delivery == "raw" ? $"value-{index}" : $"changed-{index}"));
        var expectedCalls = delivery == "raw" ? [] : Enumerable.Range(0, count)
            .SelectMany(index => new[] { $"first:value-{index}", $"throw:changed-{index}", $"last:changed-{index}" }).ToArray();
        await Assert.That(calls.SequenceEqual(expectedCalls)).IsTrue();
        foreach (var record in delivered)
        {
            await Assert.That(record.Topic).IsEqualTo(topic);
            await Assert.That(record.Partition).IsEqualTo(0);
            await Assert.That(record.Value).IsEqualTo($"changed-{record.Offset}");
            await Assert.That(record.LeaderEpoch).IsNotEqualTo(99);
        }
        if (delivery is "partitioned" or "partitioned-batches")
        {
            await using var admin = KafkaContainer.CreateAdminClient();
            var committed = await admin.ListConsumerGroupOffsetsAsync(groupId);
            await Assert.That(committed[new TopicPartition(topic, 0)]).IsEqualTo((long)count);
        }
    }

    private sealed class CallbackInterceptor(Func<ConsumeResult<string, string>, ConsumeResult<string, string>> callback)
        : IConsumerInterceptor<string, string>
    {
        public ConsumeResult<string, string> OnConsume(ConsumeResult<string, string> result) => callback(result);
        public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets) { }
    }
}
