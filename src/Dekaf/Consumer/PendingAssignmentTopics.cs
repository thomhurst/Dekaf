using System.Diagnostics.CodeAnalysis;
using Dekaf.Metadata;

namespace Dekaf.Consumer;

/// <summary>
/// Topic name resolution for a group assignment that names topic IDs metadata does not know yet.
/// While the same broker assignment stays pending, a topic ID it already resolved keeps that name
/// even if a later metadata snapshot leaves the topic out (a full refresh that briefly misses it),
/// so reprocessing the assignment never revokes partitions the broker still assigns. Only IDs that
/// never resolved stay unknown. Shared by the KIP-848 and share group coordinators.
/// </summary>
internal static class PendingAssignmentTopics
{
    /// <summary>
    /// Resolves <paramref name="topicId"/> from <paramref name="snapshot"/>, or else from the names
    /// the same pending assignment resolved earlier.
    /// </summary>
    public static bool TryResolve(
        ClusterMetadataSnapshot snapshot,
        IReadOnlyDictionary<Guid, string>? resolvedNames,
        Guid topicId,
        [NotNullWhen(true)] out string? name)
    {
        if (snapshot.TopicsById.TryGetValue(topicId, out var topic))
        {
            name = topic.Name;
            return true;
        }

        if (resolvedNames is not null && resolvedNames.TryGetValue(topicId, out name))
            return true;

        name = null;
        return false;
    }
}
