namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeTransactions response (API key 65).
/// Contains transaction coordinator state for transactional IDs.
/// </summary>
public sealed class DescribeTransactionsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.DescribeTransactions;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    public int ThrottleTimeMs { get; init; }
    public required IReadOnlyList<DescribeTransactionsResponseState> TransactionStates { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();
        var transactionStates = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => DescribeTransactionsResponseState.Read(ref r));

        reader.SkipTaggedFields();

        return new DescribeTransactionsResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            TransactionStates = transactionStates
        };
    }
}

/// <summary>
/// Transaction state in a DescribeTransactions response.
/// </summary>
public sealed class DescribeTransactionsResponseState
{
    public ErrorCode ErrorCode { get; init; }
    public required string TransactionalId { get; init; }
    public required string TransactionState { get; init; }
    public int TransactionTimeoutMs { get; init; }
    public long TransactionStartTimeMs { get; init; }
    public long ProducerId { get; init; }
    public short ProducerEpoch { get; init; }
    public required IReadOnlyList<DescribeTransactionsResponseTopic> Topics { get; init; }

    public static DescribeTransactionsResponseState Read(ref KafkaProtocolReader reader)
    {
        var errorCode = (ErrorCode)reader.ReadInt16();
        var transactionalId = reader.ReadCompactNonNullableString();
        var transactionState = reader.ReadCompactNonNullableString();
        var transactionTimeoutMs = reader.ReadInt32();
        var transactionStartTimeMs = reader.ReadInt64();
        var producerId = reader.ReadInt64();
        var producerEpoch = reader.ReadInt16();
        var topics = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => DescribeTransactionsResponseTopic.Read(ref r));

        reader.SkipTaggedFields();

        return new DescribeTransactionsResponseState
        {
            ErrorCode = errorCode,
            TransactionalId = transactionalId,
            TransactionState = transactionState,
            TransactionTimeoutMs = transactionTimeoutMs,
            TransactionStartTimeMs = transactionStartTimeMs,
            ProducerId = producerId,
            ProducerEpoch = producerEpoch,
            Topics = topics
        };
    }
}

/// <summary>
/// Topic in a DescribeTransactions response state.
/// </summary>
public sealed class DescribeTransactionsResponseTopic
{
    public required string Topic { get; init; }
    public required IReadOnlyList<int> Partitions { get; init; }

    public static DescribeTransactionsResponseTopic Read(ref KafkaProtocolReader reader)
    {
        var topic = reader.ReadCompactNonNullableString();
        var partitions = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => r.ReadInt32());

        reader.SkipTaggedFields();

        return new DescribeTransactionsResponseTopic
        {
            Topic = topic,
            Partitions = partitions
        };
    }
}
