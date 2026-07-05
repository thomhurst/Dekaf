#if NETSTANDARD2_0
namespace System;

internal readonly struct Index
{
    private readonly int _value;

    public Index(int value, bool fromEnd = false)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value));

        _value = fromEnd ? ~value : value;
    }

    private Index(int value)
    {
        _value = value;
    }

    public int Value => _value < 0 ? ~_value : _value;
    public bool IsFromEnd => _value < 0;
    public static Index Start => new(0);
    public static Index End => new(~0);
    public static implicit operator Index(int value) => new(value);
    public int GetOffset(int length) => IsFromEnd ? length - Value : Value;
}

internal readonly struct Range
{
    public Range(Index start, Index end)
    {
        Start = start;
        End = end;
    }

    public Index Start { get; }
    public Index End { get; }
    public static Range All => new(Index.Start, Index.End);
    public static Range StartAt(Index start) => new(start, Index.End);
    public static Range EndAt(Index end) => new(Index.Start, end);

    public (int Offset, int Length) GetOffsetAndLength(int length)
    {
        var start = Start.GetOffset(length);
        var end = End.GetOffset(length);
        if ((uint)end > (uint)length || (uint)start > (uint)end)
            throw new ArgumentOutOfRangeException(nameof(length));
        return (start, end - start);
    }
}

internal struct HashCode
{
    private int _hash;

    public void Add<T>(T value)
    {
        _hash = Combine(_hash, value);
    }

    public int ToHashCode() => _hash;

    public static int Combine<T1, T2>(T1 value1, T2 value2)
        => Combine(value1?.GetHashCode() ?? 0, value2);

    private static int Combine<T>(int hash, T value)
    {
        unchecked
        {
            return (hash * 397) ^ (value?.GetHashCode() ?? 0);
        }
    }
}
#endif
