namespace Dekaf.Testing;

/// <summary>
/// Options for <see cref="InMemoryShareConsumer{TKey,TValue}"/>.
/// </summary>
public sealed class InMemoryShareConsumerOptions
{
    /// <summary>
    /// Share group ID used for accepted offsets.
    /// </summary>
    public string GroupId { get; set; } = "dekaf-in-memory-share";

    /// <summary>
    /// Maximum records returned by each poll.
    /// </summary>
    public int MaxPollRecords { get; set; } = 500;

    /// <summary>
    /// Member ID reported by the fake share consumer.
    /// </summary>
    public string? MemberId { get; set; }
}
