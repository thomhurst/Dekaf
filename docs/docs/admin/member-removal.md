---
title: Consumer-group member removal
description: Evict explicit static or dynamic members, or a snapshot of all current members.
---

Consumer-group eviction removes broker membership. It does not stop the application, remotely pause consumption, or prevent later joins. Stop or reconfigure the application separately when it must remain out of the group.

```csharp
using Dekaf.Admin;

await admin.RemoveMembersFromConsumerGroupAsync("workers", new ConsumerGroupMemberRemovalOptions
{
    Members =
    [
        new ConsumerGroupMemberIdentity { GroupInstanceId = "worker-a" },
        new ConsumerGroupMemberIdentity { MemberId = "dynamic-member-id" }
    ],
    Reason = "deployment maintenance",
    TimeoutMs = 30000
}, cancellationToken);
```

Each identity must contain exactly one nonblank `GroupInstanceId` or `MemberId`. Duplicate identities are rejected. The same string in the two identity domains refers to different selectors. The original overload accepting `ConsumerGroupMemberToRemove` remains available for static instance IDs. An empty list on that overload is still invalid.

To evict a snapshot of the current membership, select the explicit remove-all mode:

```csharp
var result = await admin.RemoveMembersFromConsumerGroupAsync("workers",
    new ConsumerGroupMemberRemovalOptions { RemoveAll = true }, cancellationToken);

foreach (var member in result.Members)
{
    Console.WriteLine($"{member.GroupInstanceId} / {member.MemberId}: {member.ErrorCode}");
}
```

Do not combine `RemoveAll` with `Members`. Discovery occurs once before removal, using consumer-group description with its classic fallback. Coordinator retries reuse the discovered identities. Concurrent joins are outside the snapshot; a restarted static member with the same instance ID remains addressable by that instance ID. A discovered empty group returns an empty successful result without sending a removal request. A missing group or failed discovery reports the broker error.

Results preserve requested identity order and include one outcome per target. `GroupInstanceId` is empty for dynamic targets; `MemberId` identifies the dynamic member. Static administrative removal may return an empty member ID because the instance ID is the selector. Partial member errors do not erase successful outcomes. A missing broker outcome is reported as `UnknownServerError`, never success. Duplicate broker outcomes reject the malformed response.

The deadline covers initialization, discovery, coordinator resolution and removal retries. Caller cancellation remains `OperationCanceledException`; expiration reports `KafkaTimeoutException`. Cancellation after a request is sent cannot undo an eviction already accepted by the broker.

`TimeoutMs = 0` expires before discovery or eviction begins, including in `Dekaf.Testing`. A lost removal response leaves the outcome unknown: the new identity/options overload throws a non-retriable `KafkaException` with the original transport failure as its inner exception. It does not resend an ambiguous removal, report missing members as confirmed success, or risk evicting a replacement static member. Inspect current membership before deciding whether to retry. Explicit coordinator-error responses still permit retries using the original snapshot. The original static-only overload retains its existing retry behavior.

`LeaveGroup` v3 or later is required; reasons are sent with v5. Kafka supports this operation for classic consumer groups and KIP-848 consumer groups. Remove-all rejects non-consumer protocols. Targeted removal preserves unsupported-group/version errors returned by the broker. The capability is additive: custom `IAdminClient` implementations can implement `IConsumerGroupMemberRemovalAdminClient`; otherwise the extension reports `NotSupportedException`.

`Dekaf.Testing` supports the same selectors, validation, partial outcomes and remove-all behavior. Set `InMemoryConsumerOptions.MemberId` for deterministic dynamic identities or `GroupInstanceId` for static identities. Eviction changes fake membership and assignments; future subscriptions can rejoin. The fake does not emulate broker heartbeat timing or every classic protocol.

The snapshot behavior follows [Kafka's remove-members admin operation](https://kafka.apache.org/43/javadoc/org/apache/kafka/clients/admin/RemoveMembersFromConsumerGroupOptions.html). Explicit dynamic-member selection is also exposed directly by Dekaf.
