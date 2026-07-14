using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

[Category("Producer")]
[Category("Resilience")]
[NotInParallel("RackAwareKafkaContainer")]
[ClassDataSource<RackAwareKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class ProducerLeaderFailoverIntegrationTests(RackAwareKafkaContainer kafka)
{
    private const int InitialMessageCount = 25;
    private const int FailoverMessageCount = 75;
    private const int MessageCount = 200;

    [Test]
    [Timeout(180_000)]
    public async Task Producer_PartitionLeaderHandoff_ReroutesRetriesWithoutDuplicates(
        CancellationToken cancellationToken)
    {
        var topic = await kafka.CreateReplicatedTopicAsync().ConfigureAwait(false);
        int? stoppedBrokerId = null;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId($"leader-failover-producer-{Guid.NewGuid():N}")
            .WithAcks(Acks.All)
            .WithIdempotence(true)
            .WithRequestTimeout(TimeSpan.FromSeconds(5))
            .WithDeliveryTimeout(TimeSpan.FromSeconds(45))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var acknowledgements = new List<RecordMetadata>(MessageCount);
            await ProduceRangeAsync(
                    producer,
                    topic,
                    start: 0,
                    InitialMessageCount,
                    acknowledgements,
                    cancellationToken)
                .ConfigureAwait(false);

            stoppedBrokerId = await kafka.GetPartitionLeaderIdAsync(topic, cancellationToken)
                .ConfigureAwait(false);
            await kafka.StopBrokerAsync(stoppedBrokerId.Value, cancellationToken).ConfigureAwait(false);
            _ = await kafka.WaitForPartitionLeaderChangeAsync(topic, stoppedBrokerId.Value, cancellationToken)
                .ConfigureAwait(false);

            await ProduceRangeAsync(
                    producer,
                    topic,
                    InitialMessageCount,
                    FailoverMessageCount,
                    acknowledgements,
                    cancellationToken)
                .ConfigureAwait(false);

            await kafka.StartBrokerAsync(stoppedBrokerId.Value, cancellationToken).ConfigureAwait(false);
            await kafka.WaitForInSyncReplicasAsync(topic, 3, cancellationToken).ConfigureAwait(false);
            stoppedBrokerId = null;

            await ProduceRangeAsync(
                    producer,
                    topic,
                    InitialMessageCount + FailoverMessageCount,
                    MessageCount - InitialMessageCount - FailoverMessageCount,
                    acknowledgements,
                    cancellationToken)
                .ConfigureAwait(false);
            await producer.FlushAsync(cancellationToken).ConfigureAwait(false);

            AssertAcknowledgementOrder(acknowledgements);
            await AssertBrokerRecordsAsync(topic, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (stoppedBrokerId is { } brokerId)
                await kafka.StartBrokerAsync(brokerId, CancellationToken.None).ConfigureAwait(false);
        }
    }

    [Test]
    [Timeout(240_000)]
    public async Task Producer_NonIdempotent_BrokerRestart_DoesNotLoseAcknowledgedRecords(
        CancellationToken cancellationToken)
    {
        var topic = await kafka.CreateReplicatedTopicAsync().ConfigureAwait(false);
        var leaderBrokerId = await kafka.GetPartitionLeaderIdAsync(topic, cancellationToken)
            .ConfigureAwait(false);
        int? stoppedBrokerId = null;
        var productionPaused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumeProduction = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var acknowledgedValues = new List<string>(MessageCount);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId($"non-idempotent-restart-producer-{Guid.NewGuid():N}")
            .WithAcks(Acks.All)
            .WithIdempotence(false)
            .WithRequestTimeout(TimeSpan.FromSeconds(5))
            .WithDeliveryTimeout(TimeSpan.FromSeconds(45))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var production = ProduceAcrossRestartAsync(
                producer,
                topic,
                acknowledgedValues,
                productionPaused,
                resumeProduction.Task,
                cancellationToken);
            await productionPaused.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

            stoppedBrokerId = leaderBrokerId;
            await kafka.StopBrokerAsync(leaderBrokerId, cancellationToken).ConfigureAwait(false);
            resumeProduction.TrySetResult();
            _ = await kafka.WaitForPartitionLeaderChangeAsync(topic, leaderBrokerId, cancellationToken)
                .ConfigureAwait(false);

            await production.ConfigureAwait(false);
            await producer.FlushAsync(cancellationToken).ConfigureAwait(false);

            await Assert.That(acknowledgedValues).Count().IsGreaterThanOrEqualTo(InitialMessageCount);
            await Assert.That(acknowledgedValues.Select(int.Parse)).Contains(value => value >= InitialMessageCount);
            await AssertAcknowledgedBrokerRecordsAsync(topic, acknowledgedValues, cancellationToken)
                .ConfigureAwait(false);

            await kafka.StartBrokerAsync(leaderBrokerId, cancellationToken).ConfigureAwait(false);
            await kafka.WaitForInSyncReplicasAsync(topic, 3, cancellationToken).ConfigureAwait(false);
            stoppedBrokerId = null;
        }
        finally
        {
            resumeProduction.TrySetResult();
            if (stoppedBrokerId is { } brokerId)
                await kafka.StartBrokerAsync(brokerId, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static async Task ProduceRangeAsync(
        IKafkaProducer<string, string> producer,
        string topic,
        int start,
        int count,
        List<RecordMetadata> acknowledgements,
        CancellationToken cancellationToken)
    {
        for (var value = start; value < start + count; value++)
        {
            var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Partition = 0,
                Key = value.ToString(),
                Value = value.ToString()
            }, cancellationToken).ConfigureAwait(false);
            acknowledgements.Add(metadata);
        }
    }

    private static async Task ProduceAcrossRestartAsync(
        IKafkaProducer<string, string> producer,
        string topic,
        List<string> acknowledgedValues,
        TaskCompletionSource productionPaused,
        Task resumeProduction,
        CancellationToken cancellationToken)
    {
        for (var value = 0; value < MessageCount; value++)
        {
            if (value == InitialMessageCount)
            {
                productionPaused.TrySetResult();
                await resumeProduction.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            var payload = value.ToString();
            try
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Partition = 0,
                    Key = payload,
                    Value = payload
                }, cancellationToken).ConfigureAwait(false);
                acknowledgedValues.Add(payload);
            }
            catch (ProduceException) when (!cancellationToken.IsCancellationRequested)
            {
                // Delivery failures are allowed; losing a successfully acknowledged record is not.
            }
        }
    }

    private static void AssertAcknowledgementOrder(IReadOnlyList<RecordMetadata> acknowledgements)
    {
        if (acknowledgements.Count != MessageCount)
            throw new InvalidOperationException(
                $"Expected {MessageCount} acknowledgements, actual {acknowledgements.Count}.");

        for (var index = 0; index < acknowledgements.Count; index++)
        {
            var acknowledgement = acknowledgements[index];
            if (acknowledgement.Offset != index)
            {
                throw new InvalidOperationException(
                    $"Acknowledgement offset mismatch at index {index}: expected {index}, " +
                    $"actual {acknowledgement.Offset}.");
            }
        }
    }

    private async Task AssertBrokerRecordsAsync(string topic, CancellationToken cancellationToken)
    {
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId($"leader-failover-oracle-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync(cancellationToken)
            .ConfigureAwait(false);
        consumer.Assign(new TopicPartition(topic, 0));

        for (var index = 0; index < MessageCount; index++)
        {
            var consumed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), cancellationToken)
                .ConfigureAwait(false);
            if (consumed is null)
                throw new InvalidOperationException($"Timed out after consuming {index} of {MessageCount} records.");

            var result = consumed.Value;
            var expected = index.ToString();
            if (result.Offset != index || result.Key != expected || result.Value != expected)
            {
                throw new InvalidOperationException(
                    $"Record mismatch at index {index}: offset={result.Offset}, key={result.Key}, value={result.Value}.");
            }
        }

        var extra = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(2), cancellationToken)
            .ConfigureAwait(false);
        if (extra is not null)
        {
            throw new InvalidOperationException(
                $"Unexpected duplicate record at offset {extra.Value.Offset}: {extra.Value.Value}.");
        }
    }

    private async Task AssertAcknowledgedBrokerRecordsAsync(
        string topic,
        IReadOnlyCollection<string> acknowledgedValues,
        CancellationToken cancellationToken)
    {
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId($"non-idempotent-restart-oracle-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync(cancellationToken)
            .ConfigureAwait(false);
        consumer.Assign(new TopicPartition(topic, 0));

        var missing = acknowledgedValues.ToHashSet(StringComparer.Ordinal);
        while (missing.Count > 0)
        {
            var consumed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), cancellationToken)
                .ConfigureAwait(false);
            if (consumed is null)
            {
                throw new InvalidOperationException(
                    $"Timed out with {missing.Count} acknowledged record(s) absent: " +
                    string.Join(", ", missing.Order(StringComparer.Ordinal).Take(10)));
            }

            missing.Remove(consumed.Value.Value);
        }
    }
}
