using BenchmarkDotNet.Attributes;
using Dekaf.Outbox;
using Dekaf.Outbox.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Repeated idle/backlogged discovery against a fixed SQLite database, including EF
/// context/query costs. Both revisions acquire leases before probing the same eight
/// buckets. No rows change during measurement; growth tests index work, not payload I/O.
/// </summary>
[MemoryDiagnoser]
public class OutboxPendingDiscoveryBenchmarks
{
    [Params(0, 1000, 100000)]
    public int PendingRows { get; set; }

    private SqliteConnection _connection = null!;
    private EfCoreOutboxStore<Context> _store = null!;
    private readonly int[] _buckets = [0, 1, 2, 3, 4, 5, 6, 7];

    [GlobalSetup]
    public async Task Setup()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<Context>().UseSqlite(_connection).Options;
        var factory = new PooledDbContextFactory<Context>(options);
        using var context = factory.CreateDbContext();
        context.Database.EnsureCreated();
        _store = new EfCoreOutboxStore<Context>(factory);
        await _store.AcquireBucketLeasesAsync(new OutboxLeaseRequest
        {
            RelayId = "benchmark", BucketCount = 8, LeaseDuration = TimeSpan.FromHours(1)
        });
        using var transaction = _connection.BeginTransaction();
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO dekaf_outbox_messages (Id, MessageId, Bucket, Topic, CreatedAtUtc) VALUES ($id, $message, $bucket, 'benchmark', '2026-09-01 00:00:00+00:00')";
        var id = command.Parameters.Add("$id", SqliteType.Integer);
        command.Parameters.AddWithValue("$message", Guid.Empty);
        var bucket = command.Parameters.Add("$bucket", SqliteType.Integer);
        command.Prepare();
        for (var index = 0; index < PendingRows; index++)
        {
            id.Value = index + 1;
            bucket.Value = index % 8;
            command.ExecuteNonQuery();
        }
        transaction.Commit();
        var pending = await Probe();
        if (pending.Count != (PendingRows == 0 ? 0 : 8))
            throw new InvalidOperationException("Unexpected pending bucket count.");
    }

    [Benchmark]
    public ValueTask<IReadOnlyList<int>> Probe() => _store.GetBucketsWithPendingAsync(_buckets);

    [GlobalCleanup]
    public void Cleanup() => _connection.Dispose();

    public sealed class Context(DbContextOptions<Context> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.UseDekafOutbox();
    }
}
