using System.Text;
using BenchmarkDotNet.Attributes;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>One configured and one unrelated header lookup per record, including unrelated-name decoding.</summary>
[MemoryDiagnoser]
public class ShareBatchOversizedHeaderBenchmarks
{
    private ShareBatchHeaderKeys _keys = null!;
    private ReadOnlyMemory<byte> _configured;
    private ReadOnlyMemory<byte> _unrelated;

    [Params(8, 512)]
    public int ConfiguredNameBytes { get; set; }

    [Params(257, 512, 4096)]
    public int UnrelatedNameBytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var name = new string('r', ConfiguredNameBytes);
        var unrelatedName = new string('u', UnrelatedNameBytes);
        var router = new HeaderRoutingDeserializer<int>(name, Serializers.Int32,
            new HeaderDeserializerRoute<int>("selected"u8.ToArray(), Serializers.Int32));
        _keys = new ShareBatchHeaderKeys(RecordHeaderRoutingPlan.Create(Serializers.Int32, router)!);
        _configured = Encoding.UTF8.GetBytes(name);
        _unrelated = Encoding.UTF8.GetBytes(unrelatedName);
        // Configured names stay consumer-owned; oversized unrelated names cannot enter the shared cache.
        if (!ReferenceEquals(_keys.Get(_configured), name) || _keys.Get(_unrelated) != unrelatedName)
            throw new InvalidOperationException("The fixture must preserve both header names.");
    }

    [Benchmark]
    public int LookupRecordHeaders() => _keys.Get(_configured).Length + _keys.Get(_unrelated).Length;
}
