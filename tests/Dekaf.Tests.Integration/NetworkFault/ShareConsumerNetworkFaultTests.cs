using Dekaf.Producer;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Integration.NetworkFault;

/// <summary>
/// Share consumer behaviour when its connections are reset. The broker drops a share session when
/// the connection that holds it closes, so every reset forces the client to open a new session.
/// Faults go through the consumer proxy only; records are seeded through the producer proxy.
/// </summary>
[ClassDataSource<ShareFaultKafkaContainer>(Shared = SharedType.PerTestSession)]
[Category("NetworkPartition")]
[NotInParallel("ShareFaultKafkaContainer")]
public sealed class ShareConsumerNetworkFaultTests(ShareFaultKafkaContainer kafka)
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(3);

    [Test]
    public async Task ConnectionReset_WithAcknowledgementsPending_OpensANewSessionAndKeepsConsuming()
    {
        using var testTimeout = new CancellationTokenSource(TestTimeout);
        var cancellationToken = testTimeout.Token;
        var topic = await kafka.CreateTestTopicAsync(partitions: 1);

        using var handshakes = new ConnectionHandshakeObserver();
        using var loggerFactory = handshakes.CreateLoggerFactory();

        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(kafka.ConsumerBootstrapServers)
            .WithGroupId($"network-fault-share-{Guid.NewGuid():N}")
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit)
            .WithRequestTimeoutMs(5_000)
            .WithConnectionTimeout(TimeSpan.FromSeconds(2))
            .WithLoggerFactory(loggerFactory)
            .BuildAsync(cancellationToken);
        consumer.Subscribe(topic);

        // The share-partition start offset is set by the first ShareFetch, so records produced
        // before the consumer has fetched once would sit outside its acquisition window.
        await ShareConsumerTestHelper.PrimeShareConsumerAsync(consumer);

        await SeedAsync(topic, "before", 5, cancellationToken);

        // Their acknowledgements stay queued on the session that the reset is about to destroy.
        await PollUntilAsync(consumer, "before-", 5, cancellationToken);

        try
        {
            var handshakesBeforeFault = handshakes.Handshakes;
            await kafka.AddResetPeerAsync(ToxiproxyLane.Consumer, cancellationToken);

            // The commit writes its ShareAcknowledge into the reset. Whether it reports a
            // failure or runs out of time, the acknowledgements must survive for the new session.
            using (var commitTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                commitTimeout.CancelAfter(TimeSpan.FromSeconds(10));
                try
                {
                    await consumer.CommitAsync(commitTimeout.Token);
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                }
            }

            // A handshake after the injection proves the reset reached this client's connections.
            await handshakes.WaitForHandshakeAfterAsync(handshakesBeforeFault, cancellationToken);
        }
        finally
        {
            await kafka.HealNetworkFaultsAsync(CancellationToken.None);
        }

        // The same consumer instance must open a new session and fetch again. A client that kept
        // the dead session's epoch, or sent its pending acknowledgements on the request that opens
        // the new session, is rejected on every poll and never sees these records.
        await SeedAsync(topic, "after", 5, cancellationToken);
        await PollUntilAsync(consumer, "after-", 5, cancellationToken);

        await consumer.CommitAsync(cancellationToken);
        await consumer.CloseAsync(cancellationToken);
    }

    /// <summary>
    /// Polls and acknowledges records, without committing, until <paramref name="count"/> distinct
    /// records whose value starts with <paramref name="prefix"/> have arrived. Records from before
    /// a fault may be redelivered once their acquisition locks expire; those are acknowledged again.
    /// </summary>
    private static async Task PollUntilAsync(
        IKafkaShareConsumer<string, string> consumer,
        string prefix,
        int count,
        CancellationToken cancellationToken)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));
        try
        {
            await foreach (var record in consumer.PollAsync(timeout.Token))
            {
                consumer.Acknowledge(record);
                if (record.Value.StartsWith(prefix, StringComparison.Ordinal)
                    && seen.Add(record.Value)
                    && seen.Count == count)
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }

        throw new TimeoutException(
            $"Received {seen.Count} of {count} '{prefix}' records within 90 seconds: " +
            string.Join(',', seen));
    }

    private async Task SeedAsync(string topic, string label, int count, CancellationToken cancellationToken)
    {
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.ProducerBootstrapServers)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);

        for (var i = 0; i < count; i++)
            _ = await producer.ProduceAsync(topic, "key", $"{label}-{i}", cancellationToken);
    }
}
