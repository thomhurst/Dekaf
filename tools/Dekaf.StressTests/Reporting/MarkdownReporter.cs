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

            var transactionalResults = groupResults
                .Where(r => r.TransactionVerification is not null)
                .ToList();
            if (transactionalResults.Count > 0)
                GenerateTransactionVerificationTable(sb, transactionalResults, $"Transaction Verification - {label}");

            var resultsWithLatency = groupResults.Where(r => r.Latency is not null).ToList();
            if (resultsWithLatency.Count > 0)
                GenerateLatencyTable(sb, resultsWithLatency, $"Latency - {label}");

            GenerateGcTable(sb, groupResults, $"GC Statistics - {label}");

            var resultsWithResourceTrends = groupResults.Where(r => r.ResourceTrend is not null).ToList();
            if (resultsWithResourceTrends.Count > 0)
                GenerateResourceTrendTable(sb, resultsWithResourceTrends, $"Resource Trends - {label}");

            var resultsWithErrorSamples = groupResults
                .Where(r => r.Throughput.ErrorSamples.Count > 0)
                .ToList();
            if (resultsWithErrorSamples.Count > 0)
                GenerateErrorSamplesTable(sb, resultsWithErrorSamples, $"Error Samples - {label}");
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
            sb.AppendLine($"| {"Client".PadRight(clientWidth)} | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used | Comparison Ratio |");
            sb.AppendLine($"|{new string('-', clientWidth + 2)}|------------|--------------|--------------|--------|----------------|--------|------------|------------------|");

            var baseline = sizeResults
                .Where(r => r.Client.Equals("Confluent", StringComparison.OrdinalIgnoreCase))
                .Select(ComparisonMessagesPerSecond)
                .FirstOrDefault();

            if (baseline == 0)
            {
                baseline = sizeResults.Min(ComparisonMessagesPerSecond);
            }

            foreach (var result in OrderThroughputResults(sizeResults))
            {
                var rate = result.EffectiveMessagesPerSecond;
                var comparisonRate = ComparisonMessagesPerSecond(result);
                var ratio = baseline > 0 ? comparisonRate / baseline : 1.0;
                var median = result.MedianIntervalMessagesPerSecond is { } medianRate ? medianRate.ToString("N0") : "-";
                var accepted = result.AcceptedMessagesPerSecond is { } acceptedRate ? acceptedRate.ToString("N0") : "-";
                var cpuPerMessage = result.CpuMicrosPerMessage is { } cpu ? $"{cpu:F2}" : "-";
                var coresUsed = result.AverageCoresUsed is { } cores ? $"{cores:F2}" : "-";
                // Errors column includes broker-side delivery failures so a run that lost
                // accepted messages can never render a clean zero; the "dlv" suffix keeps
                // "client loop broke" distinguishable from "broker rejected accepted messages".
                var deliveryErrors = result.Throughput.TotalDeliveryErrors;
                var errors = deliveryErrors > 0
                    ? $"{result.Throughput.TotalErrors} (+{deliveryErrors} dlv)"
                    : result.Throughput.TotalErrors.ToString()!;
                sb.AppendLine($"| {result.Client.PadRight(clientWidth)} | {cpuPerMessage,10} | {rate,12:N0} | {median,12} | {result.EffectiveMegabytesPerSecond,6:F2} | {accepted,14} | {errors,6} | {coresUsed,10} | {ratio:F2}x |");
            }

            sb.AppendLine();

            if (sizeResults.Any(r => r.MedianIntervalMessagesPerSecond is not null))
            {
                sb.AppendLine("*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*");
                sb.AppendLine();
                sb.AppendLine("*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*");
                sb.AppendLine();
            }

            if (sizeResults.Any(r => r.DeliveredMessages is not null))
            {
                sb.AppendLine("*Messages/sec counts broker-confirmed deliveries (end-offset delta). " +
                    "Accepted msg/s is the client-side append rate — a large gap means messages were " +
                    "buffered or dropped without ever reaching the broker. Every producer run fails " +
                    "on an unexplained shortfall between accepted and delivered.*");
                sb.AppendLine();
            }
        }
    }

    private static IOrderedEnumerable<StressTestResult> OrderThroughputResults(List<StressTestResult> results) =>
        results
            .OrderByDescending(ComparisonMessagesPerSecond)
            .ThenBy(r => r.CpuMicrosPerMessage ?? double.MaxValue);

    private static double ComparisonMessagesPerSecond(StressTestResult result) =>
        result.MedianIntervalMessagesPerSecond ?? result.EffectiveMessagesPerSecond;

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

    private static void GenerateTransactionVerificationTable(
        StringBuilder sb,
        List<StressTestResult> results,
        string title)
    {
        var clientWidth = GetClientColumnWidth(results);

        sb.AppendLine($"## {title}");
        sb.AppendLine();
        sb.AppendLine($"| {"Client".PadRight(clientWidth)} | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |");
        sb.AppendLine($"|{new string('-', clientWidth + 2)}|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|");

        foreach (var result in results.OrderBy(r => r.Client))
        {
            var verification = result.TransactionVerification!;
            var status = verification.IsSuccessful ? "PASS" : "FAIL";
            sb.AppendLine(
                $"| {result.Client.PadRight(clientWidth)} | {verification.AcceptedMessages,8:N0} | " +
                $"{verification.CommittedMessages,9:N0} | {verification.AbortedMessages,7:N0} | " +
                $"{verification.DeliveredMessages,9:N0} | {verification.DuplicateMessages,10:N0} | " +
                $"{verification.ShortfallMessages,9:N0} | {verification.LeakedAbortedMessages,13:N0} | " +
                $"{verification.UnexpectedMessages,10:N0} | {verification.MissingSentinelPartitions,17:N0} | {status} |");
        }

        sb.AppendLine();
        var failureSamples = results
            .SelectMany(r => r.TransactionVerification!.FailureSamples.Select(sample => (r.Client, Sample: sample)))
            .ToList();
        if (failureSamples.Count == 0)
            return;

        sb.AppendLine("**Transaction verification failure samples:**");
        foreach (var (client, sample) in failureSamples)
            sb.AppendLine($"- {client}: {sample}");
        sb.AppendLine();
    }

    private static void GenerateErrorSamplesTable(StringBuilder sb, List<StressTestResult> results, string title)
    {
        var clientWidth = GetClientColumnWidth(results);

        sb.AppendLine($"## {title}");
        sb.AppendLine();
        sb.AppendLine($"| {"Client".PadRight(clientWidth)} | Error # | At | Message Index | Operation | Type | Message |");
        sb.AppendLine($"|{new string('-', clientWidth + 2)}|---------|----|---------------|-----------|------|---------|");

        foreach (var result in results.OrderBy(r => r.Client))
        {
            foreach (var sample in result.Throughput.ErrorSamples)
            {
                var messageIndex = sample.MessageIndex is { } index ? index.ToString("N0") : "-";
                var operation = EscapeTableCell(sample.Operation ?? "-");
                var type = EscapeTableCell(sample.ExceptionType);
                var message = EscapeTableCell(Truncate(sample.Message ?? "-", 180));

                sb.AppendLine(
                    $"| {result.Client.PadRight(clientWidth)} | {sample.ErrorNumber:N0} | " +
                    $"+{sample.ElapsedSeconds:F3}s | {messageIndex} | {operation} | {type} | {message} |");
            }
        }

        sb.AppendLine();
        sb.AppendLine("*Full exception details are serialized in the JSON results and printed in the failing CI log.*");
        sb.AppendLine();
    }

    private static void GenerateResourceTrendTable(StringBuilder sb, List<StressTestResult> results, string title)
    {
        var clientWidth = GetClientColumnWidth(results);

        sb.AppendLine($"## {title}");
        sb.AppendLine();
        sb.AppendLine($"| {"Client".PadRight(clientWidth)} | Samples | Working Set MiB/h | GC Heap MiB/h | LOH MiB/h | Produced %/h | Consumed %/h | Consumed | Status |");
        sb.AppendLine($"|{new string('-', clientWidth + 2)}|---------|-------------------|---------------|-----------|---------------|---------------|----------|--------|");

        foreach (var result in results.OrderBy(r => r.Client))
        {
            var trend = result.ResourceTrend!.Analysis;
            var consumed = result.ConsumedMessages is { } count ? count.ToString("N0") : "-";
            var status = trend.Passed ? "PASS" : "FAIL";
            sb.AppendLine(
                $"| {result.Client.PadRight(clientWidth)} | {trend.SampleCount,7:N0} | " +
                $"{trend.WorkingSetSlopeMibPerHour,17:N2} | {trend.GcHeapSlopeMibPerHour,13:N2} | " +
                $"{trend.LohSlopeMibPerHour,9:N2} | {trend.ProducedThroughputSlopePercentPerHour,13:N2} | " +
                $"{trend.ConsumedThroughputSlopePercentPerHour,13:N2} | {consumed,8} | {status} |");

            foreach (var failure in trend.Failures)
            {
                sb.AppendLine($"| {new string(' ', clientWidth)} |  |  |  |  |  |  |  | {EscapeTableCell(failure)} |");
            }
        }

        sb.AppendLine();
        sb.AppendLine("*Memory slopes use post-warmup linear regression. LOH is sampled separately; LOH collections occur with Gen2 collections.*");
        sb.AppendLine();
    }

    private static string FormatScenarioTitle(string scenario) => scenario switch
    {
        "producer" => "Producer (Fire-and-Forget) Throughput",
        "producer-idempotent" => "Producer (Fire-and-Forget, Idempotent) Throughput",
        "producer-acks-all" => "Producer (Acks All) Throughput",
        "producer-async" => "Producer (Async) Throughput",
        "producer-async-idempotent" => "Producer (Async, Idempotent) Throughput",
        "producer-transactional" => "Producer (Transactional EOS) Throughput",
        "consumer" => "Consumer Throughput",
        "consumer-batch" => "Consumer (Batch) Throughput",
        "consumer-raw" => "Consumer (Raw Bytes) Throughput",
        "consumer-raw-batch" => "Consumer (Raw Batch) Throughput",
        "soak" => "Mixed Produce/Consume Soak",
        _ => $"{scenario} Throughput"
    };

    private static string FormatScenarioLabel(string scenario) => scenario switch
    {
        "producer" => "Fire-and-Forget",
        "producer-idempotent" => "Fire-and-Forget (Idempotent)",
        "producer-acks-all" => "Acks All",
        "producer-async" => "Async",
        "producer-async-idempotent" => "Async (Idempotent)",
        "producer-transactional" => "Transactional EOS",
        "consumer" => "Consumer",
        "consumer-batch" => "Consumer (Batch)",
        "consumer-raw" => "Consumer (Raw Bytes)",
        "consumer-raw-batch" => "Consumer (Raw Batch)",
        "soak" => "Mixed Soak",
        _ => scenario
    };

    private static string FormatBytes(long numBytes) => numBytes switch
    {
        < 1024 => $"{numBytes} B",
        < 1024 * 1024 => $"{numBytes / 1024.0:F2} KB",
        < 1024 * 1024 * 1024 => $"{numBytes / (1024.0 * 1024):F2} MB",
        _ => $"{numBytes / (1024.0 * 1024 * 1024):F2} GB"
    };

    private static string EscapeTableCell(string value) =>
        value
            .Replace("\\", "\\\\")
            .Replace("|", "\\|")
            .Replace("\r\n", "<br>")
            .Replace("\n", "<br>")
            .Replace("\r", "<br>");

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : $"{value[..Math.Max(0, maxLength - 3)]}...";

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
