using System.Data.Common;
using System.Transactions;
using Dekaf.Outbox;
using Dekaf.Outbox.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Dekaf.Tests.Unit.Outbox;

public sealed class OutboxCommitNotificationTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task BucketHints_AccumulateSaves_AndUseOwnershipAtCommit(bool rollback)
    {
        var time = new ManualTimeProvider();
        using var notifier = new OutboxNotifier(time);
        notifier.SetOwnedBuckets([7]);
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<Context>().UseSqlite(connection)
            .UseDekafOutboxNotifications(notifier).Options;
        await using var context = new Context(options);
        await context.Database.EnsureCreatedAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        context.AddOutboxMessage(new OutboxMessage { Topic = "test", MessageId = Guid.NewGuid(), CreatedAtUtc = DateTimeOffset.UnixEpoch, Bucket = 1 });
        await context.SaveChangesAsync();
        context.AddOutboxMessage(new OutboxMessage { Topic = "test", MessageId = Guid.NewGuid(), CreatedAtUtc = DateTimeOffset.UnixEpoch, Bucket = 2 });
        await context.SaveChangesAsync();
        // A later save must not clear the transaction's earlier bucket hint.
        notifier.SetOwnedBuckets([1]);
        using var cancellation = new CancellationTokenSource();
        var waiting = notifier.WaitAsync(TimeSpan.FromSeconds(1), cancellation.Token);
        await Assert.That(waiting.IsCompleted).IsFalse();
        if (rollback)
        {
            await transaction.RollbackAsync();
            await Assert.That(waiting.IsCompleted).IsFalse();
            cancellation.Cancel();
            await Assert.That(async () => await waiting).Throws<OperationCanceledException>();
        }
        else
        {
            await transaction.CommitAsync();
            await waiting.AsTask().WaitAsync(TimeSpan.FromSeconds(30));
        }
    }

    [Test]
    public async Task BucketHints_ImplicitSavesAndAmbientCommits_FilterRemoteBuckets()
    {
        var time = new ManualTimeProvider();
        using var notifier = new OutboxNotifier(time);
        notifier.SetOwnedBuckets([1]);
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<Context>().UseSqlite(connection)
            .UseDekafOutboxNotifications(notifier).Options;
        await using var context = new Context(options);
        await context.Database.EnsureCreatedAsync();
        using var cancellation = new CancellationTokenSource();
        var waiting = notifier.WaitAsync(TimeSpan.FromSeconds(1), cancellation.Token);
        context.AddOutboxMessage(new OutboxMessage { Topic = "test", MessageId = Guid.NewGuid(), CreatedAtUtc = DateTimeOffset.UnixEpoch, Bucket = 2 });
        await context.SaveChangesAsync();
        await Assert.That(waiting.IsCompleted).IsFalse();
        var observer = new OutboxCommitObserver(notifier);
        using (var remote = new CommittableTransaction())
        {
            observer.ObserveAmbientCommit(remote, new HashSet<int> { 2 });
            remote.Commit();
        }
        await Assert.That(waiting.IsCompleted).IsFalse();
        using (var local = new CommittableTransaction())
        {
            observer.ObserveAmbientCommit(local, new HashSet<int> { 2 });
            observer.ObserveAmbientCommit(local, new HashSet<int> { 1 });
            local.Commit();
        }
        await waiting.AsTask().WaitAsync(TimeSpan.FromSeconds(30));
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Saving_DetectsChangesOnlyOnce_WithOrWithoutOutboxRows(bool asynchronous, bool outboxRow)
    {
        using var database = new Database();
        await using var context = database.CreateContext();
        for (var index = 1; index <= 1024; index++)
            context.Attach(new BusinessRow { Id = index });
        if (outboxRow)
            context.Set<OutboxMessage>().Add(Row());
        var detectionPasses = 0;
        context.ChangeTracker.DetectedAllChanges += (_, _) => detectionPasses++;

        await SaveAsync(context, asynchronous);

        await Assert.That(detectionPasses).IsEqualTo(1);
        await Assert.That(database.Notifier.Count).IsEqualTo(outboxRow ? 1 : 0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DirectAdd_NotifiesWithoutAcceptingTrackedChanges(bool asynchronous)
    {
        using var database = new Database();
        await using var context = database.CreateContext();
        var row = Row();
        context.Set<OutboxMessage>().Add(row);
        if (asynchronous)
            await context.SaveChangesAsync(acceptAllChangesOnSuccess: false);
        else
            context.SaveChanges(acceptAllChangesOnSuccess: false);

        await Assert.That(database.Notifier.Count).IsEqualTo(1);
        await Assert.That(context.Entry(row).State).IsEqualTo(EntityState.Added);
        await Assert.That(await context.Set<OutboxMessage>().CountAsync()).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NavigationDiscoveredInsert_NotifiesAfterCommit(bool asynchronous)
    {
        using var database = new Database();
        await using var context = database.CreateContext();
        var business = new BusinessRow { Id = 1 };
        context.Add(business);
        await SaveAsync(context, asynchronous);
        await Assert.That(database.Notifier.Count).IsEqualTo(0);

        // Adding to the CLR collection only becomes a tracked insert during EF's
        // change detection, after the SavingChanges interceptor has run.
        business.OutboxMessages.Add(Row());
        await SaveAsync(context, asynchronous);

        await Assert.That(database.Notifier.Count).IsEqualTo(1);
        await Assert.That(await context.Set<OutboxMessage>().CountAsync()).IsEqualTo(1);
    }

    [Test]
    public async Task UnchangedOutboxRows_DoNotNotifyForUnrelatedInserts()
    {
        using var database = new Database();
        await using var context = database.CreateContext();
        for (var index = 0; index < 1024; index++)
            context.Attach(Row(index + 1));
        context.Add(new BusinessRow { Id = 1 });

        await context.SaveChangesAsync();

        await Assert.That(database.Notifier.Count).IsEqualTo(0);
        await Assert.That(await context.Set<BusinessRow>().CountAsync()).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PooledContext_ReattachesNotificationsForEachLease(bool asynchronous)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var notifier = new CountingNotifier();
        var services = new ServiceCollection();
        services.AddPooledDbContextFactory<Context>(options =>
            options.UseSqlite(connection).UseDekafOutboxNotifications(notifier), poolSize: 1);
        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<Context>>();
        var previousLease = 0;
        for (var iteration = 1; iteration <= 3; iteration++)
        {
            await using var context = await factory.CreateDbContextAsync();
            if (iteration == 1)
                await context.Database.EnsureCreatedAsync();
            await Assert.That(context.ContextId.Lease).IsGreaterThan(previousLease);
            previousLease = context.ContextId.Lease;
            await SaveAsync(context, asynchronous);
            await Assert.That(notifier.Count).IsEqualTo(iteration - 1);
            context.AddOutboxMessage(Row());
            await SaveAsync(context, asynchronous);
            await Assert.That(notifier.Count).IsEqualTo(iteration);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AmbientTransaction_CoalescesSaves_AndOnlyNotifiesForCommit(bool rollback)
    {
        // SQLite does not support ambient enlistment. Exercise the real transaction
        // completion component separately; relational commit behavior is covered below.
        var notifier = new CountingNotifier();
        var observer = new OutboxCommitObserver(notifier);
        using var transaction = new CommittableTransaction();
        observer.ObserveAmbientCommit(transaction);
        observer.ObserveAmbientCommit(transaction);
        await Assert.That(notifier.Count).IsEqualTo(0);
        if (rollback)
            transaction.Rollback();
        else
            transaction.Commit();
        await Assert.That(notifier.Count).IsEqualTo(rollback ? 0 : 1);
    }

    [Test]
    public async Task ConfiguredStoreRegistration_ConnectsFactoryToRelayNotifier()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var services = new ServiceCollection();
        var notifier = new CountingNotifier();
        services.AddSingleton<IOutboxNotifier>(notifier);
        services.AddDekafEntityFrameworkCoreOutboxStore<Context>((_, options) => options.UseSqlite(connection));
        await using var provider = services.BuildServiceProvider();
        await using var context = await provider.GetRequiredService<IDbContextFactory<Context>>().CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
        context.AddOutboxMessage(Row());
        await context.SaveChangesAsync();
        await Assert.That(notifier.Count).IsEqualTo(1);
        await Assert.That(provider.GetRequiredService<IOutboxStore>()).IsTypeOf<EfCoreOutboxStore<Context>>();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ImplicitCommit_NotifiesOnceAfterRowsArePersisted(bool asynchronous)
    {
        using var database = new Database();
        await using var context = database.CreateContext();
        context.AddOutboxMessage(Row());
        context.AddOutboxMessage(Row());
        await SaveAsync(context, asynchronous);
        await Assert.That(database.Notifier.Count).IsEqualTo(1);
        await Assert.That(await context.Set<OutboxMessage>().CountAsync()).IsEqualTo(2);
        await SaveAsync(context, asynchronous);
        await Assert.That(database.Notifier.Count).IsEqualTo(1);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task ExplicitTransaction_NotifiesOnlyAfterCommit(bool asynchronous, bool rollback)
    {
        using var database = new Database();
        await using var context = database.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        for (var index = 0; index < 2; index++)
        {
            context.AddOutboxMessage(Row());
            await SaveAsync(context, asynchronous);
            await Assert.That(database.Notifier.Count).IsEqualTo(0);
        }

        if (rollback)
        {
            if (asynchronous)
                await transaction.RollbackAsync();
            else
                transaction.Rollback();
        }
        else if (asynchronous)
            await transaction.CommitAsync();
        else
            transaction.Commit();

        await Assert.That(database.Notifier.Count).IsEqualTo(rollback ? 0 : 1);
        await Assert.That(await context.Set<OutboxMessage>().CountAsync()).IsEqualTo(rollback ? 0 : 2);
    }

    [Test]
    public async Task FailedSave_DoesNotNotify_AndLaterSaveCanNotify()
    {
        using var database = new Database();
        await using var context = database.CreateContext();
        var invalid = Row();
        context.AddOutboxMessage(invalid);
        context.Entry(invalid).Property(row => row.Topic).CurrentValue = null!;
        await Assert.That(async () => await context.SaveChangesAsync()).Throws<DbUpdateException>();
        await Assert.That(database.Notifier.Count).IsEqualTo(0);
        context.Entry(invalid).Property(row => row.Topic).CurrentValue = "outbox-notification";
        await context.SaveChangesAsync();
        await Assert.That(database.Notifier.Count).IsEqualTo(1);
    }

    [Test]
    public async Task CanceledSave_DoesNotNotify_AndContextReuseDoesNotLeakPendingState()
    {
        using var database = new Database();
        await using var context = database.CreateContext();
        context.AddOutboxMessage(Row());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.That(async () => await context.SaveChangesAsync(cancellation.Token)).Throws<OperationCanceledException>();
        await Assert.That(database.Notifier.Count).IsEqualTo(0);
        context.ChangeTracker.Clear();
        await context.SaveChangesAsync();
        await Assert.That(database.Notifier.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CancellationAfterCommandInspection_DoesNotNotifyOnLaterEmptySave(bool asynchronous)
    {
        var cancel = new CancelFirstSaveCommand();
        using var database = new Database(cancel);
        await using var context = database.CreateContext();
        context.AddOutboxMessage(Row());
        await Assert.That(async () => await SaveAsync(context, asynchronous)).Throws<OperationCanceledException>();
        await Assert.That(database.Notifier.Count).IsEqualTo(0);
        context.ChangeTracker.Clear();
        await SaveAsync(context, asynchronous);
        await Assert.That(database.Notifier.Count).IsEqualTo(0);
        context.AddOutboxMessage(Row());
        await SaveAsync(context, asynchronous);
        await Assert.That(database.Notifier.Count).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SavedChangesHandler_CanSaveAgainWithoutLosingCommitNotification(bool asynchronous)
    {
        using var database = new Database();
        await using var context = database.CreateContext();
        var nested = false;
        context.SavedChanges += (_, _) =>
        {
            if (nested) return;
            nested = true;
            context.SaveChanges();
        };
        context.AddOutboxMessage(Row());
        await SaveAsync(context, asynchronous);
        await Assert.That(database.Notifier.Count).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DisabledAutomaticDetection_PreservesExplicitlyAddedNotification(bool asynchronous)
    {
        using var database = new Database();
        await using var context = database.CreateContext();
        context.ChangeTracker.AutoDetectChangesEnabled = false;
        context.Set<OutboxMessage>().Add(Row());
        await SaveAsync(context, asynchronous);
        await Assert.That(database.Notifier.Count).IsEqualTo(1);
        await Assert.That(context.ChangeTracker.AutoDetectChangesEnabled).IsFalse();
    }

    [Test]
    public async Task SharedOptions_KeepPendingSavesIndependentAcrossContexts()
    {
        using var database = new Database();
        await using var first = database.CreateContext();
        await using var second = database.CreateContext();
        first.AddOutboxMessage(Row());
        await second.SaveChangesAsync();
        await Assert.That(database.Notifier.Count).IsEqualTo(0);
        await first.SaveChangesAsync();
        await Assert.That(database.Notifier.Count).IsEqualTo(1);
    }

    private static Task SaveAsync(DbContext context, bool asynchronous)
    {
        if (asynchronous)
            return context.SaveChangesAsync();
        context.SaveChanges();
        return Task.CompletedTask;
    }

    private static OutboxMessage Row(long id = 0) => new()
    {
        Id = id, MessageId = Guid.NewGuid(), Topic = "outbox-notification", Bucket = 0,
        Value = [1], CreatedAtUtc = DateTimeOffset.UnixEpoch
    };

    private sealed class Database : IDisposable
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly DbContextOptions<Context> _options;
        public CountingNotifier Notifier { get; } = new();

        public Database(params IInterceptor[] interceptors)
        {
            _connection.Open();
            _options = new DbContextOptionsBuilder<Context>().UseSqlite(_connection)
                .UseDekafOutboxNotifications(Notifier).AddInterceptors(interceptors).Options;
            using var context = CreateContext();
            context.Database.EnsureCreated();
        }

        public Context CreateContext() => new(_options);
        public void Dispose() => _connection.Dispose();
    }

    private sealed class Context(DbContextOptions<Context> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.UseDekafOutbox();
            modelBuilder.Entity<BusinessRow>().HasKey(row => row.Id);
            modelBuilder.Entity<BusinessRow>().HasMany(row => row.OutboxMessages)
                .WithOne().HasForeignKey("BusinessId");
        }
    }

    private sealed class BusinessRow
    {
        public int Id { get; set; }
        public List<OutboxMessage> OutboxMessages { get; } = [];
    }

    private sealed class CancelFirstSaveCommand : DbCommandInterceptor
    {
        private bool _cancel = true;

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            if (_cancel && eventData.CommandSource == CommandSource.SaveChanges)
            {
                _cancel = false;
                throw new OperationCanceledException("Cancel after the notification observer inspects the write.");
            }
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default) => new(ReaderExecuting(command, eventData, result));
    }

    private sealed class CountingNotifier : IOutboxNotifier
    {
        public int Count { get; private set; }
        public void NotifyCommitted() => Count++;
        public ValueTask WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
