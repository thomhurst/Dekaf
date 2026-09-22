using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Retry;

namespace Dekaf.Tests.Unit.Networking;

public sealed partial class KafkaConnectionTests
{
    // A non-Kafka peer (an HTTP proxy, a load balancer health page) or a corrupted stream reads
    // as an impossible frame size. The connection is unusable, but the request itself may succeed
    // on a new connection, so in-flight callers must see a retriable transport failure: Java
    // closes the channel on InvalidReceiveException and reports a disconnect.
    [Test]
    [Arguments(0x48545450)] // "HTTP"
    [Arguments(2)]
    [Arguments(-1)]
    [Timeout(10_000)]
    public async Task ReceiveLoop_ImpossibleFrameSize_FailsInFlightRequestsAsRetriableAndRetiresTheConnection(
        int frameSize, CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accept = AcceptAndCompleteHandshakeAsync(listener, cancellationToken);
        await using var connection = new KafkaConnection(IPAddress.Loopback.ToString(), port);
        await connection.ConnectAsync(cancellationToken);
        using var client = await accept;

        var send = connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
            new ApiVersionsRequest { ClientSoftwareName = "test", ClientSoftwareVersion = "1.0" },
            3,
            cancellationToken).AsTask();
        _ = await ReadRequestFrameAsync(client.GetStream(), cancellationToken);

        var garbage = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(garbage, frameSize);
        await client.GetStream().WriteAsync(garbage, cancellationToken);

        var exception = await Assert.ThrowsAsync<KafkaException>(async () => await send);
        await Assert.That(exception!.IsRetriable).IsTrue();
        await Assert.That(exception.ErrorCode).IsEqualTo(ErrorCode.NetworkException);
        await Assert.That(TransportFailureClassifier.IsRetriable(exception, TransportRetryPolicy.Request, ownerDisposed: false)).IsTrue();
        await Assert.That(connection.IsConnected).IsFalse();
    }

    // A response for a correlation id this connection never issued (a broker bug, or a response
    // to a request whose tracking was already torn down) is dropped; the frame boundary is still
    // intact, so the next response on the same connection must complete its request.
    [Test]
    [Timeout(10_000)]
    public async Task ReceiveLoop_UnknownCorrelationId_IsDroppedAndTheNextResponseCompletes(
        CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accept = AcceptAndCompleteHandshakeAsync(listener, cancellationToken);
        await using var connection = new KafkaConnection(IPAddress.Loopback.ToString(), port);
        await connection.ConnectAsync(cancellationToken);
        using var client = await accept;

        var send = connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
            new ApiVersionsRequest { ClientSoftwareName = "test", ClientSoftwareVersion = "1.0" },
            3,
            cancellationToken).AsTask();
        var request = await ReadRequestFrameAsync(client.GetStream(), cancellationToken);
        var correlationId = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4, 4));

        await client.GetStream().WriteAsync(BuildApiVersionsV3ResponseFrame(correlationId + 1000), cancellationToken);
        await client.GetStream().WriteAsync(BuildApiVersionsV3ResponseFrame(correlationId), cancellationToken);

        var response = await send;
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(connection.IsConnected).IsTrue();
    }
}
