using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;
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
    private IDeserializer<Event> _topicHeaderDeserializer = null!;
    private IDeserializer<Event> _schemaIdHeaderDeserializer = null!;
    private TopicRoutingSerializer<Event> _topicSerializer = null!;
    private TypeRoutingSerializer<Event> _typeSerializer = null!;
    private SerializationContext _context;
    private Header[] _headers = null!;
    private RecordHeaderRoutingLookup _headerLookup;

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
        _topicHeaderDeserializer = new TopicRoutingDeserializer<Event>()
            .Register("events", _headerDeserializer)
            .Freeze();
        _schemaIdHeaderDeserializer = new SchemaIdRoutingDeserializer<Event>()
            .Register(42, _headerDeserializer)
            .Freeze();
        var headerPlan = RecordHeaderRoutingPlan.Create(_deserializer, _topicHeaderDeserializer)!;
        _headerLookup = new RecordHeaderRoutingLookup(
            headerPlan,
            _headers,
            _headers.Length,
            firstIndex: 1,
            secondIndex: 0,
            routedHeaderTailOffset: RecordHeaderRoutingPlan.FullyIndexedWithoutTail);
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
    public Event DeserializeByTopicThenHeader() =>
        RecordHeaderDeserializer.Deserialize(
            _topicHeaderDeserializer,
            Data,
            _context,
            in _headerLookup);

    [Benchmark]
    public Event DeserializeBySchemaIdThenHeader() =>
        RecordHeaderDeserializer.Deserialize(
            _schemaIdHeaderDeserializer,
            FramedData,
            _context,
            in _headerLookup);

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

    internal sealed class EventDeserializer : IDeserializer<Event>
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

/// <summary>Verifies parsed header routing remains O(1) and allocation-free as headers grow.</summary>
[MemoryDiagnoser]
[ShortRunJob(RuntimeMoniker.Net10_0)]
public class HeaderRoutingLookupBenchmarks
{
    private static readonly ReadOnlyMemory<byte> Data = new byte[] { 1, 2, 3 };
    private readonly RoutingSerdeBenchmarks.EventDeserializer _deserializer = new();
    private HeaderRoutingDeserializer<RoutingSerdeBenchmarks.Event> _router = null!;
    private RecordHeaderRoutingLookup _matched;
    private RecordHeaderRoutingLookup _fallback;
    private RecordHeaderRoutingLookup _null;
    private Header[] _matchedHeaders = null!;
    private SerializationContext _context;

    [Params(1, 8, 64, 1_024)]
    public int HeaderCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _context = new SerializationContext
        {
            Topic = "events",
            Component = SerializationComponent.Value
        };
        _router = new HeaderRoutingDeserializer<RoutingSerdeBenchmarks.Event>(
            "event-type",
            _deserializer,
            new HeaderDeserializerRoute<RoutingSerdeBenchmarks.Event>(
                new byte[] { 1 },
                _deserializer));
        var plan = RecordHeaderRoutingPlan.Create(_deserializer, _router)!;
        _matchedHeaders = CreateHeaders(new Header("event-type", new byte[] { 1 }));
        _matched = CreateLookup(plan, _matchedHeaders);
        _fallback = CreateLookup(plan, new Header("event-type", new byte[] { 2 }));
        _null = CreateLookup(plan, new Header("event-type", (byte[]?)null));
    }

    [Benchmark(Baseline = true)]
    public RoutingSerdeBenchmarks.Event LinearMatched() =>
        _router.DeserializeWithHeaders(Data, _context, _matchedHeaders);

    [Benchmark]
    public RoutingSerdeBenchmarks.Event Matched() => Deserialize(in _matched);

    [Benchmark]
    public RoutingSerdeBenchmarks.Event Fallback() => Deserialize(in _fallback);

    [Benchmark]
    public RoutingSerdeBenchmarks.Event Null() => Deserialize(in _null);

    private RoutingSerdeBenchmarks.Event Deserialize(in RecordHeaderRoutingLookup lookup) =>
        RecordHeaderDeserializer.Deserialize(
            _router,
            Data,
            _context,
            in lookup);

    private RecordHeaderRoutingLookup CreateLookup(
        RecordHeaderRoutingPlan plan,
        Header routedHeader) =>
        CreateLookup(plan, CreateHeaders(routedHeader));

    private static RecordHeaderRoutingLookup CreateLookup(
        RecordHeaderRoutingPlan plan,
        Header[] headers)
    {
        var record = new Record
        {
            Headers = headers,
            HeaderCount = headers.Length
        }.IndexHeaders(plan);
        return record.CreateHeaderRoutingLookup(plan);
    }

    private Header[] CreateHeaders(Header routedHeader)
    {
        var headers = new Header[HeaderCount];
        for (var index = 0; index < headers.Length; index++)
            headers[index] = new Header($"noise-{index}", Array.Empty<byte>());
        headers[0] = routedHeader;
        return headers;
    }
}

/// <summary>Measures full record parsing with nested header-routing index construction.</summary>
[MemoryDiagnoser]
[ShortRunJob(RuntimeMoniker.Net10_0)]
public class HeaderRoutingParseBenchmarks
{
    private readonly RoutingSerdeBenchmarks.EventDeserializer _deserializer = new();
    private byte[] _encodedRecord = null!;
    private RecordHeaderRoutingPlan _nestedPlan = null!;

    [GlobalSetup]
    public void Setup()
    {
        IDeserializer<RoutingSerdeBenchmarks.Event> nested = _deserializer;
        for (var route = 4; route >= 1; route--)
        {
            nested = new HeaderRoutingDeserializer<RoutingSerdeBenchmarks.Event>(
                $"route-{route}",
                nested);
        }

        _nestedPlan = RecordHeaderRoutingPlan.Create(_deserializer, nested)!;
        var record = new Record
        {
            Key = new byte[] { 1 },
            Value = new byte[] { 2 },
            Headers =
            [
                new Header("route-1", new byte[] { 1 }),
                new Header("route-2", new byte[] { 2 }),
                new Header("route-3", new byte[] { 3 }),
                new Header("route-4", new byte[] { 4 })
            ],
            HeaderCount = 4
        };
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        record.Write(ref writer);
        _encodedRecord = buffer.WrittenSpan.ToArray();

        _ = Parse(headerRoutingPlan: null);
        _ = Parse(_nestedPlan);
    }

    [Benchmark(Baseline = true)]
    public int ParseWithoutRouting() => Parse(headerRoutingPlan: null);

    [Benchmark]
    public int ParseNestedRouting() => Parse(_nestedPlan);

    private int Parse(RecordHeaderRoutingPlan? headerRoutingPlan)
    {
        var reader = new KafkaProtocolReader(_encodedRecord);
        var record = Record.Read(ref reader, headerRoutingPlan);
        var count = record.HeaderCount;
        ArrayPool<Header>.Shared.Return(record.Headers!, clearArray: true);
        return count;
    }
}
