namespace Dekaf.Producer;

/// <summary>
/// Interface for message partitioners.
/// </summary>
public interface IPartitioner
{
    /// <summary>
    /// Selects a partition for a message.
    /// </summary>
    int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount);
}

/// <summary>
/// Default partitioner - uses murmur2 hash of key, or round-robin for null keys.
/// </summary>
public sealed class DefaultPartitioner : IPartitioner
{
    private uint _counter;

    public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount)
    {
        if (keyIsNull || key.Length == 0)
        {
            // Round-robin for null keys - use uint to avoid overflow to negative values
            return (int)(Interlocked.Increment(ref _counter) % (uint)partitionCount);
        }

        // Murmur2 hash for consistent partitioning
        return (int)(Murmur2.Hash(key) % partitionCount);
    }
}

/// <summary>
/// Sticky partitioner - sticks to a partition for null keys until batch is full.
/// </summary>
public sealed class StickyPartitioner : IPartitioner
{
    private readonly Dictionary<string, int> _stickyPartitions = new(capacity: 16);
    private readonly object _lock = new();
    private uint _counter;

    public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount)
    {
        if (keyIsNull || key.Length == 0)
        {
            lock (_lock)
            {
                if (!_stickyPartitions.TryGetValue(topic, out var partition))
                {
                    // Use uint to avoid overflow to negative values
                    partition = (int)(Interlocked.Increment(ref _counter) % (uint)partitionCount);
                    _stickyPartitions[topic] = partition;
                }
                return partition;
            }
        }

        return (int)(Murmur2.Hash(key) % partitionCount);
    }

    /// <summary>
    /// Called when a batch is sent to switch to a new partition.
    /// </summary>
    public void OnBatchComplete(string topic, int partitionCount)
    {
        lock (_lock)
        {
            // Use uint to avoid overflow to negative values
            _stickyPartitions[topic] = (int)(Interlocked.Increment(ref _counter) % (uint)partitionCount);
        }
    }
}

/// <summary>
/// Round-robin partitioner - cycles through partitions.
/// </summary>
public sealed class RoundRobinPartitioner : IPartitioner
{
    private uint _counter;

    public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount)
    {
        // Use uint to avoid overflow to negative values
        return (int)(Interlocked.Increment(ref _counter) % (uint)partitionCount);
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

    public static uint Hash(ReadOnlySpan<byte> data)
    {
        var length = data.Length;
        var h = Seed ^ (uint)length;
        var offset = 0;

        while (length >= 4)
        {
            var k = (uint)(data[offset] |
                          (data[offset + 1] << 8) |
                          (data[offset + 2] << 16) |
                          (data[offset + 3] << 24));

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
