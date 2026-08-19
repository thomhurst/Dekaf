using System.Buffers;
using System.Buffers.Binary;
using System.Text;
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
