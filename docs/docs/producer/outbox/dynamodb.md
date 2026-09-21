---
sidebar_position: 2
sidebar_label: Amazon DynamoDB
description: "Register the DynamoDB outbox store, provision its table, enqueue messages inside TransactWriteItems, and understand the item schema and how relays share buckets."
---

# Amazon DynamoDB Outbox Store

`Dekaf.Outbox.DynamoDB` stores the [transactional outbox](./index.md) in one DynamoDB table. It is built for many instances of a service competing for the same buckets: relays that agree on who is alive never write the same lease, so a healthy fleet makes **no refused conditional write**. That matters on DynamoDB, because a refused write is billed, and the AWS SDK reports it as an error that tracing shows as a failed span.

Delivery guarantees, ordering, deduplication, tuning and metrics are the same for every store and are described on the [outbox overview](./index.md). This page covers what is specific to DynamoDB:

1. [Install and register](#install-and-register) the store, the writer and the relay.
2. [Provision the table](#provision-the-table) with the key schema the store expects.
3. [Enqueue messages](#enqueuing-messages) in the transaction that carries your business write.

The [item schema](#item-schema) and [how relays share buckets](#how-relays-share-buckets) are reference material for operators and for writers in other languages.

## Install and register

```bash
dotnet add package Dekaf.Outbox.DynamoDB
```

The package brings in `Dekaf.Outbox` (the relay) and `AWSSDK.DynamoDBv2`.

```csharp
using Amazon.DynamoDBv2;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Outbox;
using Dekaf.Outbox.DynamoDB;

builder.Services.AddSingleton<IAmazonDynamoDB>(new AmazonDynamoDBClient());
builder.Services.AddDekaf(dekaf => dekaf
    .AddDynamoDbOutboxStore(new DynamoDbOutboxOptions { TableName = "orders-outbox" })
    .AddOutboxRelay(producer => producer.WithBootstrapServers("localhost:9092")));
```

- `AddDynamoDbOutboxStore` registers `DynamoDbOutboxStore` as the `IOutboxStore` and `DynamoDbOutboxWriter` as the `IDynamoDbOutboxWriter`. It resolves `IAmazonDynamoDB` from the container, so register the client yourself or with `AWSSDK.Extensions.NETCore.Setup`.
- `AddOutboxRelay` adds the hosted relay that publishes and deletes the messages. Every instance of your service can run it: the relays [divide the buckets](#how-relays-share-buckets) among themselves.
- A process that only enqueues (an API that leaves publishing to a worker) registers the store call without `AddOutboxRelay` and injects `IDynamoDbOutboxWriter`.
- Without `AddDekaf`, call `services.AddDekafDynamoDbOutboxStore(...)` and `services.AddDekafOutboxRelay(...)`; they register exactly the same services.

To give the outbox its own client, for example one with another region, endpoint or retry policy, pass a factory. It runs once per container; the store and the writer share the client it returns, and the container does not dispose it:

```csharp
using Amazon.DynamoDBv2;
using Dekaf.Outbox.DynamoDB;

var outboxClient = new AmazonDynamoDBClient(Amazon.RegionEndpoint.EUWest1);
builder.Services.AddDekaf(dekaf => dekaf.AddDynamoDbOutboxStore(
    new DynamoDbOutboxOptions { TableName = "orders-outbox" },
    _ => outboxClient));
```

### Options

Every writer and every relay sharing a table must use the same `TableName`, key attribute names, `KeyPrefix` and `BucketCount`.

| Option | Default | Notes |
|---|---|---|
| `TableName` | required | The table holding the outbox items. |
| `PartitionKeyAttributeName` | `PK` | Name of the table's partition key attribute. |
| `SortKeyAttributeName` | `SK` | Name of the table's sort key attribute. |
| `KeyPrefix` | `OUTBOX` | Prefix of every partition key value the outbox writes. Distinct prefixes keep [several outboxes](#multiple-outboxes), or the outbox and your own items, apart in one table. |
| `BucketCount` | 8 | Must equal `OutboxRelayOptions.BucketCount` and the `bucketCount` passed to `OutboxMessage.Create`. See [below](#bucket-count). |
| `MaxConcurrency` | 8 | Concurrent DynamoDB requests of one store call. DynamoDB has no set-based conditional write, so leases are one request each; raise this with a large `BucketCount` to keep acquisition short. |
| `PendingCountLimit` | 10,000 | Messages counted per bucket for one [backlog sample](#cost-and-metrics). |

### Bucket count

The bucket count appears in three places, and they must agree: `DynamoDbOutboxOptions.BucketCount`, `OutboxRelayOptions.BucketCount`, and the `bucketCount` argument of `OutboxMessage.Create`. All three default to 8.

```csharp
using Dekaf.Outbox;
using Dekaf.Outbox.DynamoDB;

const int BucketCount = 32;

builder.Services.AddDekaf(dekaf => dekaf
    .AddDynamoDbOutboxStore(new DynamoDbOutboxOptions { TableName = "orders-outbox", BucketCount = BucketCount })
    .AddOutboxRelay(
        producer => producer.WithBootstrapServers("localhost:9092"),
        new OutboxRelayOptions { BucketCount = BucketCount }));
```

A writer with another count hashes the same key to another bucket, so that key's messages lose their order, and a writer with a larger count fills buckets that no relay claims. DynamoDB cannot list such partitions cheaply, so the writer stamps its count on every message instead. The relay **faults with `OutboxMisconfigurationException`** (under the default host behavior the application stops) when its own count differs from the store's, or when it reads a message stamped with another count. Drain the table before changing the count.

## Provision the table

The store needs one table with a **string partition key and a string sort key**, named `PK` and `SK` unless the options say otherwise. That is the whole schema: DynamoDB is schemaless beyond the key, and the store creates its items, including the lease items, on first use. Do not seed anything.

| Setting | Required value |
|---|---|
| Partition key | `PK`, type `S` (string) |
| Sort key | `SK`, type `S` (string) |
| Billing mode | On-demand recommended. With provisioned capacity, enable auto scaling: a throttled relay backs off and retries, which delays publishing. |
| Global secondary indexes | None needed. |
| Local secondary indexes | **None.** A local secondary index caps each partition key at 10 GB, which would cap a bucket's backlog. |
| Streams | Not needed. |
| Time to live | **Off** for the outbox items. See the warning below. |
| Region | One region. Conditional writes in a multi-region global table with eventual consistency do not arbitrate between regions, so two relays could own one bucket. |

Deletion protection and point-in-time recovery are your choice; the outbox works with either setting.

:::warning
Never let DynamoDB expire outbox items, and do not delete message items yourself. A message that disappears before the relay publishes it is lost: removing messages only after the broker acknowledged them is what makes the outbox at-least-once.
:::

**CloudFormation**

```yaml
OutboxTable:
  Type: AWS::DynamoDB::Table
  Properties:
    TableName: orders-outbox
    BillingMode: PAY_PER_REQUEST
    AttributeDefinitions:
      - { AttributeName: PK, AttributeType: S }
      - { AttributeName: SK, AttributeType: S }
    KeySchema:
      - { AttributeName: PK, KeyType: HASH }
      - { AttributeName: SK, KeyType: RANGE }
```

**Terraform**

```hcl
resource "aws_dynamodb_table" "orders_outbox" {
  name         = "orders-outbox"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "PK"
  range_key    = "SK"

  attribute {
    name = "PK"
    type = "S"
  }

  attribute {
    name = "SK"
    type = "S"
  }
}
```

**AWS CLI**

```bash
aws dynamodb create-table \
  --table-name orders-outbox \
  --billing-mode PAY_PER_REQUEST \
  --attribute-definitions AttributeName=PK,AttributeType=S AttributeName=SK,AttributeType=S \
  --key-schema AttributeName=PK,KeyType=HASH AttributeName=SK,KeyType=RANGE
```

**From .NET**, for development, tests and samples. `CreateIfNotExistsAsync` creates the same table, waits until it is active, and is safe to call from several instances at once. `DynamoDbOutboxTable.CreateTableRequest(options)` returns the definition if you want to adjust it first.

```csharp
using Amazon.DynamoDBv2;
using Dekaf.Outbox.DynamoDB;

// DynamoDB Local: docker run -p 8000:8000 amazon/dynamodb-local
var client = new AmazonDynamoDBClient(
    new Amazon.Runtime.BasicAWSCredentials("local", "local"),
    new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8000" });
var options = new DynamoDbOutboxOptions { TableName = "orders-outbox" };

await DynamoDbOutboxTable.CreateIfNotExistsAsync(client, options);
```

### Sharing a table with your own items

The outbox only touches items whose partition key starts with `KeyPrefix`, so it can live in a single-table design next to your own items, as long as the table has a string partition key and a string sort key. Point `PartitionKeyAttributeName` and `SortKeyAttributeName` at your key attributes and pick a prefix your own keys never use. A dedicated table is still the simpler choice: the outbox has a high write and delete rate, and its own table gets its own capacity, alarms and access policy.

### IAM permissions

Relays need these actions on the table. Writers need only `dynamodb:PutItem` and `dynamodb:UpdateItem`; a transaction is authorized by the actions it contains.

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "dynamodb:Query",
        "dynamodb:PutItem",
        "dynamodb:UpdateItem",
        "dynamodb:DeleteItem",
        "dynamodb:BatchWriteItem"
      ],
      "Resource": "arn:aws:dynamodb:eu-west-1:123456789012:table/orders-outbox"
    }
  ]
}
```

`CreateIfNotExistsAsync` additionally needs `dynamodb:CreateTable` and `dynamodb:DescribeTable`.

## Enqueuing messages

Put the message into the `TransactWriteItems` request that carries the business write. `IDynamoDbOutboxWriter` turns an `OutboxMessage` into the `Put` for that request:

```csharp
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Dekaf.Outbox;
using Dekaf.Outbox.DynamoDB;
using Dekaf.Serialization;

public sealed class OrderApplicationService(IAmazonDynamoDB dynamoDb, IDynamoDbOutboxWriter outbox)
{
    public async Task PlaceOrderAsync(Order order, CancellationToken cancellationToken)
    {
        var message = OutboxMessage.Create(
            "orders", order.Id, JsonSerializer.Serialize(order), Serializers.String, Serializers.String);

        await dynamoDb.TransactWriteItemsAsync(new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem { Put = new Put { TableName = "orders", Item = ToItem(order) } },
                // Reserves the message's sequence number, then returns its Put.
                await outbox.CreateTransactWriteItemAsync(message, cancellationToken)
            ]
        }, cancellationToken);

        // One commit: business item and message are atomic. Wake the local relay.
        outbox.NotifyCommitted(message);
    }

    private static Dictionary<string, AttributeValue> ToItem(Order order) => new()
    {
        ["PK"] = new AttributeValue { S = $"ORDER#{order.Id}" }
    };
}
```

If the transaction fails, the message does not exist. The business table and the outbox table can be different tables: a DynamoDB transaction spans tables in one region and account. Key and value are stored **pre-serialized**; the relay is a byte pass-through and never re-serializes.

| Writer member | Use |
|---|---|
| `CreateTransactWriteItemAsync(message)` | One message for your own transaction. |
| `CreateTransactWriteItemsAsync(messages)` | Several messages for your own transaction. One reservation per distinct bucket; messages sharing a bucket keep their list order. |
| `NotifyCommitted(message)` / `NotifyCommitted(messages)` | Wakes the relay in this process after your transaction committed. **Never call it before the commit.** |
| `EnqueueAsync(message)` / `EnqueueAsync(messages)` | Writes messages that have no business write, all or nothing, and notifies by itself. At most 100 messages per call. Safe under the AWS SDK's retries: a retried request whose first attempt was applied is a success. |

Retrying at a higher level is different. If `EnqueueAsync`, or your own transaction, fails with an ambiguous error and you run the operation again, the message takes a new sequence number and is stored a second time under the same `MessageId`. Nothing is lost and nothing is reordered for a consumer that [deduplicates on the message id](./index.md#consumer-side-deduplication); that is the same at-least-once contract as a lease takeover. Give your business write its own idempotency (a condition on the business item) if a second copy of it would matter.

`NotifyCommitted` is optional. Without it, or for a bucket that another instance owns, the owner finds the message on its next poll (one second by default) or through a [notification transport](./index.md#optional-cross-pod-notifications).

### Ordering and retries

DynamoDB has no auto-increment, so the writer takes the next number from the bucket's atomic counter before the transaction commits. The order of two messages in a bucket is the order of their reservations, whatever the host clocks say. A reservation that never commits leaves a gap, which is harmless. Two rules follow:

- **Create the items inside your retry loop.** A transaction that lost an optimistic concurrency race and is rebuilt from fresh state must reserve again. If it reuses its items, its message keeps a number older than the write that won the race.
- The message `Put` carries `attribute_not_exists`, so a counter that someone reset fails the business transaction instead of overwriting a pending message.

DynamoDB queries are only read-committed against `TransactWriteItems`: a relay's fetch that runs while a transaction commits can return a later message of that transaction without an earlier one. The store therefore probes the gaps in the sequence numbers of a fetched batch (up to eight per fetch), and fetches again when a message has appeared in one. The gap runs from the last message this relay removed, or from the start of the bucket after a start or a handover, when it has removed none yet. A batch that was fetched but not removed (a failed publish, a throttled delete) is fetched again, and its gaps are probed again: a transaction can commit into them during the second read as well as the first. A gap that stays empty (an abandoned reservation, a writer that has reserved but not committed) costs one minimal read. A message that does not exist yet cannot be waited for: as above, writers that do not serialize their commits have no order to keep.

As on [every store](./index.md#ordering), "enqueue order" means commit order for writers that serialize their writes to a key. Two uncoordinated transactions for one key have no defined order.

### Limits

- DynamoDB limits an item to **400 KB** and a transaction to **100 items and 4 MB**. A record that does not fit fails the business transaction; nothing is lost silently. Keep large payloads in S3 and publish a reference.
- One bucket is one partition key, which DynamoDB serves at up to 1,000 write units per second. Enqueuing a 1 KB message costs one write unit for the counter and two for the transactional put. Raise `BucketCount` (after draining the table) when enqueue throughput per bucket gets near that limit.

## Item schema

Every item lives under `KeyPrefix` (default `OUTBOX`). A writer in another language, a migration script or an operator inspecting the table during an incident must follow this layout exactly.

| Item | Partition key (`PK`) | Sort key (`SK`) |
|---|---|---|
| Message | `OUTBOX#MESSAGES#{bucket}` | Sequence number, 19 digits, zero padded: `0000000000000000042` |
| Sequence counter | `OUTBOX#SEQUENCE#{bucket}` | `SEQUENCE` |
| Lease | `OUTBOX#COORDINATION` | `LEASE#{bucket}`, 10 digits, zero padded: `LEASE#0000000003` |
| Relay heartbeat | `OUTBOX#COORDINATION` | `RELAY#{relayId}` |

`{bucket}` in a partition key is the decimal bucket number without padding. All timestamps are **UTC ticks** as a number (100 ns since 0001-01-01); convert with `new DateTimeOffset(ticks, TimeSpan.Zero)`.

**Message** — one per pending record, deleted after the broker acknowledges it. A strongly consistent query of one partition returns a bucket in enqueue order.

| Attribute | Type | Required | Meaning |
|---|---|---|---|
| `MessageId` | `S` | Yes | GUID. Stamped on the record as the `x-outbox-message-id` header. |
| `Topic` | `S` | Yes | Topic to publish to. |
| `Key` | `B` | No | Serialized key. Absent means a keyless record; an **empty binary is a real, empty key**. |
| `Value` | `B` | No | Serialized value. Absent means a tombstone. |
| `Headers` | `B` | No | Headers in the `OutboxHeaderCodec` encoding (leading version byte). |
| `Partition` | `N` | No | Explicit partition override. |
| `CreatedAtUtc` | `N` | Yes | Enqueue time in UTC ticks. Drives the oldest-pending-age metric, not the record timestamp. |
| `BucketCount` | `N` | Recommended | The writer's bucket count. The relay faults on a value that differs from its own; an absent value is not checked. |

The bucket is `OutboxBucket.Compute`: Kafka's Murmur2 hash of the serialized key modulo the bucket count, the explicit partition modulo the bucket count when one is set, and a hash of the message id for a keyless record.

**Sequence counter** — one per bucket. `Sequence` (`N`) is the last number handed out. A writer reserves `n` numbers with `UpdateItem`, update expression `ADD Sequence :n`, `ReturnValues = UPDATED_NEW`, and uses `new - n + 1` to `new`. The counters have their own partition keys so they do not share one partition's write capacity.

**Lease** — one per bucket, created by the first relay that claims it. `Owner` (`S`) is the relay id holding the bucket and is absent for a free bucket. `ExpiresAtUtc` (`N`) is the expiry in UTC ticks, and it doubles as the lease's version: an acquisition renews and releases under the expiry that round read, no renewal ever moves the expiry backwards, and an in-flight renewal only extends a lease that has not lapsed, so a request that a relay gave up on cannot land later and undo a newer write. A claim carries no version, because it takes a lease that the round only read as free, which is why a stopping relay reads the leases again after releasing them. Lease items beyond the current bucket count are ignored.

**Relay heartbeat** — one per relay. `LastSeenUtc` (`N`) is the relay's last round in UTC ticks, and every heartbeat write is conditional on it moving forward, so a request that a relay gave up on cannot put an older round back. A relay counts towards fair share while its heartbeat is younger than `LeaseDuration`. A stopping relay stamps `StoppedAtUtc` (`N`) on the record instead of deleting it, and peers ignore a stamped record from that moment; the record stays because a deleted one leaves no timestamp to refuse the heartbeat of the round the stop cancelled. The survivors delete a stopped record one lease duration later, and a dead relay's after ten.

## How relays share buckets

Every lease is one conditional `UpdateItem`; DynamoDB has no set-based conditional write. A relay that probed free buckets in its own order would collide with every peer doing the same, on every `LeaseRenewInterval`. The store avoids that in three steps per acquisition:

1. It writes its heartbeat, then reads **every lease and every heartbeat with one strongly consistent query**. They share a partition, so the read is one view.
2. It computes the fair share and the assigned range of **every** active relay (`OutboxFairShare`), and from them the claims of every relay: first each relay takes the free buckets of its own range, then the remaining free buckets go to the relays that are still short, in relay-id order. Shares follow what the relays hold: where the split leaves a choice, the relay that already publishes a bucket keeps it, whatever the ids sort as.
3. It carries out only its own part: renew what it holds, hand back what exceeds its share (buckets outside its range first), claim what the plan gives it.

Relays that read the same state compute the same plan, so their writes are disjoint. A refusal means that two relays read different states while membership was changing; the conditional write then picks the winner, and the next round agrees again. The plan orders writes. It never grants ownership: a bucket has one owner because DynamoDB accepts one conditional write.

| Event | What the fleet does | Refused writes |
|---|---|---|
| Steady state | Each relay renews its own leases: one write per owned bucket per `LeaseRenewInterval`. | None |
| Instance starts, scale out | It owns nothing while its peers hold every bucket. The peers see its heartbeat on their next round and hand back what exceeds their new share; it claims that on its following round. Buckets a peer keeps do not move, and nothing moves at all when the split was already fair: a pod that joins a fleet with more instances than buckets becomes a standby, even if its id sorts first. | None |
| Graceful stop, rolling update, scale in | After the publish loop ends, the relay stamps its heartbeat record as stopped and frees its leases (guarded by owner), then reads the leases again until none names it. Peers claim the buckets on their next round, not after `LeaseDuration`. The record is stamped first: a release that throttling or the shutdown deadline cuts short then leaves leases that lapse, as after a crash, and never freed buckets that peers keep reserved for a relay they still count. | None |
| Crash, `SIGKILL`, out of memory, node loss | Leases and heartbeat lapse together after `LeaseDuration`. Until then the survivors write nothing to the dead relay's buckets. Then they split them. | None |
| Whole fleet replaced at once | The new instances wait for the old leases to lapse, then split the buckets. | None |
| Stop that misses the shutdown deadline | The relay does not release, because it may still be publishing. Same as a crash. | None |
| Restart under the same `RelayId` | The leases still name the relay, so it resumes them at once. Only configure a stable `RelayId` when two live processes can never share it. | None |
| Host clock set back by more than `LeaseRenewInterval` | The relay's renewals would move its leases' expiry backwards, so it does not send them, logs a warning, and publishes nothing from those buckets until its clock has passed the stored expiry. The leases still name it, so no peer takes them. It claims no free bucket either while its heartbeat is refused: a lease claimed with a clock that is behind would expire early on its peers' clocks. | One per round: the heartbeat, whose timestamp only moves forward too |
| Stall past the lease (long pause, suspended VM) | A peer takes the expired leases. The stalled relay's in-flight renewal is refused, it stops publishing those buckets, and it gets a share back only when a peer hands one over. | One per lost bucket: a real loss |
| Many instances start at once on free buckets | Heartbeats land while peers are already planning, so plans can overlap once. Every bucket still gets exactly one owner, and the next round is quiet. | A few, once |
| More instances than buckets | The surplus instances are standbys: they keep a heartbeat, own nothing, write no lease, and step in when a bucket frees up, in relay-id order. The first standbys, one per bucket, run every round, so a takeover is as prompt as with fewer instances. A standby behind them sends nothing every other round (with the default timings): it refreshes its heartbeat within three quarters of the `LeaseDuration` for which its peers count it, and can take that long to notice that it has moved up. | None |

In-flight renewal never revives an expired lease, even one that nobody took: the relay cannot prove that it owned the bucket throughout. The next acquisition retakes such a lease under the owner condition.

Lease expiry compares host clocks, as for [every store](./index.md#ordering): keep the hosts synchronized to within `LeaseDuration − LeaseRenewInterval`. The Amazon Time Sync Service on EC2, ECS, EKS and Lambda is far tighter than that.

## Multiple outboxes

`AddDynamoDbOutboxStore` and `AddOutboxRelay` register **one** store, writer and relay per host. To run a second logical outbox, give it its own table or its own `KeyPrefix` and wire the additional pieces explicitly; every piece has a public constructor:

```csharp
using Amazon.DynamoDBv2;
using Dekaf.Outbox;
using Dekaf.Outbox.DynamoDB;

var billingOptions = new DynamoDbOutboxOptions { TableName = "outbox", KeyPrefix = "BILLING" };

services.AddKeyedSingleton<IDynamoDbOutboxWriter>("billing", (provider, _) => new DynamoDbOutboxWriter(
    provider.GetRequiredService<IAmazonDynamoDB>(), billingOptions));

// Registered (keyed) so the container owns the publisher's disposal.
services.AddKeyedSingleton<IOutboxPublisher>("billing", (provider, _) =>
    new DekafOutboxPublisher(OutboxServiceCollectionExtensions.CreateRelayProducerBuilder(
            producer => producer.WithBootstrapServers("localhost:9092"),
            provider.GetService<ILoggerFactory>())
        .Build()));

services.AddSingleton<IHostedService>(provider => new OutboxRelayService(
    new DynamoDbOutboxStore(provider.GetRequiredService<IAmazonDynamoDB>(), billingOptions),
    provider.GetRequiredKeyedService<IOutboxPublisher>("billing"),
    new OutboxRelayOptions { MetricsName = "billing" },
    provider.GetRequiredService<ILogger<OutboxRelayService>>()));
```

A writer built this way has no notifier, so its relay finds the messages by polling.

## Cost and metrics

- **Idle relay:** one eventually consistent one-key query per owned bucket per `PollInterval`, plus one query, one heartbeat write and one write per owned bucket per `LeaseRenewInterval`. The probe tolerates a stale answer; fetches are strongly consistent, so a deleted message is not published again. A longer `PollInterval` lowers the idle read cost; commit notifications keep local latency low regardless.
- **Standby:** an instance without a bucket sends the query and the heartbeat write only, and [every other round](#how-relays-share-buckets) once it is more than `BucketCount` places back in line. With many more pods than buckets, this is most of the fleet. Running the relay in a small worker deployment of its own, and only the writer in the application pods, removes that traffic altogether.
- **Per message:** one counter update and one transactional put to enqueue, a share of one query to fetch, a share of one `BatchWriteItem` (25 deletes) to mark. Deletes that DynamoDB throttles come back unprocessed and are retried with backoff; if that fails, the messages stay and are published again, as duplicates and never as a loss.
- **Backlog metrics:** DynamoDB has no aggregate, and it bills a count as a read of every counted item. `dekaf.outbox.pending.messages` therefore counts at most `PendingCountLimit` messages per bucket; a bucket at the limit reports the limit. The oldest-age metric reads the head of each bucket, which is the message a stalled outbox is stuck on. By default [only the owner of bucket zero samples](./index.md#horizontal-scaling), so the cost does not grow with the number of instances.
- **Telemetry:** a `ConditionalCheckFailedException` span from this store means a relay lost a race or a lease, as the table above lists, or that a heartbeat was refused because the record already carries a later timestamp. A stopping host produces the second kind once, for a request it abandoned. Neither is steady-state noise, and a recurring one is worth investigating: look for clock skew, relays in several regions, or two processes sharing a `RelayId`.
