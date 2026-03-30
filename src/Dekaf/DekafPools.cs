using System.Buffers;

namespace Dekaf;

/// <summary>
/// Dedicated array pools shared across Dekaf components. Uses lock-based pooling
/// (not TLS-based) to prevent WorkingSet growth when BrokerSender threads hop
/// between thread pool threads after each <c>await FlushAsync</c>.
/// </summary>
internal static class DekafPools
{
    /// <summary>
    /// Dedicated pool for serialization buffers. Shared by request serialization
    /// (<see cref="Networking.RentedBufferWriter"/>) and record batch building
    /// (<c>PooledReusableBufferWriter</c> in <c>RecordBatch.cs</c>).
    /// <para/>
    /// <c>maxArrayLength: 4MB</c> covers ProduceRequests with default 1MB batch size plus
    /// header overhead and multi-batch coalescing. <c>maxArraysPerBucket: 16</c> provides
    /// sufficient depth for concurrent BrokerSender threads without excessive retention.
    /// </summary>
    internal static readonly ArrayPool<byte> SerializationBuffers = ArrayPool<byte>.Create(
        maxArrayLength: 4 * 1024 * 1024,
        maxArraysPerBucket: 16);
}
