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

Dekaf emits spans following the [OpenTelemetry messaging semantic conventions](https://opentelemetry.io/docs/specs/semconv/messaging/kafka/):

| Span | Kind | When |
|------|------|------|
| `{topic} publish` | `Producer` | Each `ProduceAsync` / `Send` |
| `{topic} receive` | `Consumer` | Each consumed message |

### Trace Context Propagation

Producer spans inject W3C `traceparent` (and `tracestate`) headers into the outgoing message. On the consumer side, the extracted producer context is attached as a **span link** rather than a parent — consumer spans start a new trace linked to the producing trace, per the OTel messaging conventions. Messages without a valid `traceparent` header produce an unlinked consumer span.

### Span Attributes

Both spans set `messaging.system = kafka` plus:

| Attribute | Publish | Receive |
|-----------|---------|---------|
| `messaging.destination.name` (topic) | ✓ | ✓ |
| `messaging.operation.type` | `publish` | `receive` |
| `messaging.client.id` | ✓ | ✓ |
| `messaging.kafka.message.key` | ✓ (string-convertible keys) | |
| `messaging.destination.partition.id` | | ✓ |
| `messaging.kafka.message.offset` | | ✓ |
| `messaging.message.body.size` | | ✓ |
| `messaging.consumer.group.name` | | ✓ |

Failures set the span status to `Error` and record an `exception` event with `exception.type`, `exception.message`, and `exception.stacktrace`.

## Metrics

### Standard Messaging Metrics

These follow the OTel messaging semantic conventions:

| Instrument | Type | Unit | Description |
|------------|------|------|-------------|
| `messaging.client.sent.messages` | Counter | `{message}` | Messages published |
| `messaging.client.sent.bytes` | Counter | `By` | Bytes published |
| `messaging.client.sent.errors` | Counter | `{error}` | Produce errors |
| `messaging.client.sent.retries` | Counter | `{retry}` | Produce retries |
| `messaging.client.operation.duration` | Histogram | `s` | Produce/consume operation duration |
| `messaging.client.consumed.messages` | Counter | `{message}` | Messages received |
| `messaging.client.consumed.bytes` | Counter | `By` | Bytes received |
| `messaging.consumer.lag` | ObservableGauge | `{message}` | High watermark minus consumed position, per partition |
| `messaging.consumer.rebalance.duration` | Histogram | `s` | Consumer group rebalance duration |
| `messaging.consumer.fetch.duration` | Histogram | `s` | Fetch request round-trip time |
| `messaging.consumer.batch.parse.errors` | Counter | `{error}` | Record batches that failed protocol parsing |

### Dekaf Internal Metrics

Dekaf-specific instruments under the `dekaf.*` prefix expose internal controller state — useful for diagnosing backpressure, buffer exhaustion, and adaptive-connection behavior:

**Producer:**

| Instrument | Description |
|------------|-------------|
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
| `dekaf.consumer.fetch_buffer.used_bytes` / `free_bytes` | Fetch response memory reserved vs available |
| `dekaf.consumer.fetch_buffer.depleted_percent` / `depleted_duration` | Time spent waiting for fetch response memory |

All observable gauges are registered per client instance and stop reporting when the client is disposed.

## Broker-Side Telemetry (KIP-714)

Independently of OpenTelemetry, Dekaf implements [KIP-714 client metrics push telemetry](https://cwiki.apache.org/confluence/display/KAFKA/KIP-714%3A+Client+metrics+and+observability). When a broker has a client-metrics subscription configured, Dekaf clients automatically push standard client metrics to the broker at the subscribed interval — no client configuration required.

Applications can also contribute their own metrics to broker subscriptions via `ProducerOptions.ApplicationMetrics` / `ConsumerOptions.ApplicationMetrics` with `ApplicationTelemetryMetric` (name, kind, and an observe callback).
