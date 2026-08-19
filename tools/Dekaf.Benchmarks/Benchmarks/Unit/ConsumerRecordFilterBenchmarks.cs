using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Dekaf.Consumer;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures the warmed pre-deserialization filter stage for pass-all, reject-all, and mixed
/// workloads. All record storage and header values are initialized before measurement.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob(RuntimeMoniker.Net10_0)]
[Config(typeof(FilterConfig))]
public class ConsumerRecordFilterBenchmarks
{
    private readonly Header[] _acceptedHeaders =
        [new Header("route", new byte[] { 1 })];
    private readonly Header[] _rejectedHeaders =
        [new Header("route", new byte[] { 0 })];
    private readonly ReadOnlyMemory<byte> _key = new byte[] { 1, 2, 3, 4 };
    private readonly ReadOnlyMemory<byte> _value = new byte[1_000];
    private IConsumerRecordFilter _filter = null!;
    private int _mixedIndex;

    private sealed class FilterConfig : ManualConfig
    {
        public FilterConfig() => AddColumn(
            StatisticColumn.OperationsPerSecond,
            StatisticColumn.P50,
            P99Column.Instance,
            StatisticColumn.Max);
    }

    [GlobalSetup]
    public void Setup() => _filter = new RouteFilter();

    [Benchmark]
    public bool PassAll()
    {
        var context = CreateContext(_acceptedHeaders);
        return _filter.ShouldDeserialize(in context);
    }

    [Benchmark]
    public bool RejectAll()
    {
        var context = CreateContext(_rejectedHeaders);
        return _filter.ShouldDeserialize(in context);
    }

    [Benchmark]
    public bool Mixed()
    {
        var headers = (_mixedIndex++ & 1) == 0
            ? _acceptedHeaders
            : _rejectedHeaders;
        var context = CreateContext(headers);
        return _filter.ShouldDeserialize(in context);
    }

    private ConsumerRecordFilterContext CreateContext(Header[] headers) => new(
        "events",
        0,
        42,
        1_700_000_000_000,
        TimestampType.CreateTime,
        7,
        _key,
        isKeyNull: false,
        _value,
        isValueNull: false,
        headers);

    private sealed class RouteFilter : IConsumerRecordFilter
    {
        public bool ShouldDeserialize(scoped in ConsumerRecordFilterContext context)
        {
            var headers = context.Headers;
            for (var i = 0; i < headers.Length; i++)
            {
                ref readonly var header = ref headers[i];
                if (header.Key == "route")
                    return !header.IsValueNull && header.Value.Span is [1];
            }

            return false;
        }
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
