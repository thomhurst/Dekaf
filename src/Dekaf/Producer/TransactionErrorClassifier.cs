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
            // Fatal (application-recoverable per KIP-1050): the producer is fenced or
            // irrecoverably broken and must be closed; a new instance is required.
            Protocol.ErrorCode.ProducerFenced => TransactionErrorClassification.Fatal,
            Protocol.ErrorCode.TransactionalIdAuthorizationFailed => TransactionErrorClassification.Fatal,
            Protocol.ErrorCode.TransactionCoordinatorFenced => TransactionErrorClassification.Fatal,
            Protocol.ErrorCode.InvalidProducerEpoch => TransactionErrorClassification.Fatal,
            Protocol.ErrorCode.FencedInstanceId => TransactionErrorClassification.Fatal,
            Protocol.ErrorCode.UnknownMemberId => TransactionErrorClassification.Fatal,
            Protocol.ErrorCode.IllegalGeneration => TransactionErrorClassification.Fatal,

            // In Transactions V2 (KIP-890) an unexpected producer-id mapping is fatal;
            // in V1 the transaction can still be aborted.
            Protocol.ErrorCode.InvalidProducerIdMapping => tv2
                ? TransactionErrorClassification.Fatal
                : TransactionErrorClassification.Abortable,

            // Retriable: transient coordinator/transaction state; retry with backoff.
            Protocol.ErrorCode.CoordinatorLoadInProgress => TransactionErrorClassification.Retriable,
            Protocol.ErrorCode.CoordinatorNotAvailable => TransactionErrorClassification.Retriable,
            Protocol.ErrorCode.NotCoordinator => TransactionErrorClassification.Retriable,
            Protocol.ErrorCode.ConcurrentTransactions => TransactionErrorClassification.Retriable,

            // Abortable: the current transaction is broken and must be aborted, but the
            // producer stays usable for a new transaction. TransactionAbortable is the
            // dedicated KIP-890 signal; the default below also lands here per KIP-1050.
            Protocol.ErrorCode.TransactionAbortable => TransactionErrorClassification.Abortable,
            Protocol.ErrorCode.InvalidTxnState => TransactionErrorClassification.Abortable,

            _ => TransactionErrorClassification.Abortable
        };
    }
}
