using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;
using Dekaf.Protocol;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures a collision threshold reached after unrelated large keys have filled
/// active lanes. Reports the complete dispatch cost, including one hash transition.
/// </summary>
[MemoryDiagnoser]
public class BinaryHashPromotionBenchmarks
{
    private ConsumeResult<ReadOnlyMemory<byte>, string>[] _records = null!;

    [Params(16, 1024)]
    public int ExistingLanes { get; set; }

    [Params(1024, 65536)]
    public int KeySize { get; set; }

    [Params(false, true)]
    public bool RepeatExistingKeys { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var distinctCount = ExistingLanes + 9;
        _records = new ConsumeResult<ReadOnlyMemory<byte>, string>[distinctCount + (RepeatExistingKeys ? ExistingLanes : 0)];
        for (var offset = 0; offset < _records.Length; offset++)
        {
            var identity = offset < distinctCount ? offset : offset - distinctCount;
            var key = new byte[KeySize];
            // Existing keys differ in the sampled suffix; the final nine differ
            // outside the sample and reach the expensive-collision threshold.
            BinaryPrimitives.WriteInt32LittleEndian(
                key.AsSpan(identity < ExistingLanes ? KeySize - sizeof(int) : KeySize - 32), identity + 1);
            _records[offset] = new ConsumeResult<ReadOnlyMemory<byte>, string>("topic", 0, offset,
                key, false, default, false, null, 0, TimestampType.CreateTime, null,
                Serializers.RawBytes, Serializers.String);
        }
    }

    [Benchmark]
    public Task Dispatch() => BinaryKeyDispatchBenchmarks.DispatchAsync(_records,
        maxConcurrentHandlers: 2, holdAllWorkers: true, expectedKeyCount: ExistingLanes + 9);
}
