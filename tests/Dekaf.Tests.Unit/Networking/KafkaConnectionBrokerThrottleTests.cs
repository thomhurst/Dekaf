using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Networking;

public sealed class KafkaConnectionBrokerThrottleTests
{
    [Test]
    [Timeout(10_000)]
    public async Task ApplicableResponse_DelaysFollowUpAcrossPhysicalConnections(
        CancellationToken cancellationToken)
    {
        const int throttleTimeMs = 300;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var followUpReceived = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);

        var serverTask = Task.Run(async () =>
        {
            using var firstSocket = await listener.AcceptSocketAsync(cancellationToken);
            await using var firstStream = new NetworkStream(firstSocket, ownsSocket: false);
            await CompleteApiVersionsRequestAsync(firstStream, throttleTimeMs: 0, cancellationToken);

            using var secondSocket = await listener.AcceptSocketAsync(cancellationToken);
            await using var secondStream = new NetworkStream(secondSocket, ownsSocket: false);
            await CompleteApiVersionsRequestAsync(secondStream, throttleTimeMs: 0, cancellationToken);

            await CompleteApiVersionsRequestAsync(firstStream, throttleTimeMs, cancellationToken);
            var secondRequest = await ReadFrameAsync(secondStream, cancellationToken);
            followUpReceived.TrySetResult(Stopwatch.GetTimestamp());
            await WriteApiVersionsResponseAsync(secondStream, secondRequest, 0, cancellationToken);
        }, cancellationToken);

        var throttleState = new BrokerThrottleState();
        await using var first = CreateConnection(port, throttleState);
        await using var second = CreateConnection(port, throttleState);
        await first.ConnectAsync(cancellationToken);
        await second.ConnectAsync(cancellationToken);

        _ = await first.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
            CreateApiVersionsRequest(),
            3,
            cancellationToken);

        var followUpStarted = Stopwatch.GetTimestamp();
        var followUpTask = second.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
            CreateApiVersionsRequest(),
            3,
            cancellationToken);

        var earlyProgress = await Task.WhenAny(
            followUpReceived.Task,
            Task.Delay(100, cancellationToken));
        await Assert.That(earlyProgress).IsNotSameReferenceAs(followUpReceived.Task);

        _ = await followUpTask;
        var receivedAt = await followUpReceived.Task;
        var elapsedMs = (receivedAt - followUpStarted) * 1000d / Stopwatch.Frequency;
        await Assert.That(elapsedMs).IsGreaterThanOrEqualTo(200);
        await serverTask;
    }

    [Test]
    [Timeout(10_000)]
    public async Task ThrottledWait_RemainsIdleReapableAndShutdownResponsive(
        CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var releaseServer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var serverTask = Task.Run(async () =>
        {
            using var socket = await listener.AcceptSocketAsync(cancellationToken);
            await using var stream = new NetworkStream(socket, ownsSocket: false);
            await CompleteApiVersionsRequestAsync(stream, throttleTimeMs: 0, cancellationToken);
            await releaseServer.Task.WaitAsync(cancellationToken);
        }, cancellationToken);

        var throttleState = new BrokerThrottleState();
        var connection = CreateConnection(port, throttleState);
        await connection.ConnectAsync(cancellationToken);
        throttleState.Observe(10_000);

        var sendTask = connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
            CreateApiVersionsRequest(),
            3,
            CancellationToken.None);
        await Task.Delay(50, cancellationToken);

        await Assert.That(((IIdleTrackedKafkaConnection)connection).PendingRequestCount).IsEqualTo(0);
        await Assert.That(((IRetirableKafkaConnection)connection).ActiveOperationCount).IsEqualTo(0);

        await connection.DisposeAsync();
        await Assert.That(async () => await sendTask.AsTask().WaitAsync(cancellationToken))
            .Throws<OperationCanceledException>();

        releaseServer.TrySetResult();
        await serverTask;
    }

    [Test]
    [Timeout(10_000)]
    public async Task PoolThrottleWait_AllowsIdleCloseAndSurvivesReconnect(
        CancellationToken cancellationToken)
    {
        const int throttleTimeMs = 300;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var releaseSecondConnection = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var serverTask = Task.Run(async () =>
        {
            using var firstSocket = await listener.AcceptSocketAsync(cancellationToken);
            await using var firstStream = new NetworkStream(firstSocket, ownsSocket: false);
            await CompleteApiVersionsRequestAsync(firstStream, 0, cancellationToken);
            await CompleteApiVersionsRequestAsync(firstStream, throttleTimeMs, cancellationToken);

            using var secondSocket = await listener.AcceptSocketAsync(cancellationToken);
            await using var secondStream = new NetworkStream(secondSocket, ownsSocket: false);
            await CompleteApiVersionsRequestAsync(secondStream, 0, cancellationToken);
            await releaseSecondConnection.Task.WaitAsync(cancellationToken);
        }, cancellationToken);

        await using var pool = new ConnectionPool(
            connectionOptions: new ConnectionOptions
            {
                ConnectionsMaxIdleMs = 0,
                ReconnectBackoff = TimeSpan.Zero,
                ReconnectBackoffMax = TimeSpan.Zero
            });
        pool.RegisterBroker(1, "127.0.0.1", port);
        var firstConnection = await pool.GetConnectionAsync(1, cancellationToken);
        _ = await firstConnection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
            CreateApiVersionsRequest(),
            3,
            cancellationToken);

        var leaseTask = pool.LeaseConnectionAsync(1, cancellationToken).AsTask();
        var earlyProgress = await Task.WhenAny(leaseTask, Task.Delay(100, cancellationToken));
        await Assert.That(earlyProgress).IsNotSameReferenceAs(leaseTask);

        await Assert.That(await pool.ReapIdleConnectionsAsync()).IsEqualTo(1);
        using var lease = await leaseTask;
        await Assert.That(lease.Connection).IsNotSameReferenceAs(firstConnection);
        await Assert.That(lease.Connection.IsConnected).IsTrue();
        releaseSecondConnection.TrySetResult();
        await serverTask;
    }

    [Test]
    [Timeout(10_000)]
    public async Task BootstrapThrottle_SurvivesBrokerRegistrationAndReconnect(
        CancellationToken cancellationToken)
    {
        const int throttleTimeMs = 300;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var secondConnectionAccepted = new TaskCompletionSource<long>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSecondConnection = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var serverTask = Task.Run(async () =>
        {
            using var firstSocket = await listener.AcceptSocketAsync(cancellationToken);
            await using var firstStream = new NetworkStream(firstSocket, ownsSocket: false);
            await CompleteApiVersionsRequestAsync(firstStream, 0, cancellationToken);
            await CompleteApiVersionsRequestAsync(firstStream, throttleTimeMs, cancellationToken);

            using var secondSocket = await listener.AcceptSocketAsync(cancellationToken);
            secondConnectionAccepted.TrySetResult(Stopwatch.GetTimestamp());
            await using var secondStream = new NetworkStream(secondSocket, ownsSocket: false);
            using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var handshakeTask = CompleteApiVersionsRequestAsync(secondStream, 0, handshakeCts.Token);
            var completed = await Task.WhenAny(handshakeTask, releaseSecondConnection.Task);
            if (completed == handshakeTask)
                await handshakeTask;

            await releaseSecondConnection.Task.WaitAsync(cancellationToken);
            handshakeCts.Cancel();
            try { await handshakeTask; }
            catch (OperationCanceledException) when (handshakeCts.IsCancellationRequested) { }
        }, cancellationToken);

        await using var pool = new ConnectionPool(
            connectionOptions: new ConnectionOptions
            {
                ConnectionsMaxIdleMs = 0,
                ReconnectBackoff = TimeSpan.Zero,
                ReconnectBackoffMax = TimeSpan.Zero
            });
        var bootstrapConnection = await pool.GetConnectionAsync("127.0.0.1", port, cancellationToken);
        _ = await bootstrapConnection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
            CreateApiVersionsRequest(),
            3,
            cancellationToken);
        await Assert.That(await pool.ReapIdleConnectionsAsync()).IsEqualTo(1);

        pool.RegisterBroker(1, "127.0.0.1", port);
        var reconnectStarted = Stopwatch.GetTimestamp();
        var leaseTask = pool.LeaseConnectionAsync(1, cancellationToken).AsTask();
        var earlyProgress = await Task.WhenAny(
            secondConnectionAccepted.Task,
            Task.Delay(100, cancellationToken));
        await Assert.That(earlyProgress).IsNotSameReferenceAs(secondConnectionAccepted.Task);

        using var lease = await leaseTask;
        var acceptedAt = await secondConnectionAccepted.Task;
        var elapsedMs = (acceptedAt - reconnectStarted) * 1000d / Stopwatch.Frequency;
        await Assert.That(elapsedMs).IsGreaterThanOrEqualTo(200);
        releaseSecondConnection.TrySetResult();
        await serverTask;
    }

    [Test]
    [Timeout(10_000)]
    public async Task SharedThrottle_BlocksEveryClientRequestPathBeforeWrite(
        CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var acceptedSocket = new TaskCompletionSource<Socket>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseServer = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var serverTask = Task.Run(async () =>
        {
            using var socket = await listener.AcceptSocketAsync(cancellationToken);
            await using var stream = new NetworkStream(socket, ownsSocket: false);
            await CompleteApiVersionsRequestAsync(stream, 0, cancellationToken);
            acceptedSocket.TrySetResult(socket);
            await releaseServer.Task.WaitAsync(cancellationToken);
        }, cancellationToken);

        var throttleState = new BrokerThrottleState();
        await using var connection = CreateConnection(port, throttleState);
        await connection.ConnectAsync(cancellationToken);
        var serverSocket = await acceptedSocket.Task.WaitAsync(cancellationToken);
        throttleState.Observe(10_000);

        await AssertBlockedBeforeWriteAsync(
            token => connection.SendPipelinedAsync<ProduceRequest, ProduceResponse>(
                new ProduceRequest(), 9, token),
            serverSocket,
            cancellationToken);
        await AssertBlockedBeforeWriteAsync(
            async token => _ = await connection.SendAsync<FetchRequest, FetchResponse>(
                new FetchRequest(), 16, token),
            serverSocket,
            cancellationToken);
        await AssertBlockedBeforeWriteAsync(
            async token => _ = await connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                new ConsumerGroupHeartbeatRequest
                {
                    GroupId = "group",
                    MemberId = "member",
                    MemberEpoch = 1
                },
                1,
                token),
            serverSocket,
            cancellationToken);
        await AssertBlockedBeforeWriteAsync(
            async token => _ = await connection.SendAsync<ShareFetchRequest, ShareFetchResponse>(
                new ShareFetchRequest
                {
                    GroupId = "group",
                    MemberId = "member",
                    Topics = []
                },
                2,
                token),
            serverSocket,
            cancellationToken);
        await AssertBlockedBeforeWriteAsync(
            async token => _ = await connection.SendAsync<CreateTopicsRequest, CreateTopicsResponse>(
                new CreateTopicsRequest { Topics = [] }, 7, token),
            serverSocket,
            cancellationToken);
        await AssertBlockedBeforeWriteAsync(
            async token => _ = await connection.SendAsync<MetadataRequest, MetadataResponse>(
                MetadataRequest.ForAllTopics(), 13, token),
            serverSocket,
            cancellationToken);
        await AssertBlockedBeforeWriteAsync(
            async token => _ = await connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                new FindCoordinatorRequest { Key = "group" }, 5, token),
            serverSocket,
            cancellationToken);

        releaseServer.TrySetResult();
        await serverTask;
    }

    [Test]
    public async Task DifferentBrokerState_IsNotPaused()
    {
        var throttledBroker = new BrokerThrottleState();
        var unrelatedBroker = new BrokerThrottleState();
        throttledBroker.Observe(10_000);

        var unrelatedWait = unrelatedBroker.WaitAsync(
            CancellationToken.None,
            CancellationToken.None);

        await Assert.That(unrelatedWait.IsCompletedSuccessfully).IsTrue();
        await unrelatedWait;
    }

    private static ApiVersionsRequest CreateApiVersionsRequest() => new()
    {
        ClientSoftwareName = "dekaf-tests",
        ClientSoftwareVersion = "1.0"
    };

    private static KafkaConnection CreateConnection(int port, BrokerThrottleState throttleState) =>
        new(
            brokerId: 1,
            host: "127.0.0.1",
            port,
            clientId: null,
            options: null,
            logger: null,
            responseBufferPool: ResponseBufferPool.Default,
            sharedPipeMemoryPool: null,
            telemetryMetricCollector: null,
            responseMemoryAdmissionsEnabled: false,
            brokerThrottleState: throttleState);

    private static async Task AssertBlockedBeforeWriteAsync(
        Func<CancellationToken, Task> send,
        Socket serverSocket,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(25);

        await Assert.That(async () => await send(cts.Token)).Throws<OperationCanceledException>();
        await Task.Yield();
        await Assert.That(serverSocket.Available).IsEqualTo(0);
    }

    private static async Task CompleteApiVersionsRequestAsync(
        NetworkStream stream,
        int throttleTimeMs,
        CancellationToken cancellationToken)
    {
        var request = await ReadFrameAsync(stream, cancellationToken);
        await WriteApiVersionsResponseAsync(stream, request, throttleTimeMs, cancellationToken);
    }

    private static async Task WriteApiVersionsResponseAsync(
        NetworkStream stream,
        byte[] request,
        int throttleTimeMs,
        CancellationToken cancellationToken)
    {
        await Assert.That(BinaryPrimitives.ReadInt16BigEndian(request)).IsEqualTo((short)ApiKey.ApiVersions);
        var correlationId = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4));
        await stream.WriteAsync(BuildApiVersionsResponse(correlationId, throttleTimeMs), cancellationToken);
    }

    private static async Task<byte[]> ReadFrameAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var length = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(length, cancellationToken);
        var frame = new byte[BinaryPrimitives.ReadInt32BigEndian(length)];
        await stream.ReadExactlyAsync(frame, cancellationToken);
        return frame;
    }

    private static byte[] BuildApiVersionsResponse(int correlationId, int throttleTimeMs)
    {
        var body = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(body);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteUnsignedVarInt(2);
        writer.WriteInt16((short)ApiKey.ApiVersions);
        writer.WriteInt16(3);
        writer.WriteInt16(3);
        writer.WriteEmptyTaggedFields();
        writer.WriteInt32(throttleTimeMs);
        writer.WriteEmptyTaggedFields();

        var frame = new byte[sizeof(int) + sizeof(int) + body.WrittenCount];
        BinaryPrimitives.WriteInt32BigEndian(frame, frame.Length - sizeof(int));
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(sizeof(int)), correlationId);
        body.WrittenSpan.CopyTo(frame.AsSpan(sizeof(int) * 2));
        return frame;
    }
}
