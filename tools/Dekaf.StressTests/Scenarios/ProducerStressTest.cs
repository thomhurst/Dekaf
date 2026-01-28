using System.Runtime.CompilerServices;
using Dekaf.Producer;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;
using DekafLib = Dekaf;

namespace Dekaf.StressTests.Scenarios;

internal sealed class ProducerStressTest : IStressTestScenario
{
    private static readonly string[] PreAllocatedKeys = CreatePreAllocatedKeys(10_000);

    public string Name => "producer";
    public string Client => "Dekaf";

    public async Task<StressTestResult> RunAsync(StressTestOptions options, CancellationToken cancellationToken)
    {
        var messageValue = new string('x', options.MessageSizeBytes);
        var throughput = new ThroughputTracker();
        var latency = new LatencyTracker();
        var startedAt = DateTime.UtcNow;

        await using var producer = DekafLib.Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(options.BootstrapServers)
            .WithClientId("stress-producer-dekaf")
            .WithAcks(Acks.Leader)
            .WithLingerMs(options.LingerMs)
            .WithBatchSize(options.BatchSize)
            .Build();

        Console.WriteLine($"  Warming up Dekaf producer...");
        for (var i = 0; i < 1000; i++)
        {
            producer.Send(options.Topic, "warmup", "warmup");
        }
        await producer.FlushAsync(CancellationToken.None).ConfigureAwait(false);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var gcStats = new GcStats();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(options.DurationMinutes));

        Console.WriteLine($"  Running Dekaf producer stress test for {options.DurationMinutes} minutes...");
        Console.WriteLine($"  Start time: {DateTime.UtcNow:HH:mm:ss.fff} UTC");
        throughput.Start();
        var messageIndex = 0L;
        var lastStatusTime = DateTime.UtcNow;

        var samplerTask = RunSamplerAsync(throughput, cts.Token);

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                var start = System.Diagnostics.Stopwatch.GetTimestamp();
                producer.Send(options.Topic, GetKey(messageIndex), messageValue);
                latency.RecordTicks(System.Diagnostics.Stopwatch.GetTimestamp() - start);
                throughput.RecordMessage(options.MessageSizeBytes);
                messageIndex++;

                // Yield and report status periodically
                if (messageIndex % 100_000 == 0)
                {
                    await Task.Yield();
                    var now = DateTime.UtcNow;
                    if ((now - lastStatusTime).TotalSeconds >= 10)
                    {
                        Console.WriteLine($"  [{now:HH:mm:ss}] Progress: {messageIndex:N0} messages, {throughput.GetAverageMessagesPerSecond():N0} msg/sec");
                        lastStatusTime = now;
                    }
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

        await producer.FlushAsync(CancellationToken.None).ConfigureAwait(false);
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
            Latency = latency.GetSnapshot(),
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

    private static string[] CreatePreAllocatedKeys(int count)
    {
        var keys = new string[count];
        for (var i = 0; i < count; i++)
        {
            keys[i] = $"key-{i}";
        }
        return keys;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetKey(long index) => PreAllocatedKeys[index % PreAllocatedKeys.Length];
}
