using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Admin;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 10)]
public class ControllerMetadataRefreshBenchmarks
{
    private TestConnectionPool _pool = null!;
    private MetadataManager _versionManager = null!;
    private ControllerMetadataManager _manager = null!;

    [GlobalSetup]
    public void Setup()
    {
        _pool = new TestConnectionPool();
        _versionManager = new MetadataManager(_pool, []);
        _versionManager.SetApiVersion(ApiKey.DescribeCluster, 1, 2);
        _manager = new ControllerMetadataManager(
            _pool,
            _versionManager,
            ["seed:9093"],
            new MetadataOptions { InitTimeoutMs = 60_000 });
    }

    [Benchmark]
    public ValueTask Refresh() => _manager.RefreshAsync(CancellationToken.None);

    [GlobalCleanup]
    public async ValueTask Cleanup()
    {
        _manager.Dispose();
        await _versionManager.DisposeAsync().ConfigureAwait(false);
        await _pool.DisposeAsync().ConfigureAwait(false);
    }

    private sealed class TestConnectionPool : IConnectionPool
    {
        private readonly IKafkaConnection _connection = new TestConnection();

        public ValueTask<IKafkaConnection> GetConnectionAsync(
            string host,
            int port,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(_connection);

        public ValueTask<IKafkaConnection> GetConnectionAsync(
            int brokerId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<IKafkaConnection> GetConnectionByIndexAsync(
            int brokerId,
            int index,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void RegisterBroker(int brokerId, string host, int port) => throw new NotSupportedException();

        public ValueTask<int> ScaleConnectionGroupAsync(
            int brokerId,
            int newCount,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<IKafkaConnection?> ShrinkConnectionGroupAsync(
            int brokerId,
            int newCount,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask RemoveConnectionAsync(int brokerId) => throw new NotSupportedException();

        public ValueTask CloseAllAsync() => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => _connection.DisposeAsync();
    }

    private sealed class TestConnection : IKafkaConnection
    {
        private static readonly DescribeClusterResponse Response = new()
        {
            ErrorCode = ErrorCode.None,
            EndpointType = DescribeClusterEndpointType.Controller,
            ClusterId = "cluster-a",
            ControllerId = 1,
            Nodes = [new DescribeClusterNode { NodeId = 1, Host = "controller-1", Port = 19093 }]
        };

        public int BrokerId => -1;
        public string Host => "seed";
        public int Port => 9093;
        public bool IsConnected => true;

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse =>
            ValueTask.FromResult((TResponse)(IKafkaResponse)Response);

        public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse => throw new NotSupportedException();

        public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse => throw new NotSupportedException();

        public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse => throw new NotSupportedException();

        public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse => throw new NotSupportedException();

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
