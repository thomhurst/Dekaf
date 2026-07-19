using System.Diagnostics;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol;
using Microsoft.Extensions.Logging;

namespace Dekaf.Tests.Integration;

[Category("Producer")]
[Category("Resilience")]
[NotInParallel("RackAwareKafkaContainer")]
[ClassDataSource<RackAwareKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class ProducerMinInSyncReplicasIntegrationTests(RackAwareKafkaContainer kafka)
{
    private const int InitialMessageCount = 5;
    private const int RecoveryMessageCount = 20;

    [Test]
    [Timeout(180_000)]
    public async Task Producer_AcksAll_IsrBelowMinInsync_RetriesUntilDeliveryTimeout(
        CancellationToken cancellationToken)
    {
        var topic = await kafka.CreateReplicatedTopicAsync(minInSyncReplicas: 3).ConfigureAwait(false);
        var stoppedBrokers = new List<int>(1);
        using var logs = new CapturingLoggerProvider();
        using var loggerFactory = CreateLoggerFactory(logs);

        await using var producer = await CreateProducerAsync(
                Acks.All,
                enableIdempotence: true,
                deliveryTimeout: TimeSpan.FromSeconds(4),
                cancellationToken,
                loggerFactory)
            .ConfigureAwait(false);

        try
        {
            var initial = await ProduceAsync(producer, topic, 0, cancellationToken).ConfigureAwait(false);
            await StopFollowerAsync(topic, stoppedBrokers, cancellationToken).ConfigureAwait(false);

            ((IProducerDiagnostics)producer).ResetProduceRequestDiagnostics();
            var stopwatch = Stopwatch.StartNew();
            var exception = await Assert.ThrowsAsync<KafkaTimeoutException>(async () =>
                await ProduceAsync(producer, topic, 1, cancellationToken).ConfigureAwait(false));
            stopwatch.Stop();

            var diagnostics = ((IProducerDiagnostics)producer).GetDeliveryDiagnosticsSnapshot();
            var replicaError = GetReplicaError(logs);
            await Assert.That(initial.Offset).IsEqualTo(0);
            await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Delivery);
            await Assert.That(exception.Configured).IsEqualTo(TimeSpan.FromSeconds(4));
            await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(20));
            await Assert.That(diagnostics.ProduceRequestCount).IsGreaterThan(1);
            await Assert.That(replicaError).IsEqualTo(ErrorCode.NotEnoughReplicas);
            await Assert.That(replicaError!.Value.IsRetriable()).IsTrue();
        }
        finally
        {
            await RestoreBrokersAsync(topic, stoppedBrokers).ConfigureAwait(false);
        }
    }

    [Test]
    [Timeout(180_000)]
    public async Task Producer_AcksAll_IsrRecovers_RetriesSucceedWithoutLossOrDuplicates(
        CancellationToken cancellationToken)
    {
        var topic = await kafka.CreateReplicatedTopicAsync(minInSyncReplicas: 3).ConfigureAwait(false);
        var stoppedBrokers = new List<int>(1);

        await using var producer = await CreateProducerAsync(
                Acks.All,
                enableIdempotence: true,
                deliveryTimeout: TimeSpan.FromSeconds(45),
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var acknowledgements = new List<RecordMetadata>(InitialMessageCount + RecoveryMessageCount);
            for (var value = 0; value < InitialMessageCount; value++)
            {
                acknowledgements.Add(
                    await ProduceAsync(producer, topic, value, cancellationToken).ConfigureAwait(false));
            }

            await StopFollowerAsync(topic, stoppedBrokers, cancellationToken).ConfigureAwait(false);
            var diagnostics = (IProducerDiagnostics)producer;
            diagnostics.ResetProduceRequestDiagnostics();

            var pending = new Task<RecordMetadata>[RecoveryMessageCount];
            for (var index = 0; index < pending.Length; index++)
            {
                pending[index] = ProduceAsync(
                        producer,
                        topic,
                        InitialMessageCount + index,
                        cancellationToken)
                    .AsTask();
            }

            await WaitUntilAsync(
                    () => diagnostics.GetDeliveryDiagnosticsSnapshot().ProduceRequestCount > 0,
                    TimeSpan.FromSeconds(15),
                    cancellationToken)
                .ConfigureAwait(false);
            await Assert.That(pending.All(static task => !task.IsCompletedSuccessfully)).IsTrue();

            await RestoreBrokersAsync(topic, stoppedBrokers).ConfigureAwait(false);
            acknowledgements.AddRange(await Task.WhenAll(pending).ConfigureAwait(false));
            await producer.FlushAsync(cancellationToken).ConfigureAwait(false);

            AssertAcknowledgementSequence(acknowledgements);
            await AssertBrokerSequenceAsync(topic, acknowledgements.Count, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await RestoreBrokersAsync(topic, stoppedBrokers).ConfigureAwait(false);
        }
    }

    [Test]
    [Timeout(180_000)]
    public async Task Producer_AcksLeader_UnaffectedByIsrShrink(CancellationToken cancellationToken)
    {
        var topic = await kafka.CreateReplicatedTopicAsync(minInSyncReplicas: 3).ConfigureAwait(false);
        var stoppedBrokers = new List<int>(1);

        await using var producer = await CreateProducerAsync(
                Acks.Leader,
                enableIdempotence: false,
                deliveryTimeout: TimeSpan.FromSeconds(15),
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var initial = await ProduceAsync(producer, topic, 0, cancellationToken).ConfigureAwait(false);
            await StopFollowerAsync(topic, stoppedBrokers, cancellationToken).ConfigureAwait(false);
            var duringOutage = await ProduceAsync(producer, topic, 1, cancellationToken).ConfigureAwait(false);

            await Assert.That(initial.Offset).IsEqualTo(0);
            // The producer is non-idempotent with a 2s request timeout: a slow leader during
            // the ISR shrink can time out the first attempt after the broker already appended
            // it, and the retry then lands at a later offset. Delivery (not an exact offset)
            // is the acks=leader guarantee under test.
            await Assert.That(duringOutage.Offset).IsGreaterThanOrEqualTo(1);
        }
        finally
        {
            await RestoreBrokersAsync(topic, stoppedBrokers).ConfigureAwait(false);
        }
    }

    private async ValueTask<IKafkaProducer<string, string>> CreateProducerAsync(
        Acks acks,
        bool enableIdempotence,
        TimeSpan deliveryTimeout,
        CancellationToken cancellationToken,
        ILoggerFactory? loggerFactory = null)
    {
        return await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId($"min-isr-producer-{Guid.NewGuid():N}")
            .WithAcks(acks)
            .WithIdempotence(enableIdempotence)
            .WithRequestTimeout(TimeSpan.FromSeconds(2))
            .WithDeliveryTimeout(deliveryTimeout)
            .WithReconnectBackoff(TimeSpan.FromMilliseconds(50))
            .WithReconnectBackoffMax(TimeSpan.FromMilliseconds(250))
            .WithDeliveryDiagnostics()
            .WithLoggerFactory(loggerFactory ?? GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static ValueTask<RecordMetadata> ProduceAsync(
        IKafkaProducer<string, string> producer,
        string topic,
        int value,
        CancellationToken cancellationToken)
    {
        var payload = value.ToString();
        return producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Partition = 0,
            Key = payload,
            Value = payload
        }, cancellationToken);
    }

    private async Task StopFollowerAsync(
        string topic,
        List<int> stoppedBrokers,
        CancellationToken cancellationToken)
    {
        const int followerId = 2;
        await kafka.StopBrokerAsync(followerId, cancellationToken).ConfigureAwait(false);
        stoppedBrokers.Add(followerId);
        await kafka.WaitForInSyncReplicasAsync(topic, 2, cancellationToken).ConfigureAwait(false);
    }

    private async Task RestoreBrokersAsync(string topic, List<int> stoppedBrokers)
    {
        if (stoppedBrokers.Count == 0)
            return;

        var brokersToStart = stoppedBrokers.ToArray();
        await kafka.StartBrokersAsync(brokersToStart, CancellationToken.None).ConfigureAwait(false);
        stoppedBrokers.Clear();
        await kafka.WaitForInSyncReplicasAsync(topic, 3, CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task WaitUntilAsync(
        Func<bool> condition,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = Stopwatch.GetTimestamp() + (long)(timeout.TotalSeconds * Stopwatch.Frequency);
        while (!condition())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Stopwatch.GetTimestamp() >= deadline)
                throw new TimeoutException("Producer did not issue a request while ISR was below min.insync.replicas.");

            await Task.Delay(25, cancellationToken).ConfigureAwait(false);
        }
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

    private static ILoggerFactory CreateLoggerFactory(CapturingLoggerProvider logs) =>
        LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Debug)
            .AddProvider(logs));

    private static void AssertAcknowledgementSequence(IReadOnlyList<RecordMetadata> acknowledgements)
    {
        for (var index = 0; index < acknowledgements.Count; index++)
        {
            if (acknowledgements[index].Offset != index)
            {
                throw new InvalidOperationException(
                    $"Acknowledgement offset mismatch at index {index}: {acknowledgements[index].Offset}.");
            }
        }
    }

    private async Task AssertBrokerSequenceAsync(
        string topic,
        int expectedCount,
        CancellationToken cancellationToken)
    {
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId($"min-isr-oracle-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync(cancellationToken)
            .ConfigureAwait(false);
        consumer.Assign(new TopicPartition(topic, 0));

        for (var index = 0; index < expectedCount; index++)
        {
            var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), cancellationToken)
                .ConfigureAwait(false);
            if (result is null || result.Value.Offset != index || result.Value.Value != index.ToString())
            {
                throw new InvalidOperationException(
                    $"Record mismatch at index {index}: {result?.Offset}/{result?.Value}.");
            }
        }

        var extra = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(2), cancellationToken)
            .ConfigureAwait(false);
        if (extra is not null)
            throw new InvalidOperationException($"Unexpected duplicate at offset {extra.Value.Offset}.");
    }
}
