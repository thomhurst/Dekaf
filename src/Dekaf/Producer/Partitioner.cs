using System.Buffers.Binary;
using System.Collections.Concurrent;

namespace Dekaf.Producer;

/// <summary>
/// Interface for message partitioners.
/// </summary>
public interface IPartitioner
{
    /// <summary>
    /// Selects a partition for a message.
    /// The key span is valid only for the duration of this call.
    /// </summary>
    int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount);
}

internal interface IBatchCompletionAwarePartitioner
{
    void OnBatchComplete(string topic, int partitionCount);
}

/// <summary>
/// Default partitioner - uses murmur2 hash of key, or round-robin for null keys.
/// </summary>
public sealed class DefaultPartitioner : IPartitioner
{
    // Non-atomic increment is intentional: avoids Interlocked cache line contention across threads.
    // Two threads may occasionally read the same counter value (benign — just means two messages
    // land on the same partition), but distribution remains even over time.
    private uint _counter;

    public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount)
    {
        if (keyIsNull || key.Length == 0)
        {
            return (int)(++_counter % (uint)partitionCount);
        }

        // Kafka-compatible Murmur2 partitioning for keyed messages.
        return Murmur2.Partition(key, partitionCount);
    }
}

/// <summary>
/// Sticky partitioner - sticks to a partition for null keys until batch is full.
/// Uses ConcurrentDictionary for lock-free read access in the hot path.
/// </summary>
public sealed class StickyPartitioner : IPartitioner, IBatchCompletionAwarePartitioner
{
    private readonly ConcurrentDictionary<string, int> _stickyPartitions = new();
#if NETSTANDARD2_0
    private int _counter;
#else
    private uint _counter;
#endif

    public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount)
    {
        if (keyIsNull || key.Length == 0)
        {
            // Hot path: TryGetValue is lock-free for reads in ConcurrentDictionary
            if (_stickyPartitions.TryGetValue(topic, out var partition))
            {
                return partition;
            }

            // Cold path: topic not yet seen, compute and add a partition.
            // Use uint to avoid overflow to negative values.
            // GetOrAdd handles the race condition - if another thread added first, we use their value.
            var newPartition = NextPartition(partitionCount);
            return _stickyPartitions.GetOrAdd(topic, newPartition);
        }

        return Murmur2.Partition(key, partitionCount);
    }

    /// <summary>
    /// Called when a batch is sent to switch to a new partition.
    /// </summary>
    public void OnBatchComplete(string topic, int partitionCount)
    {
        // Use AddOrUpdate for thread-safe update that doesn't race with concurrent TryGetValue.
        // Use uint to avoid overflow to negative values.
        _stickyPartitions.AddOrUpdate(
            topic,
            _ => NextPartition(partitionCount),
            (_, _) => NextPartition(partitionCount));
    }

#if NETSTANDARD2_0
    private int NextPartition(int partitionCount)
        => (int)((uint)Interlocked.Increment(ref _counter) % (uint)partitionCount);
#else
    private int NextPartition(int partitionCount)
        => (int)(Interlocked.Increment(ref _counter) % (uint)partitionCount);
#endif
}

/// <summary>
/// Round-robin partitioner - cycles through partitions.
/// </summary>
public sealed class RoundRobinPartitioner : IPartitioner
{
    // Non-atomic increment — see DefaultPartitioner comment for rationale.
    private uint _counter;

    public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount)
    {
        return (int)(++_counter % (uint)partitionCount);
    }
}

/// <summary>
/// Murmur2 hash implementation (same as Java Kafka client).
/// </summary>
internal static class Murmur2
{
    private const uint Seed = 0x9747b28c;
    private const int M = 0x5bd1e995;
    private const int R = 24;
    private const uint PositiveMask = 0x7fff_ffff;

    public static int Partition(ReadOnlySpan<byte> key, int partitionCount)
    {
        return (int)((Hash(key) & PositiveMask) % (uint)partitionCount);
    }

    public static uint Hash(ReadOnlySpan<byte> data)
    {
        var length = data.Length;
        var h = Seed ^ (uint)length;
        var offset = 0;

        while (length >= 4)
        {
            var k = (uint)BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);

            k *= M;
            k ^= k >> R;
            k *= M;

            h *= M;
            h ^= k;

            offset += 4;
            length -= 4;
        }

        switch (length)
        {
            case 3:
                h ^= (uint)data[offset + 2] << 16;
                goto case 2;
            case 2:
                h ^= (uint)data[offset + 1] << 8;
                goto case 1;
            case 1:
                h ^= data[offset];
                h *= M;
                break;
        }

        h ^= h >> 13;
        h *= M;
        h ^= h >> 15;

        return h;
    }
}
