using System.Text.Json;
using System.Text.Json.Serialization;
using Dekaf.Producer;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Scenarios;

namespace Dekaf.StressTests.Reporting;

internal sealed class StressTestResult
{
    public required string Scenario { get; init; }
    public required string Client { get; set; }
    public required int DurationMinutes { get; init; }
    public required int MessageSizeBytes { get; init; }
    public required DateTime StartedAtUtc { get; init; }
    public required DateTime CompletedAtUtc { get; init; }
    public required ThroughputSnapshot Throughput { get; init; }
    public LatencySnapshot? Latency { get; init; }
    public required GcSnapshot GcStats { get; init; }
    public int BrokerCount { get; init; } = 1;
    public ProducerDeliveryDiagnosticsSnapshot? ProducerDeliveryDiagnostics { get; init; }
    public RoundTripValidationSnapshot? RoundTripValidation { get; init; }

    /// <summary>
    /// Whether a fixed message count, rather than <see cref="DurationMinutes"/>, defines
    /// successful scenario completion. Message-bounded runs may finish before the duration.
    /// </summary>
    public bool IsMessageBounded { get; init; }

    /// <summary>
    /// Whether the scenario's producer ran with idempotence enabled. Must mirror the
    /// producer configuration a few lines above where each scenario sets it. The failure
    /// policy in Program.CheckForFailures derives duplicate-delivery enforcement from
    /// this fact: idempotent runs fail when delivered exceeds accepted (broker-side
    /// retry deduplication broken); non-idempotent runs tolerate retry duplicates.
    /// </summary>
    public bool Idempotent { get; init; }
    public ResourceTrendSnapshot? ResourceTrend { get; init; }
    public long? ConsumedMessages { get; init; }

    public TransactionVerificationSnapshot? TransactionVerification { get; init; }

    /// <summary>
    /// Broker-confirmed message count, measured as the end-offset (high watermark)
    /// delta across all topic partitions between test start and end. Fire-and-forget
    /// scenarios count client-side appends in <see cref="Throughput"/>; when the client
    /// buffers faster than the broker accepts (or the broker degrades mid-run), accepted
    /// and delivered diverge — this is the honest throughput number.
    /// Non-transactional producer scenarios fail when delivered falls short of accepted
    /// minus recorded delivery errors. Transactional scenarios compare delivered records
    /// with committed records because aborted records are intentionally invisible.
    /// Null when the scenario doesn't measure it (consumers).
    /// </summary>
    public long? DeliveredMessages { get; init; }

    public double? DeliveredMessagesPerSecond =>
        DeliveredMessages is { } delivered && Throughput.ElapsedSeconds > 0
            ? delivered / Throughput.ElapsedSeconds
            : null;

    public double? DeliveredMegabytesPerSecond =>
        DeliveredMessagesPerSecond is { } rate
            ? rate * MessageSizeBytes / (1024.0 * 1024.0)
            : null;

    /// <summary>
    /// Headline throughput: the broker-confirmed delivered rate when the scenario
    /// measured it (producers), falling back to the client-side tracker average.
    /// Producer scenarios fail outright when the delivered count is unavailable
    /// (see StressTestHelpers.ComputeDelivered), so the fallback only ever applies
    /// to consumers. Serialized so both reporters (including the Python CI summary)
    /// read one field instead of re-deriving the selection policy.
    /// </summary>
    public double EffectiveMessagesPerSecond =>
        DeliveredMessagesPerSecond ?? Throughput.AverageMessagesPerSecond;

    public double EffectiveMegabytesPerSecond =>
        DeliveredMegabytesPerSecond ?? Throughput.AverageMegabytesPerSecond;

    /// <summary>
    /// Median of sampled client-side throughput intervals. This is less sensitive than
    /// the whole-run mean to a brief late-run stall, and lets reports show the steady
    /// state beside the end-to-end average.
    /// </summary>
    public double? MedianIntervalMessagesPerSecond =>
        GetMedian(Throughput.MessagesPerSecondSamples);

    /// <summary>
    /// Client-side append rate, reported only when it is distinct from the headline
    /// number (i.e. when delivered throughput was measured). Null means the headline
    /// already is the client-side rate.
    /// </summary>
    public double? AcceptedMessagesPerSecond =>
        DeliveredMessages is not null ? Throughput.AverageMessagesPerSecond : null;

    /// <summary>
    /// Process CPU time consumed during the measurement window. CPU cost per message
    /// differentiates client efficiency even when the broker caps throughput.
    /// The derived values below are serialized so downstream reports (including the
    /// Python CI summary) format them without re-deriving the math.
    /// </summary>
    public double? CpuTimeSeconds { get; init; }

    public double? CpuMicrosPerMessage =>
        CpuTimeSeconds is { } cpu && Throughput.TotalMessages > 0
            ? cpu * 1_000_000.0 / Throughput.TotalMessages
            : null;

    public double? AverageCoresUsed =>
        CpuTimeSeconds is { } cpu && Throughput.ElapsedSeconds > 0
            ? cpu / Throughput.ElapsedSeconds
            : null;

    public double? AllocatedBytesPerMessage =>
        GcStats.AllocatedBytes is { } allocated && Throughput.TotalMessages > 0
            ? (double)allocated / Throughput.TotalMessages
            : null;

    private static double? GetMedian(List<double> samples)
    {
        var sorted = samples
            .Where(double.IsFinite)
            .Order()
            .ToArray();

        return sorted.Length switch
        {
            0 => null,
            var count when count % 2 == 1 => sorted[count / 2],
            var count => (sorted[(count / 2) - 1] + sorted[count / 2]) / 2.0
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static StressTestResult? FromJson(string json) =>
        JsonSerializer.Deserialize<StressTestResult>(json, JsonOptions);
}

internal sealed class TransactionVerificationSnapshot
{
    public required long AcceptedMessages { get; init; }
    public required long CommittedMessages { get; init; }
    public required long AbortedMessages { get; init; }
    public required long DeliveredMessages { get; init; }
    public required long DuplicateMessages { get; init; }
    public required long ShortfallMessages { get; init; }
    public required long LeakedAbortedMessages { get; init; }
    public required long UnexpectedMessages { get; init; }
    public required int MissingSentinelPartitions { get; init; }
    public required List<string> FailureSamples { get; init; }

    public bool IsSuccessful =>
        DuplicateMessages == 0 &&
        ShortfallMessages == 0 &&
        LeakedAbortedMessages == 0 &&
        UnexpectedMessages == 0 &&
        MissingSentinelPartitions == 0;
}

internal sealed class StressTestResults
{
    public required DateTime RunStartedAtUtc { get; init; }
    public required DateTime RunCompletedAtUtc { get; init; }
    public required string MachineName { get; init; }
    public required int ProcessorCount { get; init; }
    public required List<StressTestResult> Results { get; init; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static StressTestResults? FromJson(string json) =>
        JsonSerializer.Deserialize<StressTestResults>(json, JsonOptions);

    public async Task SaveAsync(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(filePath, ToJson()).ConfigureAwait(false);
    }

    public static async Task<StressTestResults?> LoadAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
            return FromJson(json);
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Failed to deserialize results from {filePath}: {ex.Message}");
            return null;
        }
    }
}
