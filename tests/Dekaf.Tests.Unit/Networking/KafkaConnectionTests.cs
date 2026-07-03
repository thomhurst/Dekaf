using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Dekaf.Networking;
using Dekaf.Protocol.Messages;

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
}
