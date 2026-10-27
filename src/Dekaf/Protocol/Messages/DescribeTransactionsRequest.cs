namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeTransactions request (API key 65).
/// Describes transaction coordinator state for transactional IDs.
/// </summary>
public sealed class DescribeTransactionsRequest : IKafkaRequest<DescribeTransactionsResponse>
{
    public static ApiKey ApiKey => ApiKey.DescribeTransactions;
    public static short LowestSupportedVersion => 0;
    // Kafka 4.4.0 ships DescribeTransactions v0 only. The v1 codec (KIP-994 TransactionLastUpdateTimeMs)
    // is kept for a future release, but v1 must not be negotiated until a released schema is re-verified.
    public static short HighestSupportedVersion => 0;

    public required IReadOnlyList<string> TransactionalIds { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactArray(
            TransactionalIds,
            static (ref KafkaProtocolWriter w, string transactionalId) => w.WriteCompactString(transactionalId));

        writer.WriteEmptyTaggedFields();
    }
}
