using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 10, iterationCount: 10)]
public class CachingStringDeserializerBenchmarks
{
    private const int UniqueKeyCount = 10_000;
    private const int UniquePassCount = 50;

    private readonly SerializationContext _context = new()
    {
        Topic = "cache-benchmark",
        Component = SerializationComponent.Key
    };
    private readonly CachingStringDeserializer _repeatedDeserializer =
        new(Serializers.String, maxCachedBytes: 128, maxCachedEntries: 16_384);
    private ReadOnlyMemory<byte>[] _uniqueKeys = null!;
    private ReadOnlyMemory<byte> _repeatedKey;

    [GlobalSetup]
    public void Setup()
    {
        _uniqueKeys = new ReadOnlyMemory<byte>[UniqueKeyCount];
        for (var i = 0; i < _uniqueKeys.Length; i++)
            _uniqueKeys[i] = Encoding.UTF8.GetBytes($"unique-key-{i}");

        _repeatedKey = Encoding.UTF8.GetBytes("repeated-key");
        _repeatedDeserializer.Deserialize(_repeatedKey, _context);
    }

    [Benchmark(OperationsPerInvoke = UniqueKeyCount * UniquePassCount)]
    public int UniqueKeys()
    {
        var characters = 0;
        for (var pass = 0; pass < UniquePassCount; pass++)
        {
            var deserializer =
                new CachingStringDeserializer(Serializers.String, maxCachedBytes: 128, maxCachedEntries: 16_384);
            for (var i = 0; i < _uniqueKeys.Length; i++)
                characters += deserializer.Deserialize(_uniqueKeys[i], _context).Length;
        }

        return characters;
    }

    [Benchmark]
    public string RepeatedKey() => _repeatedDeserializer.Deserialize(_repeatedKey, _context);
}
