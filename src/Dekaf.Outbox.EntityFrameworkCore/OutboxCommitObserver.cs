using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Dekaf.Outbox.EntityFrameworkCore;

// One observer per options configuration, shared safely by its contexts (including pooled
// contexts). Weak keys prevent disposed contexts or externally owned transactions being retained.
internal sealed class OutboxCommitObserver
{
    private readonly IOutboxNotifier _notifier;
    private readonly ConditionalWeakTable<DbContext, PendingSave> _saves = new();
    private readonly ConditionalWeakTable<DbTransaction, PendingCommit> _transactions = new();
    private readonly ConditionalWeakTable<Transaction, AmbientCommit> _ambientTransactions = new();
    private readonly ConditionalWeakTable<Transaction, AmbientCommit>.CreateValueCallback _createAmbientCommit;
    private readonly ConditionalWeakTable<DbContext, PendingSave>.CreateValueCallback _createPendingSave;

    internal OutboxCommitObserver(IOutboxNotifier notifier)
    {
        _notifier = notifier;
        _createPendingSave = context => new PendingSave(context, notifier is IOutboxBucketNotifier);
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
        var pending = _saves.GetValue(context, _createPendingSave);
        pending.Stop(context);
        pending.Refresh();
        pending.Start(context);
    }

    // Inspect only EF's Added state map. Do not invoke DetectChanges here: EF runs
    // its normal pass after all SavingChanges interceptors. PendingSave observes
    // that pass so navigation-discovered inserts are included before SQL is sent.
#pragma warning disable EF1001
    private static bool HasAddedOutboxRows(IStateManager stateManager, HashSet<int>? buckets)
    {
        if (stateManager.ChangedCount == 0)
            return false;
        var entries = stateManager.GetEntriesForState(added: true);
        if (entries is Dictionary<object, InternalEntityEntry>.ValueCollection added)
        {
            foreach (var entry in added)
            {
                if (entry.Entity is OutboxMessage message)
                {
                    if (buckets is null)
                        return true;
                    buckets.Add(message.Bucket);
                }
            }
            return buckets is { Count: > 0 };
        }
        foreach (var entry in entries)
        {
            if (entry.Entity is OutboxMessage message)
            {
                if (buckets is null)
                    return true;
                buckets.Add(message.Bucket);
            }
        }
        return buckets is { Count: > 0 };
    }
#pragma warning restore EF1001

    private void Saved(DbContext? context)
    {
        if (context is null || !_saves.TryGetValue(context, out var pending))
            return;
        pending.Stop(context);
        if (!pending.HasRows)
            return;
        pending.HasRows = false;
        if (context.Database.CurrentTransaction is { } transaction)
        {
            _transactions.GetValue(transaction.GetDbTransaction(), static _ => new PendingCommit()).Add(pending.Buckets);
        }
        else if ((context.Database.GetEnlistedTransaction() ?? Transaction.Current) is { } ambient)
        {
            // The completion subscription never retains the DbContext.
            // Disposing a context before its ambient transaction completes remains safe.
            ObserveAmbientCommit(ambient, pending.Buckets);
        }
        else
        {
            // EF has already committed/disposed any implicit transaction. Single-statement
            // autocommit also reaches here, without requiring transaction callbacks.
            Notify(pending.Buckets);
        }
    }

    private void Notify(HashSet<int>? buckets)
    {
        if (buckets is not null && _notifier is IOutboxBucketNotifier notifier)
            notifier.NotifyCommitted(buckets);
        else
            _notifier.NotifyCommitted();
    }

    internal void ObserveAmbientCommit(Transaction transaction, HashSet<int>? buckets = null)
        => _ambientTransactions.GetValue(transaction, _createAmbientCommit).Pending.Add(buckets);

    private void Failed(DbContext? context)
    {
        if (context is not null && _saves.TryGetValue(context, out var pending))
        {
            pending.Stop(context);
            pending.HasRows = false;
        }
    }

    private void Committed(DbTransaction transaction)
    {
        if (_transactions.TryGetValue(transaction, out var pending) && _transactions.Remove(transaction))
            pending.Notify(this);
    }

    // The context-keyed weak table permits its value to cache a context-scoped service
    // without retaining an otherwise unreachable context. Pooled leases reuse it.
#pragma warning disable EF1001
    private sealed class PendingSave
    {
        private readonly IStateManager _stateManager;
        private readonly EventHandler<DetectedChangesEventArgs> _detected;
        public bool HasRows;
        public HashSet<int>? Buckets { get; }

        public PendingSave(DbContext context, bool captureBuckets)
        {
            Buckets = captureBuckets ? new HashSet<int>() : null;
            _stateManager = context.GetService<IStateManager>();
            _detected = OnDetected;
        }

        public void Refresh()
        {
            Buckets?.Clear();
            HasRows = HasAddedOutboxRows(_stateManager, Buckets);
        }

        public void Start(DbContext context) => context.ChangeTracker.DetectedAllChanges += _detected;
        public void Stop(DbContext context) => context.ChangeTracker.DetectedAllChanges -= _detected;

        private void OnDetected(object? sender, DetectedChangesEventArgs args) => Refresh();
    }
#pragma warning restore EF1001

    // Multiple contexts can save into one ambient or externally shared transaction.
    // Accumulate only distinct bucket ids; never retain messages, payloads or contexts.
    private sealed class PendingCommit
    {
        private HashSet<int>? _buckets;
        private int _singleBucket = -1;
        private bool _broadcast;

        public void Add(HashSet<int>? buckets)
        {
            lock (this)
            {
                if (buckets is null)
                    _broadcast = true;
                else
                {
                    // The common single-bucket transaction needs no separate set allocation.
                    foreach (var bucket in buckets)
                    {
                        if (_singleBucket == -1)
                            _singleBucket = bucket;
                        else if (_singleBucket != bucket)
                            (_buckets ??= new HashSet<int> { _singleBucket }).Add(bucket);
                    }
                }
            }
        }

        public void Notify(OutboxCommitObserver owner)
        {
            lock (this)
            {
                if (!_broadcast && _buckets is null && owner._notifier is IOutboxBucketNotifier notifier)
                    notifier.NotifyCommitted(_singleBucket);
                else
                    owner.Notify(_broadcast ? null : _buckets);
            }
        }
    }

    private sealed class AmbientCommit
    {
        private readonly OutboxCommitObserver _owner;
        private readonly Transaction _transaction;
        public PendingCommit Pending { get; } = new();

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
                Pending.Notify(_owner);
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
