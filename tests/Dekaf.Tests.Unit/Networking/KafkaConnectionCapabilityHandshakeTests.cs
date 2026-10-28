using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
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

        var serverTask = StartServerEagerly(async () =>
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
        });

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

        var serverTask = StartServerEagerly(async () =>
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
        });

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
    [Arguments(false, (short)2)]
    [Arguments(true, (short)2)]
    [Arguments(true, (short)4)]
    public async Task ConnectAsync_WhenInitialVersionIsUnsupported_RetriesBrokerSupportedVersionOnSameConnection(
        bool knownIdentity,
        short brokerMaxVersion,
        CancellationToken cancellationToken)
    {
        // A known identity sends v5 first (KIP-1242); otherwise the broadly-compatible v3 probe.
        var initialVersion = knownIdentity ? (short)5 : (short)3;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var releaseServer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var serverTask = StartServerEagerly(async () =>
        {
            using var socket = await listener.AcceptSocketAsync(cancellationToken);
            await using var stream = new NetworkStream(socket, ownsSocket: false);

            var initialRequest = await ReadFrameAsync(stream, cancellationToken);
            await Assert.That(BinaryPrimitives.ReadInt16BigEndian(initialRequest)).IsEqualTo((short)ApiKey.ApiVersions);
            await Assert.That(BinaryPrimitives.ReadInt16BigEndian(initialRequest.AsSpan(2))).IsEqualTo(initialVersion);
            var initialCorrelationId = BinaryPrimitives.ReadInt32BigEndian(initialRequest.AsSpan(4));
            await stream.WriteAsync(
                BuildV0Response(
                    initialCorrelationId,
                    ErrorCode.UnsupportedVersion,
                    new ApiVersion(ApiKey.ApiVersions, 0, brokerMaxVersion)),
                cancellationToken);

            var retryRequest = await ReadFrameAsync(stream, cancellationToken);
            await Assert.That(BinaryPrimitives.ReadInt16BigEndian(retryRequest)).IsEqualTo((short)ApiKey.ApiVersions);
            await Assert.That(BinaryPrimitives.ReadInt16BigEndian(retryRequest.AsSpan(2))).IsEqualTo(brokerMaxVersion);
            var retryCorrelationId = BinaryPrimitives.ReadInt32BigEndian(retryRequest.AsSpan(4));
            var versions = new[]
            {
                new ApiVersion(ApiKey.ApiVersions, 0, brokerMaxVersion),
                new ApiVersion(ApiKey.Metadata, 5, 12)
            };
            await stream.WriteAsync(
                brokerMaxVersion >= 3
                    ? BuildResponse(retryCorrelationId, ErrorCode.None, versions)
                    : BuildLegacyResponse(retryCorrelationId, ErrorCode.None, versions),
                cancellationToken);
            await releaseServer.Task.WaitAsync(cancellationToken);
        });

        await using var connection = new KafkaConnection(
            1,
            "127.0.0.1",
            port,
            clientId: null,
            options: null,
            logger: null,
            ResponseBufferPool.Default,
            metadataClusterIdentity: knownIdentity ? CreateExpectedIdentity() : null);
        try
        {
            await connection.ConnectAsync(cancellationToken);

            var capabilities = ((IKafkaCapabilityProvider)connection).Capabilities;
            await Assert.That(connection.IsConnected).IsTrue();
            await Assert.That(capabilities.NegotiateVersion(ApiKey.ApiVersions, 0, 5)).IsEqualTo(brokerMaxVersion);
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

        var serverTask = StartServerEagerly(async () =>
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
        });

        await using var connection = new KafkaConnection(
            1,
            "127.0.0.1",
            port,
            clientId: null,
            options: null,
            logger: null,
            ResponseBufferPool.Default,
            metadataClusterIdentity: CreateExpectedIdentity());
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

        var serverTask = StartServerEagerly(async () =>
        {
            using var socket = await listener.AcceptSocketAsync(cancellationToken);
            await using var stream = new NetworkStream(socket, ownsSocket: false);
            var request = await ReadFrameAsync(stream, cancellationToken);
            var correlationId = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4));
            await stream.WriteAsync(
                BuildV0Response(correlationId, ErrorCode.UnsupportedVersion),
                cancellationToken);
        });

        await using var connection = new KafkaConnection(
            1,
            "127.0.0.1",
            port,
            clientId: null,
            options: null,
            logger: null,
            ResponseBufferPool.Default,
            metadataClusterIdentity: CreateExpectedIdentity());
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

        var serverTask = StartServerEagerly(async () =>
        {
            using var socket = await listener.AcceptSocketAsync(cancellationToken);
            await using var stream = new NetworkStream(socket, ownsSocket: false);

            var initialRequest = await ReadFrameAsync(stream, cancellationToken);
            var initialCorrelationId = BinaryPrimitives.ReadInt32BigEndian(initialRequest.AsSpan(4));
            await stream.WriteAsync(
                BuildV0Response(
                    initialCorrelationId,
                    ErrorCode.UnsupportedVersion,
                    new ApiVersion(ApiKey.ApiVersions, 0, 2)),
                cancellationToken);

            var retryRequest = await ReadFrameAsync(stream, cancellationToken);
            await Assert.That(BinaryPrimitives.ReadInt16BigEndian(retryRequest.AsSpan(2))).IsEqualTo((short)2);
            var retryCorrelationId = BinaryPrimitives.ReadInt32BigEndian(retryRequest.AsSpan(4));
            await stream.WriteAsync(
                BuildV0Response(
                    retryCorrelationId,
                    ErrorCode.UnsupportedVersion,
                    new ApiVersion(ApiKey.ApiVersions, 0, 2)),
                cancellationToken);
        });

        await using var connection = new KafkaConnection(
            1,
            "127.0.0.1",
            port,
            clientId: null,
            options: null,
            logger: null,
            ResponseBufferPool.Default,
            metadataClusterIdentity: CreateExpectedIdentity());
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

        var serverTask = StartServerEagerly(async () =>
        {
            using var socket = await listener.AcceptSocketAsync(cancellationToken);
            await using var stream = new NetworkStream(socket, ownsSocket: false);
            var request = await ReadFrameAsync(stream, cancellationToken);
            var correlationId = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4));
            await stream.WriteAsync(
                BuildResponse(correlationId, ErrorCode.InvalidRequest),
                cancellationToken);
        });

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

        var serverTask = StartServerEagerly(async () =>
        {
            using var socket = await listener.AcceptSocketAsync(cancellationToken);
            await using var stream = new NetworkStream(socket, ownsSocket: false);
            // The identity goes out on the first request: no v3 discovery round trip.
            var request = await ReadFrameAsync(stream, cancellationToken);
            var (clusterId, nodeId) = ReadKip1242Identity(request);

            await Assert.That(BinaryPrimitives.ReadInt16BigEndian(request)).IsEqualTo((short)ApiKey.ApiVersions);
            await Assert.That(BinaryPrimitives.ReadInt16BigEndian(request.AsSpan(2))).IsEqualTo((short)5);
            await Assert.That(clusterId).IsEqualTo("cluster-a");
            await Assert.That(nodeId).IsEqualTo(7);

            var correlationId = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4));
            await stream.WriteAsync(
                BuildResponse(
                    correlationId,
                    ErrorCode.None,
                    new ApiVersion(ApiKey.ApiVersions, 0, 5),
                    new ApiVersion(ApiKey.Metadata, 9, 12)),
                cancellationToken);
            await releaseServer.Task.WaitAsync(cancellationToken);

            // Nothing else was sent before the client closed the connection.
            try
            {
                await Assert.That(await stream.ReadAsync(new byte[1], cancellationToken)).IsEqualTo(0);
            }
            catch (IOException exception) when (exception.InnerException is SocketException)
            {
                // KafkaConnection disposal aborts its socket, which can surface as a reset.
            }
        });

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
            await Assert.That(((IKafkaCapabilityProvider)connection).Capabilities
                    .NegotiateVersion(ApiKey.Metadata, 9, 13))
                .IsEqualTo((short)12);
        }
        finally
        {
            await connection.DisposeAsync();
            releaseServer.TrySetResult();
        }

        await serverTask;
    }

    // A broker older than KIP-1242 rejects the v5 identity request once. Reconnects start at the
    // version it reported, so they do not pay the UNSUPPORTED_VERSION round trip again.
    [Test]
    [Timeout(5_000)]
    public async Task ConnectAsync_Kip1242_ReconnectToPreKip1242BrokerStartsAtReportedVersion(
        CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var identity = CreateExpectedIdentity();
        var versions = new[]
        {
            new ApiVersion(ApiKey.ApiVersions, 0, 4),
            new ApiVersion(ApiKey.Metadata, 9, 12)
        };
        var initialVersions = new List<short>();

        var serverTask = StartServerEagerly(async () =>
        {
            for (var connectionIndex = 0; connectionIndex < 2; connectionIndex++)
            {
                using var socket = await listener.AcceptSocketAsync(cancellationToken);
                await using var stream = new NetworkStream(socket, ownsSocket: false);
                var request = await ReadFrameAsync(stream, cancellationToken);
                var version = BinaryPrimitives.ReadInt16BigEndian(request.AsSpan(2));
                initialVersions.Add(version);
                var correlationId = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4));
                if (version == 5)
                {
                    await stream.WriteAsync(
                        BuildV0Response(correlationId, ErrorCode.UnsupportedVersion, versions[0]),
                        cancellationToken);
                    request = await ReadFrameAsync(stream, cancellationToken);
                    await Assert.That(BinaryPrimitives.ReadInt16BigEndian(request.AsSpan(2))).IsEqualTo((short)4);
                    correlationId = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4));
                }

                await stream.WriteAsync(BuildResponse(correlationId, ErrorCode.None, versions), cancellationToken);

                // Hold the socket until the client closes it.
                try
                {
                    while (await stream.ReadAsync(new byte[1], cancellationToken) != 0)
                    {
                    }
                }
                catch (IOException exception) when (exception.InnerException is SocketException)
                {
                    // KafkaConnection disposal aborts its socket, which can surface as a reset.
                }
            }
        });

        for (var connectionIndex = 0; connectionIndex < 2; connectionIndex++)
        {
            var connection = new KafkaConnection(
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
                await connection.DisposeAsync();
            }
        }

        await serverTask;
        await Assert.That(initialVersions.Count).IsEqualTo(2);
        await Assert.That(initialVersions[0]).IsEqualTo((short)5);
        await Assert.That(initialVersions[1]).IsEqualTo((short)4);
    }

    // Some servers (the Azure Event Hubs emulator) drop the connection on an ApiVersions version
    // they do not know instead of answering UNSUPPORTED_VERSION. The maximum the bootstrap
    // connection reported caps the first connection to every broker, so it is never exceeded.
    [Test]
    [Timeout(5_000)]
    public async Task ConnectAsync_Kip1242_FirstBrokerConnectionStartsAtVersionBootstrapReported(
        CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var identity = new MetadataClusterIdentity();
        identity.Configure(enabled: true);
        var versions = new[]
        {
            new ApiVersion(ApiKey.ApiVersions, 0, 3),
            new ApiVersion(ApiKey.Metadata, 9, 12)
        };
        var initialVersions = new List<short>();

        var serverTask = StartServerEagerly(async () =>
        {
            for (var connectionIndex = 0; connectionIndex < 2; connectionIndex++)
            {
                using var socket = await listener.AcceptSocketAsync(cancellationToken);
                await using var stream = new NetworkStream(socket, ownsSocket: false);
                var request = await ReadFrameAsync(stream, cancellationToken);
                initialVersions.Add(BinaryPrimitives.ReadInt16BigEndian(request.AsSpan(2)));
                var correlationId = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4));
                await stream.WriteAsync(BuildResponse(correlationId, ErrorCode.None, versions), cancellationToken);

                // Hold the socket until the client closes it.
                try
                {
                    while (await stream.ReadAsync(new byte[1], cancellationToken) != 0)
                    {
                    }
                }
                catch (IOException exception) when (exception.InnerException is SocketException)
                {
                    // KafkaConnection disposal aborts its socket, which can surface as a reset.
                }
            }
        });

        // Bootstrap: no broker ID and no trusted cluster ID yet.
        var bootstrap = new KafkaConnection(
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
            await bootstrap.ConnectAsync(cancellationToken);
        }
        finally
        {
            await bootstrap.DisposeAsync();
        }

        identity.UpdateClusterId("cluster-a");
        var broker = new KafkaConnection(
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
            await broker.ConnectAsync(cancellationToken);
            await Assert.That(broker.IsConnected).IsTrue();
        }
        finally
        {
            await broker.DisposeAsync();
        }

        await serverTask;
        await Assert.That(initialVersions).IsEquivalentTo(new short[] { 3, 3 });
    }

    [Test]
    [Timeout(5_000)]
    public async Task ConnectAsync_Kip1242_WhenV5IsRejectedButReportedSupported_ThrowsBrokerVersionException(
        CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverTask = StartServerEagerly(async () =>
        {
            using var socket = await listener.AcceptSocketAsync(cancellationToken);
            await using var stream = new NetworkStream(socket, ownsSocket: false);
            var request = await ReadFrameAsync(stream, cancellationToken);
            await Assert.That(BinaryPrimitives.ReadInt16BigEndian(request.AsSpan(2))).IsEqualTo((short)5);
            var correlationId = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4));
            await stream.WriteAsync(
                BuildV0Response(
                    correlationId,
                    ErrorCode.UnsupportedVersion,
                    new ApiVersion(ApiKey.ApiVersions, 0, 5)),
                cancellationToken);
        });

        await using var connection = new KafkaConnection(
            7,
            "127.0.0.1",
            port,
            clientId: null,
            options: null,
            logger: null,
            ResponseBufferPool.Default,
            metadataClusterIdentity: CreateExpectedIdentity());
        await Assert.That(async () => await connection.ConnectAsync(cancellationToken))
            .Throws<BrokerVersionException>()
            .WithMessageContaining("rejected ApiVersions v5 but reported it as supported");
        await serverTask;
    }

    [Test]
    [NotInParallel]
    [Timeout(5_000)]
    public async Task ConnectAsync_Kip1242_WhenIdentityHandshakeTimesOut_ThrowsKafkaException(
        CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var identityRequestReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var serverTask = StartServerEagerly(async () =>
        {
            using var socket = await listener.AcceptSocketAsync(cancellationToken);
            await using var stream = new NetworkStream(socket, ownsSocket: false);
            _ = await ReadFrameAsync(stream, cancellationToken);
            identityRequestReceived.TrySetResult();
            try
            {
                var buffer = new byte[1];
                while (await stream.ReadAsync(buffer, cancellationToken) != 0)
                {
                    // Drain until the client closes or aborts the socket.
                }
            }
            catch (IOException exception) when (exception.InnerException is SocketException)
            {
                // KafkaConnection disposal aborts its socket, which can surface as a reset.
            }
        });

        var connection = new KafkaConnection(
            7,
            "127.0.0.1",
            port,
            clientId: null,
            options: new ConnectionOptions { RequestTimeout = TimeSpan.FromMilliseconds(50) },
            logger: null,
            ResponseBufferPool.Default,
            metadataClusterIdentity: CreateExpectedIdentity());

        try
        {
            var connectionTask = connection.ConnectAsync(cancellationToken).AsTask();
            await identityRequestReceived.Task.WaitAsync(cancellationToken);
            var exception = await Assert.That(async () => await connectionTask)
                .Throws<KafkaException>();

            await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.RequestTimedOut);
            await Assert.That(connection.IsConnected).IsFalse();
        }
        finally
        {
            await connection.DisposeAsync();
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

        var serverTask = StartServerEagerly(async () =>
        {
            using var socket = await listener.AcceptSocketAsync(cancellationToken);
            await using var stream = new NetworkStream(socket, ownsSocket: false);
            var request = await ReadFrameAsync(stream, cancellationToken);
            await Assert.That(BinaryPrimitives.ReadInt16BigEndian(request.AsSpan(2))).IsEqualTo((short)3);

            var correlationId = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4));
            await stream.WriteAsync(BuildResponse(correlationId, ErrorCode.None), cancellationToken);
            await releaseServer.Task.WaitAsync(cancellationToken);
        });

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
    public async Task ConnectAsync_Kip1242_RebootstrapRequiredPreservesIdentity(
        CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var identity = new MetadataClusterIdentity();
        identity.Configure(enabled: true);
        identity.UpdateClusterId("cluster-a");

        var serverTask = StartServerEagerly(async () =>
        {
            using var socket = await listener.AcceptSocketAsync(cancellationToken);
            await using var stream = new NetworkStream(socket, ownsSocket: false);
            var request = await ReadFrameAsync(stream, cancellationToken);
            await Assert.That(BinaryPrimitives.ReadInt16BigEndian(request.AsSpan(2))).IsEqualTo((short)5);
            var correlationId = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4));
            await stream.WriteAsync(
                BuildResponse(correlationId, ErrorCode.RebootstrapRequired),
                cancellationToken);
        });

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
        await serverTask;
    }

    [Test]
    [NotInParallel]
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
                try
                {
                    var length = new byte[sizeof(int)];
                    stream.ReadExactly(length);
                    var frame = new byte[BinaryPrimitives.ReadInt32BigEndian(length)];
                    stream.ReadExactly(frame);
                    releaseServer.Wait(cancellationToken);
                }
                catch (IOException)
                {
                    // The expected client-side timeout may close the socket before the
                    // mock server finishes reading the request (including end-of-stream).
                }
            },
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        serverStarted.Wait(cancellationToken);

        await using var connection = new KafkaConnection(
            1,
            "127.0.0.1",
            port,
            options: new ConnectionOptions { RequestTimeout = TimeSpan.FromMilliseconds(50) });

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

        var serverTask = StartServerEagerly(async () =>
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
        });

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

        var serverTask = StartServerEagerly(async () =>
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
        });

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

    [Test]
    [Timeout(30_000)]
    public async Task ConnectionPool_Kip1242_BrokerLookupRetiresBootstrapConnectionAtSameEndpoint(
        CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var serverCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var requests = new ConcurrentQueue<ApiVersionsRequestRecord>();
        var serverTask = ServeApiVersionsConnectionsAsync(listener, connectionCount: 2, requests, serverCts.Token);

        await using var pool = new ConnectionPool(connectionOptions: new ConnectionOptions
        {
            ReconnectBackoff = TimeSpan.Zero,
            ReconnectBackoffMax = TimeSpan.Zero
        });
        var identityPool = (IMetadataClusterIdentityPool)pool;
        identityPool.ConfigureMetadataClusterCheck(enabled: true);

        var bootstrap = await pool.GetConnectionAsync("127.0.0.1", port, cancellationToken);
        await Assert.That(KafkaConnectionLease.TryAcquire(bootstrap, out var bootstrapLease)).IsTrue();

        // Metadata from the bootstrap connection teaches the cluster ID and places broker 7 at
        // the bootstrap endpoint.
        identityPool.UpdateMetadataClusterId("cluster-a");
        pool.RegisterBroker(7, "127.0.0.1", port);

        IKafkaConnection broker;
        try
        {
            broker = await pool.GetConnectionAsync(7, cancellationToken);

            await Assert.That(broker).IsNotSameReferenceAs(bootstrap);
            await Assert.That(broker.BrokerId).IsEqualTo(7);
            await Assert.That(broker.IsConnected).IsTrue();
            // The superseded bootstrap socket stays usable while a lease holder still has it.
            await Assert.That(bootstrap.IsConnected).IsTrue();
        }
        finally
        {
            bootstrapLease.Dispose();
        }

        await WaitUntilAsync(() => !bootstrap.IsConnected, cancellationToken);
        await Assert.That(await pool.GetConnectionAsync(7, cancellationToken)).IsSameReferenceAs(broker);

        await serverCts.CancelAsync();
        await serverTask;

        var records = requests.ToArray();
        await Assert.That(records.Any(static r => r.Connection == 0 && r.ClusterId is not null)).IsFalse();
        await Assert.That(records.Any(static r =>
            r.Connection == 1 && r.ClusterId == "cluster-a" && r.NodeId == 7)).IsTrue();
    }

    [Test]
    [Timeout(30_000)]
    [Arguments(false, "cluster-a")]
    [Arguments(true, null)]
    public async Task ConnectionPool_Kip1242_WithoutKnownIdentityBrokerLookupReusesBootstrapConnection(
        bool checkEnabled,
        string? clusterId,
        CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var serverCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var requests = new ConcurrentQueue<ApiVersionsRequestRecord>();
        var serverTask = ServeApiVersionsConnectionsAsync(listener, connectionCount: 1, requests, serverCts.Token);

        await using var pool = new ConnectionPool(connectionOptions: new ConnectionOptions
        {
            ReconnectBackoff = TimeSpan.Zero,
            ReconnectBackoffMax = TimeSpan.Zero
        });
        var identityPool = (IMetadataClusterIdentityPool)pool;
        identityPool.ConfigureMetadataClusterCheck(checkEnabled);

        var bootstrap = await pool.GetConnectionAsync("127.0.0.1", port, cancellationToken);
        identityPool.UpdateMetadataClusterId(clusterId);
        pool.RegisterBroker(7, "127.0.0.1", port);

        var broker = await pool.GetConnectionAsync(7, cancellationToken);

        await Assert.That(broker).IsSameReferenceAs(bootstrap);
        await Assert.That(bootstrap.IsConnected).IsTrue();

        await serverCts.CancelAsync();
        await serverTask;
        await Assert.That(requests.All(static r => r.Connection == 0)).IsTrue();
    }

    [Test]
    [Timeout(30_000)]
    public async Task ConnectionPool_Kip1242_BrokersSharingEndpointReuseOneBrokerConnection(
        CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var serverCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var requests = new ConcurrentQueue<ApiVersionsRequestRecord>();
        var serverTask = ServeApiVersionsConnectionsAsync(listener, connectionCount: 2, requests, serverCts.Token);

        await using var pool = new ConnectionPool(connectionOptions: new ConnectionOptions
        {
            ReconnectBackoff = TimeSpan.Zero,
            ReconnectBackoffMax = TimeSpan.Zero
        });
        var identityPool = (IMetadataClusterIdentityPool)pool;
        identityPool.ConfigureMetadataClusterCheck(enabled: true);
        identityPool.UpdateMetadataClusterId("cluster-a");
        // A proxy or load balancer advertises one endpoint for several broker IDs.
        pool.RegisterBroker(1, "127.0.0.1", port);
        pool.RegisterBroker(2, "127.0.0.1", port);

        var broker1 = await pool.GetConnectionAsync(1, cancellationToken);

        // Alternating lookups share the broker connection instead of retiring and reopening it.
        await Assert.That(await pool.GetConnectionAsync(2, cancellationToken)).IsSameReferenceAs(broker1);
        await Assert.That(await pool.GetConnectionAsync(1, cancellationToken)).IsSameReferenceAs(broker1);
        await Assert.That(await pool.GetConnectionAsync(2, cancellationToken)).IsSameReferenceAs(broker1);
        await Assert.That(broker1.IsConnected).IsTrue();

        await serverCts.CancelAsync();
        await serverTask;
        await Assert.That(requests.All(static r => r.Connection == 0)).IsTrue();
    }

    private readonly record struct ApiVersionsRequestRecord(int Connection, short Version, string? ClusterId, int NodeId);

    /// <summary>
    /// Accepts <paramref name="connectionCount"/> connections and answers every ApiVersions
    /// request on them with v0-v5 support, recording each request's KIP-1242 identity.
    /// </summary>
    private static async Task ServeApiVersionsConnectionsAsync(
        TcpListener listener,
        int connectionCount,
        ConcurrentQueue<ApiVersionsRequestRecord> requests,
        CancellationToken cancellationToken)
    {
        var connections = new List<Task>(connectionCount);
        try
        {
            for (var i = 0; i < connectionCount; i++)
            {
                var socket = await listener.AcceptSocketAsync(cancellationToken);
                connections.Add(ServeConnectionAsync(socket, i));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }

        await Task.WhenAll(connections);

        async Task ServeConnectionAsync(Socket socket, int connection)
        {
            using var ownedSocket = socket;
            await using var stream = new NetworkStream(ownedSocket, ownsSocket: false);
            try
            {
                while (true)
                {
                    var request = await ReadFrameAsync(stream, cancellationToken);
                    var version = BinaryPrimitives.ReadInt16BigEndian(request.AsSpan(2));
                    var (clusterId, nodeId) = version >= 5 ? ReadKip1242Identity(request) : (null, -1);
                    requests.Enqueue(new ApiVersionsRequestRecord(connection, version, clusterId, nodeId));

                    var correlationId = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4));
                    await stream.WriteAsync(
                        BuildResponse(correlationId, ErrorCode.None, new ApiVersion(ApiKey.ApiVersions, 0, 5)),
                        cancellationToken);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or EndOfStreamException)
            {
                // The client closed the connection, or the test is over.
            }
        }
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

    // Invoke inline so the first socket accept is registered before client startup.
    private static Task StartServerEagerly(Func<Task> server) => server();

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

    private static byte[] BuildLegacyResponse(
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

        writer.WriteInt32(0);
        var frame = new byte[sizeof(int) + sizeof(int) + body.WrittenCount];
        BinaryPrimitives.WriteInt32BigEndian(frame, frame.Length - sizeof(int));
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(sizeof(int)), correlationId);
        body.WrittenSpan.CopyTo(frame.AsSpan(sizeof(int) * 2));
        return frame;
    }

    private static MetadataClusterIdentity CreateExpectedIdentity()
    {
        var identity = new MetadataClusterIdentity();
        identity.Configure(enabled: true);
        identity.UpdateClusterId("cluster-a");
        return identity;
    }
}
