using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Dekaf.Metadata;
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
            await Assert.That(BinaryPrimitives.ReadInt16BigEndian(request.AsSpan(2))).IsEqualTo((short)5);

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
    [Timeout(15_000)]
    public async Task DisposeDuringCapabilityHandshake_NeverPublishesReadyConnection(
        CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var requestReceived = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var serverTask = Task.Run(async () =>
        {
            using var socket = await listener.AcceptSocketAsync(cancellationToken);
            await using var stream = new NetworkStream(socket, ownsSocket: false);
            _ = await ReadFrameAsync(stream, cancellationToken);
            requestReceived.SetResult();

            var buffer = new byte[1];
            try
            {
                var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
                await Assert.That(bytesRead).IsEqualTo(0);
            }
            catch (IOException)
            {
                // Windows commonly reports the intentional client-side abort as a reset.
            }
        }, cancellationToken);

        var connection = new KafkaConnection(1, "127.0.0.1", port);
        var connectTask = connection.ConnectAsync(cancellationToken).AsTask();
        await requestReceived.Task.WaitAsync(cancellationToken);

        await connection.DisposeAsync();
        Exception? connectException = null;
        try
        {
            await connectTask;
        }
        catch (Exception exception)
        {
            connectException = exception;
        }

        await serverTask;
        await Assert.That(connectException).IsNotNull();
        await Assert.That(connection.IsConnected).IsFalse();
        await Assert.That(() => ((IKafkaCapabilityProvider)connection).Capabilities)
            .Throws<InvalidOperationException>();
    }

    [Test]
    [Timeout(5_000)]
    public async Task ConnectAsync_WhenV5IsUnsupported_RetriesBrokerSupportedVersionOnSameConnection(
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

            var initialRequest = await ReadFrameAsync(stream, cancellationToken);
            await Assert.That(BinaryPrimitives.ReadInt16BigEndian(initialRequest)).IsEqualTo((short)ApiKey.ApiVersions);
            await Assert.That(BinaryPrimitives.ReadInt16BigEndian(initialRequest.AsSpan(2))).IsEqualTo((short)5);
            var initialCorrelationId = BinaryPrimitives.ReadInt32BigEndian(initialRequest.AsSpan(4));
            await stream.WriteAsync(
                BuildV0Response(
                    initialCorrelationId,
                    ErrorCode.UnsupportedVersion,
                    new ApiVersion(ApiKey.ApiVersions, 0, 3)),
                cancellationToken);

            var retryRequest = await ReadFrameAsync(stream, cancellationToken);
            await Assert.That(BinaryPrimitives.ReadInt16BigEndian(retryRequest)).IsEqualTo((short)ApiKey.ApiVersions);
            await Assert.That(BinaryPrimitives.ReadInt16BigEndian(retryRequest.AsSpan(2))).IsEqualTo((short)3);
            var retryCorrelationId = BinaryPrimitives.ReadInt32BigEndian(retryRequest.AsSpan(4));
            await stream.WriteAsync(
                BuildResponse(
                    retryCorrelationId,
                    ErrorCode.None,
                    new ApiVersion(ApiKey.ApiVersions, 0, 3),
                    new ApiVersion(ApiKey.Metadata, 9, 12)),
                cancellationToken);
            await releaseServer.Task.WaitAsync(cancellationToken);
        }, cancellationToken);

        await using var connection = new KafkaConnection(1, "127.0.0.1", port);
        try
        {
            await connection.ConnectAsync(cancellationToken);

            var capabilities = ((IKafkaCapabilityProvider)connection).Capabilities;
            await Assert.That(connection.IsConnected).IsTrue();
            await Assert.That(capabilities.NegotiateVersion(ApiKey.ApiVersions, 0, 5)).IsEqualTo((short)3);
            await Assert.That(capabilities.NegotiateVersion(ApiKey.Metadata, 9, 13)).IsEqualTo((short)12);
        }
        finally
        {
            releaseServer.TrySetResult();
        }

        await serverTask;
    }

    [Test]
    [Timeout(5_000)]
    [Arguments((short)6, (short)7)]
    [Arguments((short)5, (short)4)]
    public async Task ConnectAsync_WhenFallbackRangeHasNoCommonVersion_ThrowsBrokerVersionException(
        short brokerMinVersion,
        short brokerMaxVersion,
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
                BuildV0Response(
                    correlationId,
                    ErrorCode.UnsupportedVersion,
                    new ApiVersion(ApiKey.ApiVersions, brokerMinVersion, brokerMaxVersion)),
                cancellationToken);
        }, cancellationToken);

        await using var connection = new KafkaConnection(1, "127.0.0.1", port);
        await Assert.That(async () => await connection.ConnectAsync(cancellationToken))
            .Throws<BrokerVersionException>();
        await serverTask;
        await Assert.That(connection.IsConnected).IsFalse();
    }

    [Test]
    [Timeout(5_000)]
    public async Task ConnectAsync_WhenFallbackOmitsApiVersionsRange_ThrowsBrokerVersionException(
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
                BuildV0Response(correlationId, ErrorCode.UnsupportedVersion),
                cancellationToken);
        }, cancellationToken);

        await using var connection = new KafkaConnection(1, "127.0.0.1", port);
        await Assert.That(async () => await connection.ConnectAsync(cancellationToken))
            .Throws<BrokerVersionException>()
            .WithMessageContaining("no valid ApiVersions range");
        await serverTask;
    }

    [Test]
    [Timeout(5_000)]
    public async Task ConnectAsync_WhenFallbackIsRejected_DoesNotRetryAgain(
        CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            using var socket = await listener.AcceptSocketAsync(cancellationToken);
            await using var stream = new NetworkStream(socket, ownsSocket: false);

            var initialRequest = await ReadFrameAsync(stream, cancellationToken);
            var initialCorrelationId = BinaryPrimitives.ReadInt32BigEndian(initialRequest.AsSpan(4));
            await stream.WriteAsync(
                BuildV0Response(
                    initialCorrelationId,
                    ErrorCode.UnsupportedVersion,
                    new ApiVersion(ApiKey.ApiVersions, 0, 3)),
                cancellationToken);

            var retryRequest = await ReadFrameAsync(stream, cancellationToken);
            await Assert.That(BinaryPrimitives.ReadInt16BigEndian(retryRequest.AsSpan(2))).IsEqualTo((short)3);
            var retryCorrelationId = BinaryPrimitives.ReadInt32BigEndian(retryRequest.AsSpan(4));
            await stream.WriteAsync(
                BuildV0Response(
                    retryCorrelationId,
                    ErrorCode.UnsupportedVersion,
                    new ApiVersion(ApiKey.ApiVersions, 0, 2)),
                cancellationToken);
        }, cancellationToken);

        await using var connection = new KafkaConnection(1, "127.0.0.1", port);
        await Assert.That(async () => await connection.ConnectAsync(cancellationToken))
            .Throws<BrokerVersionException>()
            .WithMessageContaining("limited to one retry");
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
    public async Task ConnectAsync_Kip1242_KnownBrokerSendsExpectedIdentity(
        CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var releaseServer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var identity = new MetadataClusterIdentity();
        identity.Configure(enabled: true);
        identity.UpdateClusterId("cluster-a");

        var serverTask = Task.Run(async () =>
        {
            using var socket = await listener.AcceptSocketAsync(cancellationToken);
            await using var stream = new NetworkStream(socket, ownsSocket: false);
            var request = await ReadFrameAsync(stream, cancellationToken);
            var (clusterId, nodeId) = ReadKip1242Identity(request);

            await Assert.That(BinaryPrimitives.ReadInt16BigEndian(request.AsSpan(2))).IsEqualTo((short)5);
            await Assert.That(clusterId).IsEqualTo("cluster-a");
            await Assert.That(nodeId).IsEqualTo(7);

            var correlationId = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4));
            await stream.WriteAsync(BuildResponse(correlationId, ErrorCode.None), cancellationToken);
            await releaseServer.Task.WaitAsync(cancellationToken);
        }, cancellationToken);

        await using var connection = new KafkaConnection(
            7,
            "127.0.0.1",
            port,
            clientId: null,
            options: null,
            logger: null,
            ResponseBufferPool.Default,
            metadataClusterIdentity: identity);

        try
        {
            await connection.ConnectAsync(cancellationToken);
            await Assert.That(connection.IsConnected).IsTrue();
        }
        finally
        {
            releaseServer.TrySetResult();
        }

        await serverTask;
    }

    [Test]
    [Timeout(5_000)]
    public async Task ConnectAsync_Kip1242_BootstrapConnectionOmitsLearnedIdentity(
        CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var releaseServer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var identity = new MetadataClusterIdentity();
        identity.Configure(enabled: true);
        identity.UpdateClusterId("cluster-a");

        var serverTask = Task.Run(async () =>
        {
            using var socket = await listener.AcceptSocketAsync(cancellationToken);
            await using var stream = new NetworkStream(socket, ownsSocket: false);
            var request = await ReadFrameAsync(stream, cancellationToken);
            var (clusterId, nodeId) = ReadKip1242Identity(request);

            await Assert.That(clusterId).IsNull();
            await Assert.That(nodeId).IsEqualTo(-1);

            var correlationId = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4));
            await stream.WriteAsync(BuildResponse(correlationId, ErrorCode.None), cancellationToken);
            await releaseServer.Task.WaitAsync(cancellationToken);
        }, cancellationToken);

        await using var connection = new KafkaConnection(
            -1,
            "127.0.0.1",
            port,
            clientId: null,
            options: null,
            logger: null,
            ResponseBufferPool.Default,
            metadataClusterIdentity: identity);

        try
        {
            await connection.ConnectAsync(cancellationToken);
        }
        finally
        {
            releaseServer.TrySetResult();
        }

        await serverTask;
    }

    [Test]
    [Timeout(5_000)]
    public async Task ConnectAsync_Kip1242_RebootstrapRequiredPreservesIdentityUntilRebootstrap(
        CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var identity = new MetadataClusterIdentity();
        identity.Configure(enabled: true);
        identity.UpdateClusterId("cluster-a");

        var serverTask = Task.Run(async () =>
        {
            using var socket = await listener.AcceptSocketAsync(cancellationToken);
            await using var stream = new NetworkStream(socket, ownsSocket: false);
            var request = await ReadFrameAsync(stream, cancellationToken);
            var correlationId = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4));
            await stream.WriteAsync(
                BuildResponse(correlationId, ErrorCode.RebootstrapRequired),
                cancellationToken);
        }, cancellationToken);

        await using var connection = new KafkaConnection(
            7,
            "127.0.0.1",
            port,
            clientId: null,
            options: null,
            logger: null,
            ResponseBufferPool.Default,
            metadataClusterIdentity: identity);

        var exception = await Assert.That(async () => await connection.ConnectAsync(cancellationToken))
            .Throws<KafkaException>();
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.RebootstrapRequired);
        await Assert.That(exception.IsRetriable).IsTrue();
        await Assert.That(connection.IsConnected).IsFalse();
        await Assert.That(identity.GetExpectedBrokerIdentity(7))
            .IsEqualTo(new ExpectedBrokerIdentity("cluster-a", 7));
        await Assert.That(identity.TryConsumeRebootstrapRequest()).IsTrue();
        await Assert.That(identity.TryConsumeRebootstrapRequest()).IsFalse();
        identity.BeginRebootstrap();
        await Assert.That(identity.GetExpectedBrokerIdentity(7)).IsNull();
        await serverTask;
    }

    [Test]
    [Timeout(5_000)]
    public async Task ConnectAsync_WhenNegotiationTimesOut_ThrowsKafkaException(
        CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var serverStarted = new ManualResetEventSlim();
        using var releaseServer = new ManualResetEventSlim();

        var serverTask = Task.Factory.StartNew(
            () =>
            {
                serverStarted.Set();
                using var socket = listener.AcceptSocketAsync(cancellationToken)
                    .AsTask().GetAwaiter().GetResult();
                using var stream = new NetworkStream(socket, ownsSocket: false);
                var length = new byte[sizeof(int)];
                stream.ReadExactly(length);
                var frame = new byte[BinaryPrimitives.ReadInt32BigEndian(length)];
                stream.ReadExactly(frame);
                releaseServer.Wait(cancellationToken);
            },
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        serverStarted.Wait(cancellationToken);

        await using var connection = new KafkaConnection(
            1,
            "127.0.0.1",
            port,
            options: new ConnectionOptions { RequestTimeout = TimeSpan.FromMilliseconds(500) });

        try
        {
            var exception = await Assert.That(async () => await connection.ConnectAsync(cancellationToken))
                .Throws<KafkaException>();

            await Assert.That(exception).IsNotNull();
            await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.RequestTimedOut);
            await Assert.That(connection.IsConnected).IsFalse();
        }
        finally
        {
            releaseServer.Set();
        }

        await serverTask;
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
            await ServeFeatureGenerationAsync(
                listener,
                releaseFirstGeneration.Task,
                finalizedFeaturesEpoch: 1,
                transactionVersion: 1,
                [
                    new ApiVersion(ApiKey.Metadata, 9, 12),
                    new ApiVersion(ApiKey.Produce, 3, 7)
                ],
                cancellationToken);
            await ServeFeatureGenerationAsync(
                listener,
                releaseSecondGeneration.Task,
                finalizedFeaturesEpoch: 2,
                transactionVersion: 2,
                [new ApiVersion(ApiKey.Metadata, 9, 13)],
                cancellationToken);
        }, cancellationToken);

        await using var pool = new ConnectionPool(connectionOptions: new ConnectionOptions
        {
            ReconnectBackoff = TimeSpan.Zero,
            ReconnectBackoffMax = TimeSpan.Zero
        });
        await using var metadata = new MetadataManager(
            pool,
            [$"127.0.0.1:{port}"],
            new MetadataOptions { EnableBackgroundRefresh = false });
        metadata.ObserveClusterCapabilities(
            "cluster-a",
            KafkaConnectionCapabilities.Create(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys = [],
                FinalizedFeaturesEpoch = 0,
                FinalizedFeatures = [new FinalizedFeature("transaction.version", 0, 0)]
            }));

        var firstConnection = await pool.GetConnectionAsync("127.0.0.1", port, cancellationToken);
        var firstCapabilities = ((IKafkaCapabilityProvider)firstConnection).Capabilities;
        await Assert.That(firstCapabilities.NegotiateVersion(ApiKey.Produce, 3, 13)).IsEqualTo((short)7);
        await Assert.That(metadata.TryGetFinalizedFeatureVersion(
            "transaction.version",
            out var firstFeatureVersion)).IsTrue();
        await Assert.That(firstFeatureVersion).IsEqualTo((short)1);

        releaseFirstGeneration.SetResult();
        await WaitUntilAsync(() => !firstConnection.IsConnected, cancellationToken);

        var secondConnection = await pool.GetConnectionAsync("127.0.0.1", port, cancellationToken);
        var secondCapabilities = ((IKafkaCapabilityProvider)secondConnection).Capabilities;

        await Assert.That(secondConnection).IsNotSameReferenceAs(firstConnection);
        await Assert.That(secondCapabilities.NegotiateVersion(ApiKey.Metadata, 9, 13)).IsEqualTo((short)13);
        await Assert.That(() => secondCapabilities.NegotiateVersion(ApiKey.Produce, 3, 13))
            .Throws<BrokerVersionException>();
        await Assert.That(metadata.TryGetFinalizedFeatureVersion(
            "transaction.version",
            out var secondFeatureVersion)).IsTrue();
        await Assert.That(secondFeatureVersion).IsEqualTo((short)2);

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

    private static async Task ServeFeatureGenerationAsync(
        TcpListener listener,
        Task releaseConnection,
        long finalizedFeaturesEpoch,
        short transactionVersion,
        IReadOnlyList<ApiVersion> versions,
        CancellationToken cancellationToken)
    {
        using var socket = await listener.AcceptSocketAsync(cancellationToken);
        await using var stream = new NetworkStream(socket, ownsSocket: false);
        var request = await ReadFrameAsync(stream, cancellationToken);
        var correlationId = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4));
        await stream.WriteAsync(
            BuildFeatureResponse(
                correlationId,
                finalizedFeaturesEpoch,
                transactionVersion,
                versions),
            cancellationToken);
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

    private static (string? ClusterId, int NodeId) ReadKip1242Identity(byte[] request)
    {
        var reader = new KafkaProtocolReader(request);
        _ = RequestHeader.Read(ref reader, headerVersion: 2);
        _ = reader.ReadCompactNonNullableString();
        _ = reader.ReadCompactNonNullableString();
        return (reader.ReadCompactString(), reader.ReadInt32());
    }

    private static byte[] BuildResponse(
        int correlationId,
        ErrorCode errorCode,
        params ApiVersion[] versions)
        => BuildResponseCore(correlationId, errorCode, null, null, versions);

    private static byte[] BuildFeatureResponse(
        int correlationId,
        long finalizedFeaturesEpoch,
        short transactionVersion,
        IReadOnlyList<ApiVersion> versions)
        => BuildResponseCore(
            correlationId,
            ErrorCode.None,
            finalizedFeaturesEpoch,
            transactionVersion,
            versions);

    private static byte[] BuildResponseCore(
        int correlationId,
        ErrorCode errorCode,
        long? finalizedFeaturesEpoch,
        short? transactionVersion,
        IReadOnlyList<ApiVersion> versions)
    {
        var body = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(body);
        writer.WriteInt16((short)errorCode);
        writer.WriteUnsignedVarInt(versions.Count + 1);
        foreach (var version in versions)
        {
            writer.WriteInt16((short)version.ApiKey);
            writer.WriteInt16(version.MinVersion);
            writer.WriteInt16(version.MaxVersion);
            writer.WriteEmptyTaggedFields();
        }

        writer.WriteInt32(0);
        if (finalizedFeaturesEpoch is null)
        {
            writer.WriteEmptyTaggedFields();
        }
        else
        {
            writer.WriteUnsignedVarInt(2);
            writer.WriteUnsignedVarInt(1);
            writer.WriteUnsignedVarInt(sizeof(long));
            writer.WriteInt64(finalizedFeaturesEpoch.Value);

            var features = new ArrayBufferWriter<byte>();
            var featureWriter = new KafkaProtocolWriter(features);
            featureWriter.WriteUnsignedVarInt(2);
            featureWriter.WriteCompactString("transaction.version");
            featureWriter.WriteInt16(transactionVersion!.Value);
            featureWriter.WriteInt16(0);
            featureWriter.WriteEmptyTaggedFields();

            writer.WriteUnsignedVarInt(2);
            writer.WriteUnsignedVarInt(features.WrittenCount);
            writer.WriteRawBytes(features.WrittenSpan);
        }

        var frame = new byte[sizeof(int) + sizeof(int) + body.WrittenCount];
        BinaryPrimitives.WriteInt32BigEndian(frame, frame.Length - sizeof(int));
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(sizeof(int)), correlationId);
        body.WrittenSpan.CopyTo(frame.AsSpan(sizeof(int) * 2));
        return frame;
    }

    private static byte[] BuildV0Response(
        int correlationId,
        ErrorCode errorCode,
        params ApiVersion[] versions)
    {
        var body = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(body);
        writer.WriteInt16((short)errorCode);
        writer.WriteInt32(versions.Length);
        foreach (var version in versions)
        {
            writer.WriteInt16((short)version.ApiKey);
            writer.WriteInt16(version.MinVersion);
            writer.WriteInt16(version.MaxVersion);
        }

        var frame = new byte[sizeof(int) + sizeof(int) + body.WrittenCount];
        BinaryPrimitives.WriteInt32BigEndian(frame, frame.Length - sizeof(int));
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(sizeof(int)), correlationId);
        body.WrittenSpan.CopyTo(frame.AsSpan(sizeof(int) * 2));
        return frame;
    }
}
