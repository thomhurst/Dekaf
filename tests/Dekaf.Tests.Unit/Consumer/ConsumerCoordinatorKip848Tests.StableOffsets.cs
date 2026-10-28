using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Retry;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// KIP-447 stable offsets. Committed-offset fetches require stable offsets, so a pending
/// transactional offset commit is waited out instead of read back as the pre-transaction offset.
/// </summary>
public sealed partial class ConsumerCoordinatorKip848Tests
{
    [Test]
    public async Task FetchOffsetsAsync_UnstableOffsetCommit_RetriesPastTheRequestRetryCountUntilStable()
    {
        _metadataManager.SetApiVersion(ApiKey.OffsetFetch, 9, 9);
        var script = new HeartbeatScript(this);
        var (listener, _) = CreateRecordingListener();
        await using var coordinator = await JoinAsync(script, listener);

        const int unstableResponses = RetryHelper.MaxRetries + 3;
        var requests = 0;
        var requireStable = true;
        _connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                requireStable &= call.Arg<OffsetFetchRequest>().RequireStable;
                return ValueTask.FromResult(OffsetFetchResponseFor(
                    Interlocked.Increment(ref requests) <= unstableResponses
                        ? ErrorCode.UnstableOffsetCommit
                        : ErrorCode.None));
            });

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var offsets = await coordinator.FetchOffsetsAsync([new TopicPartition("test-topic", 0)], timeout.Token);

        await Assert.That(Volatile.Read(ref requests)).IsEqualTo(unstableResponses + 1);
        await Assert.That(requireStable).IsTrue();
        await Assert.That(offsets[new TopicPartition("test-topic", 0)].Offset).IsEqualTo(42);
        await Assert.That(GetPrivateField<SemaphoreSlim>(coordinator, "_fetchLock").CurrentCount).IsEqualTo(1);
    }

    [Test]
    public async Task FetchOffsetsAsync_UnstableOffsetCommitUntilTheDeadline_TimesOutWithTheUnstableCause()
    {
        _metadataManager.SetApiVersion(ApiKey.OffsetFetch, 9, 9);
        var script = new HeartbeatScript(this);
        var (listener, _) = CreateRecordingListener();
        await using var coordinator = await JoinAsync(script, listener, requestTimeoutMs: 200);

        _connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(OffsetFetchResponseFor(ErrorCode.UnstableOffsetCommit)));

        var exception = await Assert.ThrowsAsync<KafkaTimeoutException>(async () =>
            await coordinator.FetchOffsetsAsync([new TopicPartition("test-topic", 0)], CancellationToken.None));

        await Assert.That(exception!.InnerException).IsTypeOf<GroupException>();
        await Assert.That(((GroupException)exception.InnerException!).ErrorCode).IsEqualTo(ErrorCode.UnstableOffsetCommit);
        await Assert.That(GetPrivateField<SemaphoreSlim>(coordinator, "_fetchLock").CurrentCount).IsEqualTo(1);
    }

    [Test]
    public async Task FetchOffsetsAsync_DeadlineDuringTheRequestAfterAnUnstableOffset_TimesOutWithTheUnstableCause()
    {
        _metadataManager.SetApiVersion(ApiKey.OffsetFetch, 9, 9);
        var script = new HeartbeatScript(this);
        var (listener, _) = CreateRecordingListener();
        await using var coordinator = await JoinAsync(script, listener, requestTimeoutMs: 200);

        // The first fetch reports a pending transaction; the next pass is still running at the
        // deadline, so the timeout cannot take its cause from the unstable-offset wait.
        var requests = 0;
        _connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Interlocked.Increment(ref requests) == 1
                ? ValueTask.FromResult(OffsetFetchResponseFor(ErrorCode.UnstableOffsetCommit))
                : NeverAnswersAsync(call.Arg<CancellationToken>()));

        var exception = await Assert.ThrowsAsync<KafkaTimeoutException>(async () =>
            await coordinator.FetchOffsetsAsync([new TopicPartition("test-topic", 0)], CancellationToken.None));

        await Assert.That(exception!.InnerException).IsTypeOf<GroupException>();
        await Assert.That(((GroupException)exception.InnerException!).ErrorCode).IsEqualTo(ErrorCode.UnstableOffsetCommit);
        await Assert.That(GetPrivateField<SemaphoreSlim>(coordinator, "_fetchLock").CurrentCount).IsEqualTo(1);
    }

    private static async ValueTask<OffsetFetchResponse> NeverAnswersAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new InvalidOperationException("Unreachable: the delay only ends by cancellation.");
    }

    private static OffsetFetchResponse OffsetFetchResponseFor(ErrorCode partitionErrorCode) => new()
    {
        Groups =
        [
            new OffsetFetchResponseGroup
            {
                GroupId = "test-group",
                ErrorCode = ErrorCode.None,
                Topics =
                [
                    new OffsetFetchResponseTopic
                    {
                        Name = "test-topic",
                        Partitions =
                        [
                            new OffsetFetchResponsePartition
                            {
                                PartitionIndex = 0,
                                CommittedOffset = partitionErrorCode == ErrorCode.None ? 42 : -1,
                                ErrorCode = partitionErrorCode
                            }
                        ]
                    }
                ]
            }
        ]
    };
}
