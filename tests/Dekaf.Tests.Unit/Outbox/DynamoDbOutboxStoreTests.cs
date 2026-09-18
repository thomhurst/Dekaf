using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Dekaf.Outbox;
using Dekaf.Outbox.DynamoDB;
using NSubstitute;

namespace Dekaf.Tests.Unit.Outbox;

/// <summary>
/// Request shapes and failure paths that DynamoDB Local cannot be made to produce on demand:
/// a write refused between the read and the write, throttled batch deletes, paged reads.
/// The expressions themselves are verified against DynamoDB Local in the integration suite.
/// </summary>
public sealed class DynamoDbOutboxStoreTests
{
    private static readonly DynamoDbOutboxOptions Options = new() { TableName = "outbox" };
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    private const string KeepCondition = "#owner = :me AND #expires = :seen";

    private static readonly string LeaseExpiry = Now.AddSeconds(20).UtcTicks.ToString(CultureInfo.InvariantCulture);

    private static readonly OutboxLeaseRequest Request = new()
    {
        RelayId = "relay-a",
        BucketCount = 8,
        LeaseDuration = TimeSpan.FromSeconds(30)
    };

    [Test]
    public async Task SteadyStateRound_IsOneHeartbeat_OneConsistentRead_AndOneRenewalPerOwnedBucket()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ReturnCoordination(client, [.. Leases("relay-a", 0, 3), .. Leases("relay-b", 4, 7), Relay("relay-a"), Relay("relay-b")]);
        var store = new DynamoDbOutboxStore(client, Options, new FixedClock());

        var owned = await store.AcquireBucketLeasesAsync(Request);

        await Assert.That(string.Join(',', owned)).IsEqualTo("0,1,2,3");
        await client.Received(1).PutItemAsync(
            Arg.Is<PutItemRequest>(request => request.Item["SK"].S == "RELAY#relay-a"), Arg.Any<CancellationToken>());
        await client.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(request => request.ConsistentRead == true), Arg.Any<CancellationToken>());
        // Renewals only, each guarded by owner and by the expiry this round read: nothing
        // here can be refused by a healthy peer, and a straggler from an older round is.
        await client.Received(4).UpdateItemAsync(
            Arg.Is<UpdateItemRequest>(request => request.ConditionExpression == KeepCondition
                && request.UpdateExpression == "SET #expires = :expiry"
                && request.ExpressionAttributeValues[":seen"].N == LeaseExpiry),
            Arg.Any<CancellationToken>());
        await client.Received(4).UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RefusedClaim_IsNotReportedAsOwned()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ReturnCoordination(client, []);
        RefuseLeaseWrites(client, bucket: 2);
        var store = new DynamoDbOutboxStore(client, Options, new FixedClock());

        var owned = await store.AcquireBucketLeasesAsync(Request);

        // A peer claimed bucket 2 between this relay's read and its write.
        await Assert.That(string.Join(',', owned)).IsEqualTo("0,1,3,4,5,6,7");
        await client.Received(8).UpdateItemAsync(
            Arg.Is<UpdateItemRequest>(request => request.ConditionExpression
                == "attribute_not_exists(#owner) OR attribute_not_exists(#expires) OR #expires <= :now"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RefusedRenewalDuringAcquisition_DropsTheBucket()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ReturnCoordination(client, [.. Leases("relay-a", 0, 7), Relay("relay-a")]);
        RefuseLeaseWrites(client, bucket: 5);
        var store = new DynamoDbOutboxStore(client, Options, new FixedClock());

        var owned = await store.AcquireBucketLeasesAsync(Request);

        await Assert.That(string.Join(',', owned)).IsEqualTo("0,1,2,3,4,6,7");
    }

    [Test]
    public async Task CoordinationRead_FollowsEveryPage_BeforePlanning()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var lastKey = new Dictionary<string, AttributeValue> { ["SK"] = new() { S = "LEASE#0000000003" } };
        client.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>()).Returns(
            new QueryResponse { Items = [.. Leases("relay-a", 0, 3)], LastEvaluatedKey = lastKey },
            new QueryResponse { Items = [.. Leases("relay-b", 4, 7), Relay("relay-a"), Relay("relay-b")] });
        var store = new DynamoDbOutboxStore(client, Options, new FixedClock());

        var owned = await store.AcquireBucketLeasesAsync(Request);

        // Stopping at the first page would hide the peer and send claims at its buckets.
        await Assert.That(string.Join(',', owned)).IsEqualTo("0,1,2,3");
        await client.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(request => ReferenceEquals(request.ExclusiveStartKey, lastKey)),
            Arg.Any<CancellationToken>());
        await client.DidNotReceive().UpdateItemAsync(
            Arg.Is<UpdateItemRequest>(request => request.ConditionExpression != KeepCondition),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Renewal_RequiresOwnerAndAnUnexpiredLease_AndHeartbeatsOnlyWhenEveryLeaseWasRenewed()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var store = new DynamoDbOutboxStore(client, Options, new FixedClock());

        await Assert.That(await store.RenewBucketLeasesAsync(Request, [1, 6])).IsTrue();
        await client.Received(2).UpdateItemAsync(
            Arg.Is<UpdateItemRequest>(request => request.ConditionExpression
                    == "#owner = :me AND #expires > :now AND #expires <= :expiry"
                && request.ExpressionAttributeValues[":now"].N == Now.UtcTicks.ToString(CultureInfo.InvariantCulture)
                && request.ExpressionAttributeValues[":expiry"].N
                    == (Now + Request.LeaseDuration).UtcTicks.ToString(CultureInfo.InvariantCulture)),
            Arg.Any<CancellationToken>());
        await client.Received(1).PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>());

        client.ClearReceivedCalls();
        RefuseLeaseWrites(client, bucket: 6);
        await Assert.That(await store.RenewBucketLeasesAsync(Request, [1, 6])).IsFalse();
        // A relay that lost a bucket must not look alive on the strength of this renewal.
        await client.DidNotReceive().PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Release_IsGuardedByOwner_SurvivesATakeoverAfterItsRead_AndRemovesTheHeartbeat()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ReturnCoordination(client, [.. Leases("relay-a", 0, 1), .. Leases("relay-b", 2, 3)]);
        RefuseLeaseWrites(client, bucket: 1);
        var store = new DynamoDbOutboxStore(client, Options, new FixedClock());

        await store.ReleaseBucketLeasesAsync(Request, previousBuckets: [0, 1, 2, 3]);

        // Only this relay's leases, whatever the hint lists, and each write names the owner.
        await client.Received(2).UpdateItemAsync(
            Arg.Is<UpdateItemRequest>(request => request.ConditionExpression == KeepCondition
                && request.UpdateExpression == "SET #expires = :now REMOVE #owner"
                && request.ExpressionAttributeValues[":me"].S == "relay-a"
                && request.ExpressionAttributeValues[":seen"].N == LeaseExpiry),
            Arg.Any<CancellationToken>());
        await client.Received(2).UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>());
        await client.Received(1).DeleteItemAsync(
            Arg.Is<DeleteItemRequest>(request => request.Key["SK"].S == "RELAY#relay-a" && request.ConditionExpression == null),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Fetch_IsStronglyConsistent_AndThePendingProbeIsACheapOneKeyRead()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        client.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>()).Returns(new QueryResponse());
        var store = new DynamoDbOutboxStore(client, Options, new FixedClock());

        // The AWS SDK leaves the collections of an empty response null.
        await Assert.That(await store.GetNextBatchAsync(3, 500)).IsEmpty();
        await Assert.That(await store.GetBucketsWithPendingAsync([3])).IsEmpty();

        await client.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(request => request.ConsistentRead == true && request.Limit == 500
                && request.ExpressionAttributeValues[":pk"].S == "OUTBOX#MESSAGES#3"),
            Arg.Any<CancellationToken>());
        await client.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(request => request.ConsistentRead != true && request.Limit == 1
                && request.ProjectionExpression == "#projected"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task LeaseWithoutAnExpiry_IsFree_NotStuckWithWhoeverItNames()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var foreign = Leases("relay-gone", 2, 2).Single();
        foreign.Remove("ExpiresAtUtc");
        ReturnCoordination(client, [foreign]);
        var store = new DynamoDbOutboxStore(client, Options, new FixedClock());

        var owned = await store.AcquireBucketLeasesAsync(Request);

        // No comparison can match a missing expiry, so the claim names that case itself.
        await Assert.That(string.Join(',', owned)).IsEqualTo("0,1,2,3,4,5,6,7");
    }

    [Test]
    public async Task Fetch_ReadsAgain_WhenAMessageCommittedIntoAGapBehindTheQuery()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var queries = new List<QueryRequest>();
        client.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            queries.Add(call.Arg<QueryRequest>());
            return queries.Count switch
            {
                // A transaction wrote 2 and 3. The first read passed position 2 before the
                // commit and still returned 3: a query is only read-committed.
                1 => new QueryResponse { Items = [Message(1), Message(3)] },
                // The probe of the gap finds message 2.
                2 => new QueryResponse { Count = 1 },
                _ => new QueryResponse { Items = [Message(1), Message(2), Message(3)] }
            };
        });
        var store = new DynamoDbOutboxStore(client, Options, new FixedClock());

        var batch = await store.GetNextBatchAsync(4, 100);

        await Assert.That(string.Join(',', batch.Select(message => message.Id))).IsEqualTo("1,2,3");
        await Assert.That(queries.Count).IsEqualTo(3);
        await Assert.That(queries[1].Select).IsEqualTo(Select.COUNT);
        await Assert.That(queries[1].Limit).IsEqualTo(1);
        await Assert.That(queries[1].ConsistentRead == true).IsTrue();
        await Assert.That(queries[1].ExpressionAttributeValues[":first"].S).IsEqualTo("0000000000000000002");
        await Assert.That(queries[1].ExpressionAttributeValues[":last"].S).IsEqualTo("0000000000000000002");
    }

    [Test]
    public async Task FirstFetchOfABucket_ProbesEverythingBelowItsHead_BecauseAHandoverLeavesNoBaseline()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var queries = new List<QueryRequest>();
        client.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            queries.Add(call.Arg<QueryRequest>());
            return queries.Count switch
            {
                // This store has just taken the bucket over. Its peer published up to 9; a
                // transaction wrote 10 and 11, and the query returned only 11.
                1 => new QueryResponse { Items = [Message(11)] },
                2 => new QueryResponse { Count = 1 },
                3 => new QueryResponse { Items = [Message(10), Message(11)] },
                // The second fetch of the bucket has a baseline again.
                4 => new QueryResponse { Items = [Message(12)] },
                _ => throw new InvalidOperationException("A contiguous batch needs no probe.")
            };
        });
        var store = new DynamoDbOutboxStore(client, Options, new FixedClock());

        var first = await store.GetNextBatchAsync(4, 100);
        var second = await store.GetNextBatchAsync(4, 100);

        await Assert.That(string.Join(',', first.Select(message => message.Id))).IsEqualTo("10,11");
        await Assert.That(queries[1].ExpressionAttributeValues[":first"].S).IsEqualTo("0000000000000000001");
        await Assert.That(queries[1].ExpressionAttributeValues[":last"].S).IsEqualTo("0000000000000000010");
        await Assert.That(string.Join(',', second.Select(message => message.Id))).IsEqualTo("12");
        await Assert.That(queries.Count).IsEqualTo(4);
    }

    [Test]
    public async Task Fetch_KeepsABatch_WhoseGapsStayEmpty_AndProbesTheGapBehindThePreviousBatch()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var queries = new List<QueryRequest>();
        client.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            queries.Add(call.Arg<QueryRequest>());
            return queries.Count switch
            {
                1 => new QueryResponse { Items = [Message(1), Message(2)] },
                // Sequence 3 was reserved by a transaction that never committed.
                2 => new QueryResponse { Items = [Message(4), Message(5)] },
                _ => new QueryResponse { Count = 0 }
            };
        });
        var store = new DynamoDbOutboxStore(client, Options, new FixedClock());

        await store.GetNextBatchAsync(4, 100);
        var batch = await store.GetNextBatchAsync(4, 100);

        // One minimal probe for the abandoned number, and no second fetch.
        await Assert.That(string.Join(',', batch.Select(message => message.Id))).IsEqualTo("4,5");
        await Assert.That(queries.Count).IsEqualTo(3);
        await Assert.That(queries[2].ExpressionAttributeValues[":first"].S).IsEqualTo("0000000000000000003");
    }

    [Test]
    public async Task Fetch_BoundsItsProbes_WhenManyWritersLeaveManyGaps()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var probes = 0;
        client.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (call.Arg<QueryRequest>().Select != Select.COUNT)
                return new QueryResponse { Items = [.. Enumerable.Range(0, 40).Select(index => Message(1 + (index * 2)))] };
            probes++;
            return new QueryResponse { Count = 0 };
        });
        var store = new DynamoDbOutboxStore(client, Options, new FixedClock());

        var batch = await store.GetNextBatchAsync(4, 100);

        await Assert.That(batch.Count).IsEqualTo(40);
        await Assert.That(probes).IsEqualTo(8);
    }

    [Test]
    public async Task MarkPublished_RetriesOnlyWhatDynamoDbLeftUnprocessed()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var requests = new List<List<string>>();
        client.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var deletes = call.Arg<BatchWriteItemRequest>().RequestItems["outbox"];
            requests.Add([.. deletes.Select(delete => delete.DeleteRequest.Key["SK"].S)]);
            // Throttled once: the last two deletes of the first request come back.
            return requests.Count == 1
                ? new BatchWriteItemResponse
                {
                    UnprocessedItems = new Dictionary<string, List<WriteRequest>> { ["outbox"] = deletes[^2..] }
                }
                : new BatchWriteItemResponse();
        });
        var store = new DynamoDbOutboxStore(client, Options, new ImmediateTimers());

        await store.MarkPublishedAsync(4, [.. Enumerable.Range(1, 30).Select(Published)]);

        await Assert.That(requests.Count).IsEqualTo(3);
        await Assert.That(requests[0].Count).IsEqualTo(25);
        await Assert.That(string.Join(',', requests[1])).IsEqualTo("0000000000000000024,0000000000000000025");
        await Assert.That(requests[2].Count).IsEqualTo(5);
    }

    [Test]
    public async Task MarkPublished_Fails_WhenThrottlingOutlastsItsRetries()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        client.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
            new BatchWriteItemResponse
            {
                UnprocessedItems = new Dictionary<string, List<WriteRequest>>
                {
                    ["outbox"] = call.Arg<BatchWriteItemRequest>().RequestItems["outbox"]
                }
            });
        var store = new DynamoDbOutboxStore(client, Options, new ImmediateTimers());

        // The relay treats this as a failed cycle: the rows stay and are published again.
        await Assert.That(async () => await store.MarkPublishedAsync(0, [Published(1)]))
            .Throws<InvalidOperationException>();
        await client.Received(8).BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MarkPublished_OfNothing_MakesNoRequest()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var store = new DynamoDbOutboxStore(client, Options);

        await store.MarkPublishedAsync(0, []);

        await Assert.That(client.ReceivedCalls()).IsEmpty();
    }

    private static void ReturnCoordination(IAmazonDynamoDB client, List<Dictionary<string, AttributeValue>> items) =>
        client.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = items });

    private static void RefuseLeaseWrites(IAmazonDynamoDB client, int bucket) =>
        client.UpdateItemAsync(
                Arg.Is<UpdateItemRequest>(request => request.Key["SK"].S == DynamoDbOutboxSchema.LeaseSortKey(bucket)),
                Arg.Any<CancellationToken>())
            .Returns<UpdateItemResponse>(_ => throw new ConditionalCheckFailedException("The conditional request failed"));

    private static IEnumerable<Dictionary<string, AttributeValue>> Leases(string owner, int first, int last) =>
        Enumerable.Range(first, last - first + 1).Select(bucket => new Dictionary<string, AttributeValue>
        {
            ["PK"] = new() { S = "OUTBOX#COORDINATION" },
            ["SK"] = new() { S = DynamoDbOutboxSchema.LeaseSortKey(bucket) },
            ["Owner"] = new() { S = owner },
            ["ExpiresAtUtc"] = DynamoDbOutboxSchema.Number(Now.AddSeconds(20).UtcTicks)
        });

    private static Dictionary<string, AttributeValue> Relay(string relayId) => new()
    {
        ["PK"] = new() { S = "OUTBOX#COORDINATION" },
        ["SK"] = new() { S = DynamoDbOutboxSchema.RelaySortKey(relayId) },
        ["LastSeenUtc"] = DynamoDbOutboxSchema.Number(Now.AddSeconds(-10).UtcTicks)
    };

    private static Dictionary<string, AttributeValue> Message(long sequence) => new()
    {
        ["PK"] = new() { S = "OUTBOX#MESSAGES#4" },
        ["SK"] = new() { S = DynamoDbOutboxSchema.MessageSortKey(sequence) },
        ["MessageId"] = new() { S = Guid.NewGuid().ToString() },
        ["Topic"] = new() { S = "orders" },
        ["CreatedAtUtc"] = DynamoDbOutboxSchema.Number(Now.UtcTicks),
        ["BucketCount"] = DynamoDbOutboxSchema.Number(8)
    };

    private static OutboxMessage Published(int sequence) => new()
    {
        Id = sequence,
        MessageId = Guid.NewGuid(),
        Bucket = 4,
        Topic = "orders",
        CreatedAtUtc = Now
    };

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>Fires every timer at once, so retry backoff costs the test no time.</summary>
    private sealed class ImmediateTimers : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            ThreadPool.QueueUserWorkItem(_ => callback(state));
            return new Timer();
        }

        private sealed class Timer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
