using Dekaf.Serialization;

namespace Dekaf.Testing;

/// <summary>
/// Raw record stored by <see cref="InMemoryKafkaCluster"/>.
/// </summary>
public sealed record InMemoryRecord
{
    public required string Topic { get; init; }
    public required int Partition { get; init; }
    public required long Offset { get; init; }
    public required byte[] Key { get; init; }
    public required bool IsKeyNull { get; init; }
    public required byte[] Value { get; init; }
    public required bool IsValueNull { get; init; }
    public IReadOnlyList<Header>? Headers { get; init; }
    public required long TimestampMs { get; init; }
    public DateTimeOffset Timestamp => DateTimeOffset.FromUnixTimeMilliseconds(TimestampMs);
}
