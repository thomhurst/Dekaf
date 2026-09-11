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
Invalid arguments and programming/invariant failures during input processing, request construction,
or response mapping still throw at the operation level. Connection disposal before dispatch is
`NotAttempted`. An `InvalidOperationException` from controller leasing also leaves unsent entities
`NotAttempted`: metadata can identify a controller before the connection pool registers its broker
ID. A previously confirmed success or rejection remains intact if leasing prevents a retry.
During dispatch, disposal and `InvalidOperationException` are conservatively `Unknown`: the
transport uses that exception type for connection-readiness failures as well as other faults,
and a thrown exception does not provide a definitive broker response. The original exception
remains available for diagnosis; these mutations are not automatically replayed.
Malformed protocol responses during dispatch also produce `Unknown` outcomes for the dispatched
entities and retain any confirmed sibling results from earlier attempts.

Success means broker acceptance. It does not wait for leader election, metadata propagation,
replica movement, or physical topic removal. Use metadata or reassignment inspection when those
later events matter. Existing convenience methods retain their existing exceptions, retries
and completion behavior.

`DeleteTopicsDetailedAsync` returns the per-topic broker outcomes without refreshing this
client's metadata cache. An immediate `ListTopicsAsync` or `DescribeTopicsAsync` on the same
client can therefore still report a deleted topic until metadata refreshes. The existing
`DeleteTopicsAsync` convenience methods explicitly refresh that cache after deletion. The
detailed methods omit that extra request so a refresh failure or cancellation cannot replace
the mutation results, including partial successes, with an operation-level exception.

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
| Share-group offset alteration | `AlterShareGroupOffsetsDetailedAsync` | `TopicPartition` |
| Share-group offset deletion | `DeleteShareGroupOffsetsDetailedAsync` | Topic name (all partition offsets) |
| Configuration replacement/incremental changes | Pending [#3133](https://github.com/thomhurst/Dekaf/issues/3133) | Resource |
| Client quota alteration | `AlterClientQuotasDetailedAsync` | Complete `ClientQuotaEntity` |
| ACL creation / SCRAM alteration | `CreateAclsDetailedAsync` / `AlterUserScramCredentialsDetailedAsync` | Input binding occurrence / user |
| Member removal, feature updates, Streams offsets and replica log directories | Existing detailed result APIs retained | Existing keys |

## Client quota outcomes

`AlterClientQuotasDetailedAsync` is available through `IDetailedClientQuotaMutationAdminClient`
and an `IAdminClient` extension. Custom clients without this capability throw
`NotSupportedException`; their existing `AlterClientQuotasAsync` implementation remains compatible.
The convenience method retains its existing exception and retry behavior.

Result keys preserve the complete entity: a user alone differs from that user plus a default
client ID. Component order does not affect equality. A null name identifies a default component;
an empty string is a distinct name. Inputs are copied before asynchronous work, and returned
entity components are read-only. Duplicate entities, component types, and operation keys are
rejected before dispatch. Empty input performs no network activity.

```csharp
var entity = ClientQuotaEntity.For(
    ClientQuotaEntityComponent.User("alice"),
    ClientQuotaEntityComponent.ClientId(null));
var alterations = new[]
{
    ClientQuotaAlteration.Set(entity, "consumer_byte_rate", 4096)
};
var results = await admin.AlterClientQuotasDetailedAsync(alterations);
var retry = alterations.Where(item =>
    results[item.Entity].Outcome == AdminMutationOutcome.Failed &&
    results[item.Entity].ErrorCode is ErrorCode.NotController or ErrorCode.ThrottlingQuotaExceeded)
    .ToArray();
if (retry.Length > 0)
    await admin.AlterClientQuotasDetailedAsync(retry);
```

The client already retries explicit controller/quota rejections within `TimeoutMs`; a targeted
retry can use a fresh deadline after addressing the rejection. Inspect other errors individually.
Do not replay successful siblings. For `Unknown`, inspect current quotas with
`DescribeClientQuotasAsync` and account for concurrent administrators before choosing another
mutation. Transport errors, missing or duplicate response entries, and ambiguous broker timeouts
are never automatically replayed. `ValidateOnly = true` returns validation outcomes without
changing quotas. Broker and direct-controller bootstrap use the same routing as the convenience API.

The simulator supports quota set/remove operations, validation-only execution, snapshots, deadlines,
and per-entity fault outcomes. As with its convenience API, it does not emulate every broker quota
configuration rule; inject an admin fault to model a broker rejection. Faults are consumed per quota
entity, and confirmed siblings remain in the returned results.

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

The simulator applies each mutation directly and does not parse broker responses. Omitted or
duplicate response entries are therefore covered by the real client's protocol-response fixtures,
not by the simulator's per-entity mutation tests.

## ACL and SCRAM mutations

`IDetailedSecurityMutationAdminClient` supplies both detailed security methods through
`IAdminClient` extensions. ACL creation returns an ordered list of `AclCreationOutcome`.
Each entry contains its original immutable `Binding` and an `AdminMutationResult` in `Result`.
Duplicate bindings, including repeated references to the same object, retain separate input
positions. Kafka identifies ACL results only by request position; if the response count differs
from the sent count, every outcome in that response is `Unknown` because identity cannot be
confirmed. Successful occurrences from earlier responses survive a retry of rejected occurrences.

```csharp
using Dekaf.Admin;
using Dekaf.Protocol;

await using var admin = new AdminClientBuilder().WithBootstrapServers("localhost:9092").Build();
var outcomes = await admin.CreateAclsDetailedAsync([
    AclBinding.Allow(ResourcePattern.Topic("orders"), "User:alice", AclOperation.Read),
    AclBinding.Allow(ResourcePattern.Topic("payments"), "User:bob", AclOperation.Read)]);
var rejected = outcomes.Where(item => item.Result.Outcome == AdminMutationOutcome.Failed &&
    item.Result.ErrorCode is ErrorCode.NotController or ErrorCode.ThrottlingQuotaExceeded);
await admin.CreateAclsDetailedAsync(rejected.Select(item => item.Binding));
```

SCRAM alteration returns a dictionary keyed by user. Different mechanisms for one user remain
one atomic unit, including when retrying confirmed controller/quota rejections. A duplicate
user/mechanism pair (including deletion plus upsertion of the same mechanism) fails input
validation before any user is sent. Salt arrays are copied; password derivation occurs once per
upsertion before asynchronous dispatch, and retries reuse the derived credential. The total
network deadline starts after this local input preparation. Result objects contain user keys and
outcomes, never the submitted password, salt or salted password. Broker error messages and local
exceptions retain their original contents; the client does not add credential material to them.

```csharp
using Dekaf.Admin;
using Dekaf.Protocol;

await using var admin = new AdminClientBuilder().WithBootstrapServers("localhost:9092").Build();
UserScramCredentialAlteration[] requested = [
    new UserScramCredentialDeletion { User = "former-user", Mechanism = ScramMechanism.ScramSha256 },
    new UserScramCredentialDeletion { User = "former-user", Mechanism = ScramMechanism.ScramSha512 }];
var outcomes = await admin.AlterUserScramCredentialsDetailedAsync(requested);
var retryUsers = outcomes.Where(item => item.Value.Outcome == AdminMutationOutcome.Failed &&
    item.Value.ErrorCode is ErrorCode.NotController or ErrorCode.ThrottlingQuotaExceeded)
    .Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
await admin.AlterUserScramCredentialsDetailedAsync(requested.Where(item => retryUsers.Contains(item.User)));
```

The in-memory implementation follows the existing ACL/SCRAM simulator limits: it does not store
ACLs or credentials, enforce authorization, or authenticate SCRAM passwords. ACL faults use the
resource's topic/group scope; SCRAM faults apply once per user using the generic admin scope.
It models validation, grouped outcomes, selective retries, cancellation and deadlines. Use Kafka
integration tests for persisted ACL behavior and credential atomicity.
