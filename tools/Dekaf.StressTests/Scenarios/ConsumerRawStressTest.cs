using Dekaf.Consumer;
using Dekaf.Producer;
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
        var throughput = new ThroughputTracker();
        var startedAt = DateTime.UtcNow;
        var messageValue = new string('x', options.MessageSizeBytes);

        // Create producer to feed messages to the consumer (uses string serialization for production)
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(options.BootstrapServers)
            .WithClientId("stress-consumer-raw-feeder-dekaf")
            .WithAcks(Acks.Leader)
            .WithLinger(TimeSpan.FromMilliseconds(options.LingerMs))
            .WithBatchSize(options.BatchSize)
            .BuildAsync(cancellationToken);

        // Pre-seed messages before starting consumer measurement.
        // Note: Program.cs also seeds via SeedConsumerTopicAsync when running --scenario all.
        // Both seeds are intentional — this ensures enough backlog regardless of invocation path.
        Console.WriteLine("  Pre-seeding messages for consumer-raw test...");
        const int preseedCount = 500_000;
        for (var i = 0; i < preseedCount; i++)
        {
            await producer.FireAsync(options.Topic, StressTestHelpers.GetKey(i), messageValue).ConfigureAwait(false);
        }
        await producer.FlushAsync(cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"  Pre-seeded {preseedCount:N0} messages");

        // Consumer uses Ignore for key (don't care) and ReadOnlyMemory<byte> for zero-copy value access
        await using var consumer = await Kafka.CreateConsumer<Ignore, ReadOnlyMemory<byte>>()
            .WithBootstrapServers(options.BootstrapServers)
            .WithClientId("stress-consumer-raw-dekaf")
            .WithGroupId($"stress-group-raw-dekaf-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .ForHighThroughput()
            .BuildAsync(cancellationToken);

        consumer.Subscribe(options.Topic);

        // Start background producer to continuously feed the consumer
        var producerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var producerTask = StressTestHelpers.RunBackgroundProducerAsync(producer, options.Topic, messageValue, producerCts.Token);

        // GC baseline after producer warmup but before consumer measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var gcStats = new GcStats();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(options.DurationMinutes));

        Console.WriteLine($"  Running Dekaf consumer-raw stress test for {options.DurationMinutes} minutes...");
        throughput.Start();

        var samplerTask = StressTestHelpers.RunSamplerAsync(throughput, cts.Token);

        while (!cts.IsCancellationRequested)
        {
            try
            {
                await foreach (var record in consumer.ConsumeAsync(cts.Token).ConfigureAwait(false))
                {
                    throughput.RecordMessage(record.Value.Length);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected — duration timer expired
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Consumer error: {ex.GetType().Name}: {ex.Message}");
                throughput.RecordError();

                // Brief delay to prevent tight error loops on persistent failures
                await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken.None).ConfigureAwait(false);
            }
        }

        throughput.Stop();
        gcStats.Capture();

        // Stop background producer
        await producerCts.CancelAsync().ConfigureAwait(false);
        try { await producerTask.ConfigureAwait(false); } catch (OperationCanceledException) { }

        try { await samplerTask.ConfigureAwait(false); } catch { }

        var completedAt = DateTime.UtcNow;
        Console.WriteLine($"  Completed: {throughput.MessageCount:N0} messages, {throughput.GetAverageMessagesPerSecond():N0} msg/sec");

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
            GcStats = gcStats.ToSnapshot()
        };
    }
}
