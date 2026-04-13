using Dekaf.Producer;
using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Producer;

public sealed class TransactionErrorClassifierTests
{
    [Test]
    [Arguments(ErrorCode.ProducerFenced)]
    [Arguments(ErrorCode.TransactionalIdAuthorizationFailed)]
    [Arguments(ErrorCode.TransactionCoordinatorFenced)]
    public async Task AlwaysFatal_ReturnsFatal_RegardlessOfTv2(ErrorCode errorCode)
    {
        var v1 = TransactionErrorClassifier.Classify(errorCode, tv2: false);
        var v2 = TransactionErrorClassifier.Classify(errorCode, tv2: true);

        await Assert.That(v1).IsEqualTo(TransactionErrorClassification.Fatal);
        await Assert.That(v2).IsEqualTo(TransactionErrorClassification.Fatal);
    }

    [Test]
    [Arguments(ErrorCode.CoordinatorLoadInProgress)]
    [Arguments(ErrorCode.CoordinatorNotAvailable)]
    [Arguments(ErrorCode.NotCoordinator)]
    [Arguments(ErrorCode.ConcurrentTransactions)]
    public async Task Retriable_ReturnsRetriable_RegardlessOfTv2(ErrorCode errorCode)
    {
        var v1 = TransactionErrorClassifier.Classify(errorCode, tv2: false);
        var v2 = TransactionErrorClassifier.Classify(errorCode, tv2: true);

        await Assert.That(v1).IsEqualTo(TransactionErrorClassification.Retriable);
        await Assert.That(v2).IsEqualTo(TransactionErrorClassification.Retriable);
    }

    [Test]
    public async Task InvalidProducerIdMapping_Abortable_InV1()
    {
        var result = TransactionErrorClassifier.Classify(ErrorCode.InvalidProducerIdMapping, tv2: false);
        await Assert.That(result).IsEqualTo(TransactionErrorClassification.Abortable);
    }

    [Test]
    public async Task InvalidProducerIdMapping_Fatal_InV2()
    {
        var result = TransactionErrorClassifier.Classify(ErrorCode.InvalidProducerIdMapping, tv2: true);
        await Assert.That(result).IsEqualTo(TransactionErrorClassification.Fatal);
    }

    [Test]
    public async Task UnknownError_DefaultsToAbortable()
    {
        var result = TransactionErrorClassifier.Classify(ErrorCode.UnknownServerError, tv2: false);
        await Assert.That(result).IsEqualTo(TransactionErrorClassification.Abortable);
    }
}
