namespace Dekaf.Admin;

/// <summary>
/// Optional admin-client capability for describing streams groups with options.
/// </summary>
/// <remarks>
/// Dekaf's built-in and in-memory admin clients implement this interface. Custom
/// <see cref="IAdminClient"/> implementations can implement it without an
/// <see cref="IAdminClient"/> binary-compatibility break.
/// </remarks>
public interface IStreamsGroupDescriptionAdminClient
{
    /// <summary>
    /// Describes streams groups.
    /// </summary>
    /// <param name="groupIds">The groups to describe.</param>
    /// <param name="options">Describe options; null uses the defaults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KafkaException">
    /// <see cref="DescribeStreamsGroupsOptions.IncludeTopologyDescription"/> is set and the group coordinator
    /// only supports StreamsGroupDescribe v0 (<see cref="Protocol.ErrorCode.UnsupportedVersion"/>).
    /// </exception>
    ValueTask<IReadOnlyDictionary<string, StreamsGroupDescription>> DescribeStreamsGroupsAsync(
        IEnumerable<string> groupIds,
        DescribeStreamsGroupsOptions? options,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for describing streams groups.
/// </summary>
public sealed class DescribeStreamsGroupsOptions
{
    /// <summary>
    /// Requests the full topology description from the broker's topology-description plugin (KIP-1331).
    /// Requires StreamsGroupDescribe v1 (Kafka 4.4+). The outcome is reported per group in
    /// <see cref="StreamsGroupDescription.TopologyDescriptionStatus"/>.
    /// </summary>
    public bool IncludeTopologyDescription { get; init; }
}

/// <summary>
/// Streams-group description operations for <see cref="IAdminClient"/>.
/// </summary>
public static class AdminClientStreamsGroupDescriptionExtensions
{
    /// <summary>
    /// Describes streams groups with options.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The admin client does not implement <see cref="IStreamsGroupDescriptionAdminClient"/>.
    /// </exception>
    public static ValueTask<IReadOnlyDictionary<string, StreamsGroupDescription>> DescribeStreamsGroupsAsync(
        this IAdminClient adminClient,
        IEnumerable<string> groupIds,
        DescribeStreamsGroupsOptions? options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adminClient);

        return adminClient is IStreamsGroupDescriptionAdminClient describer
            ? describer.DescribeStreamsGroupsAsync(groupIds, options, cancellationToken)
            : throw new NotSupportedException(
                $"Admin client type '{adminClient.GetType().FullName}' does not support streams-group describe options.");
    }
}
