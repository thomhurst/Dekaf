using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Errors;

public class ErrorCodeTests
{
    #region Numeric Value Tests

    [Test]
    [MethodDataSource(nameof(ErrorCodeNumericValues))]
    public async Task ErrorCode_HasExpectedNumericValue(ErrorCode errorCode, short expectedValue)
    {
        var actualValue = (short)errorCode;
        await Assert.That(actualValue).IsEqualTo(expectedValue);
    }

    public static IEnumerable<(ErrorCode, short)> ErrorCodeNumericValues()
    {
        yield return (ErrorCode.UnknownServerError, -1);
        yield return (ErrorCode.None, 0);
        yield return (ErrorCode.OffsetOutOfRange, 1);
        yield return (ErrorCode.CorruptMessage, 2);
        yield return (ErrorCode.UnknownTopicOrPartition, 3);
        yield return (ErrorCode.LeaderNotAvailable, 5);
        yield return (ErrorCode.NotLeaderOrFollower, 6);
        yield return (ErrorCode.RequestTimedOut, 7);
        yield return (ErrorCode.NetworkException, 13);
        yield return (ErrorCode.TopicAlreadyExists, 36);
        yield return (ErrorCode.InvalidRequest, 42);
        yield return (ErrorCode.ThrottlingQuotaExceeded, 89);
    }

    #endregion

    #region IsRetriable Tests

    [Test]
    [MethodDataSource(nameof(RetriableErrorCodes))]
    public async Task IsRetriable_RetriableCode_ReturnsTrue(ErrorCode errorCode)
    {
        await Assert.That(errorCode.IsRetriable()).IsTrue();
    }

    [Test]
    [MethodDataSource(nameof(NonRetriableErrorCodes))]
    public async Task IsRetriable_NonRetriableCode_ReturnsFalse(ErrorCode errorCode)
    {
        await Assert.That(errorCode.IsRetriable()).IsFalse();
    }

    public static IEnumerable<ErrorCode> RetriableErrorCodes()
    {
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
        yield return ErrorCode.NotEnoughReplicas;
        yield return ErrorCode.NotEnoughReplicasAfterAppend;
        yield return ErrorCode.NotController;
        yield return ErrorCode.KafkaStorageError;
        yield return ErrorCode.FetchSessionIdNotFound;
        yield return ErrorCode.InvalidFetchSessionEpoch;
        yield return ErrorCode.FencedLeaderEpoch;
        yield return ErrorCode.UnknownLeaderEpoch;
        yield return ErrorCode.OffsetNotAvailable;
        yield return ErrorCode.PreferredLeaderNotAvailable;
        yield return ErrorCode.EligibleLeadersNotAvailable;
        yield return ErrorCode.UnstableOffsetCommit;
        yield return ErrorCode.ThrottlingQuotaExceeded;
    }

    public static IEnumerable<ErrorCode> NonRetriableErrorCodes()
    {
        yield return ErrorCode.None;
        yield return ErrorCode.UnknownServerError;
        yield return ErrorCode.OffsetOutOfRange;
        yield return ErrorCode.InvalidFetchSize;
        yield return ErrorCode.InvalidTopicException;
        yield return ErrorCode.InvalidRequiredAcks;
        yield return ErrorCode.TopicAlreadyExists;
        yield return ErrorCode.InvalidRequest;
        yield return ErrorCode.TopicAuthorizationFailed;
        yield return ErrorCode.GroupAuthorizationFailed;
        yield return ErrorCode.ClusterAuthorizationFailed;
        yield return ErrorCode.InvalidTimestamp;
        yield return ErrorCode.UnsupportedVersion;
        yield return ErrorCode.PolicyViolation;
        yield return ErrorCode.OutOfOrderSequenceNumber;
    }

    [Test]
    public async Task IsRetriable_ExactlyTwentyThreeRetriableCodes()
    {
        var retriableCount = Enum.GetValues<ErrorCode>().Count(e => e.IsRetriable());
        await Assert.That(retriableCount).IsEqualTo(23);
    }

    #endregion

    #region RequiresMetadataRefresh Tests

    [Test]
    [MethodDataSource(nameof(MetadataRefreshErrorCodes))]
    public async Task RequiresMetadataRefresh_MetadataErrorCode_ReturnsTrue(ErrorCode errorCode)
    {
        await Assert.That(errorCode.RequiresMetadataRefresh()).IsTrue();
    }

    [Test]
    [MethodDataSource(nameof(NonMetadataRefreshErrorCodes))]
    public async Task RequiresMetadataRefresh_NonMetadataErrorCode_ReturnsFalse(ErrorCode errorCode)
    {
        await Assert.That(errorCode.RequiresMetadataRefresh()).IsFalse();
    }

    public static IEnumerable<ErrorCode> MetadataRefreshErrorCodes()
    {
        yield return ErrorCode.UnknownTopicOrPartition;
        yield return ErrorCode.LeaderNotAvailable;
        yield return ErrorCode.NotLeaderOrFollower;
        yield return ErrorCode.BrokerNotAvailable;
        yield return ErrorCode.ReplicaNotAvailable;
        yield return ErrorCode.CoordinatorNotAvailable;
        yield return ErrorCode.NotCoordinator;
        yield return ErrorCode.NotController;
        yield return ErrorCode.FencedLeaderEpoch;
        yield return ErrorCode.UnknownLeaderEpoch;
    }

    public static IEnumerable<ErrorCode> NonMetadataRefreshErrorCodes()
    {
        yield return ErrorCode.None;
        yield return ErrorCode.CorruptMessage;
        yield return ErrorCode.RequestTimedOut;
        yield return ErrorCode.NetworkException;
        yield return ErrorCode.TopicAlreadyExists;
        yield return ErrorCode.InvalidRequest;
        yield return ErrorCode.ThrottlingQuotaExceeded;
    }

    [Test]
    public async Task RequiresMetadataRefresh_ExactlyTenMetadataCodes()
    {
        var metadataCount = Enum.GetValues<ErrorCode>().Count(e => e.RequiresMetadataRefresh());
        await Assert.That(metadataCount).IsEqualTo(10);
    }

    #endregion
}
