using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

[Category("ConsumerGroup")]
public sealed class PartitionedSeekIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    private const int RecordCount = 10;

    [Test]
    public async Task SeekAsync_ReplaysPartitionWhileProcessingRuns()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var partition = new TopicPartition(topic, 0);

        await using (var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync())
        {
            for (var i = 0; i < RecordCount; i++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Partition = 0,
                    Key = $"key-{i}",
                    Value = $"value-{i}"
                }, CancellationToken.None);
            }
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"partitioned-seek-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);

        var control = new PartitionedProcessingControl();
        var received = new ConcurrentQueue<long>();
        var lastRecordSeen = 0;
        var passes = new[] { NewCompletionSource(), NewCompletionSource(), NewCompletionSource() };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var runTask = consumer.RunPartitionedAsync(
            (_, message, _) =>
            {
                received.Enqueue(message.Offset);
                if (message.Offset == RecordCount - 1)
                {
                    var pass = Interlocked.Increment(ref lastRecordSeen) - 1;
                    if (pass < passes.Length)
                        passes[pass].TrySetResult();
                }

                return ValueTask.CompletedTask;
            },
            new PartitionedProcessingOptions
            {
                Control = control,
                MaxBufferedRecordsPerPartition = 4,
                CommitPolicy = PartitionCommitPolicy.CommitCompletedOnRevoke
            },
            cts.Token).AsTask();

        await passes[0].Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await control.SeekAsync(new TopicPartitionOffset(topic, 0, 4), cts.Token).ConfigureAwait(false);
        // The seek commits its target, so a restart would replay from the same position.
        var committedAfterSeek = await consumer.GetCommittedOffsetAsync(partition, cts.Token).ConfigureAwait(false);
        await passes[1].Task.WaitAsync(cts.Token).ConfigureAwait(false);

        await control.SeekAsync(new TopicPartitionTimestamp(partition, TopicPartitionTimestamp.Earliest), cts.Token)
            .ConfigureAwait(false);
        await passes[2].Task.WaitAsync(cts.Token).ConfigureAwait(false);

        var unassigned = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await control.SeekAsync(new TopicPartitionOffset(topic, 5, 0), cts.Token).ConfigureAwait(false));

        await cts.CancelAsync().ConfigureAwait(false);
        try
        {
            await runTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }

        var expected = Enumerable.Range(0, RecordCount)
            .Concat(Enumerable.Range(4, RecordCount - 4))
            .Concat(Enumerable.Range(0, RecordCount));
        await Assert.That(string.Join(",", received)).IsEqualTo(string.Join(",", expected));
        await Assert.That(committedAfterSeek).IsEqualTo(4);
        await Assert.That(unassigned!.Message).Contains("not assigned");
        await Assert.That(await consumer.GetCommittedOffsetAsync(partition, CancellationToken.None))
            .IsEqualTo(RecordCount);
    }

    private static TaskCompletionSource NewCompletionSource()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
