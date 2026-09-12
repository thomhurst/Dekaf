using Dekaf.Serialization;

namespace Dekaf.ShareConsumer;

/// <summary>
/// A record delivered by the share consumer, with share-group-specific metadata.
/// Unlike the regular consumer's <c>ConsumeResult</c> (a readonly struct), this is a class
/// because the acknowledgement state is mutable between poll and commit.
/// </summary>
/// <remarks>
/// Deserializers that borrow their input, and header value views, remain valid for the
/// current poll round and after iterator disposal, until the next poll round starts or
/// the consumer is disposed. Copy them before keeping them beyond that boundary.
/// Renew acknowledgements made before that boundary retain the payload for subsequent replay delivery.
/// </remarks>
public sealed class ShareConsumeResult<TKey, TValue>
{
    // The batch owner also stores the topic. Reuse this reference slot so payload
    // ownership does not add a field/allocation to every delivered record.
    private object _topicOrBatchOwner = null!;
    private byte _acknowledgement = (byte)AcknowledgeType.Accept;
    private int _partitionOrGeneration;

    internal ShareRecordBatchOwner? BatchOwner
    {
        get
        {
            if (_topicOrBatchOwner is not ShareRecordBatchOwner owner)
                return null;
            if (unchecked((uint)~_partitionOrGeneration) != owner.Generation)
                throw new InvalidOperationException("Cannot renew a record after its borrowed payload lifetime has ended.");
            return owner;
        }
    }

    internal void AttachBatchOwner(ShareRecordBatchOwner owner)
    {
        // Owners keep immutable partition metadata, freeing the existing partition
        // word for a generation without growing any generic record layout. The
        // complement keeps ordinary nonnegative partition reads on the fast path.
        _partitionOrGeneration = ~unchecked((int)owner.Generation);
        _topicOrBatchOwner = owner;
    }

    // Only renewal state and undisclosed buffers may bypass the delivery token:
    // both already own an independent reference across poll boundaries.
    internal ShareRecordBatchOwner? RetainedBatchOwner => _topicOrBatchOwner as ShareRecordBatchOwner;

    internal ShareRecordBatchOwner? RefreshRetainedBatchOwner()
    {
        if (_topicOrBatchOwner is not ShareRecordBatchOwner owner)
            return null;
        var generation = owner.Generation;
        if (generation == 0)
        {
            owner = owner.RefreshGeneration();
            _topicOrBatchOwner = owner;
            generation = owner.Generation;
        }
        // A retained replay refreshes its delivery token every poll. Only generation
        // exhaustion replaces the owner; ordinary replay needs no reference write.
        _partitionOrGeneration = ~unchecked((int)generation);
        return owner;
    }

    /// <summary>
    /// The topic this record was consumed from.
    /// </summary>
    public required string Topic
    {
        get => _topicOrBatchOwner is ShareRecordBatchOwner owner ? owner.Topic : (string)_topicOrBatchOwner;
        init => _topicOrBatchOwner = value;
    }

    /// <summary>
    /// The partition this record was consumed from.
    /// </summary>
    public required int Partition
    {
        get => _partitionOrGeneration < 0 && _topicOrBatchOwner is ShareRecordBatchOwner owner
            ? owner.Partition
            : _partitionOrGeneration;
        init => _partitionOrGeneration = value;
    }

    /// <summary>
    /// The offset of this record within the partition.
    /// </summary>
    public required long Offset { get; init; }

    /// <summary>
    /// The deserialized key, or default if the record has no key.
    /// </summary>
    public TKey? Key { get; init; }

    /// <summary>
    /// The deserialized value.
    /// </summary>
    public required TValue Value { get; init; }

    /// <summary>
    /// The message headers. Empty if the record has no headers; never null.
    /// </summary>
    public IReadOnlyList<Header> Headers { get; init; } = Array.Empty<Header>();

    /// <summary>
    /// The message timestamp as raw Unix milliseconds since epoch.
    /// </summary>
    public long TimestampMs { get; init; }

    /// <summary>
    /// The message timestamp as a DateTimeOffset.
    /// Computed on demand to avoid per-message construction overhead.
    /// </summary>
    public DateTimeOffset Timestamp => DateTimeOffset.FromUnixTimeMilliseconds(TimestampMs);

    /// <summary>
    /// Number of times this record has been delivered (from AcquiredRecords.DeliveryCount).
    /// First delivery = 1.
    /// </summary>
    public required int DeliveryCount { get; init; }

    /// <summary>
    /// The acknowledgement state for this record. Defaults to Accept.
    /// Updated via <see cref="IKafkaShareConsumer{TKey,TValue}.Acknowledge"/>.
    /// </summary>
    internal AcknowledgeType AcknowledgeType
    {
        get => (AcknowledgeType)_acknowledgement;
        set
        {
            System.Diagnostics.Debug.Assert((byte)value <= (byte)AcknowledgeType.Renew);
            _acknowledgement = (byte)value;
        }
    }
}
