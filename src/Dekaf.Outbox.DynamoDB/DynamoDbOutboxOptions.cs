namespace Dekaf.Outbox.DynamoDB;

/// <summary>
/// Configuration for the DynamoDB outbox store and writer.
/// </summary>
/// <remarks>
/// Every writer and relay sharing a table must use the same values for
/// <see cref="TableName"/>, the key attribute names, <see cref="KeyPrefix"/> and
/// <see cref="BucketCount"/>.
/// </remarks>
public sealed class DynamoDbOutboxOptions
{
    /// <summary>
    /// The table holding the outbox items. It needs a string partition key and a string sort
    /// key and nothing else: no secondary index, no stream, no time to live.
    /// </summary>
    public required string TableName { get; init; }

    /// <summary>
    /// Name of the table's partition key attribute.
    /// </summary>
    public string PartitionKeyAttributeName { get; init; } = "PK";

    /// <summary>
    /// Name of the table's sort key attribute.
    /// </summary>
    public string SortKeyAttributeName { get; init; } = "SK";

    /// <summary>
    /// Prefix of every partition key value the outbox writes. Distinct prefixes keep several
    /// logical outboxes, or an outbox and application items, apart in one table.
    /// </summary>
    public string KeyPrefix { get; init; } = "OUTBOX";

    /// <summary>
    /// Number of ordering buckets. Must equal <see cref="OutboxRelayOptions.BucketCount"/> and
    /// the bucket count passed to <see cref="OutboxMessage.Create"/>. The writer stamps it on
    /// every message so a relay can detect a writer that disagrees.
    /// </summary>
    public int BucketCount { get; init; } = OutboxRelayOptions.DefaultBucketCount;

    /// <summary>
    /// Maximum concurrent DynamoDB requests for the per-bucket work of one store call: lease
    /// writes, pending probes and sequence reservations. DynamoDB has no set-based conditional
    /// write, so this bounds how long a relay with many buckets spends on one acquisition.
    /// </summary>
    public int MaxConcurrency { get; init; } = 8;

    /// <summary>
    /// Maximum messages counted per bucket for one backlog sample. DynamoDB bills a count as
    /// a read of every counted item, so an unbounded count of a large backlog would cost more
    /// than the publishing itself. A bucket that reaches the limit reports the limit.
    /// </summary>
    public int PendingCountLimit { get; init; } = 10_000;

    /// <summary>
    /// Validates option consistency. Called by the store and the writer at construction.
    /// </summary>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(TableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(PartitionKeyAttributeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(SortKeyAttributeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(KeyPrefix);
        ArgumentOutOfRangeException.ThrowIfLessThan(BucketCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxConcurrency, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(PendingCountLimit, 1);

        if (string.Equals(PartitionKeyAttributeName, SortKeyAttributeName, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "PartitionKeyAttributeName and SortKeyAttributeName must name different attributes.");
        }
    }
}
