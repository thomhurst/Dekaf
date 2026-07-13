using System.Text;
using Dekaf.StressTests.Metrics;

namespace Dekaf.StressTests.Reporting;

internal static class MarkdownReporter
{
    private const int MaxConnectionScaleTimelineRows = 100;

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
            GenerateConnectionScaleTimeline(sb, groupResults, label);
            GenerateLatencyOutlierTimeline(sb, groupResults, label);

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

            var roundTripResults = groupResults
                .Where(r => r.RoundTripValidation is not null)
                .ToList();
            if (roundTripResults.Count > 0)
                GenerateRoundTripValidationTable(sb, roundTripResults, $"Round-Trip Validation - {label}");

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
            sb.AppendLine($"| {"Client".PadRight(clientWidth)} | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used | Comparison Ratio |");
            sb.AppendLine($"|{new string('-', clientWidth + 2)}|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|------------------|");

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
                var drift = result.IntraRunDriftPercent is { } driftPercent ? $"{driftPercent:+0.0;-0.0;0.0}%" : "-";
                var slope = result.ThroughputSlopePercentPerMinute is { } slopePercent ? $"{slopePercent:+0.00;-0.00;0.00}%" : "-";
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
                sb.AppendLine($"| {result.Client.PadRight(clientWidth)} | {cpuPerMessage,10} | {rate,12:N0} | {median,12} | {drift,7} | {slope,11} | {result.EffectiveMegabytesPerSecond,6:F2} | {accepted,14} | {errors,6} | {coresUsed,10} | {ratio:F2}x |");
            }

            sb.AppendLine();

            if (sizeResults.Any(r => r.MedianIntervalMessagesPerSecond is not null))
            {
                sb.AppendLine("*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*");
                sb.AppendLine();
                sb.AppendLine("*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*");
                sb.AppendLine();
            }

            if (sizeResults.Any(r => r.IntraRunDriftPercent is not null))
            {
                sb.AppendLine($"*Drift compares last-third with first-third average throughput. Runs of at least three minutes use one-minute trend windows; shorter runs use raw intervals. Slope is the normalized least-squares trend; steady-state below {StressTestResult.SteadyStatePeakThreshold:P0} of peak or slope below {StressTestResult.SlopePercentPerMinuteThreshold:F0}%/min fails the regression gate.*");
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

    private static void GenerateConnectionScaleTimeline(
        StringBuilder sb,
        List<StressTestResult> results,
        string label)
    {
        var rows = results
            .SelectMany(result => (result.ProducerDeliveryDiagnostics?.ConnectionScaleEvents ?? [])
                .Select(scaleEvent => (Result: result, Event: scaleEvent)))
            .OrderBy(row => row.Event.OccurredAtUtc)
            .ToList();
        if (rows.Count == 0)
            return;

        var displayedRows = SampleEvenly(rows, MaxConnectionScaleTimelineRows);
        var omittedRows = rows.Count - displayedRows.Count;

        sb.AppendLine($"## Connection Scale Timeline - {label}");
        sb.AppendLine();
        sb.AppendLine("| Client | Event UTC | Broker | Connections | Buffer | Pressure (buffer/send) | Observations / duration | Nearest throughput sample |");
        sb.AppendLine("|--------|-----------|-------:|-------------|-------:|------------------------|-------------------------|---------------------------|");
        foreach (var row in displayedRows)
        {
            var nearest = row.Result.Throughput.IntervalSamples
                .MinBy(sample => Math.Abs((sample.CapturedAtUtc - row.Event.OccurredAtUtc).TotalMilliseconds));
            var throughput = nearest is null
                ? "-"
                : $"{nearest.ElapsedSeconds:F1}s / {nearest.MessagesPerSecond:N0} msg/s";
            sb.AppendLine(
                $"| {row.Result.Client} | {row.Event.OccurredAtUtc:HH:mm:ss.fff} | " +
                $"{row.Event.BrokerId} | {row.Event.OldConnectionCount}→{row.Event.NewConnectionCount} | " +
                $"{row.Event.BufferUtilization * 100:F0}% | " +
                $"{row.Event.BufferPressureDelta:N0}/{row.Event.SendLoopPressureDelta:N0} | " +
                $"{row.Event.ObservationCount:N0} / {row.Event.ObservedDurationMs / 1000.0:F1}s | {throughput} |");
        }
        if (omittedRows > 0)
            sb.AppendLine($"*{omittedRows:N0} scale event(s) omitted; rows sampled across the full timeline.*");
        sb.AppendLine();
    }

    private static List<T> SampleEvenly<T>(IReadOnlyList<T> rows, int maximumRows)
    {
        if (rows.Count <= maximumRows)
            return [.. rows];

        var sampled = new List<T>(maximumRows);
        for (var i = 0; i < maximumRows; i++)
        {
            var index = (int)Math.Round(i * (rows.Count - 1d) / (maximumRows - 1d));
            sampled.Add(rows[index]);
        }

        return sampled;
    }

    private static double ComparisonMessagesPerSecond(StressTestResult result) =>
        result.MedianIntervalMessagesPerSecond ?? result.EffectiveMessagesPerSecond;

    private static void GenerateLatencyOutlierTimeline(
        StringBuilder sb,
        List<StressTestResult> results,
        string label)
    {
        var rows = results
            .SelectMany(result => (result.Latency?.OutlierSamples ?? [])
                .Select(sample => (Result: result, Sample: sample)))
            .OrderBy(row => row.Sample.StartedAtUtc)
            .ToList();
        if (rows.Count == 0)
            return;

        sb.AppendLine($"## Delivery Latency Outliers - {label}");
        sb.AppendLine();
        sb.AppendLine("| Client | Message | Started UTC | Latency | Correlated owner | Scale events in stall | Throughput interval | GC interval delta |");
        sb.AppendLine("|--------|--------:|-------------|--------:|------------------|-----------------------|---------------------|-------------------|");
        foreach (var row in rows)
        {
            var scaleEvents = (row.Result.ProducerDeliveryDiagnostics?.ConnectionScaleEvents ?? [])
                .Where(scaleEvent =>
                    scaleEvent.OccurredAtUtc >= row.Sample.StartedAtUtc &&
                    scaleEvent.OccurredAtUtc <= row.Sample.CompletedAtUtc)
                .ToList();
            var samples = row.Result.Throughput.IntervalSamples;
            var throughputSample = samples.FirstOrDefault(sample => sample.CapturedAtUtc >= row.Sample.CompletedAtUtc)
                ?? samples.MinBy(sample => Math.Abs((sample.CapturedAtUtc - row.Sample.CompletedAtUtc).TotalMilliseconds));
            var baselineSample = samples.LastOrDefault(sample => sample.CapturedAtUtc <= row.Sample.StartedAtUtc);
            var gen2Delta = throughputSample is null
                ? 0
                : throughputSample.Gen2Collections - (baselineSample?.Gen2Collections ?? 0);
            var pauseDeltaMs = throughputSample is null
                ? 0
                : Math.Max(0, throughputSample.GcPauseDurationMs - (baselineSample?.GcPauseDurationMs ?? 0));
            var owner = ClassifyLatencyOutlier(
                scaleEvents.Count,
                gen2Delta,
                pauseDeltaMs,
                throughputSample,
                row.Result.MedianIntervalMessagesPerSecond ?? row.Result.Throughput.AverageMessagesPerSecond);
            var scaleSummary = scaleEvents.Count == 0
                ? "-"
                : string.Join(", ", scaleEvents.Select(scaleEvent =>
                    $"{scaleEvent.BrokerId}:{scaleEvent.OldConnectionCount}→{scaleEvent.NewConnectionCount}"));
            var throughputSummary = throughputSample is null
                ? "-"
                : $"{throughputSample.ElapsedSeconds:F1}s / {throughputSample.MessagesPerSecond:N0} msg/s";
            var gcSummary = throughputSample is null
                ? "-"
                : $"Gen2 +{gen2Delta:N0} / pause +{pauseDeltaMs:F1}ms";
            var messageIndex = row.Sample.MessageIndex?.ToString("N0") ?? "-";

            sb.AppendLine(
                $"| {row.Result.Client} | {messageIndex} | {row.Sample.StartedAtUtc:HH:mm:ss.fff} | " +
                $"{FormatLatencyUs(row.Sample.LatencyUs).Trim()} | {owner} | {scaleSummary} | " +
                $"{throughputSummary} | {gcSummary} |");
        }

        sb.AppendLine();
        var dropped = results.Sum(result => result.Latency?.DroppedOutlierSamples ?? 0);
        if (dropped > 0)
        {
            sb.AppendLine($"*{dropped:N0} additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*");
            sb.AppendLine();
        }
    }

    private static string ClassifyLatencyOutlier(
        int scaleEventCount,
        int gen2Delta,
        double pauseDeltaMs,
        ThroughputIntervalSample? throughputSample,
        double clientMessagesPerSecond)
    {
        if (scaleEventCount > 0)
            return "connection transition";
        if (gen2Delta > 0 || pauseDeltaMs >= 10)
            return "GC pause";
        if (throughputSample is not null &&
            throughputSample.MessagesPerSecond < clientMessagesPerSecond * 0.5)
        {
            return "throughput collapse";
        }

        return "broker/backlog (no scale or GC event)";
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

    private static void GenerateTransactionVerificationTable(
        StringBuilder sb,
        List<StressTestResult> results,
        string title)
    {
        var clientWidth = GetClientColumnWidth(results);

        sb.AppendLine($"## {title}");
        sb.AppendLine();
        sb.AppendLine($"| {"Client".PadRight(clientWidth)} | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Sentinel commit | Status |");
        sb.AppendLine($"|{new string('-', clientWidth + 2)}|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|-----------------|--------|");

        foreach (var result in results.OrderBy(r => r.Client))
        {
            var verification = result.TransactionVerification!;
            var status = verification.IsSuccessful ? "PASS" : "FAIL";
            sb.AppendLine(
                $"| {result.Client.PadRight(clientWidth)} | {verification.AcceptedMessages,8:N0} | " +
                $"{verification.CommittedMessages,9:N0} | {verification.AbortedMessages,7:N0} | " +
                $"{verification.DeliveredMessages,9:N0} | {verification.DuplicateMessages,10:N0} | " +
                $"{verification.ShortfallMessages,9:N0} | {verification.LeakedAbortedMessages,13:N0} | " +
                $"{verification.UnexpectedMessages,10:N0} | {verification.MissingSentinelPartitions,17:N0} | " +
                $"{(verification.SentinelCommitFailed ? "FAILED" : "OK"),15} | {status} |");
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

    private static void GenerateRoundTripValidationTable(
        StringBuilder sb,
        List<StressTestResult> results,
        string title)
    {
        var clientWidth = GetClientColumnWidth(results);

        sb.AppendLine($"## {title}");
        sb.AppendLine();
        sb.AppendLine($"| {"Client".PadRight(clientWidth)} | Produce msg/s | Consume msg/s | Expected | Consumed | Missing | Duplicates | Corrupt | Out of Order | Wrong Partition | Unexpected | Timed Out | Result |");
        sb.AppendLine($"|{new string('-', clientWidth + 2)}|--------------:|--------------:|----------|----------|---------|------------|---------|--------------|-----------------|------------|-----------|--------|");

        foreach (var result in results.OrderBy(r => r.Client))
        {
            var validation = result.RoundTripValidation!;
            var produceRate = result.RoundTripPhases?.ProduceMessagesPerSecond;
            var consumeRate = result.RoundTripPhases?.ConsumeMessagesPerSecond;
            sb.AppendLine(
                $"| {result.Client.PadRight(clientWidth)} | {produceRate?.ToString("N0") ?? "-",13} | " +
                $"{consumeRate?.ToString("N0") ?? "-",13} | {validation.ExpectedMessages,8:N0} | " +
                $"{validation.ConsumedMessages,8:N0} | {validation.MissingMessages,7:N0} | " +
                $"{validation.DuplicateMessages,10:N0} | {validation.CorruptMessages,7:N0} | " +
                $"{validation.OutOfOrderMessages,12:N0} | {validation.MispartitionedMessages,15:N0} | " +
                $"{validation.UnexpectedMessages,10:N0} | {(validation.TimedOut ? "yes" : "no"),9} | " +
                $"{(validation.IsSuccess ? "PASS" : "FAIL")} |");
        }

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
        "producer-roundtrip" => "Producer → Consumer Round-Trip Throughput",
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
        "producer-roundtrip" => "Producer → Consumer Round-Trip",
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
