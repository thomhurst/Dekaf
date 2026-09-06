using Dekaf.Admin;

namespace Dekaf.Testing;

public sealed partial class InMemoryAdminClient : IPartitionExpansionAdminClient
{
    /// <inheritdoc />
    async ValueTask IPartitionExpansionAdminClient.CreatePartitionsAsync(
        IReadOnlyDictionary<string, NewPartitions> newPartitions,
        CreatePartitionsOptions? options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(newPartitions);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        var timeoutMs = options?.TimeoutMs ?? 30000;
        ArgumentOutOfRangeException.ThrowIfNegative(timeoutMs);

        // Validate and snapshot the whole batch before faults can suspend or mutations begin.
        var topics = AdminClient.BuildPartitionExpansionTopics(newPartitions);

        if (topics.Count == 0)
            await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);

        foreach (var topic in topics)
        {
            await ApplyAdminFaultAsync(cancellationToken, topic: topic.Name).ConfigureAwait(false);
            _cluster.CreatePartitions(topic, options?.ValidateOnly ?? false);
        }
    }
}
