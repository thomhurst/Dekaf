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
                maxRetries: 1));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.RequestTimedOut);
        await Assert.That(attempts).IsEqualTo(2);
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
