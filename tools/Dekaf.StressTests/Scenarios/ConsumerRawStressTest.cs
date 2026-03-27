using System.Runtime.CompilerServices;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;

namespace Dekaf.StressTests.Scenarios;

/// <summary>
/// Zero-copy consumer stress test that reads raw bytes instead of deserializing strings.
/// This isolates the consumer infrastructure overhead from string deserialization allocations,
/// enabling accurate measurement of the consumer's true throughput and memory characteristics.
/// </summary>
internal sealed class ConsumerRawStressTest : IStressTestScenario
{
    private static readonly string[] PreAllocatedKeys = CreatePreAllocatedKeys(10_000);

    public string Name => "consumer-raw";
    public string Client => "Dekaf";

    public async Task<StressTestResult> RunAsync(StressTestOptions options, CancellationToken cancellationToken)
    {
        var throughput = new ThroughputTracker();
        var startedAt = DateTime.UtcNow;
        var messageValue = new string('x', options.MessageSizeBytes);

        // Create producer to feed messages to the consumer (uses string serialization for production)
        var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(options.BootstrapServers)
            .WithClientId("stress-consumer-raw-feeder-dekaf")
            .WithAcks(Acks.Leader)
            .WithLinger(TimeSpan.FromMilliseconds(options.LingerMs))
            .WithBatchSize(options.BatchSize)
            .BuildAsync(cancellationToken);

        // Pre-seed messages before starting consumer measurement
        Console.WriteLine($"  Pre-seeding messages for consumer-raw test...");
        const int preseedCount = 500_000;
        for (var i = 0; i < preseedCount; i++)
        {
            await producer.FireAsync(options.Topic, GetKey(i), messageValue);
        }
        await producer.FlushAsync(cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"  Pre-seeded {preseedCount:N0} messages");

        // Consumer uses Ignore for key (don't care) and ReadOnlyMemory<byte> for zero-copy value access
        await using var consumer = await Kafka.CreateConsumer<Ignore, ReadOnlyMemory<byte>>()
            .WithBootstrapServers(options.BootstrapServers)
            .WithClientId("stress-consumer-raw-dekaf")
            .WithGroupId($"stress-group-raw-dekaf-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .ForHighThroughput()
            .BuildAsync(cancellationToken);

        consumer.Subscribe(options.Topic);

        // Start background producer to continuously feed the consumer
        var producerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var producerTask = RunBackgroundProducerAsync(producer, options.Topic, messageValue, producerCts.Token);

        // GC baseline after producer warmup but before consumer measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var gcStats = new GcStats();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(options.DurationMinutes));

        Console.WriteLine($"  Running Dekaf consumer-raw stress test for {options.DurationMinutes} minutes...");
        throughput.Start();

        var samplerTask = RunSamplerAsync(throughput, cts.Token);

        try
        {
            await foreach (var record in consumer.ConsumeAsync(cts.Token).ConfigureAwait(false))
            {
                throughput.RecordMessage(record.Value.Length);
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
            BrokerCount = options.BrokerCount,
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
                await producer.FireAsync(topic, GetKey(messageIndex), messageValue);
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
