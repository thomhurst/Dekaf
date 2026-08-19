using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Dekaf.Serialization;
using Dekaf.Serialization.Routing;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Measures frozen routing overhead and verifies zero steady-state allocation.</summary>
[MemoryDiagnoser]
[ShortRunJob(RuntimeMoniker.Net10_0)]
[Config(typeof(RoutingConfig))]
public class RoutingSerdeBenchmarks
{
    private static readonly Event Payload = new();
    private static readonly ReadOnlyMemory<byte> Data = new byte[] { 1, 2, 3 };
    private static readonly ReadOnlyMemory<byte> FramedData = new byte[] { 0, 0, 0, 0, 42, 1, 2, 3 };
    private readonly EventDeserializer _deserializer = new();
    private readonly EventSerializer _serializer = new();
    private ArrayBufferWriter<byte> _buffer = new(16);
    private TopicRoutingDeserializer<Event> _topicDeserializer = null!;
    private SchemaIdRoutingDeserializer<Event> _schemaIdDeserializer = null!;
    private HeaderRoutingDeserializer<Event> _headerDeserializer = null!;
    private TopicRoutingSerializer<Event> _topicSerializer = null!;
    private TypeRoutingSerializer<Event> _typeSerializer = null!;
    private SerializationContext _context;
    private Header[] _headers = null!;

    private sealed class RoutingConfig : ManualConfig
    {
        public RoutingConfig() => AddColumn(
            StatisticColumn.OperationsPerSecond,
            StatisticColumn.P50,
            P99Column.Instance,
            StatisticColumn.Max);
    }

    [GlobalSetup]
    public void Setup()
    {
        _context = new SerializationContext
        {
            Topic = "events",
            Component = SerializationComponent.Value
        };
        _topicDeserializer = new TopicRoutingDeserializer<Event>()
            .Register("events", _deserializer)
            .Freeze();
        _schemaIdDeserializer = new SchemaIdRoutingDeserializer<Event>()
            .Register(42, _deserializer)
            .Freeze();
        _headerDeserializer = new HeaderRoutingDeserializer<Event>(
            "event-type",
            _deserializer,
            new HeaderDeserializerRoute<Event>(new byte[] { 1 }, _deserializer));
        _headers = [new Header("event-type", new byte[] { 1 })];
        _topicSerializer = new TopicRoutingSerializer<Event>()
            .Register("events", _serializer)
            .Freeze();
        _typeSerializer = new TypeRoutingSerializer<Event>()
            .Register(_serializer)
            .Freeze();

        SerializeDirect();
    }

    [Benchmark(Baseline = true)]
    public Event DeserializeDirect() => _deserializer.Deserialize(Data, _context);

    [Benchmark]
    public Event DeserializeByTopic() => _topicDeserializer.Deserialize(Data, _context);

    [Benchmark]
    public Event DeserializeBySchemaId() => _schemaIdDeserializer.Deserialize(FramedData, _context);

    [Benchmark]
    public Event DeserializeByHeader() =>
        _headerDeserializer.DeserializeWithHeaders(Data, _context, _headers);

    [Benchmark]
    public int SerializeDirect()
    {
        _buffer.Clear();
        _serializer.Serialize(Payload, ref _buffer, _context);
        return _buffer.WrittenCount;
    }

    [Benchmark]
    public int SerializeByTopic()
    {
        _buffer.Clear();
        _topicSerializer.Serialize(Payload, ref _buffer, _context);
        return _buffer.WrittenCount;
    }

    [Benchmark]
    public int SerializeByRuntimeType()
    {
        _buffer.Clear();
        _typeSerializer.Serialize(Payload, ref _buffer, _context);
        return _buffer.WrittenCount;
    }

    public sealed class Event;

    private sealed class EventDeserializer : IDeserializer<Event>
    {
        public Event Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) => Payload;
    }

    private sealed class EventSerializer : ISerializer<Event>
    {
        public void Serialize<TWriter>(Event value, ref TWriter destination, SerializationContext context)
            where TWriter : IBufferWriter<byte>, allows ref struct
        {
            destination.GetSpan(1)[0] = 1;
            destination.Advance(1);
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
