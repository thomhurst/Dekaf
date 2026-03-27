using System.Net.Sockets;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Tests for <see cref="KafkaConsumer{TKey,TValue}.IsFatalPrefetchError"/> which classifies
/// exceptions as fatal (propagate to pipeline runner) or transient (suppress and retry).
/// </summary>
public class FatalPrefetchErrorClassificationTests
{
    #region Fatal errors — should return true

    [Test]
    public async Task AuthenticationException_IsFatal()
    {
        var ex = new AuthenticationException("SASL handshake failed");
        await Assert.That(KafkaConsumer<string, string>.IsFatalPrefetchError(ex)).IsTrue();
    }

    [Test]
    public async Task AuthorizationException_IsFatal()
    {
        var ex = new AuthorizationException("Topic authorization failed");
        await Assert.That(KafkaConsumer<string, string>.IsFatalPrefetchError(ex)).IsTrue();
    }

    [Test]
    public async Task AuthenticationException_WithoutErrorCode_IsFatal()
    {
        // Auth* exceptions may lack ErrorCode — the classifier must still catch them
        var ex = new AuthenticationException("no error code");
        await Assert.That(ex.ErrorCode).IsNull();
        await Assert.That(KafkaConsumer<string, string>.IsFatalPrefetchError(ex)).IsTrue();
    }

    [Test]
    public async Task AuthorizationException_WithoutErrorCode_IsFatal()
    {
        var ex = new AuthorizationException("no error code");
        await Assert.That(ex.ErrorCode).IsNull();
        await Assert.That(KafkaConsumer<string, string>.IsFatalPrefetchError(ex)).IsTrue();
    }

    [Test]
    public async Task KafkaException_OffsetOutOfRange_IsFatal()
    {
        // OffsetOutOfRange with AutoOffsetReset.None — non-retriable protocol error
        var ex = new KafkaException(ErrorCode.OffsetOutOfRange, "OffsetOutOfRange for topic-0");
        await Assert.That(KafkaConsumer<string, string>.IsFatalPrefetchError(ex)).IsTrue();
    }

    [Test]
    public async Task KafkaException_NonRetriableWithErrorCode_IsFatal()
    {
        // Any non-retriable protocol error with an ErrorCode should be fatal
        var ex = new KafkaException(ErrorCode.UnknownServerError, "unknown error");
        await Assert.That(ex.IsRetriable).IsFalse();
        await Assert.That(KafkaConsumer<string, string>.IsFatalPrefetchError(ex)).IsTrue();
    }

    #endregion

    #region Transient errors — should return false

    [Test]
    public async Task KafkaException_WithoutErrorCode_IsTransient()
    {
        // Networking-layer exceptions (connection timeouts, broker unavailable) have no ErrorCode.
        // This is the critical case — these are transient but have IsRetriable == false.
        var ex = new KafkaException("Flush timeout after 5000ms");
        await Assert.That(ex.ErrorCode).IsNull();
        await Assert.That(ex.IsRetriable).IsFalse();
        await Assert.That(KafkaConsumer<string, string>.IsFatalPrefetchError(ex)).IsFalse();
    }

    [Test]
    public async Task KafkaException_RetriableWithErrorCode_IsTransient()
    {
        // Retriable protocol errors (e.g., NotLeaderOrFollower) are transient
        var ex = new KafkaException(ErrorCode.NotLeaderOrFollower, "leader changed");
        await Assert.That(ex.IsRetriable).IsTrue();
        await Assert.That(KafkaConsumer<string, string>.IsFatalPrefetchError(ex)).IsFalse();
    }

    [Test]
    public async Task IOException_IsTransient()
    {
        var ex = new IOException("connection reset");
        await Assert.That(KafkaConsumer<string, string>.IsFatalPrefetchError(ex)).IsFalse();
    }

    [Test]
    public async Task SocketException_IsTransient()
    {
        var ex = new SocketException((int)SocketError.ConnectionRefused);
        await Assert.That(KafkaConsumer<string, string>.IsFatalPrefetchError(ex)).IsFalse();
    }

    [Test]
    public async Task OperationCanceledException_IsTransient()
    {
        var ex = new OperationCanceledException();
        await Assert.That(KafkaConsumer<string, string>.IsFatalPrefetchError(ex)).IsFalse();
    }

    #endregion
}
