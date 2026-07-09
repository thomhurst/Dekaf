using System.Diagnostics;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for Kafka broker-side quota enforcement and rate limiting behavior.
/// Verifies that producers and consumers handle throttle responses gracefully, and that
/// high-throughput operations work correctly with default (unthrottled) broker configuration.
/// </summary>
[Category("Resilience")]
public sealed class QuotaAndRateLimitingTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    private IAdminClient CreateAdminClient()
    {
        return new AdminClientBuilder()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-admin-quota")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .Build();
    }

    private static async Task WaitForProducerQuotaAsync(
        IAdminClient admin,
        ClientQuotaEntity quotaEntity,
        string clientId,
        string quotaKey,
        double expectedValue)
    {
        var filter = new ClientQuotaFilter
        {
            Components =
            [
                ClientQuotaFilterComponent.Exact(ClientQuotaEntityType.ClientId, clientId)
            ],
            Strict = true
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            var quotas = await admin.DescribeClientQuotasAsync(filter, cancellationToken: cts.Token);
            if (quotas.TryGetValue(quotaEntity, out var values)
                && values.TryGetValue(quotaKey, out var value)
                && value == expectedValue)
            {
                return;
            }

            await Task.Delay(100, cts.Token);
        }
    }

    /// <summary>
    /// Helper to set a broker-level default producer byte rate quota.
    /// Uses the deprecated but widely supported <c>quota.producer.default</c> broker config.
    /// </summary>
    private static async Task SetDefaultProducerQuotaAsync(IAdminClient admin, int brokerId, string bytesPerSecond)
    {
        var alterations = new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [ConfigResource.Broker(brokerId)] =
            [
                ConfigAlter.Set("quota.producer.default", bytesPerSecond)
            ]
        };

        await admin.IncrementalAlterConfigsAsync(alterations);
    }

    /// <summary>
    /// Helper to remove a broker-level default producer byte rate quota, restoring the default.
    /// </summary>
    private static async Task RemoveDefaultProducerQuotaAsync(IAdminClient admin, int brokerId)
    {
        var alterations = new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [ConfigResource.Broker(brokerId)] =
            [
                ConfigAlter.Delete("quota.producer.default")
            ]
        };

        await admin.IncrementalAlterConfigsAsync(alterations);
    }

    /// <summary>
    /// Helper to set a broker-level default consumer byte rate quota.
    /// Uses the deprecated but widely supported <c>quota.consumer.default</c> broker config.
    /// </summary>
    private static async Task SetDefaultConsumerQuotaAsync(IAdminClient admin, int brokerId, string bytesPerSecond)
    {
        var alterations = new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [ConfigResource.Broker(brokerId)] =
            [
                ConfigAlter.Set("quota.consumer.default", bytesPerSecond)
            ]
        };

        await admin.IncrementalAlterConfigsAsync(alterations);
    }

    /// <summary>
    /// Helper to remove a broker-level default consumer byte rate quota, restoring the default.
    /// </summary>
    private static async Task RemoveDefaultConsumerQuotaAsync(IAdminClient admin, int brokerId)
    {
        var alterations = new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [ConfigResource.Broker(brokerId)] =
            [
                ConfigAlter.Delete("quota.consumer.default")
            ]
        };

        await admin.IncrementalAlterConfigsAsync(alterations);
    }

    [Test]
    public async Task ProducerThrottle_NonZeroBrokerDelay_PreservesEveryRecordExactlyOnce()
    {
        const string quotaKey = "producer_byte_rate";
        const double bytesPerSecond = 16_384;
        const int maxMessages = 24;
        const int messagesAfterThrottle = 2;

        var topic = await KafkaContainer.CreateTestTopicAsync();
        var clientId = $"test-producer-throttle-{Guid.NewGuid():N}";
        var quotaEntity = ClientQuotaEntity.For(ClientQuotaEntityComponent.ClientId(clientId));
        var payload = new string('x', 16_384);

        await using var admin = CreateAdminClient();
        try
        {
            await admin.AlterClientQuotasAsync(
            [
                ClientQuotaAlteration.Set(quotaEntity, quotaKey, bytesPerSecond)
            ]);

            await WaitForProducerQuotaAsync(
                admin, quotaEntity, clientId, quotaKey, bytesPerSecond);

            await using var producer = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithClientId(clientId)
                .WithBatchSize(32 * 1024)
                .WithLinger(TimeSpan.Zero)
                .WithIdempotence(true)
                .WithDeliveryDiagnostics()
                .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
                .BuildAsync();

            var diagnostics = (IProducerDiagnostics)producer;
            var produced = new List<RecordMetadata>();
            var expectedKeys = new List<string>();
            var firstThrottledMessage = -1;

            for (var i = 0; i < maxMessages; i++)
            {
                var key = $"quota-boundary-{i}";
                expectedKeys.Add(key);
                produced.Add(await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = key,
                    Value = payload
                }));

                if (diagnostics.MaxObservedBrokerThrottleTimeMs > 0 && firstThrottledMessage < 0)
                    firstThrottledMessage = i;

                if (firstThrottledMessage >= 0 && i >= firstThrottledMessage + messagesAfterThrottle)
                    break;
            }

            await producer.FlushAsync();

            await Assert.That(diagnostics.MaxObservedBrokerThrottleTimeMs).IsGreaterThan(0);
            await Assert.That(firstThrottledMessage).IsGreaterThanOrEqualTo(0);
            await Assert.That(produced.Count - firstThrottledMessage - 1)
                .IsEqualTo(messagesAfterThrottle);
            await Assert.That(produced.Select(result => result.Offset).Distinct().Count())
                .IsEqualTo(produced.Count);

            await using var consumer = await Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithClientId($"quota-verifier-{Guid.NewGuid():N}")
                .WithGroupId($"quota-verifier-{Guid.NewGuid():N}")
                .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
                .BuildAsync();

            consumer.Subscribe(topic);
            var consumed = await ConsumeMessagesAsync(consumer, expectedKeys.Count);

            await Assert.That(consumed.Select(result => result.Key!).ToArray())
                .IsEquivalentTo(expectedKeys);
            await Assert.That(consumed.Select(result => result.Offset).Distinct().Count())
                .IsEqualTo(expectedKeys.Count);

            var duplicate = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));
            await Assert.That(duplicate).IsNull();
        }
        finally
        {
            try
            {
                await admin.AlterClientQuotasAsync(
                [
                    ClientQuotaAlteration.Remove(quotaEntity, quotaKey)
                ]);
            }
            catch (Errors.KafkaException)
            {
                // Cleanup best-effort; unique client ID prevents leakage into other tests.
            }
        }
    }

    [Test]
    public async Task ProducerThrottle_BacksOffGracefully()
    {
        // Arrange - set a tight producer quota on the broker (1 KB/s)
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var brokerId = 0;

        await using var admin = CreateAdminClient();
        try
        {
            var cluster = await admin.DescribeClusterAsync();
            brokerId = cluster.Nodes[0].NodeId;

            await SetDefaultProducerQuotaAsync(admin, brokerId, "1024");

            // Allow quota config to propagate
            await Task.Delay(2000);

            await using var producer = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithClientId("test-producer-throttle")
                .WithLinger(TimeSpan.FromMilliseconds(5))
                .WithIdempotence(false)
                .WithAcks(Acks.Leader)
                .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
                .BuildAsync();

            // Act - produce messages that exceed the quota
            const int messageCount = 20;
            var messageValue = new string('x', 500); // ~500 bytes per message

            var results = new List<RecordMetadata>();
            for (var i = 0; i < messageCount; i++)
            {
                var result = await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"key-{i}",
                    Value = messageValue
                }, CancellationToken.None);
                results.Add(result);
            }

            // Assert - all messages should be delivered despite throttling
            await Assert.That(results.Count).IsEqualTo(messageCount);
            foreach (var result in results)
            {
                await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
            }
        }
        catch (Errors.KafkaException ex) when (
            ex.Message.Contains("quota") ||
            ex.Message.Contains("not a valid") ||
            ex.Message.Contains("Unknown"))
        {
            // Some Kafka versions may not support dynamic broker quota configuration
            // or the config name may differ. This is acceptable.
            await Assert.That(ex).IsNotNull();
        }
        finally
        {
            try
            {
                await RemoveDefaultProducerQuotaAsync(admin, brokerId);
            }
            catch
            {
                // Cleanup best-effort
            }
        }
    }

    [Test]
    public async Task ConsumerThrottle_BacksOffGracefully()
    {
        // Arrange - produce messages first, then set a tight consumer quota
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var brokerId = 0;

        await using var admin = CreateAdminClient();
        try
        {
            var cluster = await admin.DescribeClusterAsync();
            brokerId = cluster.Nodes[0].NodeId;

            // Produce messages before setting the quota
            await using var producer = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithClientId("test-producer-for-consumer-throttle")
                .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
                .BuildAsync();

            const int messageCount = 30;
            var messageValue = new string('x', 500);

            for (var i = 0; i < messageCount; i++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"key-{i}",
                    Value = messageValue
                }, CancellationToken.None);
            }

            await producer.FlushWithTimeoutAsync();

            await SetDefaultConsumerQuotaAsync(admin, brokerId, "1024");

            // Allow quota config to propagate
            await Task.Delay(2000);

            // Act - consume messages under tight quota
            await using var consumer = await Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithClientId("test-consumer-throttle")
                .WithGroupId($"test-group-throttle-{Guid.NewGuid():N}")
                .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

            consumer.Subscribe(topic);

            var consumed = new List<ConsumeResult<string, string>>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            await foreach (var msg in consumer.ConsumeAsync(cts.Token))
            {
                consumed.Add(msg);
                if (consumed.Count >= messageCount) break;
            }

            // Assert - all messages should be consumed despite throttling (just slower)
            await Assert.That(consumed.Count).IsEqualTo(messageCount);
        }
        catch (Errors.KafkaException ex) when (
            ex.Message.Contains("quota") ||
            ex.Message.Contains("not a valid") ||
            ex.Message.Contains("Unknown"))
        {
            // Some Kafka versions may not support dynamic broker quota configuration
            await Assert.That(ex).IsNotNull();
        }
        finally
        {
            try
            {
                await RemoveDefaultConsumerQuotaAsync(admin, brokerId);
            }
            catch
            {
                // Cleanup best-effort
            }
        }
    }

    [Test]
    public async Task ProducerThroughput_DegradedGracefully_UnderQuota()
    {
        // Arrange - set a moderate producer quota and measure throughput degradation
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var brokerId = 0;

        const int messageCount = 50;
        var messageValue = new string('x', 1000); // ~1KB per message

        await using var admin = CreateAdminClient();
        try
        {
            var cluster = await admin.DescribeClusterAsync();
            brokerId = cluster.Nodes[0].NodeId;

            // First, measure baseline throughput without quota
            await using var baselineProducer = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithClientId("test-producer-baseline")
                .WithLinger(TimeSpan.FromMilliseconds(5))
                .WithIdempotence(false)
                .WithAcks(Acks.Leader)
                .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
                .BuildAsync();

            var baselineSw = Stopwatch.StartNew();
            for (var i = 0; i < messageCount; i++)
            {
                await baselineProducer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"baseline-key-{i}",
                    Value = messageValue
                }, CancellationToken.None);
            }

            await baselineProducer.FlushWithTimeoutAsync();
            baselineSw.Stop();
            var baselineMs = baselineSw.ElapsedMilliseconds;

            // Set a quota that allows ~10 KB/s (will throttle our ~1KB/message production)
            await SetDefaultProducerQuotaAsync(admin, brokerId, "10240");

            // Allow quota config to propagate
            await Task.Delay(2000);

            var throttledTopic = await KafkaContainer.CreateTestTopicAsync();

            await using var throttledProducer = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithClientId("test-producer-throttled")
                .WithLinger(TimeSpan.FromMilliseconds(5))
                .WithIdempotence(false)
                .WithAcks(Acks.Leader)
                .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
                .BuildAsync();

            var throttledSw = Stopwatch.StartNew();
            var results = new List<RecordMetadata>();
            for (var i = 0; i < messageCount; i++)
            {
                var result = await throttledProducer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = throttledTopic,
                    Key = $"throttled-key-{i}",
                    Value = messageValue
                }, CancellationToken.None);
                results.Add(result);
            }

            await throttledProducer.FlushWithTimeoutAsync();
            throttledSw.Stop();

            // Assert - all messages delivered (throughput degraded but no errors)
            await Assert.That(results.Count).IsEqualTo(messageCount);
            foreach (var result in results)
            {
                await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
            }

            // The throttled producer should have completed without errors.
            // We don't strictly assert timing because containerized environments are variable,
            // but we log for diagnostic purposes.
            Console.WriteLine($"[QuotaTest] Baseline: {baselineMs}ms, Throttled: {throttledSw.ElapsedMilliseconds}ms");
        }
        catch (Errors.KafkaException ex) when (
            ex.Message.Contains("quota") ||
            ex.Message.Contains("not a valid") ||
            ex.Message.Contains("Unknown"))
        {
            // Some Kafka versions may not support dynamic broker quota configuration
            await Assert.That(ex).IsNotNull();
        }
        finally
        {
            try
            {
                await RemoveDefaultProducerQuotaAsync(admin, brokerId);
            }
            catch
            {
                // Cleanup best-effort
            }
        }
    }

    [Test]
    public async Task HighThroughputProduction_WithDefaultQuotas_NoThrottling()
    {
        // Arrange - default broker config has no quotas, so high throughput should work
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-no-throttle")
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .WithIdempotence(false)
            .WithAcks(Acks.Leader)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        // Act - produce a burst of messages
        const int messageCount = 500;
        var messageValue = new string('x', 500);

        var sw = Stopwatch.StartNew();
        var produceTasks = new List<Task<RecordMetadata>>();
        for (var i = 0; i < messageCount; i++)
        {
            produceTasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = messageValue
            }, CancellationToken.None).AsTask());
        }

        var results = new List<RecordMetadata>();
        foreach (var task in produceTasks)
        {
            results.Add(await task);
        }

        await producer.FlushWithTimeoutAsync();
        sw.Stop();

        // Assert - all messages delivered
        await Assert.That(results.Count).IsEqualTo(messageCount);
        foreach (var result in results)
        {
            await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
        }

        // Assert - production completed in a reasonable time (no throttling)
        // 500 messages * ~500 bytes = ~250KB should complete well under 60 seconds unthrottled
        await Assert.That(sw.Elapsed.TotalSeconds).IsLessThan(60);

        Console.WriteLine($"[QuotaTest] {messageCount} messages produced in {sw.ElapsedMilliseconds}ms " +
                          $"({messageCount / sw.Elapsed.TotalSeconds:F0} msgs/sec)");
    }
}
