using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.NetworkPartition;

/// <summary>
/// Tests verifying client behavior during full broker stop/start cycles.
/// Unlike pause/unpause tests (silent TCP), these simulate real broker crashes
/// where the process is killed, logs must be re-read, and connections are reset.
/// </summary>
[Category("NetworkPartition")]
[ClassDataSource<NetworkPartitionKafkaContainer>(Shared = SharedType.PerClass)]
public class BrokerRestartTests(NetworkPartitionKafkaContainer kafka)
{
    [Test]
    public async Task Producer_RecoversAndProduces_AfterBrokerRestart()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-restart-producer")
            .WithAcks(Acks.All)
            .WithDeliveryTimeout(TimeSpan.FromSeconds(60))
            .WithRequestTimeout(TimeSpan.FromSeconds(10))
            .BuildAsync();

        // Warmup: establish connection and cache metadata
        var warmup = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "warmup",
            Value = "warmup"
        });
        await Assert.That(warmup.Offset).IsGreaterThanOrEqualTo(0);

        // Act: stop and restart broker
        await kafka.StopBrokerAsync();

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            await kafka.StartBrokerAsync();

            // Allow time for client to detect recovery and reconnect
            await Task.Delay(TimeSpan.FromSeconds(3));

            // Produce after restart - should succeed
            var postResult = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "post-restart-key",
                Value = "post-restart-value"
            });

            await Assert.That(postResult.Topic).IsEqualTo(topic);
            await Assert.That(postResult.Offset).IsGreaterThanOrEqualTo(0);
        }
        finally
        {
            await kafka.TryStartBrokerAsync();
        }

        // Verify both messages via consumer
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-restart-verify-consumer")
            .WithGroupId($"restart-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .SubscribeTo(topic)
            .BuildAsync();

        var consumed = new List<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            consumed.Add(msg.Value!);
            if (consumed.Count >= 2)
                break;
        }

        await Assert.That(consumed).Contains("warmup");
        await Assert.That(consumed).Contains("post-restart-value");
    }

    [Test]
    public async Task Consumer_RecoversAndConsumes_AfterBrokerRestart()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"restart-consumer-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-restart-consumer-producer")
            .BuildAsync();

        // Produce initial messages
        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"pre-key-{i}",
                Value = $"pre-value-{i}"
            });
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-restart-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(TimeSpan.FromSeconds(10))
            .WithHeartbeatInterval(TimeSpan.FromSeconds(2))
            .SubscribeTo(topic)
            .BuildAsync();

        // Consume all initial messages
        var preMessages = new List<string>();
        using var preCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await foreach (var msg in consumer.ConsumeAsync(preCts.Token))
        {
            preMessages.Add(msg.Value!);
            if (preMessages.Count >= 5)
                break;
        }

        await Assert.That(preMessages).Count().IsEqualTo(5);

        // Act: stop and restart broker
        await kafka.StopBrokerAsync();

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            await kafka.StartBrokerAsync();

            // Allow time for consumer to detect recovery and rejoin group
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Produce new messages after restart
            for (var i = 0; i < 3; i++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"post-key-{i}",
                    Value = $"post-value-{i}"
                });
            }

            // Consumer should recover and consume new messages
            var postMessages = new List<string>();
            using var postCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await foreach (var msg in consumer.ConsumeAsync(postCts.Token))
            {
                if (msg.Value!.StartsWith("post-", StringComparison.Ordinal))
                {
                    postMessages.Add(msg.Value);
                }

                if (postMessages.Count >= 3)
                    break;
            }

            await Assert.That(postMessages).Count().IsEqualTo(3);
        }
        finally
        {
            await kafka.TryStartBrokerAsync();
        }
    }

    [Test]
    public async Task Client_ReconnectsAfterAllBrokersTemporarilyUnavailable()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-alldown-producer")
            .WithAcks(Acks.All)
            .WithDeliveryTimeout(TimeSpan.FromSeconds(60))
            .WithRequestTimeout(TimeSpan.FromSeconds(10))
            .BuildAsync();

        // Warmup
        var warmup = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "warmup",
            Value = "warmup"
        });
        await Assert.That(warmup.Offset).IsGreaterThanOrEqualTo(0);

        // Act: stop broker, then start producing in background (will block until broker returns)
        await kafka.StopBrokerAsync();

        try
        {
            var produceTask = producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "during-outage-key",
                Value = "during-outage-value"
            }).AsTask();

            // Broker is down for 8 seconds
            await Task.Delay(TimeSpan.FromSeconds(8));
            await kafka.StartBrokerAsync();

            // Assert: the blocked produce should succeed after broker recovery
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await produceTask.WaitAsync(cts.Token);

            await Assert.That(result.Topic).IsEqualTo(topic);
            await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
        }
        finally
        {
            await kafka.TryStartBrokerAsync();
        }
    }

    [Test]
    public async Task InFlightRequests_RetriedAfterBrokerRecovery()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-inflight-retry")
            .WithAcks(Acks.All)
            .WithDeliveryTimeout(TimeSpan.FromSeconds(60))
            .WithRequestTimeout(TimeSpan.FromSeconds(10))
            .BuildAsync();

        // Warmup
        var warmup = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "warmup",
            Value = "warmup"
        });
        await Assert.That(warmup.Offset).IsGreaterThanOrEqualTo(0);

        // Act: fire off concurrent produce tasks, then immediately stop broker
        var produceTasks = new Task<RecordMetadata>[5];
        for (var i = 0; i < 5; i++)
        {
            produceTasks[i] = producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"inflight-key-{i}",
                Value = $"inflight-value-{i}"
            }).AsTask();
        }

        await kafka.StopBrokerAsync();

        try
        {
            // Broker down for 5 seconds
            await Task.Delay(TimeSpan.FromSeconds(5));
            await kafka.StartBrokerAsync();

            // Assert: all in-flight produce tasks should succeed after retry
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var results = await Task.WhenAll(produceTasks).WaitAsync(cts.Token);

            for (var i = 0; i < results.Length; i++)
            {
                await Assert.That(results[i].Topic).IsEqualTo(topic);
                await Assert.That(results[i].Offset).IsGreaterThanOrEqualTo(0);
            }
        }
        finally
        {
            await kafka.TryStartBrokerAsync();
        }
    }

    [Test]
    public async Task Producer_OrderingPreserved_AcrossBrokerRestart()
    {
        // Arrange: single partition to guarantee ordering
        var topic = await kafka.CreateTestTopicAsync(partitions: 1);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-ordering-producer")
            .WithDeliveryTimeout(TimeSpan.FromSeconds(60))
            .WithRequestTimeout(TimeSpan.FromSeconds(10))
            .ForReliability()
            .BuildAsync();

        // Produce first batch (msg-0 through msg-4) to partition 0
        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"msg-{i}",
                Partition = 0
            });
        }

        // Act: stop and restart broker
        await kafka.StopBrokerAsync();

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            await kafka.StartBrokerAsync();

            // Allow time for client to reconnect
            await Task.Delay(TimeSpan.FromSeconds(3));

            // Produce second batch (msg-5 through msg-9) to same partition
            for (var i = 5; i < 10; i++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"key-{i}",
                    Value = $"msg-{i}",
                    Partition = 0
                });
            }
        }
        finally
        {
            await kafka.TryStartBrokerAsync();
        }

        // Verify: consume all messages and check ordering
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-ordering-consumer")
            .WithGroupId($"ordering-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .SubscribeTo(topic)
            .BuildAsync();

        var consumed = new List<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            consumed.Add(msg.Value!);
            if (consumed.Count >= 10)
                break;
        }

        await Assert.That(consumed).Count().IsEqualTo(10);

        // Verify strict ordering: msg-0, msg-1, ..., msg-9
        for (var i = 0; i < 10; i++)
        {
            await Assert.That(consumed[i]).IsEqualTo($"msg-{i}");
        }
    }
}
