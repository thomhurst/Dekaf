using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests verifying timeout protection in connection, flush, receive, and disposal operations.
/// These tests exercise code paths that have timeout protection to ensure operations complete without hanging.
/// </summary>
[ClassDataSource<KafkaTestContainer>(Shared = SharedType.PerTestSession)]
public class TimeoutIntegrationTests(KafkaTestContainer kafka)
{
    [Test]
    public async Task Producer_ConnectionEstablishment_CompletesWithinDefaultTimeout()
    {
        // Arrange - Test that connection establishment completes within default ConnectionTimeout (30s)
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-connection-timeout")
            .Build();

        // Act - Produce a message to trigger connection establishment
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        });

        // Assert - Connection succeeded within timeout
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_RequestWithAcksAll_CompletesWithinDefaultTimeout()
    {
        // Arrange - Test that requests with Acks.All complete within default RequestTimeout (30s)
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-request-timeout")
            .WithAcks(Acks.All)
            .Build();

        // Act - Produce a message to trigger request
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        });

        // Assert - Request succeeded within timeout
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_FlushWithBatchedMessages_CompletesWithinDefaultTimeout()
    {
        // Arrange - Test that flush completes within default RequestTimeout (30s)
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-flush-timeout")
            .WithLingerMs(100) // Add some linger to batch messages
            .Build();

        // Act - Send multiple messages then flush
        for (int i = 0; i < 10; i++)
        {
            producer.Send(topic, $"key{i}", $"value{i}");
        }

        // Flush with explicit timeout to ensure it completes
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await producer.FlushAsync(cts.Token);

        // Assert - Flush completed without throwing
    }

    [Test]
    public async Task Producer_DisposalAfterProduction_CompletesWithinReasonableTime()
    {
        // Arrange - Test that disposal completes within reasonable time (ConnectionTimeout grace period)
        var topic = await kafka.CreateTestTopicAsync();

        var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-disposal-timeout")
            .Build();

        // Act - Produce a message to establish connection
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        });

        // Dispose with timeout to ensure it doesn't hang
        var disposeTask = producer.DisposeAsync().AsTask();
        var completedInTime = await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromSeconds(35))) == disposeTask;

        // Assert - Disposal completed within timeout (30s default + 5s buffer)
        await Assert.That(completedInTime).IsTrue();
    }

    [Test]
    public async Task Producer_ConcurrentProduction_AllRequestsCompleteWithinTimeout()
    {
        // Arrange - Test that concurrent production completes within timeout
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-concurrent-timeout")
            .Build();

        // Act - Produce messages concurrently
        var tasks = new List<ValueTask<RecordMetadata>>();
        for (int i = 0; i < 10; i++)
        {
            var i1 = i;
            tasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key{i1}",
                Value = $"value{i1}"
            }));
        }

        var results = new List<RecordMetadata>();
        foreach (var task in tasks)
        {
            results.Add(await task);
        }

        // Assert - All messages produced successfully
        await Assert.That(results).Count().IsEqualTo(10);
        await Assert.That(results.All(r => r.Offset >= 0)).IsTrue();
    }

    [Test]
    public async Task Producer_LargeVolumeProduction_FlushCompletesWithoutHanging()
    {
        // Arrange - Test that large batches with backpressure still flush within timeout
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-backpressure-timeout")
            .WithBatchSize(16384)
            .WithLingerMs(10)
            .Build();

        // Act - Send many messages to trigger backpressure
        var messageValue = new string('x', 1000); // 1 KB messages
        for (int i = 0; i < 1000; i++)
        {
            producer.Send(topic, $"key{i}", messageValue);
        }

        // Flush with timeout to ensure backpressure doesn't cause hang
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await producer.FlushAsync(cts.Token);

        // Assert - Flush completed without throwing
    }

    [Test]
    public async Task Producer_ReceiveLoop_ProcessesResponsesWithinTimeout()
    {
        // Arrange - Test that receive loop processes responses within timeout
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-receive-timeout")
            .WithAcks(Acks.All)
            .Build();

        // Act - Produce multiple messages to exercise receive loop
        for (int i = 0; i < 100; i++)
        {
            var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key{i}",
                Value = $"value{i}"
            });

            // Assert each receives a response (no timeout)
            await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
        }
    }
}
