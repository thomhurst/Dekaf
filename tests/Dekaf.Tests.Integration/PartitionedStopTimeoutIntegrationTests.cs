using System.Text;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

[Category("ConsumerGroup")]
public sealed class PartitionedStopTimeoutIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [Arguments(PartitionStopPolicy.Cancel)]
    [Arguments(PartitionStopPolicy.Drain)]
    public async Task Shutdown_BoundsBlockedCallbackAndCancelsEveryPartition(PartitionStopPolicy policy)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();
        await using var consumer = await Kafka.CreateConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithKeyDeserializer(Serializers.RawBytes)
            .WithValueDeserializer(Serializers.RawBytes)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithQueuedMinMessages(1)
            .BuildAsync();
        consumer.Partitions.Assign(new TopicPartition(topic, 0), new TopicPartition(topic, 1));
        for (var partition = 0; partition < 2; partition++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic, Partition = partition, Key = "key", Value = $"value-{partition}"
            });
        }

        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var stop = new CancellationTokenSource();
        using var releaseCallback = new ManualResetEventSlim();
        var started = new[] { NewSignal(), NewSignal() };
        var cancelled = new[] { NewSignal(), NewSignal() };
        var exited = new[] { NewSignal(), NewSignal() };
        var verified = new[] { NewSignal(), NewSignal() };
        var releaseHandler = NewSignal();
        var running = consumer.RunPartitionedAsync(async (_, message, token) =>
        {
            var partition = message.Partition;
            try
            {
                using var registration = token.Register(() =>
                {
                    cancelled[partition].TrySetResult();
                    if (partition == 0)
                        releaseCallback.Wait();
                });
                started[partition].TrySetResult();
                await releaseHandler.Task;
                await Assert.That(Encoding.UTF8.GetString(message.Value.Span)).IsEqualTo($"value-{partition}");
                verified[partition].TrySetResult();
            }
            catch (Exception exception)
            {
                verified[partition].TrySetException(exception);
                throw;
            }
            finally
            {
                exited[partition].TrySetResult();
            }
        }, new PartitionedProcessingOptions
        {
            StopPolicy = policy,
            StopTimeout = TimeSpan.FromMilliseconds(100),
            CommitPolicy = PartitionCommitPolicy.UserManaged
        }, stop.Token).AsTask();

        try
        {
            await Task.WhenAll(started[0].Task, started[1].Task).WaitAsync(deadline.Token);
            await stop.CancelAsync();
            await Task.WhenAny(running).WaitAsync(TimeSpan.FromSeconds(10));
            await Task.WhenAll(cancelled[0].Task, cancelled[1].Task).WaitAsync(deadline.Token);
            await Assert.That(exited[0].Task.IsCompleted).IsFalse();
            await Assert.That(exited[1].Task.IsCompleted).IsFalse();
        }
        finally
        {
            releaseCallback.Set();
            releaseHandler.TrySetResult();
            await stop.CancelAsync();
            try
            {
                await running.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (TimeoutException) when (running.IsFaulted) { }
            catch (OperationCanceledException) when (running.IsCanceled) { }
            await Task.WhenAll(exited[0].Task, exited[1].Task).WaitAsync(TimeSpan.FromSeconds(10));
        }
        // Handler failures after the runtime returns must still fail this test.
        await Task.WhenAll(verified[0].Task, verified[1].Task).WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
