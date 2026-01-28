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

        var config = new ConfluentKafka.ConsumerConfig
        {
            BootstrapServers = options.BootstrapServers,
            ClientId = "stress-consumer-confluent",
            GroupId = $"stress-group-confluent-{Guid.NewGuid():N}",
            AutoOffsetReset = ConfluentKafka.AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var consumer = new ConfluentKafka.ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(options.Topic);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var gcStats = new GcStats();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(options.DurationMinutes));

        Console.WriteLine($"  Running Confluent consumer stress test for {options.DurationMinutes} minutes...");
        throughput.Start();

        var samplerTask = RunSamplerAsync(throughput, cts.Token);

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(TimeSpan.FromMilliseconds(100));
                    if (result is not null)
                    {
                        throughput.RecordMessage(result.Message.Value?.Length ?? 0);
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
