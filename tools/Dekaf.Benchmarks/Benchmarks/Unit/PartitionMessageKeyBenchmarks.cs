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

    [Benchmark]
    public int ByteArray() => CompareAndHash(_bytes, _equalBytes);

    [Benchmark]
    public int ReadOnlyMemory() => CompareAndHash<ReadOnlyMemory<byte>>(_bytes, _equalBytes);

    [Benchmark]
    public int Memory() => CompareAndHash<Memory<byte>>(_bytes, _equalBytes);

    [Benchmark]
    public int ArraySegment() => CompareAndHash(new ArraySegment<byte>(_bytes), new ArraySegment<byte>(_equalBytes));

    [Benchmark]
    public int String() => CompareAndHash(_text, _equalText);

    [Benchmark]
    public int Int32() => CompareAndHash(_number, _number);

    private static int CompareAndHash<TKey>(TKey first, TKey second)
    {
        var key = PartitionMessageKey<TKey>.From(first);
        var other = PartitionMessageKey<TKey>.From(second);
        var comparer = Comparers<TKey>.Instance;
        return comparer.GetHashCode(key) ^ (comparer.Equals(key, other) ? 1 : 0);
    }

    private static class Comparers<TKey>
    {
        public static readonly IEqualityComparer<PartitionMessageKey<TKey>> Instance =
            PartitionMessageKeyComparer<TKey>.Default ?? EqualityComparer<PartitionMessageKey<TKey>>.Default;
    }
}
