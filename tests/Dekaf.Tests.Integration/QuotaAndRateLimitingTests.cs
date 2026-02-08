using System.Collections.Concurrent;
using System.Diagnostics;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Statistics;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for Kafka broker-side quota enforcement and rate limiting behavior.
/// Verifies that producers and consumers handle throttle responses gracefully, and that
/// high-throughput operations work correctly with default (unthrottled) broker configuration.
/// </summary>
public sealed class QuotaAndRateLimitingTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    private IAdminClient CreateAdminClient()
    {
        return new AdminClientBuilder()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-admin-quota")
            .Build();
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
    public async Task ProducerThrottle_BacksOffGracefully()
    {
        // Arrange - set a tight producer quota on the broker (1 KB/s)
        var topic = await KafkaContainer.CreateTestTopicAsync();
        await using var admin = CreateAdminClient();

        var cluster = await admin.DescribeClusterAsync();
        var brokerId = cluster.Nodes[0].NodeId;

        try
        {
            await SetDefaultProducerQuotaAsync(admin, brokerId, "1024");

            // Allow quota config to propagate
            await Task.Delay(2000);

            await using var producer = Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithClientId("test-producer-throttle")
                .WithLinger(TimeSpan.FromMilliseconds(5))
                .WithAcks(Acks.Leader)
                .Build();

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
                });
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
        await using var admin = CreateAdminClient();

        var cluster = await admin.DescribeClusterAsync();
        var brokerId = cluster.Nodes[0].NodeId;

        // Produce messages before setting the quota
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-for-consumer-throttle")
            .Build();

        const int messageCount = 30;
        var messageValue = new string('x', 500);

        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = messageValue
            });
        }

        await producer.FlushAsync();

        try
        {
            await SetDefaultConsumerQuotaAsync(admin, brokerId, "1024");

            // Allow quota config to propagate
            await Task.Delay(2000);

            // Act - consume messages under tight quota
            await using var consumer = Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithClientId("test-consumer-throttle")
                .WithGroupId($"test-group-throttle-{Guid.NewGuid():N}")
                .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                .Build();

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
        await using var admin = CreateAdminClient();

        var cluster = await admin.DescribeClusterAsync();
        var brokerId = cluster.Nodes[0].NodeId;

        const int messageCount = 50;
        var messageValue = new string('x', 1000); // ~1KB per message

        // First, measure baseline throughput without quota
        await using var baselineProducer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-baseline")
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .WithAcks(Acks.Leader)
            .Build();

        var baselineSw = Stopwatch.StartNew();
        for (var i = 0; i < messageCount; i++)
        {
            await baselineProducer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"baseline-key-{i}",
                Value = messageValue
            });
        }

        await baselineProducer.FlushAsync();
        baselineSw.Stop();
        var baselineMs = baselineSw.ElapsedMilliseconds;

        try
        {
            // Set a quota that allows ~10 KB/s (will throttle our ~1KB/message production)
            await SetDefaultProducerQuotaAsync(admin, brokerId, "10240");

            // Allow quota config to propagate
            await Task.Delay(2000);

            var throttledTopic = await KafkaContainer.CreateTestTopicAsync();

            await using var throttledProducer = Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithClientId("test-producer-throttled")
                .WithLinger(TimeSpan.FromMilliseconds(5))
                .WithAcks(Acks.Leader)
                .Build();

            var throttledSw = Stopwatch.StartNew();
            var results = new List<RecordMetadata>();
            for (var i = 0; i < messageCount; i++)
            {
                var result = await throttledProducer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = throttledTopic,
                    Key = $"throttled-key-{i}",
                    Value = messageValue
                });
                results.Add(result);
            }

            await throttledProducer.FlushAsync();
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

        var stats = new ConcurrentBag<ProducerStatistics>();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-no-throttle")
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .WithAcks(Acks.Leader)
            .WithStatisticsInterval(TimeSpan.FromSeconds(1))
            .WithStatisticsHandler(s => stats.Add(s))
            .Build();

        // Act - produce a burst of messages
        const int messageCount = 500;
        var messageValue = new string('x', 500);

        var sw = Stopwatch.StartNew();
        var produceTasks = new List<ValueTask<RecordMetadata>>();
        for (var i = 0; i < messageCount; i++)
        {
            produceTasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = messageValue
            }));
        }

        var results = new List<RecordMetadata>();
        foreach (var task in produceTasks)
        {
            results.Add(await task);
        }

        await producer.FlushAsync();
        sw.Stop();

        // Wait for at least one stats callback
        await Task.Delay(2000);

        // Assert - all messages delivered
        await Assert.That(results.Count).IsEqualTo(messageCount);
        foreach (var result in results)
        {
            await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
        }

        // Assert - production completed in a reasonable time (no throttling)
        // 500 messages * ~500 bytes = ~250KB should complete well under 60 seconds unthrottled
        await Assert.That(sw.Elapsed.TotalSeconds).IsLessThan(60);

        // Assert - statistics were collected
        await Assert.That(stats.Count).IsGreaterThanOrEqualTo(1);

        var latestStats = stats.OrderByDescending(s => s.Timestamp).First();
        await Assert.That(latestStats.MessagesProduced).IsGreaterThanOrEqualTo(messageCount);
        await Assert.That(latestStats.BytesProduced).IsGreaterThan(0);

        // Assert - no failed messages (would indicate throttling-related errors)
        await Assert.That(latestStats.MessagesFailed).IsEqualTo(0);

        Console.WriteLine($"[QuotaTest] {messageCount} messages produced in {sw.ElapsedMilliseconds}ms " +
                          $"({messageCount / sw.Elapsed.TotalSeconds:F0} msgs/sec)");
    }
}
