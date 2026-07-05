using System.Text;

namespace Dekaf.StressTests.Reporting;

internal static class MarkdownReporter
{
    public static string Generate(StressTestResults results)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Dekaf Stress Test Results");
        sb.AppendLine();
        sb.AppendLine($"**Run Date:** {results.RunStartedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Machine:** {results.MachineName} ({results.ProcessorCount} processors)");
        sb.AppendLine($"**Total Duration:** {(results.RunCompletedAtUtc - results.RunStartedAtUtc).TotalMinutes:F1} minutes");
        sb.AppendLine();

        // Group by scenario and broker count, then output throughput + latency + GC
        // together per scenario so related data is easy to compare side-by-side.
        var scenarioGroups = GroupByScenarioAndBrokers(results.Results).ToList();

        foreach (var group in scenarioGroups)
        {
            var groupResults = group.ToList();
            var title = FormatGroupTitle(FormatScenarioTitle(group.Key.Scenario), group.Key.BrokerCount);
            var label = FormatGroupTitle(FormatScenarioLabel(group.Key.Scenario), group.Key.BrokerCount);

            GenerateThroughputTable(sb, title, groupResults);

            var resultsWithLatency = groupResults.Where(r => r.Latency is not null).ToList();
            if (resultsWithLatency.Count > 0)
                GenerateLatencyTable(sb, resultsWithLatency, $"Latency - {label}");

            GenerateGcTable(sb, groupResults, $"GC Statistics - {label}");
        }

        if (results.Results.Any(r => r.Client.Equals("Confluent", StringComparison.OrdinalIgnoreCase)))
        {
            sb.AppendLine("*Confluent.Kafka uses native librdkafka; .NET GC allocation counters exclude unmanaged allocations.*");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static IOrderedEnumerable<IGrouping<(string Scenario, int BrokerCount), StressTestResult>>
        GroupByScenarioAndBrokers(IEnumerable<StressTestResult> results) =>
        results
            .GroupBy(r => (r.Scenario, r.BrokerCount))
            .OrderBy(g => g.Key.Scenario)
            .ThenBy(g => g.Key.BrokerCount);

    private static string FormatGroupTitle(string title, int brokerCount) =>
        brokerCount > 1 ? $"{title}, {brokerCount} Brokers" : title;

    private static int GetClientColumnWidth(List<StressTestResult> results) =>
        Math.Max(9, results.Max(r => r.Client.Length));

    private static void GenerateThroughputTable(StringBuilder sb, string title, List<StressTestResult> results)
    {
        var messageSizes = results.Select(r => r.MessageSizeBytes).Distinct().ToList();
        var durationMinutes = results.Select(r => r.DurationMinutes).Distinct().FirstOrDefault();
        var clientWidth = GetClientColumnWidth(results);

        foreach (var messageSize in messageSizes)
        {
            var sizeResults = results.Where(r => r.MessageSizeBytes == messageSize).ToList();
            if (sizeResults.Count == 0)
            {
                continue;
            }

            var messageSizeKb = messageSize >= 1024 ? $"{messageSize / 1024.0:F1}KB" : $"{messageSize}B";
            sb.AppendLine($"## {title} ({durationMinutes} minutes, {messageSizeKb} messages)");
            sb.AppendLine();
            sb.AppendLine($"| {"Client".PadRight(clientWidth)} | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used | Ratio |");
            sb.AppendLine($"|{new string('-', clientWidth + 2)}|--------------|--------|----------------|--------|------------|------------|-------|");

            var baseline = sizeResults
                .Where(r => r.Client.Equals("Confluent", StringComparison.OrdinalIgnoreCase))
                .Select(r => r.EffectiveMessagesPerSecond)
                .FirstOrDefault();

            if (baseline == 0)
            {
                baseline = sizeResults.Min(r => r.EffectiveMessagesPerSecond);
            }

            foreach (var result in sizeResults.OrderByDescending(r => r.EffectiveMessagesPerSecond))
            {
                var rate = result.EffectiveMessagesPerSecond;
                var ratio = baseline > 0 ? rate / baseline : 1.0;
                var accepted = result.AcceptedMessagesPerSecond is { } acceptedRate ? acceptedRate.ToString("N0") : "-";
                var cpuPerMessage = result.CpuMicrosPerMessage is { } cpu ? $"{cpu:F2}" : "-";
                var coresUsed = result.AverageCoresUsed is { } cores ? $"{cores:F2}" : "-";
                sb.AppendLine($"| {result.Client.PadRight(clientWidth)} | {rate,12:N0} | {result.EffectiveMegabytesPerSecond,6:F2} | {accepted,14} | {result.Throughput.TotalErrors,6} | {cpuPerMessage,10} | {coresUsed,10} | {ratio:F2}x |");
            }

            sb.AppendLine();

            if (sizeResults.Any(r => r.DeliveredMessages is not null))
            {
                sb.AppendLine("*Messages/sec counts broker-confirmed deliveries (end-offset delta). " +
                    "Accepted msg/s is the client-side append rate — a large gap means messages were " +
                    "buffered or dropped without ever reaching the broker.*");
                sb.AppendLine();
            }
        }
    }

    private static void GenerateLatencyTable(StringBuilder sb, List<StressTestResult> results, string title = "Latency Percentiles")
    {
        var clientWidth = GetClientColumnWidth(results);

        sb.AppendLine($"## {title}");
        sb.AppendLine();
        sb.AppendLine($"| {"Client".PadRight(clientWidth)} | p50      | p95      | p99      | Max      |");
        sb.AppendLine($"|{new string('-', clientWidth + 2)}|----------|----------|----------|----------|");

        foreach (var result in results.OrderBy(r => r.Latency!.P50Us))
        {
            var latency = result.Latency!;
            sb.AppendLine($"| {result.Client.PadRight(clientWidth)} | {FormatLatencyUs(latency.P50Us)} | {FormatLatencyUs(latency.P95Us)} | {FormatLatencyUs(latency.P99Us)} | {FormatLatencyUs(latency.MaxUs)} |");
        }

        sb.AppendLine();
    }

    private static void GenerateGcTable(StringBuilder sb, List<StressTestResult> results, string title = "GC Statistics")
    {
        var clientWidth = GetClientColumnWidth(results);

        sb.AppendLine($"## {title}");
        sb.AppendLine();
        sb.AppendLine($"| {"Client".PadRight(clientWidth)} | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |");
        sb.AppendLine($"|{new string('-', clientWidth + 2)}|------|------|------|-----------------|-----------|");

        foreach (var result in results.OrderBy(r => r.Client))
        {
            var allocatedPerMessage = result.AllocatedBytesPerMessage is { } bytes
                ? FormatBytes((long)Math.Round(bytes))
                : "N/A";
            sb.AppendLine($"| {result.Client.PadRight(clientWidth)} | {result.GcStats.Gen0Collections,4} | {result.GcStats.Gen1Collections,4} | {result.GcStats.Gen2Collections,4} | {result.GcStats.FormatAllocatedBytes(),9} | {allocatedPerMessage,9} |");
        }

        sb.AppendLine();
    }

    private static string FormatScenarioTitle(string scenario) => scenario switch
    {
        "producer" => "Producer (Fire-and-Forget) Throughput",
        "producer-idempotent" => "Producer (Fire-and-Forget, Idempotent) Throughput",
        "producer-async" => "Producer (Async) Throughput",
        "producer-async-idempotent" => "Producer (Async, Idempotent) Throughput",
        "consumer" => "Consumer Throughput",
        "consumer-batch" => "Consumer (Batch) Throughput",
        "consumer-raw" => "Consumer (Raw Bytes) Throughput",
        "consumer-raw-batch" => "Consumer (Raw Batch) Throughput",
        _ => $"{scenario} Throughput"
    };

    private static string FormatScenarioLabel(string scenario) => scenario switch
    {
        "producer" => "Fire-and-Forget",
        "producer-idempotent" => "Fire-and-Forget (Idempotent)",
        "producer-async" => "Async",
        "producer-async-idempotent" => "Async (Idempotent)",
        "consumer" => "Consumer",
        "consumer-batch" => "Consumer (Batch)",
        "consumer-raw" => "Consumer (Raw Bytes)",
        "consumer-raw-batch" => "Consumer (Raw Batch)",
        _ => scenario
    };

    private static string FormatBytes(long numBytes) => numBytes switch
    {
        < 1024 => $"{numBytes} B",
        < 1024 * 1024 => $"{numBytes / 1024.0:F2} KB",
        < 1024 * 1024 * 1024 => $"{numBytes / (1024.0 * 1024):F2} MB",
        _ => $"{numBytes / (1024.0 * 1024 * 1024):F2} GB"
    };

    private static string FormatLatencyUs(double us)
    {
        var formatted = us switch
        {
            < 1 => $"{us:F3}\u03bcs",
            < 10 => $"{us:F2}\u03bcs",
            < 1000 => $"{us:F0}\u03bcs",
            < 1_000_000 => $"{us / 1000.0:F2}ms",
            _ => $"{us / 1_000_000.0:F1}s"
        };

        return formatted.PadRight(9);
    }

    public static async Task WriteToFileAsync(StressTestResults results, string filePath)
    {
        var markdown = Generate(results);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(filePath, markdown).ConfigureAwait(false);
    }

    public static async Task AppendToGitHubSummaryAsync(StressTestResults results)
    {
        var summaryPath = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
        if (string.IsNullOrEmpty(summaryPath))
        {
            Console.WriteLine("GITHUB_STEP_SUMMARY not set, skipping summary output");
            return;
        }

        var markdown = Generate(results);
        await File.AppendAllTextAsync(summaryPath, markdown).ConfigureAwait(false);
        Console.WriteLine($"Appended results to GitHub summary: {summaryPath}");
    }
}
