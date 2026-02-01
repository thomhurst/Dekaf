using Dekaf.Consumer;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;
using DekafLib = Dekaf;

namespace Dekaf.StressTests.Scenarios;

internal sealed class ConsumerStressTest : IStressTestScenario
{
    public string Name => "consumer";
    public string Client => "Dekaf";

    public async Task<StressTestResult> RunAsync(StressTestOptions options, CancellationToken cancellationToken)
    {
        var throughput = new ThroughputTracker();
        var startedAt = DateTime.UtcNow;

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(options.BootstrapServers)
            .WithClientId("stress-consumer-dekaf")
            .WithGroupId($"stress-group-dekaf-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(options.Topic);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var gcStats = new GcStats();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(options.DurationMinutes));

        Console.WriteLine($"  Running Dekaf consumer stress test for {options.DurationMinutes} minutes...");
        throughput.Start();

        var samplerTask = RunSamplerAsync(throughput, cts.Token);

        try
        {
            await foreach (var record in consumer.ConsumeAsync(cts.Token).ConfigureAwait(false))
            {
                throughput.RecordMessage(record.Value?.Length ?? 0);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch
        {
            throughput.RecordError();
        }

        throughput.Stop();
        gcStats.Capture();

        try { await samplerTask.ConfigureAwait(false); } catch { }

        var completedAt = DateTime.UtcNow;
        Console.WriteLine($"  Completed: {throughput.MessageCount:N0} messages, {throughput.GetAverageMessagesPerSecond():N0} msg/sec");

        return new StressTestResult
        {
            Scenario = Name,
            Client = Client,
            DurationMinutes = options.DurationMinutes,
            MessageSizeBytes = options.MessageSizeBytes,
            StartedAtUtc = startedAt,
            CompletedAtUtc = completedAt,
            Throughput = throughput.GetSnapshot(),
            Latency = null,
            GcStats = gcStats.ToSnapshot()
        };
    }

    private static async Task RunSamplerAsync(ThroughputTracker throughput, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                throughput.TakeSample();
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
