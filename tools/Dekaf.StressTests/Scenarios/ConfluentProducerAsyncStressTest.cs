using System.Diagnostics;
using System.Runtime.CompilerServices;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;
using ConfluentKafka = Confluent.Kafka;

namespace Dekaf.StressTests.Scenarios;

internal sealed class ConfluentProducerAsyncStressTest : IStressTestScenario
{
    private static readonly string[] PreAllocatedKeys = CreatePreAllocatedKeys(10_000);

    public string Name => "producer-async";
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
            ClientId = "stress-producer-async-confluent",
            Acks = ConfluentKafka.Acks.Leader,
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

        Console.WriteLine($"  Warming up Confluent async producer...");
        for (var i = 0; i < 1000; i++)
        {
            await producer.ProduceAsync(options.Topic, new ConfluentKafka.Message<string, string> { Key = "warmup", Value = "warmup" }, cancellationToken).ConfigureAwait(false);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var gcStats = new GcStats();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(options.DurationMinutes));

        Console.WriteLine($"  Running Confluent async producer stress test for {options.DurationMinutes} minutes...");
        Console.WriteLine($"  Start time: {DateTime.UtcNow:HH:mm:ss.fff} UTC");
        LogResourceUsage("Initial");

        throughput.Start();
        var messageIndex = 0L;
        var lastStatusTime = DateTime.UtcNow;
        var lastStatusMessageCount = 0L;

        var samplerTask = RunSamplerAsync(throughput, cts.Token);
        var resourceMonitorTask = RunResourceMonitorAsync(cts.Token);

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                var start = Stopwatch.GetTimestamp();
                await producer.ProduceAsync(options.Topic, new ConfluentKafka.Message<string, string>
                {
                    Key = GetKey(messageIndex),
                    Value = messageValue
                }, cts.Token).ConfigureAwait(false);
                latency.RecordTicks(Stopwatch.GetTimestamp() - start);
                throughput.RecordMessage(options.MessageSizeBytes);
                messageIndex++;

                // Report status periodically - lower threshold than fire-and-forget since throughput is lower
                if (messageIndex % 10_000 == 0)
                {
                    var now = DateTime.UtcNow;
                    if ((now - lastStatusTime).TotalSeconds >= 10)
                    {
                        var elapsedSinceLastStatus = (now - lastStatusTime).TotalSeconds;
                        var messagesSinceLastStatus = messageIndex - lastStatusMessageCount;
                        var instantaneousMsgSec = messagesSinceLastStatus / elapsedSinceLastStatus;
                        Console.WriteLine($"  [{now:HH:mm:ss}] Progress: {messageIndex:N0} messages | instant: {instantaneousMsgSec:N0} msg/sec | avg: {throughput.GetAverageMessagesPerSecond():N0} msg/sec");
                        lastStatusTime = now;
                        lastStatusMessageCount = messageIndex;
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

        throughput.Stop();
        gcStats.Capture();

        try { await samplerTask.ConfigureAwait(false); } catch { }
        try { await resourceMonitorTask.ConfigureAwait(false); } catch { }

        var completedAt = DateTime.UtcNow;
        Console.WriteLine($"  Completed: {throughput.MessageCount:N0} messages, {throughput.GetAverageMessagesPerSecond():N0} msg/sec");
        LogResourceUsage("Final");

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

    private static async Task RunResourceMonitorAsync(CancellationToken cancellationToken)
    {
        var process = Process.GetCurrentProcess();
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(5000, cancellationToken).ConfigureAwait(false);
                LogResourceUsage("Monitor", process);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static void LogResourceUsage(string label, Process? process = null)
    {
        process ??= Process.GetCurrentProcess();
        process.Refresh();

        var workingSet = process.WorkingSet64 / (1024.0 * 1024.0);
        var privateMemory = process.PrivateMemorySize64 / (1024.0 * 1024.0);
        var gcHeap = GC.GetTotalMemory(forceFullCollection: false) / (1024.0 * 1024.0);
        var threadCount = process.Threads.Count;
        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);

        Console.WriteLine($"  [{DateTime.UtcNow:HH:mm:ss}] {label} Resources: " +
            $"WorkingSet={workingSet:F1}MB, Private={privateMemory:F1}MB, GCHeap={gcHeap:F1}MB, " +
            $"Threads={threadCount}, GC=[{gen0}/{gen1}/{gen2}]");
    }
}
