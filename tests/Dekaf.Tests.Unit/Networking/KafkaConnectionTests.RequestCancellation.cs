using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Networking;

public sealed partial class KafkaConnectionTests
{
    [Test]
    [Arguments("write lock")]
    [Arguments("pending slot")]
    [Arguments("broker throttle")]
    [Timeout(10_000)]
    public async Task ResponseObservation_PreWriteWaitPreservesCallerCancellation(
        string wait, CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accept = AcceptAndCompleteHandshakeAsync(listener, cancellationToken);
        await using var connection = new KafkaConnection(IPAddress.Loopback.ToString(), port);
        await connection.ConnectAsync(cancellationToken);
        using var client = await accept;
        using var caller = new CancellationTokenSource();
        using var shutdown = new CancellationTokenSource();
        var context = new KafkaRequestWriteContext(shutdown.Token);
        SemaphoreSlim? held = null;
        var heldCount = 0;
        try
        {
            if (wait == "broker throttle")
                GetPrivateField<BrokerThrottleState>(connection, "_brokerThrottleState").Observe(60_000);
            else
            {
                held = GetPrivateField<SemaphoreSlim>(connection,
                    wait == "write lock" ? "_writeLock" : "_pendingRequestSlots");
                while (held.Wait(0, cancellationToken)) heldCount++;
            }
            var send = ((IKafkaRequestCancellationConnection)connection)
                .SendWithResponseCancellationAsync<ApiVersionsRequest, ApiVersionsResponse>(
                    new ApiVersionsRequest { ClientSoftwareName = "test", ClientSoftwareVersion = "1.0" },
                    3, context, caller.Token).AsTask();
            await Assert.That(send.IsCompleted).IsFalse();
            await Assert.That(context.WriteStarted).IsFalse();
            await caller.CancelAsync();
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await send);
            await Assert.That(caller.IsCancellationRequested).IsTrue();
            await Assert.That(context.WriteStarted).IsFalse();
            await Assert.That(client.GetStream().DataAvailable).IsFalse();
            await Assert.That(GetPrivateField<int>(connection, "_pendingRequestCount")).IsEqualTo(0);
        }
        finally
        {
            if (heldCount != 0) held!.Release(heldCount);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    [Timeout(10_000)]
    public async Task ResponseObservation_WrittenRequestUsesShutdownBudget(
        bool deadlineExpires, CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accept = AcceptAndCompleteHandshakeAsync(listener, cancellationToken);
        await using var connection = new KafkaConnection(IPAddress.Loopback.ToString(), port);
        await connection.ConnectAsync(cancellationToken);
        using var client = await accept;
        using var caller = new CancellationTokenSource();
        using var shutdown = new CancellationTokenSource();
        var context = new KafkaRequestWriteContext(shutdown.Token);
        var send = ((IKafkaRequestCancellationConnection)connection)
            .SendWithResponseCancellationAsync<ApiVersionsRequest, ApiVersionsResponse>(
                new ApiVersionsRequest { ClientSoftwareName = "test", ClientSoftwareVersion = "1.0" },
                3, context, caller.Token).AsTask();
        var request = await ReadRequestFrameAsync(client.GetStream(), cancellationToken);
        await caller.CancelAsync();
        await Assert.That(context.WriteStarted).IsTrue();
        await Assert.That(send.IsCompleted).IsFalse();
        if (deadlineExpires)
        {
            await shutdown.CancelAsync();
            var exception = await Assert.ThrowsAsync<OperationCanceledException>(async () => await send);
            await Assert.That(exception!.CancellationToken).IsEqualTo(shutdown.Token);
        }
        else
        {
            var correlationId = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4, 4));
            await client.GetStream().WriteAsync(BuildApiVersionsV3ResponseFrame(correlationId), cancellationToken);
            await Assert.That((await send).ErrorCode).IsEqualTo(ErrorCode.None);
        }
        await Assert.That(GetPrivateField<int>(connection, "_pendingRequestCount")).IsEqualTo(0);
    }
}
