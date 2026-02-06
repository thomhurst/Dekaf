using System.Runtime.CompilerServices;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;
using DekafLib = Dekaf;

namespace Dekaf.StressTests.Scenarios;

internal sealed class ConsumerStressTest : IStressTestScenario
{
    private static readonly string[] PreAllocatedKeys = CreatePreAllocatedKeys(10_000);

    public string Name => "consumer";
    public string Client => "Dekaf";

    public async Task<StressTestResult> RunAsync(StressTestOptions options, CancellationToken cancellationToken)
    {
        var throughput = new ThroughputTracker();
        var startedAt = DateTime.UtcNow;
        var messageValue = new string('x', options.MessageSizeBytes);

        // Create producer to feed messages to the consumer
        var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(options.BootstrapServers)
            .WithClientId("stress-consumer-feeder-dekaf")
            .WithAcks(Acks.Leader)
            .WithLinger(TimeSpan.FromMilliseconds(options.LingerMs))
            .WithBatchSize(options.BatchSize)
            .Build();

        // Pre-seed messages before starting consumer measurement
        Console.WriteLine($"  Pre-seeding messages for consumer test...");
        const int preseedCount = 500_000;
        for (var i = 0; i < preseedCount; i++)
        {
            producer.Send(options.Topic, GetKey(i), messageValue);
        }
        await producer.FlushAsync(CancellationToken.None).ConfigureAwait(false);
        Console.WriteLine($"  Pre-seeded {preseedCount:N0} messages");

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(options.BootstrapServers)
            .WithClientId("stress-consumer-dekaf")
            .WithGroupId($"stress-group-dekaf-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(options.Topic);

        // Start background producer to continuously feed the consumer
        var producerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var producerTask = RunBackgroundProducerAsync(producer, options.Topic, messageValue, producerCts.Token);

        // GC baseline after producer warmup but before consumer measurement
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

        // Stop background producer
        await producerCts.CancelAsync().ConfigureAwait(false);
        try { await producerTask.ConfigureAwait(false); } catch (OperationCanceledException) { }

        try { await samplerTask.ConfigureAwait(false); } catch { }

        // Clean up producer
        try
        {
            await producer.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Console.WriteLine($"  Warning: Producer dispose timed out");
        }

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

    private static async Task RunBackgroundProducerAsync(
        IKafkaProducer<string, string> producer,
        string topic,
        string messageValue,
        CancellationToken cancellationToken)
    {
        var messageIndex = 0L;

        await Task.Yield(); // Ensure we're on a background thread

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                producer.Send(topic, GetKey(messageIndex), messageValue);
                messageIndex++;

                // Yield periodically to avoid starving other tasks
                if (messageIndex % 10_000 == 0)
                {
                    await Task.Yield();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Ignore producer errors in the feeder
            }
        }
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
