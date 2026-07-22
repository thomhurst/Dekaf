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
    public async Task Relay_PublishesEnqueuedRowsInOrder_AndEmptiesTable()
    {
        var topic = $"outbox-relay-{Guid.NewGuid():N}";
        await KafkaContainer.CreateTopicAsync(topic, partitions: 2);

        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var contextOptions = new DbContextOptionsBuilder<OutboxContext>()
            .UseSqlite(connection)
            .Options;
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

        var store = new EfCoreOutboxStore<OutboxContext>(new ContextFactory(contextOptions));
        var producer = Kafka.CreateProducer<byte[]?, byte[]?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("outbox-relay-integration")
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .Build();
        await using var publisher = new DekafOutboxPublisher(producer);

        var relayOptions = new OutboxRelayOptions
        {
            BucketCount = BucketCount,
            PollInterval = TimeSpan.FromMilliseconds(50),
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
