---
sidebar_position: 1
description: "Inspect, fence, terminate, or abort Kafka transactions safely."
---

# Transaction remediation

Dekaf exposes two distinct force-abort operations:

- `ForceTerminateTransactionAsync` targets one transactional ID through its transaction coordinator. It fences the active producer by requesting a new producer epoch, which terminates any ongoing transaction. This is the operational recovery API for a stuck transactional producer or two-phase-commit participant.
- `AbortTransactionAsync` writes an abort marker for one topic partition. It requires producer and coordinator metadata obtained from `DescribeProducersAsync` and is intended for partition-scoped repair.

```csharp
await using var admin = Kafka.CreateAdminClient()
    .WithBootstrapServers("localhost:9092")
    .Build();

var result = await admin.ForceTerminateTransactionAsync(
    "payments-processor",
    new ForceTerminateTransactionOptions { TimeoutMs = 30_000 });

if (result.ErrorCode != ErrorCode.None)
{
    Console.WriteLine($"Termination failed: {result.ErrorCode}");
    Console.WriteLine($"Retriable: {result.IsRetriable}");
}
```

The timeout is sent to the transaction coordinator for its producer-epoch update. When omitted, it inherits `AdminClientOptions.RequestTimeoutMs`. The cancellation token independently controls the caller's wait and all coordinator/network operations.

Dekaf's built-in and in-memory admin clients expose this operation through `ITransactionRemediationAdminClient`. The `IAdminClient` extension method preserves the same call syntax without adding a binary-breaking member to the base interface. Custom admin-client implementations must implement the capability interface to support force termination.

`ForceTerminateTransactionAsync` resolves a `Transaction` coordinator and negotiates `InitProducerId`; it does not route through a topic leader or the KRaft controller. A broker that does not advertise `InitProducerId` produces `BrokerVersionException`. Controller-only bootstrap endpoints reject this coordinator operation with `UnsupportedEndpointType`.

Successful termination returns the newly allocated producer ID and epoch. A non-retriable broker response is preserved in `ErrorCode`; `IsRetriable` applies Dekaf's Kafka error classification. Retriable coordinator and transport failures use the admin client's configured retry policy and throw a `KafkaException` if retries are exhausted.

Force termination fences the current producer. Any later operation from that producer should be treated as failed, and applications must create or initialize a new producer before continuing.
