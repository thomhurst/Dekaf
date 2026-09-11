using System.Buffers.Binary;
using Dekaf.StressTests.Metrics;

namespace Dekaf.StressTests.Scenarios;

internal static class KeyedConsumerWorkload
{
    internal const int KeyCount = 32;
    internal const int DefaultRecordsPerPartition = 32768;
    internal const int HandlerConcurrency = 4;
    internal const int BufferedRecords = 256;
    internal const int YieldInterval = 64;

    internal static int KeySize(string shape) => shape switch
    {
        "scalar" => sizeof(int),
        "binary" => 16,
        "large-distinct" or "large-colliding" => 4096,
        _ => throw new ArgumentException($"Unknown keyed consumer shape: {shape}.")
    };

    internal static void Validate(string shape, int partitions, int recordsPerPartition, int messageSize)
    {
        _ = KeySize(shape);
        if (partitions is < 1 or > 6 || recordsPerPartition is < KeyCount or > 65536
            || recordsPerPartition % KeyCount != 0 || messageSize is < 8 or > 4096)
            throw new ArgumentException("Keyed replay requires 1..6 partitions, 32..65536 records per partition (multiple of 32), and 8..4096 value bytes.");
    }

    internal static byte[] CreateBinaryKey(string shape, int id)
    {
        var key = new byte[KeySize(shape)];
        key.AsSpan().Fill(0x5a);
        // Keep all four sampled windows identical for the collision shape. The ID
        // precedes the final 16-byte sample and remains part of content equality.
        var position = shape == "large-colliding" ? key.Length - 32 : 0;
        BinaryPrimitives.WriteInt32LittleEndian(key.AsSpan(position), id);
        return key;
    }

    internal static int ReadBinaryKey(string shape, byte[] key)
    {
        if (key.Length != KeySize(shape)) throw new InvalidOperationException("Unexpected key size.");
        return BinaryPrimitives.ReadInt32LittleEndian(key.AsSpan(shape == "large-colliding" ? key.Length - 32 : 0));
    }
}

// Independent sequence state is per partition AND logical key. A successful pass
// must include every seeded record; a duration boundary never discards its tail.
internal sealed class KeyedConsumerPass
{
    private readonly Slot[] _slots;
    private readonly int _recordsPerPartition;
    private readonly ThroughputTracker _throughput;
    private readonly CancellationTokenSource _stop;
    private long _completed;
    internal long Completed => Interlocked.Read(ref _completed);
    internal long Expected { get; }

    internal KeyedConsumerPass(int partitions, int recordsPerPartition, ThroughputTracker throughput, CancellationTokenSource stop)
    {
        _recordsPerPartition = recordsPerPartition;
        _throughput = throughput;
        _stop = stop;
        Expected = (long)partitions * recordsPerPartition;
        _slots = new Slot[partitions * KeyedConsumerWorkload.KeyCount];
        for (var i = 0; i < _slots.Length; i++)
            _slots[i].Next = i % KeyedConsumerWorkload.KeyCount;
    }

    internal int Enter(int partition, int key, long offset, ReadOnlySpan<byte> value)
    {
        if ((uint)partition >= (uint)(_slots.Length / KeyedConsumerWorkload.KeyCount)
            || (uint)key >= KeyedConsumerWorkload.KeyCount || offset < 0 || offset >= _recordsPerPartition
            || value.Length < sizeof(long) || BinaryPrimitives.ReadInt64LittleEndian(value) != offset)
            throw new InvalidOperationException("Unexpected keyed replay record or payload sequence.");
        var index = partition * KeyedConsumerWorkload.KeyCount + key;
        ref var slot = ref _slots[index];
        if (Interlocked.CompareExchange(ref slot.Active, 1, 0) != 0)
            throw new InvalidOperationException("Equal-key handlers overlapped within one partition.");
        if (slot.Next != offset)
        {
            Volatile.Write(ref slot.Active, 0);
            throw new InvalidOperationException($"Keyed replay order violation: partition={partition}, key={key}, expected={slot.Next}, actual={offset}.");
        }
        return index;
    }

    internal void Complete(int index, int valueBytes)
    {
        ref var slot = ref _slots[index];
        slot.Next += KeyedConsumerWorkload.KeyCount;
        _throughput.RecordMessage(valueBytes);
        Volatile.Write(ref slot.Active, 0);
        if (Interlocked.Increment(ref _completed) == Expected) _stop.Cancel();
    }

    internal void ValidateComplete()
    {
        if (Completed != Expected) throw new InvalidOperationException($"Keyed replay incomplete: {Completed}/{Expected} records completed.");
        for (var i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].Active != 0 || _slots[i].Next != _recordsPerPartition + i % KeyedConsumerWorkload.KeyCount)
                throw new InvalidOperationException("Keyed replay did not drain every handler and sequence.");
        }
    }

    private struct Slot
    {
        internal long Next;
        internal int Active;
    }
}

internal sealed class KeyedConsumerSnapshot
{
    public required string Shape { get; init; }
    public required int KeySizeBytes { get; init; }
    public required int Partitions { get; init; }
    public int KeysPerPartition => KeyedConsumerWorkload.KeyCount;
    public required int RecordsPerPartition { get; init; }
    public int HandlerConcurrency => KeyedConsumerWorkload.HandlerConcurrency;
    public int BufferedRecordsPerPartition => KeyedConsumerWorkload.BufferedRecords;
    public int YieldEveryKeyRecords => KeyedConsumerWorkload.YieldInterval;
    public required long CompletedPasses { get; init; }
    public required long CompletedRecords { get; init; }
    public required double ReplayBookkeepingSeconds { get; init; }
}
