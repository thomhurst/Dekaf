using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Dekaf.Networking;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Networking;

public class KafkaConnectionCapabilityHandshakeTests
{
    [Test]
    [Timeout(5_000)]
    public async Task ConnectAsync_NegotiatesCapabilitiesBeforeConnectionIsReady(
        CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var requestReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseResponse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseServer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var serverTask = Task.Run(async () =>
        {
            using var socket = await listener.AcceptSocketAsync(cancellationToken);
            await using var stream = new NetworkStream(socket, ownsSocket: false);
            var request = await ReadFrameAsync(stream, cancellationToken);

            await Assert.That(BinaryPrimitives.ReadInt16BigEndian(request)).IsEqualTo((short)ApiKey.ApiVersions);
            await Assert.That(BinaryPrimitives.ReadInt16BigEndian(request.AsSpan(2))).IsEqualTo((short)3);

            var correlationId = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4));
            requestReceived.SetResult();
            await releaseResponse.Task.WaitAsync(cancellationToken);
            var response = BuildResponse(
                correlationId,
                ErrorCode.None,
                new ApiVersion(ApiKey.Metadata, 9, 12),
                new ApiVersion(ApiKey.Produce, 3, 7));
            await stream.WriteAsync(response, cancellationToken);
            await releaseServer.Task.WaitAsync(cancellationToken);
        }, cancellationToken);

        await using var connection = new KafkaConnection(1, "127.0.0.1", port);
        var connectTask = connection.ConnectAsync(cancellationToken);
        await requestReceived.Task.WaitAsync(cancellationToken);

        await Assert.That(connection.IsConnected).IsFalse();
        await Assert.That(() => ((IKafkaCapabilityProvider)connection).Capabilities)
            .Throws<InvalidOperationException>();

        releaseResponse.SetResult();
        await connectTask;

        var capabilities = ((IKafkaCapabilityProvider)connection).Capabilities;
        await Assert.That(connection.IsConnected).IsTrue();
        await Assert.That(capabilities.NegotiateVersion(ApiKey.Metadata, 9, 13)).IsEqualTo((short)12);
        await Assert.That(capabilities.NegotiateVersion(ApiKey.Produce, 3, 13)).IsEqualTo((short)7);
        releaseServer.SetResult();
        await serverTask;
    }

    [Test]
    [Timeout(5_000)]
    public async Task ConnectAsync_WhenNegotiationFails_DoesNotPublishReadyConnection(
        CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            using var socket = await listener.AcceptSocketAsync(cancellationToken);
            await using var stream = new NetworkStream(socket, ownsSocket: false);
            var request = await ReadFrameAsync(stream, cancellationToken);
            var correlationId = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4));
            await stream.WriteAsync(
                BuildResponse(correlationId, ErrorCode.InvalidRequest),
                cancellationToken);
        }, cancellationToken);

        await using var connection = new KafkaConnection(1, "127.0.0.1", port);

        await Assert.That(async () => await connection.ConnectAsync(cancellationToken))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("ApiVersions failed");
        await serverTask;
        await Assert.That(connection.IsConnected).IsFalse();
    }

    [Test]
    [Timeout(5_000)]
    public async Task ConnectionPool_ReconnectUsesOnlyNewCapabilityGeneration(
        CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var releaseFirstGeneration = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSecondGeneration = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var serverTask = Task.Run(async () =>
        {
            await ServeGenerationAsync(
                listener,
                releaseFirstGeneration.Task,
                cancellationToken,
                new ApiVersion(ApiKey.Metadata, 9, 12),
                new ApiVersion(ApiKey.Produce, 3, 7));
            await ServeGenerationAsync(
                listener,
                releaseSecondGeneration.Task,
                cancellationToken,
                new ApiVersion(ApiKey.Metadata, 9, 13));
        }, cancellationToken);

        await using var pool = new ConnectionPool(connectionOptions: new ConnectionOptions
        {
            ReconnectBackoff = TimeSpan.Zero,
            ReconnectBackoffMax = TimeSpan.Zero
        });

        var firstConnection = await pool.GetConnectionAsync("127.0.0.1", port, cancellationToken);
        var firstCapabilities = ((IKafkaCapabilityProvider)firstConnection).Capabilities;
        await Assert.That(firstCapabilities.NegotiateVersion(ApiKey.Produce, 3, 13)).IsEqualTo((short)7);

        releaseFirstGeneration.SetResult();
        await WaitUntilAsync(() => !firstConnection.IsConnected, cancellationToken);

        var secondConnection = await pool.GetConnectionAsync("127.0.0.1", port, cancellationToken);
        var secondCapabilities = ((IKafkaCapabilityProvider)secondConnection).Capabilities;

        await Assert.That(secondConnection).IsNotSameReferenceAs(firstConnection);
        await Assert.That(secondCapabilities.NegotiateVersion(ApiKey.Metadata, 9, 13)).IsEqualTo((short)13);
        await Assert.That(() => secondCapabilities.NegotiateVersion(ApiKey.Produce, 3, 13))
            .Throws<BrokerVersionException>();

        Parallel.For(0, 10_000, _ =>
        {
            if (firstCapabilities.NegotiateVersion(ApiKey.Produce, 3, 13) != 7 ||
                secondCapabilities.HasApi(ApiKey.Produce))
            {
                throw new InvalidOperationException("Capability generations were mixed.");
            }
        });

        releaseSecondGeneration.SetResult();
        await serverTask;
    }

    [Test]
    [Timeout(5_000)]
    public async Task ConnectionPool_NegotiationFailureRetiresOnlyFailedConnection(
        CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var releaseSuccessfulConnection = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var serverTask = Task.Run(async () =>
        {
            using (var failedSocket = await listener.AcceptSocketAsync(cancellationToken))
            await using (var failedStream = new NetworkStream(failedSocket, ownsSocket: false))
            {
                var request = await ReadFrameAsync(failedStream, cancellationToken);
                var correlationId = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4));
                await failedStream.WriteAsync(
                    BuildResponse(correlationId, ErrorCode.InvalidRequest),
                    cancellationToken);
                var successfulGenerationTask = ServeGenerationAsync(
                    listener,
                    releaseSuccessfulConnection.Task,
                    cancellationToken,
                    new ApiVersion(ApiKey.Metadata, 9, 13));
                await successfulGenerationTask;
            }
        }, cancellationToken);

        await using var pool = new ConnectionPool(connectionOptions: new ConnectionOptions
        {
            ReconnectBackoff = TimeSpan.Zero,
            ReconnectBackoffMax = TimeSpan.Zero
        });

        await Assert.That(async () => await pool.GetConnectionAsync("127.0.0.1", port, cancellationToken))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("ApiVersions failed");

        var connection = await pool.GetConnectionAsync("127.0.0.1", port, cancellationToken);
        await Assert.That(connection.IsConnected).IsTrue();
        await Assert.That(((IKafkaCapabilityProvider)connection).Capabilities.HasApi(ApiKey.Metadata)).IsTrue();

        releaseSuccessfulConnection.SetResult();
        await serverTask;
    }

    private static async Task ServeGenerationAsync(
        TcpListener listener,
        Task releaseConnection,
        CancellationToken cancellationToken,
        params ApiVersion[] versions)
    {
        using var socket = await listener.AcceptSocketAsync(cancellationToken);
        await using var stream = new NetworkStream(socket, ownsSocket: false);
        var request = await ReadFrameAsync(stream, cancellationToken);
        var correlationId = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4));
        await stream.WriteAsync(BuildResponse(correlationId, ErrorCode.None, versions), cancellationToken);
        await releaseConnection.WaitAsync(cancellationToken);
    }

    private static async Task WaitUntilAsync(
        Func<bool> predicate,
        CancellationToken cancellationToken)
    {
        while (!predicate())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }
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

    private static byte[] BuildResponse(
        int correlationId,
        ErrorCode errorCode,
        params ApiVersion[] versions)
    {
        var body = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(body);
        writer.WriteInt16((short)errorCode);
        writer.WriteUnsignedVarInt(versions.Length + 1);
        foreach (var version in versions)
        {
            writer.WriteInt16((short)version.ApiKey);
            writer.WriteInt16(version.MinVersion);
            writer.WriteInt16(version.MaxVersion);
            writer.WriteEmptyTaggedFields();
        }

        writer.WriteInt32(0);
        writer.WriteEmptyTaggedFields();

        var frame = new byte[sizeof(int) + sizeof(int) + body.WrittenCount];
        BinaryPrimitives.WriteInt32BigEndian(frame, frame.Length - sizeof(int));
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(sizeof(int)), correlationId);
        body.WrittenSpan.CopyTo(frame.AsSpan(sizeof(int) * 2));
        return frame;
    }
}
