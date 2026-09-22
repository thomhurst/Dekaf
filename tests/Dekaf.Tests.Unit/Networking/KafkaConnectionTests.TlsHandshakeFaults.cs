using System.Net;
using System.Net.Sockets;
using Dekaf.Networking;
using Dekaf.Retry;

namespace Dekaf.Tests.Unit.Networking;

public sealed partial class KafkaConnectionTests
{
    public enum TlsHandshakeFault
    {
        ResetAfterClientHello,
        CloseAfterClientHello,
        CloseBeforeClientHello
    }

    // A broker that restarts, or a load balancer that drops a connection, can cut the socket in
    // the middle of the TLS handshake. That is a transport failure: the next attempt (or another
    // broker) can succeed, so it must classify as retriable and never as the fatal TLS
    // AuthenticationException reserved for a rejected certificate or a protocol mismatch.
    [Test]
    [Arguments(TlsHandshakeFault.ResetAfterClientHello)]
    [Arguments(TlsHandshakeFault.CloseAfterClientHello)]
    [Arguments(TlsHandshakeFault.CloseBeforeClientHello)]
    [Timeout(15_000)]
    public async Task ConnectAsync_PeerCutsTheTlsHandshake_FailsAsARetriableTransportFailure(
        TlsHandshakeFault fault, CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = CutTlsHandshakeAsync(listener, fault, cancellationToken);
        await using var connection = new KafkaConnection(
            IPAddress.Loopback.ToString(),
            port,
            options: new ConnectionOptions
            {
                UseTls = true,
                ConnectionTimeout = TimeSpan.FromSeconds(10)
            });

        var exception = await Assert.ThrowsAsync<Exception>(
            async () => await connection.ConnectAsync(cancellationToken));
        await server;

        await Assert.That(exception).IsNotTypeOf<Dekaf.Errors.AuthenticationException>();
        await Assert.That(TransportFailureClassifier.IsRetriable(
                exception!, TransportRetryPolicy.Request, ownerDisposed: false))
            .IsTrue();
        await Assert.That(TransportFailureClassifier.IsRetriable(
                exception!, TransportRetryPolicy.GroupJoin, ownerDisposed: false))
            .IsTrue();
    }

    private static async Task CutTlsHandshakeAsync(
        TcpListener listener,
        TlsHandshakeFault fault,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        if (fault != TlsHandshakeFault.CloseBeforeClientHello)
        {
            // Wait for the ClientHello so the cut lands inside the handshake.
            var buffer = new byte[5];
            _ = await client.GetStream().ReadAsync(buffer, cancellationToken);
        }

        if (fault == TlsHandshakeFault.ResetAfterClientHello)
            client.Client.LingerState = new LingerOption(enable: true, seconds: 0);

        client.Client.Close();
    }
}
