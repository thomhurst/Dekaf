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
    /// Automatically stores each consumed message's next offset for auto-commit.
    /// Disable to call <c>StoreOffset</c> after processing succeeds.
    /// </summary>
    public bool EnableAutoOffsetStore { get; init; } = true;

    /// <summary>
    /// Controls when automatically stored offsets become committable, mirroring
    /// <see cref="ConsumerOptions.OffsetStoreTiming"/>. The default
    /// (<see cref="OffsetStoreTiming.AfterProcessing"/>) stages a record only once the
    /// next consume call proves it was processed; <see cref="OffsetStoreTiming.OnDelivery"/>
    /// stages it immediately at delivery.
    /// </summary>
    public OffsetStoreTiming OffsetStoreTiming { get; init; } = OffsetStoreTiming.AfterProcessing;

    /// <summary>
    /// Member ID reported by the fake consumer.
    /// </summary>
    public string? MemberId { get; init; }
}
