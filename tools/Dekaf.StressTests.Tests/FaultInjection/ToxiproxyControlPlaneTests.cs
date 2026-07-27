using System.Net.Http;
using Dekaf.StressTests.FaultInjection;
using Newtonsoft.Json;

namespace Dekaf.StressTests.Tests.FaultInjection;

public class ToxiproxyControlPlaneTests
{
    [Test]
    public async Task ExecuteAsync_RetriesJsonReaderExceptionUntilSuccess()
    {
        var attempts = 0;

        await FaultInjectionKafkaEnvironment.ExecuteToxiproxyControlPlaneAsync(
            "reset proxies",
            () =>
            {
                attempts++;
                if (attempts < 3)
                {
                    throw new JsonReaderException("invalid response");
                }

                return Task.CompletedTask;
            },
            CancellationToken.None,
            retryDelay: TimeSpan.Zero);

        await Assert.That(attempts).IsEqualTo(3);
    }

    [Test]
    public async Task ExecuteAsync_RetriesHttpRequestExceptionUntilSuccess()
    {
        var attempts = 0;

        await FaultInjectionKafkaEnvironment.ExecuteToxiproxyControlPlaneAsync(
            "add toxic",
            () =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new HttpRequestException("control API unavailable");
                }

                return Task.CompletedTask;
            },
            CancellationToken.None,
            retryDelay: TimeSpan.Zero);

        await Assert.That(attempts).IsEqualTo(2);
    }

    [Test]
    public async Task ExecuteAsync_ExhaustedRetriesThrowsInfrastructureError()
    {
        var attempts = 0;

        var exception = await Assert.ThrowsAsync<ToxiproxyControlPlaneException>(() =>
            FaultInjectionKafkaEnvironment.ExecuteToxiproxyControlPlaneAsync(
                "reset proxies",
                () =>
                {
                    attempts++;
                    throw new JsonReaderException("invalid response");
                },
                CancellationToken.None,
                retryDelay: TimeSpan.Zero));

        await Assert.That(attempts).IsEqualTo(3);
        await Assert.That(exception!.Message).Contains("harness infrastructure");
        await Assert.That(exception.Message).Contains("reset proxies");
        await Assert.That(exception.InnerException).IsTypeOf<JsonReaderException>();
    }

    [Test]
    public async Task ExecuteAsync_DoesNotRetryNonTransientFailure()
    {
        var attempts = 0;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FaultInjectionKafkaEnvironment.ExecuteToxiproxyControlPlaneAsync(
                "reset proxies",
                () =>
                {
                    attempts++;
                    throw new InvalidOperationException("bad test setup");
                },
                CancellationToken.None,
                retryDelay: TimeSpan.Zero));

        await Assert.That(attempts).IsEqualTo(1);
        await Assert.That(exception!.Message).IsEqualTo("bad test setup");
    }
}
