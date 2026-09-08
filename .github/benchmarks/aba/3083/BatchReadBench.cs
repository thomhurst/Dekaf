using BenchmarkDotNet.Attributes;
using Dekaf;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

[MemoryDiagnoser]
public class BatchReadBench
{
    private Record[] _records = null!;
    private Record[] _keyedRecords = null!;
    private RecordBatch[] _batch = null!;
    private readonly IDeserializer<ReadOnlyMemory<byte>> _customRaw = new CustomRawDeserializer();
    [GlobalSetup]
    public void Setup()
    {
        _records = new Record[1000];
        _keyedRecords = new Record[1000];
        var value = new byte[128];
        var key = new byte[16];
        for (var index = 0; index < _records.Length; index++)
        {
            _records[index] = new Record { OffsetDelta = index, Value = value, IsKeyNull = true };
            _keyedRecords[index] = new Record { OffsetDelta = index, Value = value, Key = key, IsKeyNull = false };
        }
        _batch = new RecordBatch[1];
        if (Typed() != 128000 || TypedEpoch() != 1000 || RawControl() != 128000
            || TypedCustom() != 128000 || TypedStrings() != 128000 || TypedWithIgnoredKey() != 128000)
            throw new InvalidOperationException("Batch traversal did not read all records.");
    }
    private PendingFetchData Create(bool withKey = false)
    {
        var batch = RecordBatch.RentFromPool();
        batch.BaseOffset = 0;
        batch.LastOffsetDelta = 999;
        batch.PartitionLeaderEpoch = 1;
        batch.Records = withKey ? _keyedRecords : _records;
        _batch[0] = batch;
        var pending = PendingFetchData.Create("batch", 0, _batch);
        pending.EagerParseAll();
        return pending;
    }
    [Benchmark]
    public int Typed()
    {
        using var pending = Create();
        var batch = new ConsumeBatch<Ignore, ReadOnlyMemory<byte>>(pending, Serializers.Ignore, Serializers.RawBytes);
        var total = 0;
        foreach (var result in batch)
            total += result.Value.Length;
        return total;
    }
    [Benchmark]
    public int TypedEpoch()
    {
        using var pending = Create();
        var batch = new ConsumeBatch<Ignore, ReadOnlyMemory<byte>>(pending, Serializers.Ignore, Serializers.RawBytes);
        var total = 0;
        foreach (var result in batch)
            total += result.LeaderEpoch.GetValueOrDefault();
        return total;
    }
    [Benchmark]
    public int RawControl()
    {
        using var pending = Create();
        var batch = new ConsumeRawBatch(pending);
        var total = 0;
        foreach (var result in batch)
            total += result.Value.Length;
        return total;
    }

    [Benchmark]
    public int TypedCustom()
    {
        using var pending = Create();
        var batch = new ConsumeBatch<Ignore, ReadOnlyMemory<byte>>(pending, Serializers.Ignore, _customRaw);
        var total = 0;
        foreach (var result in batch)
            total += result.Value.Length;
        return total;
    }

    [Benchmark]
    public int TypedStrings()
    {
        using var pending = Create();
        var batch = new ConsumeBatch<Ignore, string>(pending, Serializers.Ignore, Serializers.String);
        var total = 0;
        foreach (var result in batch)
            total += result.Value.Length;
        return total;
    }

    [Benchmark]
    public int TypedWithIgnoredKey()
    {
        using var pending = Create(withKey: true);
        var batch = new ConsumeBatch<Ignore, ReadOnlyMemory<byte>>(pending, Serializers.Ignore, Serializers.RawBytes);
        var total = 0;
        foreach (var result in batch)
            total += result.Value.Length;
        return total;
    }

    private sealed class CustomRawDeserializer : IDeserializer<ReadOnlyMemory<byte>>
    {
        public ReadOnlyMemory<byte> Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) => data;
    }
}
