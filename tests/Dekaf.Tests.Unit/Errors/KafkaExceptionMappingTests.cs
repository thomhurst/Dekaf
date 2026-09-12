using Dekaf.Errors;
using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Errors;

public sealed class KafkaExceptionMappingTests
{
    [Test]
    [Arguments(ErrorCode.TopicAuthorizationFailed)]
    [Arguments(ErrorCode.GroupAuthorizationFailed)]
    [Arguments(ErrorCode.ClusterAuthorizationFailed)]
    [Arguments(ErrorCode.TransactionalIdAuthorizationFailed)]
    [Arguments(ErrorCode.DelegationTokenAuthorizationFailed)]
    public async Task AuthorizationErrors_MapToExactAuthorizationException(ErrorCode code)
    {
        var exception = KafkaException.FromErrorCode(code, "denied");
        await Assert.That(exception.GetType()).IsEqualTo(typeof(AuthorizationException));
        await Assert.That(exception.ErrorCode).IsEqualTo(code);
        await Assert.That(exception.Message).IsEqualTo("denied");
        await Assert.That(exception.IsRetriable).IsFalse();
    }

    [Test]
    public async Task SaslFailure_MapsToExactAuthenticationException()
    {
        var exception = KafkaException.FromErrorCode(ErrorCode.SaslAuthenticationFailed, "invalid credentials");
        await Assert.That(exception.GetType()).IsEqualTo(typeof(AuthenticationException));
        await Assert.That(exception.ErrorCode).IsEqualTo(ErrorCode.SaslAuthenticationFailed);
        await Assert.That(exception.Message).IsEqualTo("invalid credentials");
        await Assert.That(exception.IsRetriable).IsFalse();
    }

    [Test]
    [Arguments(ErrorCode.NotLeaderOrFollower, true)]
    [Arguments(ErrorCode.InvalidTopicException, false)]
    public async Task OtherErrors_PreserveCodeMessageAndRetryClassification(ErrorCode code, bool retriable)
    {
        var exception = KafkaException.FromErrorCode(code, "broker error");
        await Assert.That(exception.GetType()).IsEqualTo(typeof(KafkaException));
        await Assert.That(exception.ErrorCode).IsEqualTo(code);
        await Assert.That(exception.Message).IsEqualTo("broker error");
        await Assert.That(exception.IsRetriable).IsEqualTo(retriable);
    }
}
