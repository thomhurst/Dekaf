namespace Dekaf.Admin;

/// <summary>Optional capability for querying offsets for multiple share groups.</summary>
public interface IShareGroupOffsetQueryAdminClient
{
    ValueTask<IReadOnlyDictionary<string, ShareGroupOffsetsResult>> ListShareGroupOffsetsAsync(
        IReadOnlyDictionary<string, ListShareGroupOffsetsSpec> groupSpecs,
        ListShareGroupOffsetsOptions? options = null, CancellationToken cancellationToken = default);
}

/// <summary>Batched share-group offset queries for compatible admin clients.</summary>
public static class AdminClientShareGroupOffsetQueryExtensions
{
    /// <summary>Queries share-group offsets, retaining individual group and partition outcomes.</summary>
    public static ValueTask<IReadOnlyDictionary<string, ShareGroupOffsetsResult>> ListShareGroupOffsetsAsync(
        this IAdminClient adminClient, IReadOnlyDictionary<string, ListShareGroupOffsetsSpec> groupSpecs,
        ListShareGroupOffsetsOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adminClient);
        return adminClient is IShareGroupOffsetQueryAdminClient capability
            ? capability.ListShareGroupOffsetsAsync(groupSpecs, options, cancellationToken)
            : throw new NotSupportedException("This admin client does not support batched share-group offset queries.");
    }
}

/// <summary>Selects offsets for one share group.</summary>
public sealed class ListShareGroupOffsetsSpec
{
    /// <summary>Null selects all applicable partitions; an empty list selects none.</summary>
    public IReadOnlyList<TopicPartition>? TopicPartitions { get; init; }
}

/// <summary>Options for a batched share-group offset query.</summary>
public sealed class ListShareGroupOffsetsOptions
{
    /// <summary>Total operation budget in milliseconds, including discovery and retries.</summary>
    public int TimeoutMs { get; init; } = 30000;
}

/// <summary>Group status and individual partition outcomes. Inspect both levels for errors.</summary>
public sealed class ShareGroupOffsetsResult
{
    public required string GroupId { get; init; }
    public Protocol.ErrorCode ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public required IReadOnlyDictionary<TopicPartition, ShareGroupOffsetDescription> Offsets { get; init; }
}
