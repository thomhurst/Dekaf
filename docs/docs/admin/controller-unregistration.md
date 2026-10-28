---
sidebar_position: 14
description: "Remove a stale KRaft controller registration with UnregisterController (KIP-1312)."
---

# Controller unregistration

Kafka 4.4 adds `UnregisterController` ([KIP-1312](https://cwiki.apache.org/confluence/spaces/KAFKA/pages/406623954/KIP-1312+Support+unregistering+controllers)) so operators can remove a stale controller registration, for example one left by a decommissioned controller that blocks a feature upgrade.

```csharp
using Dekaf.Admin;

await using var admin = Kafka.CreateAdminClient()
    .WithBootstrapControllers("controller-1:9093")
    .Build();

await admin.UnregisterControllerAsync(
    controllerId: 3,
    new UnregisterControllerOptions { TimeoutMs = 10_000 });
```

## Registrations and voters are separate

A controller registration and a Raft voter are distinct. `UnregisterControllerAsync` removes only the registration from cluster metadata; it never changes the voter set. To remove a voter, call `RemoveRaftVoterAsync` separately. Dekaf does not combine the two operations.

A controller that is still running registers itself again when it sees its registration removed, so unregister only controllers that are shut down for good. Kafka 4.4 accepts the request for any registered controller ID, including a current voter. KIP-1312's later amendment, which rejects unregistering a current voter, targets Kafka 4.5.

## Requirements and errors

- The destination must advertise API key 94 (Kafka 4.4+). Older brokers and controllers fail with `BrokerVersionException` before any request is sent.
- The finalized `metadata.version` must be `4.4-IV2` or later. Otherwise the controller answers `UNSUPPORTED_VERSION` (`KafkaException` with `ErrorCode.UnsupportedVersion`).
- An ID without a registration fails with `ErrorCode.ControllerIdNotRegistered`.
- The caller needs `ALTER` on the cluster resource.

The request goes to the active controller with either `WithBootstrapServers` (brokers forward it) or `WithBootstrapControllers`. When the active controller changes, Dekaf rediscovers it and retries. If a response is lost after the request was sent, the retry may answer `ControllerIdNotRegistered` because the first attempt applied; Dekaf treats that answer as success. A first attempt that answers `ControllerIdNotRegistered` still fails.

`TimeoutMs` (default 30 seconds) bounds controller discovery, retries and the request. Zero times out immediately, negative values are invalid, and expiry throws `KafkaTimeoutException`. Cancelling the token stops the wait; a request that was already sent may still apply.

The built-in admin client exposes this operation through the optional `IControllerRegistrationAdminClient` capability and an `IAdminClient` extension method, so existing custom `IAdminClient` implementations remain compatible. Implementations without the capability throw `NotSupportedException`. `InMemoryAdminClient` implements the capability and validates arguments, cancellation and configured admin faults.
