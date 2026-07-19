using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
public class KafkaConnectionCapabilitiesBenchmarks
{
    private readonly ConcurrentDictionary<(ApiKey Key, short Min, short Max), short> _dictionary = new();
    private ApiVersionsResponse _response = null!;
    private KafkaConnectionCapabilities _capabilities = null!;
    private int _volatileProducerVersion = 13;

    [GlobalSetup]
    public void Setup()
    {
        _response = new ApiVersionsResponse
        {
            ErrorCode = ErrorCode.None,
            ApiKeys = Enum.GetValues<ApiKey>()
                .Select(static key => new ApiVersion(key, 0, 13))
                .ToArray()
        };
        _capabilities = KafkaConnectionCapabilities.Create(_response);
        _dictionary[(ApiKey.Produce, 3, 13)] = 13;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SteadyStateLookup")]
    public int VolatileProducerCache() => Volatile.Read(ref _volatileProducerVersion);

    [Benchmark]
    [BenchmarkCategory("SteadyStateLookup")]
    public short ConnectionSnapshotLookup() =>
        _capabilities.NegotiateVersion(ApiKey.Produce, 3, 13);

    [Benchmark]
    [BenchmarkCategory("SteadyStateLookup")]
    public short ConcurrentDictionaryLookup() =>
        _dictionary.TryGetValue((ApiKey.Produce, 3, 13), out var version) ? version : (short)-1;

    [Benchmark]
    [BenchmarkCategory("ConnectionSetup")]
    public object ConnectionSnapshotCreation() =>
        KafkaConnectionCapabilities.Create(_response);
}
