using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using Dekaf.Consumer;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Dekaf.Serialization.Routing;

namespace Dekaf.Tests.Unit.Serialization;

public sealed class HeaderRoutingDeserializerTests
{
    [Test]
    public async Task Deserialize_KnownHeaderValueUsesMatchingChild()
    {
        var router = CreateRouter();
        var headers = new[] { new Header("event-type", "created"u8.ToArray()) };

        var result = router.DeserializeWithHeaders("payload"u8.ToArray(), CreateContext(), headers);

        await Assert.That(result).IsEqualTo("created:payload");
    }

    [Test]
    public async Task Deserialize_SerializationContextHeadersUsesMatchingChild()
    {
        var router = CreateRouter();
        var context = CreateContext();
        context.Headers = Headers.Create("event-type", "created");

        var result = router.Deserialize("payload"u8.ToArray(), context);

        await Assert.That(result).IsEqualTo("created:payload");
    }

    [Test]
    [Arguments("created")]
    [Arguments("unknown")]
    public async Task Deserialize_ClearsCallerOwnedHeadersBeforeLeaf(string routeValue)
    {
        var leaf = new HeaderPresenceDeserializer();
        var router = new HeaderRoutingDeserializer<string>(
            "event-type",
            leaf,
            new HeaderDeserializerRoute<string>("created"u8.ToArray(), leaf));
        var context = CreateContext();
        context.Headers = Headers.Create("event-type", routeValue);

        var result = router.Deserialize("payload"u8.ToArray(), context);

        await Assert.That(result).IsEqualTo("no-headers");
        await Assert.That(context.Headers!.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Deserialize_PreservesHeadersThroughNestedRouterThenClearsLeaf()
    {
        var inner = new HeaderRoutingDeserializer<string>(
            "schema",
            new PrefixDeserializer("inner-fallback"),
            new HeaderDeserializerRoute<string>("v2"u8.ToArray(), new HeaderPresenceDeserializer()));
        var outer = new HeaderRoutingDeserializer<string>(
            "event-type",
            new PrefixDeserializer("outer-fallback"),
            new HeaderDeserializerRoute<string>("created"u8.ToArray(), inner));
        var context = CreateContext();
        context.Headers = Headers.Create("event-type", "created").Add("schema", "v2");

        var result = outer.Deserialize("payload"u8.ToArray(), context);

        await Assert.That(result).IsEqualTo("no-headers");
        await Assert.That(context.Headers!.Count).IsEqualTo(2);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Deserialize_PreservesCallerHeadersThroughRoutingWrapper(bool schemaIdRouter)
    {
        var inner = new HeaderRoutingDeserializer<string>(
            "schema",
            new PrefixDeserializer("inner-fallback"),
            new HeaderDeserializerRoute<string>("v2"u8.ToArray(), new HeaderPresenceDeserializer()));
        IDeserializer<string> wrapper = schemaIdRouter
            ? new SchemaIdRoutingDeserializer<string>().Register(42, inner).Freeze()
            : new TopicRoutingDeserializer<string>().Register("events", inner).Freeze();
        var outer = new HeaderRoutingDeserializer<string>(
            "event-type",
            new PrefixDeserializer("outer-fallback"),
            new HeaderDeserializerRoute<string>("created"u8.ToArray(), wrapper));
        var context = CreateContext();
        context.Headers = Headers.Create("event-type", "created").Add("schema", "v2");
        var data = schemaIdRouter ? Frame(42, "payload"u8.ToArray()) : "payload"u8.ToArray();

        var result = outer.Deserialize(data, context);

        await Assert.That(result).IsEqualTo("no-headers");
        await Assert.That(context.Headers).Count().IsEqualTo(2);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ConsumeResult_RoutingWrapperClearsCallerHeadersBeforeOrdinaryLeaf(bool schemaIdRouter)
    {
        var headerRouter = new HeaderRoutingDeserializer<string>(
            "event-type",
            new PrefixDeserializer("fallback"),
            new HeaderDeserializerRoute<string>("created"u8.ToArray(), new PrefixDeserializer("created")));
        IDeserializer<string> wrapper = schemaIdRouter
            ? new SchemaIdRoutingDeserializer<string>()
                .Register(42, new HeaderPresenceDeserializer())
                .Register(43, headerRouter)
                .Freeze()
            : new TopicRoutingDeserializer<string>()
                .Register("events", new HeaderPresenceDeserializer())
                .Register("header-events", headerRouter)
                .Freeze();
        var data = schemaIdRouter ? Frame(42, "payload"u8.ToArray()) : "payload"u8.ToArray();
        Header[] headers = [new Header("event-type", "created"u8.ToArray())];

        var result = new ConsumeResult<string, string>(
            "events",
            partition: 0,
            offset: 0,
            keyData: ReadOnlyMemory<byte>.Empty,
            isKeyNull: true,
            valueData: data,
            isValueNull: false,
            headers,
            timestampMs: 0,
            timestampType: TimestampType.CreateTime,
            leaderEpoch: null,
            keyDeserializer: null,
            valueDeserializer: wrapper);

        await Assert.That(result.Value).IsEqualTo("no-headers");
        await Assert.That(result.Headers).Count().IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Deserialize_MissingOrNullHeaderUsesFallback(bool includeNullHeader)
    {
        var router = CreateRouter();
        var headers = includeNullHeader
            ? new[] { new Header("event-type", (byte[]?)null) }
            : [];

        var result = router.DeserializeWithHeaders("payload"u8.ToArray(), CreateContext(), headers);

        await Assert.That(result).IsEqualTo("fallback:payload");
    }

    [Test]
    public async Task Deserialize_DuplicateHeadersUsesLastValue()
    {
        var router = CreateRouter();
        var headers = new[]
        {
            new Header("event-type", "created"u8.ToArray()),
            new Header("event-type", "deleted"u8.ToArray())
        };

        var result = router.DeserializeWithHeaders("payload"u8.ToArray(), CreateContext(), headers);

        await Assert.That(result).IsEqualTo("deleted:payload");
    }

    [Test]
    public async Task Deserialize_UnknownNewestDuplicateUsesFallback()
    {
        var router = CreateRouter();
        var headers = new[]
        {
            new Header("event-type", "created"u8.ToArray()),
            new Header("event-type", "unknown"u8.ToArray())
        };

        var result = router.DeserializeWithHeaders("payload"u8.ToArray(), CreateContext(), headers);

        await Assert.That(result).IsEqualTo("fallback:payload");
    }

    [Test]
    public async Task Deserialize_ParsedNestedRoutingUsesPooledHeaderIndex()
    {
        var leaf = new HeaderPresenceDeserializer();
        var route4 = CreateNestedRouter("route-4", "hit"u8.ToArray(), leaf);
        var route3 = CreateNestedRouter("route-3", "next"u8.ToArray(), route4);
        var route2 = CreateNestedRouter("route-2", "next"u8.ToArray(), route3);
        var root = CreateNestedRouter("route-1", "next"u8.ToArray(), route2);
        var plan = RecordHeaderRoutingPlan.Create(Serializers.String, root)!;
        var record = new Record
        {
            Value = "payload"u8.ToArray(),
            Headers =
            [
                new Header("route-1", "next"u8.ToArray()),
                new Header("route-2", "next"u8.ToArray()),
                new Header("route-3", "next"u8.ToArray()),
                new Header("route-4", "hit"u8.ToArray())
            ],
            HeaderCount = 4
        };
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        record.Write(ref writer);
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var parsed = Record.Read(ref reader, plan);
        var context = CreateContext();
        context.Headers = Headers.Create("caller", "owned");

        string result;
        try
        {
            var lookup = parsed.CreateHeaderRoutingLookup(plan);
            result = RecordHeaderDeserializer.Deserialize(
                root,
                parsed.Value,
                context,
                in lookup);
        }
        finally
        {
            ArrayPool<Header>.Shared.Return(parsed.Headers!, clearArray: true);
        }

        await Assert.That(result).IsEqualTo("no-headers");
        await Assert.That(context.Headers!.Count).IsEqualTo(1);
    }

    [Test]
    public async Task LazyRecordList_AttachingPlanAfterParsingBuildsCompletePooledHeaderIndex()
    {
        const int routeCount = 18;
        var (plan, headers) = CreateNestedHeaderRoutingPlan(routeCount);
        var record = new Record
        {
            Value = "payload"u8.ToArray(),
            Headers = headers,
            HeaderCount = headers.Length
        };
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        record.Write(ref writer);
        using var records = LazyRecordList.Create(buffer.WrittenMemory, count: 1);
        records.EnsureAllParsed();

        records.ConfigureHeaderRouting(plan);
        var parsed = records[0];
        parsed.Headers![routeCount - 1] = default;
        var lookup = parsed.CreateHeaderRoutingLookup(plan);

        var found = lookup.TryGetLast($"route-{routeCount - 1}", out var indexedHeader);

        await Assert.That(found).IsTrue();
        await Assert.That(indexedHeader.Key).IsEqualTo($"route-{routeCount - 1}");
    }

    [Test]
    public async Task RecordBatch_AttachingPlanAfterPartialParsingBuildsCompletePooledHeaderIndex()
    {
        const int routeCount = 18;
        var (plan, headers) = CreateNestedHeaderRoutingPlan(routeCount);
        var record = new Record
        {
            Value = "payload"u8.ToArray(),
            Headers = headers,
            HeaderCount = headers.Length
        };
        using var source = new RecordBatch { Records = [record, record] };
        var buffer = new ArrayBufferWriter<byte>();
        source.Write(buffer);
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var batch = RecordBatch.Read(ref reader);
        var slab = ArrayPool<Record>.Shared.Rent(3);
        slab.AsSpan().Clear();
        try
        {
            batch.UseParsedRecordSlab(slab, offset: 1);
            _ = batch.Records[0];

            batch.ConfigureHeaderRouting(plan);
            var parsedBeforeAttach = batch.Records[0];
            var parsedAfterAttach = batch.Records[1];
            parsedBeforeAttach.Headers![routeCount - 1] = default;
            parsedAfterAttach.Headers![routeCount - 1] = default;
            var lookupBeforeAttach = parsedBeforeAttach.CreateHeaderRoutingLookup(plan);
            var lookupAfterAttach = parsedAfterAttach.CreateHeaderRoutingLookup(plan);

            var foundBeforeAttach = lookupBeforeAttach.TryGetLast(
                $"route-{routeCount - 1}",
                out var indexedBeforeAttach);
            var foundAfterAttach = lookupAfterAttach.TryGetLast(
                $"route-{routeCount - 1}",
                out var indexedAfterAttach);

            await Assert.That(foundBeforeAttach).IsTrue();
            await Assert.That(indexedBeforeAttach.Key).IsEqualTo($"route-{routeCount - 1}");
            await Assert.That(foundAfterAttach).IsTrue();
            await Assert.That(indexedAfterAttach.Key).IsEqualTo($"route-{routeCount - 1}");
        }
        finally
        {
            batch.Dispose();
            ArrayPool<Record>.Shared.Return(slab, clearArray: true);
        }
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Deserialize_OuterRouterPropagatesLookupAndClearsLeaf(
        bool schemaIdRouter,
        bool selectHeaderRouter)
    {
        var headerRouter = new HeaderRoutingDeserializer<string>(
            "event-type",
            new PrefixDeserializer("fallback"),
            new HeaderDeserializerRoute<string>(
                "created"u8.ToArray(),
                new HeaderPresenceDeserializer()));
        IDeserializer<string> selected = selectHeaderRouter
            ? headerRouter
            : new HeaderPresenceDeserializer();
        IDeserializer<string> root = schemaIdRouter
            ? new SchemaIdRoutingDeserializer<string>()
                .Register(42, selected)
                .Register(43, headerRouter)
                .Freeze()
            : new TopicRoutingDeserializer<string>()
                .Register("events", selected)
                .Register("header-events", headerRouter)
                .Freeze();
        var plan = RecordHeaderRoutingPlan.Create(Serializers.String, root)!;
        var value = schemaIdRouter ? Frame(42, "payload"u8.ToArray()) : "payload"u8.ToArray();
        var record = new Record
        {
            Value = value,
            Headers = [new Header("event-type", "created"u8.ToArray())],
            HeaderCount = 1
        };
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        record.Write(ref writer);
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var parsed = Record.Read(ref reader, plan);
        var context = CreateContext();
        context.Headers = Headers.Create("caller", "owned");

        string result;
        try
        {
            var lookup = parsed.CreateHeaderRoutingLookup(plan);
            result = RecordHeaderDeserializer.Deserialize(root, parsed.Value, context, in lookup);
        }
        finally
        {
            ArrayPool<Header>.Shared.Return(parsed.Headers!, clearArray: true);
        }

        await Assert.That(result).IsEqualTo("no-headers");
        await Assert.That(context.Headers).Count().IsEqualTo(1);
    }

    [Test]
    public async Task HeaderRoutingPlan_OuterRouterWithoutHeaderChildrenReturnsNull()
    {
        var router = new TopicRoutingDeserializer<string>()
            .Register("events", Serializers.String)
            .Freeze();

        var plan = RecordHeaderRoutingPlan.Create(Serializers.String, router);

        await Assert.That(plan).IsNull();
    }

    [Test]
    public async Task Constructor_DuplicateRouteValuesThrows()
    {
        await Assert.That(() => new HeaderRoutingDeserializer<string>(
                "event-type",
                new PrefixDeserializer("fallback"),
                new HeaderDeserializerRoute<string>("same"u8.ToArray(), new PrefixDeserializer("first")),
                new HeaderDeserializerRoute<string>("same"u8.ToArray(), new PrefixDeserializer("second"))))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Constructor_DefaultRouteThrows()
    {
        await Assert.That(() => new HeaderRoutingDeserializer<string>(
                "event-type",
                new PrefixDeserializer("fallback"),
                default(HeaderDeserializerRoute<string>)))
            .Throws<ArgumentException>();
    }

    private static HeaderRoutingDeserializer<string> CreateRouter() => new(
        "event-type",
        new PrefixDeserializer("fallback"),
        new HeaderDeserializerRoute<string>("created"u8.ToArray(), new PrefixDeserializer("created")),
        new HeaderDeserializerRoute<string>("deleted"u8.ToArray(), new PrefixDeserializer("deleted")));

    private static HeaderRoutingDeserializer<string> CreateNestedRouter(
        string headerName,
        byte[] routeValue,
        IDeserializer<string> child) =>
        new(
            headerName,
            new PrefixDeserializer("fallback"),
            new HeaderDeserializerRoute<string>(routeValue, child));

    private static (RecordHeaderRoutingPlan Plan, Header[] Headers) CreateNestedHeaderRoutingPlan(
        int routeCount)
    {
        IDeserializer<string> root = new HeaderPresenceDeserializer();
        var headers = new Header[routeCount];
        for (var index = routeCount - 1; index >= 0; index--)
        {
            var headerName = $"route-{index}";
            root = CreateNestedRouter(headerName, "next"u8.ToArray(), root);
            headers[index] = new Header(headerName, "next"u8.ToArray());
        }

        return (RecordHeaderRoutingPlan.Create(Serializers.String, root)!, headers);
    }

    private static byte[] Frame(int schemaId, ReadOnlySpan<byte> payload)
    {
        var frame = new byte[sizeof(byte) + sizeof(int) + payload.Length];
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(1), schemaId);
        payload.CopyTo(frame.AsSpan(5));
        return frame;
    }

    private static SerializationContext CreateContext() => new()
    {
        Topic = "events",
        Component = SerializationComponent.Value
    };

    private sealed class PrefixDeserializer(string prefix) : IDeserializer<string>
    {
        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
            $"{prefix}:{Encoding.UTF8.GetString(data.Span)}";
    }

    private sealed class HeaderPresenceDeserializer : IDeserializer<string>
    {
        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
            context.Headers is null ? "no-headers" : "headers";
    }
}
