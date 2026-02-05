using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Errors;

public class ExceptionTypesTests
{
    #region KafkaException Tests

    [Test]
    public async Task KafkaException_DefaultConstructor_HasNullErrorCode()
    {
        var ex = new KafkaException();
        await Assert.That(ex.ErrorCode).IsNull();
    }

    [Test]
    public async Task KafkaException_MessageConstructor_SetsMessage()
    {
        var ex = new KafkaException("test message");
        await Assert.That(ex.Message).IsEqualTo("test message");
    }

    [Test]
    public async Task KafkaException_ErrorCodeConstructor_SetsErrorCode()
    {
        var ex = new KafkaException(ErrorCode.RequestTimedOut, "timed out");
        await Assert.That(ex.ErrorCode).IsEqualTo(ErrorCode.RequestTimedOut);
        await Assert.That(ex.Message).IsEqualTo("timed out");
    }

    [Test]
    public async Task KafkaException_InnerExceptionConstructor_SetsInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new KafkaException("outer", inner);
        await Assert.That(ex.InnerException).IsSameReferenceAs(inner);
    }

    [Test]
    public async Task KafkaException_ErrorCodeAndInnerException_SetsBoth()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new KafkaException(ErrorCode.NetworkException, "network error", inner);
        await Assert.That(ex.ErrorCode).IsEqualTo(ErrorCode.NetworkException);
        await Assert.That(ex.InnerException).IsSameReferenceAs(inner);
    }

    [Test]
    public async Task KafkaException_IsRetriable_WithRetriableCode_ReturnsTrue()
    {
        var ex = new KafkaException(ErrorCode.RequestTimedOut, "timed out");
        await Assert.That(ex.IsRetriable).IsTrue();
    }

    [Test]
    public async Task KafkaException_IsRetriable_WithNonRetriableCode_ReturnsFalse()
    {
        var ex = new KafkaException(ErrorCode.InvalidRequest, "invalid");
        await Assert.That(ex.IsRetriable).IsFalse();
    }

    [Test]
    public async Task KafkaException_IsRetriable_WithNullErrorCode_ReturnsFalse()
    {
        var ex = new KafkaException("no error code");
        await Assert.That(ex.IsRetriable).IsFalse();
    }

    [Test]
    public async Task KafkaException_InheritsFromException()
    {
        var ex = new KafkaException();
        await Assert.That(ex).IsAssignableTo<Exception>();
    }

    #endregion

    #region ProduceException Tests

    [Test]
    public async Task ProduceException_InheritsKafkaException()
    {
        var ex = new ProduceException();
        await Assert.That(ex).IsAssignableTo<KafkaException>();
    }

    [Test]
    public async Task ProduceException_TopicAndPartition_SetViaInit()
    {
        var ex = new ProduceException("failed") { Topic = "my-topic", Partition = 3 };
        await Assert.That(ex.Topic).IsEqualTo("my-topic");
        await Assert.That(ex.Partition).IsEqualTo(3);
    }

    [Test]
    public async Task ProduceException_DefaultTopicAndPartition_AreNull()
    {
        var ex = new ProduceException("failed");
        await Assert.That(ex.Topic).IsNull();
        await Assert.That(ex.Partition).IsNull();
    }

    [Test]
    public async Task ProduceException_ErrorCodeConstructor_SetsErrorCode()
    {
        var ex = new ProduceException(ErrorCode.MessageTooLarge, "too large");
        await Assert.That(ex.ErrorCode).IsEqualTo(ErrorCode.MessageTooLarge);
    }

    [Test]
    public async Task ProduceException_InnerExceptionConstructor_SetsInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new ProduceException("outer", inner);
        await Assert.That(ex.InnerException).IsSameReferenceAs(inner);
    }

    #endregion

    #region ConsumeException Tests

    [Test]
    public async Task ConsumeException_InheritsKafkaException()
    {
        var ex = new ConsumeException();
        await Assert.That(ex).IsAssignableTo<KafkaException>();
    }

    [Test]
    public async Task ConsumeException_MessageConstructor_SetsMessage()
    {
        var ex = new ConsumeException("consume failed");
        await Assert.That(ex.Message).IsEqualTo("consume failed");
    }

    [Test]
    public async Task ConsumeException_ErrorCodeConstructor_SetsErrorCode()
    {
        var ex = new ConsumeException(ErrorCode.OffsetOutOfRange, "offset error");
        await Assert.That(ex.ErrorCode).IsEqualTo(ErrorCode.OffsetOutOfRange);
    }

    [Test]
    public async Task ConsumeException_InnerExceptionConstructor_SetsInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new ConsumeException("outer", inner);
        await Assert.That(ex.InnerException).IsSameReferenceAs(inner);
    }

    #endregion

    #region GroupException Tests

    [Test]
    public async Task GroupException_InheritsKafkaException()
    {
        var ex = new GroupException();
        await Assert.That(ex).IsAssignableTo<KafkaException>();
    }

    [Test]
    public async Task GroupException_GroupId_SetViaInit()
    {
        var ex = new GroupException("group error") { GroupId = "my-group" };
        await Assert.That(ex.GroupId).IsEqualTo("my-group");
    }

    [Test]
    public async Task GroupException_DefaultGroupId_IsNull()
    {
        var ex = new GroupException("group error");
        await Assert.That(ex.GroupId).IsNull();
    }

    [Test]
    public async Task GroupException_ErrorCodeConstructor_SetsErrorCode()
    {
        var ex = new GroupException(ErrorCode.RebalanceInProgress, "rebalancing");
        await Assert.That(ex.ErrorCode).IsEqualTo(ErrorCode.RebalanceInProgress);
    }

    [Test]
    public async Task GroupException_InnerExceptionConstructor_SetsInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new GroupException("outer", inner);
        await Assert.That(ex.InnerException).IsSameReferenceAs(inner);
    }

    #endregion

    #region TransactionException Tests

    [Test]
    public async Task TransactionException_InheritsKafkaException()
    {
        var ex = new TransactionException();
        await Assert.That(ex).IsAssignableTo<KafkaException>();
    }

    [Test]
    public async Task TransactionException_TransactionalId_SetViaInit()
    {
        var ex = new TransactionException("txn error") { TransactionalId = "txn-1" };
        await Assert.That(ex.TransactionalId).IsEqualTo("txn-1");
    }

    [Test]
    public async Task TransactionException_DefaultTransactionalId_IsNull()
    {
        var ex = new TransactionException("txn error");
        await Assert.That(ex.TransactionalId).IsNull();
    }

    [Test]
    public async Task TransactionException_ErrorCodeConstructor_SetsErrorCode()
    {
        var ex = new TransactionException(ErrorCode.InvalidTxnState, "invalid state");
        await Assert.That(ex.ErrorCode).IsEqualTo(ErrorCode.InvalidTxnState);
    }

    #endregion

    #region AuthenticationException Tests

    [Test]
    public async Task AuthenticationException_InheritsKafkaException()
    {
        var ex = new AuthenticationException();
        await Assert.That(ex).IsAssignableTo<KafkaException>();
    }

    [Test]
    public async Task AuthenticationException_MessageConstructor_SetsMessage()
    {
        var ex = new AuthenticationException("auth failed");
        await Assert.That(ex.Message).IsEqualTo("auth failed");
    }

    [Test]
    public async Task AuthenticationException_ErrorCodeConstructor_SetsErrorCode()
    {
        var ex = new AuthenticationException(ErrorCode.SaslAuthenticationFailed, "sasl failed");
        await Assert.That(ex.ErrorCode).IsEqualTo(ErrorCode.SaslAuthenticationFailed);
    }

    [Test]
    public async Task AuthenticationException_InnerExceptionConstructor_SetsInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new AuthenticationException("outer", inner);
        await Assert.That(ex.InnerException).IsSameReferenceAs(inner);
    }

    #endregion

    #region AuthorizationException Tests

    [Test]
    public async Task AuthorizationException_InheritsKafkaException()
    {
        var ex = new AuthorizationException();
        await Assert.That(ex).IsAssignableTo<KafkaException>();
    }

    [Test]
    public async Task AuthorizationException_OperationAndResource_SetViaInit()
    {
        var ex = new AuthorizationException("denied")
        {
            Operation = "Write",
            Resource = "topic:my-topic"
        };
        await Assert.That(ex.Operation).IsEqualTo("Write");
        await Assert.That(ex.Resource).IsEqualTo("topic:my-topic");
    }

    [Test]
    public async Task AuthorizationException_DefaultOperationAndResource_AreNull()
    {
        var ex = new AuthorizationException("denied");
        await Assert.That(ex.Operation).IsNull();
        await Assert.That(ex.Resource).IsNull();
    }

    [Test]
    public async Task AuthorizationException_ErrorCodeConstructor_SetsErrorCode()
    {
        var ex = new AuthorizationException(ErrorCode.TopicAuthorizationFailed, "not authorized");
        await Assert.That(ex.ErrorCode).IsEqualTo(ErrorCode.TopicAuthorizationFailed);
    }

    #endregion

    #region SerializationException Tests

    [Test]
    public async Task SerializationException_InheritsKafkaException()
    {
        var ex = new SerializationException();
        await Assert.That(ex).IsAssignableTo<KafkaException>();
    }

    [Test]
    public async Task SerializationException_TopicAndComponent_SetViaInit()
    {
        var ex = new SerializationException("failed")
        {
            Topic = "my-topic",
            Component = SerializationComponent.Key
        };
        await Assert.That(ex.Topic).IsEqualTo("my-topic");
        await Assert.That(ex.Component).IsEqualTo(SerializationComponent.Key);
    }

    [Test]
    public async Task SerializationException_ContextConstructor_SetsAll()
    {
        var inner = new FormatException("bad format");
        var ex = new SerializationException("failed", inner, "my-topic", SerializationComponent.Value);

        await Assert.That(ex.Topic).IsEqualTo("my-topic");
        await Assert.That(ex.Component).IsEqualTo(SerializationComponent.Value);
        await Assert.That(ex.InnerException).IsSameReferenceAs(inner);
        await Assert.That(ex.Message).IsEqualTo("failed");
    }

    [Test]
    public async Task SerializationException_DefaultTopic_IsNull()
    {
        var ex = new SerializationException("failed");
        await Assert.That(ex.Topic).IsNull();
    }

    [Test]
    public async Task SerializationException_DefaultComponent_IsKey()
    {
        // Default enum value is Key (0)
        var ex = new SerializationException("failed");
        await Assert.That(ex.Component).IsEqualTo(SerializationComponent.Key);
    }

    #endregion
}
