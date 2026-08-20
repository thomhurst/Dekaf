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
    private byte[] _guidFrame = null!;
    private Header _identityHeader;
    private RecordHeaderRoutingLookup _routingLookup;

    [GlobalSetup]
    public void Setup()
    {
        _guidFrame = SchemaIdentityFraming.CreateSchemaGuidFrame(SchemaGuid);
        _identityHeader = new Header(SchemaIdentityHeaderNames.Value, _guidFrame);

        var multiHeaders = new Header[33];
        for (var index = 0; index < 32; index++)
            multiHeaders[index] = new Header($"noise-{index}", ReadOnlyMemory<byte>.Empty);
        multiHeaders[32] = new Header(
            SchemaIdentityHeaderNames.Value,
            [.. _guidFrame, 0]);

        var routingPlan = RecordHeaderRoutingPlan.Create<byte, byte>(
            null,
            IdentityRoutingDeserializer.Instance)!;
        _routingLookup = new RecordHeaderRoutingLookup(
            routingPlan,
            multiHeaders,
            multiHeaders.Length,
            firstIndex: 0,
            secondIndex: 33,
            routedHeaderTailOffset: RecordHeaderRoutingPlan.FullyIndexedWithoutTail);
    }

    [Benchmark(Baseline = true)]
    public SchemaIdentity ExistingValidatedInlinePrefixRead()
    {
        var span = _idPrefix.AsSpan();
        if (span.Length < SchemaIdentityFraming.SchemaIdFrameSize)
            throw new InvalidOperationException("Message too short to contain Schema Registry wire format");
        if (span[0] != SchemaIdentityFraming.SchemaIdMagicByte)
            throw new InvalidOperationException("Unknown Schema Registry magic byte");

        return new SchemaIdentity(BinaryPrimitives.ReadInt32BigEndian(span[1..]));
    }

    [Benchmark]
    public SchemaIdentity SharedPrefixRead() => SchemaIdentityFraming.ReadPrefix(_idPrefix, out _);

    [Benchmark]
    public SchemaIdentity SelectedHeaderRead() => SchemaIdentityFraming.ReadHeader(
        in _identityHeader,
        out _);

    [Benchmark]
    public SchemaIdentity DualSelectedHeaderRead() => SchemaIdentityFraming.Read(
        _idPrefix,
        _identityHeader,
        SchemaIdDeserializerStrategy.Dual,
        out _,
        out _);

    [Benchmark]
    public SchemaIdentity RoutedProtobufHeaderReadWith32NoiseHeaders()
    {
        if (!_routingLookup.TryGetLast(SchemaIdentityHeaderNames.Value, out var identityHeader))
            throw new InvalidOperationException("The routed schema identity header was not found.");

        var identity = SchemaIdentityFraming.ReadHeader(in identityHeader, out var messageIndexes);
        if (messageIndexes.Length != 1 || messageIndexes.Span[0] != 0)
            throw new InvalidDataException("The Protobuf message-index vector is invalid.");

        return identity;
    }

    [Benchmark]
    public Header CreateHeader() => SchemaIdentityFraming.CreateSchemaGuidHeader(
        SerializationComponent.Value,
        _guidFrame);

    private sealed class IdentityRoutingDeserializer : IDeserializer<byte>, IRecordHeaderRoutingProvider
    {
        internal static readonly IdentityRoutingDeserializer Instance = new();

        public byte Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) => 0;

        public void CollectHeaderNames(List<string> names)
        {
            names.Add(SchemaIdentityHeaderNames.Key);
            names.Add(SchemaIdentityHeaderNames.Value);
        }
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
