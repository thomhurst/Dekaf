using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

[Category("ConsumerGroup")]
public sealed class PartitionedBackpressureIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AwaitCapacity_HandlerCommitsReachBrokerWhileFullOrDraining(bool shutdown)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var partition = new TopicPartition(topic, 0);
        var groupId = $"backpressure-{Guid.NewGuid():N}";
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync();
        for (var offset = 0; offset < 3; offset++)
            await producer.ProduceAsync(topic, "key", offset.ToString(System.Globalization.CultureInfo.InvariantCulture));

        var allowCommit = NewSignal();
        var firstCommitted = NewSignal();
        var releaseFirst = NewSignal();
        var allCommitted = NewSignal();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var stop = new CancellationTokenSource();
        using var commitQueued = new ManualResetEventSlim();
        var thirdRecordRead = NewSignal();
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithQueuedMinMessages(1)
            .WithValueDeserializer(new ThirdRecordDeserializer(thirdRecordRead, shutdown ? () =>
            {
                // Force shutdown after the rejected record is decoded and before routing
                // can service the handler commit with the canceled input token.
                stop.Cancel();
                allowCommit.TrySetResult();
                commitQueued.Wait(timeout.Token);
            } : null))
            .BuildAsync();
        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        consumer.Subscribe(topic);

        var running = consumer.RunPartitionedAsync(async (context, token) =>
        {
            await foreach (var record in context.Messages.WithCancellation(token))
            {
                if (record.Offset == 0)
                    await allowCommit.Task.WaitAsync(token);
                context.MarkProcessed(record);
                var committing = context.CommitProcessedAsync(token);
                commitQueued.Set();
                await committing;
                if (record.Offset == 0)
                {
                    firstCommitted.TrySetResult();
                    if (!shutdown)
                        await releaseFirst.Task.WaitAsync(token);
                }
                if (record.Offset == 2)
                    allCommitted.TrySetResult();
            }
        }, new PartitionedProcessingOptions
        {
            BackpressureMode = PartitionBackpressureMode.AwaitCapacity,
            MaxBufferedRecordsPerPartition = 1,
            CommitPolicy = PartitionCommitPolicy.UserManaged,
            StopPolicy = PartitionStopPolicy.Drain,
            StopTimeout = TimeSpan.FromSeconds(10)
        }, stop.Token).AsTask();

        try
        {
            await thirdRecordRead.Task.WaitAsync(timeout.Token);
            allowCommit.TrySetResult();

            if (shutdown)
            {
                await Assert.That(async () => await running.WaitAsync(timeout.Token))
                    .Throws<OperationCanceledException>();
                var offsets = await admin.ListConsumerGroupOffsetsAsync(groupId, timeout.Token);
                // Offset 2 was fetched but never appended to the full lane.
                await Assert.That(offsets[partition]).IsEqualTo(2);
            }
            else
            {
                await firstCommitted.Task.WaitAsync(timeout.Token);
                var offsets = await admin.ListConsumerGroupOffsetsAsync(groupId, timeout.Token);
                await Assert.That(offsets[partition]).IsEqualTo(1);
                await Assert.That(allCommitted.Task.IsCompleted).IsFalse();
                releaseFirst.TrySetResult();
                await allCommitted.Task.WaitAsync(timeout.Token);
                offsets = await admin.ListConsumerGroupOffsetsAsync(groupId, timeout.Token);
                await Assert.That(offsets[partition]).IsEqualTo(3);
            }
        }
        finally
        {
            allowCommit.TrySetResult();
            releaseFirst.TrySetResult();
            await stop.CancelAsync();
            try { await running; }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                await Assert.That(running.IsCanceled).IsTrue();
            }
        }
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class ThirdRecordDeserializer(TaskCompletionSource thirdRecordRead, Action? beforeReturn) : IDeserializer<string>
    {
        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            if (data.Span.SequenceEqual("2"u8))
            {
                thirdRecordRead.TrySetResult();
                beforeReturn?.Invoke();
            }
            return Serializers.String.Deserialize(data, context);
        }
    }
}
