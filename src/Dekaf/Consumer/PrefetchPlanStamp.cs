using Dekaf.Metadata;

namespace Dekaf.Consumer;

/// <summary>
/// Identifies the fetch plan a broker prefetch task was started with, so the task can verify
/// that the plan is still current before re-issuing a fetch without returning to the central
/// prefetch loop (see <see cref="BrokerPrefetchScheduler"/>).
/// </summary>
/// <param name="CanReissue">
/// False when the plan was not served from the partition cache (uncached result or a
/// revocation-filtered copy); such tasks always perform a single fetch.
/// </param>
/// <param name="AssignmentVersion">
/// Partition-cache version the plan was built for; bumped by assignment, pause/resume,
/// preferred-replica and snapshot changes.
/// </param>
/// <param name="ConnectionCount">Applied routing width the plan split partitions across.</param>
/// <param name="MetadataSnapshot">
/// Cluster metadata snapshot current when the plan was stamped; any refresh (leader or topic
/// identity changes) hands control back to the loop.
/// </param>
/// <param name="PreferredReplicaExpiresAtTimestamp">
/// Earliest preferred-replica expiry in the plan, or <c>long.MaxValue</c> when none applies.
/// </param>
internal readonly record struct PrefetchPlanStamp(
    bool CanReissue,
    int AssignmentVersion,
    int ConnectionCount,
    ClusterMetadataSnapshot? MetadataSnapshot,
    long PreferredReplicaExpiresAtTimestamp)
{
    /// <summary>A plan that must not be re-issued by the task; the loop re-dispatches it.</summary>
    public static PrefetchPlanStamp SingleFetch => default;
}
