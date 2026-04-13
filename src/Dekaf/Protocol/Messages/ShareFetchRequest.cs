namespace Dekaf.Protocol.Messages;

/// <summary>
/// ShareFetch request (API key 78).
/// Fetches records from share group topic partitions per KIP-932.
/// Supports share session management and acknowledgement batching.
/// </summary>
public sealed class ShareFetchRequest : IKafkaRequest<ShareFetchResponse>
{
    public static ApiKey ApiKey => ApiKey.ShareFetch;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 2;

    /// <summary>
    /// The group ID.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// The member ID.
    /// </summary>
    public required string MemberId { get; init; }

    /// <summary>
    /// The share session epoch. 0 for a new session, -1 to close the session.
    /// </summary>
    public int ShareSessionEpoch { get; init; }

    /// <summary>
    /// Maximum wait time in milliseconds for the fetch to block on the server.
    /// </summary>
    public int MaxWaitMs { get; init; }

    /// <summary>
    /// Minimum bytes to accumulate before returning.
    /// </summary>
    public int MinBytes { get; init; }

    /// <summary>
    /// Maximum bytes to return.
    /// </summary>
    public int MaxBytes { get; init; }

    /// <summary>
    /// Maximum number of records to return (v1+).
    /// </summary>
    public int MaxRecords { get; init; }

    /// <summary>
    /// Batch size hint for the server (v1+).
    /// </summary>
    public int BatchSize { get; init; }

    /// <summary>
    /// The share acquire mode (v2+). 0 = normal acquire.
    /// </summary>
    public sbyte ShareAcquireMode { get; init; }

    /// <summary>
    /// Whether this is a renew acknowledgement request (v2+).
    /// </summary>
    public bool IsRenewAck { get; init; }

    /// <summary>
    /// Topics to fetch.
    /// </summary>
    public required IReadOnlyList<ShareFetchRequestTopic> Topics { get; init; }

    /// <summary>
    /// Topics to forget from the share session.
    /// </summary>
    public IReadOnlyList<ShareFetchForgottenTopic>? ForgottenTopicsData { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactString(GroupId);
        writer.WriteCompactString(MemberId);
        writer.WriteInt32(ShareSessionEpoch);
        writer.WriteInt32(MaxWaitMs);
        writer.WriteInt32(MinBytes);
        writer.WriteInt32(MaxBytes);

        if (version >= 1)
        {
            writer.WriteInt32(MaxRecords);
            writer.WriteInt32(BatchSize);
        }

        if (version >= 2)
        {
            writer.WriteInt8(ShareAcquireMode);
            writer.WriteInt8((sbyte)(IsRenewAck ? 1 : 0));
        }

        writer.WriteCompactArray(
            Topics,
            static (ref KafkaProtocolWriter w, ShareFetchRequestTopic tp, short v) => tp.Write(ref w, v),
            version);

        writer.WriteCompactNullableArray(
            ForgottenTopicsData,
            static (ref KafkaProtocolWriter w, ShareFetchForgottenTopic ft) => ft.Write(ref w));

        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// Topic in a ShareFetch request.
/// </summary>
public sealed class ShareFetchRequestTopic
{
    /// <summary>
    /// The topic ID (UUID).
    /// </summary>
    public required Guid TopicId { get; init; }

    /// <summary>
    /// Partitions to fetch.
    /// </summary>
    public required IReadOnlyList<ShareFetchRequestPartition> Partitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteUuid(TopicId);
        writer.WriteCompactArray(
            Partitions,
            static (ref KafkaProtocolWriter w, ShareFetchRequestPartition p, short v) => p.Write(ref w, v),
            version);
        writer.WriteEmptyTaggedFields();
    }

    public static ShareFetchRequestTopic Read(ref KafkaProtocolReader reader, short version)
    {
        var topicId = reader.ReadUuid();
        var partitions = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r, short v) => ShareFetchRequestPartition.Read(ref r, v),
            version);

        reader.SkipTaggedFields();

        return new ShareFetchRequestTopic
        {
            TopicId = topicId,
            Partitions = partitions
        };
    }
}

/// <summary>
/// Partition in a ShareFetch request.
/// </summary>
public sealed class ShareFetchRequestPartition
{
    /// <summary>
    /// Partition index.
    /// </summary>
    public int PartitionIndex { get; init; }

    /// <summary>
    /// Maximum bytes to fetch for this partition (v0 only).
    /// </summary>
    public int PartitionMaxBytes { get; init; }

    /// <summary>
    /// Acknowledgement batches to include with this fetch.
    /// </summary>
    public IReadOnlyList<ShareFetchAcknowledgementBatch>? AcknowledgementBatches { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteInt32(PartitionIndex);

        if (version == 0)
        {
            writer.WriteInt32(PartitionMaxBytes);
        }

        writer.WriteCompactNullableArray(
            AcknowledgementBatches,
            static (ref KafkaProtocolWriter w, ShareFetchAcknowledgementBatch b) => b.Write(ref w));

        writer.WriteEmptyTaggedFields();
    }

    public static ShareFetchRequestPartition Read(ref KafkaProtocolReader reader, short version)
    {
        var partitionIndex = reader.ReadInt32();

        var partitionMaxBytes = 0;
        if (version == 0)
        {
            partitionMaxBytes = reader.ReadInt32();
        }

        var ackBatchLength = reader.ReadUnsignedVarInt() - 1;
        IReadOnlyList<ShareFetchAcknowledgementBatch>? acknowledgementBatches = null;
        if (ackBatchLength > 0)
        {
            var batches = new ShareFetchAcknowledgementBatch[ackBatchLength];
            for (var i = 0; i < ackBatchLength; i++)
            {
                batches[i] = ShareFetchAcknowledgementBatch.Read(ref reader);
            }
            acknowledgementBatches = batches;
        }

        reader.SkipTaggedFields();

        return new ShareFetchRequestPartition
        {
            PartitionIndex = partitionIndex,
            PartitionMaxBytes = partitionMaxBytes,
            AcknowledgementBatches = acknowledgementBatches
        };
    }
}

/// <summary>
/// Acknowledgement batch in a ShareFetch request.
/// Contains the offset range and per-record acknowledgement types.
/// </summary>
public sealed class ShareFetchAcknowledgementBatch
{
    /// <summary>
    /// The first offset of the batch.
    /// </summary>
    public long FirstOffset { get; init; }

    /// <summary>
    /// The last offset of the batch.
    /// </summary>
    public long LastOffset { get; init; }

    /// <summary>
    /// The acknowledgement types for each record in the batch.
    /// </summary>
    public required IReadOnlyList<byte> AcknowledgeTypes { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteInt64(FirstOffset);
        writer.WriteInt64(LastOffset);
        writer.WriteCompactArray(
            AcknowledgeTypes,
            static (ref KafkaProtocolWriter w, byte t) => w.WriteInt8((sbyte)t));
        writer.WriteEmptyTaggedFields();
    }

    public static ShareFetchAcknowledgementBatch Read(ref KafkaProtocolReader reader)
    {
        var firstOffset = reader.ReadInt64();
        var lastOffset = reader.ReadInt64();
        var acknowledgeTypes = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => (byte)r.ReadInt8());

        reader.SkipTaggedFields();

        return new ShareFetchAcknowledgementBatch
        {
            FirstOffset = firstOffset,
            LastOffset = lastOffset,
            AcknowledgeTypes = acknowledgeTypes
        };
    }
}

/// <summary>
/// Forgotten topic in a ShareFetch request.
/// Used to remove partitions from a share session.
/// </summary>
public sealed class ShareFetchForgottenTopic
{
    /// <summary>
    /// The topic ID (UUID).
    /// </summary>
    public required Guid TopicId { get; init; }

    /// <summary>
    /// The partition indexes to forget.
    /// </summary>
    public required IReadOnlyList<int> Partitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteUuid(TopicId);
        writer.WriteCompactArray(
            Partitions,
            static (ref KafkaProtocolWriter w, int p) => w.WriteInt32(p));
        writer.WriteEmptyTaggedFields();
    }

    public static ShareFetchForgottenTopic Read(ref KafkaProtocolReader reader)
    {
        var topicId = reader.ReadUuid();
        var partitions = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => r.ReadInt32());

        reader.SkipTaggedFields();

        return new ShareFetchForgottenTopic
        {
            TopicId = topicId,
            Partitions = partitions
        };
    }
}
