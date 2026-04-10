using System.Diagnostics;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;
using ConfluentKafka = Confluent.Kafka;

namespace Dekaf.StressTests.Scenarios;

internal sealed class ConfluentProducerAcksAllStressTest : IStressTestScenario
{
    public string Name => "producer-acks-all";
    public string Client => "Confluent";

    public async Task<StressTestResult> RunAsync(StressTestOptions options, CancellationToken cancellationToken)
    {
        var messageValue = new string('x', options.MessageSizeBytes);
        var throughput = new ThroughputTracker();
        var latency = new LatencyTracker();
        var startedAt = DateTime.UtcNow;

        var config = new ConfluentKafka.ProducerConfig
        {
            BootstrapServers = options.BootstrapServers,
            ClientId = "stress-producer-acks-all-confluent",
            // Must match ProducerAcksAllStressTest for an apples-to-apples comparison
            Acks = ConfluentKafka.Acks.All,
            LingerMs = options.LingerMs,
            BatchSize = options.BatchSize,
            CompressionType = options.Compression switch
            {
                "lz4" => ConfluentKafka.CompressionType.Lz4,
                "snappy" => ConfluentKafka.CompressionType.Snappy,
                "zstd" => ConfluentKafka.CompressionType.Zstd,
                _ => ConfluentKafka.CompressionType.None
            }
        };

        using var producer = new ConfluentKafka.ProducerBuilder<string, string>(config).Build();

        Console.WriteLine($"  Warming up Confluent acks-all producer...");
        for (var i = 0; i < 1000; i++)
        {
            producer.Produce(options.Topic, new ConfluentKafka.Message<string, string> { Key = "warmup", Value = "warmup" });
        }
        producer.Flush(TimeSpan.FromSeconds(30));

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var gcStats = new GcStats();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(options.DurationMinutes));

        Console.WriteLine($"  Running Confluent acks-all producer stress test for {options.DurationMinutes} minutes...");
        Console.WriteLine($"  Start time: {DateTime.UtcNow:HH:mm:ss.fff} UTC");
        StressTestHelpers.LogResourceUsage("Initial");

        throughput.Start();
        var messageIndex = 0L;
        var progress = new PeriodicProgressReporter(throughput);

        var samplerTask = StressTestHelpers.RunSamplerAsync(throughput, cts.Token);
        var resourceMonitorTask = StressTestHelpers.RunResourceMonitorAsync(cts.Token);

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                var start = Stopwatch.GetTimestamp();
                producer.Produce(options.Topic, new ConfluentKafka.Message<string, string>
                {
                    Key = StressTestHelpers.GetKey(messageIndex),
                    Value = messageValue
                });
                latency.RecordTicks(Stopwatch.GetTimestamp() - start);
                throughput.RecordMessage(options.MessageSizeBytes);
                messageIndex++;

                // Yield periodically to avoid starving other tasks
                if (messageIndex % 100_000 == 0)
                {
                    await Task.Yield();
                    progress.RecordMessage();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                throughput.RecordError();
            }
        }

        producer.Flush(TimeSpan.FromSeconds(30));
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
            Latency = latency.GetSnapshot(),
            GcStats = gcStats.ToSnapshot()
        };
    }
}
