using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

[Category("Serialization")]
public sealed class RecordBoundaryRoundTripTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ProduceConsume_NullKeyValueAndZeroHeaders_PreservesBoundaries()
    {
        var consumed = await RoundTripAsync(null, null, new Headers());

        await Assert.That(consumed.Key).IsNull();
        await Assert.That(consumed.Value).IsNull();
        await Assert.That(UserHeaders(consumed.Headers).Count).IsEqualTo(0);
    }

    [Test]
    public async Task ProduceConsume_EmptyKeyValueAndHeaderValue_PreservesBoundaries()
    {
        var consumed = await RoundTripAsync(
            string.Empty,
            string.Empty,
            new Headers().Add("boundary-single", Array.Empty<byte>()));

        await Assert.That(consumed.Key).IsEqualTo(string.Empty);
        await Assert.That(consumed.Value).IsEqualTo(string.Empty);
        var header = UserHeaders(consumed.Headers).Single();
        await Assert.That(header.Key).IsEqualTo("boundary-single");
        await Assert.That(header.IsValueNull).IsFalse();
        await Assert.That(header.Value.Length).IsEqualTo(0);
    }

    [Test]
    public async Task ProduceConsume_DuplicateHeaderKeys_PreservesValues()
    {
        var consumed = await RoundTripAsync(
            "duplicates",
            "duplicates",
            new Headers()
                .Add("boundary-duplicate", (byte[]?)null)
                .Add("boundary-duplicate", Array.Empty<byte>())
                .Add("boundary-duplicate", "present"));

        var headers = consumed.Headers
            .Where(header => header.Key == "boundary-duplicate")
            .ToArray();
        await Assert.That(headers.Length).IsEqualTo(3);
        await Assert.That(headers[0].IsValueNull).IsTrue();
        await Assert.That(headers[1].IsValueNull).IsFalse();
        await Assert.That(headers[1].Value.Length).IsEqualTo(0);
        await Assert.That(headers[2].GetValueAsString()).IsEqualTo("present");
    }

    [Test]
    public async Task ProduceConsume_HighHeaderCount_PreservesOrder()
    {
        const int highHeaderCount = 1024;
        // Reuse one key so this count boundary does not fill the process-global header-key cache.
        const string repeatedHeaderKey = "boundary-high";
        var headers = new Headers(highHeaderCount);
        for (var i = 0; i < highHeaderCount; i++)
        {
            headers.Add(repeatedHeaderKey, [(byte)(i % 251)]);
        }

        var consumed = await RoundTripAsync("many", "headers", headers);

        var actualHeaders = consumed.Headers
            .Where(header => header.Key == repeatedHeaderKey)
            .ToArray();
        await Assert.That(actualHeaders.Length).IsEqualTo(highHeaderCount);
        var actualValues = actualHeaders.Select(header => header.Value.Span[0]);
        var expectedValues = Enumerable.Range(0, highHeaderCount).Select(i => (byte)(i % 251));
        await Assert.That(actualValues.SequenceEqual(expectedValues)).IsTrue();
    }

    private async Task<(string? Key, string? Value, Header[] Headers)> RoundTripAsync(
        string? key,
        string? value,
        Headers headers)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string?, string?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithKeySerializer(Serializers.NullableString)
            .WithValueSerializer(Serializers.NullableString)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string?, string?>
        {
            Topic = topic,
            Partition = 0,
            Key = key,
            Value = value,
            Headers = headers
        }, CancellationToken.None);

        await using var consumer = await Kafka.CreateConsumer<string?, string?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithKeyDeserializer(Serializers.NullableString)
            .WithValueDeserializer(Serializers.NullableString)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);
        await Assert.That(result).IsNotNull();
        var record = result!.Value;
        // Header values reference pooled fetch memory; copy before disposing the consumer.
        var copiedHeaders = record.Headers?
            .Select(header => new Header(
                header.Key,
                header.IsValueNull ? null : header.Value.ToArray()))
            .ToArray() ?? [];
        return (record.Key, record.Value, copiedHeaders);
    }

    private static IReadOnlyList<Header> UserHeaders(IEnumerable<Header> headers) =>
        headers.Where(header => header.Key.StartsWith("boundary-", StringComparison.Ordinal)).ToArray();
}
