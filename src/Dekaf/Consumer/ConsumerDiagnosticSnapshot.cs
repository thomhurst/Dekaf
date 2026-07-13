namespace Dekaf.Consumer;

using Dekaf.Networking;

internal sealed class ConsumerDiagnosticSnapshot
{
    public required DateTimeOffset CapturedAtUtc { get; init; }
    public required ConsumerPartitionOffsetDiagnostic[] FetchPositions { get; init; }
    public required ConsumerTopicPartitionDiagnostic[] Assignment { get; init; }
    public required long PrefetchedBytes { get; init; }
    public required int PendingFetchDepth { get; init; }
    public required int PrefetchBufferDepth { get; init; }
    public required int PrefetchDepth { get; init; }
    public required ConsumerTopicPartitionDiagnostic[] PendingRevocations { get; init; }
    public required bool PendingRevocationMarkerPresent { get; init; }
    public required bool PendingRevocationClearPending { get; init; }
    public required ConsumerDivergingEpochResetDiagnostic[] PendingDivergingEpochResets { get; init; }
    public required int FetchBufferEpoch { get; init; }
    public required int MinimumFetchBufferEpoch { get; init; }
    public required ConsumerPartitionEpochDiagnostic[] MinimumFetchBufferEpochsByPartition { get; init; }
    public required int? AdaptivePartitionFetchBytes { get; init; }
    public required int? AdaptiveFetchMaxBytes { get; init; }
    public ConnectionReapDiagnostic[] ConnectionReapEvents { get; init; } = [];
}

internal sealed record ConsumerTopicPartitionDiagnostic(string Topic, int Partition);

internal sealed record ConsumerPartitionOffsetDiagnostic(string Topic, int Partition, long Offset);

internal sealed record ConsumerPartitionEpochDiagnostic(string Topic, int Partition, int Epoch);

internal sealed record ConsumerDivergingEpochResetDiagnostic(
    string Topic,
    int Partition,
    long EndOffset,
    int Epoch);
