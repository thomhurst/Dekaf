using System.Diagnostics;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Targeted diagnostic tests to isolate producer hang issues.
/// Each test has a timeout (60s, accounting for container startup) and tests a specific scenario.
/// If a specific test hangs, it narrows down the root cause.
/// </summary>
[Category("Producer")]
public sealed class ProducerDiagnosticTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    // ===== PHASE 1: Does single-message produce work? =====

    [Test]
    [Timeout(60_000)]
    public async Task Diag01_ProduceAsync_SingleMessage(CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .BuildAsync(cancellationToken);

        var result = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "k",
            Value = "v"
        }, cancellationToken);

        await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
    }

    // ===== PHASE 2: Does Send + FlushAsync work for a single message? =====

    [Test]
    [Timeout(60_000)]
    public async Task Diag02_SendAndFlush_SingleMessage(CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .BuildAsync(cancellationToken);

        producer.Send(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "k",
            Value = "v"
        });

        await producer.FlushAsync(cancellationToken);
    }

    // ===== PHASE 3: Multiple messages with ProduceAsync (awaited, no flush needed) =====

    [Test]
    [Timeout(60_000)]
    public async Task Diag03_ProduceAsync_10Messages(CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .BuildAsync(cancellationToken);

        for (var i = 0; i < 10; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "k",
                Value = $"v-{i}"
            }, cancellationToken);
        }
    }

    // ===== PHASE 4: Send + Flush with increasing message counts =====

    [Test]
    [Timeout(60_000)]
    public async Task Diag04_SendAndFlush_10Messages(CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .BuildAsync(cancellationToken);

        for (var i = 0; i < 10; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "k",
                Value = $"v-{i}"
            });
        }

        await producer.FlushAsync(cancellationToken);
    }

    [Test]
    [Timeout(60_000)]
    public async Task Diag05_SendAndFlush_100Messages(CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .BuildAsync(cancellationToken);

        for (var i = 0; i < 100; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "k",
                Value = $"v-{i}"
            });
        }

        await producer.FlushAsync(cancellationToken);
    }

    // ===== PHASE 5: Small batch sizes (force many batches through BrokerSender) =====

    [Test]
    [Timeout(60_000)]
    public async Task Diag06_SendAndFlush_SmallBatch_50Messages(CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .WithBatchSize(256) // Very small batch to force many batch rotations
            .WithLinger(TimeSpan.FromMilliseconds(1))
            .BuildAsync(cancellationToken);

        for (var i = 0; i < 50; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "k",
                Value = $"v-{i}"
            });
        }

        await producer.FlushAsync(cancellationToken);
    }

    // ===== PHASE 6: Multiple partitions (tests coalescing) =====

    [Test]
    [Timeout(60_000)]
    public async Task Diag07_SendAndFlush_MultiPartition(CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        await using var producer = await Kafka.CreateProducer<int, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .BuildAsync(cancellationToken);

        for (var p = 0; p < 4; p++)
        {
            for (var i = 0; i < 25; i++)
            {
                producer.Send(new ProducerMessage<int, string>
                {
                    Topic = topic,
                    Partition = p,
                    Key = p,
                    Value = $"p{p}-v{i}"
                });
            }
        }

        await producer.FlushAsync(cancellationToken);
    }

    // ===== PHASE 7: High volume with flush timeout diagnostic =====

    [Test]
    [Timeout(60_000)]
    public async Task Diag08_SendAndFlush_500Messages_SmallBatch_WithDiagnostics(CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .WithBatchSize(512)
            .WithLinger(TimeSpan.FromMilliseconds(1))
            .BuildAsync(cancellationToken);

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 500; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "k",
                Value = $"v-{i:D4}"
            });
        }

        var sendElapsed = sw.Elapsed;

        using var flushCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        flushCts.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            await producer.FlushAsync(flushCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"FlushAsync timed out after 20s. " +
                $"Send phase took {sendElapsed.TotalMilliseconds:F0}ms for 500 messages. " +
                $"Total elapsed: {sw.Elapsed.TotalSeconds:F1}s");
        }

        // Verify messages were delivered by consuming
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync(cancellationToken);
        consumer.Assign(new TopicPartition(topic, 0));

        var count = 0;
        using var consumeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        consumeCts.CancelAfter(TimeSpan.FromSeconds(10));
        await foreach (var msg in consumer.ConsumeAsync(consumeCts.Token))
        {
            count++;
            if (count >= 500) break;
        }

        await Assert.That(count).IsEqualTo(500);
    }

    // ===== PHASE 8: Acks.None (fire-and-forget at protocol level - bypasses response path) =====

    [Test]
    [Timeout(60_000)]
    public async Task Diag09_SendAndFlush_AcksNone_100Messages(CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.None)
            .BuildAsync(cancellationToken);

        for (var i = 0; i < 100; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "k",
                Value = $"v-{i}"
            });
        }

        await producer.FlushAsync(cancellationToken);
    }
}
