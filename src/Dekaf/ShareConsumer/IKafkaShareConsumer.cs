namespace Dekaf.ShareConsumer;

/// <summary>
/// Interface for Kafka share consumer (KIP-932).
/// Share consumers provide queue-semantics consumption with record-level acknowledgement.
/// Records are acquired with locks and must be acknowledged (accepted, released, or rejected).
/// <para>
/// <b>Thread safety:</b> This interface is not thread-safe. All methods — <see cref="Subscribe"/>,
/// <see cref="Unsubscribe"/>, <see cref="PollAsync"/>, <see cref="Acknowledge"/>, and
/// <see cref="CommitAsync"/> — must be called from the same thread or with external synchronization.
/// </para>
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public interface IKafkaShareConsumer<TKey, TValue> : IInitializableKafkaClient, IAsyncDisposable
{
    /// <summary>
    /// Gets the current topic subscription.
    /// </summary>
    IReadOnlySet<string> Subscription { get; }

    /// <summary>
    /// Gets the current partition assignment from the share group coordinator.
    /// </summary>
    IReadOnlySet<TopicPartition> Assignment { get; }

    /// <summary>
    /// Gets the member ID (client-generated UUID) if part of a share group.
    /// </summary>
    string? MemberId { get; }

    /// <summary>
    /// Subscribes to topics. Share groups do not support manual partition assignment.
    /// </summary>
    IKafkaShareConsumer<TKey, TValue> Subscribe(params string[] topics);

    /// <summary>
    /// Unsubscribes from all topics and leaves the share group.
    /// Any pending unacknowledged records are released back to the group (best-effort)
    /// so other members can claim them without waiting for the acquisition lock to expire.
    /// </summary>
    IKafkaShareConsumer<TKey, TValue> Unsubscribe();

    /// <summary>
    /// Polls for records from the share group. Returns an async enumerable of acquired records.
    /// <para>
    /// <b>Important:</b> Any records from the previous poll that were not explicitly acknowledged
    /// via <see cref="Acknowledge"/> are implicitly accepted (per the KIP-932 specification).
    /// If you intend to release or reject records, you must call <see cref="Acknowledge"/> before
    /// the next <see cref="PollAsync"/> or <see cref="CommitAsync"/> call.
    /// </para>
    /// </summary>
    IAsyncEnumerable<ShareConsumeResult<TKey, TValue>> PollAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the acknowledgement type for a specific record.
    /// If not called, records default to <see cref="AcknowledgeType.Accept"/>.
    /// <para>
    /// <b>Warning:</b> You must call this before the next <see cref="PollAsync"/> or
    /// <see cref="CommitAsync"/>. Any records not explicitly acknowledged are implicitly
    /// accepted — there is no way to release or reject them after that point.
    /// </para>
    /// </summary>
    /// <param name="record">The record to acknowledge.</param>
    /// <param name="type">The acknowledgement type. Defaults to Accept.</param>
    void Acknowledge(ShareConsumeResult<TKey, TValue> record, AcknowledgeType type = AcknowledgeType.Accept);

    /// <summary>
    /// Commits all pending acknowledgements to the broker via a standalone ShareAcknowledge request.
    /// All unacknowledged records are implicitly accepted.
    /// </summary>
    ValueTask CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the share consumer: flushes pending acknowledgements, closes share sessions,
    /// leaves the share group, and releases all resources.
    /// </summary>
    ValueTask CloseAsync(CancellationToken cancellationToken = default);
}
