using System.IO.Hashing;
using System.Runtime.CompilerServices;

namespace Dekaf.Consumer;

internal readonly struct PartitionMessageKey<TKey> : IEquatable<PartitionMessageKey<TKey>>
{
    private readonly bool _hasValue;
    private readonly TKey? _value;

    internal bool HasValue => _hasValue;

    internal TKey? Value => _value;

    private PartitionMessageKey(TKey? value, bool hasValue)
    {
        _value = value;
        _hasValue = hasValue;
    }

    public static PartitionMessageKey<TKey> From(TKey? value)
    {
        return value is null
            ? new PartitionMessageKey<TKey>(default, hasValue: false)
            : new PartitionMessageKey<TKey>(value, hasValue: true);
    }

    public bool Equals(PartitionMessageKey<TKey> other)
    {
        if (_hasValue != other._hasValue)
            return false;

        return !_hasValue || EqualityComparer<TKey>.Default.Equals(_value!, other._value!);
    }

    public override bool Equals(object? obj) => obj is PartitionMessageKey<TKey> other && Equals(other);

    public override int GetHashCode()
    {
        return _hasValue
            ? EqualityComparer<TKey>.Default.GetHashCode(_value!)
            : 0;
    }

    internal bool Equals(PartitionMessageKey<TKey> other, IEqualityComparer<TKey> comparer)
        => _hasValue == other._hasValue && (!_hasValue || comparer.Equals(_value!, other._value!));

    internal int GetHashCode(IEqualityComparer<TKey> comparer)
        => _hasValue ? comparer.GetHashCode(_value!) : 0;
}

internal static class PartitionMessageKeyComparer<TKey>
{
    // Resolve the default once, before records enter the dispatcher. Scalar keys keep the
    // dictionary's default value-type comparer and its existing IEquatable<T> fast path.
    public static IEqualityComparer<PartitionMessageKey<TKey>>? Default { get; } =
        typeof(TKey) == typeof(byte[])
            || typeof(TKey) == typeof(ReadOnlyMemory<byte>)
            || typeof(TKey) == typeof(Memory<byte>)
            || typeof(TKey) == typeof(ArraySegment<byte>)
                ? new BinaryPartitionMessageKeyComparer<TKey>()
                : null;
}

// Full-content hashing/equality costs O(key length) per dispatch lookup. The large,
// distinct-key regression in #3048 remains a performance gate; selecting this comparer
// once per dispatcher does not amortize its per-record byte scans.
internal sealed class BinaryPartitionMessageKeyComparer<TKey> : IEqualityComparer<PartitionMessageKey<TKey>>
{
    public bool Equals(PartitionMessageKey<TKey> x, PartitionMessageKey<TKey> y)
    {
        return x.HasValue == y.HasValue
            && (!x.HasValue || GetBytes(x.Value!).SequenceEqual(GetBytes(y.Value!)));
    }

    public int GetHashCode(PartitionMessageKey<TKey> obj)
        => obj.HasValue ? unchecked((int)XxHash3.HashToUInt64(GetBytes(obj.Value!))) : 0;

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
