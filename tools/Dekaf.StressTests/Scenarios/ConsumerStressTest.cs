using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;

namespace Dekaf.StressTests.Scenarios;

internal sealed class ConsumerStressTest : IStressTestScenario
{
    public string Name => "consumer";
    public string Client => "Dekaf";

    public async Task<StressTestResult> RunAsync(StressTestOptions options, CancellationToken cancellationToken)
    {
        var throughput = new ThroughputTracker();
        var startedAt = DateTime.UtcNow;
        var messageValue = new string('x', options.MessageSizeBytes);

        // Create producer to feed messages to the consumer
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(options.BootstrapServers)
            .WithClientId("stress-consumer-feeder-dekaf")
            .WithAcks(Acks.Leader)
            .WithLinger(TimeSpan.FromMilliseconds(options.LingerMs))
            .WithBatchSize(options.BatchSize)
            .BuildAsync(cancellationToken);

        // Pre-seed messages before starting consumer measurement.
        // Note: Program.cs also seeds via SeedConsumerTopicAsync when running --scenario all.
        // Both seeds are intentional — this ensures enough backlog regardless of invocation path.
        Console.WriteLine("  Pre-seeding messages for consumer test...");
        const int preseedCount = 500_000;
        for (var i = 0; i < preseedCount; i++)
        {
            await producer.FireAsync(options.Topic, StressTestHelpers.GetKey(i), messageValue).ConfigureAwait(false);
        }
        await producer.FlushAsync(cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"  Pre-seeded {preseedCount:N0} messages");

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(options.BootstrapServers)
            .WithClientId("stress-consumer-dekaf")
            .WithGroupId($"stress-group-dekaf-{Guid.NewGuid():N}")
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

        Console.WriteLine($"  Running Dekaf consumer stress test for {options.DurationMinutes} minutes...");
        throughput.Start();

        var samplerTask = StressTestHelpers.RunSamplerAsync(throughput, cts.Token);

        await StressTestHelpers.RunConsumeLoopAsync(
            consumer,
            static record => record.Value?.Length ?? 0,
            throughput,
            cts.Token).ConfigureAwait(false);

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
