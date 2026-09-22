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
            Protocol.ErrorCode.UnknownTopicId => TransactionErrorClassification.Retriable,
            Protocol.ErrorCode.UnknownTopicOrPartition => TransactionErrorClassification.Retriable,

            // Abortable: the current transaction is broken and must be aborted, but the
            // producer stays usable for a new transaction. TransactionAbortable is the
            // dedicated KIP-890 signal; the default below also lands here per KIP-1050.
            Protocol.ErrorCode.TransactionAbortable => TransactionErrorClassification.Abortable,
            Protocol.ErrorCode.InvalidTxnState => TransactionErrorClassification.Abortable,
            Protocol.ErrorCode.GroupIdNotFound => TransactionErrorClassification.Abortable,
            Protocol.ErrorCode.StaleMemberEpoch => TransactionErrorClassification.Abortable,

            _ => TransactionErrorClassification.Abortable
        };
    }

    /// <summary>
    /// The error code a failed batch reports to the transaction: the delivery timeout as
    /// <c>RequestTimedOut</c>, a Kafka error as its code, anything else (a local failure such as
    /// compression or disposal) as <c>UnknownServerError</c>, which is abortable.
    /// </summary>
    internal static Protocol.ErrorCode GetFailedBatchErrorCode(Exception exception) => exception switch
    {
        Errors.KafkaTimeoutException => Protocol.ErrorCode.RequestTimedOut,
        Errors.KafkaException { ErrorCode: { } errorCode } => errorCode,
        _ => Protocol.ErrorCode.UnknownServerError
    };

    /// <summary>
    /// Classifies a produce batch of a transactional producer that failed terminally (Java
    /// <c>TransactionManager.maybeTransitionToErrorState</c>). A fence or an authorization failure
    /// leaves the producer unusable; every other failure means records the caller produced are not
    /// in the transaction, so it can only be aborted. Never <see cref="TransactionErrorClassification.Retriable"/>:
    /// the batch has already failed. <c>InvalidProducerEpoch</c> is abortable here, as in Java
    /// (KIP-588): on a produce it can also mean the coordinator timed the transaction out and bumped
    /// the epoch, and the abort's EndTxn reports a real fence.
    /// </summary>
    internal static TransactionErrorClassification ClassifyFailedBatch(Protocol.ErrorCode errorCode) =>
        errorCode is Protocol.ErrorCode.ProducerFenced
            or Protocol.ErrorCode.TransactionalIdAuthorizationFailed
            or Protocol.ErrorCode.ClusterAuthorizationFailed
            or Protocol.ErrorCode.InvalidProducerIdMapping
            ? TransactionErrorClassification.Fatal
            : TransactionErrorClassification.Abortable;
}
