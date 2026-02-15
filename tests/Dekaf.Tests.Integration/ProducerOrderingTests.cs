using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for producer ordering guarantees with max.in.flight.requests.
/// Verifies that idempotent producers with pipelined in-flight requests preserve
/// message ordering under various conditions including high concurrency,
/// multi-partition scenarios, and flush boundaries.
/// </summary>
[Category("Producer")]
public sealed class ProducerOrderingTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task IdempotentProducer_StrictOrdering_Preserved()
    {
        // Produce 100 sequenced messages with idempotence enabled
        // and verify consumption order matches production order exactly
        var topic = await KafkaContainer.CreateTestTopicAsync();
        const int messageCount = 100;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-strict-ordering")
            .WithAcks(Acks.All)

            .BuildAsync();

        // Produce sequentially to guarantee append order
        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "ordering-key",
                Value = $"seq-{i:D4}"
            });
        }

        // Consume and verify strict ordering
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= messageCount) break;
        }

        await Assert.That(messages).Count().IsEqualTo(messageCount);

        // Verify strict value ordering
        for (var i = 0; i < messageCount; i++)
        {
            await Assert.That(messages[i].Value).IsEqualTo($"seq-{i:D4}");
        }

        // Verify monotonically increasing offsets
        for (var i = 1; i < messages.Count; i++)
        {
            await Assert.That(messages[i].Offset).IsGreaterThan(messages[i - 1].Offset);
        }
    }

    [Test]
    public async Task IdempotentProducer_HighThroughput_OrderingPreserved()
    {
        // Fire many concurrent produces and verify per-partition ordering is preserved
        // even when multiple in-flight requests are pipelined.
    
        var topic = await KafkaContainer.CreateTestTopicAsync();
        const int messageCount = 500;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-high-throughput-ordering")
            .WithAcks(Acks.All)

            .WithLinger(TimeSpan.FromMilliseconds(2))
            .BuildAsync();

        // Fire all produces rapidly to stress pipelining
        for (var i = 0; i < messageCount; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "throughput-key",
                Value = $"msg-{i:D4}"
            });
        }

        await producer.FlushAsync();

        // Consume and verify ordering
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= messageCount) break;
        }

        await Assert.That(messages).Count().IsEqualTo(messageCount);

        // With idempotent producer and single partition, all messages should
        // be in the exact order they were submitted, even with concurrent produces
        for (var i = 0; i < messageCount; i++)
        {
            await Assert.That(messages[i].Value).IsEqualTo($"msg-{i:D4}");
        }
    }

    [Test]
    public async Task SinglePartition_OrderingAlwaysPreserved()
    {
        // Verify that messages to a single partition always maintain order
        // even with small batch sizes that force many in-flight batches.
    
        var topic = await KafkaContainer.CreateTestTopicAsync();
        const int messageCount = 200;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-single-partition-ordering")
            .WithAcks(Acks.All)

            .WithBatchSize(512) // Very small to force many batches in flight
            .WithLinger(TimeSpan.FromMilliseconds(2))
            .BuildAsync();

        // Fire-and-forget all messages then flush
        for (var i = 0; i < messageCount; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "single-part-key",
                Value = $"ordered-{i:D4}"
            });
        }

        await producer.FlushAsync();

        // Consume all and verify strict ordering
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= messageCount) break;
        }

        await Assert.That(messages).Count().IsEqualTo(messageCount);

        for (var i = 0; i < messageCount; i++)
        {
            await Assert.That(messages[i].Value).IsEqualTo($"ordered-{i:D4}");
        }
    }

    [Test]
    public async Task MultiPartition_PerPartitionOrdering_Preserved()
    {
        // With multiple partitions, verify that ordering is maintained within each partition
        // when messages are produced concurrently across partitions.
    
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        const int messagesPerPartition = 50;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-multi-partition-ordering")
            .WithAcks(Acks.All)

            .WithLinger(TimeSpan.FromMilliseconds(2))
            .BuildAsync();

        // Produce to all partitions rapidly using explicit partition assignment
        for (var p = 0; p < 4; p++)
        {
            for (var i = 0; i < messagesPerPartition; i++)
            {
                producer.Send(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"p{p}-msg-{i}",
                    Value = $"p{p}-seq-{i:D4}",
                    Partition = p
                });
            }
        }

        await producer.FlushAsync();

        // Consume from all partitions using Assign (no consumer group = no rebalance duplicates)
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(Enumerable.Range(0, 4).Select(p => new TopicPartition(topic, p)).ToArray());

        var messagesByPartition = new Dictionary<int, List<ConsumeResult<string, string>>>();
        for (var p = 0; p < 4; p++) messagesByPartition[p] = [];

        var totalExpected = 4 * messagesPerPartition;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messagesByPartition[msg.Partition].Add(msg);
            var total = messagesByPartition.Values.Sum(l => l.Count);
            if (total >= totalExpected) break;
        }

        // Verify per-partition ordering
        for (var p = 0; p < 4; p++)
        {
            var partitionMessages = messagesByPartition[p];
            await Assert.That(partitionMessages).Count().IsEqualTo(messagesPerPartition);

            // Values within each partition must be in sequence order
            for (var i = 0; i < messagesPerPartition; i++)
            {
                await Assert.That(partitionMessages[i].Value).IsEqualTo($"p{p}-seq-{i:D4}");
            }

            // Offsets within each partition must be monotonically increasing
            for (var i = 1; i < partitionMessages.Count; i++)
            {
                await Assert.That(partitionMessages[i].Offset)
                    .IsGreaterThan(partitionMessages[i - 1].Offset);
            }
        }
    }

    [Test]
    public async Task ConcurrentProduces_WithFlush_OrderingPreserved()
    {
        // Multiple concurrent producers writing to the same topic,
        // verify per-key ordering is preserved within each partition
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        const int producerCount = 3;
        const int messagesPerProducer = 50;

        var allMetadata = new ConcurrentBag<(int ProducerId, int Sequence, RecordMetadata Metadata)>();

        var tasks = Enumerable.Range(0, producerCount).Select(async producerId =>
        {
            await using var producer = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithClientId($"test-concurrent-flush-{producerId}")
                .WithAcks(Acks.All)
    
                .BuildAsync();

            for (var i = 0; i < messagesPerProducer; i++)
            {
                // Each producer uses a unique key to guarantee partition assignment
                var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"producer-{producerId}-key",
                    Value = $"producer-{producerId}-seq-{i:D4}"
                });
                allMetadata.Add((producerId, i, metadata));
            }

            await producer.FlushAsync();
        }).ToArray();

        await Task.WhenAll(tasks);

        await Assert.That(allMetadata.Count).IsEqualTo(producerCount * messagesPerProducer);

        // Consume all messages using Assign (no consumer group = no rebalance duplicates)
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(Enumerable.Range(0, 3).Select(p => new TopicPartition(topic, p)).ToArray());

        var messagesByKey = new Dictionary<string, List<string>>();
        for (var p = 0; p < producerCount; p++)
        {
            messagesByKey[$"producer-{p}-key"] = [];
        }

        var totalExpected = producerCount * messagesPerProducer;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            if (msg.Key is not null && messagesByKey.TryGetValue(msg.Key, out var keyMessages))
            {
                keyMessages.Add(msg.Value);
            }

            var total = messagesByKey.Values.Sum(l => l.Count);
            if (total >= totalExpected) break;
        }

        // Verify per-producer ordering: each producer's messages must appear in sequence
        for (var p = 0; p < producerCount; p++)
        {
            var key = $"producer-{p}-key";
            var values = messagesByKey[key];
            await Assert.That(values).Count().IsEqualTo(messagesPerProducer);

            for (var i = 0; i < messagesPerProducer; i++)
            {
                await Assert.That(values[i]).IsEqualTo($"producer-{p}-seq-{i:D4}");
            }
        }
    }

    [Test]
    public async Task MultiPartition_CoalescedSend_OrderingPreserved()
    {
        // 8 partitions with small batch size to force many simultaneous batches.
        // Under load, the sender loop drains multiple batches at once and coalesces them into
        // fewer ProduceRequests per broker. Verify per-partition ordering is preserved.
    
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 8);
        const int messagesPerPartition = 500;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-coalesced-ordering")
            .WithAcks(Acks.All)

            .WithBatchSize(1024) // Small to force many batches
            .WithLinger(TimeSpan.FromMilliseconds(2))
            .BuildAsync();

        // Fire-and-forget to all 8 partitions rapidly to stress the coalescing path
        for (var p = 0; p < 8; p++)
        {
            for (var i = 0; i < messagesPerPartition; i++)
            {
                producer.Send(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"p{p}-msg-{i}",
                    Value = $"p{p}-seq-{i:D4}",
                    Partition = p
                });
            }
        }

        await producer.FlushAsync();

        // Consume from all partitions using Assign (no consumer group = no rebalance duplicates)
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(Enumerable.Range(0, 8).Select(p => new TopicPartition(topic, p)).ToArray());

        var messagesByPartition = new Dictionary<int, List<ConsumeResult<string, string>>>();
        for (var p = 0; p < 8; p++) messagesByPartition[p] = [];

        var totalExpected = 8 * messagesPerPartition;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messagesByPartition[msg.Partition].Add(msg);
            var total = messagesByPartition.Values.Sum(l => l.Count);
            if (total >= totalExpected) break;
        }

        // Verify per-partition ordering
        for (var p = 0; p < 8; p++)
        {
            var partitionMessages = messagesByPartition[p];
            await Assert.That(partitionMessages).Count().IsEqualTo(messagesPerPartition);

            // Values within each partition must be in sequence order
            for (var i = 0; i < messagesPerPartition; i++)
            {
                await Assert.That(partitionMessages[i].Value).IsEqualTo($"p{p}-seq-{i:D4}");
            }

            // Offsets within each partition must be monotonically increasing
            for (var i = 1; i < partitionMessages.Count; i++)
            {
                await Assert.That(partitionMessages[i].Offset)
                    .IsGreaterThan(partitionMessages[i - 1].Offset);
            }
        }
    }

    [Test]
    public async Task DeferredChain_ManyBatchesPerPartition_OrderingPreserved()
    {
        // Regression test: exercises the deferred chain gate-holding path.
        // With tiny batch size (256 bytes) and single partition, most batches are deferred
        // (gate-busy) and must be chained. The gate must be held for the entire chain
        // to prevent the sender loop's next drain from stealing it via non-blocking Wait(0).
    
        var topic = await KafkaContainer.CreateTestTopicAsync();
        const int messageCount = 1000;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-deferred-chain")
            .WithAcks(Acks.All)

            .WithBatchSize(256) // Tiny: ~10 messages per batch → ~100 batches for 1000 messages
            .WithLinger(TimeSpan.FromMilliseconds(2))
            .BuildAsync();

        // Fire-and-forget all messages — forces many batches into the same drain
        for (var i = 0; i < messageCount; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "chain-key",
                Value = $"chain-{i:D4}"
            });
        }

        await producer.FlushAsync();

        // Consume and verify strict ordering
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= messageCount) break;
        }

        await Assert.That(messages).Count().IsEqualTo(messageCount);

        for (var i = 0; i < messageCount; i++)
        {
            await Assert.That(messages[i].Value).IsEqualTo($"chain-{i:D4}");
        }
    }

    [Test]
    public async Task MultiDrainCycles_OrderingPreservedAcrossDrains()
    {
        // Regression test: produces in waves to force multiple drain cycles, each with
        // deferred batches. Verifies that deferred chains from one drain don't race with
        // coalesced sends from the next drain on the same partition.
    
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        const int wavesCount = 5;
        const int messagesPerWave = 200;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-multi-drain")
            .WithAcks(Acks.All)

            .WithBatchSize(512) // Small to create many batches per wave
            .WithLinger(TimeSpan.FromMilliseconds(2))
            .BuildAsync();

        // Produce in waves with flushes between to create distinct drain cycles
        for (var wave = 0; wave < wavesCount; wave++)
        {
            for (var i = 0; i < messagesPerWave; i++)
            {
                var seq = wave * messagesPerWave + i;
                producer.Send(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"p{i % 4}-key",
                    Value = $"p{i % 4}-seq-{seq:D4}",
                    Partition = i % 4
                });
            }

            await producer.FlushAsync();
        }

        // Consume and verify per-partition ordering using Assign (no consumer group = no rebalance duplicates)
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(Enumerable.Range(0, 4).Select(p => new TopicPartition(topic, p)).ToArray());

        var messagesByPartition = new Dictionary<int, List<ConsumeResult<string, string>>>();
        for (var p = 0; p < 4; p++) messagesByPartition[p] = [];

        var totalExpected = wavesCount * messagesPerWave;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messagesByPartition[msg.Partition].Add(msg);
            var total = messagesByPartition.Values.Sum(l => l.Count);
            if (total >= totalExpected) break;
        }

        // Verify per-partition ordering: offsets must be strictly monotonically increasing
        for (var p = 0; p < 4; p++)
        {
            var partitionMessages = messagesByPartition[p];
            var expectedCount = wavesCount * messagesPerWave / 4;
            await Assert.That(partitionMessages).Count().IsEqualTo(expectedCount);

            for (var i = 1; i < partitionMessages.Count; i++)
            {
                await Assert.That(partitionMessages[i].Offset)
                    .IsGreaterThan(partitionMessages[i - 1].Offset);
            }

            // Verify value ordering within each partition
            for (var i = 0; i < partitionMessages.Count; i++)
            {
                var expectedSeq = p + i * 4; // Messages distributed round-robin: p, p+4, p+8, ...
                await Assert.That(partitionMessages[i].Value).IsEqualTo($"p{p}-seq-{expectedSeq:D4}");
            }
        }
    }

    [Test]
    public async Task RapidFireAndForget_WithFlush_OrderingPreserved()
    {
        // Regression test: rapid Send() (fire-and-forget) followed by FlushAsync().
        // With tiny batch size, this creates many deferred batches across multiple drain cycles.
        // Verifies that the flush waits for all deferred chains to complete.
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        const int messageCount = 600;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-rapid-fire-forget")
            .WithAcks(Acks.All)

            .WithBatchSize(512)
            .WithLinger(TimeSpan.FromMilliseconds(2))
            .BuildAsync();

        // Fire-and-forget to both partitions rapidly
        for (var i = 0; i < messageCount; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"p{i % 2}-key",
                Value = $"p{i % 2}-seq-{i / 2:D4}",
                Partition = i % 2
            });
        }

        // Flush must wait for all deferred chains to complete
        await producer.FlushAsync();

        // Consume and verify ordering using Assign (no consumer group = no rebalance duplicates)
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(Enumerable.Range(0, 2).Select(p => new TopicPartition(topic, p)).ToArray());

        var messagesByPartition = new Dictionary<int, List<ConsumeResult<string, string>>>();
        messagesByPartition[0] = [];
        messagesByPartition[1] = [];

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messagesByPartition[msg.Partition].Add(msg);
            var total = messagesByPartition.Values.Sum(l => l.Count);
            if (total >= messageCount) break;
        }

        // Each partition should have exactly half the messages, in order
        for (var p = 0; p < 2; p++)
        {
            var partitionMessages = messagesByPartition[p];
            await Assert.That(partitionMessages).Count().IsEqualTo(messageCount / 2);

            for (var i = 0; i < partitionMessages.Count; i++)
            {
                await Assert.That(partitionMessages[i].Value).IsEqualTo($"p{p}-seq-{i:D4}");
            }
        }
    }

    [Test]
    public async Task InterleavedPartitions_DeferredChainsDoNotInterfere()
    {
        // Regression test: interleaved partition access (p0, p1, p0, p1, ...) with tiny
        // batch size creates interleaved deferred chains for both partitions. Verifies that
        // deferred chains for different partitions run independently without cross-interference,
        // and that each partition's chain holds its own gate correctly.
    
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        const int messagesPerPartition = 400;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-interleaved-deferred")
            .WithAcks(Acks.All)

            .WithBatchSize(256) // Tiny: forces many batches
            .WithLinger(TimeSpan.FromMilliseconds(2))
            .BuildAsync();

        // Interleave: p0, p1, p0, p1, ... — this creates alternating batches that
        // will be grouped into deferred chains for each partition
        for (var i = 0; i < messagesPerPartition; i++)
        {
            // Partition 0
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "p0-key",
                Value = $"p0-seq-{i:D4}",
                Partition = 0
            });
            // Partition 1
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "p1-key",
                Value = $"p1-seq-{i:D4}",
                Partition = 1
            });
        }

        await producer.FlushAsync();

        // Consume and verify per-partition ordering using Assign (no consumer group = no rebalance duplicates)
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(Enumerable.Range(0, 2).Select(p => new TopicPartition(topic, p)).ToArray());

        var messagesByPartition = new Dictionary<int, List<ConsumeResult<string, string>>>();
        messagesByPartition[0] = [];
        messagesByPartition[1] = [];

        var totalExpected = 2 * messagesPerPartition;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messagesByPartition[msg.Partition].Add(msg);
            var total = messagesByPartition.Values.Sum(l => l.Count);
            if (total >= totalExpected) break;
        }

        for (var p = 0; p < 2; p++)
        {
            var partitionMessages = messagesByPartition[p];
            await Assert.That(partitionMessages).Count().IsEqualTo(messagesPerPartition);

            for (var i = 0; i < messagesPerPartition; i++)
            {
                await Assert.That(partitionMessages[i].Value).IsEqualTo($"p{p}-seq-{i:D4}");
            }
        }
    }

    [Test]
    public async Task HighPartitionCount_CoalescedBrokerGrouping_OrderingPreserved()
    {
        // Stress test: 16 partitions with concurrent produces forces complex broker grouping
        // and many deferred chains. With a single-broker test container, all partitions map
        // to the same broker, creating large coalesced requests.
    
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 16);
        const int messagesPerPartition = 100;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-high-partition-count")
            .WithAcks(Acks.All)

            .WithBatchSize(512)
            .WithLinger(TimeSpan.FromMilliseconds(2))
            .BuildAsync();

        for (var p = 0; p < 16; p++)
        {
            for (var i = 0; i < messagesPerPartition; i++)
            {
                producer.Send(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"p{p}-msg-{i}",
                    Value = $"p{p}-seq-{i:D4}",
                    Partition = p
                });
            }
        }

        await producer.FlushAsync();

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(Enumerable.Range(0, 16).Select(p => new TopicPartition(topic, p)).ToArray());

        var messagesByPartition = new Dictionary<int, List<ConsumeResult<string, string>>>();
        for (var p = 0; p < 16; p++) messagesByPartition[p] = [];

        var totalExpected = 16 * messagesPerPartition;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messagesByPartition[msg.Partition].Add(msg);
            var total = messagesByPartition.Values.Sum(l => l.Count);
            if (total >= totalExpected) break;
        }

        for (var p = 0; p < 16; p++)
        {
            var partitionMessages = messagesByPartition[p];
            await Assert.That(partitionMessages).Count().IsEqualTo(messagesPerPartition);

            for (var i = 0; i < messagesPerPartition; i++)
            {
                await Assert.That(partitionMessages[i].Value).IsEqualTo($"p{p}-seq-{i:D4}");
            }

            for (var i = 1; i < partitionMessages.Count; i++)
            {
                await Assert.That(partitionMessages[i].Offset)
                    .IsGreaterThan(partitionMessages[i - 1].Offset);
            }
        }
    }

    [Test]
    public async Task MixedProduceAndSend_SamePartition_OrderingPreserved()
    {
        // Regression test: rapid fire-and-forget with small batch size forces many batches
        // on a single partition, exercising deferred chains with the coalescing path.
    
        var topic = await KafkaContainer.CreateTestTopicAsync();
        const int messageCount = 300;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-mixed-produce-send")
            .WithAcks(Acks.All)

            .WithBatchSize(512)
            .WithLinger(TimeSpan.FromMilliseconds(2))
            .BuildAsync();

        for (var i = 0; i < messageCount; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "mixed-key",
                Value = $"mixed-{i:D4}"
            });
        }

        await producer.FlushAsync();

        // Consume and verify strict ordering
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= messageCount) break;
        }

        await Assert.That(messages).Count().IsEqualTo(messageCount);

        for (var i = 0; i < messageCount; i++)
        {
            await Assert.That(messages[i].Value).IsEqualTo($"mixed-{i:D4}");
        }
    }

    [Test]
    public async Task SustainedProduction_ContinuousWaves_OrderingPreserved()
    {
        // Stress test: sustained continuous production with multiple waves and no explicit
        // flushes between waves. Each wave fires concurrent produces while previous waves'
        // deferred chains may still be completing. This exercises the interaction between
        // active deferred chains and new drain cycles.
    
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        const int waves = 10;
        const int messagesPerWave = 100;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-sustained")
            .WithAcks(Acks.All)

            .WithBatchSize(512)
            .WithLinger(TimeSpan.FromMilliseconds(2))
            .BuildAsync();

        // Track per-partition sequence counters
        var partitionSeq = new int[4];

        for (var wave = 0; wave < waves; wave++)
        {
            for (var i = 0; i < messagesPerWave; i++)
            {
                var p = i % 4;
                var seq = partitionSeq[p]++;
                producer.Send(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"p{p}-key",
                    Value = $"p{p}-seq-{seq:D4}",
                    Partition = p
                });
            }

            // Don't flush between waves — let deferred chains overlap with new drains
        }

        // Flush to ensure all messages are delivered
        await producer.FlushAsync();

        // Consume and verify using Assign (no consumer group = no rebalance duplicates)
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(Enumerable.Range(0, 4).Select(p => new TopicPartition(topic, p)).ToArray());

        var messagesByPartition = new Dictionary<int, List<ConsumeResult<string, string>>>();
        for (var p = 0; p < 4; p++) messagesByPartition[p] = [];

        var totalExpected = waves * messagesPerWave;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messagesByPartition[msg.Partition].Add(msg);
            var total = messagesByPartition.Values.Sum(l => l.Count);
            if (total >= totalExpected) break;
        }

        for (var p = 0; p < 4; p++)
        {
            var partitionMessages = messagesByPartition[p];
            var expectedCount = partitionSeq[p];
            await Assert.That(partitionMessages).Count().IsEqualTo(expectedCount);

            for (var i = 0; i < partitionMessages.Count; i++)
            {
                await Assert.That(partitionMessages[i].Value).IsEqualTo($"p{p}-seq-{i:D4}");
            }

            for (var i = 1; i < partitionMessages.Count; i++)
            {
                await Assert.That(partitionMessages[i].Offset)
                    .IsGreaterThan(partitionMessages[i - 1].Offset);
            }
        }
    }

    [Test]
    public async Task BatchPoolRecycling_NoCrossPartitionContamination()
    {
        // Regression test for batch pool recycling bug: when the linger timer (different thread)
        // completes a batch and returns it to the pool, a stale thread-local cache entry can cause
        // subsequent messages to be appended to the wrong partition's batch.
        //
        // This test maximizes the probability of triggering the bug by using:
        // - Many partitions (8) to increase pool recycling across partitions
        // - Tiny batch size (256 bytes) so batches fill and rotate frequently
        // - Short linger (1ms) so the linger timer aggressively completes undersized batches
        // - Round-robin partition cycling to keep all cache entries populated
        // - No flushes between waves to let linger timer and produce thread race

        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 8);
        const int waves = 20;
        const int messagesPerWave = 80; // 10 per partition per wave

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-pool-recycling")
            .WithAcks(Acks.All)

            .WithBatchSize(256) // Tiny: forces very frequent batch rotation
            .WithLinger(TimeSpan.FromMilliseconds(1)) // Aggressive linger timer
            .BuildAsync();

        var partitionSeq = new int[8];

        for (var wave = 0; wave < waves; wave++)
        {
            for (var i = 0; i < messagesPerWave; i++)
            {
                var p = i % 8;
                var seq = partitionSeq[p]++;
                producer.Send(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"p{p}-key",
                    Value = $"p{p}-seq-{seq:D4}",
                    Partition = p
                });
            }

            // Small delay between waves to give the linger timer a chance to fire
            // and complete batches on a different thread — this is the race window
            if (wave % 5 == 4)
                await Task.Delay(5);
        }

        await producer.FlushAsync();

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(Enumerable.Range(0, 8).Select(p => new TopicPartition(topic, p)).ToArray());

        var messagesByPartition = new Dictionary<int, List<ConsumeResult<string, string>>>();
        for (var p = 0; p < 8; p++) messagesByPartition[p] = [];

        var totalExpected = waves * messagesPerWave;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messagesByPartition[msg.Partition].Add(msg);
            var total = messagesByPartition.Values.Sum(l => l.Count);
            if (total >= totalExpected) break;
        }

        for (var p = 0; p < 8; p++)
        {
            var partitionMessages = messagesByPartition[p];
            var expectedCount = partitionSeq[p];

            // Verify correct count per partition
            await Assert.That(partitionMessages).Count().IsEqualTo(expectedCount);

            // Verify NO cross-partition contamination: every message must have the correct prefix
            for (var i = 0; i < partitionMessages.Count; i++)
            {
                await Assert.That(partitionMessages[i].Value).IsEqualTo($"p{p}-seq-{i:D4}");
            }
        }
    }

    [Test]
    public async Task LingerDrivenCompletion_RapidPartitionCycling_NoCrossContamination()
    {
        // Regression test: targets the specific scenario where the linger timer completes
        // batches before they fill up. With a very large batch size (64KB) and short linger (1ms),
        // batches are ALWAYS completed by the linger timer (never by being full). This maximizes
        // the window where recycled batch objects can be rented for different partitions while
        // stale thread-local cache entries still reference them.

        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        const int messagesPerPartition = 300;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-linger-recycling")
            .WithAcks(Acks.All)

            .WithBatchSize(65536) // Large: batches never fill up from size alone
            .WithLinger(TimeSpan.FromMilliseconds(1)) // Very short: linger timer always fires first
            .BuildAsync();

        // Produce in bursts with small gaps to let the linger timer fire between bursts
        for (var burst = 0; burst < 10; burst++)
        {
            for (var i = 0; i < messagesPerPartition / 10; i++)
            {
                for (var p = 0; p < 4; p++)
                {
                    var seq = burst * (messagesPerPartition / 10) + i;
                    producer.Send(new ProducerMessage<string, string>
                    {
                        Topic = topic,
                        Key = $"p{p}-key",
                        Value = $"p{p}-seq-{seq:D4}",
                        Partition = p
                    });
                }
            }

            // Gap between bursts: gives linger timer time to complete and pool batches
            await Task.Delay(3);
        }

        await producer.FlushAsync();

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(Enumerable.Range(0, 4).Select(p => new TopicPartition(topic, p)).ToArray());

        var messagesByPartition = new Dictionary<int, List<ConsumeResult<string, string>>>();
        for (var p = 0; p < 4; p++) messagesByPartition[p] = [];

        var totalExpected = 4 * messagesPerPartition;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messagesByPartition[msg.Partition].Add(msg);
            var total = messagesByPartition.Values.Sum(l => l.Count);
            if (total >= totalExpected) break;
        }

        for (var p = 0; p < 4; p++)
        {
            var partitionMessages = messagesByPartition[p];
            await Assert.That(partitionMessages).Count().IsEqualTo(messagesPerPartition);

            for (var i = 0; i < partitionMessages.Count; i++)
            {
                await Assert.That(partitionMessages[i].Value).IsEqualTo($"p{p}-seq-{i:D4}");
            }
        }
    }

    [Test]
    public async Task ManyPartitions_FrequentBatchRotation_NoMisrouting()
    {
        // Regression test: 16 partitions with tiny batches forces maximum pool churn.
        // With 16 partitions and a 256-byte batch size, each batch holds ~7 messages.
        // Round-robin across 16 partitions means each partition gets 1 message every 16 calls,
        // so batches take ~112 Send() calls to fill — but the linger timer fires first.
        // This creates a scenario where the pool constantly recycles batches across partitions.

        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 16);
        const int messagesPerPartition = 100;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-many-partitions-rotation")
            .WithAcks(Acks.All)

            .WithBatchSize(256)
            .WithLinger(TimeSpan.FromMilliseconds(1))
            .BuildAsync();

        for (var i = 0; i < messagesPerPartition; i++)
        {
            for (var p = 0; p < 16; p++)
            {
                producer.Send(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"p{p}-key",
                    Value = $"p{p}-seq-{i:D4}",
                    Partition = p
                });
            }

            // Periodic small delay to trigger linger timer between batches
            if (i % 20 == 19)
                await Task.Delay(3);
        }

        await producer.FlushAsync();

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(Enumerable.Range(0, 16).Select(p => new TopicPartition(topic, p)).ToArray());

        var messagesByPartition = new Dictionary<int, List<ConsumeResult<string, string>>>();
        for (var p = 0; p < 16; p++) messagesByPartition[p] = [];

        var totalExpected = 16 * messagesPerPartition;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messagesByPartition[msg.Partition].Add(msg);
            var total = messagesByPartition.Values.Sum(l => l.Count);
            if (total >= totalExpected) break;
        }

        for (var p = 0; p < 16; p++)
        {
            var partitionMessages = messagesByPartition[p];
            await Assert.That(partitionMessages).Count().IsEqualTo(messagesPerPartition);

            for (var i = 0; i < partitionMessages.Count; i++)
            {
                await Assert.That(partitionMessages[i].Value).IsEqualTo($"p{p}-seq-{i:D4}");
            }

            for (var i = 1; i < partitionMessages.Count; i++)
            {
                await Assert.That(partitionMessages[i].Offset)
                    .IsGreaterThan(partitionMessages[i - 1].Offset);
            }
        }
    }
}
