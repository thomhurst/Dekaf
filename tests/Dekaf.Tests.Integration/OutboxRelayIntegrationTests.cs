using Dekaf.Consumer;
using Dekaf.Outbox;
using Dekaf.Outbox.EntityFrameworkCore;
using Dekaf.Producer;
using Dekaf.Serialization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dekaf.Tests.Integration;

/// <summary>
/// End-to-end outbox flow: rows enqueued in a relational database (SQLite) are published to
/// a real broker by the relay and removed once acknowledged.
/// </summary>
[Category("MessagingPatterns")]
public class OutboxRelayIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    private const int BucketCount = 4;

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Relay_PublishesEnqueuedRowsInOrder_AndEmptiesTable(bool holdForRenewal)
    {
        var topic = $"outbox-relay-{Guid.NewGuid():N}";
        await KafkaContainer.CreateTopicAsync(topic, partitions: 2);

        // A file database, not one shared in-memory SqliteConnection. The relay's background
        // cycle and this test's polling both open contexts through the factory, and a
        // SqliteConnection is not thread-safe: sharing a single instance corrupted its
        // internal command list, surfacing as NullReferenceException from
        // SqliteCommand.Dispose (relay cycle) and SqliteConnection.Close (test teardown).
        // One connection per context leaves the concurrency to SQLite's own file locking,
        // and DefaultTimeout makes a contended lock wait rather than fail.
        var databasePath = Path.Combine(Path.GetTempPath(), $"dekaf-outbox-{Guid.NewGuid():N}.db");
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            DefaultTimeout = 30,
            // Pooled connections outlive the contexts that opened them and keep the file
            // handle open, which blocks the cleanup delete on Windows.
            Pooling = false
        }.ToString();
        var contextOptions = new DbContextOptionsBuilder<OutboxContext>()
            .UseSqlite(connectionString)
            .Options;
        try
        {
            await RunRelayScenarioAsync(topic, contextOptions, holdForRenewal);
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    private async Task RunRelayScenarioAsync(
        string topic,
        DbContextOptions<OutboxContext> contextOptions,
        bool holdForRenewal)
    {
        await using (var context = new OutboxContext(contextOptions))
        {
            await context.Database.EnsureCreatedAsync();

            // Same key for all rows: they land in one bucket and must arrive in enqueue order.
            for (var i = 0; i < 5; i++)
            {
                context.AddOutboxMessage(
                    topic, "order-1", $"payload-{i}",
                    Serializers.String, Serializers.String,
                    headers: new Headers().Add("origin", "integration-test"),
                    bucketCount: BucketCount);
            }

            await context.SaveChangesAsync();
        }

        var store = new ObservedRenewalStore(
            new EfCoreOutboxStore<OutboxContext>(new ContextFactory(contextOptions)));
        var producer = Kafka.CreateProducer<byte[]?, byte[]?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("outbox-relay-integration")
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .Build();
        await using var producerPublisher = new DekafOutboxPublisher(producer);
        IOutboxPublisher publisher = holdForRenewal
            ? new HoldingPublisher(producerPublisher, store.RenewalObserved.Task)
            : producerPublisher;

        var relayOptions = new OutboxRelayOptions
        {
            BucketCount = BucketCount,
            PollInterval = TimeSpan.FromMilliseconds(50),
            LeaseRenewInterval = holdForRenewal ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(10),
            RelayId = "integration-relay"
        };

        using var relay = new OutboxRelayService(
            store, publisher, relayOptions,
            GlobalTestSetup.GetLoggerFactory().CreateLogger<OutboxRelayService>());
        await relay.StartAsync(CancellationToken.None);
        try
        {
            await using var consumer = await Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithClientId("outbox-relay-consumer")
                .WithGroupId($"outbox-relay-group-{Guid.NewGuid():N}")
                .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
                .BuildAsync();
            consumer.Subscribe(topic);

            // Headers reference pooled fetch buffers, so copy them out during enumeration.
            var messages = new List<(string? Key, string? Value, string? Origin, string? MessageId)>();
            using var consumeCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await foreach (var msg in consumer.ConsumeAsync(consumeCts.Token))
            {
                messages.Add((
                    msg.Key,
                    msg.Value,
                    ReadHeader(msg.Headers, "origin"),
                    ReadHeader(msg.Headers, OutboxRelayOptions.DefaultMessageIdHeaderName)));
                if (messages.Count >= 5)
                    break;
            }

            await Assert.That(messages.Count).IsEqualTo(5);
            if (holdForRenewal)
            {
                await store.RenewalObserved.Task.WaitAsync(TimeSpan.FromSeconds(30));
                await Assert.That(store.PeerBuckets).IsEmpty();
            }
            for (var i = 0; i < 5; i++)
            {
                await Assert.That(messages[i].Key).IsEqualTo("order-1");
                await Assert.That(messages[i].Value).IsEqualTo($"payload-{i}");
                await Assert.That(messages[i].Origin).IsEqualTo("integration-test");
                await Assert.That(Guid.TryParse(messages[i].MessageId, out _)).IsTrue();
            }

            // Acknowledged rows must be gone from every bucket.
            await WaitForConditionAsync(
                () =>
                {
                    using var context = new OutboxContext(contextOptions);
                    return !context.Set<OutboxMessage>().Any();
                },
                TimeSpan.FromSeconds(30));
        }
        finally
        {
            await relay.StopAsync(CancellationToken.None);
        }
    }

    private sealed class HoldingPublisher(IOutboxPublisher inner, Task release) : IOutboxPublisher
    {
        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => inner.InitializeAsync(cancellationToken);
        public async ValueTask<OutboxPublishResult> PublishAsync(IReadOnlyList<OutboxMessage> messages,
            string messageIdHeaderName, CancellationToken cancellationToken = default)
        {
            var result = await inner.PublishAsync(messages, messageIdHeaderName, cancellationToken);
            // Hold the complete publish call open until a real database renewal and peer probe finish.
            await release.WaitAsync(cancellationToken);
            return result;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ObservedRenewalStore(EfCoreOutboxStore<OutboxContext> inner)
        : IOutboxStore, IOutboxLeaseRenewalStore
    {
        internal TaskCompletionSource RenewalObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal IReadOnlyList<int> PeerBuckets { get; private set; } = [];
        public ValueTask<IReadOnlyList<int>> AcquireBucketLeasesAsync(OutboxLeaseRequest request,
            CancellationToken cancellationToken = default) => inner.AcquireBucketLeasesAsync(request, cancellationToken);
        public ValueTask<IReadOnlyList<int>> GetBucketsWithPendingAsync(IReadOnlyList<int> buckets,
            CancellationToken cancellationToken = default) => inner.GetBucketsWithPendingAsync(buckets, cancellationToken);
        public ValueTask<IReadOnlyList<OutboxMessage>> GetNextBatchAsync(int bucket, int maxCount,
            CancellationToken cancellationToken = default) => inner.GetNextBatchAsync(bucket, maxCount, cancellationToken);
        public ValueTask MarkPublishedAsync(int bucket, IReadOnlyList<OutboxMessage> publishedMessages,
            CancellationToken cancellationToken = default) => inner.MarkPublishedAsync(bucket, publishedMessages, cancellationToken);

        public async ValueTask<bool> RenewBucketLeasesAsync(OutboxLeaseRequest request, IReadOnlyList<int> buckets,
            CancellationToken cancellationToken = default)
        {
            var renewed = await inner.RenewBucketLeasesAsync(request, buckets, cancellationToken);
            if (renewed && !RenewalObserved.Task.IsCompleted)
            {
                PeerBuckets = await inner.AcquireBucketLeasesAsync(new OutboxLeaseRequest
                {
                    RelayId = "peer-relay", BucketCount = request.BucketCount, LeaseDuration = request.LeaseDuration
                }, cancellationToken);
                RenewalObserved.SetResult();
            }
            return renewed;
        }
    }

    /// <summary>
    /// Removes the scenario's database and any journal SQLite left beside it. Deletion is
    /// best-effort cleanup of a temp file: a failure here must not mask the test result, and
    /// the file is uniquely named so a leftover cannot affect another run.
    /// </summary>
    private static void DeleteDatabaseFiles(string databasePath)
    {
        foreach (var suffix in new[] { "", "-journal", "-wal", "-shm" })
        {
            try
            {
                File.Delete(databasePath + suffix);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static string? ReadHeader(IReadOnlyList<Header> headers, string key)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            if (headers[i].Key == key)
                return headers[i].GetValueAsString();
        }

        return null;
    }

    public sealed class OutboxContext(DbContextOptions<OutboxContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.UseDekafOutbox();
    }

    private sealed class ContextFactory(DbContextOptions<OutboxContext> options) : IDbContextFactory<OutboxContext>
    {
        public OutboxContext CreateDbContext() => new(options);
    }
}
