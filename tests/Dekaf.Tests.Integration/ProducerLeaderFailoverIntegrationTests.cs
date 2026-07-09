using Dekaf.Consumer;
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
}
