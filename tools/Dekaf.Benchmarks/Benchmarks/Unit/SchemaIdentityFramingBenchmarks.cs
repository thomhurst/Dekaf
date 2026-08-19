using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Dekaf.SchemaRegistry;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser(displayGenColumns: false)]
[ShortRunJob]
[Config(typeof(FramingConfig))]
public class SchemaIdentityFramingBenchmarks
{
    private static readonly Guid SchemaGuid = Guid.Parse("89791762-2336-4186-9674-299b90a802e2");

    private readonly byte[] _idPrefix = [0, 0, 0, 0, 42, 7, 8];
    private readonly Headers _producerHeaders = new(1);
    private byte[] _guidFrame = null!;
    private Header[] _pooledHeaders = null!;

    [GlobalSetup]
    public void Setup()
    {
        _guidFrame = SchemaIdentityFraming.CreateSchemaGuidFrame(SchemaGuid);
        _pooledHeaders = [new Header(SchemaIdentityHeaderNames.Value, _guidFrame)];

        // Retain List<Header> storage so the measured header append does not grow it.
        SchemaIdentityFraming.AddSchemaGuidHeader(
            _producerHeaders,
            SerializationComponent.Value,
            _guidFrame);
        _producerHeaders.Clear();
    }

    [Benchmark(Baseline = true)]
    public SchemaIdentity ExistingInlinePrefixRead() =>
        new(BinaryPrimitives.ReadInt32BigEndian(_idPrefix.AsSpan(1, 4)));

    [Benchmark]
    public SchemaIdentity SharedPrefixRead() => SchemaIdentityFraming.ReadPrefix(_idPrefix, out _);

    [Benchmark]
    public SchemaIdentity PooledHeaderRead() => SchemaIdentityFraming.ReadHeader(
        _pooledHeaders.AsSpan(),
        SerializationComponent.Value,
        out _);

    [Benchmark]
    public SchemaIdentity DualPooledHeaderRead() => SchemaIdentityFraming.Read(
        _idPrefix,
        _pooledHeaders.AsSpan(),
        SerializationComponent.Value,
        SchemaIdDeserializerStrategy.Dual,
        out _,
        out _);

    [Benchmark]
    public int CachedHeaderAppend()
    {
        _producerHeaders.Clear();
        SchemaIdentityFraming.AddSchemaGuidHeader(
            _producerHeaders,
            SerializationComponent.Value,
            _guidFrame);
        return _producerHeaders.Count;
    }

    private sealed class FramingConfig : ManualConfig
    {
        public FramingConfig() => AddColumn(
            StatisticColumn.OperationsPerSecond,
            StatisticColumn.P50,
            P99Column.Instance,
            StatisticColumn.Max);
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
