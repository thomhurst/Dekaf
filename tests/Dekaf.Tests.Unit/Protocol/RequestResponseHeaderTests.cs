using System.Buffers;
using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class RequestHeaderTests
{
    [Test]
    public async Task WriteRead_V0_RoundTrip()
    {
        var original = new RequestHeader
        {
            ApiKey = ApiKey.Produce,
            ApiVersion = 9,
            CorrelationId = 42,
            HeaderVersion = 0
        };

        var result = RoundTrip(original);

        await Assert.That(result.ApiKey).IsEqualTo(ApiKey.Produce);
        await Assert.That(result.ApiVersion).IsEqualTo((short)9);
        await Assert.That(result.CorrelationId).IsEqualTo(42);
    }

    [Test]
    public async Task WriteRead_V1_WithClientId_RoundTrip()
    {
        var original = new RequestHeader
        {
            ApiKey = ApiKey.Fetch,
            ApiVersion = 12,
            CorrelationId = 123,
            ClientId = "dekaf-test",
            HeaderVersion = 1
        };

        var result = RoundTrip(original);

        await Assert.That(result.ApiKey).IsEqualTo(ApiKey.Fetch);
        await Assert.That(result.ApiVersion).IsEqualTo((short)12);
        await Assert.That(result.CorrelationId).IsEqualTo(123);
        await Assert.That(result.ClientId).IsEqualTo("dekaf-test");
    }

    [Test]
    public async Task WriteRead_V1_NullClientId_RoundTrip()
    {
        var original = new RequestHeader
        {
            ApiKey = ApiKey.Metadata,
            ApiVersion = 3,
            CorrelationId = 7,
            ClientId = null,
            HeaderVersion = 1
        };

        var result = RoundTrip(original);

        await Assert.That(result.ClientId).IsNull();
    }

    [Test]
    public async Task WriteRead_V2_WithTaggedFields_RoundTrip()
    {
        var original = new RequestHeader
        {
            ApiKey = ApiKey.ApiVersions,
            ApiVersion = 3,
            CorrelationId = 999,
            ClientId = "dekaf-v2",
            HeaderVersion = 2
        };

        var result = RoundTrip(original);

        await Assert.That(result.ApiKey).IsEqualTo(ApiKey.ApiVersions);
        await Assert.That(result.ApiVersion).IsEqualTo((short)3);
        await Assert.That(result.CorrelationId).IsEqualTo(999);
        await Assert.That(result.ClientId).IsEqualTo("dekaf-v2");
    }

    private static RequestHeader RoundTrip(RequestHeader original)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        original.Write(ref writer);

        var reader = new KafkaProtocolReader(new ReadOnlySequence<byte>(buffer.WrittenMemory));
        return RequestHeader.Read(ref reader, original.HeaderVersion);
    }
}

public sealed class ResponseHeaderTests
{
    [Test]
    public async Task WriteRead_V0_RoundTrip()
    {
        var original = new ResponseHeader
        {
            CorrelationId = 42,
            HeaderVersion = 0
        };

        var result = RoundTrip(original);

        await Assert.That(result.CorrelationId).IsEqualTo(42);
    }

    [Test]
    public async Task WriteRead_V1_WithTaggedFields_RoundTrip()
    {
        var original = new ResponseHeader
        {
            CorrelationId = 999,
            HeaderVersion = 1
        };

        var result = RoundTrip(original);

        await Assert.That(result.CorrelationId).IsEqualTo(999);
    }

    [Test]
    public async Task WriteRead_V0_LargeCorrelationId_RoundTrip()
    {
        var original = new ResponseHeader
        {
            CorrelationId = int.MaxValue,
            HeaderVersion = 0
        };

        var result = RoundTrip(original);

        await Assert.That(result.CorrelationId).IsEqualTo(int.MaxValue);
    }

    private static ResponseHeader RoundTrip(ResponseHeader original)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        original.Write(ref writer);

        var reader = new KafkaProtocolReader(new ReadOnlySequence<byte>(buffer.WrittenMemory));
        return ResponseHeader.Read(ref reader, original.HeaderVersion);
    }
}
