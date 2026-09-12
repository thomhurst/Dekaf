using System.IO.Hashing;
using System.Runtime.CompilerServices;
using Dekaf.Producer;

namespace Dekaf.Consumer;

internal readonly struct PartitionMessageKey<TKey> : IEquatable<PartitionMessageKey<TKey>>
{
    private enum KeyKind : byte
    {
        WireNull,
        Value,
        DeserializedNull
    }

    private readonly TKey? _value;
    private readonly int _binaryHashCode;
    private readonly KeyKind _kind;
    internal bool HasBinaryHashCode { get; }
    internal int BinaryHashCode => _binaryHashCode;

    internal bool HasValue => _kind == KeyKind.Value;

    internal TKey? Value => _value;

    private PartitionMessageKey(TKey? value, KeyKind kind)
    {
        _value = value;
        _kind = kind;
    }

    private PartitionMessageKey(PartitionMessageKey<TKey> key, int binaryHashCode)
    {
        _value = key._value;
        _kind = key._kind;
        _binaryHashCode = binaryHashCode;
        HasBinaryHashCode = true;
    }

    internal PartitionMessageKey<TKey> WithBinaryHashCode(int hashCode) => new(this, hashCode);

    public static PartitionMessageKey<TKey> From(TKey? value, bool isKeyNull = false)
    {
        if (isKeyNull)
            return default;

        // A deserializer can return null for a non-null wire key. Keep that lane
        // separate from Kafka null keys without passing either null to a comparer.
        return new PartitionMessageKey<TKey>(value, value is null ? KeyKind.DeserializedNull : KeyKind.Value);
    }

    public bool Equals(PartitionMessageKey<TKey> other)
    {
        if (_kind != other._kind)
            return false;

        return !HasValue || EqualityComparer<TKey>.Default.Equals(_value!, other._value!);
    }

    public override bool Equals(object? obj) => obj is PartitionMessageKey<TKey> other && Equals(other);

    public override int GetHashCode()
    {
        return HasValue
            ? EqualityComparer<TKey>.Default.GetHashCode(_value!)
            : 0;
    }

    internal bool Equals(PartitionMessageKey<TKey> other, IEqualityComparer<TKey> comparer)
        => HasSameKind(other) && (!HasValue || comparer.Equals(_value!, other._value!));

    internal int GetHashCode(IEqualityComparer<TKey> comparer)
        => HasValue ? comparer.GetHashCode(_value!) : 0;

    internal bool HasSameKind(PartitionMessageKey<TKey> other) => _kind == other._kind;

    // Scalar dictionary entries do not need the binary hash retained by a lane.
    // Keep the kind independent of the value so wire null never aliases default(TKey).
    internal readonly struct Uncached(PartitionMessageKey<TKey> key) : IEquatable<Uncached>
    {
        private readonly TKey? _value = key._value;
        private readonly KeyKind _kind = key._kind;

        public bool Equals(Uncached other) => _kind == other._kind
            && (_kind != KeyKind.Value || EqualityComparer<TKey>.Default.Equals(_value!, other._value!));

        public override bool Equals(object? obj) => obj is Uncached other && Equals(other);

        public override int GetHashCode() => _kind == KeyKind.Value
            ? EqualityComparer<TKey>.Default.GetHashCode(_value!) : 0;

        internal bool Equals(Uncached other, IEqualityComparer<TKey> comparer) => _kind == other._kind
            && (_kind != KeyKind.Value || comparer.Equals(_value!, other._value!));

        internal int GetHashCode(IEqualityComparer<TKey> comparer) => _kind == KeyKind.Value
            ? comparer.GetHashCode(_value!) : 0;
    }
}

internal static class PartitionMessageKeyComparer<TKey>
{
    // Resolve the default once per dispatcher, before records enter it. Scalar keys keep the
    // dictionary's default value-type comparer and its existing IEquatable<T> fast path.
    public static IEqualityComparer<PartitionMessageKey<TKey>>? Default =>
        typeof(TKey) == typeof(byte[])
            || typeof(TKey) == typeof(ReadOnlyMemory<byte>)
            || typeof(TKey) == typeof(Memory<byte>)
            || typeof(TKey) == typeof(ArraySegment<byte>)
                ? new BinaryPartitionMessageKeyComparer<TKey>()
                : null;
}

// Dispatch computes the content hash once. The lane retains that key and its
// backing storage, so cleanup can reuse the hash without scanning the bytes again.
internal sealed class BinaryPartitionMessageKeyComparer<TKey> : IEqualityComparer<PartitionMessageKey<TKey>>
{
    private const int CollisionThreshold = 8;
    // Keep comparers at one field. Rent state when the first sampled lane appears;
    // allocate its bounded key slots only if an expensive collision occurs.
    private CollisionState? _state;
    internal bool NeedsFullHashing => _state is { SampledLanes: < 0, Count: CollisionThreshold };
    internal bool UsesSampledHashing => _state is null || _state.SampledLanes < 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(PartitionMessageKey<TKey> x, PartitionMessageKey<TKey> y)
    {
        if (!x.HasSameKind(y))
            return false;
        if (!x.HasValue)
            return true;
        var left = GetBytes(x.Value!);
        var right = GetBytes(y.Value!);
        if (left.SequenceEqual(right))
            return true;
        return ObserveCollision(x, left.Length, right.Length);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool ObserveCollision(PartitionMessageKey<TKey> key, int leftLength, int rightLength)
    {
        // Count expensive collisions within one dictionary probe, not across the
        // lifetime of a healthy dispatcher. Only its single coordinator uses this comparer.
        if (UsesSampledHashing && leftLength > 64 && leftLength == rightLength)
        {
            var state = _state ??= CollisionStatePool.Instance.Rent();
            if (state.Count < CollisionThreshold)
            {
                var keys = state.Keys ??= new PartitionMessageKey<TKey>[CollisionThreshold];
                keys[state.Count++] = key;
            }
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetHashCode(PartitionMessageKey<TKey> obj)
    {
        if (_state is { Count: > 0 } state)
        {
            Array.Clear(state.Keys!, 0, state.Count);
            state.Count = 0;
        }
        if (obj.HasBinaryHashCode)
            return obj.BinaryHashCode;
        return ComputeHashCode(obj);
    }

    internal int ComputeHashCode(PartitionMessageKey<TKey> obj) => ComputeHashCode(obj, out _);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int ComputeHashCode(PartitionMessageKey<TKey> obj, out bool sampled)
    {
        sampled = false;
        if (!obj.HasValue) return 0;
        var bytes = GetBytes(obj.Value!);
        sampled = bytes.Length > 64 && UsesSampledHashing;
        return sampled ? HashSample(bytes) : unchecked((int)XxHash3.HashToUInt64(bytes));
    }

    internal void AddSampledLane()
    {
        var state = _state ??= CollisionStatePool.Instance.Rent();
        state.SampledLanes--;
    }

    internal void EnableFullHashing()
    {
        var state = _state ??= CollisionStatePool.Instance.Rent();
        if (state.SampledLanes < 0) state.SampledLanes = ~state.SampledLanes;
    }

    internal PartitionMessageKey<TKey>[] TakeCollisionKeys(out int count)
    {
        var state = _state!;
        count = state.Count;
        // Migration performs dictionary probes. They must not reset these keys
        // before the dispatcher has visited the captured collision group.
        state.Count = 0;
        return state.Keys!;
    }

    internal void ReleaseCollisionState()
    {
        if (_state is not { } state) return;
        _state = null;
        CollisionStatePool.Instance.Return(state);
    }

    internal void RemoveSampledLane()
    {
        if (_state is not { } state) return;
        if (state.SampledLanes < -1) state.SampledLanes++;
        else if (state.SampledLanes > 0) state.SampledLanes--;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGetSampledKey(PartitionMessageKey<TKey> key, out PartitionMessageKey<TKey> sampledKey)
    {
        sampledKey = default;
        if (_state is not { SampledLanes: > 0 } || !key.HasValue) return false;
        var bytes = GetBytes(key.Value!);
        if (bytes.Length <= 64) return false;
        sampledKey = key.WithBinaryHashCode(HashSample(bytes));
        return true;
    }

    private sealed class CollisionState
    {
        internal PartitionMessageKey<TKey>[]? Keys;
        internal int Count;
        // Before promotion, encode the count as its complement; afterward count
        // remaining sampled lanes directly. The sign also identifies hashing mode.
        internal int SampledLanes = -1;
    }

    private sealed class CollisionStatePool() : ObjectPool<CollisionState>(maxPoolSize: 64, threadLocalFastPath: false)
    {
        internal static readonly CollisionStatePool Instance = new();
        protected override CollisionState Create() => new();
        protected override void Reset(CollisionState state)
        {
            if (state.Keys is { } keys) Array.Clear(keys, 0, keys.Length);
            state.Count = 0;
            state.SampledLanes = -1;
        }
    }

    private static int HashSample(ReadOnlySpan<byte> bytes)
    {
        // Equality still compares every byte. Only the triggering collision group
        // changes hashes; other active lanes retain their cached sample until drained.
        Span<byte> sample = stackalloc byte[64];
        bytes[..16].CopyTo(sample);
        bytes.Slice(bytes.Length / 3, 16).CopyTo(sample[16..]);
        bytes.Slice(bytes.Length / 3 * 2, 16).CopyTo(sample[32..]);
        bytes[^16..].CopyTo(sample[48..]);
        return unchecked((int)XxHash3.HashToUInt64(sample, bytes.Length));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReadOnlySpan<byte> GetBytes(TKey value)
    {
        // Exact type checks let the JIT specialize value keys without boxing or interface dispatch.
        if (typeof(TKey) == typeof(byte[]))
            return Unsafe.As<TKey, byte[]>(ref value);
        if (typeof(TKey) == typeof(ReadOnlyMemory<byte>))
            return Unsafe.As<TKey, ReadOnlyMemory<byte>>(ref value).Span;
        if (typeof(TKey) == typeof(Memory<byte>))
            return Unsafe.As<TKey, Memory<byte>>(ref value).Span;

        if (typeof(TKey) == typeof(ArraySegment<byte>))
            return Unsafe.As<TKey, ArraySegment<byte>>(ref value).AsSpan();

        throw new InvalidOperationException("The key type does not represent binary memory.");
    }
}

// One adapter per dispatcher, never per record. Null keys retain their separate lane even when
// a user comparer would otherwise equate null with a non-null key.
internal sealed class CustomPartitionMessageKeyComparer<TKey>(IEqualityComparer<TKey> comparer)
    : IEqualityComparer<PartitionMessageKey<TKey>>
{
    public bool Equals(PartitionMessageKey<TKey> x, PartitionMessageKey<TKey> y) => x.Equals(y, comparer);

    public int GetHashCode(PartitionMessageKey<TKey> obj) => obj.GetHashCode(comparer);
}

internal sealed class CustomUncachedPartitionMessageKeyComparer<TKey>(IEqualityComparer<TKey> comparer)
    : IEqualityComparer<PartitionMessageKey<TKey>.Uncached>
{
    public bool Equals(PartitionMessageKey<TKey>.Uncached x, PartitionMessageKey<TKey>.Uncached y)
        => x.Equals(y, comparer);

    public int GetHashCode(PartitionMessageKey<TKey>.Uncached obj) => obj.GetHashCode(comparer);
}
