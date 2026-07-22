---
sidebar_position: 12
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
| `send {topic}` | `Producer` | Each `ProduceAsync` / `Send` |
| `process {topic}` | `Consumer` | Each message from streaming `ConsumeAsync` |
| `poll {topic}` | `Client` | Each message from `ConsumeOne` / `ConsumeOneAsync` |

The two consume flavors match the span's actual lifetime. In the streaming `ConsumeAsync` path the span stays current while your handler runs and is ended when the next record is requested — a `process` operation (`CONSUMER` kind) whose duration covers message handling; any spans your handler creates are parented under it. In the single-record `ConsumeOne` paths the span ends before the record is returned, covering only delivery and deserialization — a `receive` operation (`CLIENT` kind).

### Trace Context Propagation

Producer spans inject W3C `traceparent` (and `tracestate`) headers into the outgoing message. On the consumer side, the extracted producer context is attached as a **span link** rather than a parent — consumer spans start a new trace linked to the producing trace, per the OTel messaging conventions. Messages without a valid `traceparent` header produce an unlinked consumer span.

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
| `messaging.message.body.size` | ✓ (on delivery) | ✓ |
| `messaging.kafka.message.tombstone` | ✓ (null-value messages) | ✓ (tombstone records) |
| `messaging.consumer.group.name` | | ✓ |

`messaging.message.body.size` is the value payload only — the key is not part of the message body; tombstones report `0`.

Failures set the span status to `Error`, set `error.type` to the exception's fully-qualified type name, and record an `exception` event with `exception.type`, `exception.message`, and `exception.stacktrace`. Successful spans leave the status unset, per the OTel span-status guidance.

## Metrics

### Standard Messaging Metrics

These are the spec-defined instruments from the [OTel messaging metrics conventions](https://opentelemetry.io/docs/specs/semconv/messaging/messaging-metrics/):

| Instrument | Type | Unit | Description |
|------------|------|------|-------------|
| `messaging.client.sent.messages` | Counter | `{message}` | Messages published |
| `messaging.client.operation.duration` | Histogram | `s` | Produce operation duration (successes and failures) |
| `messaging.client.consumed.messages` | Counter | `{message}` | Messages received |

All three carry the spec-required `messaging.system = kafka` and `messaging.operation.name` (`send`/`poll`) tags plus `messaging.destination.name`. Failed produce operations record `messaging.client.operation.duration` with an additional `error.type` tag, per the spec's error model.

### Dekaf Internal Metrics

Dekaf-specific instruments live under the `dekaf.*` prefix — the `messaging.*` namespace is reserved for spec-defined instruments. These cover throughput detail beyond the spec metrics plus internal controller state, useful for diagnosing backpressure, buffer exhaustion, and adaptive-connection behavior. Per-broker instruments carry a `dekaf.broker.id` tag.

**Producer:**

| Instrument | Description |
|------------|-------------|
| `dekaf.producer.sent.bytes` | Bytes published (key + value) |
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
