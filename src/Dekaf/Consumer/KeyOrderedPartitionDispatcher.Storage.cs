using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Dekaf.Consumer;

internal sealed partial class KeyOrderedPartitionDispatcher<TKey, TValue>
{
    // A dictionary entry aligns its lane reference after the key. Compact only when
    // that alignment bucket shrinks; larger keys retain their existing representation.
    // The size comparison folds away for reference keys before the binary type checks.
    private static bool UseCompactKeys
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ((Unsafe.SizeOf<PartitionMessageKey<TKey>.Uncached>() + IntPtr.Size - 1) & -IntPtr.Size)
            < ((Unsafe.SizeOf<PartitionMessageKey<TKey>>() + IntPtr.Size - 1) & -IntPtr.Size)
            && typeof(TKey) != typeof(byte[])
            && typeof(TKey) != typeof(ReadOnlyMemory<byte>)
            && typeof(TKey) != typeof(Memory<byte>)
            && typeof(TKey) != typeof(ArraySegment<byte>);
    }

    // Only the constructor assigns _lanes, using the same predicate as every access.
    // These exact casts avoid a wrapper allocation and per-record interface dispatch.
    private Dictionary<PartitionMessageKey<TKey>.Uncached, KeyLane> CompactLanes =>
        Unsafe.As<Dictionary<PartitionMessageKey<TKey>.Uncached, KeyLane>>(_lanes);

    private Dictionary<PartitionMessageKey<TKey>, KeyLane> StandardLanes =>
        Unsafe.As<Dictionary<PartitionMessageKey<TKey>, KeyLane>>(_lanes);

    private int StorageCount => UseCompactKeys ? CompactLanes.Count : StandardLanes.Count;

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetLane(PartitionMessageKey<TKey> key, out KeyLane lane) => UseCompactKeys
        ? CompactLanes.TryGetValue(new(key), out lane!)
        : StandardLanes.TryGetValue(key, out lane!);

    private void AddLane(PartitionMessageKey<TKey> key, KeyLane lane)
    {
        if (UseCompactKeys) CompactLanes.Add(new(key), lane);
        else StandardLanes.Add(key, lane);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool RemoveLane(PartitionMessageKey<TKey> key) => UseCompactKeys
        ? CompactLanes.Remove(new(key)) : StandardLanes.Remove(key);
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref KeyLane? GetLaneEntry(PartitionMessageKey<TKey> key)
    {
        if (UseCompactKeys)
            return ref CollectionsMarshal.GetValueRefOrAddDefault(CompactLanes, new(key), out _);
        return ref CollectionsMarshal.GetValueRefOrAddDefault(StandardLanes, key, out _);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool RemoveLane(PartitionMessageKey<TKey> key, out KeyLane? lane) => UseCompactKeys
        ? CompactLanes.Remove(new(key), out lane) : StandardLanes.Remove(key, out lane);
#endif

    private void ReleaseLanes()
    {
        if (UseCompactKeys)
        {
            var lanes = CompactLanes;
            foreach (var lane in lanes.Values) lane?.ReleaseKey();
            lanes.Clear();
        }
        else
        {
            var lanes = StandardLanes;
            foreach (var lane in lanes.Values) lane?.ReleaseKey();
            lanes.Clear();
        }
    }
}
