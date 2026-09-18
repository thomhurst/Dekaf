using Amazon.DynamoDBv2.Model;
using Dekaf.Outbox;
using Dekaf.Outbox.DynamoDB;

namespace Dekaf.Tests.Unit.Outbox;

public sealed class DynamoDbOutboxSchemaTests
{
    private static readonly DynamoDbOutboxOptions Options = new() { TableName = "outbox" };

    [Test]
    public async Task SortKeys_OrderAsTheirSequenceNumbers()
    {
        long[] sequences = [1, 9, 10, 99, 100, 1_000_000, long.MaxValue];

        var sortKeys = sequences.Select(DynamoDbOutboxSchema.MessageSortKey).ToArray();

        // DynamoDB compares sort keys as strings; without padding "10" sorts before "9".
        await Assert.That(sortKeys.SequenceEqual(sortKeys.Order(StringComparer.Ordinal))).IsTrue();
        await Assert.That(sortKeys.All(sortKey => sortKey.Length == 19)).IsTrue();
    }

    [Test]
    public async Task KeyPrefix_SeparatesEveryPartitionOfTwoOutboxes()
    {
        var orders = new DynamoDbOutboxSchema(new DynamoDbOutboxOptions { TableName = "shared", KeyPrefix = "ORDERS" });
        var billing = new DynamoDbOutboxSchema(new DynamoDbOutboxOptions { TableName = "shared", KeyPrefix = "BILLING" });

        await Assert.That(orders.MessagePartition(3)).IsEqualTo("ORDERS#MESSAGES#3");
        await Assert.That(orders.CoordinationPartition).IsEqualTo("ORDERS#COORDINATION");
        await Assert.That(orders.SequenceKey(3)["PK"].S).IsEqualTo("ORDERS#SEQUENCE#3");
        await Assert.That(billing.MessagePartition(3)).IsEqualTo("BILLING#MESSAGES#3");
    }

    [Test]
    public async Task Message_RoundTripsThroughItsItem()
    {
        var schema = new DynamoDbOutboxSchema(Options);
        var message = new OutboxMessage
        {
            MessageId = Guid.NewGuid(),
            Bucket = 5,
            Topic = "orders",
            Key = [],
            Value = [1, 2, 3],
            Headers = [1, 0],
            Partition = 0,
            CreatedAtUtc = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.FromHours(2))
        };

        var item = schema.ToItem(message, sequence: 42, bucketCount: 8);
        var read = schema.FromItem(5, item);

        await Assert.That(item["PK"].S).IsEqualTo("OUTBOX#MESSAGES#5");
        await Assert.That(item["SK"].S).IsEqualTo("0000000000000000042");
        await Assert.That(item["BucketCount"].N).IsEqualTo("8");
        await Assert.That(read.Id).IsEqualTo(42);
        await Assert.That(read.MessageId).IsEqualTo(message.MessageId);
        await Assert.That(read.Key).IsNotNull();
        await Assert.That(read.Key!.Length).IsEqualTo(0);
        await Assert.That(read.Value!.SequenceEqual(message.Value)).IsTrue();
        await Assert.That(read.Partition).IsEqualTo(0);
        // Stored as UTC ticks: the instant survives, the offset does not need to.
        await Assert.That(read.CreatedAtUtc).IsEqualTo(message.CreatedAtUtc);
    }

    [Test]
    public async Task NullPayloads_AreAbsentAttributes()
    {
        var schema = new DynamoDbOutboxSchema(Options);
        var message = new OutboxMessage
        {
            MessageId = Guid.NewGuid(),
            Bucket = 0,
            Topic = "orders",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var item = schema.ToItem(message, 1, 8);
        var read = schema.FromItem(0, item);

        await Assert.That(item.ContainsKey("Key") || item.ContainsKey("Value") || item.ContainsKey("Headers")
            || item.ContainsKey("Partition")).IsFalse();
        await Assert.That(read.Key).IsNull();
        await Assert.That(read.Value).IsNull();
        await Assert.That(read.Headers).IsNull();
        await Assert.That(read.Partition).IsNull();
    }

    [Test]
    public async Task ForeignItemInAMessagePartition_IsReportedWithItsLocation()
    {
        var schema = new DynamoDbOutboxSchema(Options);
        var notASequence = schema.ItemKey("OUTBOX#MESSAGES#2", "not-a-number");
        var noTopic = schema.MessageKey(2, 7);
        noTopic["MessageId"] = new AttributeValue { S = Guid.NewGuid().ToString() };
        noTopic["CreatedAtUtc"] = DynamoDbOutboxSchema.Number(1);

        await Assert.That(() => schema.FromItem(2, notASequence)).Throws<InvalidOperationException>()
            .WithMessageContaining("not-a-number");
        await Assert.That(() => schema.FromItem(2, noTopic)).Throws<InvalidOperationException>()
            .WithMessageContaining("bucket 2");
    }

    [Test]
    [Arguments("", "PK", "SK", "OUTBOX", 8, 8, 10)]
    [Arguments("outbox", " ", "SK", "OUTBOX", 8, 8, 10)]
    [Arguments("outbox", "PK", "", "OUTBOX", 8, 8, 10)]
    [Arguments("outbox", "PK", "PK", "OUTBOX", 8, 8, 10)]
    [Arguments("outbox", "PK", "SK", "", 8, 8, 10)]
    [Arguments("outbox", "PK", "SK", "OUTBOX", 0, 8, 10)]
    [Arguments("outbox", "PK", "SK", "OUTBOX", 8, 0, 10)]
    [Arguments("outbox", "PK", "SK", "OUTBOX", 8, 8, 0)]
    public async Task InvalidOptions_AreRejected(
        string tableName, string partitionKey, string sortKey, string prefix, int bucketCount, int maxConcurrency,
        int pendingCountLimit)
    {
        var options = new DynamoDbOutboxOptions
        {
            TableName = tableName,
            PartitionKeyAttributeName = partitionKey,
            SortKeyAttributeName = sortKey,
            KeyPrefix = prefix,
            BucketCount = bucketCount,
            MaxConcurrency = maxConcurrency,
            PendingCountLimit = pendingCountLimit
        };

        await Assert.That(options.Validate).Throws<ArgumentException>();
    }

    [Test]
    public async Task KeyAttributeNamedLikeAnItemAttribute_IsRejected()
    {
        var options = new DynamoDbOutboxOptions { TableName = "outbox", SortKeyAttributeName = "Topic" };

        await Assert.That(() => new DynamoDbOutboxSchema(options)).Throws<ArgumentException>();
    }

    [Test]
    public async Task TableDefinition_IsTwoStringKeysOnDemand()
    {
        var request = DynamoDbOutboxTable.CreateTableRequest(
            new DynamoDbOutboxOptions { TableName = "outbox", PartitionKeyAttributeName = "pk", SortKeyAttributeName = "sk" });

        await Assert.That(request.TableName).IsEqualTo("outbox");
        await Assert.That(request.BillingMode).IsEqualTo(Amazon.DynamoDBv2.BillingMode.PAY_PER_REQUEST);
        await Assert.That(string.Join(',', request.KeySchema.Select(key => $"{key.AttributeName}:{key.KeyType}")))
            .IsEqualTo("pk:HASH,sk:RANGE");
        await Assert.That(request.AttributeDefinitions.All(attribute => attribute.AttributeType == "S")).IsTrue();
    }
}
