using System.Collections.Concurrent;
using Dekaf.Producer;
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
///   --scenario &lt;name&gt;       Run specific scenario: producer, producer-idempotent, producer-acks-all, producer-async, producer-async-idempotent, producer-transactional, consumer, consumer-batch, consumer-raw, consumer-raw-batch, all (default: all)
///   --client &lt;name&gt;         Run specific client: dekaf, confluent, all (default: all)
///   --output &lt;path&gt;         Output directory for results (default: ./results)
///   --brokers &lt;count&gt;      Number of Kafka brokers (default: 1, use 3 for multi-broker)
///   --producer-delivery-diagnostics  Capture Dekaf producer delivery diagnostics on message-loss failures
///   report --input &lt;path&gt;   Generate report from existing results
///
/// Environment Variables:
///   KAFKA_BOOTSTRAP_SERVERS - Use external Kafka instead of Testcontainers
///
/// Examples:
///   dotnet run -c Release -- --duration 15 --message-size 1000
///   dotnet run -c Release -- --scenario producer --client dekaf --duration 5
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
        if (options.ConnectionsPerBroker > 1)
            Console.WriteLine($"Multi-connection: {options.ConnectionsPerBroker} connections per broker (Dekaf only)");
        Console.WriteLine(new string('-', 50));

        await using var kafka = await KafkaEnvironment.CreateAsync(options.Brokers).ConfigureAwait(false);

        var producerTopic = $"stress-producer-{Guid.NewGuid():N}";
        var consumerTopic = $"stress-consumer-{Guid.NewGuid():N}";
        string? transactionalTopic = null;

        var replicationFactor = Math.Min(options.Brokers, 3);
        var replayTopicConfigs = new Dictionary<string, string>
        {
            ["retention.ms"] = "-1",
            ["retention.bytes"] = "-1"
        };
        await kafka.CreateTopicAsync(producerTopic, options.Partitions, replicationFactor).ConfigureAwait(false);
        if (options.Scenario is "all" or "producer-transactional")
        {
            transactionalTopic = $"stress-transactional-{Guid.NewGuid():N}";
            await kafka.CreateTopicAsync(
                transactionalTopic,
                options.Partitions,
                replicationFactor,
                replayTopicConfigs).ConfigureAwait(false);
        }
        // Transaction verification reads from earliest after the workload, while consumer
        // scenarios replay their seeded data. Broker retention must not delete either data set.
        await kafka.CreateTopicAsync(
            consumerTopic,
            options.Partitions,
            replicationFactor,
            replayTopicConfigs).ConfigureAwait(false);

        if (options.Scenario is "consumer" or "consumer-batch" or "consumer-raw" or "consumer-raw-batch" or "all")
        {
            await SeedConsumerTopicAsync(kafka.BootstrapServers, consumerTopic, options).ConfigureAwait(false);
        }

        var scenarios = GetScenarios(options);
        var results = new List<StressTestResult>();
        var runStartedAt = DateTime.UtcNow;

        string ResolveScenarioTopic(string scenarioName)
        {
            if (scenarioName.Equals("producer-transactional", StringComparison.OrdinalIgnoreCase))
                return transactionalTopic ?? throw new InvalidOperationException("Transactional topic was not created.");

            return scenarioName.StartsWith("producer", StringComparison.OrdinalIgnoreCase)
                ? producerTopic
                : consumerTopic;
        }

        StressTestOptions BuildTestOptions(string scenarioName, int connectionsPerBroker) => new()
        {
            BootstrapServers = kafka.BootstrapServers,
            Topic = ResolveScenarioTopic(scenarioName),
            DurationMinutes = options.DurationMinutes,
            MessageSizeBytes = options.MessageSizeBytes,
            Partitions = options.Partitions,
            LingerMs = options.LingerMs,
            BatchSize = options.BatchSize,
            Compression = options.Compression,
            BrokerCount = options.Brokers,
            ConnectionsPerBroker = connectionsPerBroker,
            EnableProducerDeliveryDiagnostics = options.EnableProducerDeliveryDiagnostics
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
                var connectionsPerBroker = scenario.Name.Equals("producer-transactional", StringComparison.OrdinalIgnoreCase)
                    ? 1
                    : options.ConnectionsPerBroker;
                var connectionLabel = connectionsPerBroker > 1 ? $" ({connectionsPerBroker}conn)" : "";
                Console.WriteLine();
                Console.WriteLine($"=== Running: {scenario.Client} {scenario.Name}{connectionLabel} ===");

                var result = await scenario.RunAsync(
                    BuildTestOptions(scenario.Name, connectionsPerBroker),
                    CancellationToken.None).ConfigureAwait(false);
                if (connectionsPerBroker > 1)
                    result.Client = $"Dekaf ({connectionsPerBroker}conn)";
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
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

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
    ///   errors for non-transactional runs; committed minus delivered for transactional
    ///   runs, where aborted records are intentionally invisible to read-committed consumers.
    /// - Duplicate delivery in idempotent scenarios (delivered exceeding accepted), which
    ///   means broker-side deduplication of retries is broken. Non-idempotent scenarios
    ///   skip this check because retry duplicates are legitimate there.
    /// - A run that ended measurably earlier than its configured duration (a swallowed
    ///   cancellation or crashed loop would otherwise pass with a fraction of the load).
    /// - A sustained mid-run stall (see <see cref="StallThresholdSeconds"/>).
    /// - Task exceptions nobody observed, surfaced after a final finalizer sweep.
    /// Results are already saved at this point; the non-zero exit only fails the CI job.
    /// </summary>
    internal static bool CheckForFailures(List<StressTestResult> results)
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
            var transactionVerification = result.TransactionVerification;
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
                var expectedDelivered = transactionVerification?.CommittedMessages ?? accepted;
                lost = Math.Max(0, expectedDelivered - delivered - deliveryErrors);
                if (lost > 0)
                {
                    reasons.Add(transactionVerification is null
                        ? $"{lost:N0} undelivered messages"
                        : $"{lost:N0} committed messages undelivered");
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

            if (transactionVerification is { IsSuccessful: false })
            {
                reasons.Add("transaction verification failed");
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

            if (transactionVerification is { IsSuccessful: false })
            {
                PrintTransactionVerificationFailure(transactionVerification);
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

    private static void PrintTransactionVerificationFailure(TransactionVerificationSnapshot verification)
    {
        Console.WriteLine(
            "    Transaction verification: " +
            $"accepted={verification.AcceptedMessages:N0}, " +
            $"committed={verification.CommittedMessages:N0}, " +
            $"aborted={verification.AbortedMessages:N0}, " +
            $"delivered={verification.DeliveredMessages:N0}, " +
            $"duplicates={verification.DuplicateMessages:N0}, " +
            $"shortfall={verification.ShortfallMessages:N0}, " +
            $"leakedAborted={verification.LeakedAbortedMessages:N0}, " +
            $"unexpected={verification.UnexpectedMessages:N0}, " +
            $"missingSentinels={verification.MissingSentinelPartitions:N0}");

        foreach (var sample in verification.FailureSamples)
        {
            Console.WriteLine($"      - {sample}");
        }
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
        var scenarios = CreateAllScenarios()
            .Where(s => options.Scenario == "all" || s.Name.Equals(options.Scenario, StringComparison.OrdinalIgnoreCase))
            .Where(s => options.Client == "all" || s.Client.Equals(options.Client, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return ApplyClientOrder(scenarios, options.Client);
    }

    internal static IReadOnlyList<IStressTestScenario> CreateAllScenarios() =>
        [
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
            new TransactionalProducerStressTest(),
            new ConsumerStressTest(),
            new ConsumerBatchStressTest(),
            new ConsumerRawStressTest(),
            new ConsumerRawBatchStressTest(),
            new ConfluentConsumerStressTest()
        ];

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

        var jsonFiles = Directory.GetFiles(options.InputPath, "*.json");
        if (jsonFiles.Length == 0)
        {
            Console.WriteLine("No JSON result files found");
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
                case "--help":
                case "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;
            }
        }

        return options;
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
              --scenario <name>       Run specific scenario: producer, producer-idempotent, producer-acks-all, producer-async, producer-async-idempotent, producer-transactional, consumer, consumer-batch, consumer-raw, consumer-raw-batch, all (default: all)
              --client <name>         Run specific client: dekaf, confluent, all (default: all)
              --output <path>         Output directory for results (default: ./results)
              --partitions <count>    Number of topic partitions (default: 6)
              --linger-ms <ms>        Producer linger time (default: 5)
              --batch-size <bytes>    Producer batch size (default: 1048576)
              --compression <type>   Compression type: none, lz4, snappy, zstd (default: none)
              --brokers <count>      Number of Kafka brokers (default: 1, use 3 for multi-broker)
              --connections-per-broker <n>  TCP connections per broker (default: 1, pass 3 for multi-connection comparison)
              --seed-messages <count> Messages pre-seeded into the consumer topic (default: 2000000)
              --producer-delivery-diagnostics  Capture Dekaf producer delivery diagnostics on message-loss failures
              report --input <path>   Generate report from existing results

            Environment Variables:
              KAFKA_BOOTSTRAP_SERVERS - Use external Kafka instead of Testcontainers
              STRESS_CLIENT_ORDER      - For --client all, run paired clients as dekaf-first or confluent-first
              STRESS_BROKER_CPUSET    - Pin Testcontainers brokers to CPU cores (e.g. "0-5") so the client keeps dedicated cores
              STRESS_BROKER_TMPFS     - Mount broker log dirs on tmpfs of this size (e.g. "6g") so disk I/O never caps ingestion

            Examples:
              dotnet run -c Release -- --duration 15 --message-size 1000
              dotnet run -c Release -- --scenario producer --client dekaf --duration 5
              dotnet run -c Release -- --scenario producer-transactional --client dekaf --duration 15
              dotnet run -c Release -- report --input ./results
            """);
    }

    private sealed class CliOptions
    {
        public bool IsReport { get; set; }
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
    }
}
