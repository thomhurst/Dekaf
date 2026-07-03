using Dekaf.Compression.Lz4;
using Dekaf.Compression.Snappy;
using Dekaf.Compression.Zstd;
using Dekaf.Producer;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;

namespace Dekaf.StressTests.Scenarios;

internal sealed class ProducerAcksAllStressTest : IStressTestScenario
{
    public string Name => "producer-acks-all";
    public string Client => "Dekaf";

    public async Task<StressTestResult> RunAsync(StressTestOptions options, CancellationToken cancellationToken)
    {
        var messageValue = new string('x', options.MessageSizeBytes);
        var throughput = new ThroughputTracker();
        var latency = StressTestHelpers.CreateDeliveryLatencyTracker();
        var startedAt = DateTime.UtcNow;

        var builder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(options.BootstrapServers)
            .WithClientId("stress-producer-acks-all-dekaf")
            .WithIdempotence(false)
            // Must match ConfluentProducerAcksAllStressTest for an apples-to-apples comparison
            .WithAcks(Acks.All)
            .WithLinger(TimeSpan.FromMilliseconds(options.LingerMs))
            .WithBatchSize(options.BatchSize)
            .WithBufferMemory(StressTestHelpers.ProducerBufferMemoryBytes)
            .WithConnectionsPerBroker(options.ConnectionsPerBroker);

        _ = options.Compression switch
        {
            "lz4" => builder.UseLz4Compression(),
            "snappy" => builder.UseSnappyCompression(),
            "zstd" => builder.UseZstdCompression(),
            _ => builder
        };

        var producer = await builder.BuildAsync(cancellationToken);

        Console.WriteLine($"  Warming up Dekaf acks-all producer...");
        await producer.ProduceAsync(options.Topic, "warmup", "warmup", cancellationToken).ConfigureAwait(false);
        for (var i = 0; i < 999; i++)
        {
            await producer.FireAsync(options.Topic, "warmup", "warmup");
        }
        await producer.FlushAsync(CancellationToken.None).ConfigureAwait(false);

        var startOffset = await StressTestHelpers.QueryTotalEndOffsetAsync(options.BootstrapServers, options.Topic, options.Partitions);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var gcStats = new GcStats();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(options.DurationMinutes));

        Console.WriteLine($"  Running Dekaf acks-all producer stress test for {options.DurationMinutes} minutes...");
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
                if (messageIndex % StressTestHelpers.LatencySampleInterval == 0)
                {
                    StressTestHelpers.SampleDeliveryLatency(producer, options.Topic, StressTestHelpers.GetKey(messageIndex), messageValue, latency, throughput);
                }
                else
                {
                    await producer.FireAsync(options.Topic, StressTestHelpers.GetKey(messageIndex), messageValue);
                }
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

        Console.WriteLine($"  Flushing remaining messages...");
        try
        {
            await producer.FlushAsync(CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Console.WriteLine($"  Warning: Flush timed out after 30 seconds");
        }

        throughput.Stop();
        gcStats.Capture();

        try { await samplerTask.ConfigureAwait(false); } catch { }
        try { await resourceMonitorTask.ConfigureAwait(false); } catch { }

        var completedAt = DateTime.UtcNow;
        Console.WriteLine($"  Completed: {throughput.MessageCount:N0} messages, {throughput.GetAverageMessagesPerSecond():N0} msg/sec");
        StressTestHelpers.LogResourceUsage("Final");

        Console.WriteLine($"  Disposing producer...");
        try
        {
            await producer.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None).ConfigureAwait(false);
            Console.WriteLine($"  Producer disposed successfully");
        }
        catch (TimeoutException)
        {
            Console.WriteLine($"  Warning: Dispose timed out after 30 seconds");
        }

        // Queried after dispose so all delivery attempts (including the final flush)
        // have finished — the delta is what the broker actually accepted.
        var endOffset = await StressTestHelpers.QueryTotalEndOffsetAsync(options.BootstrapServers, options.Topic, options.Partitions);
        var delivered = StressTestHelpers.ComputeDelivered(startOffset, endOffset, throughput);

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
            DeliveredMessages = delivered,
            Latency = latency.GetSnapshot(),
            GcStats = gcStats.ToSnapshot(),
            CpuTimeSeconds = throughput.CpuTimeSeconds
        };
    }
}
