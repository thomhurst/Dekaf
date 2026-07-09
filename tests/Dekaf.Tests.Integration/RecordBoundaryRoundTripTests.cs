using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

[Category("Serialization")]
public sealed class RecordBoundaryRoundTripTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ProduceConsume_PreservesNullEmptyAndHeaderBoundaries()
    {
        const int highHeaderCount = 1024;
        // Reuse one key so this count boundary does not fill the process-global header-key cache.
        const string repeatedHeaderKey = "boundary-high";
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var highHeaders = new Headers(highHeaderCount);
        for (var i = 0; i < highHeaderCount; i++)
        {
            highHeaders.Add(repeatedHeaderKey, [(byte)(i % 251)]);
        }

        var messages = new ProducerMessage<string?, string?>[]
        {
            new()
            {
                Topic = topic,
                Partition = 0,
                Key = null,
                Value = null,
                Headers = new Headers()
            },
            new()
            {
                Topic = topic,
                Partition = 0,
                Key = string.Empty,
                Value = string.Empty,
                Headers = new Headers().Add("boundary-single", Array.Empty<byte>())
            },
            new()
            {
                Topic = topic,
                Partition = 0,
                Key = "duplicates",
                Value = "duplicates",
                Headers = new Headers()
                    .Add("boundary-duplicate", (byte[]?)null)
                    .Add("boundary-duplicate", Array.Empty<byte>())
                    .Add("boundary-duplicate", "present")
            },
            new()
            {
                Topic = topic,
                Partition = 0,
                Key = "many",
                Value = "headers",
                Headers = highHeaders
            }
        };

        await using var producer = await Kafka.CreateProducer<string?, string?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithKeySerializer(Serializers.NullableString)
            .WithValueSerializer(Serializers.NullableString)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        foreach (var message in messages)
        {
            await producer.ProduceAsync(message, CancellationToken.None);
        }

        await using var consumer = await Kafka.CreateConsumer<string?, string?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithKeyDeserializer(Serializers.NullableString)
            .WithValueDeserializer(Serializers.NullableString)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        var consumed = new List<(string? Key, string? Value, Header[] Headers)>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        for (var i = 0; i < messages.Length; i++)
        {
            var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);
            await Assert.That(result).IsNotNull();
            var record = result!.Value;
            consumed.Add((record.Key, record.Value, record.Headers?.ToArray() ?? []));
        }

        await Assert.That(consumed[0].Key).IsNull();
        await Assert.That(consumed[0].Value).IsNull();
        await Assert.That(UserHeaders(consumed[0].Headers).Count).IsEqualTo(0);

        await Assert.That(consumed[1].Key).IsEqualTo(string.Empty);
        await Assert.That(consumed[1].Value).IsEqualTo(string.Empty);
        var singleHeader = UserHeaders(consumed[1].Headers).Single();
        await Assert.That(singleHeader.Key).IsEqualTo("boundary-single");
        await Assert.That(singleHeader.IsValueNull).IsFalse();
        await Assert.That(singleHeader.Value.Length).IsEqualTo(0);

        var duplicateHeaders = consumed[2].Headers
            .Where(header => header.Key == "boundary-duplicate")
            .ToArray();
        await Assert.That(duplicateHeaders.Length).IsEqualTo(3);
        await Assert.That(duplicateHeaders[0].IsValueNull).IsTrue();
        await Assert.That(duplicateHeaders[1].IsValueNull).IsFalse();
        await Assert.That(duplicateHeaders[1].Value.Length).IsEqualTo(0);
        await Assert.That(duplicateHeaders[2].GetValueAsString()).IsEqualTo("present");

        var actualHighHeaders = consumed[3].Headers
            .Where(header => header.Key == repeatedHeaderKey)
            .ToArray();
        await Assert.That(actualHighHeaders.Length).IsEqualTo(highHeaderCount);
        var actualValues = actualHighHeaders.Select(header => header.Value.Span[0]);
        var expectedValues = Enumerable.Range(0, highHeaderCount).Select(i => (byte)(i % 251));
        await Assert.That(actualValues.SequenceEqual(expectedValues)).IsTrue();
    }

    private static IReadOnlyList<Header> UserHeaders(IEnumerable<Header> headers) =>
        headers.Where(header => header.Key.StartsWith("boundary-", StringComparison.Ordinal)).ToArray();
}
