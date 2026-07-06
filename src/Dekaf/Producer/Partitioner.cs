using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Hashing;

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

internal sealed class StickyPartitionTracker
{
    private readonly ConcurrentDictionary<string, int> _partitions = new();
#if NETSTANDARD2_0
    private int _counter;
#else
    private uint _counter;
#endif

    public int GetOrAssign(string topic, int partitionCount)
    {
        if (_partitions.TryGetValue(topic, out var partition))
        {
            return partition;
        }

        var newPartition = NextPartition(partitionCount);
        return _partitions.GetOrAdd(topic, newPartition);
    }

    public void Rotate(string topic, int partitionCount)
    {
        _partitions.AddOrUpdate(
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
/// Default partitioner - uses murmur2 hash of key, or sticky partitioning for null keys.
/// </summary>
public sealed class DefaultPartitioner : IPartitioner, IBatchCompletionAwarePartitioner
{
    private readonly StickyPartitionTracker _stickyPartitionTracker = new();

    public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount)
    {
        if (keyIsNull || key.Length == 0)
        {
            return _stickyPartitionTracker.GetOrAssign(topic, partitionCount);
        }

        // Kafka-compatible Murmur2 partitioning for keyed messages.
        return Murmur2.Partition(key, partitionCount);
    }

    /// <summary>
    /// Called when a batch is sent to switch to a new partition.
    /// </summary>
    public void OnBatchComplete(string topic, int partitionCount)
    {
        _stickyPartitionTracker.Rotate(topic, partitionCount);
    }
}

/// <summary>
/// Sticky partitioner - sticks to a partition for null keys until batch is full.
/// Uses ConcurrentDictionary for lock-free read access in the hot path.
/// </summary>
public sealed class StickyPartitioner : IPartitioner, IBatchCompletionAwarePartitioner
{
    private readonly StickyPartitionTracker _stickyPartitionTracker = new();

    public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount)
    {
        if (keyIsNull || key.Length == 0)
        {
            return _stickyPartitionTracker.GetOrAssign(topic, partitionCount);
        }

        return Murmur2.Partition(key, partitionCount);
    }

    /// <summary>
    /// Called when a batch is sent to switch to a new partition.
    /// </summary>
    public void OnBatchComplete(string topic, int partitionCount)
    {
        _stickyPartitionTracker.Rotate(topic, partitionCount);
    }
}

/// <summary>
/// Round-robin partitioner - cycles through partitions.
/// </summary>
public sealed class RoundRobinPartitioner : IPartitioner
{
    // Non-atomic increment is intentional: occasional duplicate reads are benign,
    // and distribution remains even over time without Interlocked contention.
    private uint _counter;

    public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount)
    {
        return (int)(++_counter % (uint)partitionCount);
    }
}

/// <summary>
/// Random partitioner - ignores keys and distributes across partitions.
/// </summary>
public sealed class RandomPartitioner : IPartitioner
{
    private readonly RandomPartitionSelector _randomPartitionSelector = new();

    public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount)
    {
        return _randomPartitionSelector.NextPartition(partitionCount);
    }
}

/// <summary>
/// librdkafka consistent partitioner - CRC32 hash of key.
/// Null and empty keys map to the same partition.
/// </summary>
public sealed class ConsistentPartitioner : IPartitioner
{
    public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount)
    {
        return LibrdkafkaCrc32.Partition(key, partitionCount);
    }
}

/// <summary>
/// librdkafka consistent_random partitioner - CRC32 for non-empty keys, random for null or empty keys.
/// </summary>
public sealed class ConsistentRandomPartitioner : IPartitioner
{
    private readonly RandomPartitionSelector _randomPartitionSelector = new();

    public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount)
    {
        return key.Length == 0
            ? _randomPartitionSelector.NextPartition(partitionCount)
            : LibrdkafkaCrc32.Partition(key, partitionCount);
    }
}

/// <summary>
/// librdkafka murmur2 partitioner - Java-compatible Murmur2 hash of key.
/// Null keys map to the hash of an empty key.
/// </summary>
public sealed class Murmur2Partitioner : IPartitioner
{
    public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount)
    {
        return Murmur2.Partition(key, partitionCount);
    }
}

/// <summary>
/// librdkafka murmur2_random partitioner - Murmur2 for non-null keys, random for null keys.
/// </summary>
public sealed class Murmur2RandomPartitioner : IPartitioner
{
    private readonly RandomPartitionSelector _randomPartitionSelector = new();

    public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount)
    {
        return keyIsNull
            ? _randomPartitionSelector.NextPartition(partitionCount)
            : Murmur2.Partition(key, partitionCount);
    }
}

/// <summary>
/// librdkafka fnv1a partitioner - Sarama-compatible FNV-1a hash of key.
/// Null and empty keys map to the same partition.
/// </summary>
public sealed class Fnv1APartitioner : IPartitioner
{
    public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount)
    {
        return Fnv1A.Partition(key, partitionCount);
    }
}

/// <summary>
/// librdkafka fnv1a_random partitioner - FNV-1a for non-null keys, random for null keys.
/// </summary>
public sealed class Fnv1ARandomPartitioner : IPartitioner
{
    private readonly RandomPartitionSelector _randomPartitionSelector = new();

    public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount)
    {
        return keyIsNull
            ? _randomPartitionSelector.NextPartition(partitionCount)
            : Fnv1A.Partition(key, partitionCount);
    }
}

internal sealed class RandomPartitionSelector
{
    private const int GoldenRatioIncrement = unchecked((int)0x9e3779b9);
    private const uint MixMultiplier1 = 0x7feb352d;
    private const uint MixMultiplier2 = 0x846ca68b;

    private int _state = Environment.TickCount;

    public int NextPartition(int partitionCount)
    {
        var value = unchecked((uint)Interlocked.Add(ref _state, GoldenRatioIncrement));
        value ^= value >> 16;
        value *= MixMultiplier1;
        value ^= value >> 15;
        value *= MixMultiplier2;
        value ^= value >> 16;
        return (int)(value % (uint)partitionCount);
    }
}

internal static class LibrdkafkaCrc32
{
    public static int Partition(ReadOnlySpan<byte> key, int partitionCount)
    {
        return (int)(Hash(key) % (uint)partitionCount);
    }

    public static uint Hash(ReadOnlySpan<byte> key)
    {
        return Crc32.HashToUInt32(key);
    }
}

internal static class Fnv1A
{
    private const uint Offset = 0x811c9dc5;
    private const uint Prime = 0x01000193;

    public static int Partition(ReadOnlySpan<byte> key, int partitionCount)
    {
        return (int)(Hash(key) % (uint)partitionCount);
    }

    public static uint Hash(ReadOnlySpan<byte> key)
    {
        var hash = Offset;

        foreach (var value in key)
        {
            hash ^= value;
            hash *= Prime;
        }

        var signedHash = unchecked((int)hash);
        return signedHash < 0
            ? unchecked((uint)-signedHash)
            : hash;
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
