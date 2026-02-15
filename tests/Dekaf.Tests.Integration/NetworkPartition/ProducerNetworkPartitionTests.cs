using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.NetworkPartition;

/// <summary>
/// Tests verifying producer behavior during network partitions.
/// Uses Docker pause/unpause to simulate network failures.
/// </summary>
[Category("NetworkPartition")]
[ClassDataSource<NetworkPartitionKafkaContainer>(Shared = SharedType.PerClass)]
public class ProducerNetworkPartitionTests(NetworkPartitionKafkaContainer kafka)
{
    [Test]
    public async Task Producer_RetriesAndSucceeds_AfterNetworkPartitionHeals()
    {
        // Arrange: short request timeout so failure is detected quickly
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-partition-retry")
            .WithAcks(Acks.All)
            .WithDeliveryTimeout(TimeSpan.FromSeconds(30))
            .WithRequestTimeout(TimeSpan.FromSeconds(5))
            .BuildAsync();

        // Warmup: establish connection and cache metadata
        var warmup = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "warmup",
            Value = "warmup"
        });
        await Assert.That(warmup.Offset).IsGreaterThanOrEqualTo(0);

        // Act: pause container to simulate network partition
        await kafka.PauseAsync();

        try
        {
            // Start producing during partition - will block/retry until connection is restored
            var produceTask = producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "partition-key",
                Value = "partition-value"
            }).AsTask();

            // Wait long enough for request timeout to fire, then unpause
            await Task.Delay(TimeSpan.FromSeconds(6));
            await kafka.UnpauseAsync();

            // Assert: produce should eventually succeed after recovery
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await produceTask.WaitAsync(cts.Token);
            await Assert.That(result.Topic).IsEqualTo(topic);
            await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
        }
        finally
        {
            // Safety: ensure container is always unpaused
            try { await kafka.UnpauseAsync(); } catch { /* may already be unpaused */ }
        }
    }

    [Test]
    public async Task Producer_DeliveryTimeout_FiresWhenPartitioned()
    {
        // Arrange: very short delivery timeout that will expire during partition
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-partition-timeout")
            .WithAcks(Acks.All)
            .WithDeliveryTimeout(TimeSpan.FromSeconds(10))
            .WithRequestTimeout(TimeSpan.FromSeconds(5))
            .BuildAsync();

        // Warmup: establish connection
        var warmup = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "warmup",
            Value = "warmup"
        });
        await Assert.That(warmup.Offset).IsGreaterThanOrEqualTo(0);

        // Act: pause container and do NOT unpause - delivery timeout should fire
        await kafka.PauseAsync();

        try
        {
            // This should throw once delivery timeout expires
            var exception = await Assert.ThrowsAsync<Exception>(async () =>
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = "timeout-key",
                    Value = "timeout-value"
                });
            });

            // Assert: should be a timeout or produce exception
            await Assert.That(exception).IsNotNull();
            var isExpectedException = exception is TimeoutException
                or Errors.ProduceException
                or Errors.KafkaException;

            await Assert.That(isExpectedException).IsTrue()
                .Because($"Expected TimeoutException or ProduceException but got {exception!.GetType().Name}: {exception.Message}");
        }
        finally
        {
            // Always unpause to allow cleanup
            try { await kafka.UnpauseAsync(); } catch { /* may already be unpaused */ }
        }
    }
}
