using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Tests for consumer error paths: error code context, subscribe validation,
/// post-dispose behavior, and retriability mapping for consumer-specific codes.
/// </summary>
public class ConsumerErrorTests
{
    #region ConsumeException Error Code Context Tests

    [Test]
    public async Task ConsumeException_CarriesErrorCode_OffsetOutOfRange()
    {
        var ex = new ConsumeException(ErrorCode.OffsetOutOfRange, "offset out of range");

        await Assert.That(ex.ErrorCode).IsEqualTo(ErrorCode.OffsetOutOfRange);
        await Assert.That(ex.Message).IsEqualTo("offset out of range");
    }

    [Test]
    public async Task ConsumeException_CarriesErrorCode_UnknownTopicOrPartition()
    {
        var ex = new ConsumeException(ErrorCode.UnknownTopicOrPartition, "topic not found");

        await Assert.That(ex.ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
    }

    [Test]
    public async Task ConsumeException_CarriesErrorCode_NotLeaderOrFollower()
    {
        var ex = new ConsumeException(ErrorCode.NotLeaderOrFollower, "not leader for partition");

        await Assert.That(ex.ErrorCode).IsEqualTo(ErrorCode.NotLeaderOrFollower);
    }

    [Test]
    public async Task ConsumeException_WithInnerException_PreservesContext()
    {
        var inner = new TimeoutException("fetch timed out");
        var ex = new ConsumeException("consume failed", inner);

        await Assert.That(ex.InnerException).IsSameReferenceAs(inner);
        await Assert.That(ex.Message).IsEqualTo("consume failed");
    }

    [Test]
    [MethodDataSource(nameof(ConsumerSpecificErrorCodes))]
    public async Task ConsumeException_WithVariousErrorCodes_CarriesCorrectCode(ErrorCode errorCode)
    {
        var ex = new ConsumeException(errorCode, $"Error: {errorCode}");

        await Assert.That(ex.ErrorCode).IsEqualTo(errorCode);
    }

    public static IEnumerable<ErrorCode> ConsumerSpecificErrorCodes()
    {
        yield return ErrorCode.OffsetOutOfRange;
        yield return ErrorCode.CorruptMessage;
        yield return ErrorCode.UnknownTopicOrPartition;
        yield return ErrorCode.NotLeaderOrFollower;
        yield return ErrorCode.RequestTimedOut;
        yield return ErrorCode.NetworkException;
        yield return ErrorCode.CoordinatorLoadInProgress;
        yield return ErrorCode.CoordinatorNotAvailable;
        yield return ErrorCode.NotCoordinator;
        yield return ErrorCode.TopicAuthorizationFailed;
        yield return ErrorCode.FencedLeaderEpoch;
        yield return ErrorCode.UnknownLeaderEpoch;
    }

    #endregion

    #region Subscribe After Dispose Tests

    [Test]
    public async Task ConsumeAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        await consumer.DisposeAsync();

        await Assert.That(async () =>
        {
            await foreach (var _ in consumer.ConsumeAsync())
            {
                break;
            }
        }).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task InitializeAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        await consumer.DisposeAsync();

        await Assert.That(async () =>
        {
            await consumer.InitializeAsync();
        }).Throws<ObjectDisposedException>();
    }

    #endregion

    #region Consumer Error Code Retriability Mapping Tests

    [Test]
    [MethodDataSource(nameof(RetriableConsumerErrorCodes))]
    public async Task ConsumerErrorCode_Retriable_IsRetriableReturnsTrue(ErrorCode errorCode)
    {
        await Assert.That(errorCode.IsRetriable()).IsTrue();
    }

    [Test]
    [MethodDataSource(nameof(NonRetriableConsumerErrorCodes))]
    public async Task ConsumerErrorCode_NonRetriable_IsRetriableReturnsFalse(ErrorCode errorCode)
    {
        await Assert.That(errorCode.IsRetriable()).IsFalse();
    }

    public static IEnumerable<ErrorCode> RetriableConsumerErrorCodes()
    {
        // Error codes that commonly occur during consumption and are retriable
        yield return ErrorCode.CorruptMessage;
        yield return ErrorCode.UnknownTopicOrPartition;
        yield return ErrorCode.LeaderNotAvailable;
        yield return ErrorCode.NotLeaderOrFollower;
        yield return ErrorCode.RequestTimedOut;
        yield return ErrorCode.ReplicaNotAvailable;
        yield return ErrorCode.NetworkException;
        yield return ErrorCode.CoordinatorLoadInProgress;
        yield return ErrorCode.CoordinatorNotAvailable;
        yield return ErrorCode.NotCoordinator;
        yield return ErrorCode.FencedLeaderEpoch;
        yield return ErrorCode.UnknownLeaderEpoch;
        yield return ErrorCode.OffsetNotAvailable;
        yield return ErrorCode.UnstableOffsetCommit;
    }

    public static IEnumerable<ErrorCode> NonRetriableConsumerErrorCodes()
    {
        // Error codes that commonly occur during consumption and are NOT retriable
        yield return ErrorCode.OffsetOutOfRange;
        yield return ErrorCode.TopicAuthorizationFailed;
        yield return ErrorCode.GroupAuthorizationFailed;
        yield return ErrorCode.InvalidGroupId;
        yield return ErrorCode.InvalidSessionTimeout;
        yield return ErrorCode.InvalidTopicException;
        yield return ErrorCode.UnsupportedVersion;
    }

    #endregion

    #region ConsumeException Inherits KafkaException Tests

    [Test]
    public async Task ConsumeException_IsRetriable_DelegatesToErrorCode()
    {
        var retriable = new ConsumeException(ErrorCode.RequestTimedOut, "timed out");
        var nonRetriable = new ConsumeException(ErrorCode.OffsetOutOfRange, "bad offset");

        await Assert.That(retriable.IsRetriable).IsTrue();
        await Assert.That(nonRetriable.IsRetriable).IsFalse();
    }

    [Test]
    public async Task ConsumeException_WithoutErrorCode_IsNotRetriable()
    {
        var ex = new ConsumeException("generic failure");

        await Assert.That(ex.IsRetriable).IsFalse();
        await Assert.That(ex.ErrorCode).IsNull();
    }

    #endregion

    #region GroupException Consumer-Specific Tests

    [Test]
    public async Task GroupException_RebalanceInProgress_IsNotRetriable()
    {
        // RebalanceInProgress is not in the retriable error code set;
        // the client must rejoin the group rather than retry the same request.
        var ex = new GroupException(ErrorCode.RebalanceInProgress, "rebalancing");

        await Assert.That(ex.IsRetriable).IsFalse();
    }

    [Test]
    public async Task GroupException_InvalidGroupId_IsNotRetriable()
    {
        var ex = new GroupException(ErrorCode.InvalidGroupId, "bad group id");

        await Assert.That(ex.IsRetriable).IsFalse();
    }

    [Test]
    public async Task GroupException_CarriesGroupId()
    {
        var ex = new GroupException(ErrorCode.RebalanceInProgress, "rebalancing")
        {
            GroupId = "my-consumer-group"
        };

        await Assert.That(ex.GroupId).IsEqualTo("my-consumer-group");
        await Assert.That(ex.ErrorCode).IsEqualTo(ErrorCode.RebalanceInProgress);
    }

    [Test]
    [MethodDataSource(nameof(GroupSpecificErrorCodes))]
    public async Task GroupException_WithGroupErrorCodes_CarriesCorrectCode(ErrorCode errorCode)
    {
        var ex = new GroupException(errorCode, $"Group error: {errorCode}")
        {
            GroupId = "test-group"
        };

        await Assert.That(ex.ErrorCode).IsEqualTo(errorCode);
        await Assert.That(ex.GroupId).IsEqualTo("test-group");
    }

    public static IEnumerable<ErrorCode> GroupSpecificErrorCodes()
    {
        yield return ErrorCode.CoordinatorLoadInProgress;
        yield return ErrorCode.CoordinatorNotAvailable;
        yield return ErrorCode.NotCoordinator;
        yield return ErrorCode.IllegalGeneration;
        yield return ErrorCode.InconsistentGroupProtocol;
        yield return ErrorCode.InvalidGroupId;
        yield return ErrorCode.UnknownMemberId;
        yield return ErrorCode.InvalidSessionTimeout;
        yield return ErrorCode.RebalanceInProgress;
        yield return ErrorCode.GroupAuthorizationFailed;
        yield return ErrorCode.MemberIdRequired;
        yield return ErrorCode.FencedInstanceId;
        yield return ErrorCode.GroupMaxSizeReached;
    }

    #endregion
}
