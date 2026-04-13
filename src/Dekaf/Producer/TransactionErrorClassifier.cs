namespace Dekaf.Producer;

internal enum TransactionErrorClassification
{
    Retriable,
    Abortable,
    Fatal
}

internal static class TransactionErrorClassifier
{
    internal static TransactionErrorClassification Classify(Protocol.ErrorCode errorCode, bool tv2)
    {
        return errorCode switch
        {
            Protocol.ErrorCode.ProducerFenced => TransactionErrorClassification.Fatal,
            Protocol.ErrorCode.TransactionalIdAuthorizationFailed => TransactionErrorClassification.Fatal,
            Protocol.ErrorCode.TransactionCoordinatorFenced => TransactionErrorClassification.Fatal,

            Protocol.ErrorCode.InvalidProducerIdMapping => tv2
                ? TransactionErrorClassification.Fatal
                : TransactionErrorClassification.Abortable,

            Protocol.ErrorCode.CoordinatorLoadInProgress => TransactionErrorClassification.Retriable,
            Protocol.ErrorCode.CoordinatorNotAvailable => TransactionErrorClassification.Retriable,
            Protocol.ErrorCode.NotCoordinator => TransactionErrorClassification.Retriable,
            Protocol.ErrorCode.ConcurrentTransactions => TransactionErrorClassification.Retriable,

            _ => TransactionErrorClassification.Abortable
        };
    }
}
