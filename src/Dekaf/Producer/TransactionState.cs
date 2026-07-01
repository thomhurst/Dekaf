namespace Dekaf.Producer;

/// <summary>
/// Represents the current state of the producer's transaction lifecycle.
/// </summary>
internal enum TransactionState
{
    /// <summary>
    /// InitTransactionsAsync has not been called yet.
    /// </summary>
    Uninitialized,

    /// <summary>
    /// Transactions are initialized and ready to begin a new transaction.
    /// </summary>
    Ready,

    /// <summary>
    /// A transaction is currently in progress.
    /// </summary>
    InTransaction,

    /// <summary>
    /// The current transaction is being committed.
    /// </summary>
    CommittingTransaction,

    /// <summary>
    /// The current transaction is being aborted.
    /// </summary>
    AbortingTransaction,

    /// <summary>
    /// An abortable error occurred during the current transaction. Producing and committing
    /// are rejected; the caller must abort the transaction, after which the producer returns
    /// to <see cref="Ready"/> and can begin a new transaction.
    /// </summary>
    AbortableError,

    /// <summary>
    /// A fatal error occurred (e.g., ProducerFenced). No more transactions can be started.
    /// </summary>
    FatalError
}
