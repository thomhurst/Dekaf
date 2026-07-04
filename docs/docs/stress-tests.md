---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-07-04 05:20 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 1,649,514 | 1573.10 | 1,649,514 | 0 | 0.85 | 1.40 |
| Dekaf | 1,500,057 | 1430.57 | 1,500,057 | 0 | 0.88 | 1.32 |
| Confluent | 1,285,501 | 1225.95 | 1,285,501 | 0 | 1.32 | 1.70 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.17x faster** than Confluent.Kafka for producer (fire-and-forget) throughput!
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 1,477,286 | 1408.85 | 1,477,523 | 0 | 1.11 | 1.64 |
| Dekaf | 1,384,627 | 1320.48 | 1,384,627 | 0 | 1.09 | 1.51 |
| Confluent | 935,483 | 892.15 | 935,483 | 0 | 1.62 | 1.52 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.48x faster** than Confluent.Kafka for producer (fire-and-forget), 3 brokers throughput!
:::

## Producer (producer-acks-all) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,504,076 | 1434.40 | 1,504,076 | 0 | 0.84 | 1.27 |
| Confluent | 1,419,445 | 1353.69 | 1,419,445 | 0 | 1.25 | 1.77 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.06x faster** than Confluent.Kafka for producer (producer-acks-all) throughput!
:::

## Producer (producer-acks-all), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,025,468 | 977.96 | 1,025,468 | 0 | 1.07 | 1.09 |
| Confluent | 730,828 | 696.97 | 730,828 | 0 | 2.12 | 1.55 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.40x faster** than Confluent.Kafka for producer (producer-acks-all), 3 brokers throughput!
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 1,643,436 | 1567.30 | 1,643,436 | 0 | 0.86 | 1.41 |
| Dekaf | 1,441,248 | 1374.48 | 1,441,248 | 0 | 0.92 | 1.33 |
| Confluent | 1,436,025 | 1369.50 | 1,436,025 | 0 | 1.21 | 1.74 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget, idempotent) performance.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 1,136,148 | 1083.51 | 1,136,148 | 0 | 1.04 | 1.19 |
| Dekaf | 996,551 | 950.38 | 996,551 | 0 | 1.09 | 1.08 |
| Confluent | 747,397 | 712.77 | 747,397 | 0 | 2.14 | 1.60 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.33x faster** than Confluent.Kafka for producer (fire-and-forget, idempotent), 3 brokers throughput!
:::

## Consumer Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,564,451 | 1491.98 | - | 0 | 0.96 | 1.50 |
| Confluent | 1,017,023 | 969.91 | - | 0 | 1.08 | 1.10 |

:::tip
**Dekaf is 1.54x faster** than Confluent.Kafka for consumer throughput!
:::

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated |
|--------|----------|------|------|------|-----------------|
| Confluent | Consumer | 441080 | 3 | 3 | 2080.09 GB |
| Confluent | Producer (Fire-and-Forget) | 293409 | 1 | 1 | 1388.39 GB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 216517 | 0 | 0 | 1010.35 GB |
| Confluent | producer-acks-all | 323968 | 1 | 1 | 1533.03 GB |
| Confluent | producer-acks-all, 3 Brokers | 167244 | 0 | 0 | 789.30 GB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 332093 | 1 | 1 | 1550.89 GB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 171094 | 1 | 1 | 807.23 GB |
| Dekaf | Consumer | 68221 | 40419 | 2064 | 3137.49 GB |
| Dekaf | Producer (Fire-and-Forget) | 697 | 246 | 54 | 5.61 GB |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 272 | 18 | 15 | 3.08 GB |
| Dekaf | producer-acks-all | 718 | 253 | 45 | 5.69 GB |
| Dekaf | producer-acks-all, 3 Brokers | 238 | 61 | 13 | 2.53 GB |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 648 | 193 | 48 | 5.57 GB |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 200 | 48 | 14 | 2.52 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 342 | 12 | 11 | 2.41 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 519 | 226 | 220 | 12.49 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 311 | 14 | 13 | 2.69 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 259 | 45 | 11 | 2.42 GB |

---

## About These Tests

Stress tests measure sustained performance over extended periods:

- **Real Kafka**: Tests run against actual Apache Kafka instances
- **CPU Isolation**: Brokers are pinned to dedicated cores and the client under test to its own cores, so the client — not the broker — is the measured bottleneck
- **RAM-backed Broker Logs**: Kafka log dirs are mounted on tmpfs so disk I/O never caps broker ingestion
- **Delivered Throughput**: producer tables report broker-confirmed throughput, measured as the end-offset delta across all partitions — not the client-side append rate, which can run far ahead of what the broker ever accepts
- **Backpressure Parity**: both producers are bounded to the same 512 MB local buffer (Dekaf BufferMemory, librdkafka queue.buffering.max) and block on a full buffer, so neither client can absorb an unbounded backlog into RAM
- **Consumer Loop Replay**: Consumer tests re-read a pre-seeded topic (seek to beginning when drained) instead of racing a live feeder, so the consumer itself is measured
- **Delivery Latency Sampling**: 1 in 1000 produced messages is awaited end-to-end to record true broker round-trip latency
- **CPU Efficiency**: CPU time per message differentiates client efficiency even at equal throughput
- **Parallel Execution**: Each test runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load
