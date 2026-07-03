using Dekaf.Consumer;

namespace Dekaf.Testing;

/// <summary>
/// Options for <see cref="InMemoryConsumer{TKey,TValue}"/>.
/// </summary>
public sealed class InMemoryConsumerOptions
{
    /// <summary>
    /// Consumer group ID used for committed offsets.
    /// </summary>
    public string? GroupId { get; init; }

    /// <summary>
    /// Offset reset behavior when no committed offset exists.
    /// </summary>
    public AutoOffsetReset AutoOffsetReset { get; init; } = AutoOffsetReset.Latest;

    /// <summary>
    /// Duration used when <see cref="AutoOffsetReset"/> is <see cref="AutoOffsetReset.ByDuration"/>.
    /// </summary>
    public TimeSpan? AutoOffsetResetDuration { get; init; }

    /// <summary>
    /// Commit consumed offsets automatically.
    /// </summary>
    public OffsetCommitMode OffsetCommitMode { get; init; } = OffsetCommitMode.Auto;

    /// <summary>
    /// Member ID reported by the fake consumer.
    /// </summary>
    public string? MemberId { get; init; }
}
