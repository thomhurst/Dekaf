---
sidebar_position: 12
description: "Dekaf's built-in ActivitySource and Meter for OpenTelemetry tracing and metrics, plus broker-side telemetry (KIP-714). Zero cost when nothing is listening."
---

# Observability (OpenTelemetry)

Dekaf is instrumented with the standard .NET diagnostics primitives — a `System.Diagnostics.ActivitySource` for tracing and a `System.Diagnostics.Metrics.Meter` for metrics, both named `"Dekaf"`. Instrumentation is zero-cost when nothing is listening: spans are guarded by `HasListeners()`, counters are ~3ns no-ops without a listener, and all internal state gauges are pull-based observable instruments that never touch the produce/consume hot paths.

The `Dekaf.OpenTelemetry` package provides one-line registration extensions for the OpenTelemetry SDK.

## Installation

```bash
dotnet add package Dekaf.OpenTelemetry
```

## Quick Start

```csharp
using Dekaf.OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddDekafInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddDekafInstrumentation()
        .AddOtlpExporter());
```

The package is a thin convenience layer. If you prefer not to reference it, register the source names directly — they are exposed as constants on `Dekaf.Diagnostics.DekafDiagnostics`:

```csharp
using Dekaf.Diagnostics;

tracing.AddSource(DekafDiagnostics.ActivitySourceName); // "Dekaf"
metrics.AddMeter(DekafDiagnostics.MeterName);           // "Dekaf"
```

## Tracing

Dekaf emits spans following the [OpenTelemetry messaging semantic conventions](https://opentelemetry.io/docs/specs/semconv/messaging/kafka/). Span names use the spec's `{operation name} {destination}` format:

| Span | Kind | When |
|------|------|------|
| `send {topic}` | `Producer` | Each `ProduceAsync` / `FireAsync` |
| `process {topic}` | `Consumer` | Each message from streaming `ConsumeAsync` |
| `poll {topic}` | `Client` | Each message from `ConsumeOne` / `ConsumeOneAsync` |

The two consume flavors match the span's actual lifetime. In the streaming `ConsumeAsync` path the span stays open while your handler runs and is ended when the next record is requested — a `process` operation (`CONSUMER` kind) whose duration covers message handling. Note the span is not `Activity.Current` inside your loop body, so spans your handler creates are **not** automatically parented under it; to correlate handler work with the message, create your own span and use the producer's trace context from the message `traceparent` header, or rely on duration overlap within the trace. In the single-record `ConsumeOne` paths the span ends before the record is returned, covering only delivery and deserialization — a `receive` operation (`CLIENT` kind).

### Trace Context Propagation

Producer spans inject W3C `traceparent` (and `tracestate`) headers into the outgoing message. On the consumer side, the extracted producer context is attached as a **span link** rather than a parent — consumer spans start a new trace linked to the producing trace, per the OTel messaging conventions. Messages without a valid `traceparent` header produce an unlinked consumer span.

Malformed trace context is ignored without interrupting record delivery. Extraction
requires lowercase hexadecimal, nonzero trace/span IDs and a valid version/length;
unknown future versions may append opaque fields after the standard prefix, as
specified by [W3C Trace Context](https://www.w3.org/TR/trace-context/#versioning-of-traceparent).

### Span Attributes

Both spans set `messaging.system = kafka` plus:

| Attribute | Send | Process / Poll |
|-----------|------|----------------|
| `messaging.destination.name` (topic) | ✓ | ✓ |
| `messaging.operation.name` | `send` | `process` / `poll` |
| `messaging.operation.type` | `send` | `process` / `receive` |
| `messaging.client.id` | ✓ | ✓ |
| `messaging.kafka.message.key` | ✓ (string-convertible keys) | |
| `messaging.destination.partition.id` | ✓ (on delivery) | ✓ |
| `messaging.kafka.offset` | ✓ (on delivery) | ✓ |
| `messaging.message.body.size` | | ✓ |
| `messaging.kafka.message.tombstone` | ✓ (null-value messages) | ✓ (tombstone records) |
| `messaging.consumer.group.name` | | ✓ |

`messaging.message.body.size` is set on consume spans only, and is the value payload only — the key is not part of the message body; tombstones report `0`.

Failures set the span status to `Error`, set `error.type` to the exception's fully-qualified type name, and record an `exception` event with `exception.type`, `exception.message`, and `exception.stacktrace`. Successful spans leave the status unset, per the OTel span-status guidance.

## Metrics

### Standard Messaging Metrics

These are the spec-defined instruments from the [OTel messaging metrics conventions](https://opentelemetry.io/docs/specs/semconv/messaging/messaging-metrics/):

| Instrument | Type | Unit | Description |
|------------|------|------|-------------|
| `messaging.client.sent.messages` | Counter | `{message}` | Messages published (counted per delivered batch; includes fire-and-forget) |
| `messaging.client.operation.duration` | Histogram | `s` | Produce operation duration (successes and failures) |
| `messaging.client.consumed.messages` | Counter | `{message}` | Messages received |

All three carry the spec-required `messaging.system = kafka` and `messaging.operation.name` (`send`/`poll`) tags plus `messaging.destination.name`. Failed produce operations record `messaging.client.operation.duration` with an additional `error.type` tag, per the spec's error model.

### Dekaf Internal Metrics

Dekaf-specific instruments live under the `dekaf.*` prefix — the `messaging.*` namespace is reserved for spec-defined instruments. These cover throughput detail beyond the spec metrics plus internal controller state, useful for diagnosing backpressure, buffer exhaustion, and adaptive-connection behavior. Per-broker instruments carry a `dekaf.broker.id` tag.

**Producer:**

| Instrument | Description |
|------------|-------------|
| `dekaf.producer.sent.bytes` | Encoded record-batch bytes published (wire size after compression; counted per delivered batch, includes fire-and-forget) |
| `dekaf.producer.send.errors` | Produce errors (includes fire-and-forget delivery failures) |
| `dekaf.producer.send.retries` | Produce retries |
| `dekaf.producer.buffer.used_bytes` / `limit_bytes` | `BufferMemory` reservation vs configured limit |
| `dekaf.producer.buffer.pressure_events` | Times `ProduceAsync` entered the buffer-full slow path |
| `dekaf.producer.broker.budget_bytes` / `unacked_bytes` | Per-broker unacked-byte admission budget and standing charge |
| `dekaf.producer.broker.min_rtt` / `max_delivery_rate` | BBR-style estimator inputs driving the budget |
| `dekaf.producer.broker.queue_latency_ewma` | Seal-to-send queue latency EWMA |
| `dekaf.producer.broker.latency_budget_scale` | Latency-governor derating factor (1.0 = no derating) |
| `dekaf.producer.broker.admission_blocks` | Sends blocked on the broker budget |
| `dekaf.producer.broker.capacity_probe.successes` / `failures` | Capacity probe outcomes |
| `dekaf.producer.broker.connections` | Current adaptive connection width per broker |
| `dekaf.producer.broker.in_flight_bytes` / `in_flight_requests` | Written-but-unacknowledged bytes/requests |
| `dekaf.producer.batch.splits` | Oversized batches split for retry (KIP-126) |

**Consumer:**

| Instrument | Description |
|------------|-------------|
| `dekaf.consumer.consumed.bytes` | Bytes received (key + value) |
| `dekaf.consumer.lag` | High watermark minus consumed position, per partition (ObservableGauge) |
| `dekaf.consumer.rebalance.duration` | Consumer group rebalance duration (Histogram, `s`) |
| `dekaf.consumer.fetch.duration` | Fetch request round-trip time per broker (Histogram, `s`) |
| `dekaf.consumer.batch.parse.errors` | Record batches that failed protocol parsing |
| `dekaf.consumer.fetch_buffer.used_bytes` / `free_bytes` | Fetch response memory reserved vs available |
| `dekaf.consumer.fetch_buffer.depleted_percent` / `depleted_duration` | Time spent waiting for fetch response memory |

All observable gauges are registered per client instance and stop reporting when the client is disposed.

## Broker-Side Telemetry (KIP-714)

Independently of OpenTelemetry, Dekaf implements [KIP-714 client metrics push telemetry](https://cwiki.apache.org/confluence/display/KAFKA/KIP-714%3A+Client+metrics+and+observability). When a broker has a client-metrics subscription configured, Dekaf clients automatically push standard client metrics to the broker at the subscribed interval — no client configuration required.

Applications can also contribute their own metrics to broker subscriptions via `ProducerOptions.ApplicationMetrics` / `ConsumerOptions.ApplicationMetrics` with `ApplicationTelemetryMetric` (name, kind, and an observe callback).

### Standard producer and consumer metrics

Ordinary producers and consumers export the following metrics when their names match
a broker subscription prefix. All names below start with `org.apache.kafka.`.
Share consumer metrics use their separate `consumer.share.` namespace.

Children created through `Kafka.Connect` export these metrics for their logical
producer or consumer. Connection totals and rates describe the shared physical
pool for that role; do not sum them across children sharing that pool. Each child
keeps its own collection cursor. Request timing and throttle samples belong to the
child issuing the request. Fetch and commit timing also cover bootstrap connections
whose broker ID is unknown; node latency metrics require a known broker ID.
Logical request attribution includes transaction coordination, group heartbeats,
coordinator discovery, and offset queries. Shared root metadata refreshes have no
individual child owner. Write-observed transaction requests retain their callback
and collector in pooled state until completion; a pipelined response keeps its own
collector after the write completes.

| Metric suffix | OTLP kind and unit | Measurement |
| --- | --- | --- |
| `producer.connection.creation.total`, `consumer.connection.creation.total` | Monotonic Sum, connections | Successful connection creations |
| `producer.connection.creation.rate`, `consumer.connection.creation.rate` | Gauge, `1/s` | Connection creations divided by elapsed time since the previous collection |
| `producer.node.request.latency.avg`, `.max`; `consumer.node.request.latency.avg`, `.max` | Gauge, milliseconds | Request dispatch through parsed response, with a `node_id` data-point attribute |
| `producer.produce.throttle.time.avg`, `.max`; `consumer.fetch.manager.fetch.throttle.time.avg`, `.max` | Gauge, milliseconds | Broker-reported throttle duration |
| `producer.record.queue.time.avg`, `.max` | Gauge, `ms` | Batch creation through removal from the send buffer into a produce request |
| `consumer.coordinator.commit.latency.avg`, `.max` | Gauge, `ms` | OffsetCommit request dispatch through parsed response |
| `consumer.coordinator.assigned.partitions` | Gauge, `1` | Current local assignment, including manual assignment; zero after clearing it |
| `consumer.coordinator.rebalance.latency.avg`, `.max` | Gauge, `ms` | Successful group join/rejoin, or changed heartbeat assignment, through rebalance listener completion |
| `consumer.coordinator.rebalance.latency.total` | Monotonic Sum, `ms` | Total duration of observed successful rebalances |
| `consumer.fetch.manager.fetch.latency.avg`, `.max` | Gauge, `ms` | Fetch request dispatch through parsed response, including broker long-poll time |
| `consumer.poll.idle.ratio.avg` | Gauge, `1` | Fraction of elapsed collection time spent in foreground consumer waits |

Queue, commit, fetch and rebalance average/maximum gauges aggregate samples while
their metric group remains subscribed. They have no value before the first sample;
collection does not consume their samples. Unsubscribing and later resubscribing
starts a fresh gauge window. Operations that started before that window are omitted.
Queue samples are weighted equally per batch, including retry attempts; they are
not weighted by record count. A retry samples the batch's age at its next send attempt.
Request construction must succeed before its queue samples are published; an aborted
build contributes no samples. Samples accumulate per batch and merge once per request.
Request timing starts after connection admission and before serialization. It includes
serialization, waiting for the connection's write lock, socket writing, broker time,
and response parsing. Local send contention therefore contributes to request latency.
Connection acquisition, broker-throttle waits, reauthentication admission, and pending
request-slot waits happen before this measurement starts. This dispatch boundary applies
to both ordinary and pipelined requests. The [Apache Kafka reference implementation](https://github.com/apache/kafka/blob/4.3.0/clients/src/main/java/org/apache/kafka/clients/ClientResponse.java#L104)
also includes pre-send request time in the latency used by fetch and commit metrics.
Measurements include parsed error responses but omit transport failures and canceled
response waits. Rebalance measurements omit failed or canceled rebalances.

Rates always use elapsed collection windows, regardless of the broker's requested
Sum temporality. Rebalance totals retain the observed cumulative total and use an
independent cursor for delta pushes. Newly subscribed rates and delta totals exclude
activity from before that subscription. The original node-latency and throttle
gauges retain their existing delta/cumulative collection behavior.

Dekaf's asynchronous poll ratio counts foreground waits for assignment, direct
fetches, prefetched data, and retry/no-assignment delays. Nested waits count once;
an in-flight wait contributes to each collection window it overlaps. Time outside
these waits, including application processing, counts toward the denominator only.
Background prefetch, heartbeat and auto-commit work do not count as foreground idle
time. This is an elapsed-time-weighted ratio, not an unweighted average of individual
`poll()` calls. It works across the single-record, stream and batch consumption APIs
without a clock read per returned record. No ratio is emitted until a subscribed
foreground wait is observed; a wait begun while unsubscribed is omitted.

Recording costs are amortized per connection, request, batch, foreground wait or
rebalance. Collection and OTLP encoding allocate per push. Empty and unmatched
subscriptions produce empty payloads; shutdown emits the final subscribed snapshot
before disabling recording.

### Client resource attributes

Each nonempty broker telemetry push includes applicable KIP-714 labels in the
OTLP `ResourceMetrics.resource.attributes` message. These apply to both built-in
and application metrics; data-point attributes such as `node_id` and application
labels retain their original scope.

| Attribute | Source |
| --- | --- |
| `client_rack` | Producer/consumer `ClientRack`, or share consumer `RackId`, when configured |
| `group_id` | Consumer or share consumer `GroupId` |
| `group_instance_id` | Ordinary consumer `GroupInstanceId`, when a group is configured |
| `group_member_id` | Current joined consumer/share member identity at collection time |
| `transactional_id` | Producer `TransactionalId`, when configured |

Absent and empty values are omitted. Member identity is sampled once per push;
joining, fenced, unjoined or disposed coordinators omit it. A later push reflects
a new member identity without changing earlier snapshots. Share consumers have no
static-member configuration, and Admin clients supply none of these attributes.
Collection and encoding costs are per push, with no added per-message work.

The receiving broker plugin supplies connection-derived labels such as
`client_instance_id`, `client_id`, software name/version, source address/port,
principal, and receiving `node_id`. Dekaf does not invent these labels or copy
credentials into resource attributes. A data-point `node_id` still identifies
the broker measured by that particular metric.

### Client instance IDs

Built-in producers, consumers, Share Consumers, and Admin clients implement the optional
`IKafkaClientInstanceIdentity` capability. Its `ClientInstanceId` property exposes the latest
broker-assigned KIP-714 identity without blocking or starting network I/O:

```csharp
using Dekaf.Diagnostics;

var identity = (IKafkaClientInstanceIdentity)producer;
Guid? clientInstanceId = identity.ClientInstanceId;
```

The property is `null` before telemetry negotiation succeeds, and remains `null` when
telemetry is unavailable or unsupported by the broker. Reads use an allocation-free cache.
Admin clients configured with `BootstrapControllers` do not start broker client telemetry,
so their client instance ID remains `null`.
After assignment, a refreshed subscription publishes its latest accepted identity atomically;
the last assigned value remains readable after disposal.

`IKafkaClientStatusProvider.GetStatus()` includes the same value in its immutable
`KafkaClientStatus.ClientInstanceId` snapshot. Status snapshots are intended for low-frequency
readiness and support diagnostics because snapshot construction allocates. Use the direct
identity property for frequent reads, and use the `Dekaf` OpenTelemetry meter above for
continuous client metrics.
