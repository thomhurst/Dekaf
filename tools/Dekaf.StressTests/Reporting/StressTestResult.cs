using System.Text.Json;
using System.Text.Json.Serialization;
using Dekaf.StressTests.Metrics;

namespace Dekaf.StressTests.Reporting;

internal sealed class StressTestResult
{
    public required string Scenario { get; init; }
    public required string Client { get; init; }
    public required int DurationMinutes { get; init; }
    public required int MessageSizeBytes { get; init; }
    public required DateTime StartedAtUtc { get; init; }
    public required DateTime CompletedAtUtc { get; init; }
    public required ThroughputSnapshot Throughput { get; init; }
    public required LatencySnapshot? Latency { get; init; }
    public required GcSnapshot GcStats { get; init; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
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
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
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

        var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
        return FromJson(json);
    }
}
