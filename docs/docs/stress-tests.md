---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-07-03 21:26 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 1,835,895 | 1750.85 | 1,835,895 | 0 | 0.79 | 1.44 |
| Dekaf | 1,514,385 | 1444.23 | 1,514,385 | 0 | 0.84 | 1.28 |
| Confluent | 1,450,376 | 1383.19 | 1,450,376 | 0 | 1.21 | 1.75 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget) performance.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 1,386,626 | 1322.39 | 1,387,498 | 0 | 1.19 | 1.65 |
| Dekaf | 1,334,390 | 1272.57 | 1,334,518 | 0 | 1.14 | 1.52 |
| Confluent | 942,319 | 898.67 | 942,319 | 0 | 1.60 | 1.51 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.42x faster** than Confluent.Kafka for producer (fire-and-forget), 3 brokers throughput!
:::

## Producer (producer-acks-all) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,371,658 | 1308.11 | 1,371,658 | 0 | 0.91 | 1.24 |
| Confluent | 1,229,989 | 1173.01 | 1,229,989 | 0 | 1.40 | 1.72 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.12x faster** than Confluent.Kafka for producer (producer-acks-all) throughput!
:::

## Producer (producer-acks-all), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 989,128 | 943.31 | 989,128 | 0 | 1.14 | 1.13 |
| Confluent | 875,672 | 835.11 | 875,672 | 0 | 1.76 | 1.54 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.13x faster** than Confluent.Kafka for producer (producer-acks-all), 3 brokers throughput!
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 1,632,542 | 1556.91 | 1,632,542 | 0 | 0.85 | 1.38 |
| Dekaf | 1,382,971 | 1318.90 | 1,382,972 | 0 | 0.90 | 1.24 |
| Confluent | 1,204,479 | 1148.68 | 1,204,479 | 0 | 1.43 | 1.72 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.15x faster** than Confluent.Kafka for producer (fire-and-forget, idempotent) throughput!
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 1,133,369 | 1080.86 | 1,133,369 | 0 | 1.17 | 1.32 |
| Dekaf | 970,293 | 925.34 | 970,293 | 0 | 1.16 | 1.13 |
| Confluent | 741,993 | 707.62 | 741,993 | 0 | 2.15 | 1.60 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.31x faster** than Confluent.Kafka for producer (fire-and-forget, idempotent), 3 brokers throughput!
:::

## Consumer Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,940,004 | 1850.13 | - | 0 | 0.78 | 1.51 |
| Confluent | 126,660 | 120.79 | - | 0 | 1.06 | 0.13 |

:::tip
**Dekaf is 15.32x faster** than Confluent.Kafka for consumer throughput!
:::

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated |
|--------|----------|------|------|------|-----------------|
| Confluent | Consumer | 53324 | 1 | 1 | 259.08 GB |
| Confluent | Producer (Fire-and-Forget) | 333837 | 1 | 1 | 1566.46 GB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 218283 | 1 | 1 | 1017.70 GB |
| Confluent | producer-acks-all | 281227 | 1 | 1 | 1328.42 GB |
| Confluent | producer-acks-all, 3 Brokers | 202779 | 1 | 0 | 945.77 GB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 266588 | 1 | 1 | 1300.88 GB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 168689 | 1 | 0 | 801.38 GB |
| Dekaf | Consumer | 83504 | 49609 | 2344 | 3822.21 GB |
| Dekaf | Producer (Fire-and-Forget) | 723 | 258 | 49 | 5.74 GB |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 286 | 15 | 14 | 2.92 GB |
| Dekaf | producer-acks-all | 641 | 50 | 48 | 5.35 GB |
| Dekaf | producer-acks-all, 3 Brokers | 192 | 59 | 11 | 2.49 GB |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 558 | 37 | 36 | 5.21 GB |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 211 | 12 | 11 | 2.51 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 353 | 86 | 47 | 16.70 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 482 | 164 | 160 | 8.11 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 327 | 14 | 13 | 2.73 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 193 | 13 | 12 | 2.67 GB |

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
