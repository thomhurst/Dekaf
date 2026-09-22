using System.Diagnostics;

namespace Dekaf.Tests.Integration.NetworkFault;

/// <summary>
/// Client disposal while a real connection setup is stuck in its handshake: the proxy accepts the
/// TCP connection and forwards the ApiVersions request, but no response comes back.
/// </summary>
[ClassDataSource<TransactionFaultKafkaContainer>(Shared = SharedType.PerTestSession)]
[Category("NetworkPartition")]
[NotInParallel("TransactionFaultKafkaContainer")]
public sealed class ConnectionSetupNetworkFaultTests(TransactionFaultKafkaContainer kafka)
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(3);

    // Far longer than the bound asserted below, so only disposal can end the setup in time.
    private static readonly TimeSpan SetupTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan DisposalBound = TimeSpan.FromSeconds(10);

    [Test]
    public async Task AdminDisposeAsync_DuringABlackHoledHandshake_EndsTheSetupAndThePendingCallPromptly()
    {
        using var testTimeout = new CancellationTokenSource(TestTimeout);
        var cancellationToken = testTimeout.Token;
        using var handshakes = new ConnectionHandshakeObserver();
        using var loggerFactory = handshakes.CreateLoggerFactory();

        var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(kafka.ProducerBootstrapServers)
            .WithConnectionTimeout(SetupTimeout)
            .WithConnectionTimeoutMax(SetupTimeout)
            .WithLoggerFactory(loggerFactory)
            .Build();
        var disposed = false;

        try
        {
            // Bootstrap metadata while the lane is healthy, so the metadata manager's own
            // initialisation (which disposal cancels separately) is out of the picture.
            _ = await admin.ListConsumerGroupsAsync(cancellationToken: cancellationToken);

            // Kill the established connection: a request through the reset toxic ends it (and any
            // reconnect) on the wire. Then black-hole the lane, so the next request needs a new
            // connection whose handshake never gets an answer.
            await kafka.AddResetPeerAsync(ToxiproxyLane.Producer, cancellationToken);
            using (var resetRequest = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                resetRequest.CancelAfter(TimeSpan.FromSeconds(5));
                try
                {
                    _ = await admin.ListConsumerGroupsAsync(cancellationToken: resetRequest.Token);
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    // Expected: the connection was reset.
                }
            }

            await kafka.HealNetworkFaultsAsync(cancellationToken);
            await kafka.AddTimeoutAsync(ToxiproxyLane.Producer, cancellationToken);
            var baseline = handshakes.Handshakes;

            // No token of its own, like the admin and metadata paths that pass CancellationToken.None.
            var pending = admin.ListConsumerGroupsAsync(cancellationToken: CancellationToken.None).AsTask();
            await handshakes.WaitForHandshakeAfterAsync(baseline, cancellationToken);

            var stopwatch = Stopwatch.StartNew();
            await admin.DisposeAsync().AsTask().WaitAsync(DisposalBound, cancellationToken);
            disposed = true;

            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await pending.WaitAsync(DisposalBound, cancellationToken));

            await Assert.That(stopwatch.Elapsed).IsLessThan(DisposalBound);
        }
        finally
        {
            await kafka.HealNetworkFaultsAsync(CancellationToken.None);
            if (!disposed)
                await admin.DisposeAsync();
        }
    }
}
