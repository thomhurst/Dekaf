using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;
using Microsoft.Extensions.Logging;

namespace Dekaf.Tests.Integration;

[Category("Consumer")]
[NotInParallel("RackAwareKafkaContainer")]
[ClassDataSource<RackAwareKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class ConsumerRackAwarenessIntegrationTests(RackAwareKafkaContainer kafka)
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RackAwarePrefetch_PreservesEveryPartitionSequence(bool batch)
    {
        const int partitions = 6;
        const int recordsPerPartition = 10_000;
        var topic = await kafka.CreateTopicWithRemoteLeaderAndLocalFollowerAsync(partitions);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await using (var producer = await Kafka.CreateProducer<string, byte[]>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithAcks(Acks.All)
            .WithIdempotence(true)
            .WithBatchSize(1024 * 1024)
            .BuildAsync(timeout.Token))
        {
            var value = new byte[1000];
            for (var offset = 0; offset < recordsPerPartition; offset++)
            {
                for (var partition = 0; partition < partitions; partition++)
                    await producer.FireAsync(new ProducerMessage<string, byte[]>
                    {
                        Topic = topic, Partition = partition, Key = "key", Value = value
                    });
            }
            await producer.FlushAsync(timeout.Token);
        }

        // Real connections exercise routing changes; no response or connection wrapper.
        await using var consumer = new KafkaConsumer<string, byte[]>(new ConsumerOptions
        {
            BootstrapServers = kafka.BootstrapServers.Split(','),
            ClientRack = "rack-a",
            AutoOffsetReset = AutoOffsetReset.None,
            OffsetCommitMode = OffsetCommitMode.Manual,
            EnableAutoOffsetStore = false,
            EnableFetchSessions = false,
            ConnectionsPerBroker = 3,
            MaxConnectionsPerBroker = 3,
            EnableAdaptiveConnections = false,
            PrefetchPipelineDepth = 5,
            MaxPartitionFetchBytes = 1024 * 1024,
            FetchMaxBytes = 16 * 1024 * 1024
        }, Serializers.String, Serializers.ByteArray);
        await consumer.InitializeAsync(timeout.Token);
        consumer.IncrementalAssign(Enumerable.Range(0, partitions)
            .Select(partition => new TopicPartitionOffset(topic, partition, 0)).ToArray());
        var nextOffsets = new long[partitions];
        var consumed = 0;

        void Verify(ConsumeResult<string, byte[]> record)
        {
            var expected = nextOffsets[record.Partition];
            if (record.Offset != expected || expected >= recordsPerPartition)
                throw new InvalidOperationException($"Partition {record.Partition}: got {record.Offset}, expected {expected}.");
            nextOffsets[record.Partition]++;
            consumed++;
        }

        if (batch)
        {
            await foreach (var records in consumer.ConsumeBatchAsync(timeout.Token))
            {
                foreach (var record in records)
                    Verify(record);
                if (consumed == partitions * recordsPerPartition)
                    break;
            }
        }
        else
        {
            await foreach (var record in consumer.ConsumeAsync(timeout.Token))
            {
                Verify(record);
                if (consumed == partitions * recordsPerPartition)
                    break;
            }
        }
        await Assert.That(nextOffsets.All(static offset => offset == recordsPerPartition)).IsTrue();
    }

    [Test]
    public async Task Consumer_WithClientRack_FetchesFromPreferredReadReplica()
    {
        var topic = await kafka.CreateTopicWithRemoteLeaderAndLocalFollowerAsync().ConfigureAwait(false);

        await ProduceAsync(kafka.BootstrapServers, topic, key: "first", value: "first").ConfigureAwait(false);

        using var logs = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(logs);
        });

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId($"rack-aware-consumer-{Guid.NewGuid():N}")
            .WithClientRack("rack-a")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(loggerFactory)
            .BuildAsync()
            .ConfigureAwait(false);

        consumer.Assign(new TopicPartition(topic, 0));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var first = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);
        await Assert.That(first).IsNotNull();
        await Assert.That(first!.Value.Value).IsEqualTo("first");

        await ProduceAsync(kafka.BootstrapServers, topic, key: "second", value: "second").ConfigureAwait(false);

        var second = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);
        await Assert.That(second).IsNotNull();
        await Assert.That(second!.Value.Value).IsEqualTo("second");

        await WaitForPreferredReadReplicaLogAsync(logs, topic).ConfigureAwait(false);
    }

    private static async Task ProduceAsync(string bootstrapServers, string topic, string key, string value)
    {
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(bootstrapServers)
            .WithClientId($"rack-aware-producer-{Guid.NewGuid():N}")
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync()
            .ConfigureAwait(false);

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Partition = 0,
            Key = key,
            Value = value
        }, CancellationToken.None).ConfigureAwait(false);

        await producer.FlushWithTimeoutAsync().ConfigureAwait(false);
    }

    private static async Task WaitForPreferredReadReplicaLogAsync(
        CapturingLoggerProvider logs,
        string topic)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (logs.Entries.Any(entry =>
                entry.Message.Contains($"Fetching {topic}-0 from preferred read replica 2 instead of leader 1",
                    StringComparison.Ordinal)))
            {
                return;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        throw new InvalidOperationException("Consumer did not fetch from preferred read replica 2.");
    }
}
