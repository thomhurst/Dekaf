using System.Text.Json;
using System.Text.Json.Serialization;
using Dekaf.StressTests.Metrics;

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

    /// <summary>
    /// Broker-confirmed message count, measured as the end-offset (high watermark)
    /// delta across all topic partitions between test start and end. Fire-and-forget
    /// scenarios count client-side appends in <see cref="Throughput"/>; when the client
    /// buffers faster than the broker accepts (or the broker degrades mid-run), accepted
    /// and delivered diverge — this is the honest throughput number.
    /// Null when the scenario doesn't measure it or the watermark query failed.
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
