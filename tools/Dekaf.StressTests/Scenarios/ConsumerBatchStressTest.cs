using Dekaf.Consumer;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;

namespace Dekaf.StressTests.Scenarios;

/// <summary>
/// Consumer stress test that uses <see cref="IKafkaConsumer{TKey,TValue}.ConsumeBatchAsync"/>
/// to receive pre-deserialized batches instead of individual records.
/// This measures the throughput improvement from batch-oriented consumption.
/// </summary>
internal sealed class ConsumerBatchStressTest : IStressTestScenario
{
    public string Name => "consumer-batch";
    public string Client => "Dekaf";

    public async Task<StressTestResult> RunAsync(StressTestOptions options, CancellationToken cancellationToken)
    {
        // The topic is pre-seeded by Program.SeedConsumerTopicAsync. The consumer re-reads
        // that fixed data set in a loop (seek to beginning when all partitions are drained).
        // A live feeder would compete with the consumer for CPU and cap throughput at the
        // feeder's rate, measuring the feeder instead of the consumer.
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithLoggerFactory(StressClientLogging.LoggerFactory)
            .WithBootstrapServers(options.BootstrapServers)
            .WithClientId("stress-consumer-batch-dekaf")
            .WithGroupId($"stress-group-batch-dekaf-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            // No WithCachedStringValues(): keep deserialization work identical to the
            // paired "consumer" scenario so batch-vs-single comparisons measure the API
            // shape, not a 100%-hit string cache on the identical-value seed data.
            .ForHighThroughput()
            .BuildAsync(cancellationToken);

        consumer.Subscribe(options.Topic);

        var partitions = Enumerable.Range(0, options.Partitions)
            .Select(p => new TopicPartition(options.Topic, p))
            .ToArray();

        var endOffsets = await StressTestHelpers.QueryEndOffsetsAsync(consumer, options.Topic, options.Partitions, cancellationToken);
        var replay = new PartitionReplayTracker(endOffsets);

        Console.WriteLine($"  Consuming pre-seeded topic in a loop ({endOffsets.Sum():N0} messages per pass)");

        return await StressTestHelpers.RunConsumerAsync(options, this,
            async (throughput, token) =>
            {
                var progress = new PeriodicProgressReporter(throughput);
                await foreach (var batch in consumer.ConsumeBatchAsync(token).ConfigureAwait(false))
                {
                    var lastOffset = -1L;
                    foreach (var record in batch)
                    {
                        throughput.RecordMessage(record.Value?.Length ?? 0);
                        progress.RecordMessage();
                        lastOffset = record.Offset;
                    }

                    // Offsets are monotonic within a batch, so the last one decides drain state
                    if (lastOffset >= 0 && replay.RecordConsumed(batch.Partition, lastOffset))
                    {
                        consumer.Positions.SeekToBeginning(partitions);
                    }
                }
            }, connectionsPerBroker: StressTestOptions.HighThroughputConsumerConnectionsPerBroker,
            captureConsumerDiagnostics: () => StressTestHelpers.CaptureConsumerDiagnostics(consumer), cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
