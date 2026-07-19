using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class ApiVersionNegotiationBenchmarks
{
    private MetadataManager _metadataManager = null!;
    private ApiVersionsResponse _response = null!;
    private KafkaConnectionCapabilities _capabilities = null!;
    private IKafkaConnection _connection = null!;
    private ConcurrentDictionary<ApiKey, short> _concurrentCache = null!;
    private int _volatileProducerVersion;

    [GlobalSetup]
    public void Setup()
    {
        _metadataManager = new MetadataManager(
            connectionPool: null!,
            bootstrapServers: ["localhost:9092"]);
        _metadataManager.SetApiVersion(ApiKey.Produce, 3, 13);
        _response = new ApiVersionsResponse
        {
            ErrorCode = ErrorCode.None,
            ApiKeys =
            [
                new ApiVersion(ApiKey.ApiVersions, 0, 4),
                new ApiVersion(ApiKey.Metadata, 9, 13),
                new ApiVersion(ApiKey.Produce, 3, 13),
                new ApiVersion(ApiKey.Fetch, 12, 18)
            ],
            SupportedFeatures = [new SupportedFeature("kraft.version", 0, 1)],
            FinalizedFeaturesEpoch = 42,
            FinalizedFeatures = [new FinalizedFeature("transaction.version", 2, 0)]
        };
        _capabilities = KafkaConnectionCapabilities.Create(_response);
        _connection = new CapabilityConnection(_capabilities);
        _concurrentCache = new ConcurrentDictionary<ApiKey, short>();
        _concurrentCache[ApiKey.Produce] = 13;
        _volatileProducerVersion = 13;

        _ = _metadataManager.GetNegotiatedApiVersion(_connection, ApiKey.Produce, 3, 13);
    }

    [GlobalCleanup]
    public async Task Cleanup() => await _metadataManager.DisposeAsync().ConfigureAwait(false);

    [Benchmark(Description = "Connection setup: capability snapshot")]
    public int CreateCapabilitySnapshot() =>
        KafkaConnectionCapabilities.Create(_response).ApiRangeCount;

    [Benchmark(Description = "O(1) packed-array lookup/intersection")]
    public short ConnectionSnapshotLookup() =>
        _capabilities.NegotiateVersion(ApiKey.Produce, 3, 13);

    [Benchmark(Description = "Producer send: exact connection selection")]
    public short ProducerConnectionSelection() =>
        _metadataManager.GetNegotiatedApiVersion(_connection, ApiKey.Produce, 3, 13);

    [Benchmark(Description = "Legacy producer volatile cache")]
    public int VolatileProducerCache() => Volatile.Read(ref _volatileProducerVersion);

    [Benchmark(Description = "ConcurrentDictionary cache")]
    public short ConcurrentDictionaryCache() => _concurrentCache[ApiKey.Produce];

    private sealed class CapabilityConnection(KafkaConnectionCapabilities capabilities) :
        IKafkaConnection,
        IKafkaCapabilityProvider
    {
        public int BrokerId => 1;
        public string Host => "benchmark";
        public int Port => 9092;
        public bool IsConnected => true;
        public KafkaConnectionCapabilities Capabilities { get; } = capabilities;

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse => throw new NotSupportedException();

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

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
