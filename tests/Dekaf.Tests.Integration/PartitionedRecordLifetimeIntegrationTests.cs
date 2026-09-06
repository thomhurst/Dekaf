using System.Text;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

[Category("ConsumerGroup")]
public sealed class PartitionedRecordLifetimeIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [Arguments(PartitionedProcessingOrder.Partition, false)]
    [Arguments(PartitionedProcessingOrder.Partition, true)]
    [Arguments(PartitionedProcessingOrder.Key, false)]
    [Arguments(PartitionedProcessingOrder.Key, true)]
    public Task SuspendedHandler_RemainsValidAcrossFetchTurnover(PartitionedProcessingOrder ordering, bool raw)
        => raw
            ? VerifyTurnoverAsync(ordering, Serializers.RawBytes, static value => Encoding.UTF8.GetString(value.Span))
            : VerifyTurnoverAsync(ordering, Serializers.String, static value => value);

    private async Task VerifyTurnoverAsync<T>(PartitionedProcessingOrder ordering,
        IDeserializer<T> deserializer, Func<T, string> decode)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();
        await using var consumer = await Kafka.CreateConsumer<T, T>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithKeyDeserializer(deserializer)
            .WithValueDeserializer(deserializer)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithQueuedMinMessages(1)
            .WithFetchMaxBytes(64 * 1024)
            .WithMaxPartitionFetchBytes(64 * 1024)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Partitions.Assign(new TopicPartition(topic, 0), new TopicPartition(topic, 1));
        var headers = new Headers { new Header("lifetime", "original-header"u8.ToArray()) };
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic, Partition = 0, Key = "original-key", Value = "original-value", Headers = headers
        });

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        var started = NewSignal();
        var turnover = NewSignal();
        var release = NewSignal();
        var verified = NewSignal();
        var turnedOverRecords = 0;
        var run = consumer.RunPartitionedAsync(async (_, message, token) =>
        {
            if (message.Partition == 0)
            {
                started.TrySetResult();
                await release.Task.WaitAsync(token);
                await Assert.That(decode(message.Key!)).IsEqualTo("original-key");
                await Assert.That(decode(message.Value)).IsEqualTo("original-value");
                await Assert.That(Encoding.UTF8.GetString(message.Headers[0].Value.Span)).IsEqualTo("original-header");
                verified.TrySetResult();
            }
            else if (Interlocked.Increment(ref turnedOverRecords) == 32)
            {
                turnover.TrySetResult();
            }
        }, new PartitionedProcessingOptions
        {
            Ordering = ordering,
            BackpressureMode = PartitionBackpressureMode.AwaitCapacity,
            CommitPolicy = PartitionCommitPolicy.UserManaged,
            StopPolicy = PartitionStopPolicy.Cancel,
            MaxBufferedRecordsPerPartition = 4
        }, stop.Token).AsTask();

        try
        {
            await started.Task.WaitAsync(timeout.Token);
            // Produce only after the original fetch reaches its suspended handler. One MiB
            // through 64 KiB fetches forces fresh broker responses while it remains active.
            var value = new string('X', 32 * 1024);
            for (var index = 0; index < 32; index++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic, Partition = 1, Key = "turnover", Value = value
                }, timeout.Token);
            }
            await turnover.Task.WaitAsync(timeout.Token);
            release.TrySetResult();
            await Task.WhenAny(verified.Task, run).WaitAsync(timeout.Token);
            if (run.IsCompleted)
                await run;
            await verified.Task.WaitAsync(timeout.Token);
        }
        finally
        {
            release.TrySetResult();
            await stop.CancelAsync();
            try
            {
                await run;
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                await Assert.That(run.IsCanceled).IsTrue();
            }
        }
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
