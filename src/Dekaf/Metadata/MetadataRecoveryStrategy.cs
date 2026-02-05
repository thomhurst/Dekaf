namespace Dekaf.Metadata;

/// <summary>
/// Strategy for recovering cluster metadata when all known brokers become unavailable.
/// This is critical in cloud environments where broker IPs may change during rolling upgrades.
/// </summary>
public enum MetadataRecoveryStrategy
{
    /// <summary>
    /// No special recovery - only retry known brokers.
    /// If all known brokers are unavailable, metadata refresh will fail until
    /// a known broker becomes reachable again.
    /// </summary>
    None,

    /// <summary>
    /// Re-resolve bootstrap server DNS when all known brokers are unavailable.
    /// This discovers new broker IPs that may have changed during rolling upgrades
    /// or infrastructure changes. The original bootstrap server hostnames are
    /// re-resolved via DNS, and any newly discovered endpoints are added to the
    /// connection pool.
    /// </summary>
    Rebootstrap
}
