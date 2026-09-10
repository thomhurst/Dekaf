using Dekaf.Consumer;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;

namespace Dekaf.StressTests.Scenarios;

internal sealed class ConsumerStressTest : IStressTestScenario
{
    public string Name => "consumer";
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
            .WithClientId("stress-consumer-dekaf")
            .WithGroupId($"stress-group-dekaf-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            // No WithCachedStringValues(): the seeded topic repeats one identical value, so
            // Dekaf's string cache would hit 100% and skip the per-message UTF-8 decode +
            // allocation Confluent always pays — inflating both the throughput ratio and
            // the Alloc/msg comparison. The head-to-head must deserialize like-for-like.
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
                await foreach (var record in consumer.ConsumeAsync(token).ConfigureAwait(false))
                {
                    throughput.RecordMessage(record.Value?.Length ?? 0);
                    progress.RecordMessage();

                    if (replay.RecordConsumed(record.Partition, record.Offset))
                    {
                        consumer.Positions.SeekToBeginning(partitions);
                    }
                }
            }, connectionsPerBroker: StressTestOptions.HighThroughputConsumerConnectionsPerBroker,
            captureConsumerDiagnostics: () => StressTestHelpers.CaptureConsumerDiagnostics(consumer), cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
