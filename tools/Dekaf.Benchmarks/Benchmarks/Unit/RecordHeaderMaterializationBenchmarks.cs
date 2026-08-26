using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[ShortRunJob(RuntimeMoniker.Net10_0)]
public class RecordHeaderMaterializationBenchmarks
{
    private static readonly ReadOnlyMemory<byte> Data = "value"u8.ToArray();
    private PendingFetchData _headerOwner = null!;
    private IDeserializer<string> _keyDeserializer = null!;
    private IDeserializer<string> _valueDeserializer = null!;
    private Headers _materializedHeaders = null!;
    private RecordHeaderRoutingLookup _headerLookup;

    [GlobalSetup]
    public void Setup()
    {
        _headerOwner = PendingFetchData.Create(
            "benchmark-topic",
            partitionIndex: 0,
            Array.Empty<RecordBatch>());
        _keyDeserializer = RecordHeaderDeserializer.WrapIfNeeded(new HeaderDeserializer());
        _valueDeserializer = RecordHeaderDeserializer.WrapIfNeeded(new HeaderDeserializer());
        _materializedHeaders = new Headers(32);
        var plan = RecordHeaderRoutingPlan.Create(_keyDeserializer, _valueDeserializer)!;
        var headers = new Header[32];
        for (var index = 0; index < headers.Length; index++)
            headers[index] = new Header($"header-{index}", Data);
        _headerLookup = new RecordHeaderRoutingLookup(
            plan,
            headers,
            headers.Length,
            firstIndex: 0,
            secondIndex: 0,
            routedHeaderTailOffset: RecordHeaderRoutingPlan.FullyIndexedWithoutTail);

        _ = TwoDecoratorsWith32Headers();
    }

    [GlobalCleanup]
    public void Cleanup() => _headerOwner.Dispose();

    [Benchmark]
    public ConsumeResult<string, string> TwoDecoratorsWith32Headers() =>
        ConsumeResult<string, string>.CreateWithHeaderRouting(
            topic: "benchmark-topic",
            partition: 0,
            offset: 1,
            keyData: Data,
            isKeyNull: false,
            valueData: Data,
            isValueNull: false,
            pooledHeaders: null,
            pooledHeaderCount: 0,
            in _headerLookup,
            _headerOwner,
            timestampMs: 0,
            timestampType: TimestampType.CreateTime,
            leaderEpoch: null,
            _materializedHeaders,
            _keyDeserializer,
            _valueDeserializer);

    private sealed class HeaderDeserializer : IDeserializer<string>, IRecordHeaderDeserializer
    {
        public bool ConsumesRecordHeaders => true;

        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
            context.Headers is null ? string.Empty : "value";
    }
}
