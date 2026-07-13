namespace Dekaf.Networking;

internal sealed class ConnectionReapDiagnostic
{
    public required DateTimeOffset OccurredAtUtc { get; init; }
    public required int BrokerId { get; init; }
    public required int ConnectionIndex { get; init; }
    public required long IdleDurationMs { get; init; }
    public required bool IsBootstrapConnection { get; init; }
}
