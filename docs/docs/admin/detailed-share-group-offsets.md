---
sidebar_position: 13
---

# Detailed share-group offset mutations

Use `AlterShareGroupOffsetsDetailedAsync` and `DeleteShareGroupOffsetsDetailedAsync` when a
mixed broker response must retain every confirmed outcome. Both return an `AdminMutationResult`
for every requested entity. They implement the optional `IDetailedShareGroupOffsetAdminClient`
capability; extensions expose them through `IAdminClient` without adding members to that interface.
Custom implementations without the capability throw `NotSupportedException`.

Alteration results use `TopicPartition` keys. Alteration requires an empty group and can create
an empty group when none exists. Deletion results use ordinal topic-name keys: Kafka deletes
all share offsets for a requested topic and returns a topic-level result. It does not report
individual partition deletion outcomes. Neither operation is transactional across entities.

```csharp
using Dekaf;
using Dekaf.Admin;
using Dekaf.Protocol;

ShareGroupOffsetAlteration[] requested =
[
    new() { TopicPartition = new("orders", 0), StartOffset = 42 },
    new() { TopicPartition = new("orders", 1), StartOffset = 84 }
];
var outcomes = await admin.AlterShareGroupOffsetsDetailedAsync(
    "workers", requested, new ShareGroupOffsetMutationOptions { TimeoutMs = 10_000 });

// Only confirmed coordinator rejections are safe to select for this retry.
// The built-in client already performs bounded retries for these rejections.
var retry = requested.Where(item =>
    outcomes[item.TopicPartition].Outcome == AdminMutationOutcome.Failed &&
    outcomes[item.TopicPartition].ErrorCode is ErrorCode.NotCoordinator
        or ErrorCode.CoordinatorNotAvailable or ErrorCode.CoordinatorLoadInProgress).ToArray();
if (retry.Length != 0)
    await admin.AlterShareGroupOffsetsDetailedAsync("workers", retry);

// This removes offsets for every partition of orders, not a selected partition.
var deleted = await admin.DeleteShareGroupOffsetsDetailedAsync("workers", ["orders"]);
```

Top-level group errors are copied, with their original code and message, to every entity in
that request. Confirmed successes and terminal errors survive retries of other entities.
Each retry rediscovers the group coordinator. Missing or duplicate response entries are
`Unknown`; unrequested entries are not added to the result dictionary.

A transport failure after entering the send path is `Unknown`, including cancellation or
deadline expiry during the send. It is never automatically replayed. Inspect the group's
state before deciding whether to retry an unknown mutation. Cancellation before invocation
throws; cancellation during execution returns retained outcomes and `NotAttempted` results
for entities that were never sent. `TimeoutMs` bounds discovery, sends and retries together;
zero expires before discovery. Empty input performs no network operation.

Existing convenience methods retain their existing exception and retry behavior. Existing
member-removal, feature-update and Streams-offset result APIs are unchanged.

`Dekaf.Testing` implements the same result shapes, validates inputs before applying mutations,
and retains sibling successes when group/topic/partition fault scopes fail. Active groups
reject these mutations; a missing group can be created by alteration, while deletion reports
`GroupIdNotFound`. Deletion keeps the group after removing its final offset. The simulator
knows whether cancellation precedes its synchronous state change and reports `NotAttempted`
in that case. Real ambiguous network sends are covered by protocol-response unit tests.

Protocol sources: [alteration response](https://github.com/apache/kafka/blob/trunk/clients/src/main/resources/common/message/AlterShareGroupOffsetsResponse.json),
[deletion response](https://github.com/apache/kafka/blob/trunk/clients/src/main/resources/common/message/DeleteShareGroupOffsetsResponse.json).
