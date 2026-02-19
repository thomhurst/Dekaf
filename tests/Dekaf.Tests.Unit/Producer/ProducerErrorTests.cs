using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for producer error paths: exception types, error code mapping,
/// retriability, topic context, and serialization failure wrapping.
/// </summary>
public class ProducerErrorTests
{
    #region ProduceException Error Code Tests

    [Test]
    [MethodDataSource(nameof(KnownProducerErrorCodes))]
    public async Task ProduceException_WithKnownErrorCode_CarriesCorrectErrorCode(ErrorCode errorCode)
    {
        var ex = new ProduceException(errorCode, $"Error: {errorCode}");

        await Assert.That(ex.ErrorCode).IsEqualTo(errorCode);
        await Assert.That(ex.Message).IsEqualTo($"Error: {errorCode}");
    }

    public static IEnumerable<ErrorCode> KnownProducerErrorCodes()
    {
        yield return ErrorCode.MessageTooLarge;
        yield return ErrorCode.NotLeaderOrFollower;
        yield return ErrorCode.RequestTimedOut;
        yield return ErrorCode.NotEnoughReplicas;
        yield return ErrorCode.NotEnoughReplicasAfterAppend;
        yield return ErrorCode.InvalidRequiredAcks;
        yield return ErrorCode.TopicAuthorizationFailed;
        yield return ErrorCode.UnknownTopicOrPartition;
        yield return ErrorCode.InvalidRequest;
        yield return ErrorCode.CorruptMessage;
        yield return ErrorCode.KafkaStorageError;
        yield return ErrorCode.OutOfOrderSequenceNumber;
    }

    #endregion

    #region IsRetriable Tests for Producer-Relevant Codes

    [Test]
    [MethodDataSource(nameof(RetriableProducerErrorCodes))]
    public async Task ProduceException_IsRetriable_ForRetriableCodes_ReturnsTrue(ErrorCode errorCode)
    {
        var ex = new ProduceException(errorCode, "retriable error");

        await Assert.That(ex.IsRetriable).IsTrue();
    }

    [Test]
    [MethodDataSource(nameof(NonRetriableProducerErrorCodes))]
    public async Task ProduceException_IsRetriable_ForNonRetriableCodes_ReturnsFalse(ErrorCode errorCode)
    {
        var ex = new ProduceException(errorCode, "non-retriable error");

        await Assert.That(ex.IsRetriable).IsFalse();
    }

    public static IEnumerable<ErrorCode> RetriableProducerErrorCodes()
    {
        yield return ErrorCode.NotLeaderOrFollower;
        yield return ErrorCode.RequestTimedOut;
        yield return ErrorCode.NotEnoughReplicas;
        yield return ErrorCode.NotEnoughReplicasAfterAppend;
        yield return ErrorCode.CorruptMessage;
        yield return ErrorCode.UnknownTopicOrPartition;
        yield return ErrorCode.LeaderNotAvailable;
        yield return ErrorCode.KafkaStorageError;
        yield return ErrorCode.NetworkException;
        yield return ErrorCode.ThrottlingQuotaExceeded;
    }

    public static IEnumerable<ErrorCode> NonRetriableProducerErrorCodes()
    {
        yield return ErrorCode.MessageTooLarge;
        yield return ErrorCode.InvalidRequiredAcks;
        yield return ErrorCode.TopicAuthorizationFailed;
        yield return ErrorCode.InvalidRequest;
        yield return ErrorCode.OutOfOrderSequenceNumber;
        yield return ErrorCode.InvalidTopicException;
        yield return ErrorCode.PolicyViolation;
    }

    #endregion

    #region ProduceException Topic Context Tests

    [Test]
    public async Task ProduceException_CarriesTopicContext()
    {
        var ex = new ProduceException(ErrorCode.MessageTooLarge, "message too large")
        {
            Topic = "orders-topic",
            Partition = 7
        };

        await Assert.That(ex.Topic).IsEqualTo("orders-topic");
        await Assert.That(ex.Partition).IsEqualTo(7);
        await Assert.That(ex.ErrorCode).IsEqualTo(ErrorCode.MessageTooLarge);
    }

    [Test]
    public async Task ProduceException_TopicContext_WithNullPartition()
    {
        var ex = new ProduceException(ErrorCode.NotLeaderOrFollower, "not leader")
        {
            Topic = "events-topic"
        };

        await Assert.That(ex.Topic).IsEqualTo("events-topic");
        await Assert.That(ex.Partition).IsNull();
    }

    [Test]
    public async Task ProduceException_TopicContext_WithInnerException()
    {
        var inner = new TimeoutException("broker did not respond");
        var ex = new ProduceException("produce failed", inner)
        {
            Topic = "metrics-topic",
            Partition = 0
        };

        await Assert.That(ex.Topic).IsEqualTo("metrics-topic");
        await Assert.That(ex.Partition).IsEqualTo(0);
        await Assert.That(ex.InnerException).IsSameReferenceAs(inner);
    }

    #endregion

    #region Cancellation Tests

    [Test]
    public async Task CancellationToken_PreCancelled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await Task.Run(() => cts.Token.ThrowIfCancellationRequested());
        });
    }

    [Test]
    public async Task CancellationToken_PreCancelled_ExceptionContainsCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            cts.Token.ThrowIfCancellationRequested();
            // Should not reach here
            Assert.Fail("Expected OperationCanceledException");
        }
        catch (OperationCanceledException ex)
        {
            await Assert.That(ex.CancellationToken).IsEqualTo(cts.Token);
        }
    }

    #endregion

    #region SerializationException Topic Context Tests

    [Test]
    public async Task SerializationException_WrapsInnerException_WithTopicContext()
    {
        var inner = new FormatException("invalid format for key");
        var ex = new SerializationException(
            "Key serialization failed",
            inner,
            "user-events",
            SerializationComponent.Key);

        await Assert.That(ex.Topic).IsEqualTo("user-events");
        await Assert.That(ex.Component).IsEqualTo(SerializationComponent.Key);
        await Assert.That(ex.InnerException).IsSameReferenceAs(inner);
        await Assert.That(ex.Message).IsEqualTo("Key serialization failed");
    }

    [Test]
    public async Task SerializationException_ValueComponent_WithTopicContext()
    {
        var inner = new InvalidCastException("cannot cast to target type");
        var ex = new SerializationException(
            "Value serialization failed",
            inner,
            "order-events",
            SerializationComponent.Value);

        await Assert.That(ex.Topic).IsEqualTo("order-events");
        await Assert.That(ex.Component).IsEqualTo(SerializationComponent.Value);
        await Assert.That(ex.InnerException).IsSameReferenceAs(inner);
    }

    [Test]
    public async Task SerializationException_InheritsFromKafkaException()
    {
        var ex = new SerializationException("failed");

        await Assert.That(ex).IsAssignableTo<KafkaException>();
    }

    [Test]
    public async Task SerializationException_WithNullTopic_TopicIsNull()
    {
        var inner = new FormatException("bad data");
        var ex = new SerializationException("failed", inner, null, SerializationComponent.Key);

        await Assert.That(ex.Topic).IsNull();
        await Assert.That(ex.InnerException).IsSameReferenceAs(inner);
    }

    [Test]
    public async Task SerializationException_InitProperties_SetTopicAndComponent()
    {
        var ex = new SerializationException("failed")
        {
            Topic = "my-topic",
            Component = SerializationComponent.Value
        };

        await Assert.That(ex.Topic).IsEqualTo("my-topic");
        await Assert.That(ex.Component).IsEqualTo(SerializationComponent.Value);
    }

    #endregion
}
