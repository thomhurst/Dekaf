using Dekaf.Producer;
using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Producer;

public sealed class TransactionErrorClassifierTests
{
    [Test]
    [Arguments(ErrorCode.ProducerFenced)]
    [Arguments(ErrorCode.TransactionalIdAuthorizationFailed)]
    [Arguments(ErrorCode.TransactionCoordinatorFenced)]
    [Arguments(ErrorCode.InvalidProducerEpoch)]
    public async Task AlwaysFatal_ReturnsFatal_RegardlessOfTv2(ErrorCode errorCode)
    {
        var v1 = TransactionErrorClassifier.Classify(errorCode, tv2: false);
        var v2 = TransactionErrorClassifier.Classify(errorCode, tv2: true);

        await Assert.That(v1).IsEqualTo(TransactionErrorClassification.Fatal);
        await Assert.That(v2).IsEqualTo(TransactionErrorClassification.Fatal);
    }

    [Test]
    [Arguments(ErrorCode.TransactionAbortable)]
    [Arguments(ErrorCode.InvalidTxnState)]
    public async Task AlwaysAbortable_ReturnsAbortable_RegardlessOfTv2(ErrorCode errorCode)
    {
        var v1 = TransactionErrorClassifier.Classify(errorCode, tv2: false);
        var v2 = TransactionErrorClassifier.Classify(errorCode, tv2: true);

        await Assert.That(v1).IsEqualTo(TransactionErrorClassification.Abortable);
        await Assert.That(v2).IsEqualTo(TransactionErrorClassification.Abortable);
    }

    [Test]
    [Arguments(ErrorCode.CoordinatorLoadInProgress)]
    [Arguments(ErrorCode.CoordinatorNotAvailable)]
    [Arguments(ErrorCode.NotCoordinator)]
    [Arguments(ErrorCode.ConcurrentTransactions)]
    [Arguments(ErrorCode.UnknownTopicId)]
    [Arguments(ErrorCode.UnknownTopicOrPartition)]
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

    // Java 4.4 TxnOffsetCommitHandler, plus the KIP-1050 choices documented on the classifier. The
    // expected classification is a name because the enum is internal and the test method public.
    [Test]
    [Arguments(ErrorCode.CoordinatorNotAvailable, "Retriable")]
    [Arguments(ErrorCode.NotCoordinator, "Retriable")]
    [Arguments(ErrorCode.RequestTimedOut, "Retriable")]
    [Arguments(ErrorCode.CoordinatorLoadInProgress, "Retriable")]
    [Arguments(ErrorCode.UnknownTopicOrPartition, "Retriable")]
    [Arguments(ErrorCode.UnknownTopicId, "Retriable")]
    [Arguments(ErrorCode.GroupAuthorizationFailed, "Abortable")]
    [Arguments(ErrorCode.FencedInstanceId, "Abortable")]
    [Arguments(ErrorCode.TransactionAbortable, "Abortable")]
    [Arguments(ErrorCode.UnknownMemberId, "Abortable")]
    [Arguments(ErrorCode.IllegalGeneration, "Abortable")]
    [Arguments(ErrorCode.GroupIdNotFound, "Abortable")]
    [Arguments(ErrorCode.StaleMemberEpoch, "Abortable")]
    [Arguments(ErrorCode.TransactionalIdAuthorizationFailed, "Fatal")]
    [Arguments(ErrorCode.InvalidProducerEpoch, "Fatal")]
    [Arguments(ErrorCode.ProducerFenced, "Fatal")]
    [Arguments(ErrorCode.UnsupportedForMessageFormat, "Fatal")]
    [Arguments(ErrorCode.InvalidTxnState, "Abortable")]
    [Arguments(ErrorCode.UnknownServerError, "Abortable")]
    [Arguments(ErrorCode.OffsetMetadataTooLarge, "Abortable")]
    public async Task ClassifyTxnOffsetCommit_MatchesJavaHandler_RegardlessOfTv2(
        ErrorCode errorCode,
        string expected)
    {
        var classification = Enum.Parse<TransactionErrorClassification>(expected);
        var v1 = TransactionErrorClassifier.ClassifyTxnOffsetCommit(errorCode, tv2: false);
        var v2 = TransactionErrorClassifier.ClassifyTxnOffsetCommit(errorCode, tv2: true);

        await Assert.That(v1).IsEqualTo(classification);
        await Assert.That(v2).IsEqualTo(classification);
    }

    [Test]
    [Arguments(ErrorCode.UnknownMemberId, true)]
    [Arguments(ErrorCode.IllegalGeneration, true)]
    [Arguments(ErrorCode.GroupIdNotFound, true)]
    [Arguments(ErrorCode.StaleMemberEpoch, true)]
    [Arguments(ErrorCode.FencedInstanceId, false)]
    [Arguments(ErrorCode.TransactionAbortable, false)]
    public async Task IsConsumerGroupMismatch_CoversTheKip1319Equivalents(ErrorCode errorCode, bool expected)
    {
        await Assert.That(TransactionErrorClassifier.IsConsumerGroupMismatch(errorCode)).IsEqualTo(expected);
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

    [Test]
    [Arguments(ErrorCode.ProducerFenced, true)]
    [Arguments(ErrorCode.TransactionalIdAuthorizationFailed, true)]
    [Arguments(ErrorCode.ClusterAuthorizationFailed, true)]
    [Arguments(ErrorCode.InvalidProducerIdMapping, true)]
    [Arguments(ErrorCode.InvalidProducerEpoch, false)]
    [Arguments(ErrorCode.OutOfOrderSequenceNumber, false)]
    [Arguments(ErrorCode.UnknownProducerId, false)]
    [Arguments(ErrorCode.MessageTooLarge, false)]
    [Arguments(ErrorCode.RequestTimedOut, false)]
    [Arguments(ErrorCode.NotLeaderOrFollower, false)]
    public async Task ClassifyFailedBatch_FenceAndAuthorizationAreFatal_EverythingElseAbortable(
        ErrorCode errorCode,
        bool fatal)
    {
        var result = TransactionErrorClassifier.ClassifyFailedBatch(errorCode);

        await Assert.That(result).IsEqualTo(fatal
            ? TransactionErrorClassification.Fatal
            : TransactionErrorClassification.Abortable);
    }
}
