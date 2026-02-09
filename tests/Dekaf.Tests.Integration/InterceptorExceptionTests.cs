using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for interceptor exception handling.
/// Verifies that exceptions thrown by producer/consumer interceptors are handled gracefully
/// without breaking the client: messages are still produced/consumed, other interceptors
/// still execute, and subsequent messages are unaffected.
/// Closes #214
/// </summary>
public sealed class InterceptorExceptionTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ProducerInterceptor_OnSendThrows_MessageStillProduced()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var throwingInterceptor = new ThrowingProducerInterceptor();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .AddInterceptor(throwingInterceptor)
            .BuildAsync();

        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key-1",
            Value = "value-1"
        });

        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);

        // Verify the message actually arrived by consuming it
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("key-1");
        await Assert.That(result.Value.Value).IsEqualTo("value-1");
    }

    [Test]
    public async Task ConsumerInterceptor_OnConsumeThrows_MessageStillDelivered()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var throwingInterceptor = new ThrowingConsumerInterceptor();

        // Produce a message normally (no interceptor on producer)
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key-1",
            Value = "value-1"
        });

        // Consume with a throwing interceptor
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .AddInterceptor(throwingInterceptor)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("key-1");
        await Assert.That(result.Value.Value).IsEqualTo("value-1");
    }

    [Test]
    public async Task ProducerInterceptor_MultipleWithOneThrowingInMiddle_OtherInterceptorsStillExecute()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var trackingBefore = new TrackingProducerInterceptor("before");
        var throwingInterceptor = new ThrowingProducerInterceptor();
        var trackingAfter = new TrackingProducerInterceptor("after");

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .AddInterceptor(trackingBefore)
            .AddInterceptor(throwingInterceptor)
            .AddInterceptor(trackingAfter)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key-1",
            Value = "value-1"
        });

        // The first interceptor (before the thrower) should have been called
        await Assert.That(trackingBefore.SendCount).IsEqualTo(1);

        // The third interceptor (after the thrower) should also have been called
        // because exceptions from one interceptor don't stop the chain
        await Assert.That(trackingAfter.SendCount).IsEqualTo(1);

        // Verify message was still produced
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Value).IsEqualTo("value-1");
    }

    [Test]
    public async Task ConsumerInterceptor_MultipleWithOneThrowingInMiddle_OtherInterceptorsStillExecute()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var trackingBefore = new TrackingConsumerInterceptor("before");
        var throwingInterceptor = new ThrowingConsumerInterceptor();
        var trackingAfter = new TrackingConsumerInterceptor("after");

        // Produce a message normally
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key-1",
            Value = "value-1"
        });

        // Consume with tracking + throwing + tracking interceptors
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .AddInterceptor(trackingBefore)
            .AddInterceptor(throwingInterceptor)
            .AddInterceptor(trackingAfter)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("key-1");
        await Assert.That(result.Value.Value).IsEqualTo("value-1");

        // Both tracking interceptors should have been called despite the middle one throwing
        await Assert.That(trackingBefore.ConsumeCount).IsEqualTo(1);
        await Assert.That(trackingAfter.ConsumeCount).IsEqualTo(1);
    }

    [Test]
    public async Task ProducerInterceptor_ThrowingDoesNotAffectSubsequentMessages()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var throwingInterceptor = new ThrowingProducerInterceptor();
        var trackingInterceptor = new TrackingProducerInterceptor("tracker");

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .AddInterceptor(throwingInterceptor)
            .AddInterceptor(trackingInterceptor)
            .BuildAsync();

        const int messageCount = 5;
        for (var i = 0; i < messageCount; i++)
        {
            var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });

            await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
        }

        // All messages should have been tracked despite the throwing interceptor
        await Assert.That(trackingInterceptor.SendCount).IsEqualTo(messageCount);

        // Verify all messages were produced by consuming them
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var consumed = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            consumed.Add(msg);
            if (consumed.Count >= messageCount) break;
        }

        await Assert.That(consumed).Count().IsEqualTo(messageCount);

        for (var i = 0; i < messageCount; i++)
        {
            await Assert.That(consumed[i].Value).IsEqualTo($"value-{i}");
        }
    }

    [Test]
    public async Task ConsumerInterceptor_ThrowingDoesNotAffectSubsequentMessages()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var throwingInterceptor = new ThrowingConsumerInterceptor();
        var trackingInterceptor = new TrackingConsumerInterceptor("tracker");

        // Produce multiple messages normally
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        const int messageCount = 5;
        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Consume with throwing + tracking interceptors
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .AddInterceptor(throwingInterceptor)
            .AddInterceptor(trackingInterceptor)
            .BuildAsync();

        consumer.Subscribe(topic);

        var consumed = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            consumed.Add(msg);
            if (consumed.Count >= messageCount) break;
        }

        await Assert.That(consumed).Count().IsEqualTo(messageCount);

        // All messages should have been tracked despite the throwing interceptor
        await Assert.That(trackingInterceptor.ConsumeCount).IsEqualTo(messageCount);

        for (var i = 0; i < messageCount; i++)
        {
            await Assert.That(consumed[i].Value).IsEqualTo($"value-{i}");
        }
    }

    // --- Interceptor implementations ---

    /// <summary>
    /// Producer interceptor that always throws InvalidOperationException in OnSend.
    /// </summary>
    private sealed class ThrowingProducerInterceptor : IProducerInterceptor<string, string>
    {
        public ProducerMessage<string, string> OnSend(ProducerMessage<string, string> message)
        {
            throw new InvalidOperationException("Intentional interceptor failure in OnSend");
        }

        public void OnAcknowledgement(RecordMetadata metadata, Exception? exception)
        {
            // No-op
        }
    }

    /// <summary>
    /// Consumer interceptor that always throws InvalidOperationException in OnConsume.
    /// </summary>
    private sealed class ThrowingConsumerInterceptor : IConsumerInterceptor<string, string>
    {
        public ConsumeResult<string, string> OnConsume(ConsumeResult<string, string> result)
        {
            throw new InvalidOperationException("Intentional interceptor failure in OnConsume");
        }

        public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets)
        {
            // No-op
        }
    }

    /// <summary>
    /// Producer interceptor that tracks messages it processes, for verifying other interceptors
    /// still execute when one throws.
    /// </summary>
    private sealed class TrackingProducerInterceptor(string name) : IProducerInterceptor<string, string>
    {
        private int _sendCount;
        private int _ackCount;
        private readonly ConcurrentBag<string> _seenValues = [];

        public int SendCount => _sendCount;
        public int AckCount => _ackCount;
        public ConcurrentBag<string> SeenValues => _seenValues;
        public string Name => name;

        public ProducerMessage<string, string> OnSend(ProducerMessage<string, string> message)
        {
            Interlocked.Increment(ref _sendCount);
            _seenValues.Add(message.Value);
            return message;
        }

        public void OnAcknowledgement(RecordMetadata metadata, Exception? exception)
        {
            Interlocked.Increment(ref _ackCount);
        }
    }

    /// <summary>
    /// Consumer interceptor that tracks consume results it processes, for verifying other interceptors
    /// still execute when one throws.
    /// </summary>
    private sealed class TrackingConsumerInterceptor(string name) : IConsumerInterceptor<string, string>
    {
        private int _consumeCount;
        private int _commitCount;
        private readonly ConcurrentBag<string> _seenValues = [];

        public int ConsumeCount => _consumeCount;
        public int CommitCount => _commitCount;
        public ConcurrentBag<string> SeenValues => _seenValues;
        public string Name => name;

        public ConsumeResult<string, string> OnConsume(ConsumeResult<string, string> result)
        {
            Interlocked.Increment(ref _consumeCount);
            _seenValues.Add(result.Value);
            return result;
        }

        public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets)
        {
            Interlocked.Increment(ref _commitCount);
        }
    }
}
