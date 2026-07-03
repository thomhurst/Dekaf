using System.Collections.Concurrent;
using Dekaf.Compression;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Telemetry;

namespace Dekaf.Tests.Unit.Telemetry;

public sealed class ClientTelemetryManagerTests
{
    [Test]
    public async Task StartAsync_FetchesSubscriptionAndStoresIds()
    {
        await using var context = new TelemetryTestContext();
        var clientInstanceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        context.Connection.Enqueue(Subscription(clientInstanceId, subscriptionId: 42, pushIntervalMs: 60000));

        await context.Manager.StartAsync();

        var requests = context.Connection.RequestsOfType<GetTelemetrySubscriptionsRequest>();
        await Assert.That(requests.Count).IsEqualTo(1);
        await Assert.That(requests[0].ClientInstanceId).IsEqualTo(Guid.Empty);
        await Assert.That(context.Manager.ClientInstanceId).IsEqualTo(clientInstanceId);
        await Assert.That(context.Manager.SubscriptionId).IsEqualTo(42);
        await Assert.That(context.Manager.IsStarted).IsTrue();
    }

    [Test]
    public async Task BackgroundLoop_PushesTelemetryOnBrokerInterval()
    {
        await using var context = new TelemetryTestContext();
        var clientInstanceId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        context.Connection.Enqueue(Subscription(clientInstanceId, subscriptionId: 7, pushIntervalMs: 10));
        context.Connection.Enqueue(new PushTelemetryResponse { ErrorCode = ErrorCode.None });

        await context.Manager.StartAsync();

        var pushes = await context.Connection.WaitForRequestsAsync<PushTelemetryRequest>(
            count: 1,
            timeout: TimeSpan.FromSeconds(2));

        await Assert.That(pushes[0].ClientInstanceId).IsEqualTo(clientInstanceId);
        await Assert.That(pushes[0].SubscriptionId).IsEqualTo(7);
        await Assert.That(pushes[0].Terminating).IsFalse();
    }

    [Test]
    public async Task BackgroundLoop_RefetchesSubscriptionOnUnknownSubscriptionId()
    {
        await using var context = new TelemetryTestContext();
        var clientInstanceId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        context.Connection.Enqueue(Subscription(clientInstanceId, subscriptionId: 1, pushIntervalMs: 10));
        context.Connection.Enqueue(new PushTelemetryResponse { ErrorCode = ErrorCode.UnknownSubscriptionId });
        context.Connection.Enqueue(Subscription(clientInstanceId, subscriptionId: 2, pushIntervalMs: 10));
        context.Connection.Enqueue(new PushTelemetryResponse { ErrorCode = ErrorCode.None });

        await context.Manager.StartAsync();

        var pushes = await context.Connection.WaitForRequestsAsync<PushTelemetryRequest>(
            count: 2,
            timeout: TimeSpan.FromSeconds(2));
        var subscriptions = context.Connection.RequestsOfType<GetTelemetrySubscriptionsRequest>();

        await Assert.That(subscriptions.Count).IsEqualTo(2);
        await Assert.That(subscriptions[1].ClientInstanceId).IsEqualTo(clientInstanceId);
        await Assert.That(pushes[0].SubscriptionId).IsEqualTo(1);
        await Assert.That(pushes[1].SubscriptionId).IsEqualTo(2);
        await Assert.That(context.Manager.SubscriptionId).IsEqualTo(2);
    }

    [Test]
    public async Task StopAsync_SendsTerminatingPush()
    {
        await using var context = new TelemetryTestContext();
        var clientInstanceId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        context.Connection.Enqueue(Subscription(clientInstanceId, subscriptionId: 99, pushIntervalMs: 60000));
        context.Connection.Enqueue(new PushTelemetryResponse { ErrorCode = ErrorCode.None });

        await context.Manager.StartAsync();
        await context.Manager.StopAsync(TimeSpan.FromSeconds(2));

        var pushes = context.Connection.RequestsOfType<PushTelemetryRequest>();
        await Assert.That(pushes.Count).IsEqualTo(1);
        await Assert.That(pushes[0].ClientInstanceId).IsEqualTo(clientInstanceId);
        await Assert.That(pushes[0].SubscriptionId).IsEqualTo(99);
        await Assert.That(pushes[0].Terminating).IsTrue();
    }

    [Test]
    public async Task StartAsync_SelectsFirstAcceptedSupportedCompression()
    {
        var collector = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Producer);
        collector.RecordConnectionCreated();
        await using var context = new TelemetryTestContext(
            metricCollector: collector,
            compressionCodecs: new CompressionCodecRegistry());
        var clientInstanceId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        context.Connection.Enqueue(Subscription(
            clientInstanceId,
            subscriptionId: 11,
            pushIntervalMs: 10,
            acceptedCompressionTypes: [4, 1, 0],
            requestedMetrics: [string.Empty]));

        await context.Manager.StartAsync();

        var pushes = await context.Connection.WaitForRequestsAsync<PushTelemetryRequest>(
            count: 1,
            timeout: TimeSpan.FromSeconds(2));

        await Assert.That(pushes[0].CompressionType).IsEqualTo((sbyte)1);
        await Assert.That(pushes[0].Metrics.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task StartAsync_DisablesTelemetryWhenNoAcceptedCompressionIsSupported()
    {
        await using var context = new TelemetryTestContext(compressionCodecs: new CompressionCodecRegistry());
        context.Connection.Enqueue(Subscription(
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            subscriptionId: 12,
            pushIntervalMs: 10,
            acceptedCompressionTypes: [4]));

        await context.Manager.StartAsync();

        await Assert.That(context.Manager.IsDisabled).IsTrue();
        await Assert.That(context.Manager.IsStarted).IsFalse();
    }

    [Test]
    public async Task PushTelemetry_DropsPayloadWhenTelemetryMaxBytesExceeded()
    {
        var collector = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Producer);
        collector.RecordConnectionCreated();
        await using var context = new TelemetryTestContext(metricCollector: collector);
        var clientInstanceId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        context.Connection.Enqueue(Subscription(
            clientInstanceId,
            subscriptionId: 13,
            pushIntervalMs: 10,
            telemetryMaxBytes: 1,
            requestedMetrics: [string.Empty]));

        await context.Manager.StartAsync();

        var pushes = await context.Connection.WaitForRequestsAsync<PushTelemetryRequest>(
            count: 1,
            timeout: TimeSpan.FromSeconds(2));

        await Assert.That(pushes[0].Metrics.Length).IsEqualTo(0);
        await Assert.That(pushes[0].CompressionType).IsEqualTo((sbyte)0);
    }

    [Test]
    public async Task PushTelemetry_RetriesWithEmptyPayloadWhenBrokerRejectsTelemetryTooLarge()
    {
        var collector = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Producer);
        collector.RecordConnectionCreated();
        await using var context = new TelemetryTestContext(metricCollector: collector);
        var clientInstanceId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        context.Connection.Enqueue(Subscription(
            clientInstanceId,
            subscriptionId: 14,
            pushIntervalMs: 10,
            requestedMetrics: [string.Empty]));
        context.Connection.Enqueue(new PushTelemetryResponse { ErrorCode = ErrorCode.TelemetryTooLarge });
        context.Connection.Enqueue(new PushTelemetryResponse { ErrorCode = ErrorCode.None });

        await context.Manager.StartAsync();

        var pushes = await context.Connection.WaitForRequestsAsync<PushTelemetryRequest>(
            count: 2,
            timeout: TimeSpan.FromSeconds(2));

        await Assert.That(pushes[0].Metrics.Length).IsGreaterThan(0);
        await Assert.That(pushes[1].Metrics.Length).IsEqualTo(0);
        await Assert.That(pushes[1].CompressionType).IsEqualTo(pushes[0].CompressionType);
    }

    [Test]
    public async Task UnsupportedVersion_DisablesBrokerPushTelemetry()
    {
        await using var context = new TelemetryTestContext();
        context.Connection.Enqueue(new GetTelemetrySubscriptionsResponse
        {
            ErrorCode = ErrorCode.UnsupportedVersion
        });

        await context.Manager.StartAsync();

        await Assert.That(context.Manager.IsDisabled).IsTrue();
        await Assert.That(context.Manager.IsStarted).IsFalse();
        await Assert.That(context.Connection.RequestsOfType<PushTelemetryRequest>().Count).IsEqualTo(0);
    }

    private static GetTelemetrySubscriptionsResponse Subscription(
        Guid clientInstanceId,
        int subscriptionId,
        int pushIntervalMs,
        IReadOnlyList<sbyte>? acceptedCompressionTypes = null,
        int telemetryMaxBytes = 1024,
        bool deltaTemporality = true,
        IReadOnlyList<string>? requestedMetrics = null) => new()
    {
        ErrorCode = ErrorCode.None,
        ClientInstanceId = clientInstanceId,
        SubscriptionId = subscriptionId,
        AcceptedCompressionTypes = acceptedCompressionTypes ?? [0],
        PushIntervalMs = pushIntervalMs,
        TelemetryMaxBytes = telemetryMaxBytes,
        DeltaTemporality = deltaTemporality,
        RequestedMetrics = requestedMetrics ?? ["kafka."]
    };

    private sealed class TelemetryTestContext : IAsyncDisposable
    {
        private readonly MetadataManager _metadataManager;
        private readonly TestConnectionPool _pool;

        public TelemetryTestContext(
            ClientTelemetryMetricCollector? metricCollector = null,
            CompressionCodecRegistry? compressionCodecs = null,
            IClientTelemetryPayloadProvider? payloadProvider = null)
        {
            _pool = new TestConnectionPool();
            _metadataManager = new MetadataManager(_pool, ["localhost:9092"]);
            _metadataManager.SetApiVersion(ApiKey.GetTelemetrySubscriptions, 0, 0);
            _metadataManager.SetApiVersion(ApiKey.PushTelemetry, 0, 0);
            _metadataManager.Metadata.Update(new MetadataResponse
            {
                Brokers =
                [
                    new BrokerMetadata
                    {
                        NodeId = 0,
                        Host = "localhost",
                        Port = 9092
                    }
                ],
                ClusterId = "test-cluster",
                ControllerId = 0,
                Topics = []
            });

            Manager = new ClientTelemetryManager(
                _pool,
                _metadataManager,
                metricCollector: metricCollector,
                compressionCodecs: compressionCodecs,
                payloadProvider: payloadProvider);
        }

        public ClientTelemetryManager Manager { get; }
        public TestKafkaConnection Connection => _pool.Connection;

        public async ValueTask DisposeAsync()
        {
            await Manager.DisposeAsync().ConfigureAwait(false);
            await _metadataManager.DisposeAsync().ConfigureAwait(false);
            await _pool.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class TestConnectionPool : IConnectionPool
    {
        public TestKafkaConnection Connection { get; } = new();

        public ValueTask<IKafkaConnection> GetConnectionAsync(int brokerId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IKafkaConnection>(Connection);

        public ValueTask<IKafkaConnection> GetConnectionByIndexAsync(int brokerId, int index, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IKafkaConnection>(Connection);

        public ValueTask<IKafkaConnection> GetConnectionAsync(string host, int port, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IKafkaConnection>(Connection);

        public void RegisterBroker(int brokerId, string host, int port)
        {
        }

        public ValueTask<int> ScaleConnectionGroupAsync(int brokerId, int newCount, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(newCount);

        public ValueTask<IKafkaConnection?> ShrinkConnectionGroupAsync(int brokerId, int newCount, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IKafkaConnection?>(null);

        public ValueTask RemoveConnectionAsync(int brokerId) => ValueTask.CompletedTask;

        public ValueTask CloseAllAsync() => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TestKafkaConnection : IKafkaConnection
    {
        private readonly ConcurrentQueue<object> _responses = new();
        private readonly ConcurrentQueue<object> _requests = new();
        private readonly SemaphoreSlim _requestSignal = new(0);

        public int BrokerId => 0;
        public string Host => "localhost";
        public int Port => 9092;
        public bool IsConnected => true;

        public void Enqueue<TResponse>(TResponse response)
            where TResponse : IKafkaResponse =>
            _responses.Enqueue(response);

        public IReadOnlyList<T> RequestsOfType<T>() =>
            _requests.ToArray().OfType<T>().ToArray();

        public async Task<IReadOnlyList<T>> WaitForRequestsAsync<T>(int count, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            while (true)
            {
                var requests = RequestsOfType<T>();
                if (requests.Count >= count)
                {
                    return requests;
                }

                try
                {
                    await _requestSignal.WaitAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException($"Timed out waiting for {count} {typeof(T).Name} request(s).");
                }
            }
        }

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            _requests.Enqueue(request);
            _requestSignal.Release();

            if (!_responses.TryDequeue(out var response))
            {
                if (typeof(TResponse) == typeof(PushTelemetryResponse))
                {
                    response = new PushTelemetryResponse { ErrorCode = ErrorCode.None };
                }
                else
                {
                    throw new InvalidOperationException($"No queued response for {typeof(TRequest).Name}.");
                }
            }

            return ValueTask.FromResult((TResponse)response);
        }

        public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse =>
            ValueTask.CompletedTask;

        public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse =>
            Task.FromResult(default(TResponse)!);

        public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse =>
            ValueTask.CompletedTask;

        public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse =>
            Task.FromResult(default(TResponse)!);

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            _requestSignal.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
