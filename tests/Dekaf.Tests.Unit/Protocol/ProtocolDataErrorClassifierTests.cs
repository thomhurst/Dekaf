using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class ProtocolDataErrorClassifierTests
{
    [Test]
    public async Task IsProtocolDataError_ClassifiesWireAndCodecFailures()
    {
        Exception[] protocolErrors =
        [
            new InsufficientDataException(),
            new MalformedProtocolDataException("malformed"),
            new InvalidDataException("invalid compressed payload"),
            new ArgumentOutOfRangeException("length"),
            new NotSupportedException("unsupported record format")
        ];

        foreach (var error in protocolErrors)
        {
            await Assert.That(ProtocolDataErrorClassifier.IsProtocolDataError(error)).IsTrue();
        }
    }

    [Test]
    public async Task IsProtocolDataError_DoesNotClassifyUserOrNetworkFailures()
    {
        Exception[] otherErrors =
        [
            new IOException("connection reset"),
            new FormatException("user deserializer failed"),
            new OperationCanceledException()
        ];

        foreach (var error in otherErrors)
        {
            await Assert.That(ProtocolDataErrorClassifier.IsProtocolDataError(error)).IsFalse();
        }
    }
}
