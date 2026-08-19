using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures warmed parsed-record delivery through <see cref="ConsumeBatch{TKey,TValue}"/>,
/// including filter-mode selection, rejected-record position tracking, and result construction.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob(RuntimeMoniker.Net10_0)]
[Config(typeof(FilterConfig))]
public class ConsumerRecordFilterBenchmarks
{
    private const int MessageCount = 1_000;
    private readonly SingleRecordBatchList _batchList = new();
    private readonly IConsumerRecordFilter _passAll = new PassAllFilter();
    private readonly IConsumerRecordFilter _rejectAll = new RejectAllFilter();
    private readonly IConsumerRecordFilter _mixed = new MixedFilter();
    private Record[] _records = null!;

    private sealed class FilterConfig : ManualConfig
    {
        public FilterConfig() => AddColumn(
            StatisticColumn.OperationsPerSecond,
            StatisticColumn.P50,
            P99Column.Instance,
            StatisticColumn.Max);
    }

    [GlobalSetup]
    public void Setup()
    {
        var key = new byte[] { 1, 2, 3, 4 };
        var value = new byte[1_000];
        _records = new Record[MessageCount];
        for (var index = 0; index < _records.Length; index++)
        {
            _records[index] = new Record
            {
                OffsetDelta = index,
                TimestampDelta = index,
                Key = key,
                Value = value,
                Headers = null,
                HeaderCount = 0
            };
        }

        _ = Run(_passAll);
        _ = Run(_rejectAll);
        _ = Run(_mixed);
    }

    [Benchmark(OperationsPerInvoke = MessageCount)]
    public int PassAll() => Run(_passAll);

    [Benchmark(OperationsPerInvoke = MessageCount)]
    public int RejectAll() => Run(_rejectAll);

    [Benchmark(OperationsPerInvoke = MessageCount)]
    public int Mixed() => Run(_mixed);

    private int Run(IConsumerRecordFilter filter)
    {
        using var pending = CreatePendingFetch();
        var batch = new ConsumeBatch<Ignore, ReadOnlyMemory<byte>>(
            pending,
            Serializers.Ignore,
            Serializers.RawBytes,
            recordFilter: filter);
        var delivered = 0;
        foreach (var _ in batch)
            delivered++;
        return delivered;
    }

    private PendingFetchData CreatePendingFetch()
    {
        var batch = RecordBatch.RentFromPool();
        batch.BaseOffset = 0;
        batch.BaseTimestamp = 1_700_000_000_000;
        batch.LastOffsetDelta = MessageCount - 1;
        batch.Records = _records;
        _batchList.Batch = batch;

        var pending = PendingFetchData.Create("events", 0, _batchList);
        pending.EagerParseAll();
        return pending;
    }

    private sealed class PassAllFilter : IConsumerRecordFilter
    {
        public bool ShouldDeserialize(scoped in ConsumerRecordFilterContext context) => true;
    }

    private sealed class RejectAllFilter : IConsumerRecordFilter
    {
        public bool ShouldDeserialize(scoped in ConsumerRecordFilterContext context) => false;
    }

    private sealed class MixedFilter : IConsumerRecordFilter
    {
        public bool ShouldDeserialize(scoped in ConsumerRecordFilterContext context) =>
            (context.Offset & 1) == 0;
    }

    private sealed class SingleRecordBatchList : IReadOnlyList<RecordBatch>
    {
        public RecordBatch Batch { get; set; } = null!;
        public int Count => 1;
        public RecordBatch this[int index] => index == 0
            ? Batch
            : throw new ArgumentOutOfRangeException(nameof(index));

        public IEnumerator<RecordBatch> GetEnumerator()
        {
            yield return Batch;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }

    private sealed class P99Column : IColumn
    {
        internal static readonly P99Column Instance = new();

        public string Id => nameof(P99Column);
        public string ColumnName => "P99 (ns)";
        public string Legend => "99th percentile of iteration time per operation in nanoseconds";
        public UnitType UnitType => UnitType.Time;
        public bool IsNumeric => true;
        public ColumnCategory Category => ColumnCategory.Statistics;
        public int PriorityInCategory => 0;
        public bool AlwaysShow => true;

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase) =>
            GetValue(summary, benchmarkCase, summary.Style);

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style) =>
            summary[benchmarkCase]?.ResultStatistics?.Percentiles.Percentile(99)
                .ToString("N3", style.CultureInfo) ?? "NA";

        public bool IsAvailable(Summary summary) => true;
        public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
    }
}
