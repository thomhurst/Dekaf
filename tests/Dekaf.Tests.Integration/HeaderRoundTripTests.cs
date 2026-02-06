using System.Text;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

public class HeaderRoundTripTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ProduceWithHeaders_ConsumePreservesHeaders()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        var headers = new Headers()
            .Add("content-type", "application/json")
            .Add("trace-id", "abc-123");

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value",
            Headers = headers
        });

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Headers).IsNotNull();
        await Assert.That(result.Value.Headers!.Count).IsEqualTo(2);

        var contentType = result.Value.Headers.First(h => h.Key == "content-type");
        await Assert.That(Encoding.UTF8.GetString(contentType.Value.Span)).IsEqualTo("application/json");

        var traceId = result.Value.Headers.First(h => h.Key == "trace-id");
        await Assert.That(Encoding.UTF8.GetString(traceId.Value.Span)).IsEqualTo("abc-123");
    }

    [Test]
    public async Task MultipleHeadersWithSameKey_AllPreserved()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        var headers = new Headers()
            .Add("tag", "value1")
            .Add("tag", "value2")
            .Add("tag", "value3");

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value",
            Headers = headers
        });

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        var allTags = result!.Value.Headers!.Where(h => h.Key == "tag").ToList();
        await Assert.That(allTags.Count).IsEqualTo(3);
        await Assert.That(Encoding.UTF8.GetString(allTags[0].Value.Span)).IsEqualTo("value1");
        await Assert.That(Encoding.UTF8.GetString(allTags[1].Value.Span)).IsEqualTo("value2");
        await Assert.That(Encoding.UTF8.GetString(allTags[2].Value.Span)).IsEqualTo("value3");
    }

    [Test]
    public async Task NullHeaderValue_Preserved()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        var headers = new Headers()
            .Add("null-header", (byte[]?)null)
            .Add("normal-header", "has-value");

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value",
            Headers = headers
        });

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        var nullHeader = result!.Value.Headers!.First(h => h.Key == "null-header");
        await Assert.That(nullHeader.IsValueNull).IsTrue();

        var normalHeader = result.Value.Headers.First(h => h.Key == "normal-header");
        await Assert.That(Encoding.UTF8.GetString(normalHeader.Value.Span)).IsEqualTo("has-value");
    }

    [Test]
    public async Task EmptyHeaderValue_Preserved()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        var headers = new Headers()
            .Add("empty-header", Array.Empty<byte>());

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value",
            Headers = headers
        });

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        var emptyHeader = result!.Value.Headers!.First(h => h.Key == "empty-header");
        await Assert.That(emptyHeader.IsValueNull).IsFalse();
        await Assert.That(emptyHeader.Value.Length).IsEqualTo(0);
    }

    [Test]
    public async Task UnicodeHeaderValue_Preserved()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        var headers = new Headers()
            .Add("unicode", "日本語テスト");

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value",
            Headers = headers
        });

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        var unicodeHeader = result!.Value.Headers!.First(h => h.Key == "unicode");
        await Assert.That(Encoding.UTF8.GetString(unicodeHeader.Value.Span)).IsEqualTo("日本語テスト");
    }
}
