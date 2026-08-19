using System.Text;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

[Category("Serialization")]
public class HeaderRoundTripTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ProduceWithHeaders_ConsumePreservesHeaders()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var headers = new Headers()
            .Add("content-type", "application/json")
            .Add("trace-id", "abc-123");

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value",
            Headers = headers
        }, CancellationToken.None);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        // >= 2 because TUnit's ActivityListener may inject a traceparent header
        await Assert.That(result!.Value.Headers.Count).IsGreaterThanOrEqualTo(2);

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

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

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
        }, CancellationToken.None);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        var allTags = result!.Value.Headers.Where(h => h.Key == "tag").ToList();
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

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var headers = new Headers()
            .Add("null-header", (byte[]?)null)
            .Add("normal-header", "has-value");

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value",
            Headers = headers
        }, CancellationToken.None);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        var nullHeader = result!.Value.Headers.First(h => h.Key == "null-header");
        await Assert.That(nullHeader.IsValueNull).IsTrue();

        var normalHeader = result.Value.Headers.First(h => h.Key == "normal-header");
        await Assert.That(Encoding.UTF8.GetString(normalHeader.Value.Span)).IsEqualTo("has-value");
    }

    [Test]
    public async Task ConsumeRawBatch_HeadersAndLeaderEpoch_DecodeFromBrokerFetch()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var partition = new TopicPartition(topic, 0);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        var headers = new Headers()
            .Add("null-header", (byte[]?)null)
            .Add("normal-header", "has-value");

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Partition = partition.Partition,
            Key = "key",
            Value = "value",
            Headers = headers
        }, CancellationToken.None);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithQueuedMinMessages(1)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        consumer.Assign(partition);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var batches = consumer.ConsumeRawBatchAsync(timeout.Token).GetAsyncEnumerator();

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        var batch = batches.Current;
        using var records = batch.GetEnumerator();
        await Assert.That(records.MoveNext()).IsTrue();
        var record = records.Current;
        var decodedHeaders = record.Headers;
        var leaderEpoch = record.LeaderEpoch;
        var sawNullHeader = false;
        string? normalHeaderValue = null;
        for (var index = 0; index < decodedHeaders.Length; index++)
        {
            var header = decodedHeaders.Span[index];
            if (header.Key == "null-header")
                sawNullHeader = header.IsValueNull;
            else if (header.Key == "normal-header")
                normalHeaderValue = Encoding.UTF8.GetString(header.Value.Span);
        }

        await Assert.That(batch.TopicPartition).IsEqualTo(partition);
        await Assert.That(leaderEpoch).IsNotNull();
        await Assert.That(leaderEpoch!.Value).IsGreaterThanOrEqualTo(0);
        await Assert.That(sawNullHeader).IsTrue();
        await Assert.That(normalHeaderValue).IsEqualTo("has-value");
        await Assert.That(records.MoveNext()).IsFalse();
    }

    [Test]
    public async Task EmptyHeaderValue_Preserved()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var headers = new Headers()
            .Add("empty-header", Array.Empty<byte>());

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value",
            Headers = headers
        }, CancellationToken.None);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        var emptyHeader = result!.Value.Headers.First(h => h.Key == "empty-header");
        await Assert.That(emptyHeader.IsValueNull).IsFalse();
        await Assert.That(emptyHeader.Value.Length).IsEqualTo(0);
    }

    [Test]
    public async Task UnicodeHeaderValue_Preserved()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var headers = new Headers()
            .Add("unicode", "日本語テスト");

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value",
            Headers = headers
        }, CancellationToken.None);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        var unicodeHeader = result!.Value.Headers.First(h => h.Key == "unicode");
        await Assert.That(Encoding.UTF8.GetString(unicodeHeader.Value.Span)).IsEqualTo("日本語テスト");
    }
}
