using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
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

internal interface IUniformStickyPartitioner
{
    bool UsesStickyPartition(ReadOnlySpan<byte> key, bool keyIsNull);
    void OnRecordAppended(string topic, int partition, int bytes, int partitionCount);
    void SetPartitionQueueByteProvider(Func<string, int, long> partitionQueueBytes);
}

internal sealed class StickyPartitionTracker
{
    private readonly ConcurrentDictionary<string, StickyPartitionState> _partitions = new();
    private readonly bool _adaptivePartitioning;
    private readonly int _availabilityTimeoutMs;
    private readonly RandomPartitionSelector _randomPartitionSelector = new();
    private readonly ConcurrentDictionary<(string Topic, int Partition), PartitionAvailabilityState> _availability = new();
    private Func<string, int, long>? _partitionQueueBytes;
#if NETSTANDARD2_0
    private int _counter;
#else
    private uint _counter;
#endif

    public StickyPartitionTracker(bool adaptivePartitioning = false, int availabilityTimeoutMs = 0)
    {
        if (availabilityTimeoutMs < 0)
            throw new ArgumentOutOfRangeException(nameof(availabilityTimeoutMs), "Availability timeout cannot be negative");

        _adaptivePartitioning = adaptivePartitioning;
        _availabilityTimeoutMs = availabilityTimeoutMs;
    }

    public void SetPartitionQueueByteProvider(Func<string, int, long> partitionQueueBytes)
    {
        _partitionQueueBytes = partitionQueueBytes ?? throw new ArgumentNullException(nameof(partitionQueueBytes));
    }

    public int GetOrAssign(string topic, int partitionCount)
    {
        var state = _partitions.GetOrAdd(topic,
            topicName => new StickyPartitionState(NextPartition(topicName, partitionCount, currentPartition: null)));

        while (true)
        {
            var packedState = Volatile.Read(ref state.PackedState);
            var partition = UnpackPartition(packedState);
            if (partition >= 0 && partition < partitionCount)
                return partition;

            var nextPartition = NextPartition(topic, partitionCount, currentPartition: null);
            var nextState = PackState(nextPartition, producedBytes: 0);
            if (Interlocked.CompareExchange(ref state.PackedState, nextState, packedState) == packedState)
                return nextPartition;
        }
    }

    public void Rotate(string topic, int partitionCount)
    {
        var state = _partitions.GetOrAdd(topic,
            topicName => new StickyPartitionState(NextPartition(topicName, partitionCount, currentPartition: null)));

        while (true)
        {
            var packedState = Volatile.Read(ref state.PackedState);
            var partition = UnpackPartition(packedState);
            var nextPartition = NextPartition(topic, partitionCount, partition);
            var nextState = PackState(nextPartition, producedBytes: 0);
            if (Interlocked.CompareExchange(ref state.PackedState, nextState, packedState) == packedState)
                return;
        }
    }

    public void OnRecordAppended(string topic, int partition, int bytes, int partitionCount, int stickyBatchSize)
    {
        if (bytes <= 0 || partitionCount <= 1)
            return;

        var state = _partitions.GetOrAdd(topic, _ => new StickyPartitionState(partition));
        while (true)
        {
            var packedState = Volatile.Read(ref state.PackedState);
            if (UnpackPartition(packedState) != partition)
                return;

            var producedBytes = SaturatingAdd(UnpackProducedBytes(packedState), bytes);
            if (producedBytes < stickyBatchSize)
            {
                var updatedState = PackState(partition, producedBytes);
                if (Interlocked.CompareExchange(ref state.PackedState, updatedState, packedState) == packedState)
                    return;

                continue;
            }

            var nextPartition = NextPartition(topic, partitionCount, partition);
            var nextState = PackState(nextPartition, producedBytes: 0);
            if (Interlocked.CompareExchange(ref state.PackedState, nextState, packedState) == packedState)
                return;
        }
    }

#if NETSTANDARD2_0
    private int NextSequentialPartition(int partitionCount)
        => (int)((uint)Interlocked.Increment(ref _counter) % (uint)partitionCount);
#else
    private int NextSequentialPartition(int partitionCount)
        => (int)(Interlocked.Increment(ref _counter) % (uint)partitionCount);
#endif

    private int NextPartition(string topic, int partitionCount, int? currentPartition)
    {
        var nextPartition = TryNextAdaptivePartition(topic, partitionCount, currentPartition) ?? NextSequentialPartition(partitionCount);
        if (partitionCount > 1 && currentPartition == nextPartition)
            nextPartition = (nextPartition + 1) % partitionCount;
        return nextPartition;
    }

    private int? TryNextAdaptivePartition(string topic, int partitionCount, int? currentPartition)
    {
        var partitionQueueBytes = _partitionQueueBytes;
        if (!_adaptivePartitioning || partitionQueueBytes is null || partitionCount < 2)
            return null;

        Span<long> queueSizes = partitionCount <= 64 ? stackalloc long[partitionCount] : new long[partitionCount];
        var eligibleCount = 0;
        var allEqual = true;
        var maxQueueSize = 0L;
        long? firstQueueSize = null;

        for (var partition = 0; partition < partitionCount; partition++)
        {
            if (currentPartition == partition)
            {
                queueSizes[partition] = -1;
                continue;
            }

            var queueSize = Math.Max(0, partitionQueueBytes(topic, partition));
            if (IsUnavailable(topic, partition, queueSize))
            {
                queueSizes[partition] = -1;
                continue;
            }

            queueSizes[partition] = queueSize;
            eligibleCount++;
            firstQueueSize ??= queueSize;
            if (queueSize != firstQueueSize.Value)
                allEqual = false;
            if (queueSize > maxQueueSize)
                maxQueueSize = queueSize;
        }

        if (eligibleCount == 0)
        {
            // Every other partition has stayed backed up past the timeout. Fall back to
            // the weighted choice across all partitions; NextPartition still prevents a
            // no-op rotation if the current partition wins.
            for (var partition = 0; partition < partitionCount; partition++)
            {
                var queueSize = Math.Max(0, partitionQueueBytes(topic, partition));
                queueSizes[partition] = queueSize;
                if (partition == 0)
                {
                    firstQueueSize = queueSize;
                    maxQueueSize = queueSize;
                }
                else
                {
                    if (queueSize != firstQueueSize!.Value)
                        allEqual = false;
                    if (queueSize > maxQueueSize)
                        maxQueueSize = queueSize;
                }
            }

            eligibleCount = partitionCount;
        }

        if (allEqual && eligibleCount == partitionCount)
            return null;

        var maxWeightSource = maxQueueSize == long.MaxValue ? long.MaxValue : maxQueueSize + 1;
        var totalWeight = 0L;
        for (var partition = 0; partition < partitionCount; partition++)
        {
            var queueSize = queueSizes[partition];
            if (queueSize < 0)
                continue;

            var weight = Math.Max(1L, maxWeightSource - queueSize);
            totalWeight = SaturatingAdd(totalWeight, weight);
        }

        if (totalWeight <= 0)
            return null;

        var target = PositiveRandomLong(totalWeight);
        var cumulative = 0L;
        for (var partition = 0; partition < partitionCount; partition++)
        {
            var queueSize = queueSizes[partition];
            if (queueSize < 0)
                continue;

            var weight = Math.Max(1L, maxWeightSource - queueSize);
            cumulative = SaturatingAdd(cumulative, weight);
            if (target < cumulative)
                return partition;
        }

        return null;
    }

    private bool IsUnavailable(string topic, int partition, long queueSize)
    {
        if (_availabilityTimeoutMs <= 0)
            return false;

        var key = (topic, partition);
        if (queueSize <= 0)
        {
            _availability.TryRemove(key, out _);
            return false;
        }

        var state = _availability.GetOrAdd(key, static _ => new PartitionAvailabilityState(Stopwatch.GetTimestamp()));
        var elapsedMs = (Stopwatch.GetTimestamp() - Volatile.Read(ref state.BusySinceTimestamp)) * 1000 / Stopwatch.Frequency;
        return elapsedMs >= _availabilityTimeoutMs;
    }

    private long PositiveRandomLong(long upperExclusive)
    {
        var positiveRandom = (uint)_randomPartitionSelector.NextPartition(int.MaxValue);
        return positiveRandom % upperExclusive;
    }

    private static long PackState(int partition, int producedBytes)
        => ((long)partition << 32) | (uint)producedBytes;

    private static int UnpackPartition(long packedState)
        => (int)(packedState >> 32);

    private static int UnpackProducedBytes(long packedState)
        => (int)(packedState & 0x7fff_ffff);

    private static int SaturatingAdd(int left, int right)
        => left > int.MaxValue - right ? int.MaxValue : left + right;

    private static long SaturatingAdd(long left, long right)
    {
        var result = left + right;
        return result < left ? long.MaxValue : result;
    }

    private sealed class StickyPartitionState(int partition)
    {
        public long PackedState = PackState(partition, producedBytes: 0);
    }

    private sealed class PartitionAvailabilityState(long busySinceTimestamp)
    {
        public long BusySinceTimestamp = busySinceTimestamp;
    }
}

/// <summary>
/// Default partitioner - uses murmur2 hash of key, or sticky partitioning for null keys.
/// </summary>
public sealed class DefaultPartitioner : IPartitioner, IBatchCompletionAwarePartitioner, IUniformStickyPartitioner
{
    private readonly StickyPartitionTracker _stickyPartitionTracker;
    private readonly int _stickyBatchSize;
    private readonly bool _ignoreKeys;

    public DefaultPartitioner(
        int stickyBatchSize = int.MaxValue,
        bool adaptivePartitioning = false,
        int availabilityTimeoutMs = 0,
        bool ignoreKeys = false)
    {
        if (stickyBatchSize < 1)
            throw new ArgumentOutOfRangeException(nameof(stickyBatchSize), "Sticky batch size must be at least 1 byte");

        _stickyBatchSize = stickyBatchSize;
        _ignoreKeys = ignoreKeys;
        _stickyPartitionTracker = new StickyPartitionTracker(adaptivePartitioning, availabilityTimeoutMs);
    }

    public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount)
    {
        if (UsesStickyPartition(key, keyIsNull))
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

    bool IUniformStickyPartitioner.UsesStickyPartition(ReadOnlySpan<byte> key, bool keyIsNull)
        => UsesStickyPartition(key, keyIsNull);

    void IUniformStickyPartitioner.OnRecordAppended(string topic, int partition, int bytes, int partitionCount)
    {
        _stickyPartitionTracker.OnRecordAppended(topic, partition, bytes, partitionCount, _stickyBatchSize);
    }

    void IUniformStickyPartitioner.SetPartitionQueueByteProvider(Func<string, int, long> partitionQueueBytes)
    {
        _stickyPartitionTracker.SetPartitionQueueByteProvider(partitionQueueBytes);
    }

    private bool UsesStickyPartition(ReadOnlySpan<byte> key, bool keyIsNull)
        => _ignoreKeys || keyIsNull || key.Length == 0;
}

/// <summary>
/// Sticky partitioner - sticks to a partition for null keys until batch is full.
/// </summary>
public sealed class StickyPartitioner : IPartitioner, IBatchCompletionAwarePartitioner, IUniformStickyPartitioner
{
    private readonly StickyPartitionTracker _stickyPartitionTracker;
    private readonly int _stickyBatchSize;
    private readonly bool _ignoreKeys;

    public StickyPartitioner(
        int stickyBatchSize = int.MaxValue,
        bool adaptivePartitioning = false,
        int availabilityTimeoutMs = 0,
        bool ignoreKeys = false)
    {
        if (stickyBatchSize < 1)
            throw new ArgumentOutOfRangeException(nameof(stickyBatchSize), "Sticky batch size must be at least 1 byte");

        _stickyBatchSize = stickyBatchSize;
        _ignoreKeys = ignoreKeys;
        _stickyPartitionTracker = new StickyPartitionTracker(adaptivePartitioning, availabilityTimeoutMs);
    }

    public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount)
    {
        if (UsesStickyPartition(key, keyIsNull))
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

    bool IUniformStickyPartitioner.UsesStickyPartition(ReadOnlySpan<byte> key, bool keyIsNull)
        => UsesStickyPartition(key, keyIsNull);

    void IUniformStickyPartitioner.OnRecordAppended(string topic, int partition, int bytes, int partitionCount)
    {
        _stickyPartitionTracker.OnRecordAppended(topic, partition, bytes, partitionCount, _stickyBatchSize);
    }

    void IUniformStickyPartitioner.SetPartitionQueueByteProvider(Func<string, int, long> partitionQueueBytes)
    {
        _stickyPartitionTracker.SetPartitionQueueByteProvider(partitionQueueBytes);
    }

    private bool UsesStickyPartition(ReadOnlySpan<byte> key, bool keyIsNull)
        => _ignoreKeys || keyIsNull || key.Length == 0;
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
