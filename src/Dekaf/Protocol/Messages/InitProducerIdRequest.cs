namespace Dekaf.Protocol.Messages;

/// <summary>
/// InitProducerId request (API key 22).
/// Allocates a producer ID for idempotent/transactional producing.
/// </summary>
public sealed class InitProducerIdRequest : IKafkaRequest<InitProducerIdResponse>
{
    public static ApiKey ApiKey => ApiKey.InitProducerId;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 5;

    /// <summary>
    /// The transactional ID, or null for idempotent-only producers.
    /// </summary>
    public string? TransactionalId { get; init; }

    /// <summary>
    /// The transaction timeout in milliseconds.
    /// </summary>
    public int TransactionTimeoutMs { get; init; }

    /// <summary>
    /// The current producer ID (v3+). Use -1 for new producer.
    /// </summary>
    public long ProducerId { get; init; } = -1;

    /// <summary>
    /// The current producer epoch (v3+). Use -1 for new producer.
    /// </summary>
    public short ProducerEpoch { get; init; } = -1;

    public static bool IsFlexibleVersion(short version) => version >= 2;
    public static short GetRequestHeaderVersion(short version) => version >= 2 ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => version >= 2 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 2;

        if (isFlexible)
            writer.WriteCompactNullableString(TransactionalId);
        else
            writer.WriteString(TransactionalId);

        writer.WriteInt32(TransactionTimeoutMs);

        if (version >= 3)
        {
            writer.WriteInt64(ProducerId);
            writer.WriteInt16(ProducerEpoch);
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}
