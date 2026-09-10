using System.IO.Hashing;
using System.Runtime.CompilerServices;

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
    private int _unequalLargeKeys;
    private bool _fullHashing;
    internal bool NeedsFullHashing { get; private set; }

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
        return ObserveCollision(left.Length, right.Length);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool ObserveCollision(int leftLength, int rightLength)
    {
        // Count expensive collisions within one dictionary probe, not across the
        // lifetime of a healthy dispatcher. Only its single coordinator uses this comparer.
        if (!_fullHashing && leftLength > 64 && leftLength == rightLength && ++_unequalLargeKeys >= 8)
            NeedsFullHashing = true;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetHashCode(PartitionMessageKey<TKey> obj)
    {
        _unequalLargeKeys = 0;
        if (obj.HasBinaryHashCode)
            return obj.BinaryHashCode;
        return ComputeHashCode(obj);
    }

    internal int ComputeHashCode(PartitionMessageKey<TKey> obj)
        => obj.HasValue ? HashBytes(GetBytes(obj.Value!)) : 0;

    internal void EnableFullHashing()
    {
        _fullHashing = true;
        NeedsFullHashing = false;
    }

    private int HashBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length <= 64 || _fullHashing)
            return unchecked((int)XxHash3.HashToUInt64(bytes));

        // Bound dispatch hashing for large binary keys. Equality still compares
        // every byte. Repeated expensive collisions switch this dispatcher's table
        // to full hashes once, without charging every large-key workload for a full scan.
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
