---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-07-05 03:36 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 1,612,901 | 1538.18 | 1,612,901 | 0 | 0.87 | 1.40 |
| Dekaf | 1,259,350 | 1201.01 | 1,259,350 | 0 | 1.07 | 1.35 |
| Confluent | 1,217,548 | 1161.14 | 1,217,548 | 0 | 1.42 | 1.73 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget) performance.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 1,453,236 | 1385.91 | 1,453,236 | 0 | 1.13 | 1.65 |
| Dekaf | 1,131,909 | 1079.47 | 1,131,909 | 0 | 1.36 | 1.54 |
| Confluent | 891,728 | 850.42 | 891,728 | 0 | 1.69 | 1.51 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.27x faster** than Confluent.Kafka for producer (fire-and-forget), 3 brokers throughput!
:::

## Producer (producer-acks-all) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,485,980 | 1417.14 | 1,485,980 | 0 | 0.85 | 1.27 |
| Confluent | 1,284,970 | 1225.44 | 1,284,970 | 0 | 1.32 | 1.70 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.16x faster** than Confluent.Kafka for producer (producer-acks-all) throughput!
:::

## Producer (producer-acks-all), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 992,779 | 946.79 | 992,779 | 0 | 1.13 | 1.12 |
| Confluent | 844,849 | 805.71 | 844,849 | 0 | 1.79 | 1.52 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.18x faster** than Confluent.Kafka for producer (producer-acks-all), 3 brokers throughput!
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 1,656,302 | 1579.57 | 1,656,302 | 0 | 0.85 | 1.40 |
| Confluent | 1,308,315 | 1247.71 | 1,308,315 | 0 | 1.34 | 1.75 |
| Dekaf | 1,296,995 | 1236.91 | 1,296,995 | 0 | 0.97 | 1.26 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget, idempotent) performance.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 1,096,069 | 1045.29 | 1,096,069 | 0 | 1.19 | 1.30 |
| Dekaf | 1,006,565 | 959.93 | 1,006,565 | 0 | 1.12 | 1.13 |
| Confluent | 834,848 | 796.17 | 834,848 | 0 | 1.87 | 1.56 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.21x faster** than Confluent.Kafka for producer (fire-and-forget, idempotent), 3 brokers throughput!
:::

## Consumer Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,750,454 | 1669.36 | - | 0 | 0.87 | 1.53 |
| Confluent | 1,155,476 | 1101.95 | - | 0 | 0.97 | 1.12 |

:::tip
**Dekaf is 1.51x faster** than Confluent.Kafka for consumer throughput!
:::

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated |
|--------|----------|------|------|------|-----------------|
| Confluent | Consumer | 490440 | 0 | 0 | 2363.43 GB |
| Confluent | Producer (Fire-and-Forget) | 277621 | 1 | 1 | 1314.99 GB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 205865 | 1 | 1 | 963.11 GB |
| Confluent | producer-acks-all | 296869 | 1 | 1 | 1387.82 GB |
| Confluent | producer-acks-all, 3 Brokers | 195256 | 1 | 1 | 912.47 GB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 298412 | 1 | 1 | 1413.03 GB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 193358 | 1 | 1 | 901.66 GB |
| Dekaf | Consumer | 75199 | 44808 | 2304 | 3497.49 GB |
| Dekaf | Producer (Fire-and-Forget) | 407 | 138 | 47 | 3.97 GB |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 251 | 20 | 18 | 3.43 GB |
| Dekaf | producer-acks-all | 620 | 132 | 46 | 5.67 GB |
| Dekaf | producer-acks-all, 3 Brokers | 238 | 72 | 11 | 2.39 GB |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 467 | 56 | 54 | 5.54 GB |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 199 | 63 | 12 | 2.50 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 279 | 13 | 12 | 2.66 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 449 | 170 | 165 | 9.00 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 244 | 12 | 11 | 2.51 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 229 | 26 | 12 | 2.70 GB |

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
