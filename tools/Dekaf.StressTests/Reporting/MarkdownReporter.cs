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

        var producerResults = results.Results
            .Where(r => r.Scenario.Contains("producer", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var consumerResults = results.Results
            .Where(r => r.Scenario.Contains("consumer", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (producerResults.Count > 0)
        {
            GenerateThroughputTable(sb, "Producer Throughput", producerResults);
        }

        if (consumerResults.Count > 0)
        {
            GenerateThroughputTable(sb, "Consumer Throughput", consumerResults);
        }

        var resultsWithLatency = results.Results.Where(r => r.Latency is not null).ToList();
        if (resultsWithLatency.Count > 0)
        {
            GenerateLatencyTable(sb, resultsWithLatency);
        }

        GenerateGcTable(sb, results.Results);

        return sb.ToString();
    }

    private static void GenerateThroughputTable(StringBuilder sb, string title, List<StressTestResult> results)
    {
        var messageSizes = results.Select(r => r.MessageSizeBytes).Distinct().ToList();
        var durationMinutes = results.Select(r => r.DurationMinutes).Distinct().FirstOrDefault();

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
            sb.AppendLine("| Client    | Messages/sec | MB/sec | Errors | Ratio |");
            sb.AppendLine("|-----------|--------------|--------|--------|-------|");

            var baseline = sizeResults
                .Where(r => r.Client.Equals("Confluent", StringComparison.OrdinalIgnoreCase))
                .Select(r => r.Throughput.AverageMessagesPerSecond)
                .FirstOrDefault();

            if (baseline == 0)
            {
                baseline = sizeResults.Min(r => r.Throughput.AverageMessagesPerSecond);
            }

            foreach (var result in sizeResults.OrderByDescending(r => r.Throughput.AverageMessagesPerSecond))
            {
                var ratio = baseline > 0 ? result.Throughput.AverageMessagesPerSecond / baseline : 1.0;
                sb.AppendLine($"| {result.Client,-9} | {result.Throughput.AverageMessagesPerSecond,12:N0} | {result.Throughput.AverageMegabytesPerSecond,6:F2} | {result.Throughput.TotalErrors,6} | {ratio:F2}x |");
            }

            sb.AppendLine();
        }
    }

    private static void GenerateLatencyTable(StringBuilder sb, List<StressTestResult> results)
    {
        sb.AppendLine("## Latency Percentiles");
        sb.AppendLine();
        sb.AppendLine("| Client    | p50    | p95    | p99    | Max    |");
        sb.AppendLine("|-----------|--------|--------|--------|--------|");

        foreach (var result in results.OrderBy(r => r.Latency!.P50Ms))
        {
            var latency = result.Latency!;
            sb.AppendLine($"| {result.Client,-9} | {FormatLatency(latency.P50Ms)} | {FormatLatency(latency.P95Ms)} | {FormatLatency(latency.P99Ms)} | {FormatLatency(latency.MaxMs)} |");
        }

        sb.AppendLine();
    }

    private static void GenerateGcTable(StringBuilder sb, List<StressTestResult> results)
    {
        sb.AppendLine("## GC Statistics");
        sb.AppendLine();
        sb.AppendLine("| Client    | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated |");
        sb.AppendLine("|-----------|----------|------|------|------|-----------------|");

        foreach (var result in results.OrderBy(r => r.Client).ThenBy(r => r.Scenario))
        {
            sb.AppendLine($"| {result.Client,-9} | {result.Scenario,-8} | {result.GcStats.Gen0Collections,4} | {result.GcStats.Gen1Collections,4} | {result.GcStats.Gen2Collections,4} | {result.GcStats.FormatAllocatedBytes(),9} |");
        }

        sb.AppendLine();
    }

    private static string FormatLatency(double ms)
    {
        return ms switch
        {
            < 1 => $"{ms:F2}ms",
            < 10 => $"{ms:F1}ms",
            < 1000 => $"{ms:F0}ms",
            _ => $"{ms / 1000:F1}s"
        };
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
