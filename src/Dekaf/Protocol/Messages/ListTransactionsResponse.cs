namespace Dekaf.Protocol.Messages;

/// <summary>
/// ListTransactions response (API key 66).
/// Contains transaction listings known by a broker transaction coordinator.
/// </summary>
public sealed class ListTransactionsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.ListTransactions;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 2;

    public int ThrottleTimeMs { get; init; }
    public ErrorCode ErrorCode { get; init; }
    public required IReadOnlyList<string> UnknownStateFilters { get; init; }
    public required IReadOnlyList<ListTransactionsResponseState> TransactionStates { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var unknownStateFilters = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => r.ReadCompactNonNullableString());
        var transactionStates = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => ListTransactionsResponseState.Read(ref r));

        reader.SkipTaggedFields();

        return new ListTransactionsResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            UnknownStateFilters = unknownStateFilters,
            TransactionStates = transactionStates
        };
    }
}

/// <summary>
/// Transaction state in a ListTransactions response.
/// </summary>
public sealed class ListTransactionsResponseState
{
    public required string TransactionalId { get; init; }
    public long ProducerId { get; init; }
    public required string TransactionState { get; init; }

    public static ListTransactionsResponseState Read(ref KafkaProtocolReader reader)
    {
        var transactionalId = reader.ReadCompactNonNullableString();
        var producerId = reader.ReadInt64();
        var transactionState = reader.ReadCompactNonNullableString();

        reader.SkipTaggedFields();

        return new ListTransactionsResponseState
        {
            TransactionalId = transactionalId,
            ProducerId = producerId,
            TransactionState = transactionState
        };
    }
}
