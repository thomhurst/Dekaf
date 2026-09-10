using System.Transactions;
using Dekaf.Outbox;
using Dekaf.Outbox.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dekaf.Tests.Unit.Outbox;

public sealed class OutboxCommitNotificationTests
{
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

    private static OutboxMessage Row() => new()
    {
        MessageId = Guid.NewGuid(), Topic = "outbox-notification", Bucket = 0,
        Value = [1], CreatedAtUtc = DateTimeOffset.UnixEpoch
    };

    private sealed class Database : IDisposable
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly DbContextOptions<Context> _options;
        public CountingNotifier Notifier { get; } = new();

        public Database()
        {
            _connection.Open();
            _options = new DbContextOptionsBuilder<Context>().UseSqlite(_connection)
                .UseDekafOutboxNotifications(Notifier).Options;
            using var context = CreateContext();
            context.Database.EnsureCreated();
        }

        public Context CreateContext() => new(_options);
        public void Dispose() => _connection.Dispose();
    }

    private sealed class Context(DbContextOptions<Context> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.UseDekafOutbox();
    }

    private sealed class CountingNotifier : IOutboxNotifier
    {
        public int Count { get; private set; }
        public void NotifyCommitted() => Count++;
        public ValueTask WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
