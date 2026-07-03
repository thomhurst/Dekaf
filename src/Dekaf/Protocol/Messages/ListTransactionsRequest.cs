namespace Dekaf.Protocol.Messages;

/// <summary>
/// ListTransactions request (API key 66).
/// Lists transactions known by a broker transaction coordinator.
/// </summary>
public sealed class ListTransactionsRequest : IKafkaRequest<ListTransactionsResponse>
{
    public static ApiKey ApiKey => ApiKey.ListTransactions;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 2;

    public IReadOnlyList<string>? StateFilters { get; init; }
    public IReadOnlyList<long>? ProducerIdFilters { get; init; }
    public long DurationFilterMs { get; init; } = -1;
    public string? TransactionalIdPattern { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactArray(
            StateFilters ?? [],
            static (ref KafkaProtocolWriter w, string state) => w.WriteCompactString(state));

        writer.WriteCompactArray(
            ProducerIdFilters ?? [],
            static (ref KafkaProtocolWriter w, long producerId) => w.WriteInt64(producerId));

        if (version >= 1)
        {
            writer.WriteInt64(DurationFilterMs);
        }

        if (version >= 2)
        {
            writer.WriteCompactNullableString(TransactionalIdPattern);
        }

        writer.WriteEmptyTaggedFields();
    }
}
