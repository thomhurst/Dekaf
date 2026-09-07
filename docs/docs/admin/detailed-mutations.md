---
sidebar_position: 12
---

# Detailed mutation outcomes

Batch topic mutations can succeed for some entities and fail for others. The optional
`IDetailedTopicMutationAdminClient` capability returns an `AdminMutationResult` for every
requested entity. Both the built-in admin client and `Dekaf.Testing` support it. The extension
methods work through `IAdminClient`; custom implementations without this capability throw
`NotSupportedException`.

| Outcome | Meaning | Next step |
|---|---|---|
| `Succeeded` | The broker accepted the mutation, or accepted a validate-only request. | Do not resend it. |
| `Failed` | A definitive broker error was returned. | Inspect the original `ErrorCode` and `ErrorMessage`. |
| `Unknown` | The send may have applied, or its response was missing, duplicated or ambiguous. | Inspect cluster state before deciding whether another mutation is safe. |
| `NotAttempted` | No mutation request was sent for this entity. | Inspect the local `Exception` before retrying. |

`ErrorCode` is null when there is no broker response. A transport exception's Kafka error code
does not count as a broker response. Broker timeout, network, unknown-server and leader-unavailable
errors retain their codes but have `Unknown` outcomes because the mutation may have applied.
Do not treat a missing response as success or interpret a subsequent "already exists" response
as proof that this call created the topic.

```csharp
using Dekaf.Admin;
using Dekaf.Protocol;

await using var admin = new AdminClientBuilder()
    .WithBootstrapServers("localhost:9092")
    .Build();

NewTopic[] requested = [new() { Name = "orders" }, new() { Name = "payments" }];
var results = await admin.CreateTopicsDetailedAsync(requested);

// These explicit rejections can be retried without replaying successful siblings.
var rejected = requested.Where(topic =>
    results[topic.Name].Outcome == AdminMutationOutcome.Failed &&
    results[topic.Name].ErrorCode is ErrorCode.NotController or ErrorCode.ThrottlingQuotaExceeded);

var retryResults = await admin.CreateTopicsDetailedAsync(rejected);
```

The client already retries confirmed `NotController` and `ThrottlingQuotaExceeded` responses
within its retry budget. Each retry contains only rejected entities. Other broker errors remain
available to the caller. Ambiguous transport failures are never replayed automatically. This
avoids applying a non-idempotent mutation twice. An error's `IsRetriable()` classification alone
does not establish whether replaying an ambiguous mutation is safe.

## Cancellation, deadlines and completion

Cancellation already requested at invocation throws `OperationCanceledException` before sending.
During execution, cancellation returns the results known so far: completed responses remain
intact, unconfirmed sends become `Unknown`, and entities never sent become `NotAttempted`.
Cancellation during retry backoff preserves the last confirmed rejection. `TimeoutMs` bounds
discovery, sends and retries together; local deadline failures carry `KafkaTimeoutException`.
Invalid arguments and programming/invariant failures still throw at the operation level.

Success means broker acceptance. It does not wait for leader election, metadata propagation,
replica movement, or physical topic removal. Use metadata or reassignment inspection when those
later events matter. Existing convenience methods retain their existing exceptions, retries
and completion behavior.

Inputs are validated and copied before the first await. Duplicate topic names or IDs are rejected.
Empty batches perform no discovery or network request. Typed partition expansion preserves replica
order and `ValidateOnly`. Topic-ID deletion requires DeleteTopics v6 and correlates responses by
UUID without guessing from response order. Disabling replication-factor changes requires
AlterPartitionReassignments v1; unsupported brokers return `NotAttempted` with a version exception.

## Coverage matrix

This is the maintained scope matrix for [#3126](https://github.com/thomhurst/Dekaf/issues/3126).
Outstanding families remain separate work; the parent is not complete until those rows are resolved.

| Family | Detailed alternative / status | Result key |
|---|---|---|
| Topic creation | `CreateTopicsDetailedAsync` | Topic name |
| Topic deletion, including topic IDs | `DeleteTopicsDetailedAsync` | Topic name or UUID |
| Partition expansion, including explicit replicas and validation | `CreatePartitionsDetailedAsync` | Topic name |
| Partition reassignment/cancellation | `AlterPartitionReassignmentsDetailedAsync` | `TopicPartition` |
| Consumer-group deletion and offset alteration/deletion | Pending [#3131](https://github.com/thomhurst/Dekaf/issues/3131) | Group / partition |
| Share-group offset alteration/deletion | Pending [#3132](https://github.com/thomhurst/Dekaf/issues/3132) | Partition |
| Configuration replacement/incremental changes | Pending [#3133](https://github.com/thomhurst/Dekaf/issues/3133) | Resource |
| Client quota alteration | Pending [#3134](https://github.com/thomhurst/Dekaf/issues/3134) | Quota entity |
| ACL creation / SCRAM alteration | Pending [#3135](https://github.com/thomhurst/Dekaf/issues/3135) | Binding / user |
| Member removal, feature updates, Streams offsets and replica log directories | Existing detailed result APIs retained | Existing keys |

## In-memory behavior

`InMemoryAdminClient` retains successes alongside per-topic or per-partition faults. Fault-plan
barriers allow deterministic cancellation; when cancellation happens before its synchronous
mutation, the simulator can report `NotAttempted` precisely. It copies input collections before
such barriers and enforces a total deadline. Empty batches do not consume fault-plan entries.

The in-memory cluster models one broker, ID `0`, with replication factor one. Valid reassignment
to that broker completes immediately; there is no asynchronous replica movement. Canceling an
absent reassignment returns `NoReassignmentInProgress`. Unsupported replica targets return
`InvalidReplicaAssignment`. Creation, deletion, UUID matching and partition expansion inspect
actual stored state; validate-only requests leave that state unchanged.
