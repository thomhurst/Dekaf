using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Admin;

/// <summary>
/// Tests for admin error paths: partial failure error codes, authorization/authentication
/// error code mapping, and error preservation in result types.
/// </summary>
public class AdminErrorTests
{
    #region Partial Failure Error Code Preservation Tests

    [Test]
    public async Task ElectLeadersResultInfo_PreservesPartialFailureErrorCode()
    {
        var successResult = new ElectLeadersResultInfo
        {
            TopicPartition = new TopicPartition("topic-a", 0),
            ErrorCode = ErrorCode.None
        };

        var failureResult = new ElectLeadersResultInfo
        {
            TopicPartition = new TopicPartition("topic-a", 1),
            ErrorCode = ErrorCode.PreferredLeaderNotAvailable,
            ErrorMessage = "Preferred leader not available for partition 1"
        };

        await Assert.That(successResult.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(successResult.ErrorMessage).IsNull();

        await Assert.That(failureResult.ErrorCode).IsEqualTo(ErrorCode.PreferredLeaderNotAvailable);
        await Assert.That(failureResult.ErrorMessage).IsEqualTo("Preferred leader not available for partition 1");
    }

    [Test]
    [MethodDataSource(nameof(PartialFailureErrorCodes))]
    public async Task ElectLeadersResultInfo_PreservesVariousErrorCodes(ErrorCode errorCode, string? errorMessage)
    {
        var result = new ElectLeadersResultInfo
        {
            TopicPartition = new TopicPartition("test-topic", 0),
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };

        await Assert.That(result.ErrorCode).IsEqualTo(errorCode);
        await Assert.That(result.ErrorMessage).IsEqualTo(errorMessage);
    }

    public static IEnumerable<(ErrorCode, string?)> PartialFailureErrorCodes()
    {
        yield return (ErrorCode.None, null);
        yield return (ErrorCode.PreferredLeaderNotAvailable, "Preferred leader not available");
        yield return (ErrorCode.EligibleLeadersNotAvailable, "No eligible leaders");
        yield return (ErrorCode.NotController, "Broker is not the controller");
        yield return (ErrorCode.UnknownTopicOrPartition, "Topic or partition not found");
        yield return (ErrorCode.TopicAuthorizationFailed, "Not authorized");
        yield return (ErrorCode.ClusterAuthorizationFailed, "Cluster authorization failed");
    }

    #endregion

    #region Authorization Error Code Mapping Tests

    [Test]
    public async Task AuthorizationException_TopicAuthorizationFailed_MapsCorrectly()
    {
        var ex = new AuthorizationException(ErrorCode.TopicAuthorizationFailed, "not authorized for topic")
        {
            Operation = "Write",
            Resource = "topic:orders"
        };

        await Assert.That(ex.ErrorCode).IsEqualTo(ErrorCode.TopicAuthorizationFailed);
        await Assert.That(ex.Operation).IsEqualTo("Write");
        await Assert.That(ex.Resource).IsEqualTo("topic:orders");
    }

    [Test]
    public async Task AuthorizationException_GroupAuthorizationFailed_MapsCorrectly()
    {
        var ex = new AuthorizationException(ErrorCode.GroupAuthorizationFailed, "not authorized for group")
        {
            Operation = "Read",
            Resource = "group:my-consumer-group"
        };

        await Assert.That(ex.ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
        await Assert.That(ex.Operation).IsEqualTo("Read");
        await Assert.That(ex.Resource).IsEqualTo("group:my-consumer-group");
    }

    [Test]
    [MethodDataSource(nameof(AuthorizationErrorCodes))]
    public async Task AuthorizationException_AllAuthzCodes_AreNonRetriable(ErrorCode errorCode)
    {
        var ex = new AuthorizationException(errorCode, "authorization failed");

        await Assert.That(ex.IsRetriable).IsFalse();
    }

    public static IEnumerable<ErrorCode> AuthorizationErrorCodes()
    {
        yield return ErrorCode.TopicAuthorizationFailed;
        yield return ErrorCode.GroupAuthorizationFailed;
        yield return ErrorCode.ClusterAuthorizationFailed;
        yield return ErrorCode.TransactionalIdAuthorizationFailed;
        yield return ErrorCode.DelegationTokenAuthorizationFailed;
    }

    #endregion

    #region Authentication Error Code Mapping Tests

    [Test]
    public async Task AuthenticationException_WithInnerException_PreservesDetails()
    {
        var inner = new System.Security.Authentication.AuthenticationException("TLS handshake failed");
        var ex = new AuthenticationException("authentication failed", inner);

        await Assert.That(ex.InnerException).IsSameReferenceAs(inner);
        await Assert.That(ex.Message).IsEqualTo("authentication failed");
    }

    [Test]
    [MethodDataSource(nameof(AuthenticationErrorCodes))]
    public async Task AuthenticationException_AllAuthnCodes_AreNonRetriable(ErrorCode errorCode)
    {
        var ex = new AuthenticationException(errorCode, "auth failed");

        await Assert.That(ex.IsRetriable).IsFalse();
    }

    public static IEnumerable<ErrorCode> AuthenticationErrorCodes()
    {
        yield return ErrorCode.SaslAuthenticationFailed;
        yield return ErrorCode.UnsupportedSaslMechanism;
        yield return ErrorCode.IllegalSaslState;
    }

    #endregion

    #region Admin Exception Type Hierarchy Tests

    [Test]
    public async Task AuthorizationException_WithInnerException_PreservesChain()
    {
        var inner = new UnauthorizedAccessException("access denied");
        var ex = new AuthorizationException("authorization failed", inner);

        await Assert.That(ex.InnerException).IsSameReferenceAs(inner);
        await Assert.That(ex.Message).IsEqualTo("authorization failed");
    }

    #endregion
}
