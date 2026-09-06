using System.Buffers;
using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientClusterDiscoveryTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DescribeClusterAsync_MapsLiveResultsWithoutChangingRouting(bool includeFenced)
    {
        var (admin, connection, pool, metadata) = CreateAdmin();
        await using var client = admin;
        var result = await ((IAdminClient)client).DescribeClusterAsync(
            new DescribeClusterOptions { IncludeFencedBrokers = includeFenced });

        await Assert.That(result.ClusterId).IsEqualTo("cluster");
        await Assert.That(result.ControllerId).IsEqualTo(1);
        await Assert.That(result.EndpointType).IsEqualTo(DescribeClusterEndpointType.Broker);
        await Assert.That(result.Nodes.Count).IsEqualTo(includeFenced ? 2 : 1);
        await Assert.That(result.Nodes[0].IsFenced).IsFalse();
        if (includeFenced)
        {
            var fenced = result.Nodes[1];
            await Assert.That(fenced.NodeId).IsEqualTo(2);
            await Assert.That(fenced.Host).IsEqualTo("fenced");
            await Assert.That(fenced.Port).IsEqualTo(9093);
            await Assert.That(fenced.Rack).IsEqualTo("rack-b");
            await Assert.That(fenced.IsFenced).IsTrue();
        }

        await connection.Received(1).SendAsync<DescribeClusterRequest, DescribeClusterResponse>(
            Arg.Is<DescribeClusterRequest>(r => r.IncludeFencedBrokers == includeFenced &&
                r.EndpointType == DescribeClusterEndpointType.Broker), 2, Arg.Any<CancellationToken>());
        pool.DidNotReceive().RegisterBroker(2, Arg.Any<string>(), Arg.Any<int>());
        await Assert.That(metadata.Metadata.GetBrokers().Select(n => n.NodeId)).IsEquivalentTo([1]);
        // The existing default-literal call must remain unambiguous on concrete clients.
        var cached = await client.DescribeClusterAsync(default);
        await Assert.That(cached.Nodes.Select(n => n.NodeId)).IsEquivalentTo([1]);
    }

    [Test]
    public async Task DescribeClusterAsync_EncodesRequestedOption()
    {
        var (admin, connection, _, _) = CreateAdmin();
        await using var client = admin;
        DescribeClusterRequest? request = null;
        _ = connection.SendAsync<DescribeClusterRequest, DescribeClusterResponse>(
            Arg.Do<DescribeClusterRequest>(r => request = r), Arg.Any<short>(), Arg.Any<CancellationToken>());
        await client.DescribeClusterAsync(new DescribeClusterOptions { IncludeFencedBrokers = true });
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        request!.Write(ref writer, 2);
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var authorized = reader.ReadBoolean();
        var endpoint = reader.ReadInt8();
        var includeFenced = reader.ReadBoolean();
        await Assert.That(authorized).IsFalse();
        await Assert.That(endpoint).IsEqualTo((sbyte)DescribeClusterEndpointType.Broker);
        await Assert.That(includeFenced).IsTrue();
    }

    [Test]
    public async Task DescribeClusterAsync_RequiresV2OnActualDestination()
    {
        var (admin, connection, _, metadata) = CreateAdmin(version: 1);
        await using var client = admin;
        metadata.SetApiVersion(ApiKey.DescribeCluster, 0, 2);
        await Assert.ThrowsAsync<BrokerVersionException>(async () =>
            await client.DescribeClusterAsync(new DescribeClusterOptions { IncludeFencedBrokers = true }));
        await connection.DidNotReceiveWithAnyArgs().SendAsync<DescribeClusterRequest, DescribeClusterResponse>(
            default!, default, default);
    }

    [Test]
    public async Task DescribeClusterAsync_V1ReportsUnknownFencing()
    {
        var (admin, _, _, _) = CreateAdmin(version: 1);
        await using var client = admin;
        var result = await client.DescribeClusterAsync(new DescribeClusterOptions());
        await Assert.That(result.Nodes[0].IsFenced).IsNull();
    }

    [Test]
    [Arguments(ErrorCode.ClusterAuthorizationFailed)]
    [Arguments(ErrorCode.UnsupportedVersion)]
    public async Task DescribeClusterAsync_PreservesRpcError(ErrorCode error)
    {
        var (admin, connection, _, _) = CreateAdmin();
        await using var client = admin;
        connection.SendAsync<DescribeClusterRequest, DescribeClusterResponse>(
            Arg.Any<DescribeClusterRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new DescribeClusterResponse
            { ClusterId = "cluster", Nodes = [], ErrorCode = error, ErrorMessage = "denied" }));
        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await client.DescribeClusterAsync(new DescribeClusterOptions()));
        await Assert.That(exception!.ErrorCode).IsEqualTo(error);
    }

    [Test]
    public async Task DescribeClusterAsync_RejectsMismatchedEndpoint()
    {
        var (admin, connection, _, _) = CreateAdmin();
        await using var client = admin;
        connection.SendAsync<DescribeClusterRequest, DescribeClusterResponse>(
            Arg.Any<DescribeClusterRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new DescribeClusterResponse
            { ClusterId = "cluster", Nodes = [], EndpointType = DescribeClusterEndpointType.Controller }));
        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await client.DescribeClusterAsync(new DescribeClusterOptions()));
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.MismatchedEndpointType);
    }

    [Test]
    public async Task DescribeClusterAsync_CustomAdminWithoutCapabilityFailsExplicitly()
    {
        var admin = Substitute.For<IAdminClient>();
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await admin.DescribeClusterAsync(new DescribeClusterOptions()));
    }

    [Test]
    public async Task DescribeClusterAsync_CancelsInFlightRpc()
    {
        var (admin, connection, _, _) = CreateAdmin();
        await using var client = admin;
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.SendAsync<DescribeClusterRequest, DescribeClusterResponse>(
            Arg.Any<DescribeClusterRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call => WaitForCancellationAsync(call.Arg<CancellationToken>()));
        var operation = client.DescribeClusterAsync(new DescribeClusterOptions(), cancellation.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await operation);

        async ValueTask<DescribeClusterResponse> WaitForCancellationAsync(CancellationToken token)
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("RPC should have been cancelled.");
        }
    }

    private static (AdminClient, IKafkaConnection, IConnectionPool, MetadataManager) CreateAdmin(short version = 2)
    {
        var connection = Substitute.For<IKafkaConnection>();
        connection.BrokerId.Returns(1);
        connection.Host.Returns("localhost");
        connection.Port.Returns(9092);
        connection.IsConnected.Returns(true);
        var versions = new ApiVersionsResponse
        {
            ErrorCode = ErrorCode.None,
            ApiKeys = [new(ApiKey.ApiVersions, 0, 4), new(ApiKey.Metadata, 9, 13), new(ApiKey.DescribeCluster, 0, version)]
        };
        IKafkaConnection destination = new CapabilityConnection(connection, KafkaConnectionCapabilities.Create(versions));
        connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
            Arg.Any<ApiVersionsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(versions));
        connection.SendAsync<MetadataRequest, MetadataResponse>(
            Arg.Any<MetadataRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new MetadataResponse
            { ClusterId = "cluster", ControllerId = 1, Brokers = [new() { NodeId = 1, Host = "localhost", Port = 9092 }], Topics = [] }));
        connection.SendAsync<DescribeClusterRequest, DescribeClusterResponse>(
            Arg.Any<DescribeClusterRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call => ValueTask.FromResult(new DescribeClusterResponse
            {
                ClusterId = "cluster", ControllerId = 1,
                Nodes = call.Arg<DescribeClusterRequest>().IncludeFencedBrokers
                    ? [new() { NodeId = 1, Host = "localhost", Port = 9092 }, new() { NodeId = 2, Host = "fenced", Port = 9093, Rack = "rack-b", IsFenced = true }]
                    : [new() { NodeId = 1, Host = "localhost", Port = 9092 }]
            }));
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(destination));
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(destination));
        var metadata = new MetadataManager(pool, ["localhost:9092"]);
        return (new AdminClient(new AdminClientOptions { BootstrapServers = ["localhost:9092"], RetryBackoffMs = 1 }, pool, metadata, ownsResources: true), connection, pool, metadata);
    }

    private sealed class CapabilityConnection(IKafkaConnection inner, KafkaConnectionCapabilities capabilities)
        : IKafkaConnection, IKafkaCapabilityProvider
    {
        public int BrokerId => inner.BrokerId;
        public string Host => inner.Host;
        public int Port => inner.Port;
        public bool IsConnected => inner.IsConnected;
        public KafkaConnectionCapabilities Capabilities => capabilities;
        public ValueTask ConnectAsync(CancellationToken cancellationToken = default) => inner.ConnectAsync(cancellationToken);
        public ValueTask DisposeAsync() => inner.DisposeAsync();

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request, short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse =>
            inner.SendAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);

        public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(TRequest request, short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();

        public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(TRequest request, short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();

        public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(TRequest request, short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();

        public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(TRequest request, short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();
    }
}
