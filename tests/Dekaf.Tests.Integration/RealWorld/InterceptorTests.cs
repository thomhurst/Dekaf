using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Tests for producer and consumer interceptors, commonly used for
/// cross-cutting concerns: tracing, metrics, header injection, auditing.
/// </summary>
public sealed class InterceptorTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ProducerInterceptor_OnSend_AddsTraceHeaders()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var interceptor = new TracingProducerInterceptor();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .AddInterceptor(interceptor)
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "traced-key",
            Value = "traced-value"
        });

        // Verify interceptor was called
        await Assert.That(interceptor.SendCount).IsEqualTo(1);

        // Verify the trace header was added to the message
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"trace-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Headers).IsNotNull();

        var traceHeader = result.Value.Headers!.First(h => h.Key == "trace-id");
        await Assert.That(traceHeader.GetValueAsString()).IsNotNull();
    }

    [Test]
    public async Task ProducerInterceptor_OnAcknowledgement_TracksDelivery()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var interceptor = new DeliveryTrackingInterceptor();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .AddInterceptor(interceptor)
            .Build();

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

        // Wait briefly for async acknowledgement callbacks
        await Task.Delay(500);

        await Assert.That(interceptor.SendCount).IsEqualTo(messageCount);
        await Assert.That(interceptor.SuccessCount).IsEqualTo(messageCount);
        await Assert.That(interceptor.ErrorCount).IsEqualTo(0);
    }

    [Test]
    public async Task ProducerInterceptor_MultipleInterceptors_ChainedInOrder()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var interceptor1 = new HeaderAddingInterceptor("interceptor-order", "first");
        var interceptor2 = new HeaderAddingInterceptor("interceptor-order", "second");

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .AddInterceptor(interceptor1)
            .AddInterceptor(interceptor2)
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "chained",
            Value = "intercepted"
        });

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"chain-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        var headers = result!.Value.Headers!.Where(h => h.Key == "interceptor-order").ToList();
        await Assert.That(headers).Count().IsEqualTo(2);
    }

    [Test]
    public async Task ConsumerInterceptor_OnConsume_InspectsMessages()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var interceptor = new MessageCountingConsumerInterceptor();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"intercepted-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .AddInterceptor(interceptor)
            .Build();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 5) break;
        }

        await Assert.That(interceptor.ConsumeCount).IsEqualTo(5);
        await Assert.That(interceptor.SeenTopics).Contains(topic);
    }

    [Test]
    public async Task ConsumerInterceptor_OnCommit_TracksCommittedOffsets()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var interceptor = new CommitTrackingConsumerInterceptor();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        for (var i = 0; i < 3; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"commit-track-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .AddInterceptor(interceptor)
            .Build();

        consumer.Subscribe(topic);

        var count = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            count++;
            if (count >= 3) break;
        }

        await consumer.CommitAsync();

        // Wait briefly for commit callback
        await Task.Delay(500);

        await Assert.That(interceptor.CommitCount).IsGreaterThanOrEqualTo(1);
        await Assert.That(interceptor.CommittedOffsets).IsNotEmpty();
    }

    [Test]
    public async Task ProducerInterceptor_WithConsumerInterceptor_EndToEndTracing()
    {
        // Full tracing scenario: producer adds trace ID, consumer reads it
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var producerInterceptor = new TracingProducerInterceptor();
        var consumerInterceptor = new TracingConsumerInterceptor();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .AddInterceptor(producerInterceptor)
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "e2e-trace",
            Value = "end-to-end"
        });

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"e2e-trace-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .AddInterceptor(consumerInterceptor)
            .Build();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();

        // Consumer interceptor should have seen the trace-id header added by producer interceptor
        await Assert.That(consumerInterceptor.SeenTraceIds).Count().IsEqualTo(1);
        await Assert.That(consumerInterceptor.SeenTraceIds[0])
            .IsEqualTo(producerInterceptor.LastTraceId);
    }

    // --- Interceptor implementations ---

    private sealed class TracingProducerInterceptor : IProducerInterceptor<string, string>
    {
        private int _sendCount;
        public int SendCount => _sendCount;
        public string? LastTraceId { get; private set; }

        public ProducerMessage<string, string> OnSend(ProducerMessage<string, string> message)
        {
            Interlocked.Increment(ref _sendCount);

            var traceId = Guid.NewGuid().ToString("N");
            LastTraceId = traceId;

            var headers = message.Headers ?? new Headers();
            headers.Add("trace-id", traceId);

            return message with { Headers = headers };
        }

        public void OnAcknowledgement(RecordMetadata metadata, Exception? exception) { }
    }

    private sealed class DeliveryTrackingInterceptor : IProducerInterceptor<string, string>
    {
        private int _sendCount;
        private int _successCount;
        private int _errorCount;

        public int SendCount => _sendCount;
        public int SuccessCount => _successCount;
        public int ErrorCount => _errorCount;

        public ProducerMessage<string, string> OnSend(ProducerMessage<string, string> message)
        {
            Interlocked.Increment(ref _sendCount);
            return message;
        }

        public void OnAcknowledgement(RecordMetadata metadata, Exception? exception)
        {
            if (exception is null)
                Interlocked.Increment(ref _successCount);
            else
                Interlocked.Increment(ref _errorCount);
        }
    }

    private sealed class HeaderAddingInterceptor(string headerKey, string headerValue)
        : IProducerInterceptor<string, string>
    {
        public ProducerMessage<string, string> OnSend(ProducerMessage<string, string> message)
        {
            var headers = message.Headers ?? new Headers();
            headers.Add(headerKey, headerValue);
            return message with { Headers = headers };
        }

        public void OnAcknowledgement(RecordMetadata metadata, Exception? exception) { }
    }

    private sealed class MessageCountingConsumerInterceptor : IConsumerInterceptor<string, string>
    {
        private int _consumeCount;
        private readonly ConcurrentBag<string> _seenTopics = [];

        public int ConsumeCount => _consumeCount;
        public ConcurrentBag<string> SeenTopics => _seenTopics;

        public ConsumeResult<string, string> OnConsume(ConsumeResult<string, string> result)
        {
            Interlocked.Increment(ref _consumeCount);
            _seenTopics.Add(result.Topic);
            return result;
        }

        public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets) { }
    }

    private sealed class CommitTrackingConsumerInterceptor : IConsumerInterceptor<string, string>
    {
        private int _commitCount;
        private readonly ConcurrentBag<TopicPartitionOffset> _committedOffsets = [];

        public int CommitCount => _commitCount;
        public ConcurrentBag<TopicPartitionOffset> CommittedOffsets => _committedOffsets;

        public ConsumeResult<string, string> OnConsume(ConsumeResult<string, string> result) => result;

        public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets)
        {
            Interlocked.Increment(ref _commitCount);
            foreach (var offset in offsets)
            {
                _committedOffsets.Add(offset);
            }
        }
    }

    private sealed class TracingConsumerInterceptor : IConsumerInterceptor<string, string>
    {
        private readonly ConcurrentBag<string> _seenTraceIds = [];

        public List<string> SeenTraceIds => [.. _seenTraceIds];

        public ConsumeResult<string, string> OnConsume(ConsumeResult<string, string> result)
        {
            var traceHeader = result.Headers?.FirstOrDefault(h => h.Key == "trace-id");
            if (traceHeader is { } header && !header.IsValueNull)
            {
                _seenTraceIds.Add(header.GetValueAsString()!);
            }

            return result;
        }

        public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets) { }
    }
}
