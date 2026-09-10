using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace Dekaf.Outbox.EntityFrameworkCore;

// One observer per options configuration, shared safely by its contexts (including pooled
// contexts). Weak keys prevent disposed contexts or externally owned transactions being retained.
internal sealed class OutboxCommitObserver
{
    private readonly IOutboxNotifier _notifier;
    private readonly ConditionalWeakTable<DbContext, PendingSave> _saves = new();
    private readonly ConditionalWeakTable<DbTransaction, PendingSave> _transactions = new();
    private readonly ConditionalWeakTable<Transaction, AmbientCommit> _ambientTransactions = new();
    private readonly ConditionalWeakTable<Transaction, AmbientCommit>.CreateValueCallback _createAmbientCommit;

    internal OutboxCommitObserver(IOutboxNotifier notifier)
    {
        _notifier = notifier;
        _createAmbientCommit = transaction => new AmbientCommit(this, transaction);
        SaveChanges = new SaveObserver(this);
        Transactions = new TransactionObserver(this);
    }

    internal SaveChangesInterceptor SaveChanges { get; }
    internal DbTransactionInterceptor Transactions { get; }

    private void Saving(DbContext? context)
    {
        if (context is null)
            return;
        var pending = _saves.GetOrCreateValue(context);
        pending.HasRows = false;
        foreach (var entry in context.ChangeTracker.Entries<OutboxMessage>())
        {
            if (entry.State != EntityState.Added)
                continue;
            pending.HasRows = true;
            break;
        }
    }

    private void Saved(DbContext? context)
    {
        if (context is null || !_saves.TryGetValue(context, out var pending) || !pending.HasRows)
            return;
        pending.HasRows = false;
        if (context.Database.CurrentTransaction is { } transaction)
        {
            _transactions.GetOrCreateValue(transaction.GetDbTransaction()).HasRows = true;
        }
        else if ((context.Database.GetEnlistedTransaction() ?? Transaction.Current) is { } ambient)
        {
            // The completion subscription never retains the DbContext.
            // Disposing a context before its ambient transaction completes remains safe.
            ObserveAmbientCommit(ambient);
        }
        else
        {
            // EF has already committed/disposed any implicit transaction. Single-statement
            // autocommit also reaches here, without requiring transaction callbacks.
            _notifier.NotifyCommitted();
        }
    }

    internal void ObserveAmbientCommit(Transaction transaction)
        => _ambientTransactions.GetValue(transaction, _createAmbientCommit);

    private void Failed(DbContext? context)
    {
        if (context is not null && _saves.TryGetValue(context, out var pending))
            pending.HasRows = false;
    }

    private void Committed(DbTransaction transaction)
    {
        if (!_transactions.TryGetValue(transaction, out var pending))
            return;
        _transactions.Remove(transaction);
        if (pending.HasRows)
            _notifier.NotifyCommitted();
    }

    private sealed class PendingSave
    {
        public bool HasRows;
    }

    private sealed class AmbientCommit
    {
        private readonly OutboxCommitObserver _owner;
        private readonly Transaction _transaction;

        public AmbientCommit(OutboxCommitObserver owner, Transaction transaction)
        {
            _owner = owner;
            _transaction = transaction;
            transaction.TransactionCompleted += OnCompleted;
        }

        private void OnCompleted(object? sender, TransactionEventArgs args)
        {
            _transaction.TransactionCompleted -= OnCompleted;
            _owner._ambientTransactions.Remove(_transaction);
            if (args.Transaction?.TransactionInformation.Status == TransactionStatus.Committed)
                _owner._notifier.NotifyCommitted();
        }
    }

    private sealed class SaveObserver(OutboxCommitObserver owner) : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            owner.Saving(eventData.Context);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => new(SavingChanges(eventData, result));

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            owner.Saved(eventData.Context);
            return result;
        }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) => new(SavedChanges(eventData, result));

        public override void SaveChangesFailed(DbContextErrorEventData eventData) => owner.Failed(eventData.Context);
        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData,
            CancellationToken cancellationToken = default)
        {
            owner.Failed(eventData.Context);
            return Task.CompletedTask;
        }

        public override void SaveChangesCanceled(DbContextEventData eventData) => owner.Failed(eventData.Context);
        public override Task SaveChangesCanceledAsync(DbContextEventData eventData,
            CancellationToken cancellationToken = default)
        {
            owner.Failed(eventData.Context);
            return Task.CompletedTask;
        }
    }

    private sealed class TransactionObserver(OutboxCommitObserver owner) : DbTransactionInterceptor
    {
        public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
            => owner.Committed(transaction);

        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            owner.Committed(transaction);
            return Task.CompletedTask;
        }

        public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
            => owner._transactions.Remove(transaction);

        public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            owner._transactions.Remove(transaction);
            return Task.CompletedTask;
        }
    }
}
