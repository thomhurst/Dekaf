using System.Text;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Serialization;

public sealed class HeaderRoutingDeserializerTests
{
    [Test]
    public async Task Deserialize_KnownHeaderValueUsesMatchingChild()
    {
        var router = CreateRouter();
        var headers = new[] { new Header("event-type", "created"u8.ToArray()) };

        var result = router.Deserialize("payload"u8.ToArray(), CreateContext(headers));

        await Assert.That(result).IsEqualTo("created:payload");
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

        var result = router.Deserialize("payload"u8.ToArray(), CreateContext(headers));

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

        var result = router.Deserialize("payload"u8.ToArray(), CreateContext(headers));

        await Assert.That(result).IsEqualTo("deleted:payload");
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

    private static HeaderRoutingDeserializer<string> CreateRouter() => new(
        "event-type",
        new PrefixDeserializer("fallback"),
        new HeaderDeserializerRoute<string>("created"u8.ToArray(), new PrefixDeserializer("created")),
        new HeaderDeserializerRoute<string>("deleted"u8.ToArray(), new PrefixDeserializer("deleted")));

    private static SerializationContext CreateContext(Header[] headers) => new()
    {
        Topic = "events",
        Component = SerializationComponent.Value,
        RecordHeaders = headers
    };

    private sealed class PrefixDeserializer(string prefix) : IDeserializer<string>
    {
        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
            $"{prefix}:{Encoding.UTF8.GetString(data.Span)}";
    }
}
