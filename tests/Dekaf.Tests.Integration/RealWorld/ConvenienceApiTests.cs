using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Tests for convenience APIs and builder presets that simplify common usage patterns.
/// These verify that the "happy path" shorthand APIs work correctly for quick setups.
/// </summary>
[Category("Consumer")]
public sealed class ConvenienceApiTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task FactoryShortcut_CreateProducerWithServers_ProducesSuccessfully()
    {
        // Simplest possible producer creation
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducerAsync<string, string>(KafkaContainer.BootstrapServers);

        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "shortcut-key",
            Value = "shortcut-value"
        });

        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task FactoryShortcut_CreateConsumerWithTopics_ConsumesSuccessfully()
    {
        // Simplest possible consumer creation with auto-subscribe
        var topic = await KafkaContainer.CreateTestTopicAsync();

        var groupId = $"shortcut-consumer-{Guid.NewGuid():N}";
        await using var consumer = await Kafka.CreateConsumerAsync<string, string>(
            KafkaContainer.BootstrapServers, groupId, CancellationToken.None, topic);

        // Start consuming in background (default offset is Latest, so produce after consumer joins)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        ConsumeResult<string, string>? received = null;
        var consumeTask = Task.Run(async () =>
        {
            await foreach (var msg in consumer.ConsumeAsync(cts.Token))
            {
                received = msg;
                break;
            }
        });

        // Allow time for consumer to join group
        await Task.Delay(3000);

        await using var producer = await Kafka.CreateProducerAsync<string, string>(KafkaContainer.BootstrapServers);

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value"
        });

        await consumeTask;

        await Assert.That(received).IsNotNull();
        await Assert.That(received!.Value.Value).IsEqualTo("value");
    }

    [Test]
    public async Task TopicProducer_BuildForTopic_SimplifiesTopicBoundProduction()
    {
        // Using BuildForTopic for applications that only produce to one topic
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var topicProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildForTopicAsync(topic);

        var metadata = await topicProducer.ProduceAsync("topic-bound-key", "topic-bound-value");

        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task TopicProducer_ForTopic_CreatesTopicBoundFromExistingProducer()
    {
        // Using ForTopic to create topic-specific producers from a shared producer
        var topic1 = await KafkaContainer.CreateTestTopicAsync();
        var topic2 = await KafkaContainer.CreateTestTopicAsync();

        await using var baseProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        var topic1Producer = baseProducer.ForTopic(topic1);
        var topic2Producer = baseProducer.ForTopic(topic2);

        var meta1 = await topic1Producer.ProduceAsync("key1", "value1");
        var meta2 = await topic2Producer.ProduceAsync("key2", "value2");

        await Assert.That(meta1.Topic).IsEqualTo(topic1);
        await Assert.That(meta2.Topic).IsEqualTo(topic2);
    }

    [Test]
    public async Task TopicProducer_ProduceAllAsync_BatchProduction()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var topicProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildForTopicAsync(topic);

        var messages = Enumerable.Range(0, 10)
            .Select(i => ($"key-{i}" as string, $"value-{i}" as string))
            .Cast<(string? Key, string Value)>()
            .ToList();

        var results = await topicProducer.ProduceAllAsync(messages);

        await Assert.That(results).Count().IsEqualTo(10);
        foreach (var result in results)
        {
            await Assert.That(result.Topic).IsEqualTo(topic);
            await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
        }
    }

    [Test]
    public async Task TopicProducer_SendFireAndForget_WithFlush()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var topicProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildForTopicAsync(topic);

        // Fire-and-forget through topic producer
        topicProducer.Send("fire-key", "fire-value");
        await topicProducer.FlushAsync();

        // Verify by consuming
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"fire-forget-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("fire-key");
        await Assert.That(result.Value.Value).IsEqualTo("fire-value");
    }

    [Test]
    public async Task ProducerMessage_Create_TopicAndValueOnly()
    {
        // Simplest message creation - no key
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        var message = ProducerMessage<string, string>.Create(topic, "keyless-value");
        var metadata = await producer.ProduceAsync(message);

        await Assert.That(metadata.Topic).IsEqualTo(topic);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"keyless-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsNull();
        await Assert.That(result.Value.Value).IsEqualTo("keyless-value");
    }

    [Test]
    public async Task ProducerMessage_Create_WithKeyAndValue()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        var message = ProducerMessage<string, string>.Create(topic, "my-key", "my-value");
        var metadata = await producer.ProduceAsync(message);

        await Assert.That(metadata.Topic).IsEqualTo(topic);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"kv-create-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("my-key");
        await Assert.That(result.Value.Value).IsEqualTo("my-value");
    }

    [Test]
    public async Task ProducerMessage_Create_WithHeaders()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        var headers = new Headers { { "source", "test" } };
        var message = ProducerMessage<string, string>.Create(topic, "key", "value", headers);
        await producer.ProduceAsync(message);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"headers-create-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Headers).IsNotNull();
        await Assert.That(result.Value.Headers!.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ProducerPreset_ForHighThroughput_ProducesAndConsumesCorrectly()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .ForHighThroughput()
            .UseGzipCompression() // Override LZ4 (requires separate codec package) with built-in Gzip
            .BuildAsync();

        var pendingTasks = new List<ValueTask<RecordMetadata>>();
        for (var i = 0; i < 50; i++)
        {
            pendingTasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"ht-key-{i}",
                Value = $"ht-value-{i}"
            }));
        }

        foreach (var task in pendingTasks)
        {
            var meta = await task;
            await Assert.That(meta.Offset).IsGreaterThanOrEqualTo(0);
        }

        // Consume all
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"ht-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .ForHighThroughput()
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 50) break;
        }

        await Assert.That(messages).Count().IsEqualTo(50);
    }

    [Test]
    public async Task ProducerPreset_ForLowLatency_ProducesImmediately()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .ForLowLatency()
            .BuildAsync();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "low-latency",
            Value = "fast-message"
        });
        sw.Stop();

        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
        // Low latency preset should complete quickly (no batching delay)
        // Allow generous timeout for test environments
        await Assert.That(sw.ElapsedMilliseconds).IsLessThan(10_000);
    }

    [Test]
    public async Task ProducerPreset_ForReliability_IdempotentProduction()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .ForReliability()
            .BuildAsync();

        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "reliable",
            Value = "guaranteed-delivery"
        });

        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task ProduceAsync_TopicKeyValueOverload_SimpleUsage()
    {
        // Simplest ProduceAsync overload without ProducerMessage
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        var metadata = await producer.ProduceAsync(topic, "simple-key", "simple-value");

        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"simple-overload-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("simple-key");
        await Assert.That(result.Value.Value).IsEqualTo("simple-value");
    }

    [Test]
    public async Task ConsumerBuilder_SubscribeTo_SubscribesDuringBuild()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducerAsync<string, string>(KafkaContainer.BootstrapServers);
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "pre-subscribe",
            Value = "subscribed-at-build"
        });

        // SubscribeTo at build time
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"subscribe-to-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .SubscribeTo(topic)
            .BuildAsync();

        // Should already be subscribed - no need to call Subscribe()
        await Assert.That(consumer.Subscription).Contains(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Value).IsEqualTo("subscribed-at-build");
    }

    [Test]
    public async Task ProduceAllAsync_TupleOverload_BatchProduction()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        var messages = Enumerable.Range(0, 10)
            .Select(i => ($"key-{i}" as string, $"value-{i}" as string))
            .Cast<(string? Key, string Value)>()
            .ToList();

        var results = await producer.ProduceAllAsync(topic, messages);

        await Assert.That(results).Count().IsEqualTo(10);
        foreach (var result in results)
        {
            await Assert.That(result.Topic).IsEqualTo(topic);
        }
    }

    [Test]
    public async Task Headers_FluentApi_BuildHeaders()
    {
        // Test the fluent Headers API
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        var headers = Headers.Create()
            .Add("content-type", "application/json")
            .Add("version", "1.0")
            .AddIfNotNull("optional", "present")
            .AddIfNotNull("absent", null)
            .AddIfNotNullOrEmpty("empty", "")
            .AddIfNotNullOrEmpty("notempty", "has-value");

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "fluent-headers",
            Value = "payload",
            Headers = headers
        });

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"fluent-headers-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();

        var consumedHeaders = result!.Value.Headers!;
        // content-type, version, optional, notempty = 4 headers (absent and empty should be filtered)
        await Assert.That(consumedHeaders.Count).IsEqualTo(4);
        var contentType = consumedHeaders.First(h => h.Key == "content-type");
        await Assert.That(contentType.GetValueAsString()).IsEqualTo("application/json");
        var notempty = consumedHeaders.First(h => h.Key == "notempty");
        await Assert.That(notempty.GetValueAsString()).IsEqualTo("has-value");
    }
}
