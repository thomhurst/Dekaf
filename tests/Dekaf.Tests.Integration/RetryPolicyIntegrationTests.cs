using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Retry;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for built-in retry policies with real Kafka.
/// Verifies that retry policies integrate correctly with producer and consumer.
/// </summary>
[Category("Retry")]
public sealed class RetryPolicyIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task Producer_WithRetryPolicy_SuccessfulProduceIsUnaffected()
    {
        // A retry policy should have zero impact on successful produces.
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .WithRetryPolicy(new ExponentialBackoffRetryPolicy
            {
                BaseDelay = TimeSpan.FromMilliseconds(100),
                MaxDelay = TimeSpan.FromSeconds(5),
                MaxAttempts = 3
            })
            .BuildAsync();

        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "retry-key",
            Value = "retry-value"
        });

        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Partition).IsGreaterThanOrEqualTo(0);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);

        // Verify message was actually delivered
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"test-group-retry-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            await Assert.That(msg.Key).IsEqualTo("retry-key");
            await Assert.That(msg.Value).IsEqualTo("retry-value");
            break;
        }
    }

    [Test]
    public async Task Producer_WithNoRetryPolicy_SuccessfulProduceIsUnaffected()
    {
        // Explicit opt-out via NoRetryPolicy should behave identically to no policy.
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithRetryPolicy(NoRetryPolicy.Instance)
            .BuildAsync();

        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "no-retry-key",
            Value = "no-retry-value"
        });

        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_WithFixedDelayRetryPolicy_SuccessfulProduceIsUnaffected()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithRetryPolicy(new FixedDelayRetryPolicy
            {
                Delay = TimeSpan.FromMilliseconds(50),
                MaxAttempts = 5
            })
            .BuildAsync();

        // Produce multiple messages to verify retry policy doesn't interfere
        for (var i = 0; i < 10; i++)
        {
            var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });

            await Assert.That(metadata.Topic).IsEqualTo(topic);
            await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
        }
    }

    [Test]
    public async Task Producer_WithRetryPolicy_NonRetriableError_DoesNotRetry()
    {
        // MessageTooLarge is NOT retriable - retry policy should not kick in.
        var topicName = $"test-topic-retry-nonretriable-{Guid.NewGuid():N}";

        await using var adminClient = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        await adminClient.CreateTopicsAsync([
            new Admin.NewTopic
            {
                Name = topicName,
                NumPartitions = 1,
                ReplicationFactor = 1,
                Configs = new Dictionary<string, string>
                {
                    ["max.message.bytes"] = "512"
                }
            }
        ]);

        // Wait for metadata propagation
        await Task.Delay(3000);

        var retryCount = 0;
        var trackingPolicy = new TrackingRetryPolicy(new FixedDelayRetryPolicy
        {
            Delay = TimeSpan.FromMilliseconds(50),
            MaxAttempts = 3
        }, () => Interlocked.Increment(ref retryCount));

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .WithRetryPolicy(trackingPolicy)
            .BuildAsync();

        var oversizedValue = new string('X', 4096);

        KafkaException? caughtException = null;
        try
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topicName,
                Key = "key",
                Value = oversizedValue
            });
        }
        catch (KafkaException ex)
        {
            caughtException = ex;
        }

        // Should have gotten an error
        if (caughtException is not null)
        {
            // MessageTooLarge is NOT retriable, so retry policy should not have been consulted
            await Assert.That(caughtException.IsRetriable).IsFalse();
            await Assert.That(retryCount).IsEqualTo(0);
        }
    }

    [Test]
    public async Task Producer_WithRetryPolicy_MultipleMessages_ThreadSafe()
    {
        // Verify retry policy works correctly under concurrent produce load.
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithRetryPolicy(new ExponentialBackoffRetryPolicy
            {
                BaseDelay = TimeSpan.FromMilliseconds(10),
                MaxDelay = TimeSpan.FromSeconds(1),
                MaxAttempts = 3
            })
            .BuildAsync();

        const int messageCount = 100;
        var tasks = new Task<RecordMetadata>[messageCount];

        for (var i = 0; i < messageCount; i++)
        {
            tasks[i] = producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }).AsTask();
        }

        var results = await Task.WhenAll(tasks);

        await Assert.That(results.Length).IsEqualTo(messageCount);
        foreach (var result in results)
        {
            await Assert.That(result.Topic).IsEqualTo(topic);
            await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
        }
    }

    /// <summary>
    /// Wraps an IRetryPolicy to track how many times GetNextDelay is called.
    /// </summary>
    private sealed class TrackingRetryPolicy(IRetryPolicy inner, Action onGetNextDelay) : IRetryPolicy
    {
        public TimeSpan? GetNextDelay(int attemptNumber, Exception exception)
        {
            onGetNextDelay();
            return inner.GetNextDelay(attemptNumber, exception);
        }
    }
}
