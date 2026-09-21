namespace Dekaf.StressTests.FaultInjection;

/// <summary>What the transactional producer knows about a transaction it started.</summary>
internal enum TransactionOutcomeKind
{
    /// <summary>CommitAsync returned: every record must be visible exactly once.</summary>
    Committed,

    /// <summary>AbortAsync returned: no record may be visible.</summary>
    Aborted,

    /// <summary>
    /// Neither completed (a commit that timed out after its EndTxn was written, a fatal state, a
    /// disposed producer). The broker settled it one way or the other; it must still be atomic.
    /// </summary>
    Unknown
}

internal sealed record TransactionOutcome(long TransactionId, TransactionOutcomeKind Kind);

/// <summary>A record a read-committed consumer saw: the transaction it belongs to and its index in it.</summary>
internal readonly record struct VisibleTransactionRecord(long TransactionId, int Index);

internal sealed record TransactionWindowVerification(
    bool Succeeded,
    int CommittedCount,
    int AbortedCount,
    int UnknownCount,
    IReadOnlyList<long> CommittedButIncomplete,
    IReadOnlyList<long> AbortedButVisible,
    IReadOnlyList<long> UnknownButPartial,
    IReadOnlyList<long> DuplicatedTransactions,
    IReadOnlyList<long> UnexpectedTransactions);

/// <summary>
/// Checks what a read-committed consumer saw against what the transactional producer was told.
/// Atomicity is the invariant: a transaction is visible whole and once, or not at all.
/// </summary>
internal static class TransactionWindowVerifier
{
    internal static TransactionWindowVerification Verify(
        IReadOnlyCollection<TransactionOutcome> outcomes,
        IEnumerable<VisibleTransactionRecord> visibleRecords,
        int recordsPerTransaction)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        ArgumentNullException.ThrowIfNull(visibleRecords);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(recordsPerTransaction);

        var kinds = new Dictionary<long, TransactionOutcomeKind>(outcomes.Count);
        foreach (var outcome in outcomes)
        {
            if (!kinds.TryAdd(outcome.TransactionId, outcome.Kind))
            {
                throw new ArgumentException(
                    $"Transaction {outcome.TransactionId} has more than one recorded outcome.",
                    nameof(outcomes));
            }
        }

        var visibleIndexes = new Dictionary<long, HashSet<int>>();
        var duplicated = new SortedSet<long>();
        var unexpected = new SortedSet<long>();
        foreach (var record in visibleRecords)
        {
            if (!kinds.ContainsKey(record.TransactionId)
                || record.Index < 0
                || record.Index >= recordsPerTransaction)
            {
                unexpected.Add(record.TransactionId);
                continue;
            }

            if (!visibleIndexes.TryGetValue(record.TransactionId, out var indexes))
            {
                indexes = [];
                visibleIndexes.Add(record.TransactionId, indexes);
            }

            if (!indexes.Add(record.Index))
            {
                duplicated.Add(record.TransactionId);
            }
        }

        var committedButIncomplete = new List<long>();
        var abortedButVisible = new List<long>();
        var unknownButPartial = new List<long>();
        int committed = 0, aborted = 0, unknown = 0;
        foreach (var (transactionId, kind) in kinds.OrderBy(pair => pair.Key))
        {
            var visible = visibleIndexes.TryGetValue(transactionId, out var indexes) ? indexes.Count : 0;
            switch (kind)
            {
                case TransactionOutcomeKind.Committed:
                    committed++;
                    if (visible != recordsPerTransaction)
                    {
                        committedButIncomplete.Add(transactionId);
                    }

                    break;

                case TransactionOutcomeKind.Aborted:
                    aborted++;
                    if (visible != 0)
                    {
                        abortedButVisible.Add(transactionId);
                    }

                    break;

                case TransactionOutcomeKind.Unknown:
                    unknown++;
                    if (visible != 0 && visible != recordsPerTransaction)
                    {
                        unknownButPartial.Add(transactionId);
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(outcomes), kind, "Unknown transaction outcome.");
            }
        }

        var succeeded = committedButIncomplete.Count == 0
            && abortedButVisible.Count == 0
            && unknownButPartial.Count == 0
            && duplicated.Count == 0
            && unexpected.Count == 0;

        return new TransactionWindowVerification(
            succeeded,
            committed,
            aborted,
            unknown,
            committedButIncomplete,
            abortedButVisible,
            unknownButPartial,
            [.. duplicated],
            [.. unexpected]);
    }
}
