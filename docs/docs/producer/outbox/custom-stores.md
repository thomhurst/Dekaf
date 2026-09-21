---
sidebar_position: 3
sidebar_label: Custom Stores
description: "Implement the outbox store contract for any database: lease timing, the four-method contract, stable bucket ownership, graceful handover, and wiring."
---

# Custom Outbox Stores

The relay engine (`Dekaf.Outbox`) never touches a database API. The [Entity Framework Core](./entity-framework-core.md) and [Amazon DynamoDB](./dynamodb.md) packages are two implementations of the contract below; implement it yourself for relational, NoSQL, or any other storage. Delivery, ordering and the relay's behavior are described on the [outbox overview](./index.md).

## Lease timing and migration

The EF store implements `IOutboxLeaseRenewalStore`. Its renewal atomically checks ownership and unexpired leases, extends them, and refreshes the relay heartbeat. It does **not** acquire or relinquish buckets while a publish is pending. Fair-share rebalancing resumes after each bounded sweep of ready buckets; in-flight renewal does not reset its deadline. Publication and lease store calls remain serialized: renewal runs alongside the publisher. Optional metrics collection uses a separate context and can run concurrently with these store calls.

Custom stores should implement the same optional capability. Without it, registration must provide `MaxPublishDuration`; an unspecified bound now fails at startup with `OutboxMisconfigurationException`. For example, if measurement establishes that a custom publisher's whole batch completes within two minutes:

```csharp
var relayOptions = new OutboxRelayOptions
{
    MaxPublishDuration = TimeSpan.FromMinutes(2),
    LeaseDuration = TimeSpan.FromMinutes(3),
    LeaseRenewInterval = TimeSpan.FromSeconds(10)
};
```

The relay measures lease age from **before** acquisition, then rechecks after the pending-bucket probe and batch fetch. Before a legacy-store publish, it reserves the full publish budget plus one renewal interval, renewing first if necessary. If acquisition latency still leaves too little time, it keeps the rows unpublished and logs the configuration problem. `BatchSize`, sequential submission, backpressure and delivery attempts all affect the whole-call bound. Raising a lease above one record's timeout alone does not establish safety.

`MaxPublishDuration` is a timing contract, not a timeout that aborts Kafka delivery. Exceeding it faults the relay instead of repeatedly publishing under an invalid assumption. Custom publishers must yield during asynchronous waits, honor shutdown cancellation, and account for all work covered by their bound.

Renewal cannot protect against a process pause or database outage longer than the remaining lease. After losing a lease during a pending publish, the relay observes the publisher's completion and retains the rows for takeover; it does not start another publish concurrently. Cancellation cannot retract records already appended to Kafka. Such records can still arrive after takeover, so consumer-side message-ID deduplication remains necessary.

## Store contract

`IOutboxStore` is a four-method contract (`AcquireBucketLeasesAsync`, `GetBucketsWithPendingAsync`, `GetNextBatchAsync`, `MarkPublishedAsync`) with **no relational assumptions** — implement it for Dapper, raw ADO.NET, MongoDB, DynamoDB, Cosmos DB, or any storage that offers the two primitives below. The relay engine (`Dekaf.Outbox`) never touches a database API; the EF Core package is just one store.

What a storage technology must provide:

1. **An atomic conditional write** for leases — a SQL guarded `UPDATE ... WHERE owner IS NULL OR expires <= now`, MongoDB `findOneAndUpdate`, DynamoDB conditional `PutItem`, Redis `SET NX PX`. That single primitive is the entire concurrency model; no row locks, transactions across documents, or fencing tokens are required.
2. **Per-bucket enqueue-order reads** — `GetNextBatchAsync` must return a bucket's pending messages oldest-first. *How* is the store's business: an auto-increment column, a monotonic sequence, a time-ordered document id (e.g. ObjectId), or an explicit counter all satisfy it. The `OutboxMessage.Id` long is a relational convenience, not the contract's identity — non-relational stores may leave it zero.

**Message identity is opaque to the relay.** `MarkPublishedAsync` always receives the *same instances* `GetNextBatchAsync` returned — a contiguous prefix, in order. A store can therefore identify what to delete three ways:

```csharp
public sealed class MongoOutboxMessage : OutboxMessage
{
    public required string DocumentId { get; init; }
}

public sealed class MongoOutboxStore
{
    public ValueTask MarkPublishedAsync(
        int bucket,
        IReadOnlyList<OutboxMessage> published,
        CancellationToken cancellationToken)
    {
        // Relational stores can identify rows by Id; any store can use MessageId.
        var relationalIds = published.Select(message => message.Id);
        var messageIds = published.Select(message => message.MessageId);

        // A NoSQL store can instead return a subclass carrying its native identifier.
        var documentIds = published
            .Cast<MongoOutboxMessage>()
            .Select(message => message.DocumentId);

        return ValueTask.CompletedTask;
    }
}
```

Subclassing was chosen over a generic `IOutboxStore<TMessage>` deliberately: a generic parameter would ripple through the relay, the publisher, and every DI registration for all users, while buying nothing the instance pass-back doesn't already provide.

The enqueue side is equally storage-agnostic: `OutboxMessage.Create(...)` serializes with Dekaf serializers and computes the bucket; persist the result in your service's native transaction (a MongoDB session, a DynamoDB `TransactWriteItems`) alongside the business write.

Stores without an auto-increment primitive typically reserve a per-bucket sequence number with an atomic counter before the business transaction commits, as the [DynamoDB store](./dynamodb.md) does; sequence gaps from abandoned reservations are harmless — the ordering contract only needs monotonicity, not density.

Semantics your implementation must preserve, in exchange for the relay's guarantees: rows are removed only via `MarkPublishedAsync` (never expired away — a TTL on the pending collection would convert at-least-once into loss), lease grants respect fair-share behavior across active relays (or at minimum never grant one bucket to two live relays), and out-of-range buckets should fail loudly rather than sit unclaimed.

## Stable ownership and graceful handover

`AcquireBucketLeasesAsync` receives a relay ID and a bucket count, not the buckets the relay already holds. The relay re-acquires on every `LeaseRenewInterval`, because that is how it notices membership changes. A store that claims buckets with one conditional write per bucket (DynamoDB, Redis) therefore cannot probe its own leases first. A relay whose probe order starts inside a peer's holdings is refused on every cycle, forever, on an idle outbox. Those refusals are harmless but not free: DynamoDB bills a failed conditional write, and the AWS SDK reports it as an error span.

Implement the optional `IOutboxLeaseOwnershipStore` to opt in to two things:

1. **Acquisition with a hint.** The relay calls `AcquireBucketLeasesAsync(request, previousBuckets, cancellationToken)` instead of the `IOutboxStore` method. `previousBuckets` is the result of the relay's last successful acquisition. It is a probe-order hint, never proof of ownership: the relay keeps it across lease expiry and store failures, so the conditional write stays the authority.
2. **Release on graceful shutdown.** A stop first lets the publisher call in flight return and removes the rows Kafka acknowledged before it, with a `MarkPublishedAsync` that carries the host's shutdown deadline instead of the stopping token: the release hands the bucket to a peer at once, and rows left behind would be published again on every deployment. The relay then calls `ReleaseBucketLeasesAsync` at most once, from `StopAsync`, after the publish loop has ended and the last publisher call has returned. With no time left before the deadline it starts neither call. Free the relay's leases (guarded by owner) and retire its liveness record, by deleting it or by marking it stopped. Release by owner where the storage allows it, because `previousBuckets` can be stale or incomplete; a store that can only address one bucket at a time releases the listed buckets. Honor the token: it is the host's shutdown deadline, and the relay stops waiting when it fires. Peers then claim the buckets and recompute fair share on their next acquisition, instead of after `LeaseDuration`. If the relay does not stop before the host's shutdown deadline, the relay skips the release: a peer must never be invited onto rows that are still being published. A failed release is logged and the leases expire as before.

Both contracts ask for an atomic owner condition. A store that talks to its database over requests that can outlive the caller (HTTP APIs such as DynamoDB, as opposed to a statement on a connection that dies with its process) should also guard against a request that arrives late: one the relay gave up on, or one from an earlier process under a reused `RelayId`. Treat the lease's expiry, or a counter, as a version. Condition a renewal or a release on the version the relay last read or wrote, and never let a renewal move the expiry backwards. The liveness record needs the same care: make its write conditional on its timestamp moving forward, and mark it stopped rather than deleting it, because a deleted record leaves nothing for a late heartbeat to lose to, and a relay that peers still count holds up the handover for a whole `LeaseDuration`. For the same reason, retire the liveness record before freeing the leases: a release cut short between the two then leaves leases that expire, which costs no more than a crash, instead of freed buckets that peers keep reserved for a relay they still count. One write cannot be guarded this way — a claim of a bucket the relay did not hold, which the release never writes — so read the leases again after releasing and hand back what a late claim took. The DynamoDB store does all of this; see its [lease item](./dynamodb.md#item-schema). The default `RelayId` is unique per process, which already rules out the reuse case.

Do not release leases from your own hosted service. Ordering it after the relay is easy to get wrong, and a release under a running publisher breaks single-writer ordering.

`OutboxFairShare.Assign` gives the probe order for buckets the relay does not hold yet. It accumulates the `OutboxFairShare.Compute` shares into contiguous ranges, so relays that agree on membership compute disjoint ranges covering every bucket.

Both methods have an overload that takes `heldBucketCounts`: how many unexpired leases name each relay. **Pass it whenever your store can read the lease table**, as both packaged stores do. Shares then follow what the relays hold: the remainder of an uneven split stays with the relays that already have it, and with more relays than buckets a relay that joins owns nothing and waits, whatever its id sorts as. From membership alone, shares follow the id order, and a new pod whose name sorts early takes a bucket from an incumbent that was publishing it, on every scale-out and rolling update. A store that cannot read the leases (one that only ever probes single buckets) uses the overloads without the counts:

```csharp
public sealed class LeaseProbeOrder
{
    public static List<int> Build(
        OutboxLeaseRequest request,
        IReadOnlyList<int> previousBuckets,
        List<string> activeRelayIds)
    {
        var order = new List<int>(request.BucketCount);
        var queued = new bool[request.BucketCount];

        void Queue(IEnumerable<int> buckets)
        {
            foreach (var bucket in buckets)
            {
                if ((uint)bucket < (uint)queued.Length && !queued[bucket])
                {
                    queued[bucket] = true;
                    order.Add(bucket);
                }
            }
        }

        // 1. Leases this relay already holds: the owner-is-self condition matches.
        Queue(previousBuckets);
        // 2. The range that no agreeing peer probes.
        Queue(OutboxFairShare.Assign(request.BucketCount, activeRelayIds, request.RelayId));
        // 3. The remainder, for buckets a departed relay left behind.
        Queue(Enumerable.Range(0, request.BucketCount));
        return order;
    }
}
```

`OutboxFairShare.StandbyRank` tells a relay without a share how far back it waits. Relays at the front are handed the next buckets that free up and should keep acquiring on every call. A relay further back than one standby per bucket only needs to stay counted: the packaged stores answer its acquisitions with an empty list, without touching the database, for as long as the acquisition after it still comes within three quarters of a `LeaseDuration` of its last liveness write. Peers count a relay as active for a whole `LeaseDuration`, so it never drops out of the membership, and because shares follow holdings, a standby that did drop out for a round would change nobody's share.

Claim in that order and stop at the fair share. When the share shrinks below the held count, keep the first buckets and release the rest, as the EF store does. In steady state every relay holds exactly its share from step 1 and makes no failed conditional write, so a failure now signals a real membership disagreement. Relays do not always agree: liveness records become visible with a lag, and a DynamoDB global secondary index cannot be read consistently at all. Ranges can therefore overlap for a cycle, which is why `Assign` orders probes and never replaces the conditional write.

The EF store implements the capability. Its acquisition reads the whole lease table before writing, so it ignores the hint; its release deletes the heartbeat row and then frees every lease of the relay in one guarded statement, which it repeats until a pass frees nothing, so that a claim the stop cancelled cannot outlive it. Its renewals and heartbeats only move their timestamps forward. It keeps deleting the heartbeat row rather than marking it stopped, because a stopped marker needs a new column in a table your migrations own; the row's only late writer is an insert of a relay's very first round, on a provider that breaks the connection instead of confirming a cancelled statement. The DynamoDB store ignores the hint for the same reason: it keeps leases and heartbeats in one partition, reads them with one strongly consistent query, and [plans every relay's claims](./dynamodb.md#how-relays-share-buckets) from that read.

## Wiring a Custom Store

Register your store, then add the relay — the EF Core package is not involved:

```csharp
using Dekaf.Outbox;

builder.Services.AddSingleton<IOutboxStore, CustomOutboxStore>();
builder.Services.AddDekaf(dekaf => dekaf
    .AddOutboxRelay(producer => producer.WithBootstrapServers("localhost:9092")));
```

The relay resolves whatever `IOutboxStore` is registered; `AddEntityFrameworkCoreOutboxStore` and `AddDynamoDbOutboxStore` are convenience registrations for the packaged implementations. A store package of your own can offer the same experience with an extension method on `DekafBuilder` that registers its store through `builder.Services`.
