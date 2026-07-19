using System.Diagnostics;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol;
using Microsoft.Extensions.Logging;

namespace Dekaf.Tests.Integration;

[Category("Admin")]
[Category("Producer")]
[NotInParallel("RackAwareKafkaContainer")]
[ClassDataSource<RackAwareKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class DynamicConfigurationIntegrationTests(RackAwareKafkaContainer kafka)
{
    private const int SmallMessageSize = 1_024;
    private const int LargeMessageSize = 600 * 1_024;
    private const int LowMaxMessageBytes = 256 * 1_024;
    private const int HighMaxMessageBytes = 1_024 * 1_024;

    [Test]
    [Timeout(120_000)]
    public async Task Admin_IncrementalAlterConfigs_VisibleToDescribeConfigs(
        CancellationToken cancellationToken)
    {
        var topic = await CreateTopicAsync(HighMaxMessageBytes, cancellationToken).ConfigureAwait(false);
        await using var admin = kafka.CreateAdminClient();

        await SetTopicConfigAsync(
            admin,
            topic,
            "max.message.bytes",
            LowMaxMessageBytes.ToString(),
            cancellationToken).ConfigureAwait(false);

        var value = await GetTopicConfigAsync(
            admin,
            topic,
            "max.message.bytes",
            cancellationToken).ConfigureAwait(false);
        await Assert.That(value).IsEqualTo(LowMaxMessageBytes.ToString());
    }

    [Test]
    [Timeout(120_000)]
    public async Task Producer_MaxMessageBytesLoweredMidRun_OversizedRecordsFailCleanly_UndersizedContinue(
        CancellationToken cancellationToken)
    {
        var topic = await CreateTopicAsync(HighMaxMessageBytes, cancellationToken).ConfigureAwait(false);
        await using var producer = await CreateByteProducerAsync(cancellationToken).ConfigureAwait(false);
        await using var admin = kafka.CreateAdminClient();
        var largeMessage = new byte[LargeMessageSize];
        var smallMessage = new byte[SmallMessageSize];

        var initial = await producer.ProduceAsync(topic, null, largeMessage, cancellationToken)
            .ConfigureAwait(false);
        await SetTopicConfigAsync(
            admin,
            topic,
            "max.message.bytes",
            LowMaxMessageBytes.ToString(),
            cancellationToken).ConfigureAwait(false);

        // Latent race, mirror of the raised-limit test: if the lowered limit has not reached the
        // partition leader yet, this oversized produce would be accepted and the Throws assertion
        // would flake. Not yet observed in CI; the raised-limit direction uses
        // ProduceUntilAcceptedAsync for the same reason.
        var oversizedTask = producer.ProduceAsync(topic, null, largeMessage, cancellationToken).AsTask();
        var validTask = producer.ProduceAsync(topic, null, smallMessage, cancellationToken).AsTask();
        var exception = await Assert.That(async () => await oversizedTask.ConfigureAwait(false))
            .Throws<ProduceException>();
        var valid = await validTask.ConfigureAwait(false);
        var sentinel = await producer.ProduceAsync(topic, null, smallMessage, cancellationToken)
            .ConfigureAwait(false);

        await Assert.That(initial.Offset).IsEqualTo(0);
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.MessageTooLarge);
        await Assert.That(exception.Topic).IsEqualTo(topic);
        await Assert.That(exception.Partition).IsEqualTo(0);
        await Assert.That(valid.Offset).IsEqualTo(1);
        await Assert.That(sentinel.Offset).IsEqualTo(2);
        await AssertRecordSizesAsync(topic, [LargeMessageSize, SmallMessageSize, SmallMessageSize], cancellationToken)
            .ConfigureAwait(false);
    }

    [Test]
    [Timeout(120_000)]
    public async Task Producer_MaxMessageBytesRaisedMidRun_LargerRecordsAccepted(
        CancellationToken cancellationToken)
    {
        var topic = await CreateTopicAsync(LowMaxMessageBytes, cancellationToken).ConfigureAwait(false);
        await using var producer = await CreateByteProducerAsync(cancellationToken).ConfigureAwait(false);
        await using var admin = kafka.CreateAdminClient();
        var largeMessage = new byte[LargeMessageSize];

        var exception = await Assert.That(async () =>
                await producer.ProduceAsync(topic, null, largeMessage, cancellationToken).ConfigureAwait(false))
            .Throws<ProduceException>();
        await SetTopicConfigAsync(
            admin,
            topic,
            "max.message.bytes",
            HighMaxMessageBytes.ToString(),
            cancellationToken).ConfigureAwait(false);

        // DescribeConfigs reflects the raised limit before the partition leader applies it to
        // produce validation, so the first large produce can still be rejected. Rejected records
        // are never appended, which keeps the accepted record's expected offset at 0.
        var accepted = await ProduceUntilAcceptedAsync(producer, topic, largeMessage, cancellationToken)
            .ConfigureAwait(false);

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.MessageTooLarge);
        await Assert.That(accepted.Offset).IsEqualTo(0);
        await AssertRecordSizesAsync(topic, [LargeMessageSize], cancellationToken).ConfigureAwait(false);
    }

    [Test]
    [Timeout(120_000)]
    public async Task Topic_MinInsyncRaisedMidRun_AcksAllReactsCorrectly(
        CancellationToken cancellationToken)
    {
        const int stoppedFollowerId = 2;
        var topic = await kafka.CreateReplicatedTopicAsync(minInSyncReplicas: 1).ConfigureAwait(false);
        using var logs = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Debug)
            .AddProvider(logs));
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId($"dynamic-min-isr-{Guid.NewGuid():N}")
            .WithAcks(Acks.All)
            .WithIdempotence(true)
            .WithRequestTimeout(TimeSpan.FromSeconds(1))
            .WithDeliveryTimeout(TimeSpan.FromSeconds(4))
            .WithReconnectBackoff(TimeSpan.FromMilliseconds(50))
            .WithReconnectBackoffMax(TimeSpan.FromMilliseconds(250))
            .WithLoggerFactory(loggerFactory)
            .BuildAsync(cancellationToken).ConfigureAwait(false);
        await using var admin = kafka.CreateAdminClient();

        var followerStopped = false;
        try
        {
            var initial = await producer.ProduceAsync(topic, "0", "0", cancellationToken).ConfigureAwait(false);
            await kafka.StopBrokerAsync(stoppedFollowerId, cancellationToken).ConfigureAwait(false);
            followerStopped = true;
            await kafka.WaitForInSyncReplicasAsync(topic, 2, cancellationToken).ConfigureAwait(false);
            var beforeRaise = await producer.ProduceAsync(topic, "1", "1", cancellationToken)
                .ConfigureAwait(false);

            await SetTopicConfigAsync(
                admin,
                topic,
                "min.insync.replicas",
                "3",
                cancellationToken).ConfigureAwait(false);
            var failure = await WaitForMinIsrFailureAsync(
                producer,
                topic,
                beforeRaise.Offset,
                cancellationToken).ConfigureAwait(false);
            var replicaError = GetReplicaError(logs);

            await SetTopicConfigAsync(
                admin,
                topic,
                "min.insync.replicas",
                "2",
                cancellationToken).ConfigureAwait(false);
            var recovered = await WaitForProduceSuccessAsync(
                producer,
                topic,
                failure.NextValue,
                cancellationToken).ConfigureAwait(false);

            await Assert.That(initial.Offset).IsEqualTo(0);
            await Assert.That(beforeRaise.Offset).IsEqualTo(1);
            await Assert.That(failure.Exception.TimeoutKind).IsEqualTo(TimeoutKind.Delivery);
            await Assert.That(replicaError).IsEqualTo(ErrorCode.NotEnoughReplicas);
            await Assert.That(replicaError!.Value.IsRetriable()).IsTrue();
            await Assert.That(recovered.Offset).IsEqualTo(failure.LastSuccessfulOffset + 1);
        }
        finally
        {
            if (followerStopped)
            {
                await kafka.StartBrokersAsync([stoppedFollowerId], CancellationToken.None).ConfigureAwait(false);
                await kafka.WaitForInSyncReplicasAsync(topic, 3, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private async Task<string> CreateTopicAsync(int maxMessageBytes, CancellationToken cancellationToken)
    {
        var topic = $"dynamic-config-{Guid.NewGuid():N}";
        await using var admin = kafka.CreateAdminClient();
        await admin.CreateTopicsAsync([
            new NewTopic
            {
                Name = topic,
                NumPartitions = 1,
                ReplicationFactor = 1,
                Configs = new Dictionary<string, string>
                {
                    ["max.message.bytes"] = maxMessageBytes.ToString(),
                    ["min.insync.replicas"] = "1"
                }
            }
        ], cancellationToken: cancellationToken).ConfigureAwait(false);
        return topic;
    }

    private async Task<IKafkaProducer<byte[], byte[]>> CreateByteProducerAsync(
        CancellationToken cancellationToken) =>
        await Kafka.CreateProducer<byte[], byte[]>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId($"dynamic-max-message-{Guid.NewGuid():N}")
            .WithAcks(Acks.All)
            .WithBatchSize(HighMaxMessageBytes)
            .WithMaxRequestSize(HighMaxMessageBytes * 2)
            .WithLinger(TimeSpan.FromMilliseconds(100))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken).ConfigureAwait(false);

    private static async Task<RecordMetadata> ProduceUntilAcceptedAsync(
        IKafkaProducer<byte[], byte[]> producer,
        string topic,
        byte[] message,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                return await producer.ProduceAsync(topic, null, message, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (ProduceException exception)
                when (exception.ErrorCode == ErrorCode.MessageTooLarge
                    && stopwatch.Elapsed < TimeSpan.FromSeconds(15))
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task SetTopicConfigAsync(
        IAdminClient admin,
        string topic,
        string name,
        string value,
        CancellationToken cancellationToken)
    {
        var resource = ConfigResource.Topic(topic);
        await admin.IncrementalAlterConfigsAsync(
            new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
            {
                [resource] = [ConfigAlter.Set(name, value)]
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await WaitForTopicConfigAsync(
            admin,
            resource,
            name,
            value,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task WaitForTopicConfigAsync(
        IAdminClient admin,
        ConfigResource resource,
        string name,
        string expected,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        string? actual = null;
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(15))
        {
            actual = await GetTopicConfigAsync(admin, resource, name, cancellationToken).ConfigureAwait(false);
            if (actual == expected)
                return;
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"Topic config '{name}' was '{actual}', expected '{expected}'.");
    }

    private static Task<string?> GetTopicConfigAsync(
        IAdminClient admin,
        string topic,
        string name,
        CancellationToken cancellationToken) =>
        GetTopicConfigAsync(admin, ConfigResource.Topic(topic), name, cancellationToken);

    private static async Task<string?> GetTopicConfigAsync(
        IAdminClient admin,
        ConfigResource resource,
        string name,
        CancellationToken cancellationToken)
    {
        var configs = await admin.DescribeConfigsAsync([resource], cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return configs[resource].First(entry => entry.Name == name).Value;
    }

    private async Task AssertRecordSizesAsync(
        string topic,
        IReadOnlyList<int> expectedSizes,
        CancellationToken cancellationToken)
    {
        await using var consumer = await Kafka.CreateConsumer<byte[], byte[]>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithGroupId($"dynamic-config-oracle-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken).ConfigureAwait(false);
        consumer.Assign(new TopicPartition(topic, 0));

        for (var index = 0; index < expectedSizes.Count; index++)
        {
            var record = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(15), cancellationToken)
                .ConfigureAwait(false);
            if (record is null)
                throw new TimeoutException($"Consumed {index} of {expectedSizes.Count} expected records.");

            await Assert.That(record.Value.Offset).IsEqualTo(index);
            await Assert.That(record.Value.Value.Length).IsEqualTo(expectedSizes[index]);
        }

        var unexpected = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), cancellationToken)
            .ConfigureAwait(false);
        await Assert.That(unexpected).IsNull();
    }

    private static async Task<MinIsrFailure> WaitForMinIsrFailureAsync(
        IKafkaProducer<string, string> producer,
        string topic,
        long initialOffset,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var value = checked((int)(initialOffset + 1));
        var lastSuccessfulOffset = initialOffset;
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(15))
        {
            var payload = value.ToString();
            try
            {
                var metadata = await producer.ProduceAsync(topic, payload, payload, cancellationToken)
                    .ConfigureAwait(false);
                lastSuccessfulOffset = metadata.Offset;
                value++;
            }
            catch (KafkaTimeoutException exception)
            {
                return new MinIsrFailure(exception, lastSuccessfulOffset, value);
            }
        }

        throw new TimeoutException("Broker did not enforce the raised min.insync.replicas within 15 seconds.");
    }

    private static async Task<RecordMetadata> WaitForProduceSuccessAsync(
        IKafkaProducer<string, string> producer,
        string topic,
        int value,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var payload = value.ToString();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(15))
        {
            try
            {
                return await producer.ProduceAsync(topic, payload, payload, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (KafkaTimeoutException)
            {
                // DescribeConfigs can lead the partition leader's runtime view briefly.
            }
        }

        throw new TimeoutException("Producer did not recover after lowering min.insync.replicas within 15 seconds.");
    }

    private static ErrorCode? GetReplicaError(CapturingLoggerProvider logs)
    {
        foreach (var entry in logs.Entries)
        {
            if (entry.TryGetProperty<ErrorCode>("ErrorCode", out var errorCode)
                && errorCode is ErrorCode.NotEnoughReplicas or ErrorCode.NotEnoughReplicasAfterAppend)
            {
                return errorCode;
            }
        }

        return null;
    }

    private sealed record MinIsrFailure(
        KafkaTimeoutException Exception,
        long LastSuccessfulOffset,
        int NextValue);
}
