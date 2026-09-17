using System.Diagnostics;
using System.Net.Sockets;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// The first produce to an uncached topic waits on a metadata fetch bounded by max.block.ms.
/// A cluster that resets or refuses connections during that fetch must be retried for the
/// whole budget and then reported as a produce timeout that names the transport cause, not
/// surfaced immediately as the metadata manager's raw exception.
/// </summary>
[Timeout(15_000)]
public sealed class ProducerMetadataResilienceTests
{
    private static readonly MetadataOptions FastRetryMetadataOptions = new()
    {
        EnableBackgroundRefresh = false,
        RetryBackoffMs = 1,
        RetryBackoffMaxMs = 5
    };

    [Test]
    public async Task ProduceAsync_ClusterUnreachableDuringMetadataFetch_ThrowsProduceExceptionAfterMaxBlock(
        CancellationToken cancellationToken)
    {
        await using var harness = CreateHarness(maxBlockMs: 300);
        harness.Connect = (_, _) => ValueTask.FromException<IKafkaConnection>(
            new SocketException((int)SocketError.ConnectionRefused));
        await harness.Producer.InitializeAsync(cancellationToken);

        var exception = await Assert.That(() => harness.Producer.ProduceAsync(
                new ProducerMessage<string, string> { Topic = "orders", Key = "key", Value = "value" },
                cancellationToken).AsTask())
            .Throws<ProduceException>();

        await Assert.That(exception!.Topic).IsEqualTo("orders");
        await Assert.That(exception.InnerException).IsTypeOf<SocketException>();
        await Assert.That(harness.ConnectionAttempts.Count).IsGreaterThan(1);
    }

    [Test]
    public async Task ProduceAsync_UnresolvedBootstrapHostnameDuringMetadataFetch_ThrowsProduceExceptionAfterMaxBlock(
        CancellationToken cancellationToken)
    {
        // The harness has never completed a refresh, so a DNS miss on the bootstrap endpoint is
        // the "bootstrap resolution pending" state that the public refresh reports as fatal.
        await using var harness = CreateHarness(maxBlockMs: 300);
        harness.Connect = (_, _) => ValueTask.FromException<IKafkaConnection>(
            new DnsResolutionException("localhost", 9092, new SocketException((int)SocketError.HostNotFound)));
        await harness.Producer.InitializeAsync(cancellationToken);

        var exception = await Assert.That(() => harness.Producer.ProduceAsync(
                new ProducerMessage<string, string> { Topic = "orders", Key = "key", Value = "value" },
                cancellationToken).AsTask())
            .Throws<ProduceException>();

        await Assert.That(exception!.Topic).IsEqualTo("orders");
        await Assert.That(exception.InnerException).IsTypeOf<DnsResolutionException>();
        await Assert.That(harness.ConnectionAttempts.Count).IsGreaterThan(1);
    }

    [Test]
    public async Task FireAsync_ClusterUnreachableDuringMetadataFetch_ThrowsMetadataTimeoutWithCause(
        CancellationToken cancellationToken)
    {
        await using var harness = CreateHarness(maxBlockMs: 300);
        harness.Connect = (_, _) => ValueTask.FromException<IKafkaConnection>(
            new IOException("Connection reset by peer"));
        await harness.Producer.InitializeAsync(cancellationToken);

        var exception = await Assert.That(() => harness.Producer.FireAsync(
                new ProducerMessage<string, string> { Topic = "orders", Key = "key", Value = "value" }).AsTask())
            .Throws<KafkaTimeoutException>();

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Metadata);
        await Assert.That(exception.InnerException).IsTypeOf<IOException>();
    }

    [Test]
    public async Task ProduceAsync_AuthenticationFailureDuringMetadataFetch_PropagatesWithoutWaiting(
        CancellationToken cancellationToken)
    {
        await using var harness = CreateHarness(maxBlockMs: 10_000);
        harness.Connect = (_, _) => ValueTask.FromException<IKafkaConnection>(
            new AuthenticationException("Invalid credentials"));
        await harness.Producer.InitializeAsync(cancellationToken);
        var startedAt = Stopwatch.GetTimestamp();

        await Assert.That(() => harness.Producer.ProduceAsync(
                new ProducerMessage<string, string> { Topic = "orders", Key = "key", Value = "value" },
                cancellationToken).AsTask())
            .Throws<AuthenticationException>();

        // Fatal failures are not retried for the max.block.ms budget.
        await Assert.That(Stopwatch.GetElapsedTime(startedAt)).IsLessThan(TimeSpan.FromSeconds(5));
    }

    private static ProducerInitializationHarness CreateHarness(int maxBlockMs) => new(
        maxBlockMs: maxBlockMs,
        idempotent: false,
        metadataOptions: FastRetryMetadataOptions);
}
