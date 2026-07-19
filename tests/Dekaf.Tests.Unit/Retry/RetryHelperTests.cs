using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Retry;
using NSubstitute;

namespace Dekaf.Tests.Unit.Retry;

public sealed class RetryHelperTests
{
    [Test]
    public async Task MetadataRefreshUnavailable_RetriesOriginalOperation()
    {
        await using var metadataManager = CreateUnavailableMetadataManager();
        var attempts = 0;

        var result = await RetryHelper.WithRetryAsync(
            () => Interlocked.Increment(ref attempts) == 1
                ? ValueTask.FromException<int>(CreateRequestTimeout())
                : ValueTask.FromResult(42),
            metadataManager,
            CancellationToken.None,
            retryBackoffMs: 0,
            retryBackoffMaxMs: 0,
            maxRetries: 1);

        await Assert.That(result).IsEqualTo(42);
        await Assert.That(attempts).IsEqualTo(2);
    }

    [Test]
    public async Task MetadataRefreshUnavailable_PreservesFinalKafkaFailure()
    {
        await using var metadataManager = CreateUnavailableMetadataManager();
        var attempts = 0;

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await RetryHelper.WithRetryAsync<int>(
                () =>
                {
                    Interlocked.Increment(ref attempts);
                    return ValueTask.FromException<int>(CreateRequestTimeout());
                },
                metadataManager,
                CancellationToken.None,
                retryBackoffMs: 0,
                retryBackoffMaxMs: 0,
                maxRetries: 1));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.RequestTimedOut);
        await Assert.That(attempts).IsEqualTo(2);
    }

    [Test]
    public async Task TransportFailure_RetriesOriginalOperation()
    {
        await using var metadataManager = CreateUnavailableMetadataManager();
        var attempts = 0;

        var result = await RetryHelper.WithRetryAsync(
            () => Interlocked.Increment(ref attempts) == 1
                ? ValueTask.FromException<int>(new IOException("connection reset"))
                : ValueTask.FromResult(42),
            metadataManager,
            CancellationToken.None,
            retryBackoffMs: 0,
            retryBackoffMaxMs: 0,
            maxRetries: 1);

        await Assert.That(result).IsEqualTo(42);
        await Assert.That(attempts).IsEqualTo(2);
    }

    [Test]
    public async Task Success_ResetsFailureSequenceForNextOperation()
    {
        await using var metadataManager = CreateUnavailableMetadataManager();
        var failureCounts = new List<int>();
        var attempts = 0;
        int CalculateDelay(int initialDelayMs, int maximumDelayMs, int failureCount)
        {
            _ = initialDelayMs;
            _ = maximumDelayMs;
            failureCounts.Add(failureCount);
            return 0;
        }

        for (var invocation = 0; invocation < 2; invocation++)
        {
            attempts = 0;
            await RetryHelper.WithRetryAsync(
                () => Interlocked.Increment(ref attempts) == 1
                    ? ValueTask.FromException(CreateRequestTimeout())
                    : ValueTask.CompletedTask,
                metadataManager,
                CancellationToken.None,
                retryBackoffMs: 100,
                retryBackoffMaxMs: 1000,
                maxRetries: 1,
                calculateDelayMilliseconds: CalculateDelay);
        }

        await Assert.That(failureCounts).IsEquivalentTo([1, 1]);
    }

    [Test]
    public async Task CancellationDuringBackoff_StopsRetrying()
    {
        await using var metadataManager = CreateUnavailableMetadataManager();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        var attempts = 0;

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await RetryHelper.WithRetryAsync(
                () =>
                {
                    Interlocked.Increment(ref attempts);
                    return ValueTask.FromException(CreateRequestTimeout());
                },
                metadataManager,
                cancellation.Token,
                retryBackoffMs: 1000,
                retryBackoffMaxMs: 1000,
                maxRetries: 3));

        await Assert.That(attempts).IsEqualTo(1);
    }

    private static MetadataManager CreateUnavailableMetadataManager()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        connectionPool.GetConnectionAsync(
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException<IKafkaConnection>(new TimeoutException("broker unavailable")));

        return new MetadataManager(
            connectionPool,
            ["localhost:9092"],
            new MetadataOptions
            {
                EnableBackgroundRefresh = false,
                MetadataRecoveryStrategy = MetadataRecoveryStrategy.None
            });
    }

    private static KafkaException CreateRequestTimeout() =>
        new(ErrorCode.RequestTimedOut, "request timed out");
}
