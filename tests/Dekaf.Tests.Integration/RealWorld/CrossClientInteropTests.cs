using System.Text;
using ConfluentKafka = Confluent.Kafka;
using Dekaf.Compression.Lz4;
using Dekaf.Compression.Snappy;
using Dekaf.Compression.Zstd;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Uses Confluent.Kafka as an independent wire-format oracle for Dekaf producers and consumers.
/// </summary>
[Category("Interop")]
public sealed class CrossClientInteropTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    private const int ExpectedPartition = 2;
    private const int LargeMessageSize = 950_000;
    private const int LargeFetchSize = 2 * 1024 * 1024;

    private static readonly CodecCase[] CompressionCodecs =
    [
        new("none", ConfluentKafka.CompressionType.None),
        new("gzip", ConfluentKafka.CompressionType.Gzip),
        new("lz4", ConfluentKafka.CompressionType.Lz4),
        new("snappy", ConfluentKafka.CompressionType.Snappy),
        new("zstd", ConfluentKafka.CompressionType.Zstd)
    ];

    [Test]
    public async Task DekafProduce_ConfluentConsume_PreservesWireDataExactly()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var expectedRecords = CreateEdgeCaseRecords();

        await using var producer = await Kafka.CreateProducer<byte[]?, byte[]?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        foreach (var expected in expectedRecords)
        {
            var metadata = await producer.ProduceAsync(new ProducerMessage<byte[]?, byte[]?>
            {
                Topic = topic,
                Partition = ExpectedPartition,
                Key = expected.Key,
                Value = expected.Value,
                Headers = CreateDekafHeaders(expected.Headers),
                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(expected.TimestampMs)
            }, CancellationToken.None);

            await Assert.That(metadata.Partition).IsEqualTo(ExpectedPartition);
        }

        using var consumer = CreateConfluentConsumer();
        consumer.Assign(new ConfluentKafka.TopicPartitionOffset(
            topic,
            ExpectedPartition,
            ConfluentKafka.Offset.Beginning));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        foreach (var expected in expectedRecords)
        {
            var actual = consumer.Consume(cts.Token);
            await AssertConfluentRecordAsync(actual, expected);
        }
    }

    [Test]
    public async Task ConfluentProduce_DekafConsume_PreservesWireDataExactly()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var expectedRecords = CreateEdgeCaseRecords();

        using (var producer = CreateConfluentProducer())
        {
            foreach (var expected in expectedRecords)
            {
                var delivery = await producer.ProduceAsync(
                    new ConfluentKafka.TopicPartition(topic, ExpectedPartition),
                    new ConfluentKafka.Message<byte[]?, byte[]?>
                    {
                        Key = expected.Key,
                        Value = expected.Value,
                        Headers = CreateConfluentHeaders(expected.Headers),
                        Timestamp = new ConfluentKafka.Timestamp(
                            expected.TimestampMs,
                            ConfluentKafka.TimestampType.CreateTime)
                    });

                await Assert.That(delivery.Partition.Value).IsEqualTo(ExpectedPartition);
            }
        }

        await using var consumer = await CreateDekafConsumerAsync();
        consumer.Assign(new TopicPartition(topic, ExpectedPartition));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        foreach (var expected in expectedRecords)
        {
            var actual = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);
            await Assert.That(actual).IsNotNull();
            await AssertDekafRecordAsync(actual!.Value, expected);
        }
    }

    [Test]
    public async Task AllKafkaCompressionCodecs_InteroperateInBothDirections()
    {
        foreach (var codec in CompressionCodecs)
        {
            var topic = await KafkaContainer.CreateTestTopicAsync();
            var dekafKey = Encoding.UTF8.GetBytes($"dekaf-{codec.Name}");
            var dekafValue = CreatePayload(64 * 1024, codec.Name.Length);

            var dekafBuilder = ConfigureDekafCompression(
                Kafka.CreateProducer<byte[]?, byte[]?>()
                    .WithBootstrapServers(KafkaContainer.BootstrapServers)
                    .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()),
                codec.Name);

            await using (var producer = await dekafBuilder.BuildAsync())
            {
                await producer.ProduceAsync(new ProducerMessage<byte[]?, byte[]?>
                {
                    Topic = topic,
                    Partition = 0,
                    Key = dekafKey,
                    Value = dekafValue
                }, CancellationToken.None);
            }

            using (var consumer = CreateConfluentConsumer())
            {
                consumer.Assign(new ConfluentKafka.TopicPartitionOffset(
                    topic,
                    0,
                    ConfluentKafka.Offset.Beginning));
                var actual = consumer.Consume(TimeSpan.FromSeconds(30));

                await Assert.That(actual).IsNotNull();
                await AssertBytesAsync(actual!.Message.Key, dekafKey);
                await AssertBytesAsync(actual.Message.Value, dekafValue);
            }

            var confluentKey = Encoding.UTF8.GetBytes($"confluent-{codec.Name}");
            var confluentValue = CreatePayload(64 * 1024, codec.Name.Length + 17);
            using (var producer = CreateConfluentProducer(codec.ConfluentCompression))
            {
                await producer.ProduceAsync(
                    new ConfluentKafka.TopicPartition(topic, 0),
                    new ConfluentKafka.Message<byte[]?, byte[]?>
                    {
                        Key = confluentKey,
                        Value = confluentValue
                    });
            }

            await using var dekafConsumer = await CreateDekafConsumerAsync();
            dekafConsumer.Assign(new TopicPartition(topic, 0));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var first = await dekafConsumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);
            var second = await dekafConsumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

            await Assert.That(first).IsNotNull();
            await Assert.That(second).IsNotNull();
            await AssertBytesAsync(second!.Value.Key, confluentKey);
            await AssertBytesAsync(second.Value.Value, confluentValue);
        }
    }

    [Test]
    public async Task NearBrokerLimitMessages_InteroperateInBothDirections()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var dekafValue = CreatePayload(LargeMessageSize, 31);

        await using (var producer = await Kafka.CreateProducer<byte[]?, byte[]?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync())
        {
            await producer.ProduceAsync(new ProducerMessage<byte[]?, byte[]?>
            {
                Topic = topic,
                Partition = 0,
                Key = [0xD, 0xE, 0xC, 0xA, 0xF],
                Value = dekafValue
            }, CancellationToken.None);
        }

        using (var consumer = CreateConfluentConsumer(maxFetchBytes: LargeFetchSize))
        {
            consumer.Assign(new ConfluentKafka.TopicPartitionOffset(
                topic,
                0,
                ConfluentKafka.Offset.Beginning));
            var actual = consumer.Consume(TimeSpan.FromSeconds(30));

            await Assert.That(actual).IsNotNull();
            await AssertBytesAsync(actual!.Message.Value, dekafValue);
        }

        var confluentValue = CreatePayload(LargeMessageSize, 47);
        using (var producer = CreateConfluentProducer(messageMaxBytes: LargeFetchSize))
        {
            await producer.ProduceAsync(
                new ConfluentKafka.TopicPartition(topic, 0),
                new ConfluentKafka.Message<byte[]?, byte[]?>
                {
                    Key = [0xC, 0x0, 0xF, 0xF, 0xE, 0xE],
                    Value = confluentValue
                });
        }

        await using var dekafConsumer = await CreateDekafConsumerAsync(maxFetchBytes: LargeFetchSize);
        dekafConsumer.Assign(new TopicPartition(topic, 0));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var first = await dekafConsumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);
        var second = await dekafConsumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(first).IsNotNull();
        await Assert.That(second).IsNotNull();
        await AssertBytesAsync(second!.Value.Value, confluentValue);
    }

    [Test]
    public async Task TransactionMarkers_HaveReadCommittedVisibilityAcrossClients()
    {
        var dekafTopic = await KafkaContainer.CreateTestTopicAsync();
        await ProduceDekafTransactionsAsync(dekafTopic);

        using (var consumer = CreateConfluentConsumer(readCommitted: true))
        {
            consumer.Assign(new ConfluentKafka.TopicPartitionOffset(
                dekafTopic,
                0,
                ConfluentKafka.Offset.Beginning));

            var committed = consumer.Consume(TimeSpan.FromSeconds(30));
            await Assert.That(committed).IsNotNull();
            await AssertBytesAsync(committed!.Message.Value, "dekaf-committed"u8.ToArray());

            var extra = consumer.Consume(TimeSpan.FromSeconds(2));
            await Assert.That(extra).IsNull();
        }

        var confluentTopic = await KafkaContainer.CreateTestTopicAsync();
        await ProduceConfluentTransactionsAsync(confluentTopic);

        await using var dekafConsumer = await CreateDekafConsumerAsync(readCommitted: true);
        dekafConsumer.Assign(new TopicPartition(confluentTopic, 0));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var committedRecord = await dekafConsumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);
        await Assert.That(committedRecord).IsNotNull();
        await AssertBytesAsync(committedRecord!.Value.Value, "confluent-committed"u8.ToArray());

        var extraRecord = await dekafConsumer.ConsumeOneAsync(TimeSpan.FromSeconds(2), cts.Token);
        await Assert.That(extraRecord).IsNull();
    }

    private async Task ProduceDekafTransactionsAsync(string topic)
    {
        await using var producer = await Kafka.CreateProducer<byte[]?, byte[]?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId($"interop-dekaf-{Guid.NewGuid():N}")
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.InitTransactionsAsync();

        await using (var transaction = producer.BeginTransaction())
        {
            await transaction.ProduceAsync(new ProducerMessage<byte[]?, byte[]?>
            {
                Topic = topic,
                Partition = 0,
                Key = "aborted"u8.ToArray(),
                Value = "dekaf-aborted"u8.ToArray()
            }, CancellationToken.None);
            await transaction.AbortAsync();
        }

        await using (var transaction = producer.BeginTransaction())
        {
            await transaction.ProduceAsync(new ProducerMessage<byte[]?, byte[]?>
            {
                Topic = topic,
                Partition = 0,
                Key = "committed"u8.ToArray(),
                Value = "dekaf-committed"u8.ToArray()
            }, CancellationToken.None);
            await transaction.CommitAsync();
        }
    }

    private async Task ProduceConfluentTransactionsAsync(string topic)
    {
        using var producer = CreateConfluentProducer(
            transactionalId: $"interop-confluent-{Guid.NewGuid():N}");
        producer.InitTransactions(TimeSpan.FromSeconds(30));

        producer.BeginTransaction();
        await producer.ProduceAsync(
            new ConfluentKafka.TopicPartition(topic, 0),
            new ConfluentKafka.Message<byte[]?, byte[]?>
            {
                Key = "aborted"u8.ToArray(),
                Value = "confluent-aborted"u8.ToArray()
            });
        producer.AbortTransaction(TimeSpan.FromSeconds(30));

        producer.BeginTransaction();
        await producer.ProduceAsync(
            new ConfluentKafka.TopicPartition(topic, 0),
            new ConfluentKafka.Message<byte[]?, byte[]?>
            {
                Key = "committed"u8.ToArray(),
                Value = "confluent-committed"u8.ToArray()
            });
        producer.CommitTransaction(TimeSpan.FromSeconds(30));
    }

    private ConfluentKafka.IProducer<byte[]?, byte[]?> CreateConfluentProducer(
        ConfluentKafka.CompressionType compression = ConfluentKafka.CompressionType.None,
        int messageMaxBytes = 1_000_000,
        string? transactionalId = null)
    {
        return new ConfluentKafka.ProducerBuilder<byte[]?, byte[]?>(new ConfluentKafka.ProducerConfig
        {
            BootstrapServers = KafkaContainer.BootstrapServers,
            Acks = ConfluentKafka.Acks.All,
            CompressionType = compression,
            MessageMaxBytes = messageMaxBytes,
            TransactionalId = transactionalId
        }).Build();
    }

    private ConfluentKafka.IConsumer<byte[]?, byte[]?> CreateConfluentConsumer(
        bool readCommitted = false,
        int maxFetchBytes = 1_000_000)
    {
        return new ConfluentKafka.ConsumerBuilder<byte[]?, byte[]?>(new ConfluentKafka.ConsumerConfig
        {
            BootstrapServers = KafkaContainer.BootstrapServers,
            GroupId = $"interop-confluent-{Guid.NewGuid():N}",
            AutoOffsetReset = ConfluentKafka.AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            IsolationLevel = readCommitted
                ? ConfluentKafka.IsolationLevel.ReadCommitted
                : ConfluentKafka.IsolationLevel.ReadUncommitted,
            FetchMaxBytes = maxFetchBytes,
            MaxPartitionFetchBytes = maxFetchBytes
        }).Build();
    }

    private async ValueTask<IKafkaConsumer<byte[]?, byte[]?>> CreateDekafConsumerAsync(
        bool readCommitted = false,
        int maxFetchBytes = 1_000_000)
    {
        var builder = Kafka.CreateConsumer<byte[]?, byte[]?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithFetchMaxBytes(maxFetchBytes)
            .WithMaxPartitionFetchBytes(maxFetchBytes)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory());

        if (readCommitted)
        {
            builder.WithIsolationLevel(Dekaf.Protocol.Messages.IsolationLevel.ReadCommitted);
        }

        return await builder.BuildAsync();
    }

    private static ProducerBuilder<byte[]?, byte[]?> ConfigureDekafCompression(
        ProducerBuilder<byte[]?, byte[]?> builder,
        string codec)
    {
        return codec switch
        {
            "none" => builder,
            "gzip" => builder.UseGzipCompression(),
            "lz4" => builder.UseLz4Compression(),
            "snappy" => builder.UseSnappyCompression(),
            "zstd" => builder.UseZstdCompression(),
            _ => throw new ArgumentOutOfRangeException(nameof(codec), codec, "Unsupported compression codec")
        };
    }

    private static WireRecord[] CreateEdgeCaseRecords()
    {
        var timestampMs = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds();
        return
        [
            new WireRecord(
                [0x00, 0x7F, 0x80, 0xFF],
                [0xFF, 0x00, 0xFE, 0x01],
                timestampMs,
                [
                    new ExpectedHeader("duplicate", [0x01, 0x02]),
                    new ExpectedHeader("duplicate", [0x03, 0x04]),
                    new ExpectedHeader("empty", []),
                    new ExpectedHeader("null", null)
                ]),
            new WireRecord(null, [], timestampMs + 1, []),
            new WireRecord([], null, timestampMs + 2, [])
        ];
    }

    private static Headers? CreateDekafHeaders(IReadOnlyList<ExpectedHeader> expected)
    {
        if (expected.Count == 0)
        {
            return null;
        }

        var headers = new Headers(expected.Count);
        foreach (var header in expected)
        {
            headers.Add(header.Key, header.Value);
        }

        return headers;
    }

    private static ConfluentKafka.Headers? CreateConfluentHeaders(IReadOnlyList<ExpectedHeader> expected)
    {
        if (expected.Count == 0)
        {
            return null;
        }

        var headers = new ConfluentKafka.Headers();
        foreach (var header in expected)
        {
            headers.Add(header.Key, header.Value!);
        }

        return headers;
    }

    private static async Task AssertConfluentRecordAsync(
        ConfluentKafka.ConsumeResult<byte[]?, byte[]?> actual,
        WireRecord expected)
    {
        await Assert.That(actual).IsNotNull();
        await Assert.That(actual.Partition.Value).IsEqualTo(ExpectedPartition);
        await Assert.That(actual.Message.Timestamp.UnixTimestampMs).IsEqualTo(expected.TimestampMs);
        await Assert.That(actual.Message.Timestamp.Type).IsEqualTo(ConfluentKafka.TimestampType.CreateTime);
        await AssertBytesAsync(actual.Message.Key, expected.Key);
        await AssertBytesAsync(actual.Message.Value, expected.Value);

        // TUnit installs an ActivityListener, so Dekaf correctly appends W3C propagation headers.
        // Compare the caller-supplied headers byte-for-byte after excluding those known transport headers.
        var applicationHeaders = actual.Message.Headers
            .Where(static header => header.Key is not "traceparent" and not "tracestate")
            .ToArray();
        await Assert.That(applicationHeaders.Length).IsEqualTo(expected.Headers.Count);
        for (var i = 0; i < expected.Headers.Count; i++)
        {
            await Assert.That(applicationHeaders[i].Key).IsEqualTo(expected.Headers[i].Key);
            await AssertBytesAsync(applicationHeaders[i].GetValueBytes(), expected.Headers[i].Value);
        }
    }

    private static async Task AssertDekafRecordAsync(ConsumeResult<byte[]?, byte[]?> actual, WireRecord expected)
    {
        await Assert.That(actual.Partition).IsEqualTo(ExpectedPartition);
        await Assert.That(actual.TimestampMs).IsEqualTo(expected.TimestampMs);
        await Assert.That(actual.TimestampType).IsEqualTo(TimestampType.CreateTime);
        await AssertBytesAsync(actual.Key, expected.Key);
        await AssertBytesAsync(actual.Value, expected.Value);

        var actualHeaders = actual.Headers;
        await Assert.That(actualHeaders.Count).IsEqualTo(expected.Headers.Count);
        for (var i = 0; i < expected.Headers.Count; i++)
        {
            await Assert.That(actualHeaders[i].Key).IsEqualTo(expected.Headers[i].Key);
            await AssertBytesAsync(actualHeaders[i].GetValueAsArray(), expected.Headers[i].Value);
        }
    }

    private static async Task AssertBytesAsync(byte[]? actual, byte[]? expected)
    {
        if (expected is null)
        {
            await Assert.That(actual).IsNull();
            return;
        }

        await Assert.That(actual).IsNotNull();
        var equal = actual is not null && actual.AsSpan().SequenceEqual(expected);
        await Assert.That(equal).IsTrue();
    }

    private static byte[] CreatePayload(int size, int salt)
    {
        var payload = GC.AllocateUninitializedArray<byte>(size);
        for (var i = 0; i < payload.Length; i++)
        {
            payload[i] = unchecked((byte)((i * 31) + (i / 251) + salt));
        }

        return payload;
    }

    private sealed record CodecCase(string Name, ConfluentKafka.CompressionType ConfluentCompression);
    private sealed record ExpectedHeader(string Key, byte[]? Value);
    private sealed record WireRecord(
        byte[]? Key,
        byte[]? Value,
        long TimestampMs,
        IReadOnlyList<ExpectedHeader> Headers);
}
