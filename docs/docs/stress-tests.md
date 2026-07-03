---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-07-03 11:50 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Confluent | 1,423,409 | 1357.47 | 1,423,409 | 0 | 1.24 | 1.76 |
| Dekaf | 1,290,525 | 1230.74 | 1,290,525 | 0 | 1.12 | 1.45 |
| Dekaf (3conn) | 1,077,047 | 1027.15 | 1,077,047 | 0 | 1.34 | 1.44 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::note
Confluent.Kafka is 1.10x faster for producer (fire-and-forget) throughput.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,176,051 | 1121.57 | 1,176,101 | 0 | 1.45 | 1.71 |
| Confluent | 634,646 | 605.25 | 634,646 | 0 | 2.41 | 1.53 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.85x faster** than Confluent.Kafka for producer (fire-and-forget), 3 brokers throughput!
:::

## Producer (producer-acks-all) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,154,942 | 1101.44 | 1,154,942 | 0 | 1.20 | 1.39 |
| Confluent | 1,085,510 | 1035.22 | 1,085,510 | 0 | 1.51 | 1.64 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.06x faster** than Confluent.Kafka for producer (producer-acks-all) throughput!
:::

## Producer (producer-acks-all), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,080,909 | 1030.84 | 1,080,909 | 0 | 1.24 | 1.34 |
| Confluent | 674,109 | 642.88 | 674,109 | 0 | 2.29 | 1.54 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.60x faster** than Confluent.Kafka for producer (producer-acks-all), 3 brokers throughput!
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Confluent | 1,422,974 | 1357.05 | 1,422,974 | 0 | 1.20 | 1.70 |
| Dekaf (3conn) | 1,289,361 | 1229.63 | 1,289,361 | 0 | 1.13 | 1.46 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 1,059,704 | 1010.61 | 1,059,704 | 0 | 1.52 | 1.61 |
| Confluent | 886,689 | 845.61 | 886,689 | 0 | 1.76 | 1.56 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Consumer Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,599,653 | 1525.55 | - | 0 | 0.94 | 1.51 |
| Confluent | 119,101 | 113.58 | - | 0 | 1.43 | 0.17 |

:::tip
**Dekaf is 13.43x faster** than Confluent.Kafka for consumer throughput!
:::

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated |
|--------|----------|------|------|------|-----------------|
| Confluent | Consumer | 49636 | 0 | 0 | 243.63 GB |
| Confluent | Producer (Fire-and-Forget) | 326417 | 1 | 1 | 1537.25 GB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 142948 | 0 | 0 | 685.44 GB |
| Confluent | producer-acks-all | 241755 | 1 | 1 | 1172.38 GB |
| Confluent | producer-acks-all, 3 Brokers | 152304 | 1 | 0 | 728.07 GB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 324317 | 1 | 1 | 1536.85 GB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 205382 | 1 | 1 | 957.66 GB |
| Dekaf | Consumer | 66522 | 38968 | 1557 | 3179.98 GB |
| Dekaf | Producer (Fire-and-Forget) | 484 | 145 | 15 | 6.15 GB |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 524 | 198 | 195 | 57.48 GB |
| Dekaf | producer-acks-all | 446 | 103 | 17 | 7.64 GB |
| Dekaf | producer-acks-all, 3 Brokers | 331 | 12 | 11 | 3.82 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 182 | 79 | 51 | 43.65 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 225 | 61 | 20 | 9.46 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 268 | 81 | 60 | 26.76 GB |

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
