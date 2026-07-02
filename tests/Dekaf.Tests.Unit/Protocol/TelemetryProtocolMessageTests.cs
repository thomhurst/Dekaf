using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class TelemetryProtocolMessageTests
{
    [Test]
    public async Task GetTelemetrySubscriptionsRequest_V0_FirstRequest_WritesZeroClientInstanceId()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var request = new GetTelemetrySubscriptionsRequest();

        request.Write(ref writer, version: 0);

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(new byte[17]);
    }

    [Test]
    public async Task GetTelemetrySubscriptionsResponse_V0_ReadsSubscription()
    {
        var clientInstanceId = new Guid("00112233-4455-6677-8899-aabbccddeeff");
        var data = new List<byte>();

        data.AddRange([0x00, 0x00, 0x00, 0x05]);
        data.AddRange([0x00, 0x00]);
        data.AddRange(clientInstanceId.ToByteArray(bigEndian: true));
        data.AddRange([0x00, 0x00, 0x00, 0x2A]);
        data.Add(0x03);
        data.Add(0x00);
        data.Add(0x04);
        data.AddRange([0x00, 0x00, 0xEA, 0x60]);
        data.AddRange([0x00, 0x10, 0x00, 0x00]);
        data.Add(0x01);
        data.Add(0x03);
        data.Add(0x1B);
        data.AddRange("org.apache.kafka.producer."u8.ToArray());
        data.Add(0x01);
        data.Add(0x00);

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (GetTelemetrySubscriptionsResponse)GetTelemetrySubscriptionsResponse.Read(ref reader, version: 0);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(5);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.ClientInstanceId).IsEqualTo(clientInstanceId);
        await Assert.That(response.SubscriptionId).IsEqualTo(42);
        await Assert.That(response.AcceptedCompressionTypes).IsEquivalentTo([(sbyte)0, (sbyte)4]);
        await Assert.That(response.PushIntervalMs).IsEqualTo(60_000);
        await Assert.That(response.TelemetryMaxBytes).IsEqualTo(1_048_576);
        await Assert.That(response.DeltaTemporality).IsTrue();
        await Assert.That(response.RequestedMetrics).IsEquivalentTo(["org.apache.kafka.producer.", string.Empty]);
    }

    [Test]
    public async Task PushTelemetryRequest_V0_WritesMetricsPayload()
    {
        var clientInstanceId = new Guid("00112233-4455-6677-8899-aabbccddeeff");
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var request = new PushTelemetryRequest
        {
            ClientInstanceId = clientInstanceId,
            SubscriptionId = 42,
            Terminating = true,
            CompressionType = 4,
            Metrics = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }
        };

        request.Write(ref writer, version: 0);

        var expected = new List<byte>();
        expected.AddRange(clientInstanceId.ToByteArray(bigEndian: true));
        expected.AddRange([0x00, 0x00, 0x00, 0x2A]);
        expected.Add(0x01);
        expected.Add(0x04);
        expected.Add(0x05);
        expected.AddRange([0xDE, 0xAD, 0xBE, 0xEF]);
        expected.Add(0x00);

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expected.ToArray());
    }

    [Test]
    public async Task PushTelemetryResponse_V0_ReadsError()
    {
        var data = new byte[]
        {
            0x00, 0x00, 0x00, 0x7B,
            0x00, 0x75,
            0x00
        };

        var reader = new KafkaProtocolReader(data);
        var response = (PushTelemetryResponse)PushTelemetryResponse.Read(ref reader, version: 0);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(123);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.UnknownSubscriptionId);
    }

    [Test]
    public async Task TelemetryRequests_UseFlexibleHeaders()
    {
        await Assert.That(GetRequestHeaderVersion<GetTelemetrySubscriptionsRequest, GetTelemetrySubscriptionsResponse>(0))
            .IsEqualTo((short)2);
        await Assert.That(GetResponseHeaderVersion<GetTelemetrySubscriptionsRequest, GetTelemetrySubscriptionsResponse>(0))
            .IsEqualTo((short)1);
        await Assert.That(GetRequestHeaderVersion<PushTelemetryRequest, PushTelemetryResponse>(0))
            .IsEqualTo((short)2);
        await Assert.That(GetResponseHeaderVersion<PushTelemetryRequest, PushTelemetryResponse>(0))
            .IsEqualTo((short)1);
    }

    private static short GetRequestHeaderVersion<TRequest, TResponse>(short version)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        return TRequest.GetRequestHeaderVersion(version);
    }

    private static short GetResponseHeaderVersion<TRequest, TResponse>(short version)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        return TRequest.GetResponseHeaderVersion(version);
    }
}
