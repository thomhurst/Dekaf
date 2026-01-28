using System.Runtime.CompilerServices;
using Dekaf.Producer;
using Dekaf.StressTests.Infrastructure;
using Dekaf.StressTests.Reporting;
using Dekaf.StressTests.Scenarios;
using DekafLib = Dekaf;

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
///   --scenario &lt;name&gt;       Run specific scenario: producer, consumer, all (default: all)
///   --client &lt;name&gt;         Run specific client: dekaf, confluent, all (default: all)
///   --output &lt;path&gt;         Output directory for results (default: ./results)
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
    private static readonly string[] PreAllocatedKeys = CreatePreAllocatedKeys(10_000);

    public static async Task<int> Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Console.WriteLine($"UNHANDLED EXCEPTION: {e.ExceptionObject}");
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
        Console.WriteLine(new string('-', 50));

        await using var kafka = await KafkaEnvironment.CreateAsync().ConfigureAwait(false);

        var producerTopic = $"stress-producer-{Guid.NewGuid():N}";
        var consumerTopic = $"stress-consumer-{Guid.NewGuid():N}";

        await kafka.CreateTopicAsync(producerTopic, options.Partitions).ConfigureAwait(false);
        await kafka.CreateTopicAsync(consumerTopic, options.Partitions).ConfigureAwait(false);

        if (options.Scenario is "consumer" or "all")
        {
            await SeedConsumerTopicAsync(kafka.BootstrapServers, consumerTopic, options).ConfigureAwait(false);
        }

        var scenarios = GetScenarios(options);
        var results = new List<StressTestResult>();
        var runStartedAt = DateTime.UtcNow;

        foreach (var scenario in scenarios)
        {
            Console.WriteLine();
            Console.WriteLine($"=== Running: {scenario.Client} {scenario.Name} ===");

            var testOptions = new StressTestOptions
            {
                BootstrapServers = kafka.BootstrapServers,
                Topic = scenario.Name == "producer" ? producerTopic : consumerTopic,
                DurationMinutes = options.DurationMinutes,
                MessageSizeBytes = options.MessageSizeBytes,
                Partitions = options.Partitions,
                LingerMs = options.LingerMs,
                BatchSize = options.BatchSize
            };

            var result = await scenario.RunAsync(testOptions, CancellationToken.None).ConfigureAwait(false);
            results.Add(result);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
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

        var jsonPath = Path.Combine(outputDir, $"stress-test-results-{runStartedAt:yyyyMMdd-HHmmss}.json");
        var mdPath = Path.Combine(outputDir, $"stress-test-results-{runStartedAt:yyyyMMdd-HHmmss}.md");

        await allResults.SaveAsync(jsonPath).ConfigureAwait(false);
        await MarkdownReporter.WriteToFileAsync(allResults, mdPath).ConfigureAwait(false);
        await MarkdownReporter.AppendToGitHubSummaryAsync(allResults).ConfigureAwait(false);

        Console.WriteLine();
        Console.WriteLine($"Results saved to: {jsonPath}");
        Console.WriteLine($"Markdown report: {mdPath}");

        Console.WriteLine();
        Console.WriteLine(MarkdownReporter.Generate(allResults));

        return 0;
    }

    private static async Task SeedConsumerTopicAsync(string bootstrapServers, string topic, CliOptions options)
    {
        Console.WriteLine($"Seeding consumer topic with messages...");

        var messageValue = new string('x', options.MessageSizeBytes);
        // Seed 500K messages - enough to test consumer throughput without excessive disk/time
        var totalMessages = 500_000;

        await using var producer = DekafLib.Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(bootstrapServers)
            .WithClientId("stress-seeder")
            .WithAcks(Acks.Leader)
            .WithLingerMs(5)
            .WithBatchSize(16384)
            .Build();

        var batchSize = 10_000;
        var batches = totalMessages / batchSize;

        for (var batch = 0; batch < batches; batch++)
        {
            for (var i = 0; i < batchSize; i++)
            {
                var messageIndex = batch * batchSize + i;
                producer.Send(topic, GetKey(messageIndex), messageValue);
            }

            if (batch % 10 == 0)
            {
                await producer.FlushAsync(CancellationToken.None).ConfigureAwait(false);
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
            new ConsumerStressTest(),
            new ConfluentConsumerStressTest()
        };

        return allScenarios
            .Where(s => options.Scenario == "all" || s.Name.Equals(options.Scenario, StringComparison.OrdinalIgnoreCase))
            .Where(s => options.Client == "all" || s.Client.Equals(options.Client, StringComparison.OrdinalIgnoreCase))
            .ToList();
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
              --scenario <name>       Run specific scenario: producer, consumer, all (default: all)
              --client <name>         Run specific client: dekaf, confluent, all (default: all)
              --output <path>         Output directory for results (default: ./results)
              --partitions <count>    Number of topic partitions (default: 6)
              --linger-ms <ms>        Producer linger time (default: 5)
              --batch-size <bytes>    Producer batch size (default: 16384)
              report --input <path>   Generate report from existing results

            Environment Variables:
              KAFKA_BOOTSTRAP_SERVERS - Use external Kafka instead of Testcontainers

            Examples:
              dotnet run -c Release -- --duration 15 --message-size 1000
              dotnet run -c Release -- --scenario producer --client dekaf --duration 5
              dotnet run -c Release -- report --input ./results
            """);
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
        public int BatchSize { get; set; } = 16384;
    }
}
