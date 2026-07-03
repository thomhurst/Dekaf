using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;
using ConfluentKafka = Confluent.Kafka;

namespace Dekaf.StressTests.Scenarios;

internal sealed class ConfluentConsumerStressTest : IStressTestScenario
{
    public string Name => "consumer";
    public string Client => "Confluent";

    public async Task<StressTestResult> RunAsync(StressTestOptions options, CancellationToken cancellationToken)
    {
        var throughput = new ThroughputTracker();
        var startedAt = DateTime.UtcNow;

        var consumerConfig = new ConfluentKafka.ConsumerConfig
        {
            BootstrapServers = options.BootstrapServers,
            ClientId = "stress-consumer-confluent",
            GroupId = $"stress-group-confluent-{Guid.NewGuid():N}",
            AutoOffsetReset = ConfluentKafka.AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        // The topic is pre-seeded by Program.SeedConsumerTopicAsync. The consumer re-reads
        // that fixed data set in a loop (seek to beginning when all partitions are drained).
        // A live feeder would compete with the consumer for CPU and cap throughput at the
        // feeder's rate, measuring the feeder instead of the consumer.
        using var consumer = new ConfluentKafka.ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(options.Topic);

        var partitions = Enumerable.Range(0, options.Partitions)
            .Select(p => new ConfluentKafka.TopicPartition(options.Topic, p))
            .ToArray();

        var endOffsets = ConfluentStressTestHelpers.QueryEndOffsets(consumer, options.Topic, options.Partitions, TimeSpan.FromSeconds(30));

        var replay = new PartitionReplayTracker(endOffsets);

        Console.WriteLine($"  Consuming pre-seeded topic in a loop ({endOffsets.Sum():N0} messages per pass)");

        // GC baseline before consumer measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var gcStats = new GcStats();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(options.DurationMinutes));

        Console.WriteLine($"  Running Confluent consumer stress test for {options.DurationMinutes} minutes...");
        Console.WriteLine($"  Start time: {DateTime.UtcNow:HH:mm:ss.fff} UTC");
        StressTestHelpers.LogResourceUsage("Initial");

        throughput.Start();
        var progress = new PeriodicProgressReporter(throughput);

        var samplerTask = StressTestHelpers.RunSamplerAsync(throughput, cts.Token);
        var resourceMonitorTask = StressTestHelpers.RunResourceMonitorAsync(cts.Token);

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(TimeSpan.FromMilliseconds(100));
                    if (result is null)
                    {
                        continue;
                    }

                    throughput.RecordMessage(result.Message.Value?.Length ?? 0);
                    progress.RecordMessage();

                    if (replay.RecordConsumed(result.Partition.Value, result.Offset.Value))
                    {
                        foreach (var tp in partitions)
                        {
                            consumer.Seek(new ConfluentKafka.TopicPartitionOffset(tp, ConfluentKafka.Offset.Beginning));
                        }
                    }
                }
                catch (ConfluentKafka.ConsumeException)
                {
                    throughput.RecordError();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        throughput.Stop();
        gcStats.Capture();

        try { await samplerTask.ConfigureAwait(false); } catch { }
        try { await resourceMonitorTask.ConfigureAwait(false); } catch { }

        var completedAt = DateTime.UtcNow;
        Console.WriteLine($"  Completed: {throughput.MessageCount:N0} messages, {throughput.GetAverageMessagesPerSecond():N0} msg/sec");
        StressTestHelpers.LogResourceUsage("Final");

        return new StressTestResult
        {
            Scenario = Name,
            Client = Client,
            DurationMinutes = options.DurationMinutes,
            BrokerCount = options.BrokerCount,
            MessageSizeBytes = options.MessageSizeBytes,
            StartedAtUtc = startedAt,
            CompletedAtUtc = completedAt,
            Throughput = throughput.GetSnapshot(),
            Latency = null,
            GcStats = gcStats.ToSnapshot(),
            CpuTimeSeconds = throughput.CpuTimeSeconds
        };
    }
}
