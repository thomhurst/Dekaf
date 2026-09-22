using System.Net.Sockets;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// The pattern-subscription refresh is best-effort and runs on the foreground poll, so a
/// cluster-wide blip must not throw a transport failure out of the consume call.
/// </summary>
public sealed class ConsumerPatternSubscriptionRefreshTests
{
    [Test]
    public async Task FilterRefresh_MetadataUnreachableOnce_PollSurvivesAndRetriesAfterBackoff()
    {
        var pool = Substitute.For<IConnectionPool>();
        var connection = CreateMetadataConnection("orders-eu", "orders-us", "payments");
        var attempts = 0;
        pool.GetConnectionAsync("localhost", 9092, Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref attempts) == 1
                ? throw new SocketException((int)SocketError.ConnectionRefused)
                : new ValueTask<IKafkaConnection>(connection));

        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager);
        Func<string, bool> filter = static topic => topic.StartsWith("orders-", StringComparison.Ordinal);
        consumer.Subscribe(filter);

        // No broker reachable: the refresh reports no change instead of throwing.
        await Assert.That(await InvokeRefreshFilteredTopicsAsync(consumer, filter)).IsFalse();
        await Assert.That(consumer.Subscription.Count).IsEqualTo(0);

        // Without an established subscription the next attempt is due after the retry backoff
        // (1 ms here), not after the 30 s refresh interval.
        while (!await InvokeRefreshFilteredTopicsAsync(consumer, filter))
            await Task.Yield();

        await Assert.That(consumer.Subscription).Contains("orders-eu");
        await Assert.That(consumer.Subscription).Contains("orders-us");
        await Assert.That(consumer.Subscription).DoesNotContain("payments");
    }

    [Test]
    public async Task FilterRefresh_AuthenticationFailure_Propagates()
    {
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync("localhost", 9092, Arg.Any<CancellationToken>())
            .Throws(new AuthenticationException("SASL authentication failed"));

        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager);
        Func<string, bool> filter = static _ => true;
        consumer.Subscribe(filter);

        await Assert.That(async () => await InvokeRefreshFilteredTopicsAsync(consumer, filter))
            .Throws<AuthenticationException>();
    }

    [Test]
    public async Task FilterRefresh_FailureRacingCallerCancellation_SurfacesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync("localhost", 9092, Arg.Any<CancellationToken>())
            .Returns<ValueTask<IKafkaConnection>>(_ =>
            {
                // The socket reports its failure after the caller already cancelled.
                cancellation.Cancel();
                throw new SocketException((int)SocketError.ConnectionReset);
            });

        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager);
        Func<string, bool> filter = static _ => true;
        consumer.Subscribe(filter);

        await Assert.That(async () => await InvokeRefreshFilteredTopicsAsync(consumer, filter, cancellation.Token))
            .Throws<OperationCanceledException>();
    }

    private static IKafkaConnection CreateMetadataConnection(params string[] topics)
    {
        var connection = Substitute.For<IKafkaConnection>();
        connection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<MetadataResponse>(new MetadataResponse
            {
                ClusterId = "test-cluster",
                ControllerId = 1,
                Brokers = [new BrokerMetadata { NodeId = 1, Host = "broker-1", Port = 9093 }],
                Topics =
                [
                    .. topics.Select(static topic => new TopicMetadata
                    {
                        ErrorCode = ErrorCode.None,
                        Name = topic,
                        Partitions =
                        [
                            new PartitionMetadata
                            {
                                ErrorCode = ErrorCode.None,
                                PartitionIndex = 0,
                                LeaderId = 1,
                                LeaderEpoch = 0,
                                ReplicaNodes = [1],
                                IsrNodes = [1]
                            }
                        ]
                    })
                ]
            }));
        return connection;
    }

    private static MetadataManager CreateMetadataManager(IConnectionPool pool)
    {
        var metadataManager = new MetadataManager(pool, ["localhost:9092"]);
        var field = typeof(MetadataManager)
            .GetField("_metadataApiVersion", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_metadataApiVersion field not found");
        field.SetValue(metadataManager, MetadataRequest.HighestSupportedVersion);
        return metadataManager;
    }

    private static KafkaConsumer<string, string> CreateConsumer(IConnectionPool pool, MetadataManager metadataManager)
        => new(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                ClientId = "test-consumer",
                RetryBackoffMs = 1,
                RetryBackoffMaxMs = 1
            },
            Serializers.String,
            Serializers.String,
            pool,
            metadataManager);

    private static async ValueTask<bool> InvokeRefreshFilteredTopicsAsync(
        KafkaConsumer<string, string> consumer,
        Func<string, bool> filter,
        CancellationToken cancellationToken = default)
    {
        var method = typeof(KafkaConsumer<string, string>)
            .GetMethod("RefreshFilteredTopicsAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("RefreshFilteredTopicsAsync method not found");

        try
        {
            return await ((ValueTask<bool>)method.Invoke(consumer, [filter, cancellationToken])!).ConfigureAwait(false);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
}
