---
description: "Separate producer flush checkpoints, delivery outcomes, and Kafka broker connectivity in ASP.NET Core health checks."
---

# Health Checks

Install `Dekaf.Extensions.HealthChecks` to register Kafka checks with ASP.NET Core's health-check services.

```bash
dotnet add package Dekaf.Extensions.HealthChecks
```

## Producer flush checkpoints and broker connectivity

These checks answer different questions:

| Signal | How to observe it | What success establishes |
| --- | --- | --- |
| Producer flush checkpoint | `AddDekafProducerHealthCheck<TKey, TValue>()` | The current `FlushAsync` completed within the configured timeout. |
| Individual delivery outcome | Await `ProduceAsync`, or inspect the delivery callback's error | That delivery succeeded or failed under the configured acknowledgement policy. |
| Recent delivery failures | Aggregate delivery outcomes or [producer error metrics](./observability) over an application-defined time window | Whether the workload meets the application's delivery policy during that window. |
| Broker connectivity | `AddDekafBrokerHealthCheck()` | An active admin request reached the cluster and returned at least one broker. |

The producer check reports **Healthy when its flush checkpoint completes**, even when a queued batch failed delivery. Failed batches leave the producer pipeline too. Concurrent production can leave newer messages queued after that checkpoint; Healthy does not assert that the current queue is empty. An idle producer can also report Healthy while every broker is unavailable, because an empty queue needs no broker request. The result explicitly states that delivery outcomes and broker connectivity are not checked.

Register the producer and broker checks separately when both flush completion and connectivity matter. Their producer and admin clients must already be registered in DI; see [Dependency Injection](./dependency-injection).

```csharp
using Dekaf.Extensions.HealthChecks;

builder.Services.AddHealthChecks()
    .AddDekafProducerHealthCheck<string, string>(
        name: "kafka-producer-queue",
        options: new DekafProducerHealthCheckOptions
        {
            Timeout = TimeSpan.FromSeconds(5)
        })
    .AddDekafBrokerHealthCheck(name: "kafka-broker-connectivity");
```

Broker reachability does not prove that a particular topic accepts writes, that the producer's credentials permit them, or that earlier messages succeeded. Delivery outcomes remain a separate signal. `FireAsync` has no delivery result; use a result-returning or callback-based produce API when application health depends on individual delivery outcomes.

## Failure and recovery

The producer check returns **Unhealthy** if its flush throws or exceeds the timeout. Each invocation evaluates a new flush: the next completed flush returns Healthy. The check retains no delivery-history latch and does not turn past produce failures into a permanent unhealthy state.

If recent delivery failures are part of an application's readiness policy, choose a bounded observation window and an explicit recovery condition, such as failures aging out of that window. The built-in producer check neither installs that policy nor resets delivery counters. Keep its status separate from the application's delivery-failure status.

## Migration note

Earlier descriptions claimed that a successful producer health check proved connectivity or successful delivery. Those claims were incorrect; the underlying check waited for a flush checkpoint. The corrected description states that scope explicitly. Existing Healthy/Unhealthy status behavior and registration signatures remain compatible. Applications that used this check as a connectivity probe should also register the broker check; applications that need delivery assurance must observe delivery results.

## Consumer checks

`AddDekafConsumerHealthCheck<TKey, TValue>()` evaluates consumer liveness and lag using its configured thresholds. A live group member with no assignment can be a healthy standby. See [Consumer Groups](./consumer/consumer-groups) and [Observability](./observability) for the related signals.
