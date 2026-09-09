using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

[MemoryDiagnoser]
public class BatchBoundBench
{
    private Record[][] _records = null!;
    private RecordBatch[] _parseBatches = null!;
    private PendingFetchData _constructorPending = null!;

    [Params(1, 128)]
    public int Batches { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _records = new Record[Batches][];
        var value = new byte[16];
        for (int batch = 0; batch < Batches; batch++)
        {
            _records[batch] = new Record[8];
            for (int record = 0; record < 8; record++)
                _records[batch][record] = new Record { OffsetDelta = record, Value = value, IsKeyNull = true };
        }
        _parseBatches = new RecordBatch[Batches];
        _constructorPending = Create(new RecordBatch[Batches]);
        if (ParseFetch() != Batches
            || ((ConsumeBatch<Ignore, ReadOnlyMemory<byte>>)ConstructPollBatch()).Topic != "bounds")
            throw new InvalidOperationException("Bound fixture validation failed.");
    }

    private PendingFetchData Create(RecordBatch[] batches)
    {
        for (int index = 0; index < Batches; index++)
        {
            var batch = RecordBatch.RentFromPool();
            batch.BaseOffset = index * 8;
            batch.LastOffsetDelta = 7;
            batch.PartitionLeaderEpoch = 1;
            batch.Records = _records[index];
            batches[index] = batch;
        }
        var pending = PendingFetchData.Create("bounds", 0, batches);
        pending.EagerParseAll();
        return pending;
    }

    [Benchmark]
    public object ConstructPollBatch()
        => new ConsumeBatch<Ignore, ReadOnlyMemory<byte>>(
            _constructorPending, Serializers.Ignore, Serializers.RawBytes, maxRecords: 1024);

    [Benchmark]
    public int ParseFetch()
    {
        using var pending = Create(_parseBatches);
        return pending.GetBatches().Count;
    }

    [GlobalCleanup]
    public void Cleanup() => _constructorPending.Dispose();
}
