using Dekaf.Outbox;
using Dekaf.Outbox.EntityFrameworkCore;
using Dekaf.Serialization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Dekaf.Tests.Unit.Outbox;

public class EfCoreOutboxStoreTests
{
    private static readonly int[] AllBuckets = [0, 1, 2, 3];
    private static readonly int[] BucketsZeroAndTwo = [0, 2];

    private static OutboxLeaseRequest Request(string relayId, int bucketCount = 4) => new()
    {
        RelayId = relayId,
        BucketCount = bucketCount,
        LeaseDuration = TimeSpan.FromSeconds(30)
    };

    [Test]
    public async Task SingleRelay_AcquiresAllBuckets()
    {
        using var db = new SqliteOutboxDatabase();
        var store = db.CreateStore();

        var owned = await store.AcquireBucketLeasesAsync(Request("relay-a"));

        await Assert.That(owned).IsEquivalentTo(AllBuckets);
    }

    [Test]
    public async Task Renewal_KeepsOwnershipAndExtendsExpiry()
    {
        using var db = new SqliteOutboxDatabase();
        var store = db.CreateStore();

        await store.AcquireBucketLeasesAsync(Request("relay-a"));
        db.Time.Advance(TimeSpan.FromSeconds(20));
        var renewed = await store.AcquireBucketLeasesAsync(Request("relay-a"));

        await Assert.That(renewed).IsEquivalentTo(AllBuckets);

        // 25 more seconds: past the original expiry but inside the renewed lease, so a
        // second relay can register but must not steal anything yet.
        db.Time.Advance(TimeSpan.FromSeconds(25));
        var stolen = await store.AcquireBucketLeasesAsync(Request("relay-b"));
        await Assert.That(stolen).IsEmpty();
    }

    [Test]
    public async Task ExpiredLeases_AreClaimedByNewRelay()
    {
        using var db = new SqliteOutboxDatabase();
        var store = db.CreateStore();

        await store.AcquireBucketLeasesAsync(Request("relay-a"));
        // Past lease expiry AND past relay-a's heartbeat activity window: relay-a is
        // considered dead, so relay-b's fair share is the whole table.
        db.Time.Advance(TimeSpan.FromSeconds(61));

        var owned = await store.AcquireBucketLeasesAsync(Request("relay-b"));

        await Assert.That(owned).IsEquivalentTo(AllBuckets);
    }

    [Test]
    public async Task TwoActiveRelays_ConvergeToFairShare()
    {
        using var db = new SqliteOutboxDatabase();
        var store = db.CreateStore();

        var first = await store.AcquireBucketLeasesAsync(Request("relay-a"));
        await Assert.That(first.Count).IsEqualTo(4);

        // relay-b comes up: it registers a heartbeat but everything is validly leased.
        var second = await store.AcquireBucketLeasesAsync(Request("relay-b"));
        await Assert.That(second).IsEmpty();

        // relay-a's next renewal sees two active relays and releases down to its fair share.
        db.Time.Advance(TimeSpan.FromSeconds(10));
        var rebalanced = await store.AcquireBucketLeasesAsync(Request("relay-a"));
        await Assert.That(rebalanced.Count).IsEqualTo(2);

        // relay-b now claims the released buckets.
        var claimed = await store.AcquireBucketLeasesAsync(Request("relay-b"));
        await Assert.That(claimed.Count).IsEqualTo(2);

        var overlap = rebalanced.Intersect(claimed).ToArray();
        await Assert.That(overlap).IsEmpty();
    }

    [Test]
    public async Task GetNextBatch_ReturnsOldestRowsForBucketOnly()
    {
        using var db = new SqliteOutboxDatabase();
        var store = db.CreateStore();
        await db.InsertRowsAsync(
            NewRow(bucket: 0, value: "a"),
            NewRow(bucket: 1, value: "other-bucket"),
            NewRow(bucket: 0, value: "b"),
            NewRow(bucket: 0, value: "c"));

        var batch = await store.GetNextBatchAsync(bucket: 0, maxCount: 2);

        await Assert.That(batch.Count).IsEqualTo(2);
        await Assert.That(batch[0].Id).IsLessThan(batch[1].Id);
        await Assert.That(batch[0].Value).IsEquivalentTo("a"u8.ToArray());
        await Assert.That(batch[1].Value).IsEquivalentTo("b"u8.ToArray());
    }

    [Test]
    public async Task GetBucketsWithPending_ReturnsOnlyRequestedBucketsWithRows()
    {
        using var db = new SqliteOutboxDatabase();
        var store = db.CreateStore();
        await db.InsertRowsAsync(
            NewRow(bucket: 0, value: "a"),
            NewRow(bucket: 2, value: "b"),
            NewRow(bucket: 3, value: "not-requested"));

        var pending = await store.GetBucketsWithPendingAsync([0, 1, 2]);

        var sorted = pending.Order().ToArray();
        await Assert.That(sorted).IsEquivalentTo(BucketsZeroAndTwo);
        await Assert.That(await store.GetBucketsWithPendingAsync([])).IsEmpty();
    }

    [Test]
    public async Task AcquireBucketLeases_RowsBeyondBucketCount_Throws()
    {
        using var db = new SqliteOutboxDatabase();
        var store = db.CreateStore();
        // A writer configured with a larger bucket count than this relay: its rows would
        // silently never publish, so lease acquisition must fail loudly instead.
        await db.InsertRowsAsync(NewRow(bucket: 7, value: "orphan"));

        await Assert.That(async () => await store.AcquireBucketLeasesAsync(Request("relay-a", bucketCount: 4)))
            .Throws<OutboxMisconfigurationException>();
    }

    [Test]
    public async Task ThreeRelays_NewJoinerIsNotStarved()
    {
        using var db = new SqliteOutboxDatabase();
        var store = db.CreateStore();

        // a and b converge to 2 buckets each.
        await store.AcquireBucketLeasesAsync(Request("relay-a"));
        await store.AcquireBucketLeasesAsync(Request("relay-b"));
        db.Time.Advance(TimeSpan.FromSeconds(10));
        await store.AcquireBucketLeasesAsync(Request("relay-a"));
        await store.AcquireBucketLeasesAsync(Request("relay-b"));

        // c joins: with a uniform ceiling share (ceil(4/3) = 2) neither incumbent would ever
        // release and c would starve forever. Rank-based shares sum to exactly 4 (2/1/1),
        // so one incumbent releases and c claims.
        var firstTry = await store.AcquireBucketLeasesAsync(Request("relay-c"));
        await Assert.That(firstTry).IsEmpty();

        db.Time.Advance(TimeSpan.FromSeconds(10));
        var a = await store.AcquireBucketLeasesAsync(Request("relay-a"));
        var b = await store.AcquireBucketLeasesAsync(Request("relay-b"));
        var c = await store.AcquireBucketLeasesAsync(Request("relay-c"));

        await Assert.That(a.Count).IsEqualTo(2);
        await Assert.That(b.Count).IsEqualTo(1);
        await Assert.That(c.Count).IsEqualTo(1);
        var union = a.Concat(b).Concat(c).Order().ToArray();
        await Assert.That(union).IsEquivalentTo(AllBuckets);
    }

    [Test]
    public async Task TransientHeartbeatFailure_IsNotSwallowed_RetrySucceeds()
    {
        // A DbUpdateException that created nothing must not be misread as a benign
        // concurrent-insert race: the store verifies and rethrows, and the next attempt
        // (relay backoff) proceeds normally. Save call 1 = the heartbeat insert.
        using var db = new SqliteOutboxDatabase(new FailingSaveInterceptor(failOnCallNumber: 1));
        var store = db.CreateStore();

        await Assert.That(async () => await store.AcquireBucketLeasesAsync(Request("relay-a")))
            .Throws<DbUpdateException>();

        var owned = await store.AcquireBucketLeasesAsync(Request("relay-a"));
        await Assert.That(owned).IsEquivalentTo(AllBuckets);
    }

    [Test]
    public async Task TransientSeedingFailure_DoesNotLatchSeededFlag_RetrySucceeds()
    {
        // Save call 2 = the lease seeding on a fresh store (call 1 is the heartbeat insert).
        // A seeding failure must rethrow rather than latch _leasesSeeded, or buckets would
        // stay unclaimable forever.
        using var db = new SqliteOutboxDatabase(new FailingSaveInterceptor(failOnCallNumber: 2));
        var store = db.CreateStore();

        await Assert.That(async () => await store.AcquireBucketLeasesAsync(Request("relay-a")))
            .Throws<DbUpdateException>();

        var owned = await store.AcquireBucketLeasesAsync(Request("relay-a"));
        await Assert.That(owned).IsEquivalentTo(AllBuckets);
    }

    [Test]
    public async Task MarkPublished_DeletesOnlyGivenIdsInBucket()
    {
        using var db = new SqliteOutboxDatabase();
        var store = db.CreateStore();
        await db.InsertRowsAsync(
            NewRow(bucket: 0, value: "a"),
            NewRow(bucket: 0, value: "b"),
            NewRow(bucket: 1, value: "keep"));

        var batch = await store.GetNextBatchAsync(bucket: 0, maxCount: 10);
        await store.MarkPublishedAsync(0, [batch[0]]);

        var remaining0 = await store.GetNextBatchAsync(bucket: 0, maxCount: 10);
        var remaining1 = await store.GetNextBatchAsync(bucket: 1, maxCount: 10);
        await Assert.That(remaining0.Count).IsEqualTo(1);
        await Assert.That(remaining0[0].Value).IsEquivalentTo("b"u8.ToArray());
        await Assert.That(remaining1.Count).IsEqualTo(1);
    }

    [Test]
    public async Task AddOutboxMessage_EnqueueHelper_PersistsSerializedRow()
    {
        using var db = new SqliteOutboxDatabase();

        await using (var context = db.CreateContext())
        {
            context.AddOutboxMessage(
                "orders", "order-1", "payload",
                Serializers.String, Serializers.String,
                headers: new Headers().Add("trace", "abc"),
                bucketCount: 4);
            await context.SaveChangesAsync();
        }

        var store = db.CreateStore();
        var expectedBucket = OutboxBucket.Compute("order-1"u8.ToArray(), Guid.Empty, 4);
        var batch = await store.GetNextBatchAsync(expectedBucket, maxCount: 10);

        await Assert.That(batch.Count).IsEqualTo(1);
        await Assert.That(batch[0].Topic).IsEqualTo("orders");
        await Assert.That(batch[0].Key).IsEquivalentTo("order-1"u8.ToArray());
        await Assert.That(batch[0].Value).IsEquivalentTo("payload"u8.ToArray());
        var headers = OutboxHeaderCodec.Decode(batch[0].Headers)!;
        await Assert.That(headers[0].Key).IsEqualTo("trace");
    }

    [Test]
    public async Task CustomTableNamesAndSchema_TablesCreatedAndStoreRoundTrips()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var contextOptions = new DbContextOptionsBuilder<CustomNamesContext>()
            .UseSqlite(connection)
            .Options;
        await using (var context = new CustomNamesContext(contextOptions))
        {
            await context.Database.EnsureCreatedAsync();
            context.AddOutboxMessage(NewRow(bucket: 0, value: "custom"));
            await context.SaveChangesAsync();
        }

        var tables = new List<string>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                tables.Add(reader.GetString(0));
        }

        await Assert.That(tables).Contains("custom_outbox");
        await Assert.That(tables).Contains("custom_outbox_leases");
        await Assert.That(tables).Contains("custom_outbox_relays");
        await Assert.That(tables).DoesNotContain(OutboxModelOptions.DefaultMessagesTableName);

        var store = new EfCoreOutboxStore<CustomNamesContext>(new CustomNamesFactory(contextOptions));
        var owned = await store.AcquireBucketLeasesAsync(Request("relay-a"));
        await Assert.That(owned).IsEquivalentTo(AllBuckets);

        var batch = await store.GetNextBatchAsync(bucket: 0, maxCount: 10);
        await Assert.That(batch.Count).IsEqualTo(1);
        await store.MarkPublishedAsync(0, [batch[0]]);
        await Assert.That(await store.GetBucketsWithPendingAsync([0])).IsEmpty();
    }

    public sealed class CustomNamesContext(DbContextOptions<CustomNamesContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.UseDekafOutbox(new OutboxModelOptions
            {
                MessagesTableName = "custom_outbox",
                LeasesTableName = "custom_outbox_leases",
                RelaysTableName = "custom_outbox_relays"
            });
    }

    private sealed class CustomNamesFactory(DbContextOptions<CustomNamesContext> options)
        : IDbContextFactory<CustomNamesContext>
    {
        public CustomNamesContext CreateDbContext() => new(options);
    }

    private static OutboxMessage NewRow(int bucket, string value) => new()
    {
        MessageId = Guid.NewGuid(),
        Bucket = bucket,
        Topic = "topic",
        Value = System.Text.Encoding.UTF8.GetBytes(value),
        CreatedAtUtc = new DateTimeOffset(2026, 7, 22, 0, 0, 0, TimeSpan.Zero)
    };

    public sealed class OutboxTestContext(DbContextOptions<OutboxTestContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.UseDekafOutbox();
    }

    /// <summary>
    /// Fails exactly the Nth SaveChanges call. On a fresh store, call 1 is the heartbeat
    /// insert and call 2 is the lease seeding, so tests can target either path.
    /// </summary>
    private sealed class FailingSaveInterceptor(int failOnCallNumber) : SaveChangesInterceptor
    {
        private int _calls;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (++_calls == failOnCallNumber)
                throw new DbUpdateException("Simulated transient save failure.");

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class SqliteOutboxDatabase : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<OutboxTestContext> _options;

        public SqliteOutboxDatabase(SaveChangesInterceptor? interceptor = null)
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            var builder = new DbContextOptionsBuilder<OutboxTestContext>()
                .UseSqlite(_connection);
            if (interceptor is not null)
                builder.AddInterceptors(interceptor);
            _options = builder.Options;
            // A SaveChanges interceptor never fires during EnsureCreated (DDL only), so the
            // shared options are safe here.
            using var context = CreateContext();
            context.Database.EnsureCreated();
        }

        public FakeOutboxTimeProvider Time { get; } = new();

        public OutboxTestContext CreateContext() => new(_options);

        public EfCoreOutboxStore<OutboxTestContext> CreateStore()
            => new(new ContextFactory(_options), Time);

        public async Task InsertRowsAsync(params OutboxMessage[] rows)
        {
            await using var context = CreateContext();
            foreach (var row in rows)
                context.AddOutboxMessage(row);
            await context.SaveChangesAsync();
        }

        public void Dispose() => _connection.Dispose();

        private sealed class ContextFactory(DbContextOptions<OutboxTestContext> options)
            : IDbContextFactory<OutboxTestContext>
        {
            public OutboxTestContext CreateDbContext() => new(options);
        }
    }
}
