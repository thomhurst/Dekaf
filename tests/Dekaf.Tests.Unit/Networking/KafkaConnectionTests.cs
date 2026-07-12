using System.Buffers.Binary;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Security;

namespace Dekaf.Tests.Unit.Networking;

/// <summary>
/// Tests for KafkaConnection, particularly the IsConnected fix
/// that ensures disposed connections report as disconnected.
/// </summary>
public sealed class KafkaConnectionTests
{
    [Test]
    public async Task IsConnected_BeforeConnect_ReturnsFalse()
    {
        var connection = new KafkaConnection("localhost", 9092);

        // Before ConnectAsync is called, there is no socket
        var isConnected = connection.IsConnected;

        await Assert.That(isConnected).IsFalse();
    }

    [Test]
    public async Task IsConnected_AfterDispose_ReturnsFalse()
    {
        var connection = new KafkaConnection("localhost", 9092);
        await connection.DisposeAsync();

        // After disposal, IsConnected must return false even if socket state is ambiguous
        var isConnected = connection.IsConnected;

        await Assert.That(isConnected).IsFalse();
    }

    [Test]
    public async Task IsConnected_DisposeWithoutConnect_ReturnsFalse()
    {
        // Connection created but never connected, then disposed
        var connection = new KafkaConnection(1, "localhost", 9092);
        await connection.DisposeAsync();

        var isConnected = connection.IsConnected;

        await Assert.That(isConnected).IsFalse();
    }

    [Test]
    public async Task DisposeAsync_CalledMultipleTimes_DoesNotThrow()
    {
        var connection = new KafkaConnection("localhost", 9092);

        await connection.DisposeAsync();
        await connection.DisposeAsync();
        await connection.DisposeAsync();
    }

    [Test]
    public async Task BrokerId_DefaultConstructor_IsNegativeOne()
    {
        var connection = new KafkaConnection("localhost", 9092);

        var brokerId = connection.BrokerId;

        await Assert.That(brokerId).IsEqualTo(-1);
    }

    [Test]
    public async Task BrokerId_WithExplicitId_ReturnsSetValue()
    {
        var connection = new KafkaConnection(42, "localhost", 9092);

        var brokerId = connection.BrokerId;

        await Assert.That(brokerId).IsEqualTo(42);
    }

    [Test]
    public async Task GetPreSerializeInitialCapacity_WithoutHint_ReturnsDefault()
    {
        var request = new ApiVersionsRequest { ClientSoftwareName = "test", ClientSoftwareVersion = "1.0" };

        var capacity = KafkaConnection.GetPreSerializeInitialCapacity<ApiVersionsResponse>(request);

        await Assert.That(capacity).IsEqualTo(KafkaConnection.DefaultPreSerializeInitialCapacity);
    }

    [Test]
    public async Task GetPreSerializeInitialCapacity_WithProduceHint_AddsHeaderSlack()
    {
        var request = new ProduceRequest
        {
            RequestBodySizeHint = 1_048_576
        };

        var capacity = KafkaConnection.GetPreSerializeInitialCapacity<ProduceResponse>(request);

        await Assert.That(capacity).IsEqualTo(1_048_704);
    }

    [Test]
    public async Task ParseFetchResponse_WhenParsingThrows_ReturnsPooledBuffer()
    {
        var pool = new ResponseBufferPool(1024);
        var array = pool.Pool.Rent(16);
        Array.Clear(array);
        var buffer = new PooledResponseBuffer(array, 1, isPooled: true, pool: pool);

        await Assert.That(() => KafkaConnection.ParseFetchResponse<FetchRequest, FetchResponse>(buffer, 16))
            .ThrowsException();

        var returned = pool.Pool.Rent(16);
        await Assert.That(returned).IsSameReferenceAs(array);
        pool.Pool.Return(returned);
    }

    [Test]
    public async Task Host_ReturnsConstructorValue()
    {
        var connection = new KafkaConnection("my-broker.example.com", 9092);

        var host = connection.Host;

        await Assert.That(host).IsEqualTo("my-broker.example.com");
    }

    [Test]
    public async Task Port_ReturnsConstructorValue()
    {
        var connection = new KafkaConnection("localhost", 19092);

        var port = connection.Port;

        await Assert.That(port).IsEqualTo(19092);
    }

    [Test]
    public async Task SendAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var connection = new KafkaConnection("localhost", 9092);
        await connection.DisposeAsync();

        Func<Task> act = () => connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
            new ApiVersionsRequest { ClientSoftwareName = "test", ClientSoftwareVersion = "1.0" },
            apiVersion: 3).AsTask();

        await Assert.That(act).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task SendAsync_AfterRetirementSealed_ThrowsObjectDisposedException()
    {
        await using var connection = new KafkaConnection("localhost", 9092);
        var retirableConnection = (IRetirableKafkaConnection)connection;

        await Assert.That(retirableConnection.TryAcquireLease()).IsTrue();
        retirableConnection.BeginRetirement();
        await Assert.That(retirableConnection.TryAcquireLease()).IsFalse();
        retirableConnection.ReleaseLease();
        retirableConnection.CompleteRetirement();

        Func<Task> act = () => connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
            new ApiVersionsRequest { ClientSoftwareName = "test", ClientSoftwareVersion = "1.0" },
            apiVersion: 3).AsTask();

        await Assert.That(act).Throws<ObjectDisposedException>();
        await Assert.That(retirableConnection.ActiveOperationCount).IsEqualTo(0);
    }

    [Test]
    public async Task ConnectAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var connection = new KafkaConnection("localhost", 9092);
        await connection.DisposeAsync();

        Func<Task> act = () => connection.ConnectAsync().AsTask();

        await Assert.That(act).Throws<ObjectDisposedException>();
    }

    [Test]
    [Timeout(10_000)]
    public async Task SendPipelinedWithCallerTimeoutAsync_ResponseCancellation_ThrowsTimeoutException(CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var acceptTask = listener.AcceptTcpClientAsync(cancellationToken);
            await using var connection = new KafkaConnection(
                IPAddress.Loopback.ToString(),
                port,
                options: new ConnectionOptions { RequestTimeout = TimeSpan.FromSeconds(30) });

            await connection.ConnectAsync(cancellationToken);
            using var serverClient = await acceptTask.ConfigureAwait(false);
            using var callerTimeout = new CancellationTokenSource();

            var sendTask = connection.SendPipelinedWithCallerTimeoutAsync<ApiVersionsRequest, ApiVersionsResponse>(
                new ApiVersionsRequest { ClientSoftwareName = "test", ClientSoftwareVersion = "1.0" },
                apiVersion: 3,
                callerTimeout.Token);

            await ReadRequestFrameAsync(serverClient.GetStream(), cancellationToken);
            await callerTimeout.CancelAsync();

            var act = async () => await sendTask;
            await Assert.That(act).Throws<TimeoutException>();
        }
        finally
        {
            listener.Stop();
        }
    }

    [Test]
    [Timeout(10_000)]
    public async Task SendAsync_ResponseCancellation_PreservesCallerCancellationToken(CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var acceptTask = listener.AcceptTcpClientAsync(cancellationToken);
            await using var connection = new KafkaConnection(
                IPAddress.Loopback.ToString(),
                port,
                options: new ConnectionOptions { RequestTimeout = TimeSpan.FromSeconds(30) });

            await connection.ConnectAsync(cancellationToken);
            using var serverClient = await acceptTask.ConfigureAwait(false);
            using var callerCancellation = new CancellationTokenSource();

            var sendTask = connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                new ApiVersionsRequest { ClientSoftwareName = "test", ClientSoftwareVersion = "1.0" },
                apiVersion: 3,
                callerCancellation.Token).AsTask();

            await ReadRequestFrameAsync(serverClient.GetStream(), cancellationToken);
            await callerCancellation.CancelAsync();

            var thrown = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await sendTask.ConfigureAwait(false);
            });
            await Assert.That(thrown!.CancellationToken).IsEqualTo(callerCancellation.Token);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Test]
    [Timeout(10_000)]
    public async Task ReceiveLoop_IdleLongerThanRequestTimeout_RemainsConnected(CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var acceptTask = listener.AcceptTcpClientAsync(cancellationToken);
            await using var connection = new KafkaConnection(
                IPAddress.Loopback.ToString(),
                port,
                options: new ConnectionOptions { RequestTimeout = TimeSpan.FromSeconds(1) });

            await connection.ConnectAsync(cancellationToken);
            using var serverClient = await acceptTask.ConfigureAwait(false);

            await Task.Delay(TimeSpan.FromMilliseconds(1500), cancellationToken);

            await Assert.That(connection.IsConnected).IsTrue();
        }
        finally
        {
            listener.Stop();
        }
    }

    [Test]
    [Timeout(10_000)]
    public async Task SendAsync_CallerCanceledRequest_DoesNotFailIdleConnectionAfterRequestTimeout(CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var acceptTask = listener.AcceptTcpClientAsync(cancellationToken);
            await using var connection = new KafkaConnection(
                IPAddress.Loopback.ToString(),
                port,
                options: new ConnectionOptions { RequestTimeout = TimeSpan.FromSeconds(30) });

            await connection.ConnectAsync(cancellationToken);
            using var serverClient = await acceptTask.ConfigureAwait(false);
            using var callerCancellation = new CancellationTokenSource();

            var sendTask = connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                new ApiVersionsRequest { ClientSoftwareName = "test", ClientSoftwareVersion = "1.0" },
                apiVersion: 3,
                callerCancellation.Token).AsTask();

            await ReadRequestFrameAsync(serverClient.GetStream(), cancellationToken);
            await callerCancellation.CancelAsync();

            var thrown = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await sendTask.ConfigureAwait(false);
            });
            await Assert.That(thrown!.CancellationToken).IsEqualTo(callerCancellation.Token);

            // Completion of the cancelled send synchronously removes its pending request and
            // disarms the receive timeout. Invoke a late timer callback directly instead of
            // racing caller cancellation against a one-second wall-clock timeout.
            await Assert.That(GetPrivateField<int>(connection, "_pendingRequestCount")).IsEqualTo(0);
            await Assert.That(GetPrivateField<long>(connection, "_receiveTimeoutDeadlineTimestamp")).IsEqualTo(0);
            InvokeReceiveTimeout(connection);

            await Assert.That(GetPrivateField<int>(connection, "_receiveTimeoutExpired")).IsEqualTo(0);
            await Assert.That(connection.IsConnected).IsTrue();
            await connection.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Test]
    [Timeout(10_000)]
    public async Task IsConnected_StreamAssignedBeforeConnectionReady_ReturnsFalse(CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            var acceptTask = listener.AcceptSocketAsync(cancellationToken);
            await clientSocket.ConnectAsync(IPAddress.Loopback, port, cancellationToken);
            using var serverSocket = await acceptTask.ConfigureAwait(false);
            await using var stream = new NetworkStream(clientSocket, ownsSocket: false);
            await using var connection = new KafkaConnection(IPAddress.Loopback.ToString(), port);

            SetPrivateField(connection, "_socket", clientSocket);
            SetPrivateField(connection, "_stream", stream);

            await Assert.That(connection.IsConnected).IsFalse();
        }
        finally
        {
            listener.Stop();
        }
    }

    [Test]
    [Timeout(10_000)]
    public async Task ConnectAsync_AfterPipeSetup_IsConnectedTrue(CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var acceptTask = listener.AcceptTcpClientAsync(cancellationToken);
            await using var connection = new KafkaConnection(IPAddress.Loopback.ToString(), port);

            await connection.ConnectAsync(cancellationToken);
            using var serverClient = await acceptTask.ConfigureAwait(false);

            await Assert.That(connection.IsConnected).IsTrue();
        }
        finally
        {
            listener.Stop();
        }
    }

    [Test]
    [Timeout(10_000)]
    public async Task ConnectAsync_ConfiguresTcpKeepAlive(CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var acceptTask = listener.AcceptTcpClientAsync(cancellationToken);
            await using var connection = new KafkaConnection(
                IPAddress.Loopback.ToString(),
                port,
                options: new ConnectionOptions
                {
                    TcpKeepAliveTime = TimeSpan.FromSeconds(10),
                    TcpKeepAliveInterval = TimeSpan.FromSeconds(2),
                    TcpKeepAliveRetryCount = 2
                });

            await connection.ConnectAsync(cancellationToken);
            using var serverClient = await acceptTask.ConfigureAwait(false);

            var socket = GetPrivateField<Socket>(connection, "_socket");

            await Assert.That(IsKeepAliveEnabled(socket)).IsTrue();
        }
        finally
        {
            listener.Stop();
        }
    }

    [Test]
    [Timeout(10_000)]
    public async Task SendFireAndForgetAsync_WritesFrameToStream(CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var acceptTask = listener.AcceptTcpClientAsync(cancellationToken);
            await using var connection = new KafkaConnection(IPAddress.Loopback.ToString(), port);

            await connection.ConnectAsync(cancellationToken);
            using var serverClient = await acceptTask.ConfigureAwait(false);

            await connection.SendFireAndForgetAsync<ApiVersionsRequest, ApiVersionsResponse>(
                new ApiVersionsRequest { ClientSoftwareName = "test", ClientSoftwareVersion = "1.0" },
                apiVersion: 3,
                cancellationToken);

            var frame = await ReadRequestFrameAsync(serverClient.GetStream(), cancellationToken);
            var correlationId = BinaryPrimitives.ReadInt32BigEndian(frame.AsSpan(4, 4));

            await Assert.That(frame.Length).IsGreaterThan(8);
            await Assert.That(correlationId).IsGreaterThan(0);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Test]
    [Arguments(-1)]
    [Arguments(1_048_577)]
    public async Task SendSaslMessageAsync_InvalidResponseFrameSize_ThrowsKafkaExceptionBeforeRent(int responseSize)
    {
        await using var connection = new KafkaConnection("localhost", 9092);
        await using var stream = new SaslResponseStream(responseSize);
        SetPrivateField(connection, "_stream", stream);

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
        {
            await InvokeSaslHandshakeAsync(connection, CancellationToken.None).ConfigureAwait(false);
        });

        await Assert.That(exception!.Message).Contains($"Invalid response frame size {responseSize}");
        await Assert.That(stream.BytesWritten).IsGreaterThan(0);
        await Assert.That(stream.ReadCalls).IsEqualTo(1);
    }

    [Test]
    public async Task WriteFailure_DisposeAsyncRunsFullStreamCleanup()
    {
        await using var connection = new KafkaConnection("localhost", 9092);
        var stream = new ThrowingWriteStream();
        SetPrivateField(connection, "_stream", stream);

        var (writeTask, writeLock) = await StartFrameWriteAsync(connection);

        await Assert.That(async () => await writeTask)
            .Throws<IOException>()
            .WithMessageContaining("write failed");

        // A faulted frame write may have left partial bytes on the wire: the connection
        // must abort itself and release the write lock.
        await Assert.That(GetPrivateField<int>(connection, "_disposed")).IsNotEqualTo(0);
        await Assert.That(writeLock.CurrentCount).IsEqualTo(1);

        await connection.DisposeAsync();

        await Assert.That(stream.DisposeCount).IsEqualTo(1);
    }

    [Test]
    public async Task WriteTimeout_AbandonedWriteCompletes_ConnectionStaysUsable()
    {
        await using var connection = new KafkaConnection("localhost", 9092);
        var stream = new PendingWriteStream();
        SetPrivateField(connection, "_stream", stream);

        var (writeTask, writeLock) = await StartFrameWriteAsync(connection);
        await Assert.That(writeTask.IsCompleted).IsFalse();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // The caller's timeout fires while the frame is in flight: the wait is abandoned,
        // but the socket write itself must never be cancelled mid-frame.
        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await InvokeAwaitFrameWriteAsync(connection, writeTask, callerOwnsTimeout: true, cts.Token));
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.RequestTimedOut);
        await Assert.That(exception.Message).Contains("Flush timeout");

        await Assert.That(writeTask.IsCompleted).IsFalse();
        await Assert.That(GetPrivateField<int>(connection, "_disposed")).IsEqualTo(0);

        // The stalled write drains after the caller gave up: the frame is intact, so the
        // connection stays frame-aligned and usable instead of being torn down.
        stream.CompletePendingWrite();
        await writeTask;

        await Assert.That(GetPrivateField<int>(connection, "_disposed")).IsEqualTo(0);
        await Assert.That(writeLock.CurrentCount).IsEqualTo(1);
    }

    [Test]
    [Timeout(5_000)]
    public async Task WriteTimeout_AbandonedWriteStuckPastGracePeriod_AbortsConnectionAndReleasesWriteLock(
        CancellationToken cancellationToken)
    {
        var graceWaitStarted = new TaskCompletionSource();
        var expireGracePeriod = new TaskCompletionSource();
        await using var connection = new KafkaConnection(
            "localhost",
            9092,
            clientId: null,
            options: new ConnectionOptions { RequestTimeout = TimeSpan.FromSeconds(30) },
            logger: null,
            responseBufferPool: ResponseBufferPool.Default,
            waitForAbandonedWriteAsync: (_, _) =>
            {
                graceWaitStarted.TrySetResult();
                return expireGracePeriod.Task;
            });
        var stream = new PendingWriteStream();
        SetPrivateField(connection, "_stream", stream);

        var (writeTask, writeLock) = await StartFrameWriteAsync(connection);
        await Assert.That(writeTask.IsCompleted).IsFalse();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.That(async () =>
                await InvokeAwaitFrameWriteAsync(connection, writeTask, callerOwnsTimeout: true, cts.Token))
            .Throws<KafkaException>()
            .WithMessageContaining("Flush timeout");

        await graceWaitStarted.Task.WaitAsync(cancellationToken);
        expireGracePeriod.SetException(new TimeoutException("controlled grace-period expiry"));
        await stream.Disposed.WaitAsync(cancellationToken);
        await Assert.That(async () => await writeTask)
            .Throws<IOException>()
            .WithMessageContaining("write aborted");

        await Assert.That(GetPrivateField<int>(connection, "_disposed")).IsNotEqualTo(0);
        await Assert.That(stream.DisposeCount).IsEqualTo(1);
        await Assert.That(writeTask.IsFaulted).IsTrue();
        await Assert.That(writeLock.CurrentCount).IsEqualTo(1);
    }

    [Test]
    public async Task BuildRemoteCertificateValidationCallback_WhenHostNameValidationDisabled_IgnoresNameMismatchOnly()
    {
        await using var connection = new KafkaConnection(
            "localhost",
            9092,
            options: new ConnectionOptions
            {
                TlsConfig = new TlsConfig
                {
                    ValidateServerCertificateHostName = false
                }
            });

        var callback = InvokeBuildRemoteCertificateValidationCallback(connection);

        await Assert.That(callback).IsNotNull();
        await Assert.That(callback!(connection, null, null, SslPolicyErrors.RemoteCertificateNameMismatch)).IsTrue();
        await Assert.That(callback(
                connection,
                null,
                null,
                SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors))
            .IsFalse();
    }

    [Test]
    public async Task BuildRemoteCertificateValidationCallback_WhenHostNameValidationEnabledWithoutCustomPolicy_ReturnsNull()
    {
        await using var connection = new KafkaConnection(
            "localhost",
            9092,
            options: new ConnectionOptions { TlsConfig = new TlsConfig() });

        var callback = InvokeBuildRemoteCertificateValidationCallback(connection);

        await Assert.That(callback).IsNull();
    }

    [Test]
    public async Task ApplyServerCertificateHostNamePolicy_WhenEnabled_PreservesNameMismatch()
    {
        const SslPolicyErrors errors =
            SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors;

        var result = KafkaConnection.ApplyServerCertificateHostNamePolicy(
            errors,
            validateServerCertificateHostName: true);

        await Assert.That(result).IsEqualTo(errors);
    }

    [Test]
    public async Task ApplyServerCertificateHostNamePolicy_WhenDisabled_RemovesOnlyNameMismatch()
    {
        const SslPolicyErrors errors =
            SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors;

        var result = KafkaConnection.ApplyServerCertificateHostNamePolicy(
            errors,
            validateServerCertificateHostName: false);

        await Assert.That(result).IsEqualTo(SslPolicyErrors.RemoteCertificateChainErrors);
    }

    [Test]
    public async Task ApplyServerCertificateHostNamePolicy_WhenDisabled_AcceptsNameMismatchOnly()
    {
        var result = KafkaConnection.ApplyServerCertificateHostNamePolicy(
            SslPolicyErrors.RemoteCertificateNameMismatch,
            validateServerCertificateHostName: false);

        await Assert.That(result).IsEqualTo(SslPolicyErrors.None);
    }

    private static async Task<byte[]> ReadRequestFrameAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[4];
        await stream.ReadExactlyAsync(lengthBuffer, cancellationToken).ConfigureAwait(false);

        var frameLength = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);
        var frameBuffer = new byte[frameLength];
        await stream.ReadExactlyAsync(frameBuffer, cancellationToken).ConfigureAwait(false);
        return frameBuffer;
    }

    [Test]
    public async Task DisposeAsync_ConcurrentCalls_DoesNotThrow()
    {
        var connection = new KafkaConnection("localhost", 9092);

        // Launch many concurrent disposal calls to exercise the atomic guard
        var threadCount = Math.Max(2, Environment.ProcessorCount);
        var tasks = new Task[threadCount];
        using var barrier = new Barrier(tasks.Length);

        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                barrier.SignalAndWait();
                await connection.DisposeAsync();
            });
        }

        // All concurrent disposals should complete without throwing
        await Task.WhenAll(tasks);
    }

    private static void SetPrivateField<T>(KafkaConnection connection, string name, T value)
    {
        var field = typeof(KafkaConnection).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field is null)
            throw new InvalidOperationException($"Field '{name}' was not found.");

        field.SetValue(connection, value);
    }

    private static async ValueTask InvokeSaslHandshakeAsync(KafkaConnection connection, CancellationToken cancellationToken)
    {
        var method = typeof(KafkaConnection).GetMethod(
            "SendSaslMessageAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (method is null)
            throw new InvalidOperationException("SendSaslMessageAsync was not found.");

        var genericMethod = method.MakeGenericMethod(typeof(SaslHandshakeRequest), typeof(SaslHandshakeResponse));
        var result = genericMethod.Invoke(connection,
        [
            new SaslHandshakeRequest { Mechanism = "PLAIN" },
            (short)1,
            cancellationToken
        ]);

        await ((ValueTask<SaslHandshakeResponse>)result!).ConfigureAwait(false);
    }

    private static async Task<(Task WriteTask, SemaphoreSlim WriteLock)> StartFrameWriteAsync(KafkaConnection connection)
    {
        // WriteFrameHoldingLockAsync expects the caller to have acquired the write lock;
        // it releases the lock and returns the pooled buffer itself.
        var writeLock = GetPrivateField<SemaphoreSlim>(connection, "_writeLock");
        await writeLock.WaitAsync();

        var buffer = DekafPools.SerializationBuffers.Rent(16);
        var method = typeof(KafkaConnection).GetMethod(
            "WriteFrameHoldingLockAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (method is null)
            throw new InvalidOperationException("WriteFrameHoldingLockAsync was not found.");

        var writeTask = (Task)method.Invoke(connection, [buffer, 4, false])!;
        return (writeTask, writeLock);
    }

    private static async ValueTask InvokeAwaitFrameWriteAsync(
        KafkaConnection connection,
        Task writeTask,
        bool callerOwnsTimeout,
        CancellationToken cancellationToken)
    {
        var method = typeof(KafkaConnection).GetMethod(
            "AwaitFrameWriteAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (method is null)
            throw new InvalidOperationException("AwaitFrameWriteAsync was not found.");

        var result = method.Invoke(connection, [writeTask, 1, callerOwnsTimeout, cancellationToken]);
        await ((ValueTask)result!).ConfigureAwait(false);
    }

    private static RemoteCertificateValidationCallback? InvokeBuildRemoteCertificateValidationCallback(
        KafkaConnection connection)
    {
        var method = typeof(KafkaConnection).GetMethod(
            "BuildRemoteCertificateValidationCallback",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (method is null)
            throw new InvalidOperationException("BuildRemoteCertificateValidationCallback was not found.");

        return (RemoteCertificateValidationCallback?)method.Invoke(connection, null);
    }

    private static void InvokeReceiveTimeout(KafkaConnection connection)
    {
        var method = typeof(KafkaConnection).GetMethod(
            "OnReceiveTimeout",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (method is null)
            throw new InvalidOperationException("OnReceiveTimeout was not found.");

        method.Invoke(connection, null);
    }

    private static T GetPrivateField<T>(KafkaConnection connection, string name)
    {
        var field = typeof(KafkaConnection).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field is null)
            throw new InvalidOperationException($"Field '{name}' was not found.");

        return (T)field.GetValue(connection)!;
    }

    private static bool IsKeepAliveEnabled(Socket socket)
    {
        var option = socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive);
        return option switch
        {
            int value => value != 0,
            bool value => value,
            byte[] bytes when bytes.Length >= sizeof(int) => BitConverter.ToInt32(bytes) != 0,
            byte[] bytes when bytes.Length > 0 => bytes[0] != 0,
            _ => false
        };
    }

    private sealed class SaslResponseStream(int responseSize) : Stream
    {
        private readonly MemoryStream _response = CreateSizePrefixStream(responseSize);

        public int BytesWritten { get; private set; }
        public int ReadCalls { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _response.Length;

        public override long Position
        {
            get => _response.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;

        public override int Read(byte[] buffer, int offset, int count)
        {
            ReadCalls++;
            return _response.Read(buffer, offset, count);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadCalls++;
            return _response.ReadAsync(buffer, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            BytesWritten += count;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            BytesWritten += buffer.Length;
            return ValueTask.CompletedTask;
        }

        private static MemoryStream CreateSizePrefixStream(int responseSize)
        {
            var buffer = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(buffer, responseSize);
            return new MemoryStream(buffer);
        }
    }

    private sealed class PendingWriteStream : Stream
    {
        private readonly TaskCompletionSource _pendingWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _disposeCount;

        public int DisposeCount => Volatile.Read(ref _disposeCount);
        public Task Disposed => _disposed.Task;

        public void CompletePendingWrite() => _pendingWrite.TrySetResult();

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
            => _pendingWrite.Task;

        // Deliberately ignores cancellationToken: models an in-flight socket send stalled
        // on a full TCP window, which the connection must never cancel mid-frame.
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => new(_pendingWrite.Task);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Interlocked.Increment(ref _disposeCount);
                _pendingWrite.TrySetException(new IOException("write aborted"));
                _disposed.TrySetResult();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class ThrowingWriteStream : Stream
    {
        public int DisposeCount { get; private set; }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new IOException("write failed");

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => ValueTask.FromException(new IOException("write failed"));

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DisposeCount++;

            base.Dispose(disposing);
        }
    }
}
