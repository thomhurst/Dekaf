using System.Runtime.CompilerServices;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;
using ConfluentKafka = Confluent.Kafka;

namespace Dekaf.StressTests.Scenarios;

internal sealed class ConfluentProducerStressTest : IStressTestScenario
{
    private static readonly string[] PreAllocatedKeys = CreatePreAllocatedKeys(10_000);

    public string Name => "producer";
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
            ClientId = "stress-producer-confluent",
            Acks = ConfluentKafka.Acks.Leader,
            LingerMs = options.LingerMs,
            BatchSize = options.BatchSize
        };

        using var producer = new ConfluentKafka.ProducerBuilder<string, string>(config).Build();

        Console.WriteLine($"  Warming up Confluent producer...");
        for (var i = 0; i < 1000; i++)
        {
            producer.Produce(options.Topic, new ConfluentKafka.Message<string, string> { Key = "warmup", Value = "warmup" });
        }
        producer.Flush(TimeSpan.FromSeconds(30));

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var gcStats = new GcStats();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(options.DurationMinutes));

        Console.WriteLine($"  Running Confluent producer stress test for {options.DurationMinutes} minutes...");
        throughput.Start();
        var messageIndex = 0L;

        var samplerTask = RunSamplerAsync(throughput, cts.Token);

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                var start = System.Diagnostics.Stopwatch.GetTimestamp();
                producer.Produce(options.Topic, new ConfluentKafka.Message<string, string>
                {
                    Key = GetKey(messageIndex),
                    Value = messageValue
                });
                latency.RecordTicks(System.Diagnostics.Stopwatch.GetTimestamp() - start);
                throughput.RecordMessage(options.MessageSizeBytes);
                messageIndex++;
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
