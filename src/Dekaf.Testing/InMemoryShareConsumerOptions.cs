namespace Dekaf.Testing;

/// <summary>
/// Options for <see cref="InMemoryShareConsumer{TKey,TValue}"/>.
/// </summary>
public sealed class InMemoryShareConsumerOptions
{
    /// <summary>
    /// Share group ID used for accepted offsets.
    /// </summary>
    /// <remarks>
    /// Share acquisition defaults to read-uncommitted isolation. Configure the group's
    /// <c>share.isolation.level</c> through <see cref="InMemoryAdminClient.IncrementalAlterConfigsAsync"/>
    /// to select read-committed isolation for all members of the group.
    /// </remarks>
    public string GroupId { get; init; } = "dekaf-in-memory-share";

    /// <summary>
    /// Maximum records returned by each poll.
    /// </summary>
    public int MaxPollRecords { get; init; } = 500;

    /// <summary>
    /// Member ID reported by the fake share consumer.
    /// </summary>
    public string? MemberId { get; init; }

    /// <summary>
    /// Models the group-level broker dead-letter queue (KIP-1191). When set, each rejected record is
    /// written to this topic with the <see cref="Dekaf.Consumer.DeadLetter.ShareGroupDeadLetterHeaders"/>
    /// context headers. As on the broker, the topic must already exist with the topic config
    /// <c>errors.deadletterqueue.group.enable=true</c>; otherwise nothing is written. The in-memory
    /// cluster has no delivery limit, so only rejects are dead-lettered.
    /// </summary>
    public string? DeadLetterQueueTopic { get; init; }

    /// <summary>
    /// True copies the rejected record's key and value into the dead-letter record; false writes a null
    /// key and value. Like Kafka 4.4.0, the dead-letter record carries only the context headers.
    /// </summary>
    public bool DeadLetterQueueCopyRecord { get; init; }
}
