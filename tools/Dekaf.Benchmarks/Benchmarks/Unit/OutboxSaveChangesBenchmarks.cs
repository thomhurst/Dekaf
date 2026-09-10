using System.Reflection;
using System.Threading.Channels;
using BenchmarkDotNet.Attributes;
using Dekaf.Outbox;
using Dekaf.Outbox.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures synchronous/asynchronous unchanged EF saves, rolled-back inserts, and
/// committed inserts with cleanup. SQLite may complete I/O synchronously; async cases
/// exercise EF async interception, not asynchronous database I/O latency.
/// Commit cases include notification consumption, detachment and a prepared SQL delete
/// in the measured operation, keeping every invocation at the same database size.
/// Allocation scope includes EF/SQLite costs; this is not a Kafka per-message fixture.
/// </summary>
[MemoryDiagnoser]
public class OutboxSaveChangesBenchmarks
{
    [Params(0, 1024)]
    public int TrackedBusinessRows { get; set; }

    private SqliteConnection _connection = null!;
    private ServiceProvider _provider = null!;
    private Context _context = null!;
    private OutboxMessage _message = null!;
    private SqliteCommand _deleteMessage = null!;
    private Func<TimeSpan, CancellationToken, ValueTask>? _waitForNotification;
    private ChannelReader<byte>? _notificationReader;

    [GlobalSetup]
    public async Task Setup()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<Context>().UseSqlite(_connection);
        var services = new ServiceCollection();
        services.AddDekafOutboxRelay();
        _provider = services.BuildServiceProvider();

        // The baseline predates notifications. Keep identical SQL/tracker work and
        // enable the new feature when available; reflection runs only during setup.
        var extension = typeof(OutboxDbContextExtensions).Assembly.GetType(
            "Dekaf.Outbox.EntityFrameworkCore.OutboxNotificationOptionsExtensions");
        if (extension is not null)
        {
            var notifierType = typeof(OutboxMessage).Assembly.GetType("Dekaf.Outbox.IOutboxNotifier", throwOnError: true)!;
            var configure = extension.GetMethod("UseDekafOutboxNotifications",
                BindingFlags.Public | BindingFlags.Static, [typeof(DbContextOptionsBuilder), notifierType])
                ?? throw new InvalidOperationException("Expected UseDekafOutboxNotifications(DbContextOptionsBuilder, IOutboxNotifier).");
            var notifier = _provider.GetRequiredService(notifierType);
            configure.Invoke(null, [options, notifier]);
            _waitForNotification = (notifierType.GetMethod("WaitAsync")
                ?? throw new InvalidOperationException("Expected IOutboxNotifier.WaitAsync(TimeSpan, CancellationToken)."))
                .CreateDelegate<Func<TimeSpan, CancellationToken, ValueTask>>(notifier);
            // Observe negative cases without creating a pending reader or allocating
            // a cancellation source on every unchanged save or rollback.
            _notificationReader = (notifier.GetType().GetField("_notifications",
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(notifier) as Channel<byte>)?.Reader
                ?? throw new InvalidOperationException("Expected OutboxNotifier's Channel<byte> notification storage.");
        }

        _context = new Context(options.Options);
        _context.Database.EnsureCreated();
        _deleteMessage = _connection.CreateCommand();
        _deleteMessage.CommandText = $"DELETE FROM {OutboxModelOptions.DefaultMessagesTableName} WHERE Id = 1";
        _deleteMessage.Prepare();
        for (var index = 1; index <= TrackedBusinessRows; index++)
            _context.Attach(new BusinessRow { Id = index });
        _message = new OutboxMessage
        {
            Id = 1,
            MessageId = Guid.Parse("ca8c386b-96a7-407b-b68e-0845c5488cb4"),
            Topic = "outbox-save", Bucket = 0, Value = [1], CreatedAtUtc = DateTimeOffset.UnixEpoch
        };
        if (SaveWithoutOutbox() != 0 || SaveAndRollbackOutbox() != 1 ||
            SaveAndCommitOutboxWithCleanup() != 1 || SaveAutocommitOutboxWithCleanup() != 1 ||
            await SaveWithoutOutboxAsync() != 0 || await SaveAndRollbackOutboxAsync() != 1 ||
            await SaveAndCommitOutboxWithCleanupAsync() != 1 || await SaveAutocommitOutboxWithCleanupAsync() != 1 ||
            _context.Set<OutboxMessage>().Any())
            throw new InvalidOperationException("Each save must preserve its expected row count and empty database state.");
    }

    [Benchmark]
    public int SaveWithoutOutbox()
    {
        var saved = _context.SaveChanges();
        AssertNoNotification();
        return saved;
    }

    [Benchmark]
    public int SaveAndRollbackOutbox()
    {
        using var transaction = _context.Database.BeginTransaction();
        _context.AddOutboxMessage(_message);
        var saved = _context.SaveChanges();
        transaction.Rollback();
        _context.Entry(_message).State = EntityState.Detached;
        AssertNoNotification();
        return saved;
    }

    [Benchmark]
    public int SaveAndCommitOutboxWithCleanup()
    {
        int saved;
        using (var transaction = _context.Database.BeginTransaction())
        {
            _context.AddOutboxMessage(_message);
            saved = _context.SaveChanges();
            transaction.Commit();
        }
        ConsumeCommittedNotificationAsync().GetAwaiter().GetResult();
        CleanCommittedMessage();
        return saved;
    }

    [Benchmark]
    public int SaveAutocommitOutboxWithCleanup()
    {
        _context.AddOutboxMessage(_message);
        var saved = _context.SaveChanges();
        ConsumeCommittedNotificationAsync().GetAwaiter().GetResult();
        CleanCommittedMessage();
        return saved;
    }

    [Benchmark]
    public async Task<int> SaveWithoutOutboxAsync()
    {
        var saved = await _context.SaveChangesAsync();
        AssertNoNotification();
        return saved;
    }

    [Benchmark]
    public async Task<int> SaveAndRollbackOutboxAsync()
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        _context.AddOutboxMessage(_message);
        var saved = await _context.SaveChangesAsync();
        await transaction.RollbackAsync();
        _context.Entry(_message).State = EntityState.Detached;
        AssertNoNotification();
        return saved;
    }

    [Benchmark]
    public async Task<int> SaveAndCommitOutboxWithCleanupAsync()
    {
        int saved;
        await using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            _context.AddOutboxMessage(_message);
            saved = await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        await ConsumeCommittedNotificationAsync();
        CleanCommittedMessage();
        return saved;
    }

    [Benchmark]
    public async Task<int> SaveAutocommitOutboxWithCleanupAsync()
    {
        _context.AddOutboxMessage(_message);
        var saved = await _context.SaveChangesAsync();
        await ConsumeCommittedNotificationAsync();
        CleanCommittedMessage();
        return saved;
    }

    private void AssertNoNotification()
    {
        if (_notificationReader?.TryPeek(out _) is true)
            throw new InvalidOperationException("An unchanged save or rolled-back insert must not publish a notification.");
    }

    private async ValueTask ConsumeCommittedNotificationAsync()
    {
        // Drain each notification so the measured commit does not merely write to
        // an already-full coalescing channel. Missing notification invalidates the case.
        if (_waitForNotification is not { } wait)
            return;
        if (_notificationReader!.TryPeek(out _))
            await wait(Timeout.InfiniteTimeSpan, default);
        else
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await wait(Timeout.InfiniteTimeSpan, timeout.Token);
        }
    }

    private void CleanCommittedMessage()
    {
        _context.Entry(_message).State = EntityState.Detached;
        if (_deleteMessage.ExecuteNonQuery() != 1)
            throw new InvalidOperationException("A committed insert must leave exactly one row to clean up.");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _deleteMessage.Dispose();
        _context.Dispose();
        _connection.Dispose();
        _provider.Dispose();
    }

    private sealed class Context(DbContextOptions<Context> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.UseDekafOutbox();
            modelBuilder.Entity<BusinessRow>().HasKey(row => row.Id);
        }
    }

    private sealed class BusinessRow
    {
        public int Id { get; set; }
    }
}
