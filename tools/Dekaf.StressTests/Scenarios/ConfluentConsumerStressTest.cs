using System.Runtime.CompilerServices;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;
using ConfluentKafka = Confluent.Kafka;

namespace Dekaf.StressTests.Scenarios;

internal sealed class ConfluentConsumerStressTest : IStressTestScenario
{
    private static readonly string[] PreAllocatedKeys = CreatePreAllocatedKeys(10_000);

    public string Name => "consumer";
    public string Client => "Confluent";

    public async Task<StressTestResult> RunAsync(StressTestOptions options, CancellationToken cancellationToken)
    {
        var throughput = new ThroughputTracker();
        var startedAt = DateTime.UtcNow;
        var messageValue = new string('x', options.MessageSizeBytes);

        // Create producer to feed messages to the consumer
        var producerConfig = new ConfluentKafka.ProducerConfig
        {
            BootstrapServers = options.BootstrapServers,
            ClientId = "stress-consumer-feeder-confluent",
            Acks = ConfluentKafka.Acks.Leader,
            LingerMs = options.LingerMs,
            BatchSize = options.BatchSize
        };

        using var producer = new ConfluentKafka.ProducerBuilder<string, string>(producerConfig).Build();

        // Pre-seed messages before starting consumer measurement
        Console.WriteLine($"  Pre-seeding messages for consumer test...");
        const int preseedCount = 500_000;
        for (var i = 0; i < preseedCount; i++)
        {
            producer.Produce(options.Topic, new ConfluentKafka.Message<string, string>
            {
                Key = GetKey(i),
                Value = messageValue
            });
        }
        producer.Flush(TimeSpan.FromSeconds(60));
        Console.WriteLine($"  Pre-seeded {preseedCount:N0} messages");

        var consumerConfig = new ConfluentKafka.ConsumerConfig
        {
            BootstrapServers = options.BootstrapServers,
            ClientId = "stress-consumer-confluent",
            GroupId = $"stress-group-confluent-{Guid.NewGuid():N}",
            AutoOffsetReset = ConfluentKafka.AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var consumer = new ConfluentKafka.ConsumerBuilder<string, string>(consumerConfig).Build();
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
            MessageSizeBytes = options.MessageSizeBytes,
            StartedAtUtc = startedAt,
            CompletedAtUtc = completedAt,
            Throughput = throughput.GetSnapshot(),
            Latency = null,
            GcStats = gcStats.ToSnapshot()
        };
    }

    private static async Task RunBackgroundProducerAsync(
        ConfluentKafka.IProducer<string, string> producer,
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
                producer.Produce(topic, new ConfluentKafka.Message<string, string>
                {
                    Key = GetKey(messageIndex),
                    Value = messageValue
                });
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
