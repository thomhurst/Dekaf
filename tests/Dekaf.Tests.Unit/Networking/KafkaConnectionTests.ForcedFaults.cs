using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Retry;

namespace Dekaf.Tests.Unit.Networking;

/// <summary>
/// Forced transport faults against a loopback fake broker: a response stream that stops being
/// Kafka framing, stray responses, and TLS peers that fail the handshake.
/// </summary>
public sealed partial class KafkaConnectionTests
{
    [Test]
    [Arguments("http")]
    [Arguments("oversized")]
    [Arguments("negative")]
    [Arguments("undersized")]
    [Timeout(10_000)]
    public async Task ReceiveLoop_InvalidFrameSize_FailsPendingRequestWithRetriableNetworkError(
        string sizePrefix,
        CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accept = AcceptAndCompleteHandshakeAsync(listener, cancellationToken);
        await using var connection = new KafkaConnection(IPAddress.Loopback.ToString(), port);
        await connection.ConnectAsync(cancellationToken);
        using var server = await accept;

        var response = connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
            new ApiVersionsRequest { ClientSoftwareName = "test", ClientSoftwareVersion = "1.0" },
            apiVersion: 3,
            cancellationToken).AsTask();
        _ = await ReadRequestFrameAsync(server.GetStream(), cancellationToken);
        await server.GetStream().WriteAsync(CreateInvalidSizePrefix(sizePrefix), cancellationToken);

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await response.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken));

        // A stream that stopped being Kafka framing is a broken connection, like EOF: the
        // request may be retried on a new connection, and this one is never reused.
        await Assert.That(exception!.Message).Contains("Invalid response frame size");
        await Assert.That(exception.ErrorCode).IsEqualTo(ErrorCode.NetworkException);
        await Assert.That(exception.IsRetriable).IsTrue();
        await Assert.That(TransportFailureClassifier.IsRetriable(
            exception, TransportRetryPolicy.Request, ownerDisposed: false)).IsTrue();
        await Assert.That(connection.IsConnected).IsFalse();
    }

    [Test]
    [Timeout(10_000)]
    public async Task ReceiveLoop_UnknownCorrelationIdThenMatchingResponse_CompletesRequest(
        CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accept = AcceptAndCompleteHandshakeAsync(listener, cancellationToken);
        await using var connection = new KafkaConnection(IPAddress.Loopback.ToString(), port);
        await connection.ConnectAsync(cancellationToken);
        using var server = await accept;

        var response = connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
            new ApiVersionsRequest { ClientSoftwareName = "test", ClientSoftwareVersion = "1.0" },
            apiVersion: 3,
            cancellationToken).AsTask();
        var request = await ReadRequestFrameAsync(server.GetStream(), cancellationToken);
        var correlationId = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4, 4));

        // A frame for a request this connection never sent (or already gave up on) is
        // dropped; the stream stays frame-aligned and the real response still lands.
        await server.GetStream().WriteAsync(
            BuildApiVersionsV3ResponseFrame(unchecked(correlationId + 1_000_000)),
            cancellationToken);
        await server.GetStream().WriteAsync(BuildApiVersionsV3ResponseFrame(correlationId), cancellationToken);

        var result = await response.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        await Assert.That(result.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(connection.IsConnected).IsTrue();
        await Assert.That(GetPrivateField<int>(connection, "_pendingRequestCount")).IsEqualTo(0);
    }

    [Test]
    [Arguments("close")]
    [Arguments("reset")]
    [Timeout(10_000)]
    public async Task ConnectAsync_TlsPeerDropsAfterClientHello_FailsWithRetriableTransportError(
        string fault,
        CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = RunTlsFaultPeerAsync(listener, fault, cancellationToken);
        await using var connection = new KafkaConnection(
            IPAddress.Loopback.ToString(),
            port,
            options: new ConnectionOptions { UseTls = true });

        var exception = await CaptureConnectFailureAsync(connection, cancellationToken);
        await server;

        await Assert.That(exception).IsNotTypeOf<OperationCanceledException>();
        await Assert.That(exception).IsNotAssignableTo<Dekaf.Errors.AuthenticationException>();
        await Assert.That(TransportFailureClassifier.IsRetriable(
            exception, TransportRetryPolicy.Request, ownerDisposed: false)).IsTrue();
        await Assert.That(connection.IsConnected).IsFalse();
    }

    [Test]
    [Timeout(10_000)]
    public async Task ConnectAsync_TlsPeerAnswersClientHelloWithPlaintext_FailsAsTlsHandshakeError(
        CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = RunTlsFaultPeerAsync(listener, "plaintext", cancellationToken);
        await using var connection = new KafkaConnection(
            IPAddress.Loopback.ToString(),
            port,
            options: new ConnectionOptions { UseTls = true });

        var exception = await CaptureConnectFailureAsync(connection, cancellationToken);
        await server;

        // A plaintext listener behind a TLS client is a cluster-wide configuration error, so it
        // surfaces as the fatal TLS handshake failure instead of retrying until a deadline.
        await Assert.That(exception).IsTypeOf<Dekaf.Errors.AuthenticationException>();
        await Assert.That(TransportFailureClassifier.IsRetriable(
            exception, TransportRetryPolicy.Request, ownerDisposed: false)).IsFalse();
        await Assert.That(connection.IsConnected).IsFalse();
    }

    private static byte[] CreateInvalidSizePrefix(string sizePrefix)
    {
        if (sizePrefix == "http")
            return Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request\r\n\r\n");

        var size = sizePrefix switch
        {
            "oversized" => ResponseBufferPool.DefaultMaxArrayLength + 1,
            "negative" => -1,
            "undersized" => ConnectionHelper.MinimumResponseFrameSize - 1,
            _ => throw new ArgumentOutOfRangeException(nameof(sizePrefix), sizePrefix, null)
        };
        var prefix = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(prefix, size);
        return prefix;
    }

    private static async Task<Exception> CaptureConnectFailureAsync(
        KafkaConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            await connection.ConnectAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            return exception;
        }

        throw new InvalidOperationException("ConnectAsync succeeded against a faulting TLS peer.");
    }

    /// <summary>
    /// Accepts one client, reads its whole ClientHello record, then faults: a graceful close
    /// (FIN), an abortive close (RST), or a plaintext answer followed by a close.
    /// </summary>
    private static async Task RunTlsFaultPeerAsync(
        TcpListener listener,
        string fault,
        CancellationToken cancellationToken)
    {
        using var socket = await listener.AcceptSocketAsync(cancellationToken).ConfigureAwait(false);
        using var stream = new NetworkStream(socket, ownsSocket: false);

        var header = new byte[5];
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        if (header[0] != 0x16)
            throw new InvalidOperationException($"Expected a TLS handshake record, got content type {header[0]}.");

        // Draining the record leaves nothing unread, so a plain close sends FIN rather than RST.
        var body = new byte[BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(3))];
        await stream.ReadExactlyAsync(body, cancellationToken).ConfigureAwait(false);

        switch (fault)
        {
            case "close":
                socket.Shutdown(SocketShutdown.Send);
                break;
            case "reset":
                socket.LingerState = new LingerOption(true, 0);
                break;
            case "plaintext":
                await stream.WriteAsync(
                    Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\n\r\n"),
                    cancellationToken).ConfigureAwait(false);
                socket.Shutdown(SocketShutdown.Send);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(fault), fault, null);
        }

        socket.Close();
    }
}
