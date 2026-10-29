using System.Reflection;
using Dekaf.Admin;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Telemetry;

namespace Dekaf.Tests.Integration;

public sealed partial class ClientTelemetryReceiverIntegrationTests
{
    [Test]
    public async Task AdminDisposeAsync_DuringTelemetrySubscription_CancelsStartup(CancellationToken cancellationToken)
    {
        var clientId = $"telemetry-stop-{Guid.NewGuid():N}";
        var options = new ConnectionOptions { RequestTimeout = TimeSpan.FromSeconds(30) };
        var subscriptionSeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSubscription = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var pool = new ConnectionPool(clientId, options, 1,
            async (brokerId, host, port, _, token) =>
            {
                var connection = new KafkaConnection(brokerId, host, port, clientId, options);
                await connection.ConnectAsync(token);
                return new GatedSubscriptionConnection(connection, subscriptionSeen, releaseSubscription.Task);
            });
        await using var metadata = new MetadataManager(pool, [kafka.BootstrapServers]);
        await metadata.InitializeAsync(cancellationToken);
        await using var admin = new AdminClient(new AdminClientOptions
        {
            BootstrapServers = [kafka.BootstrapServers],
            ClientId = clientId
        }, pool, metadata);
        var manager = (ClientTelemetryManager)typeof(AdminClient)
            .GetField("_telemetryManager", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(admin)!;

        var startTask = manager.StartAsync(CancellationToken.None).AsTask();
        try
        {
            // The broker has answered; hold delivery of that response until shutdown cancels it.
            await subscriptionSeen.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            await admin.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            await startTask.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            await Assert.That(manager.IsStarted).IsFalse();
            await Assert.That(admin.ClientInstanceId).IsNull();
        }
        finally
        {
            releaseSubscription.TrySetResult();
            await startTask.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        }
    }

    private sealed class GatedSubscriptionConnection(
        IKafkaConnection inner,
        TaskCompletionSource subscriptionSeen,
        Task releaseSubscription) : IKafkaConnection
    {
        public int BrokerId => inner.BrokerId;
        public string Host => inner.Host;
        public int Port => inner.Port;
        public bool IsConnected => inner.IsConnected;

        public async ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            var response = await inner.SendAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);
            if (request is GetTelemetrySubscriptionsRequest)
            {
                subscriptionSeen.TrySetResult();
                await releaseSubscription.WaitAsync(cancellationToken);
            }

            return response;
        }

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default) => inner.ConnectAsync(cancellationToken);
        public ValueTask DisposeAsync() => inner.DisposeAsync();

        public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(
            TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse =>
            inner.SendFireAndForgetAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);

        public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(
            TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse =>
            inner.SendPipelinedAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);

        public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse =>
            inner.SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);

        public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse =>
            inner.SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);
    }
}
