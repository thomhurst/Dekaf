using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;

namespace Dekaf.StressTests.Scenarios;

/// <summary>
/// Consumer stress test that uses <see cref="IKafkaConsumer{TKey,TValue}.ConsumeRawBatchAsync"/>
/// to receive raw (undeserialized) batches for maximum throughput.
/// This combines batch-oriented consumption with zero-copy raw byte access.
/// </summary>
internal sealed class ConsumerRawBatchStressTest : IStressTestScenario
{
    public string Name => "consumer-raw-batch";
    public string Client => "Dekaf";

    public async Task<StressTestResult> RunAsync(StressTestOptions options, CancellationToken cancellationToken)
    {
        var throughput = new ThroughputTracker();
        var startedAt = DateTime.UtcNow;
        var messageValue = new string('x', options.MessageSizeBytes);

        // Create producer to feed messages to the consumer (uses string serialization for production)
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(options.BootstrapServers)
            .WithClientId("stress-consumer-raw-batch-feeder-dekaf")
            .WithAcks(Acks.Leader)
            .WithLinger(TimeSpan.FromMilliseconds(options.LingerMs))
            .WithBatchSize(options.BatchSize)
            .BuildAsync(cancellationToken);

        // Pre-seed messages before starting consumer measurement.
        // Note: Program.cs also seeds via SeedConsumerTopicAsync when running --scenario all.
        // Both seeds are intentional — this ensures enough backlog regardless of invocation path.
        Console.WriteLine("  Pre-seeding messages for consumer-raw-batch test...");
        const int preseedCount = 500_000;
        for (var i = 0; i < preseedCount; i++)
        {
            await producer.FireAsync(options.Topic, StressTestHelpers.GetKey(i), messageValue).ConfigureAwait(false);
        }
        await producer.FlushAsync(cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"  Pre-seeded {preseedCount:N0} messages");

        // Consumer uses string types but ConsumeRawBatchAsync skips deserialization
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(options.BootstrapServers)
            .WithClientId("stress-consumer-raw-batch-dekaf")
            .WithGroupId($"stress-group-raw-batch-dekaf-{Guid.NewGuid():N}")
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

        Console.WriteLine($"  Running Dekaf consumer-raw-batch stress test for {options.DurationMinutes} minutes...");
        Console.WriteLine($"  Start time: {DateTime.UtcNow:HH:mm:ss.fff} UTC");
        StressTestHelpers.LogResourceUsage("Initial");

        throughput.Start();
        var progress = new PeriodicProgressReporter(throughput);

        var samplerTask = StressTestHelpers.RunSamplerAsync(throughput, cts.Token);
        var resourceMonitorTask = StressTestHelpers.RunResourceMonitorAsync(cts.Token);

        try
        {
            await foreach (var batch in consumer.ConsumeRawBatchAsync(cts.Token).ConfigureAwait(false))
            {
                foreach (var record in batch)
                {
                    throughput.RecordMessage(record.Value.Length);
                    progress.RecordMessage();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected — duration timer expired
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Consumer error: {ex.GetType().Name}: {ex.Message}");
            throughput.RecordError();
        }

        throughput.Stop();
        gcStats.Capture();

        // Stop background producer
        await producerCts.CancelAsync().ConfigureAwait(false);
        try { await producerTask.ConfigureAwait(false); } catch (OperationCanceledException) { }

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
            GcStats = gcStats.ToSnapshot()
        };
    }
}
