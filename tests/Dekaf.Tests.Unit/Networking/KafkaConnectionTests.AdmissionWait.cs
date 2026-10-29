using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Networking;

public sealed partial class KafkaConnectionTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    [Timeout(20_000)]
    public async Task FetchAdmissionWait_CancelledLastFetch_KeepsReauthenticationDrainBlocked(
        bool cancelDrain, CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accept = AcceptAndCompleteHandshakeAsync(listener, cancellationToken);
        await using var connection = new KafkaConnection(
            IPAddress.Loopback.ToString(), port, clientId: null,
            new ConnectionOptions { RequestTimeout = TimeSpan.FromSeconds(5) }, logger: null,
            ResponseBufferPool.Default, responseMemoryAdmissionsEnabled: true);
        await connection.ConnectAsync(cancellationToken);
        using var server = await accept;
        var stream = server.GetStream();
        using var memoryPool = new FetchBufferMemoryPool(64);
        using var buffered = await memoryPool.ReserveAsync(64, cancellationToken);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var renewal = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var fetch = connection.SendPipelinedAsync<FetchRequest, FetchResponse>(
            new FetchRequest { ResponseMemoryPool = memoryPool }, 16, caller.Token);
        var request = await ReadRequestFrameAsync(stream, cancellationToken);
        await stream.WriteAsync(BuildEmptyFetchV16ResponseFrame(
            BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4))), cancellationToken);
        while (!connection.IsResponseAdmissionWaiting)
            await Task.Delay(1, cancellationToken);

        var drain = StartDrain(renewal.Token);
        await Assert.That(drain.IsCompleted).IsFalse();
        var drained = GetPrivateField<TaskCompletionSource>(connection, "_reauthenticationResponsesDrained");
        await caller.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await fetch);
        await Assert.That(drained.Task.IsCompleted).IsFalse();
        await Assert.That(connection.IsResponseAdmissionWaiting).IsTrue();
        if (cancelDrain)
        {
            await renewal.CancelAsync();
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await drain);
            await Assert.That(connection.IsConnected).IsTrue();
            // A later renewal must still wait even though no requests remain pending.
            drain = StartDrain(cancellationToken);
            await Assert.That(drain.IsCompleted).IsFalse();
        }

        buffered.Dispose();
        await drain.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        await Assert.That(connection.IsResponseAdmissionWaiting).IsFalse();
        await Assert.That(connection.IsConnected).IsTrue();

        Task StartDrain(CancellationToken token) => (Task)typeof(KafkaConnection)
            .GetMethod("DrainResponsesBeforeReauthenticationAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(connection, [token])!;
    }

    [Test]
    [Arguments(false, false, "complete")]
    [Arguments(false, true, "complete")]
    [Arguments(true, false, "complete")]
    [Arguments(true, true, "complete")]
    [Arguments(false, false, "cancel")]
    [Arguments(true, false, "cancel")]
    [Arguments(false, false, "silent")]
    [Arguments(true, false, "silent")]
    [Timeout(20_000)]
    public async Task FetchAdmissionWait_QueuedControlResponse_SurvivesRequestTimeout(
        bool pipelined, bool sendBeforeWait, string outcome, CancellationToken cancellationToken)
    {
        var requestTimeout = TimeSpan.FromMilliseconds(300);
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accept = AcceptAndCompleteHandshakeAsync(listener, cancellationToken);
        await using var connection = new KafkaConnection(
            IPAddress.Loopback.ToString(), port, clientId: null,
            new ConnectionOptions { RequestTimeout = requestTimeout }, logger: null,
            ResponseBufferPool.Default, responseMemoryAdmissionsEnabled: true);
        await connection.ConnectAsync(cancellationToken);
        using var server = await accept;
        var stream = server.GetStream();
        using var memoryPool = new FetchBufferMemoryPool(64);
        using var buffered = await memoryPool.ReserveAsync(64, cancellationToken);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var fetch = connection.SendPipelinedAsync<FetchRequest, FetchResponse>(
            new FetchRequest { ResponseMemoryPool = memoryPool }, 16, cancellationToken);
        var fetchRequest = await ReadRequestFrameAsync(stream, cancellationToken);
        Task<ApiVersionsResponse>? control = null;
        if (sendBeforeWait)
            control = SendControl();
        await stream.WriteAsync(BuildEmptyFetchV16ResponseFrame(
            BinaryPrimitives.ReadInt32BigEndian(fetchRequest.AsSpan(4))), cancellationToken);
        while (!connection.IsResponseAdmissionWaiting)
            await Task.Delay(1, cancellationToken);
        control ??= SendControl();
        var controlRequest = await ReadRequestFrameAsync(stream, cancellationToken);
        if (outcome != "silent")
            await stream.WriteAsync(BuildApiVersionsV3ResponseFrame(
                BinaryPrimitives.ReadInt32BigEndian(controlRequest.AsSpan(4))), cancellationToken);

        await Task.Delay(requestTimeout * 4, cancellationToken);
        await Assert.That(fetch.IsCompleted).IsFalse();
        await Assert.That(control.IsCompleted).IsFalse();
        await Assert.That(connection.IsConnected).IsTrue();

        if (outcome == "cancel")
        {
            await caller.CancelAsync();
            var exception = await Assert.ThrowsAsync<OperationCanceledException>(async () => await control);
            await Assert.That(exception!.CancellationToken).IsEqualTo(caller.Token);
        }

        buffered.Dispose();
        var fetchResponse = await fetch.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        fetchResponse.PooledMemoryOwner?.Dispose();
        fetchResponse.ReturnToPool();
        if (outcome == "silent")
        {
            var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
                await control.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken));
            await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.RequestTimedOut);
            await Assert.That(connection.IsConnected).IsFalse();
        }
        else
        {
            if (outcome == "complete")
                await Assert.That((await control.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken)).ErrorCode)
                    .IsEqualTo(ErrorCode.None);
            await Assert.That(connection.IsConnected).IsTrue();
        }

        Task<ApiVersionsResponse> SendControl()
        {
            var request = new ApiVersionsRequest { ClientSoftwareName = "test", ClientSoftwareVersion = "1.0" };
            return pipelined
                ? connection.SendPipelinedAsync<ApiVersionsRequest, ApiVersionsResponse>(request, 3, caller.Token)
                : connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(request, 3, caller.Token).AsTask();
        }
    }
}

