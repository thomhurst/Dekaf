using System.Buffers;
using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Tests for Kafka protocol request and response header encoding.
/// Reference: https://kafka.apache.org/protocol#protocol_messages
///
/// Request Header v0: api_key (INT16) + api_version (INT16) + correlation_id (INT32)
/// Request Header v1: + client_id (NULLABLE_STRING)
/// Request Header v2: + TAG_BUFFER (flexible versions)
///
/// IMPORTANT: client_id in request header ALWAYS uses NULLABLE_STRING encoding
/// (INT16 length prefix), even in v2 headers. The schema shows flexibleVersions: "none"
/// for the client_id field.
///
/// Response Header v0: correlation_id (INT32)
/// Response Header v1: + TAG_BUFFER (flexible versions)
/// </summary>
public class HeaderEncodingTests
{
    #region Request Header v0 Tests

    [Test]
    public async Task RequestHeader_V0_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var header = new RequestHeader
        {
            ApiKey = ApiKey.Metadata,
            ApiVersion = 1,
            CorrelationId = 42,
            HeaderVersion = 0
        };
        header.Write(ref writer);

        var expected = new byte[]
        {
            0x00, 0x03, // ApiKey = 3 (Metadata)
            0x00, 0x01, // ApiVersion = 1
            0x00, 0x00, 0x00, 0x2A // CorrelationId = 42
        };
        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expected);
    }

    #endregion

    #region Request Header v1 Tests

    [Test]
    public async Task RequestHeader_V1_WithClientId_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var header = new RequestHeader
        {
            ApiKey = ApiKey.Fetch,
            ApiVersion = 5,
            CorrelationId = 100,
            ClientId = "test",
            HeaderVersion = 1
        };
        header.Write(ref writer);

        var expected = new byte[]
        {
            0x00, 0x01, // ApiKey = 1 (Fetch)
            0x00, 0x05, // ApiVersion = 5
            0x00, 0x00, 0x00, 0x64, // CorrelationId = 100
            0x00, 0x04, (byte)'t', (byte)'e', (byte)'s', (byte)'t' // ClientId (NULLABLE_STRING)
        };
        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expected);
    }

    [Test]
    public async Task RequestHeader_V1_NullClientId_EncodedAsMinusOne()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var header = new RequestHeader
        {
            ApiKey = ApiKey.Produce,
            ApiVersion = 3,
            CorrelationId = 1,
            ClientId = null,
            HeaderVersion = 1
        };
        header.Write(ref writer);

        var expected = new byte[]
        {
            0x00, 0x00, // ApiKey = 0 (Produce)
            0x00, 0x03, // ApiVersion = 3
            0x00, 0x00, 0x00, 0x01, // CorrelationId = 1
            0xFF, 0xFF // ClientId = null (NULLABLE_STRING, -1)
        };
        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expected);
    }

    #endregion

    #region Request Header v2 Tests (Flexible)

    [Test]
    public async Task RequestHeader_V2_ClientIdUsesNullableString()
    {
        // CRITICAL: Even in v2 headers, ClientId uses NULLABLE_STRING encoding
        // (INT16 length prefix), NOT COMPACT_STRING. This is per the Kafka spec
        // where client_id has flexibleVersions: "none".

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var header = new RequestHeader
        {
            ApiKey = ApiKey.Produce,
            ApiVersion = 9,
            CorrelationId = 42,
            ClientId = "test",
            HeaderVersion = 2
        };
        header.Write(ref writer);

        var expected = new byte[]
        {
            0x00, 0x00, // ApiKey = 0 (Produce)
            0x00, 0x09, // ApiVersion = 9
            0x00, 0x00, 0x00, 0x2A, // CorrelationId = 42
            0x00, 0x04, (byte)'t', (byte)'e', (byte)'s', (byte)'t', // ClientId (NULLABLE_STRING, NOT compact!)
            0x00 // Empty tagged fields
        };
        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expected);
    }

    [Test]
    public async Task RequestHeader_V2_EmptyTaggedFields()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var header = new RequestHeader
        {
            ApiKey = ApiKey.ApiVersions,
            ApiVersion = 3,
            CorrelationId = 1,
            ClientId = null,
            HeaderVersion = 2
        };
        header.Write(ref writer);

        // Last byte should be 0x00 for empty tagged fields
        await Assert.That(buffer.WrittenSpan[^1]).IsEqualTo((byte)0x00);
    }

    #endregion

    #region Request Header Round-trip Tests

    [Test]
    [Arguments((short)0)]
    [Arguments((short)1)]
    [Arguments((short)2)]
    public async Task RequestHeader_RoundTrip(short headerVersion)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new RequestHeader
        {
            ApiKey = ApiKey.Metadata,
            ApiVersion = 12,
            CorrelationId = 12345,
            ClientId = headerVersion >= 1 ? "dekaf-client" : null,
            HeaderVersion = headerVersion
        };
        original.Write(ref writer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var parsed = RequestHeader.Read(ref reader, headerVersion);

        await Assert.That(parsed.ApiKey).IsEqualTo(original.ApiKey);
        await Assert.That(parsed.ApiVersion).IsEqualTo(original.ApiVersion);
        await Assert.That(parsed.CorrelationId).IsEqualTo(original.CorrelationId);

        if (headerVersion >= 1)
        {
            await Assert.That(parsed.ClientId).IsEqualTo(original.ClientId);
        }
    }

    #endregion

    #region Response Header Tests

    [Test]
    public async Task ResponseHeader_V0_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var header = new ResponseHeader
        {
            CorrelationId = 42,
            HeaderVersion = 0
        };
        header.Write(ref writer);

        // Just correlation ID
        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { 0x00, 0x00, 0x00, 0x2A });
    }

    [Test]
    public async Task ResponseHeader_V1_WithTaggedFields()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var header = new ResponseHeader
        {
            CorrelationId = 42,
            HeaderVersion = 1
        };
        header.Write(ref writer);

        var expected = new byte[]
        {
            0x00, 0x00, 0x00, 0x2A, // CorrelationId = 42
            0x00 // Empty tagged fields
        };
        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expected);
    }

    [Test]
    [Arguments((short)0)]
    [Arguments((short)1)]
    public async Task ResponseHeader_RoundTrip(short headerVersion)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new ResponseHeader
        {
            CorrelationId = 99999,
            HeaderVersion = headerVersion
        };
        original.Write(ref writer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var parsed = ResponseHeader.Read(ref reader, headerVersion);

        await Assert.That(parsed.CorrelationId).IsEqualTo(original.CorrelationId);
    }

    #endregion
}
