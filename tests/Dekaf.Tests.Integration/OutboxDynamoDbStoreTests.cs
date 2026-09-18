using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Dekaf.Outbox;
using Dekaf.Outbox.DynamoDB;

namespace Dekaf.Tests.Integration;

/// <summary>
/// The message side of the DynamoDB outbox against DynamoDB Local: enqueue order, the
/// transactional enqueue, fetch paging, deletion of the published prefix, and the fail-fast
/// checks. Bucket ownership is covered by <see cref="OutboxDynamoDbLeaseTests"/>.
/// </summary>
[Category("MessagingPatterns")]
[ClassDataSource<DynamoDbLocalContainer>(Shared = SharedType.PerTestSession)]
public sealed class OutboxDynamoDbStoreTests(DynamoDbLocalContainer dynamoDb)
{
    [Test]
    public async Task EnqueuedMessage_RoundTripsEveryField()
    {
        using var client = dynamoDb.CreateClient();
        var options = await DynamoDbLocalContainer.CreateTableAsync(client);
        var writer = new DynamoDbOutboxWriter(client, options);
        var store = new DynamoDbOutboxStore(client, options);
        var message = new OutboxMessage
        {
            MessageId = Guid.NewGuid(),
            Bucket = 3,
            Topic = "orders",
            Key = [1, 2, 3],
            Value = [4, 5],
            Headers = [1, 0],
            Partition = 7,
            CreatedAtUtc = new DateTimeOffset(2026, 9, 1, 8, 30, 0, TimeSpan.Zero)
        };

        await writer.EnqueueAsync(message);
        var read = (await store.GetNextBatchAsync(3, 10)).Single();

        await Assert.That(read.MessageId).IsEqualTo(message.MessageId);
        await Assert.That(read.Bucket).IsEqualTo(3);
        await Assert.That(read.Topic).IsEqualTo("orders");
        await Assert.That(read.Key!.SequenceEqual(message.Key)).IsTrue();
        await Assert.That(read.Value!.SequenceEqual(message.Value)).IsTrue();
        await Assert.That(read.Headers!.SequenceEqual(message.Headers)).IsTrue();
        await Assert.That(read.Partition).IsEqualTo(7);
        await Assert.That(read.CreatedAtUtc).IsEqualTo(message.CreatedAtUtc);
        await Assert.That(read.Id).IsEqualTo(1);
    }

    [Test]
    public async Task NullAndEmptyPayloads_StayDistinct()
    {
        using var client = dynamoDb.CreateClient();
        var options = await DynamoDbLocalContainer.CreateTableAsync(client);
        var writer = new DynamoDbOutboxWriter(client, options);
        var store = new DynamoDbOutboxStore(client, options);

        // The outbox orders an empty key as a real key; only a null key is keyless. A
        // tombstone is a null value, which an empty value must not turn into.
        await writer.EnqueueAsync(Message(0, key: [], value: []));
        await writer.EnqueueAsync(Message(0, key: null, value: null));
        var read = await store.GetNextBatchAsync(0, 10);

        await Assert.That(read[0].Key).IsNotNull();
        await Assert.That(read[0].Key!.Length).IsEqualTo(0);
        await Assert.That(read[0].Value).IsNotNull();
        await Assert.That(read[0].Value!.Length).IsEqualTo(0);
        await Assert.That(read[1].Key).IsNull();
        await Assert.That(read[1].Value).IsNull();
        await Assert.That(read[1].Headers).IsNull();
        await Assert.That(read[1].Partition).IsNull();
    }

    [Test]
    public async Task Bucket_ComesBackInEnqueueOrder_AcrossSeparateAndBatchedWrites()
    {
        using var client = dynamoDb.CreateClient();
        var options = await DynamoDbLocalContainer.CreateTableAsync(client);
        var writer = new DynamoDbOutboxWriter(client, options);
        var store = new DynamoDbOutboxStore(client, options);
        var expected = new List<Guid>();

        for (var index = 0; index < 3; index++)
            expected.Add(await EnqueueOneAsync(writer, bucket: 5));
        // One transaction spanning two buckets: bucket 5 keeps its list order.
        OutboxMessage[] batch = [Message(5), Message(2), Message(5), Message(2), Message(5)];
        await writer.EnqueueAsync(batch);
        expected.AddRange(batch.Where(message => message.Bucket == 5).Select(message => message.MessageId));
        expected.Add(await EnqueueOneAsync(writer, bucket: 5));

        var read = await store.GetNextBatchAsync(5, 100);

        await Assert.That(read.Select(message => message.MessageId).SequenceEqual(expected)).IsTrue();
        await Assert.That(read.Select(message => message.Id).SequenceEqual(read.Select(message => message.Id).Order())).IsTrue();
        await Assert.That((await store.GetNextBatchAsync(2, 100)).Count).IsEqualTo(2);
    }

    [Test]
    public async Task ConcurrentWriters_NeverOverwriteEachOther()
    {
        using var client = dynamoDb.CreateClient();
        var options = await DynamoDbLocalContainer.CreateTableAsync(client);
        var store = new DynamoDbOutboxStore(client, options);
        // Separate writers, as separate pods: the counter is the only thing they share.
        var writers = Enumerable.Range(0, 4).Select(_ => new DynamoDbOutboxWriter(client, options)).ToArray();

        var ids = await Task.WhenAll(Enumerable.Range(0, 80).Select(index =>
            Task.Run(() => EnqueueOneAsync(writers[index % writers.Length], bucket: 1))));

        var read = await store.GetNextBatchAsync(1, 200);
        await Assert.That(read.Count).IsEqualTo(80);
        await Assert.That(read.Select(message => message.MessageId).Order().SequenceEqual(ids.Order())).IsTrue();
        await Assert.That(read.Select(message => message.Id).Distinct().Count()).IsEqualTo(80);
    }

    [Test]
    public async Task OutboxItem_CommitsOrRollsBack_WithTheBusinessWrite()
    {
        using var client = dynamoDb.CreateClient();
        var options = await DynamoDbLocalContainer.CreateTableAsync(client);
        var writer = new DynamoDbOutboxWriter(client, options);
        var store = new DynamoDbOutboxStore(client, options);

        await client.TransactWriteItemsAsync(new TransactWriteItemsRequest
        {
            TransactItems = [BusinessPut(options, "order-1"), await writer.CreateTransactWriteItemAsync(Message(4))]
        });
        await Assert.That((await store.GetNextBatchAsync(4, 10)).Count).IsEqualTo(1);

        // The same order again: the business condition fails, so the message must not exist.
        await Assert.That(async () => await client.TransactWriteItemsAsync(new TransactWriteItemsRequest
        {
            TransactItems = [BusinessPut(options, "order-1"), await writer.CreateTransactWriteItemAsync(Message(4))]
        })).Throws<TransactionCanceledException>();

        await Assert.That((await store.GetNextBatchAsync(4, 10)).Count).IsEqualTo(1);
    }

    [Test]
    public async Task RefusedPut_ReportsTheMessageThatHoldsTheNumber_InsideATransactionToo()
    {
        using var client = dynamoDb.CreateClient();
        var options = await DynamoDbLocalContainer.CreateTableAsync(client);
        var writer = new DynamoDbOutboxWriter(client, options);
        OutboxMessage[] messages = [Message(2), Message(5)];
        var items = await writer.CreateTransactWriteItemsAsync(messages);
        await client.TransactWriteItemsAsync(new TransactWriteItemsRequest { TransactItems = [.. items] });

        // The same puts again under a new token: what a retry looks like once DynamoDB no
        // longer recognises it. The writer tells its own retry from an overwrite by the
        // stored item that each refusal carries, so DynamoDB must really return it.
        TransactionCanceledException? canceled = null;
        try
        {
            await client.TransactWriteItemsAsync(new TransactWriteItemsRequest { TransactItems = [.. items] });
        }
        catch (TransactionCanceledException exception)
        {
            canceled = exception;
        }

        await Assert.That(canceled).IsNotNull();
        await Assert.That(canceled!.CancellationReasons.Count).IsEqualTo(2);
        for (var index = 0; index < messages.Length; index++)
        {
            await Assert.That(canceled.CancellationReasons[index].Code).IsEqualTo("ConditionalCheckFailed");
            await Assert.That(Guid.Parse(canceled.CancellationReasons[index].Item["MessageId"].S))
                .IsEqualTo(messages[index].MessageId);
        }
    }

    [Test]
    public async Task ResetSequenceCounter_FailsTheWrite_InsteadOfOverwritingAPendingMessage()
    {
        using var client = dynamoDb.CreateClient();
        var options = await DynamoDbLocalContainer.CreateTableAsync(client);
        var writer = new DynamoDbOutboxWriter(client, options);
        var store = new DynamoDbOutboxStore(client, options);
        var pending = await EnqueueOneAsync(writer, bucket: 0);

        await client.DeleteItemAsync(options.TableName, new Dictionary<string, AttributeValue>
        {
            ["PK"] = new() { S = "OUTBOX#SEQUENCE#0" },
            ["SK"] = new() { S = "SEQUENCE" }
        });

        await Assert.That(async () => await writer.EnqueueAsync(Message(0))).Throws<ConditionalCheckFailedException>();
        await Assert.That((await store.GetNextBatchAsync(0, 10)).Single().MessageId).IsEqualTo(pending);
    }

    [Test]
    public async Task AbandonedReservations_LeaveGaps_ThatHoldNothingBack()
    {
        using var client = dynamoDb.CreateClient();
        var options = await DynamoDbLocalContainer.CreateTableAsync(client);
        var writer = new DynamoDbOutboxWriter(client, options);
        var store = new DynamoDbOutboxStore(client, options);

        var first = await EnqueueOneAsync(writer, bucket: 3);
        // Reserved for transactions that failed: sequence numbers 2 and 4 never commit.
        await writer.CreateTransactWriteItemAsync(Message(3));
        var third = await EnqueueOneAsync(writer, bucket: 3);
        await writer.CreateTransactWriteItemAsync(Message(3));
        var fifth = await EnqueueOneAsync(writer, bucket: 3);

        // The store probes each gap for a message that committed behind its query. These
        // gaps stay empty, so the batch comes back whole, and a gap at the head of the next
        // fetch is no obstacle either.
        var batch = await store.GetNextBatchAsync(3, 2);
        await Assert.That(batch.Select(message => message.MessageId).SequenceEqual([first, third])).IsTrue();
        await store.MarkPublishedAsync(3, batch);

        var rest = await store.GetNextBatchAsync(3, 10);
        await Assert.That(rest.Single().MessageId).IsEqualTo(fifth);
        await Assert.That(rest.Single().Id).IsEqualTo(5);
    }

    [Test]
    public async Task MarkPublished_DeletesOnlyThePublishedPrefix_AcrossBatchWriteChunks()
    {
        using var client = dynamoDb.CreateClient();
        var options = await DynamoDbLocalContainer.CreateTableAsync(client);
        var writer = new DynamoDbOutboxWriter(client, options);
        var store = new DynamoDbOutboxStore(client, options);
        await writer.EnqueueAsync(Enumerable.Range(0, 70).Select(_ => Message(6)).ToArray());
        var batch = await store.GetNextBatchAsync(6, 70);

        // 60 rows need three BatchWriteItem requests of at most 25.
        await store.MarkPublishedAsync(6, [.. batch.Take(60)]);

        var remaining = await store.GetNextBatchAsync(6, 70);
        await Assert.That(remaining.Select(message => message.MessageId)
            .SequenceEqual(batch.Skip(60).Select(message => message.MessageId))).IsTrue();
    }

    [Test]
    public async Task GetNextBatch_HonoursMaxCount_AndReadsPastTheOneMegabytePage()
    {
        using var client = dynamoDb.CreateClient();
        var options = await DynamoDbLocalContainer.CreateTableAsync(client);
        var writer = new DynamoDbOutboxWriter(client, options);
        var store = new DynamoDbOutboxStore(client, options);
        var value = new byte[300 * 1024];
        for (var index = 0; index < 6; index++)
            await writer.EnqueueAsync(Message(1, value: value));

        // Six 300 KB rows cannot fit one 1 MB query page.
        await Assert.That((await store.GetNextBatchAsync(1, 5)).Count).IsEqualTo(5);
        await Assert.That((await store.GetNextBatchAsync(1, 50)).Count).IsEqualTo(6);
    }

    [Test]
    public async Task PendingProbe_ReportsOnlyBucketsWithMessages_InAscendingOrder()
    {
        using var client = dynamoDb.CreateClient();
        var options = await DynamoDbLocalContainer.CreateTableAsync(client);
        var writer = new DynamoDbOutboxWriter(client, options);
        var store = new DynamoDbOutboxStore(client, options);
        await writer.EnqueueAsync([Message(6), Message(1), Message(6)]);

        await Assert.That(string.Join(',', await store.GetBucketsWithPendingAsync([7, 6, 3, 1, 0]))).IsEqualTo("1,6");
        await Assert.That(await store.GetBucketsWithPendingAsync([0, 2])).IsEmpty();
        await Assert.That(await store.GetBucketsWithPendingAsync([])).IsEmpty();
    }

    [Test]
    public async Task PendingMetrics_CountTheBacklog_AndReportTheOldestBucketHead()
    {
        using var client = dynamoDb.CreateClient();
        var options = await DynamoDbLocalContainer.CreateTableAsync(client);
        var writer = new DynamoDbOutboxWriter(client, options);
        var store = new DynamoDbOutboxStore(client, options);
        var empty = await store.GetPendingMetricsAsync();
        await Assert.That(empty!.PendingCount).IsEqualTo(0);
        await Assert.That(empty.OldestCreatedAtUtc).IsNull();

        var oldest = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
        await writer.EnqueueAsync(Message(2, createdAt: oldest.AddHours(1)));
        await writer.EnqueueAsync(Message(5, createdAt: oldest));
        await writer.EnqueueAsync(Message(5, createdAt: oldest.AddHours(2)));

        var sample = await store.GetPendingMetricsAsync();
        await Assert.That(sample!.PendingCount).IsEqualTo(3);
        await Assert.That(sample.OldestCreatedAtUtc).IsEqualTo(oldest);
    }

    [Test]
    public async Task PendingMetrics_StopCountingABucketAtTheLimit()
    {
        using var client = dynamoDb.CreateClient();
        var table = await DynamoDbLocalContainer.CreateTableAsync(client);
        var options = new DynamoDbOutboxOptions { TableName = table.TableName, PendingCountLimit = 10 };
        var writer = new DynamoDbOutboxWriter(client, options);
        var store = new DynamoDbOutboxStore(client, options);
        await writer.EnqueueAsync(Enumerable.Range(0, 25).Select(_ => Message(0)).ToArray());
        await writer.EnqueueAsync(Enumerable.Range(0, 4).Select(_ => Message(1)).ToArray());

        var sample = await store.GetPendingMetricsAsync();

        await Assert.That(sample!.PendingCount).IsEqualTo(14);
    }

    [Test]
    public async Task MessageFromAWriterWithAnotherBucketCount_FaultsTheRelay()
    {
        using var client = dynamoDb.CreateClient();
        var table = await DynamoDbLocalContainer.CreateTableAsync(client);
        var misconfigured = new DynamoDbOutboxOptions { TableName = table.TableName, BucketCount = 16 };
        await new DynamoDbOutboxWriter(client, misconfigured).EnqueueAsync(Message(3));
        var store = new DynamoDbOutboxStore(client, table);

        await Assert.That(async () => await store.GetNextBatchAsync(3, 10)).Throws<OutboxMisconfigurationException>();
    }

    [Test]
    public async Task Writer_RejectsAMessageOutsideItsBucketRange_BeforeAnyRequest()
    {
        using var client = dynamoDb.CreateClient();
        var options = await DynamoDbLocalContainer.CreateTableAsync(client);
        var writer = new DynamoDbOutboxWriter(client, options);

        await Assert.That(async () => await writer.EnqueueAsync(Message(8))).Throws<ArgumentException>();
        await Assert.That(async () => await writer.EnqueueAsync(
            Enumerable.Range(0, DynamoDbOutboxWriter.MaxTransactionItems + 1).Select(_ => Message(0)).ToArray()))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task TwoOutboxes_ShareATableWithCustomKeyNames_WithoutSeeingEachOther()
    {
        using var client = dynamoDb.CreateClient();
        var orders = new DynamoDbOutboxOptions
        {
            TableName = $"shared-{Guid.NewGuid():N}",
            PartitionKeyAttributeName = "pk",
            SortKeyAttributeName = "sk",
            KeyPrefix = "ORDERS"
        };
        var billing = new DynamoDbOutboxOptions
        {
            TableName = orders.TableName,
            PartitionKeyAttributeName = "pk",
            SortKeyAttributeName = "sk",
            KeyPrefix = "BILLING"
        };
        await DynamoDbOutboxTable.CreateIfNotExistsAsync(client, orders);
        await DynamoDbOutboxTable.CreateIfNotExistsAsync(client, billing);
        await new DynamoDbOutboxWriter(client, orders).EnqueueAsync(Message(0));
        var ordersStore = new DynamoDbOutboxStore(client, orders);
        var billingStore = new DynamoDbOutboxStore(client, billing);
        var request = new OutboxLeaseRequest { RelayId = "relay", BucketCount = 8, LeaseDuration = TimeSpan.FromSeconds(30) };

        // Same relay id, same table: each outbox still leases its own eight buckets.
        await Assert.That((await ordersStore.AcquireBucketLeasesAsync(request)).Count).IsEqualTo(8);
        await Assert.That((await billingStore.AcquireBucketLeasesAsync(request)).Count).IsEqualTo(8);
        await Assert.That((await ordersStore.GetNextBatchAsync(0, 10)).Count).IsEqualTo(1);
        await Assert.That(await billingStore.GetNextBatchAsync(0, 10)).IsEmpty();
    }

    private static OutboxMessage Message(
        int bucket, byte[]? key = null, byte[]? value = null, DateTimeOffset? createdAt = null) => new()
    {
        MessageId = Guid.NewGuid(),
        Bucket = bucket,
        Topic = "orders",
        Key = key,
        Value = value,
        CreatedAtUtc = createdAt ?? DateTimeOffset.UtcNow
    };

    private static async Task<Guid> EnqueueOneAsync(IDynamoDbOutboxWriter writer, int bucket)
    {
        var message = Message(bucket);
        await writer.EnqueueAsync(message);
        return message.MessageId;
    }

    private static TransactWriteItem BusinessPut(DynamoDbOutboxOptions options, string orderId) => new()
    {
        Put = new Put
        {
            TableName = options.TableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = "ORDER#" + orderId },
                ["SK"] = new() { S = "ORDER" }
            },
            ConditionExpression = "attribute_not_exists(PK)"
        }
    };
}
