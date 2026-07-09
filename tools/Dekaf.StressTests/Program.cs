using System.Collections.Concurrent;
using Dekaf.Producer;
using Dekaf.StressTests.Diagnostics;
using Dekaf.StressTests.FaultInjection;
using Dekaf.StressTests.Infrastructure;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;
using Dekaf.StressTests.Scenarios;

namespace Dekaf.StressTests;

/// <summary>
/// Dekaf Stress Test Runner - Sustained throughput comparison between Dekaf and Confluent.Kafka
///
/// Usage:
///   dotnet run -c Release -- [options]
///
/// Options:
///   --duration &lt;minutes&gt;    Test duration in minutes (default: 15)
///   --message-size &lt;bytes&gt;  Message size in bytes (default: 1000)
///   --scenario &lt;name&gt;       Run specific scenario: producer, producer-idempotent, producer-acks-all, producer-async, producer-async-idempotent, consumer, consumer-batch, consumer-raw, consumer-raw-batch, all (default: all)
///   --client &lt;name&gt;         Run specific client: dekaf, confluent, all (default: all)
///   --output &lt;path&gt;         Output directory for results (default: ./results)
///   --brokers &lt;count&gt;      Number of Kafka brokers (default: 1, use 3 for multi-broker)
///   --producer-delivery-diagnostics  Capture Dekaf producer delivery diagnostics on message loss and watchdog stalls
///   report --input &lt;path&gt;   Generate report from existing results
///   fault [options]          Run fault-injection correctness suite
///
/// Environment Variables:
///   KAFKA_BOOTSTRAP_SERVERS - Use external Kafka instead of Testcontainers
///
/// Examples:
///   dotnet run -c Release -- --duration 15 --message-size 1000
///   dotnet run -c Release -- --scenario producer --client dekaf --duration 5
///   dotnet run -c Release -- fault --fault-profile network --brokers 1
///   dotnet run -c Release -- report --input ./results
/// </summary>
public static class Program
{
    private static readonly ConcurrentQueue<Exception> UnobservedTaskExceptions = new();

    public static async Task<int> Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Console.WriteLine($"UNHANDLED EXCEPTION: {e.ExceptionObject}");
        };

        // A task exception nobody awaited means a background failure escaped every
        // error path — collected here and escalated to a run failure at the end.
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            UnobservedTaskExceptions.Enqueue(e.Exception);
            e.SetObserved();
        };

        Console.CancelKeyPress += (_, e) =>
        {
            Console.WriteLine("CANCEL KEY PRESSED");
        };

        try
        {
            Console.WriteLine($"Process started at {DateTime.UtcNow:HH:mm:ss.fff} UTC, PID: {Environment.ProcessId}");

            var options = ParseArgs(args);

            if (options.IsReport)
            {
                return await RunReportAsync(options).ConfigureAwait(false);
            }

            if (options.IsFaultInjection)
            {
                return await FaultInjectionRunner.RunAsync(new FaultInjectionOptions
                {
                    Profile = options.FaultProfile,
                    BrokerCount = options.Brokers,
                    PartitionCount = options.Partitions,
                    MessageSizeBytes = options.MessageSizeBytes,
                    FaultDuration = TimeSpan.FromSeconds(options.FaultDurationSeconds),
                    MessagesBeforeFault = options.MessagesBeforeFault,
                    MaxMessagesDuringFault = options.MaxMessagesDuringFault,
                    MessagesAfterFault = options.MessagesAfterFault,
                    OutputPath = options.OutputPath,
                    AllowedFailureWindows = options.AllowedFailureWindows
                }).ConfigureAwait(false);
            }

            return await RunStressTestsAsync(options).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Operation was cancelled");
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex}");
            return 1;
        }
    }

    private static async Task<int> RunStressTestsAsync(CliOptions options)
    {
        Console.WriteLine("Dekaf Stress Test Runner");
        Console.WriteLine($"Duration: {options.DurationMinutes} minutes");
        Console.WriteLine($"Message Size: {options.MessageSizeBytes} bytes");
        Console.WriteLine($"Scenario: {options.Scenario}");
        Console.WriteLine($"Client: {options.Client}");
        if (options.Client.Equals("all", StringComparison.OrdinalIgnoreCase) &&
            Environment.GetEnvironmentVariable("STRESS_CLIENT_ORDER") is { Length: > 0 } clientOrder)
        {
            Console.WriteLine($"Client Order: {clientOrder}");
        }

        Console.WriteLine($"Compression: {options.Compression}");
        Console.WriteLine($"Brokers: {options.Brokers}");
        Console.WriteLine($"Producer delivery diagnostics: {(options.EnableProducerDeliveryDiagnostics ? "enabled" : "disabled")}");
        Console.WriteLine($"Progress watchdog: stacks at {ProgressWatchdog.DefaultCaptureAfter.TotalSeconds:F0}s; " +
            $"fail at {ProgressWatchdog.DefaultExitAfter.TotalMinutes:F0} minutes");
        if (options.ConnectionsPerBroker > 1)
            Console.WriteLine($"Multi-connection: {options.ConnectionsPerBroker} connections per broker (Dekaf only)");
        Console.WriteLine(new string('-', 50));

        Directory.CreateDirectory(options.OutputPath);
        using var progressWatchdog = new ProgressWatchdog(options.OutputPath);

        await using var kafka = await KafkaEnvironment.CreateAsync(options.Brokers).ConfigureAwait(false);

        var producerTopic = $"stress-producer-{Guid.NewGuid():N}";
        var consumerTopic = $"stress-consumer-{Guid.NewGuid():N}";

        var replicationFactor = Math.Min(options.Brokers, 3);
        await kafka.CreateTopicAsync(producerTopic, options.Partitions, replicationFactor).ConfigureAwait(false);
        // Consumer scenarios re-read the seeded data set in a loop for the full duration.
        // Broker-level retention (tuned to bound producer-topic disk usage) must not
        // delete it mid-run, so retention is disabled at the topic level.
        await kafka.CreateTopicAsync(consumerTopic, options.Partitions, replicationFactor, new Dictionary<string, string>
        {
            ["retention.ms"] = "-1",
            ["retention.bytes"] = "-1"
        }).ConfigureAwait(false);

        if (options.Scenario is "consumer" or "consumer-batch" or "consumer-raw" or "consumer-raw-batch" or "all")
        {
            await SeedConsumerTopicAsync(kafka.BootstrapServers, consumerTopic, options).ConfigureAwait(false);
        }

        var scenarios = GetScenarios(options);
        var results = new List<StressTestResult>();
        var runStartedAt = DateTime.UtcNow;

        StressTestOptions BuildTestOptions(string scenarioName, int connectionsPerBroker) => new()
        {
            BootstrapServers = kafka.BootstrapServers,
            Topic = scenarioName.StartsWith("producer", StringComparison.OrdinalIgnoreCase) ? producerTopic : consumerTopic,
            DurationMinutes = options.DurationMinutes,
            MessageSizeBytes = options.MessageSizeBytes,
            Partitions = options.Partitions,
            LingerMs = options.LingerMs,
            BatchSize = options.BatchSize,
            Compression = options.Compression,
            BrokerCount = options.Brokers,
            ConnectionsPerBroker = connectionsPerBroker,
            EnableProducerDeliveryDiagnostics = options.EnableProducerDeliveryDiagnostics,
            ProgressWatchdog = progressWatchdog
        };

        if (options.ConnectionsPerBroker == 1)
        {
            // Baseline pass: single connection for fair comparison with Confluent.
            foreach (var scenario in scenarios)
            {
                Console.WriteLine();
                Console.WriteLine($"=== Running: {scenario.Client} {scenario.Name} ===");

                var result = await scenario.RunAsync(BuildTestOptions(scenario.Name, connectionsPerBroker: 1), CancellationToken.None).ConfigureAwait(false);
                results.Add(result);

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }
        else
        {
            // Multi-connection pass: Dekaf-only producer scenarios with explicit
            // ConnectionsPerBroker to measure parallel TCP connection throughput.
            var multiConnScenarios = scenarios
                .Where(s => s.Client == "Dekaf" && s.Name.StartsWith("producer", StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var scenario in multiConnScenarios)
            {
                Console.WriteLine();
                Console.WriteLine($"=== Running: {scenario.Client} {scenario.Name} ({options.ConnectionsPerBroker}conn) ===");

                var result = await scenario.RunAsync(BuildTestOptions(scenario.Name, options.ConnectionsPerBroker), CancellationToken.None).ConfigureAwait(false);
                result.Client = $"Dekaf ({options.ConnectionsPerBroker}conn)";
                results.Add(result);

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        var runCompletedAt = DateTime.UtcNow;

        var allResults = new StressTestResults
        {
            RunStartedAtUtc = runStartedAt,
            RunCompletedAtUtc = runCompletedAt,
            MachineName = Environment.MachineName,
            ProcessorCount = Environment.ProcessorCount,
            Results = results
        };

        var outputDir = options.OutputPath;

        // Broker/connection counts are part of the name so runs of the same
        // client+scenario (e.g. 1-broker vs 3-broker CI matrix jobs, which can start
        // within the same second) never produce identically named files — flattening
        // their outputs into one directory would silently overwrite one.
        var connSuffix = options.ConnectionsPerBroker > 1 ? $"-{options.ConnectionsPerBroker}conn" : "";
        var fileSuffix = options.Scenario != "all" || options.Client != "all"
            ? $"-{options.Client}-{options.Scenario}-{options.Brokers}brokers{connSuffix}-{runStartedAt:yyyyMMdd-HHmmss}"
            : $"-{runStartedAt:yyyyMMdd-HHmmss}";
        var jsonPath = Path.Combine(outputDir, $"stress-test-results{fileSuffix}.json");
        var mdPath = Path.Combine(outputDir, $"stress-test-results{fileSuffix}.md");

        await allResults.SaveAsync(jsonPath).ConfigureAwait(false);
        await MarkdownReporter.WriteToFileAsync(allResults, mdPath).ConfigureAwait(false);
        await MarkdownReporter.AppendToGitHubSummaryAsync(allResults).ConfigureAwait(false);

        Console.WriteLine();
        Console.WriteLine($"Results saved to: {jsonPath}");
        Console.WriteLine($"Markdown report: {mdPath}");

        Console.WriteLine();
        Console.WriteLine(MarkdownReporter.Generate(allResults));

        return CheckForFailures(results) ? 1 : 0;
    }

    /// <summary>
    /// Maximum consecutive seconds with zero client progress before the run is failed as
    /// stalled. A healthy broker never starves a producer or consumer for 30 straight
    /// seconds; a stall that long is a hang that happened to recover. Converted to a
    /// sample count via <see cref="StressTestHelpers.SamplerIntervalSeconds"/>.
    /// </summary>
    private const int StallThresholdSeconds = 30;

    private const int StallThresholdSamples = StallThresholdSeconds / StressTestHelpers.SamplerIntervalSeconds;

    /// <summary>
    /// The stress environment (healthy broker, tmpfs logs, no restarts) never justifies a
    /// dropped message, an error, a stall, or a hung shutdown, so all of them fail the run:
    /// - Any loop error (append/consume failures) or delivery error (broker-side failure of
    ///   an accepted message, observed via the producer error metric or delivery reports).
    /// - Undelivered shortfall: accepted minus broker-confirmed delivered minus delivery
    ///   errors. The post-run drain wait already absorbs follower high-watermark lag, so
    ///   every producer scenario treats a remaining shortfall as loss — no exemptions.
    /// - Duplicate delivery in idempotent scenarios (delivered exceeding accepted), which
    ///   means broker-side deduplication of retries is broken. Non-idempotent scenarios
    ///   skip this check because retry duplicates are legitimate there.
    /// - A run that ended measurably earlier than its configured duration (a swallowed
    ///   cancellation or crashed loop would otherwise pass with a fraction of the load).
    /// - A sustained mid-run stall (see <see cref="StallThresholdSeconds"/>).
    /// - Task exceptions nobody observed, surfaced after a final finalizer sweep.
    /// Results are already saved at this point; the non-zero exit only fails the CI job.
    /// </summary>
    private static bool CheckForFailures(List<StressTestResult> results)
    {
        var anyFailure = false;

        void MarkFailure()
        {
            if (!anyFailure)
            {
                Console.WriteLine();
                Console.WriteLine("CORRECTNESS FAILURES DETECTED - failing the run:");
                anyFailure = true;
            }
        }

        foreach (var result in results)
        {
            var reasons = new List<string>();
            var errors = result.Throughput.TotalErrors;
            var deliveryErrors = result.Throughput.TotalDeliveryErrors;
            var accepted = result.Throughput.TotalMessages;
            var lost = 0L;

            if (errors > 0)
            {
                reasons.Add($"{errors:N0} errors");
            }

            if (deliveryErrors > 0)
            {
                reasons.Add($"{deliveryErrors:N0} delivery errors");
            }

            if (result.DeliveredMessages is { } delivered)
            {
                lost = Math.Max(0, accepted - delivered - deliveryErrors);
                if (lost > 0)
                {
                    reasons.Add($"{lost:N0} undelivered messages");
                }

                // Idempotent producers rely on broker-side retry deduplication, so any
                // overage means that guarantee is broken.
                if (result.Idempotent && delivered > accepted)
                {
                    reasons.Add($"{delivered - accepted:N0} duplicate deliveries (idempotence violated)");
                }
            }

            var expectedSeconds = result.DurationMinutes * 60;
            if (result.Throughput.ElapsedSeconds < expectedSeconds * 0.9)
            {
                reasons.Add(
                    $"run ended early ({result.Throughput.ElapsedSeconds:N0}s of {expectedSeconds:N0}s)");
            }

            var maxStall = MaxConsecutiveZeroSamples(result.Throughput.MessagesPerSecondSamples);
            if (maxStall >= StallThresholdSamples)
            {
                reasons.Add($"stalled for {maxStall * StressTestHelpers.SamplerIntervalSeconds:N0} consecutive seconds with zero progress");
            }

            if (reasons.Count == 0)
            {
                continue;
            }

            MarkFailure();

            var deliveredText = result.DeliveredMessages is { } d ? d.ToString("N0") : "n/a";
            Console.WriteLine(
                $"  {result.Client} {result.Scenario}: {string.Join("; ", reasons)} " +
                $"[accepted={accepted:N0}, delivered={deliveredText}, " +
                $"errors={errors:N0}, deliveryErrors={deliveryErrors:N0}]");

            if (result.Throughput.ErrorSamples.Count > 0)
            {
                PrintErrorSamples(result.Throughput.ErrorSamples);
            }
            else if (errors > 0 || deliveryErrors > 0)
            {
                Console.WriteLine("    No exception samples captured for these errors.");
            }

            if (lost > 0)
            {
                PrintProducerDeliveryDiagnostics(result.ProducerDeliveryDiagnostics);
            }
        }

        // Drain finalizers so exceptions from abandoned tasks surface before the verdict.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        if (!UnobservedTaskExceptions.IsEmpty)
        {
            MarkFailure();

            Console.WriteLine($"  {UnobservedTaskExceptions.Count:N0} unobserved task exception(s):");
            var printed = 0;
            foreach (var exception in UnobservedTaskExceptions)
            {
                Console.WriteLine($"    - {exception}");
                if (++printed >= 5)
                {
                    Console.WriteLine("    (further exceptions omitted)");
                    break;
                }
            }
        }

        return anyFailure;
    }

    private static int MaxConsecutiveZeroSamples(List<double> samples)
    {
        var max = 0;
        var current = 0;
        foreach (var sample in samples)
        {
            current = sample <= 0 ? current + 1 : 0;
            max = Math.Max(max, current);
        }

        return max;
    }

    private static void PrintProducerDeliveryDiagnostics(ProducerDeliveryDiagnosticsSnapshot? snapshot)
    {
        Console.WriteLine("    Producer delivery diagnostics:");
        if (snapshot is null)
        {
            Console.WriteLine("      Not captured. Pass --producer-delivery-diagnostics to enable.");
            return;
        }

        if (!snapshot.DiagnosticsEnabled)
        {
            Console.WriteLine("      Disabled.");
            return;
        }

        Console.WriteLine(
            $"      capturedAtUtc={snapshot.CapturedAtUtc:O} " +
            $"inFlight={snapshot.InFlightBatchCount:N0} batches={snapshot.Batches.Count:N0}");

        if (snapshot.Batches.Count == 0)
        {
            Console.WriteLine("      No live in-flight batches remained when diagnostics were captured.");
            return;
        }

        foreach (var batch in snapshot.Batches)
        {
            Console.WriteLine(
                $"      - {batch.Topic}-{batch.Partition} " +
                $"records={batch.RecordCount:N0} " +
                $"dataSize={batch.DataSize:N0} encodedSize={batch.EncodedSize:N0} " +
                $"state={batch.LifecycleState} trace={batch.Trace} last={batch.LastTouchedBy}");
            Console.WriteLine(
                $"        readyBatchId={batch.ReadyBatchId} recordBatchId={FormatNullableInt(batch.RecordBatchId)} " +
                $"arenaId={FormatNullableInt(batch.ArenaId)} " +
                $"generation={batch.PipelineGeneration}/{batch.CurrentGeneration} " +
                $"stale={batch.IsStale} " +
                $"preSerialized={batch.IsPreSerialized} sendCompleted={batch.IsSendCompleted} " +
                $"doneTaskCompleted={batch.IsDoneTaskCompleted} memoryReleased={batch.IsMemoryReleased} " +
                $"returnedToPool={batch.IsReturnedToPool} inFlightLinked={batch.InFlightLinked}");
        }
    }

    private static string FormatNullableInt(int? value) => value?.ToString() ?? "null";

    private static void PrintErrorSamples(List<ThroughputErrorSample> samples)
    {
        Console.WriteLine($"    Error samples (first {samples.Count:N0}):");
        foreach (var sample in samples)
        {
            var messageIndex = sample.MessageIndex is { } index ? index.ToString("N0") : "unknown";
            var operation = string.IsNullOrWhiteSpace(sample.Operation) ? "unknown" : sample.Operation;

            Console.WriteLine(
                $"    - error #{sample.ErrorNumber:N0} at +{sample.ElapsedSeconds:F3}s " +
                $"accepted={sample.AcceptedMessagesAtError:N0} " +
                $"messageIndex={messageIndex} operation={operation}");
            Console.WriteLine($"      {sample.ExceptionType}: {sample.Message}");

            if (!string.IsNullOrWhiteSpace(sample.Details))
            {
                Console.WriteLine("      Details:");
                WriteIndentedBlock(sample.Details, "        ");
            }
        }
    }

    private static void WriteIndentedBlock(string text, string indent)
    {
        foreach (var line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            Console.WriteLine($"{indent}{line}");
        }
    }

    private static async Task SeedConsumerTopicAsync(string bootstrapServers, string topic, CliOptions options)
    {
        Console.WriteLine($"Seeding consumer topic with messages...");

        var messageValue = new string('x', options.MessageSizeBytes);
        // Consumer scenarios loop over this data set (seek to beginning when drained),
        // so it only needs to be large enough that rewind overhead is negligible.
        var totalMessages = options.SeedMessages;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(bootstrapServers)
            .WithClientId("stress-seeder")
            .WithAcks(Acks.Leader)
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .WithBatchSize(16384)
            .BuildAsync();

        var batchSize = 10_000;
        var batches = totalMessages / batchSize;

        for (var batch = 0; batch < batches; batch++)
        {
            for (var i = 0; i < batchSize; i++)
            {
                var messageIndex = batch * batchSize + i;
                await producer.FireAsync(topic, StressTestHelpers.GetKey(messageIndex), messageValue).ConfigureAwait(false);
            }

            // Flush every batch to avoid overwhelming the buffer with backpressure
            // Each batch is 10K messages × 1KB = 10MB, buffer is 1GB
            await producer.FlushAsync(CancellationToken.None).ConfigureAwait(false);

            if (batch % 10 == 0)
            {
                Console.WriteLine($"  Seeded {(batch + 1) * batchSize:N0} / {totalMessages:N0} messages");
            }
        }

        await producer.FlushAsync(CancellationToken.None).ConfigureAwait(false);
        Console.WriteLine($"  Seeding complete: {totalMessages:N0} messages");
    }

    private static List<IStressTestScenario> GetScenarios(CliOptions options)
    {
        var allScenarios = new List<IStressTestScenario>
        {
            new ProducerStressTest(),
            new ConfluentProducerStressTest(),
            new ProducerIdempotentStressTest(),
            new ConfluentProducerIdempotentStressTest(),
            new ProducerAsyncStressTest(),
            new ConfluentProducerAsyncStressTest(),
            new ProducerAcksAllStressTest(),
            new ConfluentProducerAcksAllStressTest(),
            new ProducerAsyncIdempotentStressTest(),
            new ConfluentProducerAsyncIdempotentStressTest(),
            new ConsumerStressTest(),
            new ConsumerBatchStressTest(),
            new ConsumerRawStressTest(),
            new ConsumerRawBatchStressTest(),
            new ConfluentConsumerStressTest()
        };

        var scenarios = allScenarios
            .Where(s => options.Scenario == "all" || s.Name.Equals(options.Scenario, StringComparison.OrdinalIgnoreCase))
            .Where(s => options.Client == "all" || s.Client.Equals(options.Client, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return ApplyClientOrder(scenarios, options.Client);
    }

    private static List<IStressTestScenario> ApplyClientOrder(List<IStressTestScenario> scenarios, string requestedClient)
    {
        if (!requestedClient.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return scenarios;
        }

        var clientOrder = Environment.GetEnvironmentVariable("STRESS_CLIENT_ORDER");
        if (!string.Equals(clientOrder, "confluent-first", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(clientOrder, "dekaf-first", StringComparison.OrdinalIgnoreCase))
        {
            return scenarios;
        }

        var confluentFirst = string.Equals(clientOrder, "confluent-first", StringComparison.OrdinalIgnoreCase);
        return scenarios
            .GroupBy(s => s.Name)
            .SelectMany(group => group.OrderBy(s => ClientOrderIndex(s.Client, confluentFirst)))
            .ToList();
    }

    private static int ClientOrderIndex(string client, bool confluentFirst)
    {
        if (client.Equals("Confluent", StringComparison.OrdinalIgnoreCase))
        {
            return confluentFirst ? 0 : 1;
        }

        if (client.Equals("Dekaf", StringComparison.OrdinalIgnoreCase))
        {
            return confluentFirst ? 1 : 0;
        }

        return 2;
    }

    private static async Task<int> RunReportAsync(CliOptions options)
    {
        Console.WriteLine($"Generating report from: {options.InputPath}");

        var jsonFiles = Directory.GetFiles(options.InputPath, "stress-test-results*.json");
        if (jsonFiles.Length == 0)
        {
            Console.WriteLine("No stress-test result JSON files found");
            return 1;
        }

        var latestFile = jsonFiles.OrderByDescending(f => f).First();
        Console.WriteLine($"Using: {latestFile}");

        var results = await StressTestResults.LoadAsync(latestFile).ConfigureAwait(false);
        if (results is null)
        {
            Console.WriteLine("Failed to load results");
            return 1;
        }

        var markdown = MarkdownReporter.Generate(results);
        Console.WriteLine(markdown);

        await MarkdownReporter.AppendToGitHubSummaryAsync(results).ConfigureAwait(false);

        return 0;
    }

    private static CliOptions ParseArgs(string[] args)
    {
        var options = new CliOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i].ToLowerInvariant();

            switch (arg)
            {
                case "report":
                    options.IsReport = true;
                    break;
                case "fault":
                    options.IsFaultInjection = true;
                    break;
                case "--duration":
                    options.DurationMinutes = int.Parse(args[++i]);
                    break;
                case "--message-size":
                    options.MessageSizeBytes = int.Parse(args[++i]);
                    break;
                case "--scenario":
                    options.Scenario = args[++i].ToLowerInvariant();
                    break;
                case "--client":
                    options.Client = args[++i].ToLowerInvariant();
                    break;
                case "--output":
                    options.OutputPath = args[++i];
                    break;
                case "--input":
                    options.InputPath = args[++i];
                    break;
                case "--partitions":
                    options.Partitions = int.Parse(args[++i]);
                    break;
                case "--linger-ms":
                    options.LingerMs = int.Parse(args[++i]);
                    break;
                case "--batch-size":
                    options.BatchSize = int.Parse(args[++i]);
                    break;
                case "--compression":
                    options.Compression = args[++i].ToLowerInvariant();
                    break;
                case "--brokers":
                    options.Brokers = int.Parse(args[++i]);
                    if (options.Brokers < 1)
                    {
                        throw new ArgumentException("--brokers must be at least 1");
                    }
                    break;
                case "--connections-per-broker":
                    options.ConnectionsPerBroker = int.Parse(args[++i]);
                    if (options.ConnectionsPerBroker < 1)
                    {
                        throw new ArgumentException("--connections-per-broker must be at least 1");
                    }
                    break;
                case "--seed-messages":
                    options.SeedMessages = int.Parse(args[++i]);
                    if (options.SeedMessages < 1)
                    {
                        throw new ArgumentException("--seed-messages must be at least 1");
                    }
                    break;
                case "--producer-delivery-diagnostics":
                    options.EnableProducerDeliveryDiagnostics = true;
                    break;
                case "--fault-profile":
                    options.FaultProfile = args[++i].ToLowerInvariant();
                    break;
                case "--allowed-failure-windows":
                    foreach (var window in args[++i].Split(
                                 ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        options.AllowedFailureWindows.Add(window);
                    }
                    break;
                case "--fault-duration-seconds":
                    options.FaultDurationSeconds = ParsePositiveInt(args[++i], "--fault-duration-seconds");
                    break;
                case "--messages-before-fault":
                    options.MessagesBeforeFault = ParsePositiveInt(args[++i], "--messages-before-fault");
                    break;
                case "--max-messages-during-fault":
                    options.MaxMessagesDuringFault = ParsePositiveInt(args[++i], "--max-messages-during-fault");
                    break;
                case "--messages-after-fault":
                    options.MessagesAfterFault = ParsePositiveInt(args[++i], "--messages-after-fault");
                    break;
                case "--help":
                case "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;
            }
        }

        return options;
    }

    private static int ParsePositiveInt(string value, string optionName)
    {
        var parsed = int.Parse(value);
        if (parsed < 1)
        {
            throw new ArgumentException($"{optionName} must be at least 1");
        }

        return parsed;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Dekaf Stress Test Runner

            Usage:
              dotnet run -c Release -- [options]

            Options:
              --duration <minutes>    Test duration in minutes (default: 15)
              --message-size <bytes>  Message size in bytes (default: 1000)
              --scenario <name>       Run specific scenario: producer, producer-idempotent, producer-acks-all, producer-async, producer-async-idempotent, consumer, consumer-batch, consumer-raw, consumer-raw-batch, all (default: all)
              --client <name>         Run specific client: dekaf, confluent, all (default: all)
              --output <path>         Output directory for results (default: ./results)
              --partitions <count>    Number of topic partitions (default: 6)
              --linger-ms <ms>        Producer linger time (default: 5)
              --batch-size <bytes>    Producer batch size (default: 1048576)
              --compression <type>   Compression type: none, lz4, snappy, zstd (default: none)
              --brokers <count>      Number of Kafka brokers (default: 1, use 3 for multi-broker)
              --connections-per-broker <n>  TCP connections per broker (default: 1, pass 3 for multi-connection comparison)
              --seed-messages <count> Messages pre-seeded into the consumer topic (default: 2000000)
              --producer-delivery-diagnostics  Capture Dekaf producer delivery diagnostics on message loss and watchdog stalls
              report --input <path>   Generate report from existing results

            Fault injection:
              fault                    Run fault-injection correctness suite
              --fault-profile <name>   network, broker, or all (default: all)
              --allowed-failure-windows <names>  Comma-separated window names that may fail
              --fault-duration-seconds <n>  Active duration of each fault (default: 5)
              --messages-before-fault <n>   Messages produced before activation (default: 2000)
              --max-messages-during-fault <n>  Maximum buffered during active fault (default: 20000)
              --messages-after-fault <n>    Messages proving post-heal recovery (default: 2000)
              --brokers <count>        Fault mode accepts 1 or 3 brokers

            Environment Variables:
              KAFKA_BOOTSTRAP_SERVERS - Use external Kafka instead of Testcontainers
              STRESS_CLIENT_ORDER      - For --client all, run paired clients as dekaf-first or confluent-first
              STRESS_BROKER_CPUSET    - Pin Testcontainers brokers to CPU cores (e.g. "0-5") so the client keeps dedicated cores
              STRESS_BROKER_TMPFS     - Mount broker log dirs on tmpfs of this size (e.g. "6g") so disk I/O never caps ingestion

            Examples:
              dotnet run -c Release -- --duration 15 --message-size 1000
              dotnet run -c Release -- --scenario producer --client dekaf --duration 5
              dotnet run -c Release -- report --input ./results
              dotnet run -c Release -- fault --fault-profile broker --brokers 3
            """);
    }

    private sealed class CliOptions
    {
        public bool IsReport { get; set; }
        public bool IsFaultInjection { get; set; }
        public int DurationMinutes { get; set; } = 15;
        public int MessageSizeBytes { get; set; } = 1000;
        public string Scenario { get; set; } = "all";
        public string Client { get; set; } = "all";
        public string OutputPath { get; set; } = "./results";
        public string InputPath { get; set; } = "./results";
        public int Partitions { get; set; } = 6;
        public int LingerMs { get; set; } = 5;
        public int BatchSize { get; set; } = 1048576;
        public string Compression { get; set; } = "none";
        public int Brokers { get; set; } = 1;
        public int ConnectionsPerBroker { get; set; } = 1;
        public int SeedMessages { get; set; } = 2_000_000;
        public bool EnableProducerDeliveryDiagnostics { get; set; }
        public string FaultProfile { get; set; } = "all";
        public int FaultDurationSeconds { get; set; } = 5;
        public int MessagesBeforeFault { get; set; } = 2_000;
        public int MaxMessagesDuringFault { get; set; } = 20_000;
        public int MessagesAfterFault { get; set; } = 2_000;
        public HashSet<string> AllowedFailureWindows { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
