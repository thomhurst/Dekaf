using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
public class PartitionMessageKeyBenchmarks
{
    private readonly byte[] _bytes = [1, 2, 3, 4, 5, 6, 7, 8];
    private readonly byte[] _equalBytes = [1, 2, 3, 4, 5, 6, 7, 8];
    private readonly string _text = "partition-key";
    private readonly string _equalText = new("partition-key".ToCharArray());
    private readonly int _number = 42;
    private readonly int? _nullableNumber = 42;
    private readonly IEqualityComparer<PartitionMessageKey<string>> _customComparer =
        new CustomPartitionMessageKeyComparer<string>(StringComparer.Ordinal);
    private readonly bool _wireNull = true;
    private readonly bool _nonNullWire = false;
    private readonly byte[]? _nullBytes = null;

    [Benchmark]
    public int ByteArray() => CompareAndHash(_bytes, _equalBytes, _nonNullWire);

    [Benchmark]
    public int ReadOnlyMemory() => CompareAndHash<ReadOnlyMemory<byte>>(_bytes, _equalBytes, _nonNullWire);

    [Benchmark]
    public int Memory() => CompareAndHash<Memory<byte>>(_bytes, _equalBytes, _nonNullWire);

    [Benchmark]
    public int ArraySegment() => CompareAndHash(new ArraySegment<byte>(_bytes), new ArraySegment<byte>(_equalBytes), _nonNullWire);

    [Benchmark]
    public int String() => CompareAndHash(_text, _equalText, _nonNullWire);

    [Benchmark]
    public int Int32() => CompareAndHash(_number, _number, _nonNullWire);

    [Benchmark]
    public int NullableInt32() => CompareAndHash(_nullableNumber, _nullableNumber, _nonNullWire);

    [Benchmark]
    public int CustomString()
    {
        var key = PartitionMessageKey<string>.From(_text, _nonNullWire);
        var other = PartitionMessageKey<string>.From(_equalText, _nonNullWire);
        return _customComparer.GetHashCode(key) ^ (_customComparer.Equals(key, other) ? 1 : 0);
    }

    [Benchmark]
    public int NullKinds()
    {
        var wireNull = PartitionMessageKey<byte[]>.From(_nullBytes, _wireNull);
        var deserializedNull = PartitionMessageKey<byte[]>.From(_nullBytes, _nonNullWire);
        var comparer = Comparers<byte[]>.Instance;
        return comparer.GetHashCode(deserializedNull) ^ (comparer.Equals(wireNull, deserializedNull) ? 1 : 0);
    }

    private static int CompareAndHash<TKey>(TKey first, TKey second, bool isKeyNull)
    {
        var key = PartitionMessageKey<TKey>.From(first, isKeyNull);
        var other = PartitionMessageKey<TKey>.From(second, isKeyNull);
        var comparer = Comparers<TKey>.Instance;
        return comparer.GetHashCode(key) ^ (comparer.Equals(key, other) ? 1 : 0);
    }

    private static class Comparers<TKey>
    {
        public static readonly IEqualityComparer<PartitionMessageKey<TKey>> Instance =
            PartitionMessageKeyComparer<TKey>.Default ?? EqualityComparer<PartitionMessageKey<TKey>>.Default;
    }
}
