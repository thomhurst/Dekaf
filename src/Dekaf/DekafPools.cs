using System.Buffers;
using Dekaf.Internal;

namespace Dekaf;

/// <summary>
/// Dedicated array pools shared across Dekaf components. Uses lock-based pooling
/// (not TLS-based) to prevent WorkingSet growth when BrokerSender threads hop
/// between thread pool threads after each <c>await FlushAsync</c>.
/// </summary>
internal static class DekafPools
{
    internal const int SerializationBufferMaxArrayLength = 32 * 1024 * 1024;

    private static readonly RatchetableArrayPool<byte> s_serializationPool = new(
        maxArrayLength: SerializationBufferMaxArrayLength,
        initialArraysPerBucket: 16);

    /// <summary>
    /// Dedicated pool for serialization buffers. Shared by request serialization
    /// (<see cref="Networking.RentedBufferWriter"/>) and record batch building
    /// (<c>PooledReusableBufferWriter</c> in <c>RecordBatch.cs</c>).
    /// <para/>
    /// <c>maxArrayLength: 32MB</c> covers coalesced ProduceRequests with default 1MB
    /// batches (up to ~20MB plus protocol overhead) without falling through to exact-size
    /// LOH allocations. Bucket depth is scaled via <see cref="RatchetSerializationBucketCapacity"/>
    /// when concurrent connection counts increase.
    /// </summary>
    internal static ArrayPool<byte> SerializationBuffers => s_serializationPool.Pool;

    /// <inheritdoc cref="RatchetableArrayPool{T}.RatchetBucketCapacity"/>
    internal static void RatchetSerializationBucketCapacity(int arraysPerBucket) =>
        s_serializationPool.RatchetBucketCapacity(arraysPerBucket);
}
