namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeTransactions request (API key 65).
/// Describes transaction coordinator state for transactional IDs.
/// </summary>
public sealed class DescribeTransactionsRequest : IKafkaRequest<DescribeTransactionsResponse>
{
    public static ApiKey ApiKey => ApiKey.DescribeTransactions;
    public static short LowestSupportedVersion => 0;
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
