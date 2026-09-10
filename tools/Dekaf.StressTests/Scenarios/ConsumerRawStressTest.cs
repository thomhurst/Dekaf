using Dekaf.Consumer;
using Dekaf.Serialization;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;

namespace Dekaf.StressTests.Scenarios;

/// <summary>
/// Consumer stress test that reads raw bytes instead of deserializing strings.
/// Uses <see cref="ReadOnlyMemory{T}"/> values which avoid string allocation (zero-copy for
/// single-segment data, array copy for rare multi-segment cases). This isolates the consumer
/// infrastructure overhead from string deserialization allocations.
/// </summary>
internal sealed class ConsumerRawStressTest : IStressTestScenario
{
    public string Name => "consumer-raw";
    public string Client => "Dekaf";

    public async Task<StressTestResult> RunAsync(StressTestOptions options, CancellationToken cancellationToken)
    {
        // The topic is pre-seeded by Program.SeedConsumerTopicAsync. The consumer re-reads
        // that fixed data set in a loop (seek to beginning when all partitions are drained).
        // A live feeder would compete with the consumer for CPU and cap throughput at the
        // feeder's rate, measuring the feeder instead of the consumer.
        // Consumer uses Ignore for key (don't care) and ReadOnlyMemory<byte> for zero-copy value access
        await using var consumer = await Kafka.CreateConsumer<Ignore, ReadOnlyMemory<byte>>()
            .WithLoggerFactory(StressClientLogging.LoggerFactory)
            .WithBootstrapServers(options.BootstrapServers)
            .WithClientId("stress-consumer-raw-dekaf")
            .WithGroupId($"stress-group-raw-dekaf-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
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
                    throughput.RecordMessage(record.Value.Length);
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
