---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-07-03 20:28 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 1,527,361 | 1456.60 | 1,527,361 | 0 | 0.92 | 1.40 |
| Dekaf | 1,373,405 | 1309.78 | 1,373,405 | 0 | 0.91 | 1.25 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 1,428,449 | 1362.28 | 1,428,747 | 0 | 1.13 | 1.62 |
| Dekaf | 1,287,347 | 1227.71 | 1,287,347 | 0 | 1.20 | 1.54 |
| Confluent | 852,417 | 812.93 | 852,417 | 0 | 1.79 | 1.52 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.51x faster** than Confluent.Kafka for producer (fire-and-forget), 3 brokers throughput!
:::

## Producer (producer-acks-all) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,525,269 | 1454.61 | 1,525,269 | 0 | 0.83 | 1.27 |
| Confluent | 929,226 | 886.18 | 929,226 | 0 | 1.83 | 1.70 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.64x faster** than Confluent.Kafka for producer (producer-acks-all) throughput!
:::

## Producer (producer-acks-all), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,066,122 | 1016.73 | 1,066,122 | 0 | 1.05 | 1.12 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 1,557,044 | 1484.91 | 1,557,044 | 0 | 0.89 | 1.38 |
| Dekaf | 1,405,106 | 1340.01 | 1,405,106 | 0 | 0.86 | 1.20 |
| Confluent | 1,399,493 | 1334.66 | 1,399,493 | 0 | 1.25 | 1.74 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget, idempotent) performance.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 953,218 | 909.06 | 953,218 | 0 | 1.16 | 1.10 |
| Dekaf | 936,471 | 893.09 | 936,471 | 0 | 1.23 | 1.15 |
| Confluent | 761,924 | 726.63 | 761,924 | 0 | 2.13 | 1.62 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.23x faster** than Confluent.Kafka for producer (fire-and-forget, idempotent), 3 brokers throughput!
:::

## Consumer Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,906,177 | 1817.87 | - | 0 | 0.80 | 1.53 |
| Confluent | 104,545 | 99.70 | - | 0 | 0.92 | 0.10 |

:::tip
**Dekaf is 18.23x faster** than Confluent.Kafka for consumer throughput!
:::

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated |
|--------|----------|------|------|------|-----------------|
| Confluent | Consumer | 44589 | 0 | 0 | 213.85 GB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 196618 | 0 | 0 | 920.65 GB |
| Confluent | producer-acks-all | 208335 | 1 | 1 | 1003.56 GB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 319429 | 1 | 1 | 1511.49 GB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 175970 | 2 | 2 | 822.91 GB |
| Dekaf | Consumer | 81248 | 48075 | 2350 | 4026.25 GB |
| Dekaf | Producer (Fire-and-Forget) | 530 | 98 | 41 | 5.27 GB |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 282 | 49 | 19 | 3.60 GB |
| Dekaf | producer-acks-all | 728 | 253 | 49 | 5.84 GB |
| Dekaf | producer-acks-all, 3 Brokers | 244 | 13 | 12 | 2.51 GB |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 695 | 236 | 55 | 5.71 GB |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 197 | 36 | 13 | 2.47 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 259 | 57 | 27 | 7.34 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 492 | 198 | 195 | 7.29 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 253 | 14 | 13 | 2.80 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 137 | 20 | 11 | 1.98 GB |

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
