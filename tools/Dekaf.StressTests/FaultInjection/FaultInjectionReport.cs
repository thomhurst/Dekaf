using System.Text.Json;

namespace Dekaf.StressTests.FaultInjection;

internal sealed class FaultInjectionReport
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public required DateTime StartedAtUtc { get; init; }
    public DateTime CompletedAtUtc { get; set; }
    public required string MachineName { get; init; }
    public required string Profile { get; init; }
    public required int BrokerCount { get; init; }
    public required int PartitionCount { get; init; }
    public required int MessageSizeBytes { get; init; }
    public required int FaultDurationSeconds { get; init; }
    public List<FaultWindowRunResult> Windows { get; } = [];

    internal async Task SaveAsync(string path, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, this, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);
    }
}

internal sealed class FaultWindowRunResult
{
    public required string Name { get; init; }
    public required DateTime StartedAtUtc { get; init; }
    public DateTime CompletedAtUtc { get; set; }
    public bool Succeeded { get; set; }
    public long AcceptedMessages { get; set; }
    public long DeliveryErrors { get; set; }
    public long DeliveryCallbacks { get; set; }
    public long BrokerDeliveredMessages { get; set; }
    public long OracleConsumedMessages { get; set; }
    public long LiveConsumerMessages { get; set; }
    public long UnexplainedLoss { get; set; }
    public long Duplicates { get; set; }
    public long OracleCountMismatch { get; set; }
    public IReadOnlyList<long> MissingIds { get; set; } = [];
    public IReadOnlyList<long> DuplicateIds { get; set; } = [];
    public IReadOnlyList<long> UnexpectedIds { get; set; } = [];
    public IReadOnlyList<DeliveryErrorSample> DeliveryErrorSamples { get; set; } = [];
    public string? Failure { get; set; }
}

internal sealed record DeliveryErrorSample(long MessageId, string Error);
