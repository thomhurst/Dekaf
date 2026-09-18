using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Dekaf.Outbox;
using Dekaf.Outbox.DynamoDB;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Dekaf.Tests.Unit.Outbox;

public sealed class DynamoDbOutboxWriterTests
{
    private static readonly DynamoDbOutboxOptions Options = new() { TableName = "outbox" };

    [Test]
    public async Task Items_ReserveOncePerBucket_AndNumberEachBucketInListOrder()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var reservations = CountSequences(client, new Dictionary<string, long> { ["OUTBOX#SEQUENCE#5"] = 10 });
        var writer = new DynamoDbOutboxWriter(client, Options);
        OutboxMessage[] messages = [Message(5), Message(2), Message(5), Message(2), Message(5)];

        var items = await writer.CreateTransactWriteItemsAsync(messages);

        // One atomic counter update per bucket, however many messages it has.
        await Assert.That(string.Join(' ', reservations.Order(StringComparer.Ordinal)))
            .IsEqualTo("OUTBOX#SEQUENCE#2+2 OUTBOX#SEQUENCE#5+3");
        await Assert.That(string.Join(' ', items.Select(item => $"{item.Put.Item["PK"].S}/{item.Put.Item["SK"].S}")))
            .IsEqualTo(
                "OUTBOX#MESSAGES#5/0000000000000000011 OUTBOX#MESSAGES#2/0000000000000000001 " +
                "OUTBOX#MESSAGES#5/0000000000000000012 OUTBOX#MESSAGES#2/0000000000000000002 " +
                "OUTBOX#MESSAGES#5/0000000000000000013");
        await Assert.That(items.Select((item, index) => item.Put.Item["MessageId"].S == messages[index].MessageId.ToString())
            .All(same => same)).IsTrue();
    }

    [Test]
    public async Task Item_RefusesToOverwriteAPendingMessage_AndCarriesTheBucketCount()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        CountSequences(client);
        var writer = new DynamoDbOutboxWriter(client, new DynamoDbOutboxOptions
        {
            TableName = "outbox",
            PartitionKeyAttributeName = "pk",
            BucketCount = 16
        });

        var put = (await writer.CreateTransactWriteItemAsync(Message(12))).Put;

        await Assert.That(put.TableName).IsEqualTo("outbox");
        await Assert.That(put.ConditionExpression).IsEqualTo("attribute_not_exists(#pk)");
        await Assert.That(put.ExpressionAttributeNames["#pk"]).IsEqualTo("pk");
        await Assert.That(put.Item["BucketCount"].N).IsEqualTo("16");
    }

    [Test]
    public async Task EmptyList_MakesNoRequest()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var writer = new DynamoDbOutboxWriter(client, Options);

        await Assert.That(await writer.CreateTransactWriteItemsAsync([])).IsEmpty();
        await writer.EnqueueAsync([]);

        await Assert.That(client.ReceivedCalls()).IsEmpty();
    }

    [Test]
    public async Task MessageOutsideTheBucketRange_IsRejectedBeforeAnyRequest()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var writer = new DynamoDbOutboxWriter(client, Options);

        await Assert.That(async () => await writer.CreateTransactWriteItemsAsync([Message(0), Message(8)]))
            .Throws<ArgumentException>();
        await Assert.That(async () => await writer.CreateTransactWriteItemAsync(Message(-1))).Throws<ArgumentException>();
        await Assert.That(client.ReceivedCalls()).IsEmpty();
    }

    [Test]
    public async Task Enqueue_WritesOneMessageWithAPut_AndSeveralWithOneTransaction()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        CountSequences(client);
        var writer = new DynamoDbOutboxWriter(client, Options);

        await writer.EnqueueAsync(Message(1));
        await writer.EnqueueAsync([Message(1), Message(3)]);

        await client.Received(1).PutItemAsync(
            Arg.Is<PutItemRequest>(request => request.ConditionExpression == "attribute_not_exists(#pk)"
                && request.Item["SK"].S == "0000000000000000001"),
            Arg.Any<CancellationToken>());
        await client.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(request => request.TransactItems.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Enqueue_NotifiesTheRelay_OnlyAfterTheWriteSucceeded()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        CountSequences(client);
        client.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ProvisionedThroughputExceededException("throttled"));
        var notifier = Substitute.For<IOutboxBucketNotifier>();
        var writer = new DynamoDbOutboxWriter(client, Options, notifier);

        await Assert.That(async () => await writer.EnqueueAsync(Message(1)))
            .Throws<ProvisionedThroughputExceededException>();
        await Assert.That(notifier.ReceivedCalls()).IsEmpty();

        await writer.EnqueueAsync([Message(4), Message(6), Message(4)]);
        notifier.Received(1).NotifyCommitted(Arg.Is<IReadOnlySet<int>>(buckets => buckets.Count == 2 && buckets.Contains(4) && buckets.Contains(6)));
    }

    [Test]
    public async Task Enqueue_TreatsARetriedPutOfItsOwnMessage_AsStored()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        CountSequences(client);
        var notifier = Substitute.For<IOutboxBucketNotifier>();
        var writer = new DynamoDbOutboxWriter(client, Options, notifier);
        var message = Message(2);
        // The first attempt was applied, its response was lost, and the AWS SDK retried:
        // the retry is refused by the very item the first attempt wrote.
        client.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(call =>
            new ConditionalCheckFailedException("The conditional request failed")
            {
                Item = call.Arg<PutItemRequest>().Item
            });

        await writer.EnqueueAsync(message);

        notifier.Received(1).NotifyCommitted(2);
        await client.Received(1).PutItemAsync(
            Arg.Is<PutItemRequest>(request =>
                request.ReturnValuesOnConditionCheckFailure == ReturnValuesOnConditionCheckFailure.ALL_OLD),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Enqueue_StillFails_WhenTheSequenceNumberHoldsAnotherMessage()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        CountSequences(client);
        var notifier = Substitute.For<IOutboxBucketNotifier>();
        var writer = new DynamoDbOutboxWriter(client, Options, notifier);
        client.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(
            new ConditionalCheckFailedException("The conditional request failed")
            {
                Item = new Dictionary<string, AttributeValue> { ["MessageId"] = new() { S = Guid.NewGuid().ToString() } }
            });

        // A reset counter handed out a number that a pending message still holds.
        await Assert.That(async () => await writer.EnqueueAsync(Message(2))).Throws<ConditionalCheckFailedException>();
        await Assert.That(notifier.ReceivedCalls()).IsEmpty();
    }

    [Test]
    public async Task EnqueueOfSeveral_SendsOneIdempotencyToken_SoARetryOfAnAppliedTransactionSucceeds()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        CountSequences(client);
        var tokens = new List<string>();
        client.TransactWriteItemsAsync(Arg.Do<TransactWriteItemsRequest>(request => tokens.Add(request.ClientRequestToken)),
            Arg.Any<CancellationToken>()).Returns(new TransactWriteItemsResponse());
        var writer = new DynamoDbOutboxWriter(client, Options);

        await writer.EnqueueAsync([Message(1), Message(3)]);
        await writer.EnqueueAsync([Message(1), Message(3)]);

        // Explicit, not left to the SDK: the guarantee must not rest on its defaults. One
        // token per call, so two calls never collapse into one transaction either.
        await Assert.That(tokens.Count).IsEqualTo(2);
        await Assert.That(tokens.All(token => !string.IsNullOrEmpty(token))).IsTrue();
        await Assert.That(tokens[0]).IsNotEqualTo(tokens[1]);
    }

    [Test]
    public async Task EnqueueOfSeveral_TreatsATransactionRefusedByItsOwnItems_AsStored()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        CountSequences(client);
        var notifier = Substitute.For<IOutboxBucketNotifier>();
        var writer = new DynamoDbOutboxWriter(client, Options, notifier);
        // The first attempt committed and its response was lost; the token has expired or a
        // retry policy outside the SDK sent the transaction again.
        client.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(call => new TransactionCanceledException("Transaction cancelled")
            {
                CancellationReasons =
                [
                    .. call.Arg<TransactWriteItemsRequest>().TransactItems.Select(item => new CancellationReason
                    {
                        Code = "ConditionalCheckFailed",
                        Item = item.Put.Item
                    })
                ]
            });

        await writer.EnqueueAsync([Message(4), Message(6)]);

        notifier.Received(1).NotifyCommitted(Arg.Is<IReadOnlySet<int>>(buckets => buckets.Count == 2));
        await client.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(request => request.TransactItems.All(item =>
                item.Put.ReturnValuesOnConditionCheckFailure == ReturnValuesOnConditionCheckFailure.ALL_OLD)),
            Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments("foreign-item")]
    [Arguments("other-reason")]
    [Arguments("partly-refused")]
    public async Task EnqueueOfSeveral_StillFails_WhenTheTransactionWasNotItsOwnRetry(string scenario)
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        CountSequences(client);
        var notifier = Substitute.For<IOutboxBucketNotifier>();
        var writer = new DynamoDbOutboxWriter(client, Options, notifier);
        client.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(call =>
            {
                var puts = call.Arg<TransactWriteItemsRequest>().TransactItems;
                var own = new CancellationReason { Code = "ConditionalCheckFailed", Item = puts[0].Put.Item };
                return new TransactionCanceledException("Transaction cancelled")
                {
                    CancellationReasons =
                    [
                        own,
                        scenario switch
                        {
                            // A reset counter handed out a number that another message holds.
                            "foreign-item" => new CancellationReason
                            {
                                Code = "ConditionalCheckFailed",
                                Item = new Dictionary<string, AttributeValue> { ["MessageId"] = new() { S = Guid.NewGuid().ToString() } }
                            },
                            "other-reason" => new CancellationReason { Code = "ThrottlingError" },
                            _ => new CancellationReason { Code = "None" }
                        }
                    ]
                };
            });

        await Assert.That(async () => await writer.EnqueueAsync([Message(4), Message(6)]))
            .Throws<TransactionCanceledException>();
        await Assert.That(notifier.ReceivedCalls()).IsEmpty();
    }

    [Test]
    public async Task NotifyCommitted_UsesBucketHintsWhereTheNotifierTakesThem()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var bucketNotifier = Substitute.For<IOutboxBucketNotifier>();
        var plainNotifier = Substitute.For<IOutboxNotifier>();

        new DynamoDbOutboxWriter(client, Options, bucketNotifier).NotifyCommitted(Message(3));
        new DynamoDbOutboxWriter(client, Options, bucketNotifier).NotifyCommitted([Message(7)]);
        new DynamoDbOutboxWriter(client, Options, plainNotifier).NotifyCommitted(Message(3));
        new DynamoDbOutboxWriter(client, Options, plainNotifier).NotifyCommitted([Message(3), Message(4)]);
        // A process that only enqueues has no relay to wake.
        new DynamoDbOutboxWriter(client, Options).NotifyCommitted(Message(3));
        new DynamoDbOutboxWriter(client, Options).NotifyCommitted([Message(3), Message(4)]);

        bucketNotifier.Received(1).NotifyCommitted(3);
        bucketNotifier.Received(1).NotifyCommitted(7);
        plainNotifier.Received(2).NotifyCommitted();
        await Assert.That(client.ReceivedCalls()).IsEmpty();
    }

    /// <summary>Atomic counters, as <c>ADD</c> with <c>UPDATED_NEW</c> behaves.</summary>
    private static List<string> CountSequences(IAmazonDynamoDB client, Dictionary<string, long>? counters = null)
    {
        counters ??= [];
        var reservations = new List<string>();
        client.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = call.Arg<UpdateItemRequest>();
            var partition = (request.Key.TryGetValue("PK", out var key) ? key : request.Key["pk"]).S;
            var count = long.Parse(request.ExpressionAttributeValues[":count"].N, CultureInfo.InvariantCulture);
            lock (counters)
            {
                reservations.Add($"{partition}+{count}");
                var next = counters.GetValueOrDefault(partition) + count;
                counters[partition] = next;
                return new UpdateItemResponse
                {
                    Attributes = new Dictionary<string, AttributeValue>
                    {
                        ["Sequence"] = new() { N = next.ToString(CultureInfo.InvariantCulture) }
                    }
                };
            }
        });
        return reservations;
    }

    private static OutboxMessage Message(int bucket) => new()
    {
        MessageId = Guid.NewGuid(),
        Bucket = bucket,
        Topic = "orders",
        CreatedAtUtc = DateTimeOffset.UtcNow
    };
}
