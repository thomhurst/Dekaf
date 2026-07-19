using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 10, iterationCount: 10)]
public class CachingStringDeserializerBenchmarks
{
    private const int UniqueKeyCount = 65_536;
    private const int UniquePassCount = 10;
    private const int BoundedKeyCount = 1_000;

    private readonly SerializationContext _context = new()
    {
        Topic = "cache-benchmark",
        Component = SerializationComponent.Key
    };
    private readonly CachingStringDeserializer _repeatedDeserializer =
        new(Serializers.String, maxCachedBytes: 128, maxCachedEntries: 16_384);
    private readonly CachingStringDeserializer _boundedDeserializer =
        new(Serializers.String, maxCachedBytes: 128, maxCachedEntries: 16_384);
    private ReadOnlyMemory<byte>[] _uniqueKeys = null!;
    private ReadOnlyMemory<byte>[] _boundedKeys = null!;
    private ReadOnlyMemory<byte> _repeatedKey;

    [GlobalSetup]
    public void Setup()
    {
        _uniqueKeys = new ReadOnlyMemory<byte>[UniqueKeyCount];
        for (var i = 0; i < _uniqueKeys.Length; i++)
            _uniqueKeys[i] = Encoding.UTF8.GetBytes($"unique-key-{i}");

        _boundedKeys = new ReadOnlyMemory<byte>[BoundedKeyCount];
        for (var i = 0; i < _boundedKeys.Length; i++)
        {
            _boundedKeys[i] = Encoding.UTF8.GetBytes($"bounded-key-{i}");
            _boundedDeserializer.Deserialize(_boundedKeys[i], _context);
        }

        // Let the adaptive implementation complete its first hit-rate probe.
        for (var i = 0; i < BoundedKeyCount; i++)
            _boundedDeserializer.Deserialize(_boundedKeys[i], _context);

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

    [Benchmark(OperationsPerInvoke = BoundedKeyCount)]
    public int BoundedReusableKeys()
    {
        var characters = 0;
        for (var i = 0; i < _boundedKeys.Length; i++)
            characters += _boundedDeserializer.Deserialize(_boundedKeys[i], _context).Length;

        return characters;
    }

    [Benchmark]
    public string RepeatedKey() => _repeatedDeserializer.Deserialize(_repeatedKey, _context);
}
