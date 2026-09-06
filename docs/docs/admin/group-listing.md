---
sidebar_position: 3
description: "List Kafka groups by group type, protocol, and state."
---

# Group inventory

`ListGroupsAsync` lists groups across brokers and retains the broker's `GroupType`,
`ProtocolType`, and `State`. Import `Dekaf.Admin` to call the extension on an
`IAdminClient`:

```csharp
using Dekaf;
using Dekaf.Admin;

await using IAdminClient admin = Kafka.CreateAdminClient()
    .WithBootstrapServers("localhost:9092")
    .Build();

var inventory = await admin.ListGroupsAsync();
var connect = await admin.ListGroupsAsync(new ListGroupsOptions
{
    Types = ["classic"],
    ProtocolTypes = ["connect"]
});
var emptyConsumers = await admin.ListConsumerGroupsAsync(
    new ListConsumerGroupsOptions { States = ["Empty"] });
```

Group type identifies the coordinator's group model. Protocol identifies the
application using that model. They are separate fields:

| Group type | Protocol | Meaning |
| --- | --- | --- |
| `classic` | `consumer` | Classic consumer group |
| `classic` | empty string | Simple consumer group with committed offsets |
| `consumer` | `consumer` | Modern consumer group |
| `classic` | `connect` | Kafka Connect group |
| `share` | `share` | Share group |
| `streams` | `streams` | Streams group |

The inventory preserves unknown future types and protocol strings unchanged.
It does not coerce them into a consumer group or a fixed enum. Group IDs are
case-sensitive and occur at most once in a result. Ordering is unspecified.
Listing brokers concurrently is not an atomic cluster snapshot: a coordinator
move can produce duplicate or stale entries. Filtering precedes deduplication
so a nonmatching stale entry cannot hide a matching entry from another broker.

## Filters and compatibility

`States`, `Types`, and `ProtocolTypes` combine with AND. Values within each list
combine with OR. A null or empty list imposes no restriction. State and type
comparisons ignore case; protocol comparisons are ordinal and case-sensitive.
`ProtocolTypes = [""]` selects simple groups. Empty state/type values and null
elements are rejected.

Each destination connection negotiates `ListGroups` independently. A nonempty
state filter requires version 4; a nonempty type filter requires version 5.
An unsupported requested filter throws `KafkaException` with
`ErrorCode.UnsupportedVersion`, including the broker and negotiated version.
Protocol filtering runs on returned results. Without a type filter, older
responses remain usable: `GroupType` is null before version 5 and `State` is
null before version 4. An unavailable field is never guessed from the protocol.
A broker error fails the operation rather than returning a partial inventory.

The existing convenience methods use the same filtering and aggregation rules:

- `ListConsumerGroupsAsync` selects `classic` or `consumer` groups with protocol
  `consumer` or an empty string. **This corrects earlier behavior that also
  returned Connect, Share, Streams, and other non-consumer groups.**
- `ListShareGroupsAsync` selects type `share`.
- `ListStreamsGroupsAsync` selects type `streams`.

These convenience methods require version 5 because their group-type selection
must be reliable. Use an unfiltered `ListGroupsAsync` inventory to inspect an
older response. Filters are also applied locally, protecting callers from
nonmatching rows in a broker response.

`IGroupListingAdminClient` is an optional capability implemented by Dekaf's
`AdminClient` and `InMemoryAdminClient`. Existing third-party `IAdminClient`
implementations need no new member. Calling the extension on an implementation
without this capability throws `NotSupportedException`.

## In-memory testing

`InMemoryAdminClient` distinguishes consumer membership history, simple
offset-only groups, Share groups, and Streams groups created by a successful
`AlterStreamsGroupOffsetsAsync`. Removing the last Streams offset retains the
empty group's type until group deletion. Listing no longer presents every
in-memory consumer group as a Streams group. The fake models active groups as
`Stable` and inactive groups as `Empty`; it does not simulate broker rebalance
states or custom Classic protocols.

See [KIP-1043](https://cwiki.apache.org/confluence/spaces/KAFKA/pages/305171038/KIP-1043%2BAdministration%2Bof%2Bgroups)
for the distinction between group type and protocol.
