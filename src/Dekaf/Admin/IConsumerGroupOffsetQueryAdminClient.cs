namespace Dekaf.Admin;

/// <summary>Optional capability for batched, complete consumer-group offset queries.</summary>
/// <remarks>Implemented by Dekaf's admin client without extending the existing IAdminClient contract.</remarks>
public interface IConsumerGroupOffsetQueryAdminClient
{
    ValueTask<IReadOnlyDictionary<string, ConsumerGroupOffsetsResult>> ListConsumerGroupOffsetsAsync(
        IReadOnlyDictionary<string, ListConsumerGroupOffsetsSpec> groupSpecs,
        ListConsumerGroupOffsetsOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Complete consumer-group offset queries for IAdminClient implementations supporting the optional capability.</summary>
public static class AdminClientConsumerGroupOffsetQueryExtensions
{
    /// <summary>Queries selected groups and partitions, preserving checkpoint metadata and individual errors.</summary>
    public static ValueTask<IReadOnlyDictionary<string, ConsumerGroupOffsetsResult>> ListConsumerGroupOffsetsAsync(
        this IAdminClient adminClient,
        IReadOnlyDictionary<string, ListConsumerGroupOffsetsSpec> groupSpecs,
        ListConsumerGroupOffsetsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adminClient);
        return adminClient is IConsumerGroupOffsetQueryAdminClient capability
            ? capability.ListConsumerGroupOffsetsAsync(groupSpecs, options, cancellationToken)
            : throw new NotSupportedException("This admin client does not support complete consumer-group offset queries.");
    }
}
