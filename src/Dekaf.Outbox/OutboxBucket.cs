using System.Runtime.InteropServices;
using Dekaf.Producer;

namespace Dekaf.Outbox;

/// <summary>
/// Maps record keys to ordering buckets. The mapping is deterministic and stable across
/// processes and machines, so every writer assigns a given key to the same bucket and the
/// single relay owning that bucket preserves the key's enqueue order.
/// </summary>
public static class OutboxBucket
{
    // Kafka's frozen Murmur2 key hash (the default partitioner's hash): buckets are persisted
    // in customer tables, so the hash can never change, and reusing the partitioner's hash
    // keeps bucket affinity aligned with partition affinity for keyed records.
    private static readonly Murmur2Partitioner Partitioner = new();

    /// <summary>
    /// Computes the bucket for a record, honoring an explicit partition override: records
    /// pinned to the same Kafka partition share a bucket (their partition order is the
    /// ordering the caller asked for), otherwise the key decides.
    /// </summary>
    /// <param name="key">The serialized record key, or null for a keyless record.</param>
    /// <param name="messageId">The message id, used to spread keyless records.</param>
    /// <param name="bucketCount">The total bucket count; must match the relay configuration.</param>
    /// <param name="partition">The explicit partition override, or null when the
    /// partitioner chooses from the key.</param>
    public static int Compute(byte[]? key, Guid messageId, int bucketCount, int? partition)
    {
        if (partition is { } explicitPartition)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(bucketCount, 1);
            ArgumentOutOfRangeException.ThrowIfNegative(explicitPartition, nameof(partition));
            return explicitPartition % bucketCount;
        }

        return Compute(key, messageId, bucketCount);
    }

    /// <summary>
    /// Computes the bucket for a serialized record key. Keyless records have no ordering
    /// requirement and are spread across buckets by message id instead.
    /// </summary>
    /// <param name="key">The serialized record key, or null for a keyless record.</param>
    /// <param name="messageId">The message id, used to spread keyless records.</param>
    /// <param name="bucketCount">The total bucket count; must match the relay configuration.</param>
    public static int Compute(byte[]? key, Guid messageId, int bucketCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bucketCount, 1);

        // Any non-null key is hashed - including one that serialized to zero bytes - so
        // every row for the same logical key shares a bucket. Only truly null (keyless)
        // rows are spread by message id; they carry no ordering requirement.
        if (key is not null)
            return Partitioner.Partition(string.Empty, key, keyIsNull: false, bucketCount);

        Span<byte> idBytes = stackalloc byte[16];
        MemoryMarshal.TryWrite(idBytes, in messageId);
        return Partitioner.Partition(string.Empty, idBytes, keyIsNull: false, bucketCount);
    }
}
