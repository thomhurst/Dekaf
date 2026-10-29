using Dekaf.Consumer;
using Dekaf.ShareConsumer;

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
    /// Optional group-level reset policy. Null preserves the group policy, whose default is Latest.
    /// An explicit policy affects every member and does not rewind initialized partitions.
    /// </summary>
    public AutoOffsetReset? AutoOffsetReset { get; init; }

    /// <summary>Non-negative lookback used with ByDuration. Otherwise must be null.</summary>
    public TimeSpan? AutoOffsetResetDuration { get; init; }

    /// <summary>
    /// Maximum records returned by each poll.
    /// </summary>
    public int MaxPollRecords { get; init; } = 500;

    /// <summary>
    /// Controls whether delivered records are accepted automatically or require Acknowledge.
    /// Renew requires explicit acknowledgement mode.
    /// </summary>
    public ShareAcknowledgementMode AcknowledgementMode { get; init; } = ShareAcknowledgementMode.Implicit;

    /// <summary>
    /// Member ID reported by the fake share consumer.
    /// </summary>
    public string? MemberId { get; init; }

    /// <summary>
    /// Models the group-level broker dead-letter queue (KIP-1191). When set, each rejected record is
    /// written to this topic with the <see cref="Dekaf.Consumer.DeadLetter.ShareGroupDeadLetterHeaders"/>
    /// context headers. As on the broker, the topic must already exist with the topic config
    /// <c>errors.deadletterqueue.group.enable=true</c>; otherwise nothing is written. The in-memory
    /// cluster dead-letters explicit rejects only; delivery-limit archival does not write a dead-letter record.
    /// </summary>
    public string? DeadLetterQueueTopic { get; init; }

    /// <summary>
    /// True copies the rejected record's key and value into the dead-letter record; false writes a null
    /// key and value. Like Kafka 4.4.0, the dead-letter record carries only the context headers.
    /// </summary>
    public bool DeadLetterQueueCopyRecord { get; init; }
}
